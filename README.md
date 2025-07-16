# Anno1404To1800BuildingConverter

This tool automates the conversion of Anno 1404 buildings and props to Anno 1800 buildings and props.

You need to set up all the tools you usually need and the extracted Anno 1404 data and you need to know how to use JSON.
This will get you most of the way to a usable building in 1800, but there are some limitations and some things have to be set up yourself in the config.json or in the custom_maps folder.

## It supports:

1. Conversion of .gr2 meshes to .rdm, including rotation
2. Image conversion (diff, norm and ground diff) from the original .dds to .png, optional upscaling, then back to .dds with 3 levels of lod
3. Generation of .cfg with the correct data for the building like
    1. Center, Extent, MeshExtent, MeshRadius
    2. Ground decals with size, rotation and position. Supports complex multi part ground decals
    3. Referenced other models like props with size, rotation and position. (1404 does not use PROP elements, just FILE. So all props are referenced as FILE)
    4. Models with size, rotation and position. Supports wind ripples, diff, norm, mask metal, dye and height maps. (1404 has only diff and norm maps but you can create your own mask etc. maps and place them in the correct sub folders in custom_maps)
    5. Clothes, same as model. (The dynamic vertices have to be added in the config.json.)
    6. Particles, however they are only mapped from the 1404 effects to the ones from 1800 in the config.json. No Particle files can be converted.
    7. Collisions with size, rotation and position
4. Generation of the ifo file with the correct data for the building like
    1. BoundingBox
    2. BuildBlockers
    3. DamageImpacts
    4. InfoLayer
    5. TransporterSpawn
    6. FeedbackBlockers
    7. IntersectBoxes
    8. MeshBoundingBox

## It does not support:

1. Vegetation like trees as part of the model
2. Specific feedback unit paths (General feedback blockers for randomly spawning units do work)
3. Building animations

## Setup

1. Download/install the following tools
    1. [annotex](https://github.com/jakobharder/annotex/)
    2. [evegr2toobj](https://github.com/cppctamber/evegr2toobj)
    3. [rdm4](https://github.com/lukts30/rdm4)
    4. Blender (I use v3.5 but other 3.x that normally also work should be ok)
    5. Upscayl with the correct model (optional: only when setting SkipUpscale is false)
2. Update the paths in the config.json to your files/folders/programs


## How to convert a building

The Paths object in the config.json holds all the paths to the 1404 building that should be converted.
Each of the objects in that array 
1. The Path property is the relative path of a folder or .cfg file. If it is the path of a folder then all buildings in the folder or subfolders will be converted.
2. IsProp signals that the building(s) are a prop and will hide the build blocker and ground texture. This will still result in a .cfg that must be added via a FILE object and NOT a .prp.
3. DoNotAdjustToTerrainHeight simply sets AdjustToTerrainHeight to false. 1404 did not have sloped grounds so there are no foundations. All buildings are adjusted to terrain by default.

Once you have added the paths you want you have to save the file and then run the .exe in the command prompt.
A console window will open and you will see the buildings that have been converted as xml.
You can copy those directly from the command line into your assets under Values/Object/Variations.
The conversion has completed once you can see CONVERTER COMPLETED.

## MISSING CLOTH_DYNAMIC_VERTICES ...

If you see for example "MISSING CLOTH_DYNAMIC_VERTICES n_weaver_hut_cloth" then a model has a dynamic cloth model that was not configured in the config.json.
You can set the vertices to dynamic by adding a new property named after the printed model, in this example n_weaver_hut_cloth, the value will be a number array.
The array contains the vertex indices of the vertices that should be dynamic. 
To get those values you need to import the model into blender then mark the dynamic vertices in edit mode and then run the copySelectedVertexIndices.py script.
This adds the values to your clipboard and you can paste them into your n_weaver_hut_cloth array.

After that you need to convert the building again. The cloth should then by dynamically blow in the wind.

## MISSING PARTICLE_MAPPING ...

If you see for example "MISSING PARTICLE_MAPPING data\graphics\effects\particles\fountain_lightfoam.efc" then you have a building with a particle effect that is not configured in the config.json.
You can map a particle effect from 1404 to one from 1800 by adding it to the ParticleMappings object like this:
"data\\graphics\\effects\\particles\\fountain_lightfoam.efc": "data\\graphics\\effects\\particles\\water\\water_fountain01.rdp",
If a 1404 building uses the effect data\graphics\effects\particles\fountain_lightfoam.efc then in the converted 1800 version it will use the data\graphics\effects\particles\water\water_fountain01.rdp effect.

After that you need to convert the building again. The mapped 1800 particles should be visible.

## Setting up other maps

You can place other maps for your converted buildings that do not exist in 1404 into the custom_maps subfolders, for example mask maps for lighting.
When you run the converter the diff textures will be automatically copied to the right places. You can use those as a guide where to place your files.
For example to add lighting to the mosque at night you can find the s_mosque_diff.png in the custom_maps open and turn it into a mask, then save it as s_mosque_mask.png in the same folder.

After that you need to convert the building again. At night the mosque should light up according to your mask.
