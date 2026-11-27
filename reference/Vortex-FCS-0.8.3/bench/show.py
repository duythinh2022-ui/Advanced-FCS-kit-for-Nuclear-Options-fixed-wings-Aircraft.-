import csv,sys
f=sys.argv[1]; step=int(sys.argv[2]) if len(sys.argv)>2 else 5
cols=sys.argv[3].split(",") if len(sys.argv)>3 else ["t","ias","alpha","nz","p","q","r","phi","sp","sr","qCmd","qT","pCmd","rCmd","unload","stabL","stabR","tvc","tgtY","mfY","coupY","qTrajFF","tvcPinned"]
rows=list(csv.DictReader(open(f)))
print(" ".join(f"{c:>7s}" for c in cols))
for r in rows[::step]:
    if float(r["t"])<-0.8: continue
    print(" ".join(f"{float(r[c]):7.2f}" for c in cols))
