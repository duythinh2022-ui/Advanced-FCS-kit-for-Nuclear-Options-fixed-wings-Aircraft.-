#!/bin/bash
# copy Revoker FCS core sources from $1 (dir containing Vortex_FS20.FCS) into fcs/
S="$1/Vortex_FS20.FCS"
rm -rf fcs; mkdir fcs
for f in FcsController AsymShared EstimatorJob TelemetryRow ParallelFcs Allocator Effector SurfaceEffector TvcEffector RateTraj Lpf Lpf3 AirfoilTable AsymCap CoupledCap EvalContext FcsMode SurfaceKind FlapSystem StallStagger FuelBalance Downwash BodyLift Camber IfritEffectors TvcYawEffector HingeFlapEffector CapacityEstimator FcsNative Stovl; do cp "$S/$f.cs" fcs/; done
sed -i -E 's/\bprivate\b/internal/g' fcs/FcsController.cs fcs/AsymShared.cs
sed -i -E 's/namespace Vortex_FS20\.FCS/namespace Aryx_F22E_StrikeRaptor.FCS/; s/\bTurbojet\b/Turbofan/g' fcs/*.cs
python3 - "$S/DragRelief.cs" fcs/DragReliefMath.cs <<'PY'
import sys; s=open(sys.argv[1]).read(); s=s.replace('namespace Vortex_FS20.FCS','namespace Aryx_F22E_StrikeRaptor.FCS'); i=s.index('\tinternal class DragRelief : MonoBehaviour'); open(sys.argv[2],'w').write(s[:i]+'}\n')
PY
# the Plugin stub is regenerated from the FCS Plugin.cs + the Dev config next to this script (tomrev.cfg); fields the generator
# does not know (SplitRange and any extra bench-only statics) are carried over from the previous stub
D="$(cd "$(dirname "$0")" && pwd)"; CFG="${CFG:-$D/vortex.cfg}"; cp stubs/Plugin.cs /tmp/Plugin.stub.bak
python3 genplugin2.py "$S/Plugin.cs" "$CFG" /tmp/Plugin.gen.cs >/dev/null
python3 - <<'PY'
import re
old=open('/tmp/Plugin.stub.bak').read(); new=open('/tmp/Plugin.gen.cs').read()
nn=set(re.findall(r'internal static \S+ (\w+)\s*=',new))
oldd={re.match(r'internal static \S+ (\w+)',l).group(1):l for l in old.split('\n') if re.match(r'internal static \S+ (\w+)\s*=',l)}
extra=[l for k,l in oldd.items() if k not in nn]
L=new.split('\n')
for i,l in enumerate(L):
    m=re.match(r'internal static \S+ (\w+)',l)
    if m and m.group(1)=='SplitRange': L[i]=oldd['SplitRange']
new='\n'.join(L)
i=new.rstrip().rfind('}'); i=new.rstrip()[:i].rfind('}')
new=new[:i]+'\n'.join(extra)+'\n'+new[i:]
open('stubs/Plugin.cs','w').write(new)
PY
sed -i 's/internal static bool OnlyWhileLogging = true;/internal static bool OnlyWhileLogging = false;/' fcs/CapacityEstimator.cs
# bench-only: the allocator's last target (allocator dump, ALLOCDUMP)
python3 - <<'PY'
p='fcs/Allocator.cs'; s=open(p).read()
k='public void Solve(List<Effector> effs, Vector3 target)\n\t\t{'
if k in s: s=s.replace(k, k+'\n\t\t\tdbgLastTarget = target; dbgLastW = axisWeight;',1).replace('public bool slipFrame;','public bool slipFrame;\n\t\tpublic Vector3 dbgLastTarget, dbgLastW;',1)
open(p,'w').write(s)
PY
# bench-only experiment (Tom 17 Nov 17:30): after the solve, also price the mirrored differentials of the wing pairs and rudders
python3 - <<'PY'
p='fcs/Allocator.cs'; s=open(p).read()
k='\t\t\tresDbg = MomentSum() - target;'
add='''\t\t\tif (BenchMirror) BenchMirrorPass(target);
'''
fn='''
		public static bool BenchMirror;
		public int benchMirrorMoves;
		private void BenchMirrorPass(Vector3 target)
		{
			string[] gn = { "elevator", "aileron", "rudder" };
			var pr = new List<(int, int)>[3];
			for (int g = 0; g < 3; g++) { pr[g] = new List<(int, int)>(); for (int i = 0; i < act.Count; i++) if (act[i].name.Contains(gn[g]) && act[i].name.EndsWith("_L")) { int j = act.FindIndex(e => e.name == act[i].name.Replace("_L", "_R")); if (j >= 0) pr[g].Add((i, j)); } }
			double[] x0 = (double[])x.Clone(); double[] best = (double[])x.Clone(); float cBest = Total(target); int mBest = 0;
			for (int m = 1; m < 8; m++)
			{
				Array.Copy(x0, x, x.Length);
				for (int g = 0; g < 3; g++) if ((m & (1 << g)) != 0) foreach (var (i, j) in pr[g]) { double a = x0[i], b = x0[j]; x[i] = Mathf.Clamp((float)b, act[i].lo, act[i].hi); x[j] = Mathf.Clamp((float)a, act[j].lo, act[j].hi); }
				float c = Total(target);
				if (c < cBest) { cBest = c; best = (double[])x.Clone(); mBest = m; }
			}
			Array.Copy(best, x, x.Length);
			if (mBest != 0) benchMirrorMoves++;
		}
'''
if 'BenchMirrorPass' not in s:
    s=s.replace(k, add+k,1)
    i=s.index('\t\tprivate Vector3 MomentSum()'); s=s[:i]+fn+s[i:]
open(p,'w').write(s)
PY
# bench-only experiment (Tom 17 Nov 17:30, hard form): the two wing trailing-edge pairs may not cross. When the solve
# crosses them, it is solved again with the follower pair boxed to the leader's sense around its own mean (both ways round)
# and the cheaper of the two is kept
python3 - <<'PY'
p='fcs/Allocator.cs'; s=open(p).read()
k='\t\t\tdbgLastTarget = target; dbgLastW = axisWeight;'
add='''
			if (BenchNoFight && !inNoFight) { NoFightSolve(effs, target); return; }'''
fn='''
		public static bool BenchNoFight;
		public static float BenchNoFightTol = 1f;
		private bool inNoFight;
		public int noFightMoves;
		private void NoFightSolve(List<Effector> effs, Vector3 target)
		{
			inNoFight = true;
			try
			{
				SolveDispatch(effs, target);
				Effector eL = effs.Find(e => e.active && e.name == "elevator_L"), eR = effs.Find(e => e.active && e.name == "elevator_R");
				Effector aL = effs.Find(e => e.active && e.name == "aileron_L"), aR = effs.Find(e => e.active && e.name == "aileron_R");
				if (eL == null || eR == null || aL == null || aR == null) return;
				float dI = eL.cmd - eR.cmd, dO = aL.cmd - aR.cmd;
				if (dI * dO >= 0f || Mathf.Abs(dI) < BenchNoFightTol || Mathf.Abs(dO) < BenchNoFightTol) return;
				noFightMoves++;
				int n = effs.Count; var c0 = new float[n]; for (int i = 0; i < n; i++) c0[i] = effs[i].cmd;
				float bestCost = float.MaxValue; float[] best = null;
				for (int opt = 0; opt < 2; opt++)
				{
					for (int i = 0; i < n; i++) effs[i].cmd = c0[i];
					// opt 0: the outer pair follows the inner pair's sense; opt 1: the inner follows the outer
					Effector fL = opt == 0 ? aL : eL, fR = opt == 0 ? aR : eR; float sense = opt == 0 ? Mathf.Sign(dI) : Mathf.Sign(dO);
					float m = 0.5f * (fL.cmd + fR.cmd);
					float lL = fL.lo, hL = fL.hi, lR = fR.lo, hR = fR.hi;
					if (sense > 0f) { fL.lo = Mathf.Clamp(m, lL, hL); fR.hi = Mathf.Clamp(m, lR, hR); } else { fL.hi = Mathf.Clamp(m, lL, hL); fR.lo = Mathf.Clamp(m, lR, hR); }
					fL.cmd = Mathf.Clamp(fL.cmd, fL.lo, fL.hi); fR.cmd = Mathf.Clamp(fR.cmd, fR.lo, fR.hi);
					SolveDispatch(effs, target);
					fL.lo = lL; fL.hi = hL; fR.lo = lR; fR.hi = hR;
					if (lastCost < bestCost) { bestCost = lastCost; best = new float[n]; for (int i = 0; i < n; i++) best[i] = effs[i].cmd; }
				}
				for (int i = 0; i < n; i++) effs[i].cmd = best[i];
			}
			finally { inNoFight = false; }
		}
'''
if 'NoFightSolve' not in s:
    s=s.replace(k, k+add,1)
    i=s.index('\t\tprivate Vector3 MomentSum()'); s=s[:i]+fn+s[i:]
open(p,'w').write(s)
PY
# bench-only experiment (Tom 17 Nov 18:34, Raptor-style pitch offload): the inboard (stabilator-kind) pair's common-deflection cost as a key
python3 - <<'PY'
p='fcs/FcsController.cs'; s=open(p).read()
a='((l.kind == SurfaceKind.Aileron) ? Plugin.ElevonSymCost : 2f)'
if a in s and 'BenchStabSym' not in s:
    s=s.replace(a,'((l.kind == SurfaceKind.Aileron) ? Plugin.ElevonSymCost : BenchStabSym)',1)
    s=s.replace('internal static int FightMode = 0;','internal static int FightMode = 0;\n\t\tinternal static float BenchStabSym = 2f;',1)
open(p,'w').write(s)
PY
