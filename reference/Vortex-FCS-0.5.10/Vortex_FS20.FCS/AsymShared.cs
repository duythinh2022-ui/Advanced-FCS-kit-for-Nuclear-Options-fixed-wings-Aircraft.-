using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	internal sealed partial class FcsController
	{
		// ===== Shared asymmetric-load code: the same text in every FCS mod (only the namespace line differs). Per-jet settings live in
		// the jet's own files: FcsController.cs (AsymJetName, CplRefAuth, CplKneeOn0, AsymStabTravel, SwSlatSchedOn / SwHlAreas / SwSlatPos),
		// Plugin.cs (config) and the effectors (TvcEffector Mn / MnOn / MnNow / MnAllAxes, SurfaceEffector isCanard). =====

		// 1.9.0 low-thrust (nose-down authority) AoA cap
		public float authCap = 999f;

		public float authCapEff = 999f;

		// 2.0.9: negative AoA limit while the nozzles do not cover pitch (DisableTVC or low thrust), and the test-switch state
		public float authCapNegEff = -999f;

		private bool authInit;

		internal static float AuthCapLead = 1f;

		internal static float AuthCapTauUp = 1f;

		internal static float AuthCapTauDown = 0.3f;

		internal static float AuthCapRise = 4f;

		internal static float AuthCapRiseAtCap = 1f;

		internal static float AuthCapThrustStep = 1f;   // deg/s^2 of nozzle nose-down authority gained between sweeps

		internal static float AuthCapFastHold = 1f;     // s the fast rise stays open after the last such gain

		private float authTvPrev;

		private float authFastT;

		internal static float CoupHeldDy0 = 0.04f;

		internal static float CoupHeldDySpan = 0.08f;

		internal static float AsymPitchCapLead = 2f;

		internal static float AsymPitchCapFloor = 10f;

		public float asymCapPitch = 999f;

		private static readonly float[] asymAlphas = BuildAsymAlphas();

		private readonly float[] asymU = new float[22];

		private readonly float[] asymAr = new float[22];

		private readonly float[] asymAy = new float[22];

		private readonly float[] asymCr = new float[22];

		private readonly float[] asymCy = new float[22];

		private readonly float[] asymNetDn = new float[22];

		// 1.9.0: nose-down authority incl. the flaperons at their present deflection (asymNetDn leaves their lift out)
		private readonly float[] authNetDn = new float[22];

		private readonly float[] asymNetUp = new float[22];

		// Revoker 0.2.7 (CanardAttachedWindow)
		internal static float CanWinA0 = 30f;

		private float asymTimer = 99f;

		private bool asymValid;

		public float asymCapPos = 999f;

		public float asymCapNeg = -999f;

		public float asymCapPosEff = 999f;

		public float asymCapNegEff = -999f;

		public float asymDy;

		public float asymArNow;

		public float asymAyNow;

		public float asymUNow;

		public float asymCrNow;

		public float asymCyNow;

		public Vector3 asymMNow;

		private Lpf3 asymMF;

		public Vector3 asymFF;

		public float asymFFr;

		public float asymFFy;

		public float asymFFGate;

		public float dMeasR;

		public float dMeasY;

		private Lpf dMeasRF;

		private Lpf dTrimRF;

		public float dTrimR;

		private Lpf dMeasYF;

		private float asymLogTimer = 99f;

		private float asymLoggedCap = 999f;

		private float asymLoggedCapN = -999f;

		private int asymK = -1;

		private bool asymCycleDone;

		private bool asymFirstLogged;

		internal static int AsymPointsPerStep = 3;

		private Vector3 asymMT;

		private float[] asymDsym;

		// Compass 0.1.8 (AsymDamageGate): the asymmetric-load pitch protections (pitch cap and bias, pitch priority, stab roll price,
		// held coupling gate) were gated on the lateral CG offset alone (|asymDy| over 4-12 cm). asymDy is measured from the
		// surviving left/right surface pairs; with the Compass's outer wing, left stab and left fin shot off the only pair left was
		// the rudders, the left one hanging off the fuselage hinge, and asymDy swung -0.28 <-> 0.00 m every 15-20 s (5 Nov 20:25 log).
		// Each swing to 0 dropped the pitch cap 7 -> 12 deg, and both departures came in those windows (193 s, 217-223 s). Now a
		// control surface lost with the asymmetric-load cap in force opens the same gates.
		internal static bool AsymDamageGate = true;

		// Compass 0.1.7 (AsymPitchBias): nose-up the asymmetric-load pitch cap's model leaves out (deg/s^2, >= 0). The cap's
		// nose-down estimate is the swept bare airframe plus the stabs at full nose-down; it leaves out the other surfaces,
		// which on the T/A-30 carry the lost wing's roll and pitch nose-up as they do it, and the bare-airframe model reads
		// nose-down of the jet. Bench, right outer wing + left fin + left stab gone, full aft at 330 km/h: the ailerons /
		// flaps / rudder pitched +7..+24 deg/s^2, the airframe 7-10 more nose-up than modelled; the cap read 22-24 deg/s^2 of
		// nose-down margin at 12 deg where the jet had none (the stab on its lift peak), so it sat at 12.4 and the jet
		// departed from 12-13 deg (in game, 5 Nov 19:18 log, 138 s, the cap was 12 and its pitch part read 25-29). Measured:
		// (I wdot + w x Iw - the stabs' moment) / Iyy less the modelled airframe at the present AoA, 0.5 s low-pass, then held
		// at its peak and let down at AsymPitchBiasDecay. Only the positive (nose-up) side counts.
		internal static bool AsymPitchBias = true;

		internal static float AsymPitchBiasDecay = 2f;

		// Compass 0.1.8 (AsymPitchBiasNeg): the same at negative AoA, for a negative pitch cap (AsymPitchCapNeg). 5 Nov 20:25 log,
		// 243.5 s: full push + roll with the same damage, nose-up ran out at -7 deg (aUp at its floor, the lone stab at its
		// nose-up end) and the AoA went to -24 with full aft stick; the negative cap was the lateral one at -8.
		internal static bool AsymPitchBiasNeg = true;

		// with a stab gone, the bias also takes in the stabs' live nose-down end against the swept one at this AoA: the lone
		// slab's table reads 8-12 deg/s^2 less nose-down than the sweep at 5-8 deg (bench). Not with both stabs: there it
		// lowered the cap to 7 with the outer wing alone and cost 4 cases of 77 (bench matrix, 0.1.7 notes).
		internal static bool AsymPitchBiasStab = true;

		internal static float AsymPitchBiasTau = 0.5f;

		internal static float AsymPitchCapFloorNegNoTvc = 4f;

		// Compass 0.1.7: with no pitch TVC the asymmetric-load pitch cap may come down to 7 deg (AsymPitchCapFloor, 10, is the
		// F-22E's). Bench, right outer wing + left stab gone (with and without the left fin), full aft at 280 km/h: the slab
		// reaches its lift peak at 9-10 deg body AoA there, and a cap held at 10 departed on a roll; at 7 (~1.3 g at 280 km/h,
		// ~4 g at 470) the 20 damage cases depart once (roll spam with the fin intact) against 4-5 times at 10.
		internal static float AsymPitchCapFloorNoTvc = 7f;

		// 1.9.0: highest AoA at which the nose-down authority available now (stabs full nose-down, flaperons where they
		// are, nozzles at the present thrust - none above the vectoring cut-off speed) still leaves LowThrustCapMargin.
		internal static bool AuthCapPredict = false;

		private bool CplKneeOn => CplKneeOn0 && CplRefAuth && cplActive && CplLateral && cplLoaded;

		internal static bool CplLateral = true;          // the coupled demand drives the lateral cap (false: only the held-load pitch figure)

		internal static float CplStopFloor = 10f;

		private bool CplTop => cplActive && CplLateral && cplLoaded && CplTopEdge;

		// Ternion 0.1.1: the fixed no-TVC AoA caps off on a jet with no vectorable nozzle (bench switch)
		internal static bool NoVecUncapped = true;

		// Compass 0.1.7 (AsymPitchBias): the modelled bare-airframe pitch (deg/s^2, nose-up +) at each sweep AoA
		private readonly float[] asymAirP = new float[22];

		public float asymCapPitchNeg = -999f;

		public float asymPitchBias;

		private Lpf asymPitchBiasF;

		public float asymPitchBiasNeg;

		private Lpf asymPitchBiasNegF;

		private readonly float[] asymStabHi = new float[22];

		private readonly float[] asymStabLo = new float[22];

		public SurfaceEffector canL;

		public SurfaceEffector canR;

		private readonly float[] cplClean = new float[22];

		private readonly bool[] cplDnTv = new bool[22];

		private readonly float[] cplKa = new float[24];

		private readonly float[] cplKj = new float[24];

		public float cplKneeJ;

		private readonly float[] cplKqA = new float[22];

		public float cplLawRoll;

		private bool cplLowPos, cplLowNeg;

		private float cplRefAp, cplRefAn;

		private int cplRefI0p = -1, cplRefI1p = -1, cplRefI0n = -1, cplRefI1n = -1;

		public float cplRefPos, cplRefNeg;

		private float[] cplTvG;

		private readonly bool[] cplTvHas = new bool[4];

		private readonly Vector3[][] cplTvM = new Vector3[4][];

		private int cplTvE, cplTvN;

		private readonly float[] cplTvNow = new float[4];

		private bool cplTvOn;

		private float dmgGateHold;

		// Ternion 0.1.1: no vectorable nozzle on this jet (see the constructor)
		internal bool noVecJet;

		public float asymCapFilt = 999f;

		public float asymCapFiltNeg = -999f;

		private readonly float[] capHist = new float[3];

		private readonly float[] capHistN = new float[3];

		private int capHistI;

		private float capMed = 999f;

		private float capMedNeg = -999f;

		internal static float AsymResMaxFrac = 0.8f;

		// 1.8.45: demand at the cap (was 1.0). In-game, with the light mixed load (6.5 cm lateral CG offset, 60-110 m/s)
		// the demand rises ~0.025 per degree above the cap, so 1.08 is ~3 deg more AoA there; heavier loads rise faster.
		internal static float AsymCapUMax => Plugin.AsymCapDemand;

		internal static float AsymCapTauUp => Plugin.AsymCapSmoothing;

		internal static float AsymCapTauDown = 0.3f;

		internal static bool AsymCapGapTighten = true;

		internal static bool AsymThrustCgLoad = true;

		internal static float AsymCapGapRef = 3f;

		internal static float AsymCapGapMax = 4f;

		// Revoker 0.2.4: gravity's sideslip feed-forward fade (TAS, m/s) and cap (deg/s); F-22E / Ifrit: 40, 20, none
		// Revoker 0.2.5: 0.6 (see the authority sweep)
		internal static float ElevonAuthShare = 0.6f;

		internal static bool CplNoTvNeutral = true;

		private bool cplTvFree;

		private float cplTvFreeDn;

		// Revoker 1.1.0, port of F-22E 4.0.1 (SweepGridCache): the sweep's capacity loop evaluates each surface's whole deflection grid in every AoA point's
		// flow. The deflections' geometry does not depend on the point, so it is worked out here once per sweep tick
		// (SurfaceEffector.GridGeom, the surfaces in parallel) for the surfaces whose capacity loop at least one of the tick's points
		// runs; the points then use SurfaceEffector.EvalFlowGrid. swGrid[j] false = that surface's loop calls EvalFlow as before.
		private bool[] swGrid;

		// 4.1.0 (NativeSweep): this tick's capacity loops run in the native core (FcsNative.Grid, mode 1) where swGrid[j] is set
		private bool swNative;

		private readonly List<SurfaceEffector> swGeomList = new List<SurfaceEffector>();

		private readonly List<float[]> swGeomDefl = new List<float[]>();

		private GeomWork swGeomWork;

		// this jet: per surface, the deflections the capacity loop evaluates (the grid, or for a stabilator the AsymStabTravel list)
		private float[][] swDefl;

		private SweepWork swWork;

		private int swN;

		private Quaternion[] swRot = new Quaternion[0];

		private Vector3[] swLhs = new Vector3[0];

		private float[] swDrag = new float[0];

		private AirfoilTable[] swFoil = new AirfoilTable[0];

		private float[] swQ = new float[0];

		// the hinged flaps' game-side state for the sweep's workers (HingeFlapEffector.Snap) and their symmetric deflection (SymNow)
		private HingeFlapEffector.FlowSnap[] hSnap = new HingeFlapEffector.FlowSnap[0];

		private float[] hSym = new float[0];

		// Revoker 1.2.0 (CoupledCap, from the Ternion 0.2.2; see CoupledCap.cs). Every piece has a bench switch; CoupledCap = false is 1.1.1.
		internal static bool CoupledCap = true;

		internal static bool CplTvc = true;              // the nozzles as LP effectors (false: thrust at the present angle in the load, no nozzle authority)

		internal static float CplDeadband = 2f;          // deg/s^2 of velocity-vector roll shortfall ignored (fuel imbalance, rounding)

		internal static int CplPts = 9;                  // grid points per effector the LP takes (ends included, evenly spaced)

		internal static int CplPointsPerStep = 2;        // sweep points per tick while the LP runs (3 without it)

		internal static float CplBetaDesign = 3f;

		internal static bool CplAttachedOnly = true;

		private float cplClock;

		// bench diagnostic (run with ParallelSweep=false CplBetaDesign=0): the LP's mix for each side against the real deflections
		internal static bool CplDebug = false;

		internal static float CplDbgT2 = 0f;

		private readonly Vector3[] cplDbgNow = new Vector3[CplPoint.MaxEff];

		private readonly string[] cplDbgName = new string[CplPoint.MaxEff];

		private readonly float[] cplDbgDefl = new float[CplPoint.MaxEff];

		internal static float CplDbgT = -1f;             // bench: print one sweep's curve at this time (s)

		private bool cplActive;

		// Revoker 1.3.0: which path the coupled check runs on this sweep (latched in AsymSweepPrep): with the nozzles as effectors (1.2.0),
		// or without them (CplNoTvc) with the held-load pitch figure
		private bool cplTvMode, cplNoTv, cplPitchGate;

		private readonly float[] cplDn = new float[22];

		private readonly float[] cplUp = new float[22];

		public float cplDnNow;

		// Revoker 1.3.0 (from the Compass 0.4.0, no-TVC path only): the nose-down margin of the asymmetric pitch cap scales with dynamic
		// pressure below AsymPitchMarginIasRef, never under AsymPitchMarginMin, judged at the predicted dynamic pressure
		internal static float AsymPitchMarginIasRef = 130f;

		internal static float AsymPitchMarginMin = 6f;

		internal static bool AsymPitchAtPredQ = true;

		public float asymPitchMargDbg;

		// Revoker 1.3.0 (CplGridCache, from the Ternion 0.3.0): the coupled check's surface points on geometry worked out once per sweep
		// tick (SurfaceEffector.CplGeom) and, with the native kernels, their moments in one call per surface and sweep point. Off =
		// 1.2.0's per-point evaluation.
		internal static bool CplGridCache = true;

		private bool[] swCpl;

		private readonly List<SurfaceEffector> swCplList = new List<SurfaceEffector>();

		private bool cplLoaded;

		private readonly CplPoint[] cplPts = new CplPoint[22];

		private readonly float[] cplU = new float[22];

		private readonly float[] cplRp = new float[22];

		private readonly float[] cplRn = new float[22];

		public float cplUNow, cplRpNow, cplRnNow;

		internal static bool AuthCapFixed = true;

		private bool authFixedOn;

		internal static bool AsymThrustSplit = true;

		// 2.0.12: with an engine out the asymmetric-load cap comes down this many times faster (AsymCapTightenRate)
		internal static float EngOutTightenK = 4f;

		private bool engFixedPrev;

		// after leaving the fixed-cap state the thrust term stays out of the sweep for this long (the first sweep after the
		// match engages still sees the split settling: 23 Oct 00:00 log, cap 12 for one cycle)
		private float tKHoldT;

		// IAS at the last asymmetric-load sweep: the stored yaw authority scales with q from there
		private float asymSweepIas = 1f;

		// ---- 2.0.12 fuel trim: lateral CG moved with fuel when yaw or roll runs short -----------------------------------
		// Moving the CG sideways by d (m) changes the jet's moments by -d x (thrust + lift) about the new CG: toward the
		// live engine it shortens that engine's lever arm (less engine-out yaw: at full afterburner ~3 deg/s^2 per 10 cm,
		// of ~18), and with damage it can also offset an aerodynamic yaw or roll asymmetry. It costs roll: lift through a
		// CG 10 cm off the centreline rolls the jet ~15 deg/s^2 per g. So nothing moves until the yaw or roll usage at the
		// present AoA (load / authority, the asymmetric-load model's numbers) passes FuelTrimStart; then the offset is
		// the one that best evens out the roll and yaw usage (minimax), with the roll side judged at 1.5 g or more so a
		// pull does not run the roll out, at most FuelTrimMaxOffset either side. When both usages are back under
		// FuelTrimStop for 2 s the normal balancing brings the CG back to the centreline.
		internal static float FuelTrimStart = 0.75f;

		internal static float FuelTrimStop = 0.5f;

		internal static float FuelTrimMaxOffset = 0.3f;

		public bool fuelTrimOn;

		public float fuelTrimLat;

		public float fuelTrimDelta;

		private float fuelTrimTimer;

		private float fuelTrimCalm;

		// --- shared state added by the unification (from the Ternion 0.2.2, Ifrit 0.8.1 / 1.2.0, Ternion 0.1.9 / 0.3.1) ---
		// Ternion 0.2.2 (SweepSlatSchedule): the slats' schedule on the coupled check's load (per-jet hooks SwSlatSchedOn / SwHlAreas / SwSlatPos)
		private bool swSlatSched;
		private bool[] swHl = new bool[0];
		private float[] swHlQe = new float[0];
		private float[] swHlRet = new float[0];
		private float[] swHlDep = new float[0];

		// Ifrit 0.8.1: the sliding engine-out limit, moved by the jet's VMC code (999 = none)
		public float eoCapDyn = 999f;

		// Ternion 0.1.9 (EngineOutZeroMatch): no AoA cap for an engine OUT where the jet matches the live engine to zero thrust
		// (Plugin.EngineOutZeroThrust, per jet)
		internal static bool EngineOutZeroMatch = true;

		// Ifrit 1.2.0 (AsymPitchNoGainSkip): no pitch cap when the held-pitch margin is short already at the lowest AoA, without nozzles
		internal static bool AsymPitchNoGainSkip = true;

		// Ternion 0.3.1: each point's design-sideslip roll (CplBetaDesign), telemetry
		private readonly float[] cplVbP = new float[22];
		private readonly float[] cplVbN = new float[22];
		public float cplVbPNow, cplVbNNow;

		private void AsymStep(float dt, float rho, Quaternion rootRot, ref EvalContext ctx, Vector3 wdF, Vector3 mF)
		{
			DecelStep(dt);
			DamageGateStep(dt);
			if (havePrev)
			{
				Vector3 m = MulI(wdF) + Vector3.Cross(omega, MulI(omega)) - mF;
				// 2.0.12: on the wheels the runway takes whatever roll moment the surfaces make, so the 'unexplained' roll
				// is the gear's reaction, not an asymmetry: the estimates bleed to zero there (10:37 log: 0 -> 25 deg/s^2
				// on the take-off roll, carried into the climb)
				float rollIn = wheelsTouch ? 0f : AccRoll(m);
				dMeasR = dMeasRF.Step(rollIn, 0.3f, dt);
				if (Mathf.Abs(omega.x * 57.29578f) < 20f)
				{
					dTrimR = dTrimRF.Step(wheelsTouch ? 0f : (AccRoll(m) - Lp * omega.x * IaInv[0, 0] * 57.29578f), 0.5f, dt);
				}
				dMeasY = dMeasYF.Step(AccYaw(m), 0.3f, dt);
				bool negSide = alpha < 0f;
				if (AsymPitchBias && asymValid && !wheelsTouch && tas > 30f && Mathf.Abs(alpha) < 30f && alpha != 0f && (alpha > 0f || AsymPitchBiasNeg))
				{
					float stabNow = 0f;
					float stabLo = 0f;
					float stabHi = 0f;
					int nStab = 0;
					int nStabAll = 0;
					foreach (SurfaceEffector sB in surfaces)
					{
						if (sB.kind == SurfaceKind.Stabilator)
						{
							nStabAll++;
						}
						if (sB.active && sB.kind == SurfaceKind.Stabilator)
						{
							nStab++;
							stabNow += sB.Mnow.y;
							if (sB.M != null && sB.N > 1)
							{
								float lo = float.MaxValue;
								float hi = float.MinValue;
								for (int iB = 0; iB < sB.N; iB++)
								{
									lo = Mathf.Min(lo, sB.M[iB].y);
									hi = Mathf.Max(hi, sB.M[iB].y);
								}
								stabLo += lo;
								stabHi += hi;
							}
						}
					}
					float bRaw = (m.y + mF.y - stabNow) * IaInv[1, 1] * 57.29578f - AsymCap.Interp(asymAlphas, asymAirP, asymAlphas.Length, alpha);
					if (!negSide)
					{
						if (AsymPitchBiasStab && nStab < nStabAll)
						{
							// the stabs' live nose-down end against the swept one at this AoA
							bRaw += stabLo * IaInv[1, 1] * 57.29578f - AsymCap.Interp(asymAlphas, asymStabLo, asymAlphas.Length, alpha);
						}
						float bF = asymPitchBiasF.Step(Mathf.Max(0f, bRaw), AsymPitchBiasTau, dt);
						asymPitchBias = Mathf.Max(bF, asymPitchBias - AsymPitchBiasDecay * dt);
						asymPitchBiasNeg = Mathf.Max(0f, asymPitchBiasNeg - AsymPitchBiasDecay * dt);
					}
					else
					{
						// Compass 0.1.8: the mirror at negative AoA - nose-down the model leaves out, and the stabs' live nose-up end
						float bN = 0f - bRaw;
						if (AsymPitchBiasStab && nStab < nStabAll)
						{
							bN += AsymCap.Interp(asymAlphas, asymStabHi, asymAlphas.Length, alpha) - stabHi * IaInv[1, 1] * 57.29578f;
						}
						float bFN = asymPitchBiasNegF.Step(Mathf.Max(0f, bN), AsymPitchBiasTau, dt);
						asymPitchBiasNeg = Mathf.Max(bFN, asymPitchBiasNeg - AsymPitchBiasDecay * dt);
						asymPitchBias = Mathf.Max(0f, asymPitchBias - AsymPitchBiasDecay * dt);
					}
				}
				else
				{
					asymPitchBias = Mathf.Max(0f, asymPitchBias - AsymPitchBiasDecay * dt);
					asymPitchBiasNeg = Mathf.Max(0f, asymPitchBiasNeg - AsymPitchBiasDecay * dt);
				}
			}
			if (!Plugin.AsymLimiter)
			{
				asymCapPos = (asymCapPosEff = 999f);
				asymCapNeg = (asymCapNegEff = -999f);
				asymCapFilt = 999f;
				asymCapFiltNeg = -999f;
				asymValid = false;
				asymFF = Vector3.zero;
				return;
			}
			cplClock += dt;
			AsymPrepSym(rootRot);
			FlapSymStep(dt);
			// 0.4.1: entering the engine-out / damaged-engine state, the cap from the last sweep was judged with the thrust
			// term in (and below AsymMinIAS there is no next sweep to replace it): it is replaced by the fixed cap at once
			// and a fresh sweep (thrust term out) is started, so a damage at high AoA and low speed gets the fixed cap and
			// not a stale 12 deg.
			bool fixedNow = (engOut || engAsym) && EngineOutFixedCap;
			// Ternion 0.1.9 (EngineOutZeroMatch): with an engine OUT there is no AoA cap for it at all - above EngineOutAoACap the live
			// engine is matched to the dead one (zero thrust), so its yaw is gone instead of the AoA (Tom, 9 Nov: "match thrust, in this
			// case zero"; the jet reaches its full AoA on the airframe alone). The weak-engine case keeps EngineDamagedAoACap. Per jet:
			// Plugin.EngineOutZeroThrust is false where the jet has no zero-thrust match.
			bool zeroMatchOut = ZeroThrustPlanned();
			if (fixedNow && !engFixedPrev && asymValid && zeroMatchOut)
			{
				// thrust term out from now on: a fresh sweep, the aero-only cap
				asymK = -1;
				asymTimer = 1f;
			}
			else if (fixedNow && !engFixedPrev && asymValid)
			{
				float capF = engOut ? Plugin.EngineOutAoACap : Plugin.EngineDamagedAoACap;
				asymCapPos = capF;
				asymCapNeg = 0f - capF;
				PushCapSample(asymCapPos, asymCapNeg, reset: true);
				asymK = -1;
				asymTimer = 1f;
			}
			// leaving it because the thrust match took the split away: the limit is lifted at once to the aero-only cap
			// (the sweep's arrays were made without the thrust term) instead of relaxing up over 1.5 s - the "hitch" at 29 deg
			// on the way to the full AoA (23 Oct 00:00 / 00:06 logs: capEff 30 -> 55 in 1.5 s after each engagement)
			if (!fixedNow && engFixedPrev && asymValid && matchFull)
			{
				asymCapPos = CplKneeOn ? CplKnee(1) : CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax, ref cplLowPos) : AsymCap.Find(asymAlphas, (cplActive && CplLateral) ? cplU : asymU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax);
				asymCapNeg = CplKneeOn ? CplKnee(-1) : CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax, ref cplLowNeg) : AsymCap.Find(asymAlphas, (cplActive && CplLateral) ? cplU : asymU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax);
				PushCapSample(asymCapPos, asymCapNeg, reset: true);
				asymCapPosEff = asymCapPos;
				asymCapNegEff = asymCapNeg;
				tKHoldT = 1f;
			}
			if (!fixedNow)
			{
				tKHoldT = Mathf.Max(0f, tKHoldT - dt);
			}
			engFixedPrev = fixedNow;
			long tPh0 = System.Diagnostics.Stopwatch.GetTimestamp();
			asymMNow = AsymMomentAt(alpha, rho, rootRot, ref ctx);
			long tPh1 = System.Diagnostics.Stopwatch.GetTimestamp();
			phAsymMom += tPh1 - tPh0;
			SlipProbeStep_(dt, rho, rootRot, ref ctx);
			phSlip += System.Diagnostics.Stopwatch.GetTimestamp() - tPh1;
			Vector3 vector = asymMF.Step(asymMNow, Mathf.Max(0.05f, Plugin.AsymFFWashout), dt);
			float num = Mathf.Max(0.01f, Plugin.AsymDeadband);
			float num2 = Mathf.Clamp01(Mathf.Max(Mathf.Abs(AccRoll(asymMNow)) / num, Mathf.Abs(AccYaw(asymMNow)) / (0.25f * num)) - 1f);
			float num3 = 1f - Mathf.Clamp01((Mathf.Abs(beta) - Plugin.AsymFFMaxBeta) / 12f);
			float num4 = 1f - Mathf.Clamp01((Mathf.Abs(alpha) - Plugin.AsymFFMaxAoA) / 15f);
			asymFFGate = num2 * num3 * num4;
			asymFF = (0f - Mathf.Clamp01(Plugin.AsymFeedForward)) * asymFFGate * new Vector3(asymMNow.x - vector.x, 0f, asymMNow.z - vector.z);
			asymFFr = AccRoll(asymFF);
			asymFFy = AccYaw(asymFF);
			asymTimer += dt;
			if (asymK < 0 && asymTimer > 0.2f && ias > Plugin.AsymMinIas)
			{
				asymTimer = 0f;
				long tPh2 = System.Diagnostics.Stopwatch.GetTimestamp();
				AsymSweepPrep(rootRot);
				phSweep += System.Diagnostics.Stopwatch.GetTimestamp() - tPh2;
				asymK = 0;
			}
			if (asymK >= 0)
			{
				int num5 = Mathf.Min(asymAlphas.Length, asymK + ((cplActive && cplLoaded) ? Mathf.Min(AsymPointsPerStep, CplPointsPerStep) : AsymPointsPerStep));
				long tPh3 = System.Diagnostics.Stopwatch.GetTimestamp();
				AsymSweepPoints(asymK, num5, rho, rootRot, ref ctx);
				phSweep += System.Diagnostics.Stopwatch.GetTimestamp() - tPh3;
				asymK = ((num5 < asymAlphas.Length) ? num5 : (-1));
			}
			if (asymK < 0 && asymCycleDone)
			{
				asymCycleDone = false;
				if (cplActive)
				{
					CplDemand();
					CplFill();
					if (CplDbgT >= 0f && cplClock >= CplDbgT && cplLoaded)
					{
						// bench diagnostic: the whole curve of one sweep
						CplDbgT = -1f;
						System.Text.StringBuilder sb = new System.Text.StringBuilder();
						sb.AppendFormat(CultureInfo.InvariantCulture, "CPLDBG t {0:F2} ias {1:F1} alpha {2:F1} dy {3:F3} kq {4:F2}\n", cplClock, ias, alpha, asymDy, qPredK);
						for (int iD = 0; iD < asymAlphas.Length; iD++)
						{
							sb.AppendFormat(CultureInfo.InvariantCulture, "  a {0,4:F0}  cplU {1,6:F2}  rp {2,7:F1}  rn {3,7:F1}  heldDn {4,7:F1} stabDn {5,7:F1} | one-axis U {6,6:F2}  Ar {7,7:F1} Ay {8,7:F1} Cr {9,6:F1} Cy {10,6:F1}\n", asymAlphas[iD], cplU[iD], cplRp[iD], cplRn[iD], cplDn[iD], asymNetDn[iD], asymU[iD], asymAr[iD], asymAy[iD], asymCr[iD], asymCy[iD]);
						}
						Console.Write(sb.ToString());
					}
				}
				asymCapPos = CplKneeOn ? CplKnee(1) : CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax, ref cplLowPos) : AsymCap.Find(asymAlphas, (cplActive && CplLateral) ? cplU : asymU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax);
				asymCapNeg = CplKneeOn ? CplKnee(-1) : CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax, ref cplLowNeg) : AsymCap.Find(asymAlphas, (cplActive && CplLateral) ? cplU : asymU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax);
				if (engOut && EngineOutFixedCap && !zeroMatchOut)
				{
					asymCapPos = Mathf.Min(asymCapPos, Plugin.EngineOutAoACap);
					asymCapNeg = Mathf.Max(asymCapNeg, 0f - Plugin.EngineOutAoACap);
				}
				else if (engAsym && EngineOutFixedCap)
				{
					// Ifrit 0.4.1: a weak (damaged, running) engine gets its own fixed cap, not the dynamic thrust cap
					asymCapPos = Mathf.Min(asymCapPos, Plugin.EngineDamagedAoACap);
					asymCapNeg = Mathf.Max(asymCapNeg, 0f - Plugin.EngineDamagedAoACap);
				}
				asymCapPitch = 999f;
				float capGate = Mathf.Max(Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / CoupHeldDySpan), (AsymDamageGate && dmgGateHold > 0f) ? 1f : 0f);
				if (Plugin.AsymPitchMargin > 0f && capGate > 0.5f && (asymCapPos < Plugin.AoAPos - 0.5f || (cplActive && cplLoaded)) && Ia[1, 1] > 1f)
				{
					float tvDn = 0f;
					if (tvc.active && tvc.M != null)
					{
						float tvLo = float.MaxValue;
						for (int tk = 0; tk < tvc.N; tk++)
						{
							tvLo = Mathf.Min(tvLo, tvc.M[tk].y);
						}
						if (tvLo < float.MaxValue)
						{
							tvDn = tvLo * IaInv[1, 1] * 57.29578f;
						}
					}
					float margP = AsymPitchMarginEff(out float kqP);
					asymPitchMargDbg = margP;
					float prevA = 0f;
					float prevM = float.NaN;
					for (int ia = 0; ia < asymAlphas.Length; ia++)
					{
						float aA = asymAlphas[ia];
						if (aA < 0f)
						{
							continue;
						}
						// the nozzles' nose-down authority where the LP did not have them as effectors: from their neutral when they are
						// free (the neutral thrust moment is in the LP's load), else their whole travel at the present thrust
						float tvAdd = (cplActive && cplDnTv[ia]) ? 0f : ((cplActive && cplTvFree) ? cplTvFreeDn : tvDn);
						float mDn = kqP * (0f - ((cplActive ? cplDn[ia] : asymNetDn[ia]) + tvAdd)) - ((AsymPitchBias && (tvc == null || tvc.max <= 0.5f || Plugin.DisableTVC)) ? asymPitchBias : 0f);
						if (mDn < margP)
						{
							// Ifrit 1.2.0 (AsymPitchNoGainSkip): without nozzle authority, when the held-pitch margin is short already at the
							// lowest AoA (very low speed, idle: bench 450 km/h idle pull at 47 m/s), cutting AoA buys no nose-down authority;
							// the floor cap only saturated the stabilators (10 deg over, sideslip to 6 deg) and flipped 10<->21 deg. No pitch
							// cap then, as when the roll demand is over everywhere (FindTop).
							if (AsymPitchNoGainSkip && cplNoTv && float.IsNaN(prevM))
							{
								break;
							}
							float aP = (float.IsNaN(prevM) || prevM <= mDn) ? aA : Mathf.Lerp(prevA, aA, (prevM - margP) / (prevM - mDn));
							asymCapPitch = Mathf.Max((AsymPitchBias && (tvc == null || tvc.max <= 0.5f || Plugin.DisableTVC)) ? AsymPitchCapFloorNoTvc : AsymPitchCapFloor, aP - AsymPitchCapLead);
							break;
						}
						prevA = aA;
						prevM = mDn;
					}
					asymCapPos = Mathf.Min(asymCapPos, asymCapPitch);
				}
				asymCapPitchNeg = -999f;
				if (AsymPitchBiasNeg && Plugin.AsymPitchMargin > 0f && capGate > 0.5f && asymCapNeg > Plugin.AoANeg + 0.5f && Ia[1, 1] > 1f && (tvc == null || tvc.max <= 0.5f || Plugin.DisableTVC))
				{
					float margN = AsymPitchMarginEff(out float kqN);
					float prevAN = 0f;
					float prevMN = float.NaN;
					for (int ia = asymAlphas.Length - 1; ia >= 0; ia--)
					{
						float aA = asymAlphas[ia];
						if (aA > 0f)
						{
							continue;
						}
						float mUp = kqN * (cplActive ? cplUp[ia] : asymNetUp[ia]) - asymPitchBiasNeg;
						if (mUp < margN)
						{
							float aP = (float.IsNaN(prevMN) || prevMN <= mUp) ? aA : Mathf.Lerp(prevAN, aA, (prevMN - margN) / (prevMN - mUp));
							asymCapPitchNeg = Mathf.Min(0f - AsymPitchCapFloorNegNoTvc, aP + AsymPitchCapLead);
							break;
						}
						prevAN = aA;
						prevMN = mUp;
					}
					asymCapNeg = Mathf.Max(asymCapNeg, asymCapPitchNeg);
				}
				AuthCapFromSweep();
				PushCapSample(asymCapPos, asymCapNeg, !asymValid);
				if (!asymValid)
				{
					asymCapPosEff = asymCapPos;
					asymCapNegEff = asymCapNeg;
					asymValid = true;
				}
				if (!asymFirstLogged)
				{
					asymFirstLogged = true;
					Plugin.Log.LogInfo($"{AsymJetName} FCS: asymmetric-load check at {ias:F0} m/s IAS - lateral CG offset {asymDy * 100f:F1} cm, lateral usage at 30/45/60 deg AoA " + $"{AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, 30f):F2}/{AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, 45f):F2}/{AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, 60f):F2} " + $"(cap engages above 1.00); roll/yaw authority there {AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 30f):F0}/{AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 30f):F0}, " + $"{AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 45f):F0}/{AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 45f):F0}, " + $"{AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 60f):F0}/{AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 60f):F0} deg/s^2; " + $"load accel at 45 deg {AsymCap.Interp(asymAlphas, asymAr, asymAlphas.Length, 45f):F1}/{AsymCap.Interp(asymAlphas, asymAy, asymAlphas.Length, 45f):F1} deg/s^2 (deadband {Plugin.AsymDeadband:F1}); AoA cap {asymCapPos:F1}/{asymCapNeg:F1} deg.");
				}
			}
			if (asymValid)
			{
				CapFilterStep(dt);
				float upRate = Plugin.AsymRelax * (1f + Mathf.Max(0f, Plugin.AsymRelaxBoost) * (1f - Mathf.Clamp01(AsymCap.Interp(asymAlphas, (cplActive && CplLateral) ? cplU : asymU, asymAlphas.Length, asymCapPosEff))));
				float tightR = Plugin.AsymTighten * ((engOut || engAsym) ? Mathf.Max(1f, EngOutTightenK) : 1f);
				// Compass 0.1.9 (AsymCapGapTighten): a cap that has to come down a long way (a part shot off) comes down faster, up to AsymCapGapMax x
				// the tighten rate, in proportion to the gap over AsymCapGapRef (3) deg. At 10 deg/s a 65 -> 12 deg drop took 5 s; 5 Nov 21:09
				// F-22E log, right stab shot off at 260.6 s with the fins gone: full aft 2 s later reached 25 deg AoA under a cap still at 45,
				// and the lone stab's load rolled the jet to 78 deg of bank against full opposite roll.
				float gapP = asymCapPosEff - asymCapFilt;
				float gapN = asymCapFiltNeg - asymCapNegEff;
				float kGP = AsymCapGapTighten ? Mathf.Clamp(gapP / Mathf.Max(1f, AsymCapGapRef), 1f, AsymCapGapMax) : 1f;
				float kGN = AsymCapGapTighten ? Mathf.Clamp(gapN / Mathf.Max(1f, AsymCapGapRef), 1f, AsymCapGapMax) : 1f;
				asymCapPosEff = AsymCap.Slew(asymCapPosEff, asymCapFilt, 1, tightR * kGP, upRate, dt);
				asymCapNegEff = AsymCap.Slew(asymCapNegEff, asymCapFiltNeg, -1, tightR * kGN, upRate, dt);
				if (engOut && EngineOutFixedCap && !zeroMatchOut && eoCapDyn < 900f)
				{
					// Ifrit 0.8.1: the sliding engine-out limit (EngineOutAoACap -> EngineOutAoACapMin, rate-limited in the jet's VMC code),
					// both signs; eoCapDyn stays 999 (off) on a jet whose VMC code does not move it
					asymCapPosEff = Mathf.Min(asymCapPosEff, eoCapDyn);
					asymCapNegEff = Mathf.Max(asymCapNegEff, 0f - eoCapDyn);
				}
				asymUNow = AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, alpha);
				asymArNow = AsymCap.Interp(asymAlphas, asymAr, asymAlphas.Length, alpha);
				asymAyNow = AsymCap.Interp(asymAlphas, asymAy, asymAlphas.Length, alpha);
				asymCrNow = AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, alpha);
				asymCyNow = AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, alpha);
				if (cplActive)
				{
					cplUNow = AsymCap.Interp(asymAlphas, cplU, asymAlphas.Length, alpha);
					cplRpNow = AsymCap.Interp(asymAlphas, cplRp, asymAlphas.Length, alpha);
					cplVbPNow = AsymCap.Interp(asymAlphas, cplVbP, asymAlphas.Length, alpha);
					cplVbNNow = AsymCap.Interp(asymAlphas, cplVbN, asymAlphas.Length, alpha);
					cplRnNow = AsymCap.Interp(asymAlphas, cplRn, asymAlphas.Length, alpha);
					cplDnNow = AsymCap.Interp(asymAlphas, cplDn, asymAlphas.Length, alpha);
				}
				FuelTrimStep(dt, rootRot);
				asymLogTimer += dt;
				bool flag = asymCapPos < Plugin.AoAPos - 0.1f || asymCapNeg > Plugin.AoANeg + 0.1f;
				bool flag2 = asymLoggedCap < Plugin.AoAPos - 0.1f || asymLoggedCapN > Plugin.AoANeg + 0.1f;
				if (asymLogTimer > 2f && (flag != flag2 || (flag && (Mathf.Abs(asymCapPos - asymLoggedCap) > 3f || Mathf.Abs(asymCapNeg - asymLoggedCapN) > 3f))))
				{
					asymLogTimer = 0f;
					asymLoggedCap = asymCapPos;
					asymLoggedCapN = asymCapNeg;
					Plugin.Log.LogInfo(flag ? $"{AsymJetName} FCS: asymmetric load - lateral CG offset {asymDy * 100f:F1} cm, AoA cap {asymCapPos:F1}/{asymCapNeg:F1} deg at {ias:F0} m/s IAS ({((cplActive && cplLoaded) ? "coupled" : "one-axis")} lateral demand at 30 deg: {AsymCap.Interp(asymAlphas, (cplActive && CplLateral) ? cplU : asymU, asymAlphas.Length, 30f):F2}, at 60: {AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, 60f):F2}; load accel at 45 deg {AsymCap.Interp(asymAlphas, asymAr, asymAlphas.Length, 45f):F1} roll / {AsymCap.Interp(asymAlphas, asymAy, asymAlphas.Length, 45f):F1} yaw, authority there {AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 45f):F0} / {AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 45f):F0} deg/s^2)" : $"{AsymJetName} FCS: asymmetric-load AoA cap released (lateral CG offset {asymDy * 100f:F1} cm)");
				}
			}
		}

		private void AsymSweepPrep(Quaternion rootRot)
		{
			asymSweepIas = Mathf.Max(ias, 1f);
			AsymPrepSym(rootRot);
			// Su-30 0.5.0: the coupled check's state, latched for the whole sweep. The LP runs only with a load to judge (lateral CG
			// offset past CoupHeldDy0 or a damage gate); without one the cap reads the old sweep, as before. It runs in both TVC
			// states: with the nozzles vectoring they are two of its effectors (CplTvc), each on its own canted axis, read here on the
			// main thread; locked (DisableTVC) or not vectoring, the thrust stays in the load as before.
			cplActive = CoupledCap && Plugin.AsymCoupledCheck;   // (per jet: the F-22E ships its check off, 4.2.0)
			cplLoaded = Mathf.Abs(asymDy) > CoupHeldDy0 || (AsymDamageGate && dmgGateHold > 0f);
			cplPitchGate = Plugin.AsymPitchMargin > 0f && (Mathf.Abs(asymDy) - CoupHeldDy0 > 0.5f * CoupHeldDySpan || (AsymDamageGate && dmgGateHold > 0f));
			// Su-30 0.5.2: the clean jet's limit on the sweep's grid (the reference point of CplDemand), each side
			{
				int nRef = asymAlphas.Length;
				cplRefAp = Mathf.Clamp(Mathf.Min(Plugin.AoAPos, authCapEff), 5f, asymAlphas[nRef - 1]);
				cplRefAn = Mathf.Clamp(Mathf.Max(Plugin.AoANeg, authCapNegEff), asymAlphas[0], -5f);
				int i0p = -1, i0n = -1;
				for (int kR = 0; kR < nRef - 1; kR++)
				{
					if (asymAlphas[kR] <= cplRefAp && cplRefAp <= asymAlphas[kR + 1])
					{
						i0p = kR;
					}
					if (i0n < 0 && asymAlphas[kR] <= cplRefAn && cplRefAn <= asymAlphas[kR + 1])
					{
						i0n = kR;
					}
				}
				cplRefI0p = i0p;
				cplRefI1p = (i0p < 0) ? -1 : (i0p + 1);
				cplRefI0n = i0n;
				cplRefI1n = (i0n < 0) ? -1 : (i0n + 1);
			}
			cplTvOn = false;
			// (Revoker 1.3.0 / Ifrit 1.2.0) a pitch-grid table not while the law itself has the nozzles out (noTvcOn: low thrust), as the
			// allocator flies it; per-engine tables that carry all their nozzle's axes report each nozzle's own vectoring state (MnOn)
			if (cplActive && cplLoaded && CplTvc && tvc != null && tvc.active && !Plugin.DisableTVC && (!noTvcOn || tvc.MnAllAxes) && tvc.SharedTablesOn() && tvc.Mn != null && tvc.N > 1)
			{
				int nE = Mathf.Min(tvc.Mn.Length, cplTvM.Length);
				cplTvE = nE;
				cplTvN = tvc.N;
				if (cplTvG == null || cplTvG.Length != tvc.N)
				{
					cplTvG = new float[tvc.N];
				}
				Array.Copy(tvc.grid, cplTvG, tvc.N);
				for (int e = 0; e < nE; e++)
				{
					if (cplTvM[e] == null || cplTvM[e].Length != tvc.N)
					{
						cplTvM[e] = new Vector3[tvc.N];
					}
					Array.Copy(tvc.Mn[e], cplTvM[e], tvc.N);
					cplTvHas[e] = tvc.MnOn[e];
					cplTvNow[e] = tvc.MnNow(e);
					cplTvOn |= tvc.MnOn[e];
				}
			}
			asymMT = ((tvc.active || Plugin.DisableTVC) ? (((tvcd != null && tvcd.active) || (tvcy != null && tvcy.active)) ? tvc.MAt(tvc.now) : tvc.Mnow) : FixedThrustLat());
			// (shared: the LP with or without the nozzles as effectors, for the reserve share and the telemetry)
			cplTvMode = cplActive && cplTvOn;
			cplNoTv = cplActive && !cplTvOn;
			// Revoker 1.3.0 (CplNoTvNeutral, from the Ifrit 1.2.0 bench): with the nozzles free but not effectors of the LP (low thrust,
			// vectoring not locked) the load takes the thrust at the nozzles' neutral, not at their present angle (this controller's
			// own output). On the Ifrit (300 km/h idle pull, 30 cm load) the present angle made the held-pitch figure swing with the
			// nozzles and the cap flip 10 <-> 34 deg every 1.5 s.
			cplTvFree = cplNoTv && cplLoaded && CplNoTvNeutral && tvc != null && tvc.active && !Plugin.DisableTVC && tvc.M != null && tvc.N >= 2 && !tvc.fixedNozzles;
			cplTvFreeDn = 0f;
			if (cplTvFree)
			{
				// (Ifrit 1.2.0 / F-22E 4.2.0) and the nozzles' nose-down range from that neutral for the pitch cap: the LP's held-pitch
				// figure already carries the neutral thrust moment (it is in the load), so only the range below it is added there
				Vector3 m0 = tvc.MAt(Mathf.Clamp(0f, tvc.min, tvc.max));
				float tvLo0 = float.MaxValue;
				for (int tk = 0; tk < tvc.N; tk++)
				{
					tvLo0 = Mathf.Min(tvLo0, tvc.M[tk].y);
				}
				asymMT = m0;
				cplTvFreeDn = Mathf.Min(0f, tvLo0 - m0.y) * IaInv[1, 1] * 57.29578f;
			}
		}

		// Su-30 0.2.0 (from F-22E 3.1.2, ParallelSweep): the asymmetric-load sweep. The parts' Unity state (lift-normal rotation, centre of
		// mass and lift, areas, foil) is read once per sweep tick on the main thread (SwGather) - the same values every point of the tick
		// read before, since nothing moves the parts inside a tick - and the tick's AoA points are computed in parallel (IndexFork), each
		// writing only its own index of the sweep arrays, joined before anything reads them. Same expressions in the same order per point.
		// The hinged flaps' EvalFlow reads their host's rigidbody, so with any hinged flap active the points run on this thread.
		private void AsymSweepPoints(int k0, int k1, float rho, Quaternion rootRot, ref EvalContext ctx)
		{
			float[] array = asymDsym;
			if (array == null)
			{
				return;
			}
			float num = Mathf.Max(tas, 1f);
			Vector3 vector = asymMT;
			SwGather(rho);
			// perf port (Revoker line): the hinged surfaces' Unity state read once here, on the main thread, so the points can run in
			// parallel with them active
			HingeSnaps();
			int n = k1 - k0;
			SwGridGeom(k0, k1, ref ctx);
			swNative = FcsNative.SweepOn;   // F-22E 4.1.0 port: one answer for all of this tick's points
			if (Plugin.ParallelSweep && n > 1)
			{
				if (swWork == null)
				{
					swWork = new SweepWork(this);
				}
				swWork.Set(k0, rho, rootRot, ctx, array, num, vector);
				IndexFork.Finish(IndexFork.Start(swWork, n));
			}
			else
			{
				for (int i = k0; i < k1; i++)
				{
					AsymSweepPoint(i, rho, rootRot, ref ctx, array, num, vector);
				}
			}
			if (k1 >= asymAlphas.Length)
			{
				asymCycleDone = true;
			}
		}

		// one AoA point of the sweep: pure maths on the snapshot, the surfaces' gathered state (EvalFlow) and the controller's settings;
		// writes index i of the sweep arrays only
		private void AsymSweepPoint(int i, float rho, Quaternion rootRot, ref EvalContext ctx, float[] array, float num, Vector3 vector)
		{
			float f = asymAlphas[i] * (MathF.PI / 180f);
			Vector3 vector2 = rootRot * new Vector3(0f, (0f - Mathf.Sin(f)) * num, Mathf.Cos(f) * num);
			Vector3 vector3 = vector2 / num;
			float num2 = 0.5f * rho * num * num;
			Vector3 zero = Vector3.zero;
			// Ternion 0.2.2: for the coupled cap (a look-ahead over AoA) the slats are where the in-flight schedule puts them at this
			// point's AoA; everything else this sweep feeds reads it at the present AoA (the coupling roll limit, the capture braking,
			// the authority cap), where the slats are where they are now, so it keeps the live area. Bench, 350 km/h pull-then-
			// pushover + roll: the scheduled area there (0.42 at 9.6 deg, the slats still at 0.7 coming down) raised the coupling
			// roll limit and the push overshot to -14 deg with 5 deg of sideslip (live: -10, 2.7). No scheduled slats: zeroSch = zero.
			float slatA = swSlatSched ? SwSlatPos(asymAlphas[i]) : 0f;
			Vector3 zeroSch = Vector3.zero;
			// the parts' state from SwGather (read once per sweep tick on the main thread), same expressions, same order
			for (int k = 0; k < swN; k++)
			{
				Vector3 rhs = -vector3 * (0.5f * num2 * swDrag[k]);
				Vector3 rhsS = rhs;
				if (swFoil[k] != null)
				{
					rhs += PartForce(vector2, swRot[k], swRot[k] * Vector3.right, swFoil[k], swQ[k]);
					rhsS = (swSlatSched && swHl[k]) ? (rhsS + PartForce(vector2, swRot[k], swRot[k] * Vector3.right, swFoil[k], swHlQe[k] * Mathf.Lerp(swHlRet[k], swHlDep[k], slatA))) : rhs;
				}
				else
				{
					rhsS = rhs;
				}
				zero += Vector3.Cross(swLhs[k], rhs);
				zeroSch += Vector3.Cross(swLhs[k], rhsS);
			}
			Vector3 vector4 = Quaternion.Inverse(rootRot) * zero;
			Vector3 m = new Vector3(0f - vector4.z, 0f - vector4.x, vector4.y) + vector;
			// Su-30 0.5.0 (CoupledCap, from the Ternion 0.2.2): the airframe's own moment (the load) before any surface, slats on the
			// schedule; the surfaces become the LP's points
			Vector3 vector4S = Quaternion.Inverse(rootRot) * zeroSch;
			Vector3 mAir = new Vector3(0f - vector4S.z, 0f - vector4S.x, vector4S.y) + vector;
			CplPoint cp = null;
			bool cplHere = CplHere(i);
			if (cplActive && cplLoaded && cplHere)
			{
				cp = cplPts[i] ?? (cplPts[i] = new CplPoint());
				cp.Reset();
			}
			// (CplBetaDesign) the airframe's moment change at +/- CplBetaDesign of sideslip, same parts: each side's reserve is solved
			// with the sideslip that rolls against it (CplSolve)
			Vector3 dMbP = Vector3.zero, dMbN = Vector3.zero;
			if (cp != null && CplBetaDesign > 0f)
			{
				float bD = CplBetaDesign * (MathF.PI / 180f);
				for (int sB = 0; sB < 2; sB++)
				{
					float sb = (sB == 0) ? 1f : -1f;
					Vector3 vB = rootRot * new Vector3(sb * Mathf.Sin(bD) * num, (0f - Mathf.Sin(f)) * Mathf.Cos(bD) * num, Mathf.Cos(f) * Mathf.Cos(bD) * num);
					Vector3 vBh = vB / num;
					Vector3 zB = Vector3.zero;
					for (int k = 0; k < swN; k++)
					{
						Vector3 rhsB = -vBh * (0.5f * num2 * swDrag[k]);
						if (swFoil[k] != null)
						{
							float qSk = (swSlatSched && swHl[k]) ? (swHlQe[k] * Mathf.Lerp(swHlRet[k], swHlDep[k], slatA)) : swQ[k];
							rhsB += PartForce(vB, swRot[k], swRot[k] * Vector3.right, swFoil[k], qSk);
						}
						zB += Vector3.Cross(swLhs[k], rhsB);
					}
					Vector3 zBl = Quaternion.Inverse(rootRot) * (zB - zeroSch);
					Vector3 dM = new Vector3(0f - zBl.z, 0f - zBl.x, zBl.y);
					if (sB == 0) dMbP = dM; else dMbN = dM;
				}
			}
			// (CplTvc) the nozzles as effectors of the LP, one per engine: each swivels about its own canted axis, so one nozzle's
			// deflection makes pitch, roll and yaw together, and its table carries that engine's whole thrust moment about the CoM
			// at each angle (the thrust x lateral-offset yaw included). The thrust at the nozzles' present angle ('vector') then
			// leaves the load. Not with an engine out or weak (the thrust is left out of the cap there: EngineOutFixedCap).
			bool thrOut = ((engOut || engAsym) && EngineOutFixedCap) || tKHoldT > 0f;
			bool cplTv = cp != null && cplTvOn && !thrOut;
			if (cplTv)
			{
				for (int eT = 0; eT < cplTvE; eT++)
				{
					if (!cplTvHas[eT])
					{
						continue;
					}
					if (CplDebug) { Vector3 mNw = TvNozzleAt(eT, cplTvNow[eT]); cplDbgNow[cp.nEff] = new Vector3(AccRoll(mNw), mNw.y * IaInv[1, 1] * 57.29578f, AccYaw(mNw)); cplDbgName[cp.nEff] = "nozzle " + eT; cplDbgDefl[cp.nEff] = cplTvNow[eT]; }
					for (int kT = 0; kT < CplStride(cplTvN); kT++)
					{
						int lT = CplIdx(kT, cplTvN);
						Vector3 mT = cplTvM[eT][lT];
						cp.Add(AccRoll(mT), mT.y * IaInv[1, 1] * 57.29578f, AccYaw(mT), cplTvG[lT]);
					}
					cp.EndEff();
				}
				// (Ifrit 1.2.0 / Revoker 1.2.0) the differential nozzles (tvcd) and the yaw nozzle (tvcy) are their own effectors: their
				// grids carry the change from neutral; only the points inside the travel the law allows them now (lo..hi)
				// (not where each engine's table already carries all its nozzle's axes: the Su-30's canted nozzles, MnAllAxes)
				if (!tvc.MnAllAxes)
				{
					TvExtraEff(cp, (tvcd != null && tvcd.active) ? tvcd : null, "TVC diff");
					TvExtraEff(cp, (tvcy != null && tvcy.active) ? tvcy : null, "TVC yaw");
				}
			}
			float pAero = m.y - vector.y;
			float pStabMin = 0f;
			float pStabMax = 0f;
			float pFlapNow = 0f;
			float pRud0 = 0f;
			float pRudP = 0f;
			float pRudM = 0f;
			float toe = 0f;
			float num3 = 0f;
			float num4 = 0f;
			float t = Mathf.InverseLerp(30f, 45f, Mathf.Abs(asymAlphas[i]));
			float num5 = RudderAuthAt(asymAlphas[i]);
			// Revoker 0.2.0: the inboard elevon pair at its symmetric deflection (the roll it makes for the pilot is no load)
			float pHingeAuth = 0f;
			for (int hi = 0; hi < hinges.Count; hi++)
			{
				HingeFlapEffector hA = hinges[hi];   // perf port: on the hinge snapshot (HingeSnaps, main thread)
				if (hA.active)
				{
					Vector3 mH = hA.EvalFlowSnap(hSym[hi], vector2, rho, ref ctx, ref hSnap[hi]);
					m += mH;
					// Revoker 0.2.5: and its nose-down travel for the authority cap (ElevonAuthShare of it)
					float pLoH = mH.y;
					for (int lH = 0; lH < hA.N; lH++)
					{
						pLoH = Mathf.Min(pLoH, hA.EvalFlowSnap(hA.grid[lH], vector2, rho, ref ctx, ref hSnap[hi]).y);
					}
					pHingeAuth += Mathf.Lerp(mH.y, pLoH, Plugin.ElevonPitch ? ElevonAuthShare : 0f);
					if (cp != null)
					{
						if (CplDebug) { Vector3 mNw = hA.EvalFlowSnap(hA.now, vector2, rho, ref ctx, ref hSnap[hi]); cplDbgNow[cp.nEff] = new Vector3(AccRoll(mNw), mNw.y * IaInv[1, 1] * 57.29578f, AccYaw(mNw)); cplDbgName[cp.nEff] = "hinge " + hA.name; cplDbgDefl[cp.nEff] = hA.now; }
						for (int kH = 0; kH < CplStride(hA.N); kH++)
						{
							int lH = CplIdx(kH, hA.N);
							Vector3 mHl = hA.EvalFlowSnap(hA.grid[lH], vector2, rho, ref ctx, ref hSnap[hi]);
							cp.Add(AccRoll(mHl), mHl.y * IaInv[1, 1] * 57.29578f, AccYaw(mHl), hA.grid[lH]);
						}
						cp.EndEff();
					}
				}
			}
			for (int j = 0; j < surfaces.Count; j++)
			{
				SurfaceEffector surfaceEffector = surfaces[j];
				if (!surfaceEffector.active)
				{
					continue;
				}
				// perf port: the flow's own terms once per surface and point (PrepFlow), each deflection's after (EvalFlowAt); = EvalFlow
				surfaceEffector.PrepFlow(vector2, rho, ref ctx, out SurfaceEffector.FlowPrep fpC);
				m += surfaceEffector.EvalFlowAt(array[j], ref fpC, ref ctx);
				// the canards move together only (CanardSymmetricOnly): the pair is one effector of the LP, added at the left one
				bool canPair = cp != null && Plugin.CanardSymmetric && surfaceEffector.isCanard && canL != null && canR != null && canL.active && canR.active && canL.N == canR.N;
				if (cp != null && !(canPair && surfaceEffector == canR))
				{
					SurfaceEffector seO = canPair ? canR : null;
					SurfaceEffector.FlowPrep fpO = default(SurfaceEffector.FlowPrep);
					if (seO != null)
					{
						seO.PrepFlow(vector2, rho, ref ctx, out fpO);
					}
					// (CplAttachedOnly, CplStabPeakRule) a stab's or canard's points as the allocator would fly them: its own rule
					// (ClampStabToPeak) holds them to a local AoA of the stall angle + StabPeakMargin, opening by 8 deg per degree of body
					// AoA past 40. On this jet StabPeakMargin is 90 (the clamp is off), so every point counts (11 Nov bench, 16 cm,
					// 52 deg: the allocator holds the left elevator at 9.7 deg, stalled, where the attached side starts at 26). With a
					// clamp set, only the points inside it; if none is, the least stalled one.
					bool isStab = surfaceEffector.kind == SurfaceKind.Stabilator;
					float attLim = CplStabPeakRule ? (surfaceEffector.stallAoA + Plugin.StabPeakMargin + Mathf.Max(0f, Mathf.Abs(asymAlphas[i]) - 40f) * 8f) : surfaceEffector.stallAoA;
					bool attOnly = CplAttachedOnly && isStab && !(CplStabPeakRule && (!StabPeakClamp || attLim > 89f));
					// (Revoker 1.2.0 / Ternion 0.2.2, CanardAttachedWindow) a canard inside the attached-flow window the allocator holds it to
					// past CanWinA0 (its knee + CanardWindowMargin): only those points, as the allocator flies it - on the allocator's own
					// condition (differential canards only; at positive AoA only without the nozzles while CanWinNegOnly)
					if (CplAttachedOnly && surfaceEffector.isCanard && Plugin.CanardAttachedWindow && !Plugin.CanardSymmetric && (asymAlphas[i] < 0f - CanWinA0 || (asymAlphas[i] > CanWinA0 && (noTvcOn || !CanWinNegOnly))))
					{
						attLim = Mathf.Min(attLim, Mathf.Min(surfaceEffector.stallAoA, surfaceEffector.KneeAoA(Plugin.SettledSlopeFrac) + Plugin.CanardWindowMargin));
						attOnly = true;
					}
					// (Revoker 0.2.7 / 1.2.0, AsymStabTravel; per jet: CplStabTravelLp) and only within the travel the cap judges the stabs /
					// canards on: the 5 deg added past the stock 40 stay a reserve it does not spend. Off: the points the allocator flies
					// (the full travel unless a pitch-pair member is gone)
					float trvLim = (isStab && CplStabTravelLp && AsymStabTravel > 0f) ? AsymStabTravel : float.MaxValue;
					// Su-30 0.5.6 (CplGridCache): this tick's geometry for the points (SwGridGeom) and, native, all their moments at once
					// (both canards of the pair: each on its own geometry, summed as before)
					int jO = (seO != null) ? surfaces.IndexOf(seO) : -1;
					bool cGeo = swCpl != null && swCpl[j] && surfaceEffector.cN == CplStride(surfaceEffector.N)
						&& (seO == null || (jO >= 0 && swCpl[jO] && seO.cN == CplStride(seO.N) && seO.N == surfaceEffector.N));
					Vector3[] cM = (cGeo && swNative && surfaceEffector.CplMomentsNative(ref fpC, ref ctx, cp.scratchM)) ? cp.scratchM : null;
					Vector3[] cMO = (cM != null && seO != null) ? (seO.CplMomentsNative(ref fpO, ref ctx, cp.scratchM2) ? cp.scratchM2 : null) : null;
					if (seO != null && cMO == null)
					{
						cM = null;
					}
					int kLeast = -1;
					float locLeast = float.MaxValue;
					if (attOnly)
					{
						for (int kA = 0; kA < CplStride(surfaceEffector.N); kA++)
						{
							float gA = surfaceEffector.grid[CplIdx(kA, surfaceEffector.N)];
							if (Mathf.Abs(gA) > trvLim + 0.01f)
							{
								continue;
							}
							float la = Mathf.Abs((cGeo ? surfaceEffector.LocalAoACpl(kA, ref fpC) : surfaceEffector.LocalAoAFlow(gA, ref fpC)));
							if (la < locLeast)
							{
								locLeast = la;
								kLeast = kA;
							}
						}
					}
					if (CplDebug) { Vector3 mNw = surfaceEffector.EvalFlowAt(surfaceEffector.now, ref fpC, ref ctx); if (seO != null) mNw += seO.EvalFlowAt(seO.now, ref fpO, ref ctx); cplDbgNow[cp.nEff] = new Vector3(AccRoll(mNw), mNw.y * IaInv[1, 1] * 57.29578f, AccYaw(mNw)); cplDbgName[cp.nEff] = (seO != null) ? "canard pair" : surfaceEffector.name; cplDbgDefl[cp.nEff] = surfaceEffector.now; }
					int nC = 0;
					for (int kC = 0; kC < CplStride(surfaceEffector.N); kC++)
					{
						float gC = surfaceEffector.grid[CplIdx(kC, surfaceEffector.N)];
						if (Mathf.Abs(gC) > trvLim + 0.01f)
						{
							continue;
						}
						if (attOnly && kC != kLeast && Mathf.Abs((cGeo ? surfaceEffector.LocalAoACpl(kC, ref fpC) : surfaceEffector.LocalAoAFlow(gC, ref fpC))) > attLim)
						{
							continue;
						}
						Vector3 mC = (cM != null) ? cM[kC] : (cGeo ? surfaceEffector.EvalFlowCpl(kC, ref fpC, ref ctx) : surfaceEffector.EvalFlowAt(gC, ref fpC, ref ctx));
						if (seO != null)
						{
							mC += (cMO != null) ? cMO[kC] : (cGeo ? seO.EvalFlowCpl(kC, ref fpO, ref ctx) : seO.EvalFlowAt(gC, ref fpO, ref ctx));
						}
						cp.Add(AccRoll(mC), mC.y * IaInv[1, 1] * 57.29578f, AccYaw(mC), gC);
						nC++;
					}
					if (nC > 0)
					{
						cp.EndEff();
					}
				}
				if (surfaceEffector.kind == SurfaceKind.Flaperon || surfaceEffector.kind == SurfaceKind.Aileron)
				{
					float pRef = surfaceEffector.EvalFlowAt((flapSymRef != null && j < flapSymRef.Length) ? flapSymRef[j] : 0f, ref fpC, ref ctx).y;
					// Revoker 0.2.5 (ElevonAuthShare): the delta's elevons are pitch effectors (ElevonPitch), so their nose-down
					// travel counts toward the authority the low-thrust / no-TVC cap judges by, at ElevonAuthShare of it (the
					// rest stays for roll at the cap); the F-22E counted its flaperons only at their present trim
					if (surfaceEffector.kind == SurfaceKind.Aileron && Plugin.ElevonPitch && ElevonAuthShare > 0f)
					{
						float pLo = pRef;
						for (int lE = 0; lE < surfaceEffector.N; lE++)
						{
							pLo = Mathf.Min(pLo, surfaceEffector.EvalFlowAt(surfaceEffector.grid[lE], ref fpC, ref ctx).y);
						}
						pRef = Mathf.Lerp(pRef, pLo, ElevonAuthShare);
					}
					pFlapNow += pRef;
				}
				if (surfaceEffector.kind == SurfaceKind.Rudder && toe > 0f)
				{
					pRud0 += surfaceEffector.EvalFlowAt(0f, ref fpC, ref ctx).y;
					pRudP += surfaceEffector.EvalFlowAt(toe, ref fpC, ref ctx).y;
					pRudM += surfaceEffector.EvalFlowAt(0f - toe, ref fpC, ref ctx).y;
				}
				float num6;
				float num7;
				switch (surfaceEffector.kind)
				{
				case SurfaceKind.Flaperon:
				case SurfaceKind.Aileron:
					num6 = 1f;
					num7 = 1f;
					break;
				case SurfaceKind.Stabilator:
					num6 = (num7 = Mathf.Lerp(Plugin.StabRollShare, 1f, t));
					break;
				case SurfaceKind.Rudder:
					num6 = 0f;
					num7 = num5;
					break;
				default:
					num6 = (num7 = 0f);
					break;
				}
				if (num6 != 0f || num7 != 0f)
				{
					float num8 = float.MaxValue;
					float num9 = float.MinValue;
					float num10 = float.MaxValue;
					float num11 = float.MinValue;
					float pMinS = float.MaxValue;
					float pMaxS = float.MinValue;
					// F-22E 4.0.1 port (SweepGridCache): the deflection's geometry from this tick's GridGeom (built for the same gS list),
					// the flow's terms from fp (= EvalFlowAt). F-22E 4.1.0 port (NativeSweep): the whole loop in the native core; it leaves
					// the six values untouched when it declines
					bool gridGeom = swGrid != null && j < swGrid.Length && swGrid[j];
					SurfaceEffector.FlowPrep fp = fpC;
					if (!(gridGeom && swNative && surfaceEffector.GridRangeNative(ref fp, ref ctx, IaInv[0, 0], IaInv[0, 2], IaInv[2, 0], IaInv[2, 2], ref pMinS, ref pMaxS, ref num8, ref num9, ref num10, ref num11)))
					for (int l = 0; l < surfaceEffector.N; l++)
					{
						// Revoker 0.2.7: the asymmetric-load cap judges the canards on their stock 40 deg; the 5 deg added in 0.2.7
						// stay a reserve it does not spend. Counted, they raised the cap with a canard shot off from 30-47 to 49-64
						// deg (bench, canard_L + wingtip_R + aileron_R + flap_L gone, 350 km/h reversal), where the stop-side roll
						// authority was down to 11-13 deg/s^2 (0.2.6: 33-65) and the roll error doubled.
						// Sampled on the stock grid too (31 points over +-40), not the +-45 grid with points over 40 skipped: the cap is
						// sensitive to where the sweep samples, and the coarser grid alone raised it 25-38 -> 51-53 deg in the full
						// pull at 290-300 km/h with that damage.
						float gS = (surfaceEffector.kind == SurfaceKind.Stabilator && AsymStabTravel > 0f && surfaceEffector.N > 1) ? Mathf.Lerp(0f - AsymStabTravel, AsymStabTravel, (float)l / (float)(surfaceEffector.N - 1)) : surfaceEffector.grid[l];
						Vector3 m2 = gridGeom ? surfaceEffector.EvalFlowGrid(l, ref fp, ref ctx) : surfaceEffector.EvalFlowAt(gS, ref fp, ref ctx);
						pMinS = Mathf.Min(pMinS, m2.y);
						pMaxS = Mathf.Max(pMaxS, m2.y);
						float b = AccRoll(m2);
						float b2 = AccYaw(m2);
						num8 = Mathf.Min(num8, b);
						num9 = Mathf.Max(num9, b);
						num10 = Mathf.Min(num10, b2);
						num11 = Mathf.Max(num11, b2);
					}
					num3 += num6 * 0.5f * (num9 - num8);
					num4 += num7 * 0.5f * (num11 - num10);
					if (surfaceEffector.kind == SurfaceKind.Stabilator)
					{
						pStabMin += pMinS;
						pStabMax += pMaxS;
					}
				}
			}
			asymNetDn[i] = (pAero + pStabMin) * IaInv[1, 1] * 57.29578f;
			asymAirP[i] = pAero * IaInv[1, 1] * 57.29578f;
			asymStabLo[i] = pStabMin * IaInv[1, 1] * 57.29578f;
			asymStabHi[i] = pStabMax * IaInv[1, 1] * 57.29578f;
			// 2.0.9: the symmetric rudder toe (2.0.7 pitch aid, up to RudderToeMax each way) counts toward the nose-down
			// authority the low-thrust cap judges by (the rudders' pitch was left out before, neutral or toed)
			float pRud = (toe > 0f) ? Mathf.Min(pRud0, Mathf.Min(pRudP, pRudM)) : 0f;
			authNetDn[i] = (pAero + pStabMin + pFlapNow + pRud + pHingeAuth) * IaInv[1, 1] * 57.29578f;
			asymNetUp[i] = (pAero + pStabMax) * IaInv[1, 1] * 57.29578f;
			asymAr[i] = AccRoll(m);
			asymAy[i] = AccYaw(m);
			asymCr[i] = num3;
			asymCy[i] = num4;
			// 1.8.45: the demand is judged at the dynamic pressure predicted from a rapid IAS decay. Load and authority
			// both scale with q and the absolute reserves (AsymRollReserveAccel / AsymYawReserveAccel) do not, so where
			// those bind, and through the fixed deadband everywhere, a fast deceleration brings the cap down ahead of
			// the authority loss. The share of the authority the reserve may claim is AsymResMaxFrac (0.8, as before).
			float kq = qPredK;
			float crK = kq * num3;
			float cyK = kq * num4;
			float num12 = Mathf.Clamp01(Plugin.AsymReserve);
			float tauRes = (Plugin.AsymRollTau > 0.01f) ? (Mathf.Max(0f, pStockRef) / Plugin.AsymRollTau) : 0f;
			float num13 = Mathf.Min(Mathf.Max(num12 * crK, Mathf.Max(tauRes, Plugin.AsymRollReserveAccel)), AsymResMaxFrac * crK);
			float num14 = Mathf.Min(Mathf.Max(num12 * cyK, Plugin.AsymYawReserveAccel), AsymResMaxFrac * cyK);
			float num15 = Mathf.Max(0f, Plugin.AsymDeadband);
			float num16 = 0.25f * num15;
			// 2.0.12: the thrust part of the load (one engine out: tvc.Mnow) does not fall with q the way the aero load
			// and the authority do, so only the aero part is scaled by the predicted q. 2.0.11 scaled both: the engine-out
			// cap was judged at today's IAS however fast the pull was bleeding it (10:37 log: AoA 64.7 deg at 101 m/s before
			// the cap came down, 3 s after the pull).
			float arT = AsymThrustSplit ? AccRoll(vector) : 0f;
			float ayT = AsymThrustSplit ? AccYaw(vector) : 0f;
			// 2.0.13: with an engine out the thrust part is left out of the cap altogether (the cap is the fixed
			// EngineOutAoACap plus whatever an aerodynamic asymmetry needs): the thrust yaw is the throttle's business
			// (VMC protection), and judging it here made the cap follow speed and throttle, 12 <-> 65 deg in seconds
			float tK = (((engOut || engAsym) && EngineOutFixedCap) || tKHoldT > 0f) ? 0f : 1f;
			float arL = kq * (asymAr[i] - arT) + tK * arT;
			float ayL = kq * (asymAy[i] - ayT) + tK * ayT;
			asymU[i] = Mathf.Max(0f, Mathf.Abs(arL) - num15) / Mathf.Max(crK - num13, 1f) + Mathf.Max(0f, Mathf.Abs(ayL) - num16) / Mathf.Max(cyK - num14, 1f);
			if (cp != null)
			{
				Vector3 mLd = cplTv ? (mAir - vector) : (mAir - (1f - tK) * vector);
				cplDnTv[i] = cplTv;
				CplSolve(i, cp, mLd, kq, num12, tauRes, dMbP, dMbN);
				if (CplDebug && cplClock >= CplDbgT2 && Mathf.Abs(asymAlphas[i] - alpha) <= 2.5f) { CplDbgT2 = float.MaxValue; CplDump(i, cp, mLd); }
			}
			else if (cplActive)
			{
				cplU[i] = asymU[i];
				cplRp[i] = (cplRn[i] = 0f);
				cplDn[i] = asymNetDn[i];
				cplUp[i] = asymNetUp[i];
				cplDnTv[i] = false;
			}
		}

		// the parts' state for the sweep (main thread): rotation, arm, drag area, foil and dynamic-pressure factor
		// the differential nozzles (Ifrit) or the yaw nozzle (Revoker line) as one LP effector: the points of its grid inside the travel
		// the law gives it now (lo..hi); no effector when none is
		private void TvExtraEff(CplPoint cp, Effector eX, string nm)
		{
			if (eX == null || eX.M == null || eX.N <= 1)
			{
				return;
			}
			int nY = 0;
			if (CplDebug) { Vector3 mNw = eX.MAt(eX.now); cplDbgNow[cp.nEff] = new Vector3(AccRoll(mNw), mNw.y * IaInv[1, 1] * 57.29578f, AccYaw(mNw)); cplDbgName[cp.nEff] = nm; cplDbgDefl[cp.nEff] = eX.now; }
			for (int kT = 0; kT < CplStride(eX.N); kT++)
			{
				int lT = CplIdx(kT, eX.N);
				if (eX.grid[lT] < eX.lo - 0.01f || eX.grid[lT] > eX.hi + 0.01f)
				{
					continue;
				}
				Vector3 mT = eX.M[lT];
				cp.Add(AccRoll(mT), mT.y * IaInv[1, 1] * 57.29578f, AccYaw(mT), eX.grid[lT]);
				nY++;
			}
			if (nY > 0)
			{
				cp.EndEff();
			}
		}

		private void SwGather(float rho)
		{
			// Ternion 0.2.2 (SweepSlatSchedule): the coupled check's load with the slats where the in-flight schedule puts them at each
			// point's AoA (per-jet hook: SwSlatSchedOn / SwHlAreas / SwSlatPos; no scheduled slats = off)
			swSlatSched = SwSlatSchedOn();
			int cap = aeroParts.Count;
			if (swRot.Length < cap || swHl.Length < cap)
			{
				swRot = new Quaternion[cap];
				swLhs = new Vector3[cap];
				swDrag = new float[cap];
				swFoil = new AirfoilTable[cap];
				swQ = new float[cap];
				swHl = new bool[cap];
				swHlQe = new float[cap];
				swHlRet = new float[cap];
				swHlDep = new float[cap];
			}
			swN = 0;
			foreach (AeroPart aeroPart in aeroParts)
			{
				if (aeroPart == null || aeroPart.rb == null || aeroPart.IsDetached())
				{
					continue;
				}
				Transform transform = Acc.AP_liftNormal(aeroPart);
				if (!(transform == null))
				{
					Quaternion rotation = AirframeRot(aeroPart, transform);
					int k = swN++;
					swRot[k] = rotation;
					swLhs[k] = aeroPart.rb.worldCenterOfMass + rotation * Acc.AP_centerOfLift(aeroPart) - cg;
					swDrag[k] = Acc.AP_dragArea(aeroPart);
					if (!surfaceParts.Contains(aeroPart) && foils.TryGetValue(aeroPart, out var value) && Acc.AP_wingArea(aeroPart) > 0f)
					{
						swFoil[k] = value;
						swQ[k] = 0.5f * rho * Acc.AP_wingArea(aeroPart) * Acc.AP_wingEff(aeroPart);
						swHl[k] = swSlatSched && SwHlAreas(aeroPart, out swHlRet[k], out swHlDep[k]);
						swHlQe[k] = 0.5f * rho * Acc.AP_wingEff(aeroPart);
					}
					else
					{
						swFoil[k] = null;
						swQ[k] = 0f;
						swHl[k] = false;
					}
				}
			}
		}

		// the deflections AsymSweepPoint's capacity loop evaluates for surface j, in its order (same expression as the loop's gS)
		private float[] SwDeflections(SurfaceEffector se, int j)
		{
			if (!(se.kind == SurfaceKind.Stabilator && AsymStabTravel > 0f && se.N > 1))
			{
				return se.grid;
			}
			float[] d = swDefl[j];
			if (d == null || d.Length != se.N)
			{
				d = (swDefl[j] = new float[se.N]);
			}
			for (int l = 0; l < se.N; l++)
			{
				d[l] = Mathf.Lerp(0f - AsymStabTravel, AsymStabTravel, (float)l / (float)(se.N - 1));
			}
			return d;
		}

		private void SwGridGeom(int k0, int k1, ref EvalContext ctx)
		{
			if (swGrid == null || swGrid.Length != surfaces.Count)
			{
				swGrid = new bool[surfaces.Count];
				swDefl = new float[surfaces.Count][];
			}
			swGeomList.Clear();
			swGeomDefl.Clear();
			bool on = Plugin.SweepGridCache;
			for (int j = 0; j < surfaces.Count; j++)
			{
				swGrid[j] = false;
				SurfaceEffector se = surfaces[j];
				if (!on || !se.active || se.N <= 0 || se.grid == null)
				{
					continue;
				}
				// the capacity loop's own condition (AsymSweepPoint: num6 != 0 || num7 != 0), for any point of this tick
				bool need = false;
				switch (se.kind)
				{
				case SurfaceKind.Flaperon:
				case SurfaceKind.Aileron:
					need = true;
					break;
				case SurfaceKind.Stabilator:
					for (int i = k0; i < k1 && !need; i++)
					{
						need = Mathf.Lerp(Plugin.StabRollShare, 1f, Mathf.InverseLerp(30f, 45f, Mathf.Abs(asymAlphas[i]))) != 0f;
					}
					break;
				case SurfaceKind.Rudder:
					for (int i = k0; i < k1 && !need; i++)
					{
						need = RudderAuthAt(asymAlphas[i]) != 0f;
					}
					break;
				}
				if (need)
				{
					swGrid[j] = true;
					float[] dl = SwDeflections(se, j);
					// 4.1.0: with the native table rebuild the geometry of this tick is already there (SurfaceEffector.EvalCompute),
					// if the sweep evaluates the same deflections
					if (!se.GridGeomFreshFor(ref ctx, dl))
					{
						swGeomList.Add(se);
						swGeomDefl.Add(dl);
					}
				}
			}
			// Revoker 1.3.0 (CplGridCache, from the Ternion 0.3.0): the coupled check's points, for every surface it takes points from,
			// if any of this tick's sweep points solves it
			if (swCpl == null || swCpl.Length != surfaces.Count)
			{
				swCpl = new bool[surfaces.Count];
			}
			swCplList.Clear();
			bool cplAny = false;
			if (on && CplGridCache && cplActive && cplLoaded)
			{
				for (int i = k0; i < k1 && !cplAny; i++)
				{
					cplAny = CplHere(i);
				}
			}
			for (int j = 0; j < surfaces.Count; j++)
			{
				SurfaceEffector se = surfaces[j];
				swCpl[j] = cplAny && se.active && se.N > 0 && se.grid != null;
				if (swCpl[j])
				{
					swCplList.Add(se);
				}
			}
			int m = swGeomList.Count;
			int mc = swCplList.Count;
			if (m + mc == 0)
			{
				return;
			}
			if (Plugin.ParallelSweep && m + mc > 1)
			{
				if (swGeomWork == null)
				{
					swGeomWork = new GeomWork(swGeomList, swGeomDefl, swCplList);
				}
				swGeomWork.ctx = ctx;
				IndexFork.Finish(IndexFork.Start(swGeomWork, m + mc));
			}
			else
			{
				for (int i = 0; i < m; i++)
				{
					swGeomList[i].GridGeom(ref ctx, swGeomDefl[i]);
				}
				for (int i = 0; i < mc; i++)
				{
					swCplList[i].CplGeom(ref ctx, CplStride(swCplList[i].N));
				}
			}
		}

		private void HingeSnaps()
		{
			int nh = hinges.Count;
			if (hSnap.Length < nh)
			{
				hSnap = new HingeFlapEffector.FlowSnap[nh];
				hSym = new float[nh];
			}
			for (int hi = 0; hi < nh; hi++)
			{
				HingeFlapEffector hA = hinges[hi];
				if (hA.active)
				{
					hA.Snap(out hSnap[hi]);
					hSym[hi] = hA.SymNow;
				}
			}
		}

		// Revoker 0.2.0: a part's lift-normal rotation for the airframe models: an inboard elevon's camber host without the
		// camber the elevon's effector already models
		private Quaternion AirframeRot(AeroPart ap, Transform ln)
		{
			for (int i = 0; i < hinges.Count; i++)
			{
				if (hinges[i].host == ap && hinges[i].hostLN == ln)
				{
					return hinges[i].HostRot0;
				}
			}
			return ln.rotation;
		}

		private sealed class GeomWork : IIndexWork
		{
			private readonly List<SurfaceEffector> list;

			private readonly List<float[]> defl;

			public EvalContext ctx;

			private readonly List<SurfaceEffector> cpl;

			public GeomWork(List<SurfaceEffector> list, List<float[]> defl, List<SurfaceEffector> cpl)
			{
				this.list = list;
				this.defl = defl;
				this.cpl = cpl;
			}

			public void Do(int i)
			{
				EvalContext cx = ctx;
				if (i < list.Count)
				{
					list[i].GridGeom(ref cx, defl[i]);
				}
				else
				{
					SurfaceEffector se = cpl[i - list.Count];
					se.CplGeom(ref cx, CplStride(se.N));
				}
			}
		}

		private sealed class SweepWork : IIndexWork
		{
			private readonly FcsController c;
			private int k0;
			private float rho, num;
			private Quaternion rootRot;
			private EvalContext ctx;
			private float[] array;
			private Vector3 vector;

			public SweepWork(FcsController c)
			{
				this.c = c;
			}

			public void Set(int k0, float rho, Quaternion rootRot, EvalContext ctx, float[] array, float num, Vector3 vector)
			{
				this.k0 = k0;
				this.rho = rho;
				this.rootRot = rootRot;
				this.ctx = ctx;
				this.array = array;
				this.num = num;
				this.vector = vector;
			}

			public void Do(int i)
			{
				EvalContext c2 = ctx;
				c.AsymSweepPoint(k0 + i, rho, rootRot, ref c2, array, num, vector);
			}
		}

		private Vector3 AsymMomentAt(float aDeg, float rho, Quaternion rootRot, ref EvalContext ctx)
		{
			float[] array = asymDsym;
			if (array == null)
			{
				return Vector3.zero;
			}
			float num = Mathf.Max(tas, 1f);
			float f = aDeg * (MathF.PI / 180f);
			Vector3 vector = rootRot * new Vector3(0f, (0f - Mathf.Sin(f)) * num, Mathf.Cos(f) * num);
			Vector3 vector2 = vector / num;
			float num2 = 0.5f * rho * num * num;
			Vector3 zero = Vector3.zero;
			foreach (AeroPart aeroPart in aeroParts)
			{
				if (aeroPart == null || aeroPart.rb == null || aeroPart.IsDetached())
				{
					continue;
				}
				Transform transform = Acc.AP_liftNormal(aeroPart);
				if (!(transform == null))
				{
					Quaternion rotation = AirframeRot(aeroPart, transform);
					Vector3 lhs = aeroPart.rb.worldCenterOfMass + rotation * Acc.AP_centerOfLift(aeroPart) - cg;
					Vector3 rhs = -vector2 * (0.5f * num2 * Acc.AP_dragArea(aeroPart));
					if (!surfaceParts.Contains(aeroPart) && foils.TryGetValue(aeroPart, out var value) && Acc.AP_wingArea(aeroPart) > 0f)
					{
						rhs += PartForce(vector, rotation, rotation * Vector3.right, value, 0.5f * rho * Acc.AP_wingArea(aeroPart) * Acc.AP_wingEff(aeroPart));
					}
					zero += Vector3.Cross(lhs, rhs);
				}
			}
			Vector3 vector3 = Quaternion.Inverse(rootRot) * zero;
			Vector3 result = new Vector3(0f - vector3.z, 0f - vector3.x, vector3.y) + (tvc.active ? (((tvcd != null && tvcd.active) || (tvcy != null && tvcy.active)) ? tvc.MAt(tvc.now) : tvc.Mnow) : FixedThrustLat());
			for (int i = 0; i < surfaces.Count; i++)
			{
				if (surfaces[i].active)
				{
					result += surfaces[i].EvalFlow(array[i], vector, rho, ref ctx);
				}
			}
			foreach (HingeFlapEffector hA in hinges)
			{
				if (hA.active)
				{
					result += hA.EvalFlow(hA.SymNow, vector, rho, ref ctx);
				}
			}
			return result;
		}

		private void AsymPrepSym(Quaternion rootRot)
		{
			Vector3 rhs = rootRot * Vector3.right;
			Vector3 zero = Vector3.zero;
			int num = 0;
			foreach (SurfaceEffector surface in surfaces)
			{
				if (surface.partner != null && surface.active && surface.partner.active && surface.partner is SurfaceEffector surfaceEffector && !(surface.part.rb == null) && !(surfaceEffector.part.rb == null))
				{
					zero += 0.5f * (surface.part.rb.worldCenterOfMass + surfaceEffector.part.rb.worldCenterOfMass);
					num++;
				}
			}
			// Revoker 0.2.1: with no intact pair left the reference is the airframe's centreline (the root transform; the
			// Revoker's parts are mirrored about it); 0.2.0 reported 0
			asymDy = ((num > 0) ? Vector3.Dot(cg - zero / num, rhs) : Vector3.Dot(cg - ((Component)(object)ac).transform.position, rhs));
			float[] array = asymDsym;
			if (array == null || array.Length != surfaces.Count)
			{
				array = (asymDsym = new float[surfaces.Count]);
			}
			for (int i = 0; i < surfaces.Count; i++)
			{
				array[i] = 0f;
			}
			for (int j = 0; j < surfaces.Count; j++)
			{
				SurfaceEffector surfaceEffector2 = surfaces[j];
				if (surfaceEffector2.partner != null)
				{
					int num2 = ((surfaceEffector2.partner is SurfaceEffector item) ? surfaces.IndexOf(item) : (-1));
					if (num2 >= 0)
					{
						float num3 = surfaceEffector2.pairSign * surfaceEffector2.now;
						float num4 = surfaceEffector2.partner.pairSign * surfaceEffector2.partner.now;
						float num5 = 0.5f * (num3 + num4);
						array[j] = surfaceEffector2.pairSign * (num3 - num5);
						array[num2] = surfaceEffector2.partner.pairSign * (num4 - num5);
					}
				}
			}

			if (surfaceParts.Count != 0)
			{
				return;
			}
			foreach (SurfaceEffector surface2 in surfaces)
			{
				if (surface2.part != null)
				{
					surfaceParts.Add(surface2.part);
				}
			}
			// Revoker 0.2.0: the inboard elevons are modelled by their effector too
			foreach (HingeFlapEffector hP in hinges)
			{
				if (hP.part != null)
				{
					surfaceParts.Add(hP.part);
				}
			}
		}

		// Compass 0.1.11: the roll and yaw of fixed-nozzle thrust for the asymmetric-load model, with an engine out or down on thrust
		// only. Its pitch is left out (the pitch caps' model is as before), and with both engines matched it is left out too: counted,
		// the thrust about a store-shifted CG lowered the asymmetric-store AoA cap by up to 4 deg (bench, 435 kg on wing2_R, 250 km/h:
		// 17.2 -> 13.4), a change to a feature not touched here.
		private Vector3 FixedThrustLat()
		{
			if (tvc == null || !tvc.fixedNozzles)
			{
				return Vector3.zero;
			}
			if (engOut || engAsym)
			{
				return new Vector3(tvc.Mnow.x, 0f, tvc.Mnow.z);
			}
			// faded in on the lateral CG offset, so a symmetric jet gets exactly zero (Compass 0.4.0: on always, the tiny thrust moments
			// re-ordered the allocator's equal-cost picks in every clean case)
			float gT = AsymThrustCgLoad ? Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / CoupHeldDySpan) : 0f;
			return (gT > 0f) ? (gT * new Vector3(tvc.Mnow.x, 0f, tvc.Mnow.z)) : Vector3.zero;
		}

		// one nozzle's moment at deflection d from the sweep's copy of its table (bench diagnostic)
		private Vector3 TvNozzleAt(int e, float d)
		{
			if (cplTvN < 2)
			{
				return Vector3.zero;
			}
			float x = Mathf.Clamp((d - cplTvG[0]) / (cplTvG[cplTvN - 1] - cplTvG[0]) * (float)(cplTvN - 1), 0f, (float)(cplTvN - 1) - 0.001f);
			int k = (int)x;
			return Vector3.Lerp(cplTvM[e][k], cplTvM[e][k + 1], x - (float)k);
		}

		// point i's flight-path roll reserve each way with the load in, pitch held and no sideslip acceleration, the demand from it, and
		// the held-load pitch figure (the most nose-down / nose-up with the load's roll and yaw held)
		private void CplSolve(int i, CplPoint cp, Vector3 mLoad, float kq, float resShare, float tauRes, Vector3 dMbP, Vector3 dMbN)
		{
			CplPoint.KeepSolution = CplDebug;
			float aDeg = asymAlphas[i];
			float aR = aDeg * (MathF.PI / 180f);
			float sinA = Mathf.Sin(aR);
			float cosA = Mathf.Cos(aR);
			float bR = AccRoll(mLoad);
			float bQ = mLoad.y * IaInv[1, 1] * 57.29578f;
			float bY = AccYaw(mLoad);
			int sgn = (aDeg >= 0f) ? 1 : -1;
			if (cp.overflow || cp.nEff == 0)
			{
				cplU[i] = asymU[i];
				cplRp[i] = (cplRn[i] = 0f);
				cplDn[i] = asymNetDn[i];
				cplUp[i] = asymNetUp[i];
				cplDnTv[i] = false;
				return;
			}
			bool okA;
			float rp, rn;
			Vector3 dAgP = Vector3.zero, dAgN = Vector3.zero;
			if (CplBetaDesign > 0f)
			{
				// each side with the sideslip that rolls against it, and only that side of each (Su-30 0.5.0: the Compass solved the
				// zero-sideslip case too and both sides of all three, 3 phase 1 + 6 phase 2 where 2 + 2 give the same two figures)
				float vvP = cosA * AccRoll(dMbP) + sinA * AccYaw(dMbP);
				float vvN = cosA * AccRoll(dMbN) + sinA * AccYaw(dMbN);
				cplVbP[i] = vvP;
				cplVbN[i] = vvN;
				dAgP = (vvP <= vvN) ? dMbP : dMbN;
				dAgN = (vvP <= vvN) ? dMbN : dMbP;
				bool okP = cp.Reserve(sinA, cosA, bR + AccRoll(dAgP), bQ + dAgP.y * IaInv[1, 1] * 57.29578f, bY + AccYaw(dAgP), sgn, out rp, out float _, 1);
				bool okN = cp.Reserve(sinA, cosA, bR + AccRoll(dAgN), bQ + dAgN.y * IaInv[1, 1] * 57.29578f, bY + AccYaw(dAgN), sgn, out float _, out rn, -1);
				okA = okP && okN;
			}
			else
			{
				okA = cp.Reserve(sinA, cosA, bR, bQ, bY, sgn, out rp, out rn);
			}
			cplRp[i] = okA ? rp : -999f;
			cplRn[i] = okA ? rn : -999f;
			cplKqA[i] = kq;
			if (CplRefAuth && (i == cplRefI0p || i == cplRefI1p || i == cplRefI0n || i == cplRefI1n))
			{
				// Su-30 0.5.2: the same point without the load's roll and yaw (each side with its adverse design sideslip); the mean of the
				// two sides (they differ only by the effectors' arms about the offset CG)
				float cA = float.NaN;
				if (CplBetaDesign > 0f)
				{
					bool c1 = cp.Reserve(sinA, cosA, AccRoll(dAgP), bQ + dAgP.y * IaInv[1, 1] * 57.29578f, AccYaw(dAgP), sgn, out float cRp, out float _, 1);
					bool c2 = cp.Reserve(sinA, cosA, AccRoll(dAgN), bQ + dAgN.y * IaInv[1, 1] * 57.29578f, AccYaw(dAgN), sgn, out float _, out float cRn, -1);
					if (c1 && c2)
					{
						cA = 0.5f * (cRp + cRn);
					}
				}
				else if (cp.Reserve(sinA, cosA, 0f, bQ, 0f, sgn, out float cRp0, out float cRn0))
				{
					cA = 0.5f * (cRp0 + cRn0);
				}
				cplClean[i] = cA;
			}
			if (!okA)
			{
				// no pitch-held, sideslip-free roll either way: if the lateral problem itself is feasible, it is the pitch that cannot
				// be held here, which the nose-down / authority caps own; if not, the load cannot be held at all
				cplU[i] = cp.LateralFeasible(sinA, cosA, bR, bY) ? -1f : 9f;   // -1: filled from the point below (CplFill)
			}
			else
			{
				float rc = 0.5f * (rp + rn);
				float rMin = Mathf.Min(rp, rn);
				float rcK = kq * Mathf.Max(rc, 0f);
				float req = Mathf.Min(Mathf.Max(Mathf.Clamp01(resShare) * rcK, Mathf.Max(tauRes, Plugin.AsymRollReserveAccel)), CplResMaxFrac * rcK);
				cplU[i] = Mathf.Max(0f, kq * (rc - rMin) - CplDeadband) / Mathf.Max(rcK - req, 1f);
				float rfA = (aDeg >= 0f) ? cplRefPos : cplRefNeg;
				if (CplRefAuth && rfA > 0f)
				{
					cplU[i] = kq * rfA / Mathf.Max(kq * rMin + CplDeadband, 1f);
				}
			}
			if (!cplPitchGate)
			{
				cplDn[i] = asymNetDn[i];
				cplUp[i] = asymNetUp[i];
				cplDnTv[i] = false;
			}
			else if (sgn > 0)
			{
				float qd = cp.PitchHeld(-1, bR, bQ, bY);
				cplDn[i] = float.IsNaN(qd) ? 999f : qd;
				cplUp[i] = asymNetUp[i];
			}
			else
			{
				float qu = cp.PitchHeld(1, bR, bQ, bY);
				cplUp[i] = float.IsNaN(qu) ? -999f : qu;
				cplDn[i] = asymNetDn[i];
			}
		}

		// a point where the pitch itself cannot be held (no reserve figure) takes the demand of the next point toward zero AoA, so the
		// curve does not read as falling there
		private void CplFill()
		{
			int i0 = Array.IndexOf(asymAlphas, 0f);
			if (i0 < 0)
			{
				return;
			}
			if (cplU[i0] < 0f)
			{
				cplU[i0] = 0f;
			}
			for (int k = i0 + 1; k < asymAlphas.Length; k++)
			{
				if (cplU[k] < 0f)
				{
					cplU[k] = cplU[k - 1];
				}
			}
			for (int k = i0 - 1; k >= 0; k--)
			{
				if (cplU[k] < 0f)
				{
					cplU[k] = cplU[k + 1];
				}
			}
		}

		private void CplDump(int i, CplPoint cp, Vector3 mLoad)
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendFormat(CultureInfo.InvariantCulture, "CPLMIX t {0:F2} aPt {1:F0} aNow {2:F1} ias {3:F1} load R {4:F1} Q {5:F1} Y {6:F1} | rp {7:F1} rn {8:F1} u {9:F2} heldDn {10:F1}\n", cplClock, asymAlphas[i], alpha, ias, AccRoll(mLoad), mLoad.y * IaInv[1, 1] * 57.29578f, AccYaw(mLoad), cplRp[i], cplRn[i], cplU[i], cplDn[i]);
			for (int e = 0; e < cp.nEff; e++)
			{
				double gP = 0, gN = 0; float rN = 0, qN = 0, yN = 0, rP = 0, qP = 0, yP = 0;
				for (int j = cp.effStart[e]; j < cp.effStart[e + 1]; j++)
				{
					gP += cp.solP[j] * cp.G[j]; gN += cp.solN[j] * cp.G[j];
					rN += (float)cp.solN[j] * cp.R[j]; qN += (float)cp.solN[j] * cp.Q[j]; yN += (float)cp.solN[j] * cp.Y[j];
					rP += (float)cp.solP[j] * cp.R[j]; qP += (float)cp.solP[j] * cp.Q[j]; yP += (float)cp.solP[j] * cp.Y[j];
				}
				Vector3 nw = cplDbgNow[e];
				sb.AppendFormat(CultureInfo.InvariantCulture, "   {0,-16} now {1,6:F1} (R {2,6:F1} Q {3,6:F1} Y {4,6:F1}) | LP-neg {5,6:F1} (R {6,6:F1} Q {7,6:F1} Y {8,6:F1}) | LP-pos {9,6:F1} (R {10,6:F1} Q {11,6:F1} Y {12,6:F1}) | pts {13}\n", cplDbgName[e] ?? "?", cplDbgDefl[e], nw.x, nw.y, nw.z, gN, rN, qN, yN, gP, rP, qP, yP, cp.effStart[e + 1] - cp.effStart[e]);
			}
			foreach (Effector se in effs)
			{
				sb.AppendFormat(CultureInfo.InvariantCulture, "   BOUNDS {0,-12} now {1,6:F1} cmd {2,6:F1} lo {3,6:F1} hi {4,6:F1}\n", se.name, se.now, se.cmd, se.lo, se.hi);
			}
			Console.Write(sb.ToString());
		}

		internal static int CplIdx(int k, int n)
		{
			int m = CplStride(n);
			return (m >= n) ? k : Mathf.RoundToInt((float)k * (float)(n - 1) / (float)(m - 1));
		}

		// the LP only where the cap can use it: positive AoA up to 5 deg past the limit, the negative side down to 5 deg past its limit
		private bool CplHere(int i)
		{
			return (asymAlphas[i] >= 0f) ? (asymAlphas[i] <= Plugin.AoAPos + 5f) : (asymAlphas[i] >= Plugin.AoANeg - 5f);
		}

		private static int CplStride(int n)
		{
			return Mathf.Min(n, Mathf.Max(2, CplPts));
		}

		// the share of the flight-path roll range the reserve may claim (the one-axis check: AsymResMaxFrac 0.8): the dev setting
		// AsymCoupledReserveMax, 0.7
		// Revoker 1.3.0: the no-TVC path has its own share (AsymCoupledReserveMaxNoTvc, 0.8): without the nozzles the surfaces also make the
		// pitch-in, and at 0.7 a full pull at 500 - 550 km/h ran 6 - 9 deg past the limit with 13 deg of sideslip (bench, 30 cm)
		private float CplResMaxFrac => cplNoTv ? Plugin.AsymCoupledReserveMaxNoTvc : Plugin.AsymCoupledReserveMax;

		// at the end of a sweep: the clean reference of this sweep, and every point's demand against it
		private void CplDemand()
		{
			if (!CplRefAuth)
			{
				return;
			}
			float rP = CplRefAt(cplRefAp, cplRefI0p, cplRefI1p);
			float rN = CplRefAt(cplRefAn, cplRefI0n, cplRefI1n);
			if (!float.IsNaN(rP))
			{
				cplRefPos = rP;
			}
			if (!float.IsNaN(rN))
			{
				cplRefNeg = rN;
			}
			for (int i = 0; i < asymAlphas.Length; i++)
			{
				float rf = (asymAlphas[i] >= 0f) ? cplRefPos : cplRefNeg;
				if (rf > 0f && cplRp[i] > -900f && cplRn[i] > -900f && cplU[i] >= 0f && cplU[i] < 8.5f)
				{
					float kq = Mathf.Max(0.01f, cplKqA[i]);
					cplU[i] = kq * rf / Mathf.Max(kq * Mathf.Min(cplRp[i], cplRn[i]) + CplDeadband, 1f);
				}
			}
		}

		private float CplKnee(int side)
		{
			int n = asymAlphas.Length;
			float lim = (side > 0) ? Mathf.Min(Plugin.AoAPos, Mathf.Max(5f, authCapEff)) : (0f - Mathf.Max(0f - Plugin.AoANeg, 0f - Mathf.Min(-5f, authCapNegEff)));
			float noCap = (side > 0) ? Plugin.AoAPos : Plugin.AoANeg;
			float lAbs = Mathf.Abs(lim);
			float flo = Mathf.Min((side > 0) ? Plugin.AsymCapFloor : Plugin.AsymCapFloorNeg, lAbs);
			float wC = (side > 0) ? cplRefPos : cplRefNeg;
			float pLaw = Mathf.Max(CplStopFloor, cplLawRoll);
			if (wC <= 0f)
			{
				return noCap;
			}
			int iRef = (side > 0) ? cplRefI0p : cplRefI0n;
			float kqR = (iRef >= 0) ? Mathf.Max(0.01f, cplKqA[iRef]) : 1f;
			float tC = pLaw / Mathf.Max(1f, kqR * wC);
			float tA = (Plugin.AsymStopTime > 0f) ? Plugin.AsymStopTime : 999f;
			float tS = Mathf.Min(tC, tA);
			float dyG = Mathf.Max(Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / Mathf.Max(0.005f, CoupHeldDySpan)), (AsymDamageGate && dmgGateHold > 0f) ? 1f : 0f);
			float wE = Plugin.AsymAoAWeight;
			float bestJ = -1f, bestA = lAbs, wEnd = -1f, aEnd = 0f;
			int nC = 0;
			float[] ja = cplKa, jj = cplKj;
			for (int k = 0; k < n; k++)
			{
				int i = (side > 0) ? k : (n - 1 - k);
				float ai = side * asymAlphas[i];
				if (ai < flo - 0.01f || ai > lAbs + 0.01f)
				{
					continue;
				}
				float u = cplU[i];
				if (u < 0f)
				{
					continue;   // no pitch-held roll here: the nose-down / authority caps own this point
				}
				float w = (u < 8.5f && cplRp[i] > -900f && cplRn[i] > -900f) ? Mathf.Max(0f, cplKqA[i] * Mathf.Min(cplRp[i], cplRn[i])) : 0f;
				float roll = Mathf.Lerp(pLaw, Mathf.Min(pLaw, Mathf.Max(CplStopFloor, tS * w)), dyG);
				float j = Mathf.Pow(Mathf.Max(0.1f, ai), wE) * roll;
				ja[nC] = ai;
				jj[nC] = j;
				nC++;
				wEnd = w;
				aEnd = ai;
			}
			if (nC == 0)
			{
				return noCap;
			}
			// the limit itself: the reserve there taken from the last point the sweep has (the grid ends at +65 / -40)
			if (aEnd < lAbs - 0.01f && wEnd >= 0f)
			{
				float rollL = Mathf.Lerp(pLaw, Mathf.Min(pLaw, Mathf.Max(CplStopFloor, tS * wEnd)), dyG);
				ja[nC] = lAbs;
				jj[nC] = Mathf.Pow(lAbs, wE) * rollL;
				nC++;
			}
			int kB = 0;
			for (int k = 1; k < nC; k++)
			{
				if (jj[k] > jj[kB])
				{
					kB = k;
				}
			}
			bestA = ja[kB];
			if (kB > 0 && kB < nC - 1)
			{
				// the peak between the neighbours (parabola through three points): a smooth limit, not one stepping 5 deg at a time
				float x0 = ja[kB - 1], x1 = ja[kB], x2 = ja[kB + 1];
				float y0 = jj[kB - 1], y1 = jj[kB], y2 = jj[kB + 1];
				float den = (x0 - x1) * (x0 - x2) * (x1 - x2);
				if (Mathf.Abs(den) > 1E-06f)
				{
					float a2 = (x2 * (y1 - y0) + x1 * (y0 - y2) + x0 * (y2 - y1)) / den;
					float b2 = (x2 * x2 * (y0 - y1) + x1 * x1 * (y2 - y0) + x0 * x0 * (y1 - y2)) / den;
					if (a2 < 0f)
					{
						bestA = Mathf.Clamp(-b2 / (2f * a2), x0, x2);
					}
				}
			}
			cplKneeJ = jj[kB];
			if (bestA >= lAbs - 0.25f)
			{
				return noCap;
			}
			return side * Mathf.Max(flo, bestA);
		}

		private float CplRefAt(float a, int i0, int i1)
		{
			if (i0 < 0 || i1 < 0)
			{
				return float.NaN;
			}
			float c0 = cplClean[i0], c1 = cplClean[i1];
			bool ok0 = !float.IsNaN(c0) && c0 > 0f, ok1 = !float.IsNaN(c1) && c1 > 0f;
			if (ok0 && ok1)
			{
				float w = Mathf.Clamp01((a - asymAlphas[i0]) / Mathf.Max(0.01f, asymAlphas[i1] - asymAlphas[i0]));
				return Mathf.Lerp(c0, c1, w);
			}
			// one end of the bracket has no pitch-held roll without the load either: the end nearer the reference AoA that has one
			return ok0 ? c0 : (ok1 ? c1 : float.NaN);
		}

		private float AsymGate()
		{
			float dyG = Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / CoupHeldDySpan);
			return (AsymDamageGate && dmgGateHold > 0f) ? 1f : dyG;
		}

		private float AsymPitchMarginEff(out float kqP)
		{
			kqP = AsymPitchAtPredQ ? Mathf.Clamp(qPredK, 0.3f, 1f) : 1f;
			if (AsymPitchMarginIasRef <= 0f)
			{
				return Plugin.AsymPitchMargin;
			}
			float iasP = asymSweepIas * Mathf.Sqrt(kqP);
			float sc = Mathf.Clamp01(iasP * iasP / (AsymPitchMarginIasRef * AsymPitchMarginIasRef));
			return Mathf.Max(Mathf.Min(AsymPitchMarginMin, Plugin.AsymPitchMargin), Plugin.AsymPitchMargin * sc);
		}

		private void PushCapSample(float pos, float neg, bool reset)
		{
			if (reset)
			{
				for (int i = 0; i < 3; i++)
				{
					capHist[i] = pos;
					capHistN[i] = neg;
				}
				capMed = (asymCapFilt = pos);
				capMedNeg = (asymCapFiltNeg = neg);
				return;
			}
			capHist[capHistI] = pos;
			capHistN[capHistI] = neg;
			capHistI = (capHistI + 1) % 3;
			capMed = Med3(capHist[0], capHist[1], capHist[2]);
			capMedNeg = Med3(capHistN[0], capHistN[1], capHistN[2]);
		}

		private void CapFilterStep(float dt)
		{
			// tightening passes quickly, relaxing slowly: noise can't lift the cap, a real drop is followed in ~0.3 s
			float kU = Mathf.Clamp01(dt / Mathf.Max(0.05f, AsymCapTauUp));
			float kD = Mathf.Clamp01(dt / Mathf.Max(0.05f, AsymCapTauDown));
			asymCapFilt += (capMed - asymCapFilt) * ((capMed < asymCapFilt) ? kD : kU);
			asymCapFiltNeg += (capMedNeg - asymCapFiltNeg) * ((capMedNeg > asymCapFiltNeg) ? kD : kU);
		}

		// 17 Nov (engine-out zero-thrust match on every twin, Tom 9 Nov rule): with an engine OUT and the zero-thrust match on, the FCS
		// takes the live engine's thrust away itself from EngineOutMatchAoA up, so its pitch limit is judged with the thrust it will leave
		// the jet there: none. The nozzle term is out of the low-thrust AoA cap and the nozzles count as not covering pitch from the
		// engine loss on, so the pull is flown to the airframe's own (no-TVC) limit instead of to the TVC limit with the thrust then
		// taken away under it (bench, Ifrit, left engine out, full aft at 350 km/h: thrust zeroed at 18-27 deg AoA while rotating at
		// 53 deg/s toward the 65 deg TVC limit; the airframe alone could not stop it, AoA past 170 deg).
		private bool ZeroThrustPlanned()
		{
			return engOut && EngineOutZeroMatch && Plugin.EngineOutZeroThrust && Plugin.EngineAbAoAGate;
		}

		// 17 Nov: an engine OUT whose live engine the FCS has matched to zero thrust leaves no thrust split. The laws' engine-out yaw rules
		// (the yaw-target clamp to the fins' reach, the roll-fight exemption) are for the split, not for the engine count: with the match
		// full they stand down and the jet flies as a symmetric one at zero thrust (bench, Ifrit, left engine out, zero match: the clamp
		// held the split decelerons shut for yaw, so they were not there as the airframe's nose-down device at 40-50 deg AoA and the
		// jet pitched past 90 deg with the stick forward; with it standing down the pull holds 33-44 deg).
		private bool EngSplitOut()
		{
			return engOut && !(matchFull && ZeroThrustPlanned());
		}

		private void AuthCapFromSweep()
		{
			if (!Plugin.LowThrustAoACap || Ia[1, 1] <= 1f)
			{
				authCap = 999f;
				return;
			}
			float tvDn = 0f;
			if (tvc.active && tvc.M != null && !ZeroThrustPlanned())
			{
				float tvLo = float.MaxValue;
				for (int tk = 0; tk < tvc.N; tk++)
				{
					tvLo = Mathf.Min(tvLo, tvc.M[tk].y);
				}
				if (tvLo < float.MaxValue)
				{
					tvDn = Mathf.Min(0f, tvLo * IaInv[1, 1] * 57.29578f);
				}
			}
			// 1.9.5: nozzle nose-down authority growing (throttle up / spool-up): the cap may follow at once
			if (0f - tvDn > authTvPrev + AuthCapThrustStep)
			{
				authFastT = AuthCapFastHold;
			}
			authTvPrev = 0f - tvDn;
			float margin = Plugin.AuthCapMargin;
			float cap = 999f;
			float prevA = 0f;
			float prevM = float.NaN;
			for (int ia = 0; ia < asymAlphas.Length; ia++)
			{
				float aA = asymAlphas[ia];
				if (aA < 0f)
				{
					continue;
				}
				// Ternion 0.1.2 (AuthCapPredict): the airframe's nose-down authority at the IAS the deceleration will bring (qPredK, the
				// same prediction the lateral brakes use), so the cap comes down ahead of a bleeding pull instead of behind it
				float mDn = 0f - (authNetDn[ia] * (AuthCapPredict ? qPredK : 1f) + tvDn);
				if (mDn < margin)
				{
					float aP = (float.IsNaN(prevM) || prevM <= mDn) ? aA : Mathf.Lerp(prevA, aA, (prevM - margin) / (prevM - mDn));
					cap = Mathf.Max(Plugin.AuthCapFloor, aP - AuthCapLead);
					break;
				}
				prevA = aA;
				prevM = mDn;
			}
			authCap = cap;
		}

		private void AuthCapStep(float dt)
		{
			// 2.0.9: after a change of the airframe test switches the cap follows its new target as fast as it comes down
			// (2.0.8 let it creep up from 36 at the at-cap rise rate, which looked like the old limit was still on)
			string key = Plugin.DisableTVC + "|" + Plugin.AoAFull65 + "|" + Plugin.AoACap36 + "|" + Plugin.NoTvcAoACapPos.ToString("F1") + "|" + Plugin.NoTvcAoACapNeg.ToString("F1") + "|" + Plugin.NoTvcThrCapPos.ToString("F1") + "|" + Plugin.NoTvcThrCapNeg.ToString("F1");
			if (testSwitchKey != key)
			{
				if (testSwitchKey != null)
				{
					// a looser cap is followed as fast as a tighter one (the 1.9.5 thrust-step path)
					authFastT = AuthCapFastHold;
				}
				testSwitchKey = key;
			}
			if (!Plugin.LowThrustAoACap)
			{
				authCapEff = 999f;
				authCapNegEff = -999f;
				authInit = false;
				return;
			}
			// 2.0.9: nozzles not covering pitch: low thrust (T/W under LowThrustTW, released 0.03 above) or DisableTVC
			noTvcOn = Plugin.DisableTVC || ZeroThrustPlanned() || (noTvcOn ? (tw < Plugin.LowThrustTW + 0.03f) : (tw < Plugin.LowThrustTW));
			// Ternion 0.1.1: on a jet with no nozzle pitch at all the fixed no-TVC caps are not applied; the swept authority cap
			// (authCap: where the canards and elevators still have nose-down / nose-up authority) and AoALimitPositive / Negative rule
			bool negCapOn = noTvcOn && !Plugin.AoAFull65 && !(noVecJet && NoVecUncapped);
			// 2.2.2: with DisableTVC the negative cap is NoTvcThrustAoACapNegative (45) at every thrust
			float tgtNeg = negCapOn ? Mathf.Max(Plugin.AoANeg, 0f - (Plugin.DisableTVC ? Plugin.NoTvcThrCapNeg : Plugin.NoTvcAoACapNeg)) : -999f;
			if (!authInit || tgtNeg <= authCapNegEff)
			{
				// first step, or released / loosened: straight to the new limit
				authCapNegEff = tgtNeg;
			}
			else
			{
				// tightening toward 0: from the limit that applied so far, as fast as the positive cap comes down
				float from = Mathf.Max(authCapNegEff, Plugin.AoANeg);
				authCapNegEff = from + (tgtNeg - from) * Mathf.Clamp01(dt / AuthCapTauDown);
			}
			float tgt = Mathf.Min(authCap, Plugin.AoAPos);
			// 2.2.19 (FinLossAoACap): with the fins gone the positive AoA is capped where the sideslip stays bounded in a held
			// roll (bench, both fins detached: 35-45 deg holds, 25-30 departs with full roll held, 50-65 lets the sideslip
			// grow to 15-22 deg with the nozzles on). Phased in from one fin lost (yawHealth 0.5) to both (0).
			if (Plugin.FinLossAoACap > 0f && yawHealth < 0.5f)
			{
				float kF = Mathf.Clamp01((0.5f - yawHealth) / 0.5f);
				tgt = Mathf.Min(tgt, Mathf.Lerp(Plugin.AoAPos, Plugin.FinLossAoACap, kF));
			}
			// Revoker 0.2.7 (FinLossNoTvcAoACap): with the fin damaged and no nozzle yaw (TVC off or below the low-thrust T/W), the
			// yaw left is the canards' and the elevons' drag, a few deg/s^2 at high AoA, against a fuselage that is directionally
			// unstable there. 28 Oct 20:31 log (TVC off, fin gone at 685 s): at the 45 deg cap, 50-60 m/s, the sideslip ran
			// -1 -> -75 deg in 10 s with every surface on its stop, then the jet flipped. Bench, fin gone, TVC off, held pull +
			// roll: 45-50 deg departs at 250-300 km/h, 40 departs with the game-like command delay, 35 and below hold (sideslip
			// <= 3.3 deg). Phased in from no damage (yawHealth 1) to the fin gone (0).
			if (Plugin.FinLossNoTvcAoACap > 0f && yawHealth < 0.999f && (noTvcOn || tvcy == null || !tvcy.active))
			{
				float kF2 = Mathf.Clamp01((1f - yawHealth) / 0.5f);
				tgt = Mathf.Min(tgt, Mathf.Lerp(Plugin.AoAPos, Plugin.FinLossNoTvcAoACap, kF2));
			}
			// 2.0.3: when the nozzles can no longer cover the nose-down need (a cap exists at all), the limit is a fixed
			// AuthCapFixedAoA instead of a value sliding with IAS, as long as the authority sweep allows it there;
			// below that the swept cap still rules. Engages when a cap appears below 62 deg, releases above 64.5.
			if (AuthCapFixed)
			{
				// nozzles short of nose-down authority (decided by the thrust you have, so a pull at idle from high IAS is
				// limited from the start instead of the cap coming down onto the jet as the speed bleeds), or a swept cap
				// 2.0.5: judged by thrust-to-weight (Tom: floor T/W 0.3), release 0.03 above
				bool tvShort = authFixedOn ? (tw < Plugin.LowThrustTW + 0.03f) : (tw < Plugin.LowThrustTW);
				// 2.0.8: with the nozzles locked (DisableTVC) they never cover pitch
				tvShort |= Plugin.DisableTVC || ZeroThrustPlanned();
				authFixedOn = tvShort || (authFixedOn ? (authCap < Plugin.AoAPos - 0.5f) : (authCap < Plugin.AoAPos - 3f));
				// Ternion 0.1.1: with no nozzle pitch at all, T/W (vectorable) is always 0, so this fixed NoTvcAoACapPositive (45) capped
				// every limit above it, MPO included (Tom, 7 Nov: AoALimitPositive 50 flew to 45 and no more). The swept cap alone applies.
				if (noVecJet && NoVecUncapped)
				{
					authFixedOn = false;
				}
				// 2.0.8: AoALimit Full65 (debug) turns the fixed 36 deg rule off; the swept authority cap still applies
				if (Plugin.AoAFull65)
				{
					authFixedOn = false;
				}
				if (authFixedOn)
				{
					// 2.0.9: the fixed value is the debug NoTvcAoACapPositive (36 by default)
					tgt = Mathf.Min(tgt, NoTvcCapPosNow());
				}
			}
			if (!authInit)
			{
				authCapEff = tgt;
				authInit = true;
				return;
			}
			if (tgt < authCapEff)
			{
				authCapEff += (tgt - authCapEff) * Mathf.Clamp01(dt / AuthCapTauDown);
				return;
			}
			// 1.9.1: rising, the cap is also rate-limited, and nearly frozen while the jet sits at or above it. In 1.9.0 the
			// exponential rise (up to ~10 deg/s just after a drop) let the capture re-accelerate the nose into the still-
			// rising cap and the recovery push it back: a 1 Hz, 2-18 deg/s pitch-rate cycle at 55-60 deg AoA, half thrust.
			authFastT = Mathf.Max(0f, authFastT - dt);
			if (authFastT > 0f)
			{
				// 1.9.5: the rise comes from more thrust (the nozzles' nose-down authority grew at the last sweeps), not from
				// the airframe - follow it as fast as a drop. 1.9.1-1.9.4 held it to 1 deg/s while the jet sat at the cap, so
				// idle -> max AB with the stick held took ~40 s to give back the full AoA.
				authCapEff += (tgt - authCapEff) * Mathf.Clamp01(dt / AuthCapTauDown);
				return;
			}
			float riseMax = ((alpha > authCapEff - 1f) ? AuthCapRiseAtCap : AuthCapRise) * dt;
			authCapEff += Mathf.Min((tgt - authCapEff) * Mathf.Clamp01(dt / AuthCapTauUp), riseMax);
		}

		private void FuelTrimStep(float dt, Quaternion rootRot)
		{
			if (!Plugin.FuelTrim || !Plugin.FuelBalanceOn || !asymValid || wheelsTouch || Mode == FcsMode.Ground || (UnityEngine.Object)(object)ac == null)
			{
				fuelTrimOn = false;
				return;
			}
			float cr = Mathf.Max(asymCrNow, 1f);
			float cy = Mathf.Max(asymCyNow, 1f);
			float ur = Mathf.Abs(asymArNow) / cr;
			float uy = Mathf.Abs(asymAyNow) / cy;
			float uMax = Mathf.Max(ur, uy);
			fuelTrimCalm = (uMax < FuelTrimStop) ? (fuelTrimCalm + dt) : 0f;
			if (fuelTrimOn && fuelTrimCalm > 2f)
			{
				fuelTrimOn = false;
			}
			fuelTrimTimer -= dt;
			if (fuelTrimTimer > 0f || (!fuelTrimOn && uMax < FuelTrimStart))
			{
				return;
			}
			fuelTrimTimer = 0.5f;
			Transform tr = ((Component)(object)ac).transform;
			float m = Mathf.Max(1000f, mass);
			Vector3 fT = tr.forward * (tw * m * 9.81f);
			Vector3 fL = tr.up * (Mathf.Max(nz, 1.5f) * m * 9.81f);
			Quaternion inv = Quaternion.Inverse(rootRot);
			Vector3 w = inv * (-Vector3.Cross(tr.right, fT + fL));
			Vector3 dM = new Vector3(0f - w.z, 0f - w.x, w.y);
			float gR = AccRoll(dM);
			float gY = AccYaw(dM);
			float best = 0f;
			float bestU = uMax;
			for (int i = -30; i <= 30; i++)
			{
				float d = FuelTrimMaxOffset * i / 30f;
				float u = Mathf.Max(Mathf.Abs(asymArNow + gR * d) / cr, Mathf.Abs(asymAyNow + gY * d) / cy);
				if (u < bestU - 1e-4f)
				{
					bestU = u;
					best = d;
				}
			}
			if (!fuelTrimOn && uMax - bestU < 0.1f)
			{
				return;
			}
			float latNow = FuelBalance.LateralCg(ac);
			fuelTrimDelta = best;
			fuelTrimLat = latNow + best;
			fuelTrimOn = true;
		}

		private void DamageGateStep(float dt)
		{
			int nAll = 0;
			int nOn = 0;
			foreach (SurfaceEffector sG2 in surfaces)
			{
				nAll++;
				if (sG2.active)
				{
					nOn++;
				}
			}
			bool on = nOn < nAll && asymValid && asymCapPosEff < Plugin.AoAPos - 0.5f && (tvc == null || tvc.max <= 0.5f || Plugin.DisableTVC);
			dmgGateHold = on ? 1f : Mathf.Max(0f, dmgGateHold - dt);
		}
	}
}
