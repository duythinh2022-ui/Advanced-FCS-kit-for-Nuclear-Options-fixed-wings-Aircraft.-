import re,sys
src=open(sys.argv[1]).read(); cfgp=sys.argv[2]; out=sys.argv[3]
cfg={}
sec=None
for l in open(cfgp,encoding='utf-8-sig'):
    l=l.strip()
    if l.startswith('['): sec=l[1:-1]; continue
    if not l or l.startswith('#'): continue
    if '=' in l:
        k,v=[x.strip() for x in l.split('=',1)]; cfg[(sec,k)]=v
defs={}; types={}
for m in re.finditer(r'(c\w+)\s*=\s*(?:cfg|dev)\.Bind\("([^"]*)",\s*"([^"]*)",\s*(?:defaultValue:\s*)?(.+?),\s*(?:"|new ConfigDescription)', src):
    c,sec,key,dv=m.groups(); dv=dv.strip()
    v=cfg.get((sec,key))
    if v is not None:
        if v in ('true','false'): lit=v
        elif re.fullmatch(r'-?[\d.]+([eE]-?\d+)?',v): lit=v+'f' if dv.endswith('f') or '.' in dv or 'f' in dv else v
        elif re.fullmatch(r'\w+\.\w+',dv): lit=dv.split('.')[0]+'.'+v
        else: lit='"'+v+'"'
        defs[c]=lit
    else: defs[c]=dv.replace('BuildFlavor.Stock','false')
    types[c]=(sec,key,v is not None)
used=sum(1 for c in types if types[c][2])
enums=re.findall(r'public enum \w+\s*\{[^}]*\}',src)
lines=['using UnityEngine;','using System;','namespace Aryx_F22E_StrikeRaptor.FCS {']+enums+['internal static class Plugin {','internal static BepInEx.Logging.ManualLogSource Log = new BepInEx.Logging.ManualLogSource();']
n=0
for m in re.finditer(r'public static (\w+(?:<[^>]+>)?) (\w+)\s*=>\s*(.+?);\n', src):
    t,name,expr=m.groups()
    e2=re.sub(r'(c\w+)\.Value', lambda mm: '('+defs.get(mm.group(1),'default')+')', expr)
    e2=re.sub(r'\(?\b(c\w+)\s*!=\s*null\)?', lambda mm: 'true' if mm.group(1) in defs else mm.group(0), e2)
    e2=re.sub(r'\(?\b(c\w+)\s*==\s*null\)?', lambda mm: 'false' if mm.group(1) in defs else mm.group(0), e2)
    e2=e2.replace('BuildFlavor.Stock','false')
    if name=='StockPerformance': e2='false'
    lines.append(f'internal static {t} {name} = {e2};'); n+=1
lines+=['}','}']
open(out,'w').write('\n'.join(lines)+'\n')
print(n,'fields;',used,'from user cfg of',len(types))
