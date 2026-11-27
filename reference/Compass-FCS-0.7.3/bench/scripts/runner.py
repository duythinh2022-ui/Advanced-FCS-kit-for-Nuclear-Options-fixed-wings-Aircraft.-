import csv,subprocess,os,hashlib
from concurrent.futures import ThreadPoolExecutor
S=os.path.dirname(os.path.abspath(__file__))
def run1(args):
    h=hashlib.md5(" ".join(args).encode()).hexdigest()[:12]
    f=f"{S}/aoa/{h}.csv"
    subprocess.run(["dotnet","bin/bench.dll","one"]+args+[f"csv={f}"],cwd="/tmp/claude-0/bench",capture_output=True)
    try: return [{k:float(v) for k,v in x.items()} for x in csv.DictReader(open(f))]
    except Exception: return []
def runmany(list_of_args,par=2):
    with ThreadPoolExecutor(par) as ex: return list(ex.map(run1,list_of_args))
