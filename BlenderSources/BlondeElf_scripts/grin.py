# Paint a toothy grin over the mouth of the blonde elf's colour map (for the Smug state)
import bpy, bmesh, numpy as np, sys, math
W=r"D:\Unity\elf-blonde-work"
MODE=sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "probe"
bpy.ops.wm.open_mainfile(filepath=W+r"\BlondeElf.blend")
ob=bpy.data.objects["BlondeElf_Body"]
img=bpy.data.images.load(W+r"\Elf_BaseColor_2048.png"); T=img.size[0]
px=np.array(img.pixels[:],np.float32).reshape(T,T,4)
bm=bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.triangulate(bm,faces=bm.faces)
uvl=bm.loops.layers.uv.active
P=[];IJ=[]
for f in bm.faces:
    c=f.calc_center_median(); n=f.normal
    if not (abs(c.x)<0.07 and 1.45<c.z<1.62 and c.y<-0.13 and n.y<-0.2): continue
    uv=np.array([l[uvl].uv[:] for l in f.loops])*T
    co=np.array([l.vert.co[:] for l in f.loops])
    x0,y0=np.floor(uv.min(0)).astype(int); x1,y1=np.ceil(uv.max(0)).astype(int)
    xs,ys=np.meshgrid(np.arange(max(x0,0),min(x1+1,T)),np.arange(max(y0,0),min(y1+1,T)))
    pts=np.stack([xs.ravel()+0.5,ys.ravel()+0.5],1)
    a,b,cc=uv; v0=b-a; v1=cc-a; d=v0[0]*v1[1]-v1[0]*v0[1]
    if abs(d)<1e-12: continue
    v2=pts-a; w1=(v2[:,0]*v1[1]-v1[0]*v2[:,1])/d; w2=(v0[0]*v2[:,1]-v2[:,0]*v0[1])/d; w0=1-w1-w2
    m=(w0>=-0.03)&(w1>=-0.03)&(w2>=-0.03)
    if not m.any(): continue
    Wt=np.stack([w0[m],w1[m],w2[m]],1)
    P.append(Wt@co); IJ.append(pts[m].astype(int))
P=np.concatenate(P); IJ=np.concatenate(IJ)
X=P[:,0]; Z=P[:,2]
col=px[IJ[:,1],IJ[:,0],:3]; lum=col@np.array([0.3,0.59,0.11])
print("face texels",len(P))
skin=np.median(col[(abs(X)<0.03)&(Z>1.49)&(Z<1.53)&(lum>0.6)],0); print("skin",skin.round(3))
dark=(abs(X)<0.045)&(Z>1.485)&(Z<1.525)&(lum<lum[(abs(X)<0.03)&(Z>1.49)&(Z<1.53)].mean()-0.12)
print("dark texels",dark.sum())
if dark.sum():
    dx=X[dark]; dz=Z[dark]
    for xb in np.arange(-0.04,0.041,0.005):
        s=abs(dx-xb)<0.0025
        if s.any(): print(" x=%.3f z=%.4f..%.4f n=%d"%(xb,dz[s].min(),dz[s].max(),s.sum()))
# texel density near the mouth
s=(abs(X)<0.03)&(abs(Z-1.50)<0.01)
print("mouth texels", s.sum(), "-> px per mm approx", math.sqrt(s.sum()/(60*20)))
def seg_dist(x,z,a,b):
    ax,az=a; bx,bz=b; vx,vz=bx-ax,bz-az
    t=np.clip(((x-ax)*vx+(z-az)*vz)/(vx*vx+vz*vz),0,1)
    return np.hypot(x-(ax+t*vx), z-(az+t*vz))
def save(arr,path):
    o=bpy.data.images.new("o",T,T,alpha=False); o.pixels.foreach_set(arr.ravel())
    o.filepath_raw=path; o.file_format='PNG'; o.save(); bpy.data.images.remove(o)
if MODE=="paint":
    z0=float(sys.argv[sys.argv.index("--")+2])
    out=px.copy()
    def put(mask,c,alpha=1.0):
        cur=out[IJ[mask,1],IJ[mask,0],:3]
        a=alpha if np.isscalar(alpha) else alpha[:,None]
        out[IJ[mask,1],IJ[mask,0],:3]=cur*(1-a)+np.asarray(c)*a
    # ---- nose (shared by both faces): shadow wedge on one side, bridge stroke, small tip
    def tri(px_,pz_,a,b,c):
        def side(p,q,r): return (px_-q[0])*(r[1]-q[1])-(pz_-q[1])*(r[0]-q[0])
        d1=side(None,a,b); d2=side(None,b,c); d3=side(None,c,a)
        neg=(d1<0)|(d2<0)|(d3<0); pos=(d1>0)|(d2>0)|(d3>0)
        return ~(neg&pos)
    wedge=tri(X,Z,(0.0006,1.5228),(0.0034,1.5335),(0.0062,1.5232))
    put(wedge,skin*0.80,0.65)
    nl=np.array([0.48,0.27,0.23])
    br=seg_dist(X,Z,(0.0034,1.5335),(0.0006,1.5228))<0.00075
    put(br,nl,0.95)
    tip=seg_dist(X,Z,(-0.0013,1.5222),(0.0010,1.5219))<0.00055
    put(tip,nl,0.6)
    save(out,W+r"\Elf_BaseColor_Nose_2048.png")
    # ---- grin
    w=0.028; zt=z0+0.0055; H=0.0205
    def top(x): return zt+0.0065*np.minimum((x/w)**2,1)
    def hgt(x): return H*np.clip(1-(x/w)**2,0,1)**0.55
    def bot(x): return top(x)-hgt(x)
    def mid(x): return top(x)-0.50*hgt(x)
    inside=(abs(X)<w)&(Z<top(X))&(Z>bot(X))
    # erase the old smile line around the mouth with skin colour
    er=(abs(X)<0.040)&(Z>z0-0.020)&(Z<z0+0.012)&~inside
    blend=np.clip((0.040-abs(X[er]))/0.006,0,1)
    put(er,skin,blend)
    teeth=np.array([0.98,0.97,0.94]); shade=np.array([0.80,0.79,0.80]); gap=np.array([0.36,0.11,0.13]); line=np.array([0.12,0.06,0.06])
    xi=X[inside]; zi=Z[inside]; hi=hgt(xi)
    upper=zi>mid(xi)
    c=np.where(upper[:,None],teeth,teeth*0.93)
    # lip shadow on the top of the upper teeth
    lt=(top(xi)-zi)<0.0018; c[lt&upper]=shade
    # tooth separations, both rows
    su=upper&(abs(((xi+0.0028)%0.0056)-0.0028)<0.00038)
    sl=(~upper)&(abs(((xi)%0.0048)-0.0024)<0.00034)&(zi>bot(xi)+0.0012)
    c[su|sl]=shade*0.85
    # gap between the rows, wider and darker towards the corners
    gth=0.0007+0.0045*(np.abs(xi)/w)**3
    g=np.abs(zi-mid(xi))<gth
    c[g]=gap
    cor=np.clip((np.abs(xi)/w-0.82)/0.18,0,1)[:,None]
    c=c*(1-cor)+gap*cor
    out[IJ[inside,1],IJ[inside,0],:3]=c
    # outline, a bit heavier along the bottom
    ol=(abs(X)<w+0.0011)&(Z<top(X)+0.0017)&(Z>bot(X)-0.0017)&~inside
    put(ol,line,1.0)
    # small creases at the mouth corners
    for sgn in (-1,1):
        a=(sgn*(w+0.0006),float(top(np.array(w)))+0.0004); b=(sgn*(w+0.0032),float(top(np.array(w)))+0.0036)
        cr=seg_dist(X,Z,a,b)<0.00045
        put(cr,line,0.85)
    save(out,W+r"\Elf_BaseColor_Grin_2048.png")
    print("SAVED")
