import bpy, math, random, os
from mathutils import Vector
from math import sin, cos, pi

random.seed(21)
ROOT = r'C:\Users\top09\Desktop\unity game\Anihome8F'
SOURCE = os.path.join(ROOT, 'ArtSource', 'Characters', 'TheGrinner')
EXPORT = os.path.join(ROOT, 'Assets', 'Characters', 'TheGrinner')
os.makedirs(SOURCE, exist_ok=True)
os.makedirs(EXPORT, exist_ok=True)
scene = bpy.data.scenes.new('The Grinner | Character Studio')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
char = bpy.data.collections.new('GRINNER | export character')
studio = bpy.data.collections.new('STUDIO | preview only')
scene.collection.children.link(char)
scene.collection.children.link(studio)
parts = []

def place(obj, collection=char):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def material(name, dark, light, scale, roughness=.85, bump=.12, metallic=0, stains=False, cracks=False):
    m=bpy.data.materials.new(name); m.use_nodes=True
    m.diffuse_color=(*light,1)
    n=m.node_tree.nodes; l=m.node_tree.links; n.clear()
    out=n.new('ShaderNodeOutputMaterial'); bs=n.new('ShaderNodeBsdfPrincipled')
    l.new(bs.outputs['BSDF'],out.inputs['Surface'])
    bs.inputs['Roughness'].default_value=roughness
    bs.inputs['Metallic'].default_value=metallic
    tex=n.new('ShaderNodeTexCoord')
    noise=n.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=scale; noise.inputs['Detail'].default_value=5; noise.inputs['Roughness'].default_value=.76
    l.new(tex.outputs['Generated'],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].position=.25; ramp.color_ramp.elements[0].color=(*dark,1)
    ramp.color_ramp.elements[1].position=.76; ramp.color_ramp.elements[1].color=(*light,1)
    l.new(noise.outputs['Fac'],ramp.inputs[0]); color=ramp.outputs['Color']
    if stains:
        stain=n.new('ShaderNodeTexNoise'); stain.inputs['Scale'].default_value=scale*.55; stain.inputs['Detail'].default_value=3.5; stain.inputs['Distortion'].default_value=2
        l.new(tex.outputs['Generated'],stain.inputs['Vector'])
        sr=n.new('ShaderNodeValToRGB'); sr.color_ramp.elements[0].position=.59; sr.color_ramp.elements[1].position=.73
        l.new(stain.outputs['Fac'],sr.inputs[0])
        mix=n.new('ShaderNodeMixRGB'); mix.inputs[2].default_value=(.095,.017,.012,1)
        l.new(sr.outputs[0],mix.inputs[0]); l.new(color,mix.inputs[1]); color=mix.outputs[0]
    if cracks:
        vor=n.new('ShaderNodeTexVoronoi'); vor.feature='DISTANCE_TO_EDGE'; vor.inputs['Scale'].default_value=95
        l.new(tex.outputs['Generated'],vor.inputs['Vector'])
        cr=n.new('ShaderNodeValToRGB'); cr.color_ramp.elements[0].position=.012; cr.color_ramp.elements[0].color=(.11,.072,.057,1); cr.color_ramp.elements[1].position=.045
        l.new(vor.outputs['Distance'],cr.inputs[0])
        mul=n.new('ShaderNodeMixRGB'); mul.blend_type='MULTIPLY'; mul.inputs[0].default_value=.18
        l.new(color,mul.inputs[1]); l.new(cr.outputs[0],mul.inputs[2]); color=mul.outputs[0]
    grain=n.new('ShaderNodeTexNoise'); grain.inputs['Scale'].default_value=260; grain.inputs['Detail'].default_value=2
    l.new(tex.outputs['Generated'],grain.inputs['Vector'])
    grit=n.new('ShaderNodeMixRGB'); grit.blend_type='MULTIPLY'; grit.inputs[0].default_value=.65
    l.new(color,grit.inputs[1]); l.new(grain.outputs['Fac'],grit.inputs[2])
    l.new(grit.outputs[0],bs.inputs['Base Color'])
    fine=n.new('ShaderNodeTexNoise'); fine.inputs['Scale'].default_value=150; fine.inputs['Detail'].default_value=3
    l.new(tex.outputs['Generated'],fine.inputs['Vector'])
    b=n.new('ShaderNodeBump'); b.inputs['Strength'].default_value=bump; b.inputs['Distance'].default_value=.003
    l.new(fine.outputs['Fac'],b.inputs['Height']); l.new(b.outputs[0],bs.inputs['Normal'])
    return m

skin=material('Grinner | corpse skin',(.12,.105,.09),(.39,.36,.30),7,.76,.2,stains=True,cracks=True)
cloth=material('Grinner | rotten olive canvas',(.006,.005,.0035),(.055,.043,.026),8,.93,.34,stains=True)
edge=material('Grinner | exposed worn threads',(.024,.018,.01),(.13,.095,.05),14,.94,.28)
shirt=material('Grinner | stained undershirt',(.027,.021,.014),(.18,.145,.10),6,.9,.25,stains=True)
pants=material('Grinner | weathered trousers',(.004,.0035,.0025),(.036,.025,.015),8,.95,.3,stains=True)
leather=material('Grinner | cracked boots',(.012,.009,.008),(.067,.043,.026),5,.65,.23,cracks=True)
rust=material('Grinner | rusted cleaver',(.039,.02,.014),(.24,.15,.095),9,.52,.23,.65,stains=True)
iron=material('Grinner | old hardware',(.025,.022,.018),(.13,.11,.07),15,.5,.12,.75)
teeth=material('Grinner | decayed ivory',(.13,.087,.036),(.64,.49,.25),9,.4,.14)
gums=material('Grinner | dried gums',(.035,.005,.004),(.16,.037,.025),9,.43,.15)
socket=material('Grinner | deep facial recess',(.004,.0025,.002),(.022,.012,.009),7,.86,.08)
eyes=material('Grinner | opaque cataracts',(.22,.2,.18),(.66,.62,.55),5,.18,.06)

def mesh(name, verts, faces, mat, bone=None, sub=0):
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    ob=bpy.data.objects.new(name,me); char.objects.link(ob); ob.data.materials.append(mat)
    for p in me.polygons:p.use_smooth=True
    if bone:
        g=ob.vertex_groups.new(name=bone); g.add(list(range(len(me.vertices))),1,'REPLACE')
    if sub:
        mod=ob.modifiers.new('Surface refinement','SUBSURF');mod.levels=sub
        bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False)
    parts.append(ob)
    return ob

def ell(name, loc, size, mat, bone=None, seg=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc)
    ob=bpy.context.object;ob.name=name;ob.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    place(ob);ob.data.materials.append(mat)
    for p in ob.data.polygons:p.use_smooth=True
    if bone:ob.vertex_groups.new(name=bone).add(list(range(len(ob.data.vertices))),1,'REPLACE')
    parts.append(ob);ob.select_set(False)
    return ob

def tube(name, points, radius, mat, bone=None, sides=6):
    vv=[];ff=[]
    for i,p in enumerate(points):
        p=Vector(p);t=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        t.normalize(); ref=Vector((0,0,1)) if abs(t.z)<.95 else Vector((0,1,0));u=t.cross(ref).normalized();v=t.cross(u).normalized()
        r=radius[i] if isinstance(radius,list) else radius
        for j in range(sides):vv.append(p+r*(u*cos(2*pi*j/sides)+v*sin(2*pi*j/sides)))
    for i in range(len(points)-1):
        for j in range(sides): a=i*sides+j;b=i*sides+(j+1)%sides;ff.append((a,b,b+sides,a+sides))
    ff.extend([tuple(reversed(range(sides))),tuple(range((len(points)-1)*sides,len(points)*sides))])
    return mesh(name,vv,ff,mat,bone)

def loft(name, rings, mat, bone=None, count=32, wrinkles=0, cap=True):
    vv=[];ff=[]
    if wrinkles > .04:
        dense=[]
        for a,b in zip(rings,rings[1:]):
            for q in range(3):
                t=q/3; dense.append(tuple(aa*(1-t)+bb*t for aa,bb in zip(a,b)))
        dense.append(rings[-1]);rings=dense
    for k,(x,y,z,rx,ry) in enumerate(rings):
        for j in range(count):
            a=2*pi*j/count;w=1+wrinkles*(.5*sin(a*5+k*1.9)+.3*cos(a*9-k*2.4)+.2*sin(a*13+k*.7))
            vv.append((x+rx*sin(a)*w,y-ry*cos(a)*w,z))
    for k in range(len(rings)-1):
        for j in range(count): a=k*count+j;b=k*count+(j+1)%count;ff.append((a,b,b+count,a+count))
    if cap:ff.extend([tuple(reversed(range(count))),tuple(range((len(rings)-1)*count,len(rings)*count))])
    return mesh(name,vv,ff,mat,bone)

# Layered torso, open jacket, and cloth seams.
body=loft('Stained ribbed undershirt',[(0,0,1.07,.16,.115),(0,0,1.12,.18,.125),(0,0,1.2,.17,.12),(0,0,1.32,.18,.125),(0,0,1.44,.205,.13),(0,0,1.56,.225,.125),(0,0,1.64,.245,.115),(0,0,1.69,.17,.085),(0,0,1.72,.085,.065)],shirt,'spine',48,.045)
for k in range(0):
    z=1.13+k*.038
    tube('Shirt gathered fold',[(x,-(.13+random.uniform(0,.007)),z+.008*sin(x*35+k)) for x in [-.14,-.1,-.05,0,.05,.1,.14]],.0028,shirt,'spine')
neck=loft('Corded long neck',[(0,0,1.65,.09,.07),(0,-.005,1.73,.073,.066),(0,-.018,1.82,.069,.065),(0,-.01,1.86,.08,.074)],skin,'neck',32,.04)
for s in [-1,1]:tube('Neck tendon',[(s*.055,-.055,1.7),(s*.035,-.077,1.76),(s*.07,-.071,1.84)],[.009,.007,.009],skin,'neck',8)
coatR=[(1.005,.208,.142),(1.055,.215,.15),(1.12,.207,.148),(1.2,.2,.143),(1.29,.211,.145),(1.38,.223,.151),(1.48,.243,.157),(1.58,.26,.153),(1.65,.267,.133),(1.7,.186,.103),(1.74,.095,.078)]
vv=[];ff=[];N=64
for k,(z,rx,ry) in enumerate(coatR):
    for j in range(N+1):
        a=.36+j/N*(2*pi-.72);wave=1+.025*sin(9*a+k*2)+.018*sin(17*a-k)
        zz=z
        if k==0:zz+=.018*sin(19*a)+.02*sin(31*a)-.016*random.random()
        vv.append((sin(a)*rx*wave,-cos(a)*ry*wave,zz))
for k in range(len(coatR)-1):
    for j in range(N):
        # Small worn holes near the lower coat and back.
        if (k,j) in {(0,5),(0,45),(1,12),(1,40),(2,31),(3,53)}:continue
        a=k*(N+1)+j;ff.append((a,a+1,a+N+2,a+N+1))
coat=mesh('Tattered open field jacket',vv,ff,cloth,'spine')
mod=coat.modifiers.new('Canvas thickness','SOLIDIFY');mod.thickness=.003
bpy.context.view_layer.objects.active=coat;coat.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name);coat.select_set(False)
for s in [-1,1]:
    path=[(s*sin(.36)*rx,-cos(.36)*ry-.003,z) for z,rx,ry in coatR[:-1]]
    tube('Coat front stitched binding',path,.004,edge,'spine')
    lapel=mesh('Folded broad lapel',[(s*.063,-.089,1.75),(s*.17,-.114,1.68),(s*.146,-.174,1.52),(s*.082,-.166,1.6),(s*.052,-.125,1.7)],[(0,1,2,3,4)],cloth,'chest')
    tube('Lapel worn edge',[(s*.063,-.093,1.75),(s*.17,-.118,1.68),(s*.146,-.178,1.52),(s*.082,-.17,1.6)],.0035,edge,'chest')
    for z in [1.12,1.25,1.38,1.5]:
        ell('Oxidized jacket button',(s*.078,-.154,z),(.009,.004,.009),iron,'spine',12,8)
    # Frayed lower edge and hanging canvas strips.
for j in range(72):
    a=.4+j/71*(2*pi-.8);x=.211*sin(a);y=-.146*cos(a);z=1.007+.015*sin(19*a);length=random.uniform(.015,.075)
    tube('Dangling hem fiber',[(x,y,z),(x+random.uniform(-.005,.005),y+.002,z-length*.5),(x+random.uniform(-.008,.008),y+.006,z-length)],random.uniform(.0007,.0015),edge,'pelvis',4)
    if j%4==0:
        mesh('Torn canvas tongue',[(x-.008,y,z+.012),(x+.009,y,z+.012),(x+.002,y+.003,z-length*.75)],[(0,1,2)],cloth,'pelvis')

# Legs, sleeves, hands, and heavy lace-up boots.
for s,side in [(-1,'R'),(1,'L')]:
    leg=loft('Trouser leg '+side,[(s*.135,.01,.13,.075,.075),(s*.135,.005,.21,.084,.082),(s*.13,0,.29,.08,.081),(s*.13,.012,.39,.082,.085),(s*.13,-.025,.49,.088,.092),(s*.13,-.03,.57,.095,.097),(s*.122,-.006,.67,.105,.109),(s*.115,.005,.79,.116,.116),(s*.108,.008,.91,.12,.125),(s*.1,.004,1.02,.125,.127),(s*.085,0,1.1,.12,.125)],pants,'thigh.'+side,40,.11)
    for k in range(0):
        z=.18+k*.033
        tube('Bunched trouser cuff '+side,[(s*.135+.076*sin(a),-.005-.079*cos(a),z+.012*sin(a*3+k)) for a in [j*2*pi/24 for j in range(25)]],.0036,pants,'shin.'+side)
    tube('Outside trouser seam '+side,[(s*.21,0,.21),(s*.215,.006,.5),(s*.23,.008,.75),(s*.232,.013,1.03)],.003,edge,'thigh.'+side)
    ell('Boot sole '+side,(s*.135,-.048,.043),(.09,.168,.041),leather,'foot.'+side)
    ell('Boot toe '+side,(s*.135,-.105,.093),(.086,.117,.064),leather,'foot.'+side)
    loft('Boot ankle '+side,[(s*.135,.012,.06,.077,.084),(s*.135,.014,.14,.079,.087),(s*.135,.017,.23,.072,.07)],leather,'foot.'+side,32,.035)
    tube('Boot welt '+side,[(s*.135+.087*sin(a),-.05-.157*cos(a),.064+.002*cos(a)) for a in [j*2*pi/40 for j in range(41)]],.0034,edge,'foot.'+side)
    for k in range(6):
        z=.09+k*.019;y=-.13+k*.012
        tube('Crossed boot lace '+side,[ (s*.135-.037,y,z),(s*.135+.034,y+.012,z+.017)],.0027,edge,'foot.'+side)
        tube('Crossed boot lace '+side,[ (s*.135+.037,y,z),(s*.135-.034,y+.012,z+.017)],.0027,edge,'foot.'+side)
        for x in [-.037,.037]:ell('Boot eyelet',(s*.135+x,y,z),(.004,.002,.004),iron,'foot.'+side,8,6)
    sleeve=loft('Wrinkled jacket sleeve '+side,[(s*.397,-.038,1.06,.055,.061),(s*.39,-.035,1.1,.06,.064),(s*.38,-.026,1.16,.066,.075),(s*.37,-.018,1.23,.074,.08),(s*.355,-.009,1.3,.081,.079),(s*.345,.002,1.35,.086,.085),(s*.33,.006,1.42,.089,.092),(s*.31,.008,1.5,.097,.098),(s*.28,.012,1.58,.103,.102),(s*.245,.008,1.64,.101,.1),(s*.23,.008,1.675,.071,.071),(s*.215,.008,1.692,.028,.027)],cloth,'upper_arm.'+side,36,.12)
    for k in range(0):
        z=1.1+k*.022;x=s*(.395-k*.005)
        tube('Sleeve rumpled crease '+side,[(x+.059*sin(a),-.03-.065*cos(a),z+.009*sin(3*a+k)) for a in [j*2*pi/24 for j in range(25)]],.0029,cloth,'forearm.'+side)
    tube('Cuff binding '+side,[(s*.397+.056*sin(a),-.038-.062*cos(a),1.07+.005*sin(5*a)) for a in [j*2*pi/24 for j in range(25)]],.003,edge,'forearm.'+side)
    for j in range(9):
        a=j*2*pi/9;x=s*.397+.054*sin(a);y=-.038-.06*cos(a)
        tube('Cuff thread',[(x,y,1.07),(x+.002,y-.002,1.045-random.random()*.015)],.0008,edge,'forearm.'+side,4)
    ell('Gaunt hand '+side,(s*.405,-.045,1.008),(.047,.027,.069),skin,'hand.'+side)
    for j in range(4):
        x=s*(.377+j*.018);z=.979-abs(j-1.5)*.006
        pts=[(x,-.052,z),(x+s*.002,-.068,z-.035),(x+s*.001,-.074,z-.065),(x-s*.003,-.063,z-.078)]
        tube('Long finger '+side,pts,[.010,.009,.007,.0045],skin,'hand.'+side,9)
        ell('Discolored nail '+side,(x,-.068,z-.073),(.005,.002,.01),teeth,'hand.'+side,10,6)
        tube('Hand tendon '+side,[(x,-.071,z+.012),(x,-.073,z+.043),(s*.405+(x-s*.405)*.5,-.062,1.058)],.0025,skin,'hand.'+side)
    tube('Hooked thumb '+side,[(s*.369,-.048,1.025),(s*.351,-.078,.997),(s*.357,-.092,.973)],[.015,.012,.007],skin,'hand.'+side,10)

loft('Trouser waistband',[(0,0,1.06,.207,.134),(0,0,1.115,.199,.13)],pants,'pelvis',40,.02)
tube('Leather belt',[(.204*sin(a),-.136*cos(a),1.102) for a in [j*2*pi/48 for j in range(49)]],.012,leather,'pelvis')
tube('Belt buckle',[(-.025,-.149,1.088),(.025,-.149,1.088),(.025,-.149,1.117),(-.025,-.149,1.117),(-.025,-.149,1.088)],.004,iron,'pelvis')

print('BODY COMPLETE',len(parts))
