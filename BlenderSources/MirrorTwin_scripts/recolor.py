# Paints the evil twin's outfit over the blonde elf's colour map (Vili's reference, 2026-10-08):
# navy bodysuit with gold piping, sheer black tights and chest panel, black gloves and thigh boots
# with gold bands, a navy high collar, amber eyes, a violet circlet gem.
import bpy, numpy as np, sys
args=sys.argv[sys.argv.index("--")+1:]
SRC, DST = args[0], args[1]
N=2048
pos=np.load("texpos.npy"); nor=np.load("texnor.npy"); cov=np.load("texcov.npy")
img=bpy.data.images.load(SRC); assert img.size[0]==N
px=np.array(img.pixels[:],np.float32).reshape(N,N,4)[...,:3]
r,g,b=px[...,0],px[...,1],px[...,2]
mx=px.max(-1); mn=px.min(-1); sat=(mx-mn)/(mx+1e-5); lum=0.3*r+0.59*g+0.11*b
X,Y,Z=pos[...,0],pos[...,1],pos[...,2]; NY=nor[...,1]
hair=(r>0.5)&(g>0.4)&(b<0.42)&((r-b)>0.28)&(sat>0.35)
blue=(b>r+0.12)&(b>0.3)&~hair
teal=(g>r+0.12)&(b>r+0.05)&(g>0.3)&(b<g+0.15)&(Z>1.5)
blue&=~teal
dark=lum<0.13
white=(sat<0.2)&(lum>0.5)&~hair
brown=(r>g)&(g>=b-0.02)&(r<0.6)&((r-b)>0.1)&(lum<0.42)&~dark
skin=(r>g)&(g>b)&(r>0.62)&((r-b)>0.12)&~hair&~white&~brown
face=(Z>1.47)&(np.abs(X)<0.105)&(Y<-0.11)
legs=Z<0.735
def L(mask):
    v=lum[mask&cov]; return float(np.median(v)) if v.size else 0.5
def shade(mask,target,lo=0.65,hi=1.35):
    ref=L(mask); f=np.clip(lum/max(ref,1e-3),lo,hi)[...,None]
    return np.clip(np.array(target,np.float32)*f,0,1)
out=px.copy()
NAVY=(0.105,0.13,0.245); NAVY_DK=(0.06,0.07,0.125); BLACK=(0.055,0.055,0.07); GOLD=(0.80,0.61,0.25)
LEATHER=(0.07,0.07,0.085)
def put(mask,col): out[mask]=col[mask]
# hair: a paler gold
hc=np.clip(px*0.8+np.array((0.95,0.86,0.58))*0.2*(lum/max(L(hair),1e-3))[...,None],0,1); put(hair,hc)
# eyes amber, circlet gem violet
put(teal&face|teal&(Z>1.5), shade(teal,(0.86,0.62,0.12),0.5,1.6))
gem=blue&(Z>1.58); put(gem, shade(gem,(0.42,0.12,0.72),0.5,1.6))
# outfit (not the face)
nf=~face
sleeve=np.abs(X)>0.245
put(blue&~legs&nf&~gem&~sleeve, shade(blue,NAVY))
put(blue&~legs&nf&~gem&sleeve, shade(blue,LEATHER,0.6,1.6))
put(white&~legs&nf&~sleeve, shade(white,NAVY_DK,0.8,1.2))
put(white&~legs&nf&sleeve, shade(white,LEATHER,0.8,1.3))
put(brown&~legs&nf, shade(brown,BLACK,0.7,1.3))
# boots: everything of the stockings and shoes, black leather
boot=legs&cov&~skin&~hair
put(boot, shade(boot,LEATHER,0.6,1.6))
# sheer tights over bare legs and hips, and a sheer panel over the upper chest
sheer_legs=skin&(Z<0.97)
sheer_chest=skin&(Z>1.19)&(Z<1.41)&(np.abs(X)<0.16)&(NY<0.25)
sheer=sheer_legs|sheer_chest
tint=px*0.22+np.array((0.045,0.04,0.055),np.float32); put(sheer,tint)
# high collar around the neck
collar=skin&(Z>=1.405)&(Z<1.478)&(np.abs(X)<0.08)
put(collar, shade(skin,NAVY))
# pale, cool skin elsewhere
rest_skin=skin&~sheer&~collar
put(rest_skin, np.clip(px*np.array((0.97,0.97,1.0))+0.012,0,1))
# gold piping: dark trims bordering other colours, the boot tops, the collar edge
cls=np.zeros((N,N),np.int8)
cls[blue&nf]=1; cls[(white|brown)&nf&~legs]=2; cls[skin]=3; cls[hair]=4; cls[boot]=5; cls[collar]=6
def near(mask,rad):
    m=mask.copy()
    for dy in range(-rad,rad+1):
        for dx in range(-rad,rad+1):
            if dx*dx+dy*dy>rad*rad: continue
            m|=np.roll(np.roll(mask,dy,0),dx,1)
    return m
trim=(cls==2)&(near(cls==1,3)|near(cls==3,3)|near(cls==4,3))
boot_band=boot&(Z>0.695)
lx=np.abs(np.abs(X)-0.12); front=np.clip(-NY,0,1)
vdip=0.055*np.clip(1-lx/0.06,0,1)*front
boot_band=boot&(np.abs(Z-(0.712-vdip))<0.013)
collar_edge=collar&((Z>1.462)|near(~collar&skin,2))
glove_band=(cls==1)&(np.abs(X)>0.3)&(near(cls==3,3))
gold=trim|boot_band|collar_edge|glove_band
put(gold, shade(gold,GOLD,0.75,1.3))
# pad: texels outside the islands take their covered neighbours' colour, so seams never show the old colours
filled=cov.copy()
for it in range(6):
    acc=np.zeros_like(out); cnt=np.zeros((N,N),np.float32)
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
        m=np.roll(np.roll(filled,dy,0),dx,1); acc+=np.roll(np.roll(out,dy,0),dx,1)*m[...,None]; cnt+=m
    grow=(~filled)&(cnt>0)
    out[grow]=acc[grow]/cnt[grow][...,None]; filled|=grow
o=np.ones((N,N,4),np.float32); o[...,:3]=out
dst=bpy.data.images.new("Twin",N,N,alpha=False); dst.pixels.foreach_set(o.ravel()); dst.filepath_raw=DST; dst.file_format='PNG'; dst.save()
print("classes", {k:int(v.sum()) for k,v in dict(hair=hair,blue=blue,teal=teal,white=white,brown=brown,skin=skin,sheer=sheer,gold=gold,boot=boot).items()})
