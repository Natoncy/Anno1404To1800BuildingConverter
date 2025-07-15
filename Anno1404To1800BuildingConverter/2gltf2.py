#
# Imports
#

import bpy
import os
import sys

#
# Globals
#

#
# Functions
#

current_directory = os.getcwd()

obj_filename = None
vertices = []

for i, arg in enumerate(sys.argv):
    if arg.lower().endswith(".obj"):
        obj_filename = arg
        vertices = [int(x) for x in sys.argv[i+1:] if x.isdigit()]
        break

root, current_extension = os.path.splitext(obj_filename)
current_basename = os.path.basename(root)

bpy.ops.wm.read_factory_settings(use_empty=True)

bpy.ops.import_scene.obj(filepath=obj_filename)  

i = 0
for obj in bpy.context.selected_objects:
    if obj.type == 'MESH':
        obj.data.calc_tangents()
        
        if(len(vertices) > 0):
            
            mesh = obj.data

            # Add vertex color attribute
            if "VertexColor" not in mesh.color_attributes:
                mesh.color_attributes.new(name="VertexColor", domain='POINT', type='FLOAT_COLOR')
            color_layer = mesh.color_attributes["VertexColor"]

            # Assign colors based on vertex index
            for i, color in enumerate(color_layer.data):
                if i in vertices:
                    color.color = (0.5, 0.5, 0.5, 1.0) # dynamic
                else:
                    color.color = (1.0, 1.0, 1.0, 1.0) # fixed

            # Enter edit mode to cleanup geometry
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')

            # Merge by distance
            bpy.ops.mesh.remove_doubles()

            # Switch to face select and shade smooth
            bpy.ops.mesh.select_mode(type="FACE")
            bpy.ops.mesh.faces_shade_smooth()

            # Back to object mode
            bpy.ops.object.mode_set(mode='OBJECT')

            i += 1

export_file = current_directory + "/" + current_basename + ".glb"
print("Writing: '" + export_file + "'")
bpy.ops.export_scene.gltf(filepath=export_file, export_format='GLB', export_tangents=True)
