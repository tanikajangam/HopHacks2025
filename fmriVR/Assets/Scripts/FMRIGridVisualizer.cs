using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FMRIGridVisualizer : MonoBehaviour
{
    [Header("FMRI Data Source")]
    public FMRILoader fmriLoader;
    public bool findLoaderAutomatically = true;
    
    [Header("Volume Settings")]
    public int currentTimePoint = 0;
    public float voxelSize = 1f;
    
    [Header("Grid Settings")]
    [Range(4, 16)]
    public int gridSizeX = 8;
    [Range(4, 16)]
    public int gridSizeY = 8;
    [Range(4, 16)]
    public int gridSizeZ = 8;
    
    [Header("Volume Rendering")]
    public Material volumeMaterial;
    public bool createMaterialIfMissing = true;
    public Shader volumeShader;
    
    [System.Serializable]
    public class ColorScheme
    {
        public string name = "Grayscale";
        public Color minColor = Color.black;
        public Color maxColor = Color.white;
        public bool useTransparency = true;
        [Range(0f, 1f)]
        public float minAlpha = 0.1f;
        [Range(0f, 1f)]
        public float maxAlpha = 0.8f;
    }
    
    public ColorScheme[] colorSchemes = new ColorScheme[]
    {
        new ColorScheme { name = "Grayscale", minColor = Color.black, maxColor = Color.white, useTransparency = true },
        new ColorScheme { name = "Hot", minColor = Color.black, maxColor = Color.red, useTransparency = true },
        new ColorScheme { name = "Cool", minColor = Color.blue, maxColor = Color.cyan, useTransparency = true },
        new ColorScheme { name = "Rainbow", minColor = Color.blue, maxColor = Color.red, useTransparency = true }
    };
    
    [Range(0, 3)]
    public int currentColorSchemeIndex = 0;
    
    [Header("Visualization Settings")]
    public bool useGlobalMinMax = true;
    [Range(0f, 1f)]
    public float customMinValue = 0f;
    [Range(0f, 1f)]
    public float customMaxValue = 1f;
    
    [Header("Shader Properties")]
    [Range(0.02f, 0.1f)]
    public float stepSize = 0.08f;
    [Range(10, 100)]
    public int maxSteps = 32;
    [Range(0.1f, 3.0f)]
    public float intensityScale = 1.5f;
    
    [Header("Performance")]
    public bool updateInRealTime = true;
    public float updateInterval = 0.1f;
    
    [Header("Debug Info")]
    public bool dataReady = false;
    public float globalMinValue = 0f;
    public float globalMaxValue = 0f;
    
    // Private variables
    private GameObject volumeObject;
    private Material volumeMaterialInstance;
    private MeshRenderer volumeRenderer;
    private float[] fmriDataArray;
    
    // Calculated render bounds  
    private int renderStartX, renderStartY, renderStartZ;
    private int renderEndX, renderEndY, renderEndZ;
    private int renderSizeX, renderSizeY, renderSizeZ;
    
    // Timing
    private float lastUpdateTime = 0f;

    void Start()
    {
        // Find FMRI loader if not assigned
        if (findLoaderAutomatically && fmriLoader == null)
        {
            fmriLoader = FindObjectOfType<FMRILoader>();
            if (fmriLoader == null)
            {
                Debug.LogError("FMRIGridVisualizer: No FMRILoader found in scene!");
                enabled = false;
                return;
            }
        }
        
        StartCoroutine(UpdateVisualization());
    }

    IEnumerator UpdateVisualization()
    {
        while (true)
        {
            if (fmriLoader != null && fmriLoader.dataLoaded)
            {
                if (!dataReady)
                {
                    yield return StartCoroutine(InitializeVisualization());
                }
                
                if (updateInRealTime && Time.time - lastUpdateTime >= updateInterval)
                {
                    yield return StartCoroutine(UpdateGridData());
                    lastUpdateTime = Time.time;
                }
            }
            else if (dataReady)
            {
                dataReady = false;
                DestroyVolumeObject();
            }
            
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    IEnumerator InitializeVisualization()
    {
        Debug.Log("Initializing FMRI grid visualization...");
        
        // Calculate render bounds based on full volume
        CalculateRenderBounds();
        
        // Calculate global min/max
        yield return StartCoroutine(CalculateGlobalMinMax());
        
        // Create volume object
        CreateVolumeObject();
        
        // Initialize data array
        InitializeDataArray();
        
        dataReady = true;
        
        // Update initial data
        yield return StartCoroutine(UpdateGridData());
        
        Debug.Log($"FMRI grid visualization ready! Grid size: {gridSizeX}x{gridSizeY}x{gridSizeZ}");
    }
    
    void CalculateRenderBounds()
    {
        // Use full volume for now - we'll downsample to grid size
        renderStartX = 0; renderStartY = 0; renderStartZ = 0;
        renderEndX = fmriLoader.xSize; renderEndY = fmriLoader.ySize; renderEndZ = fmriLoader.zSize;
        renderSizeX = renderEndX - renderStartX;
        renderSizeY = renderEndY - renderStartY;
        renderSizeZ = renderEndZ - renderStartZ;
        
        Debug.Log($"Source volume: {renderSizeX}x{renderSizeY}x{renderSizeZ}");
        Debug.Log($"Target grid: {gridSizeX}x{gridSizeY}x{gridSizeZ}");
    }
    
    IEnumerator CalculateGlobalMinMax()
    {
        Debug.Log("Calculating global min/max values...");
        if (fmriLoader == null || !fmriLoader.dataLoaded) yield break;
        
        globalMinValue = float.MaxValue;
        globalMaxValue = float.MinValue;
        int validValues = 0;
        
        for (int t = 0; t < fmriLoader.timePoints; t++)
        {
            for (int x = renderStartX; x < renderEndX; x++)
            {
                for (int y = renderStartY; y < renderEndY; y++)
                {
                    for (int z = renderStartZ; z < renderEndZ; z++)
                    {
                        float value = fmriLoader.GetVoxelValue(t, x, y, z);
                        
                        if (!float.IsNaN(value) && !float.IsInfinity(value))
                        {
                            if (value < globalMinValue) globalMinValue = value;
                            if (value > globalMaxValue) globalMaxValue = value;
                            validValues++;
                        }
                    }
                }
            }
            
            if (t % 10 == 0) yield return null;
        }
        
        Debug.Log($"Global range: {globalMinValue:F6} to {globalMaxValue:F6} ({validValues} valid values)");
    }
    
    void CreateVolumeObject()
    {
        DestroyVolumeObject();
        
        volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volumeObject.name = "FMRI_Grid_Volume";
        volumeObject.transform.parent = transform;
        volumeObject.transform.localPosition = Vector3.zero;
        
        // Scale based on original data dimensions
        Vector3 scale = new Vector3(renderSizeX, renderSizeY, renderSizeZ) * voxelSize * 0.1f; // Scale down for VR
        volumeObject.transform.localScale = scale;
        
        volumeRenderer = volumeObject.GetComponent<MeshRenderer>();
        
        if (volumeMaterial == null && createMaterialIfMissing)
        {
            if (volumeShader == null)
                volumeShader = Shader.Find("Custom/FMRIGridRenderer");
            
            if (volumeShader != null)
            {
                volumeMaterial = new Material(volumeShader);
                volumeMaterial.name = "FMRI_Grid_Material";
            }
            else
            {
                Debug.LogError("Grid volume shader not found! Please assign volumeShader or volumeMaterial.");
                return;
            }
        }
        
        volumeMaterialInstance = new Material(volumeMaterial);
        volumeRenderer.material = volumeMaterialInstance;
        
        Debug.Log("Grid volume object created");
    }
    
    void InitializeDataArray()
    {
        int totalGridVoxels = gridSizeX * gridSizeY * gridSizeZ;
        fmriDataArray = new float[totalGridVoxels];
        
        // Set grid dimensions in shader
        volumeMaterialInstance.SetInt("_GridSizeX", gridSizeX);
        volumeMaterialInstance.SetInt("_GridSizeY", gridSizeY);
        volumeMaterialInstance.SetInt("_GridSizeZ", gridSizeZ);
        
        Debug.Log($"Initialized data array for {totalGridVoxels} voxels");
    }
    
    IEnumerator UpdateGridData()
    {
        if (!dataReady || fmriDataArray == null) yield break;
        
        currentTimePoint = Mathf.Clamp(currentTimePoint, 0, fmriLoader.timePoints - 1);
        
        Debug.Log($"Updating grid data for time point {currentTimePoint}");
        
        // Get normalization parameters
        float minVal = useGlobalMinMax ? globalMinValue : customMinValue;
        float maxVal = useGlobalMinMax ? globalMaxValue : customMaxValue;
        float range = maxVal - minVal;
        
        int index = 0;
        int validValues = 0;
        float maxNormalizedValue = 0f;
        
        // Downsample fMRI data to grid resolution
        for (int gz = 0; gz < gridSizeZ; gz++)
        {
            for (int gy = 0; gy < gridSizeY; gy++)
            {
                for (int gx = 0; gx < gridSizeX; gx++)
                {
                    // Map grid coordinates to original fMRI coordinates
                    int sourceX = Mathf.FloorToInt(((float)gx / gridSizeX) * renderSizeX);
                    int sourceY = Mathf.FloorToInt(((float)gy / gridSizeY) * renderSizeY);
                    int sourceZ = Mathf.FloorToInt(((float)gz / gridSizeZ) * renderSizeZ);
                    
                    // Sample multiple voxels and average them (simple downsampling)
                    float sum = 0f;
                    int sampleCount = 0;
                    int sampleRadius = 1; // Sample neighboring voxels
                    
                    for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
                    {
                        for (int dy = -sampleRadius; dy <= sampleRadius; dy++)
                        {
                            for (int dz = -sampleRadius; dz <= sampleRadius; dz++)
                            {
                                int sx = Mathf.Clamp(sourceX + dx, 0, renderSizeX - 1);
                                int sy = Mathf.Clamp(sourceY + dy, 0, renderSizeY - 1);
                                int sz = Mathf.Clamp(sourceZ + dz, 0, renderSizeZ - 1);
                                
                                float value = fmriLoader.GetVoxelValue(currentTimePoint, sx, sy, sz);
                                
                                if (!float.IsNaN(value) && !float.IsInfinity(value))
                                {
                                    sum += value;
                                    sampleCount++;
                                }
                            }
                        }
                    }
                    
                    // Average the samples
                    float averageValue = sampleCount > 0 ? sum / sampleCount : 0f;
                    
                    // Normalize to 0-1 range
                    float normalizedValue = 0f;
                    if (range > 0.000001f)
                    {
                        normalizedValue = Mathf.Clamp01((averageValue - minVal) / range);
                    }
                    
                    fmriDataArray[index] = normalizedValue;
                    
                    if (normalizedValue > 0.001f)
                    {
                        validValues++;
                        maxNormalizedValue = Mathf.Max(maxNormalizedValue, normalizedValue);
                    }
                    
                    index++;
                }
            }
            
            // Yield every few layers
            if (gz % 2 == 0) yield return null;
        }
        
        Debug.Log($"Grid data updated: {validValues}/{fmriDataArray.Length} valid voxels, max value: {maxNormalizedValue:F3}");
        
        // Pass data to shader
        volumeMaterialInstance.SetFloatArray("_FMRIData", fmriDataArray);
        
        // Update shader properties
        UpdateShaderProperties();
    }
    
    void UpdateShaderProperties()
    {
        if (volumeMaterialInstance == null) return;
        
        ColorScheme scheme = colorSchemes[Mathf.Clamp(currentColorSchemeIndex, 0, colorSchemes.Length - 1)];
        
        volumeMaterialInstance.SetFloat("_StepSize", stepSize);
        volumeMaterialInstance.SetInt("_MaxSteps", maxSteps);
        volumeMaterialInstance.SetFloat("_IntensityScale", intensityScale);
        volumeMaterialInstance.SetColor("_MinColor", scheme.minColor);
        volumeMaterialInstance.SetColor("_MaxColor", scheme.maxColor);
        volumeMaterialInstance.SetFloat("_MinAlpha", scheme.minAlpha);
        volumeMaterialInstance.SetFloat("_MaxAlpha", scheme.maxAlpha);
        volumeMaterialInstance.SetFloat("_UseTransparency", scheme.useTransparency ? 1.0f : 0.0f);
    }
    
    void DestroyVolumeObject()
    {
        if (volumeObject != null)
        {
            DestroyImmediate(volumeObject);
            volumeObject = null;
        }
        
        if (volumeMaterialInstance != null)
        {
            DestroyImmediate(volumeMaterialInstance);
            volumeMaterialInstance = null;
        }
        
        volumeRenderer = null;
    }
    
    // Public methods
    [ContextMenu("Update Grid Now")]
    public void UpdateGridNow()
    {
        if (dataReady)
        {
            StartCoroutine(UpdateGridData());
        }
    }
    
    public void SetTimePoint(int timePoint)
    {
        int newTimePoint = Mathf.Clamp(timePoint, 0, fmriLoader != null ? fmriLoader.timePoints - 1 : 0);
        if (newTimePoint != currentTimePoint)
        {
            currentTimePoint = newTimePoint;
            if (dataReady)
            {
                StartCoroutine(UpdateGridData());
            }
        }
    }
    
    public void SetColorScheme(int schemeIndex)
    {
        currentColorSchemeIndex = Mathf.Clamp(schemeIndex, 0, colorSchemes.Length - 1);
        if (dataReady)
        {
            UpdateShaderProperties();
        }
    }
    
    void OnDestroy()
    {
        DestroyVolumeObject();
    }
    
    void OnValidate()
    {
        // Ensure grid size doesn't exceed shader limits
        int totalVoxels = gridSizeX * gridSizeY * gridSizeZ;
        if (totalVoxels > 512)
        {
            Debug.LogWarning($"Grid size too large ({totalVoxels} voxels). Maximum is 512 for Quest 2 compatibility.");
            
            // Automatically adjust to fit
            float scaleFactor = Mathf.Pow(512f / totalVoxels, 1f/3f);
            gridSizeX = Mathf.Max(4, Mathf.FloorToInt(gridSizeX * scaleFactor));
            gridSizeY = Mathf.Max(4, Mathf.FloorToInt(gridSizeY * scaleFactor));
            gridSizeZ = Mathf.Max(4, Mathf.FloorToInt(gridSizeZ * scaleFactor));
        }
        
        if (dataReady && Application.isPlaying)
        {
            UpdateShaderProperties();
        }
    }
}