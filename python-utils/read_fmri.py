import nibabel as nib
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path

def load_and_display_nifti(filepath, slice_coords=None):
    """
    Load and display a NIfTI file in standard 3-slice view (sagittal, coronal, axial)
    
    Parameters:
    filepath (str): Path to the .nii or .nii.gz file
    slice_coords (tuple): (x, y, z) coordinates for slice positions. 
                         If None, uses center of the image.
    """
    
    # Load the NIfTI file
    try:
        img = nib.load(filepath)
        data = img.get_fdata()
        print(f"Loaded image with shape: {data.shape}")
        print(f"Image data type: {data.dtype}")
        print(f"Voxel dimensions: {img.header.get_zooms()}")
    except Exception as e:
        print(f"Error loading file: {e}")
        return
    
    # Handle 4D data (take first volume if it's a time series)
    if len(data.shape) == 4:
        print(f"4D data detected with {data.shape[3]} volumes. Using first volume.")
        data = data[:, :, :, 0]
    
    # Get image dimensions
    x_size, y_size, z_size = data.shape
    
    # Set slice coordinates (center of brain if not specified)
    if slice_coords is None:
        x_slice = x_size // 2
        y_slice = y_size // 2  
        z_slice = z_size // 2
        print(f"Using center coordinates: ({x_slice}, {y_slice}, {z_slice})")
    else:
        x_slice, y_slice, z_slice = slice_coords
        print(f"Using specified coordinates: ({x_slice}, {y_slice}, {z_slice})")
    
    # Ensure coordinates are within bounds
    x_slice = max(0, min(x_slice, x_size - 1))
    y_slice = max(0, min(y_slice, y_size - 1))
    z_slice = max(0, min(z_slice, z_size - 1))
    
    # Extract the three orthogonal slices
    sagittal_slice = data[x_slice, :, :]  # YZ plane (left-right view)
    coronal_slice = data[:, y_slice, :]   # XZ plane (front-back view)
    axial_slice = data[:, :, z_slice]     # XY plane (top-bottom view)
    
    # Create the figure with subplots
    fig, axes = plt.subplots(1, 3, figsize=(15, 5))
    fig.suptitle(f'NIfTI Viewer: {Path(filepath).name}\nSlice coordinates: ({x_slice}, {y_slice}, {z_slice})', 
                 fontsize=14, fontweight='bold')
    
    # Display sagittal slice (YZ plane)
    im1 = axes[0].imshow(sagittal_slice.T, cmap='gray', origin='lower', aspect='auto')
    axes[0].set_title(f'Sagittal (X={x_slice})')
    axes[0].set_xlabel('Y (Anterior-Posterior)')
    axes[0].set_ylabel('Z (Superior-Inferior)')
    axes[0].axhline(y=z_slice, color='red', linestyle='--', alpha=0.7, linewidth=1)
    axes[0].axvline(x=y_slice, color='blue', linestyle='--', alpha=0.7, linewidth=1)
    
    # Display coronal slice (XZ plane)
    im2 = axes[1].imshow(coronal_slice.T, cmap='gray', origin='lower', aspect='auto')
    axes[1].set_title(f'Coronal (Y={y_slice})')
    axes[1].set_xlabel('X (Left-Right)')
    axes[1].set_ylabel('Z (Superior-Inferior)')
    axes[1].axhline(y=z_slice, color='red', linestyle='--', alpha=0.7, linewidth=1)
    axes[1].axvline(x=x_slice, color='green', linestyle='--', alpha=0.7, linewidth=1)
    
    # Display axial slice (XY plane)
    im3 = axes[2].imshow(axial_slice.T, cmap='gray', origin='lower', aspect='auto')
    axes[2].set_title(f'Axial (Z={z_slice})')
    axes[2].set_xlabel('X (Left-Right)')
    axes[2].set_ylabel('Y (Anterior-Posterior)')
    axes[2].axhline(y=y_slice, color='blue', linestyle='--', alpha=0.7, linewidth=1)
    axes[2].axvline(x=x_slice, color='green', linestyle='--', alpha=0.7, linewidth=1)
    
    # Add colorbars
    plt.colorbar(im1, ax=axes[0], shrink=0.8)
    plt.colorbar(im2, ax=axes[1], shrink=0.8)
    plt.colorbar(im3, ax=axes[2], shrink=0.8)
    
    # Adjust layout and display
    plt.tight_layout()
    plt.show()
    
    # Print some basic statistics
    print(f"\nImage statistics:")
    print(f"Min value: {np.min(data):.3f}")
    print(f"Max value: {np.max(data):.3f}")
    print(f"Mean value: {np.mean(data):.3f}")
    print(f"Std deviation: {np.std(data):.3f}")

def interactive_viewer(filepath):
    """
    Simple interactive viewer that allows you to specify slice coordinates
    """
    img = nib.load(filepath)
    data = img.get_fdata()
    
    if len(data.shape) == 4:
        data = data[:, :, :, 0]
    
    x_size, y_size, z_size = data.shape
    print(f"Image dimensions: {x_size} x {y_size} x {z_size}")
    print("Enter slice coordinates (or press Enter for center):")
    
    try:
        x_input = input(f"X coordinate (0-{x_size-1}): ").strip()
        y_input = input(f"Y coordinate (0-{y_size-1}): ").strip()
        z_input = input(f"Z coordinate (0-{z_size-1}): ").strip()
        
        x_slice = int(x_input) if x_input else x_size // 2
        y_slice = int(y_input) if y_input else y_size // 2
        z_slice = int(z_input) if z_input else z_size // 2
        
        load_and_display_nifti(filepath, (x_slice, y_slice, z_slice))
        
    except ValueError:
        print("Invalid input. Using center coordinates.")
        load_and_display_nifti(filepath)

# Example usage
if __name__ == "__main__":
    # Replace with your NIfTI file path
    nifti_file = "/home/cartorh/Desktop/Hobbies/Projects/Hackathons/HopHacks2025/sub-10159_task-bart_bold.nii.gz"  # or .nii.gz
    
    # Method 1: Display with center coordinates (0,0,0 relative to center)
    load_and_display_nifti(nifti_file)
    
    # Method 2: Display with specific coordinates
    # load_and_display_nifti(nifti_file, slice_coords=(50, 60, 40))
    
    # Method 3: Interactive mode
    # interactive_viewer(nifti_file)