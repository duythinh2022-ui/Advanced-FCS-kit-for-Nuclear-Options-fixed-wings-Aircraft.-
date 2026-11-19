using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// Vortex 0.1.0 (Tom, 16 Nov 2026: "make vortex fcs with f-35b stovl/vtol control laws"; conversion by holding Flight Assist,
	// the throttle working like the real F-35B's).
	//
	// The FS-20's lift system in the game (SwivelDuctSystem, read from the game's code):
	//   - a three-bearing swivel nozzle: swivelPosition 0..1 (0 = aft, 1 = ~90 deg down), slewed at swivelSpeed (0.25 /s) toward
	//     1 - controlInputs.customAxis1; the first bearing also turns yawFactor x controlInputs.yaw (+-15 deg) at every speed;
	//   - a lift fan whose doors open whenever customAxis1 < 1; with the doors open its force is chosen by the game to cancel the
	//     main nozzle's pitch moment about the CG, plus doors x controlInputs.pitch x pitchFactor (-25 kN) x the RPM ratio;
	//     the engine loses parasiticLoss (25 kN) x swivel of thrust and has no afterburner past ~63 deg of swivel;
	//   - two roll posts (8 kN each x the engine's thrust ratio x controlInputs.roll, one side at a time) with the swivel past 0.1
	//     and below 120 m/s; above maxSwivelAirspeed (139 m/s) the game swings the nozzle aft whatever it is told.
	// This file turns that hardware into allocator effectors (lift-fan pitch, roll posts, the nozzle's yaw bearing) and flies
	// the jet with the F-35B's unified STOVL control laws (VAAC Harrier "Unified": throttle = acceleration around a centre
	// detent / ground speed around an aft detent, stick = flight-path rate blending to height rate, lateral stick = roll rate ->
	// bank angle -> translational rate, pedals = sideslip -> yaw rate, pitch attitude chosen by the FCS, hands-off holds).
	// The FCS's own INDI rate loop and allocator do the attitude control; the translational loops here are incremental too
	// (the propulsive force asked = the force it makes now + mass x the acceleration error), so the airframe's own lift and
	// drag never have to be modelled for them.

	internal interface IStovlHardware
	{
		bool Valid { get; }

		float Doors { get; }

		float Swivel { get; }

		float SwivelRate { get; }

		float MaxSwivelAirspeed { get; }

		Vector3 SurfaceVelocity { get; }

		// Vortex 0.4.1 / 0.4.6: 0..1, how much of the nearest ship's speed SurfaceVelocity carries (StovlShipRelative, within its range); the HUD marks it
		float ShipFrameWeight { get; }

		Transform MainThrustT { get; }

		float NozzleThrust { get; }

		float DryThrust { get; }

		float RpmRatio { get; }

		float ThrustRatio { get; }

		float MaxThrust { get; }

		float ParasiticLoss { get; }

		float AltFactor { get; }

		float MinRpm { get; }

		float MaxRpm { get; }

		// 0.2.1: the engine's spool rate (rpm/s), for the vertical axis's response time
		float SpoolRate { get; }

		Vector2 Remap { get; }

		float Condition { get; }

		int Bearings { get; }

		Transform BearingT(int i);

		Vector3 BearingMin(int i);

		Vector3 BearingMax(int i);

		Vector3 BearingYaw(int i);

		float BearingCurve(int i, float s);

		Transform FanThrustT { get; }

		// Vortex 0.5.0: the lift fan's vane box. The fan's thrust line now (deg from the fan's axis, + = the exhaust turned aft,
		// which pushes the jet forward; the game's own fixed line is ~-10), and that fixed line's direction (world)
		bool HasVanes { get; }

		float VaneNow { get; }

		Vector3 FanRestForward { get; }

		float FanPitchFactor { get; }

		int Posts { get; }

		Transform PostT(int i);

		float PostMax(int i);

		float PostRollFactor(int i);

		float PostMinSwivel(int i);

		float PostMaxSpeed(int i);

		float AircraftSpeed { get; }

		Vector3 CenterOfMass { get; }
	}

	internal enum StovlState
	{
		Off,
		Converting,
		On,
		Reconverting
	}

	internal sealed class StovlSystem
	{
		// set by the game plugin (SwivelLink) or the bench; null -> no STOVL on this jet
		public static Func<Aircraft, IStovlHardware> HardwareFactory;

		// HUD text (the game's AircraftActionsReport for the local jet); the bench prints it
		public static Action<Aircraft, string> Report;

		public readonly IStovlHardware hw;

		public readonly Aircraft ac;

		public StovlState state;

		// the FCS controls the swivel system this tick (FCS engaged, human pilot)
		public bool owns;

		// outputs to the game (the hook swaps them in for SwivelDuctSystem.FixedUpdate, and the engine's throttle read)
		public float axis1Out = 1f;

		public float fanIn;

		public float postIn;

		public float yawIn;

		public bool throttleOverride;

		public float throttleOut;

		// effectors
		public LiftFanEffector fan;

		public RollPostEffector post;

		public SwivelYawEffector yaw;

		// 0.3.6: the Detents mod stays suspended after a reconversion until the lever is out of the afterburner range
		public bool detentsAbLatch;

		// the unified-law outputs (deg/s, aero frame: p roll right, q nose up, r nose right)
		public float pOut;

		public float qOut;

		public float rOut;

		// law state
		private float hRef;

		private bool hHold;

		private float gamRef;

		private float vRef;

		private bool vHold;

		private float psiRef;

		private bool psiHold;

		private float phiHoldRef;

		private bool phiHold;

		private Vector3 posRef;

		private bool posHold;

		private float thetaRefF;

		private float thetaHold;

		public float wCtolBlend;

		private float alphaRefF;

		private bool lawInit;

		private float wowT;

		private float airT;

		private bool liftIntent;

		// on the wheels in STOVL mode (GroundIntent): touchdown latch (engine at idle until the throttle has been in the aft
		// detent once), the pilot throttle and IAS for the ground nozzle schedule
		private bool onGround;

		private bool wasAir;

		private bool touchGrace;

		private float touchT;

		private float yawLiftK = 1f;

		private float vPrio = 1f;

		private float hInt;

		private float sHov = 0.85f;

		private bool groundIdleLatch;

		// Vortex 0.3.7: the lowest lever position since the touchdown idle latched (a push 0.05 forward of it releases it)
		private float latchLeverMin;

		private bool latchLeverSet;

		private float gThrottle;

		private float gIas;

		private float swivelTarget;

		// hover regime: extra pitch attitude for the longitudinal force the swivel range cannot give (deg)
		private float thetaAssist;

		// the hover-regime yaw-rate command (pedals), slewed within the yaw authority
		private float psiDotPedal;

		// Vortex 0.5.6: the pedal pirouette (StovlPedalPirouette; see the translation loop)
		private bool pirOn;

		private Vector3 pirDir;

		// Vortex 0.5.8: the pirouette's translation part (only while the pedal is held) and the skid it keeps
		private bool pirXOn;

		private Vector3 pirSkid;

		public float dbgPir;

		public float dbgPirSlipLim;

		// Vortex 0.1.1: the attitude loops' angular-acceleration budgets are this share of what the effectors can do now
		// (LawIn.authority, live from the tables) instead of 0.1.0's fixed 25 / 15 / 20 deg/s^2 (bench hover: ~60 roll, ~37
		// yaw, ~44 pitch available). The rest is the margin for the couplings and for stopping on target.
		internal const float AuthorityShare = 0.75f;

		// 0.2.8: the share of the hover's roll authority a lateral load may take before the release guard and the conversion
		// guard step in
		internal const float GuardShare = 0.9f;

		// the throttle as the laws read it: at conversion the lever is taken as neutral (aft detent slow, centre detent at
		// speed) until it is moved 0.05 from where it was, so a conversion with the throttle at idle does not command 30 kt
		// backwards (0.1.0 lifted off and went backwards)
		private bool thrLatchPending;

		private bool thrLatched;

		// 0.2.6: set when a conversion to STOVL completes: the throttle module (ThrottleDetent, keyboard / button throttles)
		// moves the lever itself to the aft detent (zero ground speed), so the reduce key goes on into the backward range at
		// once. With an analog throttle the lever can't be moved and the latch above reads it as the aft detent instead.
		public bool leverPark;

		public bool WheelsNow => wheelsNow;

		private float thrLatchVal;

		private bool slowRegime = true;

		public bool SlowRegime => slowRegime;

		public float LeverSeen => leverSeen;

		private float EffThrottle(float raw)
		{
			if (!thrLatched)
			{
				return raw;
			}
			// 0.1.9: the aft detent in both regimes: decelerate at speed (0.1.3-0.1.8 read it as the centre detent there, hold
			// speed), zero ground speed once slow, so a conversion with the lever untouched slows the jet to a hover
			return Plugin.StovlAftDetent;
		}

		// Vortex 0.3.3: lever -> thrust share (0 idle .. 1 full dry) in the CTOL blend: zero through the aft detent and behind it,
		// linear from the detent's forward edge to the stop
		internal static float CtolLeverThrust(float lever)
		{
			float z = Mathf.Clamp(Plugin.StovlAftDetent + Plugin.StovlDetentWidth, 0f, 0.95f);
			return Mathf.Clamp01((lever - z) / (1f - z));
		}

		// throttle that gives a dry thrust (Turbojet: rpm target from the remapped throttle, thrust ratio from rpm); the game's
		// spool is a 1 s lag (rpm += clamp(target - rpm, +-spoolRate) x dt): the target is over-driven so the RPM moves as a
		// 0.35 s lag toward the one wanted (within idle..max), as the vertical axis needs. Vortex 0.1.1: no afterburner in
		// STOVL mode (as the F-35B with its lift fan clutched in): dry thrust only. (0.2.9: out of Law, for the CTOL blend's
		// thrust floor too)
		private float ThrottleForThrust(float tReq, float sNow)
		{
			float tMaxDry = Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * sNow * vnDriveNow) * Mathf.Max(0.05f, hw.AltFactor));
			float ratio = tReq / tMaxDry;
			float rpm = 0.85f * hw.MinRpm + ratio * (hw.MaxRpm - hw.MinRpm);
			float rpmNow = hw.RpmRatio * hw.MaxRpm;
			rpm = rpmNow + (rpm - rpmNow) / RpmLead;
			float tN = (rpm - hw.MinRpm) / Mathf.Max(1f, (hw.MaxRpm - hw.MinRpm) * (hw.Condition * 0.5f + 0.5f));
			Vector2 rm = hw.Remap;
			return Mathf.Clamp(rm.x + tN * (rm.y - rm.x), 0f, rm.y);
		}

		// a rate toward an angle error e (deg): k x e, but never faster than one that can be stopped on the angle with the
		// acceleration a (sqrt(2 a |e|)), and within vmax
		private static float Stop(float e, float k, float a, float vmax)
		{
			float lim = Mathf.Sqrt(2f * a * Mathf.Abs(e));
			return Mathf.Clamp(Mathf.Clamp(k * e, -lim, lim), -vmax, vmax);
		}

		private Vector3 fPropF;

		private bool fPropInit;

		private float sCLa;

		public float dbgSCLa => sCLa;

		private float throttleCmdF;

		private float reqToggleT = -10f;

		// telemetry
		public float dbgGs;

		public float dbgVs;

		public float dbgADesH;

		public float dbgADesV;

		public float dbgThetaRef;

		public float dbgAlphaRef;

		public float dbgThrustReq;

		public float dbgSwivelTgt;

		public float dbgLamV;

		public float dbgLamT;

		public float dbgGsCmd;

		public float dbgHdotCmd;

		public float dbgPhiCmd;

		public float dbgGamRef;

		public Vector3 dbgAuth;

		// Vortex 0.3.9: the pedal's full yaw rate this tick (deg/s, telemetry stYawLim)
		public float dbgYawLim;

		private float YawRateMax(float aYaw)
		{
			float r = (Plugin.StovlYawStopTime > 0f) ? (aYaw * Plugin.StovlYawStopTime) : Plugin.StovlHoverYawRate;
			dbgYawLim = r;
			return r;
		}

		public float dbgAUp;

		// Vortex 0.4.0 telemetry (the authority / physics quantities of Vortex_STOVL_authority_TRC_plan.md)
		public float dbgAHlevel;

		public float dbgAVres;

		public float dbgAUpBody;

		public float dbgATiltLeft;

		public float dbgLamL;

		public float dbgLamB;

		public float dbgThetaAssist;

		public float dbgPhiTrc;

		public float dbgVSide;

		public float dbgUFwd;

		public float dbgKRollS = 1f;

		// Vortex 0.4.0 (Vortex_STOVL_authority_TRC_plan.md, groups B-D; Tom, 17 Nov 2026 17:25). Telemetry: the side tilt rate's
		// authority term and the rate used, the fore-aft tilt rate, the hover's stopping acceleration (low-passed), full lateral
		// stick's ground speed, the thrust fit's fore-aft weight
		public float dbgTiltRateA;

		public float dbgTiltRate;

		public float dbgTiltRateU;

		public float dbgAStop;

		public float dbgTrcVmax;

		public float dbgWz = 0.25f;

		private float aTrcStopF;

		private bool aTrcStopInit;

		private float tiltRateFwdLast = 10f;

		private float aTiltRollF;

		private float aTiltPitchF;

		private bool aTiltInit;

		// Vortex 0.4.2: the lift fan's force per unit of its pitch input now (N), for the fore-aft tilt's lift limit
		private float fanFNow = 1f;

		public float dbgTiltPitchLift;

		public float dbgKBank;

		// Vortex 0.4.2: the weak roll direction's stopping power now, and the low-passed roll authority over the whole travel
		private float aRollWeakNow;

		private float aTiltFullF;

		private bool aTiltFullInit;

		internal static float TiltAuthTau = 3f;


		private float fitWz = 0.25f;

		// group C: the wing-share fade's band, airspeed in the plane of symmetry over the speed at which the wing alone carries the
		// jet at StovlWingAoAMax (the square root of the wing's share of the weight): 15 and 45 m/s on the reference jet (11 880 kg,
		// sea level, 70.5 m/s), so the reference reproduces 0.3.15's Smooth(15, 45 m/s) exactly and the band moves with
		// sqrt(mass / density) elsewhere
		internal static float WingShare0 = 0.2126f;

		internal static float WingShare1 = 0.6379f;

		// the same ratio's band for the velocity-vector roll and the sideslip loop (whether the airspeed has an aerodynamic meaning
		// yet): 10 and 25 m/s on the reference jet. (Tried first on the slip share, the airframe's restoring yaw at 45 deg of
		// sideslip over the yaw budget, with the stiffness worked out below the slip probe's 30 m/s: on the bench decel it is not
		// monotonic there, sqrt 0.16 / 0.31 / 0.23 / 0.30 at 12 / 15 / 20 / 25 m/s and 0.61 at 30, as the rudder flare fades and the
		// AoA moves, so a fade on it would come and go. Not built.)
		internal static float AeroShare0 = 0.14175f;

		internal static float AeroShare1 = 0.35438f;

		// the speed (m/s, in the plane of symmetry) at which the wing alone carries the jet at StovlWingAoAMax
		private float WingBorneSpeed(ref LawIn I)
		{
			return Mathf.Sqrt(2f * Mathf.Max(1f, I.mass) * 9.81f / Mathf.Max(0.001f, I.rho * sCLa * Mathf.Max(1f, Plugin.StovlWingAoAMax) * 0.0174532924f));
		}

		// group D: the lowest fore-aft weight of the thrust fit (the vertical weight is 1)
		internal static float VhFitWzMin = 0.01f;

		// group B: the rate (deg/s) of a translation tilt. 0.1.6-0.3.15's fixed rate (StovlTrcBankRate, which is there for the
		// height: the thrust has to keep up with the lift a moving tilt takes away) stays as the ceiling; under it, no faster than
		// the axis can start and stop within StovlTiltStopTime at its angular-acceleration budget now (aAxis, deg/s^2; the
		// pedal's StovlYawStopTime rule), so a jet short of authority tilts slower. StovlTiltStopTime 0: the fixed rate alone.
		// (Built first without the ceiling, and with a thrust term in its place, the lift lost during the thrust's lag held to a
		// reserve: the budget is larger at speed, where the surfaces add to it, and on the Buffed jet the last of a deceleration
		// then pitched at 13-16 deg/s for 10 and lost 0.19-0.24 m of height for 0.08, the thrust term not binding at those tilts.
		// Bench, 17 Nov 2026.)
		// Vortex 0.4.2: the translation bank loop's gain (1 / s)
		private static float BankLoopGain()
		{
			return Plugin.StovlBankGainFromMargin ? (1f / Mathf.Clamp(Plugin.StovlTrcLagMargin, 0.2f, 1f)) : 2.5f;
		}

		private float TiltRate(float aAxis, float fixedRate)
		{
			if (Plugin.StovlTiltStopTime <= 0f)
			{
				return fixedRate;
			}
			return Mathf.Min(fixedRate, Mathf.Max(2f, aAxis * Plugin.StovlTiltStopTime));
		}

		public float dbgGVy;

		public float dbgTMx;

		public float dbgSinkLim;

		public float dbgClimbLim;

		// climb acceleration available (m/s^2) and the radar altitude reading on the wheels (learned at each touchdown)
		private float aUpAvail = 3f;

		private const float StopLag = 1f;

		private float aStop = 1f;

		private float vStopNow = 99f;

		private float vTdNow = 5f;

		private float raGround = 2f;

		private int raGroundN;

		public float pDotOut;

		public float rDotOut;

		private bool rateFFInit;

		public float dbgGam;

		public float dbgNReq;

		public float dbgLNow;

		public bool LawActive => state == StovlState.On;

		public bool LiftIntent => liftIntent;

		public bool HardwareOut => hw != null && (hw.Doors > 0.001f || hw.Swivel > 0.001f);

		private StovlSystem(Aircraft a, IStovlHardware h)
		{
			ac = a;
			hw = h;
		}

		public static StovlSystem TryCreate(Aircraft a)
		{
			if (!Plugin.StovlEnabled || HardwareFactory == null)
			{
				return null;
			}
			IStovlHardware h = null;
			try
			{
				h = HardwareFactory(a);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Vortex FCS STOVL: lift system not read (" + ex.Message + "); STOVL laws off.");
			}
			if (h == null || !h.Valid)
			{
				return null;
			}
			return new StovlSystem(a, h);
		}

		public void BuildEffectors(TvcEffector tvc, List<Effector> effs)
		{
			fan = new LiftFanEffector(this);
			post = new RollPostEffector(this);
			yaw = new SwivelYawEffector(tvc, this);
			effs.Add(yaw);
			effs.Add(fan);
			effs.Add(post);
		}

		// wing lift slope (N per rad per Pa of dynamic pressure) of the lifting parts, for the pitch-attitude choice
		public void SetLiftSlope(float s)
		{
			sCLa = Mathf.Max(1f, s);
		}

		private static float Smooth(float a, float b, float x)
		{
			float t = Mathf.Clamp01((x - a) / (b - a));
			return t * t * (3f - 2f * t);
		}

		private void Say(string s)
		{
			Plugin.Log.LogInfo("Vortex FCS STOVL: " + s.Replace("<b>", "").Replace("</b>", "") + $" (IAS {hw.AircraftSpeed * 3.6f:F0} km/h TAS, doors {hw.Doors:F2}, swivel {hw.Swivel:F2})");
			try
			{
				Report?.Invoke(ac, s);
			}
			catch
			{
			}
		}

		// the Flight Assist hold (the game's auto-hover toggle)
		// Vortex 0.1.3: STOVL is a switch (selected). With it on, the jet is in CTOL while the wing holds level flight and in
		// STOVL once it can't (0.1.4: on the AoA the wing needs, AutoTransition; 0.1.3 used 350 / 300 km/h), converting by
		// itself between them; turned on while the wing still holds it is armed and converts once the jet slows. Turned off, the jet
		// reconverts (not below StovlReconversionMinIas in the air) and stays CTOL. StovlAutoTransition = false: 0.1.2's
		// manual toggle (conversion up to StovlConversionMaxIas, no automatic reconversion). 0.1.8: the manual toggle is the
		// default (StovlAutoTransition false), as the F-35B's STOVL button: convert at 460 km/h or less, reconvert at 240 or more
		// (or on the wheels), a press during the door travel reverses it.
		public bool selected;

		public void RequestToggle(float time, float ias)
		{
			if (time - reqToggleT < 0.5f)
			{
				return;
			}
			reqToggleT = time;
			bool auto = Plugin.StovlAutoTransition;
			if (!selected || !auto)
			{
				switch (state)
				{
				case StovlState.Off:
				case StovlState.Reconverting:
					if (auto && !onGround && ias > Plugin.StovlAutoStovlIas / 3.6f)
					{
						selected = true;
						Say($"STOVL <b>armed</b>: converts below {Plugin.StovlAutoStovlIas:F0} km/h");
						return;
					}
					if (ias > Plugin.StovlConversionMaxIas / 3.6f)
					{
						Say($"STOVL conversion <b>inhibited</b> above {Plugin.StovlConversionMaxIas:F0} km/h");
						return;
					}
					// 0.1.8: and under the true airspeed the STOVL laws can hold (their speed cap, 11 m/s under the game's swivel
					// cut-off, which is on true airspeed): at altitude the IAS limit alone let a conversion start at a TAS the jet
					// would be thrown out of at once (6 km: 460 km/h IAS is ~610 TAS, the cut-off 500)
					if (hw != null && hw.Valid && !onGround && hw.AircraftSpeed > hw.MaxSwivelAirspeed - 11f)
					{
						Say($"STOVL conversion <b>inhibited</b>: true airspeed over {(hw.MaxSwivelAirspeed - 11f) * 3.6f:F0} km/h");
						return;
					}
					// 0.3.5 (Tom, 17 Nov): not above the STOVL mode's AoA limit (StovlAoALimit, 24 deg): the limit takes over at the
					// request, so a conversion from a higher AoA would step the limit down under the jet
					if (!onGround && Plugin.StovlAoALimit > 0f && Mathf.Abs(alphaNow) > Plugin.StovlAoALimit)
					{
						Say($"STOVL conversion <b>inhibited</b>: AoA over {Plugin.StovlAoALimit:F0} deg");
						return;
					}
					// 0.2.8 conversion guard (Tom, 17 Nov 2026): a jet already too lopsided for the hover is not converted. The roll
					// moment its sideways CG offset needs trimmed, against the share of the roll posts' authority the laws do not
					// keep for control, with the posts at the thrust the hover will take (HoverRollTrim). A short warning naming
					// the heavy side. (0.2.8, 03:40: against GuardShare of the posts, as the release guard.)
					// 0.2.11 (Tom): a hard guard, no override: 0.2.8-0.2.10 converted anyway on a second Flight Assist hold within
					// AsymForceWindow. The warning shows on every hold (the log line at most once per AsymForceWindow); the
					// automatic conversion (StovlAutoTransition) is held back by the same check.
					if (ConversionUnsafe(true))
					{
						return;
					}
					asymWarnT = -100f;
					selected = true;
					state = StovlState.Converting;
					Say("STOVL conversion: doors opening");
					return;
				}
				if (auto)
				{
					// converting / on with the switch off (an owner change): take it as on
					selected = true;
					return;
				}
			}
			// switch off
			switch (state)
			{
			case StovlState.Off:
			case StovlState.Reconverting:
				selected = false;
				Say("STOVL <b>off</b>");
				break;
			case StovlState.Converting:
			case StovlState.On:
				// (on the wheels it is always allowed: back to CTOL for the taxi after a vertical landing)
				if (ias < Plugin.StovlReconversionMinIas / 3.6f && !onGround)
				{
					Say($"Reconversion <b>inhibited</b> below {Plugin.StovlReconversionMinIas:F0} km/h");
					return;
				}
				selected = false;
				leverZero = leverSeen <= Plugin.StovlAftDetent + Plugin.StovlDetentWidth + 0.005f;
				state = StovlState.Reconverting;
				Say("Reconversion to CTOL");
				break;
			}
		}

		// the automatic conversion / reconversion while the switch is on (called from PreTick). 0.1.6: back to the 0.1.3 IAS
		// gate (0.1.4-0.1.5 went on the wing's level-flight AoA): CTOL at StovlAutoCtolIas (0.1.7: 370 km/h) or more held 1 s,
		// STOVL at StovlAutoStovlIas (0.1.7: 350) or less held 2 s in calm flight. alphaLevel (the AoA the wing alone needs for level
		// flight, W / (q x wing lift slope)) is kept for the manoeuvre test.
		public float massNow = 12000f;

		public float alphaLevel = 90f;

		// the HUD throttle readout while the STOVL laws fly (null otherwise)
		public string hudText;

		// fed by the controller before PreTick: measured AoA (deg), normal load (g), pitch stick
		public float alphaNow;

		public float nzNow = 1f;

		public float spNow;

		private float calmT;

		private bool autoConverting;

		private float autoCtolT;

		private float autoStovlT;

		private void AutoTransition(float dt)
		{
			float ias = gIas;
			float q = 0.5f * 1.225f * ias * ias;
			alphaLevel = Mathf.Min(90f, Mathf.Max(1f, massNow) * 9.81f / Mathf.Max(1f, q * sCLa) * 57.29578f);
			if (!selected || !Plugin.StovlAutoTransition)
			{
				autoCtolT = autoStovlT = 0f;
				return;
			}
			// 0.1.5: not while manoeuvring. A pull bleeds the speed (alphaLevel rises) without the jet being slow: no conversion
			// while the AoA is over 6 deg past the level-flight need (20 at least; 0.1.5: StovlAutoStovlAoA + 6), the load over 1.4 g or the pitch stick past 0.3, nor for 3 s after;
			// the level-flight need has to hold 2 s in calm flight; a conversion still opening its doors (the CTOL laws flying)
			// is called off if a manoeuvre starts
			// (0.1.6: the AoA test is 6 deg over the level-flight need, 20 at least)
			bool manoeuvre = !onGround && (alphaNow > Mathf.Max(20f, alphaLevel + 6f) || nzNow > 1.4f || nzNow < 0.4f || Mathf.Abs(spNow) > 0.3f);
			calmT = manoeuvre ? 0f : (calmT + dt);
			bool calm = calmT >= 3f || onGround;
			if (manoeuvre && state == StovlState.Converting && autoConverting)
			{
				autoConverting = false;
				state = StovlState.Reconverting;
				Say("STOVL conversion called off: manoeuvring (STOVL stays armed)");
			}
			bool wantCtol = state == StovlState.On && !onGround && ias >= Plugin.StovlAutoCtolIas / 3.6f;
			bool wantStovl = (state == StovlState.Off || state == StovlState.Reconverting) && ias <= Plugin.StovlAutoStovlIas / 3.6f && calm && (onGround || Plugin.StovlAoALimit <= 0f || Mathf.Abs(alphaNow) <= Plugin.StovlAoALimit);
			autoCtolT = wantCtol ? (autoCtolT + dt) : 0f;
			autoStovlT = wantStovl ? (autoStovlT + dt) : 0f;
			if (autoCtolT >= 1f)
			{
				autoCtolT = 0f;
				state = StovlState.Reconverting;
				Say($"CTOL above {Plugin.StovlAutoCtolIas:F0} km/h (STOVL stays armed)");
			}
			else if (autoStovlT >= 2f && !ConversionUnsafe(false))
			{
				autoStovlT = 0f;
				autoConverting = true;
				state = StovlState.Converting;
				Say($"STOVL conversion below {Plugin.StovlAutoStovlIas:F0} km/h: doors opening");
			}
		}

		// on the wheels: the laws take the jet only for a vertical take-off (stick aft with the throttle in or behind the aft
		// detent, the F-35B's: the stick is the height-rate command there); a short take-off rolls on the ground laws with the
		// pilot's throttle and the nozzle aft (down to 45 deg from 35 m/s, the game's own STO schedule), and the laws take
		// over once the wheels leave the ground. Called before the law choice each tick.
		public void GroundIntent(bool wow, float sp, float throttle, float ias)
		{
			wheelsNow = wow;
			if (thrLatchPending)
			{
				thrLatchPending = false;
				// 0.2.8: not on the wheels with the lever above the aft detent: that is a short take-off (full throttle held
				// through the conversion on the runway), and the latch read it as zero ground speed until the lever was moved
				thrLatched = !(wow && throttle > Plugin.StovlAftDetent + Plugin.StovlDetentWidth);
				thrLatchVal = throttle;
			}
			if (thrLatched)
			{
				// Vortex 0.1.2: released when the lever reaches (or passes) the detent it is read as, or moves 0.05 the other
				// way. 0.1.1 released it on any 0.05 move, so pushing up from idle to the aft detent read 5-20 % on the way
				// (up to 25 kt backwards) before it got there
				// 0.1.3: only in the hover regime (behind the aft detent is backwards there); at speed (0.1.9: read as the aft
				// detent, decelerate; 0.1.3-0.1.8 hold speed) any 0.05 move lets go
				float tgt = Plugin.StovlAftDetent;
				float d0 = tgt - thrLatchVal;
				float d = throttle - thrLatchVal;
				bool reached = Mathf.Abs(throttle - tgt) <= Plugin.StovlDetentWidth || (Mathf.Abs(d0) > 1e-4f && Mathf.Sign(throttle - tgt) != Mathf.Sign(thrLatchVal - tgt));
				bool away = Mathf.Abs(d) > 0.05f && (Mathf.Abs(d0) <= 1e-4f || Mathf.Sign(d) != Mathf.Sign(d0));
				if (!slowRegime)
				{
					reached = false;
					away = Mathf.Abs(d) > 0.05f;
				}
				if (reached || away)
				{
					thrLatched = false;
				}
			}
			throttle = EffThrottle(throttle);
			gThrottle = throttle;
			leverSeen = throttle;
			gIas = ias;
			float ready = Plugin.StovlAftDetent + Plugin.StovlDetentWidth;
			if (!wow)
			{
				touchGrace = false;
				onGround = false;
				wasAir = true;
				liftIntent = true;
				return;
			}
			onGround = true;
			if (wasAir)
			{
				// a landing is the wheels touching with the stick pushed (the pilot flying it down); a touch with the stick
				// centred or pulled (a sag in a heavy, low hover) gets 0.5 s for the height hold to lift it clear before it
				// counts as one
				wasAir = false;
				touchT = 0f;
				touchGrace = state == StovlState.On && sp >= -0.05f;
				groundIdleLatch = state == StovlState.On && !touchGrace;
			}
			if (touchGrace)
			{
				touchT += Time.fixedDeltaTime;
				if (sp < -0.05f || (touchT > 0.5f && sp <= 0.1f))
				{
					touchGrace = false;
					groundIdleLatch = state == StovlState.On;
				}
				else if (sp > 0.1f)
				{
					touchGrace = false;
				}
			}
			// Vortex 0.3.7 (Tom, 15:36 log, 45-61 s: touched down translating with the stick pushed, lever 0.54, then full lever;
			// the engine stayed at idle and the HUD held "+34 KT" until the lever went back to the aft detent at 60 s): the
			// touchdown idle also lets go when the lever is pushed 0.05 forward of the lowest it has been since the touchdown
			// (an unmistakable "go"), and the HUD says IDLE while it holds
			if (groundIdleLatch)
			{
				if (!latchLeverSet)
				{
					latchLeverMin = throttle;
					latchLeverSet = true;
				}
				latchLeverMin = Mathf.Min(latchLeverMin, throttle);
				if (throttle <= ready || throttle >= latchLeverMin + 0.05f)
				{
					groundIdleLatch = false;
				}
			}
			if (!groundIdleLatch)
			{
				latchLeverSet = false;
			}
			// Vortex 0.1.1: on the wheels the laws fly the jet for a vertical take-off (stick aft) and for the taxi / short
			// take-off roll (throttle forward of the aft detent): the same laws as in the air, so the nozzle never leaves its
			// STOVL schedule on the ground (0.1.0 rolled on the CTOL laws with the nozzle aft and the afterburner, then swung
			// the nozzle down at lift-off)
			liftIntent = state == StovlState.On && !groundIdleLatch && (sp > 0.1f || throttle > ready);
			if (state == StovlState.On && !liftIntent)
			{
				// the laws are off on the wheels: no stale ground-speed command on the HUD
				hudText = groundIdleLatch ? "IDLE" : null;
			}
		}

		// once per tick before the laws: the state machine and the hardware command for the CTOL / transition states
		// Vortex 0.2.2: STOVL freestyle (STOVL on, flight assist off = MPO): the pilot's throttle, the nozzle where it is until the
		// pilot moves Custom Axis 1 (the game builds that axis on the value it holds), and rate command on roll / pitch / pedal
		// with the bank, pitch attitude and heading held when the stick and pedals are released (zero rate on every axis). The
		// fan, roll posts, yaw nozzle and surfaces are the allocator's for the attitude only. Turning flight assist back on
		// starts the STOVL laws afresh from the jet's state.
		public bool freestyle;

		// Vortex 0.5.4 (Tom, 18 Nov: "when i turn off stability assist in stovl, i want it to automatically increase throttle to
		// the last recorded thrust to keep the jet on the air. But i still have full manual throttle control"). In STOVL flight
		// the lever is the speed lever (in a hover it sits near the aft detent), and the laws set the engine themselves; Stability
		// Assist off (freestyle) hands the engine the pilot's lever, so the throttle fell to the lever's position (idle in a
		// hover) and the jet dropped. Now, switched off in the air:
		//  - the throttle that holds the engine at the RPM it has at that moment (HoldThrottle; dry, as the laws use) is taken;
		//  - keyboard / button throttles: ThrottleDetent sets the game's lever to it once (freeThrPin), and the keys go on from
		//    there: an ordinary throttle the pilot owns;
		//  - an analog axis is not moved (the game takes the axis position back the moment it moves): the engine gets the held
		//    throttle until the axis is brought to it, from either side, then the axis (freeThrHold): no step in thrust. (A
		//    0.02 "moved" test was tried first: on the bench a lever pushed up from idle cut the engine to the lever's position
		//    on the way, 0.67 -> 0.37 for a tick.)
		// On the wheels, or with the laws not flying, nothing changes.
		public bool freeThrPin;

		public float freeThrVal;

		private bool freeThrHold;

		private bool freeThrLever0Set;

		private float freeThrLever0;

		// the throttle the engine settles at its present RPM with (the inverse of the game's throttle -> RPM target that
		// ThrottleForThrust uses, without its lead: holds, does not chase)
		internal float HoldThrottle()
		{
			float rpmNow = hw.RpmRatio * hw.MaxRpm;
			float tN = (rpmNow - hw.MinRpm) / Mathf.Max(1f, (hw.MaxRpm - hw.MinRpm) * (hw.Condition * 0.5f + 0.5f));
			Vector2 rm = hw.Remap;
			return Mathf.Clamp(rm.x + Mathf.Max(0f, tN) * (rm.y - rm.x), 0f, rm.y);
		}

		public void SetFreestyle(bool on)
		{
			if (on == freestyle)
			{
				return;
			}
			// Vortex 0.5.4: Stability Assist off in STOVL flight keeps the engine where the laws had it (see HoldThrottle)
			freeThrPin = false;
			freeThrHold = false;
			freeThrLever0Set = false;
			if (on && owns && hw != null && hw.Valid && state == StovlState.On && airNow)
			{
				freeThrVal = HoldThrottle();
				freeThrPin = true;
				freeThrHold = true;
				Plugin.Log.LogInfo($"Vortex FCS: Stability Assist off in STOVL: throttle set to {freeThrVal * 100f:F1} % (the engine's {hw.RpmRatio * 100f:F1} % RPM, {hw.DryThrust / 1000f:F1} kN), the pilot's from here on.");
			}
			freestyle = on;
			lawInit = false;
			freeInit = false;
			// 0.2.3: no HUD line of its own; the game's stability-assist on / off message says it (0.2.2's line overwrote it)
		}

		public void PreTick(bool engaged, bool isAI, float dt)
		{
			guardClock += dt;
			// (Vortex 0.5.0: the vanes rest unless the law commands them this tick)
			vaneLive = false;
			owns = engaged && !isAI && hw != null && hw.Valid;
			if (!owns)
			{
				// the game's own swivel logic runs; keep the state where the hardware is so a re-engage picks it up
				state = (hw != null && hw.Doors > 0.5f) ? StovlState.On : StovlState.Off;
				throttleOverride = false;
				lawInit = false;
				return;
			}
			AutoTransition(dt);
			if (state == StovlState.On && hw.AircraftSpeed > hw.MaxSwivelAirspeed - 0.5f)
			{
				// the game swings the nozzle aft above this speed whatever it is told: hand back to the CTOL laws
				state = StovlState.Reconverting;
				Say("Overspeed: reconversion to CTOL");
			}
			switch (state)
			{
			case StovlState.Off:
				thrLatched = false;
				thrLatchPending = false;
				axis1Out = 1f;
				throttleOverride = false;
				lawInit = false;
				break;
			case StovlState.Converting:
				// doors open with the nozzle (nearly) aft; the CTOL laws keep flying until they are fully open
				// Vortex 0.3.11 (Tom, 16:20 log, 17-24 s: stick aft the moment the doors were open, the nozzle still aft and 4 s
				// from down, the jet ran forward to 7.9 m/s): on the wheels at a standstill the nozzle goes down to its ground
				// position while the doors open (both take ~4 s), so it is ready when STOVL mode comes in
				axis1Out = (onGround && hw.AircraftSpeed < 5f) ? Mathf.Clamp(1f - Plugin.StovlGroundSwivel, 0f, 0.999f) : 0.999f;
				throttleOverride = false;
				lawInit = false;
				if (hw.Doors >= 0.999f)
				{
					autoConverting = false;
					state = StovlState.On;
					thrLatchPending = true;
					leverPark = true;
					Say("<b>STOVL</b> mode");
				}
				break;
			case StovlState.Reconverting:
				axis1Out = 1f;
				throttleOverride = false;
				lawInit = false;
				if (hw.Doors <= 0.001f && hw.Swivel <= 0.001f)
				{
					state = StovlState.Off;
					Say("<b>CTOL</b> mode");
				}
				break;
			}
		}

		public struct LawIn
		{
			public float dt;

			public float time;

			public Quaternion rot;

			public Vector3 vel;

			public Vector3 wind;

			public Vector3 accel;

			public Vector3 pos;

			public float mass;

			public float tas;

			public float alpha;

			public float beta;

			public float theta;

			public float phi;

			public float rho;

			public bool wow;

			public float sp;

			public float sr;

			public float sy;

			public float throttle;

			// body rates, deg/s (aero frame: p roll right, q nose up, r nose right)
			public Vector3 rates;

			// angular acceleration the active effectors can add both ways now, deg/s^2 (same frame; FcsController.StovlAuthority)
			public Vector3 authority;

			// Vortex 0.4.2: the pitch acceleration the lift fan's pitch term makes per unit of its input, deg/s^2
			// (FcsController.StovlFanPitchAuth; 0 with the fan off)
			public float fanPitchAuth;

			// 0.2.8: the roll acceleration a lateral CG offset takes to hold, deg/s^2, positive when the load rolls the jet to
			// the right (FcsController.StovlRollLoad)
			public float rollLoad;

			// 0.2.8: the roll acceleration the active effectors make over their whole travel, to one side, deg/s^2
			// (FcsController.StovlRollFull)
			public float rollFull;

			// the game's radar altitude (m)
			public float radarAlt;

			// the highest touchdown sink rate the gear survives (m/s; FcsController.TouchdownSinkLimit)
			public float touchdownMax;

			// 0.2.1: the airframe's directional stiffness (deg/s^2 of restoring yaw per deg of sideslip, the controller's slip probe)
			public float slipStiff;

			// 0.2.9: the CTOL pitch law's pitch-rate command this tick (deg/s), and whether the CTOL laws ran (CtolW > 0 last
			// tick): at speed the propulsion carries the share of the load the wing does not, along the path the CTOL law flies
			public float qCtol;

			public bool ctolOn;
		}

		// the main nozzle's thrust direction and point (world) for a swivel s and bearing yaw input, by the game's own bearing
		// kinematics (Bearing.Animate) on the live chain below the first bearing
		private readonly List<Transform> chain = new List<Transform>();

		private readonly List<int> chainBearing = new List<int>();

		private bool chainBuilt;

		private bool BuildChain()
		{
			chainBuilt = true;
			chain.Clear();
			chainBearing.Clear();
			Transform tt = hw.MainThrustT;
			Transform top = (hw.Bearings > 0) ? hw.BearingT(0) : null;
			if (tt == null || top == null || !tt.IsChildOf(top))
			{
				return false;
			}
			for (Transform t = tt; t != null; t = t.parent)
			{
				chain.Insert(0, t);
				if (t == top)
				{
					break;
				}
			}
			foreach (Transform t in chain)
			{
				int bi = -1;
				for (int i = 0; i < hw.Bearings; i++)
				{
					if (hw.BearingT(i) == t)
					{
						bi = i;
					}
				}
				chainBearing.Add(bi);
			}
			return chain.Count > 0;
		}

		public bool NozzleAt(float s, float yawInput, out Vector3 dir, out Vector3 pos)
		{
			dir = Vector3.forward;
			pos = Vector3.zero;
			if (!chainBuilt)
			{
				BuildChain();
			}
			if (chain.Count == 0 || chain[0].parent == null)
			{
				Transform tt = hw.MainThrustT;
				if (tt == null)
				{
					return false;
				}
				dir = tt.forward;
				pos = tt.position;
				return true;
			}
			Transform p0 = chain[0].parent;
			Quaternion q = p0.rotation;
			Vector3 p = p0.position;
			for (int i = 0; i < chain.Count; i++)
			{
				Transform t = chain[i];
				p += q * t.localPosition;
				int b = chainBearing[i];
				Quaternion lr = (b >= 0) ? Quaternion.Euler(Vector3.Lerp(hw.BearingMin(b), hw.BearingMax(b), hw.BearingCurve(b, s)) + hw.BearingYaw(b) * yawInput) : t.localRotation;
				q = q * lr;
			}
			dir = q * Vector3.forward;
			pos = p;
			return true;
		}

		// the game's lift-fan force for a dry thrust T along nozzle direction d at point pn: the force that cancels the nozzle's
		// pitch moment about the CG (body x axis), plus the pitch-input term
		public float FanForce(float dryT, Vector3 d, Vector3 pn, float pitchIn)
		{
			Transform ft = hw.FanThrustT;
			if (ft == null || hw.Doors <= 0f)
			{
				return 0f;
			}
			return FanForceAt(dryT, d, pn, pitchIn, ft.forward);
		}

		// ---- Vortex 0.5.0 (Tom, 18 Nov 2026: "add F-35B-style vanes under the lift fan", 10 deg forward .. 42 deg aft): the vane box.
		// The game sizes the lift fan's force to cancel the main nozzle's pitch moment about the CG and applies it along the fan's
		// thrust transform, whatever way that points; so turning that transform about the jet's lateral axis vectors the fan and
		// the game re-trims its size by itself. The game's own transform is fixed 10 deg "forward" (the force leans aft): that is
		// the vanes' forward stop and where they rest. The STOVL laws choose the vane angle together with the nozzle swivel, in
		// the same thrust fit (the pair that gives the asked force for the least share of the thrust there is), size the
		// thrust for where both will be one thrust response time ahead, count the fan's turned force in the forward
		// translation capability, and count the lift the vanes take while they turn in the tilt governor's demand. Under the
		// CTOL blend (60-80 kt) the vanes go back to their rest, so the blend flies as in 0.4.11. Turned aft the vane cascade
		// loses some of the fan's thrust; the fan is driven harder to make it up, which the game counts as engine thrust (its
		// parasitic loss): StovlVaneLoss.
		// (the commanded thrust line, deg; the hardware slews to it at StovlVaneRate while vaneLive)
		public float vaneOut;

		public bool vaneLive;

		public bool VanesOn => Plugin.StovlVanes && hw != null && hw.HasVanes && hw.FanThrustT != null;

		internal const int NV = 11;

		// this tick's vane geometry (Law): on, the game's fixed line (deg), its direction, the jet's lateral axis, the limits
		private bool vnOn;

		private float vnStock;

		private Vector3 vnRest;

		private Vector3 vnRight;

		private float vnLo;

		private float vnHi;

		private float vnDriveNow = 1f;

		// the vane angle the steady hover keeps (learned with sHov), for the lift capability
		private float vHov;

		private bool vHovInit;

		private readonly float[] vG = new float[NV];

		private readonly float[] vDrive = new float[NV];

		private readonly Vector3[] vnDir = new Vector3[NV];

		private readonly Vector3[] g2Dir = new Vector3[21 * NV];

		private readonly float[] g2Lift = new float[21 * NV];

		private readonly float[] g2J = new float[21 * NV];

		private readonly float[] fine9 = new float[9];

		// the lift the vanes took last tick while turning away from the vertical (per second, per unit of lift), for the governor
		private bool incVaneLast;

		private float tVaneLast;

		public float dbgVaneBest;

		// the thrust's own weight in the fit over swivel x vane: (thrust asked / thrust there is at that swivel and vane)^2 x
		// full thrust^2 x this, against the force error^2. Small, so it only chooses among the pairs that give the asked force
		internal static float VhVaneThrustW = 0.002f;

		// the vanes' lift-take while they turn away from the vertical counts in the tilt governor's demand (see the vane command)
		internal static bool VhVaneGovCount = true;

		// the fan's share of the lift at the last vane command (for the fore-aft profile's StovlVaneStopJerk)
		private float fanShareLast = 0.45f;

		// the share of the fan's thrust the vane cascade loses at a thrust line v (deg): none forward of the fan's axis (the
		// game's fixed line has none), StovlVaneLoss at StovlVaneMax, with the square of the angle
		internal static float VaneLoss(float v)
		{
			if (v <= 0f)
			{
				return 0f;
			}
			float x = v / Mathf.Max(1f, Plugin.StovlVaneMax);
			return Mathf.Min(0.5f, Plugin.StovlVaneLoss * x * x);
		}

		// the factor on the fan's drive power (the game's parasitic thrust loss) that makes the loss up: thrust ~ power^(2/3)
		internal static float VaneDrive(float v)
		{
			float l = VaneLoss(v);
			return (l > 0f) ? Mathf.Pow(1f - l, -1.5f) : 1f;
		}

		// the fan's thrust direction (world) with the thrust line at v deg from the fan's axis (this tick's geometry)
		private Vector3 FanDirAt(float v)
		{
			return Quaternion.AngleAxis(v - vnStock, vnRight) * vnRest;
		}

		// the game's fixed thrust line, deg from the fan's axis (the jet's up axis; + = force leaning forward)
		public float VaneStock()
		{
			Transform root = ((Component)(object)ac).transform;
			Vector3 f = hw.FanRestForward;
			return Mathf.Atan2(Vector3.Dot(f, root.forward), Vector3.Dot(f, root.up)) * 57.29578f;
		}

		// the force per unit of dry thrust (heading frame: y up, z along the level heading) with the nozzle at swivel s (yaw
		// bearing centred) and the fan's thrust line at v
		private Vector3 GAt(float s, float v, Vector3 up, Vector3 hdg)
		{
			NozzleAt(s, 0f, out var d, out var pn);
			Vector3 fd = FanDirAt(v);
			float fk = FanForceAt(1f, d, pn, 0f, fd);
			Vector3 gW = d + fd * fk;
			return new Vector3(0f, Vector3.Dot(gW, up), Vector3.Dot(gW, hdg));
		}

		// that force's direction in the body, deg from the body's up axis toward its nose
		private float GAngBody(float s, float v, Quaternion rot)
		{
			NozzleAt(s, 0f, out var d, out var pn);
			Vector3 fd = FanDirAt(v);
			float fk = FanForceAt(1f, d, pn, 0f, fd);
			Vector3 g = Quaternion.Inverse(rot) * (d + fd * fk);
			return Mathf.Atan2(g.z, Mathf.Max(0.01f, g.y)) * 57.29578f;
		}

		// (the fan's force along a given direction fDir; FanForce is this along the thrust transform as it is)
		public float FanForceAt(float dryT, Vector3 d, Vector3 pn, float pitchIn, Vector3 fDir)
		{
			Transform ft = hw.FanThrustT;
			if (ft == null || hw.Doors <= 0f)
			{
				return 0f;
			}
			Transform root = ((Component)(object)ac).transform;
			Vector3 cg = hw.CenterOfMass;
			Vector3 m4 = root.InverseTransformDirection(Vector3.Cross(dryT * d, -(pn - cg)));
			Vector3 m6 = root.InverseTransformDirection(Vector3.Cross(fDir, -(ft.position - cg)));
			if (Mathf.Abs(m6.x) < 1E-06f)
			{
				return 0f;
			}
			float f = (0f - m4.x) / m6.x;
			f += hw.Doors * pitchIn * hw.FanPitchFactor * hw.RpmRatio;
			return Mathf.Max(f, 0f);
		}

		// propulsive force now (world): main nozzle + lift fan + roll posts
		public Vector3 PropulsiveForceNow()
		{
			Transform tt = hw.MainThrustT;
			Vector3 f = Vector3.zero;
			if (tt != null)
			{
				f += tt.forward * hw.NozzleThrust;
				float ff = FanForce(hw.DryThrust, tt.forward, tt.position, fanIn);
				if (hw.FanThrustT != null)
				{
					f += hw.FanThrustT.forward * ff;
				}
			}
			for (int i = 0; i < hw.Posts; i++)
			{
				f += PostForce(i, postIn);
			}
			return f;
		}

		// 0.2.8: the part of the propulsive force the thrust fit commands: the main nozzle and the fan's balancing force (the
		// fan's pitch-input term and the roll posts are the attitude loops', and come and go at once)
		public Vector3 ControlledForceNow()
		{
			Transform tt = hw.MainThrustT;
			Vector3 f = Vector3.zero;
			if (tt != null)
			{
				f += tt.forward * hw.NozzleThrust;
				float ff = FanForce(hw.DryThrust, tt.forward, tt.position, 0f);
				if (hw.FanThrustT != null)
				{
					f += hw.FanThrustT.forward * ff;
				}
			}
			return f;
		}

		// Vortex 0.4.7 (StovlFwdCapFromNozzle): the forward acceleration the nozzle can give while the lift system still makes the
		// lift lb (m/s^2, along the bank-free lift direction: sqrt(vertical demand^2 + sideways acceleration^2)), at 95 % of full
		// dry thrust: the swivel where 0.95 T(s) gLift(s) / m = lb, and the forward part there. The lift rises and the forward
		// part falls as the nozzle comes down, so it is the first table entry from aft that carries lb.
		private float FwdCap(float lb, float mass, float altF)
		{
			if (vnOn)
			{
				// Vortex 0.5.0: with the vane box, the best of the vane angles (each one's own swivel where the lift is carried)
				float bestC = FwdCapCol(lb, mass, altF, 0);
				for (int j = 1; j < NV; j++)
				{
					bestC = Mathf.Max(bestC, FwdCapCol(lb, mass, altF, j));
				}
				return bestC;
			}
			int n = gS.Length;
			float m = Mathf.Max(1f, mass);
			float fPrev = 0f;
			float lPrev = 0f;
			for (int k = 0; k < n; k++)
			{
				float aT = VhFwdCapThrust * Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * gS[k]) * altF) / m;
				float l = aT * gLift[k];
				float f = aT * gDir[k].z;
				if (l >= lb)
				{
					if (k == 0)
					{
						return f;
					}
					float fr = (l > lPrev) ? ((lb - lPrev) / (l - lPrev)) : 1f;
					return Mathf.Lerp(fPrev, f, Mathf.Clamp01(fr));
				}
				fPrev = f;
				lPrev = l;
			}
			// (the lift is short even with the nozzle down: what that leaves)
			return fPrev;
		}

		// Vortex 0.5.0: FwdCap for one vane angle (column j of the swivel x vane table)
		private float FwdCapCol(float lb, float mass, float altF, int j)
		{
			int n = gS.Length;
			float m = Mathf.Max(1f, mass);
			float fPrev = 0f;
			float lPrev = 0f;
			for (int k = 0; k < n; k++)
			{
				float aT = VhFwdCapThrust * Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * gS[k] * vDrive[j]) * altF) / m;
				float l = aT * g2Lift[k * NV + j];
				float f = aT * g2Dir[k * NV + j].z;
				if (l >= lb)
				{
					if (k == 0)
					{
						return f;
					}
					float fr = (l > lPrev) ? ((lb - lPrev) / (l - lPrev)) : 1f;
					return Mathf.Lerp(fPrev, f, Mathf.Clamp01(fr));
				}
				fPrev = f;
				lPrev = l;
			}
			return fPrev;
		}

		// 0.2.8: the factor (0..1) on a tilt command's rate away from level: the bank and the pitch tilt share the budget in
		// proportion to what each asks (tan(tilt) x rate, the lift each takes per second, per unit of lift)
		private float GovLambda(bool inc, float rateDeg)
		{
			if (!VhGovern || (!inc && !VhGovernDown))
			{
				dbgGovLam = 1f;
				return 1f;
			}
			float d2r = 0.0174532924f;
			float tPhi = Mathf.Tan(Mathf.Min(Mathf.Abs(phiG), 80f) * d2r) * 60f * d2r;
			float tTh = Mathf.Tan(Mathf.Min(Mathf.Abs(thetaAssist), 80f) * d2r) * ((Plugin.StovlTiltStopTime > 0f) ? tiltRateFwdLast : Plugin.StovlTrcBankRate) * d2r;
			float dem = inc ? ((incPhiLast ? tPhi : 0f) + (incThLast ? tTh : 0f)) : ((decPhiLast ? tPhi : 0f) + (decThLast ? tTh : 0f));
			// (Vortex 0.5.0: the lift-fan vanes turning away from the vertical take lift the same way: fan's share x tan x rate)
			if (inc && vnOn && incVaneLast && VhVaneGovCount)
			{
				dem += tVaneLast;
			}
			float bud = inc ? tiltBudget : tiltBudgetDn;
			// Vortex 0.4.7 (StovlGovTiltCredit): a tilt coming out gives lift back at tan(tilt) x its rate, and that is budget for
			// the other tilt coming in: the two trade lift with no thrust change. 0.2.8-0.4.6 counted spare thrust only, so with the
			// thrust at its limit (a fore-aft stop's pitch at its peak) a side command's bank waited at level until the thrust had
			// come back down (22:56 log, 33.4-33.8 s: bank target 0.9 deg for 0.4 s while the pitch tilt went out at 6.5 deg/s).
			if (inc && Plugin.StovlGovTiltCredit)
			{
				bud += retPhiLast + retThLast;
			}
			float lam = (dem > bud) ? (bud / Mathf.Max(1E-05f, dem)) : 1f;
			dbgGovLam = lam;
			return Mathf.Clamp01(lam);
		}

		public Vector3 PostForce(int i, float rollIn)
		{
			Transform t = hw.PostT(i);
			if (t == null || hw.Swivel < hw.PostMinSwivel(i) || hw.AircraftSpeed > hw.PostMaxSpeed(i))
			{
				return Vector3.zero;
			}
			float n = Mathf.Max(hw.PostMax(i) * hw.ThrustRatio * rollIn * hw.PostRollFactor(i), 0f);
			return t.forward * n;
		}

		// weighted fit of the force request fB (body: y up, z forward) by a thrust T along g: T = argmin wy (fy - T gy)^2 +
		// wz (fz - T gz)^2 within idle..max; returns the weighted residual
		private float WlsCost(Vector3 g, Vector3 fB, float s, float idleRatio, float altF, out float T)
		{
			return WlsCostD(g, fB, s, idleRatio, altF, 1f, out T);
		}

		// (Vortex 0.5.0: with the vane box's drive factor on the fan's parasitic loss; 1 = none)
		private float WlsCostD(Vector3 g, Vector3 fB, float s, float idleRatio, float altF, float drive, out float T)
		{
			const float wy = 1f;
			float wz = fitWz;
			float tMx = Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * s * drive) * altF);
			float tMn = idleRatio * tMx;
			float den = wy * g.y * g.y + wz * g.z * g.z;
			T = (den > 1E-06f) ? ((wy * fB.y * g.y + wz * fB.z * g.z) / den) : 0f;
			T = Mathf.Clamp(T, tMn, tMx);
			float ey = fB.y - T * g.y;
			float ez = fB.z - T * g.z;
			return wy * ey * ey + wz * ez * ez;
		}

		private readonly float[] gS = new float[21];

		private readonly float[] gAng = new float[21];

		private readonly float[] gMag = new float[21];

		private readonly Vector3[] gDir = new Vector3[21];

		// Vortex 0.4.7: the lift per unit thrust with the bank taken out (the part of g(s) normal to the level heading), for the
		// forward translation capability (FwdCap)
		private readonly float[] gLift = new float[21];

		// Vortex 0.4.7: last tick's position, to see the game's floating-origin shift (StovlOriginShiftFix)
		private Vector3 posLast;

		private float posLastT = -1f;

		public int dbgOriginJumps;

		// Vortex 0.4.7: the lift the tilts gave back last tick while coming out (per second, per unit of lift), for the governor
		private float retPhiLast;

		private float retThLast;

		public float dbgFwdCap;

		// Vortex 0.4.7: the vertical force the thrust fit was asked for, low-passed 0.3 s (what the nozzle and the fan's
		// balancing share have to carry: the weight and the vertical demand less the wing's lift, the roll posts and the fan's
		// pitch term), for the forward capability
		private float fReqYF;

		private bool fReqYInit;

		internal static bool VhFwdCapFromFit = true;

		public float dbgPsiRate;

		// the unified laws. Writes pOut / qOut / rOut (body rates), the swivel target, the throttle.
		private float phiTrcF;

		private bool freeInit;

		// Vortex 0.5.7: the freestyle stick's full roll rate and bank limit this tick (deg/s, deg)
		public float dbgFreeRollMax;

		public float dbgFreeBankLim;

		private bool freePhiHold;

		private float freePhiRef;

		private bool freeThetaHold;

		private float freeThetaRef;

		// Vortex 0.2.2: the freestyle law (see SetFreestyle): Euler-rate commands from the stick and pedals, holds when released,
		// to body rates; nothing on the translation, the height or the throttle
		private void FreeLaw(ref LawIn I)
		{
			float dt = I.dt;
			Transform root = ((Component)(object)ac).transform;
			Vector3 fwd = root.forward;
			Vector3 hdg = new Vector3(fwd.x, 0f, fwd.z);
			if (hdg.sqrMagnitude < 1E-06f)
			{
				hdg = new Vector3(root.up.x, 0f, root.up.z) * -1f;
			}
			hdg.Normalize();
			float psi = Mathf.Atan2(hdg.x, hdg.z) * 57.29578f;
			if (!freeInit)
			{
				freeInit = true;
				freePhiHold = false;
				freeThetaHold = false;
				psiHold = false;
				psiDotPedal = 0f;
				rateFFInit = false;
			}
			float aPitch = Mathf.Clamp(AuthorityShare * I.authority.y, 8f, 120f);
			float aRoll = Mathf.Clamp(AuthorityShare * I.authority.x, 10f, 200f);
			float aYaw = Mathf.Clamp(AuthorityShare * I.authority.z, 4f, 120f);
			dbgAuth = new Vector3(aRoll, aPitch, aYaw);
			RollStopPower(aRoll, VhRollLoad ? I.rollFull : 1000f, VhRollLoad ? I.rollLoad : 0f, out float aRollP, out float aRollN);
			// roll: rate, bank held when released (caught once the roll has stopped)
			// Vortex 0.5.7 (Tom's 11:51 log, 18 Nov: "the jet just flipped an crashed"; Stability Assist off at 40 s, in the
			// hover at 10 m/s): full right stick for 1.6 s commanded 90 deg/s of roll; the roll posts at their stop took the jet
			// to 69 deg/s by the 62 deg of bank where the stick came back, full left stick could only brake it at ~50 deg/s^2,
			// the bank ran on through 100 deg with the lift system's thrust going sideways, and the jet hit the ground upside
			// down at 45.3 s. 0.2.2-0.5.6 had no limit on the bank here and a fixed 90 deg/s, more than the hover can stop
			// (the rule: the FCS does not let the pilot command what the jet cannot hold). Now, as the pedal since 0.3.9:
			//  - the stick's full rate is what the roll axis can start and stop within StovlYawStopTime at its stopping power
			//    now (the weaker side), never more than StovlRollRateMax;
			//  - while the lift system carries the jet the bank is held to StovlBankMax (the STOVL laws' own limit), approached
			//    at a rate that can still stop on it; the limit opens to none as the wing takes the weight (the same airspeed
			//    blend as the velocity-vector roll below), so freestyle at speed rolls as before.
			float rollRateMax = Mathf.Min(Plugin.StovlRollRateMax, (Plugin.StovlYawStopTime > 0f) ? (Mathf.Min(aRollP, aRollN) * Plugin.StovlYawStopTime) : Plugin.StovlRollRateMax);
			float phiLimF;
			{
				Vector3 vAirL = I.vel - I.wind;
				Vector3 rgtL = root.right;
				float vSymL = (vAirL - rgtL * Vector3.Dot(vAirL, rgtL)).magnitude;
				float wWingL = Plugin.StovlPhysicsBlends ? Smooth(AeroShare0, AeroShare1, vSymL / WingBorneSpeed(ref I)) : Smooth(10f, 25f, vSymL);
				phiLimF = Mathf.Lerp(Mathf.Clamp(Plugin.StovlBankMax, 5f, 80f), 180f, wWingL);
			}
			dbgFreeRollMax = rollRateMax;
			dbgFreeBankLim = phiLimF;
			float phiDot;
			if (Mathf.Abs(I.sr) >= 0.05f)
			{
				freePhiHold = false;
				phiDot = I.sr * rollRateMax;
			}
			else
			{
				if (!freePhiHold && Mathf.Abs(I.rates.x) < 5f)
				{
					freePhiHold = true;
					freePhiRef = I.phi;
				}
				float ePhiF = Mathf.DeltaAngle(I.phi, freePhiRef);
				phiDot = freePhiHold ? Stop(ePhiF, 2.5f, (ePhiF > 0f) ? aRollP : aRollN, 60f) : 0f;
			}
			// pitch: rate, attitude held when released
			float thetaDot;
			if (Mathf.Abs(I.sp) >= 0.05f)
			{
				freeThetaHold = false;
				thetaDot = I.sp * Plugin.StovlFreestylePitchRate;
			}
			else
			{
				if (!freeThetaHold && Mathf.Abs(I.rates.y) < 5f)
				{
					freeThetaHold = true;
					freeThetaRef = I.theta;
				}
				thetaDot = freeThetaHold ? Stop(Mathf.DeltaAngle(I.theta, freeThetaRef), 2f, aPitch, 30f) : 0f;
			}
			// yaw: the hover's yaw-rate pedal, heading held when released
			// Vortex 0.3.9 (Tom, 17 Nov: no fixed yaw-rate limit): the pedal's full rate is what the yaw axis can start and stop
			// within StovlYawStopTime at its authority now (aYaw, the ramp below), so it follows mass, fuel, stores and the lift
			// system's thrust; StovlYawStopTime is set so a clean jet with full fuel in the hover gets the old 30 deg/s
			float yawRateMax = YawRateMax(aYaw);
			float pedalTgt = (Mathf.Abs(I.sy) < 0.05f) ? 0f : (I.sy * yawRateMax);
			psiDotPedal = Mathf.MoveTowards(psiDotPedal, pedalTgt, aYaw * dt);
			float psiDot;
			if (Mathf.Abs(I.sy) < 0.05f)
			{
				if (!psiHold && Mathf.Abs(psiDotPedal) < 0.5f && Mathf.Abs(I.rates.z) < 3f)
				{
					psiHold = true;
					psiRef = psi;
				}
				psiDot = psiHold ? Stop(Mathf.DeltaAngle(psi, psiRef), 2.5f, aYaw, 20f) : psiDotPedal;
			}
			else
			{
				psiHold = false;
				psiDot = psiDotPedal;
			}
			// 0.5.7: the bank limit (see above), approached at a rate that can stop on it; past it (Stability Assist turned off
			// banked further) back toward it. The bank hold keeps its reference inside it.
			if (phiLimF < 179f)
			{
				freePhiRef = Mathf.Clamp(freePhiRef, -phiLimF, phiLimF);
				if (phiDot > 0f || I.phi > phiLimF)
				{
					phiDot = Mathf.Min(phiDot, Stop(phiLimF - I.phi, 2.5f, aRollP, rollRateMax));
				}
				if (phiDot < 0f || I.phi < -phiLimF)
				{
					phiDot = Mathf.Max(phiDot, Stop(-phiLimF - I.phi, 2.5f, aRollN, rollRateMax));
				}
			}
			if (I.wow && airT <= 0f)
			{
				phiDot = 0f;
			}
			float cph = Mathf.Cos(I.phi * 0.0174532924f);
			float sph = Mathf.Sin(I.phi * 0.0174532924f);
			float cth = Mathf.Cos(I.theta * 0.0174532924f);
			float sth = Mathf.Sin(I.theta * 0.0174532924f);
			// (the pedal is a body yaw rate while held, the heading hold's Euler rate when released)
			bool pedalHeld = Mathf.Abs(I.sy) >= 0.05f;
			float psiDotE = pedalHeld ? 0f : psiDot;
			float p = phiDot - psiDotE * sth;
			float q = thetaDot * cph + psiDotE * cth * sph;
			float r = (0f - thetaDot) * sph + psiDotE * cth * cph;
			if (pedalHeld)
			{
				r += psiDotPedal;
			}
			// MPO axis rule (single-engine 3D TVC: the nozzle's pitch swivel and yaw bearing): velocity-vector roll with a separate
			// body yaw on the pedal. The roll is about the air velocity wherever the airspeed means something (p cos(AoA) on body
			// roll + p sin(AoA) on body yaw), faded to the body roll under 10-25 m/s (in the hover a bank is a bank)
			{
				Vector3 vAirF = I.vel - I.wind;
				Vector3 rgtB = root.right;
				float vSymF = (vAirF - rgtB * Vector3.Dot(vAirF, rgtB)).magnitude;
				float wVv = Plugin.StovlPhysicsBlends ? Smooth(AeroShare0, AeroShare1, vSymF / WingBorneSpeed(ref I)) : Smooth(10f, 25f, vSymF);
				if (wVv > 0f)
				{
					float aR = Mathf.Clamp(I.alpha, -30f, 60f) * 0.0174532924f;
					p += wVv * phiDot * (Mathf.Cos(aR) - 1f);
					r += wVv * phiDot * Mathf.Sin(aR);
				}
			}
			if (rateFFInit)
			{
				pDotOut += (Mathf.Clamp((p - pOut) / Mathf.Max(dt, 1E-04f), -aRollP, aRollN) - pDotOut) * (dt / (0.05f + dt));
				rDotOut += (Mathf.Clamp((r - rOut) / Mathf.Max(dt, 1E-04f), -aYaw, aYaw) - rDotOut) * (dt / (0.05f + dt));
			}
			else
			{
				pDotOut = 0f;
				rDotOut = 0f;
			}
			rateFFInit = true;
			pOut = p;
			qOut = q;
			rOut = r;
			// the pilot's throttle and nozzle (the hooks pass them through in freestyle)
			throttleOverride = false;
			throttleOut = I.throttle;
			// 0.5.4: just switched to freestyle in the air: the engine holds where it was until the pilot's throttle moves
			if (freeThrHold)
			{
				if (!freeThrLever0Set)
				{
					freeThrLever0Set = true;
					freeThrLever0 = I.throttle;
				}
				// handed over where the pilot's throttle meets the held one: at once for a keyboard lever (ThrottleDetent has set
				// it there), when an analog axis is brought to it (from either side: no step in thrust either way)
				float dNow = I.throttle - freeThrVal;
				float d0 = freeThrLever0 - freeThrVal;
				if (Mathf.Abs(dNow) < 0.005f || dNow * d0 < 0f)
				{
					freeThrHold = false;
				}
				else
				{
					throttleOverride = true;
					throttleOut = freeThrVal;
				}
			}
			swivelTarget = hw.Swivel;
			dbgSwivelTgt = swivelTarget;
			brakeAuto = 0f;
			hudText = null;
		}

		private float fvMaxNow;

		private float fLiftBodyMax;

		// 0.2.6: the vertical loop's acceleration demand last tick (the translation budget keeps it)
		private float aVhLast;

		public float dbgAH;

		// 0.2.1: the vertical axis's response time and gains (see the tilt block); the released stick's climb or sink decays as
		// exp(-kVHold t) to the height it arrives at
		private static float RpmLead = 0.35f;

		// 0.2.8: RpmLead is set each tick to twice the measured acceleration's delay (its 0.05 s low-pass + one tick), the
		// fastest the thrust increment can close on that measurement with margin; VhLeadFix > 0 forces a value (bench)
		internal static float VhLeadFix = 0f;

		internal static float VhHoldRatio = 0.75f;

		internal static bool VhReserveFromError = true;

		private float hErrLast;

		// 0.2.8 command governor (vertical first). The engine answers a throttle step as a 1 s lag (the game's spool:
		// rpm += (target - rpm) dt, within the spool rate), so the thrust can rise no faster than (full dry - thrust now) / 1 s.
		private const float EngineLag = 1f;

		internal static bool VhGovern = true;

		internal static bool VhGeomFF = true;

		// (03:00: off. The caps on the roll posts and the fan pitch input starved the roll trim of a lopsided jet whenever the
		// thrust was at its limit: 02:38 log, one Tusko-B released at full fuel with the throttle at full dry: the posts
		// were held to 20 % for 0.6 s and to trim + 20 % after, the roll ran at 11-14 deg/s to 49 deg of bank, 34 m/s
		// sideways, -9.6 m/s. Without them the posts go to full at once.)
		internal static bool VhEffCaps = false;

		// the share of the thrust-rate headroom the tilt (bank + pitch) may spend; the rest is the height loop's and the
		// attitude effectors'
		internal static float VhTiltShare = 0.2f;

		internal static float VhFwdCapLag = 1f;

		internal static bool VhTurnFFCmd = true;

		internal static bool VhTurnLead = true;

		internal static bool VhTurnAvail = true;

		// Vortex 0.4.7: the sideways acceleration a turn may take (see StovlTurnYawLimit), low-passed 1 s
		private float aTurnF;

		private bool aTurnInit;

		// Vortex 0.4.7: the pedal's yaw-rate command as the yaw loop gets it (behind the plan the bank is given by the bank
		// loop's lag, where the turn needs the bank)
		private float psiPedLag;

		// Vortex 0.4.7: the heading rate the hover's yaw law commanded last tick (pedal / heading hold; not the part that follows
		// the bank in forward flight), deg/s
		private float psiDotFF;

		internal static float VhFwdCapThrust = 0.95f;

		// the height one attitude-effector event (a roll post or fan pitch burst) may cost, m
		internal static float VhEventDip = 0.05f;

		internal static float VhFFLead = 0.5f;

		private float tiltBudget = 99f;

		// 0.2.8: the gains for a commanded climb or sink and its stop (large thrust swings: the engine has to spool a long
		// way and answers as 0.2.1-0.2.7 assumed, 0.35 s + the spool time); the hold's own small corrections use the faster
		// kVRate / kVHold
		private float kVRateL = 1f;

		// the furthest the flight-path regime's anchored reference may be from the jet, m
		private const float PathAnchorMax = 0.5f;

		// 0.2.9 CTOL blend: in STOVL mode at speed the stick, pedals and lever fly as in CTOL mode (the CTOL pitch and lateral
		// laws; lever = thrust), the lift system staying in: the nozzle and fan carry the part of the load the wing does not.
		// CtolW goes 0 -> 1 over VhCtolV0..VhCtolV1 of airspeed in the plane of symmetry (40 -> 60 kt: from the end of the
		// hover's stick and pedal handover, lamV / lamY, to above the game's short take-off speed).
		internal static bool VhCtolBlend = true;

		internal static float VhCtolV0 = 20.6f;

		internal static float VhCtolV1 = 30.9f;

		public float CtolW;

		// the nose-pitch rate the CTOL blend adds to the CTOL law's command to bring the AoA to the reference where the
		// propulsion carries the load (deg/s), and the path held with the stick released
		public float ctolQAoA;

		// the roll-rate window (deg/s) that keeps the CTOL roll command inside the bank the lift can hold
		public float ctolPLo = -999f;

		public float ctolPHi = 999f;

		public float dbgCtolQAoA;

		private float gamHoldC;

		private bool ctolPathInit;

		// the full-dry thrust over the mass (m/s^2), for the lever's stop
		private float aFullDry = 10f;

		// (telemetry: the propulsive load share asked along the lift direction, N; the swivel the CTOL part asks)
		public float dbgCtolFp;

		public float dbgCtolS;

		private readonly Vector3[] gWorld = new Vector3[21];

		private float kVHoldL = 0.75f;

		private float vGainF;

		private float decelFrac;

		// 0.2.8 release guard (ReleaseGuard.cs): the laws are flying the hover regime in the air; guardEpoch counts every
		// time that begins anew (law re-engaged: STOVL on, MPO off; the hover regime re-entered)
		public int guardEpoch;

		private bool hoverWas;

		private bool airNow;

		public bool GuardRegime => state == StovlState.On && !freestyle && lawInit && airNow;

		// 0.2.8 (Tom, 17 Nov 2026: "add wheel brake during conversion on ground, since during conversion it pushes the jet
		// forward"): the wheel brakes are held (Hooks.GearPrefix) on the wheels while the conversion runs, and after it for as
		// long as the lever asks for zero ground speed and nothing asks to lift or roll: the nozzle is still aft or on its way
		// down then and the idle thrust rolls the jet forward. Only at a standstill or a crawl (under 5 m/s), so a conversion
		// during a ground roll is not braked, and not in MPO.
		private bool wheelsNow;

		// Vortex 0.3.11: the brakes also hold through a vertical take-off from the wheels (lever in the aft detent), until the
		// wheels leave the ground
		public bool HoldWheels => owns && wheelsNow && !freestyle && hw != null && (state == StovlState.Converting || state == StovlState.On) && leverSeen <= Plugin.StovlAftDetent + Plugin.StovlDetentWidth + 0.005f && hw.AircraftSpeed < 5f;

		// Vortex 0.3.11: forward acceleration (m/s^2) a vertical take-off's thrust may give along the nozzle on the wheels
		internal static float VtoMaxRollAccel = 0.5f;

		// 0.2.8 conversion guard: the jet's mass and CG as the controller has them (set before RequestToggle)
		public float massSeen;

		public Vector3 cgSeen;

		private float asymWarnT = -100f;

		private const float AsymForceWindow = 10f;

		// (the guard's own clock, s, advanced in PreTick)
		private float guardClock;

		// 0.2.11: the conversion guard's check (see RequestToggle), for the button and the automatic conversion alike: true
		// (with the warning) when the jet is too lopsided for the hover's roll posts. No override.
		private bool ConversionUnsafe(bool button)
		{
			if (!Plugin.ReleaseGuardOn || !HoverRollTrim(massSeen, cgSeen, out float asymNeed, out float asymLim, out float asymY) || !(asymNeed > (asymLim *= GuardShare / AuthorityShare)))
			{
				return false;
			}
			float time = guardClock;
			bool fresh = time - asymWarnT > AsymForceWindow;
			if (button || fresh)
			{
				Say("UNSAFE STOVL - DROP " + ((asymY > 0f) ? "RIGHT" : "LEFT") + " WPN");
			}
			if (fresh)
			{
				asymWarnT = time;
				Plugin.Log.LogInfo($"Vortex FCS STOVL: conversion blocked, roll trim needed {asymNeed:F0} N m against {asymLim:F0} left of the roll posts' hover authority (CG {asymY:F3} m off the lift line, {massSeen:F0} kg).");
			}
			return true;
		}

		// the roll moment (N m) the hover would have to trim for this mass and CG, and what the roll posts allow for it:
		// the posts at the thrust a hover at this weight takes (nozzle at its hover trim, the fan balancing it), one side,
		// times AuthorityShare (75 %, as the release guard; 02:45: was the 25 % left over). Works with STOVL off (doors
		// shut, posts idle).
		// 0.2.8: how far the CG sits to the right of the line the lift acts on (m)
		public bool LiftLineOffset(Vector3 cg, out float y)
		{
			y = 0f;
			if (hw == null || !hw.Valid)
			{
				return false;
			}
			Transform ft = hw.FanThrustT;
			Transform mt = hw.MainThrustT;
			if (ft == null || mt == null)
			{
				return false;
			}
			// (the lift acts in the plane of symmetry, which the lift fan sits in. 03:40 build: the midpoint of the fan and the
			// main nozzle, and the nozzle swings 0.33 m sideways on the yaw bearing: every pedal input read as a 0.16 m lopsided
			// load, 30 deg/s^2 of roll, so the bank loops ran at a fifth of their speed with 1 deg/s^2 of stopping power one
			// way, and the side-velocity loop could not bank against the skid the bearing's side force makes. 09:42 log: pedal
			// turns, 2 kt each way and 6 kt after a release, the bank 1.5 deg for 6.8 commanded during the turn.)
			y = Vector3.Dot(cg - ft.position, ((Component)(object)ac).transform.right);
			return true;
		}

		// 0.2.8: the roll loops' stopping power, by direction. A lopsided jet holds its load with part of the roll authority
		// (one 500 kg store off an outer pylon at 13.6 t: the posts at 95 %, 02:55 log), and what is left to stop a roll
		// toward the heavy side is the rest, not the whole: the bank loop planned its stops on the whole (and no less than
		// 10 deg/s^2), so a full side-stick input toward the heavy side went to 20.5 deg for 13.9 commanded with the posts
		// at their stop for 7 s, and the jet slid off at 30 kt. aP stops a roll to the right, aN one to the left: the share of
		// what the whole travel makes less the load (full, load: deg/s^2), and no more than the loops' budget aRoll.
		internal static bool VhRollLoad = true;

		public Vector3 dbgRollStop;

		public float dbgRollWeak = 1f;

		private float phiDotS;

		private bool phiDotSInit;

		public Vector3 dbgAuthRaw;

		private static void RollStopPower(float aRoll, float full, float load, out float aP, out float aN)
		{
			aP = Mathf.Min(aRoll, AuthorityShare * Mathf.Max(0f, full - load));
			aN = Mathf.Min(aRoll, AuthorityShare * Mathf.Max(0f, full + load));
		}

		public bool HoverRollTrim(float mass, Vector3 cg, out float need, out float limit, out float yOff)
		{
			need = 0f;
			limit = 0f;
			yOff = 0f;
			if (hw == null || !hw.Valid || mass < 1000f || hw.Posts == 0)
			{
				return false;
			}
			Transform root = ((Component)(object)ac).transform;
			Transform ft = hw.FanThrustT;
			Transform mt = hw.MainThrustT;
			if (ft == null || mt == null || !NozzleAt(sHov, 0f, out var d, out var pn))
			{
				return false;
			}
			Vector3 m4 = root.InverseTransformDirection(Vector3.Cross(d, -(pn - cg)));
			Vector3 m6 = root.InverseTransformDirection(Vector3.Cross(ft.forward, -(ft.position - cg)));
			float fk = (Mathf.Abs(m6.x) > 1E-06f) ? Mathf.Max(0f, (0f - m4.x) / m6.x) : 0f;
			float gy = Vector3.Dot(d + ft.forward * fk, root.up);
			if (gy < 0.1f)
			{
				return false;
			}
			float tMx = Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * sHov) * Mathf.Max(0.05f, hw.AltFactor));
			float ratio = Mathf.Min(mass * 9.81f / gy, tMx) / Mathf.Max(1f, hw.MaxThrust);
			float mPost = 0f;
			for (int i = 0; i < hw.Posts; i++)
			{
				Transform t = hw.PostT(i);
				if (t == null)
				{
					continue;
				}
				float f = hw.PostMax(i) * ratio * Mathf.Abs(hw.PostRollFactor(i));
				mPost = Mathf.Max(mPost, f * Mathf.Abs(Vector3.Dot(t.position - cg, root.right)));
			}
			limit = AuthorityShare * mPost;
			// (the plane of symmetry, as LiftLineOffset: the nozzle's point moves with the yaw bearing)
			yOff = Vector3.Dot(cg - ft.position, root.right);
			need = Mathf.Abs(mass * 9.81f * yOff);
			return limit > 0f;
		}

		// the pilot's lever as last read (for the reconversion: see leverZero)
		private float leverSeen = 1f;

		// 0.2.8: set when STOVL is switched off with the lever in or behind the aft detent (the STOVL scale's zero): the
		// throttle module (ThrottleDetent) moves the lever to 0 once, so the CTOL throttle is at idle, not at the 20 % the
		// detent sits at
		public bool leverZero;

		private float tiltBudgetDn = 99f;

		internal static bool VhGovernDown = true;

		internal static bool VhSwivelSlew = true;

		private bool decPhiLast;

		private bool decThLast;

		private float phiG;

		private bool phiGInit;

		private bool incPhiLast;

		private bool incThLast;

		// caps on the attitude effectors' lift-losing travel (roll posts either way, the fan's pitch input toward less fan)
		public float postCap = 1f;

		public float fanCap = 1f;

		// (0.2.8, 02:40: the caps bound a burst, not the standing part. The slow part of each input, which the thrust has
		// long since made up for, is tracked here and the cap is taken around it. Taken from zero, as first built, the cap
		// took the roll posts' trim away from a lopsided jet: bench, 800 kg on the outer wing, 56 % of the posts' authority
		// to trim, flipped in the hover with the cap at its floor; 0.2.7 and the build without the caps held 900 kg.)
		public float postTrim;

		public float fanTrim;

		public float dbgGovLam = 1f;

		// 0.2.8 (height hold, Tom 17 Nov 2026: "keep the same altitude", any dip very small and flown back quickly, the vertical
		// axis first): bench switches for the parts of it, all on
		internal static bool VhIndiNozzleFan = true;

		internal static bool VhKnownFF = true;

		internal static bool VhPathAnchor = true;

		// the nozzle + fan-balance force (what the thrust fit commands), low-passed as the measured acceleration is
		private Vector3 fNfF;

		private float tauV = 0.5f;

		private float kVRate = 1f;

		private float kVHold = 0.5f;

		private float kVInt = 0.125f;

		public float dbgTauV => tauV;

		// 0.2.2: the direction the released-stick stop is moving in (+1 climbing, -1 sinking, 0 holding)
		private float holdDir;

		private float holdK = 0.8f;

		private float holdA = 2f;

		public float dbgWPed;

		// 0.2.0: the bank the lift can hold in a level turn (deg), for the telemetry
		public float dbgPhiMax;

		// 0.2.0: the STOVL laws' airbrake command (0..1), read by the controller with the pilot's brake key
		public float brakeAuto;

		// the wing's level-flight AoA band (deg) where the released stick goes from flight-path hold to attitude hold
		private const float PitchHoldAoA0 = 10f;

		private const float PitchHoldAoA1 = 14f;

		private bool lawInitTrc;

		public void Law(ref LawIn I)
		{
			if (freestyle)
			{
				// (0.2.9: no CTOL blend in freestyle: the pilot has the throttle and nozzle there already)
				CtolW = 0f;
				ctolQAoA = 0f;
				FreeLaw(ref I);
				return;
			}
			float dt = I.dt;
			Transform root = ((Component)(object)ac).transform;
			Vector3 up = Vector3.up;
			Vector3 fwd = root.forward;
			Vector3 hdg = new Vector3(fwd.x, 0f, fwd.z);
			if (hdg.sqrMagnitude < 1E-06f)
			{
				hdg = new Vector3(root.up.x, 0f, root.up.z) * -1f;
			}
			hdg.Normalize();
			Vector3 rgt = new Vector3(hdg.z, 0f, 0f - hdg.x);
			float psi = Mathf.Atan2(hdg.x, hdg.z) * 57.29578f;
			Vector3 vSurf = hw.SurfaceVelocity;
			Vector3 vg = I.vel - vSurf;
			float u = Vector3.Dot(vg, hdg);
			float v = Vector3.Dot(vg, rgt);
			float gs = new Vector2(vg.x, vg.z).magnitude;
			float hdot = I.vel.y;
			Vector3 vAir = I.vel - I.wind;
			float vA = vAir.magnitude;
			float gam = (vA > 1f) ? (Mathf.Asin(Mathf.Clamp(vAir.y / vA, -1f, 1f)) * 57.29578f) : 0f;
			// 0.1.6: the airspeed in the plane of symmetry (the side component taken out): what the wing and the surfaces see.
			// A sideways skid has airspeed but no wing lift and no roll authority from it
			Vector3 rgtB = root.right;
			float vSym = (vAir - rgtB * Vector3.Dot(vAir, rgtB)).magnitude;
			float g = 9.81f;
			// Vortex 0.5.0: the vane box's geometry this tick
			vnOn = VanesOn;
			vnDriveNow = 1f;
			if (vnOn)
			{
				vnRest = hw.FanRestForward;
				vnRight = root.right;
				vnStock = Mathf.Atan2(Vector3.Dot(vnRest, root.forward), Vector3.Dot(vnRest, root.up)) * 57.29578f;
				vnLo = Plugin.StovlVaneMin;
				vnHi = Mathf.Max(vnLo + 1f, Plugin.StovlVaneMax);
				vnDriveNow = VaneDrive(hw.VaneNow);
				if (!vHovInit)
				{
					vHovInit = true;
					vHov = Mathf.Clamp(vnStock, vnLo, vnHi);
				}
			}
			if (!lawInit)
			{
				vaneOut = vnOn ? Mathf.Clamp(hw.VaneNow, vnLo, vnHi) : 0f;
				incVaneLast = false;
				tVaneLast = 0f;
				vPrio = 1f;
				hInt = 0f;
				lawInit = true;
				guardEpoch++;
				hoverWas = false;
				// 0.1.7: the released-stick attitude hold starts from the attitude the law takes over at (0.1.4-0.1.6 caught it
				// only below the hold band, so a conversion inside it, as every automatic one at 300 km/h, held a stale one:
				// bench, 330 km/h: nose 4.6 -> 0 deg at -2.9 deg/s, the jet then sinking)
				thetaHold = I.theta;
				lawInitTrc = false;
				phiGInit = false;
				phiDotSInit = false;
				holdDir = 0f;
				aTrcStopInit = false;
				aTiltInit = false;
				aTiltFullInit = false;
				fitWz = 0.25f;
				hRef = I.pos.y;
				hHold = false;
				gamRef = gam;
				vRef = vA;
				vHold = false;
				psiRef = psi;
				psiHold = false;
				phiHold = false;
				posHold = false;
				thetaRefF = I.theta;
				thetaAssist = 0f;
				alphaRefF = I.alpha;
				fPropInit = false;
				throttleCmdF = I.throttle;
				swivelTarget = hw.Swivel;
				rateFFInit = false;
				posLastT = -1f;
				aTurnInit = false;
				psiDotFF = 0f;
				psiPedLag = 0f;
				fReqYInit = false;
				retPhiLast = 0f;
				retThLast = 0f;
			}
			// Vortex 0.4.7 (StovlOriginShiftFix): the game re-centres its world on the player now and then (FloatingOrigin: once a
			// coordinate passes its threshold, 1024 m by default, every root transform is moved back by the position rounded to
			// 64 m), so every position jumps in one tick while the velocities stay. The held spot and the held height are
			// positions: they move by the same jump (a change of position in one tick that the jet's own velocity does not
			// explain, 8 m or more = 480 m/s). 0.1.0-0.4.6 left them behind: 22:54 log, 1.5 s after lifting off a ship doing
			// 16 m/s, the hold's speed commands went 0.09 -> 2.00 m/s (their limit) in one tick with the jet at 0.15 m/s over the
			// deck, and it flew off at 2.8 m/s with 8.7 deg of bank until a stick input dropped the hold.
			if (Plugin.StovlOriginShiftFix && posLastT >= 0f && I.time - posLastT < 0.1f)
			{
				Vector3 jump = I.pos - posLast - I.vel * dt;
				if (jump.sqrMagnitude > 64f)
				{
					posRef += jump;
					hRef += jump.y;
					dbgOriginJumps++;
				}
			}
			posLast = I.pos;
			posLastT = I.time;
			// ---- ground contact
			wowT = I.wow ? (wowT + dt) : 0f;
			airT = I.wow ? 0f : (airT + dt);
			float tc = Plugin.StovlCenterDetent;
			float ta = Plugin.StovlAftDetent;
			float tw = Plugin.StovlDetentWidth;
			// ---- regime blends (ground speed, knots as the F-35B's: 30-40 kt for the stick and pedals, 25-35 kt for the throttle)
			// 0.1.6: on the forward (or aft) ground speed, not the total: a sideways skid stays in the hover laws, where the
			// released stick nulls the side velocity (translational rate command) with the height held. 0.1.5 took a 30 kt skid
			// as forward flight: bank mode, wings level with the stick released, nothing stopped the skid, and the bank flown
			// by hand to stop it came out of the lift (20:50 log). The roll-rate regime goes on the airspeed in the plane of
			// symmetry for the same reason. With no sideways motion all of these read as before.
			// (0.2.1: forward only: backward flight is always the hover regime; with |u| a 30 kt backward command crossed the
			// 25-35 kt throttle blend into the accelerating regime, where the lever at the stop asked for more, and settled at -35 kt)
			float uAbs = Mathf.Max(0f, u);
			// Vortex 0.4.1 (StovlRegimeShift, Tom 17 Nov 20:58): the hover hand-overs and the CTOL blend all come this much later
			// (20 kt by default: lever ground-speed command to 45-55 kt, stick and pedals 50-60, side-velocity hold 35-50, CTOL
			// blend 60-80 kt airspeed), so a +40-45 kt ground-speed command stays a ground-speed command. 0 = 0.4.0.
			float sh = Plugin.StovlRegimeShiftKt / 1.944f;
			float lamV = 1f - Smooth(15.4f + sh, 20.6f + sh, uAbs);
			float lamT = 1f - Smooth(12.9f + sh, 18f + sh, uAbs);
			float lamY = 1f - Smooth(15.4f + sh, 20.6f + sh, uAbs);
			float lamTrc = 1f - Smooth(7.7f + sh, 15.4f + sh, uAbs);
			float lamRoll = Smooth(51.4f, 66.9f, vSym);
			// 0.2.9: the CTOL blend's weight (see VhCtolBlend); the controller runs the CTOL laws from the next tick on
			float wC = VhCtolBlend ? Smooth(VhCtolV0 + sh, VhCtolV1 + sh, vSym) : 0f;
			CtolW = wC;
			if (!I.ctolOn)
			{
				wC = 0f;
			}
			if (wC <= 0f)
			{
				ctolQAoA = 0f;
				ctolPathInit = false;
				dbgCtolFp = 0f;
			}
			// the lever as the laws read it (EffThrottle: neutral after a conversion until moved)
			slowRegime = lamT > 0.5f;
			float thrE = EffThrottle(I.throttle);
			leverSeen = I.throttle;
			dbgLamV = lamV;
			dbgLamT = lamT;
			{
				bool hov = lamV > 0.5f;
				if (hov && !hoverWas)
				{
					guardEpoch++;
				}
				hoverWas = hov;
				airNow = !I.wow;
			}
			dbgGs = gs;
			dbgVs = hdot;
			// ---- vertical authority (Vortex 0.1.1): the climb acceleration the jet has at full dry thrust with the nozzle where
			// it is (95 %: the rest is kept for the yaw nozzle and the roll posts, which both take vertical force), from the live
			// thrust chain and mass, so a heavier jet gets smaller climb and sink rates and less tilt for translation. The sink
			// rate is held to one the jet can slow from to the gear's touchdown limit before the wheels touch (engine + loops
			// ~0.5 s, then that acceleration).
			{
				// the capability is taken with the nozzle at its hover trim (learned in the steady hover, 0.85 to start) or the
				// present swivel if further down, where the law can always put it back:
				// with the current swivel, an acceleration (nozzle aft) read as lost lift and took the climb away from the
				// height hold, which then flew the jet into the runway on a full-throttle short take-off (bench v2)
				float sCap = Mathf.Max(hw.Swivel, sHov);
				NozzleAt(sCap, (yaw != null) ? yaw.AppliedInput : 0f, out var dV, out var pV);
				// (Vortex 0.5.0: with the vane box, the fan's thrust line where the hover keeps it, vHov, which the law can always
				// put back, as the nozzle's hover trim)
				Vector3 fanCapDir = vnOn ? FanDirAt(vHov) : ((hw.FanThrustT != null) ? hw.FanThrustT.forward : Vector3.zero);
				float capDrive = vnOn ? VaneDrive(vHov) : 1f;
				float fkV = vnOn ? FanForceAt(1f, dV, pV, 0f, fanCapDir) : FanForce(1f, dV, pV, 0f);
				Vector3 gV = dV + ((hw.FanThrustT != null) ? (fanCapDir * fkV) : Vector3.zero);
				float tMx = Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * sCap * capDrive) * Mathf.Max(0.05f, hw.AltFactor));
				float fvMax = 0.95f * tMx * Mathf.Max(0f, gV.y);
				fvMaxNow = fvMax;
				aFullDry = tMx / Mathf.Max(1f, I.mass);
				// 0.2.5: for the bank limit, the propulsive lift along the body's up axis (what tilts with the bank); fvMax is
				// the world-vertical part, already cut by cos(bank), so a limit taken from it shrank as the jet banked (00:24
				// bench: 30 -> 20 deg during the roll-in, and the roll ran past it to 28)
				fLiftBodyMax = 0.95f * tMx * Mathf.Max(0f, Vector3.Dot(gV, ((Component)(object)ac).transform.up));
				// 0.2.1: the vertical axis's thrust response time and the height-hold gains derived from it (no fixed gains):
				// tau = the RPM lead the throttle command is over-driven with (RpmLead) + the spool time for the RPM a 1 m/s^2
				// vertical step takes (mass / (vertical force per rpm) / spool rate); rate gain 1 / (2 tau) (damping 0.707 against
				// that lag), height gain half of it (0.707 on the rate loop's response), trim integral a quarter of the height gain.
				// 12 t, nozzle down: tau ~0.5 s, gains ~1.0 / 0.5 / 0.13 (0.1.x-0.2.0: fixed 1.2 / 0.8 / 0.5)
				RpmLead = (VhLeadFix > 0f) ? VhLeadFix : (2f * (0.05f + dt));
				float fvPerRpm = fvMax / Mathf.Max(1f, hw.MaxRpm - hw.MinRpm);
				float spoolT = Mathf.Max(1f, I.mass) / Mathf.Max(1f, fvPerRpm) / Mathf.Max(1f, hw.SpoolRate);
				tauV = Mathf.Clamp(RpmLead + spoolT, 0.2f, 1.5f);
				kVRate = 1f / (2f * tauV);
				kVHold = VhHoldRatio * kVRate;
				// Vortex 0.4.0 (plan item A10): the large-swing gains on the measured thrust lead, as tauV (StovlClimbGainFromLead;
				// false = the 0.35 s of 0.2.1-0.3.15)
				float tauVL = Mathf.Clamp((Plugin.StovlClimbGainFromLead ? RpmLead : 0.35f) + spoolT, 0.2f, 1.5f);
				kVRateL = 1f / (2f * tauVL);
				kVHoldL = 0.75f * kVRateL;
				kVInt = 0.25f * kVHold;
				aUpAvail = fvMax / Mathf.Max(1f, I.mass) - 9.81f;
				// (the translation tilt budget is worked out below from fLiftBodyMax and the vertical demand: 0.2.6; 0.1.1-0.2.5
				// gave it half the spare lift, tiltMax)
				dbgGVy = gV.y;
				dbgTMx = tMx;
				// the yaw nozzle's deflection costs vertical thrust: with little spare lift the yaw acceleration budget is cut
				// so a pedal input or a yaw stop does not sink the jet (the spare lift is taken with the yaw nozzle centred)
				NozzleAt(sCap, 0f, out var dV0, out var pV0);
				float fk0 = vnOn ? FanForceAt(1f, dV0, pV0, 0f, fanCapDir) : FanForce(1f, dV0, pV0, 0f);
				Vector3 g0 = dV0 + ((hw.FanThrustT != null) ? (fanCapDir * fk0) : Vector3.zero);
				float aUp0 = 0.95f * tMx * Mathf.Max(0f, g0.y) / Mathf.Max(1f, I.mass) - 9.81f;
				yawLiftK = Mathf.Clamp(aUp0 / 1f, 0.25f, 1f);
				// 0.2.8 governor: the thrust-rate headroom, from the thrust the fit asked last tick against full dry (95 %, as
				// the capability above): (T_max - T) / T / lag is the rate, per second, at which the lift can still grow. A tilt
				// loses lift at tan(tilt) x its rate, so the bank and the pitch tilt may only come in as fast as a share of
				// that (they slow down by themselves as the thrust nears full dry and never ask for lift it cannot make in
				// time); going back toward level is free. The same headroom, as an acceleration, bounds what a roll post or a
				// fan pitch burst may take: a step loss d met by lift rising at r costs about (2/3) d^3 / r^2 of height.
				float tMxNow = Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * hw.Swivel * vnDriveNow) * Mathf.Max(0.05f, hw.AltFactor));
				float ratioReq = Mathf.Clamp(dbgThrustReq / tMxNow, 0.05f, 1f);
				float hroom = Mathf.Max(0f, 0.95f - ratioReq) / ratioReq;
				tiltBudget = VhTiltShare * hroom / EngineLag;
				// (and the other way: a tilt coming out gives lift back, which the thrust has to shed: it falls as the same lag
				// toward idle, within the spool rate)
				float idleR = Mathf.Clamp01(0.15f * hw.MinRpm / Mathf.Max(1f, hw.MaxRpm - hw.MinRpm));
				float spoolR = hw.SpoolRate / Mathf.Max(1f, hw.MaxRpm - hw.MinRpm);
				tiltBudgetDn = VhTiltShare * Mathf.Min(Mathf.Max(0f, ratioReq - idleR) / EngineLag, spoolR) / ratioReq;
				float aPv = Mathf.Max(0f, Vector3.Dot(fNfF, Vector3.up)) / Mathf.Max(1f, I.mass);
				float rUp = aPv * hroom / EngineLag;
				float dCap = Mathf.Pow(1.5f * VhEventDip * rUp * rUp, 1f / 3f);
				float postF = 0f;
				for (int pi = 0; pi < hw.Posts; pi++)
				{
					postF = Mathf.Max(postF, hw.PostMax(pi) * hw.ThrustRatio);
				}
				float fanF = Mathf.Abs(hw.FanPitchFactor) * hw.Doors * hw.RpmRatio;
				fanFNow = fanF;
				postTrim += (postIn - postTrim) * (dt / (EngineLag + dt));
				fanTrim += (fanIn - fanTrim) * (dt / (EngineLag + dt));
				postCap = (VhEffCaps && fPropInit && !I.wow) ? Mathf.Clamp(dCap * I.mass / Mathf.Max(1f, postF), 0.2f, 1f) : 1f;
				fanCap = (VhEffCaps && fPropInit && !I.wow) ? Mathf.Clamp(dCap * I.mass / Mathf.Max(1f, fanF), 0.1f, 1f) : 1f;
			}
			if (I.wow)
			{
				raGround = (raGroundN == 0) ? I.radarAlt : Mathf.Lerp(raGround, I.radarAlt, 0.05f);
				raGroundN = 1;
			}
			float hAgl = I.radarAlt - raGround;
			float vTd = Mathf.Clamp(I.touchdownMax, 0.5f, Plugin.StovlSinkRate);
			vTdNow = vTd;
			// braking envelope: v*tau + (v^2 - vTd^2) / 2a = height left, with 70 % of the spare lift (the rest covers the loop
			// error, gusts and the mass estimate) and tau 1 s for the engine spool and the height loop; the envelope's own
			// deceleration is fed forward below so the sink follows it down rather than trailing it
			aStop = Mathf.Max(0.3f, 0.7f * aUpAvail);
			float hRoom = Mathf.Max(0f, hAgl - 0.3f);
			float vStop = aStop * (-StopLag + Mathf.Sqrt(StopLag * StopLag + 2f * (hRoom + vTd * vTd / (2f * aStop)) / aStop));
			float sinkLim = Mathf.Max(vTd, Mathf.Min(Plugin.StovlSinkRate, vStop));
			vStopNow = vStop;
			// 0.2.6: vertical first. The vertical axis's capacity is the lift along the body's up axis (the translation tilt
			// yields to it), not the world-vertical part left at the present tilt (aUpAvail), which a translation had taken.
			float aUpBody = fLiftBodyMax / Mathf.Max(1f, I.mass) - 9.81f;
			float climbLim = Plugin.StovlClimbRate * Mathf.Clamp01(aUpBody / 1f);
			// 0.2.6: the translation budget (side and fore-aft together): everything the full-dry lift can give horizontally
			// once the vertical axis has what it asks, a_h = sqrt(a_max^2 - (g + a_v)^2), with a_v = the height loop's demand
			// (a climb command or a sink stop takes it back from the translation) + the lift the thrust lag loses while the
			// translation bank comes in at StovlTrcBankRate (d(g sec(tilt)) / dt over the thrust response time tauV). 0.2.5 gave
			// the translation half the spare lift whatever the vertical axis needed (11.9 t hover: 19 deg / 3.3 m/s^2 of a
			// possible 26 deg / 4.8); 0.1.x-0.2.4 also capped it at 0.15 g.
			// (0.4.0: rollWeak, 0.2.8, worked out here, ahead of the budget: see the comment above the position hold)
			float rollWeak;
			{
				float aRb = Mathf.Clamp(AuthorityShare * I.authority.x, 10f, 200f);
				RollStopPower(aRb, VhRollLoad ? I.rollFull : 1000f, VhRollLoad ? I.rollLoad : 0f, out float aWp, out float aWn);
				rollWeak = Mathf.Clamp01(Mathf.Min(aWp, aWn) / aRb);
				aRollWeakNow = Mathf.Max(0f, Mathf.Min(aWp, aWn));
			}
			dbgRollWeak = rollWeak;
			float kRollS = Mathf.Sqrt(rollWeak);
			dbgKRollS = kRollS;
			// Vortex 0.4.0 (group B): each tilt axis's angular-acceleration budget, for the tilt rates (roll with the one-sided-
			// load factor of 0.2.8)
			// (low-passed TiltAuthTau: the posts' and the fan's authority goes with the thrust, which rises as a tilt comes in and
			// falls as it goes out; taken raw, a stop was planned on the rate of the moment and flown out on a lower one: bench,
			// before the ceiling, a 15 kt sideways stop ran 0.27 m/s past zero against 0.15)
			float aTiltRollN = Mathf.Clamp(AuthorityShare * I.authority.x, 10f, 200f);
			float aTiltPitchN = Mathf.Clamp(AuthorityShare * I.authority.y, 8f, 120f);
			// Vortex 0.4.2 (StovlTiltLiftLimit): the pitch authority the fore-aft tilt may plan on is what the vertical axis can
			// spare of it. In the hover the pitch moment is the lift fan's pitch term, and every newton of that is lift: a pitch
			// rate started or stopped moves the vertical speed by a fixed amount per deg/s (the fan's lift per unit of pitch
			// acceleration, ~0.025 m/s per deg/s here) unless the thrust makes it up, and the thrust can only add lift at
			// (spare lift) / (engine lag). So the fan authority counted is the lift step the thrust recovers within StovlTiltLiftDip of
			// height (0.2.8's event cap, d = (1.5 dip r^2)^(1/3), here with r from the hover's own spare lift, not the thrust of
			// the moment: taken from that, the cap opened whenever the thrust was cut for a nose-up burst and the nose-down
			// burst that followed found it open), plus whatever else pitches the jet without touching the lift (surfaces, the
			// pitch nozzle). Bench, Stock 12.8 t (0.47 m/s^2 spare), fore-aft stop from 14 kt at 10 deg/s: the nose-up tilt was
			// stopped with 8.6 kN less fan while the thrust, cut 10 % for the tilt's start, was still spooling back: 0.45 m lost,
			// the budget took the tilt out again and the stop went round that three times (9.2 s); at 4.5 deg/s 0.05 m, 5.3 s.
			dbgTiltPitchLift = aTiltPitchN;
			if (Plugin.StovlTiltLiftLimit && I.fanPitchAuth > 0f && !I.wow)
			{
				float rUpS = Mathf.Max(0f, fLiftBodyMax / Mathf.Max(1f, I.mass) - 9.81f) / EngineLag;
				float dCapS = Mathf.Pow(1.5f * Mathf.Clamp(Plugin.StovlTiltLiftDip, 0.01f, 2f) * rUpS * rUpS, 1f / 3f);
				float fanShare = Mathf.Clamp01(dCapS * I.mass / Mathf.Max(1f, fanFNow));
				float fanHalf = 0.5f * (Mathf.Min(1f, Mathf.Max(0f, fanTrim) + fanCap) + 1f) * I.fanPitchAuth;
				float aOther = Mathf.Max(0f, I.authority.y - fanHalf);
				float aLift = AuthorityShare * (aOther + fanShare * I.fanPitchAuth);
				dbgTiltPitchLift = aLift;
				aTiltPitchN = Mathf.Min(aTiltPitchN, aLift);
			}
			if (!aTiltInit)
			{
				aTiltInit = true;
				aTiltRollF = aTiltRollN;
				aTiltPitchF = aTiltPitchN;
			}
			aTiltRollF += (aTiltRollN - aTiltRollF) * (dt / (TiltAuthTau + dt));
			aTiltPitchF += (aTiltPitchN - aTiltPitchF) * (dt / (TiltAuthTau + dt));
			float aTiltRoll = aTiltRollF;
			float aTiltPitch = aTiltPitchF;
			// Vortex 0.4.2 (StovlTiltRollFull): with a one-sided load the roll tilt's budget is sqrt(weak side's stopping power x
			// the roll authority over the effectors' whole travel): the balanced jet's response stretched in time by
			// 1 / sqrt(weak / whole), as 0.2.8 states it. 0.4.0 took the authority around the posts' present trim (already cut
			// by the load: 17.6 of 21.2 deg/s^2 with 500 kg on a wing) and scaled that by sqrt(weak / itself), counting the
			// load twice: 3.5 deg/s of bank where the rule gives 4.0 (0.3.15: 4.9, which the weak side took 1.15 s to stop).
			// On a balanced jet the two are the same number.
			if (Plugin.StovlTiltRollFull && VhRollLoad)
			{
				if (!aTiltFullInit)
				{
					aTiltFullInit = true;
					aTiltFullF = Mathf.Clamp(AuthorityShare * Mathf.Max(I.authority.x, I.rollFull), 10f, 200f);
				}
				aTiltFullF += (Mathf.Clamp(AuthorityShare * Mathf.Max(I.authority.x, I.rollFull), 10f, 200f) - aTiltFullF) * (dt / (TiltAuthTau + dt));
				aTiltRoll = Mathf.Sqrt(Mathf.Min(aRollWeakNow, aTiltFullF) * aTiltFullF);
			}
			else
			{
				aTiltRoll *= kRollS;
			}
			// ---- (0.4.7: moved up from the thrust fit, unchanged, for the translation budget) directions the system can point
			// its force, swivel 0..1
			Transform tt = hw.MainThrustT;
			float sNow = hw.Swivel;
			float tvcYaw = 0f;
			int nG = gS.Length;
			Transform fT = hw.FanThrustT;
			// (Vortex 0.5.0: with the vane box the table is swivel x vane angle, g2Dir / g2Lift; the swivel-only one below is then
			// the fan along the game's own fixed line, the vanes' rest, which the CTOL blend uses as it always has)
			Vector3 fanAxisDir = Vector3.zero;
			if (vnOn)
			{
				fanAxisDir = FanDirAt(Mathf.Clamp(vnStock, vnLo, vnHi));
				for (int j = 0; j < NV; j++)
				{
					vG[j] = vnLo + (vnHi - vnLo) * (float)j / (float)(NV - 1);
					vDrive[j] = VaneDrive(vG[j]);
					vnDir[j] = FanDirAt(vG[j]);
				}
			}
			for (int k = 0; k < nG; k++)
			{
				float s = (float)k / (float)(nG - 1);
				gS[k] = s;
				NozzleAt(s, tvcYaw, out var d, out var pn);
				float fk = vnOn ? FanForceAt(1f, d, pn, 0f, fanAxisDir) : FanForce(1f, d, pn, 0f);
				Vector3 gW = d + ((fT != null) ? ((vnOn ? fanAxisDir : fT.forward) * fk) : Vector3.zero);
				if (vnOn)
				{
					for (int j = 0; j < NV; j++)
					{
						float fkj = FanForceAt(1f, d, pn, 0f, vnDir[j]);
						Vector3 gWj = d + vnDir[j] * fkj;
						float gy = Vector3.Dot(gWj, up);
						float gsd = Vector3.Dot(gWj, rgt);
						g2Dir[k * NV + j] = new Vector3(0f, gy, Vector3.Dot(gWj, hdg));
						g2Lift[k * NV + j] = Mathf.Sqrt(gy * gy + gsd * gsd);
					}
				}
				gWorld[k] = gW;
				Vector3 gB = new Vector3(0f, Vector3.Dot(gW, up), Vector3.Dot(gW, hdg));
				gDir[k] = gB;
				gAng[k] = Mathf.Atan2(gB.y, gB.z) * 57.29578f;
				gMag[k] = new Vector2(gB.y, gB.z).magnitude;
				float gSide = Vector3.Dot(gW, rgt);
				gLift[k] = Mathf.Sqrt(gB.y * gB.y + gSide * gSide);
			}
			float aVbud;
			float aH;
			float aHlevel;
			{
				float g0 = 9.81f;
				float aMaxT = fLiftBodyMax / Mathf.Max(1f, I.mass);
				// 0.2.8: what the vertical axis keeps back from the translation is its command's demand (a climb on the stick, a
				// sink being stopped) and the lift to fly a height deficit back, not the loop's whole demand of the moment: that
				// carries the rate damping, which rises as the tilt comes in and falls as it goes out, so the budget and the
				// tilt chased each other (bench, side + forward stop released together: pitch target 3 <-> 13 deg, nozzle
				// 0.8 <-> 1.0, every 4.6 s, the vertical speed swinging -0.6..+0.6 m/s with it)
				float aVres = Mathf.Max(0f, VhReserveFromError ? ((hHold && holdDir == 0f) ? (kVRate * kVHold * hErrLast) : aVhLast) : aVhLast);
				float aH0 = Mathf.Sqrt(Mathf.Max(0f, aMaxT * aMaxT - (g0 + aVres) * (g0 + aVres)));
				float tl = Mathf.Atan2(aH0, g0 + aVres);
				// (0.4.0: at the faster axis's tilt rate)
				float rateBud = TiltRate(Mathf.Max(aTiltRoll, aTiltPitch), Plugin.StovlTrcBankRate);
				float aLag = g0 * Mathf.Tan(tl) / Mathf.Max(0.2f, Mathf.Cos(tl)) * rateBud * 0.0174532924f * tauV;
				float aV = g0 + aVres + aLag;
				aH = Mathf.Sqrt(Mathf.Max(0f, aMaxT * aMaxT - aV * aV));
				aVbud = ((VhFwdCapFromFit && fReqYInit) ? (fReqYF / Mathf.Max(1f, I.mass)) : (g0 + aVres)) + VhFwdCapLag * aLag;
				// (the same with no vertical demand: the hover's own stopping capability, for where a stop comes to rest)
				aHlevel = Mathf.Sqrt(Mathf.Max(0f, aMaxT * aMaxT - (g0 + aLag) * (g0 + aLag)));
				dbgAVres = aVres;
			}
			dbgAH = aH;
			dbgAHlevel = aHlevel;
			dbgAUpBody = aUpBody;
			dbgUFwd = u;
			dbgVSide = v;
			// (0.2.7: the height loop's own demand is already taken out of a_h above, so the translation no longer also shrinks
			// on the sink itself (vPrio, 0.1.1-0.2.6): with both, a 30 kt stop's pitch went 17 -> 11 -> 19 deg as a 0.4 m/s sink
			// came and went; vPrio still logs the shortfall)
			float aHv = Mathf.Max(aH, 0.2f);
			dbgAUp = aUpAvail;
			dbgSinkLim = sinkLim;
			dbgClimbLim = climbLim;
			// ---- longitudinal: the throttle
			float gsMax = Plugin.StovlCenterGroundSpeed / 1.944f;
			float gsCmd;
			if (Mathf.Abs(thrE - ta) <= tw)
			{
				gsCmd = 0f;
			}
			else if (thrE > ta)
			{
				gsCmd = (thrE - (ta + tw)) / Mathf.Max(0.01f, tc - (ta + tw)) * gsMax;
			}
			else
			{
				// 0.2.1: StovlBackGroundSpeed (30 kt) at the stop over the travel behind the aft detent (0.20 by default); 0.2.0 used
				// the forward scale over 10 % (~6 kt), 0.1.x 30 kt over 25 %
				gsCmd = -((ta - tw) - thrE) / Mathf.Max(0.01f, ta - tw) * (Plugin.StovlBackGroundSpeed / 1.944f);
			}
			// 0.1.5: what the lever commands, for the HUD throttle readout (HudThrottle): ground speed in the hover regime
			// ("0 GS" in the aft detent), hold / accelerate / decelerate at speed
			if (slowRegime)
			{
				hudText = (gsCmd == 0f) ? "0 GS" : ((gsCmd > 0f) ? ("+" + Mathf.RoundToInt(gsCmd * 1.944f) + " KT") : (Mathf.RoundToInt(gsCmd * 1.944f) + " KT"));
				// Vortex 0.4.1: "SHP" while the speeds are ship-relative (StovlShipRelative, on by default from 0.4.6)
				if (hw.ShipFrameWeight > 0.5f)
				{
					hudText += " SHP";
				}
			}
			else
			{
				hudText = (Mathf.Abs(thrE - tc) <= tw) ? "HOLD SPD" : ((thrE > tc) ? "ACCEL" : "DECEL");
			}
			if (wC >= 0.5f)
			{
				// 0.2.9: the lever is thrust here (CTOL blend)
				hudText = "THR " + Mathf.RoundToInt(CtolLeverThrust(I.throttle) * 100f) + "%";
			}
			// hover position hold: throttle in the aft detent, lateral stick centred, slow: hold the spot (moving with a ship)
			// Vortex 0.4.0: the hold is caught under StovlPosCatchSpeed (0.5 m/s; 0.1.0-0.3.15: 3) and, once it has the spot, kept up to
			// 3 m/s as before (a gust does not let go of it). Caught at up to 3 m/s, a stop that began under that speed (a tap of
			// the stick; full stick on a heavy jet, whose full-stick speed is now that low) was caught at the release with the jet
			// still banked into the move, the rest point came out short and the jet stopped past it and slid back: bench, 0.3.15,
			// 0.4 stick for 1.5 s: 0.7 m back at 11.9 t Buffed, 5.2 m at 1.97 m/s Stock, 4.6 m at 12.8 t.
			// Vortex 0.4.2 (StovlPosKeepSpeed, 0 = no limit): a spot once held is kept whatever the speed: only the lever or the
			// lateral stick gives it up. With the 3 m/s limit a gust of 3 m/s or more let go of it and the jet was left where
			// the gust had put it (bench, 3 m/s sideways kick: 5.0 m off).
			float posKeep = Plugin.StovlPosKeepSpeed;
			bool posWanted = gsCmd == 0f && Mathf.Abs(I.sr) < 0.05f && (posHold ? (posKeep <= 0f || gs < posKeep) : (gs < Mathf.Clamp(Plugin.StovlPosCatchSpeed, 0.2f, 3f))) && !I.wow;
			if (posWanted && !posHold)
			{
				posHold = true;
				// 0.2.7: caught where the jet will come to rest (the stop at the translation budget, after the tilt's lag), not
				// where it is: caught at up to 3 m/s, the hold ran on ~2 m past it and flew back at ~1 kt (bench, 30 kt stop)
				Vector3 vh = new Vector3(vg.x, 0f, vg.z);
				float aStopH = Mathf.Max(aHlevel, 0.5f);
				posRef = I.pos + vh * (vh.magnitude / (2f * aStopH) + 0.5f);
			}
			else if (!posWanted)
			{
				posHold = false;
			}
			// 0.2.8: with a lopsided load the bank cannot be turned around as on a balanced jet: starting a roll away from the
			// heavy side, and stopping one toward it, has only what the load leaves of the roll authority (rollWeak: the weaker
			// direction's stopping power over the loops' budget; 1 on a balanced jet). Everything that plans on the bank - the
			// position hold's gain, the side-velocity profile's bank rate and bank lag, the bank target's rate and the bank
			// loop's gain - runs slower by sqrt(rollWeak), which is the balanced jet's own response stretched in time by
			// 1 / sqrt(rollWeak) (the same angles with rollWeak times the angular acceleration). With the posts at 92 % a
			// 2.4 s side-stick input toward the heavy side went to 19 deg of bank for ~12 commanded, the posts at their stop
			// for 7 s, 25 kt of skid and a +-19 deg, 12 s swing that did not die out (bench with the pitch TVC out of it, as
			// in the game; 02:55 log: 20.5 deg for 13.9, 30 kt).
			float latHold = 0f;
			if (posHold)
			{
				posRef += vSurf * dt;
				Vector3 e = posRef - I.pos;
				gsCmd += Mathf.Clamp(0.3f * Vector3.Dot(e, hdg), -2f, 2f);
				latHold = Mathf.Clamp(0.3f * kRollS * Vector3.Dot(e, rgt), -2f, 2f);
			}
			dbgGsCmd = gsCmd;
			// 0.2.6: the fore-aft and side translation accelerations the hover asks for, sharing the budget: when both together
			// ask for more than it holds, both are scaled down by the same factor (the direction of the asked acceleration is
			// kept), so a sideways command and a fore-aft one divide it instead of one taking it first.
			// side: the side-velocity loop starts and stops as hard as the budget allows: full acceleration until the bank has
			// to come out (the bank moves at StovlTrcBankRate, so the acceleration can fall at g x that rate: a = sqrt(2 j |e|)),
			// and a linear end (1 / (2 tau) on the bank loop's 0.4 s) so it settles without a step. 0.1.x-0.2.5: 0.8 (v_cmd - v),
			// an exponential fade over the last few m/s, capped on its own.
			// Vortex 0.4.0 (group B): full lateral stick asks for the ground speed the hover can stop from within StovlTrcStopTime
			// at its own stopping acceleration (aHlevel: what the full-dry lift leaves horizontally once the weight is carried;
			// low-passed 1 s so fuel burn, the yaw nozzle and thrust noise do not move the command, and no more than the hover's
			// bank limit gives), so it follows mass, fuel, stores, thrust and altitude. StovlTrcStopTime 0 = the fixed StovlTrcSpeed.
			float aStopNow = Mathf.Min(Mathf.Max(aHlevel, 0.2f), 9.81f * Mathf.Tan(Mathf.Clamp(Plugin.StovlBankMax, 1f, 80f) * 0.0174532924f));
			if (!aTrcStopInit)
			{
				aTrcStopInit = true;
				aTrcStopF = aStopNow;
			}
			aTrcStopF += (aStopNow - aTrcStopF) * (dt / (1f + dt));
			float vTrcMax = (Plugin.StovlTrcStopTime > 0f) ? (aTrcStopF * Plugin.StovlTrcStopTime) : (Plugin.StovlTrcSpeed / 1.944f);
			dbgAStop = aTrcStopF;
			dbgTrcVmax = vTrcMax;
			float vCmd = I.sr * vTrcMax + latHold;
			// Vortex 0.5.6 (StovlPedalPirouette; Tom, 18 Nov: "why when i go 40-50kt something and then i yaw, the jet follows a
			// curve instead of yawing at max available yaw rate, fix that"; he chose the flat pirouette). Where the hover's pedal
			// law holds (lamY), the pedal no longer turns the velocity with the nose: while it is held the lever's speed stays
			// along the direction the nose had when the pedal came in (pirDir), the side stick and the position hold still act
			// across the present nose, and both are put into the present nose's axes for the loops below. The jet keeps flying
			// where it was going while the nose turns at the yaw axis's full rate (the turn-rate cap and the turn's own
			// acceleration, 0.4.7, stand aside), and the bank does not fight the turn. The pirouette ends once the pedal is let
			// go and the yaw has stopped; the lever's speed then runs along the new nose again, through the same start / stop
			// profiles (the side speed the jet has across the new nose is taken out as any other). In the hover, where the speed
			// command is zero, nothing changes. 0.4.7-0.5.5: at 45 kt the pedal was held to ~7.5 deg/s so the bank could bring the
			// velocity round with the nose.
			bool pirWant = (Plugin.StovlPedalPirouette != Plugin.PirouetteKeyOn) && !I.wow && lamY > 0.01f && (Mathf.Abs(I.sy) >= 0.05f || (pirOn && Mathf.Abs(psiDotPedal) > 0.5f));
			pirOn = pirWant;
			// Vortex 0.5.8 (Tom, 18 Nov: "with the pirouette logic on, no side skid will be corrected when there is yaw input";
			// on 0.5.6-0.5.7's pirouette, his 18:30 log: after the pedal was let go the jet came level and only "a moment later"
			// was the skid taken out). The translation part now holds only while the pedal is deflected: let go, and the side
			// speed is taken out along the nose at once while the yaw comes to rest (0.5.6-0.5.7 kept the pirouette on until
			// the yaw had stopped, 1.1 s on the bench and up to 2.5 s in the log at 15-40 kt, with the skid left running). The
			// yaw-axis part (no turn-rate cap, the sideslip limit) still lasts until the yaw has stopped, so the nose stops as
			// quickly as before. And the skid the jet already has when the pedal comes in (its side speed less what the side
			// stick commands) is kept through the pedal, not taken out (0.5.6-0.5.7 flew the lever's speed along the nose of
			// that moment, so a skid at pedal-in was corrected during the pedal).
			bool pirX = pirOn && Mathf.Abs(I.sy) >= 0.05f;
			if (pirX && !pirXOn)
			{
				pirDir = hdg;
				pirSkid = rgt * (v - (I.sr * vTrcMax + latHold));
			}
			pirXOn = pirX;
			float wPir = pirX ? lamY : 0f;
			dbgPir = wPir;
			if (wPir > 0f)
			{
				Vector3 cmdE = pirDir * gsCmd + pirSkid + rgt * vCmd;
				gsCmd = Mathf.Lerp(gsCmd, Vector3.Dot(cmdE, hdg), wPir);
				vCmd = Mathf.Lerp(vCmd, Vector3.Dot(cmdE, rgt), wPir);
				dbgGsCmd = gsCmd;
			}
			float eV = vCmd - v;
			// (0.4.0: the profiles' jerk from each axis's tilt rate)
			float rateSideP = TiltRate(aTiltRoll, Plugin.StovlTrcBankRate * kRollS);
			float rateFwdP = TiltRate(aTiltPitch, Plugin.StovlTrcBankRate);
			float jLat = 9.81f * rateFwdP * 0.0174532924f;
			// (the bank loop lags the bank target by ~0.4 s (gain 2.5 / s), so the profile leaves room for it: the acceleration
			// a with a^2 / (2 j) + a tau = |e|; without it a 15 m/s stop ran 1.1 m/s past zero)
			// Vortex 0.3.9 (Tom, 17 Nov: "reduce the start and braking margin for sideways skid by 20 %"): 0.4 -> 0.32 s
			float tauBank = Mathf.Max(0.05f, Plugin.StovlTrcLagMargin);
			// Vortex 0.4.2 (StovlTrcLagFromLoop): and never less than the bank loop's own lag behind a moving target, 1 / its
			// gain (0.4 s at 2.5 / s). 0.3.9 cut the margin to 0.32 s with the loop left at 2.5 / s, so the profile planned on a
			// bank that came out 0.08 s sooner than it does: bench, 15 kt stop, 0.24 m/s past zero and 5.1 s to settle (0.3.15's
			// early position catch hid part of it: 0.15, and slid a tap back 0.7 m instead). With the loop's lag: 0.14, 4.1 s,
			// the stop to 0.3 m/s 0.12 s longer. (StovlBankGainFromMargin is the other way to make the two agree.)
			if (Plugin.StovlTrcLagFromLoop)
			{
				tauBank = Mathf.Max(tauBank, 1f / BankLoopGain());
			}
			float jLatY = (Plugin.StovlTiltStopTime > 0f) ? (9.81f * rateSideP * 0.0174532924f) : (jLat * kRollS);
			float tauBankY = tauBank / Mathf.Max(0.01f, kRollS);
			float aProf = jLatY * (Mathf.Sqrt(tauBankY * tauBankY + 2f * Mathf.Abs(eV) / Mathf.Max(0.0001f, jLatY)) - tauBankY);
			float aLatW = Mathf.Sign(eV) * Mathf.Min(Mathf.Abs(eV) / (2f * tauBankY), aProf);
			// fore-aft: the same start / stop profile, on the pitch tilt (StovlTrcBankRate) and the pitch loop's 0.5 s (gain 2):
			// 0.1.x-0.2.6 used 0.6 (gs_cmd - u), whose last few m/s faded out over ~1.7 s each
			float eU = gsCmd - u;
			// Vortex 0.5.0: with the vanes the fore-aft force no longer waits for the pitch loop: whatever the attitude still
			// brakes or pushes as the speed arrives, the vanes take out at once (they turn the fan's thrust line the other way
			// while the nose comes level), so the profile plans its end on StovlVaneStopLag (0.25 s) instead of the pitch loop's
			// 0.5 s. With the 0.5 s kept, the vanes followed the profile exactly where 0.4.11's nozzle lagged it, braked on past
			// it and ran back: the stop came out later (bench, 7 m/s: Buffed 11.9 t 4.37 -> 4.68 s to 0.3 m/s, Stock 12.8 t
			// 4.52 -> 6.22 s, where 0.4.11 ran 0.24 m/s backwards). With 0.25 s: 3.70 s and 5.68 s, 0.01 m/s past zero, the
			// height band within 1.5 cm of 0.4.11's (13.8 t, 30 kt: 7.17 -> 6.53 s, the band unchanged). The Stock 12.8 t
			// still comes to 0.3 m/s 1.2 s later than 0.4.11, which got there by braking past the profile and running back.
			// StovlVaneStopJerk also lets the profile take its deceleration off as fast as the vanes turn the fan's force
			// where the pitch tilt is the slower (off by default: the stop is shorter again, Stock 12.8 t 4.60 s, 13.8 t
			// 5.90 s, but the nose comes level from higher and faster after it and the height sags 2-3 cm more there). Under
			// the CTOL blend, where the vanes go back to their rest, the profile goes back to the pitch loop's lag with them.
			float tauPitch = 0.5f;
			float jU = jLat;
			if (vnOn && !I.wow)
			{
				tauPitch = Mathf.Lerp(Mathf.Min(0.5f, Plugin.StovlVaneStopLag), 0.5f, wC);
				if (Plugin.StovlVaneStopJerk)
				{
					jU = Mathf.Lerp(Mathf.Max(jLat, fanShareLast * 9.81f * Plugin.StovlVaneRate * 0.0174532924f), jLat, wC);
				}
			}
			float aProfU = jU * (Mathf.Sqrt(tauPitch * tauPitch + 2f * Mathf.Abs(eU) / Mathf.Max(0.01f, jU)) - tauPitch);
			float aGsW = Mathf.Sign(eU) * Mathf.Min(Mathf.Abs(eU) / (2f * tauPitch), aProfU);
			// Vortex 0.4.7 (StovlTurnCoordinate): the demands above are the rates of change of the speeds along the nose and to the
			// right. With the nose turning at psiDot the acceleration that takes is (uDot - v psiDot) forward and (vDot + u psiDot)
			// to the right, so the turn's own part is added here: the bank comes in with the yaw and the velocity turns with the
			// nose. 0.1.0-0.4.6 left it to the feedback, which only acts once the skid is there: 22:56 log, 70-74 s, full left
			// pedal at 22 m/s with the stick released: 13 m/s of skid (37 deg of sideslip) in 4.5 s, and from then on the whole
			// bank budget went into holding it (u psiDot = 5 m/s^2), so neither a right nor a left stick moved the bank.
			float psiRate = Vector3.Dot(root.right * (0f - I.rates.y) + root.up * I.rates.z + root.forward * (0f - I.rates.x), up) * 0.0174532924f;
			dbgPsiRate = psiRate * 57.29578f;
			// (the heading rate the yaw law itself commanded last tick, pedal or heading hold, which leads the measured one by
			// the yaw loop's lag; a yaw the law did not command is left to the feedback)
			if (Plugin.StovlTurnCoordinate && !I.wow)
			{
				float psiFF = VhTurnFFCmd ? (psiDotFF * 0.0174532924f) : psiRate;
				// (0.5.6: not in the pedal pirouette, whose velocity stays where it is while the nose turns)
				psiFF *= 1f - wPir;
				aLatW += u * psiFF;
				aGsW -= v * psiFF;
			}
			// (for the pedal's yaw-rate limit: what the fore-aft demand of the moment leaves of the budget sideways, and never
			// less than the sideways half of an equally divided budget, aHv / sqrt(2); 1 s low-pass)
			{
				float aTurnNow = Mathf.Sqrt(Mathf.Max(0.5f * aHv * aHv, aHv * aHv - aGsW * aGsW));
				if (!aTurnInit)
				{
					aTurnInit = true;
					aTurnF = aTurnNow;
				}
				aTurnF += (aTurnNow - aTurnF) * (dt / (1f + dt));
			}
			float aHw = Mathf.Sqrt(aLatW * aLatW + aGsW * aGsW);
			// (0.2.9: not on the wheels, where nothing tilts: the translation budget held a full-lever take-off roll to ~4 m/s^2,
			// the engine throttled back to 0.48-0.61 below 35 kt; bench S1)
			float hScale = (aHw > aHv && !I.wow) ? (aHv / aHw) : 1f;
			// Vortex 0.4.7 (StovlFwdCapFromNozzle): a forward acceleration is made by the nozzle (the pitch tilt only takes what is
			// past the nozzle's travel), and the lift fan's force stays vertical, so the forward force that goes with a given
			// lift is less than a tilted lift vector's: the budget above (a circle: sqrt(aMax^2 - vertical^2) any way) asked for
			// more forward acceleration than there is, the thrust sat at its limit and the governor, which lets a tilt come in
			// only as fast as the thrust in hand can make up the lift, held the bank where it was. 22:56 log, 107.3-109.2 s,
			// 13.8 t: full right stick, 4.9 m/s sideways for 7.3 commanded, the bank target at 0.4 deg for 1.9 s while the jet
			// accelerated 7 -> 16 m/s forward; bench, same jet: thrust asked 1.04-1.06 of full dry, governor at 0 for 4 s.
			// Both demands are now scaled by the one factor that keeps the forward part inside what the nozzle gives with the
			// lift for the vertical demand and the scaled sideways part (FwdCap; found by halving), so they divide what there
			// is. The vertical demand is the force the thrust fit was asked for last tick (0.3 s low-pass: the weight and the
			// height loop's demand less what the wing, the roll posts and the fan's pitch term carry) plus the same thrust-lag
			// reserve the sideways budget keeps (aLag), at 95 % of full dry thrust as every other budget. That reserve is what
			// the governor meters a bank by, so the forward acceleration no longer takes it: 13.8 t Buffed, 3.2 -> 2.25 m/s^2
			// (bench; 0.4.6 ran at ~97 % with the governor shut). Where the lever is an acceleration command (lamT 0) nothing
			// changes.
			dbgFwdCap = 0f;
			if (Plugin.StovlFwdCapFromNozzle && !I.wow && lamT > 0f && aGsW > 0f)
			{
				float altFc = Mathf.Max(0.05f, hw.AltFactor);
				float capNow = Mathf.Max(0.2f, FwdCap(Mathf.Sqrt(aVbud * aVbud + aLatW * hScale * aLatW * hScale), I.mass, altFc));
				dbgFwdCap = capNow;
				if (aGsW * hScale > capNow)
				{
					float lo = 0f;
					float hi = hScale;
					for (int it = 0; it < 12; it++)
					{
						float mid = 0.5f * (lo + hi);
						float capM = Mathf.Max(0.2f, FwdCap(Mathf.Sqrt(aVbud * aVbud + aLatW * mid * aLatW * mid), I.mass, altFc));
						if (aGsW * mid <= capM)
						{
							lo = mid;
						}
						else
						{
							hi = mid;
						}
					}
					hScale = Mathf.Lerp(hScale, lo, lamT);
				}
			}
			float aGs = aGsW * hScale;
			float aYTrc = aLatW * hScale;
			float aAcc;
			if (Mathf.Abs(thrE - tc) <= tw)
			{
				if (!vHold)
				{
					vHold = true;
					vRef = vA;
				}
				decelFrac = 0f;
				aAcc = Mathf.Clamp(0.3f * (vRef - vA), -2f, 2f);
			}
			else
			{
				vHold = false;
				// 0.2.8: the deceleration reaches StovlDecelMax at the aft detent (the zero-ground-speed command: stop as hard as
				// the jet can) and stays there behind it; 0.1.x-0.2.7 scaled it over the whole travel down to 0 %, so the aft
				// detent at 20 % asked for 2.3 of 4 m/s^2, which idle thrust and drag nearly gave by themselves
				// 0.2.9: and the lever's last detent width at the stop asks for all the thrust there is (the full-dry thrust over
				// the mass, more than the jet can get after its lift): 0.2.8 asked StovlAccelMax there, which the jet beats on the
				// runway, so with the lever at the stop the engine was throttled back to 0.48-0.62 to hold 6 m/s^2 (10:24 log,
				// bench S1)
				float tTop = 1f - 2f * tw;
				aAcc = (thrE > tc) ? ((thrE <= tTop) ? ((thrE - (tc + tw)) / Mathf.Max(0.01f, tTop - (tc + tw)) * Plugin.StovlAccelMax) : Mathf.Lerp(Plugin.StovlAccelMax, Mathf.Max(Plugin.StovlAccelMax, aFullDry), (thrE - tTop) / Mathf.Max(0.001f, 1f - tTop))) : (0f - Mathf.Clamp01(((tc - tw) - thrE) / Mathf.Max(0.01f, (tc - tw) - (ta + tw))) * Plugin.StovlDecelMax);
				decelFrac = (thrE > tc) ? 0f : Mathf.Clamp01(((tc - tw) - thrE) / Mathf.Max(0.01f, (tc - tw) - (ta + tw)));
			}
			float aLong = Mathf.Lerp(aAcc, aGs, lamT);
			// speed protection: stay under the game's swivel cut-off
			float vMaxS = hw.MaxSwivelAirspeed - 11f;
			if (vA > vMaxS - 5f)
			{
				aLong = Mathf.Min(aLong, 0.5f * (vMaxS - vA));
			}
			// ---- vertical: the stick
			float sp = I.sp;
			float hdotCmd;
			// 0.2.2: the stick-released height hold stops the climb or sink on an exponential profile (rate gain kVHold, from the
			// thrust response time) and holds the height it comes to rest at, and a stop never asks the jet to go back the way it
			// came: whenever the jet is slower than the profile in its direction of travel (the thrust, ~0.5 s behind, overshot the
			// deceleration), the stopping point is moved to where it can stop from there, and once the vertical speed has crossed
			// zero the height there is held. 0.2.1 kept the first stopping point: the jet, slowed early, climbed back toward it (in
			// game, 22:55: "settles, rises a bit, settles"; bench at 12.8 t: 0.48 -> 0.42 m/s hang). The feed-forward is the profile's
			// own rate, d/dt (kVHold (hRef - h)) = -kVHold hdot, so moving the point makes no spike. 0.1.x-0.2.0 put the reference
			// hdot / 0.8 ahead with hdot clamped to +-2 m/s and bounced (-0.7 / +0.25 / -0.2 m/s, ~9 s).
			float hdotFF = 0f;
			if (Mathf.Abs(sp) < 0.05f)
			{
				// the hold takes over from the climb or sink rate there is when the hover regime takes over, not from where the
				// law was engaged
				float v0 = Mathf.Clamp(hdot, -Plugin.StovlSinkRate, Plugin.StovlClimbRate);
				// the stop's deceleration available: 80 % of what the vertical loop can ask for (the spare-lift cap below to stop a
				// sink, the -4 m/s^2 floor to stop a climb)
				float aCapUp = Mathf.Clamp(aUpBody / 0.95f + 0.5f, 0.5f, 4f);
				float aStopV = 0.8f * ((v0 < 0f) ? aCapUp : 4f);
				if (lamV < 0.01f && VhPathAnchor)
				{
					// 0.2.8: in the flight-path regime the released stick's path is anchored in space: the reference height moves
					// along the commanded path (level when it came from the height hold), and the path loop below flies the jet
					// back onto it. 0.1.x-0.2.7 held the path angle only: a height error made there was kept, and the path caught
					// at the handover carried the height loop's correction of that moment as a standing climb or sink (bench, yaw
					// case at 43 kt: -0.12 m/s before any input; bank case: +0.11 m/s after the reversals, 2 m in 17 s).
					if (!hHold)
					{
						hHold = true;
						hRef = I.pos.y;
					}
					holdDir = 0f;
					hRef += Mathf.Max(vA, 5f) * Mathf.Sin(gamRef * 0.0174532924f) * dt;
					// (where the wing flies the jet and the released stick holds the pitch attitude, nothing is anchored)
					hRef = Mathf.Lerp(hRef, I.pos.y, Mathf.Clamp01(Mathf.Max(wCtolBlend, wC)));
					// (and a path the jet cannot hold is not chased: the reference stays within PathAnchorMax of the jet. Without
					// it a full-throttle climb-out the thrust could not follow built up a height error, the path loop put the
					// nozzle further down to get it back and the jet accelerated slower: bench, 115 kt against 132 after 34 s)
					hRef = Mathf.Clamp(hRef, I.pos.y - PathAnchorMax, I.pos.y + PathAnchorMax);
				}
				else if (lamV < 0.01f)
				{
					hHold = false;
					holdDir = 0f;
				}
				else if (!hHold)
				{
					hHold = true;
					// 0.2.8: the stop's linear end as fast as the engine can end the deceleration: the thrust, cut for the stop,
					// comes back at (spare lift + the stop's deceleration) / the engine's lag, and the profile's end asks for
					// k x that deceleration per second; within the rate loop's own gain. (kVHoldL, 0.2.1-0.2.7's, now that the
					// jet follows the profile closely, left a ~1.3 s tail: 4.7 s to under 0.1 m/s from a full-stick climb.)
					holdK = Mathf.Min(Mathf.Min(kVHold, kVRateL), 0.8f * (Mathf.Max(0f, aUpBody) + aStopV) / (aStopV * EngineLag));
					holdK = Mathf.Max(holdK, kVHoldL);
					holdA = aStopV;
					// the stopping point the profile min(k e, sqrt(2 a e)) starts from at this speed
					hRef = I.pos.y + Mathf.Sign(v0) * Mathf.Max(Mathf.Abs(v0) / holdK, v0 * v0 / (2f * holdA));
					holdDir = (Mathf.Abs(v0) > 0.1f) ? Mathf.Sign(v0) : 0f;
				}
				else if (holdDir != 0f)
				{
					float vAlong = v0 * holdDir;
					float eAl = (hRef - I.pos.y) * holdDir;
					float cmdAlong = (eAl > 0f) ? Mathf.Min(holdK * eAl, Mathf.Sqrt(2f * holdA * eAl)) : 0f;
					if (vAlong <= 0f)
					{
						hRef = I.pos.y;
						holdDir = 0f;
					}
					else if (vAlong < cmdAlong - 0.05f)
					{
						hRef = I.pos.y + holdDir * Mathf.Max(vAlong / holdK, vAlong * vAlong / (2f * holdA));
					}
				}
				if (!hHold || lamV < 0.01f)
				{
					hdotCmd = Mathf.Max(vA, 5f) * Mathf.Sin(gamRef * 0.0174532924f);
					hdotFF = 0f;
				}
				else if (holdDir != 0f)
				{
					float eAl = (hRef - I.pos.y) * holdDir;
					float lin = holdK * Mathf.Max(0f, eAl);
					float sq = Mathf.Sqrt(2f * holdA * Mathf.Max(0f, eAl));
					// never back the way it came (a stop that ran past its point holds instead of reversing)
					hdotCmd = holdDir * Mathf.Min(lin, sq);
					// the profile's own rate: -k hdot on the linear part, -a hdot / |cmd| on the constant-deceleration part
					hdotFF = (lin <= sq) ? ((0f - holdK) * hdot) : ((0f - holdA) * hdot / Mathf.Max(0.1f, Mathf.Abs(hdotCmd)));
				}
				else
				{
					hdotCmd = kVHold * (hRef - I.pos.y);
					hdotFF = 0f;
				}
				hErrLast = hHold ? (hRef - I.pos.y) : 0f;
				hdotCmd = Mathf.Clamp(hdotCmd, -Plugin.StovlSinkRate, Plugin.StovlClimbRate);
			}
			else
			{
				hHold = false;
				hdotCmd = (sp > 0f) ? (sp * Plugin.StovlClimbRate) : (sp * Plugin.StovlSinkRate);
			}
			float hdotWant = hdotCmd;
			hdotCmd = Mathf.Clamp(hdotCmd, 0f - sinkLim, climbLim);
			if (I.wow && sp <= 0.1f && !touchGrace)
			{
				hdotCmd = Mathf.Min(hdotCmd, 0f);
				hRef = I.pos.y;
				holdDir = 0f;
				hdotFF = 0f;
			}
			dbgHdotCmd = hdotCmd;
			{
				// against what the stick / height hold asks (before the climb limit), so a sink while the jet accelerates
				// pulls the acceleration back
				float shortfall = (I.wow ? 0f : (Mathf.Max(hdotCmd, Mathf.Min(hdotWant, 0f)) - hdot)) - 0.25f;
				float tgt = Mathf.Clamp(1f - shortfall / 0.5f, 0.15f, 1f);
				vPrio = Mathf.MoveTowards(vPrio, tgt, ((tgt < vPrio) ? 4f : 0.5f) * dt);
			}
			// on the braking envelope, its deceleration along the descent: dv/dt = |hdot| / (tau + v / a)
			float aEnv = 0f;
			if (hdot < 0f && hdotCmd <= 0f - sinkLim + 0.01f && vStopNow < Plugin.StovlSinkRate && vStopNow > vTdNow)
			{
				aEnv = (0f - hdot) / (StopLag + vStopNow / aStop);
			}
			// the demand may reach full thrust (aUpAvail keeps 5 % back) and a slow integral takes out the force model's bias
			// (the roll posts' and the yaw nozzle's losses), frozen at the limits and on the wheels
			float aVhHi = Mathf.Clamp(aUpBody / 0.95f + 0.5f, 0.5f, 4f);
			// (0.2.1: rate gain from the thrust response time, kVRate)
			// (0.2.8: a commanded rate and its stop on the large-swing gain; released and holding, the fast one. With the
			// fast one on the stop, a release from a 5.5 m/s climb at 11.9 t cut the thrust to 1860 rpm and could not get it
			// back in time: -0.63 then +0.63 m/s around the stopping height, bench)
			bool holdingNow = Mathf.Abs(sp) < 0.05f && hHold && holdDir == 0f;
			vGainF = Mathf.MoveTowards(vGainF, holdingNow ? 1f : 0f, dt / 0.5f);
			float aVhRaw = Mathf.Lerp(kVRateL, kVRate, vGainF) * (hdotCmd - hdot) + aEnv + hInt + hdotFF;
			float aVh = Mathf.Clamp(aVhRaw, -4f, aVhHi);
			aVhLast = aVh;
			if (I.wow || lamV < 0.01f)
			{
				hInt = Mathf.MoveTowards(hInt, 0f, 2f * dt);
			}
			else if (aVhRaw > -4f && aVhRaw < aVhHi && Mathf.Abs(hdotCmd - hdot) < 1f && Mathf.Abs(hdotCmd) < 0.3f)
			{
				// only near the commanded rate: it trims the model bias, it does not chase a stick input (wound up at a
				// push-over it held the descent 0.7 m/s past the touchdown limit); 0.2.1: and only near a steady rate (|command|
				// under 0.3 m/s), so a released-stick stop does not wind it up (bench: a 0.4 m/s hang at the end of the stop)
				hInt = Mathf.Clamp(hInt + kVInt * (hdotCmd - hdot) * dt, -1f, 1f);
			}
			// flight-path mode
			float gamDot = sp * Plugin.StovlPathRate;
			if (Mathf.Abs(sp) < 0.05f)
			{
				gamDot = 0f;
			}
			gamRef = Mathf.Clamp(gamRef + gamDot * dt, -60f, 60f);
			// 0.2.9: the reference stays within the path loop's own error window (+-5 deg, as aN below) of the path the jet
			// flies, so a command the thrust cannot follow does not wind up: 10:24 log, full aft for 8.7 s ran it to the 60 deg
			// clamp with the jet at 46, then a 3.4 s push left it at -35 and the released stick held that dive at idle thrust.
			// Under the CTOL blend it follows the jet's path (the CTOL laws fly it there).
			if (vA > 5f)
			{
				gamRef = Mathf.Clamp(gamRef, gam - 5f, gam + 5f);
				gamRef = Mathf.Lerp(gamRef, gam, wC);
			}
			if (lamV > 0.01f)
			{
				// with any hover weight the path reference is the height loop's climb rate (level with the stick centred), so
				// the flight-path hold takes over the height hold's intent, not a path caught at a few knots
				// (0.2.8: the height hold's intent, level, not its correction of the moment; the correction goes on below)
				bool holding = VhPathAnchor && hHold && holdDir == 0f && Mathf.Abs(sp) < 0.05f;
				gamRef = holding ? 0f : (Mathf.Asin(Mathf.Clamp(hdotCmd / Mathf.Max(vA, 5f), -1f, 1f)) * 57.29578f);
			}
			// 0.2.8: the path the loop flies = the commanded path + the way back onto the anchored one (the height hold's own
			// gain on the height error, as a path angle)
			float gamFly = gamRef;
			if (VhPathAnchor && hHold && holdDir == 0f && Mathf.Abs(sp) < 0.05f)
			{
				gamFly += Mathf.Asin(Mathf.Clamp(kVHold * (hRef - I.pos.y) / Mathf.Max(vA, 5f), -1f, 1f)) * 57.29578f * (1f - Mathf.Clamp01(wCtolBlend));
			}
			float aN = Mathf.Max(vA, 10f) * (gamDot + 1.5f * Mathf.Clamp(gamFly - gam, -5f, 5f)) * 0.0174532924f;
			dbgGamRef = gamRef;
			dbgGam = gam;
			Vector3 tHat = (vA > 1f) ? (Vector3.Normalize(new Vector3(vAir.x, 0f, vAir.z)) * Mathf.Cos(gam * 0.0174532924f) + up * Mathf.Sin(gam * 0.0174532924f)) : hdg;
			Vector3 nHat = Vector3.Cross(tHat, Vector3.Cross(up, tHat)).normalized;
			if (nHat.sqrMagnitude < 0.5f)
			{
				nHat = up;
			}
			if (!I.wow && lamV > 0.9f && Mathf.Abs(aLong) < 0.3f && gs < 3f)
			{
				sHov = Mathf.Lerp(sHov, Mathf.Clamp(hw.Swivel, 0.6f, 1f), dt / 2f);
				if (vnOn)
				{
					vHov = Mathf.Lerp(vHov, Mathf.Clamp(hw.VaneNow, vnLo, vnHi), dt / 2f);
				}
			}
			Vector3 aHover = hdg * aLong + up * aVh + rgt * 0f;
			Vector3 aPath = tHat * aLong + nHat * aN;
			Vector3 aDes = Vector3.Lerp(aPath, aHover, lamV);
			if (I.wow && !liftIntent)
			{
				aDes = Vector3.zero;
			}
			dbgADesH = Vector3.Dot(aDes, hdg);
			dbgADesV = aDes.y;
			// ---- propulsive force asked (incremental on the measured acceleration)
			Vector3 fNow = PropulsiveForceNow();
			Vector3 fNf = ControlledForceNow();
			if (!fPropInit)
			{
				fPropInit = true;
				fPropF = fNow;
				fNfF = fNf;
			}
			fPropF += (fNow - fPropF) * (dt / (0.05f + dt));
			fNfF += (fNf - fNfF) * (dt / (0.05f + dt));
			// 0.2.8: the increment is taken on the force the fit commands (nozzle + fan balance), not on the whole propulsive
			// force. The roll posts push the wing down (5-6 kN, 0.45 m/s^2 at 11.9 t, whenever the roll accelerates) and the fan's
			// pitch term comes and goes with the pitch loop; with them inside "the force now" the request came out as if the
			// fit could make them, so the thrust never answered a post firing and only the height loop did, after the sink
			// (bench, bank case: -0.49 m/s^2 for 0.8 s at the roll-in with the thrust unchanged). They are known the tick they
			// are commanded, so their change since the measured acceleration's low-pass is taken out at once (fK - its low-pass)
			// instead of 50 ms later through the measurement.
			Vector3 fReq;
			if (VhIndiNozzleFan)
			{
				Vector3 fK = fNow - fNf;
				Vector3 fKF = fPropF - fNfF;
				fReq = fNfF + I.mass * (aDes - I.accel) - (VhKnownFF ? (fK - fKF) : Vector3.zero);
			}
			else
			{
				fReq = fPropF + I.mass * (aDes - I.accel);
			}
			if (I.wow && I.sp <= 0.1f)
			{
				// rolling on the wheels (taxi / short take-off): the incremental vertical request means nothing against the
				// gear, so the jet is asked to carry a share of the weight the wing does not, 30 % at a standstill to 90 % at
				// 35 m/s (the game's own STO speed); the wheels keep the rest and the stick lifts it off
				float lw = 0.5f * I.rho * vA * vA * sCLa * Mathf.Max(I.alpha, 0f) * 0.0174532924f;
				fReq.y = Mathf.Lerp(0.3f, 0.9f, Smooth(0f, 35f, vA)) * Mathf.Max(0f, I.mass * 9.81f - lw);
			}
			// 0.2.5: the request and the force directions are compared in the heading frame (y = world up, z = level heading),
			// not the body frame. The side force is the attitude's to make (the bank / translation bank tilts the thrust on
			// purpose), so the fit leaves it free and matches the world-vertical and fore-aft parts. In body axes a bank turned
			// part of the weight into body-sideways force the fit ignored, so it asked for T = mg cos(bank) where mg / cos(bank)
			// was needed, and the incremental loop only made it up with a standing sink error, g tan^2(bank) / rate gain:
			// 00:24 log, 31 deg of bank at 33 kt, -3.4 m/s with the throttle at 0.56-0.66 (not saturated); 35 deg, -9 m/s.
			Vector3 fReqB = new Vector3(0f, Vector3.Dot(fReq, up), Vector3.Dot(fReq, hdg));
			// (0.4.7: for next tick's forward capability; not on the wheels, where the request is a share of the weight)
			if (!I.wow)
			{
				if (!fReqYInit)
				{
					fReqYInit = true;
					fReqYF = Mathf.Max(0f, fReqB.y);
				}
				fReqYF += (Mathf.Max(0f, fReqB.y) - fReqYF) * (dt / (0.3f + dt));
			}
			else
			{
				fReqYInit = false;
			}
			// Vortex 0.4.0 (group D, StovlFitWeights): the fit's fore-aft weight from what each axis can tolerate (1 / tolerance^2,
			// the vertical weight kept at 1). A vertical miss has only the spare lift to be flown back with; a fore-aft miss is made
			// up by the pitch tilt, which has the hover's own translation capability (aHlevel). 0.1.0-0.3.15: 0.25 (4 : 1)
			// everywhere; this gives 15 : 1 on the clean stock jet with full fuel in the hover, 7 : 1 Buffed, 30 : 1 at 12.8 t, and
			// never less than 4 : 1 (a light jet). Where the pitch tilt is not used (the lever's accelerating regime, lamT) and on
			// the wheels (the gear takes the vertical miss) it stays 0.25. Low-passed 0.3 s.
			// (Two earlier forms on the bench, both wrong the same way, the fore-aft weight rising to 1 : 1 just when the vertical
			// axis was short: on the tilt still left, g tan(budget tilt - thetaAssist), the Buffed jet's deceleration ended with the
			// thrust swinging 0.35 <-> 0.88 and 0.2 m lost; on the budget left after the vertical demand, aHv, the same at 12.8 t.
			// aHlevel moves with neither.)
			{
				float wzT = 0.25f;
				if (Plugin.StovlFitWeights && !I.wow)
				{
					float tolV = Mathf.Max(0.05f, aUpBody);
					float tolH = Mathf.Max(tolV, aHlevel);
					wzT = Mathf.Lerp(0.25f, Mathf.Clamp(tolV * tolV / (tolH * tolH), VhFitWzMin, 0.25f), lamT);
				}
				fitWz += (wzT - fitWz) * (dt / (0.3f + dt));
				dbgWz = fitWz;
			}
			// ---- nozzle angle and thrust: directions the system can point its force, swivel 0..1
			// (0.4.7: the direction table is filled before the translation budget, which reads it; see there)
			// weighted least squares over the swivel range: the thrust T(s) along g(s) (nozzle + the fan's balancing share) that
			// best gives the asked force, the body-vertical part weighted 4x the longitudinal one (vertical priority); T within
			// idle..max (afterburner only with the nozzle mostly aft). A light pull toward the swivel now keeps the nozzle still
			// when the request does not care (e.g. idle thrust in a decelerating transition).
			float altF = Mathf.Max(0.05f, hw.AltFactor);
			float idleRatio = Mathf.Clamp01(0.15f * hw.MinRpm / Mathf.Max(1f, hw.MaxRpm - hw.MinRpm));
			float mg = I.mass * 9.81f;
			float sBest = sNow;
			float best = float.MaxValue;
			float vBest = 0f;
			if (!vnOn)
			{
			for (int k = 0; k < nG; k++)
			{
				float c = WlsCost(gDir[k], fReqB, gS[k], idleRatio, altF, out var _) + 0.02f * mg * mg * (gS[k] - sNow) * (gS[k] - sNow);
				if (c < best)
				{
					best = c;
					sBest = gS[k];
				}
			}
			// refine on the neighbours (parabola through the three costs)
			int kb = Mathf.Clamp(Mathf.RoundToInt(sBest * (nG - 1)), 0, nG - 1);
			if (kb > 0 && kb < nG - 1)
			{
				float c0 = WlsCost(gDir[kb - 1], fReqB, gS[kb - 1], idleRatio, altF, out var _) + 0.02f * mg * mg * (gS[kb - 1] - sNow) * (gS[kb - 1] - sNow);
				float c1 = best;
				float c2 = WlsCost(gDir[kb + 1], fReqB, gS[kb + 1], idleRatio, altF, out var _) + 0.02f * mg * mg * (gS[kb + 1] - sNow) * (gS[kb + 1] - sNow);
				float den = c0 - 2f * c1 + c2;
				if (den > 1E-06f)
				{
					sBest = gS[kb] + Mathf.Clamp(0.5f * (c0 - c2) / den, -0.5f, 0.5f) / (float)(nG - 1);
				}
			}
			}
			else
			{
				// Vortex 0.5.0: the fit over swivel x vane angle together: two effectors for one direction, so a line of pairs gives
				// the asked force. Each pair's cost is the swivel-only fit's (the weighted force error and the light pull toward the
				// swivel now) plus the same pull toward the vane angle now (per degree of thrust line: the swivel's 0..1 is ~90 deg)
				// and the share of the available thrust the pair needs (VhVaneThrustW), which is what chooses along that line: the
				// pair that leaves the most thrust in hand. The lowest table entry, then one Newton step on the nine costs around
				// it (the line of equal force runs diagonally through the table, so the two are refined together).
				float vAct = Mathf.Clamp(hw.VaneNow, vnLo, vnHi);
				float tFull = Mathf.Max(1f, hw.MaxThrust * altF);
				int kb = 0;
				int jb = 0;
				for (int k = 0; k < nG; k++)
				{
					float ds = gS[k] - sNow;
					for (int j = 0; j < NV; j++)
					{
						float rj = WlsCostD(g2Dir[k * NV + j], fReqB, gS[k], idleRatio, altF, vDrive[j], out float tj);
						float tMxj = Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * gS[k] * vDrive[j]) * altF);
						float share = tj / tMxj;
						float dvn = (vG[j] - vAct) / 90f;
						float cj = rj + 0.02f * mg * mg * ds * ds + 0.02f * mg * mg * dvn * dvn + VhVaneThrustW * tFull * tFull * share * share;
						g2J[k * NV + j] = cj;
						if (cj < best)
						{
							best = cj;
							kb = k;
							jb = j;
						}
					}
				}
				float os = 0f;
				float ov = 0f;
				bool inS = kb > 0 && kb < nG - 1;
				bool inV = jb > 0 && jb < NV - 1;
				float c00 = g2J[kb * NV + jb];
				float gs1 = 0f;
				float hss = 0f;
				float gv1 = 0f;
				float hvv = 0f;
				if (inS)
				{
					gs1 = 0.5f * (g2J[(kb + 1) * NV + jb] - g2J[(kb - 1) * NV + jb]);
					hss = g2J[(kb + 1) * NV + jb] - 2f * c00 + g2J[(kb - 1) * NV + jb];
				}
				if (inV)
				{
					gv1 = 0.5f * (g2J[kb * NV + jb + 1] - g2J[kb * NV + jb - 1]);
					hvv = g2J[kb * NV + jb + 1] - 2f * c00 + g2J[kb * NV + jb - 1];
				}
				if (inS && inV)
				{
					float hsv = 0.25f * (g2J[(kb + 1) * NV + jb + 1] - g2J[(kb + 1) * NV + jb - 1] - g2J[(kb - 1) * NV + jb + 1] + g2J[(kb - 1) * NV + jb - 1]);
					float det = hss * hvv - hsv * hsv;
					if (hss > 1E-06f && det > 1E-06f * hss * hss)
					{
						os = (0f - (hvv * gs1 - hsv * gv1)) / det;
						ov = (0f - (hss * gv1 - hsv * gs1)) / det;
					}
					else if (hss > 1E-06f)
					{
						os = (0f - gs1) / hss;
					}
				}
				else if (inS && hss > 1E-06f)
				{
					os = (0f - gs1) / hss;
				}
				else if (inV && hvv > 1E-06f)
				{
					ov = (0f - gv1) / hvv;
				}
				sBest = gS[kb] + Mathf.Clamp(os, -0.5f, 0.5f) / (float)(nG - 1);
				vBest = vG[jb] + Mathf.Clamp(ov, -0.5f, 0.5f) * (vnHi - vnLo) / (float)(NV - 1);
				// then the same step once more on nine pairs close around that point, worked out from the geometry itself (a fifth
				// of a table step each way): over the table's 5 deg steps the force error's own curvature hides the thrust share's
				// slope along the line of equal force (a few parts in a thousand per step in the hover), and the pair then stays
				// wherever it happens to be; close up it shows. A minimum past a limit is taken on the limit, with the other of
				// the two re-fitted there; no further than one table step from the first point.
				{
					float hS = 0.2f / (float)(nG - 1);
					float hV = 0.2f * (vnHi - vnLo) / (float)(NV - 1);
					float sC = Mathf.Clamp(sBest, hS, 1f - hS);
					float vC = Mathf.Clamp(vBest, vnLo + hV, vnHi - hV);
					for (int a = 0; a < 3; a++)
					{
						float sa = sC + (float)(a - 1) * hS;
						NozzleAt(sa, 0f, out var dA, out var pA);
						float dsa = sa - sNow;
						for (int b = 0; b < 3; b++)
						{
							float vb = vC + (float)(b - 1) * hV;
							Vector3 fd = FanDirAt(vb);
							float fkb = FanForceAt(1f, dA, pA, 0f, fd);
							Vector3 gWb = dA + fd * fkb;
							float drv = VaneDrive(vb);
							float rb = WlsCostD(new Vector3(0f, Vector3.Dot(gWb, up), Vector3.Dot(gWb, hdg)), fReqB, sa, idleRatio, altF, drv, out float tb);
							float shb = tb / Mathf.Max(1f, (hw.MaxThrust - hw.ParasiticLoss * sa * drv) * altF);
							float dvb = (vb - vAct) / 90f;
							fine9[a * 3 + b] = rb + 0.02f * mg * mg * dsa * dsa + 0.02f * mg * mg * dvb * dvb + VhVaneThrustW * tFull * tFull * shb * shb;
						}
					}
					float f00 = fine9[4];
					float fgs = 0.5f * (fine9[7] - fine9[1]);
					float fgv = 0.5f * (fine9[5] - fine9[3]);
					float fhss = fine9[7] - 2f * f00 + fine9[1];
					float fhvv = fine9[5] - 2f * f00 + fine9[3];
					float fhsv = 0.25f * (fine9[8] - fine9[6] - fine9[2] + fine9[0]);
					float fdet = fhss * fhvv - fhsv * fhsv;
					if (fhss > 0f && fhvv > 0f && fdet > 1E-06f * fhss * fhvv)
					{
						// (in stencil steps)
						float xs = (0f - (fhvv * fgs - fhsv * fgv)) / fdet;
						float xv = (0f - (fhss * fgv - fhsv * fgs)) / fdet;
						float sLoX = (Mathf.Max(0.001f, sC - 1f / (float)(nG - 1)) - sC) / hS;
						float sHiX = (Mathf.Min(1f, sC + 1f / (float)(nG - 1)) - sC) / hS;
						float vLoX = (Mathf.Max(vnLo, vC - (vnHi - vnLo) / (float)(NV - 1)) - vC) / hV;
						float vHiX = (Mathf.Min(vnHi, vC + (vnHi - vnLo) / (float)(NV - 1)) - vC) / hV;
						float xvC = Mathf.Clamp(xv, vLoX, vHiX);
						if (xvC != xv)
						{
							xs = (0f - (fgs + fhsv * xvC)) / fhss;
						}
						float xsC = Mathf.Clamp(xs, sLoX, sHiX);
						if (xsC != xs)
						{
							xvC = Mathf.Clamp((0f - (fgv + fhsv * xsC)) / fhvv, vLoX, vHiX);
						}
						sBest = sC + xsC * hS;
						vBest = vC + xvC * hV;
					}
				}
			}
			if (I.wow && !liftIntent)
			{
				sBest = Mathf.Max(sNow, Plugin.StovlGroundSwivel);
				// (the vanes at their rest on the wheels)
				vBest = Mathf.Clamp(vnStock, vnLo, vnHi);
			}
			// ---- 0.2.9 CTOL blend: the lift system as the wing's helper. The CTOL pitch law flies the nose (qCtol); the force
			// normal to the flight path that keeps the path turning with the nose is m (V q + g cos(gamma) ...) along the lift
			// direction (normal to the airspeed in the plane of symmetry). The wing gives its share at the reference AoA
			// (the STOVL laws' own, PitchHoldAoA0 within StovlWingAoAMax); the propulsion is asked for the rest. The nozzle is
			// set so the thrust there is now gives that along the lift direction, the rest of it goes along the path; the
			// thrust is the lever's (full lever = full dry), never less than what the nozzle needs fully down to carry the
			// share. An AoA away from the reference lifts or sinks the path by the wing's difference, which brings the AoA back
			// over m V / (q S CLa) (7 s at 55 kt, 2 s at 150), so the AoA ends at the reference and the path follows the nose;
			// once the wing alone carries the load at the reference AoA the propulsion is asked for nothing (nozzle aft) and
			// the wing flies the jet as in CTOL mode.
			float sCtol = sNow;
			float thrCtolMin = 0f;
			if (wC > 0f)
			{
				Vector3 vHat = (vA > 5f) ? (vAir / vA) : root.forward;
				Vector3 lHat = Vector3.Cross(vHat, rgtB);
				lHat = (lHat.sqrMagnitude > 0.01f) ? lHat.normalized : root.up;
				float aRefW = Mathf.Min(Plugin.StovlWingAoAMax, PitchHoldAoA0);
				float qS = 0.5f * I.rho * vSym * vSym * sCLa;
				float lRef = qS * aRefW * 0.0174532924f;
				// the share of the weight the propulsion carries with the wing at the reference AoA (1 slow, 0 once the wing
				// carries the jet there, ~150 kt at 12 t)
				float sPr = Mathf.Clamp01(1f - lRef / Mathf.Max(1f, I.mass * 9.81f));
				// released stick: the path it was released on is held (the path loop's own gain and window, as aN); held stick:
				// the path turns with the nose, at the CTOL law's pitch-rate command
				bool relC = Mathf.Abs(I.sp) < 0.05f;
				if (!relC || I.wow || !ctolPathInit)
				{
					gamHoldC = gam;
					ctolPathInit = true;
				}
				float gDotC = I.qCtol * 0.0174532924f + ((relC && !I.wow) ? (1.5f * Mathf.Clamp(gamHoldC - gam, -5f, 5f) * 0.0174532924f) : 0f);
				float n1C = I.mass * 9.81f * Vector3.Dot(Vector3.up, lHat);
				float nReqC = n1C + I.mass * Mathf.Max(vA, 0f) * gDotC;
				float fP;
				if (I.wow)
				{
					// rolling: 90 % of the load, as the STOVL laws' own take-off roll at speed; the rotation (q) lifts it off
					fP = Mathf.Max(0f, 0.9f * nReqC - qS * Mathf.Max(I.alpha, 0f) * 0.0174532924f);
					ctolQAoA = 0f;
				}
				else
				{
					// where the propulsion carries most of the load the path is flown exactly (the propulsion makes up whatever the
					// wing makes now, measured) and the nose brings the AoA to the reference (ctolQAoA, at the STOVL laws' own AoA
					// reference rates, 1.5 deg/s up, 3 down); as the wing takes over, the propulsion is asked only for the 1 g the wing
					// does not give at the reference AoA, and a manoeuvre is the wing's (the AoA rises above the reference by what
					// the pull needs, as on a wing-borne jet). With the weight sPr: path rate = sPr q + (1 - sPr) (L - L_ref) / m V,
					// so the AoA settles at the reference + m V q / (q S CLa)
					Vector3 fAero = I.mass * (I.accel - Physics.gravity) - fPropF;
					float lAct = Vector3.Dot(fAero, lHat);
					fP = Mathf.Max(0f, sPr * (nReqC - lAct) + (1f - sPr) * (n1C - lRef));
					ctolQAoA = sPr * Mathf.Clamp(2f * (aRefW - I.alpha), -3f, 1.5f);
				}
				dbgCtolQAoA = ctolQAoA;
				dbgCtolFp = fP;
				float tNowC = Mathf.Max(1f, hw.DryThrust);
				float cWant = fP / tNowC;
				// the lift-direction component per unit thrust rises from the nozzle aft (s = 0) to down (s = 1): the first swivel
				// that reaches the share
				float cTop = Vector3.Dot(gWorld[nG - 1], lHat);
				sCtol = 1f;
				float cPrev = Vector3.Dot(gWorld[0], lHat);
				if (cWant <= cPrev)
				{
					sCtol = 0f;
				}
				else
				{
					for (int k = 1; k < nG; k++)
					{
						float cK = Vector3.Dot(gWorld[k], lHat);
						if (cK >= cWant)
						{
							float fr = (cK > cPrev) ? ((cWant - cPrev) / (cK - cPrev)) : 1f;
							sCtol = gS[k - 1] + Mathf.Clamp01(fr) * (gS[k] - gS[k - 1]);
							break;
						}
						cPrev = cK;
					}
				}
				// the thrust floor: what the nozzle fully down needs for the share (95 %, as the hover's capability)
				float tFloor = fP / Mathf.Max(0.05f, 0.95f * cTop);
				thrCtolMin = ThrottleForThrust(tFloor, sNow);
				dbgCtolS = sCtol;
				sBest = Mathf.Lerp(sBest, Mathf.Clamp(sCtol, 0.001f, 1f), wC);
				// (Vortex 0.5.0: and the vanes to their rest, the game's fixed line the share's table was read at, so the blend
				// flies as in 0.4.11)
				vBest = Mathf.Lerp(vBest, Mathf.Clamp(vnStock, vnLo, vnHi), wC);
			}
			// 0.2.8: the nozzle target moves no faster than the nozzle can (SwivelRate), from where it was: the fit's best
			// swivel jumped about as the request moved (bench, yaw skid: 0.56 -> 0.68 -> 0.77 -> 0.60 -> 0.95 within 2.5 s) and
			// the nozzle, always chasing, reversed a dozen times; each reversal is a lift and fore-aft disturbance of its own
			sBest = Mathf.Clamp(sBest, 0.001f, 1f);
			swivelTarget = (VhSwivelSlew && !I.wow) ? Mathf.MoveTowards(Mathf.Clamp(swivelTarget, sNow - 0.02f, sNow + 0.02f), sBest, hw.SwivelRate * dt) : sBest;
			dbgSwivelTgt = swivelTarget;
			// the longitudinal force left over at the best swivel (the nozzle tops out ~86 deg, so a hover deceleration has
			// under 1 m/s^2 from it): integrated into a pitch attitude offset in the hover regime (nose up to stop, nose down to
			// go), which the incremental force request then sees as achieved
			{
				int kr = Mathf.Clamp(Mathf.RoundToInt(swivelTarget * (nG - 1)), 0, nG - 1);
				// (Vortex 0.5.0: with the vane box, at the vane angle commanded too)
				Vector3 gRes = vnOn ? GAt(gS[kr], Mathf.Clamp(vaneOut, vnLo, vnHi), up, hdg) : gDir[kr];
				WlsCostD(gRes, fReqB, gS[kr], idleRatio, altF, vnOn ? VaneDrive(vaneOut) : 1f, out float tR);
				float resZ = fReqB.z - tR * gRes.z;
				// 0.2.0: the airbrake takes the deceleration the propulsion can't give: an integral on that residual (the
				// longitudinal force asked beyond what the nozzle and thrust can do, as an acceleration), while the laws
				// decelerate, with the airspeed for it to do anything (faded out 30 -> 15 m/s), off on the wheels; it closes at
				// 0.5/s otherwise. The pilot's brake key still opens it fully.
				float spdAb = Smooth(15f, 30f, vSym);
				if (aLong < -0.2f && spdAb > 0.01f && !I.wow)
				{
					brakeAuto = Mathf.Clamp01(brakeAuto + 0.5f * (-resZ / Mathf.Max(1f, I.mass)) * dt);
				}
				else
				{
					brakeAuto = Mathf.MoveTowards(brakeAuto, 0f, 0.5f * dt);
				}
				// 0.2.8: and directly with the lever: decelerating at speed the airbrake is open by the share of the deceleration
				// the lever asks, fully at the aft detent (zero ground speed) and behind it. The residual alone opened it 4-25 %
				// in the decel after a conversion (01:22 log, 100 -> 60 m/s with the lever in the aft detent).
				if (!I.wow)
				{
					brakeAuto = Mathf.Max(brakeAuto, Mathf.MoveTowards(brakeAuto, decelFrac * (1f - lamT), 2f * dt));
				}
				// (0.2.9: not under the CTOL blend, where the lever is thrust and the airbrake the pilot's)
				brakeAuto = Mathf.Min(brakeAuto, spdAb * (1f - wC));
				if (lamV < 0.01f || I.wow)
				{
					thetaAssist = Mathf.MoveTowards(thetaAssist, 0f, 4f * dt);
				}
				else
				{
					// 0.2.7: the hover's fore-aft tilt worked out directly instead of integrated: the nozzle takes what it can
					// (its force direction in the body runs from delta_min, swivel 1, to delta_max, swivel 0), and the pitch
					// takes the rest: theta = delta_lim - atan(a_long / g) (nose up to stop, nose down past the nozzle's aft
					// limit), within the tilt the translation budget allows (atan(a_h / g)), moved at StovlTrcBankRate (the
					// translation tilt's rate, which the budget's thrust-lag reserve assumes). It follows the fore-aft command
					// both ways, so the nose comes back down as the stop ends.
					// 0.1.1-0.2.6: an integral of the residual force (6 deg/s at most, -6..+10 deg, 3 s leak), and the attitude
					// reference moved at 1.5 deg/s nose-up above 10 m/s: a stop from 30 kt reached its pitch in ~6 s (the "brake
					// that ramps up"), and the leak left the nose up and the jet drifting back ~1 kt after the stop.
					float dMin;
					float dMax;
					if (vnOn)
					{
						// Vortex 0.5.0: with the vane box the range is the nozzle's and the vanes' together
						float a1l = GAngBody(1f, vnLo, I.rot);
						float a1h = GAngBody(1f, vnHi, I.rot);
						float a0l = GAngBody(0f, vnLo, I.rot);
						float a0h = GAngBody(0f, vnHi, I.rot);
						dMin = Mathf.Min(Mathf.Min(a1l, a1h), Mathf.Min(a0l, a0h));
						dMax = Mathf.Max(Mathf.Max(a1l, a1h), Mathf.Max(a0l, a0h));
					}
					else
					{
						NozzleAt(1f, 0f, out var d1, out var p1);
						float f1 = FanForce(1f, d1, p1, 0f);
						Vector3 g1 = Quaternion.Inverse(I.rot) * (d1 + ((fT != null) ? (fT.forward * f1) : Vector3.zero));
						NozzleAt(0f, 0f, out var d0, out var p0);
						float f0 = FanForce(1f, d0, p0, 0f);
						Vector3 g0 = Quaternion.Inverse(I.rot) * (d0 + ((fT != null) ? (fT.forward * f0) : Vector3.zero));
						float a1 = Mathf.Atan2(g1.z, Mathf.Max(0.01f, g1.y)) * 57.29578f;
						float a0 = Mathf.Atan2(g0.z, Mathf.Max(0.01f, g0.y)) * 57.29578f;
						dMin = Mathf.Min(a0, a1);
						dMax = Mathf.Max(a0, a1);
					}
					float want = Mathf.Atan2(aLong, 9.81f) * 57.29578f;
					float tgtA = (want < dMin) ? (dMin - want) : ((want > dMax) ? (dMax - want) : 0f);
					float tiltLim = Mathf.Atan2(aHv, 9.81f) * 57.29578f;
					tgtA = Mathf.Clamp(lamT * tgtA, -tiltLim, tiltLim);
					// (0.2.8: away from level, only as fast as the governor's share of the thrust-rate headroom)
					bool incTh = Mathf.Abs(tgtA) > Mathf.Abs(thetaAssist) && tgtA * thetaAssist >= 0f;
					// (0.4.0: at the pitch axis's tilt rate, group B)
					float rateFwd = TiltRate(aTiltPitch, Plugin.StovlTrcBankRate);
					tiltRateFwdLast = rateFwd;
					dbgTiltRateU = rateFwd;
					float lamG = GovLambda(incTh, rateFwd);
					float thA0 = thetaAssist;
					thetaAssist = Mathf.MoveTowards(thetaAssist, tgtA, rateFwd * lamG * dt);
					// (0.4.7: the lift this tilt gave back this tick, for the governor's budget next tick)
					retThLast = (Mathf.Abs(thetaAssist) < Mathf.Abs(thA0)) ? (Mathf.Tan(Mathf.Min(Mathf.Abs(thA0), 80f) * 0.0174532924f) * (Mathf.Abs(thA0) - Mathf.Abs(thetaAssist)) / Mathf.Max(dt, 0.0001f) * 0.0174532924f) : 0f;
					dbgATiltLeft = 9.81f * Mathf.Tan(Mathf.Max(0f, tiltLim - Mathf.Abs(thetaAssist)) * 0.0174532924f);
					incThLast = incTh;
					decThLast = !incTh && Mathf.Abs(tgtA - thetaAssist) > 0.01f;
				}
			}
			// thrust for the nozzle where it is now, by the same fit
			// (with the nozzle yaw where it is: a yawed nozzle loses vertical force, 0.1.0 fitted the unyawed one)
			NozzleAt(sNow, (yaw != null) ? yaw.AppliedInput : 0f, out var dN, out var pN);
			float fN = FanForce(1f, dN, pN, 0f);
			Vector3 gNowW = dN + ((fT != null) ? (fT.forward * fN) : Vector3.zero);
			Vector3 gNowB = new Vector3(0f, Vector3.Dot(gNowW, up), Vector3.Dot(gNowW, hdg));
			// Vortex 0.5.0: the vane command. It moves toward the fit's angle at the vanes' own rate from where they are, as the
			// nozzle target does at the nozzle's, and the thrust is sized for where both will be (below). The lift the vanes take
			// while they turn the fan's thrust line away from the vertical (the vane angle + the pitch attitude: the fan's share
			// of the lift x tan(line's tilt) x the rate) is counted in the tilt governor's demand, so a bank or a pitch tilt
			// coming in at the same time shares the thrust-rate headroom with it. The vanes themselves are not metered: they do
			// what the nozzle would otherwise do with more lift lost, and held back by the governor the nozzle swung further
			// aft in their place, the thrust went to its limit and the governor shut (bench, 13.8 t, hover to 30 kt: thrust at
			// 97 % for 0.5 s, 0.05 m of height lost and the vanes stuck at 11 deg; unmetered 0.01 m and 88 % for an instant).
			float vaneRate = Plugin.StovlVaneRate;
			if (vnOn)
			{
				float vAct = hw.VaneNow;
				float vFrom = Mathf.Clamp(Mathf.Clamp(vaneOut, vAct - 0.08f * vaneRate, vAct + 0.08f * vaneRate), vnLo, vnHi);
				float tauNow = vFrom + I.theta;
				float tauTgt = vBest + I.theta;
				bool incV = !I.wow && Mathf.Abs(tauTgt) > Mathf.Abs(tauNow) + 0.01f;
				dbgVaneBest = vBest;
				vaneOut = Mathf.Clamp(Mathf.MoveTowards(vFrom, vBest, vaneRate * dt), vnLo, vnHi);
				float liftNow = Vector3.Dot(gNowW, up);
				float fanShare = (fT != null && liftNow > 0.01f) ? Mathf.Clamp01(fN * Vector3.Dot(fT.forward, up) / liftNow) : 0f;
				fanShareLast = fanShare;
				incVaneLast = incV && Mathf.Abs(vBest - vFrom) > 0.01f;
				tVaneLast = fanShare * Mathf.Tan(Mathf.Min(Mathf.Abs(tauNow), 80f) * 0.0174532924f) * vaneRate * 0.0174532924f;
				vaneLive = true;
			}
			else
			{
				vaneLive = false;
				incVaneLast = false;
			}
			// 0.2.8: the thrust is sized for the lift direction one thrust response time ahead: where the body's rotation
			// (measured rates) and the nozzle's slew toward its target will have put it. The thrust answers ~tauV late, so
			// sized for the direction now it arrived that much behind every tilt and nozzle move.
			Vector3 gFitB = gNowB;
			if (VhGeomFF && !I.wow)
			{
				float tl = VhFFLead * tauV;
				Vector3 wW = (root.right * (0f - I.rates.y) + root.up * I.rates.z + root.forward * (0f - I.rates.x)) * 0.0174532924f;
				Vector3 dG = Vector3.Cross(wW, gNowW) * tl;
				float sP = Mathf.MoveTowards(sNow, swivelTarget, hw.SwivelRate * tl);
				float x0 = Mathf.Clamp01(sNow) * (float)(nG - 1);
				float x1 = Mathf.Clamp01(sP) * (float)(nG - 1);
				int i0 = Mathf.Min((int)x0, nG - 2);
				int i1 = Mathf.Min((int)x1, nG - 2);
				Vector3 g0s = Vector3.Lerp(gDir[i0], gDir[i0 + 1], x0 - (float)i0);
				Vector3 g1s = Vector3.Lerp(gDir[i1], gDir[i1 + 1], x1 - (float)i1);
				if (vnOn)
				{
					// (Vortex 0.5.0: and where the vanes will have turned the fan's thrust)
					float vNow0 = hw.VaneNow;
					g0s = GAt(sNow, vNow0, up, hdg);
					g1s = GAt(sP, Mathf.MoveTowards(vNow0, vaneOut, vaneRate * tl), up, hdg);
				}
				gFitB = gNowB + new Vector3(0f, Vector3.Dot(dG, up), Vector3.Dot(dG, hdg)) + (g1s - g0s);
			}
			WlsCostD(gFitB, fReqB, sNow, idleRatio, altF, vnDriveNow, out float tReq);
			if (I.wow && !liftIntent)
			{
				tReq = 0f;
			}
			// Vortex 0.3.11: a vertical take-off from the wheels (lever in or behind the aft detent: zero ground speed) does not
			// push the jet along the runway while the nozzle is still swinging down: the thrust is held to what gives at most
			// VtoMaxRollAccel of forward force along the nozzle where it is now (the wheel brakes hold that), so the jet lifts
			// once the nozzle points down (16:20 log, 17-24 s: 7.9 m/s forward before it left the ground)
			if (I.wow && liftIntent && gThrottle <= Plugin.StovlAftDetent + Plugin.StovlDetentWidth && gFitB.z > 0.02f)
			{
				tReq = Mathf.Min(tReq, I.mass * VtoMaxRollAccel / gFitB.z);
			}
			dbgThrustReq = tReq;
			float thr = ThrottleForThrust(tReq, sNow);
			Vector2 rm = hw.Remap;
			if (wC > 0f)
			{
				// 0.2.9 CTOL blend: the lever is the thrust (idle .. full dry over its travel, no afterburner in STOVL mode),
				// within the floor the lift share needs (above)
				// Vortex 0.3.3 (Tom, 17 Nov: "at that new zero point, i want no thrust"): the lever's zero is the aft detent, where
				// the conversion parks it; from the detent's back edge forward the lever is idle .. full dry (0.2.9-0.3.2: the whole
				// travel was, so the parked lever at 20 % gave ~18 % thrust and the jet would not slow down)
				float thrLever = rm.x + CtolLeverThrust(I.throttle) * (rm.y - rm.x);
				thr = Mathf.Lerp(thr, Mathf.Clamp(Mathf.Max(thrLever, thrCtolMin), 0f, rm.y), wC);
			}
			throttleCmdF = thr;
			throttleOut = throttleCmdF;
			throttleOverride = true;
			axis1Out = Mathf.Clamp(1f - swivelTarget, 0f, 0.999f);
			// ---- pitch attitude (the FCS's, not the pilot's): hover attitude at low airspeed, the wing's share at speed
			// (0.1.6: on the airspeed in the plane of symmetry: in a skid the wing carries nothing)
			// Vortex 0.4.0 (group C, StovlPhysicsBlends): on the wing's share of the weight, not the airspeed: vSym over the speed at
			// which the wing alone carries the jet at StovlWingAoAMax (the square root of the share, so the fade's shape over the
			// speed is 0.3.15's). 15-45 m/s on the reference jet; a heavier jet or thinner air moves it up with sqrt(mass / density).
			float vWingBorne = WingBorneSpeed(ref I);
			float wWing = Plugin.StovlPhysicsBlends ? Smooth(WingShare0, WingShare1, vSym / vWingBorne) : Smooth(15f, 45f, vSym);
			float qDyn = 0.5f * I.rho * vSym * vSym;
			float alphaRef = alphaRefF;
			if (qDyn > 50f && !I.wow)
			{
				// the normal force the path wants, the aero normal force there is now (measured), the AoA that would carry it
				Vector3 fOther = I.mass * (I.accel - Physics.gravity) - fPropF;
				float nReq = I.mass * Vector3.Dot(aDes - Physics.gravity, nHat);
				float lNow = Vector3.Dot(fOther, nHat);
				// the wing's share of it (wWing): the rest is the jet's, through the incremental force request
				float aNeed = I.alpha + (wWing * nReq - lNow) / Mathf.Max(1f, qDyn * sCLa) * 57.29578f;
				dbgNReq = nReq;
				dbgLNow = lNow;
				// 0.1.5: capped at the AoA the attitude hold below takes over at (PitchHoldAoA0, 10 deg), so the nose does not
				// go up to StovlWingAoAMax and come back down as the jet speeds up (0.1.6 bench without it: 13.8 -> 7.3 deg
				// pitch at -2.8 deg/s, 240-320 km/h)
				float aMax = Mathf.Min(Plugin.StovlWingAoAMax, PitchHoldAoA0);
				// 0.1.5: not below the hover attitude's AoA: with the wing's share still small the target fell under it and the
				// nose dipped 3-4 deg before coming up (bench, full throttle from the hover: -3.4 then +3.4 deg/s)
				float aFloor = Mathf.Min(Plugin.StovlHoverAttitude - gam, aMax);
				alphaRef = Mathf.Clamp(aNeed, Mathf.Max(-2f, aFloor), aMax);
			}
			// 0.1.5: and moved at 1.5 deg/s up, 3 down at most (0.1.4: a 0.5 s lag only, ~3 deg/s of pitch-up as the jet sped up)
			if (wWing < 0.01f)
			{
				// hover: the wing's AoA reference starts from the hover attitude's when the wing begins to take part
				alphaRefF = Mathf.Clamp(Plugin.StovlHoverAttitude - gam, -2f, Plugin.StovlWingAoAMax);
			}
			float aLp = alphaRefF + (alphaRef - alphaRefF) * (dt / (0.5f + dt));
			alphaRefF = Mathf.MoveTowards(alphaRefF, aLp, ((aLp > alphaRefF) ? 1.5f : 3f) * dt);
			// (0.2.7: the hover's fore-aft tilt (thetaAssist) is added after the attitude reference's slew below, so it moves at
			// its own rate: 0.1.5-0.2.6 slewed it with the rest at 1.5 deg/s nose-up above 10 m/s, so a stop from 30 kt took
			// ~6 s to reach its pitch, the "brake that ramps up")
			float thetaRef = Mathf.Lerp(Plugin.StovlHoverAttitude, gam + alphaRefF, wWing);
			// Vortex 0.1.4: once the wing can carry the jet (its level-flight AoA from PitchHoldAoA1 14 down to PitchHoldAoA0 10
			// deg; sea level, 12.1 t: about 237 -> 280 km/h) the released stick holds the pitch attitude, as the CTOL laws do,
			// instead of the flight path: holding the path lowered the nose as the jet accelerated (bench: -3 deg/s, 9 -> 5 deg)
			// and the CTOL laws then held the attitude it had reached. The stick still commands the path rate. (0.1.4-0.1.5 used
			// the changeover AoA keys for this; 0.1.6 moved the changeover itself back to the 350 / 300 km/h IAS gate and keeps
			// the blend on the wing, where the nose drop starts: on the IAS gate the nose dropped at -2.8 deg/s before 300.)
			wCtolBlend = (!I.wow && Mathf.Abs(I.sp) < 0.05f) ? (1f - Smooth(PitchHoldAoA0, PitchHoldAoA1, alphaLevel)) : 0f;
			if (wCtolBlend < 0.01f)
			{
				thetaHold = I.theta;
			}
			else
			{
				thetaRef = Mathf.Lerp(thetaRef, thetaHold, wCtolBlend);
				gamRef = Mathf.Lerp(gamRef, gam, wCtolBlend);
			}
			if (I.wow && airT <= 0f)
			{
				thetaRef = I.theta;
			}
			// 0.1.5: once moving (over 10 m/s of airspeed) the attitude reference moves at 1.5 deg/s nose-up and 3 nose-down at
			// most (the acceleration's nose-down offset and the wing's AoA coming in gave -3.4 / +4 deg/s at full throttle); 8 deg/s
			// for the hover attitude
			float thUp = Mathf.Lerp(8f, 1.5f, Smooth(5f, 10f, vA));
			float thDn = Mathf.Lerp(8f, 3f, Smooth(5f, 10f, vA));
			thetaRefF = Mathf.MoveTowards(thetaRefF, thetaRef, ((thetaRef > thetaRefF) ? thUp : thDn) * dt);
			// 0.2.9: under the CTOL blend the attitude references follow the jet (the CTOL pitch law flies the nose), so the
			// STOVL laws take over from where it is when the airspeed falls back through the blend
			if (wC > 0f)
			{
				thetaRefF = Mathf.Lerp(thetaRefF, I.theta, wC);
				thetaHold = Mathf.Lerp(thetaHold, I.theta, wC);
			}
			float thetaTgt = thetaRefF + ((I.wow && airT <= 0f) ? 0f : ((1f - wWing) * (1f - wCtolBlend) * thetaAssist));
			dbgThetaAssist = thetaAssist;
			dbgLamL = qDyn * sCLa * Plugin.StovlWingAoAMax * 0.0174532924f / Mathf.Max(1f, I.mass * 9.81f);
			dbgThetaRef = thetaTgt;
			dbgAlphaRef = alphaRefF;
			float aPitch = Mathf.Clamp(AuthorityShare * I.authority.y, 8f, 120f);
			float aRoll = Mathf.Clamp(AuthorityShare * I.authority.x, 10f, 200f);
			float aYaw = Mathf.Clamp(AuthorityShare * I.authority.z * Mathf.Lerp(1f, yawLiftK, lamV), 4f, 120f);
			dbgAuth = new Vector3(aRoll, aPitch, aYaw);
			RollStopPower(aRoll, VhRollLoad ? I.rollFull : 1000f, VhRollLoad ? I.rollLoad : 0f, out float aRollP, out float aRollN);
			dbgRollStop = new Vector3(aRollP, aRollN, I.rollLoad);
			dbgAuthRaw = new Vector3(I.authority.x, I.rollFull, I.authority.z);
			float thetaDot = Stop(Mathf.DeltaAngle(I.theta, thetaTgt), 2f, aPitch, 15f);
			// ---- lateral: bank / translational rate / roll rate
			// (0.2.6: from the shared translation budget, above)
			float aY = aYTrc;
			float phiTrcRaw = Mathf.Atan2(aY, g) * 57.29578f;
			// 0.1.6: the translation bank moves at StovlTrcBankRate at most, so the thrust (0.35 s spool) keeps up with the lift
			// the bank tilts away: a stop from a 29 kt skid snapped to 11 deg in 0.3 s, the jet sank 0.5 m/s, the height priority
			// cut the bank to 3 and it came in again (bench). Outside the TRC regime it follows at once.
			// 0.2.4: the weight of the translational-rate bank. With the stick held it is the stick's regime (lamTrc: lateral
			// speed command in the hover, bank command from 15-30 kt forward). With the stick released its job is to null the
			// side velocity, and it keeps that job until the sideslip loop takes it over (1 - lamY: yaw into the relative
			// wind from 30-40 kt forward). 0.1.6-0.2.3 used lamTrc for both, so between 15 and 30 kt forward neither did it:
			// 00:14 log, full pedal at 17-19 m/s yawed the nose 50 deg off the flight path; released, the lever held 12 m/s
			// along the nose and the other 15 m/s stayed sideways (bank 3.4 deg, the skid only bled off by drag, 22 -> 17.5 m/s
			// in 13 s).
			float wLat = Mathf.Lerp(lamY, lamTrc, Smooth(0.05f, 0.2f, Mathf.Abs(I.sr)));
			// (0.4.0: at the roll axis's tilt rate, group B)
			float rateSide = TiltRate(aTiltRoll, Plugin.StovlTrcBankRate * kRollS);
			dbgTiltRateA = aTiltRoll * Plugin.StovlTiltStopTime;
			dbgTiltRate = rateSide;
			phiTrcF = (wLat < 0.01f || !lawInitTrc) ? phiTrcRaw : Mathf.MoveTowards(phiTrcF, phiTrcRaw, rateSide * dt);
			lawInitTrc = true;
			float phiTrc = phiTrcF;
			dbgPhiTrc = phiTrcF;
			// 0.2.0: the bank the lift can hold. The turn is coordinated (g tan(bank) / V of turn rate), and the wing's AoA is
			// capped at StovlWingAoAMax in these laws, so a bank past what the wing at that AoA plus the spare propulsive lift
			// carries pulls the nose up through it: 20:46 log, 62 deg held at ~270 km/h, AoA 14 -> 29 in 6 s; bench, 67 deg held
			// from 240 km/h, AoA past 60. n = (85 % of q S CLa alphaMax + the propulsive lift at full dry, which keeps 5 % back) / W, bank <= acos(1 / n),
			// 20..75 deg. Stick bank (StovlBankMax), the roll-rate mode and the bank hold all stay inside it.
			float phiMax;
			{
				float qS = 0.5f * I.rho * vSym * vSym;
				float lWing = qS * sCLa * Plugin.StovlWingAoAMax * 0.0174532924f;
				// 0.2.5: where the thrust holds the height (lamV), the bank spends only half the spare lift, as the translation
				// tilt does (tiltMax), so the height loop keeps the other half for the thrust lag and its own stop; in the
				// flight-path regime all of it, as 0.2.0. The lift along the body's up axis (fLiftBodyMax), not the world-vertical
				// part, which already carries the bank. No 20 deg floor: with no spare lift there is no bank to give (the floor
				// is the translation's 0.2 m/s^2, ~1 deg). 0.2.0-0.2.4: all of the spare lift, floor 20 deg; bench (00:24 log
				// repro, ~17 m/s forward), 31 deg held at full dry thrust, -0.65 m/s with the stick held, -1.75 on the roll-out.
				float w = Mathf.Max(1f, I.mass * 9.81f);
				float spare = Mathf.Max(0f, 0.85f * lWing + fLiftBodyMax - w);
				float nAv = (w + (1f - 0.5f * lamV) * spare) / w;
				float phiMin = Mathf.Atan2(0.2f, 9.81f) * 57.29578f;
				phiMax = Mathf.Clamp(Mathf.Acos(Mathf.Clamp(1f / Mathf.Max(nAv, 1.0001f), 0f, 1f)) * 57.29578f, phiMin, 75f);
			}
			dbgPhiMax = phiMax;
			float phiBank = Mathf.Clamp(I.sr * Plugin.StovlBankMax, -phiMax, phiMax);
			float phiCmd = Mathf.Lerp(phiBank, phiTrc, wLat);
			if (VhGovern && lamRoll < 0.99f && !I.wow)
			{
				// 0.2.8: the bank command moves toward its target at the bank loop's own top rate; past level on the target's
				// side, only as fast as the governor allows (see tiltBudget). 0.1.x-0.2.7 let it jump: a stop from full side
				// stick swung the target 29 -> -31 deg at once, the roll went through at 59 deg/s with the thrust cut on the
				// way, and ran on to -46 with 23 deg of pitch (bench, buffed 11.0 t: -1.45 m/s, 1.6 m lost).
				if (!phiGInit)
				{
					phiGInit = true;
					phiG = I.phi;
				}
				bool incPhi = Mathf.Abs(phiCmd) > Mathf.Abs(phiG) && phiCmd * phiG >= 0f;
				float lamG = GovLambda(incPhi, 60f);
				float lvl = (phiCmd * phiG < 0f) ? 0f : phiCmd;
				// (toward level, or to a smaller bank on the same side: free)
				float phiG0 = phiG;
				phiG = Mathf.MoveTowards(phiG, incPhi ? phiCmd : lvl, 60f * lamG * dt);
				retPhiLast = (Mathf.Abs(phiG) < Mathf.Abs(phiG0)) ? (Mathf.Tan(Mathf.Min(Mathf.Abs(phiG0), 80f) * 0.0174532924f) * (Mathf.Abs(phiG0) - Mathf.Abs(phiG)) / Mathf.Max(dt, 0.0001f) * 0.0174532924f) : 0f;
				phiG = Mathf.Lerp(phiG, I.phi, wC);
				incPhiLast = incPhi;
				decPhiLast = !incPhi && Mathf.Abs(lvl - phiG) > 0.01f;
				phiCmd = Mathf.Lerp(phiG, phiCmd, lamRoll);
			}
			else
			{
				phiGInit = false;
				incPhiLast = false;
				decPhiLast = false;
				retPhiLast = 0f;
			}
			dbgPhiCmd = phiCmd;
			float ePhiB = Mathf.DeltaAngle(I.phi, phiCmd);
			// Vortex 0.4.2 (StovlBankGainFromMargin, off by default): the translation bank loop at 1 / StovlTrcLagMargin instead
			// of 2.5 / s, so the bank trails a moving target by the margin the side-velocity profile is given (3.1 / s for
			// 0.32 s). Bench, Buffed 11.9 t, 15 kt stop: 0.11 m/s past zero and the stop 0.09 s quicker than with the margin at
			// the loop's lag, but the loop then asks for 31 deg/s^2 at every corner of the bank target where the roll budget is
			// 24: posts at their stop 0.72 s (0.22), heading +-1.1 deg (0.1), 4.5 deg on a short side step (1.8).
			// (Tried before both: feeding the loop its target's rate. The target's slope steps at every corner, the posts went
			// to their stops there and the height moved 0.09 m where it had moved 0.02.)
			float kBank = BankLoopGain();
			dbgKBank = kBank;
			float phiDotBank = Stop(ePhiB, kBank * kRollS, (ePhiB > 0f) ? aRollP : aRollN, 60f);
			float phiDotRate;
			if (Mathf.Abs(I.sr) < 0.05f)
			{
				// stick released: stop the roll, then hold the bank where it stopped
				if (!phiHold && Mathf.Abs(I.rates.x) < 5f)
				{
					phiHold = true;
					phiHoldRef = I.phi;
				}
				float ePhiH = Mathf.DeltaAngle(I.phi, phiHoldRef);
				phiDotRate = phiHold ? Stop(ePhiH, 2.5f * kRollS, (ePhiH > 0f) ? aRollP : aRollN, 60f) : 0f;
			}
			else
			{
				phiHold = false;
				phiDotRate = I.sr * Plugin.StovlRollRateMax;
			}
			// below the roll-rate regime the bank / TRC target is what the rate mode takes over (not a bank caught earlier)
			if (lamRoll < 0.5f)
			{
				phiHold = Mathf.Abs(I.sr) < 0.05f;
				phiHoldRef = phiCmd;
			}
			// (0.2.0: the rate mode and its hold inside the bank the lift can hold; past it, back to it)
			phiHoldRef = Mathf.Clamp(phiHoldRef, -phiMax, phiMax);
			// (0.2.9: and under the CTOL blend it follows the jet's bank, as the attitude references above)
			phiHoldRef = Mathf.Lerp(phiHoldRef, I.phi, wC);
			phiDotRate = Mathf.Clamp(phiDotRate, Stop(-phiMax - I.phi, 2.5f * kRollS, (-phiMax - I.phi > 0f) ? aRollP : aRollN, 60f), Stop(phiMax - I.phi, 2.5f * kRollS, (phiMax - I.phi > 0f) ? aRollP : aRollN, 60f));
			float phiDot = Mathf.Lerp(phiDotBank, phiDotRate, lamRoll);
			// 0.2.9 CTOL blend: the CTOL roll-rate command keeps inside the bank the lift can hold (phiMax: the wing at
			// StovlWingAoAMax + the propulsive lift; 20-30 deg slow, 75 at most), stopped on it as the STOVL roll-rate mode is.
			// Bench, 110 km/h, half roll stick for 2 s under the CTOL lateral law alone: 88 deg of bank and 46 m/s of sink.
			ctolPLo = Stop(-phiMax - I.phi, 2.5f * kRollS, (-phiMax - I.phi > 0f) ? aRollP : aRollN, 60f);
			ctolPHi = Stop(phiMax - I.phi, 2.5f * kRollS, (phiMax - I.phi > 0f) ? aRollP : aRollN, 60f);
			if (I.wow && airT <= 0f)
			{
				phiDot = 0f;
			}
			// 0.2.8: the roll-rate command changes no faster than the jet can follow on the side the load has taken the
			// authority from (the net roll acceleration left that way; no limit where the whole budget is there, and the
			// two blend as b a / (b - a), b the budget). Without it a bank target on the far side asked for 14 deg/s at once
			// from a jet with 1 deg/s^2 that way: the posts sat at their stop and the allocator spent the yaw bearing and
			// the throttle on the roll they could not make (bench, posts at 92 %: yaw +-6 deg/s, throttle 0.2..0.88, a
			// 1.3 s cycle; the 02:55 log shows the same yaw swing with the posts at the stop).
			{
				float lDn = (aRollP >= aRoll - 0.001f) ? float.PositiveInfinity : (aRoll * aRollP / (aRoll - aRollP));
				float lUp = (aRollN >= aRoll - 0.001f) ? float.PositiveInfinity : (aRoll * aRollN / (aRoll - aRollN));
				if (!phiDotSInit || (I.wow && airT <= 0f))
				{
					phiDotSInit = true;
					phiDotS = phiDot;
				}
				phiDotS = Mathf.Clamp(phiDot, phiDotS - lDn * dt, phiDotS + lUp * dt);
				phiDot = phiDotS;
			}
			// ---- directional: yaw rate (hover, heading hold) / coordinated with sideslip (forward flight)
			float psiDotH;
			// Vortex 0.3.9 (Tom, 17 Nov: no fixed yaw-rate limit): the pedal's full rate is what the yaw axis can start and stop
			// within StovlYawStopTime at its authority now (aYaw, the ramp below), so it follows mass, fuel, stores and the lift
			// system's thrust; StovlYawStopTime is set so a clean jet with full fuel in the hover gets the old 30 deg/s
			float yawRateMax = YawRateMax(aYaw);
			float yawSlew = aYaw;
			float turnLag = 0f;
			// Vortex 0.4.7 (StovlTurnYawLimit; Tom's rule: the FCS does not let the pilot command what the jet cannot hold): where
			// the side-velocity loop holds the velocity on the nose (lamY), turning the nose at psiDot takes gs x psiDot of
			// sideways acceleration. The pedal's rate is therefore no more than StovlTurnShare (0.707: the sideways half of a
			// budget divided equally between sideways and fore-aft) of the sideways acceleration there is (the hover's stopping
			// acceleration, the number the side-speed command comes from, less what the fore-aft demand of the moment takes)
			// over the ground speed, and it builds and decays no faster than the bank can follow (g x the bank's tilt rate /
			// gs). In the hover and at a few m/s neither binds. 22:56 log: 20.5 deg/s commanded at 22 m/s needs 8 m/s^2 (39 deg
			// of bank) where the jet had about 4.7. With the whole of it taken by the turn (share 1) nothing was left to take
			// out the skid the turn-in leaves: bench, 13.8 t, 45 kt: 1.5 m/s held through the turn, and 9 deg of bank the other
			// way after the pedal was let go; with 0.707: 0.55 m/s and 2.7 deg.
			if (Plugin.StovlTurnYawLimit && !I.wow && !pirOn)
			{
				float gsT = Mathf.Max(gs, 0.5f);
				float rTurn = Mathf.Clamp(Plugin.StovlTurnShare, 0.3f, 1f) * (VhTurnAvail ? Mathf.Min(aTrcStopF, aTurnF) : aTrcStopF) / gsT * 57.29578f;
				float sTurn = 9.81f * rateSide / gsT;
				// (and the yaw loop gets the command the bank loop's lag later than the bank does, by the share of the sideways
				// capability a full-pedal turn takes at this speed: none in the hover. With both given it at once the bank
				// trailed the yaw by that lag on the way in and on the way out: bench, 13.8 t, 30 kt, 1.6 m/s of skid into the
				// turn after the pedal was let go and 10 deg of bank the other way to stop it.)
				turnLag = VhTurnLead ? (lamY * Mathf.Clamp01(yawRateMax / Mathf.Max(0.1f, rTurn)) / Mathf.Max(0.5f, BankLoopGain() * kRollS)) : 0f;
				yawRateMax = Mathf.Lerp(yawRateMax, Mathf.Min(yawRateMax, rTurn), lamY);
				yawSlew = Mathf.Lerp(aYaw, Mathf.Min(aYaw, sTurn), lamY);
				dbgYawLim = yawRateMax;
			}
			float pedalTgt = (Mathf.Abs(I.sy) < 0.05f) ? 0f : (I.sy * yawRateMax);
			// Vortex 0.5.6: in the pedal pirouette the nose turns away from the flight path, and with airspeed the airframe's
			// weathervane (directional stiffness x sideslip) grows until it takes the yaw authority. The pedal's rate is held to
			// what can still be stopped short of the sideslip where the weathervane would take StovlPirouetteSlipShare (0.8) of
			// the yaw authority, with what is left of it then; past that it yaws back. In the hover (no stiffness) no limit.
			dbgPirSlipLim = 0f;
			if (pirOn && I.slipStiff > 0.02f && !I.wow)
			{
				float shr = Mathf.Clamp(Plugin.StovlPirouetteSlipShare, 0.2f, 0.95f);
				float bLim = Mathf.Clamp(shr * aYaw / I.slipStiff, 5f, 180f);
				dbgPirSlipLim = bLim;
				float aLeft = Mathf.Max(0.5f, aYaw * (1f - shr));
				// (right pedal = nose right = the sideslip going negative)
				if (pedalTgt > 0f)
				{
					pedalTgt = Mathf.Min(pedalTgt, Stop(bLim + I.beta, 2.5f, aLeft, yawRateMax));
				}
				else if (pedalTgt < 0f)
				{
					pedalTgt = Mathf.Max(pedalTgt, -Stop(bLim - I.beta, 2.5f, aLeft, yawRateMax));
				}
			}
			psiDotPedal = Mathf.MoveTowards(psiDotPedal, pedalTgt, yawSlew * dt);
			psiPedLag = (turnLag > 0f && !(I.wow && airT <= 0f)) ? (psiPedLag + (psiDotPedal - psiPedLag) * (dt / (turnLag + dt))) : psiDotPedal;
			if (Mathf.Abs(I.sy) < 0.05f)
			{
				// pedals released: the yaw rate is brought to zero, then the heading where it stopped is held
				if (!psiHold && Mathf.Abs(psiPedLag) < 0.5f && Mathf.Abs(I.rates.z) < 3f)
				{
					psiHold = true;
					psiRef = psi;
				}
				psiDotH = psiHold ? Stop(Mathf.DeltaAngle(psi, psiRef), 2.5f, aYaw, 20f) : psiPedLag;
			}
			else
			{
				psiHold = false;
				psiDotH = psiPedLag;
			}
			float psiDotP = (vA > 5f) ? (g * Mathf.Tan(Mathf.Clamp(I.phi, -80f, 80f) * 0.0174532924f) / vA * 57.29578f) : 0f;
			float psiDot = Mathf.Lerp(psiDotP, psiDotH, lamY);
			// 0.2.1: the pedal is the hover's yaw-rate command (StovlHoverYawRate) wherever the yaw axis easily beats the airframe's
			// weathervane: the restoring yaw at 45 deg of sideslip (directional stiffness x 45) under a quarter of the yaw budget,
			// blending to the sideslip command by half of it. 21:46-22:24 logs: stiffness 0.05 deg/s^2 per deg at 15-30 m/s,
			// 0.22 at 30-40, 0.46 at 40-60, against ~25 deg/s^2 of yaw, so the yaw-rate pedal reaches ~60-70 kt. Outside the hover
			// regime it adds to the coordinated turn and the sideslip loop stands aside while the pedal is held.
			float lamB = Mathf.Max(0f, I.slipStiff) * 45f / Mathf.Max(1f, aYaw);
			dbgLamB = lamB;
			float wPed = 1f - Smooth(0.25f, 0.5f, lamB);
			float pedOn = Smooth(0.05f, 0.2f, Mathf.Abs(I.sy));
			dbgWPed = wPed;
			psiDot += (1f - lamY) * wPed * psiDotPedal;
			psiDotFF = (I.wow && airT <= 0f) ? 0f : (lamY * (psiHold ? psiDotH : psiDotPedal) + (1f - lamY) * wPed * psiDotPedal);
			if (lamY < 0.5f)
			{
				psiRef = psi;
				if (Mathf.Abs(I.sy) < 0.05f || wPed < 0.01f)
				{
					psiDotPedal = 0f;
				}
			}
			// Euler rates -> body rates
			float cph = Mathf.Cos(I.phi * 0.0174532924f);
			float sph = Mathf.Sin(I.phi * 0.0174532924f);
			float cth = Mathf.Cos(I.theta * 0.0174532924f);
			float sth = Mathf.Sin(I.theta * 0.0174532924f);
			float p = phiDot - psiDot * sth;
			float q = thetaDot * cph + psiDot * cth * sph;
			float r = (0f - thetaDot) * sph + psiDot * cth * cph;
			// 0.2.0: the bank (roll) part rolls about the air velocity, not the body x axis, wherever the airspeed means
			// something: rolling about the body axis at AoA turns the AoA into sideslip (20:46 log, 263 km/h, 30 deg AoA, full
			// roll stick: -39 deg of sideslip). It fades to the body roll toward the hover (translational-rate regime, under
			// 10-25 m/s), where a bank is a side translation. p_s about the velocity = p_s cos(alpha) on body x + p_s sin(alpha)
			// on body z.
			{
				// (0.4.0, group C: on the airspeed over the wing-borne speed, as wWing; 10-25 m/s on the reference jet)
				float wVv = (1f - wLat) * (Plugin.StovlPhysicsBlends ? Smooth(AeroShare0, AeroShare1, vSym / vWingBorne) : Smooth(10f, 25f, vSym));
				if (wVv > 0f)
				{
					float aR = Mathf.Clamp(I.alpha, -30f, 60f) * 0.0174532924f;
					p += wVv * phiDot * (Mathf.Cos(aR) - 1f);
					r += wVv * phiDot * Mathf.Sin(aR);
				}
			}
			// sideslip loop in forward flight (pedal = sideslip), faded out in the hover
			// (0.2.1: right pedal = nose right = negative sideslip, as the CTOL laws' betaPedalCmd; 0.1.0-0.2.0 had it the other
			// way round: 22:24 log, left pedal at 34-20 m/s settled at -9 deg of sideslip, nose right)
			float betaCmd = (0f - I.sy) * Plugin.StovlPedalSideslip;
			// (only with airspeed enough for the sideslip to mean something)
			r += (1f - lamY) * (Plugin.StovlPhysicsBlends ? Smooth(AeroShare0, AeroShare1, vA / vWingBorne) : Smooth(10f, 25f, vA)) * (1f - wPed * pedOn) * Mathf.Clamp(1.5f * (I.beta - betaCmd), -20f, 20f);
			// command derivatives for the INDI feed-forward (low-passed 0.05 s; clamped to the budgets)
			if (lawInit && rateFFInit)
			{
				pDotOut += (Mathf.Clamp((p - pOut) / Mathf.Max(dt, 1E-04f), -aRollP, aRollN) - pDotOut) * (dt / (0.05f + dt));
				rDotOut += (Mathf.Clamp((r - rOut) / Mathf.Max(dt, 1E-04f), -aYaw, aYaw) - rDotOut) * (dt / (0.05f + dt));
			}
			else
			{
				pDotOut = 0f;
				rDotOut = 0f;
			}
			rateFFInit = true;
			pOut = p;
			qOut = q;
			rOut = r;
		}

		// after the allocation: the commands to the hardware
		public void PostAlloc(float dt, bool lawActive, bool groundMode)
		{
			if (!owns)
			{
				return;
			}
			fanIn = (fan != null && fan.active && !groundMode) ? Mathf.Clamp(fan.cmdOut, -1f, 1f) : 0f;
			postIn = (post != null && post.active && !groundMode) ? Mathf.Clamp(post.cmdOut, -1f, 1f) : 0f;
			if (yaw != null)
			{
				yaw.Advance(dt, groundMode);
				yawIn = yaw.AppliedInput;
			}
			if (!lawActive && state == StovlState.Off)
			{
				fanIn = 0f;
				postIn = 0f;
			}
			if (freestyle)
			{
				// (freestyle: the pilot's throttle on the wheels too; the law, which also clears it, only runs in the air)
				// 0.5.4: except while the engine is held where it was at the switch (FreeLaw ends that when the throttle moves;
				// on the wheels it ends here)
				throttleOverride = freeThrHold && airNow;
				if (throttleOverride)
				{
					throttleOut = freeThrVal;
				}
				else
				{
					freeThrHold = false;
				}
			}
						if (onGround && state == StovlState.On && !liftIntent && !freestyle)
			{
				// on the wheels, throttle in (or behind) the aft detent or just landed: engine at idle, nozzle down (ready to lift)
				fanIn = 0f;
				postIn = 0f;
				throttleOverride = true;
				throttleOut = 0f;
				axis1Out = Mathf.Clamp(1f - Plugin.StovlGroundSwivel, 0f, 0.999f);
				vaneLive = false;
			}
		}
	}

	// the lift fan's pitch input as an effector: cmd -1..1 = controlInputs.pitch as SwivelDuctSystem reads it; force =
	// doors x cmd x pitchFactor x RPM ratio on top of the game's own balancing force (never below zero)
	internal sealed class LiftFanEffector : Effector
	{
		private readonly StovlSystem S;

		public LiftFanEffector(StovlSystem s)
		{
			S = s;
			name = "FAN";
			min = -1f;
			max = 1f;
			lo = min;
			hi = max;
			regNorm = 1f;
			mu = 2E-04f;
			lam = 2E-05f;
			AllocGrid(11);
			for (int i = 0; i < N; i++)
			{
				grid[i] = min + (float)i * (max - min) / (float)(N - 1);
			}
		}

		public override void Evaluate(ref EvalContext ctx)
		{
			active = false;
			for (int i = 0; i < N; i++)
			{
				M[i] = Vector3.zero;
			}
			Mnow = Vector3.zero;
			IStovlHardware hw = S.hw;
			Transform ft = hw.FanThrustT;
			Transform tt = hw.MainThrustT;
			if (!S.owns || ft == null || tt == null || hw.Doors <= 0.01f)
			{
				now = 0f;
				cmd = 0f;
				lo = 0f;
				hi = 0f;
				return;
			}
			now = S.fanIn;
			Vector3 arm = ft.position - ctx.cg;
			float f0 = S.FanForce(hw.DryThrust, tt.forward, tt.position, 0f);
			for (int i = 0; i < N; i++)
			{
				float fi = S.FanForce(hw.DryThrust, tt.forward, tt.position, grid[i]);
				M[i] = ToAero(ctx.rootInv, Vector3.Cross(arm, ft.forward * (fi - f0)));
			}
			float fn = S.FanForce(hw.DryThrust, tt.forward, tt.position, now);
			Mnow = ToAero(ctx.rootInv, Vector3.Cross(arm, ft.forward * (fn - f0)));
			rate = 8f;
			lo = min;
			// (0.2.8: toward less fan, lift-losing, only as far as the vertical axis can spare: StovlSystem.fanCap)
			hi = Mathf.Min(max, Mathf.Max(0f, S.fanTrim) + S.fanCap);
			active = hw.RpmRatio > 0.2f;
			if (!active)
			{
				cmd = 0f;
			}
		}
	}

	// the two roll posts as one effector: cmd -1..1 = controlInputs.roll as SwivelDuctSystem reads it (one post fires per side)
	internal sealed class RollPostEffector : Effector
	{
		private readonly StovlSystem S;

		public RollPostEffector(StovlSystem s)
		{
			S = s;
			name = "POST";
			min = -1f;
			max = 1f;
			lo = min;
			hi = max;
			regNorm = 1f;
			mu = 2E-04f;
			lam = 2E-05f;
			AllocGrid(11);
			for (int i = 0; i < N; i++)
			{
				grid[i] = min + (float)i * (max - min) / (float)(N - 1);
			}
		}

		public override void Evaluate(ref EvalContext ctx)
		{
			active = false;
			for (int i = 0; i < N; i++)
			{
				M[i] = Vector3.zero;
			}
			Mnow = Vector3.zero;
			IStovlHardware hw = S.hw;
			if (!S.owns || hw.Posts == 0)
			{
				now = 0f;
				cmd = 0f;
				lo = 0f;
				hi = 0f;
				return;
			}
			bool any = false;
			for (int i = 0; i < N; i++)
			{
				for (int j = 0; j < hw.Posts; j++)
				{
					Transform t = hw.PostT(j);
					Vector3 f = S.PostForce(j, grid[i]);
					if (t != null && f.sqrMagnitude > 0f)
					{
						M[i] += ToAero(ctx.rootInv, Vector3.Cross(t.position - ctx.cg, f));
						any = true;
					}
				}
			}
			now = S.postIn;
			for (int j = 0; j < hw.Posts; j++)
			{
				Transform t = hw.PostT(j);
				if (t != null)
				{
					Mnow += ToAero(ctx.rootInv, Vector3.Cross(t.position - ctx.cg, S.PostForce(j, now)));
				}
			}
			rate = 8f;
			// (0.2.8: the posts push down; only as hard as the vertical axis can spare: StovlSystem.postCap)
			lo = Mathf.Max(min, S.postTrim - S.postCap);
			hi = Mathf.Min(max, S.postTrim + S.postCap);
			active = any;
			if (!active)
			{
				cmd = 0f;
			}
		}
	}

	// the nozzle's yaw bearing (the game's Bearing.yawFactor x controlInputs.yaw, +-15 deg on the first bearing, at every speed)
	// as the jet's yaw TVC, in the slot of the FS-12's TvcYawEffector so every yaw-TVC rule (capacity, tiers, slip assist) uses
	// it. cmd in deg of bearing yaw. The game applies the input at once; the FCS slews it at TvcSlewRate (75 deg/s). The table
	// is relative to the yaw where it is now, with the pitch TVC table (live transforms, so it already holds the present yaw)
	// carrying the rest: sum = tvc.M(pitch) + M(yaw) - M(yaw now).
	internal sealed class SwivelYawEffector : TvcYawEffector
	{
		private readonly StovlSystem S;

		private float applied;

		private readonly float yawDeg;

		public float AppliedInput => (yawDeg > 0.1f) ? Mathf.Clamp(applied / yawDeg, -1f, 1f) : 0f;

		public SwivelYawEffector(TvcEffector sym, StovlSystem s)
			: base(sym)
		{
			S = s;
			name = "TVCy3BSM";
			float y = 0f;
			IStovlHardware hw = s.hw;
			for (int i = 0; i < hw.Bearings; i++)
			{
				y = Mathf.Max(y, hw.BearingYaw(i).magnitude);
			}
			yawDeg = Mathf.Max(0.1f, y);
			min = 0f - yawDeg;
			max = yawDeg;
			lo = min;
			hi = max;
			for (int i = 0; i < N; i++)
			{
				grid[i] = min + (float)i * (max - min) / (float)(N - 1);
			}
		}

		public void Advance(float dt, bool groundMode)
		{
			float target = (active && !groundMode) ? Mathf.Clamp(cmdOut, lo, hi) : 0f;
			applied = Mathf.MoveTowards(applied, target, Plugin.TvcSlew * dt);
		}

		public override float YawInput(float y)
		{
			return AppliedInput;
		}

		public override void Evaluate(ref EvalContext ctx)
		{
			active = false;
			for (int i = 0; i < N; i++)
			{
				M[i] = Vector3.zero;
			}
			Mnow = Vector3.zero;
			IStovlHardware hw = S.hw;
			Transform tt = hw.MainThrustT;
			if (Plugin.DisableTVC || !S.owns || tt == null)
			{
				now = applied;
				lo = 0f;
				hi = 0f;
				cmd = 0f;
				return;
			}
			float th = hw.NozzleThrust;
			now = applied;
			if (th <= 1f)
			{
				lo = min;
				hi = max;
				return;
			}
			float s = hw.Swivel;
			float yNowIn = AppliedInput;
			if (!S.NozzleAt(s, yNowIn, out var d0, out var p0))
			{
				return;
			}
			// the live nozzle includes the pitch TVC (thrustTransform's own rotation); NozzleAt rebuilds the chain from the live
			// local rotations below the bearings, so it does too
			Vector3 m0 = ToAero(ctx.rootInv, Vector3.Cross(p0 - ctx.cg, d0 * th));
			for (int k = 0; k < N; k++)
			{
				S.NozzleAt(s, grid[k] / yawDeg, out var dk, out var pk);
				M[k] = ToAero(ctx.rootInv, Vector3.Cross(pk - ctx.cg, dk * th)) - m0;
			}
			Mnow = Vector3.zero;   // tvc.Mnow is the nozzle as it is (this yaw included)
			rate = Plugin.TvcSlew;
			active = true;
			lo = min;
			hi = max;
		}
	}
}
