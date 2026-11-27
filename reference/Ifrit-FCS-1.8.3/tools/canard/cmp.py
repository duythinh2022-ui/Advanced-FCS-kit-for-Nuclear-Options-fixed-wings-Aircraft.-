import sys,re,os,math
# usage: cmp.py outdir tagA tagB [tagC...]  -> per block summary of sideslip metrics and per-case worst changes
d=sys.argv[1]; tags=sys.argv[2:]
def rows(path, keycols, valcol, skip_hdr_pred):
    out={}
    if not os.path.exists(path): return out
    L=[l.rstrip('\n') for l in open(path) if l.strip()]
    hdr=None
    for l in L:
        if hdr is None:
            if skip_hdr_pred(l): hdr=l; continue
            else: continue
        parts=re.split(r'\s{2,}', l.strip()) if valcol=='reg' else l.split()
        out[l]=parts
    return hdr,out
def reg(tag):
    p=f'{d}/out_reg_{tag}.txt'; R={}
    if not os.path.exists(p): return R
    for l in open(p):
        m=re.match(r'^\s*(.+?\d{2,4})\s+(-?[\d.]+|nan)\s+(-?[\d.]+|nan)\s+(-?[\d.]+|nan)\s+(-?[\d.]+|nan)\s+(-?[\d.]+|nan)',l)
        if m and not l.strip().startswith('case'):
            b=float(m.group(2))
            if b<100: R[m.group(1).strip()]=dict(beta=b,pMax=float(m.group(5)),pMean=float(m.group(6)))
    return R
def sw(tag):
    p=f'{d}/out_sw_{tag}.txt'; R={}
    if not os.path.exists(p): return R
    for l in open(p):
        f=l.split()
        if len(f)==11 and re.match(r'^\d+$',f[0]):
            R[f'{f[0]} {f[1]} {"rev" if f[2]=="1" else "stop"}']=dict(beta=float(f[7]),tRev=float(f[8]) if f[8]!='NaN' else math.nan,pmean=float(f[4]))
    return R
def cork(tag):
    p=f'{d}/out_cork_{tag}.txt'; R={}
    if not os.path.exists(p): return R
    for l in open(p):
        m=re.match(r'^\s*(\S.*?\d{3})\s+(-?[\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+(\d+)\s+(-?\d+)\s+([\d.]+)',l)
        if m: R[m.group(1).strip()]=dict(beta=float(m.group(5)),pS=float(m.group(8)),pSStd=float(m.group(9)))
    return R
def dmg(tag):
    p=f'{d}/out_dmg_{tag}.txt'; R={}
    if not os.path.exists(p): return R
    for l in open(p):
        m=re.match(r'^(.{26}\S?.*?)\s{2,}(.+?)\s{2,}(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+|nan)\s+(-?[\d.]+|nan)\s+(-?[\d.]+|nan)\s+(-?[\d.]+|nan)\s+(\S+)?',l)
        if m and not l.startswith('case') and not l.startswith('=='):
            R[(m.group(1).strip()+' | '+m.group(2).strip())]=dict(beta=float(m.group(6)),aft=float(m.group(8)) if m.group(8)!='nan' else math.nan, dep=(m.group(10) or ''))
    return R
for name,fn in [('reg',reg),('sw',sw),('cork',cork),('dmg',dmg)]:
    D=[fn(t) for t in tags]; base=D[0]
    keys=[k for k in base if all(k in x for x in D)]
    if not keys: print(name,'no data', [len(x) for x in D]); continue
    print("=== %s (%d cases)  mean/worst sideslip: %s" % (name, len(keys), "  ".join("%s: %.2f/%.1f" % (t, sum(x[k]["beta"] for k in keys)/len(keys), max(x[k]["beta"] for k in keys)) for t,x in zip(tags,D))))
    for t,x in list(zip(tags,D))[1:]:
        ch=sorted(((x[k]['beta']-base[k]['beta'],k) for k in keys), key=lambda z:z[0])
        better=[f'{k} {base[k]["beta"]:.1f}->{x[k]["beta"]:.1f}' for dlt,k in ch if dlt<-0.5]
        worse=[f'{k} {base[k]["beta"]:.1f}->{x[k]["beta"]:.1f}' for dlt,k in ch[::-1] if dlt>0.5]
        print(f'  {t} vs {tags[0]}: better {len(better)}, worse {len(worse)}')
        if worse: print('    worse:', '; '.join(worse[:8]))
        if better: print('    better:', '; '.join(better[:8]))
