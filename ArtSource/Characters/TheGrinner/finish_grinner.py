# Run within the shared build namespace.
# Basic deform skeleton, with continuous weights at knees, elbows and torso.
bpy.ops.object.select_all(action='DESELECT')
arm=bpy.data.armatures.new('Grinner Humanoid Skeleton')
rig=bpy.data.objects.new('TheGrinner_Rig',arm);char.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
defs=[('root',(0,0,0),(0,0,.18),None),('pelvis',(0,0,.98),(0,0,1.15),'root'),('spine',(0,0,1.15),(0,0,1.43),'pelvis'),('chest',(0,0,1.43),(0,0,1.66),'spine'),('neck',(0,0,1.66),(0,-.01,1.81),'chest'),('head',(0,-.01,1.81),(0,-.01,2.04),'neck')]
for s,side in [(-1,'R'),(1,'L')]:
    defs.extend([('clavicle.'+side,(0,0,1.64),(s*.25,0,1.64),'chest'),('upper_arm.'+side,(s*.25,0,1.64),(s*.355,-.01,1.32),'clavicle.'+side),('forearm.'+side,(s*.355,-.01,1.32),(s*.397,-.04,1.07),'upper_arm.'+side),('hand.'+side,(s*.397,-.04,1.07),(s*.41,-.06,.96),'forearm.'+side),('thigh.'+side,(s*.115,0,1.065),(s*.13,-.025,.54),'pelvis'),('shin.'+side,(s*.13,-.025,.54),(s*.135,.015,.16),'thigh.'+side),('foot.'+side,(s*.135,.015,.16),(s*.135,-.13,.065),'shin.'+side),('toe.'+side,(s*.135,-.13,.065),(s*.135,-.2,.065),'foot.'+side)])
for name,h,t,parent in defs:
    b=arm.edit_bones.new(name);b.head=h;b.tail=t
    if parent:b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;rig.display_type='WIRE'

def assign_blend(ob, group_names, weights):
    for g in list(ob.vertex_groups):ob.vertex_groups.remove(g)
    groups={n:ob.vertex_groups.new(name=n) for n in group_names}
    for v in ob.data.vertices:
        p=ob.matrix_world@v.co
        for name,w in weights(p).items():
            if w>.0001:groups[name].add([v.index],w,'REPLACE')

def blend_z(z,z0,z1,lower,upper):
    t=max(0,min(1,(z-z0)/(z1-z0)));t=t*t*(3-2*t)
    return {lower:1-t,upper:t}
for ob in parts:
    if ob.name.startswith('Trouser leg'):
        side='R' if ' R' in ob.name else 'L'
        assign_blend(ob,['thigh.'+side,'shin.'+side],lambda p,s=side:blend_z(p.z,.43,.64,'shin.'+s,'thigh.'+s))
    elif ob.name.startswith('Wrinkled jacket sleeve'):
        side='R' if ' R' in ob.name else 'L'
        assign_blend(ob,['upper_arm.'+side,'forearm.'+side],lambda p,s=side:blend_z(p.z,1.24,1.41,'forearm.'+s,'upper_arm.'+s))
    elif ob.name.startswith(('Tattered open','Stained ribbed','Coat front','Shirt gathered','Oxidized jacket')):
        def torso_weights(p):
            if p.z<1.32:return blend_z(p.z,1.1,1.32,'pelvis','spine')
            return blend_z(p.z,1.38,1.61,'spine','chest')
        assign_blend(ob,['pelvis','spine','chest'],torso_weights)
    ob.select_set(False)

# Keep weapon independently editable and attached through a hand bone.
weapons=[ob for ob in parts if ob.name.startswith('Cleaver')]
bodyparts=[ob for ob in parts if ob not in weapons]
def join_parts(obs,name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join()
    ob=bpy.context.object;ob.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Orient all normals outward after joining clockwise loft strips.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    mod=ob.modifiers.new('Humanoid deform','ARMATURE');mod.object=rig
    ob.parent=rig
    return ob
character=join_parts(bodyparts,'TheGrinner_Body')
weapon=join_parts(weapons,'TheGrinner_Cleaver')
rig['character']='The Grinner / Anihome8F idea 21'
rig['reference_height_m']=2.13
rig['notes']='Reference-inspired model. Basic 22-bone skeleton; no production walk animation or facial rig.'

# Preview-only studio: not exported into Unity.
groundmat=material('Studio | charcoal',(.022,.026,.028),(.038,.043,.046),4,.95,.03)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003));ground=bpy.context.object;ground.name='Studio floor';place(ground,studio);ground.data.materials.append(groundmat)
def point_at(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,power,color,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=loc;point_at(ob,(0,0,1.1));return ob
area('Key | warm softbox',(-2,-3,3.8),480,(1,.84,.66),2.5)
area('Fill | cold', (2,-2,2.3),220,(.62,.76,1),2)
area('Rim | pale gold',(0,1.8,3.3),650,(1,.82,.6),1.8)
area('Face catchlight',(0,-2.5,2.4),70,(.85,.9,1),1)
world=bpy.data.worlds.new('Grinner studio world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.065,.08,1);world.node_tree.nodes['Background'].inputs[1].default_value=.3;scene.world=world
camdata=bpy.data.cameras.new('Character portrait');cam=bpy.data.objects.new('Character portrait',camdata);studio.objects.link(cam);cam.location=(.9,-4.2,2);point_at(cam,(0,0,1.09));camdata.type='ORTHO';camdata.ortho_scale=2.5;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=1150;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(SOURCE,'TheGrinner_FullBody.png')
scene.render.film_transparent=False
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.region_3d.view_distance=3.2
            ar.spaces.active.region_3d.view_location=Vector((0,0,1.08))
            ar.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
            ar.spaces.active.shading.type='MATERIAL'
# Save before render or baking, keeping all procedural materials editable.
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'TheGrinner.blend'))
print('RIG & STUDIO COMPLETE',len(arm.bones),'bones',len(character.data.vertices),'body vertices')
