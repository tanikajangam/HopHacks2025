import nibabel as nib
import numpy as np
import scipy.io as sio
import json
from pathlib import Path

def load_and_convert_nifti(filepath, n_preview=5, output_dir=None):
    """
    Load NIfTI fMRI data, preview first n time slices, and save as Unity-compatible 4D format
    
    Parameters:
    filepath (str): Path to the .nii or .nii.gz file
    n_preview (int): Number of time slices to preview
    output_dir (str): Directory to save output files (default: same as input file)
    
    Returns:
    np.ndarray: 4D array with shape (t, x, y, z)
    """
    
    # Load the NIfTI file
    try:
        img = nib.load(filepath)
        data = img.get_fdata()
        header = img.header
        affine = img.affine
        
        print(f"Successfully loaded: {Path(filepath).name}")
        print(f"Original shape: {data.shape}")
        print(f"Data type: {data.dtype}")
        print(f"Voxel dimensions: {header.get_zooms()}")
        
    except Exception as e:
        print(f"Error loading file: {e}")
        return None
    
    # Check if it's 4D data
    if len(data.shape) != 4:
        print(f"Warning: Expected 4D data, got {len(data.shape)}D data with shape {data.shape}")
        if len(data.shape) == 3:
            print("Adding time dimension...")
            data = data[:, :, :, np.newaxis]
        else:
            print("Cannot process this data format.")
            return None
    
    # Get dimensions
    x_size, y_size, z_size, t_size = data.shape
    print(f"Spatial dimensions: {x_size} x {y_size} x {z_size}")
    print(f"Number of time points: {t_size}")
    
    # Preview first n time slices
    print(f"\n=== PREVIEW: First {min(n_preview, t_size)} time slices ===")
    for t in range(min(n_preview, t_size)):
        time_slice = data[:, :, :, t]
        print(f"Time slice {t}:")
        print(f"  Shape: {time_slice.shape}")
        print(f"  Min: {np.min(time_slice):.3f}, Max: {np.max(time_slice):.3f}, Mean: {np.mean(time_slice):.3f}")
        print(f"  Non-zero voxels: {np.count_nonzero(time_slice)}")
        
        # Show a sample of values from the center of the volume
        center_x, center_y, center_z = x_size//2, y_size//2, z_size//2
        sample_region = time_slice[center_x-2:center_x+3, center_y-2:center_y+3, center_z]
        print(f"  Center slice sample (5x5 at z={center_z}):")
        print(f"    {sample_region}")
        print()
    
    # Reshape to Unity format: (t, x, y, z)
    print("Reshaping to Unity format (t, x, y, z)...")
    unity_data = np.transpose(data, (3, 0, 1, 2))  # Move time dimension to front
    
    print(f"Unity format shape: {unity_data.shape}")
    print(f"Memory usage: {unity_data.nbytes / (1024**2):.1f} MB")
    
    # Set output directory
    if output_dir is None:
        output_dir = Path(filepath).parent
    else:
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
    
    # Generate output filename base
    input_name = Path(filepath).stem
    if input_name.endswith('.nii'):  # Handle .nii.gz files
        input_name = input_name[:-4]
    
    # Save in multiple Unity-compatible formats
    save_formats(unity_data, output_dir, input_name, header, affine)
    
    return unity_data

def save_formats(data, output_dir, filename_base, header, affine):
    """
    Save 4D data in multiple formats compatible with Unity
    """
    
    print(f"\nSaving files to: {output_dir}")
    
    # 1. Save as NumPy binary format (.npy) - Recommended for Unity
    npy_path = output_dir / f"{filename_base}_unity.npy"
    np.save(npy_path, data)
    print(f"✓ Saved NumPy format: {npy_path}")
    
    # 2. Save as MATLAB format (.mat) - Also compatible with Unity
    mat_path = output_dir / f"{filename_base}_unity.mat"
    sio.savemat(mat_path, {
        'fmri_data': data,
        'shape': data.shape,
        'data_info': {
            'description': 'fMRI data in Unity format (t,x,y,z)',
            'original_voxel_size': [float(x) for x in header.get_zooms()[:3]],  # Convert to Python float
            'time_step': float(header.get_zooms()[3]) if len(header.get_zooms()) > 3 else 1.0
        }
    })
    print(f"✓ Saved MATLAB format: {mat_path}")
    
    # 3. Save as raw binary (.bytes) - Direct binary format for Unity
    bytes_path = output_dir / f"{filename_base}_unity.bytes"
    # Convert to float32 for Unity compatibility and save as binary
    data_float32 = data.astype(np.float32)
    data_float32.tofile(bytes_path)
    print(f"✓ Saved binary format: {bytes_path}")
    
    # 4. Save metadata as JSON for Unity scripts
    json_path = output_dir / f"{filename_base}_metadata.json"
    metadata = {
        'shape': data.shape,
        'dtype': str(data.dtype),
        'dimensions': {
            't': int(data.shape[0]),
            'x': int(data.shape[1]), 
            'y': int(data.shape[2]),
            'z': int(data.shape[3])
        },
        'voxel_size': [float(x) for x in header.get_zooms()[:3]],  # FIX: Convert to Python float
        'time_step': float(header.get_zooms()[3]) if len(header.get_zooms()) > 3 else 1.0,
        'file_info': {
            'npy_file': f"{filename_base}_unity.npy",
            'mat_file': f"{filename_base}_unity.mat", 
            'bytes_file': f"{filename_base}_unity.bytes",
            'format_note': 'Data is in (t,x,y,z) format - time first, then spatial dimensions'
        },
        'data_stats': {
            'min': float(np.min(data)),
            'max': float(np.max(data)),
            'mean': float(np.mean(data)),
            'std': float(np.std(data))
        }
    }
    
    with open(json_path, 'w') as f:
        json.dump(metadata, f, indent=2)
    print(f"✓ Saved metadata: {json_path}")
    
    # 5. Create Unity C# script template
    create_unity_loader_script(output_dir, filename_base, data.shape)

def create_unity_loader_script(output_dir, filename_base, shape):
    """
    Create a Unity C# script template for loading the fMRI data
    """
    
    script_content = f'''using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class FMRILoader : MonoBehaviour
{{
    [Header("FMRI Data Settings")]
    public string dataFileName = "{filename_base}_unity.bytes";
    public int timePoints = {shape[0]};
    public int xSize = {shape[1]};
    public int ySize = {shape[2]};
    public int zSize = {shape[3]};
    
    [Header("Runtime Data")]
    public float[,,,] fmriData;
    public bool dataLoaded = false;
    
    void Start()
    {{
        LoadFMRIData();
    }}
    
    public void LoadFMRIData()
    {{
        string filePath = Path.Combine(Application.streamingAssetsPath, dataFileName);
        
        if (File.Exists(filePath))
        {{
            byte[] fileBytes = File.ReadAllBytes(filePath);
            float[] floatArray = new float[fileBytes.Length / 4];
            
            Buffer.BlockCopy(fileBytes, 0, floatArray, 0, fileBytes.Length);
            
            // Initialize 4D array
            fmriData = new float[timePoints, xSize, ySize, zSize];
            
            // Fill the 4D array
            int index = 0;
            for (int t = 0; t < timePoints; t++)
            {{
                for (int x = 0; x < xSize; x++)
                {{
                    for (int y = 0; y < ySize; y++)
                    {{
                        for (int z = 0; z < zSize; z++)
                        {{
                            fmriData[t, x, y, z] = floatArray[index++];
                        }}
                    }}
                }}
            }}
            
            dataLoaded = true;
            Debug.Log($"FMRI data loaded successfully! Shape: {{timePoints}}x{{xSize}}x{{ySize}}x{{zSize}}");
        }}
        else
        {{
            Debug.LogError($"FMRI data file not found: {{filePath}}");
        }}
    }}
    
    // Get value at specific coordinates
    public float GetVoxelValue(int timePoint, int x, int y, int z)
    {{
        if (!dataLoaded || 
            timePoint >= timePoints || x >= xSize || y >= ySize || z >= zSize ||
            timePoint < 0 || x < 0 || y < 0 || z < 0)
        {{
            return 0f;
        }}
        
        return fmriData[timePoint, x, y, z];
    }}
    
    // Get entire time series for a voxel
    public float[] GetVoxelTimeSeries(int x, int y, int z)
    {{
        float[] timeSeries = new float[timePoints];
        for (int t = 0; t < timePoints; t++)
        {{
            timeSeries[t] = GetVoxelValue(t, x, y, z);
        }}
        return timeSeries;
    }}
    
    // Get a specific time slice
    public float[,,] GetTimeSlice(int timePoint)
    {{
        float[,,] timeSlice = new float[xSize, ySize, zSize];
        for (int x = 0; x < xSize; x++)
        {{
            for (int y = 0; y < ySize; y++)
            {{
                for (int z = 0; z < zSize; z++)
                {{
                    timeSlice[x, y, z] = GetVoxelValue(timePoint, x, y, z);
                }}
            }}
        }}
        return timeSlice;
    }}
}}
'''
    
    script_path = output_dir / f"FMRILoader_{filename_base}.cs"
    with open(script_path, 'w') as f:
        f.write(script_content)
    print(f"✓ Created Unity C# script: {script_path}")

def load_unity_format(filepath):
    """
    Load Unity format data back into Python for verification
    """
    print(f"Loading Unity format data from: {filepath}")
    
    if filepath.endswith('.npy'):
        data = np.load(filepath)
    elif filepath.endswith('.mat'):
        mat_data = sio.loadmat(filepath)
        data = mat_data['fmri_data']
    else:
        print("Unsupported format for loading")
        return None
        
    print(f"Loaded shape: {data.shape} (t, x, y, z)")
    return data

# Example usage
if __name__ == "__main__":
    # Replace with your NIfTI file path
    nifti_file = "/home/cartorh/Desktop/Hobbies/Projects/Hackathons/HopHacks2025/sub-10159_task-bart_bold.nii.gz"
    
    # Convert and save
    unity_data = load_and_convert_nifti(
        filepath=nifti_file,
        n_preview=3,  # Preview first 3 time slices
        output_dir="./unity_output"  # Optional: specify output directory
    )
    
    if unity_data is not None:
        print(f"\nConversion complete!")
        print(f"Final Unity format shape: {unity_data.shape} (t, x, y, z)")
        
        # Verify by loading one of the saved files
        # verify_data = load_unity_format("./unity_output/sub-10159_task-bart_bold_unity.npy")