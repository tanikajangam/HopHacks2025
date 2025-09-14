using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Optimized_FMRIVolumeVisualizer : MonoBehaviour
{
    [Header("FMRI Data Source")]
    public FMRILoader fmriLoader;
    public bool findLoaderAutomatically = true;
    
    [Header("Volume Settings")]
    public int currentTimePoint = 0;
    public float voxelSize = 1f; // Now controls overall volume scale
    
    [Header("Volume Rendering")]
    public Material volumeMaterial; // Assign the FMRIVolumeRenderer material
    public bool createMaterialIfMissing = true;
    public Shader volumeShader; // Reference to the volume shader
    
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
    [Range(0.01f, 0.2f)]
    public float stepSize = 0.05f;
    [Range(10, 200)]
    public int maxSteps = 50;
    [Range(0.1f, 50.0f)]
    public float intensityScale = 1.0f;
    
    [Header("Performance")]
    public bool updateInRealTime = true;
    public float updateInterval = 0.1f;
    
    [Header("Animation")]
    public bool animateTimePoints = false;
    public float animationSpeed = 1f;
    
    [Header("Debug Info")]
    public bool dataReady = false;
    public float globalMinValue = 0f;
    public float globalMaxValue = 0f;
    
    [Header("Volume Subset")]
    public int xOffset = 0;
    public int yOffset = 0; 
    public int zOffset = 0;
    public int chunkSizeX = 20;
    public int chunkSizeY = 20;
    public int chunkSizeZ = 20;
    public bool useFullVolume = true;
    
    // Private variables for volume rendering
    private GameObject volumeObject;
    private Texture3D volumeTexture;
    private Material volumeMaterialInstance;
    private MeshRenderer volumeRenderer;
    
    // Calculated render bounds
    private int renderStartX, renderStartY, renderStartZ;
    private int renderEndX, renderEndY, renderEndZ;
    private int renderSizeX, renderSizeY, renderSizeZ;
    
    // Timing
    private float lastUpdateTime = 0f;
    private float animationTimer = 0f;

    void Start()
    {
        // Find FMRI loader if not assigned
        if (findLoaderAutomatically && fmriLoader == null)
        {
            fmriLoader = FindObjectOfType<FMRILoader>();
            if (fmriLoader == null)
            {
                Debug.LogError("FMRIVolumeVisualizer: No FMRILoader found in scene!");
                enabled = false;
                return;
            }
        }
        
        // Start the main update coroutine
        StartCoroutine(UpdateVisualization());
    }

    IEnumerator UpdateVisualization()
    {
        while (true)
        {
            // Wait for FMRI data to load
            if (fmriLoader != null && fmriLoader.dataLoaded)
            {
                if (!dataReady)
                {
                    // First time data is ready
                    yield return StartCoroutine(InitializeVisualization());
                }
                
                // Handle animation
                if (animateTimePoints && fmriLoader.timePoints > 1)
                {
                    animationTimer += Time.deltaTime * animationSpeed;
                    int newTimePoint = Mathf.FloorToInt(animationTimer) % fmriLoader.timePoints;
                    if (newTimePoint != currentTimePoint)
                    {
                        currentTimePoint = newTimePoint;
                        yield return StartCoroutine(UpdateVolumeTexture());
                    }
                }
                
                // Update visualization if enough time has passed
                if (updateInRealTime && Time.time - lastUpdateTime >= updateInterval)
                {
                    UpdateShaderProperties();
                    lastUpdateTime = Time.time;
                }
            }
            else if (dataReady)
            {
                // Data was ready but now isn't - cleanup
                dataReady = false;
                DestroyVolumeObject();
            }
            
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    IEnumerator InitializeVisualization()
    {
        Debug.Log("Initializing FMRI volume visualization...");
        
        // Calculate render bounds
        CalculateRenderBounds();
        
        // Calculate global min/max for the subset
        yield return StartCoroutine(CalculateGlobalMinMax());
        
        // Create the volume rendering object
        CreateVolumeObject();
        
        // Create the 3D texture
        yield return StartCoroutine(CreateVolumeTexture());
        
        // IMPORTANT: Set dataReady BEFORE calling UpdateVolumeTexture
        dataReady = true;
        
        // Now update the texture (this will now work because dataReady = true)
        yield return StartCoroutine(UpdateVolumeTexture());
        
        // Set initial shader properties
        UpdateShaderProperties();
        
        Debug.Log($"FMRI volume visualization ready! Volume size: {renderSizeX}x{renderSizeY}x{renderSizeZ}");
    }
    
    void CalculateRenderBounds()
    {
        if (useFullVolume)
        {
            renderStartX = 0; renderStartY = 0; renderStartZ = 0;
            renderEndX = fmriLoader.xSize; renderEndY = fmriLoader.ySize; renderEndZ = fmriLoader.zSize;
        }
        else
        {
            renderStartX = Mathf.Clamp(xOffset, 0, fmriLoader.xSize);
            renderStartY = Mathf.Clamp(yOffset, 0, fmriLoader.ySize);
            renderStartZ = Mathf.Clamp(zOffset, 0, fmriLoader.zSize);
            renderEndX = Mathf.Clamp(renderStartX + chunkSizeX, renderStartX, fmriLoader.xSize);
            renderEndY = Mathf.Clamp(renderStartY + chunkSizeY, renderStartY, fmriLoader.ySize);
            renderEndZ = Mathf.Clamp(renderStartZ + chunkSizeZ, renderStartZ, fmriLoader.zSize);
        }
        
        renderSizeX = renderEndX - renderStartX;
        renderSizeY = renderEndY - renderStartY;
        renderSizeZ = renderEndZ - renderStartZ;
        
        Debug.Log($"Render bounds: X[{renderStartX}-{renderEndX-1}] Y[{renderStartY}-{renderEndY-1}] Z[{renderStartZ}-{renderEndZ-1}]");
    }
    
    IEnumerator CalculateGlobalMinMax()
    {
        Debug.Log("Calculating global min/max values for subset...");
        if (fmriLoader == null || !fmriLoader.dataLoaded) yield break;
        
        globalMinValue = float.MaxValue;
        globalMaxValue = float.MinValue;
        
        int validValues = 0;
        
        // Calculate min/max for the render subset across all time points
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
            
            // Yield every time point to prevent freezing
            yield return null;
        }
        
        Debug.Log($"Global range: {globalMinValue:F6} to {globalMaxValue:F6} ({validValues} valid values)");
    }
    
    void CreateVolumeObject()
    {
        // Clean up existing volume object
        DestroyVolumeObject();
        
        // Create a cube primitive for volume rendering
        volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volumeObject.name = "FMRI_Volume";
        volumeObject.transform.parent = transform;
        volumeObject.transform.localPosition = Vector3.zero;
        
        // Scale the volume object based on the actual data dimensions and voxel size
        Vector3 scale = new Vector3(renderSizeX, renderSizeY, renderSizeZ) * voxelSize;
        volumeObject.transform.localScale = scale;
        
        // Get or create the volume material
        volumeRenderer = volumeObject.GetComponent<MeshRenderer>();
        
        if (volumeMaterial == null && createMaterialIfMissing)
        {
            // Try to find the shader
            if (volumeShader == null)
                volumeShader = Shader.Find("Custom/FMRIVolumeRenderer");
            
            if (volumeShader != null)
            {
                volumeMaterial = new Material(volumeShader);
                volumeMaterial.name = "FMRI_Volume_Material";
            }
            else
            {
                Debug.LogError("Volume shader not found! Please assign volumeShader or volumeMaterial.");
                return;
            }
        }
        
        // Create instance of material
        volumeMaterialInstance = new Material(volumeMaterial);
        volumeRenderer.material = volumeMaterialInstance;
        
        Debug.Log("Volume object created");
    }
    
    IEnumerator CreateVolumeTexture()
    {
        if (volumeTexture != null)
        {
            DestroyImmediate(volumeTexture);
        }
        
        // Create 3D texture
        volumeTexture = new Texture3D(renderSizeX, renderSizeY, renderSizeZ, TextureFormat.RFloat, false);
        volumeTexture.name = "FMRI_Volume_Texture";
        volumeTexture.filterMode = FilterMode.Trilinear;
        volumeTexture.wrapMode = TextureWrapMode.Clamp;
        
        Debug.Log($"Created 3D texture: {renderSizeX}x{renderSizeY}x{renderSizeZ}");
        yield return null;
    }
    
    IEnumerator UpdateVolumeTexture()
    {
        if (volumeTexture == null || !dataReady) yield break;
        
        currentTimePoint = Mathf.Clamp(currentTimePoint, 0, fmriLoader.timePoints - 1);
        
        // Create color array for the texture
        Color[] colors = new Color[renderSizeX * renderSizeY * renderSizeZ];
        int index = 0;
        
        // Get normalization parameters
        float minVal = useGlobalMinMax ? globalMinValue : customMinValue;
        float maxVal = useGlobalMinMax ? globalMaxValue : customMaxValue;
        float range = maxVal - minVal;
        
        // DEBUG: Track actual data statistics
        float actualMin = float.MaxValue;
        float actualMax = float.MinValue;
        float sum = 0f;
        int validCount = 0;
        List<float> sampleValues = new List<float>();
        List<float> normalizedSamples = new List<float>();
        
        Debug.Log($"=== FMRI Data Debug - Time Point {currentTimePoint} ===");
        Debug.Log($"Normalization range: {minVal:F6} to {maxVal:F6} (span: {range:F6})");
        
        // Fill texture with current time point data
        for (int z = 0; z < renderSizeZ; z++)
        {
            for (int y = 0; y < renderSizeY; y++)
            {
                for (int x = 0; x < renderSizeX; x++)
                {
                    // Get voxel value (convert from relative to absolute coordinates)
                    float rawValue = fmriLoader.GetVoxelValue(currentTimePoint, 
                        x + renderStartX, y + renderStartY, z + renderStartZ);
                    
                    // DEBUG: Collect raw statistics
                    if (!float.IsNaN(rawValue) && !float.IsInfinity(rawValue))
                    {
                        actualMin = Mathf.Min(actualMin, rawValue);
                        actualMax = Mathf.Max(actualMax, rawValue);
                        sum += rawValue;
                        validCount++;
                        
                        if (sampleValues.Count < 20)
                            sampleValues.Add(rawValue);
                    }
                    
                    // NORMALIZE the value to 0-1 range before storing in texture
                    float normalizedValue = 0f;
                    if (range > 0.000001f && !float.IsNaN(rawValue) && !float.IsInfinity(rawValue))
                    {
                        normalizedValue = Mathf.Clamp01((rawValue - minVal) / range);
                    }
                    
                    // Collect normalized samples for debugging
                    if (normalizedSamples.Count < 20)
                        normalizedSamples.Add(normalizedValue);
                    
                    // Store NORMALIZED value in red channel (shader expects 0-1 range)
                    colors[index] = new Color(normalizedValue, 0, 0, 1);
                    index++;
                }
            }
            
            // Yield periodically to prevent freezing
            if (z % 2 == 0) yield return null;
        }
        
        // DEBUG: Print comprehensive statistics
        Debug.Log($"Raw data range this timepoint: {actualMin:F6} to {actualMax:F6}");
        Debug.Log($"Global range (all timepoints): {globalMinValue:F6} to {globalMaxValue:F6}");
        Debug.Log($"Average raw value: {(sum / validCount):F6}");
        Debug.Log($"Valid voxel count: {validCount} / {colors.Length}");
        
        // Print raw sample values
        string rawSampleStr = "Raw samples: ";
        foreach (float val in sampleValues)
            rawSampleStr += $"{val:F1}, ";
        Debug.Log(rawSampleStr);
        
        // Print normalized sample values  
        string normSampleStr = "Normalized samples: ";
        foreach (float val in normalizedSamples)
            normSampleStr += $"{val:F3}, ";
        Debug.Log(normSampleStr);
        
        // Check for proper normalization - calculate min/max manually
        float normMin = 1f;
        float normMax = 0f;
        if (normalizedSamples.Count > 0)
        {
            foreach (float val in normalizedSamples)
            {
                if (val < normMin) normMin = val;
                if (val > normMax) normMax = val;
            }
        }
        else
        {
            normMin = 0f;
            normMax = 0f;
        }
        
        Debug.Log($"Normalized range in texture: {normMin:F3} to {normMax:F3}");
        
        if (normMax - normMin < 0.01f)
        {
            Debug.LogWarning("Very little variation in normalized data - check if global min/max calculation is correct!");
        }
        
        // Apply the color data to the texture
        volumeTexture.SetPixels(colors);
        volumeTexture.Apply();
        
        // Set texture on material
        if (volumeMaterialInstance != null)
        {
            volumeMaterialInstance.SetTexture("_VolumeTexture", volumeTexture);
        }
        
        Debug.Log($"Updated volume texture for time point {currentTimePoint}");
    }
    
    
    void UpdateShaderProperties()
    {
        if (volumeMaterialInstance == null) return;
        
        // Get current color scheme
        ColorScheme scheme = colorSchemes[Mathf.Clamp(currentColorSchemeIndex, 0, colorSchemes.Length - 1)];
        
        // IMPORTANT: Since we're now normalizing data in UpdateVolumeTexture, 
        // the shader should always work with 0-1 range
        float shaderMinValue = 0.0f;  // Always 0 since we normalize the texture data
        float shaderMaxValue = 1.0f;  // Always 1 since we normalize the texture data
        
        //Debug.Log($"Setting shader properties - Texture data is normalized to 0-1 range");
        
        // Update shader properties
        volumeMaterialInstance.SetFloat("_StepSize", stepSize);
        volumeMaterialInstance.SetInt("_MaxSteps", maxSteps);
        volumeMaterialInstance.SetFloat("_IntensityScale", intensityScale);
        volumeMaterialInstance.SetFloat("_MinValue", shaderMinValue);  // Always 0
        volumeMaterialInstance.SetFloat("_MaxValue", shaderMaxValue);  // Always 1
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
        
        if (volumeTexture != null)
        {
            DestroyImmediate(volumeTexture);
            volumeTexture = null;
        }
        
        volumeRenderer = null;
    }
    
    // Public methods to maintain compatibility with existing code
    [ContextMenu("Update Volume Now")]
    public void UpdateVolumeNow()
    {
        if (dataReady)
        {
            StartCoroutine(UpdateVolumeTexture());
        }
    }
    
    [ContextMenu("Recalculate Min/Max")]
    public void RecalculateMinMax()
    {
        if (dataReady)
        {
            StartCoroutine(CalculateGlobalMinMax());
        }
    }
    
    [ContextMenu("Recreate Visualization")]
    public void RecreateVisualization()
    {
        if (fmriLoader != null && fmriLoader.dataLoaded)
        {
            dataReady = false;
            StartCoroutine(InitializeVisualization());
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
                StartCoroutine(UpdateVolumeTexture());
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
    
    public void ToggleAnimation()
    {
        animateTimePoints = !animateTimePoints;
        animationTimer = currentTimePoint;
    }
    
    public void UpdateSubsetSettings()
    {
        if (fmriLoader != null && fmriLoader.dataLoaded)
        {
            dataReady = false;
            StartCoroutine(InitializeVisualization());
        }
    }
    
    void OnDestroy()
    {
        DestroyVolumeObject();
    }
    
    void OnValidate()
    {
        if (fmriLoader != null && dataReady)
        {
            currentTimePoint = Mathf.Clamp(currentTimePoint, 0, Mathf.Max(0, fmriLoader.timePoints - 1));
        }
        
        currentColorSchemeIndex = Mathf.Clamp(currentColorSchemeIndex, 0, Mathf.Max(0, colorSchemes.Length - 1));
        
        if (customMaxValue < customMinValue)
        {
            customMaxValue = customMinValue + 0.1f;
        }
        
        // Update shader properties in real-time during inspector changes
        if (dataReady && Application.isPlaying)
        {
            UpdateShaderProperties();
        }
        
        // Clamp subset parameters
        if (fmriLoader != null)
        {
            xOffset = Mathf.Clamp(xOffset, 0, fmriLoader.xSize - 1);
            yOffset = Mathf.Clamp(yOffset, 0, fmriLoader.ySize - 1);
            zOffset = Mathf.Clamp(zOffset, 0, fmriLoader.zSize - 1);
            
            chunkSizeX = Mathf.Clamp(chunkSizeX, 1, fmriLoader.xSize - xOffset);
            chunkSizeY = Mathf.Clamp(chunkSizeY, 1, fmriLoader.ySize - yOffset);
            chunkSizeZ = Mathf.Clamp(chunkSizeZ, 1, fmriLoader.zSize - zOffset);
        }
    }
}