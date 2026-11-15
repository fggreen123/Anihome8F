import bpy
import json
import math
import os
import shutil
from mathutils import Matrix, Vector, Quaternion

root = os.path.dirname(os.path.abspath(__file__))
body = bpy.data.objects['Grinner_Body']
rig = bpy.data.objects['Grinner_Rig']
assert sum(len(p.vertices)-2 for p in body.data.polygons) == 50000
assert all(v.groups for v in body.data.vertices)
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
for pb in rig.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
rig['TEST_POSE_ACTIVE'] = False
bpy.context.view_layer.update()

# Preserve the previous texture files before replacing the current delivery.
textures = os.path.join(root, 'Textures')
backup = body['replacement_backup']
old_textures = os.path.join(backup, 'Previous_Textures')
if os.path.isdir(textures) and not os.path.exists(old_textures):
    shutil.copytree(textures, old_textures)
os.makedirs(textures, exist_ok=True)
materials = set(m for m in body.data.materials if m)
images = {node.image for mat in materials for node in mat.node_tree.nodes
          if node.type == 'TEX_IMAGE' and node.image}
for material in list(bpy.data.materials):
    if material.name.startswith('tripo_mat_') and material not in materials and material.users == 0:
        bpy.data.materials.remove(material)
for image in list(bpy.data.images):
    if image.name.startswith('undead_zombie_3d_model_') and image not in images and image.users == 0:
        bpy.data.images.remove(image)
for image in images:
    role = next(r for r in ('basecolor','normal','roughness','metallic') if r in image.name)
    image.filepath_raw = os.path.join(textures, 'undead_zombie_3d_model_'+role+'.png')
    image.file_format = 'PNG'
    image.save()
    image.pack()

widgets = bpy.data.collections.get('Rig Widgets')
if widgets is None:
    widgets = bpy.data.collections.new('Rig Widgets')
    bpy.context.scene.collection.children.link(widgets)
widgets.hide_render = True
def widget(name, spherical):
    obj = bpy.data.objects.get(name)
    if obj:
        return obj
    verts, edges = [], []
    for plane in range(3 if spherical else 1):
        offset = len(verts)
        for i in range(24):
            x,y = math.cos(i*math.tau/24), math.sin(i*math.tau/24)
            verts.append((x,0,y) if plane==0 else (x,y,0) if plane==1 else (0,x,y))
            edges.append((offset+i,offset+(i+1)%24))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts,edges,[])
    obj = bpy.data.objects.new(name,mesh)
    widgets.objects.link(obj)
    obj.hide_render = True
    obj.hide_set(True)
    return obj
control_shape = widget('WGT_HandFoot_Control', True)
root_shape = widget('WGT_Root_Control', False)
for pb in rig.pose.bones:
    if pb.name.startswith('CTRL_') or pb.name=='Root':
        pb.custom_shape = root_shape if pb.name=='Root' else control_shape
        pb.use_custom_shape_bone_size = False
        size = .05 if ('Knee' in pb.name or 'Elbow' in pb.name) else .08
        if pb.name=='Root':
            size=.38
        pb.custom_shape_scale_xyz = (size,size,size)
        pb.lock_scale = (True,True,True)

bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=os.path.join(root,'Grinner_Rigged.fbx'),
    use_selection=True, object_types={'ARMATURE','MESH'}, add_leaf_bones=False,
    use_armature_deform_only=True, bake_anim=False, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, path_mode='COPY', embed_textures=True)
rig.data.collections['Deform - Unity'].is_visible = False
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        space=area.spaces.active
        space.overlay.show_overlays=True
        space.show_region_ui=True
        space.region_3d.view_perspective='ORTHO'
        space.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2)
        space.region_3d.view_location=Vector((0,0,1.07))
        space.region_3d.view_distance=3.8
        area.tag_redraw()
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='POSE')
for bone in rig.data.bones:
    bone.select=False
rig.data.bones['CTRL_Hand.R'].select=True
rig.data.bones.active=rig.data.bones['CTRL_Hand.R']
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'Grinner_Rigged.blend'),compress=True)
print(json.dumps({'saved':bpy.data.filepath,'triangles':50000,
    'body_material':body.active_material.name,'textures':len(images),
    'previous_version_backup':backup},ensure_ascii=False))
