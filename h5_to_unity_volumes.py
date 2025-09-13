#!/usr/bin/env python3
# h5_to_unity_volumes.py
import os, json, argparse, math
import numpy as np
import h5py

def find_4d_dataset(h5):
    best = None
    best_size = -1
    def walk(name, obj):
        nonlocal best, best_size
        if isinstance(obj, h5py.Dataset) and obj.ndim == 4:
            size = np.prod(obj.shape)
            if size > best_size and np.issubdtype(obj.dtype, np.number):
                best, best_size = obj, size
    h5.visititems(walk)
    if best is None:
        raise RuntimeError("No 4D numeric dataset found (need T×X×Y×Z).")
    return best

def percentile_window(a, lo=2.0, hi=98.0):
    lo_v = np.percentile(a, lo)
    hi_v = np.percentile(a, hi)
    if hi_v <= lo_v:
        hi_v = lo_v + 1e-6
    return lo_v, hi_v

def to_uint8(volume, vmin, vmax):
    v = np.clip((volume - vmin) / (vmax - vmin), 0, 1)
    return (v * 255.0 + 0.5).astype(np.uint8)

def crop_and_center(vol4d, thresh=0.05):
    """
    vol4d: (T, X, Y, Z) in [0,1] float
    1) Build a mask from the mean volume > thresh
    2) Compute bounding box, crop
    3) Symmetrically pad to center the brain in the crop box
    Returns cropped+centered vol4d and crop/pad info.
    """
    mean_vol = vol4d.mean(axis=0)  # (X,Y,Z)
    mask = mean_vol > thresh
    if not mask.any():
        # nothing crosses threshold -> do nothing
        return vol4d, dict(cropped=False)
    coords = np.argwhere(mask)
    (x0,y0,z0) = coords.min(axis=0)
    (x1,y1,z1) = coords.max(axis=0) + 1
    vol4d = vol4d[:, x0:x1, y0:y1, z0:z1]
    # Center by symmetric padding to closest cube-ish shape? We’ll only center, not force cube.
    X, Y, Z = vol4d.shape[1:]
    # Find COM and pad so COM sits near center (approximate by equal padding each side)
    # Simpler: just pad to equalize dimensions parity and ensure even margins.
    # Here we’ll just pad 2 voxels each side as a small margin.
    pad = [(0,0), (2,2), (2,2), (2,2)]
    vol4d = np.pad(vol4d, pad_width=pad, mode='constant', constant_values=0.0)
    info = dict(cropped=True, x0=int(x0),y0=int(y0),z0=int(z0),x1=int(x1),y1=int(y1),z1=int(z1),pad=2)
    return vol4d, info

def main():
    ap = argparse.ArgumentParser(description="Export fMRI HDF5 (T×X×Y×Z) to Unity .vol frames + manifest.json")
    ap.add_argument("h5_path", help="HDF5 file (e.g., sub-10159_task-bart_bold_timeseries.h5)")
    ap.add_argument("--outdir", required=True, help="Output folder (will be created)")
    ap.add_argument("--baseline", choices=["firstN","mean"], default="firstN", help="Baseline for PSC")
    ap.add_argument("--baselineN", type=int, default=10, help="N frames for baseline if baseline=firstN")
    ap.add_argument("--psc_range", type=float, default=5.0, help="+/- PSC range mapped to colors (percent)")
    ap.add_argument("--crop_center", action="store_true", help="Auto-crop to brain & center")
    ap.add_argument("--downsample", type=int, default=1, help="Integer downsample factor (1=no downsample)")
    ap.add_argument("--tr", type=float, default=None, help="Override TR seconds (if unknown)")
    ap.add_argument("--no_psc", action="store_true", help="Skip PSC export (raw/mean anatomy only)")
    args = ap.parse_args()

    os.makedirs(args.outdir, exist_ok=True)

    with h5py.File(args.h5_path, "r") as h5:
        ds = find_4d_dataset(h5)
        data = ds[()]  # shape unknown order; expect (T,X,Y,Z) or (X,Y,Z,T). We’ll normalize to (T,X,Y,Z).
        shp = data.shape
        # Heuristics: if first dim is smallest (<10?) assume it's Z or X; most BOLD have T ~100-300
        # Typical: (T,X,Y,Z) has T ~ 150-300, X,Y,Z ~ 64-ish.
        if shp[0] < 16 and shp[-1] > 32:
            # assume (X,Y,Z,T) -> roll last to first
            data = np.moveaxis(data, -1, 0)  # (T,X,Y,Z)
        elif shp[0] >= 16 and shp[-1] < 16:
            # assume already (T,X,Y,Z)
            pass
        else:
            # choose the axis with largest size as T
            t_axis = int(np.argmax(shp))
            if t_axis != 0:
                data = np.moveaxis(data, t_axis, 0)

        T, X, Y, Z = data.shape
        # optional integer downsample
        if args.downsample > 1:
            f = args.downsample
            data = data[:, ::f, ::f, ::f]
            T, X, Y, Z = data.shape

        # TR guess
        tr = args.tr
        # Try to pull TR from attributes if present
        for obj in [ds, h5]:
            if tr is None and hasattr(obj, "attrs"):
                for key in obj.attrs.keys():
                    k = key.decode() if isinstance(key, bytes) else key
                    if k.lower() in ("tr","repetition_time","pixdim4"):
                        try:
                            val = obj.attrs[key]
                            if np.ndim(val) == 0:
                                tr = float(val)
                            else:
                                tr = float(val[0])
                        except Exception:
                            pass
        if tr is None:
            tr = 2.0  # safe default

        # Robust intensity window on the whole timeseries
        lo, hi = percentile_window(data, 2.0, 98.0)
        data_norm = (data - lo) / (hi - lo)
        data_norm = np.clip(data_norm, 0.0, 1.0).astype(np.float32)

        # (Optional) crop + center
        if args.crop_center:
            data_norm, cropinfo = crop_and_center(data_norm, thresh=0.05)
            T, X, Y, Z = data_norm.shape
        else:
            cropinfo = dict(cropped=False)

        # Mean anatomy (time-mean)
        mean_vol = data_norm.mean(axis=0)  # (X,Y,Z)

        # Write anatomy (time-mean) as 8-bit .vol
        anatomy_u8 = to_uint8(mean_vol, 0.0, 1.0)
        anatomy_path = os.path.join(args.outdir, "anatomy_mean.vol")
        anatomy_u8.tofile(anatomy_path)

        frames_meta = []
        psc_frames = []

        # PSC
        if not args.no_psc:
            if args.baseline == "firstN":
                N = max(1, min(args.baselineN, T))
                baseline = data_norm[:N].mean(axis=0)
            else:
                baseline = data_norm.mean(axis=0)

            # Avoid div-by-zero
            eps = 1e-6
            # PSC percent
            psc = 100.0 * (data_norm - baseline[None, ...]) / (baseline[None, ...] + eps)  # (T,X,Y,Z)

            # Map PSC to [0,1] by +/- psc_range
            pr = float(args.psc_range)
            psc01 = np.clip((psc + pr) / (2*pr), 0.0, 1.0).astype(np.float32)

            # Write PSC frames as uint8
            for t in range(T):
                fname = f"psc_{t:04d}.vol"
                path = os.path.join(args.outdir, fname)
                to_uint8(psc01[t], 0.0, 1.0).tofile(path)
                psc_frames.append(dict(file=fname, t=t))

        # Also (optional) write raw frames (normalized) if you want to scrub them instead of mean anatomy
        # Keeping this OFF by default to save time/space; enable if needed.
        # for t in range(T):
        #     fname = f"raw_{t:04d}.vol"
        #     path = os.path.join(args.outdir, fname)
        #     to_uint8(data_norm[t], 0.0, 1.0).tofile(path)
        #     frames_meta.append(dict(file=fname, t=t))

        manifest = {
            "dims": {"x": int(X), "y": int(Y), "z": int(Z)},
            "voxel_size_mm": [1.0, 1.0, 1.0],  # update if you know spacing
            "timepoints": int(T),
            "tr_seconds": float(tr),
            "format": "R8",
            "byte_order": "x_fastest",  # contiguous [x,y,z] with x changing fastest
            "anatomy": {"file": os.path.basename(anatomy_path), "desc": "time-mean of normalized BOLD [0..255]"},
            "psc": {"enabled": (not args.no_psc), "range_percent": float(args.psc_range), "frames": psc_frames},
            "raw_frames_included": False,
            "cropinfo": cropinfo,
        }
        with open(os.path.join(args.outdir, "manifest.json"), "w") as f:
            json.dump(manifest, f, indent=2)
    print("Export complete.")

if __name__ == "__main__":
    main()
