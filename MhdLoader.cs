// Assets/Scripts/MhdLoader.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class MhdLoader
{
    public struct MhdInfo
    {
        public int X, Y, Z;
        public string ElementType;  // e.g., MET_UCHAR
        public string RawFile;      // relative file name
    }

    public static Texture3D Load(string mhdPath)
    {
        if (!File.Exists(mhdPath)) throw new FileNotFoundException(mhdPath);

        var lines = File.ReadAllLines(mhdPath);
        var info = ParseHeader(lines);

        var dir = Path.GetDirectoryName(mhdPath) ?? "";
        var rawPath = Path.Combine(dir, info.RawFile);
        if (!File.Exists(rawPath)) throw new FileNotFoundException(rawPath);

        TextureFormat fmt;
        int bytesPerVoxel;
        switch (info.ElementType)
        {
            case "MET_UCHAR": fmt = TextureFormat.R8; bytesPerVoxel = 1; break;
            case "MET_USHORT": fmt = TextureFormat.R16; bytesPerVoxel = 2; break;
            default: throw new NotSupportedException($"Unsupported ElementType: {info.ElementType}");
        }

        int voxelCount = info.X * info.Y * info.Z;
        byte[] raw = File.ReadAllBytes(rawPath);
        if (raw.Length != voxelCount * bytesPerVoxel)
            throw new InvalidDataException($"Raw length {raw.Length} != expected {voxelCount * bytesPerVoxel}");

        var tex = new Texture3D(info.X, info.Y, info.Z, fmt, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear, // or Bilinear if you prefer
        };

        if (fmt == TextureFormat.R8)
        {
            tex.SetPixelData(raw, 0, 0);
        }
        else // R16
        {
            var ush = new ushort[raw.Length / 2];
            Buffer.BlockCopy(raw, 0, ush, 0, raw.Length);
            tex.SetPixelData(ush, 0, 0);
        }

        tex.Apply(false, false);
        return tex;
    }

    private static MhdInfo ParseHeader(string[] lines)
    {
        var dict = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var ln in lines)
        {
            var line = ln.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            var idx = line.IndexOf('=');
            if (idx < 0) continue;
            var key = line.Substring(0, idx).Trim();
            var val = line.Substring(idx + 1).Trim();
            dict[key] = val;
        }

        string dim = Require(dict, "DimSize");
        var parts = dim.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) throw new InvalidDataException("DimSize must have 3 ints");
        int x = int.Parse(parts[0], CultureInfo.InvariantCulture);
        int y = int.Parse(parts[1], CultureInfo.InvariantCulture);
        int z = int.Parse(parts[2], CultureInfo.InvariantCulture);

        return new MhdInfo
        {
            X = x,
            Y = y,
            Z = z,
            ElementType = Require(dict, "ElementType"),
            RawFile = Require(dict, "ElementDataFile"),
        };
    }

    private static string Require(Dictionary<string, string> d, string k)
    {
        if (!d.TryGetValue(k, out var v)) throw new KeyNotFoundException($"Missing '{k}' in .mhd");
        return v;
    }
}
