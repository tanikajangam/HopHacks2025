import nibabel as nib
import numpy as np
import h5py
from pathlib import Path
import os

def process_fmri_to_hdf5(nifti_filepath, output_filepath=None, n_print_slices=5, compression='gzip'):
    """
    Load 4D fMRI NIfTI data, print first n time slices, and save as HDF5 format
    
    Parameters:
    nifti_filepath (str): Path to the input .nii or .nii.gz file
    output_filepath (str): Path for output HDF5 file (optional, auto-generated if None)
    n_print_slices (int): Number of time slices to print for inspection
    compression (str): HDF5 compression method ('gzip', 'lzf', 'szip', or None)
    
    Returns:
    str: Path to the saved HDF5 file
    """
    
    print(f"Loading NIfTI file: {nifti_filepath}")
    
    # Load the NIfTI file
    try:
        img = nib.load(nifti_filepath)
        data = img.get_fdata()
        header = img.header
        affine = img.affine
        
        print(f"Successfully loaded image with shape: {data.shape}")
        print(f"Data type: {data.dtype}")
        print(f"Voxel dimensions: {header.get_zooms()}")
        
    except Exception as e:
        print(f"Error loading NIfTI file: {e}")
        return None
    
    # Verify this is 4D data
    if len(data.shape) != 4:
        print(f"Error: Expected 4D data, but got {len(data.shape)}D data with shape {data.shape}")
        return None
    
    x_size, y_size, z_size, t_size = data.shape
    print(f"Spatial dimensions: {x_size} x {y_size} x {z_size}")
    print(f"Number of time points: {t_size}")
    
    # Print first n time slices for inspection
    print(f"\n=== Printing first {min(n_print_slices, t_size)} time slices ===")
    for t in range(min(n_print_slices, t_size)):
        time_slice = data[:, :, :, t]
        print(f"\nTime slice {t}:")
        print(f"  Shape: {time_slice.shape}")
        print(f"  Min: {np.min(time_slice):.6f}")
        print(f"  Max: {np.max(time_slice):.6f}")
        print(f"  Mean: {np.mean(time_slice):.6f}")
        print(f"  Std: {np.std(time_slice):.6f}")
        print(f"  Non-zero voxels: {np.count_nonzero(time_slice)}")
        
        # Print a small sample of values from the center of the brain
        center_x, center_y, center_z = x_size//2, y_size//2, z_size//2
        sample_region = time_slice[center_x-2:center_x+3, center_y-2:center_y+3, center_z]
        print(f"  Sample values (center slice at z={center_z}):")
        print(f"    {sample_region}")
    
    # Reshape data from (x,y,z,t) to (t,x,y,z)
    print(f"\nReshaping data from {data.shape} to ({t_size}, {x_size}, {y_size}, {z_size})")
    data_reshaped = np.transpose(data, (3, 0, 1, 2))
    print(f"Reshaped data shape: {data_reshaped.shape}")
    
    # Generate output filepath if not provided
    if output_filepath is None:
        input_path = Path(nifti_filepath)
        output_filepath = input_path.parent / f"{input_path.stem}_timeseries.h5"
        if input_path.suffix == '.gz':  # Handle .nii.gz files
            output_filepath = input_path.parent / f"{input_path.stem.replace('.nii', '')}_timeseries.h5"
    
    output_filepath = str(output_filepath)
    print(f"\nSaving to HDF5 file: {output_filepath}")
    
    try:
        # Save to HDF5 format
        with h5py.File(output_filepath, 'w') as hf:
            # Save the main data array
            if compression:
                dataset = hf.create_dataset('fmri_data', 
                                          data=data_reshaped, 
                                          compression=compression,
                                          compression_opts=9 if compression=='gzip' else None,
                                          shuffle=True,
                                          fletcher32=True)
            else:
                dataset = hf.create_dataset('fmri_data', data=data_reshaped)
            
            # Add attributes with metadata
            dataset.attrs['description'] = 'fMRI time series data in format (t,x,y,z)'
            dataset.attrs['original_shape'] = data.shape
            dataset.attrs['reshaped_shape'] = data_reshaped.shape
            dataset.attrs['voxel_dimensions'] = header.get_zooms()
            dataset.attrs['data_type'] = str(data.dtype)
            dataset.attrs['source_file'] = str(nifti_filepath)
            
            # Save affine transformation matrix
            hf.create_dataset('affine', data=affine)
            hf['affine'].attrs['description'] = 'Affine transformation matrix from NIfTI header'
            
            # Save additional header information
            header_group = hf.create_group('header_info')
            header_group.attrs['pixdim'] = header['pixdim']
            header_group.attrs['xyzt_units'] = header['xyzt_units'] 
            header_group.attrs['qform_code'] = header['qform_code']
            header_group.attrs['sform_code'] = header['sform_code']
            
            # Calculate and save some basic statistics
            stats_group = hf.create_group('statistics')
            stats_group.create_dataset('mean_timeseries', data=np.mean(data_reshaped, axis=(1,2,3)))
            stats_group.create_dataset('std_timeseries', data=np.std(data_reshaped, axis=(1,2,3)))
            stats_group.create_dataset('global_mean', data=np.mean(data_reshaped))
            stats_group.create_dataset('global_std', data=np.std(data_reshaped))
            
        print(f"Successfully saved HDF5 file!")
        
        # Print file size information
        original_size = os.path.getsize(nifti_filepath) / (1024**2)  # MB
        hdf5_size = os.path.getsize(output_filepath) / (1024**2)  # MB
        compression_ratio = original_size / hdf5_size if hdf5_size > 0 else 0
        
        print(f"\nFile size comparison:")
        print(f"  Original NIfTI: {original_size:.2f} MB")
        print(f"  HDF5 file: {hdf5_size:.2f} MB")
        print(f"  Compression ratio: {compression_ratio:.2f}x")
        
    except Exception as e:
        print(f"Error saving HDF5 file: {e}")
        return None
    
    return output_filepath

def load_and_inspect_hdf5(hdf5_filepath, n_inspect_slices=3):
    """
    Load and inspect the saved HDF5 file to verify the data
    
    Parameters:
    hdf5_filepath (str): Path to the HDF5 file
    n_inspect_slices (int): Number of time slices to inspect
    """
    
    print(f"\n=== Inspecting HDF5 file: {hdf5_filepath} ===")
    
    try:
        with h5py.File(hdf5_filepath, 'r') as hf:
            # Print file structure
            print("File structure:")
            def print_structure(name, obj):
                print(f"  {name}: {type(obj)}")
                if isinstance(obj, h5py.Dataset):
                    print(f"    Shape: {obj.shape}, Dtype: {obj.dtype}")
            
            hf.visititems(print_structure)
            
            # Load the main dataset
            fmri_data = hf['fmri_data'][:]
            print(f"\nLoaded fMRI data shape: {fmri_data.shape}")
            print(f"Data type: {fmri_data.dtype}")
            
            # Print attributes
            print(f"\nDataset attributes:")
            for key, value in hf['fmri_data'].attrs.items():
                print(f"  {key}: {value}")
            
            # Inspect first few time slices
            print(f"\n=== Inspecting first {min(n_inspect_slices, fmri_data.shape[0])} time slices from HDF5 ===")
            for t in range(min(n_inspect_slices, fmri_data.shape[0])):
                time_slice = fmri_data[t, :, :, :]
                print(f"\nTime slice {t} from HDF5:")
                print(f"  Shape: {time_slice.shape}")
                print(f"  Min: {np.min(time_slice):.6f}")
                print(f"  Max: {np.max(time_slice):.6f}")
                print(f"  Mean: {np.mean(time_slice):.6f}")
                print(f"  Std: {np.std(time_slice):.6f}")
            
            # Load and print some statistics
            if 'statistics' in hf:
                stats = hf['statistics']
                print(f"\nGlobal statistics:")
                print(f"  Global mean: {stats['global_mean'][()]:.6f}")
                print(f"  Global std: {stats['global_std'][()]:.6f}")
                
                mean_ts = stats['mean_timeseries'][:]
                print(f"  Mean timeseries shape: {mean_ts.shape}")
                print(f"  First 5 timepoints mean: {mean_ts[:5]}")
                
    except Exception as e:
        print(f"Error inspecting HDF5 file: {e}")

# Example usage and batch processing function
def batch_convert_nifti_to_hdf5(input_directory, output_directory=None, pattern="*.nii*"):
    """
    Batch convert multiple NIfTI files to HDF5 format
    
    Parameters:
    input_directory (str): Directory containing NIfTI files
    output_directory (str): Output directory (uses input_directory if None)
    pattern (str): File pattern to match
    """
    
    input_path = Path(input_directory)
    output_path = Path(output_directory) if output_directory else input_path
    
    nifti_files = list(input_path.glob(pattern))
    print(f"Found {len(nifti_files)} NIfTI files matching pattern '{pattern}'")
    
    for i, nifti_file in enumerate(nifti_files):
        print(f"\n{'='*60}")
        print(f"Processing file {i+1}/{len(nifti_files)}: {nifti_file.name}")
        print(f"{'='*60}")
        
        output_file = output_path / f"{nifti_file.stem.replace('.nii', '')}_timeseries.h5"
        result = process_fmri_to_hdf5(str(nifti_file), str(output_file))
        
        if result:
            print(f"✓ Successfully converted: {nifti_file.name}")
        else:
            print(f"✗ Failed to convert: {nifti_file.name}")

if __name__ == "__main__":
    # Example usage
    nifti_file = "/home/cartorh/Desktop/Hobbies/Projects/Hackathons/HopHacks2025/sub-10159_task-bart_bold.nii.gz"  # Replace with your file path
    
    # Convert single file
    output_file = process_fmri_to_hdf5(nifti_file, n_print_slices=5)
    
    # Inspect the resulting HDF5 file
    if output_file:
        load_and_inspect_hdf5(output_file)
    
    # Example: Batch convert all NIfTI files in a directory
    # batch_convert_nifti_to_hdf5("path/to/nifti/directory", "path/to/output/directory")