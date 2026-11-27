import re,sys
src=open(sys.argv[1]).read()
old=open(sys.argv[2]).read()
# defaults
defs={}
for m in re.finditer(r'(c\w+)\s*=\s*cfg\.Bind\("[^"]*",\s*"[^"]*",\s*(?:defaultValue:\s*)?(.+?),\s*"', src):
    defs[m.group(1)]=m.group(2).strip()
enum_types={}
props=[]
for m in re.finditer(r'public static (\w+(?:<[^>]+>)?) (\w+)\s*=>\s*(.+?);\n', src):
    t,name,expr=m.groups()
    e2=re.sub(r'(c\w+)\.Value', lambda mm: '('+defs.get(mm.group(1),'default')+')', expr)
    if 'c' in e2 and re.search(r'\bc[A-Z]\w*\b',e2): continue
    props.append((t,name,e2))
have=set(re.findall(r'internal static \S+ (\w+)\s*=',old))
out=[]
for t,name,e in props:
    if name in have: continue
    out.append(f'internal static {t} {name} = {e};')
print(len(props),"props,",len(out),"new")
new=old.rstrip()
# insert before final closing braces of class
idx=new.rfind('}'); idx=new.rfind('}',0,idx)
new=new[:idx]+"\n".join(out)+"\n"+new[idx:]
open(sys.argv[2],'w').write(new)
print("\n".join(out))
