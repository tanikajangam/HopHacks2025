using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class VolumePlayer : MonoBehaviour
{
    [Header("Folder (under StreamingAssets)")]
    public string framesFolder = "fmri/psc_mhd"; // where the .mhd/.raw live

    [Header("Target")]
    public Renderer targetRenderer;    // the cube/mesh using the volume shader

    [Header("Playback")]
    public float trSeconds = 2.0f;     // seconds per frame
    public float speed = 1.0f;         // 1.0 = realtime
    public bool autoPlay = true;
    [Range(0, 1f)] public float scrub = 0f; // manual control when not autoplay
    public bool preloadAll = true;     // load all frames into memory (simpler)

    [Header("Coloring")] public bool usePSCColors = true; // set shader mode

    // Use explicit constructors for Unity's C# version compatibility
    private List<string> _mhdPaths = new List<string>();
    private List<Texture3D> _frames = new List<Texture3D>();
    private Material _mat;
    private int _curIndex = 0;
    private float _tAccum = 0f;

    private void OnValidate()
    {
        trSeconds = Mathf.Max(1e-3f, trSeconds);
        speed = Mathf.Max(0f, speed);
    }

    void Start()
    {
        // If you need a log, do it here:
        // Debug.Log("VolumePlayer Start()");

        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        if (!targetRenderer) throw new Exception("Assign a Renderer with the volume material.");
        _mat = Application.isPlaying ? targetRenderer.material : targetRenderer.sharedMaterial;
        if (!_mat) throw new Exception("Target Renderer has no material.");

        string root = Path.Combine(Application.streamingAssetsPath, framesFolder);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Folder not found: {root}");
        _mhdPaths = Directory.GetFiles(root, "*.mhd", SearchOption.TopDirectoryOnly)
                             .OrderBy(p => p)
                             .ToList();
        if (_mhdPaths.Count == 0) throw new Exception($"No .mhd files in {root}");

        // Try to read TR from manifest.json
        string manifest = Path.Combine(root, "manifest.json");
        if (File.Exists(manifest))
        {
            try
            {
                var j = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifest).Replace("\n", " "));
                if (j != null && j.tr_seconds > 0) trSeconds = j.tr_seconds;
            }
            catch { }
        }

        if (preloadAll)
        {
            foreach (var mhd in _mhdPaths)
                _frames.Add(MhdLoader.Load(mhd));
        }
        else
        {
            // Lazy mode: load the first frame now
            _frames.Add(MhdLoader.Load(_mhdPaths[0]));
        }

        ApplyFrame(0);
    }

    void Update()
    {
        if (_frames.Count == 0) return;
        _mat.SetFloat("_UsePSC", usePSCColors ? 1f : 0f);

        if (autoPlay && Application.isPlaying)
        {
            _tAccum += Time.deltaTime * Mathf.Max(speed, 0f);
            int frame = Mathf.FloorToInt(_tAccum / Mathf.Max(trSeconds, 1e-3f));
            frame = frame % _mhdPaths.Count;
            if (frame != _curIndex) ApplyFrame(frame);
        }
        else
        {
            int frame = Mathf.Clamp(Mathf.FloorToInt(scrub * (_mhdPaths.Count - 1)), 0, _mhdPaths.Count - 1);
            if (frame != _curIndex) ApplyFrame(frame);
        }
    }

    void ApplyFrame(int idx)
    {
        _curIndex = idx;
        if (preloadAll)
        {
            _mat.SetTexture("_VolumeTex", _frames[idx]);
        }
        else
        {
            var tex = MhdLoader.Load(_mhdPaths[idx]);
            _mat.SetTexture("_VolumeTex", tex);
        }
    }

    [Serializable]
    private class Manifest { public int n_frames; public float tr_seconds; }
}
