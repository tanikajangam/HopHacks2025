using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class FMRILoader : MonoBehaviour
{
    [Header("FMRI Data Settings")]
    public string dataFileName = "sub-10159_task-bart_bold_unity.bytes";
    public int timePoints = 267;
    public int xSize = 64;
    public int ySize = 64;
    public int zSize = 34;
    
    [Header("Runtime Data")]
    public float[,,,] fmriData;
    public bool dataLoaded = false;
    
    void Start()
    {
        LoadFMRIData();
    }
    
    public void LoadFMRIData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, dataFileName);
        
        if (File.Exists(filePath))
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            float[] floatArray = new float[fileBytes.Length / 4];
            
            Buffer.BlockCopy(fileBytes, 0, floatArray, 0, fileBytes.Length);
            
            // Initialize 4D array
            fmriData = new float[timePoints, xSize, ySize, zSize];
            
            // Fill the 4D array
            int index = 0;
            for (int t = 0; t < timePoints; t++)
            {
                for (int x = 0; x < xSize; x++)
                {
                    for (int y = 0; y < ySize; y++)
                    {
                        for (int z = 0; z < zSize; z++)
                        {
                            fmriData[t, x, y, z] = floatArray[index++];
                        }
                    }
                }
            }
            
            dataLoaded = true;
            Debug.Log($"FMRI data loaded successfully! Shape: {timePoints}x{xSize}x{ySize}x{zSize}");
        }
        else
        {
            Debug.LogError($"FMRI data file not found: {filePath}");
        }
    }
    
    // Get value at specific coordinates
    public float GetVoxelValue(int timePoint, int x, int y, int z)
    {
        if (!dataLoaded || 
            timePoint >= timePoints || x >= xSize || y >= ySize || z >= zSize ||
            timePoint < 0 || x < 0 || y < 0 || z < 0)
        {
            return 0f;
        }
        
        return fmriData[timePoint, x, y, z];
    }
    
    // Get entire time series for a voxel
    public float[] GetVoxelTimeSeries(int x, int y, int z)
    {
        float[] timeSeries = new float[timePoints];
        for (int t = 0; t < timePoints; t++)
        {
            timeSeries[t] = GetVoxelValue(t, x, y, z);
        }
        return timeSeries;
    }
    
    // Get a specific time slice
    public float[,,] GetTimeSlice(int timePoint)
    {
        float[,,] timeSlice = new float[xSize, ySize, zSize];
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                for (int z = 0; z < zSize; z++)
                {
                    timeSlice[x, y, z] = GetVoxelValue(timePoint, x, y, z);
                }
            }
        }
        return timeSlice;
    }
}
