using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	internal sealed class Allocator
	{
		public Vector3 axisWeight = new Vector3(1f, 1.5f, 1f);

		public int gnIterations = 4;

		public float regScale = 1f;

		public static double LmFactor = 0.001;

		// 1.8.41 roll-fight cost: sqrt-weight on the roll acceleration each surface group (stabs, flaperons) makes by its
		// own deflection. Splitting the groups against each other (roll cancelled) to buy yaw or anything else costs both
		// groups' roll squared; an ordinary roll costs little because the groups add. 0 = off.
		public float fightK;

		// 2.2.8: per-group weight on the roll-fight cost (index = roll group; 1 stabs, 2 ailerons, 3 flaperons). The deceleron
		// split relaxes groups 2 and 3 only, so the stabs are still kept from rolling against the wing.
		public readonly float[] fightW = new float[] { 1f, 1f, 1f, 1f };

		// 1.8.42 pitch-fight cost: the stabs' and nozzles' own pitch accelerations; whichever group pitches against their
		// net costs pitchFightK x its pitch acceleration squared (stabs rolled trailing-edge up past their roll reversal,
		// nozzles pinned nose-down to cancel it).
		public float pitchFightK;

		// +1 at positive AoA: only 'stabs nose-up, nozzles nose-down' counts as a fight (mirrored at negative AoA)
		public float pitchFightSign = 1f;

		private int[] pgrp = new int[0];

		private double[] y0 = new double[0];

		private double[] Jp = new double[0];

		private readonly double[] pgR = new double[3];

		private readonly bool[] pgOn = new bool[3];

		private double[] r0 = new double[0];

		private int[] grp = new int[0];

		private double[] Jg = new double[0];

		public const int FightGroups = 3;

		// 2.2.16b: reference sign for the roll-fight cost. When the roll the surfaces must add (target less their roll at
		// zero deflection) is beyond FightTargetMin deg/s^2, the groups opposing THAT are the ones penalised; the groups' own
		// net only decides when the target is near zero (steady, or the crossed set with the stick centred). With the net as
		// the reference, a roll stop was fought: the net still had the old roll's sign, so the group reversing first (the
		// stabs) paid the cost and the wing alone stopped the roll (bench, release at 450 km/h: overshoot 1 -> 9 deg/s).
		public static float FightTargetMin = 15f;

		private double fightSignHint;

		private readonly double[] grR = new double[FightGroups + 1];

		private readonly bool[] grOn = new bool[FightGroups + 1];

		private float i00;

		private float i01;

		private float i02;

		private float i10;

		private float i11;

		private float i12;

		private float i20;

		private float i21;

		private float i22;

		private readonly List<Effector> act = new List<Effector>();

		private double[] x = new double[0];

		private double[] prev = new double[0];

		private double[] dx = new double[0];

		private double[,] H = new double[0, 0];

		private double[] rhs = new double[0];

		private Vector3[] Jc = new Vector3[0];

		private int[] pidx = new int[0];

		public void SetInverseInertia(float[,] inv)
		{
			i00 = inv[0, 0];
			i01 = inv[0, 1];
			i02 = inv[0, 2];
			i10 = inv[1, 0];
			i11 = inv[1, 1];
			i12 = inv[1, 2];
			i20 = inv[2, 0];
			i21 = inv[2, 1];
			i22 = inv[2, 2];
		}

		// F-22E 3.0.4 (EstimatorJob): the worker's allocator takes the main one's configuration (everything a solve reads that is not
		// static and not rebuilt inside the solve); scratch arrays, counters, resDbg and lastCost stay its own
		internal void CopyConfigFrom(Allocator s)
		{
			axisWeight = s.axisWeight;
			gnIterations = s.gnIterations;
			regScale = s.regScale;
			fightK = s.fightK;
			for (int i = 0; i < fightW.Length; i++)
			{
				fightW[i] = s.fightW[i];
			}
			pitchFightK = s.pitchFightK;
			pitchFightSign = s.pitchFightSign;
			i00 = s.i00;
			i01 = s.i01;
			i02 = s.i02;
			i10 = s.i10;
			i11 = s.i11;
			i12 = s.i12;
			i20 = s.i20;
			i21 = s.i21;
			i22 = s.i22;
			PairedTrimSearch = s.PairedTrimSearch;
			pairBlocks = s.pairBlocks;
		}

		private Vector3 E(Vector3 m)
		{
			float num = i00 * m.x + i01 * m.y + i02 * m.z;
			float num2 = i10 * m.x + i11 * m.y + i12 * m.z;
			return new Vector3(z: (i20 * m.x + i21 * m.y + i22 * m.z) * Mathf.Sqrt(axisWeight.z), x: num * Mathf.Sqrt(axisWeight.x), y: num2 * Mathf.Sqrt(axisWeight.y));
		}

		private float Cost(Vector3 err)
		{
			return E(err).sqrMagnitude;
		}

		private float Reg(Effector e, float c, float p)
		{
			float num = e.RegN;
			float num2 = c / num;
			float num3 = (c - p) / num;
			return regScale * (e.mu * num2 * num2 + e.lam * num3 * num3) + BudgetCost(e, c);
		}

		// Budget term is NOT scaled by regScale (the low-q relief): it has to stay a firm budget at low dynamic
		// pressure, where the stabs still have the authority to take the pitch. The controller fades it instead.
		private static float BudgetCost(Effector e, float c)
		{
			if (e.anchorK <= 0f)
			{
				return 0f;
			}
			float ex = BudgetExcess(e, c) / e.RegN;
			return e.anchorK * ex * ex;
		}

		private static float BudgetExcess(Effector e, float c)
		{
			float d = c - e.anchor;
			if (d > e.band)
			{
				return d - e.band;
			}
			if (d < 0f - e.band)
			{
				return d + e.band;
			}
			return 0f;
		}

		private float PairReg(int i)
		{
			int num = pidx[i];
			if (num < 0)
			{
				return 0f;
			}
			Effector effector = act[i];
			Effector effector2 = act[num];
			float num2 = effector.RegN;
			float num3 = effector.pairSign * (float)x[i] / num2;
			float num4 = effector2.pairSign * (float)x[num] / num2;
			float num5 = 0.5f * (num3 - num4) - 0.5f * (effector.pairSign - effector2.pairSign) * effector.symTarget / num2;
			float num6 = 0.5f * (num3 + num4);
			return regScale * (effector.symCost * num5 * num5 + effector.antiCost * num6 * num6);
		}

		private float PairRegWith(int j, float c)
		{
			double num = x[j];
			x[j] = c;
			float num2 = 0f;
			for (int i = 0; i < act.Count; i++)
			{
				if (pidx[i] >= 0 && (i == j || pidx[i] == j))
				{
					num2 += PairReg(i);
				}
			}
			x[j] = num;
			return num2;
		}

		// 2.2.21: for the stabilators the fight cost sees only the roll of their split, not of their common (pitch)
		// deflection. In sideslip the two stabs see different flow, so a common deflection also rolls the jet; counted as
		// group roll it was priced as a "fight" and the stabs stopped taking pitch with the stick released (19 Oct 22:05 log,
		// fins gone, 2-6 deg of sideslip at 200-330 m/s: stabs frozen for 3-6 s, the nozzles walking to their stop and the
		// zero-rate hold drifting 1-2 deg/s, then a jump to +8 deg/s when the pitch priority overrode it).
		public static bool FightStabSplitOnly = true;

		private bool StabSplitMode(int i)
		{
			return FightStabSplitOnly && grp[i] == 1 && pidx[i] >= 0 && grp[pidx[i]] == 1;
		}

		private double GroupRoll(int g)
		{
			double s = 0.0;
			for (int i = 0; i < act.Count; i++)
			{
				if (grp[i] == g)
				{
					if (StabSplitMode(i))
					{
						int j = pidx[i];
						double sym = 0.5 * ((double)act[i].pairSign * x[i] - (double)act[j].pairSign * x[j]);
						float xs = (float)((double)act[i].pairSign * sym);
						s += (double)i00 * ((double)act[i].MAt((float)x[i]).x - (double)act[i].MAt(xs).x);
						continue;
					}
					s += (double)i00 * ((double)act[i].MAt((float)x[i]).x - r0[i]);
				}
			}
			return s;
		}

		// sign of the horizontal surfaces' net roll: groups rolling against it are the ones penalised
		private double NetSign()
		{
			if (fightSignHint != 0.0)
			{
				return fightSignHint;
			}
			double t = 0.0;
			for (int g = 1; g <= FightGroups; g++)
			{
				t += GroupRoll(g);
			}
			return (t >= 0.0) ? 1.0 : (-1.0);
		}

		private float FightCost()
		{
			if (fightK <= 0f)
			{
				return 0f;
			}
			double sg = NetSign();
			double t = 0.0;
			for (int g = 1; g <= FightGroups; g++)
			{
				double a = GroupRoll(g);
				if (a * sg < 0.0)
				{
					t += (double)fightW[g] * a * a;
				}
			}
			return (float)((double)fightK * t);
		}

		// fight cost with effector j at deflection c, others at x
		private float FightCostWith(int j, float c)
		{
			if (fightK <= 0f || grp[j] == 0)
			{
				return 0f;
			}
			double d = (double)i00 * ((double)act[j].MAt(c).x - (double)act[j].MAt((float)x[j]).x);
			double tot = 0.0;
			bool splitJ = StabSplitMode(j);
			double xj = x[j];
			if (splitJ)
			{
				x[j] = c;
			}
			for (int g = 1; g <= FightGroups; g++)
			{
				grR[g] = GroupRoll(g) + ((g == grp[j] && !splitJ) ? d : 0.0);
				tot += grR[g];
			}
			x[j] = xj;
			double sg = (fightSignHint != 0.0) ? fightSignHint : ((tot >= 0.0) ? 1.0 : (-1.0));
			double t = 0.0;
			for (int g = 1; g <= FightGroups; g++)
			{
				if (grR[g] * sg < 0.0)
				{
					t += (double)fightW[g] * grR[g] * grR[g];
				}
			}
			return (float)((double)fightK * t);
		}

		private double GroupPitch(int g)
		{
			double s = 0.0;
			for (int i = 0; i < act.Count; i++)
			{
				if (pgrp[i] == g)
				{
					s += (double)i11 * ((double)act[i].MAt((float)x[i]).y - y0[i]);
				}
			}
			return s;
		}

		// a: stabs' pitch, b: nozzles' pitch (rad/s^2). Only the stabs-nose-up / nozzles-nose-down pattern counts; in it,
		// the group opposing the pair's net pitch is penalised.
		private double PFTerm(double a, double b)
		{
			double k = pitchFightSign;
			if (a * k <= 0.0 || b * k >= 0.0)
			{
				return 0.0;
			}
			double sg = (a + b >= 0.0) ? 1.0 : (-1.0);
			return ((a * sg < 0.0) ? (a * a) : 0.0) + ((b * sg < 0.0) ? (b * b) : 0.0);
		}

		private float PitchFightCost()
		{
			if (pitchFightK <= 0f)
			{
				return 0f;
			}
			return (float)((double)pitchFightK * PFTerm(GroupPitch(1), GroupPitch(2)));
		}

		private float PFPair(int s, float cs, int t, float ct)
		{
			if (pitchFightK <= 0f)
			{
				return 0f;
			}
			double xs = x[s];
			double xt = x[t];
			x[s] = cs;
			x[t] = ct;
			float r = PitchFightCost();
			x[s] = xs;
			x[t] = xt;
			return r;
		}

		private float PitchFightCostWith(int j, float c)
		{
			if (pitchFightK <= 0f || pgrp[j] == 0)
			{
				return 0f;
			}
			double d = (double)i11 * ((double)act[j].MAt(c).y - (double)act[j].MAt((float)x[j]).y);
			double a = GroupPitch(1) + ((pgrp[j] == 1) ? d : 0.0);
			double b = GroupPitch(2) + ((pgrp[j] == 2) ? d : 0.0);
			return (float)((double)pitchFightK * PFTerm(a, b));
		}

		private float Total(Vector3 target)
		{
			Vector3 zero = Vector3.zero;
			float num = 0f;
			for (int i = 0; i < act.Count; i++)
			{
				zero += act[i].MAt((float)x[i]);
				num += Reg(act[i], (float)x[i], (float)prev[i]) + PairReg(i);
			}
			return Cost(zero - target) + num + FightCost() + PitchFightCost();
		}

		private void Ensure(int n)
		{
			if (x.Length != n)
			{
				x = new double[n];
				prev = new double[n];
				dx = new double[n];
				H = new double[n, n];
				rhs = new double[n];
				Jc = new Vector3[n];
				pidx = new int[n];
				r0 = new double[n];
				grp = new int[n];
				Jg = new double[n];
				pgrp = new int[n];
				y0 = new double[n];
				Jp = new double[n];
				pcls = new int[n];
				pside = new int[n];
				y0A = new double[n];
				mvAll = new Vector3[n];
				rtAll = new double[n];
				pyAll = new double[n];
				ecN = new int[n];
				ecC = new float[n * MaxCand];
				ecMx = new float[n * MaxCand];
				ecMy = new float[n * MaxCand];
				ecEx = new float[n * MaxCand];
				ecEy = new float[n * MaxCand];
				ecEz = new float[n * MaxCand];
				ecReg = new float[n * MaxCand];
				ecGi = new int[n * MaxCand];
				mtOk = new bool[2 * n];
				mtS = new float[2 * n * MaxCand];
				mtT1 = new double[2 * n * MaxCand];
				mtT2 = new double[2 * n * MaxCand];
				mtT3 = new double[2 * n * MaxCand];
				mtD = new double[2 * n * MaxCand];
			}
		}

		// 2.41.0 reach pass. The solve places every effector at its command as if it were there now. A stab sent across its
		// travel takes up to half a second to get there, and on the way it passes its lift peak: the nozzles were balanced
		// against the stab's moment at the far end while the jet got the peak's. When an effector with reachTau is
		// commanded further than its servo reaches in reachTau, its command stands (where it is going), and the rest are
		// re-solved against the moment at the reachable position (where it will be).
		public static bool ReachPass = true;

		public int reachMoves;

		private readonly List<Effector> rchE = new List<Effector>();
		private readonly List<float> rchDest = new List<float>();
		private readonly List<float> rchLo = new List<float>();
		private readonly List<float> rchHi = new List<float>();
		private readonly List<Effector> rchAll = new List<Effector>();
		private readonly List<float> rchCmd0 = new List<float>();

		public float lastCost;

		// F-22E 3.1.0: the main allocator times its solves (per-phase timer) and may run the two-horizon reach plan on a worker
		public bool timeSolves;

		public long solveTicks;

		internal bool parallelReach;

		public void Solve(List<Effector> effs, Vector3 target)
		{
			if (PairStat)
			{
				long a0 = System.Diagnostics.Stopwatch.GetTimestamp();
				SolveDispatch(effs, target);
				System.Threading.Interlocked.Add(ref PairStatAll, System.Diagnostics.Stopwatch.GetTimestamp() - a0);
				System.Threading.Interlocked.Increment(ref PairStatAllN);
				return;
			}
			if (!timeSolves)
			{
				SolveDispatch(effs, target);
				return;
			}
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try
			{
				SolveDispatch(effs, target);
			}
			finally
			{
				solveTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
			}
		}

		// ---- F-22E 4.0.0 (NativeAllocator): the solve in the native core (FcsNative, native/fcs_core). SolveInner below is unchanged:
		// it is the reference the native code was ported from line for line, the fallback, and what the checked solves compare against.
		// With NativeAllocatorCheck 1, each allocator's first NativeCheckFirst solves and every NativeCheckEvery-th after them are
		// solved both ways (2 = every solve) and the C# result is the one kept. Where float arithmetic is strict (FcsNative.StrictFloat)
		// one differing bit is a fault and switches the native solve off; in the game's Mono the two are expected to differ in the last
		// digits (see FcsNative), so there the comparison is only counted: identical / close / an equal-cost alternative / different.
		internal const int NativeCheckFirst = 64;

		internal const int NativeCheckEvery = 256;

		private FcsNative.EffN[] nbuf;

		private int nativeCount;

		private void SolveDispatch(List<Effector> effs, Vector3 target)
		{
			if (FcsNative.Wanted && FcsNative.Ready)
			{
				int chk = Plugin.NativeAllocatorCheck;
				int k = nativeCount++;
				bool check = chk >= 2 || (chk == 1 && (k < NativeCheckFirst || k % NativeCheckEvery == 0));
				if (NativeSolve(effs, target, check))
				{
					return;
				}
			}
			SolveInner(effs, target);
		}

		private static unsafe int Bits(float f)
		{
			return *(int*)&f;
		}

		private static bool Same(float a, float b)
		{
			return Bits(a) == Bits(b) || (a != a && b != b);
		}

		// true: the solve is done (native result applied, or, on a checked solve, the C# result with the native one compared to it).
		// false: nothing was changed and the caller solves in C#.
		private unsafe bool NativeSolve(List<Effector> effs, Vector3 target, bool check)
		{
			int n = effs.Count;
			if (n > FcsNative.MaxEff)
			{
				System.Threading.Interlocked.Increment(ref FcsNative.Fallbacks);
				return false;
			}
			if (nbuf == null)
			{
				nbuf = new FcsNative.EffN[FcsNative.MaxEff];
			}
			FcsNative.AllocIo io = default(FcsNative.AllocIo);
			fixed (FcsNative.EffN* b = nbuf)
			{
				for (int i = 0; i < n; i++)
				{
					Effector e = effs[i];
					int N = e.N;
					if (N < 0 || N > FcsNative.MaxN || (N > 0 && (e.grid == null || e.M == null || e.grid.Length < N || e.M.Length < N)))
					{
						System.Threading.Interlocked.Increment(ref FcsNative.Fallbacks);
						return false;
					}
					FcsNative.EffN* d = b + i;
					d->active = e.active ? 1 : 0;
					d->kind = (e is TvcEffector) ? 1 : ((e is SurfaceEffector se && se.kind == SurfaceKind.Stabilator) ? 2 : 0);
					int pi = -1;
					Effector p = e.partner;
					if (p != null)
					{
						for (int k = 0; k < n; k++)
						{
							if (ReferenceEquals(effs[k], p))
							{
								pi = k;
								break;
							}
						}
					}
					d->partner = pi;
					d->rollGroup = e.rollGroup;
					d->pitchGroup = e.pitchGroup;
					d->twoHorizon = e.twoHorizon ? 1 : 0;
					d->n = N;
					d->nodeOn = e.nodeOn ? 1 : 0;
					d->nodeK = e.nodeK;
					d->min = e.min;
					d->max = e.max;
					d->now = e.now;
					d->cmd = e.cmd;
					d->rate = e.rate;
					d->reachTau = e.reachTau;
					d->mu = e.mu;
					d->lam = e.lam;
					d->lo = e.lo;
					d->hi = e.hi;
					d->pairSign = e.pairSign;
					d->symCost = e.symCost;
					d->symTarget = e.symTarget;
					d->antiCost = e.antiCost;
					d->anchor = e.anchor;
					d->band = e.band;
					d->anchorK = e.anchorK;
					d->regN = e.RegN;
					d->step = e.Step;
					d->nodeC = e.nodeC;
					d->nodeDx = e.nodeD.x;
					d->nodeDy = e.nodeD.y;
					d->nodeDz = e.nodeD.z;
					d->pcls = PairClass(e);
					d->side = PairSide(e);
					float[] yt = e.yTrue;
					d->ytrueOn = (yt != null && N > 0 && yt.Length >= N) ? 1 : 0;
					if (d->ytrueOn != 0)
					{
						fixed (float* ys = yt)
						{
							float* dy = d->ytrue;
							for (int k = 0; k < N; k++)
							{
								dy[k] = ys[k];
							}
						}
					}
					if (N > 0)
					{
						fixed (float* g = e.grid)
						{
							float* dg = d->grid;
							for (int k = 0; k < N; k++)
							{
								dg[k] = g[k];
							}
						}
						fixed (Vector3* mv = e.M)
						{
							float* s = (float*)mv;
							float* dm = d->m;
							int n3 = 3 * N;
							for (int k = 0; k < n3; k++)
							{
								dm[k] = s[k];
							}
						}
					}
				}
				io.lmFactor = LmFactor;
				io.n = n;
				io.gnIterations = gnIterations;
				io.pairedTrimSearch = PairedTrimSearch ? 1 : 0;
				io.fightStabSplitOnly = FightStabSplitOnly ? 1 : 0;
				io.reachPass = ReachPass ? 1 : 0;
				io.twoHorizon = TwoHorizon ? 1 : 0;
				io.twoHorizonPath = TwoHorizonPath ? 1 : 0;
				io.coordNewton = CoordNewton;
				io.axX = axisWeight.x;
				io.axY = axisWeight.y;
				io.axZ = axisWeight.z;
				io.regScale = regScale;
				io.fightK = fightK;
				io.fightW[0] = fightW[0];
				io.fightW[1] = fightW[1];
				io.fightW[2] = fightW[2];
				io.fightW[3] = fightW[3];
				io.pitchFightK = pitchFightK;
				io.pitchFightSign = pitchFightSign;
				io.inv[0] = i00;
				io.inv[1] = i01;
				io.inv[2] = i02;
				io.inv[3] = i10;
				io.inv[4] = i11;
				io.inv[5] = i12;
				io.inv[6] = i20;
				io.inv[7] = i21;
				io.inv[8] = i22;
				io.fightTargetMin = FightTargetMin;
				io.pairHorizon = PairHorizon;
				io.tX = target.x;
				io.tY = target.y;
				io.tZ = target.z;
				io.lastCost = lastCost;
				io.resX = resDbg.x;
				io.resY = resDbg.y;
				io.resZ = resDbg.z;
				io.lastNeededPick = lastNeededPick ? 1 : 0;
				io.pairBlocks = pairBlocks ? 1 : 0;
				io.pairSet = PairBlockSet;
				io.pairTrue = PairTrueCurves ? 1 : 0;
				io.pairPasses = PairBlockPasses;
				io.pairHorizonB = PairBlockHorizon;
				io.horizonOn = HorizonObjective ? 1 : 0;
				io.horizonSteps = HorizonSteps;
				io.branchStride = BranchStride;
				io.branchScan = branchScan ? 1 : 0;
				io.horizonT = HorizonT;
				int rc = FcsNative.Solve(&io, b);
				if (check)
				{
					// both: the C# solve is the one that counts; the native answer in the buffers is compared to it
					int r0 = reachMoves, t0 = twoHorizonBound, c0 = coordNewtonMoves, p0 = pairedMoves, q0 = pairBlockMoves;
					SolveInner(effs, target);
					System.Threading.Interlocked.Increment(ref FcsNative.Checked);
					if (rc != 0)
					{
						System.Threading.Interlocked.Increment(ref FcsNative.Mismatches);
						FcsNative.Disable("the library returned error " + rc + " on a checked solve");
						return true;
					}
					int bad = -1;
					float far = 0f;
					for (int i = 0; i < n; i++)
					{
						if (!Same(effs[i].cmd, b[i].cmd))
						{
							if (bad < 0)
							{
								bad = i;
							}
							float dc = Mathf.Abs(effs[i].cmd - b[i].cmd);
							if (!(dc <= far))
							{
								far = dc;
							}
						}
					}
					bool same = bad < 0 && Same(lastCost, io.lastCost) && Same(resDbg.x, io.resX) && Same(resDbg.y, io.resY) && Same(resDbg.z, io.resZ)
						&& (lastNeededPick ? 1 : 0) == io.lastNeededPick && reachMoves - r0 == io.reachMoves && twoHorizonBound - t0 == io.twoHorizonBound
						&& coordNewtonMoves - c0 == io.coordNewtonMoves && pairedMoves - p0 == io.pairedMoves && pairBlockMoves - q0 == io.pairMoves;
					if (same)
					{
						System.Threading.Interlocked.Increment(ref FcsNative.Identical);
					}
					else if (!FcsNative.StrictFloat)
					{
						// the game's Mono: last-digit differences are expected; count how far apart the two answers are
						if (far <= FcsNative.CloseDeg)
						{
							System.Threading.Interlocked.Increment(ref FcsNative.Close);
						}
						else if (Mathf.Abs(lastCost - io.lastCost) <= FcsNative.TieRel * Mathf.Abs(lastCost))
						{
							System.Threading.Interlocked.Increment(ref FcsNative.Tie);
						}
						else
						{
							System.Threading.Interlocked.Increment(ref FcsNative.Mismatches);
						}
					}
					else
					{
						System.Threading.Interlocked.Increment(ref FcsNative.Mismatches);
						string what = (bad >= 0)
							? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} command {1:R} (C#) / {2:R} (native)", effs[bad].name, effs[bad].cmd, b[bad].cmd)
							: string.Format(System.Globalization.CultureInfo.InvariantCulture, "cost {0:R} / {1:R}, residual {2:R} {3:R} {4:R} / {5:R} {6:R} {7:R}, counters {8} {9} {10} {11} / {12} {13} {14} {15}",
								lastCost, io.lastCost, resDbg.x, resDbg.y, resDbg.z, io.resX, io.resY, io.resZ,
								reachMoves - r0, twoHorizonBound - t0, coordNewtonMoves - c0, pairedMoves - p0, io.reachMoves, io.twoHorizonBound, io.coordNewtonMoves, io.pairedMoves);
						FcsNative.Disable("a checked solve differed from the C# solve: " + what);
					}
					return true;
				}
				if (rc != 0)
				{
					System.Threading.Interlocked.Increment(ref FcsNative.Fallbacks);
					FcsNative.Disable("the library returned error " + rc);
					return false;
				}
				for (int i = 0; i < n; i++)
				{
					effs[i].cmd = b[i].cmd;
				}
				lastCost = io.lastCost;
				resDbg = new Vector3(io.resX, io.resY, io.resZ);
				lastNeededPick = io.lastNeededPick != 0;
				reachMoves += io.reachMoves;
				twoHorizonBound += io.twoHorizonBound;
				coordNewtonMoves += io.coordNewtonMoves;
				pairedMoves += io.pairedMoves;
				pairBlockMoves += io.pairMoves;
				horizonPicks += io.horizonPicks;
				branchRefines += io.branchRefines;
				branchTaken += io.branchTaken;
				for (int k = 0; k < PairKinds; k++)
				{
					pairBlockMovesK[k] += io.pairMovesK[k];
				}
				System.Threading.Interlocked.Increment(ref FcsNative.Solves);
				return true;
			}
		}

		private void SolveInner(List<Effector> effs, Vector3 target)
		{
			bool any = false;
			if (ReachPass)
			{
				foreach (Effector e in effs)
				{
					if (e.active && e.reachTau > 0f) { any = true; break; }
				}
			}
			if (!any)
			{
				SolveCore(effs, target);
				return;
			}
			// F-22E 3.1.0 (ParallelAllocator): the reach plan below depends only on the commands before this solve, so it can start now,
			// on copies, while this thread does the destination solve and the reach pass. Started only when the last solve needed the
			// two-horizon pick (it comes in runs during manoeuvres); otherwise, or if no copy set is free, the sequential path below.
			ReachJob rj = null;
			if (parallelReach && TwoHorizon && ParallelReachOn && lastNeededPick)
			{
				rj = KickReach(effs, target);
			}
			rchAll.Clear();
			rchCmd0.Clear();
			foreach (Effector e in effs)
			{
				rchAll.Add(e);
				rchCmd0.Add(e.cmd);
			}
			SolveCore(effs, target);
			if (HorizonObjective && TwoHorizon)
			{
				HorizonPick(effs, target, rj);
				return;
			}
			if (!TwoHorizon)
			{
				rj?.Drop();
				ReachFrom(effs, target);
				return;
			}
			// 2.42 (TwoHorizon): the destination plan (each stab sent where the solve wants it, the rest placed against where it will be in
			// reachTau) against the reach plan (each stab only as far as it gets in reachTau, everything solved together): each scored over
			// two horizons - the cost in reachTau and the cost once the stabs are where they were sent - and the cheaper kept. The
			// destination plan's near cost is the reach solve's, its far cost the first solve's; the reach plan's is the same at both.
			// The near horizon alone is the 1.8.14 stab rate bound; the far one alone, the 2.41 reach pass.
			float jD = lastCost;
			ReachFrom(effs, target);
			if (rchE.Count == 0)
			{
				rj?.Drop();
				lastNeededPick = false;
				return;
			}
			bool anyTH = false;
			foreach (Effector e in rchE) if (e.twoHorizon) anyTH = true;
			if (!anyTH)
			{
				rj?.Drop();
				lastNeededPick = false;
				return;
			}
			lastNeededPick = true;
			float jR = lastCost;
			thR.Clear();
			foreach (Effector e in effs) thR.Add(e.cmd);
			if (TwoHorizonPath)
			{
				// the destination plan's near cost over its whole transit: the stabs walked from where they are to where they were sent
				// (the rest as the reach solve placed them), the moment error averaged along the way
				Vector3 oth = Vector3.zero;
				foreach (Effector e in effs) if (e.active && !rchE.Contains(e)) oth += e.MAt(e.cmd);
				float acc = 0f;
				for (int k = 1; k <= 8; k++)
				{
					float f = k / 8f;
					Vector3 tot = oth;
					for (int i = 0; i < rchE.Count; i++) { Effector e = rchE[i]; tot += e.MAt(e.now + f * (e.cmd - e.now)); }
					acc += Cost(tot - target);
				}
				jR = Mathf.Max(jR, acc / 8f);
			}
			if (rj != null)
			{
				// the reach plan from the worker: its cost, its residual and its commands, exactly as the sequential solve below leaves them
				rj.Join();
				reachUsed++;
				ReachUsedTotal++;
				float jBw = rj.cost;
				lastCost = jBw;
				resDbg = rj.resDbg;
				coordNewtonMoves += rj.coordMoves;
				pairedMoves += rj.pairMoves;
				pairBlockMoves += rj.blockMoves;
				for (int k = 0; k < PairKinds; k++)
				{
					pairBlockMovesK[k] += rj.blockMovesK[k];
				}
				if (2f * jBw <= jR + jD)
				{
					twoHorizonBound++;
					ReachPickedTotal++;
					List<Effector> we = rj.mirror.effs;
					for (int i = 0; i < rchAll.Count; i++) rchAll[i].cmd = we[i].cmd;
					return;
				}
				for (int i = 0; i < rchAll.Count; i++) rchAll[i].cmd = thR[i];
				return;
			}
			thLo.Clear(); thHi.Clear();
			for (int i = 0; i < rchAll.Count; i++)
			{
				Effector e = rchAll[i];
				thLo.Add(e.lo); thHi.Add(e.hi);
				e.cmd = rchCmd0[i];
				if (e.active && e.reachTau > 0f && e.twoHorizon)
				{
					float reach = Mathf.Max(1f, e.rate) * e.reachTau;
					float lo = Mathf.Max(e.lo, Mathf.Min(e.now, e.hi) - reach), hi = Mathf.Min(e.hi, Mathf.Max(e.now, e.lo) + reach);
					if (lo <= hi) { e.lo = lo; e.hi = hi; }
				}
			}
			SolveCore(effs, target);
			float jB = lastCost;
			for (int i = 0; i < rchAll.Count; i++) { rchAll[i].lo = thLo[i]; rchAll[i].hi = thHi[i]; }
			if (2f * jB <= jR + jD)
			{
				twoHorizonBound++;
				return;   // the reach plan's commands are in place
			}
			for (int i = 0; i < rchAll.Count; i++) rchAll[i].cmd = thR[i];
		}

		// ---- F-22E 5.1.0 (HorizonObjective; Tom 26 Nov 21:45, "my goal is it is inherent into the allocator itself"). Replaces the
		// 2.42 two-horizon pick's scoring. Each plan is scored by the allocator's own cost over the next HorizonT seconds with the slow
		// effectors (the stabs, under the reach model) moving from where they are toward the plan's commands at their servo rates: the
		// plan's cost once there, plus whatever extra moment error the trip costs before it is there (the other effectors answering
		// along the way from their local slopes, clipped to their travel). The 2.42 pick weighed the trip and the destination equally
		// however long each lasts, so a 0.4 s trip across a stab's lift peak always lost to a pose that would stay stuck for seconds
		// (21:21 log, 25.0 s: stuck 8.4, the stabs' other branch 0.006, the pick kept stuck). The plans: the destination solve (from
		// the commands in place), the reach plan (each stab only as far as its servo gets in reachTau), and on the main allocator the
		// best pair of stab table points with everything else answering linearly, solved for real (stabs held there, then freed)
		// when its linear score over the horizon beats the destination plan's. The cheapest over the horizon is commanded, as the
		// 2.42 pick did (stabs to their commands, everything else placed against where they will be in reachTau).
		// 5.1.1 (22:25 log: the stabs' commands reversed about twice as often as 5.0.2's): the horizon decides between branches only.
		// A branch is a stab pair on the other side of a fold (CrossesFold) - within one side the local stages already find the best
		// point - and the destination and reach plans, both in the basin the stabs are in, are weighed by the 2.42 rule as before.
		public static bool HorizonObjective = true;

		public static float HorizonT = 1.0f;

		public static int HorizonSteps = 10;

		public static int BranchStride = 2;

		public bool branchScan;   // main allocator only (set by FcsController; not copied to mirrors / probes)

		public int horizonPicks, branchRefines, branchTaken;


		private readonly List<float> hD = new List<float>(), hE = new List<float>(), hB = new List<float>(), hC = new List<float>(), hCE = new List<float>();

		private int[] lnIdx = new int[0];

		private Vector3[] lnG = new Vector3[0];

		private float[] lnC = new float[0], lnLo = new float[0], lnHi = new float[0], lnSlow = new float[0];

		private int lnN;

		private float lnA, lnB2, lnC2, lnD, lnE2, lnF, lnG2, lnH, lnI;

		// the other effectors' linear answer around the commands in place: E-space slope per degree, travel; slow = the effectors
		// that move at their servo rate on the horizon (skip == true), the rest answer
		private bool LinBuild(List<Effector> effs, List<Effector> slow)
		{
			int n = effs.Count;
			if (lnIdx.Length < n)
			{
				lnIdx = new int[n]; lnG = new Vector3[n]; lnC = new float[n]; lnLo = new float[n]; lnHi = new float[n];
			}
			lnN = 0;
			float sxx = 0f, sxy = 0f, sxz = 0f, syy = 0f, syz = 0f, szz = 0f;
			for (int k = 0; k < n; k++)
			{
				Effector e = effs[k];
				if (!e.active || e.M == null || e.N < 2 || slow.Contains(e))
				{
					continue;
				}
				float lo = Mathf.Max(e.min, e.lo), hi = Mathf.Min(e.max, e.hi);
				if (hi - lo < 0.01f)
				{
					continue;
				}
				float c = Mathf.Clamp(e.cmd, lo, hi);
				float h = Mathf.Max(0.5f, 0.5f * e.Step);
				float c0 = Mathf.Max(lo, c - h), c1 = Mathf.Min(hi, c + h);
				if (c1 - c0 < 1e-3f)
				{
					continue;
				}
				Vector3 g = E(e.MAt(c1) - e.MAt(c0)) / (c1 - c0);
				lnIdx[lnN] = k; lnG[lnN] = g; lnC[lnN] = c; lnLo[lnN] = lo; lnHi[lnN] = hi;
				sxx += g.x * g.x; sxy += g.x * g.y; sxz += g.x * g.z; syy += g.y * g.y; syz += g.y * g.z; szz += g.z * g.z;
				lnN++;
			}
			float eps = 1e-4f * (sxx + syy + szz) / 3f + 1e-9f;
			float a = sxx + eps, b = sxy, c3 = sxz, d = sxy, ee = syy + eps, f = syz, g3 = sxz, hh = syz, ii = szz + eps;
			float A = ee * ii - f * hh, B = -(d * ii - f * g3), C = d * hh - ee * g3;
			float det = a * A + b * B + c3 * C;
			if (!(Mathf.Abs(det) > 1e-20f))
			{
				return false;
			}
			float id = 1f / det;
			lnA = A * id; lnB2 = -(b * ii - c3 * hh) * id; lnC2 = (b * f - c3 * ee) * id;
			lnD = B * id; lnE2 = (a * ii - c3 * g3) * id; lnF = -(a * f - c3 * d) * id;
			lnG2 = C * id; lnH = -(a * hh - b * g3) * id; lnI = (a * ee - b * d) * id;
			return true;
		}

		// what is left of the E-space residual r once the answering effectors make their clipped least-squares move
		private float LinScore(Vector3 r)
		{
			Vector3 v = new Vector3(lnA * r.x + lnB2 * r.y + lnC2 * r.z, lnD * r.x + lnE2 * r.y + lnF * r.z, lnG2 * r.x + lnH * r.y + lnI * r.z);
			Vector3 got = Vector3.zero;
			for (int q = 0; q < lnN; q++)
			{
				Vector3 g = lnG[q];
				float du = g.x * v.x + g.y * v.y + g.z * v.z;
				float c = lnC[q];
				float u = Mathf.Clamp(c + du, lnLo[q], lnHi[q]);
				got += g * (u - c);
			}
			return (r - got).sqrMagnitude;
		}

		// the plan's cost over the horizon: jDest once every slow effector is at its command, before that jDest plus the extra
		// moment error of the slow effectors still on their way (the others answering linearly around the plan's commands)
		private float HorizonCost(List<Effector> effs, List<Effector> slow, float jDest, Vector3 target)
		{
			int K = Mathf.Max(1, HorizonSteps);
			float h = HorizonT / K;
			float tArr = 0f;
			for (int i = 0; i < slow.Count; i++)
			{
				Effector e = slow[i];
				tArr = Mathf.Max(tArr, Mathf.Abs(e.cmd - e.now) / Mathf.Max(1f, e.rate));
			}
			if (tArr <= h)
			{
				return jDest;
			}
			Vector3 oth = Vector3.zero;
			foreach (Effector e in effs)
			{
				if (e.active && !slow.Contains(e))
				{
					oth += e.MAt(e.cmd);
				}
			}
			Vector3 dest = oth;
			for (int i = 0; i < slow.Count; i++)
			{
				dest += slow[i].MAt(slow[i].cmd);
			}
			float s0 = LinScore(E(target - dest));
			float acc = 0f;
			for (int k = 1; k <= K; k++)
			{
				float t = k * h;
				if (t >= tArr)
				{
					acc += jDest * (K - k + 1);
					break;
				}
				// the slow effectors move together and arrive together (the allocator re-plans them every tick, so they are kept in
				// step rather than each racing to its command at full rate)
				float f = t / tArr;
				Vector3 tot = oth;
				for (int i = 0; i < slow.Count; i++)
				{
					Effector e = slow[i];
					tot += e.MAt(e.now + f * (e.cmd - e.now));
				}
				acc += jDest + Mathf.Max(0f, LinScore(E(target - tot)) - s0);
			}
			return acc / K;
		}

		private readonly List<Effector> hSlow = new List<Effector>(), hStab = new List<Effector>();

		private void HorizonPick(List<Effector> effs, Vector3 target, ReachJob rj)
		{
			float jD = lastCost;
			Vector3 resD = resDbg;
			hD.Clear();
			foreach (Effector e in effs) hD.Add(e.cmd);
			hSlow.Clear();
			foreach (Effector e in effs)
			{
				if (e.active && e.reachTau > 0f && e.twoHorizon) hSlow.Add(e);
			}
			ReachFrom(effs, target);
			bool needB = rchE.Count > 0;
			if (needB)
			{
				bool anyTH = false;
				foreach (Effector e in rchE) if (e.twoHorizon) anyTH = true;
				needB = anyTH;
			}
			hStab.Clear();
			foreach (Effector e in hSlow)
			{
				if (e is SurfaceEffector se && se.kind == SurfaceKind.Stabilator && e.M != null && e.N >= 2) hStab.Add(e);
			}
			bool scan = branchScan && hStab.Count == 2;
			lastNeededPick = needB;
			if (!needB && !scan)
			{
				rj?.Drop();
				return;   // nothing past its reach and no branch to look for: the destination plan as the reach pass leaves it
			}
			hE.Clear();
			foreach (Effector e in effs) hE.Add(e.cmd);
			float jE = lastCost;
			Vector3 resE = resDbg;
			// the destination plan over the horizon (its commands, the slow effectors from where they are)
			for (int i = 0; i < effs.Count; i++) effs[i].cmd = hD[i];
			float JhD = LinBuild(effs, hSlow) ? HorizonCost(effs, hSlow, jD, target) : jD;
			float best = JhD;
			int pick = 0;
			float jB = 0f, JhB = float.MaxValue;
			Vector3 resB = Vector3.zero;
			if (needB)
			{
				if (rj != null)
				{
					rj.Join();
					reachUsed++;
					ReachUsedTotal++;
					jB = rj.cost;
					resB = rj.resDbg;
					coordNewtonMoves += rj.coordMoves;
					pairedMoves += rj.pairMoves;
					pairBlockMoves += rj.blockMoves;
					for (int k = 0; k < PairKinds; k++)
					{
						pairBlockMovesK[k] += rj.blockMovesK[k];
					}
					hB.Clear();
					List<Effector> we = rj.mirror.effs;
					for (int i = 0; i < rchAll.Count; i++) hB.Add(we[i].cmd);
				}
				else
				{
					thLo.Clear(); thHi.Clear();
					for (int i = 0; i < rchAll.Count; i++)
					{
						Effector e = rchAll[i];
						thLo.Add(e.lo); thHi.Add(e.hi);
						e.cmd = rchCmd0[i];
						if (e.active && e.reachTau > 0f && e.twoHorizon)
						{
							float reach = Mathf.Max(1f, e.rate) * e.reachTau;
							float lo = Mathf.Max(e.lo, Mathf.Min(e.now, e.hi) - reach), hi = Mathf.Min(e.hi, Mathf.Max(e.now, e.lo) + reach);
							if (lo <= hi) { e.lo = lo; e.hi = hi; }
						}
					}
					SolveCore(effs, target);
					jB = lastCost;
					resB = resDbg;
					for (int i = 0; i < rchAll.Count; i++) { rchAll[i].lo = thLo[i]; rchAll[i].hi = thHi[i]; }
					hB.Clear();
					foreach (Effector e in effs) hB.Add(e.cmd);
				}
				for (int i = 0; i < effs.Count; i++) effs[i].cmd = hB[i];
				JhB = LinBuild(effs, hSlow) ? HorizonCost(effs, hSlow, jB, target) : jB;
				// 5.1.1: the reach plan against the destination plan by the 2.42 rule - both are the same basin, and within a basin
				// the horizon costs of the two differ by little more than the linear model's noise (5.1.0 judged them over the
				// horizon and the pick flipped between them: half of its extra stab reversals)
				if (2f * jB <= jE + jD)
				{
					best = JhB;
					pick = 1;
				}
			}
			else
			{
				rj?.Drop();
			}
			float jC = 0f, jCE = 0f;
			Vector3 resC = Vector3.zero, resCE = Vector3.zero;
			if (scan)
			{
				// the best pair of stab table points, everything else answering linearly around the destination plan
				for (int i = 0; i < effs.Count; i++) effs[i].cmd = hD[i];
				Effector sL = hStab[0], sR = hStab[1];
				if (LinBuild(effs, hStab))
				{
					Vector3 oth = Vector3.zero;
					foreach (Effector e in effs) if (e.active && e != sL && e != sR) oth += e.MAt(e.cmd);
					Vector3 r0 = E(target - oth);
					float lLo = Mathf.Max(sL.min, sL.lo) - 1e-4f, lHi = Mathf.Min(sL.max, sL.hi) + 1e-4f, rLo = Mathf.Max(sR.min, sR.lo) - 1e-4f, rHi = Mathf.Min(sR.max, sR.hi) + 1e-4f;
					int st = Mathf.Max(1, BranchStride);
					float sBest = float.MaxValue;
					int iB = -1, jBi = -1;
					for (int i = 0; i < sL.N; i += st)
					{
						float gL = sL.grid[i];
						if (gL < lLo || gL > lHi) continue;
						Vector3 rL = r0 - E(sL.M[i]);
						for (int j = 0; j < sR.N; j += st)
						{
							float gR = sR.grid[j];
							if (gR < rLo || gR > rHi) continue;
							float sc = LinScore(rL - E(sR.M[j]));
							if (sc < sBest) { sBest = sc; iB = i; jBi = j; }
						}
					}
					float dL = sL.cmd, dR = sR.cmd;
					// 5.1.1: only a pair across a fold from the destination plan is another branch (5.1.0 took any pair a grid step
					// away, and a coarse grid point of the same basin won or lost on noise-level cost differences every tick)
					if (iB >= 0 && (CrossesFold(sL, dL, sL.grid[iB]) || CrossesFold(sR, dR, sR.grid[jBi])))
					{
						// both on the same linear model over the horizon: worth a real solve only if the pair beats the destination plan there
						float sD = LinScore(r0 - E(sL.MAt(dL)) - E(sR.MAt(dR)));
						float pD = HorizonProxy(sL, sR, dL, dR, r0, sD);
						float pC = HorizonProxy(sL, sR, sL.grid[iB], sR.grid[jBi], r0, sBest);
						if (pC < pD)
						{
							branchRefines++;
							float sLlo = sL.lo, sLhi = sL.hi, sRlo = sR.lo, sRhi = sR.hi;
							sL.lo = sL.hi = sL.cmd = sL.grid[iB];
							sR.lo = sR.hi = sR.cmd = sR.grid[jBi];
							SolveCore(effs, target);
							sL.lo = sLlo; sL.hi = sLhi; sR.lo = sRlo; sR.hi = sRhi;
							SolveCore(effs, target);
							jC = lastCost;
							resC = resDbg;
							hC.Clear();
							foreach (Effector e in effs) hC.Add(e.cmd);
							float JhC = LinBuild(effs, hSlow) ? HorizonCost(effs, hSlow, jC, target) : jC;
							if (JhC < best && (CrossesFold(sL, dL, sL.cmd) || CrossesFold(sR, dR, sR.cmd)))
							{
								best = JhC;
								pick = 2;
								// commanded as the destination plan is: stabs to their commands, the rest against where they will be in reachTau
								ReachFrom(effs, target);
								jCE = lastCost;
								resCE = resDbg;
								hCE.Clear();
								foreach (Effector e in effs) hCE.Add(e.cmd);
							}
						}
					}
				}
			}
			horizonPicks++;
			if (pick == 1)
			{
				twoHorizonBound++;
				ReachPickedTotal++;
				for (int i = 0; i < effs.Count; i++) effs[i].cmd = hB[i];
				lastCost = jB;
				resDbg = resB;
				return;
			}
			if (pick == 2)
			{
				branchTaken++;
				for (int i = 0; i < effs.Count; i++) effs[i].cmd = hCE[i];
				lastCost = jCE;
				resDbg = resCE;
				return;
			}
			for (int i = 0; i < effs.Count; i++) effs[i].cmd = hE[i];
			lastCost = jE;
			resDbg = resE;
		}

		// 5.1.1: true when the effector's moment, walked over its table from a to b, turns back on itself (its projection on the chord from
		// M(a) to M(b) is not monotone): a and b sit on different sides of a fold - for a stab, of a lift peak. Within one side the
		// local stages already find the best point, so only a crossing is another branch.
		private bool CrossesFold(Effector e, float a, float b)
		{
			if (e.N < 3)
			{
				return false;
			}
			float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
			Vector3 ma = E(e.MAt(lo)), mb = E(e.MAt(hi));
			Vector3 ch = mb - ma;
			float prevP = 0f;
			for (int i = 0; i < e.N; i++)
			{
				float g = e.grid[i];
				if (g <= lo || g >= hi)
				{
					continue;
				}
				Vector3 d = E(e.M[i]) - ma;
				float p = d.x * ch.x + d.y * ch.y + d.z * ch.z;
				if (p < prevP)
				{
					return true;
				}
				prevP = p;
			}
			Vector3 dEnd = mb - ma;
			return dEnd.x * ch.x + dEnd.y * ch.y + dEnd.z * ch.z < prevP;
		}

		// HorizonCost on the linear model alone (scan screening): the pair's linear score once there, plus the extra linear error
		// while the two stabs travel there from where they are
		private float HorizonProxy(Effector sL, Effector sR, float cL, float cR, Vector3 r0, float sDest)
		{
			int K = Mathf.Max(1, HorizonSteps);
			float h = HorizonT / K;
			float tArr = Mathf.Max(Mathf.Abs(cL - sL.now) / Mathf.Max(1f, sL.rate), Mathf.Abs(cR - sR.now) / Mathf.Max(1f, sR.rate));
			if (tArr <= h)
			{
				return sDest;
			}
			float acc = 0f;
			for (int k = 1; k <= K; k++)
			{
				float t = k * h;
				if (t >= tArr)
				{
					acc += sDest * (K - k + 1);
					break;
				}
				float f = t / tArr;
				float pl = sL.now + f * (cL - sL.now), pr = sR.now + f * (cR - sR.now);
				acc += sDest + Mathf.Max(0f, LinScore(r0 - E(sL.MAt(pl)) - E(sR.MAt(pr))) - sDest);
			}
			return acc / K;
		}

		// ---- F-22E 3.1.0 (ParallelAllocator): the two-horizon reach plan on a worker
		public static bool ParallelReachOn = true;

		private bool lastNeededPick;

		private ReachJob[] reachJobs;   // created on first use (main allocator only: a mirror allocator never runs a reach plan in parallel)

		public int reachKicks, reachUsed;

		public static int ReachKicksTotal, ReachUsedTotal, ReachPickedTotal;   // bench / diagnostics (main thread only)

		private ReachJob KickReach(List<Effector> effs, Vector3 target)
		{
			if (reachJobs == null)
			{
				reachJobs = new ReachJob[] { new ReachJob(), new ReachJob() };
			}
			foreach (ReachJob j in reachJobs)
			{
				if (j.Idle)
				{
					j.Start(effs, this, target);
					reachKicks++;
					ReachKicksTotal++;
					return j;
				}
			}
			return null;
		}

		// the sequential path's third solve, on the worker's copies: every two-horizon effector bounded to what its servo reaches in
		// reachTau from where it is, from the commands before this solve
		private void ReachPlanOn(List<Effector> effs, Vector3 target)
		{
			foreach (Effector e in effs)
			{
				if (e.active && e.reachTau > 0f && e.twoHorizon)
				{
					float reach = Mathf.Max(1f, e.rate) * e.reachTau;
					float lo = Mathf.Max(e.lo, Mathf.Min(e.now, e.hi) - reach), hi = Mathf.Min(e.hi, Mathf.Max(e.now, e.lo) + reach);
					if (lo <= hi) { e.lo = lo; e.hi = hi; }
				}
			}
			SolveCore(effs, target);
		}

		internal sealed class ReachJob : Claimable
		{
			public readonly EffectorMirror mirror = new EffectorMirror();
			private Vector3 tgt;
			public float cost;
			public Vector3 resDbg;
			public int coordMoves, pairMoves, blockMoves;

			public readonly int[] blockMovesK = new int[PairKinds];

			public void Start(List<Effector> effs, Allocator main, Vector3 target)
			{
				mirror.Sync(effs, main);
				tgt = target;
				Queue();
			}

			protected override void Work()
			{
				Allocator a = mirror.alloc;
				int c0 = a.coordNewtonMoves, p0 = a.pairedMoves, b0 = a.pairBlockMoves;
				for (int k = 0; k < PairKinds; k++)
				{
					blockMovesK[k] = -a.pairBlockMovesK[k];
				}
				a.ReachPlanOn(mirror.effs, tgt);
				cost = a.lastCost;
				resDbg = a.resDbg;
				coordMoves = a.coordNewtonMoves - c0;
				pairMoves = a.pairedMoves - p0;
				blockMoves = a.pairBlockMoves - b0;
				for (int k = 0; k < PairKinds; k++)
				{
					blockMovesK[k] += a.pairBlockMovesK[k];
				}
			}

			public void Join()
			{
				JoinCore(out _);
			}

			public void Drop()
			{
				Abandon();
			}
		}

		public static bool TwoHorizon = true;

		public static bool TwoHorizonPath = true;
		public int twoHorizonBound;
		private readonly List<float> thR = new List<float>();
		private readonly List<float> thLo = new List<float>();
		private readonly List<float> thHi = new List<float>();

		// the reach pass from the commands now in place (the destinations); rchAll / rchCmd0 hold the pre-solve commands
		private void ReachFrom(List<Effector> effs, Vector3 target)
		{
			rchE.Clear();
			rchDest.Clear();
			rchLo.Clear();
			rchHi.Clear();
			foreach (Effector e in effs)
			{
				if (!e.active || e.reachTau <= 0f) continue;
				float reach = Mathf.Max(1f, e.rate) * e.reachTau;
				if (Mathf.Abs(e.cmd - e.now) <= reach) continue;
				rchE.Add(e);
				rchDest.Add(e.cmd);
				rchLo.Add(e.lo);
				rchHi.Add(e.hi);
			}
			if (rchE.Count == 0) return;
			reachMoves++;
			for (int i = 0; i < rchAll.Count; i++) rchAll[i].cmd = rchCmd0[i];
			for (int i = 0; i < rchE.Count; i++)
			{
				Effector e = rchE[i];
				float reach = Mathf.Max(1f, e.rate) * e.reachTau;
				float at = e.now + Mathf.Clamp(rchDest[i] - e.now, -reach, reach);
				e.lo = e.hi = e.cmd = at;
			}
			SolveCore(effs, target);
			for (int i = 0; i < rchE.Count; i++)
			{
				Effector e = rchE[i];
				e.lo = rchLo[i];
				e.hi = rchHi[i];
				e.cmd = rchDest[i];
			}
		}

		private void SolveCore(List<Effector> effs, Vector3 target)
		{
			act.Clear();
			foreach (Effector eff in effs)
			{
				if (eff.active)
				{
					act.Add(eff);
				}
			}
			int count = act.Count;
			if (count == 0)
			{
				return;
			}
			Ensure(count);
			for (int i = 0; i < count; i++)
			{
				x[i] = Mathf.Clamp(act[i].cmd, act[i].lo, act[i].hi);
				prev[i] = act[i].cmd;
			}
			for (int j = 0; j < count; j++)
			{
				pidx[j] = ((act[j].partner != null) ? act.IndexOf(act[j].partner) : (-1));
				grp[j] = ((fightK > 0f) ? act[j].rollGroup : 0);
				r0[j] = act[j].MAt(0f).x;
				pgrp[j] = ((pitchFightK > 0f) ? act[j].pitchGroup : 0);
				y0[j] = act[j].MAt(0f).y;
			}
			nBlk = 0;
			if (pairBlocks)
			{
				for (int j = 0; j < count; j++)
				{
					pcls[j] = PairClass(act[j]);
					pside[j] = PairSide(act[j]);
				}
				BuildBlocks(count);
			}
			fightSignHint = 0.0;
			if (fightK > 0f && FightTargetMin > 0f)
			{
				double tr = (double)i00 * (double)target.x;
				for (int jh = 0; jh < count; jh++)
				{
					tr -= (double)i00 * r0[jh];
				}
				if (Math.Abs(tr * 57.29578) > FightTargetMin)
				{
					fightSignHint = (tr >= 0.0) ? 1.0 : (-1.0);
				}
			}
			float num = Total(target);
			for (int k = 0; k < gnIterations; k++)
			{
				Vector3 zero = Vector3.zero;
				for (int l = 0; l < count; l++)
				{
					zero += act[l].MAt((float)x[l]);
				}
				Vector3 vector = E(zero - target);
				for (int m = 0; m < count; m++)
				{
					Effector effector = act[m];
					float num2 = 0.5f * effector.Step;
					float num3 = Mathf.Max(effector.min, (float)x[m] - num2);
					float num4 = Mathf.Min(effector.max, (float)x[m] + num2);
					Jc[m] = ((num4 > num3) ? (E(effector.MAt(num4) - effector.MAt(num3)) / (num4 - num3)) : Vector3.zero);
					Jp[m] = ((pgrp[m] != 0 && num4 > num3) ? (Math.Sqrt(pitchFightK) * (double)i11 * (double)(effector.MAt(num4).y - effector.MAt(num3).y) / (double)(num4 - num3)) : 0.0);
					Jg[m] = ((grp[m] != 0 && num4 > num3) ? (Math.Sqrt(fightK * fightW[grp[m]]) * (double)i00 * (double)(effector.MAt(num4).x - effector.MAt(num3).x) / (double)(num4 - num3)) : 0.0);
					if (grp[m] != 0 && num4 > num3 && StabSplitMode(m))
					{
						double xm = x[m];
						x[m] = num4;
						double g4 = GroupRoll(grp[m]);
						x[m] = num3;
						double g3 = GroupRoll(grp[m]);
						x[m] = xm;
						Jg[m] = Math.Sqrt(fightK * fightW[grp[m]]) * (g4 - g3) / (double)(num4 - num3);
					}
				}
				if (pitchFightK > 0f)
				{
					double pa = GroupPitch(1);
					double pb = GroupPitch(2);
					double sgP = (pa + pb >= 0.0) ? 1.0 : (-1.0);
					bool pat = pa * (double)pitchFightSign > 0.0 && pb * (double)pitchFightSign < 0.0;
					pgOn[1] = pat && pa * sgP < 0.0;
					pgOn[2] = pat && pb * sgP < 0.0;
					pgR[1] = pgOn[1] ? (Math.Sqrt(pitchFightK) * pa) : 0.0;
					pgR[2] = pgOn[2] ? (Math.Sqrt(pitchFightK) * pb) : 0.0;
				}
				else
				{
					pgOn[1] = (pgOn[2] = false);
				}
				if (fightK > 0f)
				{
					double sgF = NetSign();
					for (int g = 1; g <= FightGroups; g++)
					{
						double gr = GroupRoll(g);
						grOn[g] = gr * sgF < 0.0;
						grR[g] = grOn[g] ? (Math.Sqrt(fightK * fightW[g]) * gr) : 0.0;
					}
				}
				for (int n = 0; n < count; n++)
				{
					Effector effector2 = act[n];
					float num5 = effector2.RegN;
					double num6 = 1.0 / (double)(num5 * num5);
					for (int num7 = 0; num7 < count; num7++)
					{
						H[n, num7] = Vector3.Dot(Jc[n], Jc[num7]);
						if (pgrp[n] != 0 && pgrp[n] == pgrp[num7] && pgOn[pgrp[n]])
						{
							H[n, num7] += Jp[n] * Jp[num7];
						}
						if (grp[n] != 0 && grp[n] == grp[num7] && grOn[grp[n]])
						{
							H[n, num7] += Jg[n] * Jg[num7];
						}
					}
					double num8 = LmFactor * H[n, n] + 1E-09;
					double num9 = regScale;
					H[n, n] += num9 * (double)(effector2.mu + effector2.lam) * num6 + num8;
					rhs[n] = 0.0 - ((double)Vector3.Dot(Jc[n], vector) + num9 * ((double)effector2.mu * num6 * x[n] + (double)effector2.lam * num6 * (x[n] - prev[n])));
					if (grp[n] != 0 && grOn[grp[n]])
					{
						rhs[n] -= Jg[n] * grR[grp[n]];
					}
					if (pgrp[n] != 0 && pgOn[pgrp[n]])
					{
						rhs[n] -= Jp[n] * pgR[pgrp[n]];
					}
					if (effector2.anchorK > 0f)
					{
						float bx = BudgetExcess(effector2, (float)x[n]);
						if (bx != 0f)
						{
							H[n, n] += (double)effector2.anchorK * num6;
							rhs[n] -= (double)effector2.anchorK * num6 * (double)bx;
						}
					}
				}
				for (int num10 = 0; num10 < count; num10++)
				{
					int num11 = pidx[num10];
					if (num11 >= 0)
					{
						Effector effector3 = act[num10];
						Effector effector4 = act[num11];
						float num12 = effector3.RegN;
						double num13 = 1.0 / (double)(num12 * num12);
						double num14 = effector3.pairSign;
						double num15 = effector4.pairSign;
						double num16 = 0.25 * (double)regScale * (double)effector3.symCost * num13;
						double num17 = 0.25 * (double)regScale * (double)effector3.antiCost * num13;
						double num18 = num14 * x[num10] - num15 * x[num11] - (num14 - num15) * (double)effector3.symTarget;
						double num19 = num14 * x[num10] + num15 * x[num11];
						H[num10, num10] += num16 + num17;
						H[num11, num11] += num16 + num17;
						H[num10, num11] += (0.0 - num16 + num17) * num14 * num15;
						H[num11, num10] += (0.0 - num16 + num17) * num14 * num15;
						rhs[num10] -= num16 * num14 * num18 + num17 * num14 * num19;
						rhs[num11] -= (0.0 - num16) * num15 * num18 + num17 * num15 * num19;
					}
				}
				if (!SolveLinear(H, rhs, dx, count))
				{
					break;
				}
				double num20 = 1.0;
				bool flag = false;
				double[] array = new double[count];
				int num21 = 0;
				while (num21 < 4 && !flag)
				{
					for (int num22 = 0; num22 < count; num22++)
					{
						array[num22] = x[num22];
					}
					for (int num23 = 0; num23 < count; num23++)
					{
						x[num23] = Mathf.Clamp((float)(array[num23] + num20 * dx[num23]), act[num23].lo, act[num23].hi);
					}
					float num24 = Total(target);
					if (num24 < num)
					{
						num = num24;
						flag = true;
					}
					else
					{
						for (int num25 = 0; num25 < count; num25++)
						{
							x[num25] = array[num25];
						}
					}
					num21++;
					num20 *= 0.5;
				}
				if (!flag)
				{
					break;
				}
			}
			Vector3 vector2 = Vector3.zero;
			for (int num26 = 0; num26 < count; num26++)
			{
				vector2 += act[num26].MAt((float)x[num26]);
			}
			for (int num27 = 0; num27 < count; num27++)
			{
				Effector effector5 = act[num27];
				float num28 = (float)x[num27];
				float p = (float)prev[num27];
				Vector3 vector3 = vector2 - effector5.MAt(num28);
				Vector3 vector4 = target - vector3;
				float num29 = Cost(effector5.MAt(num28) - vector4) + Reg(effector5, num28, p) + PairRegWith(num27, num28) + FightCostWith(num27, num28) + PitchFightCostWith(num27, num28);
				float num30 = num28;
				int num31 = -1;
				for (int num32 = 0; num32 < effector5.N; num32++)
				{
					float num33 = effector5.grid[num32];
					if (!(num33 < effector5.lo - 0.0001f) && !(num33 > effector5.hi + 0.0001f))
					{
						float num34 = Cost(effector5.M[num32] - vector4) + Reg(effector5, num33, p) + PairRegWith(num27, num33) + FightCostWith(num27, num33) + PitchFightCostWith(num27, num33);
						if (num34 < num29 - 1E-09f)
						{
							num29 = num34;
							num30 = num33;
							num31 = num32;
						}
					}
				}
				if (num31 > 0 && num31 < effector5.N - 1)
				{
					float c = effector5.grid[num31 - 1];
					float c2 = effector5.grid[num31 + 1];
					float num35 = Cost(effector5.M[num31 - 1] - vector4) + Reg(effector5, c, p) + PairRegWith(num27, c) + FightCostWith(num27, c) + PitchFightCostWith(num27, c);
					float num36 = Cost(effector5.M[num31 + 1] - vector4) + Reg(effector5, c2, p) + PairRegWith(num27, c2) + FightCostWith(num27, c2) + PitchFightCostWith(num27, c2);
					float num37 = num35 - 2f * num29 + num36;
					if (num37 > 1E-12f)
					{
						float num38 = Mathf.Clamp(effector5.grid[num31] + Mathf.Clamp(0.5f * (num35 - num36) / num37, -0.5f, 0.5f) * effector5.Step, effector5.lo, effector5.hi);
						float num39 = Cost(effector5.MAt(num38) - vector4) + Reg(effector5, num38, p) + PairRegWith(num27, num38) + FightCostWith(num27, num38) + PitchFightCostWith(num27, num38);
						if (num39 < num29)
						{
							num29 = num39;
							num30 = num38;
						}
					}
				}
				// 2.4.7: a local Newton candidate. The pass above only tries the effector's grid points (the nozzles': every
				// 4 deg) and a parabola through the best of them, so an effector sitting between two points stays put when
				// neither point beats where it is, however large the miss. The Gauss-Newton steps before it are joint: with
				// the stabs on their stall plateau and a large yaw miss the line search rejects them all, and the nozzles,
				// the one pitch effector with a gradient, were never moved (bench, corkscrew release at 64 deg: nozzles
				// frozen 0.15 s at -3.8 deg with a 2-10 deg/s^2 nose-down pitch miss; the hold sagged to q -2.5).
				if (CoordNewton == 2 || (CoordNewton == 1 && effector5 is TvcEffector))
				{
					float xc = (float)x[num27];
					float hN = 0.25f * effector5.Step;
					float aN = Mathf.Max(effector5.lo, xc - hN);
					float bN = Mathf.Min(effector5.hi, xc + hN);
					if (bN > aN + 1E-05f)
					{
						Vector3 gN = E(effector5.MAt(bN) - effector5.MAt(aN)) / (bN - aN);
						Vector3 rN = E(effector5.MAt(xc) - vector4);
						float rn2 = effector5.RegN * effector5.RegN;
						double denN = (double)Vector3.Dot(gN, gN) + (double)(regScale * (effector5.mu + effector5.lam) / rn2);
						if (denN > 1E-12)
						{
							double grN = (double)Vector3.Dot(gN, rN) + (double)(regScale * (effector5.mu * xc + effector5.lam * (xc - p)) / rn2);
							float cN = Mathf.Clamp((float)((double)xc - grN / denN), effector5.lo, effector5.hi);
							float costN = Cost(effector5.MAt(cN) - vector4) + Reg(effector5, cN, p) + PairRegWith(num27, cN) + FightCostWith(num27, cN) + PitchFightCostWith(num27, cN);
							if (costN < num29)
							{
								num29 = costN;
								num30 = cN;
								coordNewtonMoves++;
							}
						}
					}
				}
				x[num27] = num30;
				vector2 = vector3 + effector5.MAt(num30);
			}
			if (PairedTrimSearch)
			{
				PairedSearch(target, ref vector2);
			}
			// F-22E 5.0.0: the exact pair search, last, so its pose is the one commanded (see PairBlockSearch)
			if (pairBlocks && nBlk > 0)
			{
				long pt0 = PairStat ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
				PairBlockSearch(target);
				if (PairStat)
				{
					System.Threading.Interlocked.Add(ref PairStatTicks, System.Diagnostics.Stopwatch.GetTimestamp() - pt0);
					System.Threading.Interlocked.Increment(ref PairStatSolves);
					if (timeSolves)
					{
						System.Threading.Interlocked.Increment(ref PairStatMain);
					}
				}
			}
			// F-22E 3.0.1: what the allocation leaves unmade of its target (telemetry: unR / unY)
			resDbg = MomentSum() - target;
			for (int num40 = 0; num40 < count; num40++)
			{
				act[num40].cmd = Mathf.Clamp((float)x[num40], act[num40].lo, act[num40].hi);
				x[num40] = act[num40].cmd;
			}
			lastCost = Total(target);
		}

		// F-22E 3.0.1: the allocation's moment minus its target (body axes, N m), and the conversion to deg/s^2 (telemetry)
		public Vector3 resDbg;

		public Vector3 Accel(Vector3 m)
		{
			return new Vector3(i00 * m.x + i01 * m.y + i02 * m.z, i10 * m.x + i11 * m.y + i12 * m.z, i20 * m.x + i21 * m.y + i22 * m.z) * 57.29578f;
		}

		private Vector3 MomentSum()
		{
			Vector3 t = Vector3.zero;
			for (int i = 0; i < act.Count; i++)
			{
				t += act[i].MAt((float)x[i]);
			}
			return t;
		}

		// ---- F-22E 5.0.0 pair-complete allocator (Tom 26 Nov 18:11: "effectors must never sit in poses that oppose and cancel each
		// other", no gates, latches or clamps; design: claude/F22E_pair_allocator_design_2026-11-26.md).
		// The stages above follow local slopes (the joint Gauss-Newton steps) or move one effector at a time over its whole table (the
		// per-effector pass); the 1.8.18 paired search moves a stab with the nozzles taking its pitch. A pose that needs two effectors
		// to move at once survives all of them: 17:40 log, 234 s, TVC off, right roll bled from 35 to 1 deg AoA, the right stab at
		// -28 (trailing edge down, the high-AoA roll pose) and the left at +13: both rolled the jet left and held each other only by
		// cancelling in pitch. Left down + right up is pure roll at the same pitch, but no single-stab move gets there, and past the
		// right stab's lift peak its roll curve had a dip, so a small step back lost roll.
		// Here every block of two effectors that trade each other's off-axis effects is searched over every pair of their table
		// points the servos reach within PairBlockHorizon (an unreachable cell cannot be commanded anyway), everything else held, on
		// the allocator's own objective (moment error, regularisation and move cost, pair costs, fight costs). Blocks are swept until
		// none improves (at most PairBlockPasses). At the end no move of any one or two effectors in a block lowers the cost, so a
		// cancelling pair cannot persist, whatever the AoA, speed, TVC state or where the peaks are. It runs last so the pose it
		// leaves is the one commanded.
		// PairTrueCurves: the stabilators' pitch is their true curve (past the lift peak included, SurfaceEffector.rawY) instead of the
		// monotone envelope the slope-following stages use: the envelope is flat past the peak and so hides which side of the fold
		// a stab is on (a stab deep past its peak looks as nose-down as one at it).
		// Blocks (PairBlockSet 1 = the first, 2 = all): stab L + R; each wing trailing-edge pair (outboard, inboard); rudder L + R;
		// each stab + the nozzles; each stab + the same side's outboard and inboard flaperon.
		public bool pairBlocks;

		public static int PairBlockSet = 1;

		public static bool PairTrueCurves = false;

		public static float PairBlockHorizon = 0.1f;

		public static int PairBlockPasses = 3;

		public const int PairKinds = 5;

		public const int MaxBlocks = 16;

		public int pairBlockMoves;

		// diagnostics (bench): wall time and count of the pair searches, candidate pairs costed, of them cut short by the bound
		internal static bool PairStat;

		internal static readonly long[] Seg = new long[4];   // bench: candidate cache / block terms / pair loop / (spare)

		internal static long PairStatAll, PairStatAllN, PairStatMain, PairStatTicks, PairStatSolves, PairStatCand, PairStatFull;

		public readonly int[] pairBlockMovesK = new int[PairKinds];

		private int nBlk;

		private readonly int[] blkA = new int[MaxBlocks];

		private readonly int[] blkB = new int[MaxBlocks];

		private readonly int[] blkK = new int[MaxBlocks];

		private int[] pcls = new int[0];

		private int[] pside = new int[0];

		private double[] y0A = new double[0];

		private const int MaxCand = 34;   // the current command + up to 32 table points; the last slot holds the refinement's trial point

		private const int TrialSlot = MaxCand - 1;

		// Per effector, per pass, flat [effector * MaxCand + slot]: its candidates and what does not depend on the block (deflection,
		// roll and pitch moment on the true curve, E(moment), Reg, table index). Rebuilt only when a block moves the effector.
		private int[] ecN = new int[0];

		private float[] ecC = new float[0], ecMx = new float[0], ecMy = new float[0], ecEx = new float[0], ecEy = new float[0], ecEz = new float[0], ecReg = new float[0];

		private int[] ecGi = new int[0];

		// Per effector and mode, flat [(effector * 2 + mode) * MaxCand + slot]: its block terms - Reg plus the pair costs of it alone
		// (s), its roll-fight terms by group (t1..t3) and its pitch-fight term (d). Mode 1: its pair partner is the block's other member
		// (their pair cost and a split stab's roll term are joint, worked out per pair); mode 0: the partner is held. A member's terms
		// are the same in every block of the same mode (the left stab's with the nozzles and with each left flaperon), so each is
		// worked out once per pass and again only when a move changes what it reads.
		private bool[] mtOk = new bool[0];

		private float[] mtS = new float[0];

		private double[] mtT1 = new double[0], mtT2 = new double[0], mtT3 = new double[0], mtD = new double[0];

		private Vector3[] mvAll = new Vector3[0];

		private double[] rtAll = new double[0];

		private double[] pyAll = new double[0];

		private readonly double[] baseG = new double[FightGroups + 1];

		private double baseP1, baseP2;

		private float e0x, e0y, e0z;

		private float swX, swY, swZ;   // Mathf.Sqrt(axisWeight.*), as E() works them out

		private int blkJp, blkJp2, blkJs;

		// the block being searched: members, their offsets into the caches
		private int bA, bB, oA, oB, tA, tB;

		// the effector's class for the blocks: 1 stabilator, 2 wing trailing edge (flaperon / aileron), 3 rudder, 4 nozzles, 0 other
		private static int PairClass(Effector e)
		{
			if (e is TvcEffector)
			{
				return 4;
			}
			if (e is SurfaceEffector se)
			{
				switch (se.kind)
				{
				case SurfaceKind.Stabilator:
					return 1;
				case SurfaceKind.Flaperon:
				case SurfaceKind.Aileron:
					return 2;
				case SurfaceKind.Rudder:
					return 3;
				}
			}
			return 0;
		}

		private static int PairSide(Effector e)
		{
			string n = e.name;
			if (n == null)
			{
				return 0;
			}
			if (n.EndsWith("_L"))
			{
				return -1;
			}
			return n.EndsWith("_R") ? 1 : 0;
		}

		private void AddBlock(int a, int b, int k)
		{
			if (nBlk < MaxBlocks)
			{
				blkA[nBlk] = a;
				blkB[nBlk] = b;
				blkK[nBlk] = k;
				nBlk++;
			}
		}

		// the blocks over the active effectors (act order); the native core builds the same list from the same fields
		private void BuildBlocks(int count)
		{
			nBlk = 0;
			for (int i = 0; i < count; i++)
			{
				if (pcls[i] == 1 && pidx[i] >= 0 && pcls[pidx[i]] == 1)
				{
					AddBlock(i, pidx[i], 0);
				}
			}
			if (PairBlockSet < 2)
			{
				return;
			}
			for (int i = 0; i < count; i++)
			{
				if (pcls[i] == 2 && pidx[i] >= 0 && pcls[pidx[i]] == 2)
				{
					AddBlock(i, pidx[i], 1);
				}
			}
			for (int i = 0; i < count; i++)
			{
				if (pcls[i] == 3 && pidx[i] >= 0 && pcls[pidx[i]] == 3)
				{
					AddBlock(i, pidx[i], 2);
				}
			}
			for (int i = 0; i < count; i++)
			{
				if (pcls[i] != 1)
				{
					continue;
				}
				for (int t = 0; t < count; t++)
				{
					if (pcls[t] == 4)
					{
						AddBlock(i, t, 3);
					}
				}
			}
			for (int i = 0; i < count; i++)
			{
				if (pcls[i] != 1 || pside[i] == 0)
				{
					continue;
				}
				for (int j = 0; j < count; j++)
				{
					if (pcls[j] == 2 && pside[j] == pside[i])
					{
						AddBlock(i, j, 4);
					}
				}
			}
		}

		// Effector.MAt with the pitch read from the true curve where there is one
		private Vector3 MA(int i, float c)
		{
			Effector e = act[i];
			Vector3 m = e.MAt(c);
			if (PairTrueCurves && e.yTrue != null && e.N >= 2 && e.yTrue.Length >= e.N)
			{
				float[] y = e.yTrue;
				float num = (c - e.min) / e.Step;
				if (num <= 0f)
				{
					m.y = y[0];
				}
				else if (num >= (float)(e.N - 1))
				{
					m.y = y[e.N - 1];
				}
				else
				{
					int k = (int)num;
					float f = num - (float)k;
					m.y = y[k] + (y[k + 1] - y[k]) * f;
				}
			}
			return m;
		}

		// GroupRoll's term for one effector at the present x
		private double RollTerm(int i)
		{
			if (StabSplitMode(i))
			{
				int j = pidx[i];
				return SplitTerm(i, (float)x[i], act[i].MAt((float)x[i]).x, (float)x[j]);
			}
			return (double)i00 * ((double)act[i].MAt((float)x[i]).x - r0[i]);
		}

		// a split-mode stab's roll-fight term with it at ci (roll moment mxi there) and its partner at cp
		private double SplitTerm(int i, float ci, float mxi, float cp)
		{
			int j = pidx[i];
			double sym = 0.5 * ((double)act[i].pairSign * (double)ci - (double)act[j].pairSign * (double)cp);
			float xs = (float)((double)act[i].pairSign * sym);
			return (double)i00 * ((double)mxi - (double)act[i].MAt(xs).x);
		}

		// PairReg(i) with i at ci and its partner at cp
		private float PairRegAt(int i, float ci, float cp)
		{
			Effector e = act[i];
			Effector e2 = act[pidx[i]];
			float n2 = e.RegN;
			float n3 = e.pairSign * ci / n2;
			float n4 = e2.pairSign * cp / n2;
			float n5 = 0.5f * (n3 - n4) - 0.5f * (e.pairSign - e2.pairSign) * e.symTarget / n2;
			float n6 = 0.5f * (n3 + n4);
			return regScale * (e.symCost * n5 * n5 + e.antiCost * n6 * n6);
		}

		// how many pair relations effector k is in (its own partner, and effectors whose partner it is)
		private int PairRelations(int k)
		{
			int n = (pidx[k] >= 0) ? 1 : 0;
			int count = act.Count;
			for (int i = 0; i < count; i++)
			{
				if (pidx[i] == k)
				{
					n++;
				}
			}
			return n;
		}

		// the cached moment, roll-fight and pitch-fight term of effector i at the present x
		private void PairCacheOne(int i)
		{
			mvAll[i] = MA(i, (float)x[i]);
			rtAll[i] = (grp[i] != 0) ? RollTerm(i) : 0.0;
			pyAll[i] = (pgrp[i] != 0) ? ((double)i11 * ((double)mvAll[i].y - y0A[i])) : 0.0;
		}

		// one candidate (moment m on the true curve, deflection c, table index g) into effector cache slot o; E() and Reg() as in the
		// solve, E with the search's cached square roots (the same arithmetic)
		private void EcPut(Effector e, int o, float c, int g, Vector3 m, float p)
		{
			float num = i00 * m.x + i01 * m.y + i02 * m.z;
			float num2 = i10 * m.x + i11 * m.y + i12 * m.z;
			float num3 = i20 * m.x + i21 * m.y + i22 * m.z;
			ecC[o] = c;
			ecMx[o] = m.x;
			ecMy[o] = m.y;
			ecEx[o] = num * swX;
			ecEy[o] = num2 * swY;
			ecEz[o] = num3 * swZ;
			ecReg[o] = Reg(e, c, p);
			ecGi[o] = g;
		}

		// effector k's candidates: where it is now in the solve, then every table point inside its travel that its servo reaches within
		// PairBlockHorizon of where it is (a table point's moment is the table's own, as in the per-effector pass)
		private void EcBuild(int k)
		{
			Effector e = act[k];
			float p = (float)prev[k];
			float x0 = (float)x[k];
			int o = k * MaxCand;
			EcPut(e, o, x0, -1, MA(k, x0), p);
			int n = 1;
			int N = e.N;
			float[] grid = e.grid;
			Vector3[] M = e.M;
			float[] yt = (PairTrueCurves && e.yTrue != null && N >= 2 && e.yTrue.Length >= N) ? e.yTrue : null;
			float lo = e.lo - 0.0001f;
			float hi = e.hi + 0.0001f;
			float now = e.now;
			float reach = Mathf.Max(e.Step, PairBlockHorizon * Mathf.Max(1f, e.rate));
			for (int g = 0; g < N && n < TrialSlot; g++)
			{
				float gs = grid[g];
				if (gs < lo || gs > hi || Mathf.Abs(gs - now) > reach || gs == x0)
				{
					continue;
				}
				Vector3 m = M[g];
				if (yt != null)
				{
					m.y = yt[g];
				}
				EcPut(e, o + n, gs, g, m, p);
				n++;
			}
			ecN[k] = n;
			mtOk[2 * k] = false;
			mtOk[2 * k + 1] = false;
		}

		// what effector k's block terms read besides its own candidate, worked out once per build: its partner held (mode 0), the
		// effectors whose partner it is (held, mode 0; in mode 1 that relation is the joint one), and whether it is a split stab
		private int mtPk, mtNq;

		private float mtXpk;

		private bool mtSplitK;

		private readonly int[] mtQ = new int[8];

		private readonly bool[] mtQsplit = new bool[8];

		private void MtSetup(int k)
		{
			int count = act.Count;
			mtPk = pidx[k];
			mtXpk = (mtPk >= 0) ? ((float)x[mtPk]) : 0f;
			mtSplitK = grp[k] != 0 && StabSplitMode(k);
			mtNq = 0;
			for (int i = 0; i < count && mtNq < mtQ.Length; i++)
			{
				if (i != k && pidx[i] == k)
				{
					mtQ[mtNq] = i;
					mtQsplit[mtNq] = grp[i] != 0 && StabSplitMode(i);
					mtNq++;
				}
			}
		}

		// the block terms of effector k's candidate in slot q (written at t of the mode's term cache); MtSetup(k) first
		private void MtPut(int k, int mode, int q, int t)
		{
			int o = k * MaxCand + q;
			float c = ecC[o];
			float s = ecReg[o];
			if (mode == 0)
			{
				if (mtPk >= 0)
				{
					s += PairRegAt(k, c, mtXpk);
				}
				for (int r = 0; r < mtNq; r++)
				{
					int i = mtQ[r];
					s += PairRegAt(i, (float)x[i], c);
				}
			}
			mtS[t] = s;
			double t1 = 0.0, t2 = 0.0, t3 = 0.0;
			int gk = grp[k];
			if (gk != 0 && !(mode == 1 && mtSplitK))
			{
				double v = mtSplitK ? SplitTerm(k, c, ecMx[o], mtXpk) : ((double)i00 * ((double)ecMx[o] - r0[k]));
				if (gk == 1)
				{
					t1 += v;
				}
				else if (gk == 2)
				{
					t2 += v;
				}
				else
				{
					t3 += v;
				}
			}
			if (mode == 0)
			{
				// a held split stab whose partner is k
				for (int r = 0; r < mtNq; r++)
				{
					if (!mtQsplit[r])
					{
						continue;
					}
					int i = mtQ[r];
					double v = SplitTerm(i, (float)x[i], mvAll[i].x, c);
					if (grp[i] == 1)
					{
						t1 += v;
					}
					else if (grp[i] == 2)
					{
						t2 += v;
					}
					else
					{
						t3 += v;
					}
				}
			}
			mtT1[t] = t1;
			mtT2[t] = t2;
			mtT3[t] = t3;
			mtD[t] = (pgrp[k] != 0) ? ((double)i11 * ((double)ecMy[o] - y0A[k])) : 0.0;
		}

		private void MtBuild(int k, int mode)
		{
			MtSetup(k);
			int t = (2 * k + mode) * MaxCand;
			int n = ecN[k];
			for (int q = 0; q < n; q++)
			{
				MtPut(k, mode, q, t + q);
			}
			mtOk[2 * k + mode] = true;
		}

		// a trial deflection (the refinement) into effector k's spare slot, both caches
		private void Trial(int k, int mode, float c)
		{
			Effector e = act[k];
			EcPut(e, k * MaxCand + TrialSlot, c, -1, MA(k, c), (float)prev[k]);
			MtSetup(k);
			MtPut(k, mode, TrialSlot, (2 * k + mode) * MaxCand + TrialSlot);
		}

		// the block's cost with member A at its slot i and B at slot j, everything else held: the moment error and the members' own
		// terms (PairHead), then the joint pair cost, roll fight and pitch fight (PairTail; each term >= 0)
		private float PairHead(int i, int j)
		{
			int ia = oA + i;
			int ib = oB + j;
			float vx = e0x + ecEx[ia] + ecEx[ib];
			float vy = e0y + ecEy[ia] + ecEy[ib];
			float vz = e0z + ecEz[ia] + ecEz[ib];
			float c = vx * vx + vy * vy + vz * vz;
			c += mtS[tA + i];
			c += mtS[tB + j];
			return c;
		}

		private float PairTail(float c, int i, int j)
		{
			if (PairStat)
			{
				PairStatFull++;
			}
			float ca = ecC[oA + i];
			float cb = ecC[oB + j];
			if (blkJp >= 0)
			{
				c += PairRegAt(blkJp, ca, cb);
			}
			if (blkJp2 >= 0)
			{
				c += PairRegAt(blkJp2, cb, ca);
			}
			if (fightK > 0f)
			{
				double g1 = baseG[1] + mtT1[tA + i] + mtT1[tB + j];
				double g2 = baseG[2] + mtT2[tA + i] + mtT2[tB + j];
				double g3 = baseG[3] + mtT3[tA + i] + mtT3[tB + j];
				if (blkJs >= 0)
				{
					double v = (blkJs == bA) ? SplitTerm(blkJs, ca, ecMx[oA + i], cb) : SplitTerm(blkJs, cb, ecMx[oB + j], ca);
					int gj = grp[blkJs];
					if (gj == 1)
					{
						g1 += v;
					}
					else if (gj == 2)
					{
						g2 += v;
					}
					else
					{
						g3 += v;
					}
				}
				double sg = (fightSignHint != 0.0) ? fightSignHint : ((g1 + g2 + g3 >= 0.0) ? 1.0 : (-1.0));
				double t = 0.0;
				if (g1 * sg < 0.0)
				{
					t += (double)fightW[1] * g1 * g1;
				}
				if (g2 * sg < 0.0)
				{
					t += (double)fightW[2] * g2 * g2;
				}
				if (g3 * sg < 0.0)
				{
					t += (double)fightW[3] * g3 * g3;
				}
				c += (float)((double)fightK * t);
			}
			if (pitchFightK > 0f)
			{
				double pa = baseP1;
				double pb = baseP2;
				int ga = pgrp[bA];
				int gb = pgrp[bB];
				if (ga == 1)
				{
					pa += mtD[tA + i];
				}
				else if (ga == 2)
				{
					pb += mtD[tA + i];
				}
				if (gb == 1)
				{
					pa += mtD[tB + j];
				}
				else if (gb == 2)
				{
					pb += mtD[tB + j];
				}
				c += (float)((double)pitchFightK * PFTerm(pa, pb));
			}
			return c;
		}

		private float PairCostFull(int i, int j)
		{
			return PairTail(PairHead(i, j), i, j);
		}

		private void PairBlockSearch(Vector3 target)
		{
			int count = act.Count;
			swX = Mathf.Sqrt(axisWeight.x);
			swY = Mathf.Sqrt(axisWeight.y);
			swZ = Mathf.Sqrt(axisWeight.z);
			for (int i = 0; i < count; i++)
			{
				y0A[i] = (double)MA(i, 0f).y;
			}
			for (int i = 0; i < count; i++)
			{
				PairCacheOne(i);
				ecN[i] = -1;
			}
			for (int pass = 0; pass < PairBlockPasses; pass++)
			{
				bool moved = false;
				for (int k = 0; k < nBlk; k++)
				{
					int a = blkA[k];
					int b = blkB[k];
					if (SearchBlock(a, b, blkK[k], target, count))
					{
						moved = true;
						// what a move changes: the members' cached moments and candidates, and every term that reads their x (their
						// partners' held-partner terms, a held split stab's roll term)
						for (int i = 0; i < count; i++)
						{
							if (i == a || i == b || (grp[i] != 0 && StabSplitMode(i) && (pidx[i] == a || pidx[i] == b)))
							{
								PairCacheOne(i);
							}
							if (i == a || i == b || pidx[i] == a || pidx[i] == b || (pidx[a] == i) || (pidx[b] == i))
							{
								mtOk[2 * i] = false;
								mtOk[2 * i + 1] = false;
							}
						}
						ecN[a] = -1;
						ecN[b] = -1;
					}
				}
				if (!moved)
				{
					break;
				}
			}
		}

		private unsafe bool SearchBlock(int a, int b, int kind, Vector3 target, int count)
		{
			long st0 = PairStat ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
			if (ecN[a] < 0)
			{
				EcBuild(a);
			}
			if (ecN[b] < 0)
			{
				EcBuild(b);
			}
			int na = ecN[a];
			int nb = ecN[b];
			if (na < 2 && nb < 2)
			{
				return false;
			}
			int modeA = (pidx[a] == b || pidx[b] == a) ? 1 : 0;
			if (modeA == 1 && (PairRelations(a) != 1 || PairRelations(b) != 1))
			{
				return false;   // the block-term cache assumes a joint pair's members have no other pair partner (true of this jet)
			}
			if (!mtOk[2 * a + modeA])
			{
				MtBuild(a, modeA);
			}
			if (!mtOk[2 * b + modeA])
			{
				MtBuild(b, modeA);
			}
			long st1 = PairStat ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
			bA = a;
			bB = b;
			oA = a * MaxCand;
			oB = b * MaxCand;
			tA = (2 * a + modeA) * MaxCand;
			tB = (2 * b + modeA) * MaxCand;
			blkJp = (pidx[a] == b) ? a : (-1);
			blkJp2 = (pidx[b] == a) ? b : (-1);
			blkJs = -1;
			if (grp[a] != 0 && StabSplitMode(a) && pidx[a] == b)
			{
				blkJs = a;
			}
			else if (grp[b] != 0 && StabSplitMode(b) && pidx[b] == a)
			{
				blkJs = b;
			}
			Vector3 oth = Vector3.zero;
			for (int g = 0; g <= FightGroups; g++)
			{
				baseG[g] = 0.0;
			}
			baseP1 = 0.0;
			baseP2 = 0.0;
			for (int i = 0; i < count; i++)
			{
				if (i == a || i == b)
				{
					continue;
				}
				oth += mvAll[i];
				if (grp[i] != 0 && !(StabSplitMode(i) && (pidx[i] == a || pidx[i] == b)))
				{
					baseG[grp[i]] += rtAll[i];
				}
				if (pgrp[i] == 1)
				{
					baseP1 += pyAll[i];
				}
				else if (pgrp[i] == 2)
				{
					baseP2 += pyAll[i];
				}
			}
			Vector3 e0 = E(oth - target);
			e0x = e0.x;
			e0y = e0.y;
			e0z = e0.z;
			float best = PairCostFull(0, 0);
			float bound = best - 1E-09f;
			int bi = 0;
			int bj = 0;
			fixed (float* ex = ecEx, ey = ecEy, ez = ecEz, ms = mtS)
			{
				float* bx = ex + oB;
				float* by = ey + oB;
				float* bz = ez + oB;
				float* bs = ms + tB;
				for (int i = 0; i < na; i++)
				{
					float ux = e0x + ex[oA + i];
					float uy = e0y + ey[oA + i];
					float uz = e0z + ez[oA + i];
					float sa = ms[tA + i];
					for (int j = (i == 0) ? 1 : 0; j < nb; j++)
					{
						// PairHead written out (the same arithmetic in the same order)
						float vx = ux + bx[j];
						float vy = uy + by[j];
						float vz = uz + bz[j];
						float c = vx * vx + vy * vy + vz * vz;
						c += sa;
						c += bs[j];
						// every later term is >= 0 and adding one never lowers a float sum, so a pair whose head already reaches the
						// bound cannot be kept: its tail is not worked out
						if (c < bound)
						{
							c = PairTail(c, i, j);
							if (c < bound)
							{
								best = c;
								bound = best - 1E-09f;
								bi = i;
								bj = j;
							}
						}
					}
				}
			}
			if (PairStat)
			{
				PairStatCand += na * nb - 1;
				long st2 = System.Diagnostics.Stopwatch.GetTimestamp();
				Seg[0] += st1 - st0;
				Seg[2] += st2 - st1;
			}
			if (bi == 0 && bj == 0)
			{
				return false;
			}
			// a parabola through the best cell's table neighbours, one member at a time with the other held (as the per-effector pass);
			// the trial points go in the spare slot
			Effector ea = act[a];
			Effector eb = act[b];
			int g0 = ecGi[oA + bi];
			if (g0 > 0 && g0 < ea.N - 1)
			{
				Trial(a, modeA, ea.grid[g0 - 1]);
				float jL = PairCostFull(TrialSlot, bj);
				Trial(a, modeA, ea.grid[g0 + 1]);
				float jR = PairCostFull(TrialSlot, bj);
				float den = jL - 2f * best + jR;
				if (den > 1E-12f)
				{
					float cN = Mathf.Clamp(ea.grid[g0] + Mathf.Clamp(0.5f * (jL - jR) / den, -0.5f, 0.5f) * ea.Step, ea.lo, ea.hi);
					Trial(a, modeA, cN);
					float jN = PairCostFull(TrialSlot, bj);
					if (jN < best)
					{
						best = jN;
						bi = TrialSlot;
					}
				}
			}
			int g1 = ecGi[oB + bj];
			if (g1 > 0 && g1 < eb.N - 1)
			{
				Trial(b, modeA, eb.grid[g1 - 1]);
				float jL = PairCostFull(bi, TrialSlot);
				Trial(b, modeA, eb.grid[g1 + 1]);
				float jR = PairCostFull(bi, TrialSlot);
				float den = jL - 2f * best + jR;
				if (den > 1E-12f)
				{
					float cN = Mathf.Clamp(eb.grid[g1] + Mathf.Clamp(0.5f * (jL - jR) / den, -0.5f, 0.5f) * eb.Step, eb.lo, eb.hi);
					Trial(b, modeA, cN);
					float jN = PairCostFull(bi, TrialSlot);
					if (jN < best)
					{
						best = jN;
						bj = TrialSlot;
					}
				}
			}
			x[a] = ecC[oA + bi];
			x[b] = ecC[oB + bj];
			pairBlockMoves++;
			pairBlockMovesK[kind]++;
			return true;
		}

		public static int CoordNewton = 1;

		public int coordNewtonMoves;

		public static bool RegOrigRange = true;

		public bool PairedTrimSearch;

		public static float PairHorizon = 0.1f;

		public int pairedMoves;

		private void PairedSearch(Vector3 target, ref Vector3 total)
		{
			int it = -1;
			for (int i = 0; i < act.Count; i++)
			{
				if (act[i] is TvcEffector)
				{
					it = i;
					break;
				}
			}
			if (it < 0)
			{
				return;
			}
			Effector tv = act[it];
			if (tv.hi - tv.lo < 0.05f)
			{
				return;
			}
			Vector3 tLo = tv.MAt(tv.lo);
			Vector3 tHi = tv.MAt(tv.hi);
			float kY = (tHi.y - tLo.y) / (tv.hi - tv.lo);
			if (Mathf.Abs(kY) < 1E-06f)
			{
				return;
			}
			for (int s = 0; s < act.Count; s++)
			{
				if (!(act[s] is SurfaceEffector surfaceEffector) || surfaceEffector.kind != SurfaceKind.Stabilator)
				{
					continue;
				}
				float xs = (float)x[s];
				float xt = (float)x[it];
				Vector3 mS = surfaceEffector.MAt(xs);
				Vector3 mT = tv.MAt(xt);
				Vector3 others = total - mS - mT;
				float best = Cost(total - target) + Reg(surfaceEffector, xs, (float)prev[s]) + PairRegWith(s, xs) + Reg(tv, xt, (float)prev[it]) + FightCostWith(s, xs) + PFPair(s, xs, it, xt);
				float bs = xs;
				float bt = xt;
				Vector3 bMS = mS;
				Vector3 bMT = mT;
				for (int g = 0; g < surfaceEffector.N; g++)
				{
					float gs = surfaceEffector.grid[g];
					if (gs < surfaceEffector.lo - 0.0001f || gs > surfaceEffector.hi + 0.0001f || Mathf.Abs(gs - surfaceEffector.now) > Mathf.Max(surfaceEffector.Step, PairHorizon * Mathf.Max(1f, surfaceEffector.rate)))
					{
						continue;
					}
					Vector3 ms = surfaceEffector.M[g];
					float gt = Mathf.Clamp(xt + (mS.y - ms.y) / kY, tv.lo, tv.hi);
					Vector3 mt = tv.MAt(gt);
					float c = Cost(others + ms + mt - target) + Reg(surfaceEffector, gs, (float)prev[s]) + PairRegWith(s, gs) + Reg(tv, gt, (float)prev[it]) + FightCostWith(s, gs) + PFPair(s, gs, it, gt);
					if (c < best - 1E-09f)
					{
						best = c;
						bs = gs;
						bt = gt;
						bMS = ms;
						bMT = mt;
					}
				}
				if (bs != xs)
				{
					x[s] = bs;
					x[it] = bt;
					total = others + bMS + bMT;
					pairedMoves++;
				}
			}
		}

		private static bool SolveLinear(double[,] A0, double[] b0, double[] outX, int n)
		{
			double[,] array = (double[,])A0.Clone();
			double[] array2 = (double[])b0.Clone();
			for (int i = 0; i < n; i++)
			{
				int num = i;
				double num2 = Math.Abs(array[i, i]);
				for (int j = i + 1; j < n; j++)
				{
					if (Math.Abs(array[j, i]) > num2)
					{
						num2 = Math.Abs(array[j, i]);
						num = j;
					}
				}
				if (num2 < 1E-18)
				{
					return false;
				}
				if (num != i)
				{
					for (int k = 0; k < n; k++)
					{
						double num3 = array[i, k];
						array[i, k] = array[num, k];
						array[num, k] = num3;
					}
					double num4 = array2[i];
					array2[i] = array2[num];
					array2[num] = num4;
				}
				for (int l = i + 1; l < n; l++)
				{
					double num5 = array[l, i] / array[i, i];
					if (num5 != 0.0)
					{
						for (int m = i; m < n; m++)
						{
							array[l, m] -= num5 * array[i, m];
						}
						array2[l] -= num5 * array2[i];
					}
				}
			}
			for (int num6 = n - 1; num6 >= 0; num6--)
			{
				double num7 = array2[num6];
				for (int num8 = num6 + 1; num8 < n; num8++)
				{
					num7 -= array[num6, num8] * outX[num8];
				}
				outX[num6] = num7 / array[num6, num6];
			}
			return true;
		}
	}
}
