import bpy
import json
import math
import os
import numpy as np
from mathutils import Vector

OUT = r'C:\Users\top09\Desktop\unity game\Anihome8F\ArtSource\Anomalies\AN_21_MysteriousMonster'
body = bpy.data.objects['Grinner_Body']
S = 2.13 / .979248046875
def P(x, y, z):
    return Vector((x*S, y*S, z*S))

bpy.ops.object.select_all(action='DESELECT')
arm_data = bpy.data.armatures.new('Grinner_Skeleton')
rig = bpy.data.objects.new('Grinner_Rig', arm_data)
body.users_collection[0].objects.link(rig)
rig.show_in_front = True
arm_data.display_type = 'OCTAHEDRAL'
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name, head, tail, parent=None, connected=False, deform=True):
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = P(*head), P(*tail)
    if parent:
        b.parent = arm_data.edit_bones[parent]
        b.use_connect = connected
    b.use_deform = deform
    b.align_roll(Vector((0, 1, 0)))
    return b

bone('Root', (0,0,0), (0,0,.08), deform=False)
bone('Hips', (.014,.03,.48), (.014,.03,.55), 'Root')
bone('Spine', (.014,.03,.55), (.014,.03,.64), 'Hips', True)
bone('Chest', (.014,.03,.64), (.015,.03,.73), 'Spine', True)
bone('UpperChest', (.015,.03,.73), (.018,.028,.79), 'Chest', True)
bone('Neck', (.018,.028,.79), (.025,.02,.839), 'UpperChest', True)
bone('Head', (.025,.02,.839), (.027,.022,.951), 'Neck', True)

landmarks = {
    'R': {'shoulder':(-.090,.028,.772), 'elbow':(-.157,.041,.650),
          'wrist':(-.191,.006,.515), 'hand':(-.190,-.022,.468),
          'hip':(-.058,.03,.481), 'knee':(-.070,.003,.278),
          'ankle':(-.083,.031,.078), 'ball':(-.083,-.060,.028),
          'toe':(-.083,-.101,.022)},
    'L': {'shoulder':(.123,.028,.773), 'elbow':(.185,.045,.650),
          'wrist':(.229,.003,.529), 'hand':(.214,-.025,.477),
          'hip':(.084,.03,.481), 'knee':(.119,.008,.282),
          'ankle':(.133,.034,.080), 'ball':(.136,-.060,.028),
          'toe':(.136,-.103,.022)},
}
for side, p in landmarks.items():
    bone('Shoulder.'+side, (.018,.028,.782), p['shoulder'], 'UpperChest')
    bone('UpperArm.'+side, p['shoulder'], p['elbow'], 'Shoulder.'+side, True)
    bone('LowerArm.'+side, p['elbow'], p['wrist'], 'UpperArm.'+side, True)
    bone('Hand.'+side, p['wrist'], p['hand'], 'LowerArm.'+side, True)
    bone('UpperLeg.'+side, p['hip'], p['knee'], 'Hips')
    bone('LowerLeg.'+side, p['knee'], p['ankle'], 'UpperLeg.'+side, True)
    bone('Foot.'+side, p['ankle'], p['ball'], 'LowerLeg.'+side, True)
    bone('Toes.'+side, p['ball'], p['toe'], 'Foot.'+side, True)
bone('Weapon', (-.190,-.022,.468), (-.239,-.020,.16), 'Hand.R', deform=False)
bpy.ops.object.mode_set(mode='OBJECT')

# Bone heat is run only on the deform skeleton, without controller or weapon bones.
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
result = bpy.ops.object.parent_set(type='ARMATURE_AUTO')
if not any(m.type=='ARMATURE' and m.object==rig for m in body.modifiers):
    raise RuntimeError('Armature binding failed')
if not body.vertex_groups:
    raise RuntimeError('Bone heat did not create weights')

# Override rigid surfaces and normalize; keep at most four influences for games.
arm_data.bones['Weapon'].use_deform = True
if not body.vertex_groups.get('Weapon'):
    body.vertex_groups.new(name='Weapon')
deform_names = {b.name for b in arm_data.bones if b.use_deform}
groups = {g.index:g.name for g in body.vertex_groups}
def rigid(indices, target):
    if not indices:
        return
    for g in body.vertex_groups:
        g.remove(indices)
    body.vertex_groups[target].add(indices, 1.0, 'REPLACE')

weapon = [v.index for v in body.data.vertices if v.co.x/S < -.145 and v.co.z/S < .434]
hat_head = [v.index for v in body.data.vertices if v.co.z/S > .854]
rigid(weapon, 'Weapon')
rigid(hat_head, 'Head')

bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
unweighted = [v.index for v in body.data.vertices if sum(g.weight for g in v.groups)<.999]
if unweighted:
    # Tiny isolated heat-solver failures use the closest deform segment.
    segments = [(b.name,b.head_local.copy(),b.tail_local.copy()) for b in arm_data.bones if b.use_deform]
    for index in unweighted:
        v = body.data.vertices[index].co
        def distance(item):
            _, a, b = item
            d = b-a
            q = a+d*max(0,min(1,(v-a).dot(d)/d.length_squared))
            return (v-q).length_squared
        name = min(segments,key=distance)[0]
        rigid([index],name)

# Controls are separate non-deforming bones; FBX export omits them.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
for side,p in landmarks.items():
    bone('CTRL_Hand.'+side,p['wrist'],p['hand'],'Root',deform=False)
    elbow = p['elbow']
    bone('CTRL_Elbow.'+side,(elbow[0],.34,elbow[2]),(elbow[0],.34,elbow[2]+.045),'Root',deform=False)
    bone('CTRL_Foot.'+side,p['ankle'],p['ball'],'Root',deform=False)
    knee = p['knee']
    bone('CTRL_Knee.'+side,(knee[0],-.33,knee[2]),(knee[0],-.33,knee[2]+.045),'Root',deform=False)
bpy.ops.object.mode_set(mode='OBJECT')
deform_collection=arm_data.collections.new('Deform - Unity')
control_collection=arm_data.collections.new('Controls - Hands and Feet')
body_collection=arm_data.collections.new('Controls - Body')
for b in arm_data.bones:
    if b.name.startswith('CTRL_'):
        control_collection.assign(b)
        b.color.palette='THEME04'
    elif b.name in ('Root','Hips','Spine','Chest','UpperChest','Neck','Head','Weapon'):
        body_collection.assign(b)
        b.color.palette='THEME03'
    else:
        deform_collection.assign(b)
        b.color.palette='THEME01' if b.name.endswith('.L') else 'THEME02'

# Calibrate each pole angle against the rest bend to avoid a snap when IK enables.
for side in ('L','R'):
    for limb,base,lower,target,pole in (
        ('Arm','UpperArm.','LowerArm.','CTRL_Hand.','CTRL_Elbow.'),
        ('Leg','UpperLeg.','LowerLeg.','CTRL_Foot.','CTRL_Knee.'),
    ):
        pb=rig.pose.bones[lower+side]
        c=pb.constraints.new('IK')
        c.name=limb+' IK'
        c.target=rig;c.subtarget=target+side
        c.pole_target=rig;c.pole_subtarget=pole+side
        c.chain_count=2;c.use_stretch=False;c.iterations=200
        rest_joint=arm_data.bones[lower+side].head_local.copy()
        best=(float('inf'),0)
        for i in range(128):
            angle=-math.pi+2*math.pi*i/128
            c.pole_angle=angle
            bpy.context.view_layer.update()
            err=(pb.head-rest_joint).length_squared
            if err<best[0]:best=(err,angle)
        for i in range(-10,11):
            angle=best[1]+i*(2*math.pi/128)/10
            c.pole_angle=angle
            bpy.context.view_layer.update()
            err=(pb.head-rest_joint).length_squared
            if err<best[0]:best=(err,angle)
        c.pole_angle=best[1]
        property_name=limb+'_IK_'+side
        rig[property_name]=1.0
        rig.id_properties_ui(property_name).update(min=0,max=1,description='1: move the hand/foot control; 0: rotate the deform bones (FK)')
        drv=c.driver_add('influence').driver
        drv.type='AVERAGE';v=drv.variables.new();v.name='influence';v.type='SINGLE_PROP'
        v.targets[0].id=rig;v.targets[0].data_path='["'+property_name+'"]'
        pb.ik_stretch=0
        rig.pose.bones[base+side].ik_stretch=0
        if limb=='Leg':
            rot=rig.pose.bones['Foot.'+side].constraints.new('COPY_ROTATION')
            rot.name='Foot orientation';rot.target=rig;rot.subtarget=target+side
            rot.target_space='WORLD';rot.owner_space='WORLD'

for pb in rig.pose.bones:
    pb.rotation_mode='XYZ'
    if pb.name.startswith('CTRL_Elbow') or pb.name.startswith('CTRL_Knee'):
        pb.lock_rotation=(True,True,True);pb.lock_scale=(True,True,True)

rig['Rig_Instructions']='Pose Mode: move CTRL_Hand / CTRL_Foot. CTRL_Elbow / CTRL_Knee set bend direction. Root moves the whole character. Arm_IK / Leg_IK properties blend to FK.'
rig['Source']='Tripo character; neutral smiling face; fingers and coat are part of the fused mesh.'
body['rigged_triangle_count']=len(body.data.polygons)
body['weapon_vertex_count']=len(weapon)
for m in body.modifiers:
    if m.type=='ARMATURE':m.use_deform_preserve_volume=True
bpy.context.view_layer.update()
report={'vertices':len(body.data.vertices),'triangles':len(body.data.polygons),
        'bones':len(arm_data.bones),'deform_bones':len(deform_names),
        'weapon_vertices':len(weapon),'fallback_vertices':len(unweighted),
        'unweighted_vertices':sum(1 for v in body.data.vertices if not v.groups),
        'max_influences':max(len(v.groups) for v in body.data.vertices)}
with open(os.path.join(OUT,'rig_report.json'),'w',encoding='utf-8') as f:json.dump(report,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Grinner_Rigged.blend'),compress=True)
print(json.dumps(report))
