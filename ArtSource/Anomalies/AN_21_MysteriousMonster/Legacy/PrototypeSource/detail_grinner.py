# Run after build_grinner.py in the same Blender Python scope.
# Sculpted head surface: hollow sockets and an actual opening for the grin.
vv=[];ff=[];NR=48;NC=64
profile=[(1.78,.035,.04),(1.8,.072,.061),(1.83,.095,.078),(1.87,.109,.087),(1.91,.113,.09),(1.95,.113,.092),(1.99,.108,.093),(2.025,.092,.077),(2.045,.055,.048)]
def head_section(z):
    for i in range(len(profile)-1):
        a,b=profile[i:i+2]
        if a[0]<=z<=b[0]:
            t=(z-a[0])/(b[0]-a[0]);return (a[1]*(1-t)+b[1]*t,a[2]*(1-t)+b[2]*t)
    return profile[-1][1:]
for k in range(NR+1):
    z=1.78+k/NR*.265;rx,ry=head_section(z)
    for j in range(NC):
        a=j/NC*2*pi;x=rx*sin(a);y=-.013-ry*cos(a)
        front=max(cos(a),0)**5
        # Sculpt cheek bones, hollow temples, orbital hollows, chin and brow.
        eyes_d=.018*math.exp(-((abs(x)-.052)/.032)**2-((z-1.964)/.029)**2)
        cheek=.012*math.exp(-((abs(x)-.083)/.023)**2-((z-1.917)/.026)**2)
        chin=.013*math.exp(-(x/.06)**2-((z-1.807)/.021)**2)
        brow=.012*math.exp(-((abs(x)-.052)/.043)**2-((z-1.995)/.013)**2)
        y+=front*(eyes_d-cheek-chin-brow)
        y+=.0013*sin(x*190+z*140)*front
        vv.append((x,y,z))
for k in range(NR):
    for j in range(NC):
        ids=(k*NC+j,k*NC+(j+1)%NC,(k+1)*NC+(j+1)%NC,(k+1)*NC+j)
        center=sum((Vector(vv[i]) for i in ids),Vector())/4
        mouthz=1.858+.019*(abs(center.x)/.095)**1.7
        if center.y<-.05 and (center.x/.094)**2+((center.z-mouthz)/.031)**2<1.0:continue
        ff.append(ids)
ff.extend([tuple(reversed(range(NC))),tuple(range(NR*NC,(NR+1)*NC))])
head=mesh('Sculpt | hollow grinning head',vv,ff,skin,'head')
ell('Mouth interior',(0,-.071,1.86),(.103,.033,.046),socket,'head',40,24)

for s in [-1,1]:
    ell('Deep orbital cavity',(s*.052,-.104,1.963),(.028,.012,.025),socket,'head',32,20)
    ell('Blind cloudy eyeball',(s*.052,-.11,1.965),(.019,.012,.017),eyes,'head',32,20)
    # Narrow flesh eyelids around each dead eye.
    for upper in [True,False]:
        pts=[]
        for j in range(17):
            a=j*pi/16 if upper else pi+j*pi/16
            pts.append((s*.052+.024*cos(a),-.109-.003*sin(a),1.964+.021*sin(a)))
        tube('Shrunken eyelid',pts,.0023,skin,'head',8)
    tube('Heavy brow ridge',[(s*.017,-.104,1.99),(s*.045,-.116,2.002),(s*.079,-.105,1.996),(s*.092,-.09,1.98)],[.007,.01,.008,.003],skin,'head',9)
    tube('Cheek scar fold',[(s*.033,-.11,1.928),(s*.064,-.118,1.911),(s*.095,-.099,1.884),(s*.096,-.083,1.851)],[.004,.006,.005,.002],skin,'head',9)
    tube('Lower cheek tendon',[(s*.092,-.086,1.877),(s*.087,-.097,1.836),(s*.057,-.096,1.811)],[.006,.008,.004],skin,'head',9)
    ell('Ear',(s*.117,.001,1.922),(.021,.025,.045),skin,'head')
    ell('Ear concha',(s*.127,-.018,1.922),(.01,.007,.026),socket,'head')
    tube('Ear cartilage',[(s*.119,-.023,1.892),(s*.135,-.02,1.91),(s*.137,-.015,1.944),(s*.125,-.012,1.954)],[.004,.005,.005,.003],skin,'head',8)
    ell('Nose alar wing',(s*.019,-.119,1.927),(.013,.014,.012),skin,'head')
    ell('Nostril',(s*.018,-.132,1.923),(.007,.0035,.0055),socket,'head',16,12)
tube('Angular nose bridge',[(0,-.101,1.986),(0,-.118,1.956),(0,-.138,1.935)],[.01,.014,.018],skin,'head',12)
ell('Nose tip',(0,-.137,1.934),(.019,.017,.013),skin,'head')

# Lips and gums follow an unnaturally wide, rising smile.
for upper in [True,False]:
    pts=[]
    for j in range(37):
        t=-1+j/18;x=.102*t;arch=max(0,1-t*t)**.6
        z=1.879+(.019*arch if upper else -.053*arch)
        y=-.095-.025*(1-t*t)
        pts.append((x,y,z))
    tube('Torn grin rim',pts,[.0025+.0035*sin(pi*j/36) for j in range(37)],gums,'head',9)
    tube('Pale scar rim',[(x,y+.002,z+(.007 if upper else -.008)) for x,y,z in pts],.0035,skin,'head',8)
for upper in [True,False]:
    for j in range(15):
        t=(j-7)/7.5;x=.093*t;arc=abs(t)**1.8
        z=(1.88+.008*arc) if upper else (1.84+.03*arc)
        y=-.121+.024*arc
        h=(.017 if upper else .012)*(1-.25*arc)*random.uniform(.72,1.24)
        width=.0058*(1-.25*arc)*random.uniform(.9,1.1)
        sign=-1 if upper else 1
        tooth=loft('Uneven tooth',[(x,y,z,width,.006),(x+random.uniform(-.002,.002),y-.001,z+sign*h*.58,width*.86,.0054),(x+random.uniform(-.002,.002),y-.001,z+sign*h,width*.48,.0039)],teeth,'head',10)
        ell('Exposed gum socket',(x,y+.003,z),(.007,.006,.005),gums,'head',12,8)
# Small raised scars and stitches, restrained so the face reads at distance.
for s in [-1,1]:
    for j in range(5):
        z=1.9+j*.013;x=s*(.087+.005*sin(j))
        tube('Fine cheek fissure',[(x,-.087,z),(x+s*.007,-.078,z+.006),(x+s*.009,-.07,z+.014)],.0009,gums,'head',4)

# Floppy hat with irregular brim, dented crown, leather band and frayed fibers.
N=96;vv=[];ff=[]
for k in range(5):
    t=k/4
    for j in range(N):
        a=j*2*pi/N
        rx=.111+t*.116;ry=.094+t*.105
        rag=(random.uniform(-.004,.004) if k==4 else 0)
        z=2.026+(.01*sin(a*3+.5)+.008*cos(a*5))*t-.014*t*t+.006*sin(a)
        vv.append(((rx+rag)*sin(a),-.005-(ry+rag)*cos(a),z))
for k in range(4):
    for j in range(N):a=k*N+j;b=k*N+(j+1)%N;ff.append((a,b,b+N,a+N))
hat=mesh('Hat | ragged sweeping brim',vv,ff,cloth,'head')
mod=hat.modifiers.new('Frayed canvas thickness','SOLIDIFY');mod.thickness=.003
bpy.context.view_layer.objects.active=hat;hat.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name);hat.select_set(False)
loft('Hat | dented crown',[(0,0,2.025,.115,.096),(.001,.003,2.05,.113,.09),(-.006,.005,2.081,.101,.081),(-.004,.012,2.107,.087,.072),(-.01,.01,2.126,.077,.064),(-.012,.012,2.132,.047,.04)],cloth,'head',64,.07)
loft('Hat | weathered band',[(0,-.001,2.034,.116,.096),(.001,.002,2.057,.112,.091)],leather,'head',64,.025,False)
for j in range(10):
    a=-.9+j*.2
    ell('Hat band rivet',(.114*sin(a),-.003-.094*cos(a),2.046),(.0035,.003,.0035),iron,'head',10,8)
for j in range(85):
    a=j*2*pi/85;x=.226*sin(a);y=-.005-.198*cos(a);z=2.012+.01*sin(a*3+.5)+.008*cos(a*5)+.006*sin(a)
    length=random.uniform(.007,.031)
    tube('Hat brim loose fiber',[(x,y,z),(x+sin(a)*.006,y-cos(a)*.006,z-.004),(x+sin(a)*.007,y-cos(a)*.007,z-length)],.0009,edge,'head',4)

# Separately weighted heavy machete; attached to the right hand.
# Blade hangs down beside the right boot, as in the concept.
tube('Cleaver | wrapped handle',[(-.403,-.082,1.014),(-.425,-.092,.883)],[.014,.016],leather,'hand.R',12)
for j in range(12):
    z=.892+j*.0095;x=-.425+(z-.883)/.131*.022
    tube('Cleaver | grip winding',[(x+.015*cos(a),-.09+.015*sin(a),z+.002*sin(a)) for a in [i*pi/6 for i in range(13)]],.0018,edge,'hand.R',4)
tube('Cleaver | guard',[(-.457,-.091,.883),(-.394,-.091,.883)],.005,iron,'hand.R',8)
outline=[(-.447,.878),(-.412,.874),(-.44,.73),(-.479,.57),(-.52,.39),(-.553,.33),(-.558,.397),(-.527,.58),(-.489,.74)]
vv=[(x,y,z) for y in [-.096,-.087] for x,z in outline];n=len(outline)
ff=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
mesh('Cleaver | broad rusted blade',vv,ff,rust,'hand.R')
tube('Cleaver | worn cutting edge',[(-.412,-.096,.874),(-.44,-.096,.73),(-.479,-.096,.57),(-.52,-.096,.39),(-.553,-.096,.33)],.0018,iron,'hand.R',4)
print('DETAIL COMPLETE',len(parts))
