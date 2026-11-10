import bpy, os, json, traceback
ROOT=r'C:\Users\top09\Desktop\unity game\Anihome8F\ArtSource\Characters\TheGrinner'
ns={}
try:
    for name in ['build_grinner.py','detail_grinner.py','finish_grinner.py']:
        p=os.path.join(ROOT,name)
        exec(compile(open(p,encoding='utf-8').read(),name,'exec'),ns)
    p=os.path.join(ROOT,'export_grinner.py')
    code=open(p,encoding='utf-8').read().replace("bpy.app.timers.register(run_export,first_interval=1)","run_export()")
    code=code.replace("report('QUEUED')",'')
    exec(compile(code,p,'exec'),ns)
    assert json.load(open(os.path.join(ROOT,'export_status.json')))['stage']=='DONE'
    sc=ns['scene'];cam=ns['cam']
    sc.cycles.samples=24
    bpy.ops.render.render(write_still=True)
    cam.location=(.7,-2,2.05);ns['point_at'](cam,(0,-.025,1.93));cam.data.ortho_scale=.58
    sc.render.resolution_x=1000;sc.render.resolution_y=1000;sc.render.filepath=os.path.join(ROOT,'TheGrinner_Head.png')
    bpy.ops.render.render(write_still=True)
    cam.location=(.9,-4.2,2);ns['point_at'](cam,(0,0,1.09));cam.data.ortho_scale=2.5
    sc.render.resolution_x=900;sc.render.resolution_y=1150;sc.render.filepath=os.path.join(ROOT,'TheGrinner_FullBody.png')
    ns['rig'].show_in_front=False
    for screen in bpy.data.screens:
        for a in screen.areas:
            if a.type=='VIEW_3D':a.spaces.active.overlay.show_overlays=False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'TheGrinner.blend'))
    with open(os.path.join(ROOT,'final_validation.json'),'w') as f:json.dump({'status':'COMPLETE','renders':2,'export_status':'DONE'},f)
except Exception:
    with open(os.path.join(ROOT,'final_validation.json'),'w') as f:json.dump({'status':'ERROR','traceback':traceback.format_exc()},f)
    raise
