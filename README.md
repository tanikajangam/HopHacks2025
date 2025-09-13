# VR fMRI Viewer

## Data

Data is imported in .nii (or .nii.gz if zipped). Then converted to h5 format for compatibility with C# and stored in the data folder. The Python utils for this are in the python-utils folder


## Cubes In Unity

1) Run `nii2unity` from python-utils to convert the fmri.nii file into a bunch of files that unity can use. This will create a unity_output folder where the data files are stored.
2) Take the data and put them in the unity project under `Assets/StreamingAssets/`.
3) Put the script [FMRICubeVisualizer.cs](c-utils/FMRICubeVisualizer.cs) from c-utils into `Assets/Scripts` then drag it over the object you want to color based on the fmri data.