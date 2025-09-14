using System.Collections;
using UnityEngine;

public class Quest2FMRIVisualizer : MonoBehaviour
{
    [Header("FMRI Data Source")]
    public FMRILoader fmriLoader;
    public bool findLoaderAutomatically = true;
    
    [Header("Volume Settings")]
    public int currentTimePoint = 0;
    public float voxelSize = 1f;
    
    [Header("Atlas Settings")]
    [Range(4, 16)]
    public int gridSize = 8; // Creates 8x8x8 volume
    
    [Header("Volume Rendering")]
    public Material volumeMaterial;
    public bool createMaterialIfMissing = true;
    public Shader volumeShader;
    
    [Header("Color Schemes")]
    public Color minColor = Color.black;
    public Color maxColor = Color.white;
    [Range(0f, 1f)]
    public float minAlpha = 0.1f;
    [Range(0f, 1f)]
    public float maxAlpha = 0.8f;
    
    [Header("Visualization Settings")]
    public bool useGlobalMinMax = true;
    [Range(0f, 1f)]
    public float customMinValue = 0f;
    [Range(0f, 1f)]
    public float customMaxValue = 1f;
    
    [Header("Shader Properties - Quest 2 Optimized")]
    [Range(0.05f, 0.2f)]
    public float stepSize = 0.1f;        // Larger steps for performance
    [Range(8, 64)]
    public int maxSteps = 24;            // Fewer steps for performance
    [Range(0.5f, 4.0f)]
    public float intensityScale = 2.0f;  // Higher intensity for visibility
    
    [Header("Debug Info")]
    public bool dataReady = false;
    public float globalMinValue = 0f;
    public float globalMaxValue = 0f;
    
    // Private variables
    private GameObject volumeObject;
    private Material volumeMaterialInstance;
    private MeshRenderer volumeRenderer;
    private Texture2D atlasTexture;
    
    // Calculated render bounds
    private int renderStartX, renderStartY, renderStartZ;
    private int renderEndX, renderEndY, renderEndZ;
    private int renderSizeX, renderSizeY, renderSizeZ;

    void Start()
    {
        if (findLoaderAutomatically && fmriLoader == null)
        {
            fmriLoader = FindObjectOfType<FMRILoader>();
            if (fmriLoader == null)
            {
                Debug.LogError("Quest2FMRIVisualizer: No FMRILoader found in scene!");
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
            }
            else if (dataReady)
            {
                dataReady = false;
                DestroyVolumeObject();
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    IEnumerator InitializeVisualization()
    {
        Debug.Log("Initializing Quest 2 FMRI atlas visualization...");
        
        CalculateRenderBounds();
        yield return StartCoroutine(CalculateGlobalMinMax());
        CreateVolumeObject();
        yield return StartCoroutine(CreateAtlasTexture());
        
        dataReady = true;
        
        Debug.Log($"Quest 2 FMRI visualization ready! Grid: {gridSize}x{gridSize}x{gridSize}");
    }
    
    void CalculateRenderBounds()
    {
        renderStartX = 0; renderStartY = 0; renderStartZ = 0;
        renderEndX = fmriLoader.xSize; renderEndY = fmriLoader.ySize; renderEndZ = fmriLoader.zSize;
        renderSizeX = renderEndX - renderStartX;
        renderSizeY = renderEndY - renderStartY;
        renderSizeZ = renderEndZ - renderStartZ;
        
        Debug.Log($"Source: {renderSizeX}x{renderSizeY}x{renderSizeZ} -> Target: {gridSize}x{gridSize}x{gridSize}");
    }
    
    IEnumerator CalculateGlobalMinMax()
    {
        Debug.Log("Calculating global min/max...");
        if (fmriLoader == null || !fmriLoader.dataLoaded) yield break;
        
        globalMinValue = float.MaxValue;
        globalMaxValue = float.MinValue;
        int validValues = 0;
        
        for (int t = 0; t < fmriLoader.timePoints; t++)
        {
            for (int x = renderStartX; x < renderEndX; x += 2) // Sample every 2nd voxel for speed
            {
                for (int y = renderStartY; y < renderEndY; y += 2)
                {
                    for (int z = renderStartZ; z < renderEndZ; z += 2)
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
            
            if (t % 20 == 0) yield return null;
        }
        
        Debug.Log($"Global range: {globalMinValue:F3} to {globalMaxValue:F3} ({validValues} samples)");
    }
    
    void CreateVolumeObject()
    {
        DestroyVolumeObject();
        
        volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volumeObject.name = "Quest2_FMRI_Volume";
        volumeObject.transform.parent = transform;
        volumeObject.transform.localPosition = Vector3.zero;
        
        Vector3 scale = new Vector3(renderSizeX, renderSizeY, renderSizeZ) * voxelSize * 0.05f; // Scale for VR
        volumeObject.transform.localScale = scale;
        
        volumeRenderer = volumeObject.GetComponent<MeshRenderer>();
        
        if (volumeMaterial == null && createMaterialIfMissing)
        {
            if (volumeShader == null)
                volumeShader = Shader.Find("Custom/Quest2VolumeAtlas");
            
            if (volumeShader != null)
            {
                volumeMaterial = new Material(volumeShader);
                volumeMaterial.name = "Quest2_FMRI_Material";
            }
            else
            {
                Debug.LogError("Quest2VolumeAtlas shader not found!");
                return;
            }
        }
        
        volumeMaterialInstance = new Material(volumeMaterial);
        volumeRenderer.material = volumeMaterialInstance;
        
        Debug.Log("Quest 2 volume object created");
    }
    
    IEnumerator CreateAtlasTexture()
    {
        Debug.Log("Creating 2D atlas texture...");
        
        // Create atlas texture: gridSize slices arranged horizontally
        int atlasWidth = gridSize * gridSize;  // All Z slices side by side
        int atlasHeight = gridSize;
        
        if (atlasTexture != null)
        {
            DestroyImmediate(atlasTexture);
        }
        
        // Use simple RGBA32 format - guaranteed to work on Quest 2
        atlasTexture = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        atlasTexture.name = "FMRI_Atlas";
        atlasTexture.filterMode = FilterMode.Bilinear;
        atlasTexture.wrapMode = TextureWrapMode.Clamp;
        
        Color[] atlasPixels = new Color[atlasWidth * atlasHeight];
        
        // Get normalization parameters
        float minVal = useGlobalMinMax ? globalMinValue : customMinValue;
        float maxVal = useGlobalMinMax ? globalMaxValue : customMaxValue;
        float range = maxVal - minVal;
        
        currentTimePoint = Mathf.Clamp(currentTimePoint, 0, fmriLoader.timePoints - 1);
        
        Debug.Log($"Filling atlas for time point {currentTimePoint}...");
        
        int validVoxels = 0;
        float maxNormalized = 0f;
        
        // Fill atlas: each Z slice becomes a vertical strip
        for (int sliceZ = 0; sliceZ < gridSize; sliceZ++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    // Map grid coordinates to original fMRI coordinates
                    int sourceX = Mathf.FloorToInt(((float)x / gridSize) * renderSizeX);
                    int sourceY = Mathf.FloorToInt(((float)y / gridSize) * renderSizeY);
                    int sourceZ = Mathf.FloorToInt(((float)sliceZ / gridSize) * renderSizeZ);
                    
                    // Sample and average surrounding voxels
                    float sum = 0f;
                    int sampleCount = 0;
                    
                    for (int dx = 0; dx < 2; dx++)
                    {
                        for (int dy = 0; dy < 2; dy++)
                        {
                            for (int dz = 0; dz < 2; dz++)
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
                    
                    float averageValue = sampleCount > 0 ? sum / sampleCount : 0f;
                    
                    // Normalize to 0-1 range
                    float normalizedValue = 0f;
                    if (range > 0.000001f)
                    {
                        normalizedValue = Mathf.Clamp01((averageValue - minVal) / range);
                    }
                    
                    if (normalizedValue > 0.01f)
                    {
                        validVoxels++;
                        maxNormalized = Mathf.Max(maxNormalized, normalizedValue);
                    }
                    
                    // Calculate atlas pixel position
                    int atlasX = sliceZ * gridSize + x;
                    int atlasY = y;
                    int pixelIndex = atlasY * atlasWidth + atlasX;
                    
                    // Store in red channel, alpha = 1 for visibility
                    atlasPixels[pixelIndex] = new Color(normalizedValue, 0, 0, 1);
                }
            }
            
            if (sliceZ % 2 == 0) yield return null;
        }
        
        Debug.Log($"Atlas filled: {validVoxels}/{atlasPixels.Length} valid voxels, max: {maxNormalized:F3}");
        
        // Apply pixels to texture
        atlasTexture.SetPixels(atlasPixels);
        atlasTexture.Apply();
        
        // Assign to material
        volumeMaterialInstance.SetTexture("_VolumeAtlas", atlasTexture);
        volumeMaterialInstance.SetVector("_AtlasSize", new Vector4(gridSize, gridSize, gridSize, 0));
        
        UpdateShaderProperties();
        
        Debug.Log($"Atlas texture created: {atlasWidth}x{atlasHeight}");
    }
    
    void UpdateShaderProperties()
    {
        if (volumeMaterialInstance == null) return;
        
        volumeMaterialInstance.SetFloat("_StepSize", stepSize);
        volumeMaterialInstance.SetInt("_MaxSteps", maxSteps);
        volumeMaterialInstance.SetFloat("_IntensityScale", intensityScale);
        volumeMaterialInstance.SetColor("_MinColor", minColor);
        volumeMaterialInstance.SetColor("_MaxColor", maxColor);
        volumeMaterialInstance.SetFloat("_MinAlpha", minAlpha);
        volumeMaterialInstance.SetFloat("_MaxAlpha", maxAlpha);
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
        
        if (atlasTexture != null)
        {
            DestroyImmediate(atlasTexture);
            atlasTexture = null;
        }
        
        volumeRenderer = null;
    }
    
    [ContextMenu("Update Atlas Now")]
    public void UpdateAtlasNow()
    {
        if (dataReady)
        {
            StartCoroutine(CreateAtlasTexture());
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
                StartCoroutine(CreateAtlasTexture());
            }
        }
    }
    
    void OnDestroy()
    {
        DestroyVolumeObject();
    }
    
    void OnValidate()
    {
        // Ensure grid size stays reasonable for Quest 2
        if (gridSize > 12)
        {
            Debug.LogWarning("Grid size > 12 may cause performance issues on Quest 2");
        }
        
        if (dataReady && Application.isPlaying)
        {
            UpdateShaderProperties();
        }
    }
}