import csv,subprocess,hashlib,os
from concurrent.futures import ThreadPoolExecutor
B='/home/claude/w/src/bench'; R='/home/claude/w/fit/runs'
def run1(args):
    h=hashlib.md5(" ".join(args).encode()).hexdigest()[:14]; f=f"{R}/{h}.csv"
    subprocess.run(["dotnet","bin/bench.dll","one"]+args+[f"csv={f}"],cwd=B,capture_output=True)
    try: return [{k:float(v) for k,v in x.items()} for x in csv.DictReader(open(f))]
    except Exception: return []
def runmany(L,par=4):
    with ThreadPoolExecutor(par) as ex: return list(ex.map(run1,L))
