import UnityPy,json,sys
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
D="/mnt/user-data/uploads/Nuclear Option/NuclearOption_Data"
env=UnityPy.load(D+"/resources.assets",D+"/globalgamemanagers.assets",D+"/globalgamemanagers")
RA=[f for n,f in env.files.items() if n.endswith("resources.assets")][0]
g=TypeTreeGenerator(RA.unity_version); g.load_local_dll_folder(D+"/Managed"); env.typetree_generator=g
O=RA.objects
d=O[62240].read_typetree()
print({k:(v if not isinstance(v,(list,dict)) or len(str(v))<200 else str(v)[:200]) for k,v in d.items()})
p=O[62241].read_typetree()
for k,v in p.items():
    if k!="airfoils": print(k, str(v)[:300])
json.dump(p,open("/root/w/ifrit/params.json","w"),default=str)
for a in p["airfoils"]: print(a["name"], len(a["liftCoef"]["m_Curve"]), len(a["dragCoef"]["m_Curve"]))
