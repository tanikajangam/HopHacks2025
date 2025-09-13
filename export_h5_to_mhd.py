
#!/usr/bin/env python3
# export_h5_to_mhd.py
# Minimal-dependency HDF5 -> MetaImage sequence exporter for fMRI time series.
# Works with Python 3.9+; depends only on h5py & numpy.

import argparse, json, os, sys
from pathlib import Path
import numpy as np
import h5py

EPS = 1e-6

# ---------------- Utils ----------------

def find_4d_dataset(h: h5py.File, prefer_name_substrings=("bold", "timeseries", "data")):
    candidates = []
    def visit(name, obj):
        if isinstance(obj, h5py.Dataset) and obj.ndim == 4:
            candidates.append((name, obj))
    h.visititems(lambda n, o: visit(n, o))
    if not candidates:
        raise RuntimeError("No 4D dataset found in HDF5 file.")

    # Prefer datasets whose name contains known substrings
    ranked = sorted(
        candidates,
        key=lambda t: (
            -int(any(s in t[0].lower() for s in prefer_name_substrings)),
            -np.prod(t[1].shape)
        )
    )
    name, ds = ranked[0]
    return name, ds


def block_reduce_mean(vol, factor):
    """Downsample (Z,Y,X) by integer factor using mean pooling; fallback to stride if not divisible."""
    z, y, x = vol.shape
    f = factor
    if f <= 1:
        return vol
    if (z % f == 0) and (y % f == 0) and (x % f == 0):
        vol = vol.reshape(z//f, f, y//f, f, x//f, f).mean(axis=(1,3,5))
        return vol
    # fallback: stride sampling (fast but not averaging)
    return vol[::f, ::f, ::f]


def write_mhd_raw(out_dir: Path, frame_idx: int, vol_zyx_u8: np.ndarray):
    """Write one frame as .mhd + .raw. Input vol is (Z,Y,X) uint8."""
    assert vol_zyx_u8.dtype == np.uint8 and vol_zyx_u8.ndim == 3
    Z, Y, X = vol_zyx_u8.shape
    # MHD expects X Y Z order (x fastest). Reorder to (X,Y,Z) for C-ordered dump.
    vol_xyz = np.ascontiguousarray(vol_zyx_u8.transpose(2,1,0))

    raw_name = f"frame_{frame_idx:04d}.raw"
    mhd_name = f"frame_{frame_idx:04d}.mhd"

    with open(out_dir / raw_name, "wb") as f:
        f.write(vol_xyz.tobytes(order="C"))

    header = (
        "ObjectType = Image\n"
        "NDims = 3\n"
        f"DimSize = {X} {Y} {Z}\n"
        "ElementType = MET_UCHAR\n"
        "ElementSpacing = 1 1 1\n"
        "ElementByteOrderMSB = False\n"
        f"ElementDataFile = {raw_name}\n"
    )

    with open(out_dir / mhd_name, "w") as f:
            f.write(header + "\n")


# ---------------- Export logic ----------------

def main():
    p = argparse.ArgumentParser(description="Export 4D fMRI HDF5 to MetaImage frames for Unity.")
    p.add_argument("h5_path", help="Path to .h5 file")
    p.add_argument("--outdir", default="unity_export_psc", help="Output directory (will be created)")
    p.add_argument("--dataset", default=None, help="Path to 4D dataset inside HDF5 (default: auto-detect)")
    p.add_argument("--time_axis", type=int, default=0, help="Which axis is time (default: 0)")
    p.add_argument("--mode", choices=["raw","psc"], default="psc", help="Export intensities or PSC")
    p.add_argument("--baseline", choices=["firstN"], default="firstN", help="PSC baseline strategy")
    p.add_argument("--baselineN", type=int, default=10, help="Frames for baseline if baseline=firstN")
    p.add_argument("--clamp", default="-5,5", help="PSC clamp range as 'min,max' (e.g., '-5,5')")
    p.add_argument("--downsample", type=int, default=2, help="Integer spatial downsample (default 2)")
    p.add_argument("--dtype", choices=["u8"], default="u8", help="Output dtype (only u8 supported here)")
    p.add_argument("--tr", type=float, default=None, help="TR seconds (optional; saved to manifest)")

    args = p.parse_args()

    h5_path = Path(args.h5_path)
    out_dir = Path(args.outdir)
    out_dir.mkdir(parents=True, exist_ok=True)

    with h5py.File(h5_path, "r") as h:
        if args.dataset is None:
            ds_name, ds = find_4d_dataset(h)
        else:
            ds_name = args.dataset
            ds = h[ds_name]

        shape = tuple(int(s) for s in ds.shape)
        print(f"Using dataset '{ds_name}' with shape {shape}")

        t_axis = int(args.time_axis)
        if t_axis < 0 or t_axis > 3:
            raise ValueError("time_axis must be 0..3")

        # Move time axis to axis 0, spatial axes to (Z,Y,X) afterwards
        # We prefer spatial order (Z,Y,X) internally.
        perm = [t_axis] + [i for i in range(4) if i != t_axis]
        data = np.asarray(ds, dtype=np.float32).transpose(perm)
        T, A, B, C = data.shape

        # Heuristic to map (A,B,C) -> (Z,Y,X): assume X and Y are similar (~64-128), Z is smaller (~30-60)
        spatial = np.array([A,B,C])
        z_idx = int(np.argmin(spatial))  # often Z has the smallest extent
        order_map = [z_idx] + [i for i in range(3) if i != z_idx]
        Z, Y, X = spatial[order_map]

        print(f"Interpreted spatial dims as Z,Y,X = {Z},{Y},{X}")

        # Reorder to (T,Z,Y,X)
        data = data.transpose([0, 1+order_map[0], 1+order_map[1], 1+order_map[2]])

        # Downsample spatially
        if args.downsample and args.downsample > 1:
            f = int(args.downsample)
            ds_vols = np.empty((T, 0, 0, 0), dtype=np.float32)  # placeholder for shape introspection
            downs = []
            for t in range(T):
                vol = data[t]
                vol = block_reduce_mean(vol, f)
                downs.append(vol)
            data = np.stack(downs, axis=0)
            T, Z, Y, X = data.shape
            print(f"Downsampled to (T,Z,Y,X) = {data.shape}")

        # Normalize to uint8 per mode
        if args.mode == "psc":
            N = int(args.baselineN)
            if N <= 0 or N > T:
                raise ValueError("baselineN must be in 1..T")
            baseline = data[:N].mean(axis=0, keepdims=True)
            baseline = np.where(np.abs(baseline) < EPS, EPS, baseline)
            psc = 100.0 * (data - baseline) / baseline  # percent signal change
            cmin, cmax = [float(x) for x in args.clamp.split(",")]
            psc = np.clip(psc, cmin, cmax)
            # map [-|cmin|, |cmax|] → [0,1], center at 0.5
            vmin, vmax = cmin, cmax
            norm = (psc - vmin) / max(vmax - vmin, EPS)
            u8 = np.clip(norm * 255.0, 0, 255).astype(np.uint8)
            out_mode = "psc"
            clamp_tuple = [cmin, cmax]
        else:
            # raw: global min/max (robust percentiles to avoid outliers)
            lo = float(np.percentile(data, 1))
            hi = float(np.percentile(data, 99))
            data = np.clip(data, lo, hi)
            norm = (data - lo) / max(hi - lo, EPS)
            u8 = np.clip(norm * 255.0, 0, 255).astype(np.uint8)
            out_mode = "raw"
            clamp_tuple = [lo, hi]

        # Write frames
        for t in range(T):
            write_mhd_raw(out_dir, t, u8[t])  # (Z,Y,X) u8
            if t % 10 == 0:
                print(f"Wrote frame {t+1}/{T}")

        # Manifest (ensure pure Python types for json)
        manifest = {
            "n_frames": int(T),
            "dims": [int(X), int(Y), int(Z)],
            "dtype": "uint8",
            "mode": out_mode,
            "clamp": [float(clamp_tuple[0]), float(clamp_tuple[1])],
            "downsample": int(args.downsample),
            "tr_seconds": float(args.tr) if args.tr is not None else None,
        }
        with open(out_dir / "manifest.json", "w") as f:
            json.dump(manifest, f, indent=2)
        print(f"Done. Wrote {T} frames to {out_dir}")

if __name__ == "__main__":
    main()
