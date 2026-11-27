# merge.py <fcslog.csv> <dbg.csv> <out.csv> <ailL,ailR> <flapL,flapR> <stabL,stabR> : one CSV in loadrev.py's columns
import csv, sys
fl, db, out, ail, flp, stb = sys.argv[1:7]
L = [x for x in csv.DictReader(l for l in open(fl) if l.strip())]
D = {round(float(x['t']), 3): x for x in csv.DictReader(open(db))}
rL = sys.argv[7] if len(sys.argv) > 7 else 'none'; rR = sys.argv[8] if len(sys.argv) > 8 else 'none'
aL, aR = ail.split(','); fL, fR = flp.split(','); sL, sR = stb.split(',')
cols = ["t", "sr", "pS", "p", "beta", "alpha", "ias", "asymDy", "ailL", "ailR", "flapL", "flapR", "stabLcmd", "stabRcmd", "pSTgt", "asymCapPos", "latAcc", "loadRev", "revA", "rudL", "rudR"]
with open(out, "w") as w:
    w.write(",".join(cols) + "\n")
    for x in L:
        t = round(float(x['t']), 3)
        d = D.get(t)
        if d is None: continue
        g = lambda k: x.get(k, "nan")
        row = [x['t'], g('sr'), d['pS'], g('p'), g('beta'), g('alpha'), str(float(g('ias'))*3.6), g('asymDy'), g(aL+'_now'), g(aR+'_now'), g(fL+'_now'), g(fR+'_now'),
               g(sL+'_cmd'), g(sR+'_cmd'), d['pSTgtDbg'], g('asymCap'), d['latAccDbg'], d['loadRevDbg'], d['revA'], g(rL+'_now'), g(rR+'_now')]
        w.write(",".join(row) + "\n")
