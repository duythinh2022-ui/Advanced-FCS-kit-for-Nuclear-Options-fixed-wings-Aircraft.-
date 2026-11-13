using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	internal sealed class FcsController
	{
		public readonly Aircraft ac;

		public readonly ControlInputs inputs;

		public readonly List<SurfaceEffector> surfaces = new List<SurfaceEffector>();

		public readonly TvcEffector tvc;

		public readonly FlapSystem flaps;

		private readonly List<Effector> effs = new List<Effector>();

		private readonly Allocator alloc = new Allocator();

		private readonly List<LandingGear> gears = new List<LandingGear>();

		private readonly List<Rigidbody> bodies = new List<Rigidbody>();

		private readonly List<AeroPart> aeroParts = new List<AeroPart>();

		private readonly Dictionary<AeroPart, AirfoilTable> foils = new Dictionary<AeroPart, AirfoilTable>();

		public bool Engaged;

		public bool Failed;

		public FcsMode Mode;

		private float mass = 20000f;

		private Vector3 cg;
		// 2.30.0: the air data at the aircraft CG. ac.rb is the ROOT part's rigidbody, whose CoM sits 2.48 m ahead of the CG
		// (bench leaf calibration, 3 Nov): its velocity carries omega x 2.48 m, so the AoA, sideslip, TAS and flight-path rate the
		// laws used read q*2.48/V of extra AoA and r*2.48/V of extra sideslip (1-2 deg in a 60-75 deg/s rotation at 130-200 kt).
		// vCg = the mass-weighted velocity of every body, the same weighting as cg. nz stays the root body's (unchanged).
		private Vector3 vCg;
		private Vector3 vCgPrev;
		private Lpf3 accCgF;
		internal static bool AirDataAtCg = true;   // false = 2.27.0 (air data from the root rigidbody)
		internal static bool FlightPathAtCg = true;   // the flight-path rate / speed rate (omegaV, vDot) from the CG's acceleration too
		public float alphaRoot, betaRoot;          // the root body's AoA / sideslip, telemetry

		private readonly float[,] Ia = new float[3, 3];

		private readonly float[,] IaInv = new float[3, 3];

		private float massTimer = 99f;

		private float nAlphaTimer = 99f;

		private Vector3 omega;

		private Vector3 omegaPrev;

		private Lpf3 omegaDotF;

		private Lpf3 mNowF;

		private Lpf3 accF;

		private Vector3 velPrev;

		private bool havePrev;

		private Lpf alphaDotF;

		private Lpf aUpF;

		private Lpf aDnF;

		private Lpf qTdotF;

		private float alphaPrev;

		public float alphaU;

		public bool protHold;

		private float protHoldSide;

		public float alpha;

		public float beta;

		public float tas;

		public float ias;

		public float nz;

		public float omegaV;

		public float eLy;

		public float phi;

		public float theta;

		public float alphaDot;

		public float nAlpha = 0.1f;

		public float tw;

		public float nMaxAero = 9f;

		// 1.9.0: wing lift-to-weight ratio at the lift peak, thrust not counted
		public float nLiftMax = 9f;

		// 1.9.0 command shaping (AoA/G pre-filter)
		public float alphaCmdRaw;

		public float nCmdRaw = 1f;

		private float aRefV;

		private float nRefV;

		private bool shapeInit;

		// 1.9.0 AoA -> pitch-rate hand-over
		public float rateBlendAoA = 36f;

		// 1.9.7 negative side (magnitude), which side the rate command / floating hold belongs to, wing negative-lift capacity
		public float rateBlendAoANeg = 36f;

		private int hiSide = 1;

		private int floatSide = 1;

		public float nLiftMin = 9f;

		private bool rbInit;

		public bool hiRate;

		public float rateW;

		public float zoneX;

		private bool hiAnchor;

		public bool floatOn;

		public float floatS;

		internal static float FloatKnee = 2f;

		private float hiAS;

		private float hiAQ;

		// 1.9.0 low-thrust (nose-down authority) AoA cap
		public float authCap = 999f;

		public float authCapEff = 999f;

		// 2.0.9: negative AoA limit while the nozzles do not cover pitch (DisableTVC or low thrust), and the test-switch state
		public float authCapNegEff = -999f;

		private bool noTvcOn;

		private string testSwitchKey;

		private bool authInit;

		internal static float AuthCapLead = 1f;

		internal static float ReleaseTauG = 0.3f;

		internal static float AuthCapTauUp = 1f;

		internal static float AuthCapTauDown = 0.3f;

		internal static float AuthCapRise = 4f;

		internal static float AuthCapRiseAtCap = 1f;

		internal static float AuthCapThrustStep = 1f;   // deg/s^2 of nozzle nose-down authority gained between sweeps

		internal static float AuthCapFastHold = 1f;     // s the fast rise stays open after the last such gain

		private float authTvPrev;

		private float authFastT;

		public float alphaAtNMax = 30f;

		public float gAvail = 9f;

		public float gInt;

		public float vDot;

		public float alphaDotHoldG;

		private Lpf vDotF;

		public float aUp = 60f;

		public float aDn = 60f;

		internal static float RollLoadLead = 0.2f;

		// 2.1.10: allocator yaw weight above ~40 deg AoA while sideslip builds (YawAxisPriority below 20 deg, blended between).
		// The sideslip rate is p sin(AoA) - r cos(AoA): at 45 deg a body-roll error counts as much as a yaw-rate error, but
		// the allocator weighted yaw 8 : roll 0.35, so it let the stabs' yaw (their roll side-effect uncancelled, flaperons at
		// their stops) run the body roll 30 deg/s past its command (13 Oct log, 45 deg limit: sideslip 20-28 deg in rolls
		// and reversals at 70-100 m/s). With |sideslip + 0.3 s lead| over 2 deg the yaw weight comes down toward this value
		// (full at 8 deg); <= 0 = 2.1.9 (flat YawAxisPriority).
		internal static float YawPrioHi = 2f;

		// sideslip gate for YawPrioHi: 0 below YawHiSlip0 of |sideslip + lead|, 1 at YawHiSlip0 + YawHiSlipSpan (0 span = always 1)
		internal static float YawHiSlip0 = 2f;

		internal static float YawHiSlipSpan = 6f;

		internal static float YawHiSlipLead = 0.3f;

		internal static float YawHiSat0 = 0.6f;

		internal static float YawHiSatSpan = 0f;


		private float YawHiSlipGate()
		{
			if (YawHiSlipSpan <= 0f)
			{
				return 1f;
			}
			float bl = beta + slipRateDbg * YawHiSlipLead;
			float g = Mathf.Clamp01((Mathf.Abs(bl) - YawHiSlip0) / YawHiSlipSpan);
			if (YawHiSatSpan > 0f)
			{
				g *= 1f - Mathf.Clamp01((yawSat - YawHiSat0) / YawHiSatSpan);
			}
			return g;
		}

		internal static float RollStickRate = 10f;

		internal static float PreLag = 0.01f;

		internal static bool LegacyLag;

		internal static float RollJerkServo = 1f;

		internal static float RollJerkTau = 0.18f;

		internal static bool BrakeUseAll = true;

		internal static float RollEndJerk = 0.35f;



		// 2.1.0 sideslip-rate lead for the lateral guard (see LateralLaw)
		internal static float SlipLeadT = 0.8f;

		internal static float SlipRateTau = 0.05f;

		internal static float GuardCutTau = 0.08f;

		internal static float SlipGovStart = 4f;

		internal static float SlipGovSpan = 10f;

		public float slipGovDbg = 1f;

		public float rateCmdG;

		private float stickLagF;

		public bool capOn;

		private float capSp;

		private float capQ;

		private float capSpPrev;

		private float capSteadyT;

		internal static float CapStickRate = 0.15f;

		internal static float CapSteadyTime = 0.15f;

		internal static float CapAoABand = 0.5f;

		internal static float CapAlphaDot = 2f;

		internal static float CapStickBand = 0.02f;

		private bool leadInit;

		public float stickLeadDbg;

		public bool latRevDbg;

		private float latRevSign;

		internal static float NegGateA0 = 0f;

		internal static bool LowCapLatFix = true;

		internal static bool AsymSharedLat = true;

		public float sharedLatDbg = 1f;

		internal static float LoadRevYawK = 0.4f;

		public float loadRevDbg;

		internal static float LowCapCoupBand = 4f;

		internal static bool CoupGateAtLimit = true;

		internal static float CoupLimBand = 4f;

		internal static float CoupGateRelease = 0.6f;

		internal static float CoupGateLoaded = 0f;

		private float coupGateHeld;

		private bool LowThrustCapOn => authFixedOn || authCapEff < Plugin.AoAPos - 0.5f;

		internal static float NegGateSpan = 3f;

		public float negGateDbg;

		internal static float YawAnchorK = 0.5f;

		internal static float YawAnchorT = 0.25f;

		internal static float YawAnchorFloor = 10f;

		internal static bool YawAnchorGrav = true;

		public float yawAnchorDbg = 999f;

		private Lpf bdkF;

		public float slipRateDbg;

		// 2.23.0: stays 0.5. The sweep cap is the kinematic demand (a coordinated rotation pS needs pS cos AoA of body yaw per unit
		// AoA, see LateralLaw) against half the yaw capacity; it is the binder of every released push-over from 65 deg once the AoA
		// falls through ~55 deg (3 Nov 11:19 log, 47.5-48.1 s: 15-21 deg/s of AoA rate allowed against 20-23 of flight-path rotation
		// = a pitch command of +4..+9). 1 removes that and doubles the bench's bottom sideslip (clean released push 600: 9.2 -> 21.3;
		// 0.65: 13.3): the larger pitch demand takes the stabs as they return to pitch and the lateral axes lose the coordination.
		internal static float SweepYawK = 0.7f;   // 2.27.0: 0.5 -> 0.7. Since 2.26.0 the kinematic cap acts on the HELD push only (released: scaled out, the follow pace paces). Tom (14:28): "for the held push-over the pitch-down is too slow, the sideslip is being kept too tight" - 14:23 log held pushes 135-201 kt: sideslip <= 2 deg of the 4 deg margin, AoA rate 15-20 deg/s against +12..+23 of flight-path rotation. Bench (its held pushes do not reach this cap): one-fin held 560 5.7 -> 9.3, push+roll 300 2.7 -> 3.2, pull-then-pushover 350 4.2 -> 4.8, push-then-pullthrough 450 7.5 -> 8.3; the 2.23.0 bisect at 1.0 doubled the released push's sideslip when this cap still paced it.
		// 2.24.0: a cap on the sweep toward zero AoA may hold the nose, never turn it back: the pitch-rate command it leaves is bounded
		// at zero (the AoA still falls at the flight-path rate). Where the deficit is real (11:19 log, 223-244 kt with 70-80 deg/s on,
		// sideslip 5-6 deg and climbing) the honest AoA-rate cap of 14-19 deg/s sat under +20..+24 of flight-path rotation = a nose-UP
		// command of +5..+18 with the stick full forward - Tom's hitch, whatever the pace behind it.
		internal static bool SweepNoReverse = true;
		internal static float SweepNoReverseStick = 0.2f;   // lateral stick fraction over which 'released' fades to 'held' (the floor and the delivered term are released-push devices; the held push keeps 2.10.0's accepted pace)
		// 2.25.0: the floor on the held push too (Tom, 3 Nov 13:17: "sometimes it steps, sometimes it goes all the way to the hard stop then
		// goes again, for both the released push-over and the held push-over" - 13:12 log, 122.0 s held push: qCmd -25 -> -13 -> 0 -> +7 ->
		// -9 -> -36, body pitch rate +4 with the stick full forward). A positive pitch command with the stick forward is a hitch whatever
		// the rotation behind it; what the held push loses in the bottom sideslip the shed cap's pace has to buy back.
		internal static bool SweepNoReverseHeld = true;
		// 2.34.0 (the push governor): the push side of the reference-capacity governor replaces the 2.5.8-2.27.0 push-over stack (the kinematic
		// sweep cap and its delivered term, the shed cap and its follow pace, the crossing lead, the cap filters and slews, the released
		// follow latch, its roll target and its yaw brake). Tom: "it limits the lateral rate when only the yaw is the bottleneck and the
		// pitch rate when both yaw and roll are". A sweep toward zero AoA under a velocity roll pS drives the stability-axis yaw rate
		// rS = r cos a - p sin a at pS x AoA-rate; the yaw (through cos a) and the roll (through sin a) turn it back. The yaw sheds at
		// most capY: the velocity roll may come down no faster than (pS cos a AoA-rate - capY) / sin a (and must GROW when that is
		// positive: the roll follows the yaw) - the lateral limit. Where the roll cannot grow that fast either (capX), the AoA rate is
		// limited, and over the whole sweep the AoA may come no lower than atan(yaw left / body-roll ceiling) (released: 1.5 x the body
		// roll at the push; held: the stick's target), closed on at PushGovDecel. Capacities: one 0.2 s filter, TradeCapShare of them for
		// the pitch, all of the yaw for the lateral edge. Bench record: claude/Pushover_governor_2.34_dev_findings.md
		// 2.35.3: the stack it replaced (and the PushGov switch back to it) is deleted; it had not run since 2.34.0.
		internal static float PushGovCapTau = 0.2f;   // s: the governor's one capacity filter (the pace is one curve, not the estimator's tick-to-tick read - 2.25.0)
		internal static float PushGovTau = 0.5f;      // s: the pace closes on the yaw-limited AoA over this time (when PushGovDecel = 0)
		internal static bool PushGovNoStab = true;   // 2.34.1: plan the push on the yaw capacity without the stabs (see LateralLaw)
		internal static float PushGovSlipK = 0.5f;    // x the PushoverSideslip allowance the pace may leave to the bottom
		internal static float PushGovDecel = 40f;     // deg/s^2: the pace closes on a_min at this deceleration (sqrt(2 D gap)); 0 = over PushGovTau
		internal static float PushGovRollK = 1.5f;    // the body roll may rise to this x the body roll it had when the sweep began
		// 2.35.0 (PushPlan, Tom 22:19: "velocity vector roll bleeds while the pitch is smoothly slowed down for the push over"). The push
		// is PLANNED once, when it starts, not re-paced every tick from the capacity read (2.34.0/2.34.1: a pace that follows a read
		// swinging +-30 % in 0.3 s, sitting at the flight path's own 15-20 deg/s of rotation, hesitated). At the push: the yaw on board
		// (less what PushPlanSlip deg of sideslip absorbs at the bottom) over the yaw the push will shed - from the logs, 77 push-overs
		// 14:45-22:06, the mean shed from the push to 10 deg AoA was 28.6e-4 x IAS^2 (r 0.81 with IAS; the capacity reads 0.53-0.64)
		// - gives the push time T; the body pitch rate is set to bring the AoA to 10 deg in T (less the flight path's rotation), ramped
		// in at PushPlanJerk and then held; it may rise (never fall) at PushPlanRise when the yaw goes faster than planned. With the
		// lateral stick released the velocity-vector roll bleeds to zero over the same T (not the stop law's brake). One number to tune
		// the trade: PushPlanK (higher = faster push, more sideslip at the bottom).
		internal static bool PushPlan = true;
		internal static float PushPlanK = 0.028f;     // deg/s^2 of yaw shed per (IAS m/s)^PushPlanExp (the logs' median; 2.35.0: 0.0029 with IAS^2)
		internal static float PushPlanExp = 1.5f;       // 2.35.2: the logs' shed goes as IAS^1.58 (93 push-overs to 22:52); IAS^2 read 22 % low under 70 m/s and 13 % high over 110
		internal static float PushPlanSlip = 4f;      // deg of sideslip the yaw left at the bottom may make (2.35.2: 2 -> 4, the plan left 2.3 deg median at low IAS against 2.31.2's 4.3)
		internal static float PushPlanA0 = 20f;       // deg: the push is planned only from above this AoA
		internal static float PushPlanQMin = 8f;      // deg/s: the nose always visibly moves
		internal static float PushPlanJerk = 120f;    // deg/s^2: the planned rate is ramped in at this
		// 3.1.8: 15 -> 40 deg/s^2 (Tom 7 Nov 01:27, 01:23 log: "still a little bumpy especially with the first few push overs" - 43.9 / 68.6 s: the plan
		// jumped to its rate, then crept up at 15 deg/s^2 as the yaw shed faster than its model, so the nose went 85 deg/s^2 -> 15 and the nozzles
		// -18 -> -6 -> -14). At 40 the rise lands inside the onset. Bench: those two pushes 5 -> 3 and 3 -> 1 nozzle reversals; the 87 logged push-overs'
		// sideslip +1 deg or more in 15 (largest 2.2 -> 12.9), worst 24.4 -> 25.1 (Tom 00:51: "some more sideslip is ok")
		internal static float PushPlanRise = 40f;
		// 3.1.4 (Tom 6 Nov 22:40/22:47): a minimum push-over rate - the plan's rate is floored at Plugin.PushOverMinRate, so a deep-stall,
		// very low airspeed push (22:22 log, 457.4 s: 92 kt, 64 deg, r -30, the plan alone held 9.1 deg/s for 1.3 s) still brings the nose down;
		// a plan at or above the floor is untouched. 3.1.5 (23:29: "no guard, just a plan floor of 23deg/s"): 23 deg/s and no sideslip guard
		// (3.1.4's fade from 12 to 15 deg of predicted sideslip took the floor away near the bottom of the 23:24 push: -20 -> -14.5 deg/s at 21-10 deg AoA).
		internal static float PushPlanTMin = 0.6f;
		internal static float PushPlanR0 = 15f;       // deg/s of body yaw on board at the push for it to be planned
		internal static float PushPlanWvK = 1f;       // the flight path's rotation at the push counted against the planned AoA rate
		internal static float PushPlanRelease = 60f;  // deg/s^2: the bound ramps out below 10 deg AoA
		internal static bool PushPlanOwnsPitch = true;    // 2.36.0: under a plan the sweep caps (the 60 deg/s AoA-rate ceiling) stand aside; with PushPlanClipKey the allocator's pitch priority and the stabs' window read the plan's bound (2.35.1's try without ClipKey: 4.8 -> 11.2)
		internal static bool PushPlanWinOff = true;
		internal static bool PushPlanClipKey = true;      // 2.36.0: pitchNeedDn / sweepHoldWin read the plan's bound (see the pitch law)
		// 2.40.0 (PushPlanHeld; 2.36.0's held-stick rule): a planned push owns the held lateral stick too, in place of the push governor's
		// lateral band. The band paced the roll toward the stick's target by L = (pS cos a a' - capY) / sin a with capY the measured yaw
		// capacity read, which swings +-30 % in 0.3 s (the 2.34.1 noise). Under a plan the same rule reads the plan's own yaw budget D,
		// fixed at the push's start - D = max(capPlan, (Y0 - Yend) / T): the coordinated yaw of the rotation, Y = pS sin a, drained to its
		// end (the held roll's coordinated yaw at 10 deg AoA) over the planned push time - and this tick's commanded AoA rate a'.
		// Released stick: unchanged (the plan's linear bleed). 2.36.0 flown 09:03: "held push over is smooth".
		internal static bool PushPlanHeld = true;
		// 2.41.0 (PushPlanReachTau): through a planned push the allocator places the nozzles and wing surfaces against where each stab will
		// be within this many seconds (its servo's reach toward its command), not against the command (Allocator.ReachPass). 11:01 log
		// (2.40.0), 34.1 s: the window sent the left stab -25 -> +35 deg (0.45 s, through its lift peak); the allocator, seeing the stab
		// already at +35 (nose-up), drove the nozzles -4 -> -9 deg nose-down while the stab gave its peak nose-down on the way: -34 deg/s
		// flown against -26.8 asked. Hybrid replays, 59 released pushes: overshoot 6.8 -> 2.8 deg/s median, the mid-push step 3.9 -> 1.3,
		// the body-roll rise 9.3 -> 4.8; sideslip 5.0 -> 7.4 median (the stab's transit roll pulse had been holding it down). 0 = off.
		internal static float PushPlanReachTau = 0.1f;
		// 2.44.0 (StickCancelsLimit, Tom 4 Nov 20:26): "if i hold stick all the way back at max aoa, and the jet still can do positive pitch
		// rate, then nothing happens; as soon as the only pitch rate that can hold that aoa is 0, that is the point the stick can cancel the
		// protection". At the AoA limit the nose-down part of the limit's own command (the capture bound below the limit, protection
		// above it) scales with 1 - stick / 0.9, as protection's push always did; a positive holding rate is never scaled. Before, two
		// rules disagreed at the limit: below limit + 0.5 the aft-stick floor held the command at 0, above it 1.9.5's 'pilot command'
		// (the stick clamped by the capture bound) brought the bound's nose-down back through full aft. Slowing at 22-50 IAS with the
		// flight path falling 1-8 deg/s (20:07 log, 143-154 s, 173-182 s) the command cycled 0 <-> -11..-13 deg/s at ~2 Hz, AoA
		// 64.5-66.6, nozzles -13 <-> +16 deg. Now full aft holds zero rate there and the AoA rises with the falling flight path (bench,
		// those segments: 112 / 85 deg at their ends); release and protection pushes. 1.9.5's rule keeps its purpose: a stick asking for
		// more nose-down than protection (forward stick above the limit) still gets it.
		internal static bool StickCancelsLimit = true;
		private bool capAtLimU, capAtLimD;

		internal static float SweepPs0 = 10f;


		internal static float SweepFloor = 6f;
		// 3.1.6 (Tom 6 Nov 23:44: "instant pitch response from stick input, an actual smooth pitch rate curve, no spike no hitch no sudden
		// slow down, no chatter, no hesitation"). The push governor's toward-zero sweep cap was switched on at a velocity roll of 5 deg/s, all at
		// once, with the 60 deg/s AoA-rate ceiling (StopSlipPitchFree) as its open value and a 6 deg/s floor:
		//  - 23:36 log 39.95 s (78 kt, roll 4-7 deg/s): on/off/on/off mid-push, command -43 -> -60 -> 0 -> -60, nozzles -14 -> +20 -> -20;
		//  - 45.07 s pull from -37 deg (63 kt): command held at 0 for 0.8 s with the nose at -5..-3 deg;
		//  - 52.55 s (72 kt, roll 10-16 deg/s): the push held to 6-23 deg/s of AoA rate for 2.4 s;
		//  - 23:24 log 54.37 s (73 kt): the 60 deg/s ceiling, taken against the flight path's own turn, cut the stick's -60 to -46, fading to -22.
		// Now the toward-zero cap fades in with the velocity roll from PushGovFadeLo to PushGovFadeHi deg/s (open = PushGovOpenRate, not the
		// 60 deg/s ceiling), and it never holds the nose's own pitch rate under the push-over minimum rate (Plugin.PushOverMinRate).
		internal static bool PushGovFade = true;
		internal static float PushGovFadeLo = 5f;
		internal static float PushGovFadeHi = 15f;
		internal static float PushGovOpenRate = 120f;
		internal static bool SweepFloorPushMin = true;
		// 3.1.7 (Tom 7 Nov 00:27: "smooth pitch curve, instant response, zero hesitation, zero chatter"; bench only). Three hand-offs in the
		// sweep caps made the pitch curve notch or the command spike:
		//  - PushGovPaceYaw: the governor's toward-zero pace closes on a_min = atan(r_ex / pCeil) at PushGovDecel and its shed time runs to zero
		//    at zero AoA, even with no yaw over the allowance (r_ex 0: a_min 0, nothing to shed). Pulling from -48 deg under a 10-20 deg/s roll
		//    the pace slowed the nose from 40 to 23 deg/s toward zero AoA, then let go at the crossing (bench low set, pull_0.6_x_0.5: q 40 -> 25
		//    -> 40). The pace now fades in with the excess yaw, open at r_ex 0, whole at PushGovPaceR deg/s.
		//  - PushGovFadeGrow: 3.1.6 faded the toward-zero cap in with the velocity roll; the grow side past zero AoA (capPullG's alpha < 0 term,
		//    floored at SweepFloor 6) still came in whole at a roll of 5 deg/s: a push through zero with the roll dying clipped the command
		//    -53 -> -24 for one tick. It fades with the same weight now.
		//  - PushPlanDoneOpen: a plan that ramped out at the bottom (10 deg) handed the last 10 deg to the governor's caps, which came back at
		//    their floor: -50 -> -23 for a tick, then back. The push the plan finished stays open until the AoA changes side or the stick lets go.
		internal static bool PushGovPaceYaw = true;
		internal static float PushGovPaceR = 3f;
		internal static bool PushGovFadeGrow = true;
		// the far side (growing |AoA| past zero under a push) fades in over a larger velocity roll: there the yaw demand is the roll still held
		// (pS sin a), not yaw left over - at 12 deg/s it is 8 deg/s at -40 deg and the bench sideslip stayed 2 deg with no cap (00:23 log 98.3 s);
		// at 55 deg/s (reg, pull-then-pushover + full roll, 350 km/h) the cap is what keeps it from 26 deg (Tom 7 Nov 00:51: "some more sideslip is ok")
		internal static float PushGovFarLo = 20f;
		internal static float PushGovFarHi = 40f;
		internal static bool PushPlanDoneOpen = true;
		internal static bool SweepSideKept = true;
		internal static bool SweepZeroBlend = true;
		// 3.1.7 (PushNoRetreat; Tom 7 Nov 00:51: "if it is real excess yaw, then just go, I'd rather have the pitch down than basically almost stay
		// frozen for a few sec for that yaw to catch up, that is what the floor meant for, let the yaw/roll do its best during that pitch down,
		// some more sideslip is ok"): the sweep caps may hold back how fast a held push (or a pull from negative AoA toward zero) builds, never
		// take back the rate it has reached - the nose-down rate the trajectory has got to stands as the bound until the stick lets go (fins healthy)
		internal static bool PushNoRetreat = true;
		internal static bool PushPlanThrough = true;
		internal static float PushPlanReleaseSoft = 30f;
		// 3.1.8 (PushPlanRamp; Tom 7 Nov 01:27, 01:23 log: "best push over performance so far, but still a little bumpy especially with the first few
		// push overs"): the plan was a rate - jumped to at 120 deg/s^2, crept up at 15 deg/s^2 as the yaw shed faster than planned, held, then
		// ramped out at the bottom - so the nose went 85 deg/s^2 -> 15 -> 0 -> 30 (43.9 s: q -34 / -42 / -42 / -60, nozzles -18 -> -6 -> -14). Now
		// the plan is a ramp: from the push-over floor (or what the shed time needs) the planned rate rises at a constant acceleration sized so the
		// AoA reaches 10 deg when the yaw has shed, and that acceleration only grows (smoothly, PushPlanAJerk) if the yaw sheds faster; it carries
		// on through the bottom (at least PushPlanReleaseSoft) to the stick's 60. One rising curve, no hold.
		internal static bool PushPlanRamp = false;   // (benched off: the 87 logged push-overs' sideslip 24.4 -> 35.8 max - the ramp's rise outran the yaw)
		// 3.1.8 (PitchTrajLead): the pitch-rate trajectory captured the command as if it were standing still. When the command itself was moving -
		// the plan creeping up at 15 deg/s^2, an AoA-limit bound coming in - the trajectory braked to zero acceleration on it and then had to
		// accelerate again: 01:23 log 43.9 s, nozzles -18 -> -6 -> -14 and the rate kinked (Tom 01:27: "still a little bumpy especially with the first
		// few push overs"). The command's own slope (low-passed over PitchTrajLeadTau, steps left out) is now what the capture lands on.
		internal static bool PitchTrajLead = false;   // (benched off: nozzle reversals 34 -> 42 on the 01:23 replays)
		internal static float PitchTrajLeadTau = 0.08f;
		internal static float PitchTrajLeadStep = 2f;   // deg/s in one tick: a step, not a slope
		internal static float PushPlanRampMax = 40f;    // deg/s^2: the ramp's acceleration when the plan starts (above it the start rate rises instead)
		internal static float PushPlanRampHi = 80f;     // deg/s^2: as far as a faster shed may raise it
		internal static float PushPlanAJerk = 120f;     // deg/s^3: how fast the ramp's acceleration may rise
		internal static float SweepZeroDecel = 60f;
		// the pitch-rate trajectory: the share of the available pitch acceleration it plans with, and its jerk time (bench switches, 3.1.7)
		internal static float PitchTrajAccK = 0.7f;
		internal static float PitchTrajJerkT = 0.15f;





		internal static bool SweepPosPullFree = true;



		internal static float SweepPosK = 0.25f;

		internal static float SlipGovPs0 = 10f;





		internal static float PullRollTradeFinLoss = 0.7f;









		public float decelDbg;

		private float decelF;

		private float decelYieldF;

		internal static float DecelYieldFall = 1f;

		internal static float DecelFightRelax = 1f;

		internal static float DecelStabRelax = 0f;




		internal static float DecelRise = 3f;

		internal static float DecelFall = 1.5f;

		private float pullGovF = 1f;

		// 2.2.24 adaptive feed-forward in the rolling-pull governor
		private float pullFFBoost = 1f;

		public float pullServoDbg;

		private float pullServoEx;






		public float pullServoWDbg;







		internal static float RelLatRollPrio = 3f;

		public float relLatPrioDbg = 1f;






		public float pullXsDbg;

		public float pullLeadDbg;





		internal static float PullFFBoostIdle = 2f;




		// 2.2.8 governor state
		internal static float PullGovIdleRise = 4f;

		internal static float PullTradeFloor = 30f;

		// 2.4.17 roll-stop sideslip governor (see LateralLaw)
		internal static float StopSlipYawUse = 1f;



		internal static float YawSlewLeadK = 1f;

		public float yawLeadDbg;

		private float rCmdRateF;



		private bool rCmdLeadInit;

		// 2.5.0 reference governor on the commanded roll rate (see LateralLaw). The stop / reversal sideslip model fitted
		// on the 30 Oct flight logs (200 stops and reversals, 3-40 deg AoA): peak sideslip after the stop = StopSlipBase +
		// |pS| sin(AoA) cos(AoA) x (StopSlipTau0 + StopSlipTau1 x rudder load above StopSlipKnee), with the rudder load the share
		// of the rudders' yaw on the roll's side (StopSlipHoldTau filter).
		internal static float StopSlipBase = 2.0f;

		internal static float StopSlipTau0 = 0.054f;

		internal static float StopSlipTau1 = 0.22f;

		internal static float StopSlipKnee = 0.7f;

		// 2.5.2 yaw-reserve governor: rudder load (share of their yaw on the roll's side) above which the roll reference is
		// trimmed, and below which it is released; the release rises at StopSlipReserveRiseK x the fall rate
		internal static float StopSlipLoad = 0.8f;


		internal static float StopSlipReserveRiseK = 0.5f;


		// 2.21.0: the AoA-rise cap (StopSlipPitchK / Start / Lead / Close, StopSlipPinAB0 / 1) is deleted - see LateralLaw. The
		// floor stays as the trade's pitch-share floor (deg/s of AoA rate)
		internal static float StopSlipPitchFloor = 6f;

		private float paceRDotLast;

		// 2.6.4: the trade's split. PullRollTrade is the share of a rolling pull's yaw shortfall the roll gives up (the roll limit
		// goes that far from pSmax toward the trade's target); the pitch gives up the rest - the AoA-rate ask is scaled so that the
		// pair's sweep demand (pS cos AoA x AoA rate) fits the share: with the roll at R_r of pSmax and the demand needing R of it,
		// the AoA rate is held to R / R_r of the ask. 1 = all roll (2.5.1-2.6.3: the remainder was simply uncovered - at 170-210 kt
		// the roll sat at the 30-35 deg/s floor with the pitch at full rate, Tom: "the slower, the more pitch and less roll; not the
		// way the old governor divided pitch and roll with the 0.7 trade"; 22:17 log); 0 = all pitch; your 0.7 = 70 / 30.
		private float pullTradeAsk;
		internal float tradeAlphaRate = -1f;

		// 2.8.0: the trade from the measured capacity (CapacityEstimator) against the roll the stick asks, not the roll limit
		// (15:02 log, 210.3 / 215.0 / 224.1 s: stick released, roll braking to zero, yaw idle, and the pitch still held to 30 deg/s
		// for ~0.35 s while the slewed roll limit climbed back - the re-pull hitch). 2.10.0: the 2.7.x arithmetic is deleted.

		// share of the measured yaw capacity a rolling sweep may spend before the trade cuts in
		internal static float TradeCapShare = 0.7f;

		// the AoA-rate cap the measured trade asks of the pitch this tick (-1: none), and the roll the stick asks before any cap
		internal float tradeAlphaNew = -1f;

		internal float pSAskStick;
		public float tradeAlphaDbg;


		internal static float StopSlipPitchFree = 60f;

		private float stopSlipAbsF;

		internal float yawSatAlphaRate = -1f;

		// 2.5.5: the lateral governors (margin cap, yaw reserve, pace, pitch caps, trade, no-chase) act in the AoA/G and rate laws
		// and, with the gear down, in the air (no wheel on the ground; GearDownSlipProtect as for the 2.4.13 limiters). 10:10 log:
		// 51 s gear-down airborne, 20.9 deg of sideslip, every governor column idle.
		private bool LatGovMode => Mode == FcsMode.AoAG || Mode == FcsMode.Rate || (Mode == FcsMode.GearDownRate && Plugin.GearDownSlipProtect && !gsAnyContact);

		private float yawSatCapQ = 120f;

		private float yawSatCapQLo = -120f;

		internal float yawSatAlphaRateDn = -1f;
		internal float yawSatAlphaSign = 1f;
		internal float yawSatFarRate = 999f;
		internal float pushNrQ, pullNrQ;   // 3.1.7 PushNoRetreat: the rate this held push / pull toward zero has reached (trajectory, deg/s)   // 3.1.7: the grow rate at zero AoA on the far side of the toward-zero sweep (SweepZeroBlend)   // 3.1.7: the AoA side the two rates were sized for

		private bool yawSatCapOn;

		public float yawSatPitchDbg;
		private float sweepClipDbg;   // 2.36.0: the push's clip (ask - command) by the caps or the plan (see PushPlanClipKey)
		private bool sweepClipOn;

		// margin schedule: RollStopSideslip up to StopSlipAoALo, RollStopSideslip50 at 50, RollStopSideslip65 at 65, off past StopSlipAoAEnd
		internal static float StopSlipAoALo = 10f;

		internal static float StopSlipAoAEnd = 70f;

		// the limit comes down at StopSlipFallK x (the rudders' braking yaw acceleration / sin AoA), within StopSlipFallMin..Max
		// deg/s per s, so a falling limit is a roll the yaw can coordinate, never a stop of its own
		internal static float StopSlipFallK = 0.35f;

		internal static float StopSlipFallMin = 20f;


		// while the trajectory is still accelerating into the roll (faster than this, deg/s^2) the limit does not come down:
		// the rudders pin in every fast entry and unpin as the yaw catches up

		// yaw-saturation hold (see LateralLaw)
		internal static float StopSlipHoldTau = 0.08f;

		public float stopSlipUDbg;

		public float stopSlipPredDbg;

		public float stopSlipMarginDbg;

		private float rCmdCoordPrev;


		public float noChaseDbg;

		// no-chase and the yaw slew lead fade out over this AoA band (the one-axis high-AoA law owns the yaw there)
		internal static float StopSlipA0 = 36f;

		internal static float StopSlipA1 = 45f;


		internal static float StopSlipRiseK = 1.5f;


		public float stopSlipLimDbg = 999f;

		public float stopSlipABDbg;

		// 2.5.3 yaw-reach (pace) governor, RollGovernorReplaces >= 2 (see LateralLaw, after the roll trajectory step). The lateral
		// pair's body-yaw demand (pS sin AoA + the sideslip correction's cos AoA share) may not get further from the measured yaw
		// rate than the yaw axis can close within PaceHorizon: the yaw acceleration the jet is showing now (PaceAccelTau filter)
		// times the horizon, plus what the rudders can add within it from where they are (their tables, slew-limited, at
		// PaceRudderUse), plus PaceFloor deg/s for the loop's normal tracking error. The roll reference is clamped to that window.
		internal static float PaceHorizon = 0.15f;

		internal static float PaceFloor = 4f;

		internal static float PaceRudderUse = 1f;

		internal static float PaceAccelTau = 0.08f;

		// below this |sin AoA| the roll's yaw share is too small for the window to mean anything
		internal static float PaceSinMin = 0.05f;

		internal static float PaceRollUse = 1f;

		// open-loop: share of the rudders' available yaw the trajectory's acceleration / braking may ask for (level 3)
		internal static float PaceYawUse = 0.85f;

		private float paceAccelCap = -1f;

		// 1 = the open-loop cap / the rudder-usage gate fade out over the rudder fade (StopSlipA0..A1); 0 = they stand at every AoA


		// 1 = the open-loop cap hands over to the one-axis law as the rudders fade (rudderAuth); 0 = 2.5.5 (cap at every AoA)

		// false = keep the margin cap and the yaw-reserve governor under the pace governor (2.5.6 and before), for A/B
		// 2.5.15 sideslip-tolerance probe: the airframe's own aero (every AeroPart at the game's per-part law, the surfaces at
		// their present deflection) evaluated at the present AoA and a sweep of hypothetical sideslip, every SlipProbePeriod.
		// Two bounds come out: the sideslip where the restoring yaw moment stops growing (directional stiffness gone - the
		// departure boundary) and the sideslip where the rudders' or the roll surfaces' authority has fallen to SlipAuthFrac of
		// its zero-sideslip value. The smaller, x SlipTolUse, caps the margin. Nothing entered: the tables are the craft's.
		internal static float SlipProbePeriod = 0.5f;
		internal static float SlipProbeStep = 4f;
		internal static float SlipProbeMax = 20f;
		internal static float SlipTolUse = 0.7f;
		internal static float SlipAuthFrac = 0.5f;
		internal static float SlipAuthUse = 0.5f;
		private float slipSignR;
		internal static float SlipTolTau = 0.5f;
		private float slipProbeTimer;
		private float slipTolF = 25f;
		public float slipTolDbg = 25f;
		public float slipDepDbg = 25f;
		public float slipAuthDbg = 25f;
		public float slipStiffDbg;

		// 3.1.0: the 2.31.2 passive-airframe telemetry (PassiveStep) is archived (C:\Games\NO mods\F22E-FCS-retired-passive-step). Its CSV
		// columns (psAX..psNr, cePassXP..cePassSN) are kept and write 0. AirframeMomentAt stays: the bench's sliptruth tool uses it.
		private List<(ControlSurface cs, AeroPart host, AirfoilTable table)> psCamber;
		private readonly Dictionary<AeroPart, float> psCdMin = new Dictionary<AeroPart, float>();
		// 2.5.8 rate mode (RollPaceRateMode): the lever is the commanded roll rate, not the trajectory's acceleration or braking.
		// The roll-rate target is capped at the rate whose stick reversal, run at the tuned acceleration / braking, the yaw can
		// follow within the sideslip margin: the reversal swings the yaw demand by 2 pS sin AoA; the rudders first cross from
		// where they are (slew) and then pull at their full yaw on the new side; the yaw-rate error that accumulates meanwhile,
		// x cos AoA, is sideslip. PaceRateK scales the predicted sideslip (1 = kinematic, no credit for the airframe's own
		// directional stiffness); PaceRateFloor is the least rate the cap leaves (deg/s).
		internal static float PaceRateK = 1f;

		internal static float PaceRateFloor = 10f;

		// 2.6.9: the rate cap's blend weighted by rudderAuth (its model's validity); false = 2.6.8 (blend at every AoA)


		// 2.6.9: the rate cap models the ramped reversal the law flies (false = the 2.5.8 step model)
		internal static bool PaceRateRamp = true;

		// bench: scale on the cap's rudder-crossing dead time (1 = 2.6.9)
		internal static float RateCapTauK = 1f;

		// 2.6.9: the cap's dead time is the travel to the deflection the reversal needs, not to the far stop
		internal static bool RateCapNeedTravel = true;

		// 2.6.11: the pace window reads the whole airframe's yaw - its usage for the gate, every effector's travel for the reach - at every AoA (false = rudders)

		internal static bool PaceMarginReach = true;

		// bench: false = no open-loop acceleration cap at any blend (the trajectory always runs at its tuned acceleration / braking)
		internal static bool PaceAccelCapOn = true;

		// 2.7.0 measured pace window (PaceMeasured). One governor on the commanded roll rate, from measured state and the
		// allocator's own output - no effector tables beyond the deflections the allocator has actually commanded, no surface
		// list, no rate cap, no blend, no open-loop acceleration cap. Each tick, for the yaw axis and the roll axis:
		//   what the axis will have done by the end of PaceHorizon = its measured acceleration now (everything acting on the
		//   airframe, passive and active) plus the increment still in flight toward the effectors' commands, as far as their
		//   servos get within the horizon (the tables at the commanded deflection: the allocator's decision, not a hypothesis);
		//   the sideslip margin still unused, spent at that acceleration (sqrt(2 a (margin - |beta|) / cos AoA) of yaw-rate lead);
		//   PaceFloor for the loop's ordinary tracking error.
		// The roll target may not ask the axis for more than that - but only while the axis measurably cannot keep up: the
		// allocator reports the target unmet at the commands it gave (allocUnmet), or the commanded increment is further away
		// than the servos reach within the horizon (slew). In a coordinated roll, a corkscrew, the falling leaf, nothing binds
		// (no steady-state tax). Where an axis is out - rudders crossing their travel in a reversal, the stabs busy with the
		// pull - the target is held to what the axis delivers and the trajectory still runs at its tuned acceleration / braking
		// toward it (Tom's rule: the commanded rate is the only lever).

		// deg/s^2 of unmet-plus-undelivered acceleration at which the gate starts (PaceSatDead) and is fully on (PaceSatDead + PaceSatSpan)
		internal static float PaceSatDead = 5f;

		// 2.7.2: the unmet acceleration, as a share of the axis's authority, at which it counts as fully saturated
		internal static float PaceSatFrac = 0.25f;

		internal static float PaceSatSpan = 40f;

		// filter on the allocator's unmet report (s)
		internal static float PaceSatTau = 0.05f;

		// share of the axis's demonstrated acceleration (a decaying peak of |omega-dot|, PaceSeenTau) credited where nothing is
		// yet committed: the margin lead and the release pace after a hold
		internal static float PaceSeenUse = 0.5f;

		internal static float PaceSeenTau = 3f;

		// 2.7.1: lead allowed over the axis as a time at its capacity (s) - the sideslip rate accepted while the roll waits for it
		internal static float PaceLeadT = -1f;   // 2.17.0: < 0 = 1 / KYaw (0.4 s at the default gain); 0.1 = the 2.7.1 value

		private float paceRatePrev;

		private bool paceRateInit;

		// 2.7.0: the trajectory's acceleration law reads the stick's intent (a reversal is a reversal even while the window
		// holds the target on the near side), so a governed reversal brakes at the reversal's tuned acceleration, never the stop's
		internal static bool PaceRevIntent = true;

		public Vector3 allocUnmet;

		// 2.7.5: measured per-axis capacity estimator, telemetry only (drives nothing)
		internal readonly CapacityEstimator capEst = new CapacityEstimator();

		// F-22E 3.1.0: the table rebuild's surfaces for the fork-join (ParallelTables)
		private readonly List<SurfaceEffector> parSurf = new List<SurfaceEffector>();

		// F-22E 3.1.0 per-phase timer (Stopwatch ticks, this tick): table rebuild, allocator solves (main allocator), estimator kick, CSV capture
		internal long phRebuild, phAlloc, phKick, phRow;

		// F-22E 3.1.1: the rest split further - laws, the asym step (its moment, slip probe and sweep inside it), the envelope, mass
		// properties, flaps, ground steering, VmcApply (timed by the hook)
		internal long phLaws, phAsym, phAsymMom, phSlip, phSweep, phEnv, phMass, phFlaps, phGround, phVmc;

		// F-22E 4.1.3 (diagnostic): checkpoints through Tick (Stopwatch ticks), so its whole time is accounted for in 13 consecutive
		// segments (TickStats.SegNames); mkDone = this tick ran to its end. Drives nothing.
		internal readonly long[] mk = new long[14];

		internal bool mkDone;

		// F-22E 4.1.3: the segments of the last finished tick (us), the managed heap's growth over it (bytes, Hooks) and whether a
		// garbage collection ran during it (the CSV row of that tick, FinishRow)
		internal readonly float[] segUs = new float[13];

		internal long memDelta;

		internal int gcDuring;

		internal long memT0;

		internal int gcT0;

		internal float FlightTime => time;

		// F-22E 4.1.3: the performance switches as digits: Telemetry, NativeAllocator, NativeSweep, NativeTables, SweepGridCache,
		// ParallelTables, ParallelSweep, ParallelAllocator, EstimatorThreaded
		internal static int SwitchMask()
		{
			int m = Plugin.TelemetryEnabled ? 1 : 0;
			m = m * 10 + Plugin.NativeAllocator;
			m = m * 10 + (Plugin.NativeSweep ? 1 : 0);
			m = m * 10 + (Plugin.NativeTables ? 1 : 0);
			m = m * 10 + (Plugin.SweepGridCache ? 1 : 0);
			m = m * 10 + (Plugin.ParallelTables ? 1 : 0);
			m = m * 10 + (Plugin.ParallelSweep ? 1 : 0);
			m = m * 10 + (Plugin.ParallelAllocator ? 1 : 0);
			m = m * 10 + (Plugin.EstimatorThreaded ? 1 : 0);
			return m;
		}


		internal float EstLastWaitUs => (estJobF != null) ? estJobF.lastWaitUs : 0f;

		// F-22E 3.0.4: the estimator's probes on a worker thread (EstimatorJob); kicked where the estimator ran, joined at the top of
		// the next Tick
		private EstimatorJob estJobF;

		internal EstimatorJob estJob => estJobF ?? (estJobF = new EstimatorJob(capEst));

		// collect the estimator now (bench: before reading capEst after a Tick)
		internal void EstJoin()
		{
			estJobF?.Join();
		}

		// F-22E 3.0.4: the CSV row of tick N is captured at the end of tick N and finished at the top of tick N+1, once the estimator
		// job of tick N is joined (its columns) and tick N's time is known (fcsUs)
		private TelemetryRow pendingRow;

		// F-22E 3.0.4 tick timer (Hooks: Tick + VmcApply, wall time, us)
		internal float lastTickUs;

		internal void NoteTickTime(float us, long vmcTicks = 0L)
		{
			phVmc = vmcTicks;
			lastTickUs = us;
			phAlloc = alloc.solveTicks - allocTicks0;
		}

		private long allocTicks0;

		internal static float TicksToUs(long t)
		{
			return (float)((double)t * 1000000.0 / (double)System.Diagnostics.Stopwatch.Frequency);
		}

		// the allocator re-solves against the stabs' reach (telemetry): the probes' share is on the worker's allocator when threaded
		private int ReachMovesTotal => alloc.reachMoves + ((estJobF != null) ? estJobF.WorkerReachMoves : 0);

		private Lpf3 allocUnmetF;

		public float aYawSeen;

		private float aRollSeen;

		public float paceGateZDbg;

		public float paceGateXDbg;

		public float paceDzHDbg;

		public float paceDzAllDbg;

		public float paceDxHDbg;

		public float paceDxAllDbg;

		public float paceRHDbg;

		public float pacePHDbg;

		public float paceTgtRaw;

		public float paceSeenZDbg;

		private float paceBetaG;

		public float paceMLeftDbg;

		public float paceLoDbg;

		public float paceHiDbg;

		public float paceClampDbg;

		// 2.20.0: the window's no-fin stop (the roll target held at the stopped-body-roll rotation r sin AoA; the yaw loop asked for its brake)
		internal static bool PaceStopNoFin = true;

		internal static bool PaceStopNoFinGate = true;   // 3.0.2, see PaceStopNoFin's use

		private float paceStopNF;

		public float paceStopNFDbg;

		public float paceCapZPDbg;

		public float paceCapZNDbg;

		public float paceCapXPDbg;

		public float paceCapXNDbg;

		public float paceHdZPDbg;

		public float paceHdZNDbg;

		// 2.7.2: passive directional stiffness credited to the reversal cap, as a share of the restoring yaw at the margin sideslip
		internal static float PaceStiffUse = 1f;

		// 2.7.1's target rate limit at the capacity (Tom, 10:19: felt as slowed deceleration - off)


		internal static float PaceGateOn = 0.5f;

		internal static float PaceGateOff = 0.25f;

		private bool paceLatchZ;

		private bool paceLatchX;

		public float paceNEffDbg;

		public float paceDdDbg;


		private float revBlendF;

		internal static float PaceMarginK = 1f;

		public float paceMarginLeadDbg;

		// 2.6.9: the pace window's gate reads the whole airframe's yaw usage past the rudder fade (false = rudder load only)

		private float yawUseAllF;

		private float rateBlendEffDbg;
		// 2.5.12: the yaw the cap credits is derated by the allocator's own unmet-yaw report (yawSat, 0..1 over 10-50 deg/s^2
		// unmet, 0.25 s filter): by PaceSatDerateHi of it past the rudder fade (the stabs are the yaw axis there and the tables
		// overstate them 3-4x: 13:31 log, 55 s - a 64 deg/s roll allowed at 65 deg AoA, the release left 60 deg/s of yaw the
		// axis shed at 25 deg/s^2, 11 deg as the AoA fell through 30), PaceSatDerateLo below it. "Not too much" (Tom).
		internal static float PaceSatDerateHi = 0.5f;
		internal static float PaceSatDerateLo = 0.25f;




		// 2.6.6: 1 - cos AoA at which the shed cap stands at full strength (0.5 = 60 deg); the pace eases below it as holding buys less
		internal static float SweepShedCos = 0.05f;   // 2.8.0: 0.5 -> 0.05, the cap stands at every AoA (Tom, 2 Nov: smooth over fast in the push-over)

		// weight of the allocator's unmet yaw (yawSat) in the window's gate

		// the window's gate releases over this (s); the rudders read zero usage for a few ticks as they swing through center
		internal static float PaceGateTau = 0.3f;

		private float paceGateF;

		private float paceGateXF;
		// 2.6.1: the window's release is paced by the yaw. The window holds the target while the rudders are loaded (PaceLoad0..1 of
		// their yaw, 0.08 s filter) and let it go in one tick when they unloaded, into the full trade cap at the blended acceleration
		// (300-720 deg/s^2 at blend 0.41): the yaw demand jumped, the rudders reloaded, the window held again - a 1.9 Hz hold /
		// release cycle, +-5 deg/s on the target and +-5 deg on the rudders, in every corkscrew entry at 20-50 deg AoA once the
		// 2.5.14 trade share gave the target room above the window (bench; 14:55 / 15:10 logs: pSTarget chatter 0.7 -> 3.0-3.7
		// deg/s from 2.5.11). The hold may still come on in one tick (that is the protection); the climb back out of it may not
		// ask the yaw for more than PaceYawUse of what the rudders can add toward the move's side (their tables, from where they
		// are): roll-target rise x sin AoA = yaw acceleration. The trajectory's own acceleration and braking are untouched; this
		// is the commanded rate, climbing out of a hold at the yaw's pace instead of in a step. PaceReleasePace 0 = 2.6.0.
		// Only a hold with the roll already going the target's way is paced out (paceWinSame): a stick reversal's hold (the
		// window keeps the new target at zero while the roll brakes through) is released as before, so the reversal still runs at
		// the tuned acceleration (pacing it too took 450 km/h / 12-19 deg AoA reversals from 0.3 to 0.7-1.0 s on the bench).
		private bool paceWinHeld;
		private bool paceWinSame;
		private float paceWinPrev;
		public float paceRelDbg;
		internal static float PaceReleasePace = 1f;

		// the window applies as the rudders' usage (either side) runs from PaceLoad0 to PaceLoad1 of their yaw
		internal static float PaceLoad0 = 0.5f;

		internal static float PaceLoad1 = 0.75f;

		private Lpf paceRDotF;

		private Lpf pacePDotF;

		private float paceOmegaZPrev;

		private float paceOmegaXPrev;

		private bool paceInit;

		public float paceLeadDbg;

		public float paceHoldDbg;

		internal static float PullTradeFFK = 0.3f;
		// 2.5.14: the share of the yaw authority an AoA sweep under a roll may spend, of the part the rudders are not using
		internal static float PullTradeFreeK = 0.35f;

		internal static float PullTradeFFIas0 = 93f;

		internal static float PullTradeFFIasSpan = 25f;

		internal static float PullTradeFall = 300f;

		internal static float PullTradeRise = 120f;

		internal static float PullTradeCutA = 15f;








		private bool pullGovTouched;


		internal static float GovCapTau = 0.3f;



		private float pullTradePs = 999f;

		private float pullTradeTgt = 999f;

		public bool pullTradeHoldDbg;

		internal static float PullTradeRiseHold = 0f;

		public float pullTradeDbg;

		public float slipGovPullDbg = 1f;






		internal static float ToeLatRelease = 0.5f;

		// bench probes (2.4.7 investigation)
		private float toeLatGate;

		public float toeTgtF;


		public float coordJerkDbg;

		internal static float AsymRollMax = 120f;

		internal static float AsymRollU0 = 0.1f;

		internal static bool AsymRollHardOnly = true;

		internal static float EndJerkHi = 2f;

		internal static float CoupGateA0 = 12f;

		// 2.9.0 SweepHoldPitchPrio: the nose-down counterpart of OvershootPitchPriority. While the AoA-sweep cap holds the pitch-rate
		// command above the stick's push (a push-over under a rotation the yaw cannot shed in time) and the nose drops faster than
		// that command by SweepHoldErr0..+SweepHoldErrSpan deg/s, the pitch hold owns the stabilators (allocator pitch weight up,
		// the released-roll priority off, the honest stab model, the nozzles free), as the run-away case does. 15:59 log (2.8.1),
		// 46.3 / 87.9 s: push-over from 65 deg AoA at 225 kt with 72-79 deg/s of rotation; the cap asked -12 then +9 deg/s, the
		// nose went down at 31 deg/s with the right stab at its +35 peak and the left at -8 (both on the roll stop, 240-280 deg/s^2
		// of roll unmet at weight 1.3 against pitch's 7.5), AoA 40 -> 20 in 0.4 s against a nose-up command, 19.5 / 21.2 deg of
		// sideslip at 25 -> 5 deg AoA from the 50 deg/s of yaw still on board. Tom: slow the pitch, not the rotation, at high AoA.
		internal static float SweepHoldErr0 = 2f;

		internal static float SweepHoldErrSpan = 8f;

		// 2.9.2 SweepHoldStallWindow: during the capped sweep toward zero AoA, once the stabilators have pitch range back
		// (stabPitchFrac > 0), they are held inside their attached window (local AoA <= lift peak + RpMainStallMargin), the
		// 1.8.40 StallWindow. 16:37 log (2.9.1), 53.7 s: the right stab was parked at -30 (45 deg of local AoA) by the roll
		// stop through 65 -> 50 deg while pitch was TVC-only; when the hold came (pitch weight 8, rate-bounded) the allocator
		// had no nose-up gradient on a stab that deep past its stall - the honest model reads TE-up as un-stalling it (more
		// tail lift, nose-down) - so it drove it further TE-down (-25) and put all the nose-up on the nozzles (+20, the stop);
		// the nose went 37 -> 22 deg at -24 deg/s against +5..+10 commanded until the stab had been walked through (0.5 s).
		// Inside the window the stabs have gradient the moment the hold needs them; the roll stop keeps what they give there.
		internal static float CoupHeldGate = 1f;

		internal static float CoupHeldDy0 = 0.04f;

		internal static float CoupHeldDySpan = 0.08f;

		internal static float AsymPitchPrio = 1f;

		internal static float AsymPitchPrioLead = 3f;

		internal static float AsymStabAntiCost = 0f;

		internal static float AsymPitchCapLead = 2f;

		internal static float AsymPitchCapFloor = 10f;

		public float asymCapPitch = 999f;


		internal static float EndJerkLp0 = 1.5f;

		internal static float EndJerkLp1 = 2.5f;

		public float lpDbg;

		internal static float AIRollDirectAccel = 3000f;

		public bool aiGunTrack;

		public float aiRollFade = 1f;

		private float rollSlew = 100f;

		public static bool DebugYaw;

		public float dbgWVe;

		public float dbgYawRud;

		public float dbgYawWing;

		public float dbgYawStab;

		public float dbgYawTvc;

		public float dbgTargetZ;

		public float dbgTargetX;

		// 2.4.6 telemetry
		internal Vector3 axWDbg;

		public float dbgMfZ;

		public float dbgTgtY;

		public float dbgMfY;

		public float aRoll = 300f;

		public float aYaw = 60f;

		private Lpf aRollF;

		private Lpf aYawF;

		private Lpf aRollWF;

		public float aRollWing = 200f;

		public float pS;

		private RateTraj pTraj;

		private RateTraj qTraj;
		private float qCmdPrevL, qCmdSlopeF;   // 3.1.8 PitchTrajLead

		private Lpf rSF;

		public float rudderAuth = 1f;

		// 1.8.35 sideslip rudder: signed yaw-command travel allowed beyond the normal rudder fade (deg), and the pedal
		// sideslip command it corrects toward (from the previous lateral-law pass)
		public float rudSideDbg;

		private float betaPedalCmd;

		private float rudSideF;

		// 1.8.36 ground steering
		public bool gsActive;

		public float gsNwsDeg;

		public float gsRCmd;

		public float gsRud;

		public bool gsHold;

		public float gsLineY;

		public float gsChiRef;

		private Vector3 gsLineP;

		// the captured line's point in the game's global coordinates: NO shifts its Unity world origin as the jet travels, so a
		// Unity world position stored at the capture jumps (27 Oct F-22E log: 242 m and 58 m in one tick)
		private GlobalPosition gsLinePG;

		// GroundStockMatch: the jet's own ControlsFilter (stock fly-by-wire parameters), the stock yaw sent to the gear and
		// rudders, and the sideslip rate for the drift limit
		public ControlsFilter stockCf;

		private float gsStockYaw;

		public bool gsStockPhase;

		private float gsBetaPrev = float.NaN;

		private float gsBetaDot;

		private float gsInt;

		// the raw pedal (inp.yaw), as LandingGear reads it: no stick smoothing or rate limit
		public float gsPedRaw;

		private float gsRudInt;

		private float gsBrakeT;

		private float gsLead = -1f;

		public float gsChiAir;

		private float gsSinceContact = 99f;

		public float gsAirT;

		public float aInt;

		// 2.0.14: AoA limit capture. The capture bound used to stop at zero once the AoA was past the limit (the
		// flight-path rate only: nothing brought it back) and protection only began at limit + 0.5 with zero onset, so
		// the jet sat 0.3-0.6 deg over the limit in rolls. The bound now continues through the limit (CapKp per deg,
		// nose-down past it) and an integrator (CapKi) trims the measured residual while the pilot asks for more than
		// the limit. Protection is never less nose-down than this bound, so there is no step at limit + 0.5.
		public float capIUp;

		public float capIDn;

		public float capBoundU;

		public float capBoundD;

		// nose-down (U) / nose-up (D) AoA rate the capture asks for past the flight-path rate, for protection's onset
		public float capPushU;

		public float capPushD;

		internal static float CapKi = 3f;

		internal static float CapIMax = 4f;

		internal static float CapIAlphaDot = 3f;

		internal static float CapIBand = 0.6f;

		internal static float CapIHoldT = 0.3f;

		public float capHoldTU;

		public float capHoldTD;

		public float yawSat;

		private Lpf yawSatF;

		private Lpf guardF;

		public float stabPitchFrac = 1f;

		public float tvcAnchor;

		public float tvcBudgetGate;

		private float tvcBudgetLat;

		private bool tvcAnchorInit;

		private float pCmdPrev;

		private float rCmdPrev;

		private Lpf pDotF;

		private Lpf rDotF;

		public float pFull = 300f;

		public float pStockRef;

		public float pMaxDbg;

		public float rMaxDbg;

		public float rollCtrlHalf = 100000f;

		public float guardDbg = 1f;

		public float pSmaxDbg;

		public float pSTgtDbg;

		public float latAccDbg;

		public float latJerkDbg;

		public float rSDbg;

		public bool revFlow;

		private bool revFlowLogged;

		public float sp;

		public float sr;

		public float sy;

		private float spF;

		private float srF;

		private float syF;

		public float qB;

		public float qRaw;

		public float qCmd;

		public float qT;

		public float qTprev;

		public float betaG;

		public float alphaCmd;

		public float nCmd;

		public bool unload;

		public bool prot;

		public float qMaxDbg;

		public float qMinDbg;

		public float gUnderDbg;

		public float pCmd;

		public float rCmd;

		public float pDotFF;

		public float rDotFF;

		public Vector3 omegaDotDes;

		public Vector3 mTarget;

		// 2.0.8: DisableTVC (debug) locks the nozzles at 0 whatever the FCS computes
		private float tvcPitchRaw;

		public float tvcPitchInput
		{
			get
			{
				return Plugin.DisableTVC ? 0f : tvcPitchRaw;
			}
			set
			{
				tvcPitchRaw = value;
			}
		}

		private int groundFrames;

		public bool rotating;

		internal static float RotLeadT = 0.1f;

		internal static float RotLeadMax = 3f;

		public bool mainsOnGround;

		// 2.0.6 ground rate law state
		public bool groundRateOn;

		public bool noseContactLaw;

		private float gNeutralT;

		private float gContactAir;

		private float rotMomLim;

		private bool rotMomOn;

		public float groundBrake;

		public float airBrake;

		// 2.2.16 (AirbrakeAoAFade): the in-flight brake's share of the drag and its opening target
		private readonly List<Airbrake> airbrakes = new List<Airbrake>();

		private static readonly System.Reflection.FieldInfo fABDrag = HarmonyLib.AccessTools.Field(typeof(Airbrake), "dragAmount");

		private static readonly System.Reflection.FieldInfo fABPart = HarmonyLib.AccessTools.Field(typeof(Airbrake), "part");

		private readonly List<Rigidbody> abRb = new List<Rigidbody>();

		private readonly List<float> abDrag = new List<float>();

		private readonly List<UnitPart> abPart = new List<UnitPart>();

		public float airbrakeShare = 1f;

		public float airbrakeFade = 1f;

		public float airbrakeOpenNow;

		// the opening the brake is driven to: 0 when not commanded, else 1 or less with AirbrakeAoAFade
		// 2.4.15: with ThrottleIndependentAirbrake off the panels open fully (stock), no AoA fade
		public float AirbrakeOpenTarget => AirbrakeOut ? ((Plugin.AirbrakeAoAFade && Plugin.ThrottleIndependentAirbrake) ? airbrakeFade : 1f) : 0f;

		public bool onGround;

		private float[] brakeBias;

		private float gbLogTimer;

		private float noseDownTime;

		private float noseUpTime;

		private float airTime;

		private LandingGear noseGearCached;

		private bool noseGearSearched;

		private FcsMode lastLoggedMode;

		private Telemetry telemetry;

		private float time;

		private static readonly float[] envAlphas = new float[25]
		{
			-1f, 0f, 1f, 5f, 10f, 15f, 20f, 25f, 30f, 35f,
			40f, 45f, 50f, 55f, 60f, 65f, -5f, -10f, -15f, -20f,
			-25f, -30f, -35f, -40f, -45f
		};

		private const int EnvNegStart = 16; // 1.9.7: negative-lift points for the negative hand-over threshold

		private readonly float[] envLift = new float[25];

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

		public float aBrakeTgt;

		internal static int CaptureMode = 1;

		internal static bool RollCapture = true;

		internal static float CapLagT = 0.3f;

		// share of the nose-down (nose-up at negative AoA) pitch authority that the nozzles carry
		private float CapTvcShare()
		{
			if (tvc == null || !tvc.active || tvc.M == null || tvc.N < 2 || Ia[1, 1] <= 1f)
			{
				return 0f;
			}
			float lo = float.MaxValue;
			float hi = float.MinValue;
			for (int k = 0; k < tvc.N; k++)
			{
				lo = Mathf.Min(lo, tvc.M[k].y);
				hi = Mathf.Max(hi, tvc.M[k].y);
			}
			float half = 0.5f * (hi - lo) * IaInv[1, 1] * 57.29578f;
			return Mathf.Clamp01(half / Mathf.Max(20f, (alpha >= 0f) ? aDn : aUp));
		}


		internal static float RollCaptureCoupK = 1f;

		internal static float PitchLeadT = 0.25f;

		internal static float StabsPitchOnlyCost = 1e6f;

		internal static float HiAoAStabRollCost = 0f;

		internal static float TvcNeutralQdot = 20f;

		internal static float TvcNeutralTau = 1f;

		internal static float TvcNeutralBand = 1f;

		private float tvcNeutralW;

		public float tvcNeutralDbg;

		internal static float HiAoAStabRollA0 = 22f;

		internal static float HiAoAStabRollSpan = 10f;

		public float pitchLeadDbg;



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

		public float rollPitchFF;

		private float tvcPin;

		private bool tvcPinned;

		private readonly float[] pinLo = new float[16];

		private readonly float[] pinHi = new float[16];

		private readonly float[] symSave = new float[16];

		public float dMeasR;

		public float dMeasY;

		private Lpf dMeasRF;

		private Lpf dTrimRF;

		public float dTrimR;

		public float aRollPos = 300f;

		public float aRollNeg = 300f;

		internal static bool RollTrimHeadroom = true;

		private Lpf dMeasYF;

		private float asymLogTimer = 99f;

		private float asymLoggedCap = 999f;

		private float asymLoggedCapN = -999f;

		private readonly HashSet<AeroPart> surfaceParts = new HashSet<AeroPart>();

		private int asymK = -1;

		private bool asymCycleDone;

		private bool asymFirstLogged;

		internal static int AsymPointsPerStep = 3;

		private Vector3 asymMT;

		private float[] asymDsym;

		public float Lp = -100000f;

		private const float KCapture = 3f;

		internal static float YawBoostFixed = -1f;

		internal static bool RudderRevBack = true;

		internal static float YawFFAoA0 = 15f;

		internal static float YawFFAoA1 = 28f;

		internal static bool ReversalFloor = true;

		internal static float RevFloorK = 1f;

		internal static bool RevConst = true;

		internal static bool RevCarry = true;

		// F-22E 4.1.1: one acceleration through a reversal, the load's limit included (false = 4.1.0)
		internal static bool RevOneAccel = true;

		// F-22E 4.1.2: a reversal the load itself brakes builds its acceleration over the rudders' travel (false = 4.1.1)
		internal static bool RevLoadRamp = true;

		private float revA;

		private float revTarget;

		internal static float WashK = -1f;

		internal static float WashTau = -1f;

		internal static float WashUtilHi = 0.75f;

		internal static bool GUnderAboveOnly = true;

		private float washApplied;

		internal static float MLeadTau = 0.04f;

		internal static float IndiTau = 0.04f;

		// 1.9.0 hybrid INDI (pitch): the incremental law takes the airframe's own moment from filtered measurements, so
		// it lags the true one by the filter (IndiTau). At high dynamic pressure the non-control-surface moment moves fast
		// with AoA and pitch rate (the fuselage/LERX nose-up grows with AoA), and that lag let the pitch rate overshoot its
		// command by ~2 deg/s for 0.3 s at 1300 km/h - ~1.4 g at that speed, the G-command overshoot/undershoot cycle.
		// The lag is predicted from the airframe model: dM = dM/dAoA x (AoA - AoA filtered) + dM/dq x (q - q filtered),
		// slopes of the non-surface parts' moment evaluated every 0.1 s at the present flow.
		public float mAlphaO;

		public float mQO;

		public float hybridDM;

		private float dHatPrevY;

		private bool dHatInit;

		internal float dDotYF;

		private float distLeadGate;

		internal float distLeadAcc;

		internal static float DistLeadTau = 0.06f;

		internal static float DistLeadMax = 15f;

		internal static bool DistLeadNoDamp = true;

		internal static float DistLeadTw0 = 0.2f;

		internal static float DistLeadTw1 = 0.4f;

		internal static float NewtonMinTw = 0.25f;


		private Lpf alphaIndiF;

		private Lpf qIndiF;

		private float mPrevY;

		private bool mLeadInit;

		private float mDotYF;

		public float mLeadGate;

		public float mLeadAgree = 1f;

		internal static bool PairedTrim = true;

		public float latReserve;

		public bool isAI;

		public float aiGLimit = 9.5f;

		public float aiCornerSpeed = 140f;

		private bool telemetryOpened;

		private float aiLogT;

		private float aiLogAMax;

		private float aiLogAMin;

		private float aiLogGMax;

		private float aiLogGMin;

		private float aiLogIasMin = 9999f;

		private float aiLogIasMax;

		private float aiLogProtT;

		private int aiLogN;

		private float aiLogP2;

		private int aiLogRev;

		private int aiLogRevSign;

		private float aiLogGunT;

		private float aiLogFadeT;

		private void AILogStep(float dt)
		{
			if (aiLogN == 0)
			{
				aiLogAMax = (aiLogAMin = alpha);
				aiLogGMax = (aiLogGMin = nz);
			}
			aiLogN++;
			aiLogT += dt;
			aiLogAMax = Mathf.Max(aiLogAMax, alpha);
			aiLogAMin = Mathf.Min(aiLogAMin, alpha);
			aiLogGMax = Mathf.Max(aiLogGMax, nz);
			aiLogGMin = Mathf.Min(aiLogGMin, nz);
			aiLogIasMin = Mathf.Min(aiLogIasMin, ias * 3.6f);
			aiLogIasMax = Mathf.Max(aiLogIasMax, ias * 3.6f);
			if (prot)
			{
				aiLogProtT += dt;
			}
			float pDeg = omega.x * 57.29578f;
			aiLogP2 += pDeg * pDeg * dt;
			int num = ((pDeg > 15f) ? 1 : ((pDeg < -15f) ? (-1) : 0));
			if (num != 0)
			{
				if (aiLogRevSign != 0 && num != aiLogRevSign)
				{
					aiLogRev++;
				}
				aiLogRevSign = num;
			}
			if (aiGunTrack)
			{
				aiLogGunT += dt;
				if (aiRollFade < 0.99f)
				{
					aiLogFadeT += dt;
				}
			}
			if (aiLogT >= 20f)
			{
				Plugin.Log.LogInfo($"F-22E FCS AI jet #{((UnityEngine.Object)(object)ac).GetInstanceID()} last {aiLogT:0}s: mode {Mode}, IAS {aiLogIasMin:0}-{aiLogIasMax:0} km/h, AoA {aiLogAMin:0.0}..{aiLogAMax:0.0} deg, g {aiLogGMin:0.0}..{aiLogGMax:0.0}, AoA protection active {aiLogProtT:0.0}s, roll rate rms {Mathf.Sqrt(aiLogP2 / Mathf.Max(aiLogT, 0.01f)):0} deg/s with {aiLogRev} left/right reversals (>15 deg/s), gun-tracking a moving target {aiLogGunT:0.0}s (roll faded {aiLogFadeT:0.0}s).");
				aiLogT = 0f;
				aiLogN = 0;
				aiLogProtT = 0f;
				aiLogP2 = 0f;
				aiLogRev = 0;
				aiLogGunT = 0f;
				aiLogFadeT = 0f;
				aiLogIasMin = 9999f;
				aiLogIasMax = 0f;
			}
		}

		public int stabBranch;

		public float pitchNeed;

		// 2.9.0: the two halves of pitchNeed - the nose-up run-away (1.8.23) and the capped nose-down sweep running ahead of its cap
		public float pitchNeedUp;

		public float pitchNeedDn;

		public bool sweepHoldWinDbg;

		internal static float CoupRollFloor = 5f;

		// 2.6.9: the coupling roll limit from the pitch axis's measured nose-down headroom (false = the table cap + gates)

		// bench: split the blend's two effects (target mix with the rate cap / relaxation of the acceleration cap)
		internal static float PaceBlendTargetK = 1f;

		internal static float PaceBlendAccelK = 1f;

		public float coupHeadDbg;

		public float rcAOppDbg;

		public float rcTcDbg;

		public float rcSinHDbg;

		public float coupRollLim;

		private bool branchGateOn;

		internal static float BranchFailTime = 0.25f;

		internal static bool BranchFreeTvc = true;


		internal static bool BranchScaleToPeak = true;

		internal static bool BranchFullTravel = true;

		internal static float BranchDeepMargin = 5f;

		internal static float BranchCoolTime = 0.25f; // 2.0.1: 1 s -> 0.25 s (a stab could park deep in stall during the wait)

		internal static float BranchCoolTimeStop = 0.25f;

		internal static float LatTrimGain = 100f;

		internal static bool LatReservePitchFrac = true;
		public float latFloorDbg;
		internal static bool ReleaseCoordGate = true;   // 3.0.2, see ReleaseCoordinate's use
		internal static bool ReleaseCoordinate = true;   // 2.18.0: released roll under a capped nose-down sweep, fins gone: target = r / sin AoA
		internal static bool SweepHoldFade = true;       // 2.18.0: the nose waits above the rudder fade while excess yaw remains, fins gone
		internal static float SweepHoldRate = 12f;       // 2.19.0: velocity-roll rotation (deg/s) above which the nose waits, fins gone
		internal static float SweepHoldRateBlend = 6f;   // deg/s of blend above it
		internal static float SweepHoldStabFrac = 0.25f; // the hold stands while stabPitchFrac is below this (stabs off pitch)
		internal static bool PaceHoldPacedByRudder = true;   // 2.19.0: the window paces where the rudders are not the yaw axis (rudW < 1)
		public float sweepHoldDbg;
		public float pSStickRaw;
		internal static bool PaceNoRetreat = true;   // 2.18.0: the latched window never moves the target back toward the rotation
		internal static float PaceHoldCapFloor = 5f;   // deg/s^2


		public float capSweepDbg, capShedDbg;   // 2.23.0: the two AoA-rate caps on the nose-down sweep this tick (deg/s; 60 = open) - which one binds
		public float releaseBrakeTdDbg;   // 2.35.3: the device is deleted; the CSV column is kept (always 0)
		public float sweepDelF;   // 2.24.0: the delivered wind-yaw deceleration the sweep cap credited (deg/s^2, low-passed)
		public float stickRelW;   // 2.24.0: lateral stick released (1) .. held (0), over SweepNoReverseStick of stick travel
		public float relFollowDbg;   // 2.35.3: the device is deleted; the CSV column is kept (always 0)
		// 2.34.0 (PushGov): the lateral limit this tick (deg/s^2 of velocity-roll change allowed: < 0 the most the rotation may come down,
		// > 0 what it must grow; NaN = not in force), the rotation side, the roll capacity read (3-tick, TradeCapTau)
		public float pushGovL = float.NaN, pushGovU, pushGovA;
		private float pushGovSide, pushGovCapXF, pushGovCapYF, pushGovP0, pushGovGrowF;
		private bool pushGovXInit, pushGovActive;
		public float pushGovCeil, pushGovP0Dbg, pushGovAMin;
		private int reachLogPrev;
		public float pushFloorDbg, pushGovFadeW;
		public bool pushPlanOn, pushPlanDone, pushPlanReleasing; public float pushPlanA; public float pushPlanQ, pushPlanQNow, pushPlanBleed, pushPlanT, pushPlanSide, pushPlanD, pushHeldB;

		public float aYawShedDbg;   // 2.22.0: the shed the cap paced to (deg/s^2)
		internal static bool LatLimitYawFloor = true;   // 2.16.0: the lateral limit's coordination floor (see num11)
		internal static float LatFloorSinMin = 0.26f;    // sin 15 deg

		public float aUpPitch;

		public float aDnPitch;


		internal static bool LeadAgreeGate = true;

		public float pitchPrio = 1f;

		public bool stabRateBound;

		private float tvcTrimRef;

		private float tvcSlow;

		private float[] cmdSave = new float[64];

		public bool SplitDeploy
		{
			get
			{
				return (Plugin.AirbrakeSplit || !Plugin.ThrottleIndependentAirbrake) && AirbrakeOut;
			}
		}

		private LandingGear NoseGear()
		{
			if (noseGearSearched)
			{
				return noseGearCached;
			}
			noseGearSearched = true;
			LandingGear landingGear = null;
			float num = float.MinValue;
			float num2 = float.MinValue;
			int num3 = 0;
			foreach (LandingGear gear in gears)
			{
				if (!(gear == null))
				{
					num3++;
					float num4 = Vector3.Dot(gear.transform.position - ((ac.rb != null) ? ac.rb.worldCenterOfMass : ((Component)(object)ac).transform.position), ((Component)(object)ac).transform.forward);
					if (num4 > num)
					{
						num2 = num;
						num = num4;
						landingGear = gear;
					}
					else if (num4 > num2)
					{
						num2 = num4;
					}
				}
			}
			noseGearCached = ((num3 >= 2 && num - num2 > 1.5f) ? landingGear : null);
			Plugin.Log.LogInfo((noseGearCached != null) ? $"F-22E FCS: nose gear '{noseGearCached.name}' ({num - num2:F1} m ahead of the next leg), {num3} gear legs." : $"F-22E FCS: no distinct nose gear among {num3} gear legs; rotation law disabled.");
			return noseGearCached;
		}

		public FcsController(Aircraft aircraft)
		{
			ac = aircraft;
			inputs = aircraft.GetInputs();
			AircraftParameters aircraftParameters = aircraft.GetAircraftParameters();
			foreach (ControlSurface item in Acc.Collect<ControlSurface>(aircraft))
			{
				AeroPart aeroPart = Acc.CS_attached(item) as AeroPart;
				if (!(aeroPart == null))
				{
					SurfaceKind kind = Classify(item);
					surfaces.Add(new SurfaceEffector(item, aeroPart, kind, FoilFor(aeroPart, aircraftParameters)));
				}
			}
			if (surfaces.Count < 6)
			{
				throw new InvalidOperationException("F-22E FCS: expected 8 control surfaces, found " + surfaces.Count);
			}
			// 2.2.0 flap-camber coupling: each rudder / flaperon / aileron knows the fixed part ahead of it
			Dictionary<string, AeroPart> partsByName = new Dictionary<string, AeroPart>();
			foreach (AeroPart apH in Acc.Collect<AeroPart>(aircraft))
			{
				partsByName[apH.gameObject.name.Replace("Aryx_KingRaptor_", "")] = apH;
			}
			foreach (SurfaceEffector seH in surfaces)
			{
				string hn = (seH.cs != null) ? Camber.HostOf(seH.cs.gameObject.name) : null;
				if (hn != null && partsByName.TryGetValue(hn, out var hp))
				{
					seH.host = hp;
					seH.hostFoil = FoilFor(hp, aircraftParameters, liftShaped: false);
				}
			}
			foreach (SurfaceEffector surface in surfaces)
			{
				if (surface.kind == SurfaceKind.Stabilator)
				{
					surface.mu = 0.0002f;
					surface.linearPitch = true;
				}
				else
				{
					// 2.0.6: with RudderPitchAid the rudders keep their true pitch moment in the allocator's model (toe-in /
					// toe-out of the canted fins), so a symmetric rudder split is available as a pitch effector at a cost
					surface.noPitch = !(surface.kind == SurfaceKind.Rudder && Plugin.RudderPitchAid);
				}
				surface.rollGroup = ((surface.kind == SurfaceKind.Stabilator) ? 1 : ((surface.kind == SurfaceKind.Aileron) ? 2 : ((surface.kind == SurfaceKind.Flaperon) ? 3 : 0)));
				surface.pitchGroup = ((surface.kind == SurfaceKind.Stabilator) ? 1 : 0);
				effs.Add(surface);
			}
			// 2.0.0: the roll-time nozzle budget and the roll-pitch pass fade out by body AoA at points chosen for Aryx's stab
			// foil (peak ~34 deg local). They stand for "the roll's down-going stab is reaching the flat top of its lift
			// curve", so they move with the stab's own modelled peak (StallStagger): 25 deg stabs -> ~8.5 deg lower.
			float stabPeak = float.MaxValue;
			foreach (SurfaceEffector sp0 in surfaces)
			{
				if (sp0.kind == SurfaceKind.Stabilator)
				{
					stabPeak = Mathf.Min(stabPeak, sp0.stallAoA);
				}
			}
			stabStallShift = (StabPeakShift && stabPeak < 90f) ? Mathf.Max(0f, StabPeakRef - stabPeak) : 0f;
			foreach (SurfaceEffector l in surfaces)
			{
				if (!l.name.EndsWith("_L"))
				{
					continue;
				}
				SurfaceEffector surfaceEffector = surfaces.Find((SurfaceEffector o) => o.kind == l.kind && o.name == l.name.Substring(0, l.name.Length - 2) + "_R");
				if (surfaceEffector == null)
				{
					continue;
				}
				bool num = l.kind == SurfaceKind.Rudder;
				float num2 = (num ? Mathf.Sign(l.origYawRange) : Mathf.Sign(l.origRollRange));
				float num3 = (num ? Mathf.Sign(surfaceEffector.origYawRange) : Mathf.Sign(surfaceEffector.origRollRange));
				if (num2 != 0f && num3 != 0f && num2 != num3)
				{
					l.partner = surfaceEffector;
					l.pairSign = num2;
					surfaceEffector.pairSign = num3;
					if (l.kind == SurfaceKind.Stabilator)
					{
						l.symCost = Plugin.StabTrimCost;
						l.antiCost = 0.01f;
					}
					else
					{
						l.symCost = (l.kind == SurfaceKind.Rudder && Plugin.RudderPitchAid) ? Plugin.RudderToeCost : 2f;
						l.antiCost = 0f;
					}
				}
			}
			tvc = new TvcEffector(Acc.Collect<Turbofan>(aircraft));
			effs.Insert(0, tvc);
			tvc.pitchGroup = 2;
			gears.AddRange(Acc.Collect<LandingGear>(aircraft));
			try
			{
				foreach (Airbrake ab in Acc.Collect<Airbrake>(aircraft))
				{
					float dA = (fABDrag != null) ? Convert.ToSingle(fABDrag.GetValue(ab)) : 0f;
					Component pc = (fABPart != null) ? (fABPart.GetValue(ab) as Component) : null;
					Rigidbody rbA = (pc is UnitPart up) ? up.rb : null;
					if (rbA != null && dA > 0f)
					{
						airbrakes.Add(ab);
						abRb.Add(rbA);
						abDrag.Add(dA);
						abPart.Add(pc as UnitPart);
					}
				}
			}
			catch (Exception exAB)
			{
				Plugin.Log.LogWarning("F-22E FCS airbrake drag lookup: " + exAB.Message);
			}
			foreach (UnitPart item2 in aircraft.partLookup)
			{
				if (item2 is AeroPart aeroPart2)
				{
					aeroParts.Add(aeroPart2);
					if (!foils.ContainsKey(aeroPart2))
					{
						foils[aeroPart2] = FoilFor(aeroPart2, aircraftParameters);
					}
				}
			}
			RebuildBodies();
			flaps = new FlapSystem(aircraft, surfaces);
			Plugin.Log.LogInfo(string.Format("F-22E FCS attached: {0} surfaces [{1}], ", surfaces.Count, string.Join(", ", surfaces.ConvertAll((SurfaceEffector s) => s.name + ":" + s.kind.ToString() + ((s.min == -s.max) ? (" ±" + s.max.ToString("0")) : (" " + s.min.ToString("0") + "/+" + s.max.ToString("0")))))) + $"TVC engines {tvc.engines.Count} (±{tvc.max:0} deg pitch only), {bodies.Count} rigidbodies, {flaps.Describe()}.");
		}

		private static SurfaceKind Classify(ControlSurface cs)
		{
			string name = cs.gameObject.name;
			if (Mathf.Abs(Acc.CS_pitchRange(cs)) > 0.01f || name.Contains("Elevator") || name.Contains("Stab"))
			{
				return SurfaceKind.Stabilator;
			}
			if (name.Contains("Rudder"))
			{
				return SurfaceKind.Rudder;
			}
			if (name.Contains("Flap"))
			{
				return SurfaceKind.Flaperon;
			}
			if (name.Contains("Aileron"))
			{
				return SurfaceKind.Aileron;
			}
			if (Mathf.Abs(Acc.CS_yawRange(cs)) > Mathf.Abs(Acc.CS_rollRange(cs)))
			{
				return SurfaceKind.Rudder;
			}
			return SurfaceKind.Other;
		}

		private static AirfoilTable FoilFor(AeroPart part, AircraftParameters prm, bool liftShaped = true)
		{
			Airfoil af = null;
			int num = Acc.AP_airfoil(part);
			if (num >= 0 && prm != null && prm.airfoils != null && num < prm.airfoils.Length)
			{
				af = prm.airfoils[num];
			}
			return StallStagger.TableFor(part, af, liftShaped);
		}

		private void RebuildBodies()
		{
			bodies.Clear();
			foreach (UnitPart item in ac.partLookup)
			{
				if (!(item == null) && !(item.rb == null) && !item.IsDetached() && !bodies.Contains(item.rb))
				{
					bodies.Add(item.rb);
				}
			}
			if (bodies.Count == 0 && ac.rb != null)
			{
				bodies.Add(ac.rb);
			}
		}

		public void Dispose()
		{
			try
			{
				estJobF?.Join();
				FlushPendingRow();
			}
			catch (Exception)
			{
			}
			telemetry?.Close();
			telemetry = null;
		}

		private void UpdateMassProps(bool full, Quaternion rootInv)
		{
			float num = 0f;
			Vector3 zero = Vector3.zero;
			Vector3 vSum = Vector3.zero;
			for (int num2 = bodies.Count - 1; num2 >= 0; num2--)
			{
				Rigidbody rigidbody = bodies[num2];
				if (rigidbody == null)
				{
					bodies.RemoveAt(num2);
				}
				else
				{
					num += rigidbody.mass;
					zero += rigidbody.mass * rigidbody.worldCenterOfMass;
					vSum += rigidbody.mass * rigidbody.velocity;
				}
			}
			if (num < 1f)
			{
				return;
			}
			mass = num;
			cg = zero / num;
			vCg = vSum / num;
			if (!full)
			{
				return;
			}
			double[,] array = new double[3, 3];
			foreach (Rigidbody body in bodies)
			{
				Matrix4x4 matrix4x = Matrix4x4.Rotate(body.rotation * body.inertiaTensorRotation);
				Vector3 inertiaTensor = body.inertiaTensor;
				Vector3 vector = body.worldCenterOfMass - cg;
				float sqrMagnitude = vector.sqrMagnitude;
				for (int i = 0; i < 3; i++)
				{
					for (int j = 0; j < 3; j++)
					{
						double num3 = matrix4x[i, 0] * inertiaTensor.x * matrix4x[j, 0] + matrix4x[i, 1] * inertiaTensor.y * matrix4x[j, 1] + matrix4x[i, 2] * inertiaTensor.z * matrix4x[j, 2];
						num3 += (double)(body.mass * (((i == j) ? sqrMagnitude : 0f) - vector[i] * vector[j]));
						array[i, j] += num3;
					}
				}
			}
			Matrix4x4 matrix4x2 = Matrix4x4.Rotate(rootInv);
			double[,] array2 = new double[3, 3];
			for (int k = 0; k < 3; k++)
			{
				for (int l = 0; l < 3; l++)
				{
					double num4 = 0.0;
					for (int m = 0; m < 3; m++)
					{
						for (int n = 0; n < 3; n++)
						{
							num4 += (double)matrix4x2[k, m] * array[m, n] * (double)matrix4x2[l, n];
						}
					}
					array2[k, l] = num4;
				}
			}
			int[] array3 = new int[3] { 2, 0, 1 };
			float[] array4 = new float[3] { -1f, -1f, 1f };
			for (int num5 = 0; num5 < 3; num5++)
			{
				for (int num6 = 0; num6 < 3; num6++)
				{
					Ia[num5, num6] = (float)((double)(array4[num5] * array4[num6]) * array2[array3[num5], array3[num6]]);
				}
			}
			Invert3(Ia, IaInv);
			alloc.SetInverseInertia(IaInv);
		}

		private static void Invert3(float[,] a, float[,] o)
		{
			float num = a[0, 0] * (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1]) - a[0, 1] * (a[1, 0] * a[2, 2] - a[1, 2] * a[2, 0]) + a[0, 2] * (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]);
			if (Mathf.Abs(num) < 1E-06f)
			{
				for (int i = 0; i < 3; i++)
				{
					for (int j = 0; j < 3; j++)
					{
						o[i, j] = ((i == j) ? (1f / Mathf.Max(1f, a[i, i])) : 0f);
					}
				}
				return;
			}
			float num2 = 1f / num;
			o[0, 0] = (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1]) * num2;
			o[0, 1] = (a[0, 2] * a[2, 1] - a[0, 1] * a[2, 2]) * num2;
			o[0, 2] = (a[0, 1] * a[1, 2] - a[0, 2] * a[1, 1]) * num2;
			o[1, 0] = (a[1, 2] * a[2, 0] - a[1, 0] * a[2, 2]) * num2;
			o[1, 1] = (a[0, 0] * a[2, 2] - a[0, 2] * a[2, 0]) * num2;
			o[1, 2] = (a[0, 2] * a[1, 0] - a[0, 0] * a[1, 2]) * num2;
			o[2, 0] = (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]) * num2;
			o[2, 1] = (a[0, 1] * a[2, 0] - a[0, 0] * a[2, 1]) * num2;
			o[2, 2] = (a[0, 0] * a[1, 1] - a[0, 1] * a[1, 0]) * num2;
		}

		private Vector3 MulI(Vector3 v)
		{
			return new Vector3(Ia[0, 0] * v.x + Ia[0, 1] * v.y + Ia[0, 2] * v.z, Ia[1, 0] * v.x + Ia[1, 1] * v.y + Ia[1, 2] * v.z, Ia[2, 0] * v.x + Ia[2, 1] * v.y + Ia[2, 2] * v.z);
		}

		private static float[] BuildAsymAlphas()
		{
			float[] array = new float[22];
			for (int i = 0; i < 22; i++)
			{
				array[i] = -40f + 5f * (float)i;
			}
			return array;
		}

		// 1.8.35: past the normal rudder fade (RudderFadeStartAoA-EndAoA) the canted rudders' "inverse roll" is what
		// moves sideslip: beta-dot = p sin(a) - r cos(a), and a yaw-right rudder command rolls the jet left, so both terms
		// push beta the same way. Bench sweep: from ~55 deg AoA their sideslip effect is >3x their velocity-vector roll
		// (6x at 75), while the flaperons' and stabs' sideslip authority collapses near 60 and reverses past ~62.
		// So above the fade the rudders get a limited range that can only REDUCE the sideslip error: deflection only in
		// the beta-reducing direction (taken from the live surface model, so it follows the sign flip at negative AoA),
		// scaled in by the error (0 below 0.5 deg, full at 3 deg), and capped so the body yaw acceleration the rudders make
		// stays within RudderSideslipYawCap. Their yaw per degree falls with AoA (about 0 by 80 deg), so the travel opens
		// up as AoA rises. Uses the previous tick's surface model and pedal-sideslip command.
		private void SideslipRudder(float dt)
		{
			rudSideDbg = 0f;
			float cap = Plugin.RudderSideslipYawCap;
			float gate = (1f - rudderAuth) * ((Mathf.Abs(alpha) < 100f) ? 1f : 0f);
			SurfaceEffector rl = null;
			SurfaceEffector rr = null;
			foreach (SurfaceEffector se in surfaces)
			{
				if (se.kind == SurfaceKind.Rudder)
				{
					if (se.name.EndsWith("_L"))
					{
						rl = se;
					}
					else if (se.name.EndsWith("_R"))
					{
						rr = se;
					}
				}
			}
			if (cap <= 0f || gate <= 0.001f || !Engaged || !havePrev || Mode == FcsMode.Ground || Mode == FcsMode.Off || rl == null || rr == null || !rl.active || !rr.active || rl.N < 2 || rr.N < 2)
			{
				rudSideF = Mathf.MoveTowards(rudSideF, 0f, 60f * dt);
				return;
			}
			float sL = Mathf.Sign(rl.origYawRange);
			float sR = Mathf.Sign(rr.origYawRange);
			const float probe = 10f;
			Vector3 m = rl.MAt(sL * probe) + rr.MAt(sR * probe) - rl.MAt(0f) - rr.MAt(0f);
			float pDot = AccRoll(m) / probe;
			float rDot = AccYaw(m) / probe;
			float ar = alpha * (MathF.PI / 180f);
			float bdd = pDot * Mathf.Sin(ar) - rDot * Mathf.Cos(ar);
			float err = beta - betaPedalCmd;
			float y = 0f;
			if (Mathf.Abs(bdd) > 1E-4f)
			{
				float span = Mathf.Min(Mathf.Max(rl.max, 0f - rl.min), Mathf.Max(rr.max, 0f - rr.min));
				float lim = Mathf.Min(span, cap / Mathf.Max(Mathf.Abs(rDot), 1E-4f));
				float use = Mathf.Clamp01((Mathf.Abs(err) - 0.5f) / 2.5f);
				y = (0f - Mathf.Sign(err) * Mathf.Sign(bdd)) * lim * use * gate;
			}
			// servo-rate-limited so the allowed range cannot jump across zero in one frame when the error changes sign
			rudSideF = Mathf.MoveTowards(rudSideF, y, Mathf.Max(1f, rl.rate) * dt);
			rudSideDbg = rudSideF;
			float yLo = Mathf.Min(0f, rudSideF);
			float yHi = Mathf.Max(0f, rudSideF);
			ApplyYawInterval(rl, sL, yLo, yHi);
			ApplyYawInterval(rr, sR, yLo, yHi);
		}

		private static void ApplyYawInterval(SurfaceEffector se, float sgn, float yLo, float yHi)
		{
			float a = sgn * yLo;
			float b = sgn * yHi;
			float lo = Mathf.Min(a, b);
			float hi = Mathf.Max(a, b);
			se.lo = Mathf.Clamp(Mathf.Min(se.lo, lo), se.min, se.max);
			se.hi = Mathf.Clamp(Mathf.Max(se.hi, hi), se.min, se.max);
		}

		// 1.8.36 fly-by-wire ground steering. Outer loop: pedal -> yaw-rate command (GroundYawRateMax, limited to
		// GroundLateralAccel / V); pedal released -> yaw braking (zero-rate command) until |r| < 0.7 deg/s, then capture the
		// ground track (or the heading when nearly stopped) and the line through the CG, and hold that line: desired track =
		// captured track minus atan(cross-track / max(4 s x V, 25 m)) (<= 5 deg), desired heading = desired track (the heading
		// is lined up with the track: a touchdown crab or a weathervaning crosswind is steered out, the mains' side force
		// removes the drift). Inner loop: nose-wheel angle = kinematic feed-forward atan(r L / V) + yaw-rate P+I, limited to
		// what the tyre can use at that speed (NO's tyre saturates at 1/(0.2 + 0.01 v) deg of slip); in the ground roll the
		// rudders run the same yaw-rate error. Below 1.5 m/s the pedal steers the wheel directly.
		private void GroundSteer(float dt, bool mainsWow, bool noseWowWeight)
		{
			// contact rather than weight: in the ground roll the F-22E's nose leg carries only ~15-20 kN (about 0.02 m of
			// its 0.7 m travel), below WeightOnWheel(0.05), yet the tyre still steers with that load
			bool noseWow = false;
			bool anyContact = mainsWow;
			foreach (LandingGear g in gears)
			{
				if (g != null && g.WeightOnWheel(0.002f))
				{
					anyContact = true;
					if (g == NoseGear())
					{
						noseWow = true;
					}
				}
			}
			gsAnyContact = anyContact;
			// 4.1.5: the brake effector is armed tick by tick (GsBrakeAlloc); the light-nose hold flag for the log
			gsBrkOnPrev = gsBrkOn;
			gsBrkOn = false;
			gsLnDbg = false;
			gsBrkDbgL = -1f;
			gsBrkDbgR = -1f;
			// human pilots only: AI taxi logic is written for the stock pedal-to-wheel-angle steering
			bool on = Plugin.NoseWheelFbw && !isAI && Engaged && Mode != FcsMode.Off && anyContact;
			if (!anyContact && ac.rb != null)
			{
				// airborne: remember the ground track (0.5 s filter) so a touchdown captures the approach track (the runway),
				// not the track the mains have already dragged toward a crabbed heading
				Vector3 va = ac.rb.velocity;
				if (va.x * va.x + va.z * va.z > 400f)
				{
					float ca = Mathf.Atan2(va.x, va.z) * 57.29578f;
					gsChiAir = ((gsAirT > 0f) ? (gsChiAir + Mathf.DeltaAngle(gsChiAir, ca) * (dt / (0.5f + dt))) : ca);
					gsAirT += dt;
				}
				else
				{
					gsAirT = 0f;
				}
			}
			if (!on)
			{
				gsActive = false;
				gsStockPhase = false;
				gsBetaPrev = float.NaN;
				gsRPrev = float.NaN;
				gsRdotF = 0f;
				gsFyF = 0f;
				gsIndiOn = false;
				gsLnHold = false;
				gsInt = 0f;
				gsSinceContact += dt;
				if (gsSinceContact < 1.5f && Plugin.NoseWheelFbw && !isAI)
				{
					// a bounce: keep the captured line and track
					gsNwsDeg = 0f;
					gsRud = 0f;
					gsMainsContact = false;
					return;
				}
				gsHold = false;
				gsRudInt = 0f;
				gsBrakeT = 0f;
				gsRCmdMains = 0f;
				gsSurfV = Vector3.zero;
				gsSurfR = 0f;
				gsNwsDeg = 0f;
				gsRud = 0f;
				gsMainsContact = false;
				return;
			}
			LandingGear nose = NoseGear();
			float steerLock = ((nose != null) ? Mathf.Max(1f, Mathf.Abs(Acc.LG_steerLock(nose))) : 45f);
			if (gsLead < 0f)
			{
				gsLead = 5.9f;
				if (nose != null)
				{
					float best = float.MinValue;
					foreach (LandingGear g in gears)
					{
						if (g != null && g != nose)
						{
							best = Mathf.Max(best, Vector3.Dot(g.transform.position - nose.transform.position, -((Component)(object)ac).transform.forward));
						}
					}
					if (best > 1f && best < 30f)
					{
						gsLead = best;
					}
				}
			}
			Rigidbody rb = ac.rb;
			Vector3 v = ((rb != null) ? rb.velocity : Vector3.zero);
			// 2.4.14: everything on the ground relative to the surface under the wheels, as the game's tyres see it (a moving
			// carrier deck: its rigidbody's point velocity plus an AnimatedPhysicsSurface's velocity). With world velocity the
			// line hold captured the ship's track and steered the jet back toward the ship's heading on every release, and the
			// drift limit (on the aerodynamic sideslip) read the wind over the deck as a skid.
			GsSurface(nose);
			v -= gsSurfV;
			Vector3 vh = new Vector3(v.x, 0f, v.z);
			float V = vh.magnitude;
			Vector3 fwd = ((Component)(object)ac).transform.forward;
			float psi = Mathf.Atan2(fwd.x, fwd.z) * 57.29578f;
			float chi = ((V > 0.5f) ? (Mathf.Atan2(vh.x, vh.z) * 57.29578f) : psi);
			float r = ((rb != null) ? (Vector3.Dot(rb.angularVelocity, Vector3.up) * 57.29578f) : 0f);
			// slip over the surface (+ = moving right of the nose), for the drift limit
			gsSlip = ((V > 1f) ? Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), vh, Vector3.up) : 0f);
			Vector3 pos = ((rb != null) ? rb.worldCenterOfMass : ((Component)(object)ac).transform.position);
			float rLim = Mathf.Min(Plugin.GroundYawRateMax, Plugin.GroundLateralAccel / Mathf.Max(V, 0.5f) * 57.29578f);
			float ped = sy;
			float rc;
			// GroundStockMatch (2.4.12): stock steering, a drift limit, and the straight-line correction. While the pedal is in,
			// and after release until the yaw has stopped, the nose leg, the rudders and the main legs' differential brake get
			// the stock fly-by-wire's yaw - clamp(yawTightness x remap x (pedal - yaw rate) + yawWeathervaning x sideslip) with
			// the jet's own ControlsFilter parameters, raw pedal below its minSpeed - exactly as stock steers. On top of it the
			// drift limit lets the sideslip reach GroundDriftAllow and counters what goes past it (sideslip led by its rate,
			// faded in over GroundDriftFadeSpeed .. +10 m/s, so tight taxi turns are untouched). Once the yaw has stopped
			// (|r| < 0.7 deg/s, or 2 s after release) the FCS holds the straight line along the track until the pedal moves;
			// the touchdown capture holds the approach track the same way.
			bool sm = Plugin.GroundStockMatch;
			if (sm)
			{
				ped = gsPedRaw;
			}
			if (float.IsNaN(gsBetaPrev) || dt <= 0f)
			{
				gsBetaDot = 0f;
			}
			else
			{
				gsBetaDot += (Mathf.DeltaAngle(gsBetaPrev, gsSlip) / dt - gsBetaDot) * Mathf.Clamp01(dt / 0.08f);
			}
			gsBetaPrev = gsSlip;
			// 2.4.14 INDI: measured yaw acceleration and the nose tyre's lateral force now, through the same low-pass so
			// the increment is taken between synchronous values
			float aIndi = (dt > 0f) ? (dt / (Mathf.Max(0.005f, Plugin.GroundIndiTau) + dt)) : 0f;
			gsRdotF += (((float.IsNaN(gsRPrev) || dt <= 0f) ? 0f : ((r - gsRPrev) / dt)) - gsRdotF) * aIndi;
			gsRPrev = r;
			bool indiOk = GsNoseTyre(nose, noseWow, out float fyNow, out float alphaC, out float kTyre, out float muN, out float yawPerN);
			gsFyF += (fyNow - gsFyF) * aIndi;
			bool indi = Plugin.GroundIndi && indiOk && V >= 1.5f;
			if (indi)
			{
				gsYawPerNLast = yawPerN;
				gsLnT = 0f;
			}
			// 4.1.5 light nose: the nose tyre can't be used by INDI (no load, or off the ground) during a release stop or the hold
			// that INDI was flying - keep the wheel at INDI's last slip from the contact velocity
			// (at most GsLnMaxT s: the logged events last ~0.55 s; a deliberate nose-up - the take-off rotation - is left to 4.1.4)
			bool lnHold = Plugin.GroundIndiLightNoseHold && Plugin.GroundIndi && !indi && gsLnHold && V >= 1.5f && Mathf.Abs(ped) <= 0.05f && gsLnT < GsLnMaxT;
			if (lnHold)
			{
				gsLnT += dt;
			}
			if (Plugin.GroundIndiLightNoseHold && nose != null)
			{
				gsWheelDbg = Acc.LG_steerAngle(nose);
			}
			if (!indi && !lnHold)
			{
				gsLnHold = false;
			}
			gsIndiOn = false;
			gsSinceContact = 0f;
			if (!gsActive && !gsHold && gsAirT > 0.3f && V > 20f && Mathf.Abs(ped) <= 0.05f)
			{
				// touchdown: hold the approach track through the touchdown point straight away (drift / crab correction)
				gsHold = true;
				gsChiRef = ((gsSurfV.sqrMagnitude > 1f) ? chi : gsChiAir);
				gsLineY = 0f;
				gsLineP = pos;
				gsLinePG = ac.GlobalPosition();
				gsRCmd = r;
			}
			if (anyContact)
			{
				gsAirT = 0f;
			}
			if (Mathf.Abs(ped) > 0.05f || V < 1.5f)
			{
				rc = (sm ? r : (ped * rLim));
				gsHold = false;
				gsBrakeT = 0f;
			}
			else
			{
				rc = 0f;
				if (!gsHold)
				{
					gsBrakeT += dt;
					if (Mathf.Abs(r - gsSurfR) < 0.7f || gsBrakeT > 2f)
					{
						gsHold = true;
						gsChiRef = ((V > 3f) ? chi : psi);
						gsLineY = 0f;
						gsLineP = pos;
						gsLinePG = ac.GlobalPosition();
					}
				}
				if (gsHold)
				{
					gsChiRef += gsSurfR * dt;
					float cr = gsChiRef * (MathF.PI / 180f);
					Vector3 right = new Vector3(Mathf.Cos(cr), 0f, 0f - Mathf.Sin(cr));
					// cross-track from the surface-relative velocity (2.4.12-2.4.13: from the global position, which on a moving
					// deck is the ship's own travel)
					gsLineY += Vector3.Dot(vh, right) * dt;
					float chiDes = gsChiRef - Mathf.Clamp(Mathf.Atan2(gsLineY * Mathf.Clamp01(Plugin.GroundLineHold), Mathf.Max(4f * V, 25f)) * 57.29578f, -5f, 5f);
					float e = Mathf.DeltaAngle(psi, chiDes);
					// corrections are not limited by the pedal's comfort limit (GroundLateralAccel), only by GsHoldRate
					float rHold = Mathf.Min(GsHoldRate, Plugin.GroundYawRateMax);
					rc = Mathf.Clamp(GsKpsi * e, 0f - rHold, rHold) + gsSurfR;
				}
			}
			// yaw-acceleration limit on the command so the nose tyre is not asked for more than it can grip at the entry
			// stock-matched: the release braking asks for zero rate at once (no yaw-acceleration limit); the line hold and the
			// touchdown capture keep the limit
			float rCmdPrev = gsRCmd;
			gsRCmd = ((sm && !gsHold) ? r : Mathf.MoveTowards(gsRCmd, rc, GsYawAccel * dt));
			float rCmdDot = ((sm && !gsHold) || dt <= 0f) ? 0f : ((gsRCmd - rCmdPrev) / dt);
			// 2.4.14: the yaw-rate command for the nose-up roll on the mains (gear-down law, below). 2.4.12-2.4.13 handed it
			// gsRCmd, which in the stock phase is the measured yaw rate, so the rate error was zero and the pedal did nothing
			// (27 Oct 12:58 log, 381-386 s: 71 m/s on the mains, theta 3.8, full left / right pedal, yaw rate 1.9 throughout).
			// Pedal: the capped rate of 1.8.36-2.4.11 (GroundLateralAccel / V); released: stop the yaw; holding: the hold's.
			gsRCmdMains = ((sm && !gsHold) ? Mathf.MoveTowards(gsRCmdMains, ((Mathf.Abs(ped) > 0.05f) ? (ped * rLim) : 0f) + gsSurfR, GsYawAccel * dt) : gsRCmd);
			rc = gsRCmd;
			float err = rc - r;
			if (sm && (Mathf.Abs(ped) > 0.05f || V < 1.5f || !gsHold))
			{
				// stock steering (pedal in, and after release until the line is captured) with the drift limit on top
				float ys = GsStockYaw(ped, V);
				gsStockYaw = ys;
				gsStockPhase = true;
				gsInt = 0f;
				gsRudInt = 0f;
				gsNwsDeg = (noseWow ? (ys * steerLock) : 0f);
				if (indi && Mathf.Abs(ped) <= 0.05f)
				{
					// 2.4.14 release braking: the nose wheel stops the yaw by INDI (rudders and differential brake stay stock)
					// 4.1.5: with GroundIndiBrake the outside main's brake takes what lies beyond the nose tyre's grip
					float nuRel = (0f - Plugin.GroundIndiGain) * (r - gsSurfR);
					float nuNose = GsBrakeAlloc(nuRel, noseUsable: true, muN, yawPerN, V, dt, nose, ys, aIndi);
					gsNwsDeg = GsIndiWheel(nuRel, alphaC, kTyre, muN, yawPerN, steerLock, ped, V, nuNose);
				}
				else if (lnHold)
				{
					GsBrakeAlloc((0f - Plugin.GroundIndiGain) * (r - gsSurfR), noseUsable: false, 0f, 0f, V, dt, nose, ys, aIndi);
					gsNwsDeg = GsLightNoseWheel(nose, steerLock, ped, V);
				}
				else if (Mathf.Abs(ped) > 0.05f && V >= 1.5f)
				{
					GsPedalBrake(ped, nose, ys);
				}
				gsRud = ys;
				gsMainsContact = mainsContactNow(nose);
				gsActive = true;
				return;
			}
			gsStockPhase = false;
			if (indi)
			{
				// 2.4.14: INDI on the nose wheel - wanted yaw acceleration = GroundIndiGain x rate error + the command's slope
				gsInt = 0f;
				float nuHold = Plugin.GroundIndiGain * err + rCmdDot;
				float nuNose = GsBrakeAlloc(nuHold, noseUsable: true, muN, yawPerN, V, dt, nose, float.NaN, aIndi);
				gsNwsDeg = GsIndiWheel(nuHold, alphaC, kTyre, muN, yawPerN, steerLock, ped, V, nuNose);
			}
			else if (lnHold)
			{
				gsInt = 0f;
				GsBrakeAlloc(Plugin.GroundIndiGain * err + rCmdDot, noseUsable: false, 0f, 0f, V, dt, nose, float.NaN, aIndi);
				gsNwsDeg = GsLightNoseWheel(nose, steerLock, ped, V);
			}
			else if (noseWow && V >= 1.5f)
			{
				float ff = Mathf.Atan(rc * (MathF.PI / 180f) * gsLead / Mathf.Max(V, 1f)) * 57.29578f;
				float kp = Mathf.Clamp(GsKp0 / Mathf.Max(V, 3f), GsKpMin, GsKpMax);
				float lim = Mathf.Min(steerLock, GsLimK / Mathf.Max(V, 1f));
				// NO's tyre: lateral force = clamp(slip x (0.2 + 0.01 v), +-1) x muN, so it saturates at 1 / (0.2 + 0.01 v) deg of
				// slip. Feedback beyond that only drives the wheel into a skid and winds the integrator up.
				float sat = GsSatMargin / (0.2f + 0.01f * V);
				// stock-matched release braking: proportional only (bench: the integrator swung the yaw back 1-2.5 deg/s)
				gsInt = ((sm && !gsHold) ? 0f : Mathf.Clamp(gsInt + GsKi * kp * err * dt, 0f - sat, sat));
				float fb = Mathf.Clamp(kp * err + gsInt, 0f - sat, sat);
				float blend = Mathf.InverseLerp(1.5f, 3f, V);
				gsNwsDeg = Mathf.Lerp(ped * steerLock, Mathf.Clamp(ff + fb, 0f - lim, lim), blend);
			}
			else if (noseWow)
			{
				gsInt = 0f;
				gsNwsDeg = ped * steerLock;
			}
			else
			{
				gsInt = 0f;
				gsNwsDeg = 0f;
			}
			float rq = Mathf.InverseLerp(10f, 25f, V);
			gsRudInt = ((rq > 0f && !(sm && !gsHold)) ? Mathf.Clamp(gsRudInt + GsKri * err * dt * rq, -0.5f, 0.5f) : 0f);
			// below taxi speed the rudders have no authority: they show the pedal, as stock
			// stock-matched, pedal released: the rudders on the (released) pedal with the yaw-rate loop trimming on top
			gsRud = (sm ? Mathf.Clamp(ped + rq * (GsKr * err + gsRudInt), -1f, 1f) : Mathf.Lerp(ped, Mathf.Clamp(GsKr * err + gsRudInt, -1f, 1f), rq));
			gsMainsContact = mainsContactNow(nose);
			gsActive = true;
		}

		public bool gsMainsContact;

		// 2.4.14 INDI nose wheel state: filtered yaw acceleration (deg/s^2) and nose-tyre lateral force (N), the yaw-rate
		// command for the nose-up-on-the-mains phase, debug values
		private float gsRPrev = float.NaN;

		private float gsRdotF;

		private float gsFyF;

		public float gsRCmdMains;

		private bool gsIndiOn;

		private float gsNuDbg;

		private float gsWheelDbg;

		// any wheel touching the ground this tick (set by GroundSteer, which runs every tick)
		public bool gsAnyContact;

		// 2.4.14 INDI nose wheel. The game's nose tyre (LandingGear.FixedUpdate): lateral force = clamp(clamp(slip, +-10) x
		// response x (0.2 + 0.01 |wheelSpeed|), +-1) x mu N, perpendicular to the contact velocity, slip = angle from the wheel to
		// the contact velocity. Its yaw moment about the CG divided by the yaw inertia is the wheel's yaw effectiveness; all
		// of it is read from the game each tick (leg load, friction, the FCS's own CG and inertia), so loads, fuel and CG
		// move it with them. The virtual control is that force: wanted = force now (filtered) + (wanted yaw acceleration -
		// measured, filtered) / effectiveness, so the mains, the rudders, the brakes, gusts and model errors all show up in the
		// measured yaw acceleration rather than having to be modelled.
		private bool GsNoseTyre(LandingGear nose, bool noseWow, out float fy, out float alphaC, out float k, out float muN, out float yawPerN)
		{
			fy = 0f;
			alphaC = 0f;
			k = 0.2f;
			muN = 0f;
			yawPerN = 0f;
			Rigidbody body = ac.rb;
			if (nose == null || body == null || !noseWow)
			{
				return false;
			}
			Transform tr = ((Component)(object)ac).transform;
			Vector3 up = tr.up;
			Vector3 p = nose.transform.position;
			RaycastHit hit = Acc.LG_hit(nose);
			// the contact point when it is current (a floating-origin shift leaves last frame's hit point behind)
			if (hit.collider != null && (hit.point - p).sqrMagnitude < 9f)
			{
				p = hit.point;
			}
			Vector3 vc = body.GetPointVelocity(p) - SurfaceVelAt(hit, p, out float _);
			Vector3 vp = Vector3.ProjectOnPlane(vc, up);
			float sp = vp.magnitude;
			if (sp < 0.5f)
			{
				return false;
			}
			// + = the contact moving right of the nose
			alphaC = Vector3.SignedAngle(Vector3.ProjectOnPlane(tr.forward, up), vp, up);
			float delta = Acc.LG_steerAngle(nose);
			k = Mathf.Max(0.05f, Acc.LG_response(nose)) * (0.2f + 0.01f * Mathf.Abs(Acc.LG_wheelSpeed(nose)));
			muN = Mathf.Max(0f, Acc.LG_compForce(nose) + Acc.LG_dampForce(nose)) * Acc.LG_friction(nose);
			// + = force to the right of the contact velocity
			fy = Mathf.Clamp(Mathf.Clamp(delta - alphaC, -10f, 10f) * k, -1f, 1f) * muN;
			Vector3 right = Vector3.Cross(up, vp / sp);
			float iz = Ia[2, 2];
			yawPerN = ((iz > 1f) ? (Vector3.Dot(Vector3.Cross(p - cg, right), up) / iz * 57.29578f) : 0f);
			gsWheelDbg = delta;
			return muN > 1f && yawPerN > 1E-06f;
		}

		// nose-wheel angle for a wanted yaw acceleration nu (deg/s^2): the INDI force increment, turned into a slip angle through the
		// tyre's slope and kept inside GsSatMargin x its saturation slip (more slip only skids and scrubs), plus the contact
		// velocity's angle. Below 1.5-3 m/s it blends to the pedal, as before.
		// 4.1.5: nuNose = the nose tyre's share of the wanted yaw acceleration (absolute, deg/s^2) when the main brakes are in the
		// loop (GsBrakeAlloc); NaN = the nose alone, the 2.4.14 increment
		private float GsIndiWheel(float nu, float alphaC, float k, float muN, float yawPerN, float steerLock, float ped, float V, float nuNose = float.NaN)
		{
			float fDes = float.IsNaN(nuNose) ? (gsFyF + (nu - gsRdotF) / yawPerN) : (nuNose / yawPerN);
			float sMax = GsSatMargin / k;
			float slip = Mathf.Clamp(fDes / (muN * k), 0f - sMax, sMax);
			float d = Mathf.Clamp(alphaC + slip, 0f - steerLock, steerLock);
			gsNuDbg = nu;
			gsIndiOn = true;
			gsLnSlip = slip;
			gsLnHold = true;
			return Mathf.Lerp(ped * steerLock, d, Mathf.InverseLerp(1.5f, 3f, V));
		}

		// 4.1.5 light-nose hold: the nose tyre has no usable load (the leg extending - damping force negative - or the wheel off the
		// ground) in a stop or hold INDI was flying. The game slews the wheel toward its command even off the ground, so the 4.1.4
		// command (the stock law's angle, i.e. full opposite lock, then 0) ran the wheel away from the contact velocity and it met
		// the ground again ~25 deg out (Su-30 log 11 Nov 18:55, 25.90-26.42 s: 23.7 -> 0.4 deg; same code here). Here it keeps INDI's last slip from the
		// contact velocity, so on re-contact the tyre picks up where it left off; INDI resumes from the filtered force and yaw
		// acceleration, which followed the lost load all along.
		private float GsLightNoseWheel(LandingGear nose, float steerLock, float ped, float V)
		{
			float d = gsNwsDeg;
			if (GsNoseAlpha(nose, out float alphaC))
			{
				d = Mathf.Clamp(alphaC + gsLnSlip, 0f - steerLock, steerLock);
			}
			gsLnDbg = true;
			return Mathf.Lerp(ped * steerLock, d, Mathf.InverseLerp(1.5f, 3f, V));
		}

		// the nose contact velocity's angle from the nose (deg, + = moving right of the nose), contact or not
		private bool GsNoseAlpha(LandingGear nose, out float alphaC)
		{
			alphaC = 0f;
			Rigidbody body = ac.rb;
			if (nose == null || body == null)
			{
				return false;
			}
			Transform tr = ((Component)(object)ac).transform;
			Vector3 up = tr.up;
			Vector3 p = nose.transform.position;
			RaycastHit hit = Acc.LG_hit(nose);
			if (hit.collider != null && (hit.point - p).sqrMagnitude < 9f)
			{
				p = hit.point;
			}
			Vector3 vp = Vector3.ProjectOnPlane(body.GetPointVelocity(p) - gsSurfV, up);
			if (vp.sqrMagnitude < 0.25f)
			{
				return false;
			}
			alphaC = Vector3.SignedAngle(Vector3.ProjectOnPlane(tr.forward, up), vp, up);
			return true;
		}

		// 4.1.5 main brakes as the INDI's second yaw effector. The game (LandingGear.FixedUpdate) brakes a leg with
		// brakeStrength = Lerp(brakeStrength, b, 5 dt), b = (braked ? brake : 0) + Clamp01(yaw x 0.3 x differentialBrakeFactor) for
		// 3-30 m/s ground speed, as a force b x mu N along -contact velocity; the leg's total tyre force is clamped to mu N. Each
		// braked main's yaw effectiveness per unit brake is read from the game every tick (its load, friction, contact velocity,
		// the CG and yaw inertia). Allocation: the wanted total effector yaw acceleration next tick (the nose's and the brakes'
		// filtered values now + the INDI increment) goes to the nose tyre up to its grip; only what lies beyond it goes to the
		// brakes on the side that yaws the right way (never below what the game already applies there: your brake + the stock
		// differential term); the nose then takes the rest, crediting each brake's lagged value next tick, so a brake that is
		// releasing is covered by the nose instead of being held on. Authority GroundIndiBrakeMax, faded from
		// GroundIndiBrakeFullSpeed to GroundIndiBrakeZeroSpeed. Returns the nose's share (deg/s^2), NaN when the brakes are not
		// in the loop (the nose then flies the 2.4.14 increment). stockYaw: the yaw the main legs read this tick in the stock phase
		// (StockBrakeYaw), NaN = their own pedal input.
		private float GsBrakeAlloc(float nu, bool noseUsable, float muN, float yawPerN, float V, float dt, LandingGear nose, float stockYaw, float aIndi)
		{
			if (!Plugin.GroundIndiBrake || !Plugin.GroundIndi)
			{
				return float.NaN;
			}
			float vF = Plugin.GroundIndiBrakeFullSpeed;
			float vZ = Plugin.GroundIndiBrakeZeroSpeed;
			float fade = (vZ > vF + 0.01f) ? Mathf.Clamp01((vZ - V) / (vZ - vF)) : ((V <= vZ) ? 1f : 0f);
			float bMax = Plugin.GroundIndiBrakeMax * fade;
			Rigidbody body = ac.rb;
			float iz = Ia[2, 2];
			if (bMax <= 0f || body == null || iz <= 1f)
			{
				return float.NaN;
			}
			int n = gears.Count;
			if (gsBrkF == null || gsBrkF.Length != n)
			{
				gsBrkF = new float[n];
				gsBrkE = new float[n];
				gsBrkBase = new float[n];
				gsBrkTgt = new float[n];
				gsBrkOnPrev = false;
			}
			Transform tr = ((Component)(object)ac).transform;
			Vector3 up = tr.up;
			Vector3 rightAx = tr.right;
			bool init = !gsBrkOnPrev;
			float mb = 0f;
			float ePos = 0f;
			float eNeg = 0f;
			int legs = 0;
			for (int i = 0; i < n; i++)
			{
				gsBrkE[i] = 0f;
				gsBrkTgt[i] = -1f;
				LandingGear g = gears[i];
				if (g == null || g == nose || !Acc.LG_braked(g) || !g.WeightOnWheel(0.002f))
				{
					continue;
				}
				Vector3 p = g.transform.position;
				RaycastHit hit = Acc.LG_hit(g);
				if (hit.collider != null && (hit.point - p).sqrMagnitude < 9f)
				{
					p = hit.point;
				}
				Vector3 vp = Vector3.ProjectOnPlane(body.GetPointVelocity(p) - SurfaceVelAt(hit, p, out float _), up);
				float sp = vp.magnitude;
				float muNm = Mathf.Max(0f, Acc.LG_compForce(g) + Acc.LG_dampForce(g)) * Acc.LG_friction(g);
				if (sp < 0.5f || muNm <= 1f)
				{
					continue;
				}
				float e = Vector3.Dot(Vector3.Cross(p - cg, -vp / sp), up) / iz * 57.29578f * muNm;
				ControlInputs ci = Acc.LG_inputs(g);
				float yawIn = float.IsNaN(stockYaw) ? ((ci != null) ? ci.yaw : 0f) : stockYaw;
				float dbf = Acc.LG_dbf(g);
				float gsp = Acc.LG_groundSpeed(g);
				float stock = (dbf != 0f && gsp > 3f && gsp < 30f) ? Mathf.Clamp01(yawIn * 0.3f * dbf) : 0f;
				gsBrkBase[i] = ((ci != null) ? ci.brake : 0f) + stock;
				float bs = Acc.LG_brakeStrength(g);
				gsBrkF[i] = init ? bs : (gsBrkF[i] + (bs - gsBrkF[i]) * aIndi);
				gsBrkE[i] = e;
				mb += gsBrkF[i] * e;
				if (e > 0f)
				{
					ePos += e;
				}
				else
				{
					eNeg += e;
				}
				legs++;
			}
			if (legs == 0)
			{
				return float.NaN;
			}
			// wanted total effector yaw acceleration next tick; the nose tyre's part of the filtered state uses its last
			// effectiveness, so a nose that has just gone light is counted as it fades (as the measured yaw acceleration sees it)
			float mTot = gsFyF * (noseUsable ? yawPerN : gsYawPerNLast) + mb + (nu - gsRdotF);
			float nCap = noseUsable ? (muN * yawPerN) : 0f;
			float mbT = mTot - Mathf.Clamp(mTot, 0f - nCap, nCap);
			float eSide = (mbT > 0f) ? ePos : eNeg;
			float cmd = (eSide != 0f && mbT * eSide > 0f) ? Mathf.Min(bMax, mbT / eSide) : 0f;
			float lerpK = Mathf.Clamp01(5f * dt);
			float mbP = 0f;
			for (int i = 0; i < n; i++)
			{
				float e = gsBrkE[i];
				if (e == 0f)
				{
					continue;
				}
				float tgt = Mathf.Max(gsBrkBase[i], (e * mbT > 0f) ? cmd : 0f);
				gsBrkTgt[i] = tgt;
				mbP += (gsBrkF[i] + (tgt - gsBrkF[i]) * lerpK) * e;
				float side = Vector3.Dot(gears[i].transform.position - cg, rightAx);
				if (side < 0f)
				{
					gsBrkDbgL = tgt;
				}
				else
				{
					gsBrkDbgR = tgt;
				}
			}
			gsBrkOn = true;
			return noseUsable ? (mTot - mbP) : float.NaN;
		}

		// 4.1.5 pedal brake (drift help): while the pedal is in, the main whose brake yaws the jet the pedal's way gets
		// |pedal| x GroundPedalBrake (never below what the game applies there: your brake + the stock differential term). A
		// braked tyre also gives up side grip (the game clamps each tyre's total force to mu N), so the mains break loose.
		private void GsPedalBrake(float ped, LandingGear nose, float stockYaw)
		{
			float k = Plugin.GroundPedalBrake;
			Rigidbody body = ac.rb;
			if (k <= 0f || body == null)
			{
				return;
			}
			int n = gears.Count;
			if (gsBrkF == null || gsBrkF.Length != n)
			{
				gsBrkF = new float[n];
				gsBrkE = new float[n];
				gsBrkBase = new float[n];
				gsBrkTgt = new float[n];
			}
			Transform tr = ((Component)(object)ac).transform;
			Vector3 up = tr.up;
			Vector3 rightAx = tr.right;
			bool any = false;
			for (int i = 0; i < n; i++)
			{
				gsBrkTgt[i] = -1f;
				LandingGear g = gears[i];
				if (g == null || g == nose || !Acc.LG_braked(g) || !g.WeightOnWheel(0.002f))
				{
					continue;
				}
				Vector3 p = g.transform.position;
				RaycastHit hit = Acc.LG_hit(g);
				if (hit.collider != null && (hit.point - p).sqrMagnitude < 9f)
				{
					p = hit.point;
				}
				Vector3 vp = Vector3.ProjectOnPlane(body.GetPointVelocity(p) - SurfaceVelAt(hit, p, out float _), up);
				float sp = vp.magnitude;
				if (sp < 0.5f)
				{
					continue;
				}
				// sign of the yaw this leg's brake gives (+ = nose right)
				float e = Vector3.Dot(Vector3.Cross(p - cg, -vp / sp), up);
				ControlInputs ci = Acc.LG_inputs(g);
				float dbf = Acc.LG_dbf(g);
				float gsp = Acc.LG_groundSpeed(g);
				float stock = (dbf != 0f && gsp > 3f && gsp < 30f) ? Mathf.Clamp01(stockYaw * 0.3f * dbf) : 0f;
				float bse = ((ci != null) ? ci.brake : 0f) + stock;
				float tgt = (e * ped > 0f) ? Mathf.Max(bse, Mathf.Abs(ped) * k) : bse;
				gsBrkTgt[i] = tgt;
				if (Vector3.Dot(g.transform.position - cg, rightAx) < 0f)
				{
					gsBrkDbgL = tgt;
				}
				else
				{
					gsBrkDbgR = tgt;
				}
				any = true;
			}
			gsBrkOn = any;
		}

		// the brake GsBrakeAlloc wants on this leg this tick (total, as the game sums it), for Hooks.GearPrefix
		public bool MainBrakeTarget(LandingGear g, out float target)
		{
			target = 0f;
			if (!gsActive || !gsBrkOn || gsBrkTgt == null)
			{
				return false;
			}
			int i = gears.IndexOf(g);
			if (i < 0 || i >= gsBrkTgt.Length || gsBrkTgt[i] < 0f)
			{
				return false;
			}
			target = gsBrkTgt[i];
			return true;
		}

		private bool gsBrkOn;

		private bool gsBrkOnPrev;

		private float[] gsBrkF;

		private float[] gsBrkE;

		private float[] gsBrkBase;

		private float[] gsBrkTgt;

		private float gsBrkDbgL = -1f;

		private float gsBrkDbgR = -1f;

		private bool gsLnHold;

		private float gsLnSlip;

		private bool gsLnDbg;

		private float gsYawPerNLast;

		private float gsLnT;

		internal static float GsLnMaxT = 1f;

		// 2.4.14: the velocity (and yaw rate, deg/s) of the surface the wheels stand on - a moving deck's rigidbody plus an
		// AnimatedPhysicsSurface, the same two the game's LandingGear takes off the contact velocity. Zero on fixed ground.
		public Vector3 gsSurfV;

		public float gsSurfR;

		private float gsSlip;

		private Collider gsSurfCol;

		private AnimatedPhysicsSurface gsSurfAps;

		private Vector3 SurfaceVelAt(RaycastHit hit, Vector3 p, out float yawRate)
		{
			yawRate = 0f;
			Collider col = hit.collider;
			if (col == null)
			{
				return Vector3.zero;
			}
			Vector3 vs = Vector3.zero;
			Rigidbody srb = col.attachedRigidbody;
			if (srb != null)
			{
				vs += srb.GetPointVelocity(p);
				yawRate = Vector3.Dot(srb.angularVelocity, Vector3.up) * 57.29578f;
			}
			if (col != gsSurfCol)
			{
				gsSurfCol = col;
				gsSurfAps = col.gameObject.GetComponent<AnimatedPhysicsSurface>();
			}
			if (gsSurfAps != null)
			{
				vs += gsSurfAps.GetVelocity();
			}
			return vs;
		}

		// the surface under the wheels this tick: a main leg in contact first, else the nose; kept through a bounce
		private void GsSurface(LandingGear nose)
		{
			LandingGear pick = null;
			foreach (LandingGear g in gears)
			{
				if (g != null && g.WeightOnWheel(0.002f) && (pick == null || pick == nose))
				{
					pick = g;
				}
			}
			if (pick == null)
			{
				return;
			}
			RaycastHit hit = Acc.LG_hit(pick);
			Vector3 p = pick.transform.position;
			if (hit.collider != null && (hit.point - p).sqrMagnitude < 16f)
			{
				p = hit.point;
			}
			gsSurfV = SurfaceVelAt(hit, p, out gsSurfR);
		}

		private bool mainsContactNow(LandingGear nose)
		{
			foreach (LandingGear g in gears)
			{
				if (g != null && g != nose && g.WeightOnWheel(0.002f))
				{
					return true;
				}
			}
			return false;
		}

		private static System.Reflection.FieldInfo fCfFbw, fCfMinSpeed, fCfMinAlt, fFbwEnabled, fFbwYawT, fFbwYawWv, fFbwCorner;

		private static bool fCfFailed;

		// the stock fly-by-wire's yaw (FlyByWire.Filter, read from the game's IL) for pedal ped, plus the drift limit
		private float GsStockYaw(float ped, float V)
		{
			float y = ped;
			try
			{
				if (stockCf != null && !fCfFailed)
				{
					if (fCfFbw == null)
					{
						Type t = typeof(ControlsFilter);
						fCfFbw = HarmonyLib.AccessTools.Field(t, "flyByWire");
						fCfMinSpeed = HarmonyLib.AccessTools.Field(t, "minSpeed");
						fCfMinAlt = HarmonyLib.AccessTools.Field(t, "minAlt");
						Type ft = fCfFbw.FieldType;
						fFbwEnabled = HarmonyLib.AccessTools.Field(ft, "Enabled");
						fFbwYawT = HarmonyLib.AccessTools.Field(ft, "yawTightness");
						fFbwYawWv = HarmonyLib.AccessTools.Field(ft, "yawWeathervaning");
						fFbwCorner = HarmonyLib.AccessTools.Field(ft, "cornerSpeed");
					}
					float spd = ac.speed;
					bool below = spd < (float)fCfMinSpeed.GetValue(stockCf) && ac.radarAlt < (float)fCfMinAlt.GetValue(stockCf);
					object fbw = fCfFbw.GetValue(stockCf);
					if (!below && fbw != null && (bool)fFbwEnabled.GetValue(fbw))
					{
						float T = (float)fFbwYawT.GetValue(fbw);
						float wv = (float)fFbwYawWv.GetValue(fbw);
						float corner = Mathf.Max(1f, (float)fFbwCorner.GetValue(fbw));
						float x = spd * spd * ac.airDensity / (corner * corner * 1.225f);
						float remap = 1f / Mathf.Max(x, 1f);
						Rigidbody body = ac.rb;
						float wy = (body != null) ? body.transform.InverseTransformDirection(body.angularVelocity).y : 0f;
						float k = T * remap * (wy - ped);
						if (wv > 0f && body != null)
						{
							Transform tr = ((Component)(object)ac).transform;
							k -= wv * Vector3.SignedAngle(Vector3.ProjectOnPlane(tr.forward, tr.up), Vector3.ProjectOnPlane(body.velocity, tr.up), tr.up);
						}
						y = Mathf.Clamp(0f - k, -1f, 1f);
					}
				}
			}
			catch (Exception e)
			{
				fCfFailed = true;
				Plugin.Log.LogWarning("ground steering: stock fly-by-wire parameters not readable, using the raw pedal (" + e.Message + ")");
				y = ped;
			}
			// drift limit: sideslip (led by its rate) past GroundDriftAllow is countered; beta > 0 = velocity right of the nose,
			// so a nose-right skid (beta < 0) is met with left yaw
			float fade = Mathf.InverseLerp(Plugin.GroundDriftFadeSpeed, Plugin.GroundDriftFadeSpeed + 10f, V);
			if (fade > 0f && Plugin.GroundDriftGain > 0f)
			{
				float bp = gsSlip + Plugin.GroundDriftLead * gsBetaDot;
				float allow = Mathf.Max(0f, Plugin.GroundDriftAllow);
				float over = bp - Mathf.Clamp(bp, 0f - allow, allow);
				y = Mathf.Clamp(y + fade * Plugin.GroundDriftGain * over, -1f, 1f);
			}
			return y;
		}

		// the main legs' differential brake reads the stock fly-by-wire's yaw in stock, not the pedal
		public bool StockBrakeYaw(out float yaw)
		{
			yaw = gsStockYaw;
			return gsActive && gsStockPhase;
		}

		public bool NoseSteerOverride(out float yaw)
		{
			LandingGear nose = NoseGear();
			float steerLock = ((nose != null) ? Mathf.Max(1f, Mathf.Abs(Acc.LG_steerLock(nose))) : 45f);
			yaw = Mathf.Clamp(gsNwsDeg / steerLock, -1f, 1f);
			return gsActive;
		}

		internal static float GsKpsi = 1f;

		internal static float GsHoldRate = 4f;

		internal static float GsYawAccel = 20f;

		internal static float GsSatMargin = 1.2f;

		internal static float GsKp0 = 15f;

		internal static float GsKpMin = 0.15f;

		internal static float GsKpMax = 3f;

		internal static float GsKi = 1f;

		internal static float GsLimK = 300f;

		internal static float GsKr = 0.1f;

		internal static float GsKphi = 3f;

		internal static float GsPhiFull = 10f;

		internal static float GsKri = 0.1f;

		private static float RudderAuthAt(float a)
		{
			float num = 1f - Mathf.Clamp01((Mathf.Abs(a) - Plugin.RudderFadeStart) / Mathf.Max(1f, Plugin.RudderFadeEnd - Plugin.RudderFadeStart));
			if (RudderRevBack)
			{
				num = Mathf.Max(num, Mathf.InverseLerp(120f, 140f, Mathf.Abs(a)));
			}
			return num;
		}

		private float AccRoll(Vector3 m)
		{
			return (IaInv[0, 0] * m.x + IaInv[0, 2] * m.z) * 57.29578f;
		}

		private float AccYaw(Vector3 m)
		{
			return (IaInv[2, 0] * m.x + IaInv[2, 2] * m.z) * 57.29578f;
		}

		private void AsymStep(float dt, float rho, Quaternion rootRot, ref EvalContext ctx, Vector3 wdF, Vector3 mF)
		{
			DecelStep(dt);
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
			// 2.4.1: entering the engine-out / damaged-engine state, the cap from the last sweep was judged with the thrust
			// term in (and below AsymMinIAS there is no next sweep to replace it): it is replaced by the fixed cap at once
			// and a fresh sweep (thrust term out) is started
			bool fixedNow = (engOut || engAsym) && EngineOutFixedCap;
			if (fixedNow && !engFixedPrev && asymValid)
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
				asymCapPos = (CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax, ref cplLowPos) : AsymCap.Find(asymAlphas, cplActive ? cplU : asymU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax));
				asymCapNeg = (CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax, ref cplLowNeg) : AsymCap.Find(asymAlphas, cplActive ? cplU : asymU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax));
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
					CplFill();
					if (CplDbgT >= 0f && cplClock >= CplDbgT && cplLoaded)
					{
						// bench diagnostic: the whole curve of one sweep (demand and reserve each way per AoA)
						CplDbgT = -1f;
						System.Text.StringBuilder sb = new System.Text.StringBuilder();
						sb.AppendFormat(CultureInfo.InvariantCulture, "CPLDBG t {0:F2} ias {1:F1} alpha {2:F1} tw {3:F2} dy {4:F3} kq {5:F2}\n", cplClock, ias, alpha, tw, asymDy, qPredK);
						for (int iD = 0; iD < asymAlphas.Length; iD++)
						{
							sb.AppendFormat(CultureInfo.InvariantCulture, "  a {0,4:F0}  cplU {1,6:F2}  rp {2,7:F1}  rn {3,7:F1}  | one-axis U {4,6:F2}  Ar {5,7:F1} Ay {6,7:F1} Cr {7,6:F1} Cy {8,6:F1}\n", asymAlphas[iD], cplU[iD], cplRp[iD], cplRn[iD], asymU[iD], asymAr[iD], asymAy[iD], asymCr[iD], asymCy[iD]);
						}
						Console.Write(sb.ToString());
					}
				}
				asymCapPos = (CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax, ref cplLowPos) : AsymCap.Find(asymAlphas, cplActive ? cplU : asymU, asymAlphas.Length, 1, Plugin.AoAPos, Mathf.Min(Plugin.AsymCapFloor, Plugin.AoAPos), AsymCapUMax));
				asymCapNeg = (CplTop ? AsymCap.FindTop(asymAlphas, cplU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax, ref cplLowNeg) : AsymCap.Find(asymAlphas, cplActive ? cplU : asymU, asymAlphas.Length, -1, Plugin.AoANeg, 0f - Mathf.Min(Plugin.AsymCapFloorNeg, 0f - Plugin.AoANeg), AsymCapUMax));
				if (engOut && EngineOutFixedCap)
				{
					asymCapPos = Mathf.Min(asymCapPos, Plugin.EngineOutAoACap);
					asymCapNeg = Mathf.Max(asymCapNeg, 0f - Plugin.EngineOutAoACap);
				}
				else if (engAsym && EngineOutFixedCap)
				{
					// 2.4.1: a weak (damaged, running) engine gets its own fixed cap, not the dynamic thrust cap
					asymCapPos = Mathf.Min(asymCapPos, Plugin.EngineDamagedAoACap);
					asymCapNeg = Mathf.Max(asymCapNeg, 0f - Plugin.EngineDamagedAoACap);
				}
				asymCapPitch = 999f;
				float capGate = Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / CoupHeldDySpan);
				if (Plugin.AsymPitchMargin > 0f && capGate > 0.5f && (asymCapPos < Plugin.AoAPos - 0.5f || (cplNoTv && cplLoaded)) && Ia[1, 1] > 1f)
				{
					float tvDn = 0f;
					// F-22E 4.2.0: on the no-TVC path the held-load figure has the thrust at the nozzles' present angle in its load already
					if (tvc.active && tvc.M != null && !cplPitchGate)
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
					float kqP = 1f;
					float margP = cplPitchGate ? AsymPitchMarginEff(out kqP) : Plugin.AsymPitchMargin;
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
						float mDn = cplPitchGate ? (kqP * (0f - cplDn[ia]) - cplTvFreeDn) : (0f - (asymNetDn[ia] + tvDn));
						if (mDn < margP)
						{
							// F-22E 4.2.0 (as the Ifrit 1.2.0): on the no-TVC path, when the held-pitch margin is short already at the lowest AoA,
							// cutting AoA buys no nose-down authority: no pitch cap then, as when the roll demand is over everywhere (FindTop)
							if (cplPitchGate && AsymPitchNoGainSkip && float.IsNaN(prevM))
							{
								break;
							}
							float aP = (float.IsNaN(prevM) || prevM <= mDn) ? aA : Mathf.Lerp(prevA, aA, (prevM - margP) / (prevM - mDn));
							asymCapPitch = Mathf.Max(AsymPitchCapFloor, aP - AsymPitchCapLead);
							break;
						}
						prevA = aA;
						prevM = mDn;
					}
					asymCapPos = Mathf.Min(asymCapPos, asymCapPitch);
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
					Plugin.Log.LogInfo($"F-22E FCS: asymmetric-load check at {ias:F0} m/s IAS - lateral CG offset {asymDy * 100f:F1} cm, lateral usage at 30/45/60 deg AoA " + $"{AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, 30f):F2}/{AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, 45f):F2}/{AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, 60f):F2} " + $"(cap engages above 1.00); roll/yaw authority there {AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 30f):F0}/{AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 30f):F0}, " + $"{AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 45f):F0}/{AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 45f):F0}, " + $"{AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 60f):F0}/{AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 60f):F0} deg/s^2; " + $"load accel at 45 deg {AsymCap.Interp(asymAlphas, asymAr, asymAlphas.Length, 45f):F1}/{AsymCap.Interp(asymAlphas, asymAy, asymAlphas.Length, 45f):F1} deg/s^2 (deadband {Plugin.AsymDeadband:F1}); AoA cap {asymCapPos:F1}/{asymCapNeg:F1} deg.");
				}
			}
			if (asymValid)
			{
				CapFilterStep(dt);
				float upRate = Plugin.AsymRelax * (1f + Mathf.Max(0f, Plugin.AsymRelaxBoost) * (1f - Mathf.Clamp01(AsymCap.Interp(asymAlphas, cplActive ? cplU : asymU, asymAlphas.Length, asymCapPosEff))));
				float tightR = Plugin.AsymTighten * ((engOut || engAsym) ? Mathf.Max(1f, EngOutTightenK) : 1f);
				// 3.0.3 (AsymCapGapTighten): a cap that has to come down a long way (a part shot off) comes down faster, up to AsymCapGapMax x
				// the tighten rate, in proportion to the gap over AsymCapGapRef (3) deg. At 10 deg/s a 65 -> 12 deg drop took 5 s; 5 Nov 21:09
				// F-22E log, right stab shot off at 260.6 s with the fins gone: full aft 2 s later reached 25 deg AoA under a cap still at 45,
				// and the lone stab's load rolled the jet to 78 deg of bank against full opposite roll.
				float gapP = asymCapPosEff - asymCapFilt;
				float gapN = asymCapFiltNeg - asymCapNegEff;
				float kGP = AsymCapGapTighten ? Mathf.Clamp(gapP / Mathf.Max(1f, AsymCapGapRef), 1f, AsymCapGapMax) : 1f;
				float kGN = AsymCapGapTighten ? Mathf.Clamp(gapN / Mathf.Max(1f, AsymCapGapRef), 1f, AsymCapGapMax) : 1f;
				asymCapPosEff = AsymCap.Slew(asymCapPosEff, asymCapFilt, 1, tightR * kGP, upRate, dt);
				asymCapNegEff = AsymCap.Slew(asymCapNegEff, asymCapFiltNeg, -1, tightR * kGN, upRate, dt);
				asymUNow = AsymCap.Interp(asymAlphas, asymU, asymAlphas.Length, alpha);
				asymArNow = AsymCap.Interp(asymAlphas, asymAr, asymAlphas.Length, alpha);
				asymAyNow = AsymCap.Interp(asymAlphas, asymAy, asymAlphas.Length, alpha);
				asymCrNow = AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, alpha);
				asymCyNow = AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, alpha);
				if (cplActive)
				{
					cplUNow = AsymCap.Interp(asymAlphas, cplU, asymAlphas.Length, alpha);
					cplRpNow = AsymCap.Interp(asymAlphas, cplRp, asymAlphas.Length, alpha);
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
					Plugin.Log.LogInfo(flag ? $"F-22E FCS: asymmetric load - lateral CG offset {asymDy * 100f:F1} cm, AoA cap {asymCapPos:F1}/{asymCapNeg:F1} deg at {ias:F0} m/s IAS ({((cplActive && cplLoaded) ? (cplTvMode ? "coupled" : "coupled, no TVC") : "one-axis")} lateral demand at 30 deg: {AsymCap.Interp(asymAlphas, cplActive ? cplU : asymU, asymAlphas.Length, 30f):F2}, at 60: {AsymCap.Interp(asymAlphas, cplActive ? cplU : asymU, asymAlphas.Length, 60f):F2}; load accel at 45 deg {AsymCap.Interp(asymAlphas, asymAr, asymAlphas.Length, 45f):F1} roll / {AsymCap.Interp(asymAlphas, asymAy, asymAlphas.Length, 45f):F1} yaw, authority there {AsymCap.Interp(asymAlphas, asymCr, asymAlphas.Length, 45f):F0} / {AsymCap.Interp(asymAlphas, asymCy, asymAlphas.Length, 45f):F0} deg/s^2)" : $"F-22E FCS: asymmetric-load AoA cap released (lateral CG offset {asymDy * 100f:F1} cm)");
				}
			}
		}

		// ---- 1.8.45: IAS-decay prediction and asymmetric-cap filter ----
		public float iasDotF;

		public float qPredK = 1f;

		public float latPredK = 1f;

		public float asymCapFilt = 999f;

		public float asymCapFiltNeg = -999f;

		private float iasPrevD = -1f;

		private readonly float[] capHist = new float[3];

		private readonly float[] capHistN = new float[3];

		private int capHistI;

		private float capMed = 999f;

		private float capMedNeg = -999f;

		internal static float BrakeRefIas => Plugin.YawBrakeRefKmh / 3.6f;   // 1.9.2: 270 km/h (was 280): the reference manoeuvre's IAS

		// 1.9.2: below YawMarginIasHi the yaw braking margin grows smoothly (smoothstep) to YawMarginLow x at YawMarginIasLo
		internal static float YawMarginIasHi => Plugin.YawMarginStartKmh;

		internal static float YawMarginIasLo => Plugin.YawMarginFullKmh;

		internal static float YawMarginLow => Plugin.YawMarginLowIas;

		internal static float BrakeSoftMin = 0.12f;      // soft-min width, fraction of the limits' mean

		public float yawMarginDbg = 1f;

		public float yawMidDbg = 1f;

		internal static float RollStopTau = 0.5f;

		internal static float ProtOnsetGain = 8f;

		internal static float ProtFlagBand = 1.5f;

		private float rollStopAF = -1f;

		// C1-smooth minimum: equals min(a, b) once they differ by more than k, at most k/4 below it where they cross
		private static float SoftMin(float a, float b, float k)
		{
			if (k <= 1e-4f)
			{
				return Mathf.Min(a, b);
			}
			float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
			return Mathf.Lerp(b, a, h) - k * h * (1f - h);
		}

		internal static float DecelTau = 0.25f;          // s, IAS-rate filter

		internal static float DecelLook => Plugin.DecelLookahead;   // s, how far ahead the decay is projected

		internal static float Decel0 => Plugin.DecelStart;          // m/s^2: below this, no anticipation

		internal static float Decel1 => Plugin.DecelFull;           // m/s^2: full weight from here (quadratic in between)

		internal static float DecelKMin = 0.35f;

		internal static float AsymResMaxFrac = 0.8f;

		// 1.8.45: demand at the cap (was 1.0). In-game, with the light mixed load (6.5 cm lateral CG offset, 60-110 m/s)
		// the demand rises ~0.025 per degree above the cap, so 1.08 is ~3 deg more AoA there; heavier loads rise faster.
		internal static float AsymCapUMax => Plugin.AsymCapDemand;

		internal static float AsymCapTauUp => Plugin.AsymCapSmoothing;

		internal static float AsymCapTauDown = 0.3f;

		internal static bool LatBrakeFloor => Plugin.HighAoABrakeFloor;

		internal static float LatBrakeA0 = 25f;

		internal static float LatBrakeA1 = 35f;

		internal static float LatBrakeMinRate = 8f;

		private void DecelStep(float dt)
		{
			if (dt <= 0f)
			{
				return;
			}
			if (iasPrevD < 0f || !Engaged)
			{
				iasPrevD = ias;
				iasDotF = 0f;
				qPredK = 1f;
				return;
			}
			float d = (ias - iasPrevD) / dt;
			iasPrevD = ias;
			iasDotF += (Mathf.Clamp(d, -80f, 80f) - iasDotF) * Mathf.Clamp01(dt / Mathf.Max(0.02f, DecelTau));
			float dec = Mathf.Max(0f, 0f - iasDotF);
			float w = Mathf.Clamp01((dec - Decel0) / Mathf.Max(0.1f, Decel1 - Decel0));
			float iasP = Mathf.Max(ias - dec * w * w * DecelLook, 0.1f * ias);
			float r = (ias > 1f) ? (iasP / ias) : 1f;
			qPredK = Mathf.Clamp(r * r, DecelKMin, 1f);
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

		private static float Med3(float a, float b, float c)
		{
			return Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
		}

		internal static bool AsymCapGapTighten = true;

		internal static float AsymCapGapRef = 3f;

		internal static float AsymCapGapMax = 4f;

		private void CapFilterStep(float dt)
		{
			// tightening passes quickly, relaxing slowly: noise can't lift the cap, a real drop is followed in ~0.3 s
			float kU = Mathf.Clamp01(dt / Mathf.Max(0.05f, AsymCapTauUp));
			float kD = Mathf.Clamp01(dt / Mathf.Max(0.05f, AsymCapTauDown));
			asymCapFilt += (capMed - asymCapFilt) * ((capMed < asymCapFilt) ? kD : kU);
			asymCapFiltNeg += (capMedNeg - asymCapFiltNeg) * ((capMedNeg > asymCapFiltNeg) ? kD : kU);
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
			asymDy = ((num > 0) ? Vector3.Dot(cg - zero / num, rhs) : 0f);
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
		}

		// 2.5.15: the whole airframe's aero moment (aero frame: x roll, z yaw) at a hypothetical AoA + sideslip, the surfaces at
		// their present deflection. Same per-part law as AsymMomentAt (the game's AeroJob_Math per AeroPart, the FCS's foil tables).
		private Vector3 SlipMomentAt(float aDeg, float bDeg, float rho, Quaternion rootRot, ref EvalContext ctx)
		{
			float num = Mathf.Max(tas, 1f);
			float fa = aDeg * (MathF.PI / 180f);
			float fb = bDeg * (MathF.PI / 180f);
			Vector3 vector = rootRot * new Vector3(Mathf.Sin(fb) * num, (0f - Mathf.Sin(fa)) * Mathf.Cos(fb) * num, Mathf.Cos(fa) * Mathf.Cos(fb) * num);
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
					Quaternion rotation = transform.rotation;
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
			Vector3 result = new Vector3(0f - vector3.z, 0f - vector3.x, vector3.y);
			for (int i = 0; i < surfaces.Count; i++)
			{
				if (surfaces[i].active)
				{
					result += surfaces[i].EvalFlow(surfaces[i].now, vector, rho, ref ctx);
				}
			}
			return result;
		}

		// 2.31.2: the whole airframe's aero moment (aero frame: x roll, z yaw) at a hypothetical CG air velocity vAir and body rotation
		// w (world, rad/s), every aero part evaluated as the game's AeroJob_Math does it (the bench plant's Forces): the flow at the part
		// vAir + w x arm, lift and drag from the part's table, the parasitic drag, the transonic factor, the stabs' downwash on the root
		// body's flow, the per-part force clamp (30 x mass x |v|), and the flap-camber increment as CamberForce applies it. Control
		// surfaces sit where their transforms are now; no thrust. SlipMomentAt (2.5.15) differs: uniform flow, surfaces through their
		// effector model, no clamp - it over-reads the sideslip stiffness 2-15x above 40 deg AoA (claude/Passive_lateral_stiffness_finding.md).
		private Vector3 AirframeMomentAt(Vector3 vAir, Vector3 w, float rho, Quaternion rootRot)
		{
			Vector3 vRootRef = vAir + Vector3.Cross(w, ac.rb.worldCenterOfMass - cg);
			Vector3 m = Vector3.zero;
			foreach (AeroPart ap in aeroParts)
			{
				if (ap == null || ap.rb == null || ap.IsDetached())
				{
					continue;
				}
				Transform lt = Acc.AP_liftNormal(ap);
				if (lt == null)
				{
					continue;
				}
				Vector3 com = ap.rb.worldCenterOfMass;
				Vector3 v = vAir + Vector3.Cross(w, com - cg);
				float v2 = v.sqrMagnitude;
				if (v2 < 1f)
				{
					continue;
				}
				float vmag = Mathf.Sqrt(v2);
				Quaternion lr = lt.rotation;
				Vector3 vf = Downwash.IsStabName(ap.name) ? Downwash.Apply(v, rootRot, vRootRef) : v;
				Vector3 v6 = Quaternion.Inverse(lr) * vf;
				float a = Mathf.Atan2(v6.y, v6.z);
				float cl, cd;
				if (foils.TryGetValue(ap, out var foil) && foil != null)
				{
					foil.Get(a, out cl, out cd);
				}
				else
				{
					cl = 1.8f * Mathf.Sin(5f * a);
					cd = 1.5f * (1f - Mathf.Cos(2f * a)) + 0.02f;
				}
				float area = Acc.AP_wingArea(ap), eff = Acc.AP_wingEff(ap), dA = Acc.AP_dragArea(ap);
				float n8 = (dA + area * 0.1f) * (1f - eff);
				float n9 = 0.5f * rho * v2 * 0.5f * (dA + n8);
				float n10 = cd * rho * v2 * 0.5f * area * eff;
				float tr = DragReliefMath.Transonic(vmag / DragReliefMath.SpeedOfSound(com.y));
				Vector3 vfHat = vf.normalized, vHat = v / vmag;
				Vector3 f = (-vfHat * n10 - vHat * n9) * tr;
				// the buffed profile's drag relief (DragRelief hook): part of the foil drag above its minimum, by part AoA and Mach
				if (Plugin.DragReliefAoA > 0f || Plugin.DragReliefAoAHigh > 0f || Plugin.DragReliefTransonic > 0f)
				{
					if (!psCdMin.TryGetValue(ap, out float cdMin))
					{
						cdMin = float.MaxValue;
						if (foil != null)
						{
							for (float aa = -0.35f; aa <= 0.35f; aa += 0.005f)
							{
								foil.Get(aa, out _, out var cdv);
								cdMin = Mathf.Min(cdMin, cdv);
							}
						}
						if (cdMin == float.MaxValue)
						{
							cdMin = 0f;
						}
						psCdMin[ap] = cdMin;
					}
					float dRel = DragReliefMath.Relief(n9, n10, Mathf.Min(cdMin, cd) * rho * v2 * 0.5f * area * eff, vmag / DragReliefMath.SpeedOfSound(com.y), a * 57.29578f);
					if (dRel > 0f)
					{
						f += vHat * dRel;
					}
				}
				Vector3 nrm = Vector3.Cross(vf, lr * Vector3.right);
				float nm = nrm.magnitude;
				if (nm > 0.0001f)
				{
					f += -nrm / nm * (cl * rho * v2 * 0.5f * area * eff);
				}
				f = Vector3.ClampMagnitude(f, vmag * 0.5f * ap.mass * 60f);
				m += Vector3.Cross(com + lr * Acc.AP_centerOfLift(ap) - cg, f);
			}
			// flap-camber coupling, as CamberForce: the host's force increment for its surface's present deflection, unclamped
			if (psCamber == null)
			{
				psCamber = new List<(ControlSurface, AeroPart, AirfoilTable)>();
				AircraftParameters prm = ac.GetAircraftParameters();
				Dictionary<string, AeroPart> byName = new Dictionary<string, AeroPart>();
				foreach (AeroPart ap in aeroParts)
				{
					if (ap != null)
					{
						byName[ap.gameObject.name.Replace("Aryx_KingRaptor_", "")] = ap;
					}
				}
				foreach (ControlSurface cs in Acc.Collect<ControlSurface>(ac))
				{
					string hn = Camber.HostOf(cs.gameObject.name);
					if (hn == null || !byName.TryGetValue(hn, out var host))
					{
						continue;
					}
					int idx = Acc.AP_airfoil(host);
					Airfoil af = (idx >= 0 && prm != null && prm.airfoils != null && idx < prm.airfoils.Length) ? prm.airfoils[idx] : null;
					if (af == null || af.liftCoef == null || af.dragCoef == null || Acc.CS_visible(cs) == null)
					{
						continue;
					}
					psCamber.Add((cs, host, StallStagger.TableFor(host, af, liftShaped: false)));
				}
			}
			foreach (var (cs, host, table) in psCamber)
			{
				if (cs == null || host == null || host.rb == null || host.IsDetached() || !Acc.JobCreated(cs))
				{
					continue;
				}
				ref NuclearOption.Jobs.ControlSurfaceFields job = ref Acc.Job(cs);
				if (job.IsDetached)
				{
					continue;
				}
				float k = Camber.KForName(cs.gameObject.name);
				Transform lt = Acc.AP_liftNormal(host);
				if (k <= 0f || lt == null)
				{
					continue;
				}
				Transform vis = Acc.CS_visible(cs).transform;
				float delta = Camber.DeflectionOf(vis, job.restingRotation);
				Vector3 com = host.rb.worldCenterOfMass;
				Vector3 v = vAir + Vector3.Cross(w, com - cg);
				float v2 = v.sqrMagnitude;
				if (v2 < 25f)
				{
					continue;
				}
				float qS = 0.5f * rho * v2 * Acc.AP_wingArea(host) * Acc.AP_wingEff(host);
				Vector3 dF = Camber.Delta(v, lt.rotation, vis.rotation * Vector3.right, k, delta, table, qS);
				m += Vector3.Cross(com + lt.rotation * Acc.AP_centerOfLift(host) - cg, dF);
			}
			Vector3 mb = Quaternion.Inverse(rootRot) * m;
			return new Vector3(0f - mb.z, 0f - mb.x, mb.y);
		}


		// the rudders' one-side yaw and the other surfaces' roll authority at a hypothetical flow (deg/s^2)
		private void SlipAuthorityAt(float aDeg, float bDeg, float rho, Quaternion rootRot, ref EvalContext ctx, out float yawA, out float rollA)
		{
			float num = Mathf.Max(tas, 1f);
			float fa = aDeg * (MathF.PI / 180f);
			float fb = bDeg * (MathF.PI / 180f);
			Vector3 vector = rootRot * new Vector3(Mathf.Sin(fb) * num, (0f - Mathf.Sin(fa)) * Mathf.Cos(fb) * num, Mathf.Cos(fa) * Mathf.Cos(fb) * num);
			yawA = 0f;
			rollA = 0f;
			for (int i = 0; i < surfaces.Count; i++)
			{
				SurfaceEffector se = surfaces[i];
				if (!se.active)
				{
					continue;
				}
				se.PrepFlow(vector, rho, ref ctx, out SurfaceEffector.FlowPrep fp);   // 3.1.3: = EvalFlow twice in one flow
				Vector3 mMax = se.EvalFlowAt(se.max, ref fp, ref ctx);
				Vector3 mMin = se.EvalFlowAt(se.min, ref fp, ref ctx);
				if (se.kind == SurfaceKind.Rudder)
				{
					yawA += 0.5f * Mathf.Abs(mMax.z - mMin.z) * IaInv[2, 2] * 57.29578f;
				}
				else
				{
					rollA += 0.5f * Mathf.Abs(mMax.x - mMin.x) * IaInv[0, 0] * 57.29578f;
				}
			}
		}

		private void SlipProbeStep_(float dt, float rho, Quaternion rootRot, ref EvalContext ctx)
		{
			if (!Plugin.RollStopSlipProbe || isAI)
			{
				slipTolF = 25f;
				slipTolDbg = 25f;
				return;
			}
			slipProbeTimer += dt;
			if (slipProbeTimer >= SlipProbePeriod && tas > 30f)
			{
				slipProbeTimer = 0f;
				// the sign of a restoring yaw moment, latched once at low AoA where any conventional airframe is directionally
				// stable (the probe's sweep gives it: the yaw moment grows with the sideslip one way or the other)
				if (slipSignR == 0f && Mathf.Abs(alpha) < 10f && tas > 60f)
				{
					float n0 = AccYaw(SlipMomentAt(alpha, 0f, rho, rootRot, ref ctx));
					float nP = AccYaw(SlipMomentAt(alpha, 8f, rho, rootRot, ref ctx));
					float nN = AccYaw(SlipMomentAt(alpha, -8f, rho, rootRot, ref ctx));
					float dP = nP - n0;
					float dN = n0 - nN;
					slipSignR = ((dP + dN) >= 0f) ? 1f : (-1f);
				}
				if (slipSignR == 0f)
				{
					slipTolDbg = 25f;
				}
				else
				{
					float dep = SlipProbeMax + 5f;
					float auth = SlipProbeMax + 5f;
					float stiff = 0f;
					float n0 = AccYaw(SlipMomentAt(alpha, 0f, rho, rootRot, ref ctx));
					SlipAuthorityAt(alpha, 0f, rho, rootRot, ref ctx, out float y0, out float r0);
					for (int sgn = -1; sgn <= 1; sgn += 2)
					{
						for (float b = SlipProbeStep; b <= SlipProbeMax + 0.01f; b += SlipProbeStep)
						{
							float bb = sgn * b;
							// restoring yaw at this sideslip (deg/s^2; positive = toward zero sideslip)
							float rest = (AccYaw(SlipMomentAt(alpha, bb, rho, rootRot, ref ctx)) - n0) * slipSignR * sgn;
							if (b <= SlipProbeStep + 0.01f)
							{
								stiff += 0.5f * rest / SlipProbeStep;
							}
							SlipAuthorityAt(alpha, bb, rho, rootRot, ref ctx, out float yB, out float rB);
							// departure: the airframe pushes the sideslip open harder than SlipAuthUse of the yaw axis can pull back - the
							// rudders' yaw at that sideslip, or past the rudder fade the allocator's yaw authority (the stabs'): 2.5.16,
							// the 14:55 log read a 4 deg bound at -20..-60 deg AoA where the rudders' tables are faded to nothing
							// and any destabilizing moment at all tripped the test (the margin never moved, the episodes were short)
							float yHold = Mathf.Max(yB, aYaw * (1f - rudderAuth));
							if (rest < 0f - SlipAuthUse * yHold && b < dep)
							{
								dep = b;
							}
							// authority: the rudders' yaw or the surfaces' roll down to SlipAuthFrac of their zero-sideslip value
							if ((yB < SlipAuthFrac * y0 || rB < SlipAuthFrac * r0) && b < auth)
							{
								auth = b;
							}
							if (dep <= b && auth <= b)
							{
								break;
							}
						}
					}
					slipDepDbg = dep;
					slipAuthDbg = auth;
					slipStiffDbg = stiff;
					slipTolDbg = Mathf.Min(dep, auth);
				}
			}
			slipTolF += (slipTolDbg - slipTolF) * (dt / (SlipTolTau + dt));
		}

		private void AsymSweepPrep(Quaternion rootRot)
		{
			asymSweepIas = Mathf.Max(ias, 1f);
			AsymPrepSym(rootRot);
			// F-22E 4.2.0: the coupled check where the nozzles cover the jet (vectoring on, T/W over LowThrustFixedAoATW): there they are
			// effectors of its LP (as the Revoker 1.2.0). Latched for the whole sweep.
			cplTvMode = CoupledCap && Plugin.AsymCoupledCheck && tvc != null && tvc.active && !Plugin.DisableTVC && !noTvcOn;
			// F-22E 4.2.0 (CplNoTvc): with the nozzles locked, cut out or the thrust low, the coupled check runs as well, without the
			// nozzles as effectors (the thrust at their present angle stays in the load), and the nose-down cap reads the same LP's
			// held-load pitch figure (PitchHeld) with a margin that scales with dynamic pressure (the Compass 0.4.0 / Su-30 0.5.0 pieces).
			// The Revoker 1.3.0 bench found that lifting the lateral cap there alone let the old stabs-only nose-down cap fall under the jet (23 -> 12 deg)
			// and the bench departed; the two move together here.
			cplNoTv = CoupledCap && Plugin.AsymCoupledCheck && CplNoTvc && !cplTvMode;
			cplActive = cplTvMode || cplNoTv;
			cplLoaded = Mathf.Abs(asymDy) > CoupHeldDy0;
			cplPitchGate = cplNoTv && Plugin.AsymPitchMargin > 0f && Mathf.Abs(asymDy) - CoupHeldDy0 > 0.5f * CoupHeldDySpan;
			asymMT = ((tvc.active || Plugin.DisableTVC) ? tvc.Mnow : Vector3.zero);
			// F-22E 4.2.0 (CplNoTvNeutral, as the Ifrit 1.2.0): on the no-TVC path with the nozzles still free (low thrust, vectoring not
			// locked) the load takes the thrust at the nozzles' neutral, not at their present angle (this controller's own output), and the
			// nose-down figure gets the nozzles' own nose-down range on top (only with a load on: the sweep's other users keep the present angle). Ifrit bench, 300 km/h idle pull, 30 cm load: with the present
			// angle the held-pitch figure swung with the nozzles and the cap flipped 10 <-> 34 deg every 1.5 s.
			cplTvFree = cplNoTv && cplLoaded && CplNoTvNeutral && tvc != null && tvc.active && !Plugin.DisableTVC && tvc.M != null && tvc.N >= 2;
			cplTvFreeDn = 0f;
			if (cplTvFree)
			{
				Vector3 m0 = tvc.MAt(Mathf.Clamp(0f, tvc.min, tvc.max));
				float tvLo = float.MaxValue;
				for (int tk = 0; tk < tvc.N; tk++)
				{
					tvLo = Mathf.Min(tvLo, tvc.M[tk].y);
				}
				asymMT = m0;
				cplTvFreeDn = Mathf.Min(0f, tvLo - m0.y) * IaInv[1, 1] * 57.29578f;
			}
		}
		internal static bool CplNoTvNeutral = true;
		private bool cplTvFree;
		private float cplTvFreeDn;

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
					Quaternion rotation = transform.rotation;
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
			Vector3 result = new Vector3(0f - vector3.z, 0f - vector3.x, vector3.y) + (tvc.active ? tvc.Mnow : Vector3.zero);
			for (int i = 0; i < surfaces.Count; i++)
			{
				if (surfaces[i].active)
				{
					result += surfaces[i].EvalFlow(array[i], vector, rho, ref ctx);
				}
			}
			return result;
		}

		// F-22E 3.1.2 (ParallelSweep): the asymmetric-load sweep. The parts' Unity state (lift-normal rotation, centre of mass and lift,
		// areas, foil) is read once per sweep tick on the main thread (swGather) - the same values every point of the tick read before,
		// since nothing moves the parts inside a tick - and the tick's AoA points are computed in parallel (IndexFork), each writing only
		// its own index of the sweep arrays, joined before anything reads them. Same expressions in the same order per point.
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
			int n = k1 - k0;
			SwGridGeom(k0, k1, ref ctx);
			swNative = FcsNative.SweepOn;   // 4.1.0: one answer for all of this tick's points
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

		// F-22E 4.0.1 (SweepGridCache): the sweep's capacity loop evaluates each surface's whole deflection grid in every AoA point's
		// flow. The deflections' geometry does not depend on the point, so it is worked out here once per sweep tick
		// (SurfaceEffector.GridGeom, the surfaces in parallel) for the surfaces whose capacity loop at least one of the tick's points
		// runs; the points then use SurfaceEffector.EvalFlowGrid. swGrid[j] false = that surface's loop calls EvalFlow as before.
		private bool[] swGrid;

		// 4.1.0 (NativeSweep): this tick's capacity loops run in the native core (FcsNative.Grid, mode 1) where swGrid[j] is set
		private bool swNative;

		private readonly List<SurfaceEffector> swGeomList = new List<SurfaceEffector>();

		private GeomWork swGeomWork;

		private void SwGridGeom(int k0, int k1, ref EvalContext ctx)
		{
			if (swGrid == null || swGrid.Length != surfaces.Count)
			{
				swGrid = new bool[surfaces.Count];
			}
			swGeomList.Clear();
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
					// 4.1.0: with the native table rebuild the geometry of this tick is already there (SurfaceEffector.EvalCompute)
					if (!se.GridGeomFreshFor(ref ctx))
					{
						swGeomList.Add(se);
					}
				}
			}
			// F-22E 4.2.0 (CplGridCache, from the Ternion 0.3.0): the coupled check's points, for every surface it takes points from, if any
			// of this tick's sweep points solves it
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
					swGeomWork = new GeomWork(swGeomList, swCplList);
				}
				swGeomWork.ctx = ctx;
				IndexFork.Finish(IndexFork.Start(swGeomWork, m + mc));
			}
			else
			{
				for (int i = 0; i < m; i++)
				{
					swGeomList[i].GridGeom(ref ctx);
				}
				for (int i = 0; i < mc; i++)
				{
					swCplList[i].CplGeom(ref ctx, CplStride(swCplList[i].N));
				}
			}
		}

		private sealed class GeomWork : IIndexWork
		{
			private readonly List<SurfaceEffector> list;

			public EvalContext ctx;

			private readonly List<SurfaceEffector> cpl;

			public GeomWork(List<SurfaceEffector> list, List<SurfaceEffector> cpl)
			{
				this.list = list;
				this.cpl = cpl;
			}

			public void Do(int i)
			{
				EvalContext cx = ctx;
				if (i < list.Count)
				{
					list[i].GridGeom(ref cx);
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
				EvalContext cx = ctx;
				c.AsymSweepPoint(k0 + i, rho, rootRot, ref cx, array, num, vector);
			}
		}

		private SweepWork swWork;

		// the sweep's per-part snapshot (swGather)
		private int swN;
		private Quaternion[] swRot = new Quaternion[0];
		private Vector3[] swLhs = new Vector3[0];
		private float[] swDrag = new float[0];
		private AirfoilTable[] swFoil = new AirfoilTable[0];
		private float[] swQ = new float[0];

		private void SwGather(float rho)
		{
			int cap = aeroParts.Count;
			if (swRot.Length < cap)
			{
				swRot = new Quaternion[cap];
				swLhs = new Vector3[cap];
				swDrag = new float[cap];
				swFoil = new AirfoilTable[cap];
				swQ = new float[cap];
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
					Quaternion rotation = transform.rotation;
					int k = swN++;
					swRot[k] = rotation;
					swLhs[k] = aeroPart.rb.worldCenterOfMass + rotation * Acc.AP_centerOfLift(aeroPart) - cg;
					swDrag[k] = Acc.AP_dragArea(aeroPart);
					if (!surfaceParts.Contains(aeroPart) && foils.TryGetValue(aeroPart, out var value) && Acc.AP_wingArea(aeroPart) > 0f)
					{
						swFoil[k] = value;
						swQ[k] = 0.5f * rho * Acc.AP_wingArea(aeroPart) * Acc.AP_wingEff(aeroPart);
					}
					else
					{
						swFoil[k] = null;
						swQ[k] = 0f;
					}
				}
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
			// 3.1.2: the parts' state from swGather (read once per sweep tick on the main thread), same expressions, same order
			for (int k = 0; k < swN; k++)
			{
				Vector3 rhs = -vector3 * (0.5f * num2 * swDrag[k]);
				if (swFoil[k] != null)
				{
					rhs += PartForce(vector2, swRot[k], swRot[k] * Vector3.right, swFoil[k], swQ[k]);
				}
				zero += Vector3.Cross(swLhs[k], rhs);
			}
			Vector3 vector4 = Quaternion.Inverse(rootRot) * zero;
			Vector3 m = new Vector3(0f - vector4.z, 0f - vector4.x, vector4.y) + vector;
			// F-22E 4.2.0 (CoupledCap, from the Ternion 0.2.2 by way of the Revoker 1.2.0 / 1.3.0): the airframe's own moment (the load) before any surface; the surfaces
			// and the nozzles become the LP's points
			Vector3 mAir = m;
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
							rhsB += PartForce(vB, swRot[k], swRot[k] * Vector3.right, swFoil[k], swQ[k]);
						}
						zB += Vector3.Cross(swLhs[k], rhsB);
					}
					Vector3 zBl = Quaternion.Inverse(rootRot) * (zB - zero);
					Vector3 dM = new Vector3(0f - zBl.z, 0f - zBl.x, zBl.y);
					if (sB == 0) dMbP = dM; else dMbN = dM;
				}
			}
			// (CplTvc) the nozzles as effectors of the LP: the pitch nozzle's grid carries the whole thrust moment about the CoM at each
			// angle (the thrust x lateral-offset yaw, and the roll its pitch angle makes through that offset); this jet's nozzles move in
			// pitch only. The thrust at the nozzle's present angle ('vector') then leaves the load. 1.1.1 kept that moment in the
			// load and counted no nozzle in the authority: level at 82 m/s on afterburner with 34 cm of offset the demand read 4.7, 4.4
			// of it yaw the nozzle was holding with 1.6 deg (11 Nov 13:16 log).
			bool cplTv = cp != null && cplTvMode && CplTvc && tvc.M != null && tvc.N > 1;
			if (cplTv)
			{
				if (CplDebug) { Vector3 mNw = tvc.MAt(tvc.now); cplDbgNow[cp.nEff] = new Vector3(AccRoll(mNw), mNw.y * IaInv[1, 1] * 57.29578f, AccYaw(mNw)); cplDbgName[cp.nEff] = "TVC pitch"; cplDbgDefl[cp.nEff] = tvc.now; }
				for (int kT = 0; kT < CplStride(tvc.N); kT++)
				{
					int lT = CplIdx(kT, tvc.N);
					Vector3 mT = tvc.M[lT];
					cp.Add(AccRoll(mT), mT.y * IaInv[1, 1] * 57.29578f, AccYaw(mT), tvc.grid[lT]);
				}
				cp.EndEff();
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
			for (int j = 0; j < surfaces.Count; j++)
			{
				SurfaceEffector surfaceEffector = surfaces[j];
				if (!surfaceEffector.active)
				{
					continue;
				}
				// 3.1.3: the flow's own terms once per surface and point (PrepFlow), each deflection's after (EvalFlowAt); = EvalFlow
				surfaceEffector.PrepFlow(vector2, rho, ref ctx, out SurfaceEffector.FlowPrep fp);
				m += surfaceEffector.EvalFlowAt(array[j], ref fp, ref ctx);
				if (cp != null)
				{
					// (CplAttachedOnly, CplStabPeakRule, as the Su-30 0.5.0) a stab's points as the allocator would fly them: its own rule
					// (ClampStabToPeak) holds them to a local AoA of the stall angle + StabPeakMargin, opening by 8 deg per degree of body AoA
					// past 40. With the clamp off or that limit past 89 deg every point counts; otherwise only the points inside it, and if
					// none is, the least stalled one.
					float attLim = CplStabPeakRule ? (surfaceEffector.stallAoA + Plugin.StabPeakMargin + Mathf.Max(0f, Mathf.Abs(asymAlphas[i]) - 40f) * 8f) : surfaceEffector.stallAoA;
					bool attOnly = CplAttachedOnly && surfaceEffector.kind == SurfaceKind.Stabilator && !(CplStabPeakRule && (!StabPeakClamp || attLim > 89f));
					// F-22E 4.2.0 (CplGridCache): this tick's geometry for the points (SwGridGeom) and, native, all their moments at once
					bool cGeo = swCpl != null && swCpl[j] && surfaceEffector.cN == CplStride(surfaceEffector.N);
					Vector3[] cM = (cGeo && swNative && surfaceEffector.CplMomentsNative(ref fp, ref ctx, cp.scratchM)) ? cp.scratchM : null;
					int kLeast = -1;
					float locLeast = float.MaxValue;
					if (attOnly)
					{
						for (int kA = 0; kA < CplStride(surfaceEffector.N); kA++)
						{
							float la = Mathf.Abs((cGeo ? surfaceEffector.LocalAoACpl(kA, ref fp) : surfaceEffector.LocalAoAFlow(surfaceEffector.grid[CplIdx(kA, surfaceEffector.N)], ref fp)));
							if (la < locLeast)
							{
								locLeast = la;
								kLeast = kA;
							}
						}
					}
					if (CplDebug) { Vector3 mNw = surfaceEffector.EvalFlowAt(surfaceEffector.now, ref fp, ref ctx); cplDbgNow[cp.nEff] = new Vector3(AccRoll(mNw), mNw.y * IaInv[1, 1] * 57.29578f, AccYaw(mNw)); cplDbgName[cp.nEff] = surfaceEffector.name; cplDbgDefl[cp.nEff] = surfaceEffector.now; }
					for (int kC = 0; kC < CplStride(surfaceEffector.N); kC++)
					{
						float gC = surfaceEffector.grid[CplIdx(kC, surfaceEffector.N)];
						if (attOnly && kC != kLeast && Mathf.Abs((cGeo ? surfaceEffector.LocalAoACpl(kC, ref fp) : surfaceEffector.LocalAoAFlow(gC, ref fp))) > attLim)
						{
							continue;
						}
						Vector3 mC = (cM != null) ? cM[kC] : (cGeo ? surfaceEffector.EvalFlowCpl(kC, ref fp, ref ctx) : surfaceEffector.EvalFlowAt(gC, ref fp, ref ctx));
						cp.Add(AccRoll(mC), mC.y * IaInv[1, 1] * 57.29578f, AccYaw(mC), gC);
					}
					cp.EndEff();
				}
				if (surfaceEffector.kind == SurfaceKind.Flaperon || surfaceEffector.kind == SurfaceKind.Aileron)
				{
					pFlapNow += surfaceEffector.EvalFlowAt((flapSymRef != null && j < flapSymRef.Length) ? flapSymRef[j] : 0f, ref fp, ref ctx).y;
				}
				if (surfaceEffector.kind == SurfaceKind.Rudder && toe > 0f)
				{
					pRud0 += surfaceEffector.EvalFlowAt(0f, ref fp, ref ctx).y;
					pRudP += surfaceEffector.EvalFlowAt(toe, ref fp, ref ctx).y;
					pRudM += surfaceEffector.EvalFlowAt(0f - toe, ref fp, ref ctx).y;
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
					// 4.0.1 (SweepGridCache): the deflection's geometry from this tick's GridGeom, the flow's terms from fp (= EvalFlow)
					bool gridGeom = swGrid != null && j < swGrid.Length && swGrid[j];
					// 4.1.0 (NativeSweep): the whole loop in the native core; it leaves the six values untouched when it declines
					if (!(gridGeom && swNative && surfaceEffector.GridRangeNative(ref fp, ref ctx, IaInv[0, 0], IaInv[0, 2], IaInv[2, 0], IaInv[2, 2], ref pMinS, ref pMaxS, ref num8, ref num9, ref num10, ref num11)))
					{
						for (int l = 0; l < surfaceEffector.N; l++)
						{
							Vector3 m2 = gridGeom ? surfaceEffector.EvalFlowGrid(l, ref fp, ref ctx) : surfaceEffector.EvalFlow(surfaceEffector.grid[l], vector2, rho, ref ctx);
							pMinS = Mathf.Min(pMinS, m2.y);
							pMaxS = Mathf.Max(pMaxS, m2.y);
							float b = AccRoll(m2);
							float b2 = AccYaw(m2);
							num8 = Mathf.Min(num8, b);
							num9 = Mathf.Max(num9, b);
							num10 = Mathf.Min(num10, b2);
							num11 = Mathf.Max(num11, b2);
						}
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
			// 2.0.9: the symmetric rudder toe (2.0.7 pitch aid, up to RudderToeMax each way) counts toward the nose-down
			// authority the low-thrust cap judges by (the rudders' pitch was left out before, neutral or toed)
			float pRud = (toe > 0f) ? Mathf.Min(pRud0, Mathf.Min(pRudP, pRudM)) : 0f;
			authNetDn[i] = (pAero + pStabMin + pFlapNow + pRud) * IaInv[1, 1] * 57.29578f;
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
				// the load the LP holds: the airframe, plus the thrust at the nozzle's present angle only where the nozzles are not
				// its effectors (TVC off, or cut out by speed); with an engine-out fixed cap the thrust part stays out as before (tK)
				CplSolve(i, cp, cplTv ? (mAir - vector) : (mAir - (1f - tK) * vector), kq, num12, tauRes, dMbP, dMbN);
				if (CplDebug && cplClock >= CplDbgT2 && Mathf.Abs(asymAlphas[i] - alpha) <= 2.5f) { CplDbgT2 = float.MaxValue; CplDump(i, cp, cplTv ? (mAir - vector) : (mAir - (1f - tK) * vector)); }
			}
			else if (cplActive)
			{
				cplU[i] = asymU[i];
				cplRp[i] = (cplRn[i] = 0f);
				cplDn[i] = asymNetDn[i];
				cplUp[i] = asymNetUp[i];
			}
		}

		// F-22E 4.2.0 (CoupledCap, from the Ternion 0.2.2 / Revoker 1.2.0; see CoupledCap.cs). Every piece has a bench switch; CoupledCap = false is 4.1.5.
		internal static bool CoupledCap = true;
		internal static bool CplTvc = true;              // the nozzles as LP effectors (false: thrust at the present angle in the load, no nozzle authority)
		internal static float CplDeadband = 2f;          // deg/s^2 of velocity-vector roll shortfall ignored (fuel imbalance, rounding)
		internal static int CplPts = 9;                  // grid points per effector the LP takes (ends included, evenly spaced)
		internal static int CplPointsPerStep = 2;        // sweep points per tick while the LP runs (3 without it)
		internal static float CplBetaDesign = 3f;
		internal static bool CplAttachedOnly = true;
		internal static bool CplStabPeakRule = true;
		// F-22E 4.2.0 (CplTopEdge, from the Su-30 0.5.0): while the coupled curve is in use the cap is the lower edge of its top over-threshold
		// stretch (AsymCap.FindTop), not the first crossing from zero AoA. At low speed this jet's coupled curve has a hump near 10 deg
		// (bench, 8 cm, afterburner, 37 m/s: 1.11 at 10 deg, 0.25 at the 65 deg the jet was flying): the first crossing put the cap on
		// its 12 deg floor and pushed the nose down through the hump.
		internal static bool CplTopEdge = true;
		private bool cplLowPos, cplLowNeg;
		private bool CplTop => cplActive && cplLoaded && CplTopEdge;
		// the share of the flight-path roll range the reserve may claim (the one-axis check: AsymResMaxFrac 0.8): the dev setting
		// AsymCoupledReserveMax
		// F-22E 4.2.0 (as the Revoker 1.3.0): the no-TVC path has its own share (AsymCoupledReserveMaxNoTvc, 0.8): without the nozzles the
		// surfaces also make the pitch-in (Revoker bench, 30 cm: at 0.7 a full pull at 500 - 550 km/h ran 6 - 9 deg past the limit)
		private float CplResMaxFrac => cplNoTv ? Plugin.AsymCoupledReserveMaxNoTvc : Plugin.AsymCoupledReserveMax;
		private float cplClock;
		// bench diagnostic (run with ParallelSweep=false CplBetaDesign=0): the LP's mix for each side against the real deflections
		internal static bool CplDebug = false;
		internal static float CplDbgT2 = 0f;
		private readonly Vector3[] cplDbgNow = new Vector3[CplPoint.MaxEff];
		private readonly string[] cplDbgName = new string[CplPoint.MaxEff];
		private readonly float[] cplDbgDefl = new float[CplPoint.MaxEff];

		private void CplDump(int i, CplPoint cp, Vector3 mLoad)
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendFormat(CultureInfo.InvariantCulture, "CPLMIX t {0:F2} aPt {1:F0} aNow {2:F1} ias {3:F1} load R {4:F1} Q {5:F1} Y {6:F1} | rp {7:F1} rn {8:F1} u {9:F2}\n", cplClock, asymAlphas[i], alpha, ias, AccRoll(mLoad), mLoad.y * IaInv[1, 1] * 57.29578f, AccYaw(mLoad), cplRp[i], cplRn[i], cplU[i]);
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
		internal static float CplDbgT = -1f;             // bench: print one sweep's curve at this time (s)
		private bool cplActive;

		// F-22E 4.2.0 (CplGridCache, from the Ternion 0.3.0): the coupled check's surface points on geometry worked out once per sweep
		// tick (SurfaceEffector.CplGeom) and, with the native kernels, their moments in one call per surface and sweep point. Off =
		// the previous release's per-point evaluation.
		internal static bool CplGridCache = true;

		// F-22E 4.2.0: CplSolve's one-side solves (see there)
		internal static bool CplOneSide = true;

		private bool[] swCpl;

		private readonly List<SurfaceEffector> swCplList = new List<SurfaceEffector>();

		// the LP only where the cap can use it: positive AoA up to 5 deg past the limit, the negative side down to 5 deg past its limit
		private bool CplHere(int i)
		{
			return (asymAlphas[i] >= 0f) ? (asymAlphas[i] <= Plugin.AoAPos + 5f) : (asymAlphas[i] >= Plugin.AoANeg - 5f);
		}

		// F-22E 4.2.0: which path the coupled check runs on this sweep (latched in AsymSweepPrep): with the nozzles as effectors (1.2.0),
		// or without them (CplNoTvc) with the held-load pitch figure
		private bool cplTvMode, cplNoTv, cplPitchGate;

		internal static bool CplNoTvc = true;

		private readonly float[] cplDn = new float[22];

		private readonly float[] cplUp = new float[22];

		public float cplDnNow;

		// F-22E 4.2.0 (from the Compass 0.4.0, no-TVC path only): the nose-down margin of the asymmetric pitch cap scales with dynamic
		// pressure below AsymPitchMarginIasRef, never under AsymPitchMarginMin, judged at the predicted dynamic pressure
		internal static bool AsymPitchNoGainSkip = true;
		internal static float AsymPitchMarginIasRef = 130f;
		internal static float AsymPitchMarginMin = 6f;
		internal static bool AsymPitchAtPredQ = true;
		public float asymPitchMargDbg;

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
		private bool cplLoaded;
		private readonly CplPoint[] cplPts = new CplPoint[22];
		private readonly float[] cplU = new float[22];
		private readonly float[] cplRp = new float[22];
		private readonly float[] cplRn = new float[22];
		public float cplUNow, cplRpNow, cplRnNow;

		private static int CplStride(int n)
		{
			return Mathf.Min(n, Mathf.Max(2, CplPts));
		}

		internal static int CplIdx(int k, int n)
		{
			int m = CplStride(n);
			return (m <= 1) ? 0 : Mathf.Min(n - 1, Mathf.RoundToInt((float)k * (float)(n - 1) / (float)(m - 1)));
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

		// point i's flight-path roll reserve each way with the load in, pitch held and no sideslip acceleration, and the demand from it
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
				return;
			}
			// F-22E 4.2.0 (CplOneSide, as the Su-30 0.5.0): with the design sideslip the zero-sideslip case is solved only for its feasibility
			// (phase 1, side 2) and each design case only on its own side: 3 phase 1 + 2 phase 2 instead of 3 + 6, the same figures
			// (the skipped phase 2s were thrown away). false = the previous release's calls.
			bool okA;
			float rp, rn;
			if (CplOneSide && CplBetaDesign > 0f)
			{
				okA = cp.Reserve(sinA, cosA, bR, bQ, bY, sgn, out rp, out rn, 2);
			}
			else
			{
				okA = cp.Reserve(sinA, cosA, bR, bQ, bY, sgn, out rp, out rn);
			}
			if (okA && CplBetaDesign > 0f)
			{
				float vvP = cosA * AccRoll(dMbP) + sinA * AccYaw(dMbP);
				float vvN = cosA * AccRoll(dMbN) + sinA * AccYaw(dMbN);
				Vector3 dAgP = (vvP <= vvN) ? dMbP : dMbN;
				Vector3 dAgN = (vvP <= vvN) ? dMbN : dMbP;
				bool okP = cp.Reserve(sinA, cosA, bR + AccRoll(dAgP), bQ + dAgP.y * IaInv[1, 1] * 57.29578f, bY + AccYaw(dAgP), sgn, out float rpB, out float _, CplOneSide ? 1 : 0);
				bool okN = cp.Reserve(sinA, cosA, bR + AccRoll(dAgN), bQ + dAgN.y * IaInv[1, 1] * 57.29578f, bY + AccYaw(dAgN), sgn, out float _, out float rnB, CplOneSide ? -1 : 0);
				okA = okP && okN;
				rp = rpB;
				rn = rnB;
			}
			cplRp[i] = okA ? rp : -999f;
			cplRn[i] = okA ? rn : -999f;
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
			}
			// F-22E 4.2.0 (no-TVC path): the most nose-down (positive AoA) / nose-up (negative) with the load's roll and yaw held, every
			// surface counted; the asymmetric pitch cap reads it in place of the canards-only figure (asymNetDn)
			if (!cplPitchGate)
			{
				cplDn[i] = asymNetDn[i];
				cplUp[i] = asymNetUp[i];
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

		private float NonSurfacePitch(Vector3 wind, float rho, Quaternion rootInv, Vector3 rootRight, float dAlphaDeg, float qRad)
		{
			Quaternion rot = Quaternion.AngleAxis(dAlphaDeg, rootRight);
			Vector3 w = -rootRight * qRad;
			Vector3 m = Vector3.zero;
			foreach (AeroPart aeroPart in aeroParts)
			{
				if (aeroPart == null || aeroPart.rb == null || aeroPart.IsDetached() || surfaceParts.Contains(aeroPart))
				{
					continue;
				}
				Transform transform = Acc.AP_liftNormal(aeroPart);
				float area = Acc.AP_wingArea(aeroPart);
				if (transform == null || area <= 0f || !foils.TryGetValue(aeroPart, out var foil))
				{
					continue;
				}
				Quaternion rotation = transform.rotation;
				Vector3 arm = aeroPart.rb.worldCenterOfMass - cg;
				Vector3 r = arm + rotation * Acc.AP_centerOfLift(aeroPart);
				// flow at the part: the CG's air velocity turned by dAlpha, plus the pitch rotation qRad
				Vector3 v = rot * (vCgAir) + Vector3.Cross(w, arm);
				m += Vector3.Cross(r, PartForce(v, rotation, rotation * Vector3.right, foil, 0.5f * rho * area * Acc.AP_wingEff(aeroPart)));
			}
			return 0f - (rootInv * m).x;
		}

		private Vector3 vCgAir;

		// F-22E 3.1.3 (ParallelEnvelope): the four NonSurfacePitch evaluations of ComputeHybridSlopes on the parts' state read once
		// (HsGather, main thread); HsPoint(k) = NonSurfacePitch with the k-th arguments, same expressions in the same part order.
		private void HsGather(Vector3 wind, float rho, Vector3 rootRight)
		{
			if (surfaceParts.Count == 0)
			{
				foreach (SurfaceEffector se in surfaces)
				{
					if (se.part != null)
					{
						surfaceParts.Add(se.part);
					}
				}
			}
			hsRootInv = Quaternion.Inverse(((Component)(object)ac).transform.rotation);
			vCgAir = ((AirDataAtCg && vCg.sqrMagnitude > 0f) ? vCg : ac.rb.velocity) - wind;   // 2.30.0: the CG's, as the model below assumes
			float q0 = omega.y;
			hsDA[0] = 1f;
			hsQR[0] = q0;
			hsDA[1] = -1f;
			hsQR[1] = q0;
			hsDA[2] = 0f;
			hsQR[2] = q0 + 0.05f;
			hsDA[3] = 0f;
			hsQR[3] = q0 - 0.05f;
			hsRootRight = rootRight;
			int cap = aeroParts.Count;
			if (hsArm.Length < cap)
			{
				hsArm = new Vector3[cap];
				hsR = new Vector3[cap];
				hsRot = new Quaternion[cap];
				hsFoil = new AirfoilTable[cap];
				hsQS = new float[cap];
			}
			hsN = 0;
			foreach (AeroPart aeroPart in aeroParts)
			{
				if (aeroPart == null || aeroPart.rb == null || aeroPart.IsDetached() || surfaceParts.Contains(aeroPart))
				{
					continue;
				}
				Transform transform = Acc.AP_liftNormal(aeroPart);
				float area = Acc.AP_wingArea(aeroPart);
				if (transform == null || area <= 0f || !foils.TryGetValue(aeroPart, out var foil))
				{
					continue;
				}
				Quaternion rotation = transform.rotation;
				Vector3 arm = aeroPart.rb.worldCenterOfMass - cg;
				int k = hsN++;
				hsRot[k] = rotation;
				hsArm[k] = arm;
				hsR[k] = arm + rotation * Acc.AP_centerOfLift(aeroPart);
				hsFoil[k] = foil;
				hsQS[k] = 0.5f * rho * area * Acc.AP_wingEff(aeroPart);
			}
		}

		private void HsPoint(int c)
		{
			Quaternion rot = Quaternion.AngleAxis(hsDA[c], hsRootRight);
			Vector3 w = -hsRootRight * hsQR[c];
			Vector3 m = Vector3.zero;
			for (int k = 0; k < hsN; k++)
			{
				Quaternion rotation = hsRot[k];
				// flow at the part: the CG's air velocity turned by dAlpha, plus the pitch rotation qRad
				Vector3 v = rot * (vCgAir) + Vector3.Cross(w, hsArm[k]);
				m += Vector3.Cross(hsR[k], PartForce(v, rotation, rotation * Vector3.right, hsFoil[k], hsQS[k]));
			}
			hsOut[c] = 0f - (hsRootInv * m).x;
		}

		private void HsFinish()
		{
			mAlphaO = 0.5f * (hsOut[0] - hsOut[1]);
			mQO = (hsOut[2] - hsOut[3]) / (0.1f * 57.29578f);
		}

		private Quaternion hsRootInv;
		private Vector3 hsRootRight;
		private readonly float[] hsDA = new float[4];
		private readonly float[] hsQR = new float[4];
		private readonly float[] hsOut = new float[4];
		private int hsN;
		private Vector3[] hsArm = new Vector3[0];
		private Vector3[] hsR = new Vector3[0];
		private Quaternion[] hsRot = new Quaternion[0];
		private AirfoilTable[] hsFoil = new AirfoilTable[0];
		private float[] hsQS = new float[0];

		// the envelope's per-part snapshot (ComputeEnvelope) and per-point rotations / downwash
		private int enN;
		private Vector3[] enVel = new Vector3[0];
		private Quaternion[] enRot = new Quaternion[0];
		private Vector3[] enRight = new Vector3[0];
		private AirfoilTable[] enFoil = new AirfoilTable[0];
		private float[] enQS = new float[0];
		private bool[] enStab = new bool[0];
		private readonly Quaternion[] enQ = new Quaternion[25];
		private readonly float[] enEps = new float[25];
		private Quaternion enRoot;
		private Vector3 enEL;

		// one envelope point: envLift[j] summed over the parts in the same order as the part-major loop it replaces
		private void EnvPoint(int j)
		{
			Quaternion quaternion2 = enQ[j];
			for (int p = 0; p < enN; p++)
			{
				Vector3 vj = quaternion2 * enVel[p];
				if (enStab[p])
				{
					vj = Downwash.Turn(vj, enRoot, enEps[j]);   // = Downwash.Apply(vj, root, quaternion2 * (v_ac - wind)): eps depends on j only
				}
				envLift[j] += PartLift(vj, enRot[p], enRight[p], enFoil[p], enQS[p], enEL);
			}
		}

		private sealed class EnvWork : IIndexWork
		{
			private readonly FcsController c;

			public EnvWork(FcsController c)
			{
				this.c = c;
			}

			public void Do(int i)
			{
				if (i < 25)
				{
					c.EnvPoint(i);
				}
				else
				{
					c.HsPoint(i - 25);
				}
			}
		}

		private EnvWork envWork;

		private void ComputeEnvelope(Vector3 wind, float rho, Vector3 rootRight, Vector3 eL)
		{
			for (int i = 0; i < envLift.Length; i++)
			{
				envLift[i] = 0f;
			}
			Quaternion quaternion = Quaternion.Inverse(((Component)(object)ac).transform.rotation);
			Vector3 lhs = -((Component)(object)ac).transform.forward * 0.2f;
			float num = 0f;
			float num2 = 0f;
			float num3 = mass * 9.81f;
			Vector3 vCgHat = (((AirDataAtCg && vCg.sqrMagnitude > 0f) ? vCg : ac.rb.velocity) - wind).normalized;
			float dragAf = 0f;
			// 3.1.3: the 25 points' per-part inputs are kept (enVel ... enStab) and the points computed after the loop (EnvPoint)
			int cap = aeroParts.Count;
			if (enVel.Length < cap)
			{
				enVel = new Vector3[cap];
				enRot = new Quaternion[cap];
				enRight = new Vector3[cap];
				enFoil = new AirfoilTable[cap];
				enQS = new float[cap];
				enStab = new bool[cap];
			}
			enN = 0;
			bool anyStab = false;
			foreach (AeroPart aeroPart in aeroParts)
			{
				if (aeroPart == null || aeroPart.rb == null)
				{
					continue;
				}
				Transform transform = Acc.AP_liftNormal(aeroPart);
				float num4 = Acc.AP_wingArea(aeroPart);
				if (transform == null || aeroPart.IsDetached() || num4 <= 0f || !foils.TryGetValue(aeroPart, out var value))
				{
					continue;
				}
				Vector3 vector = aeroPart.rb.velocity - wind;
				Vector3 vRaw = vector;
				float sqrMagnitude = vector.sqrMagnitude;
				bool dwPart = Downwash.IsStab(aeroPart);
				if (dwPart)
				{
					vector = Downwash.Apply(vector, ((Component)(object)ac).transform.rotation, ac.rb.velocity - wind);
				}
				if (!(sqrMagnitude < 25f))
				{
					float qS = 0.5f * rho * sqrMagnitude * num4 * Acc.AP_wingEff(aeroPart);
					Quaternion rotation = transform.rotation;
					Vector3 right = rotation * Vector3.right;
					Vector3 lhs2 = aeroPart.rb.worldCenterOfMass + rotation * Acc.AP_centerOfLift(aeroPart) - cg;
					Vector3 v = vector + Vector3.Cross(lhs, aeroPart.rb.worldCenterOfMass - cg);
					num += 0f - (quaternion * Vector3.Cross(lhs2, PartForce(vector, rotation, right, value, 0.5f * rho * num4 * Acc.AP_wingEff(aeroPart)))).z;
					num2 += 0f - (quaternion * Vector3.Cross(lhs2, PartForce(v, rotation, right, value, 0.5f * rho * num4 * Acc.AP_wingEff(aeroPart)))).z;
					// 2.2.16: airframe drag along the flight path (airfoil + parasitic, AeroJob_Math terms), for the brake's share
					float effD = Acc.AP_wingEff(aeroPart);
					float dArea = Acc.AP_dragArea(aeroPart);
					dragAf += 0f - Vector3.Dot(PartForce(vector, rotation, right, value, 0.5f * rho * num4 * effD), vCgHat) + 0.25f * rho * sqrMagnitude * (dArea + (dArea + num4 * 0.1f) * (1f - effD));
					int p = enN++;
					enVel[p] = vRaw;
					enRot[p] = rotation;
					enRight[p] = right;
					enFoil[p] = value;
					enQS[p] = qS;
					enStab[p] = dwPart;
					anyStab |= dwPart;
				}
			}
			enRoot = ((Component)(object)ac).transform.rotation;
			enEL = eL;
			Vector3 vAc = ac.rb.velocity - wind;
			for (int j = 0; j < envAlphas.Length; j++)
			{
				enQ[j] = Quaternion.AngleAxis((j < 3) ? envAlphas[j] : (envAlphas[j] - alpha), rootRight);
				enEps[j] = anyStab ? Downwash.EpsFor(enQ[j] * vAc, enRoot) : 0f;
			}
			HsGather(wind, rho, rootRight);
			if (Plugin.ParallelEnvelope)
			{
				if (envWork == null)
				{
					envWork = new EnvWork(this);
				}
				IndexFork.Finish(IndexFork.Start(envWork, envAlphas.Length + 4));
			}
			else
			{
				for (int j = 0; j < envAlphas.Length; j++)
				{
					EnvPoint(j);
				}
				for (int c = 0; c < 4; c++)
				{
					HsPoint(c);
				}
			}
			HsFinish();
			// 2.2.16 (AirbrakeAoAFade): the brake's drag (game Airbrake.FixedUpdate: dragAmount x rho x v^2 x opening, on each
			// carrying part) against the airframe's own. Full open while the brake is AirbrakeFadeShareFull of the total drag or
			// more, stowed at AirbrakeFadeShareZero or less. Clean jet, bench: share 81 % at 0 deg AoA, 56 % at 10, 42 % at 15,
			// 32 % at 20, 19 % at 30, 12 % at 40, 9 % at 50.
			float dragAb = 0f;
			for (int iA = 0; iA < abRb.Count; iA++)
			{
				if (abRb[iA] != null && !(abPart[iA] != null && abPart[iA].IsDetached()))
				{
					dragAb += abDrag[iA] * rho * (abRb[iA].velocity - wind).sqrMagnitude;
				}
			}
			airbrakeShare = (dragAb > 0f) ? (dragAb / (dragAb + Mathf.Max(0f, dragAf))) : 1f;
			float abTgt = Mathf.Clamp01((airbrakeShare - Plugin.AirbrakeFadeShareZero) / Mathf.Max(0.01f, Plugin.AirbrakeFadeShareFull - Plugin.AirbrakeFadeShareZero));
			airbrakeFade = Mathf.MoveTowards(airbrakeFade, abTgt, 0.2f);
			nAlpha = Mathf.Clamp((envLift[2] - envLift[0]) * 0.5f / num3, 0.005f, 12f);
			Lp = Mathf.Min((num2 - num) / 0.2f, -1f);
			float num5 = float.MinValue;
			float num6 = 30f;
			for (int k = 3; k < EnvNegStart; k++)
			{
				float num7 = envLift[k] / num3 + tw * Mathf.Sin(envAlphas[k] * (MathF.PI / 180f));
				if (num7 > num5)
				{
					num5 = num7;
					num6 = envAlphas[k];
				}
			}
			nMaxAero = Mathf.Max(1f, num5);
			float nl = 0f;
			for (int kl = 3; kl < EnvNegStart; kl++)
			{
				nl = Mathf.Max(nl, envLift[kl] / num3);
			}
			nLiftMax = nl;
			float nlN = 0f;
			for (int kn = EnvNegStart; kn < envAlphas.Length; kn++)
			{
				nlN = Mathf.Max(nlN, (0f - envLift[kn]) / num3);
			}
			nLiftMin = nlN;
			alphaAtNMax = num6;
		}

		private static Vector3 PartForce(Vector3 v, Quaternion lr, Vector3 right, AirfoilTable t, float qSr)
		{
			float sqrMagnitude = v.sqrMagnitude;
			if (sqrMagnitude < 1f)
			{
				return Vector3.zero;
			}
			Vector3 vector = Quaternion.Inverse(lr) * v;
			t.Get(Mathf.Atan2(vector.y, vector.z), out var CL, out var CD);
			Vector3 vector2 = Vector3.Cross(v, right);
			float magnitude = vector2.magnitude;
			if (magnitude < 0.0001f)
			{
				return Vector3.zero;
			}
			float num = qSr * sqrMagnitude;
			return -vector2 / magnitude * (CL * num) - v / Mathf.Sqrt(sqrMagnitude) * (CD * num);
		}

		private float StockRollRef()
		{
			float num = 0.01868f * tas;
			float b = tas * tas * Mathf.Max(0.01f, ac.airDensity) / 24010f;
			float num2 = 1f / Mathf.Max(1f, b);
			float num3 = Mathf.Min(num, num * (1f + 4.5f * num2) / (1f + 1.1f * num2 * num)) * 57.29578f;
			float num4 = Mathf.Lerp(Plugin.RollGainLowIAS, Plugin.RollGainHighIAS, Mathf.InverseLerp(120f, 170f, ias));
			return num3 * num4;
		}

		private float NPath(float a)
		{
			float W = mass * 9.81f;
			if (a <= 5f)
			{
				return Nk(3) + nAlpha * (a - 5f);
			}
			if (a >= 65f)
			{
				return Nk(15);
			}
			float num = (a - 5f) / 5f;
			int num2 = Mathf.Clamp((int)num, 0, 11);
			float num3 = num - (float)num2;
			return Nk(3 + num2) + (Nk(4 + num2) - Nk(3 + num2)) * num3;
			float Nk(int k)
			{
				return envLift[k] / W + tw * Mathf.Sin(envAlphas[k] * (MathF.PI / 180f));
			}
		}

		private static float PartLift(Vector3 v, Quaternion lr, Vector3 right, AirfoilTable t, float qS, Vector3 eL)
		{
			Vector3 vector = Quaternion.Inverse(lr) * v;
			float a = Mathf.Atan2(vector.y, vector.z);
			t.Get(a, out var CL, out var _);
			Vector3 vector2 = Vector3.Cross(v, right);
			float magnitude = vector2.magnitude;
			if (magnitude < 0.0001f)
			{
				return 0f;
			}
			return Vector3.Dot(-vector2 / magnitude * (CL * qS), eL);
		}

		// 2.2.6: the pilot's throttle as it came into the FCS this frame (the stock airbrake rule needs it)
		public float rawThrottle = 1f;

		// 2.2.6: whether the in-flight speedbrake is commanded out now (brake held, or with
		// ThrottleIndependentAirbrake off, throttle at zero too). Never on the ground: there the brake is the rollout aero-brake.
		public bool AirbrakeOut
		{
			get
			{
				// 2.4.15: with ThrottleIndependentAirbrake off, the stock rule on the ground too (Airbrake.Update opens the panels at
				// zero throttle wherever the jet is)
				return airBrake > 0.01f && (!onGround || !Plugin.ThrottleIndependentAirbrake);
			}
		}

		public void Tick(ControlInputs inp, bool flightAssist)
		{
			// F-22E 3.0.4: collect last tick's estimator job before anything reads capEst, then finish last tick's CSV row
			mkDone = false;
			mk[0] = System.Diagnostics.Stopwatch.GetTimestamp();
			estJobF?.Join();
			FlushPendingRow();
			mk[1] = System.Diagnostics.Stopwatch.GetTimestamp();
			// F-22E 3.1.0: per-phase timer for this tick, and the main allocator's switches
			phRebuild = phKick = phRow = 0L;
			phLaws = phAsym = phAsymMom = phSlip = phSweep = phEnv = phMass = phFlaps = phGround = 0L;
			allocTicks0 = alloc.solveTicks;
			alloc.timeSolves = true;
			alloc.parallelReach = Plugin.ParallelAllocator;
			rawThrottle = (inp != null) ? inp.throttle : 1f;
			float num = Mathf.Max(0.001f, Time.fixedDeltaTime);
			time += num;
			Rigidbody rb = ac.rb;
			Transform transform = ((Component)(object)ac).transform;
			if (rb == null || transform == null)
			{
				return;
			}
			Quaternion rotation = transform.rotation;
			Quaternion quaternion = Quaternion.Inverse(rotation);
			massTimer += num;
			bool flag = massTimer > 0.5f;
			long tMass0 = System.Diagnostics.Stopwatch.GetTimestamp();
			if (flag)
			{
				massTimer = 0f;
				RebuildBodies();
			}
			UpdateMassProps(flag || !havePrev, quaternion);
			phMass += System.Diagnostics.Stopwatch.GetTimestamp() - tMass0;
			mk[2] = System.Diagnostics.Stopwatch.GetTimestamp();
			Vector3 vector = Acc.AC_wind(ac);
			Vector3 vRef = (AirDataAtCg && vCg.sqrMagnitude > 0f) ? vCg : rb.velocity;
			Vector3 vector2 = vRef - vector;
			tas = vector2.magnitude;
			float num2 = Mathf.Max(0.01f, ac.airDensity);
			ias = tas * Mathf.Sqrt(num2 / 1.225f);
			Vector3 vector3 = quaternion * vector2;
			alpha = ((tas > 1f) ? (Mathf.Atan2(0f - vector3.y, vector3.z) * 57.29578f) : 0f);
			beta = ((tas > 1f) ? (Mathf.Asin(Mathf.Clamp(vector3.x / tas, -1f, 1f)) * 57.29578f) : 0f);
			{
				Vector3 vRootB = quaternion * (rb.velocity - vector);
				float tasR = vRootB.magnitude;
				alphaRoot = (tasR > 1f) ? (Mathf.Atan2(0f - vRootB.y, vRootB.z) * 57.29578f) : 0f;
				betaRoot = (tasR > 1f) ? (Mathf.Asin(Mathf.Clamp(vRootB.x / tasR, -1f, 1f)) * 57.29578f) : 0f;
			}
			Vector3 vector4 = quaternion * rb.angularVelocity;
			omega = new Vector3(0f - vector4.z, 0f - vector4.x, vector4.y);
			Vector3 x = (havePrev ? ((rb.velocity - velPrev) / num) : Vector3.zero);
			Vector3 vector5 = accF.Step(x, 0.05f, num);
			Vector3 xCg = (havePrev ? ((vRef - vCgPrev) / num) : Vector3.zero);
			Vector3 accCg = accCgF.Step(xCg, 0.05f, num);
			if (!FlightPathAtCg) accCg = vector5;   // 2.30.0: the flight path's acceleration (omegaV, vDot) at the CG
			Vector3 lhs = vector5 - Physics.gravity;
			nz = Vector3.Dot(lhs, transform.up) / 9.81f;
			Vector3 vector6 = ((tas > 1f) ? Vector3.Cross(vector2, transform.right).normalized : transform.up);
			eLy = vector6.y;
			float vc = Mathf.Max(tas, 30f);
			omegaV = Vector3.Dot(accCg, vector6) / Mathf.Max(tas, 5f) * 57.29578f;
			vDot = vDotF.Step((tas > 1f) ? Vector3.Dot(accCg, vector2 / tas) : 0f, 0.2f, num);
			theta = Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * 57.29578f;
			phi = Mathf.Atan2(0f - transform.right.y, transform.up.y) * 57.29578f;
			float x2 = (havePrev ? (Mathf.DeltaAngle(alphaPrev, alpha) / num) : 0f);
			alphaDot = alphaDotF.Step(x2, 0.05f, num);
			alphaPrev = alpha;
			alphaU = ((Mathf.Abs(alpha) < 90f || !havePrev) ? alpha : Mathf.Clamp(alphaU + Mathf.DeltaAngle(alphaU, alpha), -300f, 300f));
			bool flag2 = false;
			foreach (LandingGear gear in gears)
			{
				if (gear != null && gear.WeightOnWheel(0.05f))
				{
					flag2 = true;
					break;
				}
			}
			long tFl0 = System.Diagnostics.Stopwatch.GetTimestamp();
			mk[3] = tFl0;
			flaps.Update(num, this, flag2 && tas < 110f, ac.speed, Mathf.Clamp(inp.roll, -1f, 1f), groundBrake);
			phFlaps += System.Diagnostics.Stopwatch.GetTimestamp() - tFl0;
			mk[4] = System.Diagnostics.Stopwatch.GetTimestamp();
			rudderAuth = 1f - Mathf.Clamp01((Mathf.Abs(alpha) - Plugin.RudderFadeStart) / Mathf.Max(1f, Plugin.RudderFadeEnd - Plugin.RudderFadeStart));
			if (RudderRevBack)
			{
				rudderAuth = Mathf.Max(rudderAuth, Mathf.InverseLerp(120f, 140f, Mathf.Abs(alpha)));
			}
			foreach (SurfaceEffector surface in surfaces)
			{
				float num3 = ((surface.kind == SurfaceKind.Rudder) ? rudderAuth : 1f);
				surface.lo = surface.min * num3;
				surface.hi = surface.max * num3;
				if (StabPeakClamp && surface.kind == SurfaceKind.Stabilator)
				{
					ClampStabToPeak(surface);
				}
			}
			SideslipRudder(num);
			EvalContext ctx = new EvalContext
			{
				cg = cg,
				rootInv = quaternion,
				rho = num2,
				wind = vector,
				vAir = ac.rb.velocity - vector
			};
			Vector3 zero = Vector3.zero;
			RollPitchGateStep(num);
			long tReb0 = System.Diagnostics.Stopwatch.GetTimestamp();
			mk[5] = tReb0;
			if (Plugin.ParallelTables)
			{
				// F-22E 3.1.0 (ParallelTables): the surfaces' game reads here (EvalGather), their table maths on the pool and this thread
				// (EvalCompute), the nozzles' Evaluate here meanwhile (it reads the engines' transforms throughout); then the same sum, in
				// the same order, as below
				parSurf.Clear();
				foreach (Effector eff in effs)
				{
					if (eff is SurfaceEffector seH)
					{
						seH.honestGate = Mathf.Max(Plugin.OvershootPitchPriority ? pitchNeed : 0f, HonestRollGate * rpLat);
						if (seH.EvalGather(ref ctx))
						{
							parSurf.Add(seH);
						}
					}
				}
				TableFork.Run tRun = TableFork.Start(parSurf, ctx);
				foreach (Effector eff in effs)
				{
					if (!(eff is SurfaceEffector))
					{
						eff.Evaluate(ref ctx);
					}
				}
				TableFork.Finish(tRun);
				foreach (Effector eff in effs)
				{
					if (eff.active)
					{
						zero += eff.Mnow;
						if (!Engaged || !havePrev)
						{
							eff.cmd = eff.now;
							eff.cmdOut = eff.now;
							eff.cmdLag = eff.now;
							eff.outTraj.Reset(eff.now);
						}
					}
				}
			}
			else
			{
				foreach (Effector eff in effs)
				{
					if (eff is SurfaceEffector seH)
					{
						seH.honestGate = Mathf.Max(Plugin.OvershootPitchPriority ? pitchNeed : 0f, HonestRollGate * rpLat);
					}
					eff.Evaluate(ref ctx);
					if (eff.active)
					{
						zero += eff.Mnow;
						if (!Engaged || !havePrev)
						{
							eff.cmd = eff.now;
							eff.cmdOut = eff.now;
							eff.cmdLag = eff.now;
							eff.outTraj.Reset(eff.now);
						}
					}
				}
			}
			phRebuild += System.Diagnostics.Stopwatch.GetTimestamp() - tReb0;
			mk[6] = System.Diagnostics.Stopwatch.GetTimestamp();
			Vector3 x3 = (havePrev ? ((omega - omegaPrev) / num) : Vector3.zero);
			Vector3 vector7 = omegaDotF.Step(x3, IndiTau, num);
			Vector3 vector8 = mNowF.Step(zero, IndiTau, num);
			float aIndiF = alphaIndiF.Step(alpha, IndiTau, num);
			float qIndiFv = qIndiF.Step(omega.y * 57.29578f, IndiTau, num);
			// faded out from 25 to 40 deg AoA: the lag matters at high dynamic pressure, and the stalled-foil slopes up there cost roll (bench, full pull + roll at 350 km/h, 64 deg: bank at 1 s 60 -> 48 deg without the fade)
			float hybLo = 1f - Mathf.InverseLerp(25f, 40f, Mathf.Abs(alpha));
			hybridDM = hybLo * ((Mathf.Abs(alpha) < 80f && tas > 30f) ? (mAlphaO * Mathf.Clamp(alpha - aIndiF, -5f, 5f) + mQO * Mathf.Clamp(omega.y * 57.29578f - qIndiFv, -20f, 20f)) : 0f);
			// 2.4.7 disturbance-rate lead. INDI's pitch target is I(wdot_des - d), d = the filtered measured pitch acceleration
			// less the effectors' modelled share (the airframe's own moment, gravity-driven AoA change, coupling, model error:
			// everything the effectors are not doing). INDI uses d as it was IndiTau ago and the nozzles need ~0.1 s more to
			// deliver, so while d falls steadily the effectors trail it by d' x that lag and the rate hold sags. After a
			// corkscrew release the fuselage loses ~15 deg/s^2 of nose-up in half a second as the AoA drops 57 -> 48 deg
			// (bench: d' about -30 deg/s^3; 24 Oct 22:26 log, 118.3 s: q -3.7). Most of that dip was the allocator leaving the
			// nozzles short (Allocator, CoordNewton); this covers the lag that remains (bench, corkscrew release at 1 km:
			// q min -1.38 -> -0.68). The lead takes d forward by d' x HybridIndiHighAoALead. Model-free on purpose:
			// the stalled-foil slopes the 1.9.0 hybrid lead uses have the wrong sign up here (the fuselage's moment falls as
			// the AoA falls through 57-41 deg). Above the 25-40 deg fade only, with the pitch stick released (zero-rate hold
			// or AoA-limit ride with the stick centred); with the stick in, the 2.4.6 law (a held pull + roll at 64 deg lost
			// roll rate to a high-AoA lead: bench, 350 km/h, bank at 1 s 74 -> 58 deg).
			float dHatY = vector7.y - IaInv[1, 1] * vector8.y;
			float dDotRaw = (dHatInit && num > 1E-05f) ? ((dHatY - dHatPrevY) / num) : 0f;
			dHatPrevY = dHatY;
			dHatInit = havePrev;
			// the airframe's pitch damping is in d too; its rate (mQ x the pitch acceleration) is the rate loop's own doing, not
			// a disturbance, and leading it fought the capture (bench: q 1.4 -> -0.5 deg/s at the moment the hold took over)
			dDotRaw -= DistLeadNoDamp ? (IaInv[1, 1] * mQO * vector7.y * 57.29578f) : 0f;
			dDotYF += (Mathf.Clamp(dDotRaw, -10f, 10f) - dDotYF) * (num / (DistLeadTau + num));
			// its horizon is the nozzles' response: with their thrust near idle the stabs carry the pitch and the lead only
			// shuffled them (bench, idle release at 380 km/h: q min -0.9 -> -2.0), so it fades out with T/W 0.4 -> 0.2
			float hiGate = (1f - hybLo) * (1f - Mathf.Clamp01((Mathf.Abs(sp) - 0.03f) / 0.15f)) * ((tas > 30f && Mathf.Abs(alpha) < 80f) ? 1f : 0f) * Mathf.InverseLerp(DistLeadTw0, DistLeadTw1, tw);
			distLeadGate = Mathf.MoveTowards(distLeadGate, hiGate, num / 0.15f);
			distLeadAcc = distLeadGate * Mathf.Clamp(dDotYF * Plugin.HybridIndiHighAoALead, -DistLeadMax * (MathF.PI / 180f), DistLeadMax * (MathF.PI / 180f));
			omegaPrev = omega;
			velPrev = rb.velocity;
			vCgPrev = (AirDataAtCg && vCg.sqrMagnitude > 0f) ? vCg : rb.velocity;
			havePrev = true;
			float num4 = 0f;
			float num5 = 0f;
			Vector3 zero2 = Vector3.zero;
			Vector3 zero3 = Vector3.zero;
			float zNowAll = 0f;
			foreach (Effector eff2 in effs)
			{
				if (eff2.active)
				{
					if (eff2.M != null && eff2.N >= 2)
					{
						zNowAll += eff2.MAt(eff2.now).z;
					}
					Vector3 vector9 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
					Vector3 vector10 = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
					for (int i = 0; i < eff2.N; i++)
					{
						vector9 = Vector3.Max(vector9, eff2.M[i]);
						vector10 = Vector3.Min(vector10, eff2.M[i]);
					}
					zero2 += vector9;
					zero3 += vector10;
				}
			}
			num4 = zero2.y;
			num5 = zero3.y;
			float num6 = IaInv[1, 1];
			aUp = Mathf.Clamp(aUpF.Step((vector7.y + (num4 - vector8.y) * num6) * 57.29578f, 0.15f, num), 5f, 1500f);
			aDn = Mathf.Clamp(aDnF.Step((0f - (vector7.y + (num5 - vector8.y) * num6)) * 57.29578f, 0.15f, num), 5f, 1500f);
			float num7 = 0f;
			float num8 = 0f;
			rollSlew = 100f;
			foreach (SurfaceEffector surface2 in surfaces)
			{
				if (!surface2.active)
				{
					continue;
				}
				float num9 = ((surface2.kind == SurfaceKind.Flaperon || surface2.kind == SurfaceKind.Aileron) ? 1f : ((surface2.kind == SurfaceKind.Stabilator) ? Mathf.Lerp(Plugin.StabRollShare, 0.25f, Mathf.InverseLerp(160f, 240f, ias)) : 0f));
				if (num9 != 0f)
				{
					float num10 = float.MinValue;
					float num11 = float.MaxValue;
					for (int j = 0; j < surface2.N; j++)
					{
						num10 = Mathf.Max(num10, surface2.M[j].x);
						num11 = Mathf.Min(num11, surface2.M[j].x);
					}
					num7 += num9 * 0.5f * (num10 - num11);
					if (surface2.kind != SurfaceKind.Stabilator)
					{
						num8 += 0.5f * (num10 - num11);
					}
					if (surface2.kind != SurfaceKind.Stabilator && surface2.rate > 0f)
					{
						rollSlew = Mathf.Min(rollSlew, surface2.rate / Mathf.Max(1f, 0.5f * (surface2.max - surface2.min)));
					}
				}
			}
			aRoll = Mathf.Clamp(aRollF.Step(num7 * IaInv[0, 0] * 57.29578f, 0.2f, num), 10f, 5000f);
			rollCtrlHalf = aRoll * (MathF.PI / 180f) / Mathf.Max(IaInv[0, 0], 1E-09f);
			aRollWing = Mathf.Clamp(aRollWF.Step(num8 * IaInv[0, 0] * 57.29578f, 0.2f, num), 10f, 5000f);
			aYaw = Mathf.Clamp(aYawF.Step(0.5f * (zero2.z - zero3.z) * IaInv[2, 2] * 57.29578f, 0.2f, num), 5f, 3000f);
			// 2.6.9: how much of the airframe's yaw range all the effectors together are using now (0 = centred, 1 = at the stops):
			// the same effectors and tables as aYaw, read from where they are - whatever carries the yaw at this AoA
			float yawUseRaw = Mathf.Abs(zNowAll - 0.5f * (zero2.z + zero3.z)) / Mathf.Max(1E-06f, 0.5f * (zero2.z - zero3.z));
			yawUseAllF += (Mathf.Clamp01(yawUseRaw) - yawUseAllF) * (num / (0.05f + num));
			float t = Mathf.Clamp01((Mathf.Abs(alpha) - 20f) / 20f);
			float num12 = ((!flightAssist) ? Plugin.MpoGPos : Plugin.GPos);
			float num13 = ((!flightAssist) ? Plugin.MpoGNeg : Plugin.GNeg);
			float t2 = Mathf.Clamp01(Mathf.Max((nz - 0.85f * num12) / (0.15f * num12), (0.85f * num13 - nz) / (0.15f * (0f - num13))));
			stabPitchFrac = 1f;
			foreach (SurfaceEffector surface3 in surfaces)
			{
				if (surface3.kind == SurfaceKind.Stabilator && surface3.active)
				{
					stabPitchFrac = Mathf.Min(stabPitchFrac, surface3.pitchFrac);
				}
			}
			float t3 = 1f - stabPitchFrac;
			pitchNeed = 0f;
			pitchNeedUp = 0f;
			pitchNeedDn = 0f;
			if (Plugin.OvershootPitchPriority && Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off)
			{
				float num47 = Mathf.Sign(alpha) * (omega.y * 57.29578f - qT);
				float pnA0 = 15f;
				float pnRamp = 10f;
				if (Plugin.AsymRollCoupling && AsymPitchPrio > 0f && alpha > 0f && asymCapPosEff < 60f)
				{
					float pnG = AsymPitchPrio * Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / CoupHeldDySpan);
					pnA0 = Mathf.Lerp(15f, Mathf.Min(15f, asymCapPosEff - AsymPitchPrioLead), pnG);
					pnRamp = Mathf.Lerp(10f, 4f, pnG);
				}
				pitchNeed = Mathf.Clamp01((num47 - 2f) / 8f) * Mathf.Clamp01((Mathf.Abs(alpha) - pnA0) / pnRamp);
				if (prot)
				{
					pitchNeed = 1f;
				}
				pitchNeedUp = pitchNeed;
				// 2.9.0: the capped sweep toward zero AoA (last tick's cap state; the cap runs later in the tick). Only while the cap
				// clipped the command on the toward-zero side (positive AoA: the stick asked for more nose-down than the cap gave,
				// yawSatPitchDbg < 0; negative AoA: more nose-up, > 0) and the nose is moving toward zero faster than the capped
				// command. Not into the coupling roll gate (gateC reads pitchNeedUp): the rotation is not cut, the stabs are taken
				// back for the hold. (Bench: gated on the raw sign it also fired on a push to -20 deg AoA under a roll, where the cap
				// clipped the sweep that grows |AoA| and the pitch merely lagged it: pitch weight 8 -> 16 for 0.2 s, 1.3 deg more
				// sideslip in the pull-through after.)
				pitchNeedDn = 0f;
				if (sweepClipOn && Mathf.Sign(alpha) * sweepClipDbg < -1f)
				{
					float dnOver = Mathf.Sign(alpha) * (qT - omega.y * 57.29578f);
					pitchNeedDn = Mathf.Clamp01((dnOver - SweepHoldErr0) / Mathf.Max(0.5f, SweepHoldErrSpan)) * Mathf.Clamp01((Mathf.Abs(alpha) - 15f) / 10f);
				}
				pitchNeed = Mathf.Max(pitchNeed, pitchNeedDn);
				t3 *= 1f - pitchNeed;
			}
			alloc.axisWeight = new Vector3(Mathf.Lerp(Mathf.Lerp(1f, 0.35f, t), 1.5f, t3), Mathf.Lerp(Mathf.Lerp(8f, 15f, t2), 1f, t3), Mathf.Lerp(Mathf.Lerp(Plugin.YawPriority, (YawPrioHi > 0f && Plugin.SlipYawWeightOn) ? Mathf.Lerp(Mathf.Max(2f, Plugin.YawPriority), Mathf.Min(Plugin.YawPriority, YawPrioHi), YawHiSlipGate()) : Mathf.Max(2f, Plugin.YawPriority), t), Mathf.Max(2.5f, Plugin.YawPriority), t3));
			alloc.PairedTrimSearch = PairedTrim && Plugin.StabTvcPairedSearch;
			// 2.0.6 rudder pitch aid: the symmetric rudder split is cheap only while no roll or yaw is asked for (stick,
			// pedal, or a roll still running); with lateral input the rudders go back to yaw, as the stabs do.
			if (!Plugin.RudderPitchAid)
			{
				foreach (SurfaceEffector seT0 in surfaces)
				{
					if (seT0.kind == SurfaceKind.Rudder && seT0.partner != null)
					{
						seT0.symCost = 2f;
						seT0.symTarget = 0f;
						seT0.noPitch = true;
					}
				}
			}
			else
			{
				float latNow = Mathf.Max(Mathf.Max(Mathf.Clamp01((Mathf.Abs(sr) - 0.03f) / 0.12f), Mathf.Clamp01((Mathf.Abs(sy) - 0.03f) / 0.12f)), Mathf.Clamp01((Mathf.Abs(pS) - 5f) / 20f));
				toeLatGate = Mathf.Max(latNow, toeLatGate - num / ToeLatRelease);
				float toeCost = Mathf.Lerp(Plugin.RudderToeCost, 2f, toeLatGate);
				// 2.0.7 toe schedule: the rudders split with the pitch demand (the pitch loop's desired pitch acceleration as a
				// share of the authority in that direction, x RudderToeGain), up to RudderToeMax each way, in every airborne
				// mode and at any AoA (a symmetric split makes no net roll, so the rudder fade does not apply to it); the
				// allocator pulls the pair toward it (and still uses more split when the stabs and nozzles run short). Zero
				// with lateral input. Pair value +d (both rudders at +d) is nose-up across the whole AoA range in the surface
				// model; nose-down authority is about 40 % of nose-up.
				float nuY = omegaDotDes.y * 57.29578f;
				float share = (nuY >= 0f) ? (nuY / Mathf.Max(20f, aUp)) : (nuY / Mathf.Max(20f, aDn));
				float toeT = (Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off) ? (Plugin.RudderToeMax * Mathf.Clamp(share * Plugin.RudderToeGain, -1f, 1f) * (1f - toeLatGate)) : 0f;
				toeTgtF = Mathf.MoveTowards(toeTgtF, toeT, 100f * num);
				foreach (SurfaceEffector seT in surfaces)
				{
					if (seT.kind == SurfaceKind.Rudder && seT.partner != null)
					{
						seT.symCost = toeCost;
						seT.symTarget = toeTgtF;
						// in a roll the allocator does not see the rudders' pitch at all (2.0.5 model), so it has no reason to
						// split them; takes effect from the next tick's surface model
						seT.noPitch = toeLatGate > 0.5f;
					}
					if (seT.kind == SurfaceKind.Rudder && Mathf.Abs(toeTgtF) > 0.01f)
					{
						// open the window to the toe value past the rudder fade too (both rudders the same value = no net roll)
						seT.lo = Mathf.Clamp(Mathf.Min(seT.lo, toeTgtF), seT.min, seT.max);
						seT.hi = Mathf.Clamp(Mathf.Max(seT.hi, toeTgtF), seT.min, seT.max);
					}
				}
			}
			// active while the pedals are in (and FightPedalRelease s after): rolls without pedal allocate as in 1.8.40
			float pedAct = Mathf.Clamp01((Mathf.Abs(sy) - 0.03f) / 0.15f);
			fightPed = Mathf.Max(pedAct, fightPed - num / Mathf.Max(0.05f, FightPedalRelease));
			float rollIn = Mathf.Clamp01((Mathf.Abs(sr) - 0.05f) / 0.2f);
			fightRollHold = Mathf.Max(rollIn, fightRollHold - num / Mathf.Max(0.05f, FightRollRelease));
			// 2.2.16: RollFightReleased (default on) = FightMode 2: the roll-fight cost also acts while the lateral stick is
			// centred (and FightRollRelease s after). Without it, a crossed set (one stab split against the flaperons, ailerons
			// and rudders at their stops) could hold with the stick centred: 19 Oct 18:05 / 18:32 logs, TVC off.
			int fightMode = (FightMode == 0 && Plugin.RollFightReleased) ? 2 : FightMode;
			// 2.2.16b: with the stick released the cost waits for the pitch to settle (pitch-rate command steady, pitch-rate
			// error under 2-6 deg/s): on a release from a rolling pull the stabs carry the nose-down and their split against
			// the wing is the price of that; pricing it there moved the second stab nose-down too (bench 600 km/h, release
			// at 30 deg AoA: pitch-rate dip -4 -> -8 deg/s). The locked sets formed and held with the pitch settled.
			// 2.2.21: not with a fin lost or the deceleron split working: the surfaces are crossed on purpose there (wing for
			// yaw, stabs for the roll it makes), and pricing it froze the stabs for pitch (19 Oct 22:05 log, fins gone)
			float relFin = (yawHealth > 0.99f && decelF <= 0.01f) ? 1f : 0f;
			float relQuiet = (Plugin.RollFightReleased && FightMode == 0) ? relFin * ((RelQuietHold ? (1f - pfQHold) : 1f) * (1f - Mathf.Clamp01((Mathf.Abs(omega.y * 57.29578f - qT) - RelQuietErr0) / Mathf.Max(0.1f, RelQuietErrSpan)))) : 1f;
			float fightGate = (fightMode == 1) ? 1f : ((fightMode == 2) ? Mathf.Max(fightPed, (1f - fightRollHold) * relQuiet) : fightPed);
			alloc.fightK = ((Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off) ? (Mathf.Max(0f, Plugin.RollFightCost) * fightGate) : 0f);
			// 1.8.42: stab-vs-nozzle pitch fight, while a roll is commanded (and RpRelease after)
			// not while the pitch-rate command is moving (captures, releases: the nozzles are the fast pitch effector there),
			// in AoA protection or on an AoA run-away
			pfQHold = Mathf.Max(Mathf.Clamp01(Mathf.Abs(qTraj.v) / Mathf.Max(1f, PitchFightQdot)), pfQHold - num / Mathf.Max(0.05f, PitchFightQHold));
			// and not while the nose is coming up faster than commanded: the nozzles have to be free to stop it
			float pfErr = Mathf.Sign(alpha) * (omega.y * 57.29578f - qT);
			if (PitchFightAbsErr) pfErr = Mathf.Abs(pfErr);
			float pfGate = rpLat * (1f - pfQHold) * (prot ? 0f : (1f - pitchNeed)) * (1f - Mathf.Clamp01((pfErr - PitchFightErr0) / Mathf.Max(0.5f, PitchFightErrSpan)));
			alloc.pitchFightSign = ((alpha >= 0f) ? 1f : (-1f));
			alloc.pitchFightK = ((Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off) ? (Mathf.Max(0f, Plugin.RollPitchFightCost) * pfGate) : 0f);
			float num14 = 1f + Mathf.Max(0f, Plugin.AsymAxisBoost) * Mathf.Clamp01((asymUNow - 0.5f) / 0.5f);
			if (num14 > 1.001f)
			{
				alloc.axisWeight = new Vector3(alloc.axisWeight.x * num14, alloc.axisWeight.y, alloc.axisWeight.z * num14);
			}
			foreach (SurfaceEffector surface4 in surfaces)
			{
				if (surface4.kind == SurfaceKind.Stabilator && surface4.partner != null)
				{
					surface4.antiCost = Mathf.Lerp(0.0002f, Mathf.Lerp(Plugin.StabRollCost, 0.01f, Mathf.InverseLerp(160f, 240f, ias)), stabPitchFrac);
					surface4.antiCost = Mathf.Lerp(surface4.antiCost, Mathf.Max(surface4.antiCost, AsymStabAntiCost), pitchNeed * Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / CoupHeldDySpan));
					// 2.15.0: the lateral reserve prices the stabs' pitch use only where they have pitch use (stabPitchFrac). Past their
					// pitch stall (65 deg AoA, stabPitchFrac 0) the symmetric movement the allocator needs for yaw and roll is not pitch
					// trim, and x 71 on it held the stabs still for 0.5 s at a no-fin reversal while the yaw loop was 60 deg/s^2 short
					// (19:54 log, 164.8 s: symCost 0 -> 0.28 at the stick, stabs parked until the roll target fell away).
					// 2.15.0: the lateral reserve keeps symmetric stab travel for anti-symmetric use. Where the estimator counts the
					// stabs' SYMMETRIC motion as the yaw capacity (rudW -> 0: fins gone or rudders faded) that reserve starves the
					// yaw it is meant to serve (19:54 log, 164.8 s: stabs parked 0.5 s on symCost 0.28), so it goes with the pitch
					// range the stabs still have. With the rudders present it is unchanged.
					float rudWsym = Mathf.Clamp01(rudderAuth) * Mathf.Clamp01(yawHealth / 0.5f);
					surface4.symCost = Plugin.StabTrimCost * (1f + LatTrimGain * latReserve * (LatReservePitchFrac ? Mathf.Lerp(stabPitchFrac, 1f, rudWsym) : 1f));
					// 2.2.14 test: extra cost on the stab split at high AoA (roll moved to the rudders / flaperons / sideslip)
					if (HiAoAStabRollCost > 0f)
					{
						surface4.antiCost = Mathf.Max(surface4.antiCost, HiAoAStabRollCost * Mathf.Clamp01((Mathf.Abs(alpha) - HiAoAStabRollA0) / Mathf.Max(1f, HiAoAStabRollSpan)));
					}
					// 2.2.5 test switch: stabs pitch only (any antisymmetric deflection made prohibitively expensive)
					if (Plugin.StabsPitchOnly && !isAI)
					{
						surface4.antiCost = StabsPitchOnlyCost;
					}
				}
			}
			// 2.2.29 (RelLatRollPrio): with the lateral stick and pedals released, the roll axis is weighted up by up to this
			// factor as the body roll runs ahead of its command (2 -> 10 deg/s). Stopping a high-AoA rotation brakes the yaw
			// with the rudders, whose inverse roll then rolled the jet toward the yaw with the ailerons already at their stops
			// (20 Oct 10:35 log, falling-leaf stops at 198 / 285 s: body roll 14 -> 34 deg/s against a 19 command)
			if (RelLatRollPrio > 1f)
			{
				float relLat = 1f - Mathf.Max(Mathf.Clamp01((Mathf.Abs(sr) - 0.03f) / 0.12f), Mathf.Clamp01((Mathf.Abs(sy) - 0.03f) / 0.12f));
				float pErr = Mathf.Abs(omega.x * 57.29578f - pCmd);
				// 2.9.0: not while the capped sweep's pitch hold has the stabs (pitchNeedDn)
				float rlp = Mathf.Lerp(1f, RelLatRollPrio, relLat * Mathf.Clamp01((pErr - 2f) / 8f) * (1f - pitchNeedDn));
				relLatPrioDbg = rlp;
				if (rlp > 1.001f)
				{
					alloc.axisWeight = new Vector3(alloc.axisWeight.x * rlp, alloc.axisWeight.y, alloc.axisWeight.z);
				}
			}
			float relStick = 1f - Mathf.Clamp01((Mathf.Abs(sp) - 0.03f) / 0.15f);
			float relErr = Mathf.Clamp01((Mathf.Abs(omega.y * 57.29578f - qT) - 1f) / 8f);
			pitchPrio = Mathf.Lerp(1f, Mathf.Max(1f, Plugin.ReleasedPitchPriority), stabPitchFrac * relStick * relErr);
			if (pitchPrio > 1.001f)
			{
				alloc.axisWeight = new Vector3(alloc.axisWeight.x, alloc.axisWeight.y * pitchPrio, alloc.axisWeight.z);
			}
			// 2.4.6 telemetry: the allocator's axis weights after the priority terms (roll, pitch, yaw)
			axWDbg = alloc.axisWeight;
			// 2.9.1: also while the capped sweep's pitch hold has the stabs (pitchNeedDn). 16:29 log (2.9.0), 75.0 s: the hold took
			// the weights (pitch 8, roll 0.5) and the allocator still left the left stab at -6 while it sent the right from -30 to
			// +35 - in command space one stab's travel covered the pitch increment, so the other stayed on the roll; the servo
			// took 0.9 s over the 65 deg and the nose dropped 40 -> 20 deg AoA meanwhile, then the same stab swung back +35 -> -30
			// for the push once the hold let go: the "hitch" at 17 deg AoA (q -2 for 0.3 s, then -47). Bounded to what the servos
			// reach in 0.1 s, the increment is spread over both stabs and the nozzles and no stab swings stop to stop.
			// 2.41.0 (PushPlanReach): through a planned push the allocator balances the nozzles and wings against where the stabs
			// will be within PushPlanReachTau s, not where they are sent (see Allocator.ReachPass)
			// 2.43.0: the stab rate bound (ReleasedStickStabRateBound, 1.8.14 / 2.9.1) is replaced by the allocator's reach model, in the same
			// places (pitch stick released, or the capped sweep's pitch hold, scaled by the stabs' pitch share) and under a planned push.
			// The bound clipped each stab command to 0.1 s of servo travel. The reach model keeps the command and places the other surfaces
			// against where the stab will be in PushPlanReachTau (Allocator.ReachPass), then compares that plan with the stab moved only
			// as far as it gets in that time, over both horizons and the stab's whole transit (Allocator.TwoHorizon): the bound is the
			// second plan, chosen when the first one's transit costs more than its destination saves. Not under a plan (2.41.0 unchanged).
			// The two-horizon pick matters: with the reach pass alone a roll entry at -43 deg AoA (23:37 log, 50.6 s) sent the right stab to
			// the far stop the wrong way through its range, the roll lagged 0.4 s (pinned command dip 2.8 -> 5.8 deg/s, sideslip 5.7 -> 7.4).
			float relG = Mathf.Max(relStick, pitchNeedDn) * stabPitchFrac;
			bool reachHere = (PushPlan && pushPlanOn) || relG > 0.01f;
			stabRateBound = false;
			foreach (SurfaceEffector seR in surfaces)
			{
				seR.reachTau = (seR.kind == SurfaceKind.Stabilator && reachHere && PushPlanReachTau > 0f && Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off) ? PushPlanReachTau : 0f;
				seR.twoHorizon = !(PushPlan && pushPlanOn);
			}
			// 1.8.40: while rolling at low/moderate AoA, the stabs are not driven past their lift peak in any pass: beyond
			// it the down-going stab's lift falls off and its nose-down turns into pitch-up exactly when it rolls hardest.
			// 2.9.2: also during the capped sweep toward zero AoA with the stabs' pitch range back (see SweepHoldStallWindow)
			sweepHoldWinDbg = !isAI && sweepClipOn && Mathf.Sign(alpha) * sweepClipDbg < -1f && stabPitchFrac > 0.05f && Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off;
			if (sweepHoldWinDbg)
			{
				foreach (SurfaceEffector sw in surfaces)
				{
					if (sw.kind == SurfaceKind.Stabilator && sw.active)
					{
						StallWindow(sw, sw.stallAoA + RpMainStallMargin);
					}
				}
			}
			// 2.2.15: with the lateral stick and pedals centred and the AoA under StabParkAoA, a stab past its lift peak is walked
			// back to the attached side. On the stall plateau its modelled moment hardly changes with deflection, so the
			// allocator had no gradient to bring it back and held every other surface against it: 19 Oct 18:05 log (TVC off),
			// 212-227 s, left stab at -30 (local AoA 31-35 deg against a 25 deg peak), flaperons and rudders at their stops
			// against it, for 15 s with the stick centred and the AoA falling 45 -> 0.
			// 2.3.0 (SettledStallWindow): the 2.2.15 window only acted below 30 deg AoA and let a stab rest ON its lift peak
			// (peak + 3 deg). A stab resting there has no modelled gradient in pitch or roll, so nothing could move it, while it
			// made its maximum lift: the other stab had to match that lift to cancel the roll (both at maximum nose-down) and
			// the nozzles cancelled the pitch with nose-up to their stop. Bench replay of the 21 Oct 21:28 log, release from the
			// 65 deg corkscrew at 68 s: calm from 1.5 to 4.8 s, stabs walked -3/-9 -> -9/-22, nozzles 3 -> 20, rudders +-10.
			// Now: with the lateral stick and pedals centred and the lateral rates settled (the stop finished), the stabilators
			// and rudders may not rest past (peak - SettledStallMargin) deg of local AoA, at any AoA where such an attached range
			// exists (none: the window is left open). The gate latches on below SettledRateOn deg/s of body roll and yaw and
			// off above SettledRateOff, or with lateral input.
			if (Plugin.StabParkEscape && !isAI && Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off)
			{
				float latRate = Mathf.Max(Mathf.Abs(omega.x), Mathf.Abs(omega.z)) * 57.29578f;
				bool latIn = Mathf.Abs(sr) >= 0.05f || Mathf.Abs(sy) >= 0.05f;
				if (latIn || latRate > Plugin.SettledRateOff)
				{
					settledWin = false;
				}
				else if (latRate < Plugin.SettledRateOn)
				{
					settledWin = true;
				}
				if (settledWin)
				{
					foreach (SurfaceEffector sw in surfaces)
					{
						if ((sw.kind == SurfaceKind.Stabilator || (SettledWindowRudders && sw.kind == SurfaceKind.Rudder)) && sw.active)
						{
							SettledWindow(sw, Mathf.Max(5f, sw.stallAoA - Plugin.SettledStallMargin), Plugin.SettledWalkRate, num);
						}
					}
				}
			}
			else
			{
				settledWin = false;
			}
			settledWinDbg = settledWin;
			nAlphaTimer += num;
			if (nAlphaTimer > 0.1f)
			{
				nAlphaTimer = 0f;
				long tEnv0 = System.Diagnostics.Stopwatch.GetTimestamp();
				ComputeEnvelope(vector, num2, transform.right, vector6);   // 3.1.3: with the hybrid slopes (ComputeHybridSlopes) inside
				phEnv += System.Diagnostics.Stopwatch.GetTimestamp() - tEnv0;
			}
			long tAsym0 = System.Diagnostics.Stopwatch.GetTimestamp();
			mk[7] = tAsym0;
			AsymStep(num, num2, rotation, ref ctx, vector7, vector8);
			phAsym += System.Diagnostics.Stopwatch.GetTimestamp() - tAsym0;
			mk[8] = System.Diagnostics.Stopwatch.GetTimestamp();
			tw = tvc.totalThrust / (mass * 9.81f);
			float num15 = 0.5f * num2 * tas * tas;
			float num16 = 13781.25f;
			float num17 = Mathf.Clamp01(num15 / num16);
			num17 *= num17;
			float t4 = Mathf.Clamp01(Plugin.LowQRegRelief) * Mathf.InverseLerp(40f, 60f, ias);
			alloc.regScale = Mathf.Lerp(1f, Mathf.Max(0.02f, num17), t4);
			if (!revFlow && Mathf.Abs(alpha) > 100f)
			{
				revFlow = true;
			}
			else if (revFlow && Mathf.Abs(alpha) < 80f)
			{
				revFlow = false;
			}
			if (revFlow != revFlowLogged)
			{
				revFlowLogged = revFlow;
				Plugin.Log.LogInfo(revFlow ? ((Plugin.RevFlowRollSense && Plugin.RevFlowNoseAxis) ? "F-22E FCS: reversed flow (flying backwards) - stick and pedals turn the nose the way they point." : "F-22E FCS: reversed flow (flying backwards) - roll sense flipped to body axes.") : "F-22E FCS: forward flow.");
			}
			bool flag3 = false;
			bool flag4 = false;
			bool flag5 = false;
			LandingGear landingGear = NoseGear();
			foreach (LandingGear gear2 in gears)
			{
				if (!(gear2 == null) && gear2.WeightOnWheel(0.05f))
				{
					flag3 = true;
					if (gear2 == landingGear)
					{
						flag5 = true;
					}
					else
					{
						flag4 = true;
					}
				}
			}
			groundFrames = (flag3 ? Mathf.Min(groundFrames + 1, 1000) : 0);
			onGround = flag3;
			airTime = (flag3 ? 0f : (airTime + num));
			bool flag6 = flag3 || airTime < 0.2f;
			noseUpTime = ((landingGear != null && flag4 && !flag5 && tas > Plugin.RotationMinSpeed) ? (noseUpTime + num) : 0f);
			if (noseUpTime > 0.15f)
			{
				rotating = true;
				noseDownTime = 0f;
			}
			else if (rotating)
			{
				noseDownTime = (flag5 ? (noseDownTime + num) : 0f);
				if (!flag3 || noseDownTime > 0.3f || tas < Plugin.RotationMinSpeed - 8f)
				{
					rotating = false;
				}
			}
			mainsOnGround = flag4;
			// 2.0.6: pitch-rate command in every airborne mode on request (MPO stays MPO: it is rate command already)
			FcsMode airMode = (!flightAssist) ? FcsMode.MPO : ((!ac.gearDeployed) ? (isAI ? FcsMode.AIRate : (Plugin.RateCommandOnly ? FcsMode.Rate : FcsMode.AoAG)) : FcsMode.GearDownRate);
			FcsMode fcsMode;
			if (Plugin.GroundRateLaw && landingGear != null)
			{
				// 2.0.6 ground pitch: the gear-down rate law flies the pitch axis on the runway as soon as the pilot pulls
				// above RotationMinSpeed, and keeps it through the rotation and the lift-off. 2.0.5 flew an open-loop stick
				// map until the nose wheel had been off for 0.15 s, then an 8 deg/s rotation law, and dropped back to the
				// open-loop map for 0.2 s every time the mains' weight-on-wheel flickered as the wing took the weight: full
				// stab and nozzle deflection for those frames, the spike at lift-off. Contact here is 'touching' (0.2 % of
				// the leg travel) rather than 'carrying weight' (5 %), so a lightly loaded leg still counts.
				bool noseC = landingGear.WeightOnWheel(0.002f);
				bool mainC = false;
				foreach (LandingGear gC in gears)
				{
					if (gC != null && gC != landingGear && gC.WeightOnWheel(0.002f))
					{
						mainC = true;
					}
				}
				bool anyC = noseC || mainC;
				wheelsTouch = anyC;
				gContactAir = anyC ? 0f : (gContactAir + num);
				if (anyC && tas >= Plugin.RotationMinSpeed)
				{
					if (!groundRateOn)
					{
						if (!noseC || sp > 0.08f)
						{
							groundRateOn = true;
							gNeutralT = 0f;
						}
					}
					else
					{
						gNeutralT = (noseC && mainC && sp < 0.03f) ? (gNeutralT + num) : 0f;
						if (gNeutralT > 0.3f)
						{
							groundRateOn = false;
						}
					}
				}
				else if (anyC && tas < Plugin.RotationMinSpeed - 8f)
				{
					groundRateOn = false;
				}
				else if (!anyC && gContactAir > 0.5f)
				{
					groundRateOn = false;
				}
				noseContactLaw = noseC && groundRateOn;
				if (anyC)
				{
					fcsMode = groundRateOn ? FcsMode.GearDownRate : FcsMode.Ground;
				}
				else
				{
					// a bounce in the open-loop ground mode stays there for 0.2 s, as before, unless the pilot pulls
					fcsMode = (Mode == FcsMode.Ground && gContactAir < 0.2f && sp <= 0.08f && tas < 110f) ? FcsMode.Ground : (groundRateOn ? FcsMode.GearDownRate : airMode);
				}
			}
			else
			{
				noseContactLaw = false;
				fcsMode = ((rotating && flag4) ? FcsMode.GearDownRate : ((flag6 && (tas < 110f || groundFrames > 10)) ? FcsMode.Ground : airMode));
			}
			if (fcsMode != Mode)
			{
				if (Mode == FcsMode.Ground || Mode == FcsMode.Off)
				{
					qT = omega.y * 57.29578f;
					qTprev = qT;
					qTraj.Reset(qT);
					pTraj.Reset(0f);
					pS = 0f;
					aInt = 0f;
					capIUp = (capIDn = 0f);
					gInt = 0f;
				}
				Mode = fcsMode;
			}
			if (Mode != lastLoggedMode)
			{
				Plugin.Log.LogInfo("F-22E FCS mode: " + Mode);
				lastLoggedMode = Mode;
			}
			gbLogTimer += num;
			if (Plugin.GroundBrakeLog && flag3 && gbLogTimer > 1f)
			{
				gbLogTimer = 0f;
				LandingGear landingGear2 = NoseGear();
				Plugin.Log.LogInfo(string.Format("F-22E FCS ground brake: mode {0}, mainsWow {1}, noseWow {2} (nose gear {3}), ", Mode, flag4, flag5, (landingGear2 != null) ? landingGear2.name : "none") + $"tas {tas:F1} (min {Plugin.GroundBrakeMinSpeed:F0}), brake {inp.brake:F2}, throttle {inp.throttle:F2} (< {Plugin.GroundBrakeThrottle:F2}), " + string.Format("rotating {0}, blend {1:F2}, airbrake {2:F2} (split {3})", rotating, groundBrake, airBrake, SplitDeploy ? "out" : "stowed"));
			}
			float num18 = num / (Plugin.StickSmoothing + num);
			spF += (Mathf.Clamp(0f - inp.pitch, -1f, 1f) - spF) * num18;
			srF += (Mathf.Clamp(inp.roll, -1f, 1f) - srF) * num18;
			syF += (Mathf.Clamp(inp.yaw, -1f, 1f) - syF) * num18;
			sp = Mathf.MoveTowards(sp, spF, 8f * num);
			sr = Mathf.MoveTowards(sr, srF, RollStickRate * num);
			sy = Mathf.MoveTowards(sy, syF, 8f * num);
			// 2.2.6: the brake command: the brake key, or with ThrottleIndependentAirbrake off zero throttle instead (stock rule).
			// In the air it is the speedbrake (AirbrakeOut), on the main wheels the rollout aero-brake below.
			// F-22E 2.4.15 / Ifrit 0.5.5 / Revoker 0.2.6: with ThrottleIndependentAirbrake off the brake key no longer opens it: the stock
			// rule only (Airbrake.Update and the split surfaces open at throttle == 0; the stock Brake action is the wheel brake alone).
			// Before, the brake key opened it as well, so a controller with Brake and Countermeasures on one button popped the
			// airbrake with every flare in the air.
			bool abCmd = Plugin.ThrottleIndependentAirbrake ? (Mathf.Clamp01(inp.brake) > 0.05f) : (rawThrottle <= 0f);
			airBrake = Mathf.MoveTowards(airBrake, abCmd ? 1f : 0f, 2f * num);
			gsPedRaw = Mathf.Clamp(inp.yaw, -1f, 1f);
			long tGs0 = System.Diagnostics.Stopwatch.GetTimestamp();
			mk[9] = tGs0;
			GroundSteer(num, flag4, flag5);
			phGround += System.Diagnostics.Stopwatch.GetTimestamp() - tGs0;
			mk[10] = System.Diagnostics.Stopwatch.GetTimestamp();
			Vector3 vector11;
			tvc.anchorK = 0f;
			if (Mode == FcsMode.Ground)
			{
				float num19 = 0f - sp;
				float num20 = sr;
				if (gsActive && gsMainsContact)
				{
					num20 = Mathf.Lerp(Mathf.Clamp((0f - phi) / GsPhiFull, -1f, 1f), sr, Mathf.Clamp01(Mathf.Abs(sr) / 0.3f));
				}
				float num21 = (gsActive ? gsRud : sy);
				float target = 0f;
				bool flag7 = flag4;
				bool flag8 = inp.brake > 0.05f || (!Plugin.ThrottleIndependentAirbrake && rawThrottle <= 0f) || (Plugin.GroundBrakeThrottle >= 0f && inp.throttle < Plugin.GroundBrakeThrottle);
				// 2.4.15: the rollout aero-brake is an FCS addition: off with ThrottleIndependentAirbrake off (stock: the zero-throttle
				// panels and the wheel brake only)
				if (Plugin.GroundAeroBrake > 0f && Plugin.ThrottleIndependentAirbrake && flag7 && flag8 && tas >= Plugin.GroundBrakeMinSpeed)
				{
					target = Plugin.GroundAeroBrake * airBrake;
				}
				groundBrake = Mathf.MoveTowards(groundBrake, target, 1.5f * num);
				if (brakeBias == null || brakeBias.Length != surfaces.Count)
				{
					brakeBias = new float[surfaces.Count];
				}
				for (int k = 0; k < surfaces.Count; k++)
				{
					brakeBias[k] = 0f;
				}
				if (groundBrake > 0.01f)
				{
					for (int l = 0; l < surfaces.Count; l++)
					{
						SurfaceEffector surfaceEffector = surfaces[l];
						if (surfaceEffector.active && surfaceEffector.kind != SurfaceKind.Rudder)
						{
							float teUp = surfaceEffector.teUp;
							if (teUp != 0f)
							{
								brakeBias[l] = groundBrake * teUp * ((surfaceEffector.kind == SurfaceKind.Stabilator) ? Plugin.GroundBrakeStab : Plugin.GroundBrakeFlap);
							}
						}
					}
					for (int m = 0; m < surfaces.Count; m++)
					{
						SurfaceEffector surfaceEffector2 = surfaces[m];
						if (surfaceEffector2.kind == SurfaceKind.Rudder && surfaceEffector2.partner is SurfaceEffector surfaceEffector3 && surfaceEffector2.active && surfaceEffector3.active)
						{
							int num22 = surfaces.IndexOf(surfaceEffector3);
							if (num22 >= 0)
							{
								float num23 = groundBrake * Plugin.GroundBrakeRudder;
								brakeBias[m] = surfaceEffector2.pairSign * num23;
								brakeBias[num22] = (0f - surfaceEffector3.pairSign) * num23;
							}
						}
					}
				}
				for (int n = 0; n < surfaces.Count; n++)
				{
					SurfaceEffector surfaceEffector4 = surfaces[n];
					// 2.0.7: on the runway the rudders split with the pitch stick too (pull = the nose-up split), as for rotation
					float gToe = (Plugin.RudderPitchAid && surfaceEffector4.kind == SurfaceKind.Rudder) ? ((0f - num19) * Plugin.RudderToeMax) : 0f;
					surfaceEffector4.cmd = Mathf.Clamp(num19 * surfaceEffector4.origPitchRange + num20 * surfaceEffector4.origRollRange + num21 * surfaceEffector4.origYawRange + brakeBias[n] + gToe, surfaceEffector4.min, surfaceEffector4.max);
				}
				tvc.cmd = Mathf.Clamp((0f - num19) * tvc.max + groundBrake * Plugin.GroundBrakeTvc, tvc.min, tvc.max);
				qCmd = (qT = omega.y * 57.29578f);
				qTprev = qT;
				qTraj.Reset(qT);
				pTraj.Reset(0f);
				pCmd = omega.x * 57.29578f;
				rCmd = omega.z * 57.29578f;
				omegaDotDes = Vector3.zero;
				unload = (prot = false);
				pS = 0f;
				gInt = 0f;
				aInt = 0f;
				capIUp = (capIDn = 0f);
				vector11 = vector8;
				tvcPitchInput = Mathf.Clamp(num19, -1f, 1f);
			}
			else
			{
				long tLaw0 = System.Diagnostics.Stopwatch.GetTimestamp();
				PitchLaw(num, vc);
				LateralLaw(num);
				phLaws += System.Diagnostics.Stopwatch.GetTimestamp() - tLaw0;
				if (gsActive && gsMainsContact)
				{
					// rolling on the mains with the nose up (rotation / landing flare-out): the ground yaw-rate command replaces
					// the airborne sideslip law, so a crosswind cannot weathervane the jet off the line
					rCmd = gsRCmdMains;
					rDotFF = 0f;
					// on the mains the roll axis is a bank hold in the air law; a crabbed touchdown's tyre side force (2 m below
					// the CG) tips the jet onto one main and the neutral stick would then hold that bank. Level the wings instead,
					// fading out with roll stick.
					pCmd = Mathf.Lerp(Mathf.Clamp((0f - GsKphi) * phi, -30f, 30f), pCmd, Mathf.Clamp01(Mathf.Abs(sr) / 0.3f));
				}
				qTprev = qT;
				Vector3 vector12 = new Vector3(pCmd, qT, rCmd) * (MathF.PI / 180f) - omega;
				// 2.2.5 pitch lead with lateral stick in a pull: the stabs go to the roll (latReserve) and the nozzles, slewing
				// ~70 deg/s, carry the pitch. The rate loop then reads the pitch-rate error ~0.3 s late, drives the nozzles
				// stop to stop and the pitch rate swings 20-40 deg/s about its command (14 Oct 18:36 log, 666-671 s: full aft
				// from a 525 km/h dive, full roll at 37 deg AoA, q 61 against 48, then 10 against 46, AoA 66.6 at the 65 limit).
				// The measured pitch rate is taken PitchLeadT x nozzle share ahead along the measured pitch acceleration.
				pitchLeadDbg = 0f;
				// pulls only (positive pitch stick, positive AoA): the push + roll corkscrews are left as they were
				float leadT = PitchLeadT * Plugin.RollPitchLead / 0.25f;
				if (leadT > 0f && Plugin.LateralStabReserve > 0.01f && latReserve > 0.001f && sp > 0f && alpha > 0f)
				{
					float gL = Mathf.Clamp01(latReserve / Plugin.LateralStabReserve) * CapTvcShare();
					pitchLeadDbg = leadT * gL;
					// (lead on q itself; taking the trajectory's own acceleration out doubled the bench hitch, 6 -> 12 deg/s)
					vector12.y -= pitchLeadDbg * vector7.y;
				}
				// 2.4.18 yaw slew lead: the rudders reach the other side of their travel no faster than their servo allows (35 deg
				// at 100 deg/s), so the yaw loop reads the yaw-rate error late by the time they need to swing back through neutral,
				// keeps accelerating the yaw past its command and then cannot brake it in time. 30 Oct 22:36 log, 51.6-52.4 s:
				// rudders at +35, yaw rate 42 against a command of 28 -> 7, sideslip -12.4 on the reversal that followed (the
				// 2.4.17 roll limit held the roll to 30-51 deg/s and did not prevent it). The measured yaw rate is taken ahead
				// along the measured yaw acceleration by that time (antisymmetric rudder deflection / servo rate), as the 2.2.5
				// pitch lead does for the nozzles. Rudders near neutral = no lead, so a yaw axis that is keeping up is unchanged.
				yawLeadDbg = 0f;
				if (Plugin.YawSlewLead && YawSlewLeadK > 0f && !isAI && (Mode == FcsMode.AoAG || Mode == FcsMode.Rate))
				{
					float rSum = 0f;
					int rN = 0;
					float rRate = 0f;
					foreach (SurfaceEffector seL in surfaces)
					{
						if (seL.kind == SurfaceKind.Rudder && seL.active && seL.rate > 0f)
						{
							rSum += seL.now;
							rN++;
							rRate = (rRate <= 0f) ? seL.rate : Mathf.Min(rRate, seL.rate);
						}
					}
					if (rN > 0 && rRate > 0f)
					{
						float rMean = (rN >= 2) ? (rSum / rN) : 0f;
						float dA = 0f;
						foreach (SurfaceEffector seL in surfaces)
						{
							if (seL.kind == SurfaceKind.Rudder && seL.active && seL.rate > 0f)
							{
								dA += Mathf.Abs(seL.now - rMean);
							}
						}
						dA /= rN;
						float gYL = 1f - Mathf.Clamp01((Mathf.Abs(alpha) - StopSlipA0) / Mathf.Max(1f, StopSlipA1 - StopSlipA0));
						float tauY = YawSlewLeadK * gYL * dA / rRate;
						// only where the yaw is carried onto or past its command within that time (the error closing by at least
						// half of itself); a yaw rate still building toward a far command is left alone, so roll entries keep their
						// yaw build-up
						float travel = tauY * (vector7.z - rCmdRateF * (MathF.PI / 180f));
						float kY = (Mathf.Abs(vector12.z) > 1E-04f) ? (travel / vector12.z) : ((Mathf.Abs(travel) > 1E-04f) ? 9f : 0f);
						float gO = Mathf.Clamp01((kY - 0.5f) / 0.5f);
						gO = gO * gO * (3f - 2f * gO);
						yawLeadDbg = tauY * gO;
						vector12.z -= yawLeadDbg * vector7.z;
					}
				}
				omegaDotDes = new Vector3(Plugin.KRoll * vector12.x + pDotFF * (MathF.PI / 180f), Plugin.KPitch * vector12.y + 0.5f * qTraj.v * (MathF.PI / 180f), Plugin.KYaw * vector12.z + rDotFF * (MathF.PI / 180f));
				float mDotY = (mLeadInit ? ((vector8.y - mPrevY) / num) : 0f);
				mPrevY = vector8.y;
				mLeadInit = true;
				mDotYF = mDotYF + (mDotY - mDotYF) * (num / (MLeadTau + num));
				vector11 = vector8 + MulI(omegaDotDes - vector7);
				vector11.y -= Plugin.HybridIndiGain * hybridDM;
				vector11.y -= MulI(new Vector3(0f, distLeadAcc, 0f)).y;
				if (Plugin.ReleasedMomentLead > 0f)
				{
					float leadGate = (1f - Mathf.Clamp01((Mathf.Abs(sp) - 0.03f) / 0.15f)) * (1f - Mathf.Clamp01(Mathf.Abs(qTraj.v) / 20f)) * ((Mathf.Abs(qT) < 1f && !prot && !unload) ? 1f : 0f) * stabPitchFrac;
					mLeadGate = Mathf.MoveTowards(mLeadGate, leadGate, num / 0.2f);
					float leadM = Plugin.ReleasedMomentLead * mLeadGate * mDotYF * Mathf.Clamp01(1f - Plugin.HybridIndiGain); // 1.9.0: the hybrid INDI term covers this lag
					float leadErr = vector12.y * 57.29578f;
					mLeadAgree = ((LeadAgreeGate && leadM * leadErr < 0f) ? (1f - Mathf.Clamp01((Mathf.Abs(leadErr) - 1f) / 2f)) : 1f);
					vector11.y += leadM * mLeadAgree;
				}
				Vector3 vector13 = new Vector3(pCmd, qT, rCmd) * (MathF.PI / 180f);
				vector11 += Plugin.CouplingFF * (Vector3.Cross(vector13, MulI(vector13)) - Vector3.Cross(omega, MulI(omega)));
				vector11 += asymFF;
				// 2.0.12: on the wheels the incremental (INDI) roll target is an integrator with nothing to stop it: the gear
				// holds the wings, the measured roll acceleration stays 0, and any roll moment the surfaces make (the mode
				// switch at the rotation speed left one) is kept for the whole roll. Take-off logs: stabs split +7 / -6 deg
				// from 46 m/s to lift-off. While a wheel touches, the surfaces' roll moment is the proportional roll law
				// alone (I x the wanted roll acceleration), with the airframe's own roll moment from the model.
				if (GroundRollP && wheelsTouch)
				{
					float restX = vector8.x;
					foreach (Effector eX in effs)
					{
						if (eX.active)
						{
							restX -= eX.MAt(eX.now).x;
						}
					}
					vector11.x = restX + MulI(new Vector3(omegaDotDes.x, 0f, 0f)).x;
				}
				// 2.0.12: with a rudder gone or an engine out the yaw target can sit far beyond what the jet has (10:29 log,
				// fin shot off: -50..-106 deg/s^2 asked, -12 available). Chasing it, the allocator bought small yaw gains with
				// the stabs and flaperons at any roll / pitch cost (one stab swapping +35 <-> -30 at 0.6 Hz, roll +18 <-> -68
				// with the stick held, pitch rate +-40). Here the yaw target is limited to what the rudders reach with every
				// other surface where it is now, so the rest of the allocation is not traded away for yaw it cannot get.
				decelDbg = 0f;
				yawClampStabDbg = 0f;
				alloc.fightW[1] = (alloc.fightW[2] = (alloc.fightW[3] = 1f));
				if (YawTargetClamp && (yawHealth < 0.999f || engOut || engAsym))
				{
					float restZ = vector8.z;
					float rudLo = 0f;
					float rudHi = 0f;
					float decelLo = 0f;
					float decelHi = 0f;
					foreach (Effector eY in effs)
					{
						if (!eY.active)
						{
							continue;
						}
						float zNow = eY.MAt(eY.now).z;
						restZ -= zNow;
						if (eY is SurfaceEffector sF && (sF.kind == SurfaceKind.Aileron || sF.kind == SurfaceKind.Flaperon))
						{
							float fl = float.MaxValue;
							float fh = float.MinValue;
							for (int iF = 0; iF < eY.N; iF++)
							{
								fl = Mathf.Min(fl, eY.M[iF].z);
								fh = Mathf.Max(fh, eY.M[iF].z);
							}
							decelLo += Mathf.Min(0f, fl - zNow);
							decelHi += Mathf.Max(0f, fh - zNow);
						}
						if (eY is SurfaceEffector sY && sY.kind == SurfaceKind.Rudder)
						{
							float zl = float.MaxValue;
							float zh = float.MinValue;
							for (int iY = 0; iY < eY.N; iY++)
							{
								zl = Mathf.Min(zl, eY.M[iY].z);
								zh = Mathf.Max(zh, eY.M[iY].z);
							}
							rudLo += zl;
							rudHi += zh;
						}
						else if (YawClampStabReach && eY is SurfaceEffector sS && sS.kind == SurfaceKind.Stabilator && stabPitchFrac < YawClampStabFree)
						{
							// 2.20.0: where the stabs are off the pitch (nozzles carry it, stabPitchFrac -> 0: ~60 deg and up) their
							// differential IS the yaw axis without fins - the reach includes their yaw range, scaled by how free
							// they are. 00:25 log (2.19.0, no fins): the clamp held the yaw target at the flaperons' drag-yaw (+8..+20
							// deg/s^2) against a ~100 demand for 4 s per right-going reversal, stabs idle at -7/+2 with 22-27 deg/s^2
							// of yaw in their tables; the left-going ones were quick only because the roll loop's stab differential
							// happened to yaw the right way. On the pitch (2.0.12's one-fin mid-AoA case) the reach is unchanged.
							float zl = float.MaxValue;
							float zh = float.MinValue;
							for (int iS = 0; iS < eY.N; iS++)
							{
								zl = Mathf.Min(zl, eY.M[iS].z);
								zh = Mathf.Max(zh, eY.M[iS].z);
							}
							float wFree = Mathf.Clamp01((YawClampStabFree - stabPitchFrac) / YawClampStabFree);
							rudLo += zNow + wFree * Mathf.Min(0f, zl - zNow);
							rudHi += zNow + wFree * Mathf.Max(0f, zh - zNow);
							yawClampStabDbg = wFree;
						}
						else
						{
							rudLo += zNow;
							rudHi += zNow;
						}
					}
					float zMargin = YawTargetMargin * (rudHi - rudLo);
					float zLo = restZ + rudLo - zMargin;
					float zHi = restZ + rudHi + zMargin;
					// 2.2.7 split flaperons as decelerons: when the yaw asked for is past what the rudders reach (both fins
					// gone, one fin gone, or the live engine's thrust more than the rudders hold), the reach is extended by
					// SplitFlaperonYawUse x the drag yaw the four flaperons have left. The allocator then buys it by opening the
					// flaperons on one wing against each other (inner and outer pair cancelling each other's roll, their drag
					// yawing the jet), with the roll-fight cost relaxed for the inner/outer split in proportion. Only the part
					// of the demand the rudders cannot cover; the VMC throttle limit (which acts on the remaining yaw
					// saturation) comes after it.
					// 2.2.8: lateral stick takes the flaperons back (the split's reach shrinks by SplitFlaperonRollYield x |lateral
					// stick|, held for 1 s after the stick comes back), the roll-fight cost is relaxed for the wing groups only
					// (the stabs still may not roll against the wing), and both follow a rate-limited gate (up 3/s, down 1.5/s)
					// instead of the frame's own saturation. 19 Oct log, both fins gone: the split held all four flaperons at
					// +-25 through rolls, the gate flicked 0.3 <-> 1 frame to frame and the fight cost with it; roll rate
					// swung 5 <-> 40 deg/s against a 35 command, stab difference -46..+3, nozzles chasing the pitch.
					float dTgt = 0f;
					decelYieldF = Mathf.Max(Mathf.Clamp01(Mathf.Abs(sr)), Mathf.MoveTowards(decelYieldF, 0f, DecelYieldFall * num));
					if (Plugin.SplitFlaperonYaw && (vector11.z < zLo || vector11.z > zHi))
					{
						float k = Plugin.SplitFlaperonYawUse * (1f - Plugin.SplitFlaperonRollYield * decelYieldF);
						float span = Mathf.Max(1E-06f, k * ((vector11.z < zLo) ? (0f - decelLo) : decelHi));
						float over = (vector11.z < zLo) ? (zLo - vector11.z) : (vector11.z - zHi);
						dTgt = Mathf.Clamp01(over / span);
					}
					decelF = Mathf.MoveTowards(decelF, dTgt, ((dTgt > decelF) ? DecelRise : DecelFall) * num);
					if (Plugin.SplitFlaperonYaw && decelF > 0f)
					{
						float k = Plugin.SplitFlaperonYawUse * (1f - Plugin.SplitFlaperonRollYield * decelYieldF);
						zLo += k * decelF * decelLo;
						zHi += k * decelF * decelHi;
						alloc.fightW[2] = (alloc.fightW[3] = 1f - DecelFightRelax * decelF);
						// 2.2.19 (SplitFlaperonStabRoll): the stabs may take up the wing's roll too, so the flaperons can be set for drag
						// yaw without having to cancel their own roll inside each wing
						alloc.fightW[1] = 1f - Mathf.Clamp01(DecelStabRelax) * decelF;
					}
					decelDbg = decelF;
					// 2.2.22: what the clamp takes away still counts as yaw saturation (the VMC throttle limit watches it). With
					// both fins gone the clamped target was always "reached", yawSat stayed 0 and the limiter never cut the
					// live engine (bench, one engine out + fins gone: departures at 250-600 km/h with the throttle untouched).
					yawClampExcess = Mathf.Abs(vector11.z - Mathf.Clamp(vector11.z, zLo, zHi));
					vector11.z = Mathf.Clamp(vector11.z, zLo, zHi);
				}
				else
				{
					decelF = 0f;
				}
				// 2.0.6: with the nose wheel on the runway the measured pitch acceleration stays ~0 whatever the surfaces do,
				// so the incremental (INDI) pitch target would integrate to full nose-up deflection before the nose moves and
				// then have to be unwound at servo rate after it lifts (the over-rotation). While the nose wheel touches, the
				// nose-up pitch moment target may only grow at GroundRotationRamp (deg/s^2 per s), starting from what the
				// surfaces make now; the nose comes up when that moment unloads the leg, with little left over.
				if (noseContactLaw)
				{
					float rampM = Plugin.GroundRotationRamp * (MathF.PI / 180f) / Mathf.Max(1E-06f, IaInv[1, 1]) * num;
					if (!rotMomOn)
					{
						rotMomLim = vector8.y;
						rotMomOn = true;
					}
					rotMomLim = Mathf.Min(rotMomLim + rampM, Mathf.Max(rotMomLim, vector11.y));
					vector11.y = Mathf.Min(vector11.y, rotMomLim);
				}
				else
				{
					rotMomOn = false;
				}
				tvc.lam = Mathf.Lerp(2E-05f, Mathf.Max(2E-05f, Plugin.TvcMoveCost), stabPitchFrac);
				// 2.0.3: with little thrust the nozzles make almost no moment per degree, so the allocator swung them across
				// their travel chasing pitch errors they could not fix (idle at the AoA limit: +-10-13 deg sd). Their movement
				// costs more as the thrust they vector falls.
				tvc.lam *= 1f + TvcLowThrustLam * (1f - Mathf.Clamp01(tw / TvcLowThrustTw));
				// (with the nozzles' thrust near idle their moment per degree is small and the grid's coarse steps were the
				// only thing holding them still; the Newton step had them chase the hold: bench, idle release at 450 km/h,
				// q held +1 deg/s for 2 s)
				Allocator.CoordNewton = (Plugin.AllocNozzleNewton && tw >= NewtonMinTw) ? 1 : 0;
				TvcBudgetStep(num, vector11);
				// 2.2.14 (TvcNeutralHold, Dev): while the pitch rate is held (pitch-rate command steady, no pitch error, no roll
				// budget in force), the nozzles are drawn back to neutral and the stabs take the steady pitch moment; the
				// pull toward neutral drops at once when the pitch-rate command moves or a pitch error opens, so the nozzles
				// still take the changes. It returns over TvcNeutralTau.
				tvcNeutralDbg = 0f;
				if (Plugin.TvcNeutralHold > 0f && tvc.anchorK <= 0f && !prot && !isAI && Mode == FcsMode.AoAG)
				{
					float steadyW = (1f - Mathf.Clamp01(Mathf.Abs(qTraj.v) / TvcNeutralQdot)) * (1f - Mathf.Clamp01((Mathf.Abs(omega.y * 57.29578f - qT) - 1f) / 4f)) * stabPitchFrac;
					tvcNeutralW = (steadyW < tvcNeutralW) ? steadyW : Mathf.MoveTowards(tvcNeutralW, steadyW, num / Mathf.Max(0.05f, TvcNeutralTau));
					if (tvcNeutralW > 0.001f)
					{
						tvc.anchor = 0f;
						tvc.band = TvcNeutralBand;
						tvc.anchorK = Plugin.TvcNeutralHold * tvcNeutralW;
						tvcNeutralDbg = tvcNeutralW;
					}
				}
				else
				{
					tvcNeutralW = 0f;
				}
				float num24 = Mathf.Clamp01(Mathf.Abs(pCmd) / 40f) * stabPitchFrac * (1f - Mathf.InverseLerp(12f, 20f, Mathf.Abs(alpha))) * (1f - Mathf.Clamp01(Mathf.Abs(sp) / 0.15f)) * (1f - Mathf.Clamp01(Mathf.Abs(qT) / 8f));
				float num25 = ((WashK >= 0f) ? WashK : Plugin.RollNozzleHold);
				if (num24 < 0.05f)
				{
					tvcTrimRef += (tvc.cmdOut - tvcTrimRef) * (num / (0.3f + num));
				}
				tvc.lam = Mathf.Max(tvc.lam, 0.05f * Mathf.Clamp01(num25) * num24 * Mathf.InverseLerp(125f, 150f, ias));
				if (num25 > 0f && num24 > 0.02f)
				{
					for (int num26 = 0; num26 < effs.Count; num26++)
					{
						cmdSave[num26] = effs[num26].cmd;
					}
					alloc.Solve(effs, vector11);
					float b = ((WashTau >= 0f) ? WashTau : Plugin.RollNozzleWashout);
					tvcSlow += (tvc.cmd - tvcTrimRef - tvcSlow) * (num / (Mathf.Max(0.02f, b) + num));
					float num27 = 0f;
					foreach (SurfaceEffector surface5 in surfaces)
					{
						if (surface5.kind == SurfaceKind.Stabilator && surface5.active)
						{
							num27 = Mathf.Max(num27, Mathf.Abs(surface5.cmd) / Mathf.Max(1f, surface5.LimitFor(surface5.cmd)));
						}
					}
					float num28 = Mathf.Clamp01((WashUtilHi - num27) / 0.2f);
					float washTarget = Mathf.Clamp01(num25) * num24 * num28 * tvcSlow;
					washApplied = ((Plugin.RollNozzleWashoutSlew > 0f) ? Mathf.MoveTowards(washApplied, washTarget, Plugin.RollNozzleWashoutSlew * num) : washTarget);
					float hi = Mathf.Clamp(tvc.cmd - washApplied, tvc.lo, tvc.hi);
					for (int num29 = 0; num29 < effs.Count; num29++)
					{
						effs[num29].cmd = cmdSave[num29];
					}
					float lo = tvc.lo;
					float hi2 = tvc.hi;
					tvc.lo = (tvc.hi = hi);
					alloc.Solve(effs, vector11);
					tvc.lo = lo;
					tvc.hi = hi2;
					tvcPin = hi;
					tvcPinned = true;
				}
				else
				{
					tvcSlow += (0f - tvcSlow) * (num / (0.2f + num));
					washApplied = ((Plugin.RollNozzleWashoutSlew > 0f) ? Mathf.MoveTowards(washApplied, 0f, Plugin.RollNozzleWashoutSlew * num) : 0f);
					tvcPinned = false;
					alloc.Solve(effs, vector11);
				}
				stabBranch = 0;
				bool branchGate = Plugin.StabBranchSwitch && Plugin.StabBranchMaxAoA > 0f;
				if (branchGate)
				{
					float aAbs = Mathf.Abs(alpha);
					branchGateOn = branchGateOn ? (aAbs <= Plugin.StabBranchMaxAoA + 2f) : (aAbs <= Plugin.StabBranchMaxAoA);
					branchGate = branchGateOn;
				}
				foreach (SurfaceEffector sb in surfaces)
				{
					if (sb.kind != SurfaceKind.Stabilator)
					{
						continue;
					}
					sb.branchCool = Mathf.Max(0f, sb.branchCool - num);
					if (Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy)) < 0.1f)
					{
						sb.branchCool = Mathf.Min(sb.branchCool, BranchCoolTimeStop);
					}
					if (!branchGate || !sb.active)
					{
						sb.branchOn = false;
						continue;
					}
					if (!sb.branchOn && sb.branchCool > 0f)
					{
						continue;
					}
					// 2.0.1: "deep past the peak" and the target are scaled to the stab's own peak (10 and 20 deg were set
					// for Aryx's 33.75 deg foil), and the attached-side target is searched over the stab's whole travel, not
					// this tick's rate-limited window: from -30 deg that window never reached the attached side, so the
					// switch never fired.
					// (with lateral input only: without it the 2.0.0 behaviour is kept)
					bool latIn = Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy)) >= 0.1f;
					float peakK = (BranchScaleToPeak && latIn) ? Mathf.Clamp(sb.stallAoA / StabPeakRef, 0.3f, 1.5f) : 1f;
					if (Mathf.Abs(sb.LocalAoAAt(sb.cmd)) < sb.stallAoA + (latIn ? BranchDeepMargin : 10f) * peakK)
					{
						continue;
					}
					float bestD = sb.cmd;
					float bestE = float.MaxValue;
					float gLo = (BranchFullTravel && latIn) ? sb.min : sb.lo;
					float gHi = (BranchFullTravel && latIn) ? sb.max : sb.hi;
					for (int gi = 0; gi < sb.N; gi++)
					{
						float gd = sb.grid[gi];
						if (gd < gLo - 0.0001f || gd > gHi + 0.0001f)
						{
							continue;
						}
						float la = Mathf.Abs(sb.LocalAoAAt(gd));
						if (la >= sb.stallAoA)
						{
							continue;
						}
						float e = Mathf.Abs(la - Plugin.StabBranchTarget * peakK);
						if (e < bestE)
						{
							bestE = e;
							bestD = gd;
						}
					}
					if (bestE == float.MaxValue)
					{
						sb.branchOn = false;
						continue;
					}
					float y0 = 0f;
					for (int ci = 0; ci < effs.Count; ci++)
					{
						cmdSave[ci] = effs[ci].cmd;
						if (effs[ci].active)
						{
							y0 += effs[ci].MAt(effs[ci].cmd).y;
						}
					}
					float slo = sb.lo;
					float shi = sb.hi;
					// the stab is walked there through this tick's reachable window, so the allocator plans the nozzles and
					// the other surfaces around where it actually is on the way across its lift peak
					float pinD = (BranchFullTravel && latIn) ? Mathf.Clamp(bestD, sb.lo, sb.hi) : bestD;
					sb.lo = (sb.hi = (sb.cmd = pinD));
					// 2.0.1: the nozzles are free in the branch solve. With the roll-time nozzle budget on, they could not take the
					// stab's nose-down, the pitch check failed every time and the stab stayed parked deep in stall until the AoA
					// was low (in game: right stab at -30 from 29 to 3 deg AoA with full roll, then a roll-rate jump).
					float akSave = tvc.anchorK;
					if (BranchFreeTvc)
					{
						tvc.anchorK = 0f;
					}
					alloc.Solve(effs, vector11);
					tvc.anchorK = akSave;
					sb.lo = slo;
					sb.hi = shi;
					float y1 = 0f;
					for (int ci2 = 0; ci2 < effs.Count; ci2++)
					{
						if (effs[ci2].active)
						{
							y1 += effs[ci2].MAt(effs[ci2].cmd).y;
						}
					}
					float kP = IaInv[1, 1] * 57.29578f;
					bool pitchOk = Mathf.Abs(y1 - vector11.y) * kP <= Mathf.Abs(y0 - vector11.y) * kP + Plugin.StabBranchPitchTol;
					// 2.0.4: with lateral input, nozzles at the end of their travel no longer block the switch when it does not
					// make the modelled pitch error worse: at idle they sit pinned holding against the parked stab, so the test
					// failed every time and the stab stayed in stall (in game, 156-161 s: right stab -30, nozzles +20, AoA 36 -> 0).
					bool tvcRoom = tvc == null || !tvc.active || Mathf.Abs(tvc.cmd) <= 0.8f * Mathf.Max(1f, tvc.max) || (latIn && BranchFreeTvc && Mathf.Abs(y1 - vector11.y) <= Mathf.Abs(y0 - vector11.y));
					if (sb.branchOn)
					{
						sb.branchFail = (pitchOk ? 0f : (sb.branchFail + num));
						if (sb.branchFail > BranchFailTime)
						{
							for (int ci3 = 0; ci3 < effs.Count; ci3++)
							{
								effs[ci3].cmd = cmdSave[ci3];
							}
							sb.branchOn = false;
							sb.branchCool = ((Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy)) < 0.1f) ? BranchCoolTimeStop : BranchCoolTime);
						}
						else
						{
							stabBranch++;
						}
					}
					else if (pitchOk && tvcRoom)
					{
						sb.branchOn = true;
						sb.branchFail = 0f;
						stabBranch++;
					}
					else
					{
						for (int ci4 = 0; ci4 < effs.Count; ci4++)
						{
							effs[ci4].cmd = cmdSave[ci4];
						}
						sb.branchCool = ((Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy)) < 0.1f) ? BranchCoolTimeStop : BranchCoolTime);
					}
				}
				rollPitchFF = 0f;
				float num30 = Plugin.RollPitchFF * (1f - Mathf.Clamp01((Mathf.Abs(alpha) - (Plugin.RollPitchFFAoA - 15f)) / 15f));
				if (num30 > 0.01f)
				{
					foreach (SurfaceEffector surface6 in surfaces)
					{
						if (surface6.active && surface6.noPitch)
						{
							float c = Mathf.MoveTowards(surface6.now, surface6.cmd, Mathf.Max(1f, surface6.rate) * num);
							rollPitchFF += surface6.PitchRawAt(c) - surface6.PitchRawAt(surface6.now);
						}
					}
					rollPitchFF *= num30;
					float num31 = Plugin.RollPitchFFMax / Mathf.Max(IaInv[1, 1] * 57.29578f, 1E-06f);
					rollPitchFF = Mathf.Clamp(rollPitchFF, 0f - num31, num31);
					if (Mathf.Abs(rollPitchFF) * IaInv[1, 1] * 57.29578f > 0.5f)
					{
						float lam = tvc.lam;
						int num32 = 0;
						if (Plugin.RollPitchFFStabCost > 1.01f)
						{
							tvc.lam = 2E-05f;
							foreach (SurfaceEffector surface7 in surfaces)
							{
								if (surface7.kind == SurfaceKind.Stabilator && surface7.partner != null && num32 < symSave.Length)
								{
									symSave[num32] = surface7.symCost;
									surface7.symCost *= Plugin.RollPitchFFStabCost;
									num32++;
								}
							}
						}
						float lo2 = tvc.lo;
						float hi3 = tvc.hi;
						if (tvcPinned)
						{
							tvc.lo = (tvc.hi = tvcPin);
						}
						int num33 = 0;
						foreach (SurfaceEffector surface8 in surfaces)
						{
							if (surface8.active && surface8.noPitch && num33 < pinLo.Length)
							{
								pinLo[num33] = surface8.lo;
								pinHi[num33] = surface8.hi;
								surface8.lo = (surface8.hi = Mathf.Clamp(surface8.cmd, surface8.lo, surface8.hi));
								num33++;
							}
						}
						alloc.Solve(effs, vector11 - new Vector3(0f, rollPitchFF, 0f));
						num33 = 0;
						foreach (SurfaceEffector surface9 in surfaces)
						{
							if (surface9.active && surface9.noPitch && num33 < pinLo.Length)
							{
								surface9.lo = pinLo[num33];
								surface9.hi = pinHi[num33];
								num33++;
							}
						}
						tvc.lo = lo2;
						tvc.hi = hi3;
						tvc.lam = lam;
						if (Plugin.RollPitchFFStabCost > 1.01f)
						{
							num32 = 0;
							foreach (SurfaceEffector surface10 in surfaces)
							{
								if (surface10.kind == SurfaceKind.Stabilator && surface10.partner != null && num32 < symSave.Length)
								{
									surface10.symCost = symSave[num32];
									num32++;
								}
							}
						}
					}
				}
				RollPitchPass(vector11, num);
				Vector3 zero4 = Vector3.zero;
				dbgYawRud = (dbgYawWing = (dbgYawStab = (dbgYawTvc = 0f)));
				foreach (Effector eff3 in effs)
				{
					if (!eff3.active)
					{
						continue;
					}
					Vector3 vector14 = eff3.MAt(eff3.cmd);
					zero4 += vector14;
					if (DebugYaw)
					{
						float num34 = vector14.z * IaInv[2, 2] * 57.29578f;
						if (eff3.name.Contains("Rudder"))
						{
							dbgYawRud += num34;
						}
						else if (eff3.name.Contains("Flap") || eff3.name.Contains("Aileron"))
						{
							dbgYawWing += num34;
						}
						else if (eff3.name.Contains("Elevator"))
						{
							dbgYawStab += num34;
						}
						else
						{
							dbgYawTvc += num34;
						}
					}
				}
				dbgTargetZ = vector11.z * IaInv[2, 2] * 57.29578f;
				dbgTargetX = vector11.x * IaInv[0, 0] * 57.29578f;
				dbgMfZ = vector8.z * IaInv[2, 2] * 57.29578f;
				dbgTgtY = vector11.y * IaInv[1, 1] * 57.29578f;
				dbgMfY = vector8.y * IaInv[1, 1] * 57.29578f;
				float num35 = Mathf.Max((vector11.z - zero4.z) * Mathf.Sign(vector11.z - vector8.z), YawSatCountClamp ? yawClampExcess : 0f);
				yawClampExcess = 0f;
				yawSat = yawSatF.Step(Mathf.Clamp01((num35 * IaInv[2, 2] * 57.29578f - 10f) / 40f), 0.25f, num);
				// 2.7.0: the allocator's own unmet report per axis, signed with the increment it was asked for (deg/s^2): what the
				// effectors at their commands fall short of the target by, toward the side the demand went. Read by the measured
				// pace window as its saturation gate (with the slew still in flight), no surface list, no filter beyond PaceSatTau.
				Vector3 unmetRaw = new Vector3(Mathf.Max(0f, (vector11.x - zero4.x) * Mathf.Sign(vector11.x - vector8.x)) * IaInv[0, 0] * 57.29578f * Mathf.Sign(vector11.x - vector8.x), Mathf.Max(0f, (vector11.y - zero4.y) * Mathf.Sign(vector11.y - vector8.y)) * IaInv[1, 1] * 57.29578f * Mathf.Sign(vector11.y - vector8.y), Mathf.Max(0f, num35) * IaInv[2, 2] * 57.29578f * Mathf.Sign(vector11.z - vector8.z));
				allocUnmet = allocUnmetF.Step(unmetRaw, PaceSatTau, num);
				// 2.7.5: capacity estimator telemetry, from this tick's commands (restores them); drives nothing
				// 2.14.0: the two-axis probe needs the AoA and whether the rudders are the yaw axis (their fade x the fins still there)
				// F-22E 3.0.1: what the allocation left unmade of the INDI target (deg/s^2), before the estimator's probes re-solve
				allocResMain = alloc.Accel(alloc.resDbg);
				// F-22E 3.0.4: on a worker thread when EstimatorThreaded (the same Step on copies; collected at the top of the next Tick)
				long tKick0 = System.Diagnostics.Stopwatch.GetTimestamp();
				estJob.Kick(Plugin.EstimatorThreaded, effs, alloc, vector7, IaInv, zero4, num, unmetRaw, LatAxisAlpha(alpha), Mathf.Clamp01(rudderAuth) * Mathf.Clamp01(yawHealth / 0.5f));
				phKick += System.Diagnostics.Stopwatch.GetTimestamp() - tKick0;
				float num36 = 10f;
				float num37 = 1f;
				if (tvc.engines.Count > 0 && tvc.engines[0] != null)
				{
					num36 = Mathf.Max(0.1f, Mathf.Abs(Acc.TF_vec(tvc.engines[0]).x));
					float x4 = Acc.TF_vecGain(tvc.engines[0]).x;
					num37 = ((Mathf.Abs(x4) > 0.001f) ? x4 : 1f);
				}
				tvcPitchInput = Mathf.Clamp((0f - tvc.cmdOut) / (num36 * num37), -1f, 1f);
			}
			mTarget = vector11;
			mk[11] = System.Diagnostics.Stopwatch.GetTimestamp();
			float actuatorDamping = Plugin.ActuatorDamping;
			foreach (Effector eff4 in effs)
			{
				if (Mode == FcsMode.Ground || actuatorDamping <= 0.0001f)
				{
					eff4.cmdOut = eff4.cmd;
					eff4.cmdLag = eff4.cmd;
					eff4.outTraj.Reset(eff4.cmd);
				}
				else if (LegacyLag)
				{
					eff4.cmdOut += (eff4.cmd - eff4.cmdOut) * (num / (actuatorDamping + num));
					eff4.outTraj.Reset(eff4.cmdOut);
				}
				else
				{
					float num38 = Mathf.Max(1f, eff4.rate);
					eff4.cmdLag += (eff4.cmd - eff4.cmdLag) * (num / (PreLag + num));
					eff4.outTraj.Step(eff4.cmdLag, num38, num38 / actuatorDamping, num);
					eff4.cmdOut = eff4.outTraj.x;
				}
			}
			if (Mode == FcsMode.Ground)
			{
				float num39 = 10f;
				float num40 = 1f;
				if (tvc.engines.Count > 0 && tvc.engines[0] != null)
				{
					num39 = Mathf.Max(0.1f, Mathf.Abs(Acc.TF_vec(tvc.engines[0]).x));
					float x5 = Acc.TF_vecGain(tvc.engines[0]).x;
					num40 = ((Mathf.Abs(x5) > 0.001f) ? x5 : 1f);
				}
				float num41 = groundBrake * Plugin.GroundBrakeTvc / (num39 * num40);
				tvcPitchInput = Mathf.Clamp(0f - sp - num41, -1f, 1f);
			}
			foreach (SurfaceEffector surface11 in surfaces)
			{
				if (surface11.active)
				{
					surface11.localAoACmd = surface11.LocalAoAAt(surface11.cmd);
				}
			}
			Engaged = true;
			mk[12] = System.Diagnostics.Stopwatch.GetTimestamp();
			if (isAI && Plugin.AIFlightLog)
			{
				AILogStep(num);
			}
			if (!telemetryOpened && Plugin.TelemetryEnabled && !isAI)
			{
				telemetryOpened = true;
				telemetry = new Telemetry(this);
			}
			// 4.1.3: the Telemetry setting takes effect at once (rows stop and resume; the file stays open)
			if (telemetry != null && Plugin.TelemetryEnabled)
			{
				long tRow0 = System.Diagnostics.Stopwatch.GetTimestamp();
				pendingRow = CaptureRow(time, sp, sr, sy, num2);   // 3.0.4: finished and written at the top of the next Tick
				phRow += System.Diagnostics.Stopwatch.GetTimestamp() - tRow0;
			}
			mk[13] = System.Diagnostics.Stopwatch.GetTimestamp();
			mkDone = true;
		}

		// F-22E 3.0.4: finish the pending CSV row (the estimator job it waits for is joined first) and hand it to the writer thread
		private void FlushPendingRow()
		{
			if (pendingRow == null)
			{
				return;
			}
			estJobF?.Join();
			TelemetryRow r = pendingRow;
			pendingRow = null;
			FinishRow(r);
			telemetry?.Enqueue(r);
		}

		private float RateBudget()
		{
			float num = Mathf.Clamp(5f + 0.15f * ias + 40f * tw, 5f, Plugin.MaxPitchRate);
			float b = Mathf.Clamp((ias - 22f) * 1.1f, 0f, Plugin.MaxPitchRate);
			float t = Mathf.Clamp01((Mathf.Abs(alpha) - (Plugin.CoupleAoA - 6f)) / 12f);
			return Mathf.Clamp(Mathf.Lerp(Mathf.Max(num, b), num, t), 6f, Plugin.MaxPitchRate);
		}

		private float AIStickRate()
		{
			float num = Mathf.Max(1f, aiCornerSpeed);
			float num2 = tas * tas * Mathf.Max(0.01f, ac.airDensity) / (num * num * 1.225f);
			float num3 = aiGLimit * 9.81f / Mathf.Max(tas, 0.75f * num) * 57.29578f;
			if (num2 < 1f)
			{
				num3 *= Mathf.Clamp(num2, 0.3f, 1f);
			}
			return num3;
		}

		// 1.8.40: roll-pitch priority. While a roll is commanded (and for RpRelease s after), the flaperons' pitch moment
		// is in the allocator's model instead of being fed forward: the roll's pitch side-effect is then balanced in the
		// same solve that picks the roll deflections, with pitch weighted ~8-15x roll, so the solver takes it off the roll
		// (less flaperon/stab differential) before it pushes the stabs past their lift peak or the nozzles off their trim.
		// The flaperon pair's collective costs RpSymCost x more meanwhile, so they are not used as pitch surfaces.
		internal static float RpEnable = 1f;

		internal static float RpSymCost = 50f;

		internal static float RpRelease = 0.5f;

		internal static float RpAoA0 = 18f;

		internal static float RpAoA1 = 26f;

		private float rpLat;

		public float rpGate;

		internal static float RpPitchW = 10f;

		internal static float RpPitchTol = 4f;

		internal static float RpYawW = 10f;

		internal static float RpErrBand = 0f;

		internal static float RpHpTau = 0.4f;

		internal static float RpHpK = 0f;

		private float rpTvcLp;

		private bool rpLpInit;

		internal static float RpErr0 = 1.5f;

		internal static float RpBandMin = 0.5f;

		public float rpBand = -1f;

		public float rpPitchErr;

		// After the normal allocation: if the nozzles were sent more than RollTvcBudget off their pre-roll trim while a
		// roll is commanded, solve again with the nozzles held inside that band, every surface's true pitch curve
		// (flaperon pitch moment in, stab lift past its peak honest) and pitch weighted RpPitchW x more. The roll is what
		// gives. Accepted only if its modelled pitch error stays within RpPitchTol deg/s^2 of the normal solution's;
		// otherwise the band is widened (x3, x8) and finally the normal solution kept.
		private void RollPitchPass(Vector3 target, float dt)
		{
			rpBand = -1f;
			rpPitchErr = 0f;
			if (tvc == null || !tvc.active)
			{
				return;
			}
			// the band is centred on the pre-roll trim plus the fast (high-passed) part of what the normal solution wants
			// from the nozzles: they keep doing the quick pitch corrections the surfaces cannot follow (reversals, actuator
			// lag) and hand the steady part of the roll's pitch moment to the stabs within RpHpTau.
			float dtR = Mathf.Max(0.001f, dt);
			float tvcMain = tvc.cmd;
			if (!rpLpInit || rpGate <= 0.01f)
			{
				rpTvcLp = tvcMain;
				rpLpInit = true;
			}
			else
			{
				rpTvcLp += (tvcMain - rpTvcLp) * (dtR / (Mathf.Max(0.01f, RpHpTau) + dtR));
			}
			if (rpGate <= 0.01f)
			{
				return;
			}
			float center = tvcAnchor + RpHpK * (tvcMain - rpTvcLp);
			// the band opens with the pitch-rate error: the nozzles stay the fast pitch effector for transients the
			// surfaces cannot follow (roll reversals: rate limits and actuator lag), only the steady roll trim is held off them
			float qErrNow = Mathf.Abs(omega.y * 57.29578f - qT);
			float band0 = Mathf.Max(RpBandMin, Plugin.RollTvcBudget) + (1f - rpGate) * (1f - rpGate) * Mathf.Max(tvc.max, 0f - tvc.min) * RpGateSpan + RpErrBand * Mathf.Max(0f, qErrNow - RpErr0);
			// with afterburner-class thrust the nozzles are the stronger and faster pitch effector: holding them off the
			// trim moves the stabs' reserve for reversal transients into the trim (bench: asym-load AB roll spam overshot
			// the AoA cap by 3-4 deg more). The band widens by RpTwBand between T/W 0.7 and 1.1.
			band0 += RpTwBand * Mathf.Clamp01((tw - TvcBudgetTw0) / Mathf.Max(0.05f, TvcBudgetTwFade));
			// roll stop / reversal (stick against the roll rate): the roll's pitch moment is about to swing the other way
			// faster than the nozzles slew, so they are let go for it
			float pNow = omega.x * 57.29578f;
			float rev = Mathf.Clamp01(-Mathf.Sign(pNow) * sr * 2f) * Mathf.Clamp01((Mathf.Abs(pNow) - 20f) / 40f);
			band0 += RpRevBand * rev;
			rpRevNow = Mathf.Max(rev, rpRevNow - dtR / Mathf.Max(0.05f, RpRevHold));
			// AoA protection / overshoot past the AoA (or asymmetric-load) limit: the nozzles are free again
			band0 += Mathf.Max(prot ? 1f : 0f, pitchNeed) * Mathf.Max(tvc.max, 0f - tvc.min) * 2f;
			float dev = tvc.cmd - center;
			if (Mathf.Abs(dev) <= band0 + 0.05f)
			{
				return;
			}
			int n = effs.Count;
			if (cmdSave.Length < n)
			{
				cmdSave = new float[n * 2];
			}
			for (int i = 0; i < n; i++)
			{
				cmdSave[i] = effs[i].cmd;
			}
			float kP = IaInv[1, 1] * 57.29578f;
			int ns = 0;
			int nl = 0;
			foreach (SurfaceEffector se in surfaces)
			{
				// stabs stay on the attached side of their lift peak in this pass: past it, their pitch moment turns with the
				// roll (the pitch-up this pass exists to avoid) and lags the plan by a stall's worth of travel
				if (RpStallLimit && se.kind == SurfaceKind.Stabilator && se.active && nl < pinLo.Length)
				{
					pinLo[nl] = se.lo;
					pinHi[nl] = se.hi;
					nl++;
					StallWindow(se, se.stallAoA + RpStallMargin);
					// and keep a reserve short of the travel stops for the pitch transients to come (a roll reversal
					// swings the roll's pitch moment the other way faster than the nozzles slew)
					float rlo = se.min + RpStabReserve;
					float rhi = se.max - RpStabReserve;
					if (se.lo < rlo)
					{
						se.lo = Mathf.Min(rlo, se.hi);
					}
					if (se.hi > rhi)
					{
						se.hi = Mathf.Max(rhi, se.lo);
					}
				}
				se.HonestPitchOn();
				if (se.noPitch && se.partner != null && se.kind != SurfaceKind.Rudder && ns < symSave.Length)
				{
					symSave[ns++] = se.symCost;
					se.symCost *= 1f + RpSymCost;
				}
			}
			Vector3 w0 = alloc.axisWeight;
			alloc.axisWeight = new Vector3(w0.x, w0.y * RpPitchW, w0.z * RpYawW);
			float lo0 = tvc.lo;
			float hi0 = tvc.hi;
			// reference: the same solve with the nozzles free - the pitch error the surfaces cannot avoid anyway
			alloc.Solve(effs, target);
			float eA = Mathf.Abs(PitchSum() - target.y) * kP;
			bool ok = false;
			float e = 0f;
			for (int k = 0; k < 3 && !ok; k++)
			{
				float band = band0 * ((k == 0) ? 1f : ((k == 1) ? 3f : 8f));
				for (int i = 0; i < n; i++)
				{
					effs[i].cmd = cmdSave[i];
				}
				float lo = Mathf.Max(lo0, center - band);
				float hi = Mathf.Min(hi0, center + band);
				if (lo > hi)
				{
					lo = (hi = ((center - band > hi0) ? hi0 : lo0));
				}
				tvc.lo = lo;
				tvc.hi = hi;
				alloc.Solve(effs, target);
				e = Mathf.Abs(PitchSum() - target.y) * kP;
				if (e <= eA + RpPitchTol + RpRevTol * rpRevNow)
				{
					ok = true;
					rpBand = band;
				}
			}
			tvc.lo = lo0;
			tvc.hi = hi0;
			alloc.axisWeight = w0;
			ns = 0;
			nl = 0;
			foreach (SurfaceEffector se2 in surfaces)
			{
				if (RpStallLimit && se2.kind == SurfaceKind.Stabilator && se2.active && nl < pinLo.Length)
				{
					se2.lo = pinLo[nl];
					se2.hi = pinHi[nl];
					nl++;
				}
				se2.HonestPitchOff();
				if (se2.noPitch && se2.partner != null && se2.kind != SurfaceKind.Rudder && ns < symSave.Length)
				{
					se2.symCost = symSave[ns++];
				}
			}
			rpPitchErr = e;
			if (!ok)
			{
				for (int i = 0; i < n; i++)
				{
					effs[i].cmd = cmdSave[i];
				}
			}
		}

		internal static bool RpStallLimit = true;

		internal static float RpStallMargin = 0f;

		internal static float RpStabReserve = 0f;

		internal static float RpTwBand = 0f;

		// 1.8.42: on roll stops/reversals the nozzles are no longer let go (RpRevBand 12 -> 0); the pass is instead accepted
		// with up to RpRevTol deg/s^2 more modelled pitch error than free nozzles would leave, so the stabs and roll rate
		// take the reversal's pitch swing and the nozzles stay near trim (2026-09-28 21:18 log: -20 deg and +1.7..3.3 deg
		// over the 12 deg cap on reversals at 600 km/h).
		internal static float RpRevBand = 12f;

		internal static float RpRevTol = 0f;

		internal static float RpRevHold = 0.6f;

		internal static float HonestRollGate = 0f;

		private float rpRevNow;

		internal static float RpGateSpan = 1f;



		internal static float RpMainStallMargin = 3f;


		internal static float StabParkAoA = 30f;

		// 2.3.0 settled stall window: gate rates (deg/s of body roll / yaw), and whether the rudders are windowed too

		// like StallWindow, but a surface outside the attached range is brought in at 'rate' deg/s: the window's near edge
		// starts at the surface's own command and moves toward the range, so the allocator re-solves around a small step each
		// frame instead of a 30 deg jump (bench: the jump rolled the jet 5 deg/s and sent the wing to its stops)
		private static void SettledWindow(SurfaceEffector se, float lim, float rate, float dt)
		{
			float a = float.MaxValue;
			float b = float.MinValue;
			for (int i = 0; i < se.N; i++)
			{
				float g = se.grid[i];
				if (Mathf.Abs(se.LocalAoAAt(g)) <= lim)
				{
					a = Mathf.Min(a, g);
					b = Mathf.Max(b, g);
				}
			}
			if (a > b)
			{
				return;
			}
			float h = 0.5f * se.Step;
			float lo = Mathf.Max(se.lo, a - h);
			float hi = Mathf.Min(se.hi, b + h);
			if (lo > hi)
			{
				return;
			}
			float c = se.cmd;
			float step = Mathf.Max(0.01f, rate * dt);
			if (c < lo)
			{
				lo = Mathf.Min(lo, c + step);
				hi = Mathf.Max(hi, lo);
			}
			else if (c > hi)
			{
				hi = Mathf.Max(hi, c - step);
				lo = Mathf.Min(lo, hi);
			}
			se.lo = Mathf.Max(se.lo, lo);
			se.hi = Mathf.Min(se.hi, hi);
			if (se.lo > se.hi)
			{
				se.lo = (se.hi = Mathf.Clamp(c, se.min, se.max));
			}
		}

		internal static bool SettledWindowRudders = true;

		private bool settledWin;

		public Vector3 allocResMain;

		public bool settledWinDbg;

		// 2.3.0 coordinated stop: AoA fade-in (deg), gate time constant, feed-forward filter
		internal static float CoordStopA0 = 15f;



		internal static float CoordStopErr0 = 3f;





		// 2.3.3 rate-capture effector lag: servo/actuator lag added to the unwind time (s)
		internal static float RateCaptureLagServo = 0.06f;

		public float rateCapLagDbg;

		private static void StallWindow(SurfaceEffector se, float lim)
		{
			float a = float.MaxValue;
			float b = float.MinValue;
			// 2.2.18: the attached window is searched over the surface's whole travel, not only this frame's reach. Searched
			// within the reach, a stab more than one frame's travel past its stall found no attached point and was left
			// alone, so it stayed stalled until the AoA brought it back (19 Oct 20:11 log, 31.5-34.3 s: left stab at -30,
			// local AoA 47 -> 38, for 2.8 s after the release while the jet kept rolling at 3-17 deg/s).
			for (int i = 0; i < se.N; i++)
			{
				float g = se.grid[i];
				if (Mathf.Abs(se.LocalAoAAt(g)) <= lim)
				{
					a = Mathf.Min(a, g);
					b = Mathf.Max(b, g);
				}
			}
			if (a > b)
			{
				return;
			}
			float h = 0.5f * se.Step;
			float lo = Mathf.Max(se.lo, a - h);
			float hi = Mathf.Min(se.hi, b + h);
			if (lo > hi)
			{
				// the attached window is out of this frame's reach: go toward it as far as the surface can
				lo = (hi = ((b + h < se.lo) ? se.lo : se.hi));
			}
			se.lo = lo;
			se.hi = hi;
		}

		// 2.14.0 telemetry: the stabilators' allocator windows and pair costs (index 0 = first stab found, 1 = second), for the no-fin stop diagnosis
		private float StabWin(int which, bool lo)
		{
			int k = 0;
			foreach (SurfaceEffector se in surfaces)
			{
				if (se.kind != SurfaceKind.Stabilator) continue;
				if (k == which) return lo ? se.lo : se.hi;
				k++;
			}
			return 0f;
		}

		private float StabCost(bool anti)
		{
			foreach (SurfaceEffector se in surfaces)
			{
				if (se.kind == SurfaceKind.Stabilator) return anti ? se.antiCost : se.symCost;
			}
			return 0f;
		}

		private float PitchSum()
		{
			float y = 0f;
			foreach (Effector ef in effs)
			{
				if (ef.active)
				{
					y += ef.MAt(ef.cmd).y;
				}
			}
			return y;
		}

		private void RollPitchGateStep(float dt)
		{
			float g = (Plugin.RollPitchPriority && Engaged && Mode != FcsMode.Ground && Mode != FcsMode.Off) ? RpEnable : 0f;
			float latNow = Mathf.Max(Mathf.Clamp01((Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy)) - 0.02f) / 0.2f), Mathf.Clamp01(Mathf.Abs(pCmd) / 30f));
			rpLat = Mathf.Max(latNow, rpLat - dt / Mathf.Max(0.05f, RpRelease));
			g *= rpLat * (1f - Mathf.Clamp01((Mathf.Abs(alpha) + stabStallShift - RpAoA0) / Mathf.Max(1f, RpAoA1 - RpAoA0)));
			rpGate = Mathf.Clamp01(g);
		}

		// 1.8.32: during roll/yaw the nozzles keep within RollTvcBudget of where they were before the lateral input
		// (the pitch trim they held for the pilot's own pitch command); the pitch moment the roll itself makes is
		// taken by the stabilators beyond that. A cost in the allocator rather than a clamp: when the stabs cannot
		// supply it (on a stop, stalled) the nozzles still go past the budget, as far as the pitch error needs.
		// The band opens to full travel between 14 and 20 deg AoA, between T/W 0.7 and 1.1, in AoA protection / run-away
		// and while the pitch-rate command is moving (AoA/G capture braking).
		private void TvcBudgetStep(float dt, Vector3 target)
		{
			tvc.anchorK = 0f;
			float budget = Plugin.RollTvcBudget;
			if (!tvcAnchorInit)
			{
				tvcAnchor = tvc.cmd;
				tvcAnchorInit = true;
			}
			float latNow = Mathf.Max(Mathf.Clamp01((Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy)) - 0.02f) / 0.2f), Mathf.Clamp01(Mathf.Abs(pCmd) / 30f));
			tvcBudgetLat = Mathf.Max(latNow, tvcBudgetLat - dt / Mathf.Max(0.05f, TvcBudgetRelease));
			// Where the stabs can take the pitch (g = 1) the band is RollTvcBudget; it opens toward the full nozzle travel
			// as they lose that ability or the nozzles need the room: AoA from 14 to 20 deg (the stab going trailing-edge
			// down for the roll is then at ~30 deg local AoA, where the F22E_Wing foil's lift curve flattens, and nose-down from it costs roll rate:
			// 13-22 % in 250 km/h roll spam on the bench), AoA protection / run-away, and while the pitch-rate command is
			// moving (AoA/G capture braking keeps full authority).
			float g = 1f;
			g *= 1f - Mathf.Clamp01((Mathf.Abs(alpha) + stabStallShift - TvcBudgetAoA) / Mathf.Max(1f, TvcBudgetAoAFade));
			g *= prot ? 0f : (1f - pitchNeed);
			g *= 1f - Mathf.Clamp01(Mathf.Abs(qTraj.v) / TvcBudgetQdot);
			g *= stabPitchFrac;
			// With afterburner-class thrust the nozzles' roll trim is only a few degrees anyway (1.8.31 bench: -2..-7 mean)
			// and they are the fast pitch effector; holding them there moved that fast work onto the stabs, which rang
			// with 3 frames of control delay (450 km/h AB roll spam: q sd 2.1 -> 3.8 deg/s). Full budget up to T/W 0.7,
			// open by 1.1.
			g *= 1f - Mathf.Clamp01((tw - TvcBudgetTw0) / Mathf.Max(0.05f, TvcBudgetTwFade));
			float active = tvcBudgetLat;
			if (budget <= 0f || Plugin.RollTvcBudgetCost <= 0f)
			{
				active = 0f;
			}
			tvcBudgetGate = active * g;
			// the anchor (pre-roll nozzle trim for the pilot's own pitch command) follows the nozzles only while no lateral input is held
			float follow = 1f - Mathf.Clamp01(active / 0.3f);
			tvcAnchor += (tvc.cmd - tvcAnchor) * follow * (dt / (TvcAnchorTau + dt));
			// 2.0.0: with lateral input held, the anchor no longer freezes at the trim from before the roll. It follows the
			// nozzle position the same allocation gives for this tick's pitch demand with no roll or yaw demand: the trim for
			// the pilot's own (possibly changing) pitch command, so only the roll's pitch moment is held off the nozzles. A
			// frozen anchor made the stabs carry every change of pitch trim during a held roll (slow ease from high to low
			// AoA): with the 25 deg stab peak they sat on it for nose-down, the roll stab parked deep in stall, the nozzles
			// pushed against them, and roll rate spiked when the stabs came free (bench, 400 km/h: 141 vs 186 deg/s mean).
			if (AnchorPitchOnly && active > 0.001f && budget > 0f && Plugin.RollTvcBudgetCost > 0f)
			{
				int n = effs.Count;
				if (cmdSave.Length < n)
				{
					cmdSave = new float[n * 2];
				}
				for (int i = 0; i < n; i++)
				{
					cmdSave[i] = effs[i].cmd;
				}
				// the stabs are kept on the attached side of their peak here: a stab parked past it (and the nose-up the
				// nozzles then hold against it) is not the pilot's trim
				int nl = 0;
				foreach (SurfaceEffector se in surfaces)
				{
					if (se.kind == SurfaceKind.Stabilator && se.active && nl < pinLo.Length)
					{
						pinLo[nl] = se.lo;
						pinHi[nl] = se.hi;
						nl++;
						float g0 = se.cmd;
						StallWindow(se, se.stallAoA);
						se.cmd = Mathf.Clamp(g0, se.lo, se.hi);
					}
				}
				alloc.Solve(effs, new Vector3(0f, target.y, 0f));
				nl = 0;
				foreach (SurfaceEffector se in surfaces)
				{
					if (se.kind == SurfaceKind.Stabilator && se.active && nl < pinLo.Length)
					{
						se.lo = pinLo[nl];
						se.hi = pinHi[nl];
						nl++;
					}
				}
				float trimNow = tvc.cmd;
				for (int i = 0; i < n; i++)
				{
					effs[i].cmd = cmdSave[i];
				}
				tvcAnchor += (trimNow - tvcAnchor) * (1f - follow) * (dt / (TvcAnchorTau + dt));
			}
			if (active <= 0.001f)
			{
				return;
			}
			float span = Mathf.Max(tvc.max, 0f - tvc.min);
			tvc.anchor = tvcAnchor;
			tvc.band = Mathf.Lerp(span * 2f, budget, active * g);
			tvc.anchorK = Plugin.RollTvcBudgetCost;
		}

		internal static float TvcBudgetRelease = 0.5f;

		internal static float TvcBudgetAoA = 14f;

		internal static bool StabPeakShift = true;

		internal static float StabPeakRef = 33.75f;

		public float stabStallShift;

		internal static float TvcBudgetAoAFade = 6f;

		internal static float TvcBudgetQdot = 25f;

		internal static float TvcBudgetTw0 = 0.7f;

		internal static float TvcBudgetTwFade = 0.4f;

		internal static float TvcAnchorTau = 0.25f;

		internal static bool AnchorPitchOnly = true;

		internal static bool AuthRateLimit = true;

		internal static float TvcLowThrustLam = 0f;

		internal static float TvcLowThrustTw = 0.3f;

		internal static float AuthRateStopAngle = 15f;

		internal static float AuthRateLook = 5f;

		internal static float AuthRateMin = 6f;

		internal static float AuthBrakeFloor = 4f;

		public float authRateUp = 999f;

		public float authRateDn = 999f;

		internal static bool AuthCapFixed = true;

		internal static float AuthCapFixedAoA = 36f;


		private bool authFixedOn;

		// 1.9.0: highest AoA at which the nose-down authority available now (stabs full nose-down, flaperons where they
		// are, nozzles at the present thrust - none above the vectoring cut-off speed) still leaves LowThrustCapMargin.
		private void AuthCapFromSweep()
		{
			if (!Plugin.LowThrustAoACap || Ia[1, 1] <= 1f)
			{
				authCap = 999f;
				return;
			}
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
				float mDn = 0f - (authNetDn[ia] + tvDn);
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

		// 2.0.3: nose-down pitch acceleration (deg/s^2) the nozzles give at the present thrust below which they count as
		// not covering the pitch authority (fixed AuthCapFixedAoA limit). 2.0.4: 12 -> 20 (~T/W 0.2; idle is ~9): between T/W 0.13
		// and 0.2 a pull from high IAS overshot the swept cap by 13 deg as it came down with the speed (bench, in game 102-135 s).
		internal static float AuthFixedTvcMin = 20f;

		// 1.9.0: the flaperons' symmetric deflection for the low-thrust cap, followed only while no roll/yaw is commanded:
		// during a roll one flaperon runs into its stop and the pair's mean shifts, which is not a trim the cap should judge by
		private float[] flapSymRef;

		private bool flapSymInit;

		private void FlapSymStep(float dt)
		{
			float[] anti = asymDsym;
			if (anti == null || anti.Length != surfaces.Count)
			{
				return;
			}
			if (flapSymRef == null || flapSymRef.Length != surfaces.Count)
			{
				flapSymRef = new float[surfaces.Count];
				flapSymInit = false;
			}
			bool quiet = Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy)) < 0.05f && Mathf.Abs(omega.x * 57.29578f) < 15f;
			float k = Mathf.Clamp01(dt / 0.3f);
			for (int i = 0; i < surfaces.Count; i++)
			{
				SurfaceEffector se = surfaces[i];
				float sym = se.now - anti[i];
				if (!flapSymInit)
				{
					flapSymRef[i] = sym;
				}
				else if (quiet)
				{
					flapSymRef[i] += (sym - flapSymRef[i]) * k;
				}
			}
			flapSymInit = true;
		}

		// 2.0.10: below the high-AoA regime a stab is not commanded past its lift peak (local AoA within the peak +
		// StabPeakMargin, either sign). Past the peak the lift curve is flat or falling, so further travel buys nothing,
		// and the allocator could not get back: in Tom's 10:00 flight a left roll at ~5 deg AoA drove the right stab to
		// -30 (local 32, peak 25) and it stayed there for 10 s, the rolling-back direction looking worse until it had
		// crossed the peak, with every other surface saturated against it. At 36 deg AoA (stab at its peak by design)
		// the same search swung the stabs +-30 every 1.5 s. The margin opens up from 40 to 50 deg body AoA, so the deep-
		// stall use above that (falling leaf, post-stall roll) is unchanged.
		internal static bool StabPeakClamp = true;


		private void ClampStabToPeak(SurfaceEffector se)
		{
			float x = Mathf.Abs(alpha);
			float lim = se.stallAoA + Plugin.StabPeakMargin + Mathf.Max(0f, x - 40f) * 8f;
			if (lim > 89f || !se.Valid)
			{
				return;
			}
			float l0 = se.LocalAoAAt(0f);
			float slope = (se.LocalAoAAt(10f) - l0) / 10f;
			if (float.IsNaN(slope) || Mathf.Abs(slope) < 0.5f)
			{
				return;
			}
			float d1 = (lim - l0) / slope;
			float d2 = (0f - lim - l0) / slope;
			float dLo = Mathf.Min(d1, d2);
			float dHi = Mathf.Max(d1, d2);
			float lo = Mathf.Max(se.lo, dLo);
			float hi = Mathf.Min(se.hi, dHi);
			if (lo < hi - 1f)
			{
				se.lo = lo;
				se.hi = hi;
			}
		}

		// 2.2.2: the positive no-TVC cap. With DisableTVC the stabs alone hold 45 deg with the thrust up (bench, 160-750 km/h,
		// rolls included); at part throttle, rolling as the speed bled through ~200-250 km/h, 45 departed where 36 held. So
		// NoTvcAoACapPositive (36) up to T/W NoTvcThrustCapTW0, NoTvcThrustAoACapPositive (45) from NoTvcThrustCapTW1.
		internal static float NoTvcThrustCapTW0 = 0.8f;

		internal static float NoTvcThrustCapTW1 = 1f;

		private float NoTvcCapPosNow()
		{
			if (!Plugin.DisableTVC)
			{
				return Plugin.NoTvcAoACapPos;
			}
			float k = Mathf.Clamp01((tw - NoTvcThrustCapTW0) / Mathf.Max(0.05f, NoTvcThrustCapTW1 - NoTvcThrustCapTW0));
			return Mathf.Lerp(Plugin.NoTvcAoACapPos, Mathf.Max(Plugin.NoTvcAoACapPos, Plugin.NoTvcThrCapPos), k);
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
			noTvcOn = Plugin.DisableTVC || (noTvcOn ? (tw < Plugin.LowThrustTW + 0.03f) : (tw < Plugin.LowThrustTW));
			bool negCapOn = noTvcOn && !Plugin.AoAFull65;
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
				tvShort |= Plugin.DisableTVC;
				authFixedOn = tvShort || (authFixedOn ? (authCap < Plugin.AoAPos - 0.5f) : (authCap < Plugin.AoAPos - 3f));
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

		private static float SmoothMin(float a, float b, float k)
		{
			float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / Mathf.Max(0.01f, k));
			return Mathf.Lerp(b, a, h) - k * h * (1f - h);
		}

		private static float Smooth01(float x)
		{
			x = Mathf.Clamp01(x);
			return x * x * (3f - 2f * x);
		}

		// 1.9.0: AoA/G command pre-filter. The reference moves toward the stick's command at a speed that grows with the
		// step (small steps gently, large ones as before), its rate building up over an onset time that also shrinks with
		// the step. Released stick: the reference sits on the jet's present AoA/G, so the next step is measured from there.
		private static float ShapeStep(ref float x, ref float v, ref float mag, float target, float span, float kSmall, float kLarge, float onsetSmall, float dt)
		{
			float d = target - x;
			// the step's size is held (decaying at one span per second) so a large step keeps its speed all the way in
			// instead of slowing down on its last few degrees / tenths of a g as if it were a small one
			mag = Mathf.Max(Mathf.Abs(d), mag - span * dt);
			float r = Mathf.Clamp01(mag / span);
			float s = r * r;
			float k = Mathf.Lerp(kSmall, kLarge, s);
			float ton = Mathf.Lerp(onsetSmall, 0.03f, s);
			float vDes = k * d;
			if (vDes * v < 0f)
			{
				v = 0f;
			}
			float aLim = Mathf.Max(Mathf.Abs(vDes) / Mathf.Max(0.01f, ton), k * k * Mathf.Abs(d));
			v = Mathf.MoveTowards(v, vDes, aLim * dt);
			float xn = x + v * dt;
			if ((target - xn) * d <= 0f)
			{
				xn = target;
				v = 0f;
			}
			x = xn;
			return x;
		}

		private void ShapeCommands(float dt, bool held)
		{
			if (!Plugin.CmdShaping)
			{
				alphaCmd = alphaCmdRaw;
				nCmd = nCmdRaw;
				shapeInit = false;
				return;
			}
			if (!held || !shapeInit)
			{
				aRefX = alpha;
				nRefX = nz;
				aRefV = 0f;
				nRefV = 0f;
				aRefMag = 0f;
				nRefMag = 0f;
				shapeInit = true;
			}
			if (held)
			{
				ShapeStep(ref aRefX, ref aRefV, ref aRefMag, alphaCmdRaw, Plugin.ShapeAoASpan, Plugin.ShapeAoAGainSmall, Plugin.ShapeAoAGainLarge, Plugin.ShapeOnsetSmall, dt);
				ShapeStep(ref nRefX, ref nRefV, ref nRefMag, nCmdRaw, Plugin.ShapeGSpan, Plugin.ShapeGGainSmall, Plugin.ShapeGGainLarge, Plugin.ShapeOnsetSmall, dt);
				alphaCmd = aRefX;
				nCmd = nRefX;
			}
			else
			{
				alphaCmd = alphaCmdRaw;
				nCmd = nCmdRaw;
			}
		}

		private float aRefX;

		private float nRefX = 1f;

		private float aRefMag;

		private float nRefMag;

		private void PitchLaw(float dt, float Vc)
		{
			qB = RateBudget();
			AuthCapStep(dt);
			latReserve = Mathf.Clamp01(Plugin.LateralStabReserve) * Mathf.Clamp01(Mathf.Max(Mathf.Abs(sr), Mathf.Abs(sy))) * Mathf.Clamp01((Mathf.Abs(sp) - 0.03f) / 0.15f);
			float aUpP = aUp;
			float aDnP = aDn;
			if (latReserve > 0.001f)
			{
				float tvUp = 0f;
				float tvDn = 0f;
				if (tvc.active && tvc.M != null && tvc.N > 1)
				{
					float tvMax = float.MinValue;
					float tvMin = float.MaxValue;
					for (int tvI = 0; tvI < tvc.N; tvI++)
					{
						tvMax = Mathf.Max(tvMax, tvc.M[tvI].y);
						tvMin = Mathf.Min(tvMin, tvc.M[tvI].y);
					}
					tvUp = Mathf.Max(0f, (tvMax - tvc.Mnow.y) * IaInv[1, 1] * 57.29578f);
					tvDn = Mathf.Max(0f, (tvc.Mnow.y - tvMin) * IaInv[1, 1] * 57.29578f);
				}
				aUpP = Mathf.Max(20f, aUp - latReserve * Mathf.Max(0f, aUp - tvUp));
				aDnP = Mathf.Max(20f, aDn - latReserve * Mathf.Max(0f, aDn - tvDn));
			}
			aUpPitch = aUpP;
			aDnPitch = aDnP;
			bool flag = Mode != FcsMode.MPO;
			float num = (flag ? Plugin.AoAPos : 999f);
			float num2 = (flag ? Plugin.AoANeg : (-999f));
			float num3 = (flag ? Mathf.Min(Mathf.Min(num, asymCapPosEff), authCapEff) : num);
			float num4 = (flag ? Mathf.Max(Mathf.Max(num2, asymCapNegEff), authCapNegEff) : num2);
			float num5 = ((Mode == FcsMode.MPO) ? Plugin.MpoGPos : Plugin.GPos);
			float num6 = ((Mode == FcsMode.MPO) ? Plugin.MpoGNeg : Plugin.GNeg);
			float num7 = 9.81f / Vc * 57.29578f;
			float num8 = (dbgWVe = omega.y * 57.29578f - alphaDot);
			alphaDotHoldG = Mathf.Clamp(2f * Mathf.Abs(nz) * Mathf.Max(0f, 0f - vDot) / (Vc * Mathf.Max(nAlpha, 0.02f)), 0f, 15f);
			unload = false;
			prot = false;
			float num9 = 1f;
			float num10 = 0f;
			alphaCmd = 0f;
			nCmd = 1f;
			betaG = 0f;
			if (Mode == FcsMode.AoAG)
			{
				// 1.9.0: hand-over AoA (lower when the wings can barely lift the weight), the stick breakpoint that keeps the
				// AoA scale below it, and how far into the rate-command travel the stick is
				float aTgtRb = Plugin.RateBlendAoA;
				if (nLiftMax < Plugin.RateBlendLiftG)
				{
					aTgtRb = Mathf.Lerp(Mathf.Min(Plugin.RateBlendAoAMin, Plugin.RateBlendAoA), Plugin.RateBlendAoA, Mathf.Clamp01((nLiftMax - 1f) / (Plugin.RateBlendLiftG - 1f)));
				}
				// 1.9.7: the negative side's threshold (magnitude) from the wings' negative-lift capacity
				float aTgtRbN = Plugin.RateBlendAoANeg;
				if (nLiftMin < Plugin.RateBlendLiftG)
				{
					aTgtRbN = Mathf.Lerp(Mathf.Min(Plugin.RateBlendAoAMinNeg, Plugin.RateBlendAoANeg), Plugin.RateBlendAoANeg, Mathf.Clamp01((nLiftMin - 1f) / (Plugin.RateBlendLiftG - 1f)));
				}
				rateBlendAoA = (rbInit ? (rateBlendAoA + (aTgtRb - rateBlendAoA) * Mathf.Clamp01(dt / 0.5f)) : aTgtRb);
				rateBlendAoANeg = (rbInit ? (rateBlendAoANeg + (aTgtRbN - rateBlendAoANeg) * Mathf.Clamp01(dt / 0.5f)) : aTgtRbN);
				rbInit = true;
				// 1.9.1 stick map (fixed AoA per stick: RateBlendAoA at RateBlendStick, 72 deg per full stick by default).
				// The threshold's stick point moves with the threshold (24 deg -> 0.333). Up to it: AoA command on that line.
				// Past it: AoA command on from the threshold to the envelope limit at full stick (full stick = the limit, so
				// the jet goes through the threshold at the rate budget); once the jet crosses the threshold with the stick
				// there, pitch-rate command. With the envelope limit (asymmetric / low-thrust cap) at or below the
				// threshold the whole travel maps onto the limit as before; blended over the 4 deg above it.
				// 1.9.7: the same on the negative side, mirrored. Everything below runs in side space (s = +1 pull, -1 push):
				// stick s*sp, AoA s*alpha, limit s*limit, threshold as a magnitude; the outputs are mapped back with s.
				bool blendP = Plugin.HighAoARateBlend;
				bool blendN = Plugin.HighAoARateBlendNeg;
				float aTP = rateBlendAoA;
				float aTN = rateBlendAoANeg;
				float limP = num3;
				float limN = 0f - num4;
				float gAP = Plugin.RateBlendAoA / Plugin.RateBlendStick;
				StickCurveSetup();
				float gAN = Plugin.RateBlendAoANeg / Plugin.RateBlendStick;
				float zwP = (blendP ? Mathf.Clamp01((limP - aTP) / 4f) : 0f);
				float zwN = (blendN ? Mathf.Clamp01((limN - aTN) / 4f) : 0f);
				float aBP = (blendP ? Mathf.Min(aTP, limP) : limP); // 1.9.5: with the blend off full stick is the AoA limit again (1.9.1-1.9.4 stopped at the threshold)
				float aBN = (blendN ? Mathf.Min(aTN, limN) : limN);
				float sBP = Mathf.Lerp(1f, Mathf.Clamp(StickCurveInv(aTP), 0.05f, 0.95f), zwP);
				float sBN = Mathf.Lerp(1f, Mathf.Clamp(aTN / gAN, 0.05f, 0.95f), zwN);
				// floating hold point: the jet came back down to the threshold out of rate command with the stick past the
				// threshold's point - that stick position is the threshold now; easing below it holds the (live) threshold
				// with a soft knee until the stick meets the threshold's own point on the line, where it is caught
				if (floatOn)
				{
					float fsp = (float)floatSide * sp;
					bool fzone = ((floatSide > 0) ? zwP : zwN) > 0.001f;
					if (!fzone || fsp <= ((floatSide > 0) ? sBP : sBN) || Mathf.Abs(sp) < 0.03f || fsp > 0.98f)
					{
						floatOn = false;
					}
				}
				int sm = ((sp >= 0f) ? 1 : (-1));
				float spS = (float)sm * sp;
				float aT = ((sm > 0) ? aTP : aTN);
				float zw = ((sm > 0) ? zwP : zwN);
				bool zone = zw > 0.001f;
				float aB = ((sm > 0) ? aBP : aBN);
				float sB = ((sm > 0) ? sBP : sBN);
				float limS = ((sm > 0) ? limP : limN);
				float sH = ((floatOn && floatSide == sm) ? Mathf.Max(floatS, sB) : sB);
				float aCmdS;
				if (spS <= sH || sH > 0.999f)
				{
					// 2.0.12: the pull side follows the stick curve (StickFor24degAoA / StickFor36degAoA), scaled so the
					// breakpoint still lands on aB; on the default points it is the 1.9.1 straight line
					aCmdS = ((sm > 0) ? (StickCurve(Mathf.Min(spS, 1f)) * aB / Mathf.Max(StickCurve(Mathf.Max(sB, 0.01f)), 0.01f)) : (aB * Mathf.Min(spS, 1f) / Mathf.Max(sB, 0.01f)));
					if (floatOn && floatSide == sm)
					{
						aCmdS = SmoothMin(aCmdS, aT, FloatKnee);
					}
					aCmdS = Mathf.Min(aCmdS, limS);
				}
				else
				{
					aCmdS = aB + (spS - sH) / (1f - sH) * Mathf.Max(0f, limS - aB);
				}
				alphaCmdRaw = (float)sm * aCmdS;
				nCmdRaw = ((sp >= 0f) ? (1f + sp * (num5 - 1f)) : (1f + sp * (1f - num6)));
				zoneX = ((zone && spS > sH) ? Mathf.Clamp01((spS - sH) / Mathf.Max(0.02f, 1f - sH)) : 0f);
				// 2.1.0 G + pitch-rate law (PitchCommandLaw = GAndPitchRate): no AoA command at all. The AoA branch below is
				// replaced by a pitch-rate command; the G branch and the betaG blend are unchanged. The AoA envelope (limits,
				// low-thrust / asymmetric-load caps, AoA protection) still bounds the rate further down as a limit only.
				bool gRate = Plugin.GRateLaw;
				if (gRate)
				{
					alphaCmdRaw = alpha;
					zoneX = 0f;
					hiRate = false;
					floatOn = false;
				}
				num9 = Mathf.Clamp01((Mathf.Abs(sp) - 0.01f) / 0.06f);
				ShapeCommands(dt, num9 > 0.5f);
				float a = 1f + qB * (MathF.PI / 180f) * Vc / 9.81f;
				gAvail = Mathf.Min(a, nMaxAero);
				betaG = Mathf.Clamp01((gAvail - num5) / 3f + 0.5f);
				if (gRate)
				{
				}
				else if (!hiRate)
				{
					if (zone && zoneX > 0f && (float)sm * alpha >= aT && betaG < 0.5f && num9 > 0.5f)
					{
						hiRate = true;
						hiSide = sm;
						hiAnchor = true;
						hiAS = spS;
						hiAQ = Mathf.Clamp((float)sm * qTraj.x, 0f, qB);
						floatOn = false;
					}
				}
				else
				{
					// 1.9.5: only a pilot ease latches it - not a full pull (full stick always means all the way), and not the
					// jet being pushed down through the threshold by an envelope cap coming down (throttle to idle)
					float hs = hiSide;
					float aTh = ((hiSide > 0) ? aTP : aTN);
					float sBh = ((hiSide > 0) ? sBP : sBN);
					bool zoneh = ((hiSide > 0) ? zwP : zwN) > 0.001f;
					bool capPush = ((hiSide > 0) ? (num3 < num - 0.05f && alphaU > num3 - 1f) : (num4 > num2 + 0.05f && alphaU < num4 + 1f));
					if (hs * alpha < aTh && hs * sp > sBh && hs * sp < 0.95f && !capPush && !floatOn && zoneh)
					{
						floatOn = true;
						floatS = hs * sp;
						floatSide = hiSide;
					}
					if (!zoneh || hs * alpha < aTh - Plugin.RateBlendBand || betaG > 0.6f)
					{
						hiRate = false;
					}
				}
				// 2.1.8 AoAThenRateHold: AoA command until the stick is steady and the AoA has settled on the command, then hold
				// the pitch rate the jet has at that moment (the AoA is left to drift with IAS); a stick movement past
				// CapStickBand returns to AoA command (shaping restarts from the present AoA), a release ends it too.
				bool hyb = !gRate && Plugin.AoAThenRateLaw;
				float stickRate = Mathf.Abs(sp - capSpPrev) / Mathf.Max(dt, 1E-4f);
				capSpPrev = sp;
				capSteadyT = (stickRate < CapStickRate) ? (capSteadyT + dt) : 0f;
				if (!hyb || hiRate || num9 < 0.5f || betaG >= 0.5f)
				{
					capOn = false;
				}
				else if (capOn)
				{
					if (Mathf.Abs(sp - capSp) > CapStickBand)
					{
						capOn = false;
						shapeInit = false;
					}
				}
				else if (capSteadyT >= CapSteadyTime && Mathf.Abs(alphaCmd - alpha) < CapAoABand && Mathf.Abs(alphaDot) < CapAlphaDot && Mathf.Abs(sp) > 0.03f)
				{
					capOn = true;
					capSp = sp;
					capQ = omega.y * 57.29578f;
				}
				bool flag2 = !gRate && !hiRate && !capOn && sp * alpha >= 0f && Mathf.Abs(alpha) > 3f && Mathf.Abs(alphaCmd) < Mathf.Abs(alpha) - 1f;
				bool flag3 = sp * (nz - 1f) >= 0f && Mathf.Abs(nCmd - 1f) < Mathf.Abs(nz - 1f) - 0.3f;
				unload = ((betaG < 0.5f) ? flag2 : flag3);
				if (nz > num5 || nz < num6 || alpha > num || alpha < num2 || num9 < 0.5f)
				{
					unload = false;
				}
				num10 = ((betaG < 0.5f) ? Mathf.Sign(alpha) : Mathf.Sign(nz - 1f));
				float num11 = alphaCmd - alpha;
				float num12 = Mathf.Sign(num11) * Mathf.Min(Plugin.KpAoA * Mathf.Abs(num11), Mathf.Sqrt(240f * Mathf.Abs(num11)));
				if (!gRate && !capOn && betaG < 0.5f && num9 > 0.5f && !flag2 && !hiRate && Mathf.Abs(num11) < 4f && Mathf.Abs(qT) < 0.95f * qB)
				{
					aInt = Mathf.Clamp(aInt + Plugin.KiAoA * num11 * dt, -10f, 10f);
				}
				else
				{
					aInt = Mathf.MoveTowards(aInt, 0f, 20f * dt);
				}
				float a2 = num8 + num12 + aInt;
				if (flag2)
				{
					float num13 = num8 + (NPath(alphaCmd) - NPath(alpha)) * num7;
					float num14 = Mathf.Max(0f, num10 * alphaDot);
					a2 = num13 + (0.15f + 0.45f * (1f - Mathf.Clamp01(Mathf.Abs(num11) / 10f))) * num11 - num10 * Plugin.UnloadRiseGain * num14;
				}
				rateW = 0f;
				if (hiRate)
				{
					// past the threshold: pitch-rate command. The map runs through (0, 0), the entry point (stick, rate at
					// the crossing) and (full stick, budget); a release drops the entry point (zero-rate hold, and the next
					// pull maps 0..full stick straight onto 0..budget).
					// a release or a full pull drops the entry point: from then on 0..full stick = 0..budget
					// 1.9.7: in side space (hiSide), so a push past the negative threshold is the same nose-down rate command
					float hs = hiSide;
					float spH = hs * sp;
					if (Mathf.Abs(sp) < 0.03f || spH > 0.98f)
					{
						hiAnchor = false;
					}
					float qMap;
					if (spH <= 0f)
					{
						qMap = spH * qB;
					}
					else if (hiAnchor && hiAS > 0.05f)
					{
						qMap = ((spH <= hiAS || hiAS > 0.999f) ? (hiAQ * spH / hiAS) : Mathf.Lerp(hiAQ, Mathf.Max(hiAQ, qB), (spH - hiAS) / (1f - hiAS)));
					}
					else
					{
						qMap = spH * qB;
					}
					qMap = Mathf.Min(qMap, qB);
					float aTh = ((hiSide > 0) ? aTP : aTN);
					rateW = Smooth01((hs * alpha - (aTh - Plugin.RateBlendBand)) / Plugin.RateBlendBand);
					a2 = Mathf.Lerp(a2, hs * qMap, rateW);
				}
				if (capOn)
				{
					a2 = capQ;
				}
				if (gRate)
				{
					// stick -> body pitch rate, 0..full stick = 0..the pitch-rate budget. Pull side keeps the pilot's stick
					// shape (section 5 curve, normalised to full stick), push side linear as on the AoA law. Released stick =
					// zero-rate hold (num9), as before.
					float spAbs = Mathf.Min(Mathf.Abs(sp), 1f);
					float frac = (sp >= 0f) ? (StickCurve(spAbs) / Mathf.Max(1f, StickCurve(1f))) : spAbs;
					rateCmdG = Mathf.Sign(sp) * Mathf.Clamp01(frac) * qB;
					// 2.1.7 stick lead (StickLeadGain, default 0 = off): a washed-out term on stick movement, so a quick small
					// input moves the nose a little at once (as an AoA step would) and then settles into the plain rate
					// response. Extra nose movement ~ gain x stick change x budget x tau; slow, smooth stick motion barely
					// triggers it; a held stick gets none.
					float sfr = Mathf.Sign(sp) * Mathf.Clamp01(frac);
					if (!leadInit)
					{
						stickLagF = sfr;
						leadInit = true;
					}
					float tauL = Mathf.Max(0.02f, Plugin.StickLeadTau);
					stickLagF += (sfr - stickLagF) * Mathf.Clamp01(dt / tauL);
					stickLeadDbg = Plugin.StickLeadGain * (sfr - stickLagF) * qB;
					a2 = rateCmdG + stickLeadDbg;
				}
				float num15 = Mathf.Max(nAlpha, 0.1f);
				float b;
				if (flag3)
				{
					float num16 = Mathf.Max(0f, num10 * alphaDot);
					b = (nCmd - eLy) * num7 + 0.3f * Plugin.KpG * (nCmd - nz) / num15 - num10 * Plugin.UnloadRiseGain * num16;
				}
				else
				{
					b = (nCmd - eLy) * num7 + Plugin.KpG * (nCmd - nz) / num15 + gInt + Mathf.Sign(nCmd - 1f) * alphaDotHoldG;
				}
				float num17 = Mathf.Lerp(a2, b, betaG);
				if (betaG > 0.5f && num9 > 0.5f && !unload && Mathf.Abs(qT) < 0.95f * qB && Mathf.Abs(nCmd - nz) < 1.5f)
				{
					gInt = Mathf.Clamp(gInt + Plugin.KiG * (nCmd - nz) / Mathf.Max(nAlpha, 0.1f) * dt, -10f, 10f);
				}
				else
				{
					gInt = Mathf.MoveTowards(gInt, 0f, 10f * dt);
				}
				qRaw = num17 * num9;
			}
			else
			{
				qRaw = sp * (isAI ? Mathf.Min(qB, AIStickRate()) : qB);
				hiRate = false;
				floatOn = false;
				shapeInit = false;
				leadInit = false;
				stickLeadDbg = 0f;
				rateW = 0f;
				zoneX = 0f;
				if (rotating && mainsOnGround && !Plugin.GroundRateLaw)
				{
					qRaw = sp * Mathf.Min(qB, Plugin.RotationRate);
				}
				gInt = 0f;
				aInt = 0f;
				capIUp = (capIDn = 0f);
			}
			float num18 = qB;
			float num19 = 0f - qB;
			// 2.0.3: the pitch-rate budget is IAS/T-W based and knows nothing about what can stop the rate. With little
			// nozzle thrust and the stabs near the end of their authority (idle, high AoA) it allowed rates the surfaces
			// could not stop or regulate. The AoA rate (body pitch rate less the flight-path rate) is now also held to what
			// the braking authority available a little further on can stop within AuthRateStopAngle: nose-down authority
			// (stabs full nose-down, flaperons, nozzles at the present thrust) AuthRateLook deg above the present AoA for
			// a nose-up rate, nose-up authority below it for a nose-down rate. Measured state, no schedule. Only while the
			// low-thrust AoA cap is in force (the nozzles cannot cover the nose-down need); otherwise the old budget.
			authRateUp = authRateDn = 999f;
			if (AuthRateLimit && flag && asymValid && tvc != null && tvc.M != null && tvc.N > 1 && (authFixedOn || authCapEff < Plugin.AoAPos - 0.5f))
			{
				float tvLo = float.MaxValue;
				float tvHi = float.MinValue;
				for (int tvJ = 0; tvJ < tvc.N; tvJ++)
				{
					tvLo = Mathf.Min(tvLo, tvc.M[tvJ].y);
					tvHi = Mathf.Max(tvHi, tvc.M[tvJ].y);
				}
				float tvLoA = (tvc.active ? (tvLo * IaInv[1, 1] * 57.29578f) : 0f);
				float tvHiA = (tvc.active ? (tvHi * IaInv[1, 1] * 57.29578f) : 0f);
				float brDnA = 0f - (AsymCap.Interp(asymAlphas, asymNetDn, asymAlphas.Length, alpha + AuthRateLook) + tvLoA);
				float brUpA = AsymCap.Interp(asymAlphas, asymNetUp, asymAlphas.Length, alpha - AuthRateLook) + tvHiA;
				authRateUp = Mathf.Max(AuthRateMin, Mathf.Sqrt(2f * Mathf.Max(0f, brDnA) * AuthRateStopAngle));
				authRateDn = Mathf.Max(AuthRateMin, Mathf.Sqrt(2f * Mathf.Max(0f, brUpA) * AuthRateStopAngle));
				if (authRateUp < num18 - num8)
				{
					num18 = Mathf.Max(num8 + authRateUp, Mathf.Min(num18, AuthRateMin));
				}
				if (authRateDn < num8 - num19)
				{
					num19 = Mathf.Min(num8 - authRateDn, Mathf.Max(num19, 0f - AuthRateMin));
				}
			}
			capAtLimU = capAtLimD = false;
			capPushU = (capPushD = 0f);
			if (!flag)
			{
				capIUp = (capIDn = 0f);
			}
			if (flag)
			{
				float num20 = num3;
				float num21 = num4;
				if (Mode == FcsMode.AoAG && !Plugin.GRateLaw && !capOn && !unload && num9 > 0.5f && betaG < 0.5f && !hiRate)
				{
					if (alphaCmdRaw > 5f)
					{
						num20 = Mathf.Min(num3, alphaCmdRaw + 0.2f);
					}
					if (alphaCmdRaw < -5f)
					{
						num21 = Mathf.Max(num4, alphaCmdRaw - 0.2f);
					}
				}
				float eCapU = (num20 - alpha) * 0.9f - Mathf.Max(0f, alphaDot) * 0.15f;
				float eCapD = (alpha - num21) * 0.9f - Mathf.Max(0f, 0f - alphaDot) * 0.15f;
				float num22 = Mathf.Max(0f, eCapU);
				float num23 = Mathf.Max(0f, eCapD);
				// 2.0.14: at the envelope limit itself (not the 0.2 deg shelf above an ordinary AoA command) the bound goes
				// on through the limit with the slope it has at zero (3 per unit = 2.7 deg/s per deg), and the integrator
				// trims what the flight-path-rate estimate misses. Only while the pilot asks for more than the limit.
				bool capAtU = num20 >= num3 - 0.01f && Mode == FcsMode.AoAG && num9 > 0.5f;
				bool capAtD = num21 <= num4 + 0.01f && Mode == FcsMode.AoAG && num9 > 0.5f;
				capAtLimU = capAtU && alpha > num20 - 2f;
				capAtLimD = capAtD && alpha < num21 + 2f;
				float capNegU = (capAtU && eCapU < 0f) ? (3f * eCapU) : 0f;
				float capNegD = (capAtD && eCapD < 0f) ? (3f * eCapD) : 0f;
				aBrakeTgt = aDn;
				if (CaptureMode == 1 && Plugin.AoACaptureThrustBrake && asymValid && tvc.M != null && tvc.N > 1)
				{
					float tvLo = float.MaxValue;
					float tvHi = float.MinValue;
					for (int tvJ = 0; tvJ < tvc.N; tvJ++)
					{
						tvLo = Mathf.Min(tvLo, tvc.M[tvJ].y);
						tvHi = Mathf.Max(tvHi, tvc.M[tvJ].y);
					}
					float tvLoA = (tvc.active ? (tvLo * IaInv[1, 1] * 57.29578f) : 0f);
					float tvHiA = (tvc.active ? (tvHi * IaInv[1, 1] * 57.29578f) : 0f);
					float brDn = 0f - (AsymCap.Interp(asymAlphas, asymNetDn, asymAlphas.Length, num20) + tvLoA);
					float brUp = AsymCap.Interp(asymAlphas, asymNetUp, asymAlphas.Length, num21) + tvHiA;
					// 2.0.3: floor 20 -> AuthBrakeFloor: with little authority the capture planned with braking it did not have
					aBrakeTgt = Mathf.Max(AuthBrakeFloor, Mathf.Min(aDn, brDn));
					float aBrakeUp = Mathf.Max(AuthBrakeFloor, Mathf.Min(aUp, brUp));
					aBrakeTgt += Mathf.Clamp01(Plugin.AoACaptureBrakeBlend) * Mathf.Max(0f, aDn - aBrakeTgt);
					aBrakeUp += Mathf.Clamp01(Plugin.AoACaptureBrakeBlend) * Mathf.Max(0f, aUp - aBrakeUp);
					// 2.1.10 rolling capture: in a roll the capture planned with braking the jet did not have - the stabs are
					// partly spent on the roll (aDnPitch) and the roll's inertial pitch-up, (Iyaw - Iroll) / Ipitch x p r, works
					// against the stop. At the 45 deg limit the AoA overshot 3-6 deg, protection cut the rate to zero, the AoA fell
					// 5-10 deg below and the cycle repeated every ~1.2 s (bench 520 km/h entry; 13 Oct log 92-95 s). While a
					// velocity-vector roll runs, the braking is taken as what is left for pitch less the coupling, and the
					// capture's near-limit gain comes down with it.
					float kU = 3f;
					float kD = 3f;
					if (RollCapture && Plugin.CoupGateAtLimitOn && Ia[1, 1] > 1f)
					{
						float saR = Mathf.Sin(alpha * (MathF.PI / 180f));
						float caR = Mathf.Cos(alpha * (MathF.PI / 180f));
						if (Plugin.RevFlowRollSense && Plugin.RevFlowNoseAxis)
						{
							caR = Mathf.Abs(caR);   // 4.3.0: the pilot's lateral axis (LateralLaw)
						}
						float pSr = Mathf.Abs((omega.x * caR + omega.z * saR) * 57.29578f);
						float lg = Mathf.Clamp01((pSr - 10f) / 20f);
						if (lg > 0f)
						{
							float coupA = (Ia[2, 2] - Ia[0, 0]) / Ia[1, 1] * omega.x * omega.z * 57.29578f;
							float bU = Mathf.Max(AuthBrakeFloor, Mathf.Min(aBrakeTgt, aDnPitch) - RollCaptureCoupK * Mathf.Max(0f, coupA));
							float bD = Mathf.Max(AuthBrakeFloor, Mathf.Min(aBrakeUp, aUpPitch) - RollCaptureCoupK * Mathf.Max(0f, 0f - coupA));
							bU = Mathf.Lerp(aBrakeTgt, Mathf.Min(aBrakeTgt, bU), lg);
							bD = Mathf.Lerp(aBrakeUp, Mathf.Min(aBrakeUp, bD), lg);
							kU = 3f * Mathf.Sqrt(bU / Mathf.Max(1f, aBrakeTgt));
							kD = 3f * Mathf.Sqrt(bD / Mathf.Max(1f, aBrakeUp));
							aBrakeTgt = bU;
							aBrakeUp = bD;
						}
					}
					// 2.2.1: the stop also takes the pitch effectors' lag (CapLagT, scaled by the nozzles' share of the authority:
					// they slew ~70 deg/s across their travel and carry most of it at low IAS). Stopping distance q tau + q^2 / 2a
					// instead of q^2 / 2a: a re-pull at ~200 km/h from 38 deg came in at 30 deg/s against a bound that assumed the
					// jet could stop at once, and overshot the 45 deg limit by 1.5 deg (13 Oct 10:46 log, 107 s; bench 46.4).
					float sqU = Mathf.Sqrt(1.4f * aBrakeTgt * num22);
					float sqD = Mathf.Sqrt(1.4f * aBrakeUp * num23);
					if (CapLagT > 0f && Plugin.CapLagOn)
					{
						float tauC = CapLagT * CapTvcShare();
						if (tauC > 0.001f)
						{
							float aU = 0.7f * Mathf.Max(1f, aBrakeTgt);
							float aD = 0.7f * Mathf.Max(1f, aBrakeUp);
							sqU = aU * (Mathf.Sqrt(tauC * tauC + 2f * num22 / aU) - tauC);
							sqD = aD * (Mathf.Sqrt(tauC * tauC + 2f * num23 / aD) - tauC);
						}
					}
					capBoundU = num8 + Mathf.Min(sqU, kU * num22);
					capBoundD = num8 - Mathf.Min(sqD, kD * num23);
				}
				else
				{
					capBoundU = num8 + Mathf.Min(Mathf.Sqrt(2f * aDn * num22), 3f * num22);
					capBoundD = num8 - Mathf.Min(Mathf.Sqrt(2f * aUp * num23), 3f * num23);
				}
				capBoundU += capNegU;
				capBoundD -= capNegD;
				// integrators: while the pilot's command is held back by this bound near the limit. Past the limit they
				// learn whenever the AoA rate is small. Short of it only after CapIHoldT of continuous hold within CapIBand:
				// the proportional capture converges from below, and learning on that approach banked error that carried
				// the jet over the limit on arrival. They keep their value through transients and bleed off otherwise.
				bool bindU = capAtU && Mathf.Abs(num20 - alpha) < 2f && qRaw >= capBoundU + capIUp - 0.5f;
				bool bindD = capAtD && Mathf.Abs(alpha - num21) < 2f && qRaw <= capBoundD + capIDn + 0.5f;
				bool capSettled = Mathf.Abs(alphaDot) < CapIAlphaDot;
				capHoldTU = (bindU && Mathf.Abs(num20 - alpha) < CapIBand) ? (capHoldTU + dt) : 0f;
				capHoldTD = (bindD && Mathf.Abs(alpha - num21) < CapIBand) ? (capHoldTD + dt) : 0f;
				if (bindU)
				{
					float eI = num20 - alpha;
					if (capSettled && capHoldTU > 0f && (eI < 0f || capHoldTU > CapIHoldT))
					{
						capIUp = Mathf.Clamp(capIUp + CapKi * eI * dt, 0f - CapIMax, CapIMax);
					}
				}
				else
				{
					capIUp = Mathf.MoveTowards(capIUp, 0f, 8f * dt);
				}
				if (bindD)
				{
					float eI = num21 - alpha;
					if (capSettled && capHoldTD > 0f && (eI > 0f || capHoldTD > CapIHoldT))
					{
						capIDn = Mathf.Clamp(capIDn + CapKi * eI * dt, 0f - CapIMax, CapIMax);
					}
				}
				else
				{
					capIDn = Mathf.MoveTowards(capIDn, 0f, 8f * dt);
				}
				capBoundU += capIUp;
				capBoundD += capIDn;
				capPushU = Mathf.Max(0f, num8 - capBoundU);
				capPushD = Mathf.Max(0f, capBoundD - num8);
				num18 = Mathf.Min(num18, capBoundU);
				num19 = Mathf.Max(num19, capBoundD);
			}
			if (tas > 25f)
			{
				float num24 = Mathf.Max(nAlpha, 0.02f);
				float num25 = ((nz > 1f) ? alphaDotHoldG : 0f);
				float num26 = ((nz < 0f) ? alphaDotHoldG : 0f);
				float num27 = 0.15f + RollLoadLead * Mathf.Clamp01(Mathf.Abs(omega.x * 57.29578f) / 150f);
				float num28 = (num5 - nz) / num24 - Mathf.Max(0f, alphaDot - num25) * num27;
				float num29 = (nz - num6) / num24 - Mathf.Max(0f, 0f - alphaDot - num26) * num27;
				float num30 = ((num28 > 0f) ? Mathf.Min(Mathf.Sqrt(2f * aDn * num28), 3f * num28) : (3f * num28));
				float num31 = ((num29 > 0f) ? Mathf.Min(Mathf.Sqrt(2f * aUp * num29), 3f * num29) : (3f * num29));
				num18 = Mathf.Min(num18, num8 + num25 + num30);
				num19 = Mathf.Max(num19, num8 - num26 - num31);
				// 1.9.0: only while the load is being taken off (the stick eased, or the shaped G command still coming down to
				// it). In a steady hold its 0.15 g dead zone pinned the load 0.15-0.2 g above the command once the hybrid
				// pitch loop removed the oscillation that used to hide it.
				bool gUnderGate = unload || nCmdRaw < nCmd - 0.05f || !Plugin.CmdShaping;
				if (Mode == FcsMode.AoAG && betaG > 0.5f && Plugin.GUndershootCapture > 0f && gUnderGate)
				{
					float num32 = (nz - nCmd - 0.15f) / num24;
					float num33 = ((num32 > 0f) ? Mathf.Min(Mathf.Sqrt(2f * aUp * num32), 3f * num32) : (3f * num32));
					if (num32 > 0f || !GUnderAboveOnly)
					{
						num19 = Mathf.Max(num19, num8 - num33 / Mathf.Clamp01(Plugin.GUndershootCapture));
					}
					gUnderDbg = num33;
				}
				else
				{
					gUnderDbg = 0f;
				}
			}
			if (protHold && (!flag || Mode != FcsMode.AoAG || Mathf.Abs(sp) < 0.7f || (protHoldSide > 0f && alphaU < num - 5f) || (protHoldSide < 0f && alphaU > num2 + 5f)))
			{
				protHold = false;
			}
			if (flag && Mode == FcsMode.AoAG && !protHold)
			{
				if (alphaU > num + 5f && sp > 0.85f)
				{
					protHold = true;
					protHoldSide = 1f;
				}
				else if (alphaU < num2 - 5f && sp < -0.85f)
				{
					protHold = true;
					protHoldSide = -1f;
				}
			}
			float num34;
			// 1.9.5: the pilot's own command within the normal bounds - the AoA protection may not hold it back from a
			// command that is already beyond protection's own (more nose-down above the limit, more nose-up below the
			// negative one). Before, protection set both bounds to its command, and from 1.9.3 (soft onset) that command is
			// ~the flight-path rate just past the limit: full forward stick at 65.5 deg still got +8..+12 deg/s nose-up.
			float qPilot = ((!(num19 > num18)) ? Mathf.Clamp(qRaw, num19, num18) : ((alpha >= 0f) ? num18 : num19));
			if (protHold)
			{
				num34 = 0f;
				num18 = (num19 = num34);
				prot = true;
			}
			else if (flag && alphaU > num + 0.5f)
			{
				// 1.9.3: continuous onset. The protection branch starts at alpha > limit + 0.5, where the capture branch commands
				// the flight-path rate; it used to subtract sqrt(2 a e) + 4 there at once (~14 deg/s), a step that pitched the
				// jet 3-4.5 deg below the limit in max-AoA turns (the 'pitch hitch'). Now 0 at the threshold, linear
				// (ProtOnsetGain /s per deg) until it meets the braking curve, and the +4 ramps in over 2 deg.
				float eP = alphaU - num - 0.5f;
				float num35 = Mathf.Min(Plugin.ProtRate, Mathf.Min(ProtOnsetGain * eP, Mathf.Sqrt(2f * Mathf.Max(aUp, 40f) * (alphaU - num))) + 4f * Mathf.Clamp01(eP / 2f));
				num35 = Mathf.Min(Plugin.ProtRate, Mathf.Max(num35, capPushU)); // 2.0.14: never weaker than the capture just below
				float num36 = Mathf.Max(num8 - num35, 0f - Plugin.ProtRate);
				float num37 = 1f - Mathf.Clamp01(sp / 0.9f);
				num34 = ((num36 < 0f) ? (num36 * num37) : num36);
				if (!StickCancelsLimit || qRaw < num34) num34 = Mathf.Min(num34, qPilot);
				num18 = (num19 = num34);
				// 1.9.3: the protection state (pitch priority in the allocator, fight costs off, ...) only past ProtFlagBand,
				// so a jet holding the limit in a turn does not toggle it every few frames
				prot = eP > ProtFlagBand;
			}
			else if (flag && alphaU < num2 - 0.5f)
			{
				float eN = num2 - alphaU - 0.5f;
				float num38 = Mathf.Min(Plugin.ProtRate, Mathf.Min(ProtOnsetGain * eN, Mathf.Sqrt(2f * Mathf.Max(aDn, 40f) * (num2 - alphaU))) + 4f * Mathf.Clamp01(eN / 2f));
				num38 = Mathf.Min(Plugin.ProtRate, Mathf.Max(num38, capPushD)); // 2.0.14
				float num39 = Mathf.Min(num8 + num38, Plugin.ProtRate);
				float num40 = 1f - Mathf.Clamp01((0f - sp) / 0.9f);
				num34 = ((num39 > 0f) ? (num39 * num40) : num39);
				if (!StickCancelsLimit || qRaw > num34) num34 = Mathf.Max(num34, qPilot);
				num18 = (num19 = num34);
				prot = eN > ProtFlagBand;
			}
			else
			{
				num34 = ((!(num19 > num18)) ? Mathf.Clamp(qRaw, num19, num18) : ((alpha >= 0f) ? num18 : num19));
				if (unload)
				{
					if (num10 > 0f)
					{
						num34 = Mathf.Max(num34, 0f);
					}
					else if (num10 < 0f)
					{
						num34 = Mathf.Min(num34, 0f);
					}
				}
				if (Mode == FcsMode.AoAG)
				{
					// 2.44.0 (StickCancelsLimit): at the AoA limit the limit's own nose-down (the capture bound) scales with 1 - stick / 0.9
					float flU = (StickCancelsLimit && capAtLimU) ? Mathf.Min(0f, num18) * (1f - Mathf.Clamp01(sp / 0.9f)) : 0f;
					float flD = (StickCancelsLimit && capAtLimD) ? Mathf.Max(0f, num19) * (1f - Mathf.Clamp01((0f - sp) / 0.9f)) : 0f;
					if (sp > 0.02f && alpha > -2f)
					{
						num34 = Mathf.Max(num34, (nz > num5) ? ((0f - Plugin.ProtRate) * (1f - sp)) : flU);
					}
					else if (sp < -0.02f && alpha < 2f)
					{
						num34 = Mathf.Min(num34, (nz < num6) ? (Plugin.ProtRate * (1f + sp)) : flD);
					}
				}
			}
			if (flag && !prot)
			{
				if (num3 < num - 0.05f && alphaU > num3)
				{
					float num41 = alphaU - num3;
					float num42 = Mathf.Min(Plugin.AsymRecoveryRate, Mathf.Min(3f * num41, Mathf.Sqrt(2f * Mathf.Max(aUp, 40f) * num41)));
					num34 = Mathf.Min(num34, Mathf.Max(num8 - num42, 0f - Plugin.ProtRate));
				}
				else if (num4 > num2 + 0.05f && alphaU < num4)
				{
					float num43 = num4 - alphaU;
					float num44 = Mathf.Min(Plugin.AsymRecoveryRate, Mathf.Min(3f * num43, Mathf.Sqrt(2f * Mathf.Max(aDn, 40f) * num43)));
					num34 = Mathf.Max(num34, Mathf.Min(num8 + num44, Plugin.ProtRate));
				}
			}
			// 2.1.8 AoA-sweep yaw limit. A velocity-vector roll pS at AoA a is coordinated when the stability-axis yaw rate
			// rS = -p sin a + r cos a is zero; the kinematic sideslip rate is -rS. Sweeping the AoA under a roll changes rS by
			// -pS * alphaDot, and only the yaw (r, through cos a) and roll (p, through sin a) accelerations can turn the rate
			// vector back. 12 Oct 23:00 log, 232-243 s: a 30 deg/s roll at 65 deg AoA, 57 m/s, left r = +25..29 deg/s; the full
			// push right after swept the AoA 65 -> -43 at 48 deg/s, the yaw (12-15 deg/s^2 modelled) could not reverse r, the
			// sideslip ran to -70 deg and the reversed dihedral at negative AoA drove the roll up to 43 deg/s against the
			// command. The AoA rate is held to what the lateral axes can follow; a sweep that brings rS back to zero is free.
			// Measured state only; off with no roll (|pS| under SweepPs0), beyond the AoA limits and for AI.
			pullGovTouched = false;
			pullTradeTgt = 999f;
			tradeAlphaNew = -1f;
			pullTradeHoldDbg = false;
			float qAskPre = num34;
			// 2.6.7: whether the pitch command is asking for a sweep at all. The FF roll trade's ask is the command's distance
			// from the measured flight-path rate (qAskPre - wVe); with the stick in the detent the command is *exactly* zero
			// (qRaw = sp x qB behind the num9 detent gate), so that distance becomes the measured rate itself and the trade
			// pays for a sweep nobody asked for. Rolling at 250 km/h through zero AoA with the pitch stick centred, the jet's
			// own settling sweeps the AoA 12 -> -2 deg at 2-7 deg/s, and the trade read that as a 2-7 deg/s ask: the roll-rate
			// limit swung 30 <-> 160 deg/s as it wandered - the roll "randomly speeding up and down" at low IAS. The Raptor
			// hides it: its rudders keep the capacity high enough that the same cut only moves the limit 106-127.
			// The pilot's stick (the law's own detent gate) and any bound that actually bound the command - protection, the
			// g / AoA-rate clamps, the unload - all count; a centred stick with nothing binding does not. A steady rolling
			// pull still trades (the stick is out of the detent), and the ask there is 0 anyway once the g has settled.
			// |sp| > 0.04 is the law's own detent (num9 = Clamp01((|sp| - 0.01) / 0.06) crosses 0.5 there), written on sp
			// directly so it reads the same under the rate law, where num9 stays 1
			bool qSweepAsk = Mathf.Abs(sp) > 0.04f || prot || protHold || Mathf.Abs(num34 - qRaw) > 0.05f;
			// 2.4.16: also with PitchRateCommandOnly (FcsMode.Rate). The sweep limit, the rolling-pull sideslip governor and
			// PullRollTrade only ever act on the pitch-rate command (num34) and the measured state, so they apply the same way
			// under the rate law; gated to AoAG they were simply off there (28-29 Oct logs, mode 6: sweepLim 0, slipGovPull 1,
			// pullTrade 0 in every rolling pull; full aft + full roll ran to 17-23 deg of sideslip at 150-340 kt)
			// 2.2.11 PullRollTrade, feed-forward part: with the trade on, the roll limit is also set from the AoA rate the
			// stick asks for, before the governor has to react: the roll whose coordinating yaw the yaw axis can build while
			// the AoA rises at that rate (the governor's own feed-forward, solved for pS). At high IAS the roll got to
			// 140-157 deg/s inside 0.4 s while the AoA rose at 50-60 deg/s; the governor caught it on the sideslip and
			// unloaded, and pitch and roll hitched together (19 Oct 10:29 log, 254 s at 295 kt: sideslip 14.7 deg, q 61 -> 5
			// -> 40, pS 138 -> 56 -> 129; 467 s at 345 kt the same).
			// 2.5.2 roll first, then pitch: the lateral governor's AoA-rise cap (yaw saturated with the roll already held; see
			// LateralLaw) holds the nose-up pitch-rate command to the AoA-hold rate plus that rise, with the 2.2.9 release lag
			// 2.5.3: both ways. The yaw the pair needs is |pS sin AoA|; any AoA sweep under a roll changes it at pS cos AoA x AoA
			// rate, nose-up or nose-down, at positive or negative AoA. A sweep that grows the demand is paid from the yaw left
			// on the roll's side (yawSatAlphaRate); one that shrinks it from the rudders' other side (yawSatAlphaRateDn). This is
			// the 2.1.8 sweep yaw limit (off from RollGovernorReplaces 2) from the same rule as the pull cap, so a full push +
			// full roll from positive AoA no longer sweeps the yaw demand through zero faster than the rudders can swing (bench,
			// 300 km/h: the roll outran the yaw at the zero crossing and the sideslip diverged).
			yawSatPitchDbg = 0f;
			float num34Ask = num34;
			bool planOwns = PushPlan && pushPlanOn && PushPlanOwnsPitch && !pushPlanReleasing;   // 3.1.7: not in the ramp-out (the caps see the crossing)
			if (yawSatAlphaRate > 0f && flag && !prot && !isAI && LatGovMode && !planOwns)
			{
				// nose-up pitch grows |AoA| at positive AoA and shrinks it at negative AoA
				// 3.1.6: the toward-zero sweep rate fades in with the velocity roll (PushGovFade): open with no rotation to coordinate
				float rateTz = PushGovFade ? Mathf.Lerp(PushGovOpenRate, yawSatAlphaRateDn, pushGovFadeW) : yawSatAlphaRateDn;
				// 3.1.7 (SweepSideKept): the rates were sized on the lateral pass for the AoA side it saw; read them as that side's (a tick across zero AoA the
				// grow rate for negative AoA - the push side - was read as the pull side's: bench, pull through zero under a 20 deg/s roll, 39 -> 32 -> 39 for a tick)
				bool aPosS = SweepSideKept ? (yawSatAlphaSign >= 0f) : (alpha >= 0f);
				// 3.1.7 (SweepZeroBlend): the toward-zero sweep arrives at zero AoA at the rate the far side allows (braking at SweepZeroDecel), not at its
				// own and then a wall: pushing through zero under a 17 deg/s roll the command ran -52 at the crossing and -16 a tick later (bench low set,
				// push_1.0_4.0_0.5: q overshot to -49 against -16, nozzles -19 -> +20)
				// (applied last, after the floor and PushNoRetreat: the one slow-down a held push keeps is the one a roll still held on the far side needs)
				float zbRate = 999f;
				if (SweepZeroBlend && yawSatFarRate < 900f && aPosS)   // (push-overs only: past zero from negative AoA is the rolling pull's trade, tuned on its own - bench, 22:22 log 240.4 s pull-through + full roll: blended in, the pitch fell 44 -> 29 before the crossing)
				{
					float aLeftZ = ((alpha >= 0f) == aPosS) ? Mathf.Abs(alpha) : 0f;
					zbRate = Mathf.Max(yawSatFarRate, 0f) + Mathf.Sqrt(2f * Mathf.Max(1f, SweepZeroDecel) * aLeftZ);
				}
				float upRate = aPosS ? yawSatAlphaRate : rateTz;
				float dnRate = aPosS ? rateTz : yawSatAlphaRate;
				// 2.24.0 (SweepNoReverse), 2.31.1 written into the rates: a sweep toward zero AoA may be paced down to the flight-path
				// rotation, never below it (the nose is held, never turned back: the pitch-rate command the pace leaves is never on the
				// far side of zero). Held pushes too (SweepNoReverseHeld, 2.25.0); not under the no-fin nose-hold (2.19.0: holding the
				// AoA on a turning flight path IS a positive body rate; with the floor the no-fin held pushes departed on the bench).
				if (SweepNoReverse)
				{
					float wNr = (SweepNoReverseHeld ? 1f : stickRelW) * (1f - sweepHoldDbg);
					upRate = Mathf.Lerp(upRate, Mathf.Max(upRate, 0f - num8), wNr);
					dnRate = Mathf.Lerp(dnRate, Mathf.Max(dnRate, num8), wNr);
				}
				float capY = num8 + upRate;
				float capLo = num8 - dnRate;
				// 3.1.6 (SweepFloorPushMin): the toward-zero sweep never holds the nose itself (body pitch rate) under the push-over minimum rate.
				// Its pace is an AoA rate against the flight path's turn; at 60-80 kt the path falls at ~20 deg/s, so a 23 deg/s AoA rate left
				// the nose at -2 deg/s (bench, 63 kt push with a 12 deg/s roll on: command 0 while the AoA fell, nozzles -17 -> +20).
				if (SweepFloorPushMin && Plugin.PushOverMinRate > 0f && yawHealth >= 0.99f && sweepHoldDbg <= 0f)   // (not with fins damaged: the bench's no-fin push-overs departed, 76-78 deg)
				{
					// scaled by the pilot's push toward zero AoA: a minimum for HIS push, not for the law's own released-stick recovery (bench, 18:23 log
					// 80.6 s: unscaled it loosened the released corkscrew's nose-down from -1 to -10 deg/s and changed everything after it)
					if (aPosS || SweepZeroBlend) capLo = Mathf.Min(capLo, 0f - Plugin.PushOverMinRate * Mathf.Clamp01(0f - sp));   // 3.1.7: past zero too (the push goes on through)
					if (!aPosS) capY = Mathf.Max(capY, Plugin.PushOverMinRate * Mathf.Clamp01(sp));
				}
				if (PushNoRetreat && yawHealth >= 0.99f && sweepHoldDbg <= 0f)
				{
					// toward zero only: past it the far side's grow cap is a roll still being held, not yaw left over (bench reg, pull-then-pushover + full roll
					// at 350 km/h: with the far side held too the sideslip ran to 26 deg at -45 AoA)
					bool towardZ = (alpha >= 0f) == aPosS;
					if (aPosS && towardZ) capLo = Mathf.Min(capLo, 0f - pushNrQ);
					if (!aPosS && towardZ) capY = Mathf.Max(capY, pullNrQ);
				}
				if (zbRate < 900f)
				{
					if (aPosS) capLo = Mathf.Max(capLo, num8 - zbRate);
					else capY = Mathf.Min(capY, num8 + zbRate);
					if (SweepFloorPushMin && Plugin.PushOverMinRate > 0f && yawHealth >= 0.99f && sweepHoldDbg <= 0f)
					{
						if (aPosS) capLo = Mathf.Min(capLo, 0f - Plugin.PushOverMinRate * Mathf.Clamp01(0f - sp));
						else capY = Mathf.Max(capY, Plugin.PushOverMinRate * Mathf.Clamp01(sp));
					}
				}
				if (num34 > capY)
				{
					yawSatPitchDbg = num34 - capY;
					num34 = capY;
				}
				else if (num34 < capLo)
				{
					yawSatPitchDbg = num34 - capLo;
					num34 = capLo;
				}
				yawSatCapQ = capY;
				yawSatCapQLo = capLo;
				yawSatCapOn = true;
			}
			else if (planOwns)
			{
				// 2.36.0: the plan owns the nose-down pitch; the caps re-arm from open when it ends (no stale bound at the bottom)
				yawSatCapQ = 120f;
				yawSatCapQLo = -120f;
				yawSatCapOn = false;
			}
			else if (yawSatCapOn && !(PushPlan && pushPlanOn))
			{
				// let go over GovCapTau, not in one frame
				yawSatCapQ = Mathf.Lerp(yawSatCapQ, 120f, Mathf.Clamp01(dt / Mathf.Max(0.02f, GovCapTau)));
				yawSatCapQLo = Mathf.Lerp(yawSatCapQLo, -120f, Mathf.Clamp01(dt / Mathf.Max(0.02f, GovCapTau)));
				num34 = Mathf.Clamp(num34, yawSatCapQLo, yawSatCapQ);
				if (yawSatCapQ > 100f)
				{
					yawSatCapOn = false;
				}
			}
			// 2.35.0 (PushPlan): the planned push - the nose-down body rate is bounded at the plan's, one curve
			if (PushPlan && pushPlanOn && !prot && !isAI)
			{
				if (pushPlanSide > 0f) num34 = Mathf.Max(num34, 0f - pushPlanQNow);
				else num34 = Mathf.Min(num34, pushPlanQNow);
			}
			// 2.36.0 (PushPlanClipKey): "the sweep toward zero is held back" - the allocator's pitch priority (pitchNeedDn) and the stabs'
			// attached window (sweepHoldWin) read it next tick - is whatever bounds the push: the sweep caps, or the plan when it is on.
			// Until 2.35.3 only the caps' clip was read, so under a plan those two ran only because the 60 deg/s ceiling also clipped
			// the stick (a full push always does) - the ceiling could not stand aside without taking them with it (2.35.1: sideslip
			// median 4.8 -> 11.2 with PushPlanOwnsPitch).
			sweepClipDbg = yawSatPitchDbg;
			sweepClipOn = yawSatCapOn;
			if (PushPlanClipKey && PushPlan && pushPlanOn && !prot && !isAI)
			{
				sweepClipDbg = num34Ask - num34;
				sweepClipOn = true;
			}
			// 2.5.1 (RollGovernorReplaces >= 1): the trade is the rolling-pull protection on its own - the AoA-rise hold above is off, so
			// the trade no longer waits for it: no IAS fade (the hold's own reaction covered low IAS before), and its slow release
			// while the stick still asks (PullTradeRiseHold) is kept here
			// 2.6.0: level 3 only - the trade is the rolling-pull protection on its own, at every IAS and either AoA sign
			// 2.5.5: both ways and at either AoA sign. A nose-down sweep (push-over under a roll) or a pull from negative AoA moves
			// the yaw the roll needs just as a pull does, and the roll is what is traded first; the AoA-rate caps then only have the
			// roll that is left to contend with. 10:10 log, 268 / 315 s: full push + full roll from +9 deg at 104 / 68 m/s - no trade
			// (pullTrade 139-154 against pSmax 119), the sweep cap took 15-50 deg/s off the pitch-rate command instead, and the pace
			// window cut the roll a second later anyway.
			if (Plugin.PullRollTrade > 0f && PullTradeFFK > 0f && SweepPosPullFree && flag && !prot && !isAI && tas > 25f && LatGovMode)
			{
				float aAskF = qSweepAsk ? Mathf.Abs(qAskPre - num8) : 0f;
				pullTradeHoldDbg = aAskF > 5f;
				pullTradeAsk = aAskF;
				if (aAskF > 5f)
				{
					float caF = Mathf.Abs(Mathf.Cos(alpha * (MathF.PI / 180f)));
					// faded in with IAS (PullTradeFFIas0 -> +PullTradeFFIasSpan m/s, 180 -> 230 kt; 2.2.12, was 230 -> 280: 13 deg of sideslip at 240 kt in the 10:53 log): below that the governor's own reaction keeps up
					// with the roll build (your 0.7 / 9 setting held at 250 kt and below)
					float tgtF = 999f;
					// 2.8.0: the yaw the pair needs grows at pS cos AoA x AoA rate. Against the roll the stick asks (pSAskStick, before
					// any cap) and the measured capacity on the side the sweep needs (CapacityEstimator, last tick), TradeCapShare of it:
					// no shortfall, no trade - the pitch runs free whatever the roll limit is doing; a shortfall is split PullRollTrade to
					// the roll (its target drops until its share is paid) and the rest to the AoA rate. Nothing here reads the roll limit,
					// so a released stick (pSAskStick 0) asks nothing of the pitch.
					float kDem = caF * aAskF * (MathF.PI / 180f);
					float growM = ((qAskPre - num8) * Mathf.Sign(alpha) >= 0f) ? 1f : -1f;
					float rollRef = (Mathf.Abs(pTraj.x) > 2f) ? pTraj.x : paceTgtRaw;
					float sideM = growM * Mathf.Sign(rollRef * Mathf.Sin(alpha * (MathF.PI / 180f)));
					// 2.21.0: the low-passed read (TradeCapTau) - the trade
					// is the one accounting of the rolling pull's pitch share and must not dip with the rudders' servo transit (see CapacityEstimator)
					float capM = (sideM >= 0f) ? capEst.capTrZeP : capEst.capTrZeN;
					float capS = Mathf.Max(0f, TradeCapShare * capM);
					float demandM = pSAskStick * kDem;
					if (demandM > capS && kDem > 1E-04f)
					{
						float shortM = demandM - capS;
						float trM = (yawHealth >= 0.99f) ? Plugin.PullRollTrade : Mathf.Min(Plugin.PullRollTrade, PullRollTradeFinLoss);
						float pSNew = Mathf.Max(PullTradeFloor, pSAskStick - trM * shortM / kDem);
						float leftM = pSNew * kDem - capS;
						float aNew = (leftM > 0f && pSNew > 1f) ? (aAskF - leftM / (pSNew * caF * (MathF.PI / 180f))) : aAskF;
						tgtF = pSNew;
						tradeAlphaNew = Mathf.Max(StopSlipPitchFloor, aNew);
					}
					else
					{
						tgtF = 999f;
					}
					if (tgtF < pullTradeTgt)
					{
						float pSF = Mathf.Abs((omega.x * caF + omega.z * Mathf.Sin(alpha * (MathF.PI / 180f))) * 57.29578f);
						// it stops the roll building past the target, it does not cut a roll already established (cutting a
						// 70 deg/s roll at 35 deg AoA swung the sideslip 4 -> -10 deg on the bench); the governor's own trade does that
						// 2.2.25: below PullTradeCutA (fading out over the next 10 deg) it does cut an established roll: a pull
						// into a fast roll from low AoA otherwise ran the roll on until the governor caught the sideslip, then cut
						// the AoA rise and the roll together (20 Oct 01:41 log, 230-260 kt, rolling 175-210 deg/s: pitch rate
						// 35 -> 22-31 -> 48 deg/s, sideslip 6-8 deg). At low AoA a velocity-vector roll is nearly a body roll, so
						// slowing it does not swing the sideslip the way it did at 35 deg AoA.
						float cutW = (PullTradeCutA > 0f) ? Mathf.Clamp01((PullTradeCutA + 10f - Mathf.Abs(alpha)) / 10f) : 0f;
						tgtF = Mathf.Max(tgtF, Mathf.Lerp(Mathf.Min(pSF, pullTradePs), tgtF, cutW));
						if (pullTradePs > Mathf.Max(pSF, tgtF))
						{
							pullTradePs = Mathf.Max(pSF, tgtF);
						}
						pullTradeTgt = tgtF;
					}
				}
			}
			// 2.2.8: the gate recovers whenever it is not being updated (no roll, negative AoA, at a limit). It used to hold
			// its last value until the next rolling pull, which then started governed (19 Oct log, 30-36 s: 0.10 held through
			// 5 s of wings-level flight; the next rolling pull at 36.3 s was held to 4 deg/s of AoA rise at 1 deg of sideslip).
			if (!pullGovTouched)
			{
				pullGovF = Mathf.MoveTowards(pullGovF, 1f, PullGovIdleRise * dt);
				pullFFBoost = Mathf.MoveTowards(pullFFBoost, 1f, PullFFBoostIdle * dt);
				pullServoEx = 0f;
				slipGovPullDbg = 1f;
				// 2.2.20: when the governor stops acting (roll under SlipGovPs0, AoA through zero) its cap is let go over
				// GovCapTau instead of at once, so an unload does not snap back to the stick's 48 deg/s in one frame
			}
			pullTradePs = Mathf.MoveTowards(pullTradePs, pullTradeTgt, ((pullTradeTgt < pullTradePs) ? PullTradeFall : ((pullTradeHoldDbg && PullTradeRiseHold > 0f) ? PullTradeRiseHold : PullTradeRise)) * dt);
			if (pullTradeTgt >= 999f && pullTradePs > 400f)
			{
				pullTradePs = 999f;
			}
			qMaxDbg = num18;
			qMinDbg = num19;
			// 2.0.5: the final clamp used AoARecoveryRate (48) only, so MaxPitchRate above it did nothing
			float qAbsMax = Mathf.Max(Plugin.ProtRate, Plugin.MaxPitchRate);
			qCmd = Mathf.Clamp(num34, 0f - qAbsMax, qAbsMax);
			if (Mode == FcsMode.AoAG && !prot && Plugin.PitchAntiWindup > 0f && num9 > 0.5f)
			{
				float aw = Plugin.PitchAntiWindup * (qCmd - qRaw) * dt;
				if (aw * aInt < 0f)
				{
					aInt = ((aInt > 0f) ? Mathf.Max(0f, aInt + (1f - betaG) * aw) : Mathf.Min(0f, aInt + (1f - betaG) * aw));
				}
				if (aw * gInt < 0f)
				{
					gInt = ((gInt > 0f) ? Mathf.Max(0f, gInt + betaG * aw) : Mathf.Min(0f, gInt + betaG * aw));
				}
			}
			float num45 = qCmd - qTraj.x;
			bool pitchOnset = num45 * qTraj.x >= 0f;
			float a3 = Mathf.Max(PitchTrajAccK * ((num45 > 0f) ? (pitchOnset ? aUpP : aUp) : (pitchOnset ? aDnP : aDn)), 30f);
			a3 = Mathf.Min(a3, Mathf.Max(30f, Plugin.PitchOnsetGps / Mathf.Max(0.05f, nAlpha)));
			// 1.9.0: stick released in the G regime: the pitch rate is taken off over ~ReleaseTauG instead of in one step, so
			// the load comes off at a steadier rate at high dynamic pressure (the zero-rate hold itself is unchanged)
			if (Mode == FcsMode.AoAG && Plugin.CmdShaping && !prot && betaG > 0.5f && Mathf.Abs(sp) < 0.07f && num45 * qTraj.x < 0f)
			{
				a3 = Mathf.Min(a3, Mathf.Max(3f, Mathf.Abs(qTraj.x) / ReleaseTauG));
			}
			float j = (prot ? (a3 / 0.1f) : (a3 / Mathf.Max(0.02f, PitchTrajJerkT)));
			if (unload)
			{
				a3 = ((num10 * alphaDot > 0.5f) ? Mathf.Min(a3, Mathf.Max(60f, 2f * Plugin.UnloadDecel)) : Mathf.Min(a3, Plugin.UnloadDecel));
				j = a3 / 0.3f;
			}
			// 2.3.3 (RateCaptureEffectorLag): a pitch-rate capture toward zero ends with the decel ramping off over a3 / j =
			// 0.15 s, but the effectors braking the rate need longer to get back to trim: the nozzles at -20 deg take ~0.35 s
			// to swing to +6 at 70 deg/s. 21 Oct 23:44 log, 255 s: release from a 35 deg/s pull, the rate crossed zero at 0.3 s
			// with the nozzles still at -19, and ran to -10.6 deg/s (1.1 s below -2). The end jerk is limited so the decel is
			// off by the time the slowest pitch effector can be back at its trim: j_end = a3 / t_unwind, with t_unwind the
			// nozzles' |cmd - trim| / slew or the stabs' |cmd| / servo, whichever is longer. Only while braking a pitch rate
			// toward the command (not on onset, not in AoA protection or an unload, where the capture planning is their own).
			float jEnd = j;
			rateCapLagDbg = 0f;
			if (Plugin.RateCaptureLag > 0f && !prot && !unload && num45 * qTraj.x < 0f && Mathf.Abs(qTraj.x) > 2f)
			{
				float tUn = 0f;
				if (tvc.active && tvc.rate > 1f)
				{
					tUn = Mathf.Abs(tvc.cmd) / tvc.rate;
				}
				foreach (SurfaceEffector seU in surfaces)
				{
					if (seU.kind == SurfaceKind.Stabilator && seU.active && seU.rate > 1f)
					{
						tUn = Mathf.Max(tUn, Mathf.Abs(seU.cmd) / seU.rate);
					}
				}
				tUn = Plugin.RateCaptureLag * tUn + RateCaptureLagServo;
				rateCapLagDbg = tUn;
				jEnd = Mathf.Min(j, a3 / Mathf.Max(0.15f, tUn));
			}
			// 3.1.8 PitchTrajLead: the command's slope (deg/s^2), steps ignored
			float dqc = qCmd - qCmdPrevL;
			qCmdPrevL = qCmd;
			// a step (the stick, a bound switching) is not a slope: the estimate starts again from zero
			if (Mathf.Abs(dqc) > PitchTrajLeadStep || dt <= 1E-05f) qCmdSlopeF = 0f;
			else qCmdSlopeF += (dqc / dt - qCmdSlopeF) * Mathf.Clamp01(dt / Mathf.Max(dt, PitchTrajLeadTau));
			float vTq = (PitchTrajLead && !prot && !unload && !noseContactLaw) ? qCmdSlopeF : 0f;
			if (Mathf.Abs(vTq) < 2f) vTq = 0f;
			qTraj.Step(qCmd, a3, j, jEnd, dt, vTq);
			// 2.0.6: nose wheel still on the runway in the rate law: the jet cannot pitch yet, so the rate trajectory is
			// kept just ahead of the measured rate (its acceleration still asks for the rotation). When the nose comes up
			// the command starts from the jet's own rate, as it would in the air, instead of a step to the full command.
			if (noseContactLaw && qTraj.x > 0f)
			{
				float qMeas = omega.y * 57.29578f;
				qTraj.x = Mathf.Min(qTraj.x, Mathf.Max(qMeas, 0f) + Mathf.Min(Mathf.Max(qTraj.v, 0f) * RotLeadT, RotLeadMax));
			}
			qT = qTraj.x;
			// 3.1.7 PushNoRetreat: what the held push (stick forward) / the pull toward zero from negative AoA has reached
			pushNrQ = (sp < -0.3f) ? Mathf.Max(pushNrQ, 0f - qT) : 0f;
			pullNrQ = (sp > 0.3f && alpha < 0f) ? Mathf.Max(pullNrQ, qT) : 0f;
		}

		internal static float LowIasYawBoostAt(float iasNow)
		{
			if (YawBoostFixed >= 1f)
			{
				return YawBoostFixed;
			}
			float num = Mathf.Max(1f, Plugin.LowIasYawBoost) - 1f;
			float lowIasYawBoostFullIas = Plugin.LowIasYawBoostFullIas;
			float num2 = ((iasNow <= lowIasYawBoostFullIas) ? 1f : ((float)Math.Exp((0f - (iasNow - lowIasYawBoostFullIas)) / Mathf.Max(1f, Plugin.LowIasYawBoostFalloff))));
			num2 *= Mathf.Clamp01((120f - iasNow) / 10f);
			return 1f + num * num2;
		}

		internal static float FightPedalRelease = 1f;

		internal static int FightMode = 0;

		internal static bool RelQuietHold = true;

		internal static float RelQuietErr0 = 2f;

		internal static float RelQuietErrSpan = 4f;


		internal static float PitchFightQdot = 25f;

		internal static float PitchFightQHold = 0.6f;

		private float pfQHold;

		public static float PitchFightErr0 = 3f;

		public static bool PitchFightAbsErr = false;

		public static float PitchFightErrSpan = 5f;

		internal static float FightRollRelease = 0.3f;

		private float fightRollHold;

		private float fightPed;

		// 1.8.41 pitch-reserve roll governor. Rolling at a held or restricted AoA makes pitch-up: the canted/stalled
		// down-going stab and flaperon lose lift for roll, and the inertial coupling (Izz-Ixx)/Iyy * p * r grows with the
		// roll rate squared. Above the roll-pitch-priority band (18-26 deg AoA) only the nozzles are left to hold it, and with
		// a store they sat on the -20 deg stop for whole rolls (2026-09-28 log: 28 deg cap, full aft + roll, nozzles pinned
		// 4 s). The governor watches the nozzles' remaining nose-down travel (nose-up at negative AoA) at the live thrust and,
		// while a roll is commanded, lowers the roll-rate limit and entry acceleration when less than RollPitchReserve of
		// their nose-down authority is left; it gives it back as the margin returns. Measured state only - no AoA/speed
		// schedule. 0 = off (1.8.40).
		internal static float RollGovDown = 1.5f;

		internal static float RollGovUp = 0.6f;

		internal static float RollGovFloor = 0.35f;

		internal static float RollGovMinRate = 30f;

		internal static float RollGovAccelFloor = 0.5f;

		internal static float RollGovAoA0 = 3f;

		public float rollGov = 1f;

		public float rollGovMargin;

		private Lpf rollGovMF;

		private Lpf govMarginRateF;

		private float rollGovMarginPrev = -1f;

		public float rollGovPredDbg = 1f;

		internal static bool GovPredictive = true;

		internal static float GovLookT = 0.3f;

		internal static float GovNeedHi = 0.2f;

		internal static float GovDownP = 0.8f;

		internal static float GovUpP = 1.0f;

		private void RollGovStep(float dt, float pLim, float rollSide)
		{
			float res = Plugin.RollPitchReserve;
			if (res <= 0f || tvc == null || !tvc.active || tvc.N < 2 || tvc.M == null || !Engaged || Mode == FcsMode.Ground || Mode == FcsMode.Off || revFlow)
			{
				rollGov = Mathf.MoveTowards(rollGov, 1f, 2f * dt);
				rollGovMargin = 1f;
				slatRelief = Mathf.MoveTowards(slatRelief, 0f, SlatReliefOut * dt);
				return;
			}
			float yLo = float.MaxValue;
			float yHi = float.MinValue;
			for (int k = 0; k < tvc.N; k++)
			{
				yLo = Mathf.Min(yLo, tvc.M[k].y);
				yHi = Mathf.Max(yHi, tvc.M[k].y);
			}
			float yCmd = tvc.MAt(tvc.cmd).y;
			float yMid = tvc.MAt(0f).y;
			// margin toward the side the roll's pitch moment pushes against: nose-down at positive AoA
			float half = (alpha >= 0f) ? (yMid - yLo) : (yHi - yMid);
			float marg = (alpha >= 0f) ? (yCmd - yLo) : (yHi - yCmd);
			float frac = (half > 1E-06f) ? Mathf.Clamp01(marg / half) : 1f;
			// 2.1.3: at the low-thrust AoA cap the nozzles carry little of the nose-down authority, so "nozzles pinned" no
			// longer means "no pitch reserve" (the stabs and the airframe hold it). The margin counts only by the nozzles'
			// share of the nose-down authority there; elsewhere unchanged. Without it the governor sat at its floor in every
			// idle-cap roll and jumped back whenever a pitch transient unpinned the nozzles (12 Oct log, 657 / 713 s).
			if (LowCapLatFix && Plugin.LowCapRollFix && LowThrustCapOn && alpha > 0f)
			{
				float tvShare = Mathf.Clamp01(half * IaInv[1, 1] * 57.29578f / Mathf.Max(20f, aDn));
				float nearCap = Mathf.Clamp01((alpha - (authCapEff - LowCapCoupBand)) / Mathf.Max(0.5f, LowCapCoupBand));
				frac = Mathf.Lerp(frac, Mathf.Lerp(1f, frac, tvShare), nearCap);
			}
			frac = rollGovMF.Step(frac, 0.06f, dt);
			// 2.1.5 predictive governor: the margin's rate of change projected GovLookT ahead. The old governor cut only once
			// the nozzles were already pinned (margin < 15 %) and then recovered at 0.6/s: a cut-and-restore of the roll target
			// (12 Oct 18:50 log: 120 -> 42 -> 85 deg/s at 96 s, 120 -> 74 -> 86 at 202 s, clean jet 347 s). Now it eases off
			// early and in proportion while the margin is heading for the stop, and comes back only when the margin is there and
			// no longer falling.
			float fracRate = govMarginRateF.Step(rollGovMarginPrev >= 0f ? (frac - rollGovMarginPrev) / Mathf.Max(dt, 1E-4f) : 0f, 0.1f, dt);
			rollGovMarginPrev = frac;
			float fracPred = Mathf.Clamp01(frac + Mathf.Min(0f, fracRate) * (Plugin.LoadedRollSteadyOn ? 0f : GovLookT));
			rollGovPredDbg = fracPred;
			float aoaGate = Mathf.Clamp01((Mathf.Abs(alpha) - RollGovAoA0) / 3f);
			float latNow = Mathf.Max(Mathf.Clamp01((Mathf.Abs(sr) - 0.05f) / 0.2f), Mathf.Clamp01((Mathf.Abs(pTraj.x) - 10f) / 20f));
			// 2.1.9 slat roll relief. The slats (Aryx's high-lift devices) are, in this game, only wing area on WingRear_L/R
			// whose centre of lift sits 1.34 m ahead of the CG, and the flap schedule has them fully out from 20 deg AoA. Out,
			// they push the nose up: retracting them gives 8-11 deg/s^2 nose-down at 300 km/h, 14-20 at 400, 27-39 at 550
			// (20-40 deg AoA), for ~7 % of the lift. While a roll is running at positive AoA and the nozzles' nose-down margin
			// heads toward the governor's threshold, the slats come in before the governor has to cut the roll; they go back
			// out once the margin is back or the roll is over. Measured state only (same margin the governor reads).
			SlatReliefStep(dt, fracPred, frac, aoaGate * latNow);
			if (latNow * aoaGate < 0.01f)
			{
				rollGov = Mathf.MoveTowards(rollGov, 1f, 2f * dt);
				return;
			}
			float need = Mathf.Clamp01(res);
			if (GovPredictive && Plugin.RollGovPredictiveOn)
			{
				float needHi = Mathf.Max(need + 0.05f, GovNeedHi);
				if (fracPred < needHi)
				{
					float deficitP = (needHi - fracPred) / needHi;
					rollGov = Mathf.Max(RollGovFloor, rollGov - GovDownP * deficitP * aoaGate * latNow * dt);
				}
				else if (frac >= need && fracRate > -0.05f)
				{
					rollGov = Mathf.Min(1f, rollGov + GovUpP * Mathf.Clamp01((frac - need) / Mathf.Max(0.05f, need)) * dt);
				}
				return;
			}
			if (frac < need)
			{
				float deficit = (need - frac) / Mathf.Max(0.05f, need);
				rollGov = Mathf.Max(RollGovFloor, rollGov - RollGovDown * deficit * aoaGate * latNow * dt);
			}
			else
			{
				rollGov = Mathf.Min(1f, rollGov + RollGovUp * Mathf.Clamp01((frac - need) / Mathf.Max(0.05f, need)) * dt);
			}
		}

		public float slatRelief;

		internal static float SlatReliefStart = 0.5f;

		internal static float SlatReliefFull = 0.2f;

		internal static float SlatReliefIn = 4f;

		internal static float SlatReliefOut = 1f;

		internal static float SlatReliefAoA0 = 12f;

		internal static float SlatReliefAoASpan = 6f;

		internal static float SlatReliefAoAHi = 35f;

		internal static float SlatReliefAoAHiSpan = 10f;

		private void SlatReliefStep(float dt, float fracPred, float frac, float rollGate)
		{
			if (!Plugin.SlatRollReliefOn || alpha <= 0f || isAI)
			{
				slatRelief = Mathf.MoveTowards(slatRelief, 0f, SlatReliefOut * dt);
				return;
			}
			float aG = Mathf.Clamp01((alpha - SlatReliefAoA0) / Mathf.Max(1f, SlatReliefAoASpan));
			aG *= 1f - Mathf.Clamp01((alpha - SlatReliefAoAHi) / Mathf.Max(1f, SlatReliefAoAHiSpan));
			float g = aG * Mathf.Clamp01(rollGate);
			// demand from the predicted margin: 0 at SlatReliefStart, 1 at SlatReliefFull (the governor's own threshold)
			float want = g * Mathf.Clamp01((SlatReliefStart - fracPred) / Mathf.Max(0.05f, SlatReliefStart - SlatReliefFull));
			if (want > slatRelief)
			{
				slatRelief = Mathf.MoveTowards(slatRelief, want, SlatReliefIn * dt);
			}
			else if (g < 0.01f || frac > SlatReliefStart)
			{
				// roll over, or the margin is back above the start: out again (no chasing the margin it creates while rolling)
				slatRelief = Mathf.MoveTowards(slatRelief, want, SlatReliefOut * dt);
			}
		}

		internal static bool DamageLatLimit = true;

		internal static bool YawTargetClamp = true;

		internal static bool GroundRollP = true;

		public bool wheelsTouch;

		internal static bool AsymThrustSplit = true;

		internal static bool EngineOutFixedCap = true;

		// 2.0.12: with an engine out the asymmetric-load cap comes down this many times faster (AsymCapTightenRate)
		internal static float EngOutTightenK = 4f;

		internal static float YawTargetMargin = 1f;

		// 2.20.0: the yaw-target clamp's reach includes the stabilators' yaw range while they are off the pitch
		// (stabPitchFrac below YawClampStabFree, scaled to 0 at it)
		internal static bool YawClampStabReach = true;

		internal static float YawClampStabFree = 0.25f;

		public float yawClampStabDbg;

		public bool engOut;

		// 2.4.1 (Ifrit 0.4.1): one engine runs but is down on thrust: the measured weak / strong thrust ratio (filtered 1 s) is
		// under EngineAsymEnter (release above EngineAsymExit), and the split is allowed (no thrust match holding it)
		public bool engAsym;

		public float engRatioF = 1f;

		internal static float EngineAsymEnter = 0.85f;

		internal static float EngineAsymExit = 0.92f;

		// weaker / stronger engine's thrust at full throttle, predicted from their damage state (1 = even)
		public float engAbRatio = 1f;

		// the throttle input where the afterburner starts (1 without one)
		public float engAbStart = 1f;

		// one engine can still light its afterburner and another cannot (predicted from the damage state)
		public bool engAbMismatch;

		// thrust match: the parasitic loss the FCS is holding on the stronger engine(s), N (0 = none), for the telemetry
		public float engMatchLoss;

		private bool matchApplied;

		// the AoA gate is holding the throttle / the thrust match
		public bool abGateOn;

		private bool engFixedPrev;

		// 0.4.1 / 2.4.1 thrust match on prediction: the yaw the thrust split WOULD make without the match (deg/s^2, from the
		// engines' RPMs and the game's thrust law) against the usable yaw authority at this AoA and speed; the match comes
		// in above MatchYawOn and lets go under MatchYawOff, so a split the fins cannot hold is never allowed to form
		public float splitYawPred;

		public float splitUsage;

		internal static float MatchYawOn = 0.8f;

		internal static float MatchYawOff = 0.5f;

		private bool matchYawOn;

		// 2.4.1b: the match is proportional. On the way up to the cap it comes in ahead of the AoA (the AoA predicted
		// MatchLead s ahead from its rate, over the MatchBand deg under the cap), so the healthy engine is already down to
		// the damaged one's thrust when the cap is reached and the cap lifts without a step; stick released with the AoA
		// under the cap, it eases back out (unless the fins could not hold the split: splitUsage / the VMC latch).
		public float matchF;

		public bool matchFull;

		internal static float MatchBand = 4f;

		internal static float MatchLead = 0.3f;

		// the match is complete this long before the AoA would reach the cap at its present rate
		internal static float MatchLeadTime = 0.8f;

		internal static float MatchRiseRate = 4f;

		internal static float MatchFallRate = 1.5f;

		// the VMC protection with a weak (running) engine: the match, latched until the sideslip is calm, instead of the throttle
		private bool vmcMatch;

		private float vmcMatchCalmT;

		// after leaving the fixed-cap state the thrust term stays out of the sweep for this long (the first sweep after the
		// match engages still sees the split settling: 23 Oct 00:00 log, cap 12 for one cycle)
		private float tKHoldT;

		// IAS at the last asymmetric-load sweep: the stored yaw authority scales with q from there
		private float asymSweepIas = 1f;

		private struct EngModel
		{
			public float rMin;

			public float rMax;

			public float ceil;

			public float remapX;

			public float remapY;

			public float t0;

			public float ab;

			public float abStart;

			public float abEnd;
		}

		private readonly EngModel[] engModel = new EngModel[4];

		private readonly float[] engUnmatched = new float[4];

		// predicted thrust of engine i at throttle input x (the game's Turbofan model, see the engine scan; the dynamic
		// factor is common to both engines and left out)
		private float EngThrustAt(int i, float x)
		{
			ref EngModel em = ref engModel[i];
			float tr = (em.remapY > em.remapX + 0.001f) ? Mathf.Clamp01((x - em.remapX) / (em.remapY - em.remapX)) : Mathf.Clamp01(x);
			float rpm = Mathf.Lerp(em.rMin, em.ceil, tr);
			float dry = (em.rMax > em.rMin) ? Mathf.Clamp01((rpm - 0.85f * em.rMin) / (em.rMax - em.rMin)) : tr;
			float th = em.t0 * dry;
			if (em.ab > 0f && em.rMax > 0f && rpm / em.rMax > 0.85f && x > em.abStart)
			{
				th += em.ab * Mathf.Clamp01((x - em.abStart) / Mathf.Max(0.01f, em.abEnd - em.abStart));
			}
			return th;
		}

		// weaker / stronger predicted thrust at throttle input x (1 when both are at idle or there is one engine)
		private float EngRatioAt(int n, float x)
		{
			float lo = float.MaxValue;
			float hi = 0f;
			for (int i = 0; i < n; i++)
			{
				float th = EngThrustAt(i, x);
				lo = Mathf.Min(lo, th);
				hi = Mathf.Max(hi, th);
			}
			return (hi > 1000f) ? Mathf.Clamp01(lo / hi) : 1f;
		}

		private int engTotal = -1;

		internal static float DamageLatFloor = 0.35f;

		public float yawHealth = 1f;

		private int rudTotal = -1;

		// 2.0.12: share of the rudders still on the jet (a rudder goes with its fin)
		private void YawHealthStep()
		{
			if (rudTotal < 0)
			{
				rudTotal = 0;
				foreach (SurfaceEffector s0 in surfaces)
				{
					if (s0.kind == SurfaceKind.Rudder)
					{
						rudTotal++;
					}
				}
			}
			if (engTotal < 0)
			{
				engTotal = tvc.engines.Count;
			}
			// an engine counts as out when destroyed / inoperable or when it makes under 15 % of the strongest one's
			// thrust while that one makes over 20 kN (flame-out, damage)
			int engOk = 0;
			float tMax = 0f;
			float tMin = float.MaxValue;
			// 2.4.1: each engine's thrust-vs-throttle from its damage state (Turbofan.FixedUpdate): the RPM it can reach is
			// minRPM + (maxRPM - minRPM) x (0.5 x condition + 0.5); at a throttle input x the target RPM is
			// Lerp(minRPM, that ceiling, (x - remap.x) / (remap.y - remap.x)); dry thrust = (RPM - 0.85 minRPM) / (maxRPM - minRPM)
			// of the maximum; the afterburner runs only above 85 % of maxRPM and fades in from its start throttle to its end.
			// From that: the weaker / stronger ratio at full throttle (engAbRatio) and whether one engine can light its
			// afterburner while another cannot (engAbMismatch).
			int nE = 0;
			engAbStart = 1f;
			foreach (Turbofan eg in tvc.engines)
			{
				float tE = 0f;
				if (eg != null && Acc.TF_operable(eg))
				{
					engOk++;
					if (nE < engModel.Length)
					{
						ref EngModel em = ref engModel[nE];
						em.rMin = Acc.TF_minRPM(eg);
						em.rMax = Acc.TF_maxRPM(eg);
						em.ceil = em.rMin + (em.rMax - em.rMin) * (0.5f * Mathf.Clamp01(Acc.TF_condition(eg)) + 0.5f);
						Vector2 rm = Acc.TF_remap(eg);
						em.remapX = rm.x;
						em.remapY = rm.y;
						em.t0 = Mathf.Max(0f, Acc.TF_staticThrust(eg));
						em.ab = 0f;
						em.abStart = 1f;
						em.abEnd = 1f;
						nE++;
					}
					JetNozzle[] nzE = Acc.TF_nozzles(eg);
					if (nzE != null)
					{
						foreach (JetNozzle jE in nzE)
						{
							if (jE != null)
							{
								tE += Mathf.Max(0f, Acc.JN_totalThrust(jE));
								if (nE > 0 && nE <= engModel.Length)
								{
									ref EngModel em2 = ref engModel[nE - 1];
									em2.ab += Mathf.Max(0f, Acc.JN_abMax(jE));
									em2.abStart = Mathf.Min(em2.abStart, Acc.JN_abStart(jE));
									em2.abEnd = Mathf.Min(em2.abEnd, Acc.JN_abEnd(jE));
								}
								engAbStart = Mathf.Min(engAbStart, Acc.JN_abStart(jE));
							}
						}
					}
				}
				tMax = Mathf.Max(tMax, tE);
				tMin = Mathf.Min(tMin, tE);
			}
			engAbRatio = 1f;
			engAbMismatch = false;
			if (engTotal > 1 && nE >= 2)
			{
				engAbRatio = EngRatioAt(nE, 1f);
				bool anyAb = false;
				bool allAb = true;
				for (int ie = 0; ie < nE; ie++)
				{
					ref EngModel emc = ref engModel[ie];
					bool ok = emc.ab > 0f && emc.rMax > 0f && emc.ceil / emc.rMax > 0.85f;
					anyAb |= ok;
					allAb &= ok;
				}
				engAbMismatch = anyAb && !allAb;
			}
			// the yaw the thrust split would make right now without the match: each engine's dry thrust from its RPM, plus
			// the afterburner it would have at the pilot's throttle (rawThrottle, before the match holds it under the
			// afterburner's start), less the weakest one's, on its nozzles' arms.
			// 2.4.3 (Ifrit 0.4.3): the afterburner was left out ("equal on both when both have it, and the throttle is held
			// under it when only one has") - but the throttle is only held while the match is in. Ifrit, 23 Oct 00:51 log,
			// 62-70 s: vertical at 35-42 m/s, the match released as the AoA fell under the cap, the healthy engine lit its
			// afterburner and the split was 7.1 deg/s^2 against 6-7 of yaw authority; predicted without the afterburner:
			// 3.8, splitUsage 0.80 at most (the match latches above 0.8), and the nose yawed 0.3 -> 9.5 deg/s in 8 s with
			// the stick centred. With it the prediction is 1.5 and the match holds. On this jet the arms are shorter
			// (the same code; the split's yaw comes from the nozzles' own positions).
			splitYawPred = 0f;
			splitUsage = 0f;
			if (engTotal > 1 && engOk == engTotal && nE >= 2)
			{
				float tgt = float.MaxValue;
				int k = 0;
				foreach (Turbofan eg in tvc.engines)
				{
					if (eg != null && Acc.TF_operable(eg) && k < nE)
					{
						ref EngModel emk = ref engModel[k];
						float ratio = (emk.rMax > emk.rMin) ? Mathf.Clamp01((Acc.TF_rpm(eg) - 0.85f * emk.rMin) / (emk.rMax - emk.rMin)) : 1f;
						engUnmatched[k] = emk.t0 * ratio;
						if (emk.ab > 0f && emk.rMax > 0f && emk.ceil / emk.rMax > 0.85f && rawThrottle > emk.abStart)
						{
							engUnmatched[k] += emk.ab * Mathf.Clamp01((rawThrottle - emk.abStart) / Mathf.Max(0.01f, emk.abEnd - emk.abStart));
						}
						tgt = Mathf.Min(tgt, engUnmatched[k]);
						k++;
					}
				}
				Vector3 mW = Vector3.zero;
				k = 0;
				foreach (Turbofan eg in tvc.engines)
				{
					if (eg != null && Acc.TF_operable(eg) && k < nE)
					{
						float extra = engUnmatched[k] - tgt;
						JetNozzle[] nzS = Acc.TF_nozzles(eg);
						if (extra > 1f && nzS != null && nzS.Length > 0)
						{
							float per = extra / nzS.Length;
							foreach (JetNozzle jS in nzS)
							{
								Transform tS = (jS != null) ? Acc.JN_thrustT(jS) : null;
								if (tS != null)
								{
									mW += Vector3.Cross(tS.position - cg, tS.forward * per);
								}
							}
						}
						k++;
					}
				}
				Quaternion rInv = Quaternion.Inverse(((Component)(object)ac).transform.rotation);
				Vector3 mA = rInv * mW;
				splitYawPred = Mathf.Abs(AccYaw(new Vector3(0f - mA.z, 0f - mA.x, mA.y)));
				float qScale = Mathf.Clamp((ias * ias) / (asymSweepIas * asymSweepIas), 0.05f, 2f);
				float cyNow = asymValid ? (asymCyNow * qScale) : 0f;
				splitUsage = (cyNow > 1f) ? (splitYawPred / Mathf.Max(1f, AsymResMaxFrac * cyNow)) : ((splitYawPred > 1f) ? 9f : 0f);
			}
			engOut = engTotal > 1 && engOk > 0 && (engOk < engTotal || (tMax > 20000f && tMin < 0.15f * tMax));
			float ratioNow = (engTotal > 1 && engOk == engTotal && tMax > 20000f) ? Mathf.Clamp01(tMin / tMax) : 1f;
			engRatioF += (ratioNow - engRatioF) * Mathf.Clamp01(Time.fixedDeltaTime / 1f);
			// the asymmetric state is the MEASURED thrust split (filtered 1 s, with hysteresis), and only while the split is
			// allowed: with the thrust match holding the healthy engine down (AoA above the damaged-engine cap) the thrust
			// is symmetric and there is nothing to cap for
			engAsym = !engOut && engTotal > 1 && engOk == engTotal && tMax > 20000f && !matchFull && (engAsym ? (engRatioF < EngineAsymExit) : (engRatioF < EngineAsymEnter));
			if (rudTotal <= 0)
			{
				yawHealth = 1f;
				return;
			}
			int act = 0;
			int any = 0;
			foreach (SurfaceEffector s1 in surfaces)
			{
				if (s1.active)
				{
					any++;
					if (s1.kind == SurfaceKind.Rudder)
					{
						act++;
					}
				}
			}
			yawHealth = (any == 0) ? 1f : ((float)act / (float)rudTotal);
		}

		// ---- 2.0.12 pitch stick curve: monotone cubic (Fritsch-Carlson) through (0,0), (s24,24), (s36,36), straight on
		// past 36 with the end slope -----------------------------------------------------------------------------------
		private readonly float[] scX = new float[3];

		private readonly float[] scY = new float[3] { 0f, 24f, 36f };

		private readonly float[] scM = new float[3];

		private float scK24 = -1f;

		private float scK36 = -1f;

		private void StickCurveSetup()
		{
			float a = Plugin.StickAoA24;
			float b = Plugin.StickAoA36;
			if (a == scK24 && b == scK36)
			{
				return;
			}
			scK24 = a;
			scK36 = b;
			scX[0] = 0f;
			scX[1] = a;
			scX[2] = b;
			float d0 = (scY[1] - scY[0]) / (scX[1] - scX[0]);
			float d1 = (scY[2] - scY[1]) / (scX[2] - scX[1]);
			// interior tangent: weighted harmonic mean (monotone, no overshoot); ends: one-sided, kept inside 3x the secant
			scM[1] = (d0 * d1 > 0f) ? (3f * (scX[2] - scX[0]) / ((2f * scX[2] - scX[1] - scX[0]) / d0 + (scX[2] + scX[1] - 2f * scX[0]) / d1)) : 0f;
			scM[0] = Mathf.Clamp(((2f * (scX[1] - scX[0]) + (scX[2] - scX[1])) * d0 - (scX[1] - scX[0]) * d1) / (scX[2] - scX[0]), 0.2f * d0, 3f * d0);
			scM[2] = Mathf.Clamp(((2f * (scX[2] - scX[1]) + (scX[1] - scX[0])) * d1 - (scX[2] - scX[1]) * d0) / (scX[2] - scX[0]), 0.2f * d1, 3f * d1);
		}

		internal float StickCurve(float s)
		{
			if (s <= 0f)
			{
				return 0f;
			}
			if (s >= scX[2])
			{
				return scY[2] + scM[2] * (s - scX[2]);
			}
			int k = (s < scX[1]) ? 0 : 1;
			float h = scX[k + 1] - scX[k];
			float t = (s - scX[k]) / h;
			float t2 = t * t;
			float t3 = t2 * t;
			return (2f * t3 - 3f * t2 + 1f) * scY[k] + (t3 - 2f * t2 + t) * h * scM[k] + (-2f * t3 + 3f * t2) * scY[k + 1] + (t3 - t2) * h * scM[k + 1];
		}

		internal float StickCurveInv(float aoa)
		{
			if (aoa <= 0f)
			{
				return 0f;
			}
			if (aoa >= scY[2])
			{
				return scX[2] + (aoa - scY[2]) / Mathf.Max(1f, scM[2]);
			}
			float lo = 0f;
			float hi = scX[2];
			for (int i = 0; i < 30; i++)
			{
				float mid = 0.5f * (lo + hi);
				if (StickCurve(mid) < aoa)
				{
					lo = mid;
				}
				else
				{
					hi = mid;
				}
			}
			return 0.5f * (lo + hi);
		}

		// ---- 2.0.12 VMC protection: last resort only -------------------------------------------------------------------
		// With an engine out the live engine's thrust yaws the jet; the rudders hold it down to the speed where their
		// authority (falling with q) equals that yaw (afterburner: ~64 m/s IAS on the bench airframe). Below it nothing the
		// FCS has can hold the heading and the jet yaws off into a departure (10:37 log, 150-158 s: sideslip -1 -> -67 deg
		// at 12 deg AoA, 47-58 m/s, every surface on its stop). The throttle is NOT touched while the FCS still holds the
		// yaw: only once the yaw is saturated AND the sideslip is running away (past VmcBeta and growing, or past
		// 2 x VmcBeta) is the throttle input pulled back, at VmcCutRate per s, until the sideslip stops growing; it is given
		// back at VmcReleaseRate per s once the sideslip is under VmcBeta / 2 and the yaw has room again. The pilot's
		// throttle is never raised, only capped.
		internal static bool VmcProtect = true;

		internal static float VmcBeta = 5f;

		internal static float VmcQuiet = 1f;

		private float vmcQuietT;

		internal static float VmcCutRate = 0.8f;

		internal static float VmcReleaseRate = 0.2f;

		public float vmcLim = 1f;

		private float yawClampExcess;

		internal static bool YawSatCountClamp = true;

		public bool vmcOn;

		private float vmcBetaPrev;

		private float vmcSatF;

		public void VmcApply(ControlInputs inp, float dt)
		{
			if (!VmcProtect || !Engaged || Failed || inp == null || Mode == FcsMode.Ground || wheelsTouch)
			{
				vmcLim = 1f;
				vmcOn = false;
				vmcBetaPrev = beta;
				abGateOn = false;
				matchYawOn = false;
				vmcMatch = false;
				matchF = 0f;
				matchFull = false;
				ThrustMatchClear();
				return;
			}
			float bDot = (beta - vmcBetaPrev) / Mathf.Max(dt, 0.001f);
			vmcBetaPrev = beta;
			// saturation: the yaw target out of reach (yawSat) or the asymmetric-load model's yaw usage past 1 at this AoA
			// yaw usage including the dead engine's thrust (the cap no longer counts it)
			float uY = asymValid ? (Mathf.Abs(asymAyNow) / Mathf.Max(asymCyNow, 1f)) : 0f;
			float satNow = Mathf.Max(yawSat, Mathf.Clamp01((uY - 0.9f) / 0.3f));
			vmcSatF += (satNow - vmcSatF) * Mathf.Clamp01(dt / 0.3f);
			// 2.0.13: sideslip from the pilot's own roll / pedal is not the engine's doing. The 13:07 log cut the throttle at
			// 80-87 m/s (290-315 km/h) on full-stick rolls toward the dead engine (sideslip -10..-16 from the roll, with the
			// rudders busy holding the thrust): those don't count. The lateral input (and the roll rate it makes) must have
			// been quiet for VmcQuiet s, and the sideslip must be on the side the live engine's thrust pushes it.
			bool latQuiet = Mathf.Abs(sr) < 0.15f && Mathf.Abs(sy) < 0.15f && Mathf.Abs(omega.x * 57.29578f) < 20f;
			vmcQuietT = latQuiet ? (vmcQuietT + dt) : 0f;
			bool thrustSide = !asymValid || asymAyNow * beta < 0f;
			bool growing = Mathf.Abs(beta) > VmcBeta && beta * bDot > 0f;
			bool runaway = (engOut || engAsym) && vmcSatF > 0.5f && thrustSide && (vmcQuietT > VmcQuiet || Mathf.Abs(beta) > 3f * VmcBeta) && (growing || Mathf.Abs(beta) > 2f * VmcBeta);
			// with a weak (running) engine the run-away is answered by the thrust match, not by cutting both engines
			// (23 Oct 00:06 F-22E log: both throttled to T/W 0.13 for 5 s at 40-60 m/s): the match holds until the
			// sideslip has been calm for 2 s
			bool weakEng = engAbRatio < EngineAsymEnter && !engOut;
			if (runaway && weakEng && Plugin.EngineAbAoAGate && EngineOutFixedCap)
			{
				vmcMatch = true;
				vmcMatchCalmT = 0f;
				runaway = false;
			}
			if (vmcMatch)
			{
				bool calmM = Mathf.Abs(beta) < 0.5f * VmcBeta && vmcSatF < 0.3f;
				vmcMatchCalmT = calmM ? (vmcMatchCalmT + dt) : 0f;
				if (vmcMatchCalmT > 2f || !weakEng)
				{
					vmcMatch = false;
				}
			}
			if (runaway)
			{
				if (!vmcOn)
				{
					vmcLim = Mathf.Min(vmcLim, inp.throttle);
				}
				vmcOn = true;
				vmcLim = Mathf.Max(0f, vmcLim - VmcCutRate * dt);
			}
			else
			{
				bool calm = Mathf.Abs(beta) < 0.5f * VmcBeta && vmcSatF < 0.3f;
				if (calm || !(engOut || engAsym))
				{
					vmcLim = Mathf.Min(1f, vmcLim + VmcReleaseRate * dt);
				}
				if (vmcLim >= 0.999f)
				{
					vmcOn = false;
				}
			}
			if (vmcLim < 0.999f)
			{
				inp.throttle = Mathf.Min(inp.throttle, vmcLim);
			}
			// 2.4.1 thrust match (Ifrit 0.4.1): with one engine down on thrust (predicted full-throttle ratio under
			// EngineAsymEnter) and the AoA above the damaged-engine cap, the healthy engine is held down to the damaged one's
			// thrust at whatever throttle the pilot sets: the game derates a damaged engine's whole throttle-to-RPM map, so
			// the two only ever match at idle by throttle alone; the healthy engine's parasiticThrustLoss (unused on this
			// jet) is set each tick from the measured RPMs so its dry thrust equals the weak one's. If the weak engine cannot
			// light its afterburner the throttle input is also held at the afterburner's start. No thrust split, so no AoA
			// cap for it: the pilot keeps the full AoA, and the thrust comes back once he brings the AoA down to the cap
			// himself (on above cap + 2, off under cap + 0.5: riding the limiter counts as inside). With an engine OUT
			// there is nothing to match: above EngineOutAoACap the throttle is held at military power. The VMC protection
			// (measured sideslip run-away) stays the last resort.
			bool weak = engAbRatio < EngineAsymEnter;
			bool gateNeed = Plugin.EngineAbAoAGate && EngineOutFixedCap && (engOut || weak);
			float tgtF = 0f;
			if (gateNeed && engOut)
			{
				// an engine out: nothing to match; above EngineOutAoACap the throttle is held at the afterburner's start
				float capO = Plugin.EngineOutAoACap;
				float aAbsO = Mathf.Abs(alpha);
				abGateOn = abGateOn ? (aAbsO > capO + 0.5f) : (aAbsO > capO + 2f);
				matchYawOn = false;
				vmcMatch = false;
			}
			else if (gateNeed)
			{
				float capA = Plugin.EngineDamagedAoACap;
				float aAbs = Mathf.Abs(alpha);
				float spDir = sp * ((alpha >= 0f) ? 1f : (-1f));
				// 0.4.2 / 2.4.2: the match is timed, not placed. The AoA limiter starts braking for the cap well before it, and
				// a match that only completes as the AoA arrives at the cap leaves the pilot feeling the brake until it does
				// (23 Oct 00:39 log: full match 1.4-1.9 s after it began at 230-360 kt, where the AoA came up at 18 deg/s,
				// against 0.23 s at 95-170 kt and 35 deg/s). Now: with the stick pulling toward the cap, the match goes to 1
				// once the AoA is predicted to reach the cap within MatchLeadTime s at its present rate (or is already within
				// MatchBand of it), so it is complete, at MatchRiseRate, before the limiter would have begun to brake,
				// whatever the speed; above the cap it is 1 whatever the stick; stick relaxed under the cap it is 0 (thrust
				// comes back).
				float aRate = alphaDot * ((alpha >= 0f) ? 1f : (-1f));
				float tCap = (capA - aAbs) / Mathf.Max(aRate, 0.5f);
				float ramp = 0f;
				if (aAbs > capA + 2f)
				{
					ramp = 1f;
				}
				else if (spDir > 0.15f && (aAbs > capA - MatchBand || (aRate > 0.5f && tCap < MatchLeadTime)))
				{
					ramp = 1f;
				}
				else if (spDir >= 0.05f && aAbs > capA - MatchBand)
				{
					ramp = Mathf.Clamp01((aAbs - (capA - MatchBand)) / MatchBand);
				}
				// and on prediction: the split's yaw against what the fins have at this AoA and speed (see the engine scan)
				matchYawOn = matchYawOn ? (splitUsage > MatchYawOff) : (splitUsage > MatchYawOn);
				tgtF = Mathf.Max(ramp, (matchYawOn || vmcMatch) ? 1f : 0f);
				abGateOn = tgtF > 0.001f || matchF > 0.001f;
			}
			else
			{
				abGateOn = false;
				matchYawOn = false;
				vmcMatch = false;
			}
			if (abGateOn && engOut)
			{
				inp.throttle = Mathf.Min(inp.throttle, engAbStart);
				matchF = 0f;
				matchFull = false;
				ThrustMatchClear();
			}
			else if (gateNeed && !engOut)
			{
				matchF = Mathf.MoveTowards(matchF, tgtF, ((tgtF > matchF) ? MatchRiseRate : MatchFallRate) * dt);
				matchFull = matchF >= 0.995f;
				if (matchF > 0.001f)
				{
					if (engAbMismatch)
					{
						// the afterburner the weak engine cannot light fades out with the match
						inp.throttle = Mathf.Min(inp.throttle, Mathf.Lerp(1f, engAbStart, matchF));
					}
					ThrustMatchApply(matchF);
				}
				else
				{
					ThrustMatchClear();
				}
			}
			else
			{
				matchF = 0f;
				matchFull = false;
				ThrustMatchClear();
			}
		}

		// hold every stronger engine's dry thrust down to the weakest one's, from the measured RPMs (see VmcApply)
		private void ThrustMatchApply(float frac)
		{
			float target = float.MaxValue;
			foreach (Turbofan eg in tvc.engines)
			{
				if (eg != null && Acc.TF_operable(eg))
				{
					float rMin = Acc.TF_minRPM(eg);
					float rMax = Acc.TF_maxRPM(eg);
					float ratio = (rMax > rMin) ? Mathf.Clamp01((Acc.TF_rpm(eg) - 0.85f * rMin) / (rMax - rMin)) : 1f;
					target = Mathf.Min(target, Mathf.Max(0f, Acc.TF_staticThrust(eg) * Acc.TF_dynFactor(eg)) * ratio);
				}
			}
			if (target == float.MaxValue)
			{
				ThrustMatchClear();
				return;
			}
			float lossMax = 0f;
			foreach (Turbofan eg in tvc.engines)
			{
				if (eg == null)
				{
					continue;
				}
				float loss = 0f;
				if (Acc.TF_operable(eg))
				{
					float rMin = Acc.TF_minRPM(eg);
					float rMax = Acc.TF_maxRPM(eg);
					float ratio = (rMax > rMin) ? Mathf.Clamp01((Acc.TF_rpm(eg) - 0.85f * rMin) / (rMax - rMin)) : 1f;
					float t0 = Mathf.Max(0f, Acc.TF_staticThrust(eg) * Acc.TF_dynFactor(eg));
					if (ratio > 0.001f)
					{
						loss = Mathf.Clamp(t0 - target / ratio, 0f, t0) * Mathf.Clamp01(frac);
					}
				}
				Acc.TF_parasitic(eg) = loss;
				lossMax = Mathf.Max(lossMax, loss);
			}
			engMatchLoss = lossMax;
			matchApplied = true;
		}

		public void ThrustMatchClear()
		{
			if (!matchApplied)
			{
				return;
			}
			matchApplied = false;
			engMatchLoss = 0f;
			if (tvc == null)
			{
				return;
			}
			foreach (Turbofan eg in tvc.engines)
			{
				if (eg != null)
				{
					Acc.TF_parasitic(eg) = 0f;
				}
			}
		}

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

		// 2.35.0 (PushPlan, see the statics): plan the push once at its start; hold; let it rise only
		private void PushPlanStep(float dt, float cA, float sA, float num11Stick)
		{
			// (fins damaged: the plan's IAS-based shed does not hold - the 2.19.0 nose hold and the governor pace the push instead)
			if (!PushPlan || isAI || tas < 40f || yawHealth < 0.99f)
			{
				pushPlanOn = false;
				pushPlanDone = false;
				return;
			}
			float aAbs = Mathf.Abs(alpha);
			float sgA = (alpha >= 0f) ? 1f : -1f;
			bool pushing = sgA * sp < -0.3f;
			float rDeg = Mathf.Abs(omega.z * 57.29578f);
			float capPlan = Mathf.Clamp(PushPlanK * Mathf.Pow(Mathf.Max(1f, ias), PushPlanExp), 6f, 80f);
			float rLeft = Mathf.Sqrt(2f * capPlan * Mathf.Max(0f, PushPlanSlip));
			float wv = Mathf.Max(0f, sgA * omegaV);
			if (pushPlanDone && (!pushing || sgA != pushPlanSide)) pushPlanDone = false;
			if (!pushPlanOn)
			{
				pushFloorDbg = 0f;
				if (pushing && aAbs > PushPlanA0 && rDeg > PushPlanR0)   // a push-over OF a rotation (push + roll entered together is the trade's)
				{
					pushPlanOn = true;
					pushPlanDone = false;
					pushPlanReleasing = false;
					pushPlanSide = sgA;
					float T0 = Mathf.Max(PushPlanTMin, Mathf.Max(0f, rDeg - rLeft) / capPlan);
					pushPlanT = T0;
					pushPlanQ = Mathf.Clamp(Mathf.Max(0f, aAbs - 10f) / T0 - PushPlanWvK * wv, PushPlanQMin, 60f);
					pushPlanA = 0f;
					if (PushPlanRamp)
					{
						// q(t) = q0 + A t; the AoA covered in T0 is (q0 + wv) T0 + A T0^2 / 2 = aAbs - 10
						float dA = Mathf.Max(0f, aAbs - 10f);
						float q0 = Mathf.Max(Mathf.Max(Plugin.PushOverMinRate, PushPlanQMin), (dA - 0.5f * PushPlanRampMax * T0 * T0) / T0 - PushPlanWvK * wv);
						pushPlanQ = Mathf.Clamp(q0, PushPlanQMin, 60f);
						pushPlanA = Mathf.Clamp(2f * (dA - (pushPlanQ + PushPlanWvK * wv) * T0) / (T0 * T0), 0f, PushPlanRampMax);
					}
					// 2.40.0: the 2.35.1 clamp under the sweep's AoA-rate ceiling is gone. It kept the plan 15 deg/s under the 60 deg/s ceiling
					// so the ceiling would not cut it mid-push; since 2.36.0 the ceiling stands aside under a plan (PushPlanOwnsPitch), and the
					// clamp only limited speed - the plan's own yaw model sets it now. Hybrid replays, the 20 pushes it bound: 10 deg AoA reached
					// 0.05-0.23 s sooner, sideslip +0..+2.6 deg (worst push unchanged, 8.3), the re-attach overshoot mostly smaller (23:10 70.4 s
					// 24.5 -> 3.5 deg/s: the plan now matches the jet's own pitch-down).
					pushPlanQNow = Mathf.Max(0f, -sgA * qCmd);
					pushPlanBleed = Mathf.Abs(pTraj.x) / T0;
					float pTEnd = (stickRelW > 0.5f || num11Stick * pTraj.x <= 0f) ? 0f : Mathf.Min(Mathf.Abs(num11Stick), Mathf.Abs(pTraj.x));
					pushPlanD = Mathf.Max(capPlan, (Mathf.Abs(pTraj.x) * Mathf.Abs(sA) - pTEnd * 0.17364818f) / T0);
				}
				return;
			}
			// end: the stick no longer pushes or the AoA changed side; below 10 deg the bound ramps out at PushPlanRelease (no kick at the
			// bottom) and lets go once it is above anything the stick can ask
			// 2.40.0 (2.36-dev's PushPlanEndAtBottom, now always): only the bottom's ramp-out ends it - a plan whose rate is the stick's full 60 deg/s mid-push is
			// still the plan (the yaw allows the full rate); ending there handed the push to the governor's caps mid-sweep (-60 -> -51)
			// 3.1.7 (PushPlanThrough): the ramp-out goes on through zero AoA while the stick still pushes the plan's way, at PushPlanReleaseSoft - it was
			// cut at the crossing (and ramped at 60 deg/s^2 before it), so a planned push went -40 -> -60 in 0.3 s at the bottom (Tom 7 Nov 00:46: "smooth
			// for a sec then it suddenly spikes"; 00:23 log 27.8 / 39.4 s)
			bool planPush = pushPlanSide * sp < -0.3f;
			pushPlanReleasing = false;
			if (PushPlanThrough && planPush && (sgA != pushPlanSide || aAbs < 10f))
			{
				pushPlanReleasing = true;
				pushPlanQ = 60f;
				// (the ramp goes on at its own acceleration, at least PushPlanReleaseSoft: no kink at 10 deg)
				pushPlanQNow = Mathf.MoveTowards(pushPlanQNow, 60f, (PushPlanRamp ? Mathf.Max(PushPlanReleaseSoft, pushPlanA) : PushPlanReleaseSoft) * dt);
				if (pushPlanQNow < 60f) return;
			}
			if (!pushing || sgA != pushPlanSide || (pushPlanQNow >= 60f && aAbs < 10f))
			{
				pushPlanDone = PushPlanDoneOpen && pushing && sgA == pushPlanSide;   // 3.1.7: finished at the bottom - open to the crossing
				pushPlanOn = false;
				return;
			}
			if (aAbs < 10f)
			{
				pushPlanQ = 60f;
				pushPlanQNow = Mathf.MoveTowards(pushPlanQNow, 60f, PushPlanRelease * dt);
				return;
			}
			// rise only: what the yaw left now would allow, approached at PushPlanRise
			float Tn = Mathf.Max(0.2f, Mathf.Max(0f, rDeg - rLeft) / capPlan);
			float qNeed = Mathf.Clamp(Mathf.Max(0f, aAbs - 10f) / Tn - PushPlanWvK * wv, PushPlanQMin, 60f);
			if (PushPlanRamp)
			{
				// the acceleration the rest of the push needs from the planned rate now (rise only, and smoothly)
				float qPl = Mathf.Max(pushPlanQ, Plugin.PushOverMinRate);
				float aNeed = Mathf.Clamp(2f * (Mathf.Max(0f, aAbs - 10f) - (qPl + PushPlanWvK * wv) * Tn) / (Tn * Tn), 0f, PushPlanRampHi);
				if (aNeed > pushPlanA) pushPlanA = Mathf.MoveTowards(pushPlanA, aNeed, PushPlanAJerk * dt);
				pushPlanQ = Mathf.Min(60f, qPl + pushPlanA * dt);
			}
			else if (qNeed > pushPlanQ) pushPlanQ = Mathf.Min(qNeed, pushPlanQ + PushPlanRise * dt);
			// 3.1.4/3.1.5: the minimum push-over rate (no sideslip guard)
			pushFloorDbg = Plugin.PushOverMinRate;
			float qTgt = Mathf.Max(pushPlanQ, pushFloorDbg);
			pushPlanQNow = Mathf.MoveTowards(pushPlanQNow, qTgt, PushPlanJerk * dt);
		}

		// F-22E 4.1.2: 1 where the load takes no roll authority from the stick's side (the load pulls that way), fading to 0 as the
		// trim it needs takes a tenth of the roll authority there (aRollGo: the stick-side authority after the load's trim)
		private float LoadSideWeight(float aRollGo)
		{
			return Mathf.Clamp01(1f - (aRoll - aRollGo) / Mathf.Max(1f, 0.1f * aRoll));
		}

		// F-22E 4.1.2: how much of a reversal's build-up the rudders' travel paces (0..1): with an asymmetric load (the 2.1.6 gate), toward
		// the side the load pulls, and where the yaw left after the load is what bounds the reversal (yawTop under the law's own reversal
		// acceleration; at low AoA the roll asks little yaw, yawTop is above it and nothing is paced)
		// 4.3.0 (ReversedFlowNoseAxis): the AoA whose velocity-vector axis is the pilot's lateral axis - |AoA| past 90 mirrored to
		// 180 - |AoA| with its sign (cos = |cos AoA|, sin = sin AoA). Forward flight: the AoA itself.
		internal static float LatAxisAlpha(float a)
		{
			if (!Plugin.RevFlowRollSense || !Plugin.RevFlowNoseAxis || Mathf.Abs(a) <= 90f)
			{
				return a;
			}
			return ((a >= 0f) ? 1f : (-1f)) * (180f - Mathf.Abs(a));
		}

		// 4.3.0: |AoA| (deg) where the sideslip hold starts / ends stepping aside in reversed flow (ReversedFlowNoseAxis), and the
		// lateral input (stick + pedal, 0-1) / commanded rotation (deg/s) from which it is fully aside
		public static float RevSlipHoldA0 = 90f;
		public static float RevSlipHoldA1 = 110f;
		public static float RevSlipHoldStick = 0.25f;
		public static float RevSlipHoldRate = 5f;
		// 4.3.0: TAS (m/s) below which the sideslip hold steps aside entirely while the pilot turns the nose in reversed flow (full
		// hold from RevSlipTas1)
		public static float RevSlipTas0 = 20f;
		public static float RevSlipTas1 = 40f;
		public float revSlipHoldW = 1f;

		private float LoadRevPace(float aRollGo, float cosA, float yawTop)
		{
			if (!RevLoadRamp || !AsymSharedLat || !Plugin.AsymSharedLatOn || !asymValid || LoadRevYawK <= 0f)
			{
				return 0f;
			}
			float dyG = Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / Mathf.Max(0.005f, CoupHeldDySpan));
			if (dyG <= 0f || RudderCrossTime() <= 0f)
			{
				return 0f;
			}
			float revLaw = RevFloorK * aRoll / Mathf.Max(Mathf.Abs(cosA), 0.05f);
			return dyG * LoadSideWeight(aRollGo) * Mathf.Clamp01(2f * (1f - yawTop / Mathf.Max(1f, revLaw)));
		}

		// F-22E 4.1.2: the time the rudders need from where they are to the stop on the other side of center (0: no rudder left)
		private float RudderCrossTime()
		{
			float sum = 0f;
			int n = 0;
			foreach (SurfaceEffector se in surfaces)
			{
				if (se.kind != SurfaceKind.Rudder || !se.active)
				{
					continue;
				}
				sum += (0.5f * (se.max - se.min) + Mathf.Abs(se.now - 0.5f * (se.max + se.min))) / Mathf.Max(1f, se.rate);
				n++;
			}
			return (n > 0) ? (sum / n) : 0f;
		}

		private void LateralLaw(float dt)
		{
			float f = alpha * (MathF.PI / 180f);
			float num = Mathf.Cos(f);
			float num2 = Mathf.Sin(f);
			// 4.3.0 (ReversedFlowNoseAxis): the velocity-vector roll's body roll share is |cos AoA| instead of cos AoA. Forward flight
			// (|AoA| < 90) is unchanged; flying backwards the rotation axis is forward flight's mirrored about the wings, so the stick and
			// the pedals turn the nose the way they point (roll and yaw both with the stick at positive AoA), continuous through 90 deg.
			// Tom, 12 Nov: "i want my roll and yaw direction not reversed when i'm flying backwards, like how the flanker handles ... roll/yaw
			// still blends to the same axis on aoa". 16:08 log, 180.0-181.0 s, MPO, 134 deg AoA, full right stick: the flipped roll sense
			// commanded p +35 / r -23 (nose left), and left stick from 181.3 s r +35 (nose right). numPhys keeps cos AoA for what is
			// physics, not the pilot's axis: the sideslip kinematics and the sideslip hold's stability-axis yaw (rS).
			float numPhys = num;
			bool noseAx = Plugin.RevFlowRollSense && Plugin.RevFlowNoseAxis;
			if (noseAx)
			{
				num = Mathf.Abs(num);
			}
			pFull = rollCtrlHalf / Mathf.Max(1f, 0f - Lp) * 57.29578f;
			pStockRef = StockRollRef();
			float trimR = ((RollTrimHeadroom && Plugin.AsymRollHeadroom) ? (Mathf.Sign(dTrimR) * Mathf.Min(Mathf.Max(0f, Mathf.Abs(dTrimR) - Mathf.Max(0f, Plugin.AsymDeadband)), 0.8f * aRoll)) : 0f);
			aRollPos = aRoll - Mathf.Max(0f, 0f - trimR);
			aRollNeg = aRoll - Mathf.Max(0f, trimR);
			float rollSide = sr + Mathf.Clamp01((Mathf.Abs(alpha) - Plugin.CoupleAoA) / 14f) * sy;
			if (Mathf.Abs(rollSide) < 0.02f)
			{
				rollSide = pTraj.x;
			}
			float aRollGo = ((rollSide >= 0f) ? aRollPos : aRollNeg);
			float aRollStop = ((rollSide >= 0f) ? aRollNeg : aRollPos);
			YawHealthStep();
			float num3 = Mathf.Min(Plugin.MaxRollRate, Mathf.Min(pStockRef, Plugin.RollAuthorityUse * pFull * (aRollGo / aRoll)));
			if (Plugin.RollStopFactor > 0.01f)
			{
				num3 = Mathf.Min(num3, Plugin.RollStopFactor * Mathf.Min(aRollGo, aRollStop));
			}
			float num4 = ((Mode == FcsMode.MPO) ? Plugin.MpoGPos : Plugin.GPos);
			float num5 = ((Mode == FcsMode.MPO) ? Plugin.MpoGNeg : Plugin.GNeg);
			float value = 1f;
			float t = Mathf.Clamp01((ias - 250f) / 150f);
			float num6 = Mathf.Lerp(6f, 3f, t);
			if (nz > num6)
			{
				value = Mathf.Lerp(1f, Mathf.Lerp(0.5f, 0.35f, t), (nz - num6) / Mathf.Max(0.5f, num4 - num6));
			}
			else if (nz < -1.5f)
			{
				value = Mathf.Lerp(1f, 0.6f, (-1.5f - nz) / Mathf.Max(0.5f, -1.5f - num5));
			}
			num3 *= Mathf.Clamp(value, 0.4f, 1f);
			num3 = (pMaxDbg = Mathf.Max(num3, 30f));
			float num7 = LowIasYawBoostAt(ias);
			float num8 = (rMaxDbg = Mathf.Lerp(Plugin.MaxYawRate, Mathf.Clamp(num7 * Plugin.YawStopFactor * aYaw, 20f * num7, Plugin.MaxYawRate), Mathf.Clamp01((Mathf.Abs(alpha) - 15f) / 10f)));
			// 1.8.45 braking floor: at high AoA the roll and yaw rate limits may not exceed what the stop-side authority,
			// at the IAS predicted from a rapid deceleration, brakes in the stop time of the reference manoeuvre (full
			// pedal + full lateral stick at 65 deg AoA, 280 km/h: RollStopFactor for roll, YawStopFactor x yaw boost there
			// for yaw). Replaces the fixed 30 deg/s roll / 20 x boost yaw floors there, which outran the authority below ~65 m/s.
			float gB = Mathf.Clamp01((Mathf.Abs(alpha) - LatBrakeA0) / Mathf.Max(1f, LatBrakeA1 - LatBrakeA0));
			latPredK = qPredK;
			if (LatBrakeFloor && gB > 0f)
			{
				// 1.9.3: yaw floor = the yaw braking at 270 km/h; from 210 to 150 km/h the allowed stop time grows smoothly
				// (smoothstep) by YawMarginLow (1.5 x: 50 % more yaw rate for the same authority, slower braking), flat below.
				// The roll stop time gets the same factor: at 65 deg AoA a velocity-vector turn needs body roll = cos(AoA) x its
				// rate, and a roll limit left at 1.0 s capped the turn (and so the yaw rate) at ~160 km/h.
				// The floor limits join the normal limits with a C1 soft minimum (no corner near 270 km/h).
				// Roll: the stop-side authority is the modelled asymmetric-load trim off the full authority, low-passed. The
				// measured trim (dTrimR) it used carried the turn's own dihedral / yaw-rate roll moment, which swings with
				// sideslip and yaw rate: in a max-AoA stick turn it moved 20 <-> 29 deg/s^2 at 1.4 Hz, the roll ceiling
				// 9 <-> 18 deg/s with it, the velocity-vector ceiling 20 <-> 42, and yaw rate 27 <-> 40 (a limit cycle).
				float iasKmh = ias * 3.6f;
				float sm = Mathf.Clamp01((YawMarginIasHi - iasKmh) / Mathf.Max(1f, YawMarginIasHi - YawMarginIasLo));
				sm = sm * sm * (3f - 2f * sm);
				float yawMargin = Mathf.Lerp(1f, Mathf.Max(1f, YawMarginLow), sm);
				yawMarginDbg = yawMargin;
				float tY = Plugin.YawStopFactor * LowIasYawBoostAt(BrakeRefIas) * yawMargin;
				float asymTrim = asymValid ? Mathf.Max(0f, Mathf.Abs(asymArNow) - Mathf.Max(0f, Plugin.AsymDeadband)) : 0f;
				float stopA = Mathf.Max(0.2f * aRoll, aRoll - Mathf.Min(asymTrim, 0.8f * aRoll));
				rollStopAF = (rollStopAF < 0f) ? stopA : (rollStopAF + (stopA - rollStopAF) * Mathf.Clamp01(dt / Mathf.Max(0.02f, RollStopTau)));
				float pB = Mathf.Max(LatBrakeMinRate, Mathf.Max(0.01f, Plugin.RollStopFactor) * yawMargin * rollStopAF * qPredK);
				float rB = Mathf.Max(LatBrakeMinRate, tY * aYaw * qPredK);
				num3 = Mathf.Lerp(num3, SoftMin(num3, pB, BrakeSoftMin * 0.5f * (num3 + pB)), gB);
				// 1.9.4: mid-IAS band (290-330 km/h) with less yaw stop time (x YawMidFactor), smoothstep ramps outside it
				float mLo = Mathf.Clamp01((iasKmh - (Plugin.YawMidLoKmh - Plugin.YawMidRampKmh)) / Mathf.Max(1f, Plugin.YawMidRampKmh));
				float mHi = Mathf.Clamp01(((Plugin.YawMidHiKmh + Plugin.YawMidRampKmh) - iasKmh) / Mathf.Max(1f, Plugin.YawMidRampKmh));
				mLo = mLo * mLo * (3f - 2f * mLo);
				mHi = mHi * mHi * (3f - 2f * mHi);
				float yawMid = Mathf.Lerp(1f, Plugin.YawMidFactor, Mathf.Min(mLo, mHi));
				yawMidDbg = yawMid;
				num8 = Mathf.Lerp(num8, yawMid * SoftMin(num8, rB, BrakeSoftMin * 0.5f * (num8 + rB)), gB);
				pMaxDbg = num3;
				rMaxDbg = num8;
			}
			// 2.0.12: rudder lost (fin shot off): the roll and yaw rate limits shrink with the yaw authority left. A body roll
			// at AoA turns AoA into sideslip at p sin(AoA) unless the rudders make the matching yaw rate; the clean limits
			// (250 deg/s) asked one rudder for twice what it has (10:29 log, 276 s: -265 deg/s, sideslip -29, departure).
			// 2.0.13: and with the yaw already partly spent holding an asymmetry (engine out: the live engine's yaw), by the
			// share of the yaw authority left over at this AoA
			float yawLeft = 1f;
			if (DamageLatLimit && asymValid && asymCyNow > 1f)
			{
				yawLeft = Mathf.Clamp01(1f - Mathf.Abs(asymAyNow) / asymCyNow);
				// 2.1.4: past the rudder fade the stabs are the yaw source as well as a roll source, and the deflection that
				// trims the load's roll is the same deflection the yaw needs: the roll trim also comes out of the yaw left
				// (in proportion to the rudder fade). Only the load beyond the deadbands counts, so a clean jet is unchanged.
				// 12 Oct 18:29 log: at the asymmetric cap (~50 deg AoA) the limits still allowed 25-33 deg/s of roll and
				// 20-30 deg/s of yaw rate; centring the stick, the stabs could not brake both against the load and the
				// sideslip ran to 23 deg (74 s, 209 s).
				if (AsymSharedLat && Plugin.AsymSharedLatOn && asymCrNow > 1f)
				{
					float dbR = Mathf.Max(0f, Plugin.AsymDeadband);
					float rollUse = Mathf.Max(0f, Mathf.Abs(asymArNow) - dbR) / asymCrNow;
					float yawUse = Mathf.Max(0f, Mathf.Abs(asymAyNow) - 0.25f * dbR) / asymCyNow;
					yawLeft = Mathf.Clamp01(1f - yawUse - (1f - rudderAuth) * rollUse);
				}
			}
			sharedLatDbg = yawLeft;
			// 2.12.0: the fin-health factor fades with the rudders (rudderAuth, 50 -> 60 deg AoA): past the fade the fins are not the
			// yaw axis, so losing them takes nothing away there - the stabs, flaperons and the airframe's own balance carry the
			// velocity-vector roll, and the measured capacity says so (18:32 log, both fins gone: capZ 38 at 50-65 deg against 39 with
			// fins; 29-33 against 125-147 at 15-40 deg). Un-faded, rMax x 0.35 capped the corkscrew at 24 deg/s of yaw = 28 deg/s of
			// velocity roll at 65 deg with the margin untouched (sideslip 2-6 deg) - Tom: "isn't that when the rudders can't really
			// do any yaw, so why is it capped". Below the fade the open-loop factor stays until the rate cap's reversal probe
			// carries the roll demand (18:08 log, 541 s: capZ 99 -> 5-23 once a no-fin reversal loaded the flaperons: 17.6 deg).
			// The asymmetric-load share (yawLeft) is already rudderAuth-aware and is not faded.
			float finH = Mathf.Lerp(1f, yawHealth, Mathf.Clamp01(rudderAuth));
			if (DamageLatLimit && Mathf.Min(finH, yawLeft) < 0.999f)
			{
				float hK = Mathf.Max(DamageLatFloor, Mathf.Min(finH, yawLeft));
				num3 = Mathf.Max(30f * hK, num3 * hK);
				num8 *= hK;
				pMaxDbg = num3;
				rMaxDbg = num8;
			}
			float num9 = Mathf.Clamp01((Mathf.Abs(alpha) - Plugin.CoupleAoA) / 14f);
			float num10 = Mathf.Min(num3 / Mathf.Max(Mathf.Abs(num), 0.05f), num8 / Mathf.Max(Mathf.Abs(num2), 0.05f));
			float num11raw = Mathf.Clamp(((revFlow && Plugin.RevFlowRollSense && !noseAx) ? (-1f) : 1f) * (sr + num9 * sy), -1f, 1f);
			// 2.16.0: a rate LIMIT bounds the rotation the stick may build; it may not cut a rotation the jet already has faster than
			// the yaw can shed it. With the stick on the rotation's side the limit is floored at the velocity roll the body yaw
			// implies (r / sin AoA: the coordinated roll for the yaw the jet has), which decays as the yaw brakes - the roll follows the
			// yaw down instead of braking away from it. 20:19 log (both fins gone), held push-over from 62 deg: as the AoA crossed the
			// rudder fade the fin-health factor took pSmax 61 -> 16, the trajectory braked at the tuned rate, p -28 -> -13 against a
			// yaw of -47 that needed -42 to stay coordinated, sideslip +20 deg/s, departure (and the bench twin, dmgsuite 'pushover
			// HELD', the same chain). The stick scales the target as always (easing off is the pilot's deceleration; a release is the
			// window's job); sin AoA floored at LatFloorSinMin so a low-AoA turn's yaw is not read as a rotation.
			latFloorDbg = 0f;
			pSStickRaw = Mathf.Abs(num11raw) * num10;   // 2.18.0: the stick's own target before the floor (the shed cap's credit)
			stickRelW = Mathf.Clamp01(1f - Mathf.Abs(num11raw) / Mathf.Max(0.02f, SweepNoReverseStick));   // 2.24.0: 1 = lateral stick released
			float finKL = Mathf.Clamp01(yawHealth / 0.5f);
			if (LatLimitYawFloor)
			{
				float sinF = ((num2 >= 0f) ? 1f : -1f) * Mathf.Max(Mathf.Abs(num2), LatFloorSinMin);
				float pSyaw = omega.z * 57.29578f / sinF;
				float gTermRC = (tas > 40f) ? (9.81f * Mathf.Sin(phi * (MathF.PI / 180f)) * Mathf.Cos(theta * (MathF.PI / 180f)) / tas * 57.29578f * Mathf.Clamp01((tas - 40f) / 20f)) : 0f;
				float pSyawRel = (omega.z * 57.29578f - gTermRC * Mathf.Cos(alpha * (MathF.PI / 180f))) / sinF;
				if (Mathf.Abs(num11raw) > 0.05f)
				{
					if (num11raw * pSyaw > 0f)
					{
						float fl = Mathf.Abs(pSyaw);
						if (fl > num10)
						{
							latFloorDbg = fl - num10;
							num10 = fl;
						}
					}
				}
				// 3.0.2 (ReleaseCoordGate): only where the floor on sin AoA does not bind (above ~15 deg AoA), on the yaw less the turn's own.
				// 5 Nov 20:33 log, fins gone, banked 17-37 deg at 4 deg AoA, stick centred: the turn's yaw rate (-2.4 deg/s) / sin 15
				// made a -9 deg/s roll target, the bank grew, the turn's yaw with it - a roll creep that fed itself (139.6-143 s).
				else if (ReleaseCoordinate && finKL < 1f && yawSatAlphaRateDn >= 0f && alphaDot * alpha < 0f && Mathf.Abs(pSyaw) > 5f && (!ReleaseCoordGate || (Mathf.Abs(num2) >= LatFloorSinMin && Mathf.Abs(pSyawRel) > 5f)))
				{
					// 2.18.0: stick released under a capped nose-down sweep with the fins gone: the roll stays coordinated with the yaw
					// the jet has (target = r / sin AoA, decaying as the yaw sheds) instead of going to zero while 20-30 deg/s of yaw is
					// still on (23:49 log, 122 s: roll target 9 -> 0 in 1.5 s, r 29 -> 22, sideslip -1 -> -53 deg). With the fins the
					// rudders shed the yaw in tenths and the stop stands as it is (finK 1: off).
					float fl = Mathf.Abs(pSyaw) * (1f - finKL);
					num10 = Mathf.Max(num10, fl);
					num11raw = ((pSyaw >= 0f) ? 1f : -1f) * Mathf.Min(1f, fl / Mathf.Max(1f, num10));
					latFloorDbg = fl;
				}
			}
			float num11 = num11raw * num10;
			pSAskStick = Mathf.Abs(num11);
			coupRollLim = 0f;
			if (Plugin.CouplingRollLimit > 0f && Ia[1, 1] > 1f && asymValid && tvc.M != null && tvc.N > 1)
			{
				float kIc = (Ia[2, 2] - Ia[0, 0]) / Ia[1, 1];
				float s2c = Mathf.Abs(Mathf.Sin(2f * f));
				{
					float gateC = Mathf.Max(pitchNeedUp, Mathf.Clamp01((Mathf.Sign(alpha) * alphaDot - 5f) / 15f)) * Mathf.Clamp01((Mathf.Abs(alpha) - CoupGateA0) / 10f);
					// 2.1.3: at the low-thrust AoA cap the gate stays on within LowCapCoupBand of the cap instead of waiting for the
					// AoA to rise: with the nozzles pinned and weak, the inertial pitch-up of the roll took the jet 2 deg past the cap,
					// pitch priority took the stabs back and the roll was cut and restored in a cycle.
					if (LowCapLatFix && Plugin.LowCapRollFix && LowThrustCapOn && alpha > 0f)
					{
						gateC = Mathf.Max(gateC, Mathf.Clamp01((alpha - (authCapEff - LowCapCoupBand)) / Mathf.Max(0.5f, LowCapCoupBand)));
					}
					gateC = Mathf.Max(gateC, (Plugin.AsymRollCoupling ? CoupHeldGate : 0f) * Mathf.Clamp01((Mathf.Abs(alpha) - 4f) / 4f) * Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / CoupHeldDySpan));
					// 2.1.10: at any AoA limit (not only the low-thrust cap) the gate stays on within CoupLimBand of the limit, and it
					// comes off over CoupGateRelease s instead of at once. It used to follow pitchNeed, which flips 0 <-> 1 as the pitch
					// error comes and goes: the roll limit switched off and on every ~0.6 s, the roll rate (and its inertial pitch-up)
					// jumped 60 <-> 110 deg/s, the nozzles went stop to stop and the pitch rate bounced (13 Oct logs, 45 deg limit,
					// 89-97 s: q 0..62 deg/s, AoA 39..52).
					if (CoupGateAtLimit && Plugin.CoupGateAtLimitOn)
					{
						float limU = Mathf.Min(Plugin.AoAPos, Mathf.Min(authCapEff, asymCapPosEff));
						float limD = Mathf.Max(Plugin.AoANeg, authCapNegEff);
						float nearLim = (alpha >= 0f) ? Mathf.Clamp01((alpha - (limU - CoupLimBand)) / Mathf.Max(0.5f, CoupLimBand)) : Mathf.Clamp01(((limD + CoupLimBand) - alpha) / Mathf.Max(0.5f, CoupLimBand));
						// only while the pilot holds the jet at the limit (stick toward it)
						nearLim *= Mathf.Clamp01(((alpha >= 0f) ? sp : (0f - sp)) / 0.5f);
						gateC = Mathf.Max(gateC, nearLim);
						// 2.2.2 (LoadedRollSteady, off by default): in a loaded roll (stick toward the AoA side, lateral held, AoA past
						// the gate start) the coupling roll limit stays on instead of following pitchNeed, and the roll governor judges
						// the nozzles' margin as it is, not projected 0.3 s ahead (their slew alone made the projection hit zero). 13 Oct
						// 11:08 log, full aft + full roll from 690 km/h: both switched every ~0.45 s, roll target 98 <-> 172 deg/s,
						// nozzles stop to stop, pitch rate 15..50, AoA 41..48 at the limit.
						float gLoaded = Plugin.LoadedRollSteadyOn ? 1f : CoupGateLoaded;
						if (gLoaded > 0f)
						{
							float loadedC = Mathf.Clamp01(((alpha >= 0f) ? sp : (0f - sp)) / 0.5f) * Mathf.Clamp01((Mathf.Abs(sr) - 0.05f) / 0.2f) * Mathf.Clamp01((Mathf.Abs(alpha) - CoupGateA0) / 10f);
							gateC = Mathf.Max(gateC, gLoaded * loadedC);
						}
						coupGateHeld = Mathf.Max(gateC, coupGateHeld - dt / Mathf.Max(0.05f, CoupGateRelease));
						gateC = coupGateHeld;
					}
					if (gateC > 0.001f && s2c > 0.05f && kIc > 0.01f)
					{
						float tvLoC = float.MaxValue;
						float tvHiC = float.MinValue;
						for (int tvK = 0; tvK < tvc.N; tvK++)
						{
							tvLoC = Mathf.Min(tvLoC, tvc.M[tvK].y);
							tvHiC = Mathf.Max(tvHiC, tvc.M[tvK].y);
						}
						float netC = ((alpha >= 0f) ? (0f - (AsymCap.Interp(asymAlphas, asymNetDn, asymAlphas.Length, alpha) + (tvc.active ? (tvLoC * IaInv[1, 1] * 57.29578f) : 0f))) : (AsymCap.Interp(asymAlphas, asymNetUp, asymAlphas.Length, alpha) + (tvc.active ? (tvHiC * IaInv[1, 1] * 57.29578f) : 0f)));
						float aAvC = Mathf.Max(CoupRollFloor, Plugin.CouplingRollLimit * netC) * (MathF.PI / 180f);
						float pLimC = Mathf.Sqrt(2f * aAvC / (kIc * s2c)) * 57.29578f;
						coupRollLim = pLimC;
						num10 = Mathf.Lerp(num10, Mathf.Min(num10, pLimC), gateC);
						num11 = Mathf.Clamp(num11, 0f - num10, num10);
					}
				}
			}
			if (AsymRollMax > 0f && (!AsymRollHardOnly || aRollGo < aRollStop))
			{
				float asymG = Mathf.Clamp01((asymUNow - AsymRollU0) / 0.3f);
				if (asymG > 0f)
				{
					num10 = Mathf.Min(num10, Mathf.Lerp(num10, AsymRollMax, asymG));
					num11 = Mathf.Clamp(num11, 0f - num10, num10);
				}
			}
			RollGovStep(dt, num10, rollSide);
			if (rollGov < 0.999f)
			{
				num10 = Mathf.Min(num10, Mathf.Max(num10 * rollGov, RollGovMinRate));
				num11 = Mathf.Clamp(num11, 0f - num10, num10);
			}
			pullTradeDbg = 0f;
			if (pullTradePs < 998f && Plugin.PullRollTrade > 0f)
			{
				// start from the roll in force so the limit acts at once, not after it has slewed down from 999
				if (pullTradePs > num10)
				{
					pullTradePs = num10;
				}
				// 2.2.26: with a fin lost the trade stays at most PullRollTradeFinLoss (bench, both fins gone, full aft + full
				// roll at 243 kt: the full trade let the AoA reach the 40 deg cap and the sideslip went 16 -> 23 deg)
				float trK = (yawHealth >= 0.99f) ? Plugin.PullRollTrade : Mathf.Min(Plugin.PullRollTrade, PullRollTradeFinLoss);
				float trLim = Mathf.Min(num10, Mathf.Max(PullTradeFloor, pullTradePs));
				pullTradeDbg = trLim;
				// 2.6.4: the pitch covers what the roll's share leaves (see pullTradeAsk)
				tradeAlphaRate = -1f;
				// 2.7.4: in measured mode the pitch's share is the measured AoA-rise cap below (capacity less the roll's build); this
				// second cap waited for the roll to slew down before letting the pull through (11:30 log, 112.7 / 167.8 s: qCmd 24 -> 12
				// -> 44 on a re-pull with the roll still at 60-190 deg/s - the hitch)
				// (2.7.4 tried it off in measured mode: 13-17 deg on pull + roll reversals on the bench, AoA overshoots; it stays)
				// 2.8.0: the pitch share is what the measured trade computed from the asked roll, not the limit's slew
				tradeAlphaRate = (pullTradeHoldDbg && tradeAlphaNew > 0f) ? tradeAlphaNew : -1f;
				num10 = Mathf.Min(num10, trLim);
				num11 = Mathf.Clamp(num11, 0f - num10, num10);
			}
			else
			{
				tradeAlphaRate = -1f;
			}
			// 2.5.0 reference governor on the commanded roll rate (replaces the 2.4.17 cap and the 2.4.18 rudder-load gate).
			// A velocity-vector roll at AoA carries a body yaw rate of pS sin(AoA); when the roll stops or reverses the wing kills
			// the body roll in a few tenths of a second while the yaw rate is braked more slowly (rudders slewing across their
			// travel, then their yaw authority), and the yaw left over turns into sideslip at r cos(AoA). Fitted on the 30 Oct
			// flight logs (200 stops and reversals, 3-40 deg AoA, 150-420 kt), the sideslip excursion of a stop or reversal is
			//   |pS| sin(AoA) cos(AoA) x (StopSlipTau0 + StopSlipTau1 x max(0, (L - StopSlipKnee) / (1 - StopSlipKnee)))
			// with L the rudders' load for the roll's yaw (their yaw moment toward the roll's yaw side over what they have on
			// that side, StopSlipHoldTau filter). It is linear in the roll rate and did not depend on IAS or on the rudders'
			// authority in the logs (the 2.4.17 bench model, quadratic in the yaw rate over the rudder authority, held low-IAS
			// rolls that stop cleanly in game to its 30 deg/s floor). Stops with the rudders at their stop before the release
			// (14:35 log 267.7 / 271.2 / 273.8 s, 10:28 log 61.8 / 139.3 / 277.1 s: 12-16 deg) are the knee term; stops from
			// rolls settled with rudder margin were benign at 124-152 deg/s (14:35 log 85.0 / 152.9 s: 1.1 deg).
			// The governor caps the roll-rate command (not the trajectory's acceleration or braking) to the rate whose
			// predicted excursion is the margin schedule: RollStopSideslip up to StopSlipAoALo deg AoA, RollStopSideslip50 at 50,
			// RollStopSideslip65 at 65, off past StopSlipAoAEnd; RollStopSlipGain scales the prediction (1 = the median fit).
			// Reference-governor dynamics: the cap comes down at StopSlipFallK x aB / sin(AoA) (and rises at StopSlipRiseK times that) - the
			// roll deceleration the rudders' braking yaw aB could coordinate - so a falling cap is a roll the yaw can follow, not
			// a stop of its own (2.4.18's gate, engaging over 0.3 s mid-roll, braked the roll with the rudders pinned and made the
			// sideslip it was meant to prevent: 23:27 log, 96-98 s). While the trajectory is still accelerating into the roll the
			// cap does not come down: the rudders pin in every fast entry and unpin as the yaw catches up.
			stopSlipABDbg = 0f;
			stopSlipMarginDbg = 0f;
			// 2.5.3: no AoA gate here (was |sin AoA| > 0.05): the AoA-sweep cap below must hold through zero AoA - with the gate
			// the cap let go for 0.15 s as a push + roll crossed zero and the pitch rate stepped from -24 to -60 deg/s there
			// (bench, 300 km/h). The margin cap itself is unbounded near zero AoA through sc's floor.
			if (!isAI && LatGovMode)
			{
				float aAbs = Mathf.Abs(alpha);
				float gS = 1f - Mathf.Clamp01((aAbs - StopSlipAoAEnd) / 5f);
				float sgnR = (Mathf.Abs(num11) > 2f) ? Mathf.Sign(num11) : ((Mathf.Abs(pTraj.x) > 2f) ? Mathf.Sign(pTraj.x) : 0f);
				// yaw moment sign that brakes the roll's yaw (the roll's yaw rate is pS sin(AoA))
				float yB = 0f - sgnR * Mathf.Sign(alpha);
				float nPos = 0f;
				float nNeg = 0f;
				foreach (SurfaceEffector seS in surfaces)
				{
					if (seS.kind != SurfaceKind.Rudder || !seS.active || seS.M == null || seS.N < 2)
					{
						continue;
					}
					float zHi = float.MinValue;
					float zLo = float.MaxValue;
					for (int iS = 0; iS < seS.N; iS++)
					{
						zHi = Mathf.Max(zHi, seS.M[iS].z);
						zLo = Mathf.Min(zLo, seS.M[iS].z);
					}
					nPos += Mathf.Max(0f, zHi);
					nNeg += Mathf.Max(0f, 0f - zLo);
				}
				float nB = (yB > 0f) ? nPos : ((yB < 0f) ? nNeg : Mathf.Min(nPos, nNeg));
				// the rudders' load for the roll's yaw: their yaw moment now, toward the roll's yaw side, over what they have there
				float zRudNow = 0f;
				foreach (SurfaceEffector seU in surfaces)
				{
					if (seU.kind == SurfaceKind.Rudder && seU.active && seU.M != null && seU.N >= 2)
					{
						zRudNow += seU.MAt(seU.now).z;
					}
				}
				float sY = 0f - yB;
				float nDrive = (sY > 0f) ? nPos : nNeg;
				float uR = (sY != 0f && nDrive > 1E-06f) ? Mathf.Clamp01(sY * zRudNow / nDrive) : 0f;
				stopSlipUDbg = uR;
				// 2.5.2: the rudders' usage whichever side they are on - after a reversal they are still pinned the old way, and
				// the yaw axis is just as saturated for the new roll until they have crossed
				float nAbs = (zRudNow >= 0f) ? nPos : nNeg;
				float uAbs = (nAbs > 1E-06f) ? Mathf.Clamp01(Mathf.Abs(zRudNow) / nAbs) : 0f;
				stopSlipAbsF += (uAbs - stopSlipAbsF) * (dt / (StopSlipHoldTau + dt));
				float aB = Mathf.Max(1f, StopSlipYawUse * nB * IaInv[2, 2] * 57.29578f);
				stopSlipABDbg = aB;
				if (gS > 0f)
				{
					// margin schedule
					float bLo = Plugin.RollStopSideslip;
					// 2.5.14: one margin at every AoA (RollStopSideslip); the 50 / 65 deg values are no longer read - a schedule by AoA
					// was the last one in the lateral law, and the fins' stall bound runs the other way and is not what carries the
					// lateral authority above 35 deg (14:05 analysis). The margin is a handling number; the yaw side is from the tables.
					float bM = bLo;
					if (Plugin.RollStopSlipProbe)
					{
						bM = Mathf.Max(1f, Mathf.Min(bM, SlipTolUse * slipTolF));
					}
					stopSlipMarginDbg = bM;
					float sc = Mathf.Max(0.02f, Mathf.Abs(num2 * num));
					// 2.21.0: the 2.5.3 AoA-rise cap ('roll first, then pitch': StopSlipPitchK x (capacity - the roll's own build x sin AoA)
					// / (pS cos AoA), gated by the rudders' yaw and the sideslip lead) is deleted. It spent the same yaw capacity the trade
					// had already divided (TradeCapShare, PullRollTrade), a second time and with the roll trajectory's acceleration taken
					// off it first, so a pull with the roll stick coming in 0.3 s later went 60 -> 24 -> 44 deg/s of pitch command where
					// the trade's own share read 26-38 (3 Nov 09:53 log, 39.9-40.5 s, 214 kt: rudders to the stop, sideslip 0.1-1.5 deg)
					// - the pitch hitch. The trade is the one accounting of the sweep's yaw demand on the sweep that grows |AoA|; the
					// roll's own build is a transient the sideslip margin pays for, not a steady demand (counted as one it is this cap
					// again: 141 deg/s of roll asked, 40 deg/s^2 of build at 12 deg AoA, 45 of capacity -> 10 deg/s of pitch).
					yawSatAlphaRate = -1f;
					yawSatAlphaRateDn = -1f;
					// 2.5.4: sized from the roll the jet has, not only the reference - when the window drops the reference the body roll
					// is still 70-120 deg/s for half a second and the sweep under it still needs the yaw (09:44 log, 324 / 328 s: the cap
					// let go and the push / pull ran at 50-60 deg/s of AoA rate over a 100 deg/s roll, 9-11 deg); and whenever a roll
					// is in progress, stick held or released (280 s: released mid-roll, cap off, 13.5 deg)
					float pSNow = Mathf.Abs(omega.x * num + omega.z * num2) * 57.29578f;
					float pSCap = Mathf.Max(Mathf.Abs(pTraj.x), pSNow);
					pushGovL = float.NaN;
					PushPlanStep(dt, num, num2, num11);
					if (Plugin.RollGovernorPitch && pSCap > 5f)
					{
						// 2.34.0 (PushGov, see the static): the sweep that grows |AoA| is the trade's (either AoA sign, held stick; a released
						// stick asks nothing of it); the sweep toward zero is the governor's push side.
						float capPullG = StopSlipPitchFree;
						if (tradeAlphaRate > 0f) capPullG = Mathf.Min(capPullG, tradeAlphaRate);
						tradeAlphaDbg = (tradeAlphaRate > 0f) ? tradeAlphaRate : 0f;
						float sG = Mathf.Abs(num2), cG = Mathf.Abs(num);
						float pSSignedG = omega.x * num + omega.z * num2;
						float sideG = (Mathf.Abs(pTraj.x) > 2f) ? Mathf.Sign(pTraj.x) : Mathf.Sign(pSSignedG);
						pushGovSide = sideG;
						// the yaw that coordinates the rotation is pS sin a: its sign is the side the yaw sheds FROM
						float rSideG = sideG * Mathf.Sign(num2);
						// 2.34.1 (PushGovNoStab): the yaw the push will have is the yaw WITHOUT the stabilators - the push itself takes them for pitch
						// (21:52 log, 149.0 / 189.1 s: stabPitchFrac 0 -> 0.9 by 53 deg, the full read said 30-37 deg/s^2 of shed, 16-20 was delivered
						// at 50-30 deg; the no-stab read said 14-28). With the fins gone the stabs ARE the yaw (2.22.0's blend): by the fin health.
						float capYFull = (rSideG > 0f) ? capEst.capZeN : capEst.capZeP;
						float capYNs = (rSideG > 0f) ? capEst.capZeNsN : capEst.capZeNsP;
						float capYRaw = PushGovNoStab ? Mathf.Lerp(capYFull, Mathf.Min(capYFull, capYNs), Mathf.Clamp01(yawHealth / 0.5f)) : capYFull;
						// the body roll pS cos a grows on the rotation's side
						// (the full-travel read: the discounted one follows the roll loop's own last command - braking, it read 30; growing, 94)
						float capXRaw = (sideG > 0f) ? capEst.capXPraw : capEst.capXNraw;
						float kG = dt / (Mathf.Max(0f, PushGovCapTau) + dt);
						if (!pushGovXInit) { pushGovCapXF = capXRaw; pushGovCapYF = capYRaw; pushGovXInit = true; }
						pushGovCapXF += (capXRaw - pushGovCapXF) * kG;
						pushGovCapYF += (capYRaw - pushGovCapYF) * kG;
						float capYG = Mathf.Max(0f, TradeCapShare * pushGovCapYF);
						float capXG = Mathf.Max(0f, TradeCapShare * pushGovCapXF);
						float aCmdG = Mathf.Max(0f, (0f - Mathf.Sign(alpha)) * (qCmd - omegaV));
						bool actG = aCmdG > 2f && !isAI;
						// the body roll when the sweep began (frozen while it runs)
						if (!actG) pushGovP0 = Mathf.Abs(omega.x) * 57.29578f;
						pushGovActive = actG;
						// how far the velocity roll may grow: held stick, the stick's own target (never faster than the pilot asks); released,
						// PushGovRollK x the body roll it had when the sweep began (Tom: "the roll stops, a little slower is fine" - never faster)
						float growRawG = Mathf.Max(5f, Mathf.Lerp(Mathf.Abs(num11), PushGovRollK * Mathf.Max(10f, pushGovP0) / Mathf.Max(0.2f, cG), stickRelW));
						if (!actG || pushGovGrowF <= 0f) pushGovGrowF = growRawG;
						pushGovGrowF += (growRawG - pushGovGrowF) * kG;   // (the same 0.2 s: the stick's target steps as the law's limits move)
						float growCapG = pushGovGrowF;
						float pCeilG = growCapG * cG;   // as a body roll
						pushGovCeil = growCapG; pushGovP0Dbg = pushGovP0;
						// the roll's growth is credited only while the velocity roll is under that cap
						float capXAvail = capXG * Mathf.Clamp01((growCapG - Mathf.Abs(pTraj.x)) / Mathf.Max(1f, 0.25f * growCapG));
						// (2) over the sweep: coordinated with the body roll at its ceiling the yaw is pCeil tan a, so while the yaw is r (less what
						// PushoverSideslip absorbs at the bottom) the AoA may come no lower than a_min = atan(r_ex / pCeil); a_min falls as the yaw
						// sheds (at capY: d a_min / dt = capY pCeil / (pCeil^2 + r_ex^2)). The pace closes on a_min over PushGovTau.
						float rOnG = Mathf.Max(0f, rSideG * omega.z * 57.29578f);
						float rLeftG = Mathf.Sqrt(2f * capYG * Mathf.Max(0f, PushGovSlipK * Plugin.PushoverSideslip));
						float rExG = Mathf.Max(0f, rOnG - rLeftG);
						float aMinG = Mathf.Atan(rExG / Mathf.Max(5f, pCeilG)) * 57.29578f;
						float gapG = Mathf.Max(0f, Mathf.Abs(alpha) - aMinG);
						float capTermG = ((PushGovDecel > 0f) ? Mathf.Sqrt(2f * PushGovDecel * gapG) : (gapG / Mathf.Max(0.05f, PushGovTau))) + 57.29578f * capYG * pCeilG / Mathf.Max(1f, pCeilG * pCeilG + rExG * rExG);
						pushGovAMin = aMinG;
						// (3) the shed time: the yaw over the allowance has to be gone when the AoA is at zero - |AoA-rate| <= |a| capY / r_ex. When
						// the yaw sheds as planned, a and r_ex fall together and this is ONE constant rate through the push (rising at the bottom as
						// r_ex reaches the allowance); r_ex weighted by (1 - cos a) against SweepShedCos (2.6.6: near zero a hold buys nothing)
						float wShG = Mathf.Clamp01((1f - cG) / Mathf.Max(0.05f, SweepShedCos));
						float rExW = rExG * wShG;
						float capShedG = (rExW < 0.5f) ? StopSlipPitchFree : Mathf.Abs(alpha) * capYG / rExW;
						// The roll's ACCELERATION is never what binds (100-500 deg/s^2 against the yaw's 20-50): what binds is how far it may grow, which
						// is (2). (1), the one-tick two-axis rate (capY cos a + capX sin a) / pS, was a bench switch (PushGovInst, off; deleted 2.44.1).
						float capPaceG = Mathf.Min(capShedG, capTermG);
						// 3.1.7 (PushGovPaceYaw): no yaw over the allowance, nothing to shed - the pace is open, faded in with r_ex (and no step at rExW 0.5)
						if (PushGovPaceYaw && yawHealth >= 0.99f) capPaceG = Mathf.Lerp(StopSlipPitchFree, Mathf.Min(StopSlipPitchFree, Mathf.Min(capTermG, (rExW < 1E-04f) ? StopSlipPitchFree : Mathf.Abs(alpha) * capYG / rExW)), Mathf.Clamp01(rExG / Mathf.Max(0.1f, PushGovPaceR)));
						float capDnG = Mathf.Max(SweepFloor, capPaceG);
						// 2.19.0 nose-hold without fins (unchanged)
						sweepHoldDbg = 0f;
						float finKcG = Mathf.Clamp01(yawHealth / 0.5f);
						if (SweepHoldFade && finKcG < 1f)
						{
							float rotNowG = Mathf.Abs(pSSignedG) * 57.29578f;
							float wRG = Mathf.Clamp01((rotNowG - SweepHoldRate) / Mathf.Max(1f, SweepHoldRateBlend));
							float wSG = Mathf.Clamp01((SweepHoldStabFrac - stabPitchFrac) / Mathf.Max(0.05f, SweepHoldStabFrac));
							float wHG = (1f - finKcG) * wRG * wSG;
							capDnG = Mathf.Lerp(capDnG, 0f, wHG);
							sweepHoldDbg = wHG;
						}
						// 3.1.7 (SweepZeroBlend): the grow rate the sweep will meet at zero AoA on the far side (positive: the pull's own cap, the trade; negative:
						// the term below for the side the yaw will be on there, at cos 0)
						float pSFadeG = (PushGovFadeGrow && PushGovFade && yawHealth >= 0.99f) ? Mathf.Clamp01((pSCap - PushGovFarLo) / Mathf.Max(0.1f, PushGovFarHi - PushGovFarLo)) : 1f;
						if (alpha >= 0f)
						{
							float capYFar = (0f - rSideG > 0f) ? capEst.capTrZeP : capEst.capTrZeN;
							yawSatFarRate = Mathf.Lerp(PushGovOpenRate, Mathf.Max(SweepFloor, 57.29578f * SweepYawK * Mathf.Max(0f, capYFar) / Mathf.Max(5f, pSCap)), pSFadeG);
						}
						else
						{
							yawSatFarRate = capPullG;
						}
						// the sweep that grows |AoA| from negative AoA (a push through zero under a held roll): the trade, and the yaw the rotation
						// needs on the far side may build no faster than its capacity there allows, SweepYawK x capY(grow) cos a / pS (the 2.1.8
						// rule for that sweep, unchanged in kind; the roll's share is the trade's)
						if (alpha < 0f)
						{
							float capYGrow = (rSideG > 0f) ? capEst.capTrZeP : capEst.capTrZeN;
							float capGrowNeg = Mathf.Max(SweepFloor, 57.29578f * SweepYawK * Mathf.Max(0f, capYGrow) * cG / Mathf.Max(5f, pSCap));
							// 3.1.7 (PushGovFadeGrow): in with the velocity roll, as the toward-zero side (not whole at 5 deg/s)
							if (PushGovFadeGrow && PushGovFade && yawHealth >= 0.99f) capGrowNeg = Mathf.Lerp(PushGovOpenRate, capGrowNeg, pSFadeG);
							capPullG = Mathf.Min(capPullG, capGrowNeg);
						}
						// 3.1.6: the toward-zero cap fades in with the velocity roll (no on/off at the gate)
						pushGovFadeW = (PushGovFade && yawHealth >= 0.99f) ? Mathf.Clamp01((pSCap - PushGovFadeLo) / Mathf.Max(0.1f, PushGovFadeHi - PushGovFadeLo)) : 1f;   // (fins damaged: the 2.19.0 nose-hold paces the push, as for the plan)
						yawSatAlphaRate = capPullG;
						yawSatAlphaSign = (alpha >= 0f) ? 1f : -1f;
						yawSatAlphaRateDn = Mathf.Min(StopSlipPitchFree, (PushPlan && (pushPlanOn || pushPlanDone)) ? StopSlipPitchFree : capDnG);
						// lateral: with the commanded AoA rate toward zero (this tick's pitch command less the flight path's rotation) the
						// velocity roll may come down no faster than L, and must grow when L > 0 (applied to the roll trajectory below)
						pushGovA = aCmdG;
						if (actG)
						{
							pushGovL = (Mathf.Abs(pTraj.x) * cG * aCmdG * (MathF.PI / 180f) - Mathf.Max(0f, pushGovCapYF)) / Mathf.Max(0.05f, sG);
							// the roll side: the body roll pS cos a grows by pS sin a x AoA-rate at constant pS - the velocity roll may grow by no
							// more than (capX available - pS sin a AoA-rate) / cos a (negative at the ceiling: it must come down)
							pushGovU = Mathf.Lerp(1E+06f, (capXAvail - Mathf.Abs(pTraj.x) * sG * aCmdG * (MathF.PI / 180f)) / Mathf.Max(0.2f, cG), stickRelW);
						}
						capSweepDbg = Mathf.Min(StopSlipPitchFree, capDnG);
						capShedDbg = float.IsNaN(pushGovL) ? 0f : Mathf.Clamp(pushGovL, -999f, 999f);
						aYawShedDbg = capYG;
						sweepDelF = capXAvail;   // (telemetry 'capSm' under PushGov: the roll growth credited)
					}
					else
					{
						tradeAlphaDbg = 0f;
						pushGovFadeW = 0f;
						sweepDelF = 0f;
						capSweepDbg = 0f;
						capShedDbg = 0f;
						aYawShedDbg = 0f;
						pushGovXInit = false;
						pushGovActive = false;
					}
					num11 = Mathf.Clamp(num11, 0f - num10, num10);
				}
			}
			pSmaxDbg = num10;
			float num12 = (0f - (1f - num9)) * sy * Plugin.PedalBeta;
			betaPedalCmd = num12;
			// 2.1.0 sideslip-rate lead: the guard also looks at where the sideslip is heading, from the measured body rates
			// (kinematic sideslip rate p sin(AoA) - r cos(AoA) plus gravity's g cos(theta) sin(phi) / V). A stability-axis roll
			// needs a yaw rate of p sin(AoA); when the yaw axis cannot build it (low IAS, an AoA sweep under a roll, fins near
			// stall, the roll's own adverse yaw) the sideslip rate shows it at once, while the sideslip itself builds a second
			// later (10 Oct log: -7..-10 deg/s of sideslip rate at 2..5 deg of sideslip, departing to 30..50 deg).
			float gTermB = ((tas > 40f) ? (9.81f * Mathf.Sin(phi * (MathF.PI / 180f)) * Mathf.Cos(theta * (MathF.PI / 180f)) / tas * 57.29578f * Mathf.Clamp01((tas - 40f) / 20f)) : 0f);
			float bdk = (omega.x * num2 - omega.z * numPhys) * 57.29578f + gTermB;
			bdk = bdkF.Step(bdk, SlipRateTau, dt);
			slipRateDbg = bdk;
			float bErrNow = beta - num12;
			float bErrLead = bErrNow + bdk * SlipLeadT;
			// 2.11.0: the 2.0.15 sideslip guard (SideslipGuardStart: the stick's roll scaled down past 4 deg of sideslip error) and the
			// yaw-saturation guard (YawSaturationGuard x yawSat) are deleted. 2 Nov, 2.9.x-2.10.0 logs: the sideslip term read 1.0 in
			// 93-96 % of roll-asked ticks (Tom flew it at 40 = off from 16:37 with the 22-34 deg AoA rolls unchanged: |pS| 70 -> 71,
			// sideslip p90 5.7 -> 6.4 deg), and the yaw-saturation term only ever cut (x 0.66-0.78) where the pace window was already
			// holding the target (paceHold > 2 in 67-100 % of those ticks: reversals and stops with the roll near zero). The window is
			// the saturation guard; the asymmetric-load sideslip lead (slipGov) below still scales x.
			float x = 1f;
			// the asymmetric-load sideslip lead: backs the
			// velocity-vector roll off only while the predicted sideslip error is large AND still growing, so a sideslip that is
			// already recovering never holds the roll back
			slipGovDbg = 1f;
			// 2.1.1: neither the sideslip lead nor the yaw anchor acts in a roll reversal (pilot target against the running
			// roll): the yaw rate the old roll left behind sat on the wrong side of the anchor window and the reversal's own
			// transient sideslip read as a departure, so the new roll was held near zero for ~0.7 s and then released at once
			// (10 Oct 22:50 log, 196.9 / 293.9 / 748.8 s). Reversals use the 2.0.15 braking / reversal limits again. (No AoA
			// fade: in a push + roll the AoA sweeps through any fade band exactly while the roll builds.)
			// latched from the reversal until the new roll is half-way to its target (as RevCarry does), so the sideslip the
			// reversal leaves behind does not hold the new roll back right after the zero crossing
			float tgtPre = num11;
			if (tgtPre * pTraj.x < 0f && Mathf.Abs(pTraj.x) > 2f && Mathf.Abs(tgtPre) > 5f)
			{
				latRevSign = Mathf.Sign(tgtPre);
			}
			else if (latRevSign != 0f && (tgtPre * latRevSign < 5f || pTraj.x * latRevSign >= 0.5f * Mathf.Abs(tgtPre)))
			{
				latRevSign = 0f;
			}
			bool latRev = latRevSign != 0f;
			latRevDbg = latRev;
			// 2.1.2: negative AoA only. Both limiters were for the push + lateral departure; at positive AoA the lateral law
			// is 2.0.15's again (2.1.0 / 2.1.1 changed the positive-AoA roll feel). Full at AoA <= NegGateA0 - NegGateSpan,
			// off at AoA >= NegGateA0.
			// 2.6.0: the 2.1.0-2.1.2 push + roll limiters (sideslip-rate lead, yaw anchor) act only for an asymmetric load past the
			// rudder fade (the damage case, below); the clean jet's push + roll is the governor's
			float negGate = 0f;
			// 2.1.4: at positive AoA with an asymmetric load past the rudder fade the same two limiters act: the flaperons are
			// spent trimming the load's roll, the stabs are the only yaw source and their yaw carries roll that nothing can
			// cancel, so the roll outruns the yaw it needs (12 Oct 18:29 log: sideslip 10-24 deg at ~50 deg AoA). Clean jets
			// (lateral CG offset under CoupHeldDy0) keep the 2.0.15 law.
			if (AsymSharedLat && Plugin.AsymSharedLatOn && alpha > 0f && asymValid)
			{
				float asymG = Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / Mathf.Max(0.005f, CoupHeldDySpan)) * (1f - rudderAuth);
				negGate = Mathf.Max(negGate, asymG);
			}
			negGateDbg = negGate;
			// 2.4.13: the push + roll limiters (sideslip lead, yaw anchor) also act gear-down in the air - 2.1.0 left them off with
			// the gear down, and a full roll then full push at idle, gear down, 165 kt went to -34 deg AoA and -55 deg sideslip
			// (27 Oct 12:12 log, 136-138 s). On the runway (any wheel touching) the gear-down law is unchanged.
			if (SlipLeadT > 0f && !isAI && !latRev && negGate > 0f && Mode != FcsMode.Ground && (Mode != FcsMode.GearDownRate || (Plugin.GearDownSlipProtect && !gsAnyContact)))
			{
				float growing = (bdk * bErrNow > 0f || Mathf.Abs(bErrNow) < 1f) ? 1f : 0f;
				float sg = Mathf.Clamp01(1f - (Mathf.Abs(bErrLead) - SlipGovStart) / Mathf.Max(1f, SlipGovSpan));
				sg = Mathf.Lerp(1f, sg, growing * negGate);
				slipGovDbg = sg;
				x = Mathf.Min(x, sg);
			}
			// the fast cut only when the sideslip lead is what cuts (the 2.0.15 guard terms keep their 0.25 s)
			x = guardF.Step(x, (guardF.init && x < guardF.y && slipGovDbg < 0.999f) ? GuardCutTau : 0.25f, dt);
			num11 *= x;
			// 2.1.0 yaw-anchored roll: the velocity-vector roll needs a body yaw rate of pS sin(AoA). It may only ask for a yaw
			// rate the yaw axis can reach from the yaw rate it has now within YawAnchorT (at YawAnchorK of the modelled yaw
			// authority). In a coordinated roll the yaw rate already matches and this never binds; when the AoA sweeps under a
			// held roll (full push + full lateral) or the yaw axis stalls, the roll shrinks with what the yaw can follow instead
			// of turning the AoA into sideslip.
			yawAnchorDbg = 999f;
			if (YawAnchorK > 0f && !isAI && !latRev && negGate > 0f && Mode != FcsMode.Ground && (Mode != FcsMode.GearDownRate || (Plugin.GearDownSlipProtect && !gsAnyContact)) && Mathf.Abs(num2) > 0.05f)
			{
				// 2.2.10: the yaw rate the turn itself needs (gravity's g cos(theta) sin(phi) / V, the same term the
				// sideslip-rate lead uses) is taken out first. Without it, banked past ~60 deg at low IAS the turn's own yaw
				// rate (+4.5 deg/s at 55 m/s) looked like yaw going the wrong way for a right roll at -5 deg AoA, and the
				// roll was held at the 10 deg/s floor for 5 s with full stick (19 Oct 10:00 log, 205.9-210.7 s).
				// Only near zero AoA (full above -10, gone below -20 deg), where the turn's yaw is large next to pS sin(AoA), and
				// only with the AoA steady (gone above 15 deg/s of AoA rate): in pushes with roll it let the sideslip grow
				// (bench, -50 deg at 200 km/h: 3.2 -> 7.5 deg).
				float rNow = omega.z * 57.29578f - (YawAnchorGrav ? (gTermB / Mathf.Max(0.3f, numPhys) * Mathf.Clamp01((alpha + 20f) / 10f) * Mathf.Clamp01(1f - (Mathf.Abs(alphaDot) - 5f) / 10f)) : 0f);
				float reach = YawAnchorK * aYaw * YawAnchorT;
				float lo = (rNow - reach) / num2;
				float hi = (rNow + reach) / num2;
				if (lo > hi)
				{
					float tmp = lo;
					lo = hi;
					hi = tmp;
				}
				// keep zero inside the window (stopping the roll is always allowed) and never below the floor rate
				lo = Mathf.Min(lo, 0f - YawAnchorFloor);
				hi = Mathf.Max(hi, YawAnchorFloor);
				yawAnchorDbg = (num11 >= 0f) ? hi : lo;
				num11 = Mathf.Lerp(num11, Mathf.Clamp(num11, lo, hi), negGate);
			}
			// 2.5.3 yaw-reach (pace) governor (RollGovernorReplaces >= 2): one rule for the pace of every lateral move - the
			// pair's body-yaw demand, pS sin(AoA) plus the sideslip correction's rS cos(AoA), may not lead the measured yaw
			// rate by more than the yaw axis can close within PaceHorizon. The reach is what the jet's yaw is doing now
			// (filtered yaw acceleration x horizon, so a yaw that is already braking opens the window ahead of itself) plus what
			// the rudders can add within the horizon from where they are (their moment tables, slewed from the present
			// deflection, at PaceRudderUse), plus PaceFloor for the loop's normal tracking error. The roll-rate target is held
			// inside that window and the trajectory runs toward it at its tuned acceleration / braking, so where the yaw keeps
			// up nothing changes and where it does not the roll reference moves at the yaw's own pace. In a coordinated roll
			// the yaw already matches and the window never binds; in a stop or reversal the roll reference comes down as the
			// yaw actually brakes instead of at the wing's rate (31 Oct 01:40 log, level 3: 720 deg/s^2 brakes at 22 deg AoA
			// asked the yaw for 270 deg/s^2 against 85-100 from pinned rudders - 10.5 / 14.2 deg at 195.4 / 198.8 s); with
			// the stick released at 65 deg AoA it does CoordinatedStop's job (214 s: a 62 deg/s rotation told to stop in
			// 0.3 s, 15.5 deg for 5 s); at negative AoA it is the 2.1.0 yaw anchor with the reach from the tables and the
			// measured yaw instead of a constant. It never asks for a roll against the one in progress: when the yaw the jet
			// has only fits a roll the other way (the AoA swept through zero under a roll), the target is zero - stop, not
			// reverse. Replaces the trajectory's yaw-coordination caps (RollEntryCoord, BrakeYawCap, RevYawCap, CoordJerk,
			// CoordinatedStop) at level 3 and the push + roll limiters at level 2.
			paceLeadDbg = 0f;
			paceHoldDbg = 0f;
			rateBlendEffDbg = 0f;
			paceAccelCap = -1f;
			paceTgtRaw = num11;
			paceGateZDbg = 0f;
			paceGateXDbg = 0f;
			// 2.35.3: with the lateral stick released in a planned push-over the plan's bleed owns the velocity roll (target 0 at the bleed
			// rate, set below); the stop window's hold and pacing were computed and then overwritten every tick, so they stand down
			bool pushBleed = PushPlan && pushPlanOn && stickRelW > 0.5f && Mathf.Abs(pTraj.x) > 0.5f;
			bool pushHeld = PushPlanHeld && PushPlan && pushPlanOn && !pushBleed && Mathf.Abs(pTraj.x) > 0.5f;
			bool winDown = pushBleed && PushPlanWinOff;
			if (winDown)
			{
				paceStopNF = 0f;   // (the no-fin stop; inert here: the plan needs both fins)
			}
			if (!winDown && !isAI && LatGovMode && tas > 40f && Mathf.Max(Mathf.Abs(num2), Mathf.Abs(Mathf.Sin((alpha + alphaDot * PaceHorizon) * (MathF.PI / 180f)))) > PaceSinMin)
			{
				float rM = omega.z * 57.29578f;
				float rDotRaw = paceInit ? ((omega.z - paceOmegaZPrev) / Mathf.Max(dt, 1E-04f) * 57.29578f) : 0f;
				float rDot = paceRDotF.Step(rDotRaw, PaceAccelTau, dt);
				paceRDotLast = rDot;
				float gPos = 0f;
				float gNeg = 0f;
				float iz = IaInv[2, 2] * 57.29578f;
				foreach (Effector seP in effs)
				{
					if (!seP.active || seP.M == null || seP.N < 2 || !(seP is SurfaceEffector seK) || seK.kind != SurfaceKind.Rudder)
					{
						continue;
					}
					float zNow = seP.MAt(seP.now).z;
					float zAtMax = seP.MAt(seP.max).z;
					float zAtMin = seP.MAt(seP.min).z;
					float rate = Mathf.Max(1f, seP.rate);
					for (int dirP = -1; dirP <= 1; dirP += 2)
					{
						float tgt = ((zAtMax >= zAtMin) == (dirP > 0)) ? seP.max : seP.min;
						float dd = Mathf.Abs(tgt - seP.now);
						float ts = Mathf.Min(PaceHorizon, dd / rate);
						float dh = seP.now + Mathf.Sign(tgt - seP.now) * rate * ts;
						float aS = Mathf.Max(0f, dirP * (seP.MAt(dh).z - zNow)) * iz;
						// the yaw acceleration ramps with the slew to ts, then holds to the horizon
						float g = 0.5f * aS * ts + aS * (PaceHorizon - ts);
						if (dirP > 0)
						{
							gPos += g;
						}
						else
						{
							gNeg += g;
						}
					}
				}
				float reachPos = Mathf.Max(0f, rDot * PaceHorizon + PaceRudderUse * gPos) + PaceFloor;
				float reachNeg = Mathf.Max(0f, 0f - rDot * PaceHorizon + PaceRudderUse * gNeg) + PaceFloor;
				// 2.6.11 (PaceMarginReach): the sideslip margin still unused is reach too. A yaw-rate lead L that the yaw closes at a
				// builds L^2 cos(AoA) / (2 a) of sideslip, so the lead whose sideslip stays inside margin - |beta| is
				// sqrt(2 a (margin - |beta|) / cos AoA), a = PaceYawUse of what the rudders can add toward that side (gPos / gNeg
				// over the horizon, as a rate) - the same yaw the window already credits. Measured sideslip, so the window holds
				// only as the margin is actually being used (02 Nov 00:54 log, 340 kt corkscrew entry: roll held 70 -> 17 deg/s at
				// 45-55 deg AoA with the sideslip at 0.8-2.2 of a 4 deg margin).
				paceMarginLeadDbg = 0f;
				if (PaceMarginReach)
				{
					float mLeft = Mathf.Max(0f, Mathf.Max(1f, stopSlipMarginDbg) - Mathf.Abs(beta - betaPedalCmd));
					float cAm = Mathf.Max(0.2f, Mathf.Abs(num));
					float aP = PaceYawUse * Mathf.Max(1f, gPos / Mathf.Max(0.02f, PaceHorizon));
					float aN = PaceYawUse * Mathf.Max(1f, gNeg / Mathf.Max(0.02f, PaceHorizon));
					float lP = PaceMarginK * Mathf.Sqrt(2f * aP * mLeft / cAm);
					float lN = PaceMarginK * Mathf.Sqrt(2f * aN * mLeft / cAm);
					paceMarginLeadDbg = Mathf.Max(lP, lN);
					reachPos += lP;
					reachNeg += lN;
				}
				// 2.7.0 (PaceMeasured): the reach from what the axes are doing and what the allocator has already committed
				float pMm = omega.x * 57.29578f;
				float pDotRawM = paceInit ? ((omega.x - paceOmegaXPrev) / Mathf.Max(dt, 1E-04f) * 57.29578f) : 0f;
				float pDotM = pacePDotF.Step(pDotRawM, PaceAccelTau, dt);
				float dzH = 0f;
				float dzAll = 0f;
				float dxH = 0f;
				float dxAll = 0f;
				float gateZ = 0f;
				float gateX = 0f;
				float rHm = rM;
				float pHm = pMm;
				float ix = IaInv[0, 0] * 57.29578f;
				float hdZP = 0f;
				float hdZN = 0f;
				float hdXP = 0f;
				float hdXN = 0f;
				paceNEffDbg = 0f;
				paceDdDbg = 0f;
				foreach (Effector seM in effs)
				{
					if (seM == null || !seM.active || seM.M == null || seM.N < 2)
					{
						continue;
					}
					// the increment in flight: from where the effector is to where the allocator sent it, and how much of
					// that its servo covers within the horizon
					float dd = seM.cmd - seM.now;
					paceNEffDbg += 1f;
					paceDdDbg += Mathf.Abs(dd);
					float rateM = Mathf.Max(1f, seM.rate);
					float ts = Mathf.Min(PaceHorizon, Mathf.Abs(dd) / rateM);
					float dh = seM.now + Mathf.Sign(dd) * rateM * ts;
					Vector3 mNow = seM.MAt(seM.now);
					Vector3 mH = seM.MAt(dh);
					Vector3 mT = seM.MAt(seM.cmd);
					dzH += (mH.z - mNow.z) * iz;
					dzAll += (mT.z - mNow.z) * iz;
					dxH += (mH.x - mNow.x) * ix;
					dxAll += (mT.x - mNow.x) * ix;
					// 2.7.1: yaw and roll still available toward each side from the commanded deflections (the open capacity)
					Vector3 mMax = seM.MAt(seM.max);
					Vector3 mMin = seM.MAt(seM.min);
					hdZP += Mathf.Max(0f, Mathf.Max(mMax.z, mMin.z) - mT.z) * iz;
					hdZN += Mathf.Max(0f, mT.z - Mathf.Min(mMax.z, mMin.z)) * iz;
					hdXP += Mathf.Max(0f, Mathf.Max(mMax.x, mMin.x) - mT.x) * ix;
					hdXN += Mathf.Max(0f, mT.x - Mathf.Min(mMax.x, mMin.x)) * ix;
				}
				// the axis's demonstrated acceleration (a decaying peak), credited where nothing is committed yet
				aYawSeen = Mathf.Max(Mathf.Abs(rDot), Mathf.Max(PaceFloor / PaceHorizon, aYawSeen - aYawSeen * dt / PaceSeenTau));
				aRollSeen = Mathf.Max(Mathf.Abs(pDotM), Mathf.Max(PaceFloor / PaceHorizon, aRollSeen - aRollSeen * dt / PaceSeenTau));
				rHm = rM + (rDot + 0.5f * dzH) * PaceHorizon;
				pHm = pMm + (pDotM + 0.5f * dxH) * PaceHorizon;
				// gravity's share of the sideslip: the jet falling sideways under the sideslip loop's own correction (its time
				// constant 1 / KBeta). Tom: the one acceptable failure - excluded from the margin, never paid for with roll.
				float gBm = ((tas > 40f) ? (9.81f * Mathf.Sin(phi * (MathF.PI / 180f)) * Mathf.Cos(theta * (MathF.PI / 180f)) / tas * 57.29578f * Mathf.Clamp01((tas - 40f) / 20f)) : 0f);
				paceBetaG += (gBm - Mathf.Max(0.2f, Plugin.KBeta) * paceBetaG) * dt;
				paceBetaG = Mathf.Clamp(paceBetaG, -20f, 20f);
				// gravity is charged only for sideslip that exists and has its sign (the loop may be correcting it already)
				float bErrM = beta - betaPedalCmd;
				float bGEff = (bErrM * paceBetaG > 0f) ? Mathf.Sign(bErrM) * Mathf.Min(Mathf.Abs(bErrM), Mathf.Abs(paceBetaG)) : 0f;
				float betaOwn = bErrM - bGEff;
				float mLeftM = Mathf.Max(0f, Mathf.Max(1f, stopSlipMarginDbg) - Mathf.Abs(betaOwn));
				paceMLeftDbg = mLeftM;
				float cAmM = Mathf.Max(0.2f, Mathf.Abs(num));
				// the yaw's capacity toward each side: no more than the allocator is delivering of the loop's demand (demand less
				// its unmet report), no more than the axis is measurably doing plus the committed increment arriving within the
				// horizon; at least a share of what it has shown it can do (so a move can start)
				float desZ = omegaDotDes.z * 57.29578f;
				float capZP = Mathf.Min(Mathf.Max(0f, desZ - Mathf.Max(0f, allocUnmet.z)), Mathf.Max(0f, rDot + dzH));
				float capZN = Mathf.Min(Mathf.Max(0f, 0f - desZ - Mathf.Max(0f, 0f - allocUnmet.z)), Mathf.Max(0f, 0f - (rDot + dzH)));
				capZP = Mathf.Max(capZP, PaceSeenUse * aYawSeen);
				capZN = Mathf.Max(capZN, PaceSeenUse * aYawSeen);
				// 2.7.1: while the allocator reports nothing unmet toward a side, the capacity there is the authority still available
				// (so the loop is let ask for it); it falls to what is being delivered as the allocator reports it cannot. 2.7.0 used
				// only the delivered / demonstrated figure, and the loop's demand is proportional to the lead the window allows, so
				// the yaw was never asked for more than it had shown: a reversal at 413 kt sat at zero roll for 0.3 s with the rudders
				// at 14 of 35 deg and unmet 0, and one at 369 kt rolled back toward the old side (Tom, 09:59 log).
				// 2.7.2: saturation relative to the axis's authority (an unmet of 15 deg/s^2 is nothing at 400 kt and most of the yaw at
				// 200 km/h, where aYaw is 23)
				float satScZ = Mathf.Max(PaceSatDead, PaceSatFrac * aYaw);
				float satZP = Mathf.Clamp01(Mathf.Max(0f, allocUnmet.z) / satScZ);
				float satZN = Mathf.Clamp01(Mathf.Max(0f, 0f - allocUnmet.z) / satScZ);
				// the open capacity: what the axis is doing plus the committed increment arriving within the horizon, plus the
				// authority still available beyond the commanded deflections (zero with the effectors sent to their stops: then the
				// move is paced by what arrives, the slew case of the 154 s / 339 kt reversal)
				float openZP = Mathf.Max(0f, rDot + dzH) + PaceYawUse * hdZP;
				float openZN = Mathf.Max(0f, 0f - (rDot + dzH)) + PaceYawUse * hdZN;
				capZP = Mathf.Lerp(Mathf.Max(capZP, openZP), capZP, satZP);
				capZN = Mathf.Lerp(Mathf.Max(capZN, openZN), capZN, satZN);
				// 2.7.4: never above the allocator's own authority less its saturation derate (the 2.5.12 ceiling: past the rudder
				// fade the tables overstate the stabs' yaw 3-4x, and the committed increment is read from those tables). 11:30 log,
				// 116 s: a push-over from 64 deg AoA read 143-162 of capacity with 26-129 unmet and made 22 deg of sideslip.
				float capCeilZ = Mathf.Max(5f, aYaw) * (1f - Mathf.Lerp(PaceSatDerateLo, PaceSatDerateHi, 1f - rudderAuth) * Mathf.Clamp01(yawSat));
				capZP = Mathf.Min(capZP, capCeilZ);
				capZN = Mathf.Min(capZN, capCeilZ);
				// the lead that capacity closes inside the margin left (L^2 cos AoA / 2a = margin), plus the loop's ordinary
				// tracking error, which shrinks with the margin so that at the margin the roll waits for the yaw
				float flM = PaceFloor * Mathf.Clamp01(mLeftM / 2f);
				float lPm = PaceMarginK * Mathf.Sqrt(2f * capZP * mLeftM / cAmM);
				float lNm = PaceMarginK * Mathf.Sqrt(2f * capZN * mLeftM / cAmM);
				// never less than what the capacity closes within the horizon: the loop keeps driving the axis at its capacity
				// with the margin spent, instead of pinning the target to the axis (which asks it for nothing and stalls the move)
				// 2.7.1: the floor is what the yaw loop needs as error to ask for that capacity (its gain KYaw; a held target has no
				// feed-forward), so at the margin the lead x gain = the delivered capacity - the loop keeps asking exactly what the
				// axis gives. 2.7.0's capacity x 0.15 s asked for a fraction of it.
				// 2.7.1: the lead is the margin's (plus PaceLeadT of the capacity, the sideslip rate accepted while the roll waits); the
				// yaw is driven by the loop's feed-forward instead - the target moves toward the demand at the capacity (below), so the
				// loop asks exactly what the axis gives with no lead at all
				// 2.17.0: the lead floor is the error the yaw loop needs to ask for the whole capacity, capacity / KYaw (the 2.7.1 comment
				// above; the code had 0.1 s of capacity against a 2.5 /s loop, a quarter of it, and 2.7.2 switched off the feed-forward that
				// covered the rest). With the margin spent and the yaw weak (no fins, 65 deg) the target was pinned 1 deg/s ahead of the
				// rotation and the yaw was asked for ~5 deg/s^2 of its 20 (23:30 log, 110 s reversal: 2.5 s at +19 against a -30 demand).
				float leadT = (PaceLeadT < 0f) ? (1f / Mathf.Max(0.5f, Plugin.KYaw)) : PaceLeadT;
				reachPos = Mathf.Max(lPm, capZP * leadT) + flM;
				reachNeg = Mathf.Max(lNm, capZN * leadT) + flM;
				paceMarginLeadDbg = Mathf.Max(lPm, lNm);
				paceCapZPDbg = capZP;
				paceCapZNDbg = capZN;
				// the roll axis the same way (its shortfall is sideslip at sin AoA)
				float desX = omegaDotDes.x * 57.29578f;
				float capXP = Mathf.Min(Mathf.Max(0f, desX - Mathf.Max(0f, allocUnmet.x)), Mathf.Max(0f, pDotM + dxH));
				float capXN = Mathf.Min(Mathf.Max(0f, 0f - desX - Mathf.Max(0f, 0f - allocUnmet.x)), Mathf.Max(0f, 0f - (pDotM + dxH)));
				float satScX = Mathf.Max(PaceSatDead, PaceSatFrac * aRoll);
				float satXP = Mathf.Clamp01(Mathf.Max(0f, allocUnmet.x) / satScX);
				float satXN = Mathf.Clamp01(Mathf.Max(0f, 0f - allocUnmet.x) / satScX);
				float openXP = Mathf.Max(0f, pDotM + dxH) + PaceRollUse * hdXP;
				float openXN = Mathf.Max(0f, 0f - (pDotM + dxH)) + PaceRollUse * hdXN;
				paceCapXPDbg = Mathf.Lerp(Mathf.Max(capXP, PaceSeenUse * aRollSeen, openXP), Mathf.Max(capXP, PaceSeenUse * aRollSeen), satXP);
				paceCapXNDbg = Mathf.Lerp(Mathf.Max(capXN, PaceSeenUse * aRollSeen, openXN), Mathf.Max(capXN, PaceSeenUse * aRollSeen), satXN);
				paceCapXPDbg = Mathf.Min(paceCapXPDbg, Mathf.Max(5f, aRollPos));
				paceCapXNDbg = Mathf.Min(paceCapXNDbg, Mathf.Max(5f, aRollNeg));
				// 2.7.2 (Tom, 10:19): the window does not hold the target in measured mode - the commanded steady rate is the only
				// lever (the cap below), and a reversal from it runs at the tuned acceleration / braking whatever the yaw does.
				// The window binds only while the allocator reports the axis cannot deliver what the loop asks (airframe-wide, any
				// effector): a latch on the unmet report, on past PaceSatDead + PaceSatSpan x PaceGateOn, off below x PaceGateOff -
				// never a blend toward the raw target (2.7.0's partial gate leaked it).
				float satZm = Mathf.Max(satZP, satZN);
				if (satZm > PaceGateOn)
				{
					paceLatchZ = true;
				}
				else if (satZm < PaceGateOff)
				{
					paceLatchZ = false;
				}
				gateZ = paceLatchZ ? 1f : 0f;
				float satXm = Mathf.Max(satXP, satXN);
				if (satXm > PaceGateOn)
				{
					paceLatchX = true;
				}
				else if (satXm < PaceGateOff)
				{
					paceLatchX = false;
				}
				gateX = paceLatchX ? 1f : 0f;
				paceHdZPDbg = hdZP;
				paceHdZNDbg = hdZN;
				paceDzHDbg = dzH;
				paceDzAllDbg = dzAll;
				paceDxHDbg = dxH;
				paceDxAllDbg = dxAll;
				paceRHDbg = rHm;
				pacePHDbg = pHm;
				paceSeenZDbg = aYawSeen;
				// the sideslip correction's share of the body yaw (last tick's rS, velocity-vector yaw)
				float yawCorr = rSDbg * numPhys;
				// the demand is judged at the AoA the jet will have at the end of the horizon (an AoA sweep under the roll moves
				// the yaw the roll needs at pS cos AoA x AoA rate; a push + roll through zero AoA reverses it)
				float aH = (alpha + alphaDot * PaceHorizon) * (MathF.PI / 180f);
				float sinH = Mathf.Sin(aH);
				// 2.5.4: inside +-PaceSinMin of zero AoA the roll's share of the yaw demand (pS sin AoA) is too small for any roll to
				// settle a yaw error - the yaw loop owns it - so the window is off there (2.5.3 divided by a floored sin and turned a
				// leftover yaw rate at the zero crossing into roll demands of 400-570 deg/s, then a flip to zero: 09:44 log, 318 / 324
				// / 328 s)
				bool winOn = Mathf.Abs(sinH) >= PaceSinMin;
				float sinD = winOn ? sinH : (PaceSinMin * ((sinH >= 0f) ? 1f : -1f));
				float lo = (rM - reachNeg - yawCorr) / sinD;
				float hi = (rM + reachPos - yawCorr) / sinD;
				if (lo > hi)
				{
					float tmpP = lo;
					lo = hi;
					hi = tmpP;
				}
				// the roll axis the same way: the pair's body-roll demand (pS cos AoA - rS sin AoA) may not lead the measured roll
				// rate by more than the roll axis can close within the horizon (its acceleration now, plus PaceRollUse of the
				// roll authority on that side). Past ~40 deg AoA the roll axis is the weak one: the yaw delivers its share and the
				// roll does not, and that too is sideslip (bench, full aft + roll at 400 km/h, 42 deg AoA: p 1 against 30 asked,
				// r 22, sideslip -9).
				float pM = pMm;
				float pDot = pDotM;
				float cosH = Mathf.Cos(aH);
				if (noseAx)
				{
					cosH = Mathf.Abs(cosH);   // 4.3.0: the pilot's axis (see LateralLaw's head)
				}
				float loR = float.MinValue;
				float hiR = float.MaxValue;
				if (Mathf.Abs(cosH) > PaceSinMin)
				{
					float rollPos = Mathf.Max(0f, pDot * PaceHorizon + PaceRollUse * aRollPos * PaceHorizon) + PaceFloor;
					float rollNeg = Mathf.Max(0f, 0f - pDot * PaceHorizon + PaceRollUse * aRollNeg * PaceHorizon) + PaceFloor;
					// 2.7.0: measured; a roll shortfall is sideslip at sin AoA, so the margin buys sqrt(2 a (margin - |beta|) / sin AoA) of roll-rate lead
					float sAmR = Mathf.Max(0.05f, Mathf.Abs(num2));
					float flR = PaceFloor * Mathf.Clamp01(paceMLeftDbg / 2f);
					rollPos = Mathf.Max(PaceMarginK * Mathf.Sqrt(2f * paceCapXPDbg * paceMLeftDbg / sAmR), paceCapXPDbg * PaceLeadT) + flR;
					rollNeg = Mathf.Max(PaceMarginK * Mathf.Sqrt(2f * paceCapXNDbg * paceMLeftDbg / sAmR), paceCapXNDbg * PaceLeadT) + flR;
					float rollCorr = 0f - rSDbg * num2;
					loR = (pM - rollNeg - rollCorr) / cosH;
					hiR = (pM + rollPos - rollCorr) / cosH;
					if (loR > hiR)
					{
						float tmpR = loR;
						loR = hiR;
						hiR = tmpR;
					}
					// intersect; when the two axes disagree the yaw window stands (the sideslip is what is protected)
					if (loR <= hi && hiR >= lo)
					{
						lo = Mathf.Max(lo, loR);
						hi = Mathf.Min(hi, hiR);
					}
					else
					{
						// 2.7.0: the windows do not overlap - one axis is ahead of the other. The laggard is the one whose implied velocity
						// roll is further from the stick's demand; its window stands, so the other axis is brought back to it (a yaw that
						// cannot follow holds the roll down to it; a roll that cannot follow holds the yaw back to it)
						float pSrM = (rM - yawCorr) / sinD;
						float pSpM = (pM - rollCorr) / cosH;
						if (Mathf.Abs(num11 - pSpM) > Mathf.Abs(num11 - pSrM))
						{
							lo = loR;
							hi = hiR;
						}
					}
				}
				// never a roll against the one in progress (or against the one asked for, from rest): stop instead
				float sideP = (Mathf.Abs(pTraj.x) > 2f) ? Mathf.Sign(pTraj.x) : Mathf.Sign(num11);
				if (sideP > 0f && hi < 0f)
				{
					lo = 0f;
					hi = 0f;
				}
				else if (sideP < 0f && lo > 0f)
				{
					lo = 0f;
					hi = 0f;
				}
				// 2.7.0: the gate is the measured shortfall (allocator unmet + undelivered slew) per axis, nothing from the rudders' load
				// (2.12.1: the 2.5.x rudder-usage gate and its airframe-usage variant, overwritten here since 2.7.0, are deleted)
				float wSat = gateZ;
				paceGateF = Mathf.Max(wSat, paceGateF - dt / Mathf.Max(0.02f, PaceGateTau));
				wSat = winOn ? paceGateF : 0f;
				paceGateXF = Mathf.Max(gateX, paceGateXF - dt / Mathf.Max(0.02f, PaceGateTau));
				paceGateZDbg = wSat;
				paceGateXDbg = paceGateXF;
				float tgtRaw = num11;
				paceTgtRaw = tgtRaw;
				// 2.5.11: the two laws are blended by RollPaceRateBlend (0 = 2.5.7 pacing: window + acceleration cap; 1 = 2.5.8 rate
				// cap; between = both, in proportion). RollPaceRateMode false = blend 0.
				// 2.6.9: the rate cap below is a model of the RUDDERS - their crossing time to the far stop, their tables - so it holds
				// only where the rudders are the yaw axis. Past the rudder fade (rudderAuth -> 0, 50-60 deg AoA here) it read the
				// faded rudders' crossing against the stabs' authority and sat on PaceRateFloor (65 deg / 300 km/h: cap 10 against an
				// airframe limit of 36), and at 0.4 that held the steady falling-leaf command to ~70 % of the limit while the
				// window alone kept the reversal sideslip the same (5.7 vs 5.5 deg). So the blend counts where its model is valid:
				// your RollPaceRateBlend at full where the rudders have their authority, fading to the window alone as they fade.
				// 2.7.2: in measured mode the cap counts at every AoA (its capacity is the whole airframe's), at Tom's blend
				// 2.7.2: in measured mode the cap's capacity is the whole airframe's; its share is Tom's blend, fading with the rudders
				// (past their fade the yaw is the velocity roll and the one-axis law's own braking owns the reversal: no falling-leaf tax)
				float rateBlend = Plugin.RollPaceRateBlend * Mathf.Clamp01(rudderAuth);
				rateBlendEffDbg = rateBlend;
				float num11Rate = num11;
				float pSRateCap = 9999f;
				if (rateBlend > 0f)
				{
					// 2.5.8 rate mode: no window, no acceleration cap. The target is capped at the roll rate whose full stick
					// reversal at the tuned acceleration / braking the yaw can follow within the sideslip margin (your rule: slow
					// the commanded rate, not the move). Yaw-rate swing of the reversal: dR = 2 pS |sin AoA|. The rudders cross
					// from where they are to the other stop (tCross, slew), then pull dR down at their one-side yaw aOpp:
					// sideslip ~ cos AoA x (dR tCross / 2 + dR^2 / (2 aOpp)) x PaceRateK. Solve for dR at the margin, cap pS.
					float aOpp = 0f;
					float tCross = 0f;
					foreach (SurfaceEffector seP in surfaces)
					{
						if (seP.kind != SurfaceKind.Rudder || !seP.active || seP.M == null || seP.N < 2)
						{
							continue;
						}
						aOpp += 0.5f * Mathf.Abs(seP.MAt(seP.max).z - seP.MAt(seP.min).z) * iz;
						// the crossing: from where the rudder is now to the stop on the far side of center (the reversal side)
						float far = (Mathf.Abs(seP.now - seP.max) > Mathf.Abs(seP.now - seP.min)) ? seP.max : seP.min;
						tCross = Mathf.Max(tCross, Mathf.Abs(far - seP.now) / Mathf.Max(1f, seP.rate));
					}
					// past the rudder fade the stabs carry the yaw: credit the allocator's yaw authority instead of the faded tables
					aOpp = Mathf.Max(aOpp * Mathf.Max(0.05f, rudderAuth), aYaw * (1f - rudderAuth));
					// 2.5.12: less what the allocator reports it cannot deliver
					aOpp *= 1f - Mathf.Lerp(PaceSatDerateLo, PaceSatDerateHi, 1f - rudderAuth) * Mathf.Clamp01(yawSat);
					float bMrg = Mathf.Max(1f, stopSlipMarginDbg);
					// 2.7.2 (Tom, 10:19): the capacity a reversal can call on is the whole airframe's, not the rudders': every effector's
					// yaw still available toward the reversal's side from where the allocator has it (rudders, stabs, the Ifrit's roll TVC
					// - whatever the allocator can turn to yaw), less what it reports unmet, plus the airframe's own directional
					// stiffness at the margin sideslip (the passive share: the airframe balancing laterally at this AoA). The dead time
					// is the yaw-weighted travel of those effectors to the deflection the reversal needs.
					float sideRev = 0f - Mathf.Sign(num11 * sinH);
					float passive = PaceStiffUse * Mathf.Max(0f, slipStiffDbg) * bMrg;
					// 2.8.1: the reversal's capacity is the estimator's figure toward the reversal side (measured yaw acceleration now, the
					// committed increment arriving within the horizon, and what the allocator itself would still give - 1.2-1.4x the realised
					// yaw in the 12:31 flight where the table headroom read 1.8x), plus the passive stiffness at the margin
					aOpp = Mathf.Max(5f, ((sideRev >= 0f) ? capEst.capZeP : capEst.capZeN) + passive);
					tCross = 0f;
					float wT = 0f;
					foreach (Effector seA in effs)
					{
						if (seA == null || !seA.active || seA.M == null || seA.N < 2)
						{
							continue;
						}
						Vector3 mA = seA.MAt(seA.max);
						Vector3 mB = seA.MAt(seA.min);
						float yA = Mathf.Abs(mA.z - mB.z) * iz;
						if (yA < 5f)
						{
							continue;
						}
						float farA = ((mA.z >= mB.z) == (sideRev > 0f)) ? seA.max : seA.min;
						tCross += yA * Mathf.Abs(farA - seA.cmd) / Mathf.Max(1f, seA.rate);
						wT += yA;
					}
					tCross = (wT > 1f) ? (tCross / wT) : 0f;
					rcAOppDbg = aOpp;
					float cH = Mathf.Max(0.2f, Mathf.Abs(cosH));
						// 2.5.10: RollPaceRateSlip (Dev key) scales the margin the cap aims at - the roll rate / sideslip compromise in flight
						float cQ = 2f * bMrg * Plugin.RollPaceRateSlip / Mathf.Max(1f, aOpp * cH * Mathf.Max(0.1f, PaceRateK));
					float dR = aOpp * (0f - 0.5f * tCross + Mathf.Sqrt(0.25f * tCross * tCross + cQ));
					// 2.6.9 (PaceRateRamp): the reversal the cap models is the one the law will fly, not a step. The demanded yaw
					// rate swings at aT |sin AoA|, aT = the trajectory's reversal acceleration as the law sets it (RevFloorK x roll
					// authority, or the brake law) bounded by the governor's own open-loop cap for the reversing side - the num17
					// the trajectory gets. The yaw answers after tCross / 2 at aOpp. Sideslip ~ the area between the two:
					//   E(dR) = dR tau + dR^2 / 2 (1/aOpp - 1/aD),  tau = tCross / 2
					// which is the 2.5.8 step model exactly as aD -> infinity. Where the yaw out-accelerates the demand it catches
					// the ramp and tracks it: E saturates at aD tm tau / 2 and, inside the margin, the cap does not bind. Measured
					// state and the law's own numbers only. (Bench, blend 0.4: full-aft reversal 320 km/h 6.6 -> 3.6 deg, worst of
					// the regression set 6.6 -> 4.3; Ifrit 0.7.2 found the same 4x pessimism in the step model.)
					if (PaceRateRamp)
					{
						float cA = Mathf.Max(Mathf.Abs(num), 0.05f);
						float aLaw = (ReversalFloor && RevConst) ? (RevFloorK * aRoll / cA) : (Plugin.RollBrakeUse * (BrakeUseAll ? aRollGo : aRollWing) / cA);
						// 2.7.2: in measured mode the trajectory runs at the law's own acceleration (no open-loop cap)
						float aT = Mathf.Clamp(aLaw, 20f, Plugin.MaxRollAccel);
						float aD = aT * Mathf.Max(Mathf.Abs(sinH), PaceSinMin);
						float tau = 0.5f * tCross * RateCapTauK;
						// 2.6.9 (RateCapNeedTravel): the rudders do not have to reach the far stop before the yaw turns - only the
						// deflection that makes the yaw acceleration the reversal asks for (aD of their one-side aOpp), from where they
						// sit, through center. At high dynamic pressure that is a few degrees (corkscrew entry at 700 km/h: rudders at
						// +-8..13 deg, the far-stop crossing put the cap at 25-30 deg/s where reversals from 208 made 3.9 deg); at
						// moderate q it is most of the stroke and nothing changes.
						if (RateCapNeedTravel)
						{
							// the effectors need only the deflection that makes aD of their aOpp, through center: about (0.5 + 0.5 need) of the stroke
							float needM = Mathf.Clamp01(aD / Mathf.Max(1f, aOpp));
							tau = 0.5f * tCross * Mathf.Lerp(0.5f, 1f, needM) * RateCapTauK;
						}
						else if (RateCapNeedTravel)
						{
							float need = Mathf.Clamp01(aD / Mathf.Max(1f, aOpp));
							float tN = 0f;
							foreach (SurfaceEffector seN in surfaces)
							{
								if (seN.kind != SurfaceKind.Rudder || !seN.active || seN.M == null || seN.N < 2)
								{
									continue;
								}
								float cN = 0.5f * (seN.max + seN.min);
								float hN = 0.5f * (seN.max - seN.min);
								float farN = (Mathf.Abs(seN.now - seN.max) > Mathf.Abs(seN.now - seN.min)) ? seN.max : seN.min;
								float dN = Mathf.Min(Mathf.Abs(farN - seN.now), Mathf.Abs(seN.now - cN) + need * hN);
								tN = Mathf.Max(tN, dN / Mathf.Max(1f, seN.rate));
							}
							tau = 0.5f * tN * RateCapTauK;
						}
						float qM = bMrg * Plugin.RollPaceRateSlip / Mathf.Max(0.01f, cH * Mathf.Max(0.1f, PaceRateK));
						float kQ = 0.5f * (1f / Mathf.Max(1f, aOpp) - 1f / Mathf.Max(1f, aD));
						float disc = tau * tau + 4f * kQ * qM;
						dR = (disc > 0f && tau + Mathf.Sqrt(disc) > 1E-04f) ? (2f * qM / (tau + Mathf.Sqrt(disc))) : 1E+06f;
					}
					pSRateCap = Mathf.Max(PaceRateFloor, dR / (2f * Mathf.Max(Mathf.Abs(sinH), PaceSinMin)));
					rcAOppDbg = aOpp;
					rcTcDbg = tCross;
					rcSinHDbg = sinH;
					num11Rate = Mathf.Clamp(num11, 0f - pSRateCap, pSRateCap);
				}
				// the window may hold the roll in progress while the yaw comes down (that is the paced brake / reversal) but never
				// asks for more roll than the stick or the roll in progress, and never for a roll against the one in progress
				float clamped;
				// 2.19.0: where the rudders are the yaw axis (rudW -> 1) the band holds the roll at the yaw's rotation; where they are not
				// (faded past ~55 deg, or gone) the yaw effectors ARE the roll effectors (the stabs' differential, the flaperons) and a
				// roll loop holding the roll against them starves the yaw brake: 3 Nov 00:09 log, both fins gone, 82.4 s reversal at 65
				// deg - target held at 0 for 3.8 s, one stab at -21 and the other at 0, 8 deg/s^2 of yaw against a measured 24 when the
				// roll went with the yaw (23:49 log, 62 s). There the target is paced toward the demand at the yaw's capacity instead.
				float rudWwin = Mathf.Clamp01(rudderAuth) * Mathf.Clamp01(yawHealth / 0.5f);
				float clampedBand = Mathf.Clamp(num11, lo, hi);
				if (PaceHoldPacedByRudder && rudWwin < 0.999f)
				{
					// 2.17.0: while the yaw is reported saturated (the latch), the target is PACED toward the demand at the yaw's measured
					// capacity (the 2.7.1 limiter, on the estimator's fin-aware figure), not clamped to the band. The band's lead scales
					// with the sideslip margin LEFT, so with the margin spent it pinned the target to the rotation the jet had; the yaw loop
					// then had no error to brake with and nothing moved until the unmet report happened to fall under the latch's release
					// (23:30 log, both fins gone, 65 deg: reversal at 110 s held at +19 deg/s for 2.5 s against a -30 demand, zero crossing
					// 4.0 s after the stick; held push-over at 157 s cut the roll target -47 -> -10..-23 while coordination needed -30..-54,
					// sideslip +9 deg/s, departure). A move toward the axes (less lead) is free; a move away is paced at capacity / sin AoA.
					float pSrNow = (rM - yawCorr) / sinD;
					float from = paceRateInit ? paceRatePrev : pTraj.x;
					float dirT = Mathf.Sign(num11 - from);
					float capZd = Mathf.Max(PaceHoldCapFloor, (dirT * sinD > 0f) ? capEst.capZeP : capEst.capZeN);
					float rateT = capZd / Mathf.Max(Mathf.Abs(sinD), PaceSinMin);
					bool away = (num11 - pSrNow) * dirT > 0f;
					float lim = from + dirT * rateT * dt;
					float clampedPaced = (away && (num11 - lim) * dirT > 0f) ? lim : num11;
					clamped = Mathf.Lerp(clampedPaced, clampedBand, rudWwin);
				}
				else
				{
					clamped = clampedBand;
				}
				// 2.18.0: the window may HOLD the target where it is; it may never move it back toward the rotation (away from the stick's
				// demand). 23:49 log (both fins gone, 65 deg), 59.8 s reversal: the first 0.3 s of braking took the body roll 6 -> 1 deg/s
				// while the yaw (30 deg/s, slow to shed) still implied a 33 deg/s rotation; the band set the target at rotation - lead = 25
				// (p = 10.6 coordinated), the roll loop drove +X the OLD way for 1.2 s and took the stabs and flaperons from the yaw brake
				// (tgtX +47..+58 against a -30 demand); once the target came down the yaw braked at 24 deg/s^2. Tom rejected the same
				// behaviour in 2.7.0 ('keeps rolling the old way for a moment then reverses'). The target only ever moves toward the demand.
				if (PaceNoRetreat && paceRateInit && Mathf.Abs(num11 - paceRatePrev) > 0.01f)
				{
					float dirD = Mathf.Sign(num11 - paceRatePrev);
					if ((clamped - paceRatePrev) * dirD < 0f)
					{
						clamped = paceRatePrev;
					}
				}
				// 2.20.0 (PaceStopNoFin): without the rudders the yaw sheds at the stabs' pace (20-25 deg/s^2 at 65 deg) whatever the
				// roll does; a velocity-roll target of 0 against a yaw still carrying the rotation asks the body roll for -r tan AoA
				// (anti-coordinated), and the roll loop's try at it is the reversal's sideslip (bench leaf rev 370 no fins: p +11 -> -10
				// while r went 24 -> 7, sideslip 23 deg). Here the target may not go below the rotation the jet has with the body roll
				// stopped (r sin AoA, falling with the yaw): p -> 0, the nose yaws through, the sideslip grows at r cos AoA only.
				// The yaw loop is asked for its brake capacity directly (below, on rCmd), since this target alone gives it ~0.18 r.
				// Scaled by the FIN loss, not the rudder fade: with the fins on above the fade the sideslip the old stop builds is what
				// weathercocks the yaw round (bench 65 deg fullaft reversal 500: 1.6 s to zero; held to r sin AoA it took 3.1 s) - the
				// fins are the yaw brake there, and the clean jet keeps its reversal.
				paceStopNF = 0f;
				float finKw = Mathf.Clamp01(yawHealth / 0.5f);
				// 3.0.2: only for a roll being stopped (the trajectory still rotating, or the stick asking), and only where the window is
				// on. sideP fell back to Mathf.Sign(num11), and Sign(0) is +1: with the stick centred and no roll at all this ran every
				// tick, and with the fins gone (finKw 0) its yaw command below (rCmd = r - capacity / KYaw) replaced the sideslip loop's.
				// 5 Nov 20:29 log, both fins and the right stab gone, straight and accelerating at 2-10 deg AoA: rCmd flipped +1.4 / -2.0
				// every ~0.1 s as the test came and went (the roll twitch, p +-7 deg/s through the flaperons doing the yaw), then held
				// r - 2 deg/s from 111.5 s while the sideslip loop asked for +26: sideslip 3 -> 48 deg at 160-245 m/s.
				bool stopNFRoll = PaceStopNoFinGate ? (winOn && (Mathf.Abs(pTraj.x) > 2f || Mathf.Abs(num11) > 2f)) : true;
				float stopNF = (PaceStopNoFin && stopNFRoll && finKw < 0.999f && sideP != 0f) ? Mathf.Max(0f, sideP * rM * sinD) : 0f;
				if (sideP != 0f && clamped * sideP < stopNF)
				{
					clamped = Mathf.Lerp(sideP * stopNF, 0f, finKw);
					paceStopNF = (stopNF > 0f) ? sideP : 0f;
				}
				else
				{
					float limP = Mathf.Max(Mathf.Abs(num11), Mathf.Abs(pTraj.x));
					clamped = Mathf.Clamp(clamped, 0f - limP, limP);
				}
				paceLoDbg = lo;
				paceHiDbg = hi;
				paceClampDbg = clamped;
				float num11Win = Mathf.Lerp(num11, clamped, wSat);
				// the paced target continues from the target actually flown (the raw one while the latch is off)
				paceRatePrev = num11Win;
				paceRateInit = true;
				// 2.6.1: climbing back out of a hold at the yaw's pace (see paceWinHeld)
				paceRelDbg = 0f;
				if (PaceReleasePace > 0f)
				{
					float dirRel = Mathf.Sign(num11Win * sinH);
					float relAcc = PaceReleasePace * Mathf.Max(PaceFloor / PaceHorizon, PaceYawUse * Mathf.Max(dirRel * (rDot + dzH), PaceSeenUse * aYawSeen)) / Mathf.Max(Mathf.Abs(sinD), PaceSinMin);
					if (num11Win * paceWinPrev < 0f)
					{
						paceWinSame = false;
					}
					if (!paceWinHeld)
					{
						paceWinSame = Mathf.Abs(pTraj.x) > 2f && pTraj.x * num11Win > 0f;
					}
					if (paceWinHeld && paceWinSame && num11Win * paceWinPrev >= 0f && Mathf.Abs(num11Win) > Mathf.Abs(paceWinPrev))
					{
						// the measured yaw already supports a roll of (r - correction) / sin AoA: up to that the climb is free (no yaw
						// demand leads the yaw there); the pacing applies to the part above it
						float pSup = (rM - yawCorr) / sinD;
						float lim = Mathf.Max(Mathf.Abs(paceWinPrev) + relAcc * dt, (pSup * num11Win > 0f) ? Mathf.Abs(pSup) : 0f);
						if (Mathf.Abs(num11Win) > lim)
						{
							paceRelDbg = Mathf.Abs(num11Win) - lim;
							num11Win = Mathf.Sign(num11Win) * lim;
						}
					}
					paceWinHeld = Mathf.Abs(num11Win) < Mathf.Abs(tgtRaw) - 1f;
					paceWinPrev = num11Win;
				}
				else
				{
					paceWinHeld = false;
					paceWinSame = false;
					paceWinPrev = num11Win;
				}
				// blend: the window's hold and the rate cap's trim of the target, in proportion
				// 2.6.12 (RateCapOnReversal): the cap's share of the target applies only while a reversal is under way - the stick
				// asking for the opposite direction to the roll in progress. It predicts what a reversal from the present rate would
				// cost, and with no reversal it was trimming the steady roll for one that never came (02 Nov 00:54 log, 340 kt
				// corkscrew entry: roll 70 -> 17 deg/s at 45-55 deg AoA, sideslip 0.8-2.2 of a 4 deg margin; bench, the window
				// holds 0 there and the whole hold is the cap's 41 %)
				float revW = 1f;
				num11 = Mathf.Lerp(num11Win, num11Rate, rateBlend * PaceBlendTargetK * revW);
				paceLeadDbg = (rateBlend >= 0.5f) ? pSRateCap : ((sideP >= 0f) ? reachPos : reachNeg);
				paceHoldDbg = tgtRaw - num11;
				// 2.10.0: the 2.5.x open-loop acceleration cap (rudder tables) went with the table governor; the trajectory runs at the law's own acceleration
				paceAccelCap = -1f;
			}
			else
			{
				paceRDotF.init = false;
				pacePDotF.init = false;
				paceRateInit = false;
				paceGateF = 0f;
				paceWinHeld = false;
				paceWinSame = false;
				paceWinPrev = 0f;
				paceRelDbg = 0f;
			}
			paceOmegaZPrev = omega.z;
			paceOmegaXPrev = omega.x;
			paceInit = true;
			guardDbg = x;
			// 2.34.0 (PushGov): the lateral limit of the push side. Where the law would hold or bring the rotation down (stop, release,
			// reversal, a stick target under the rotation) and the yaw cannot shed the sweep's demand, the trajectory comes down no faster
			// than -L; where L > 0 it grows at L (the roll follows the yaw down)
			// rate band [L, U] for the velocity-roll reference; outside it the trajectory is re-aimed (target) and its acceleration set to the
			// band's edge. L > U (the pitch ran ahead of both axes this tick - the cap catches it next tick): the roll's edge, U.
			bool pushGovOn = !float.IsNaN(pushGovL) && pushGovSide != 0f && !pushBleed && !pushHeld;
			if (pushBleed) num11 = 0f;
			float pushGovRate = 0f;
			if (pushGovOn)
			{
				bool lawBrakes = pushGovSide * (num11 - pTraj.x) < 0f;
				float num11Law = num11;
				float lo = pushGovL, hi = Mathf.Max(pushGovL, pushGovU);
				if (pushGovL > pushGovU) lo = hi = pushGovU;
				// the law's own rate sign: braking (fast), or holding / growing (toward its target)
				if (lawBrakes && lo > -1E+05f)
				{
					// braking: as fast as the law wants, but no faster than -lo; if lo > 0 the reference grows at lo instead
					pushGovRate = lo;   // applied as min(num17, |lo|) below when lo < 0
					if (lo > 0f) num11 = pushGovSide * Mathf.Max(Mathf.Max(Mathf.Abs(pTraj.x), Mathf.Min(Mathf.Abs(pTraj.x) + 30f, pushGovCeil)), pushGovSide * num11);
					else if (hi < 0f) { }   // the law's brake is at least as fast as needed? (num17 >= |hi| normally)
				}
				else if (hi < 0f)
				{
					// holding or growing where the roll's ceiling says it must come down: brake at |hi|
					pushGovRate = hi;
					num11 = pushGovSide * Mathf.Max(0f, Mathf.Abs(pTraj.x) - 30f);
				}
				else if (lo > 0f)
				{
					// holding where the yaw says it must grow: grow at lo (no faster than hi)
					pushGovRate = Mathf.Min(lo, hi);
					num11 = pushGovSide * Mathf.Max(Mathf.Max(Mathf.Abs(pTraj.x), Mathf.Min(Mathf.Abs(pTraj.x) + 30f, pushGovCeil)), pushGovSide * num11);
				}
				else
				{
					pushGovOn = false;   // the law's own move is inside the band
				}
				// faded in over 2..8 deg/s of commanded AoA rate toward zero (an AoA held at a limit flickers around zero: no band there)
				float wGt = Mathf.Clamp01((pushGovA - 2f) / 6f);
				num11 = Mathf.Lerp(num11Law, num11, wGt);
			}
			pSTgtDbg = num11;
			float num15 = Mathf.Abs(pTraj.x * num);
			float max = Mathf.Lerp(Plugin.RollAccelUseLowIas, Plugin.RollAccelUse, Mathf.InverseLerp(60f, 110f, ias));
			float a = Mathf.Clamp(0.85f - num15 / Mathf.Max(30f, pFull * (aRollGo / aRoll)), 0.1f, max) * aRollGo / Mathf.Max(Mathf.Abs(num), 0.05f);
			float num16 = 0.6f * aYaw / Mathf.Max(Mathf.Abs(num2), 0.05f);
			// 2.6.0: the trajectory's yaw-coordination caps (RollEntryCoord, BrakeYawCap, RevYawCap, CoordJerk) are gone; the pace
			// governor's acceleration cap (paceAccelCap) is the only pacing of the trajectory
			float num17 = (latAccDbg = a);
			// 1.8.41: the roll entry's acceleration yields with the governor too (the entry is where the surfaces deflect most)
			num17 *= Mathf.Lerp(RollGovAccelFloor, 1f, rollGov);
			loadRevDbg = 0f;
			// 4.1.2: the roll entry's acceleration as the law has it now (stick-side authority, the governor's share), and how much of a
			// loaded reversal's build-up is paced by the rudders' travel (0 = none: clean jet, 4.1.1)
			float entryA = num17;
			float revRampW = 0f;
			if (pTraj.x * (num11 - pTraj.x) < 0f)
			{
				float num18 = aRoll / Mathf.Max(30f, pFull);
				num17 = (Plugin.RollBrakeUse * (BrakeUseAll ? aRollGo : aRollWing) + num18 * num15) / Mathf.Max(Mathf.Abs(num), 0.05f);
				// 2.7.0 (PaceRevIntent): a reversal the stick asked for is a reversal, even while the measured window holds the target on
				// the near side - it brakes at the reversal's tuned acceleration, not the stop's
				bool revIntent = PaceRevIntent && paceTgtRaw * pTraj.x < 0f && Mathf.Abs(paceTgtRaw) > 10f;
				bool revNow = ((num11 * pTraj.x < 0f && Mathf.Abs(num11) > 10f) || revIntent) && ReversalFloor;
				float revFree = 0f;
				if (revNow)
				{
					float num19 = RevFloorK * aRoll / Mathf.Max(Mathf.Abs(num), 0.05f);
					num17 = (revFree = (RevConst ? num19 : Mathf.Max(num17, num19)));
				}
				// 2.1.6: with an asymmetric load, a roll brake / reversal may only accelerate as fast as the yaw left after the load
				// can follow (the roll's yaw rate p sin AoA has to reverse with it; with the rudders already deflected for the load
				// they slew across their whole travel). 12 Oct 19:30 log, 144 s: a 110 deg/s reversal at 30 deg AoA left the yaw
				// 0.7 s behind, sideslip -11 deg, and the guard and governor cut the roll (target 116 -> 20 deg/s). Clean jets unchanged.
				if (AsymSharedLat && Plugin.AsymSharedLatOn && asymValid && LoadRevYawK > 0f)
				{
					float dyG = Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / Mathf.Max(0.005f, CoupHeldDySpan));
					if (dyG > 0f)
					{
						float revCap = LoadRevYawK * aYaw * Mathf.Max(0.2f, sharedLatDbg) / Mathf.Max(Mathf.Abs(num2), 0.05f);
						// F-22E 4.1.2 (RevLoadRamp): a reversal toward the side the load pulls. There the load and the roll damping brake the
						// old roll by themselves (the roll surfaces sit near neutral), and held to this limit the trajectory waited at it
						// and built the new roll's acceleration after the zero crossing: 10 Nov 19:33 log (4.1.1), 34.0 / 45.6 / 49.7 s,
						// 132-141 deg/s^2 braking with the ailerons at 4-6 deg, then 222-261 at 0.17-0.25 s after the crossing with the
						// ailerons and flaps at their stops ("stop the roll, then spike"; 4.1.1 only made the rise continuous). What the
						// 40 % stands for is the rudders' travel from where the load and the old roll hold them to the other side. On
						// that side the reversal is therefore not held flat at 40 % and then released: its acceleration builds over that
						// travel time (the jerk below) to one figure - the entry's, within the trajectory's own yaw figure of the yaw left
						// (60 %) - and keeps it through the crossing up to the new rate (the carry below). Against the load the roll
						// surfaces are at their stops through the whole reversal, there is no such pause, and the limit stays as it is
						// (bench, lifted there: the yaw 16 deg/s behind, the governor holding the roll at the crossing). Stops are
						// unchanged. RevLoadRamp = false: 4.1.1.
						if (RevLoadRamp && revNow && RudderCrossTime() > 0f)
						{
							// up to the acceleration the new roll starts with, within the trajectory's own yaw figure (num16: 0.6 of the yaw
							// authority) taken of the yaw left after the load
							float yawTop = num16 * Mathf.Max(0.2f, sharedLatDbg);
							revRampW = LoadRevPace(aRollGo, num, yawTop);
							revCap = Mathf.Lerp(revCap, Mathf.Max(revCap, Mathf.Min(entryA, yawTop)), LoadSideWeight(aRollGo));
						}
						loadRevDbg = dyG;
						num17 = Mathf.Lerp(num17, Mathf.Min(num17, revCap), dyG);
					}
				}
				if (revNow)
				{
					// F-22E 4.1.1 (RevOneAccel): the reversal's acceleration is the one its brake flies, the load's limit included, and
					// the new roll starts there (the 1.7.4 rule: one acceleration through the reversal, "capped by what the yaw axis
					// can follow"). The load's limit of 2.1.6 sat after this branch and revA was stored before it, so a loaded
					// reversal braked at the limit and started its new roll at the unlimited figure (10 Nov 18:10 log, six
					// reversals with full aft stick at 14-24 deg AoA: 138-168 deg/s^2 braking, 328-414 within 0.1 s of the zero
					// crossing; the ailerons from 7-13 deg to their stops in four ticks). RevOneAccel = false: 4.1.0.
					revA = RevOneAccel ? num17 : revFree;
					revTarget = revIntent ? paceTgtRaw : num11;
				}
			}
			else if (revA > 0f && RevCarry)
			{
				if (num11 * revTarget <= 0f || Mathf.Abs(num11) < 10f)
				{
					revA = 0f;
				}
				else
				{
					// the new roll starts at the reversal's acceleration and blends to the entry's by half its target rate. 4.1.1: from
					// either side (4.1.0 only raised the entry's figure; a limited reversal starts below it)
					// 4.1.2: a reversal the load brakes (pace > 0) keeps its one acceleration up to the new rate: the blend runs over the
					// whole target rate and the entry's figure stays within the top its brake had, so nothing rises a second time after
					// the crossing; and the build-up keeps the rudders' pace (a reversal from a low rate crosses zero before they have)
					float yawTopC = num16 * Mathf.Max(0.2f, sharedLatDbg);
					float pace = RevOneAccel ? LoadRevPace(aRollGo, num, yawTopC) : 0f;
					float num20 = 1f - Mathf.Clamp01(Mathf.Abs(pTraj.x) / Mathf.Max(10f, Mathf.Lerp(0.5f, 1f, pace) * Mathf.Abs(num11)));
					float carried = Mathf.Lerp(num17, revA, num20);
					num17 = RevOneAccel ? carried : Mathf.Max(num17, carried);
					if (pace > 0f)
					{
						num17 = Mathf.Lerp(num17, Mathf.Min(num17, Mathf.Min(entryA, yawTopC)), pace);
					}
					revRampW = pace;
					if (num20 <= 0f)
					{
						revA = 0f;
					}
				}
			}
			if (paceAccelCap > 0f)
			{
				num17 = Mathf.Min(num17, paceAccelCap);
			}
			num17 = Mathf.Clamp(num17, 20f, Plugin.MaxRollAccel);
			if (pushGovOn)
			{
				// faded in over 2..8 deg/s of commanded AoA rate (as the target above)
				float wG = Mathf.Clamp01((pushGovA - 2f) / 6f);
				num17 = Mathf.Lerp(num17, Mathf.Min(num17, Mathf.Abs(pushGovRate)), wG);
			}
			pushHeldB = 0f;
			if (pushBleed)
			{
				num17 = Mathf.Max(0.5f, pushPlanBleed);
			}
			else if (pushHeld && pTraj.x * (num11 - pTraj.x) < 0f)
			{
				// held stick, the roll moving toward a lower target (the law's own: the stick, the pace window): no faster than the plan's
				// yaw budget leaves after the AoA's share, and no faster than the law's own brake
				float aDotP = Mathf.Max(0f, (0f - Mathf.Sign(alpha)) * (qCmd - omegaV));
				pushHeldB = Mathf.Max(0f, pushPlanD - Mathf.Abs(pTraj.x) * Mathf.Abs(num) * aDotP * (MathF.PI / 180f)) / Mathf.Max(0.05f, Mathf.Abs(num2));
				num17 = Mathf.Max(0.5f, Mathf.Min(num17, pushHeldB));
			}
			float a2 = Mathf.Min(num17 / RollJerkTau, RollJerkServo * aRoll * rollSlew / Mathf.Max(Mathf.Abs(num), 0.05f));
			// 4.1.2: the jerk as the law has it; the run-out onto the target keeps it where a loaded reversal's build-up is paced below
			float a2Free = a2;
			if (revRampW > 0f)
			{
				// 4.1.2: the acceleration of a reversal the load brakes builds over the time the rudders need from where they are to the
				// other side's stop (measured positions, their servo rate), not over RollJerkTau
				float tRud = RudderCrossTime();
				if (tRud > RollJerkTau) a2 = Mathf.Min(a2, num17 / Mathf.Lerp(RollJerkTau, tRud, revRampW));
			}
			if (isAI && Plugin.AIDirectRoll)
			{
				float aDir = Mathf.Min(AIRollDirectAccel, 1.3f * num16);
				num17 = Mathf.Max(num17, aDir);
				a2 = Mathf.Max(a2, aDir / 0.05f);
			}
			// 2.0.6 coordinated roll jerk: the stability-axis roll needs a yaw rate of p sin(a); the rudders reach full travel
			// no faster than their servo allows (35 deg at 100 deg/s), so the yaw acceleration can only build at about
			// aYaw x (rudder rate / half travel). The roll's jerk is held to what keeps that yaw build-up within reach
			// (CoordJerkK of it), below the rudder fade only (rudderAuth), so roll entries and reversals at moderate AoA no
			// longer outrun the yaw axis and leave sideslip behind.
			float jFloor = 100f;
			coordJerkDbg = a2;
			latAccDbg = num17;
			latJerkDbg = Mathf.Max(a2, jFloor);
			float endJ = RollEndJerk * Mathf.Lerp(1f, EndJerkHi, Mathf.InverseLerp(EndJerkLp0, EndJerkLp1, aRoll / Mathf.Max(30f, pFull)));
			lpDbg = aRoll / Mathf.Max(30f, pFull);
			// 2.3.0 (CoordinatedStop): with the lateral stick and pedals released at AoA, the stop is paced by what the jet is
			// doing, not by the modelled authority. The (pCmd, rCmd) pair below is coordinated only if both axes track it;
			// stopping a 65 deg AoA rotation the wing braked the roll at the command's pace while the yaw axis could not (21 Oct
			// 21:28 log: roll decel 10.9 deg/s^2 against yaw 17.5; coordinated would be 8.2), the sideslip rate p sin(a) - r cos(a)
			// ran 8-11 deg/s, sideslip reached 4-6.6 deg and its correction rolled the jet back the way it had been rolling (5 deg/s
			// at 2-3 s after the release; 1 deg/s on 2.0.5). On the bench it is the other way round (the yaw stops first, the
			// wing at its stops, sideslip +2.8). Here the trajectory's deceleration is scaled down as either axis falls behind
			// its command (CoordStopErr0..Err1 deg/s, roll error in velocity-vector terms), to zero: the command then holds
			// until the slower axis has caught up, so the roll and yaw commands stay coordinated with the rates the jet has.
			// Only with the stick released and above CoordStopA0..A1 deg AoA, so the roll feel with the stick in is unchanged.
			// 2.5.3: kept at every level (the yaw-reach governor paces entries, brakes and reversals below the rudder fade; this
			// paces the released-stick stop at high AoA from the measured lag, 01:40 log 214 s: 15.5 deg for 5 s without it)
			pTraj.Step(num11, Mathf.Max(num17, 0.5f), Mathf.Max(a2, jFloor), Mathf.Max(Mathf.Max(a2, a2Free) * Mathf.Min(endJ, 1f), jFloor), dt);
			pS = pTraj.x;
			float num21 = 0f;
			if (tas > 40f)
			{
				num21 = 9.81f * Mathf.Sin(phi * (MathF.PI / 180f)) * Mathf.Cos(theta * (MathF.PI / 180f)) / tas * 57.29578f * Mathf.Clamp01((tas - 40f) / 20f);
			}
			float num22 = ((Mode == FcsMode.GearDownRate) ? 1f : Plugin.KBeta);
			// 4.3.0 (ReversedFlowNoseAxis): flying backwards the pilot's axis is not the flight path (90 deg off it at 135 deg AoA), so
			// turning the nose builds sideslip. In a slow tail slide (below RevSlipTas0 -> Tas1 m/s TAS) the sideslip hold (and the
			// turn's gravity yaw with it) steps aside while the pilot turns the nose: lateral stick or pedal in, or the rotation still
			// running (full from RevSlipHoldStick of input or RevSlipHoldRate deg/s of commanded rotation), faded in over RevSlipHoldA0 ->
			// A1 |AoA|. At speed it stays on, as in 4.2.0: there the airframe weathervanes away from the flight path and the hold is what
			// brings the sideslip back. Bench, MPO, 135 deg AoA at 155 km/h, full stick 1.5 s: same body rates with the hold on or off
			// (p -11.4 / r -7.0 against -11.4 / -7.4 deg/s), peak sideslip 16.6 against 24.3 deg; with it off after the release too the
			// jet hung at 27 deg with every surface at its stop. 16:08 log, 180-185 s (pitch pinned, 240-375 km/h): nose +26 / -31 deg/s
			// with the stick either way, peak sideslip 25 deg on, 28 off.
			revSlipHoldW = 1f;
			if (noseAx)
			{
				float xh = Mathf.Clamp01((Mathf.Abs(alpha) - RevSlipHoldA0) / Mathf.Max(1f, RevSlipHoldA1 - RevSlipHoldA0));
				float inW = Mathf.Clamp01(Mathf.Max(Mathf.Abs(num11raw) / Mathf.Max(0.01f, RevSlipHoldStick), Mathf.Abs(pS) / Mathf.Max(0.1f, RevSlipHoldRate)));
				float xt = Mathf.Clamp01((tas - RevSlipTas0) / Mathf.Max(1f, RevSlipTas1 - RevSlipTas0));
				revSlipHoldW = 1f - xh * xh * (3f - 2f * xh) * inW * (1f - xt * xt * (3f - 2f * xt));
			}
			float num23 = (rSDbg = rSF.Step(revSlipHoldW * (Mathf.Clamp(num22 * (beta - num12), 0f - num8, num8) + num21), 0.12f, dt));
			float num24 = pS * num - num23 * num2;
			float num25 = pS * num2 + num23 * numPhys;
			float num26 = Mathf.Max(1f, Mathf.Max(Mathf.Abs(num24) / num3, Mathf.Abs(num25) / num8));
			pCmd = num24 / num26;
			rCmd = num25 / num26;
			// 2.20.0 (PaceStopNoFin): in the no-fin stop the yaw loop is asked for its brake capacity (the error that makes it, capacity /
			// KYaw, from the measured yaw), where the stopped-roll target alone gives it a fraction of that
			paceStopNFDbg = paceStopNF;
			if (paceStopNF != 0f)
			{
				float capB = Mathf.Max(PaceHoldCapFloor, (paceStopNF > 0f) ? capEst.capZeN : capEst.capZeP);
				float rNF = omega.z * 57.29578f - paceStopNF * capB / Mathf.Max(0.5f, Plugin.KYaw);
				float finKnf = Mathf.Clamp01(yawHealth / 0.5f);
				if ((rNF - rCmd) * paceStopNF < 0f)
				{
					rCmd = Mathf.Lerp(rNF, rCmd, finKnf);
				}
			}
			// 2.4.17 no-chase on a roll brake: while the trajectory brakes the roll (stop, reversal, or a limit coming down), the
			// yaw loop is not asked for more yaw rate than the jet already has on the same side. In a fast roll at AoA the yaw
			// trails its coordinated command (the rudders at their stop: 30 Oct 14:35 log, 267.7 s, r -27 against -36); left to
			// chase it, the yaw kept accelerating for 0.3 s after the stick came back (bench: -33 -> -43 deg/s), and all of that
			// had to be braked again by rudders slewing across their travel. Same AoA band as the roll-stop governor.
			float rCmdCoord = rCmd;
			noChaseDbg = 0f;
			// 2.4.18: only while the coordinated yaw command itself is shrinking. Under a rolling pull the trajectory brakes the
			// roll (the pull governor / trade) while the rising AoA raises the yaw the roll needs (pS sin AoA); holding the yaw there
			// built sideslip (bench, full roll then full aft at 400 km/h: 2.9 -> 4.9 deg in 2.4.17)
			// rate of the yaw the roll itself needs, |pS| sin|AoA|, from the trajectory's own acceleration and the AoA rate
			// (or the AoA not rising fast: at a steady AoA the braking trajectory alone sets it, from its first tick)
			// 2.5.15: retired with the pace governor at level 3 (bench at blend 0.41 / margin 4: stops 5.2 -> 4.1 worst, reversals
			// 8.8 -> 9.3, regressions mostly down 0.2-4 deg, one up 1.6; a wash - one device fewer)
			pDotFF = pTraj.v * num / num26;
			float num27 = aRoll / Mathf.Max(30f, pFull);
			pCmd += Plugin.RollLead * Mathf.Max(0f, num27 - 2.5f) * pDotFF;
			float t2 = Mathf.Max(Mathf.InverseLerp(200f, 300f, ias), Mathf.InverseLerp(YawFFAoA0, YawFFAoA1, Mathf.Abs(alpha)));
			// 2.4.17: the yaw feed-forward follows the coordinated command, not the no-chase hold (its release would step it)
			rDotFF = Mathf.Lerp(Plugin.YawRateFF, 1f, t2) * rDotF.Step((rCmdCoord - rCmdCoordPrev) / dt, 0.1f, dt);
			rCmdCoordPrev = rCmdCoord;
			pCmdPrev = pCmd;
			// 2.4.18: the rate of the yaw-rate command the loop actually follows (after the no-chase hold), for the yaw slew lead
			rCmdRateF += ((rCmdLeadInit ? ((rCmd - rCmdPrev) / Mathf.Max(dt, 1E-04f)) : 0f) - rCmdRateF) * (dt / (0.05f + dt));
			rCmdLeadInit = true;
			rCmdPrev = rCmd;
			if (Mathf.Abs(omega.z * 57.29578f) > num8 && rDotFF * omega.z > 0f)
			{
				rDotFF = 0f;
			}
		}

		public void WriteHeader(TextWriter w)
		{
			w.Write("t,mode,ias,tas,rho,alpha,beta,nz,nAlpha,tw,p,q,r,sp,sr,sy,qB,qRaw,qCmd,qT,pCmd,rCmd,aUp,aDn,betaG,alphaCmd,nCmd,unload,prot,omegaV,phi,theta,tvcCmd,tvcNow,nMaxAero,alphaAtNMax,gAvail,gInt,aInt,flapBias,flapTarget,hlPos,relief,rudderAuth,yawSat,aRoll,aYaw,pMax,pStockRef,pFull,rMax,guard,pSmax,pSTarget,latAcc,rS,stabPitchFrac,gndBrake,revFlow,asymDy,asymAr,asymAy,asymU,asymCap,asymCapEff,asymCapNeg,dMeasR,dMeasY,asymCr,asymCy,asymFFr,asymFFy,asymMr,asymMy,asymFFGate,rollPitchFF,gUnder,pitchPrio,stabRateBound,momentLead,rollTrim,aRollPos,aRollNeg,latReserve,aUpPitch,aDnPitch,aBrakeTgt,stabBranch,tvcAnchor,tvcBudget,rudSide,gsRCmd,gsNws,gsRud,gsHold,gsLineY,asymCapPitch,pitchNeed,pitchNeedDn,sweepWin,coupRollLim,rpGate,rpBand,rollGov,rollGovMargin,iasDot,qPredK,asymCapRaw,asymCapFilt,alphaCmdRaw,nCmdRaw,rateBlendAoA,hiRate,rateW,zoneX,authCap,authCapEff,nLiftMax,hybridDMacc,floatOn,floatS,rateBlendAoANeg,nLiftMin,hiSide,authCapNeg,vmcLim,engOut,fuelTrimCm,yawHealth,capIUp,capIDn,capPushU,slipRate,slipGov,yawAnchor,gRateLaw,rateCmdG,negGate,sharedLat,rollGovPred,loadRev,stickLead,capOn,capQ,slatRelief,hlPosNow,slipAlloc,coupGate,pitchLead,slipGovPull,decel,pullTrade,abShare,abTgt,abOpen,fightK,pullFFBoost,settledWin,rateCapLag,abGate,engAbRatio,engMatchLoss,splitUsage,axWr,axWp,axWy,tgtX,tgtZ,gsWheel,gsIndi,gsRdot,gsNu,gsFyK,stopSlipLim,stopSlipAB,stopSlipU,stopSlipPred,stopSlipMargin,yawSatPitch,yawLead,paceLead,paceHold,slipTol,slipDep,slipAuth,slipStiff,paceRel,tradeAlpha,yawUse,rateBlendEff,paceMarginLead,paceGateZ,paceGateX,unmetX,unmetZ,paceDzH,paceDzAll,paceDxH,paceDxAll,paceRH,pacePH,aYawSeen,pSRaw,paceMLeft,capZP,capZN,capXP,capXN,betaG2,ceWdZ,ceWdX,ceCmZr,ceCmZs,ceCmZf,ceCmXr,ceCmXs,ceCmXf,cePrZPr,cePrZPs,cePrZPf,cePrZNr,cePrZNs,cePrZNf,cePrXPr,cePrXPs,cePrXPf,cePrXNr,cePrXNs,cePrXNf,cePrZPx,cePrZNx,cePrXPz,cePrXNz,ceGZ,ceGX,ceDr,ceDs,ceDf,ceCapZP,ceCapZN,ceCapXP,ceCapXN,ceCapZPraw,ceCapZNraw,ceCapXPraw,ceCapXNraw,ceGUpdZ,ceGUpdX,ceCapSP,ceCapSN,ceCapZeP,ceCapZeN,ceRudW,cePrSPs,cePrSNs,cePrSPf,cePrSNf,stabLlo,stabLhi,stabRlo,stabRhi,stabAnti,stabSym,ceCapTrP,ceCapTrN,ceCapNsP,ceCapNsN,ceWdNs,capSwp,capShd,sweepDel,aYawShed,relFollow,capDn,alphaRoot,betaRoot,airRefCg,relBrakeTd,psAX,psAZ,psDX,psDZ,cePassXP,cePassXN,cePassZP,cePassZN,cePassSP,cePassSN,psLb,psNb,psLp,psNp,psLr,psNr,ppOn,ppQ,ppReach,nuR,nuY,anchR,anchY,unR,unY,fcsUs,estWaitUs,estThr,tRebUs,tAllocUs,tKickUs,tRowUs,tLawUs,tAsymUs,tAsymMomUs,tSlipUs,tSweepUs,tEnvUs,tMassUs,tFlapUs,tGndUs,tVmcUs,ppFloor,sgJoin,sgMass,sgAir,sgFlap,sgSurf,sgTab,sgPre,sgAsym,sgMode,sgGnd,sgLaw,sgApply,sgRow,heapB,gcRan,swMask,gsBrkL,gsBrkR,gsBsL,gsBsR,gsLn,cplU,cplRp,cplRn,cplDn,cplMode,pitchMarg,revSlipHold");
			foreach (SurfaceEffector surface in surfaces)
			{
				w.Write("," + surface.name + "_cmd," + surface.name + "_now," + surface.name + "_aoaNow," + surface.name + "_aoaCmd");
			}
			w.WriteLine();
		}

		// F-22E 3.0.4: WriteRow split in two. CaptureRow runs at the end of tick N on the main thread and boxes every value, in the same
		// order and with the same format strings as 3.0.3's WriteRow; the estimator's columns (and the reach count, which includes the
		// probes' re-solves) are slots that FinishRow fills at the top of tick N+1, once the estimator job of tick N is joined. The text
		// is made by TelemetryRow.WriteTo on the writer thread: string.Format(InvariantCulture, fmt, args) per part, as before.
		private TelemetryRow CaptureRow(float t, float spv, float srv, float syv, float rho)
		{
			TelemetryRow r = new TelemetryRow();
			r.Add("{0:F3},{1},{2:F1},{3:F1},{4:F3},{5:F2},{6:F2},{7:F2},{8:F3},{9:F2},{10:F1},{11:F1},{12:F1},{13:F2},{14:F2},{15:F2},{16:F1},{17:F1},{18:F1},{19:F1},{20:F1},{21:F1},{22:F0},{23:F0},{24:F2},{25:F1},{26:F2},{27},{28},{29:F1},{30:F1},{31:F1},{32:F2},{33:F2}", t, (int)Mode, ias, tas, rho, alpha, beta, nz, nAlpha, tw, omega.x * 57.29578f, omega.y * 57.29578f, omega.z * 57.29578f, spv, srv, syv, qB, qRaw, qCmd, qT, pCmd, rCmd, aUp, aDn, betaG, alphaCmd, nCmd, unload ? 1 : 0, prot ? 1 : 0, omegaV, phi, theta, tvc.cmd, tvc.now);
			r.Add(",{0:F2},{1:F0},{2:F2},{3:F2},{4:F2},{5:F1},{6:F1},{7:F2},{8:F1},{9:F2},{10:F2},{11:F0},{12:F0}", nMaxAero, alphaAtNMax, gAvail, gInt, aInt, flaps.flapBias, flaps.flapTarget, flaps.hlPos, flaps.relief, rudderAuth, yawSat, aRoll, aYaw);
			r.Add(",{0:F0},{1:F0},{2:F0},{3:F0},{4:F2},{5:F0},{6:F0},{7:F0},{8:F1},{9:F2}", pMaxDbg, pStockRef, pFull, rMaxDbg, guardDbg, pSmaxDbg, pSTgtDbg, latAccDbg, rSDbg, stabPitchFrac);
			r.Add(",{0:F2},{1}", groundBrake, revFlow ? 1 : 0);
			r.Add(",{0:F3},{1:F1},{2:F1},{3:F2},{4:F1},{5:F1},{6:F1},{7:F1},{8:F1}", asymDy, asymArNow, asymAyNow, asymUNow, Mathf.Min(asymCapPos, 999f), Mathf.Min(asymCapPosEff, 999f), Mathf.Max(asymCapNegEff, -999f), dMeasR, dMeasY);
			r.Add(",{0:F0},{1:F0},{2:F1},{3:F1},{4:F1},{5:F1}", asymCrNow, asymCyNow, asymFFr, asymFFy, AccRoll(asymMNow), AccYaw(asymMNow));
			r.Add(",{0:F2}", asymFFGate);
			r.Add(",{0:F1},{1:F1}", rollPitchFF * IaInv[1, 1] * 57.29578f, gUnderDbg);
			r.Add(",{0:F1},{1},{2:F2},{3:F0},{4:F0},{5:F0},{6:F2},{7:F0},{8:F0},{9:F0},{10}", pitchPrio, stabRateBound ? 1 : 0, mLeadGate * mLeadAgree, dTrimR, aRollPos, aRollNeg, latReserve, aUpPitch, aDnPitch, aBrakeTgt, stabBranch);
			r.Add(",{0:F2},{1:F2},{2:F1}", tvcAnchor, tvcBudgetGate, rudSideDbg);
			r.Add(",{0:F2},{1:F2},{2:F2},{3},{4:F2},{5:F1},{6:F2},{7:F2},{8},{9:F0}", gsRCmd, gsNwsDeg, gsRud, gsHold ? 1 : 0, gsLineY, Mathf.Min(asymCapPitch, 999f), pitchNeed, pitchNeedDn, sweepHoldWinDbg ? 1 : 0, coupRollLim);
			r.Add(",{0:F2},{1:F1},{2:F2},{3:F2}", rpGate, rpBand, rollGov, rollGovMargin);
			r.Add(",{0:F2},{1:F3},{2:F1},{3:F1}", iasDotF, qPredK, Mathf.Clamp(asymCapPos, -99f, 99f), Mathf.Clamp(asymCapFilt, -99f, 99f));
			r.Add(",{0:F1},{1:F2},{2:F1},{3},{4:F2},{5:F2},{6:F1},{7:F1},{8:F2},{9:F1}", alphaCmdRaw, nCmdRaw, rateBlendAoA, hiRate ? 1 : 0, rateW, zoneX, Mathf.Min(authCap, 99f), Mathf.Min(authCapEff, 99f), nLiftMax, hybridDM * IaInv[1, 1] * 57.29578f);
			r.Add(",{0},{1:F2}", floatOn ? 1 : 0, floatS);
			r.Add(",{0:F1},{1:F2},{2},{3:F1}", rateBlendAoANeg, nLiftMin, hiRate ? hiSide : 0, Mathf.Max(authCapNegEff, -99f));
			r.Add(",{0:F2},{1},{2:F1},{3:F2}", vmcLim, engOut ? 1 : (engAsym ? 2 : 0), fuelTrimOn ? (fuelTrimDelta * 100f) : 0f, yawHealth);
			r.Add(",{0:F2},{1:F2},{2:F2}", capIUp, capIDn, capPushU);
			r.Add(",{0:F1},{1:F2},{2:F1},{3},{4:F1}", slipRateDbg, slipGovDbg, Mathf.Clamp(yawAnchorDbg, -999f, 999f), Plugin.GRateLaw ? 1 : 0, rateCmdG);
			r.Add(",{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5},{6:F1},{7:F2},{8:F2},{9:F2},{10:F2}", negGateDbg, sharedLatDbg, rollGovPredDbg, loadRevDbg, stickLeadDbg, capOn ? 1 : 0, capQ, slatRelief, flaps != null ? flaps.hlPos : 0f, (YawPrioHi > 0f && Plugin.SlipYawWeightOn) ? YawHiSlipGate() * Mathf.Clamp01((Mathf.Abs(alpha) - 20f) / 20f) : 0f, coupGateHeld);
			r.Add(",{0:F3},{1:F2},{2:F2},{3:F0},{4:F3},{5:F2},{6:F2},{7:F2},{8:F2}", pitchLeadDbg, slipGovPullDbg, decelDbg, pullTradeDbg, airbrakeShare, AirbrakeOpenTarget, airbrakeOpenNow, alloc.fightK, pullFFBoost);
			r.Add(",{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2}", settledWinDbg ? 1 : 0, rateCapLagDbg, abGateOn ? ((engOut || matchF <= 0f) ? 1f : matchF) : 0f, engAbRatio, engMatchLoss * 0.001f, splitUsage);
			r.Add(",{0:F2},{1:F2},{2:F2},{3:F0},{4:F0}", axWDbg.x, axWDbg.y, axWDbg.z, dbgTargetX, dbgTargetZ);
			r.Add(",{0:F2},{1},{2:F1},{3:F1},{4:F2}", gsWheelDbg, gsIndiOn ? 1 : 0, gsRdotF, gsNuDbg, gsFyF * 0.001f);
			r.Add(",{0:F0},{1:F0},{2:F2},{3:F2},{4:F1},{5:F1},{6:F3}", Mathf.Min(stopSlipLimDbg, 999f), stopSlipABDbg, stopSlipUDbg, stopSlipPredDbg, stopSlipMarginDbg, yawSatPitchDbg, yawLeadDbg);
			r.Add(",{0:F1},{1:F1},{2:F1},{3:F1},{4:F1},{5:F2},{6:F2},{7:F1}", paceLeadDbg, paceHoldDbg, slipTolDbg, slipDepDbg, slipAuthDbg, slipStiffDbg, paceRelDbg, tradeAlphaDbg);
			r.Add(",{0:F2},{1:F2},{2:F1}", yawUseAllF, rateBlendEffDbg, paceMarginLeadDbg);
			r.Add(",{0:F2},{1:F2},{2:F0},{3:F0},{4:F0},{5:F0},{6:F0},{7:F0},{8:F1},{9:F1},{10:F0},{11:F0}", paceGateZDbg, paceGateXDbg, allocUnmet.x, allocUnmet.z, paceDzHDbg, paceDzAllDbg, paceDxHDbg, paceDxAllDbg, paceRHDbg, pacePHDbg, aYawSeen, paceTgtRaw);
			r.Add(",{0:F1},{1:F0},{2:F0},{3:F0},{4:F0},{5:F1}", paceMLeftDbg, paceCapZPDbg, paceCapZNDbg, paceCapXPDbg, paceCapXNDbg, paceBetaG);
			{
				r.ceA = r.Slot();
				r.ceB = r.Slot();
				r.ceC = r.Slot();
				r.ceD = r.Slot();
				r.ceE = r.Slot();
				r.ceF = r.Slot();
				r.Add(",{0:F1},{1:F1},{2:F1},{3:F1},{4:F4},{5:F4}", StabWin(0, true), StabWin(0, false), StabWin(1, true), StabWin(1, false), StabCost(true), StabCost(false));
				r.ceG = r.Slot();
				r.Add(",{0:F1},{1:F1},{2:F1},{3:F1},{4:F2},{5:F1}", capSweepDbg, capShedDbg, sweepDelF, aYawShedDbg, relFollowDbg, yawSatAlphaRateDn);
				r.Add(",{0:F2},{1:F2},{2}", alphaRoot, betaRoot, AirDataAtCg ? 1 : 0);   // 2.30.0
				r.Add(",{0:F2}", releaseBrakeTdDbg);   // 2.31.0
				r.Add(",{0:F1},{1:F1},{2:F1},{3:F1},{4:F1},{5:F1},{6:F1},{7:F1},{8:F1},{9:F1}", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);   // 3.1.0: PassiveStep archived, columns kept at 0   // 2.31.2
				r.Add(",{0:F3},{1:F3},{2:F3},{3:F3},{4:F3},{5:F3}", 0f, 0f, 0f, 0f, 0f, 0f);   // 3.1.0: PassiveStep archived
				r.Add(",{0},{1:F1}", pushPlanOn ? 1 : 0, pushPlanOn ? pushPlanQNow : 0f);   // 2.36.0
				r.reachSlot = r.Slot();   // 2.41.0: allocations re-solved against the stabs' reach since the last row (3.0.4: filled in FinishRow)
				r.reachMain = alloc.reachMoves;
			}
			// F-22E 3.0.1: what the roll / yaw rate loops ask for (deg/s^2; ~0 with the stick centred once the jet holds its command),
			// how far the tables' roll / yaw columns were off their live moment this tick before anchoring (deg/s^2, summed), and what
			// the allocation left unmade of its target (deg/s^2, the main solve)
			{
				Vector3 anchSum = Vector3.zero;
				foreach (SurfaceEffector sA in surfaces)
				{
					if (sA.active)
					{
						anchSum += sA.anchorDbg;
					}
				}
				r.Add(",{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2}", omegaDotDes.x * 57.29578f, omegaDotDes.z * 57.29578f, anchSum.x * IaInv[0, 0] * 57.29578f, anchSum.z * IaInv[2, 2] * 57.29578f, -allocResMain.x, -allocResMain.z);
			}
			r.timeSlot = r.Slot();   // 3.0.4: fcsUs, estWaitUs, estThr (3.1.0: + tRebUs..tRowUs)
			r.timeSlot2 = r.Slot();   // 3.1.1: tLawUs..tVmcUs
			r.Add(",{0:F1}", pushPlanOn ? pushFloorDbg : 0f);   // 3.1.4: the push-over minimum rate in force
			r.timeSlot3 = r.Slot();   // 4.1.3: sgJoin..sgRow, heapB, gcRan, swMask
			// 4.1.5: the brake each main is asked for by the yaw effector (-1 = not in the loop), the brake it applies (lagged), and the
			// light-nose hold
			float bsL = 0f;
			float bsR = 0f;
			if (gsAnyContact)
			{
				Vector3 rightAx = ((Component)(object)ac).transform.right;
				foreach (LandingGear g in gears)
				{
					if (g != null && !Acc.LG_steering(g) && Acc.LG_braked(g))
					{
						if (Vector3.Dot(g.transform.position - cg, rightAx) < 0f)
						{
							bsL = Acc.LG_brakeStrength(g);
						}
						else
						{
							bsR = Acc.LG_brakeStrength(g);
						}
					}
				}
			}
			r.Add(",{0:F2},{1:F2},{2:F2},{3:F2},{4}", gsBrkDbgL, gsBrkDbgR, bsL, bsR, gsLnDbg ? 1 : 0);
			r.Add(",{0:F2},{1:F1},{2:F1}", (cplActive && cplLoaded) ? cplUNow : 0f, (cplActive && cplLoaded) ? cplRpNow : 0f, (cplActive && cplLoaded) ? cplRnNow : 0f);
			r.Add(",{0:F1},{1},{2:F1}", (cplPitchGate && cplLoaded) ? cplDnNow : 0f, (cplActive && cplLoaded) ? (cplTvMode ? 1 : 2) : 0, asymPitchMargDbg);
			r.Add(",{0:F2}", revSlipHoldW);   // 4.3.0: the sideslip hold's share in reversed flow (1 = full)
			r.threaded = estJobF != null && estJobF.Pending;
			foreach (SurfaceEffector surface in surfaces)
			{
				r.Add(",{0:F2},{1:F2},{2:F1},{3:F1}", surface.cmd, surface.now, surface.localAoANow, surface.localAoACmd);
			}
			return r;
		}

		private void FinishRow(TelemetryRow r)
		{
			CapacityEstimator ce = capEst;
			r.Fill(r.ceA, ",{0:F1},{1:F1},{2:F1},{3:F1},{4:F1},{5:F1},{6:F1},{7:F1}", ce.wdZ, ce.wdX, ce.cmZr, ce.cmZs, ce.cmZf, ce.cmXr, ce.cmXs, ce.cmXf);
			r.Fill(r.ceB, ",{0:F1},{1:F1},{2:F1},{3:F1},{4:F1},{5:F1},{6:F1},{7:F1},{8:F1},{9:F1},{10:F1},{11:F1}", ce.prZPr, ce.prZPs, ce.prZPf, ce.prZNr, ce.prZNs, ce.prZNf, ce.prXPr, ce.prXPs, ce.prXPf, ce.prXNr, ce.prXNs, ce.prXNf);
			r.Fill(r.ceC, ",{0:F1},{1:F1},{2:F1},{3:F1}", ce.prZPx, ce.prZNx, ce.prXPz, ce.prXNz);
			r.Fill(r.ceD, ",{0:F3},{1:F3},{2:F2},{3:F2},{4:F2}", ce.gZ, ce.gX, ce.dr, ce.ds, ce.df);
			r.Fill(r.ceE, ",{0:F1},{1:F1},{2:F1},{3:F1},{4:F1},{5:F1},{6:F1},{7:F1},{8},{9}", ce.capZP, ce.capZN, ce.capXP, ce.capXN, ce.capZPraw, ce.capZNraw, ce.capXPraw, ce.capXNraw, ce.gUpdZ, ce.gUpdX);
			r.Fill(r.ceF, ",{0:F1},{1:F1},{2:F1},{3:F1},{4:F2},{5:F1},{6:F1},{7:F1},{8:F1}", ce.capSP, ce.capSN, ce.capZeP, ce.capZeN, ce.rudWDbg, ce.prSPs, ce.prSNs, ce.prSPf, ce.prSNf);
			r.Fill(r.ceG, ",{0:F1},{1:F1},{2:F1},{3:F1},{4:F1}", ce.capTrZeP, ce.capTrZeN, ce.capZeNsP, ce.capZeNsN, ce.wdZNs);
			// the main allocator's count at capture (tick N's real solves; in the synchronous path also its probes) + the worker's probes
			int reachTot = r.reachMain + ((estJobF != null) ? estJobF.WorkerReachMoves : 0);
			r.Fill(r.reachSlot, ",{0}", reachTot - reachLogPrev);
			reachLogPrev = reachTot;
			r.Fill(r.timeSlot, ",{0:F0},{1:F0},{2},{3:F0},{4:F0},{5:F0},{6:F0}", lastTickUs, r.threaded ? estJobF.lastWaitUs : 0f, r.threaded ? 1 : 0, TicksToUs(phRebuild), TicksToUs(phAlloc), TicksToUs(phKick), TicksToUs(phRow));
			r.Fill(r.timeSlot2, ",{0:F0},{1:F0},{2:F0},{3:F0},{4:F0},{5:F0},{6:F0},{7:F0},{8:F0},{9:F0}", TicksToUs(phLaws), TicksToUs(phAsym), TicksToUs(phAsymMom), TicksToUs(phSlip), TicksToUs(phSweep), TicksToUs(phEnv), TicksToUs(phMass), TicksToUs(phFlaps), TicksToUs(phGround), TicksToUs(phVmc));
			float[] g = segUs;
			r.Fill(r.timeSlot3, ",{0:F0},{1:F0},{2:F0},{3:F0},{4:F0},{5:F0},{6:F0},{7:F0},{8:F0},{9:F0},{10:F0},{11:F0},{12:F0},{13},{14},{15}", g[0], g[1], g[2], g[3], g[4], g[5], g[6], g[7], g[8], g[9], g[10], g[11], g[12], memDelta, gcDuring, SwitchMask());
		}
	}
}
