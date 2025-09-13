using System.Collections;
using UnityEngine;

public class FMRICubeVisualizer : MonoBehaviour
{
    [Header("FMRI Data Source")]
    public FMRILoader fmriLoader;
    public bool findLoaderAutomatically = true;
    
    [Header("Voxel Selection")]
    public int currentTimePoint = 0;
    public int currentX = 0;
    public int currentY = 0; 
    public int currentZ = 0;
    
    [Header("Visualization Settings")]
    public bool useGlobalMinMax = true;
    [Range(0f, 1f)]
    public float customMinValue = 0f;
    [Range(0f, 1f)]
    public float customMaxValue = 1f;
    
    [Header("Color Settings")]
    public Color minColor = Color.black;
    public Color maxColor = Color.white;
    public bool updateInRealTime = true;
    public float updateInterval = 0.1f; // seconds between updates
    
    [Header("Animation")]
    public bool animateTimePoints = false;
    public float animationSpeed = 1f; // time points per second
    
    [Header("Debug Info")]
    public float currentVoxelValue = 0f;
    public float globalMinValue = 0f;
    public float globalMaxValue = 0f;
    public Color currentColor = Color.gray;
    public bool dataReady = false;
    
    // Private variables
    private Renderer cubeRenderer;
    private Material cubeMaterial;
    private float lastUpdateTime = 0f;
    private float animationTimer = 0f;
    
    void Start()
    {
        // Get the cube's renderer
        cubeRenderer = GetComponent<Renderer>();
        if (cubeRenderer == null)
        {
            Debug.LogError("FMRICubeVisualizer: No Renderer found on this GameObject!");
            enabled = false;
            return;
        }
        
        // Create a new material instance so we don't affect other objects
        cubeMaterial = new Material(cubeRenderer.material);
        cubeRenderer.material = cubeMaterial;
        
        // Find FMRI loader if not assigned
        if (findLoaderAutomatically && fmriLoader == null)
        {
            fmriLoader = FindObjectOfType<FMRILoader>();
            if (fmriLoader == null)
            {
                Debug.LogError("FMRICubeVisualizer: No FMRILoader found in scene!");
                enabled = false;
                return;
            }
        }
        
        // Start the update coroutine
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
                    // First time data is ready - calculate global min/max
                    CalculateGlobalMinMax();
                    dataReady = true;
                    Debug.Log($"FMRI data ready! Global range: {globalMinValue:F3} to {globalMaxValue:F3}");
                }
                
                // Handle animation
                if (animateTimePoints && fmriLoader.timePoints > 1)
                {
                    animationTimer += Time.deltaTime * animationSpeed;
                    currentTimePoint = Mathf.FloorToInt(animationTimer) % fmriLoader.timePoints;
                }
                
                // Update visualization if enough time has passed
                if (Time.time - lastUpdateTime >= updateInterval || !updateInRealTime)
                {
                    UpdateCubeColor();
                    lastUpdateTime = Time.time;
                }
            }
            else if (dataReady)
            {
                // Data was ready but now isn't - reset
                dataReady = false;
                SetCubeColor(Color.gray);
            }
            
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    void CalculateGlobalMinMax()
    {
        Debug.Log("Calculating global min/max values...");
        if (fmriLoader == null || !fmriLoader.dataLoaded) return;
        
        globalMinValue = float.MaxValue;
        globalMaxValue = float.MinValue;
        
        // Sample approach - check every 10th voxel for performance
        // int step = Mathf.Max(1, Mathf.FloorToInt(fmriLoader.timePoints * fmriLoader.xSize * fmriLoader.ySize * fmriLoader.zSize / 100000));
        int step = 1;

        Debug.Log($"Shape: {fmriLoader.timePoints}x{fmriLoader.xSize}x{fmriLoader.ySize}x{fmriLoader.zSize}, Sampling every {step} voxels");

        for (int t = 0; t < fmriLoader.timePoints; t += step)
        {
            for (int x = 0; x < fmriLoader.xSize; x += step)
            {
                for (int y = 0; y < fmriLoader.ySize; y += step)
                {
                    for (int z = 0; z < fmriLoader.zSize; z += step)
                    {
                        float value = fmriLoader.GetVoxelValue(t, x, y, z);
                        // Debug.Log($"Voxel value at ({t}, {x}, {y}, {z}): {value}");
                        if (value < globalMinValue) globalMinValue = value;
                        if (value > globalMaxValue) globalMaxValue = value;
                    }
                }
            }
        }
        
        Debug.Log($"Calculated global min/max: {globalMinValue:F3} / {globalMaxValue:F3}");
    }
    
    void UpdateCubeColor()
    {
        if (!dataReady) return;
        
        // Clamp coordinates to valid ranges
        currentTimePoint = Mathf.Clamp(currentTimePoint, 0, fmriLoader.timePoints - 1);
        currentX = Mathf.Clamp(currentX, 0, fmriLoader.xSize - 1);
        currentY = Mathf.Clamp(currentY, 0, fmriLoader.ySize - 1);
        currentZ = Mathf.Clamp(currentZ, 0, fmriLoader.zSize - 1);
        
        // Get the voxel value
        currentVoxelValue = fmriLoader.GetVoxelValue(currentTimePoint, currentX, currentY, currentZ);
        Debug.Log($"Voxel value at ({currentTimePoint}, {currentX}, {currentY}, {currentZ}): {currentVoxelValue}");
        
        // Determine min/max for normalization
        float minVal = useGlobalMinMax ? globalMinValue : customMinValue;
        float maxVal = useGlobalMinMax ? globalMaxValue : customMaxValue;
        
        // Normalize the value to 0-1 range
        float normalizedValue = 0f;
        if (maxVal > minVal)
        {
            normalizedValue = Mathf.Clamp01((currentVoxelValue - minVal) / (maxVal - minVal));
        }
        
        // Interpolate between min and max colors
        currentColor = Color.Lerp(minColor, maxColor, normalizedValue);
        
        // Apply the color
        SetCubeColor(currentColor);
    }
    
    void SetCubeColor(Color color)
    {
        if (cubeMaterial != null)
        {
            cubeMaterial.color = color;
        }
    }
    
    // Public methods for external control
    [ContextMenu("Update Color Now")]
    public void UpdateColorNow()
    {
        if (dataReady)
        {
            UpdateCubeColor();
        }
    }
    
    [ContextMenu("Recalculate Min/Max")]
    public void RecalculateMinMax()
    {
        if (dataReady)
        {
            CalculateGlobalMinMax();
        }
    }
    
    public void SetVoxelCoordinates(int t, int x, int y, int z)
    {
        currentTimePoint = t;
        currentX = x;
        currentY = y;
        currentZ = z;
        
        if (dataReady && !updateInRealTime)
        {
            UpdateCubeColor();
        }
    }
    
    public void SetTimePoint(int timePoint)
    {
        currentTimePoint = Mathf.Clamp(timePoint, 0, fmriLoader != null ? fmriLoader.timePoints - 1 : 0);
        if (dataReady && !updateInRealTime)
        {
            UpdateCubeColor();
        }
    }
    
    public void ToggleAnimation()
    {
        animateTimePoints = !animateTimePoints;
        animationTimer = currentTimePoint; // Start animation from current time point
    }
    
    public float GetNormalizedVoxelValue()
    {
        if (!dataReady) return 0f;
        
        float minVal = useGlobalMinMax ? globalMinValue : customMinValue;
        float maxVal = useGlobalMinMax ? globalMaxValue : customMaxValue;
        
        if (maxVal <= minVal) return 0f;
        
        return Mathf.Clamp01((currentVoxelValue - minVal) / (maxVal - minVal));
    }
    
    void OnDestroy()
    {
        // Clean up the material instance
        if (cubeMaterial != null)
        {
            DestroyImmediate(cubeMaterial);
        }
    }
    
    // Validation in inspector
    void OnValidate()
    {
        if (fmriLoader != null)
        {
            currentTimePoint = Mathf.Clamp(currentTimePoint, 0, Mathf.Max(0, fmriLoader.timePoints - 1));
            currentX = Mathf.Clamp(currentX, 0, Mathf.Max(0, fmriLoader.xSize - 1));
            currentY = Mathf.Clamp(currentY, 0, Mathf.Max(0, fmriLoader.ySize - 1));
            currentZ = Mathf.Clamp(currentZ, 0, Mathf.Max(0, fmriLoader.zSize - 1));
        }
        
        if (customMaxValue < customMinValue)
        {
            customMaxValue = customMinValue + 0.1f;
        }
    }
}