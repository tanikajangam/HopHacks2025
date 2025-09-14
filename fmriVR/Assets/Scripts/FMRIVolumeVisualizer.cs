using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FMRIVolumeVisualizer : MonoBehaviour
{
    [Header("FMRI Data Source")]
    public FMRILoader fmriLoader;
    public bool findLoaderAutomatically = true;
    
    [Header("Volume Settings")]
    public int currentTimePoint = 0;
    public float voxelSize = 1f;
    public float voxelSpacing = 1.1f; // Multiplier for spacing between cubes
    
    [Header("Cube Prefab")]
    public GameObject cubePrefab;
    public bool createCubePrefabIfMissing = true;
    
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
    
    [Header("Performance")]
    public bool updateInRealTime = true;
    public float updateInterval = 0.1f;
    public bool useLevelOfDetail = true;
    public float maxRenderDistance = 50f;
    
    [Header("Animation")]
    public bool animateTimePoints = false;
    public float animationSpeed = 1f;
    
    [Header("Debug Info")]
    public bool dataReady = false;
    public float globalMinValue = 0f;
    public float globalMaxValue = 0f;
    public int totalCubes = 0;
    public int visibleCubes = 0;
    
    // Private variables
    private GameObject[,,] cubeGrid;
    private Material[] cubeMaterials;
    private float lastUpdateTime = 0f;
    private float animationTimer = 0f;
    private Camera playerCamera;
    private Transform cubeContainer;

    [Header("Volume Subset")]
    public int xOffset = 0;
    public int yOffset = 0; 
    public int zOffset = 0;
    public int chunkSizeX = 20;
    public int chunkSizeY = 20;
    public int chunkSizeZ = 20;
    public bool useFullVolume = true;
    
    // Store the actual render bounds for other functions to use
    private int renderStartX, renderStartY, renderStartZ;
    private int renderEndX, renderEndY, renderEndZ;
    private int renderSizeX, renderSizeY, renderSizeZ;
    
    void Start()
    {
        // Get main camera for LOD calculations
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();

        // find parent, if it doesnt exist, just make one
        GameObject parent = GameObject.Find("EmptyTransform");
        if (parent == null)
        {
            parent = new GameObject("EmptyTransform");
        }

        // Create container for all cubes
        GameObject container = new GameObject("FMRI_Cube_Container");
        if (existingContainer != null)
        {
            // destroy old container if it already exists (and all its children cubes)
            DestroyImmediate(existingContainer.gameObject);
        }

        container.transform.SetParent(parent.transform);
        //container.transform.parent = transform;
        cubeContainer = container.transform;

        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;
        
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
                        yield return StartCoroutine(UpdateAllCubes());
                    }
                }
                
                // Update visualization if enough time has passed
                if (updateInRealTime && Time.time - lastUpdateTime >= updateInterval)
                {
                    yield return StartCoroutine(UpdateAllCubes());
                    lastUpdateTime = Time.time;
                }
                
                // Update LOD if enabled
                if (useLevelOfDetail)
                {
                    UpdateLevelOfDetail();
                }
            }
            else if (dataReady)
            {
                // Data was ready but now isn't - cleanup
                dataReady = false;
                DestroyAllCubes();
            }
            
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    IEnumerator InitializeVisualization()
    {
        Debug.Log("Initializing FMRI volume visualization...");
        
        // Calculate render bounds first
        CalculateRenderBounds();
        
        // Calculate global min/max for the subset only
        yield return StartCoroutine(CalculateGlobalMinMax());
        
        // Create the cube grid
        yield return StartCoroutine(CreateCubeGrid());
        
        // Initial color update
        yield return StartCoroutine(UpdateAllCubes());
        
        dataReady = true;
        Debug.Log($"FMRI visualization ready! {totalCubes} cubes created for subset [{renderStartX}-{renderEndX-1}, {renderStartY}-{renderEndY-1}, {renderStartZ}-{renderEndZ-1}]");
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
        Debug.Log($"Render size: {renderSizeX}x{renderSizeY}x{renderSizeZ} = {renderSizeX * renderSizeY * renderSizeZ} cubes");
    }
    
    IEnumerator CalculateGlobalMinMax()
    {
        Debug.Log("Calculating global min/max values for subset...");
        if (fmriLoader == null || !fmriLoader.dataLoaded) yield break;
        
        globalMinValue = float.MaxValue;
        globalMaxValue = float.MinValue;
        
        int validValues = 0;
        
        // Only calculate min/max for the render subset
        for (int t = 0; t < fmriLoader.timePoints; t++)
        {
            for (int x = renderStartX; x < renderEndX; x++)
            {
                for (int y = renderStartY; y < renderEndY; y++)
                {
                    for (int z = renderStartZ; z < renderEndZ; z++)
                    {
                        float value = fmriLoader.GetVoxelValue(t, x, y, z);
                        
                        // Add detailed logging for first few values
                        if (validValues < 10)
                        {
                            Debug.Log($"Sample voxel ({t},{x},{y},{z}): {value}");
                        }
                        
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
        
        Debug.Log($"Valid values found in subset: {validValues}");
        Debug.Log($"Subset range: {globalMinValue:F6} to {globalMaxValue:F6}");
        Debug.Log($"Range difference: {(globalMaxValue - globalMinValue):F6}");
    }

    IEnumerator CreateCubeGrid()
    {
        Debug.Log("Creating cube grid...");

        // Clear existing cubes
        DestroyAllCubes();

        // Create cube prefab if missing
        if (cubePrefab == null && createCubePrefabIfMissing)
        {
            cubePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubePrefab.name = "FMRI_Voxel_Cube";
        }

        if (cubePrefab == null)
        {
            Debug.LogError("No cube prefab assigned and createCubePrefabIfMissing is false!");
            yield break;
        }

        // Initialize grid for the render area only
        cubeGrid = new GameObject[renderSizeX, renderSizeY, renderSizeZ];
        cubeMaterials = new Material[renderSizeX * renderSizeY * renderSizeZ];

        // Calculate center offset for positioning
        Vector3 centerOffset = new Vector3(
            (renderSizeX - 1) * voxelSize * voxelSpacing * 0.5f,
            (renderSizeY - 1) * voxelSize * voxelSpacing * 0.5f,
            (renderSizeZ - 1) * voxelSize * voxelSpacing * 0.5f
        );

        int cubeIndex = 0;
        totalCubes = 0;

        for (int x = renderStartX; x < renderEndX; x++)
        {
            for (int y = renderStartY; y < renderEndY; y++)
            {
                for (int z = renderStartZ; z < renderEndZ; z++)
                {
                    // Create cube
                    GameObject cube = Instantiate(cubePrefab, cubeContainer);
                    cube.name = $"Voxel_{x}_{y}_{z}";

                    // Position cube (relative to render area)
                    int relX = x - renderStartX;
                    int relY = y - renderStartY;
                    int relZ = z - renderStartZ;

                    Vector3 position = new Vector3(
                        relX * voxelSize * voxelSpacing - centerOffset.x,
                        relY * voxelSize * voxelSpacing - centerOffset.y,
                        relZ * voxelSize * voxelSpacing - centerOffset.z
                    );
                    cube.transform.localPosition = position;
                    cube.transform.localScale = Vector3.one * voxelSize;

                    // Create unique material
                    Renderer renderer = cube.GetComponent<Renderer>();
                    Material cubeMaterial = new Material(renderer.material);

                    // Set surface type to transparent
                    cubeMaterial.SetFloat("_Surface", 1);
                    cubeMaterial.SetFloat("_Blend", 0);
                    cubeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    cubeMaterial.EnableKeyword("_ALPHABLEND_ON");
                    cubeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    cubeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                    // Set initial color
                    Color cubeColor = GetColorFromScheme(colorSchemes[currentColorSchemeIndex], 0.5f);
                    cubeMaterial.color = cubeColor;
                    cubeMaterial.SetFloat("_QueueOffset", 0);

                    renderer.material = cubeMaterial;
                    cubeMaterials[cubeIndex] = cubeMaterial;

                    // Store in grid (using relative coordinates)
                    cubeGrid[relX, relY, relZ] = cube;

                    // Add LOD component if needed
                    if (useLevelOfDetail)
                    {
                        FMRIVoxelLOD lodComponent = cube.AddComponent<FMRIVoxelLOD>();
                        lodComponent.maxDistance = maxRenderDistance;
                    }

                    cubeIndex++;
                    totalCubes++;

                    // Yield periodically
                    if (totalCubes % 1000 == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        Debug.Log($"Cube grid created: {totalCubes} cubes ({renderSizeX}x{renderSizeY}x{renderSizeZ})");
        cubePrefab.SetActive(false);
    }
    
    IEnumerator UpdateAllCubes()
    {
        if (!dataReady || cubeGrid == null) yield break;
        
        currentTimePoint = Mathf.Clamp(currentTimePoint, 0, fmriLoader.timePoints - 1);
        ColorScheme scheme = colorSchemes[Mathf.Clamp(currentColorSchemeIndex, 0, colorSchemes.Length - 1)];
        
        float minVal = useGlobalMinMax ? globalMinValue : customMinValue;
        float maxVal = useGlobalMinMax ? globalMaxValue : customMaxValue;
        
        int updatedCubes = 0;
        int cubeIndex = 0;

        for (int x = renderStartX; x < renderEndX; x++)
        {
            for (int y = renderStartY; y < renderEndY; y++)
            {
                for (int z = renderStartZ; z < renderEndZ; z++)
                {
                    int relX = x - renderStartX;
                    int relY = y - renderStartY;
                    int relZ = z - renderStartZ;
                    
                    GameObject cube = cubeGrid[relX, relY, relZ];
                    if (cube == null) continue;

                    Material cubeMaterial = cubeMaterials[cubeIndex];
                    if (cubeMaterial == null) continue;
                    
                    // Get voxel value (using absolute coordinates from FMRI data)
                    float voxelValue = fmriLoader.GetVoxelValue(currentTimePoint, x, y, z);
                    
                    // Normalize value
                    float normalizedValue = 0f;
                    if (maxVal > minVal && !float.IsNaN(voxelValue) && !float.IsInfinity(voxelValue))
                    {
                        normalizedValue = Mathf.Clamp01((voxelValue - minVal) / (maxVal - minVal));
                    }
                    
                    // Calculate and apply color
                    Color cubeColor = GetColorFromScheme(scheme, normalizedValue);
                    cubeMaterial.color = cubeColor;

                    // Ensure shader is set correctly
                    cubeMaterial.shader = cubeMaterial.shader;
                    
                    cubeIndex++;
                    updatedCubes++;
                    
                    if (updatedCubes % 5000 == 0)
                    {
                        yield return null;
                    }
                }
            }
        }
        
        Debug.Log($"Updated {updatedCubes} cubes for time point {currentTimePoint}");
    }
    
    Color GetColorFromScheme(ColorScheme scheme, float normalizedValue)
    {
        Color baseColor = Color.Lerp(scheme.minColor, scheme.maxColor, normalizedValue);
        
        if (scheme.useTransparency)
        {
            float alpha = Mathf.Lerp(scheme.minAlpha, scheme.maxAlpha, normalizedValue);
            baseColor.a = alpha;
        }
        else
        {
            baseColor.a = 1f;
        }
        
        return baseColor;
    }
    
    void UpdateLevelOfDetail()
    {
        if (playerCamera == null || cubeGrid == null) return;
        
        visibleCubes = 0;
        Vector3 cameraPos = playerCamera.transform.position;
        
        // Use the actual cube grid dimensions, not the FMRI loader dimensions
        for (int x = 0; x < renderSizeX; x++)
        {
            for (int y = 0; y < renderSizeY; y++)
            {
                for (int z = 0; z < renderSizeZ; z++)
                {
                    GameObject cube = cubeGrid[x, y, z];
                    if (cube == null) continue;
                    
                    float distance = Vector3.Distance(cameraPos, cube.transform.position);
                    bool shouldBeVisible = distance <= maxRenderDistance;
                    
                    if (cube.activeInHierarchy != shouldBeVisible)
                    {
                        cube.SetActive(shouldBeVisible);
                    }
                    
                    if (shouldBeVisible) visibleCubes++;
                }
            }
        }
    }
    
    void DestroyAllCubes()
    {
        if (cubeGrid != null)
        {
            int xSize = cubeGrid.GetLength(0);
            int ySize = cubeGrid.GetLength(1);
            int zSize = cubeGrid.GetLength(2);
            
            for (int x = 0; x < xSize; x++)
            {
                for (int y = 0; y < ySize; y++)
                {
                    for (int z = 0; z < zSize; z++)
                    {
                        if (cubeGrid[x, y, z] != null)
                        {
                            DestroyImmediate(cubeGrid[x, y, z]);
                        }
                    }
                }
            }
        }
        
        // Clean up materials
        if (cubeMaterials != null)
        {
            for (int i = 0; i < cubeMaterials.Length; i++)
            {
                if (cubeMaterials[i] != null)
                {
                    DestroyImmediate(cubeMaterials[i]);
                }
            }
        }
        
        cubeGrid = null;
        cubeMaterials = null;
        totalCubes = 0;
        visibleCubes = 0;
    }
    
    // Public methods
    [ContextMenu("Update All Cubes Now")]
    public void UpdateAllCubesNow()
    {
        if (dataReady)
        {
            StartCoroutine(UpdateAllCubes());
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
                StartCoroutine(UpdateAllCubes());
            }
        }
    }
    
    public void SetColorScheme(int schemeIndex)
    {
        currentColorSchemeIndex = Mathf.Clamp(schemeIndex, 0, colorSchemes.Length - 1);
        if (dataReady)
        {
            StartCoroutine(UpdateAllCubes());
        }
    }
    
    public void ToggleAnimation()
    {
        animateTimePoints = !animateTimePoints;
        animationTimer = currentTimePoint;
    }
    
    public GameObject GetCubeAt(int x, int y, int z)
    {
        if (cubeGrid != null && x >= 0 && x < cubeGrid.GetLength(0) &&
            y >= 0 && y < cubeGrid.GetLength(1) && z >= 0 && z < cubeGrid.GetLength(2))
        {
            return cubeGrid[x, y, z];
        }
        return null;
    }
    
    public void UpdateSubsetSettings()
    {
        if (fmriLoader != null && fmriLoader.dataLoaded)
        {
            // Recalculate bounds and recreate visualization
            dataReady = false;
            StartCoroutine(InitializeVisualization());
        }
    }
    
    void OnDestroy()
    {
        DestroyAllCubes();
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
        
        // Clamp subset parameters to valid ranges
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

// Helper component for Level of Detail
public class FMRIVoxelLOD : MonoBehaviour
{
    public float maxDistance = 50f;
    private Camera playerCamera;
    
    void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();
    }
    
    void Update()
    {
        if (playerCamera != null)
        {
            float distance = Vector3.Distance(playerCamera.transform.position, transform.position);
            gameObject.SetActive(distance <= maxDistance);
        }
    }
}