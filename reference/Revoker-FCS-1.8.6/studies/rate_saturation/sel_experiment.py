# experiment: two estimators (command-based and hold-based) and a per-consumer selection mask HoldSel
import sys, re; sys.path.insert(0, '/home/claude/work/tools')
from pt import F
f = F(sys.argv[1])
f.sub("internal EstimatorJob estJob => estJobF ?? (estJobF = new EstimatorJob(capEst));",
      "internal EstimatorJob estJob => estJobF ?? (estJobF = new EstimatorJob(capEst));\n\t\tinternal readonly CapacityEstimator capEstC = new CapacityEstimator();\n\t\tprivate EstimatorJob estJobCF;\n\t\tinternal EstimatorJob estJobC => estJobCF ?? (estJobCF = new EstimatorJob(capEstC));\n\t\tinternal static int HoldSel = 15;   // 1 rate cap, 2 pace, 4 trade, 8 push governor\n\t\tprivate CapacityEstimator CE(int bit) { return ((HoldSel & bit) != 0) ? capEst : capEstC; }")
f.sub("\t\t\testJobF?.Join();\n\t\t}", "\t\t\testJobF?.Join();\n\t\t\testJobCF?.Join();\n\t\t}")
f.sub("\t\t\testJobF?.Join();\n\t\t\tFlushPendingRow();", "\t\t\testJobF?.Join();\n\t\t\testJobCF?.Join();\n\t\t\tFlushPendingRow();")
L = f.s.split('\n'); i = next(i for i, l in enumerate(L) if 'estJob.Kick(Plugin.EstimatorThreaded' in l)
ind = re.match(r'\t*', L[i]).group(0)
L.insert(i + 1, ind + "if (HoldSel != 15) " + L[i].strip().replace('estJob.Kick(', 'estJobC.Kick(').replace('Plugin.CapacityFromHold);', 'false);'))
f.s = '\n'.join(L)
for a, bit in [("float capM = (sideM >= 0f) ? capEst.capTrZeP : capEst.capTrZeN;", 4),
               ("float capYFull = (rSideG > 0f) ? capEst.capZeN : capEst.capZeP;", 8),
               ("float capYNs = (rSideG > 0f) ? capEst.capZeNsN : capEst.capZeNsP;", 8),
               ("float capXRaw = (sideG > 0f) ? capEst.capXPraw : capEst.capXNraw;", 8),
               ("float capYGrow = (rSideG > 0f) ? capEst.capTrZeP : capEst.capTrZeN;", 8),
               ("float capYFar = (0f - rSideG > 0f) ? capEst.capTrZeP : capEst.capTrZeN;", 8),
               ("aOpp = Mathf.Max(5f, ((sideRev >= 0f) ? capEst.capZeP : capEst.capZeN) + passive);", 1),
               ("float capZd = Mathf.Max(PaceHoldCapFloor, (dirT * sinD > 0f) ? capEst.capZeP : capEst.capZeN);", 2),
               ("float capB = Mathf.Max(PaceHoldCapFloor, (paceStopNF > 0f) ? capEst.capZeN : capEst.capZeP);", 2)]:
    if f.s.count(a) == 1: f.sub(a, a.replace('capEst.', 'CE(%d).' % bit))
    else: print('skip', a[:40], f.s.count(a))
f.save()
