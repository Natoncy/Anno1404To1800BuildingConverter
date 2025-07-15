# Use in blender to get a list of vertex indices for Constants.CLOTH_DYNAMIC_VERTICES

import bpy
import bmesh

# Get the active object
obj = bpy.context.active_object

# Switch to edit mode and get bmesh data
bpy.ops.object.mode_set(mode='EDIT')
bm = bmesh.from_edit_mesh(obj.data)

# Get selected vertex indices
selected_verts = [v.index for v in bm.verts if v.select]

# Print or copy as string
output = ', '.join(str(i) for i in selected_verts)
print("Selected Vertex Indices:", output)

# Optional: copy to clipboard
bpy.context.window_manager.clipboard = output