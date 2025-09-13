using UnityEngine;
using System.Collections;
using UnityEngine;
using HDF.PInvoke; // You'll need to install HDF5 library for Unity
using System;
using System.IO;

public class ColorCube2 : MonoBehaviour
{
    [Header("H5 File Settings")]
    [SerializeField] private string h5FilePath = "Assets/Data/sub-10159_task-bart_bold_timeseries.h5";
    [SerializeField] private string datasetName = "/matrix"; // Path to dataset in H5 file
    
    [Header("Color Settings")]
    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 255f;
    [SerializeField] private bool autoCalculateMinMax = true;
    
    [Header("Cube Reference")]
    [SerializeField] private GameObject targetCube;
    
    // 4D matrix storage
    private int[,,,] matrixData;
    private int matrixT, matrixX, matrixY, matrixZ;
    
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
        
        // Load the H5 file
        StartCoroutine(LoadH5Matrix());
    }
    
    private IEnumerator LoadH5Matrix()
    {
        yield return StartCoroutine(ReadH5File());
        
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
    
    private IEnumerator ReadH5File()
    {
        if (!File.Exists(h5FilePath))
        {
            Debug.LogError($"H5 file not found at path: {h5FilePath}");
            yield break;
        }
        
        try
        {
            // Open HDF5 file
            var fileId = H5F.open(h5FilePath, H5F.ACC_RDONLY);
            if (fileId < 0)
            {
                Debug.LogError("Failed to open H5 file");
                yield break;
            }
            
            // Open dataset
            var datasetId = H5D.open2(fileId, datasetName);
            if (datasetId < 0)
            {
                Debug.LogError($"Failed to open dataset: {datasetName}");
                H5F.close(fileId);
                yield break;
            }
            
            // Get dataspace
            var spaceId = H5D.get_space(datasetId);
            var rank = H5S.get_simple_extent_ndims(spaceId);
            
            if (rank != 4)
            {
                Debug.LogError($"Expected 4D matrix, got {rank}D");
                H5D.close(datasetId);
                H5F.close(fileId);
                yield break;
            }
            
            // Get dimensions
            ulong[] dims = new ulong[4];
            H5S.get_simple_extent_dims(spaceId, dims, null);
            
            matrixT = (int)dims[0];
            matrixX = (int)dims[1];
            matrixY = (int)dims[2];
            matrixZ = (int)dims[3];
            
            Debug.Log($"Matrix dimensions: t={matrixT}, x={matrixX}, y={matrixY}, z={matrixZ}");
            
            // Allocate memory for matrix
            matrixData = new int[matrixT, matrixX, matrixY, matrixZ];
            
            // Create a flattened array for reading
            int totalSize = matrixT * matrixX * matrixY * matrixZ;
            int[] flatData = new int[totalSize];
            
            // Read data
            var memSpaceId = H5S.create_simple(1, new ulong[] { (ulong)totalSize }, null);
            var status = H5D.read(datasetId, H5T.NATIVE_INT, memSpaceId, spaceId, H5P.DEFAULT, flatData);
            
            if (status < 0)
            {
                Debug.LogError("Failed to read dataset");
            }
            else
            {
                // Convert flat array to 4D matrix
                int index = 0;
                for (int t = 0; t < matrixT; t++)
                {
                    for (int x = 0; x < matrixX; x++)
                    {
                        for (int y = 0; y < matrixY; y++)
                        {
                            for (int z = 0; z < matrixZ; z++)
                            {
                                matrixData[t, x, y, z] = flatData[index++];
                            }
                        }
                    }
                }
                
                Debug.Log("Successfully loaded H5 matrix data");
                Debug.Log($"Value at (0,0,0,0): {matrixData[0, 0, 0, 0]}");
            }
            
            // Cleanup
            H5S.close(memSpaceId);
            H5S.close(spaceId);
            H5D.close(datasetId);
            H5F.close(fileId);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error reading H5 file: {e.Message}");
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