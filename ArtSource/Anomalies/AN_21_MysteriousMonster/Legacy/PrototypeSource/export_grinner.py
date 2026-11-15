# Run after finish_grinner.py. Bake procedural materials for portable game assets.
import json, traceback
TEX=os.path.join(EXPORT,'Textures');os.makedirs(TEX,exist_ok=True)
status_path=os.path.join(SOURCE,'export_status.json')
def report(stage,**extra):
    with open(status_path,'w',encoding='utf-8') as f:json.dump(dict(stage=stage,**extra),f,indent=2)

def bake_asset(ob,label,size):
    report('Unwrapping '+label)
    bpy.ops.object.select_all(action='DESELECT');ob.hide_set(False);ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.003,area_weight=.25,correct_aspect=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    original_mats=list(ob.data.materials);original_indices=[p.material_index for p in ob.data.polygons]
    materials=list(set(m for m in original_mats if m))
    maps={}
    for kind,baketype in [('BaseColor','DIFFUSE'),('Normal','NORMAL'),('Roughness','ROUGHNESS')]:
        report('Baking '+label+' '+kind)
        res=size if kind!='Roughness' else min(size,2048)
        im=bpy.data.images.new('Grinner_'+label+'_'+kind,width=res,height=res,alpha=False)
        im.colorspace_settings.name='sRGB' if kind=='BaseColor' else 'Non-Color'
        for m in materials:
            n=m.node_tree.nodes.new('ShaderNodeTexImage');n.name='BakeTarget_'+kind;n.image=im;m.node_tree.nodes.active=n
        scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False;scene.render.bake.use_pass_color=True
        scene.render.bake.margin=12;scene.render.bake.use_selected_to_active=False
        bpy.ops.object.bake(type=baketype)
        im.filepath_raw=os.path.join(TEX,'Grinner_'+label+'_'+kind+'.png');im.file_format='PNG';im.save()
        maps[kind]=im
    mat=bpy.data.materials.new('Grinner_'+label+'_Baked');mat.use_nodes=True
    n=mat.node_tree.nodes;l=mat.node_tree.links;bs=n.get('Principled BSDF')
    for kind,im in maps.items():
        t=n.new('ShaderNodeTexImage');t.image=im;t.label=kind
        if kind=='BaseColor':l.new(t.outputs['Color'],bs.inputs['Base Color'])
        elif kind=='Roughness':l.new(t.outputs['Color'],bs.inputs['Roughness'])
        else:
            norm=n.new('ShaderNodeNormalMap');l.new(t.outputs['Color'],norm.inputs['Color']);l.new(norm.outputs[0],bs.inputs['Normal'])
    bs.inputs['Metallic'].default_value=.25 if label=='Weapon' else 0
    return original_mats,original_indices,mat

def run_export():
    try:
        scene.cycles.samples=4
        body_data=bake_asset(character,'Body',4096)
        weapon_data=bake_asset(weapon,'Weapon',1024)
        for ob,data in [(character,body_data),(weapon,weapon_data)]:
            ob.data.materials.clear();ob.data.materials.append(data[2])
            for p in ob.data.polygons:p.material_index=0
        report('Exporting FBX and GLB')
        bpy.ops.object.select_all(action='DESELECT')
        for ob in [character,weapon,rig]:ob.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,'TheGrinner.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='COPY',embed_textures=True,use_mesh_modifiers=True)
        bpy.ops.export_scene.gltf(filepath=os.path.join(SOURCE,'TheGrinner.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=False,export_yup=True)
        # Save a portable baked version alongside the editable source.
        for im in bpy.data.images:
            if im.name.startswith('Grinner_') and im.has_data:im.pack()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'TheGrinner_Baked.blend'),copy=True)
        for ob,data in [(character,body_data),(weapon,weapon_data)]:
            ob.data.materials.clear()
            for m in data[0]:ob.data.materials.append(m)
            for p,i in zip(ob.data.polygons,data[1]):p.material_index=i
        scene.cycles.samples=32
        refpath=r'C:\Users\top09\AppData\Local\Temp\codex-clipboard-5b08b09c-2913-4229-929f-29d3f23c15c4.png'
        ref=bpy.data.images.load(refpath,check_existing=True);ref.name='REFERENCE | user character concept';ref.pack()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'TheGrinner.blend'))
        stats={}
        for ob in [character,weapon]:
            ob.data.calc_loop_triangles()
            stats[ob.name]={'vertices':len(ob.data.vertices),'triangles':len(ob.data.loop_triangles),'uv_layers':len(ob.data.uv_layers),'weighted_vertices':sum(bool(v.groups) for v in ob.data.vertices)}
        report('DONE',meshes=stats,bones=len(arm.bones),fbx=os.path.join(EXPORT,'TheGrinner.fbx'),blend=os.path.join(SOURCE,'TheGrinner.blend'))
    except Exception:
        report('ERROR',traceback=traceback.format_exc())
    return None

bpy.app.timers.register(run_export,first_interval=1)
report('QUEUED')
