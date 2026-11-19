import sys; sys.path.insert(0,'/home/claude/work/geo')
from scene import *
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
COL={'SmallFighter1_skin':'tab:gray','jet_mechanical':'tab:red','aircraft_structure':'tab:blue','Matte_black':'k','honeycomb':'tab:orange','LOD':'c'}
parts=["liftFan","liftFan_blades","fanDoor_LL","fanDoor_LR","liftFan_door_upper","intake","cockpit","SmallFighter1","wingRoot_L","wingRoot_R","gearDoor_FL","gearDoor_FR","weaponBay_L","weaponBay_R","gear_F"]
def cut_all(axis,val):
    out=[]
    for n in parts:
        try: P,N,UV,subs,mn=world(n)
        except Exception as e: continue
        for s,m in zip(subs,mn):
            sg=plane_cut(P,s,axis,val)
            if len(sg): out.append((n,m,sg))
    return out
fig,axs=plt.subplots(2,3,figsize=(24,15))
def draw(ax,axis,val,ia,ib,title,xl,yl):
    for n,m,sg in cut_all(axis,val):
        for a,b in sg: ax.plot([a[ia],b[ia]],[a[ib],b[ib]],color=COL.get(m,'m'),lw=1.0 if n.startswith('liftFan') or n.startswith('fanDoor') else 0.5)
    ax.set_title(title); ax.set_aspect('equal'); ax.grid(True,alpha=.3); ax.set_xlim(*xl); ax.set_ylim(*yl)
draw(axs[0,0],0,0.0,2,1,'x=0 (side, z fwd right)',(0.9,3.5),(-1.5,1.2))
draw(axs[0,1],0,0.35,2,1,'x=0.35',(0.9,3.5),(-1.5,1.2))
draw(axs[0,2],0,0.62,2,1,'x=0.62',(0.9,3.5),(-1.5,1.2))
draw(axs[1,0],2,2.32,0,1,'z=2.32 (front view)',(-1.3,1.3),(-1.5,1.2))
draw(axs[1,1],2,1.8,0,1,'z=1.80',(-1.3,1.3),(-1.5,1.2))
draw(axs[1,2],2,2.85,0,1,'z=2.85',(-1.3,1.3),(-1.5,1.2))
plt.tight_layout(); plt.savefig('/home/claude/work/geo/sect1.png',dpi=70)
fig,axs=plt.subplots(2,3,figsize=(24,15))
for ax,y in zip(axs.flat,(-0.70,-0.85,-1.00,-1.10,-1.16,-1.25)):
    draw(ax,1,y,0,2,f'y={y} (top view, z fwd up)',(-1.3,1.3),(1.0,3.4))
plt.tight_layout(); plt.savefig('/home/claude/work/geo/sect2.png',dpi=70)
