using System.Collections;
using UnityEngine;
using System;
using System.IO;

public class H5MatrixCubeColorizer : MonoBehaviour
{
    [Header("Data File Settings")]
    [SerializeField] private string dataFilePath = "Assets/Data/matrix_data.txt";
    [SerializeField] private TextAsset dataFile; // Drag a text file here as alternative
    
    [Header("Matrix Dimensions")]
    [SerializeField] private int matrixT = 10;
    [SerializeField] private int matrixX = 10;
    [SerializeField] private int matrixY = 10;
    [SerializeField] private int matrixZ = 10;
    
    [Header("Color Settings")]
    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 255f;
    [SerializeField] private bool autoCalculateMinMax = true;
    [SerializeField] private bool useRandomData = true; // For testing without file
    
    [Header("Cube Reference")]
    [SerializeField] private GameObject targetCube;
    
    // 4D matrix storage
    private int[,,,] matrixData;
    
    // Cube renderer for color application
    private Renderer cubeRenderer;
    private Material cubeMaterial;
    
    void Start()
    {
        // Get cube renderer
        if (targetCube == null)
            targetCube = gameObject;
            
        cubeRenderer = targetCube.GetComponent<Renderer>();
        if (cubeRenderer == null)
        {
            Debug.LogError("No Renderer found on target cube!");
            return;
        }
        
        // Create a material instance
        cubeMaterial = new Material(Shader.Find("Standard"));
        cubeRenderer.material = cubeMaterial;
        
        // Load the matrix data
        StartCoroutine(LoadMatrixData());
    }
    
    private IEnumerator LoadMatrixData()
    {
        if (useRandomData)
        {
            GenerateRandomData();
        }
        else
        {
            yield return StartCoroutine(ReadDataFile());
        }
        
        if (matrixData != null)
        {
            // Calculate min/max if auto-calculate is enabled
            if (autoCalculateMinMax)
            {
                CalculateMinMaxValues();
            }
            
            // Apply color to cube based on value at (0,0,0,0)
            ApplyCubeColor();
        }
    }
    
    private void GenerateRandomData()
    {
        Debug.Log("Generating random matrix data for testing...");
        
        matrixData = new int[matrixT, matrixX, matrixY, matrixZ];
        
        for (int t = 0; t < matrixT; t++)
        {
            for (int x = 0; x < matrixX; x++)
            {
                for (int y = 0; y < matrixY; y++)
                {
                    for (int z = 0; z < matrixZ; z++)
                    {
                        matrixData[t, x, y, z] = UnityEngine.Random.Range(0, 256);
                    }
                }
            }
        }
        
        Debug.Log($"Generated matrix with dimensions: {matrixT}x{matrixX}x{matrixY}x{matrixZ}");
        Debug.Log($"Value at (0,0,0,0): {matrixData[0, 0, 0, 0]}");
    }
    
    private IEnumerator ReadDataFile()
    {
        string fileContent = "";
        
        // Try to read from TextAsset first
        if (dataFile != null)
        {
            fileContent = dataFile.text;
            Debug.Log("Reading from TextAsset");
        }
        // Otherwise try to read from file path
        else if (File.Exists(dataFilePath))
        {
            fileContent = File.ReadAllText(dataFilePath);
            Debug.Log($"Reading from file: {dataFilePath}");
        }
        else
        {
            Debug.LogError($"Data file not found at path: {dataFilePath}. Using random data instead.");
            GenerateRandomData();
            yield break;
        }
        
        try
        {
            // Parse the file content
            // Expected format: each line contains comma-separated values
            // First line should contain dimensions: t,x,y,z
            // Following lines contain the matrix data in order
            
            string[] lines = fileContent.Split('\n');
            
            if (lines.Length < 1)
            {
                Debug.LogError("File is empty");
                yield break;
            }
            
            // Parse dimensions from first line
            string[] dimensions = lines[0].Split(',');
            if (dimensions.Length >= 4)
            {
                matrixT = int.Parse(dimensions[0].Trim());
                matrixX = int.Parse(dimensions[1].Trim());
                matrixY = int.Parse(dimensions[2].Trim());
                matrixZ = int.Parse(dimensions[3].Trim());
            }
            
            Debug.Log($"Matrix dimensions: t={matrixT}, x={matrixX}, y={matrixY}, z={matrixZ}");
            
            // Allocate memory for matrix
            matrixData = new int[matrixT, matrixX, matrixY, matrixZ];
            
            // Parse data from remaining lines
            int dataLineIndex = 1;
            for (int t = 0; t < matrixT && dataLineIndex < lines.Length; t++)
            {
                for (int x = 0; x < matrixX && dataLineIndex < lines.Length; x++)
                {
                    for (int y = 0; y < matrixY && dataLineIndex < lines.Length; y++)
                    {
                        if (dataLineIndex >= lines.Length) break;
                        
                        string[] values = lines[dataLineIndex].Split(',');
                        for (int z = 0; z < matrixZ && z < values.Length; z++)
                        {
                            if (int.TryParse(values[z].Trim(), out int value))
                            {
                                matrixData[t, x, y, z] = value;
                            }
                        }
                        dataLineIndex++;
                    }
                }
            }
            
            Debug.Log("Successfully loaded matrix data from file");
            Debug.Log($"Value at (0,0,0,0): {matrixData[0, 0, 0, 0]}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error reading data file: {e.Message}. Using random data instead.");
            GenerateRandomData();
        }
        
        yield return null;
    }
    
    private void CalculateMinMaxValues()
    {
        if (matrixData == null) return;
        
        float min = float.MaxValue;
        float max = float.MinValue;
        
        for (int t = 0; t < matrixT; t++)
        {
            for (int x = 0; x < matrixX; x++)
            {
                for (int y = 0; y < matrixY; y++)
                {
                    for (int z = 0; z < matrixZ; z++)
                    {
                        float value = matrixData[t, x, y, z];
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }
        }
        
        minValue = min;
        maxValue = max;
        
        Debug.Log($"Calculated min/max values: {minValue} / {maxValue}");
    }
    
    private void ApplyCubeColor()
    {
        if (matrixData == null || cubeMaterial == null) return;
        
        // Get value at position (0,0,0,0)
        int valueAt000 = matrixData[0, 0, 0, 0];
        
        // Normalize the value to 0-1 range
        float normalizedValue = Mathf.InverseLerp(minValue, maxValue, valueAt000);
        
        // Create grayscale color
        Color grayscaleColor = new Color(normalizedValue, normalizedValue, normalizedValue, 1f);
        
        // Apply color to material
        cubeMaterial.color = grayscaleColor;
        
        Debug.Log($"Applied color to cube. Value: {valueAt000}, Normalized: {normalizedValue:F3}, Color: {grayscaleColor}");
    }
    
    // Public method to update cube color with a different position
    public void UpdateCubeColor(int t, int x, int y, int z)
    {
        if (matrixData == null || t >= matrixT || x >= matrixX || y >= matrixY || z >= matrixZ)
        {
            Debug.LogWarning("Invalid position or matrix not loaded");
            return;
        }
        
        int value = matrixData[t, x, y, z];
        float normalizedValue = Mathf.InverseLerp(minValue, maxValue, value);
        Color grayscaleColor = new Color(normalizedValue, normalizedValue, normalizedValue, 1f);
        
        cubeMaterial.color = grayscaleColor;
        
        Debug.Log($"Updated cube color for position ({t},{x},{y},{z}). Value: {value}, Color: {grayscaleColor}");
    }
    
    // Public method to manually set min/max values
    public void SetMinMaxValues(float min, float max)
    {
        minValue = min;
        maxValue = max;
        autoCalculateMinMax = false;
        
        if (matrixData != null)
        {
            ApplyCubeColor();
        }
    }
    
    // Get matrix dimensions
    public Vector4 GetMatrixDimensions()
    {
        return new Vector4(matrixT, matrixX, matrixY, matrixZ);
    }
    
    // Get value at specific position
    public int GetValueAt(int t, int x, int y, int z)
    {
        if (matrixData == null || t >= matrixT || x >= matrixX || y >= matrixY || z >= matrixZ)
        {
            Debug.LogWarning("Invalid position or matrix not loaded");
            return 0;
        }
        
        return matrixData[t, x, y, z];
    }
    
    void OnDestroy()
    {
        // Clean up material
        if (cubeMaterial != null)
        {
            DestroyImmediate(cubeMaterial);
        }
    }
}