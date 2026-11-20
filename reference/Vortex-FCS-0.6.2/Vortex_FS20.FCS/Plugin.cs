using System.Reflection;
﻿using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// 1.8.43: one DLL for both builds. PerformanceProfile picks stock or buffed performance at runtime.
	public enum PerformanceProfile
	{
		Buffed,
		Stock
	}

	// Ifrit 0.3.1: lateral law with Stability Assist off (MPO)
	// Vortex 0.5.16: the caution box's text colour
	public enum CautionColor
	{
		Hud,
		Amber
	}

	public enum MpoRollMode
	{
		VelocityVectorRoll,
		SeparateRollYaw
	}

	// 2.1.0: gear-up pitch command law
	public enum PitchCommandLaw
	{
		GAndPitchRate,
		GAoAAndPitchRate,
		AoAThenRateHold
	}

	// 2.0.8: debug AoA-limit test switch
	public enum AoALimitTest
	{
		Normal,
		Full65,
		Cap36
	}

	internal static class Plugin
	{
		internal static ManualLogSource Log;

		private static ConfigEntry<bool> cEnabled;

		private static ConfigEntry<PerformanceProfile> cProfile;

		private static ConfigEntry<bool> cHostAllowFcs;

		private static ConfigEntry<bool> cHostLock;


		private static ConfigEntry<bool> cTelemetry;

		private static ConfigEntry<bool> cEstThreaded;

		private static ConfigEntry<bool> cTickTimer;

		private static ConfigEntry<bool> cParTables;

		private static ConfigEntry<bool> cParAlloc;

		private static ConfigEntry<bool> cParSweep;

		private static ConfigEntry<int> cNativeAlloc;

		private static ConfigEntry<bool> cSweepGrid;

		private static ConfigEntry<bool> cNativeSweep;

		private static ConfigEntry<bool> cNativeCpl;

		private static ConfigEntry<bool> cNativeTables;

		private static ConfigEntry<int> cNativeAllocCheck;

		private static ConfigEntry<float> cSlowTick;

		private static ConfigEntry<bool> cParEnv;

		private static ConfigEntry<bool> cAIFcs;

		private static ConfigEntry<bool> cAILog;

		private static ConfigEntry<bool> cAIDirectRoll;

		private static ConfigEntry<float> cStabUp;

		private static ConfigEntry<float> cStabDn;


		private static ConfigEntry<float> cFlapRange;

		private static ConfigEntry<float> cTvcRange;

		private static ConfigEntry<float> cTvcSpeed;

		private static ConfigEntry<bool> cAIGunRollFade;

		private static ConfigEntry<bool> cAsymOn;

		private static ConfigEntry<bool> cAsymRollHead;

		private static ConfigEntry<bool> cRevRoll;
		private static ConfigEntry<bool> cRevNose;



		private static ConfigEntry<float> cAoAPos;

		private static ConfigEntry<float> cAoANeg;

		private static ConfigEntry<int> cAoANegRev;

		private static ConfigEntry<bool> cIfritSplit;

		private static ConfigEntry<float> cFlapCamber;

		private static ConfigEntry<float> cAilCamber;

		// Vortex 0.3.1: elevons as flaps on the wing, wing vortex lift, no manoeuvre droop
		private static ConfigEntry<bool> cElevonFlapModel;

		private static ConfigEntry<float> cElevonFlapK;

		private static ConfigEntry<float> cInnerStripArea;

		private static ConfigEntry<float> cOuterStripArea;

		private static ConfigEntry<float> cElevonPlateLift;

		private static ConfigEntry<bool> cWingVortexLift;

		private static ConfigEntry<string> cWingLiftSched;

		private static ConfigEntry<bool> cElevonDroop;

		public static bool ElevonFlapModel => cElevonFlapModel == null || cElevonFlapModel.Value;

		public static float ElevonFlapK => (cElevonFlapK != null) ? Mathf.Clamp(cElevonFlapK.Value, 0f, 1f) : 0.21f;

		public static float InnerStripArea => (cInnerStripArea != null) ? Mathf.Clamp(cInnerStripArea.Value, 0.5f, 3f) : 1.48f;

		public static float OuterStripArea => (cOuterStripArea != null) ? Mathf.Clamp(cOuterStripArea.Value, 0.5f, 3f) : 1.44f;

		public static float ElevonPlateLiftScale => (cElevonPlateLift != null) ? Mathf.Clamp(cElevonPlateLift.Value, 0.2f, 1f) : 1f;

		public static bool WingVortexLift => cWingVortexLift == null || cWingVortexLift.Value;

		public static string WingLiftSchedule => (cWingLiftSched != null) ? cWingLiftSched.Value : "0:1,15:1,17:1.02,20:1.13,25:1.30,30:1.41,33:1.43,36:1.36,40:1.21,45:1.08,50:1.02,55:1";

		private static ConfigEntry<float> cPostStallFlap;

		public static float PostStallFlapEff => (cPostStallFlap != null) ? Mathf.Clamp(cPostStallFlap.Value, 0f, 1f) : 0.35f;

		private static ConfigEntry<float> cFlapCamberHi;

		public static float FlapCamberHiAoA => (cFlapCamberHi != null) ? Mathf.Clamp(cFlapCamberHi.Value, 30f, 90f) : 45f;

		private static ConfigEntry<float> cLeafFadeLo;

		private static ConfigEntry<float> cLeafFadeHi;

		public static float LeafRateCapFadeLo => (cLeafFadeLo != null) ? Mathf.Clamp(cLeafFadeLo.Value, 20f, 90f) : 45f;

		public static float LeafRateCapFadeHi => (cLeafFadeHi != null) ? Mathf.Clamp(cLeafFadeHi.Value, 20f, 91f) : 45f;

		public static bool ElevonManeuverDroop => cElevonDroop != null && cElevonDroop.Value;

		private static ConfigEntry<MpoRollMode> cMpoSep;

		private static ConfigEntry<int> cIfritRev;

		// Revoker 0.1.0
		private static ConfigEntry<bool> cYawTvc;

		private static ConfigEntry<float> cTvcYawMu;

		private static ConfigEntry<bool> cElevonPitch;

		private static ConfigEntry<bool> cCanardYawTier;

		private static ConfigEntry<float> cGroundElevonPitch;

		private static ConfigEntry<bool> cSlipYawTvc;

		private static ConfigEntry<float> cCorkTol;

		// Revoker 0.2.7
		private static ConfigEntry<float> cHiAoARollW;

		private static ConfigEntry<bool> cCanWin;

		private static ConfigEntry<float> cCanWinMargin;

		private static ConfigEntry<bool> cFinTvcHold;

		private static ConfigEntry<float> cFinNoTvcCap;

		private static ConfigEntry<bool> cVisHinge;

		private static ConfigEntry<float> cYawTvcNeutral;

		private static ConfigEntry<bool> cYawTvcNeutralYawGate;

		private static ConfigEntry<bool> cFlapRelief;

		private static ConfigEntry<bool> cCanardPitch;

		private static ConfigEntry<int> cRevokerRev;

		private static ConfigEntry<bool> cFlapFlaperons;

		private static ConfigEntry<float> cFlapHinge;

		private static ConfigEntry<float> cFlapHiLift;

		private static ConfigEntry<float> cElevonSymCost;

		public static float FlapCamberK => (cFlapCamber != null) ? Mathf.Clamp(cFlapCamber.Value, 0f, 1f) : 0.46f;

		public static float AileronCamberK => (cAilCamber != null) ? Mathf.Clamp(cAilCamber.Value, 0f, 1f) : 0.45f;

		public static bool MpoSeparateAxes => cMpoSep != null && cMpoSep.Value == MpoRollMode.SeparateRollYaw;

		private static ConfigEntry<float> cMpoWash;

		public static float MpoSlipWashout => (cMpoWash != null) ? Mathf.Max(0f, cMpoWash.Value) : 0.5f;

		private static ConfigEntry<bool> cIfritTvcRoll;

		private static ConfigEntry<float> cSplitMu;

		private static ConfigEntry<bool> cSlipTvc;

		private static ConfigEntry<bool> cIfritFlaperons;

		private static ConfigEntry<float> cServoSpeed;

		private static ConfigEntry<float> cSplitRange;

		private static ConfigEntry<bool> cTvcRollNeutral;

		private static ConfigEntry<float> cTvcRollNeutralW;

		private static ConfigEntry<float> cSlipGainHi;

		private static ConfigEntry<bool> cSlipGainLoops;

		private static ConfigEntry<float> cActLag;

		private static ConfigEntry<bool> cStagger;

		private static ConfigEntry<float> cStallInner;

		private static ConfigEntry<float> cStallOuter;

		private static ConfigEntry<float> cStallSlope;

		private static ConfigEntry<bool> cSlipGovLead;

		private static ConfigEntry<bool> cDecHldFold;

		private static ConfigEntry<float> cSlipHoldTas0;

		private static ConfigEntry<float> cSlipLimTas0;

		private static ConfigEntry<float> cSlipLimTasSpan;

		private static ConfigEntry<float> cSlipHoldTasSpan;

		private static ConfigEntry<float> cSlipPredT;

		private static ConfigEntry<float> cSlipPredRollGate;

		private static ConfigEntry<bool> cSplitYawTier;

		private static ConfigEntry<float> cStabYawRelax;


		private static ConfigEntry<float> cSlipPredW;

		private static ConfigEntry<float> cRevS0;

		private static ConfigEntry<float> cRevS1;

		private static ConfigEntry<float> cSlipGainA0;

		private static ConfigEntry<float> cFullStickQ;

		private static ConfigEntry<float> cSplitBrakeHold;

		private static ConfigEntry<float> cTvcDiffMu;

		public static bool IfritSplitYaw => cIfritSplit == null || cIfritSplit.Value;

		public static bool IfritTvcRoll => cIfritTvcRoll == null || cIfritTvcRoll.Value;

		public static bool SlipTvcAssist => cSlipTvc == null || cSlipTvc.Value;

		public static bool IfritFlaperons => cIfritFlaperons == null || cIfritFlaperons.Value;

		public static float SurfaceServoSpeed => (cServoSpeed != null) ? Mathf.Clamp(cServoSpeed.Value, 0f, 300f) : 100f;

		// split limit: SplitRange, or 0 = the deceleron's own travel (FlaperonRange; the stock 25 when that is 0 too)
		public static float SplitRange => (cSplitRange != null && cSplitRange.Value > 0f) ? Mathf.Clamp(cSplitRange.Value, 1f, 45f) : ((FlaperonRange > 0f) ? FlaperonRange : 0f);

		public static bool TvcRollNeutralHold => cTvcRollNeutral != null && cTvcRollNeutral.Value;

		public static float TvcRollNeutralWeight => (cTvcRollNeutralW != null) ? Mathf.Max(0f, cTvcRollNeutralW.Value) : 30f;

		public static float FullStickPitchRate => (cFullStickQ != null) ? Mathf.Clamp(cFullStickQ.Value, 0f, 90f) : 60f;

		public static float SlipGainAoAStart => (cSlipGainA0 != null) ? Mathf.Clamp(cSlipGainA0.Value, 0f, 60f) : 5f;

		public static float RevFlowSenseStart => (cRevS0 != null) ? Mathf.Clamp(cRevS0.Value, 90f, 178f) : 120f;

		public static float RevFlowSenseEnd => (cRevS1 != null) ? Mathf.Clamp(cRevS1.Value, 91f, 180f) : 150f;

		public static float SlipLimTasMin => (cSlipLimTas0 != null) ? Mathf.Clamp(cSlipLimTas0.Value, 0f, 100f) : 25f;

		public static float SlipLimTasSpan => (cSlipLimTasSpan != null) ? Mathf.Clamp(cSlipLimTasSpan.Value, 1f, 100f) : 50f;

		public static float SlipHoldTasMin => (cSlipHoldTas0 != null) ? Mathf.Clamp(cSlipHoldTas0.Value, 0f, 60f) : 15f;

		public static float SlipHoldTasSpan => (cSlipHoldTasSpan != null) ? Mathf.Clamp(cSlipHoldTasSpan.Value, 1f, 60f) : 20f;

		public static bool DeceleronHldFold => cDecHldFold == null || cDecHldFold.Value;

		public static float SlipPredictTime => (cSlipPredT != null) ? Mathf.Clamp(cSlipPredT.Value, 0f, 0.8f) : 0.3f;

		public static bool SplitYawTier => cSplitYawTier == null || cSplitYawTier.Value;


		public static float StabYawRelax => (cStabYawRelax != null) ? Mathf.Clamp01(cStabYawRelax.Value) : 1f;

		public static float SlipPredictRollGate => (cSlipPredRollGate != null) ? Mathf.Clamp(cSlipPredRollGate.Value, 0f, 1f) : 0f;

		public static float SlipPredictWashout => (cSlipPredW != null) ? Mathf.Clamp(cSlipPredW.Value, 0f, 10f) : 1.5f;

		public static float SlipGainHighAoA => (cSlipGainHi != null) ? Mathf.Max(0f, cSlipGainHi.Value) : 4f;

		public static bool SlipGainFromLoops => cSlipGainLoops == null || cSlipGainLoops.Value;

		public static bool SlipGovLeadFromLoops => cSlipGovLead == null || cSlipGovLead.Value;

		public static float ActuatorLagT => (cActLag != null) ? Mathf.Clamp(cActLag.Value, 0f, 0.5f) : 0.07f;

		public static float SplitMu => (cSplitMu != null) ? Mathf.Max(1E-06f, cSplitMu.Value) : 1E-03f;

		public static float SplitBrakeHoldCost => (cSplitBrakeHold != null) ? Mathf.Max(0f, cSplitBrakeHold.Value) : 0.5f;

		public static bool YawTvc => cYawTvc == null || cYawTvc.Value;

		public static float TvcYawMu => (cTvcYawMu != null) ? Mathf.Max(1E-06f, cTvcYawMu.Value) : 0.0005f;

		public static bool ElevonPitch => cElevonPitch == null || cElevonPitch.Value;

		public static bool FlapFlaperons => cFlapFlaperons == null || cFlapFlaperons.Value;

		public static float FlapHingeRange => (cFlapHinge != null) ? Mathf.Clamp(cFlapHinge.Value, 5f, 35f) : 25f;

		public static float FlapHighLiftDroop => (cFlapHiLift != null) ? Mathf.Clamp(cFlapHiLift.Value, 0f, 35f) : 0f;

		public static bool CanardPitchPriority => cCanardPitch == null || cCanardPitch.Value;

		public static bool FlaperonLoadRelief => cFlapRelief != null && cFlapRelief.Value;

		public static float YawTvcNeutralHold => (cYawTvcNeutral != null) ? Mathf.Max(0f, cYawTvcNeutral.Value) : 300f;

		public static bool YawTvcNeutralYawGate => cYawTvcNeutralYawGate == null || cYawTvcNeutralYawGate.Value;

		public static bool VisualHingeFix => cVisHinge == null || cVisHinge.Value;

		public static float CorkscrewSlipTol => (cCorkTol != null) ? Mathf.Clamp(cCorkTol.Value, 0f, 15f) : 0f;

		public static float HighAoARollWeight => (cHiAoARollW != null) ? Mathf.Clamp(cHiAoARollW.Value, 0.1f, 5f) : 1f;

		public static bool CanardAttachedWindow => cCanWin == null || cCanWin.Value;

		public static float CanardWindowMargin => (cCanWinMargin != null) ? Mathf.Clamp(cCanWinMargin.Value, 0f, 30f) : 12f;

		public static bool FinLossTvcHold => cFinTvcHold == null || cFinTvcHold.Value;

		public static float FinLossNoTvcAoACap => (cFinNoTvcCap != null) ? Mathf.Clamp(cFinNoTvcCap.Value, 0f, 70f) : 30f;

		public static bool SlipYawTvcAssist => cSlipYawTvc == null || cSlipYawTvc.Value;

		public static float GroundElevonPitch => (cGroundElevonPitch != null) ? Mathf.Clamp01(cGroundElevonPitch.Value) : 1f;

		public static bool CanardYawTier => cCanardYawTier != null && cCanardYawTier.Value;

		public static float ElevonSymCost => (cElevonSymCost != null) ? Mathf.Max(0f, cElevonSymCost.Value) : 2f;

		public static float TvcDiffMu => (cTvcDiffMu != null) ? Mathf.Max(1E-06f, cTvcDiffMu.Value) : 0.003f;

		private static ConfigEntry<float> cGPos;

		private static ConfigEntry<float> cGNeg;

		private static ConfigEntry<float> cMpoGPos;

		private static ConfigEntry<float> cMpoGNeg;

		private static ConfigEntry<float> cMaxPitch;

		private static ConfigEntry<float> cProt;

		private static ConfigEntry<float> cMaxRoll;

		private static ConfigEntry<float> cMaxYaw;

		private static ConfigEntry<float> cCouple;

		private static ConfigEntry<float> cPedalBeta;

		private static ConfigEntry<float> cKBeta;

		private static ConfigEntry<float> cKRoll;

		private static ConfigEntry<float> cKPitch;

		private static ConfigEntry<float> cKYaw;

		private static ConfigEntry<float> cKpAoA;

		private static ConfigEntry<bool> cPairTrim;

		private static ConfigEntry<float> cLatRes;

		private static ConfigEntry<bool> cBranch;

		private static ConfigEntry<bool> cOvPrio;

		private static ConfigEntry<float> cCoupRoll;

		private static ConfigEntry<float> cBranchMaxAoA;

		private static ConfigEntry<float> cBranchTarget;

		private static ConfigEntry<float> cBranchTol;

		private static ConfigEntry<float> cAntiWind;

		private static ConfigEntry<bool> cThrBrake;

		private static ConfigEntry<float> cBrakeBlend;

		private static ConfigEntry<float> cKiAoA;

		private static ConfigEntry<float> cKpG;

		private static ConfigEntry<float> cKiG;



		private static ConfigEntry<float> cActuator;



		private static ConfigEntry<float> cUnloadDecel;

		private static ConfigEntry<float> cUnloadRise;

		private static ConfigEntry<float> cRollLo;

		private static ConfigEntry<float> cRollHi;

		private static ConfigEntry<float> cRollAuth;

		private static ConfigEntry<float> cRollBrake;

		private static ConfigEntry<float> cStabTrim;





		private static ConfigEntry<float> cStabPeakMargin;




		private static ConfigEntry<bool> cTvcOff;

		private static ConfigEntry<AoALimitTest> cAoATest;

		private static ConfigEntry<float> cNoTvcCapPos;

		private static ConfigEntry<float> cNoTvcCapNeg;

		private static ConfigEntry<float> cNoTvcThrCapPos;

		private static ConfigEntry<float> cNoTvcThrCapNeg;

		private static ConfigEntry<float> cLowThrustTW;

		private static ConfigEntry<bool> cRateOnly;

		private static ConfigEntry<PitchCommandLaw> cPitchLaw;

		private static ConfigEntry<bool> cLowCapRoll;

		private static ConfigEntry<bool> cAsymShared;

		private static ConfigEntry<bool> cGovPred;

		private static ConfigEntry<float> cPullRollTrade;

		// Vortex FCS 0.3.1 (F-22E 2.6.0): the lateral reference governor's knobs (player config section 6) and the 2.4.18 yaw slew lead (Dev)
		private static ConfigEntry<float> cRollStopSlip;

		private static ConfigEntry<int> cGovRev;
		private static ConfigEntry<float> cPushSlip;


		private static ConfigEntry<bool> cYawSlewLead;

		private static ConfigEntry<bool> cRollGovPitch;

		private static ConfigEntry<bool> cRollStopSlipProbe;

		private static ConfigEntry<float> cRollPaceRateSlip;

		private static ConfigEntry<float> cRollPaceRateBlend;

		private static ConfigEntry<float> cSplitFlapRollYield;

		private static ConfigEntry<bool> cSplitFlapYaw;

		private static ConfigEntry<float> cSplitFlapYawUse;

		private static ConfigEntry<float> cRollPitchLead;

		private static ConfigEntry<bool> cStabsPitchOnly;

		private static ConfigEntry<bool> cSlatRelief;

		private static ConfigEntry<bool> cCoupGateLim;

		private static ConfigEntry<bool> cLoadedRollSteady;

		private static ConfigEntry<bool> cSlipYawW;


		private static ConfigEntry<bool> cCapLag;


		private static ConfigEntry<float> cLeadK;

		private static ConfigEntry<float> cLeadTau;

		private static ConfigEntry<bool> cFuelBal;

		private static ConfigEntry<bool> cFuelAttached;

		private static ConfigEntry<bool> cFuelBalLog;

		private static ConfigEntry<float> cFuelRate;

		private static ConfigEntry<float> cFuelTolLat;

		private static ConfigEntry<float> cFuelTolLong;





		private static ConfigEntry<bool> cGroundRate;

		private static ConfigEntry<float> cGroundRotRamp;






		private static ConfigEntry<float> cTvcNeutral;

		private static ConfigEntry<bool> cStabPark;

		private static ConfigEntry<float> cSettledMargin;

		private static ConfigEntry<float> cSettledOn;

		private static ConfigEntry<float> cSettledOff;

		private static ConfigEntry<float> cSettledWalk;

		private static ConfigEntry<float> cSettledSlope;

		private static ConfigEntry<float> cSettledYawUse;


		private static ConfigEntry<bool> cRollFightRel;


		private static ConfigEntry<int> cAirframeRev;


		private static ConfigEntry<float> cCoupFF;

		private static ConfigEntry<float> cRelPitchPrio;

		private static ConfigEntry<float> cWashSlew;

		private static ConfigEntry<float> cMomLead;

		private static ConfigEntry<float> cOnset;

		private static ConfigEntry<float> cStabRoll;

		private static ConfigEntry<float> cStabRollShare;

		private static ConfigEntry<float> cStickSmooth;

		private static ConfigEntry<float> cActDamp;

		private static ConfigEntry<float> cYawStop;

		private static ConfigEntry<float> cOuterFlap;

		private static ConfigEntry<float> cRollAcc;

		private static ConfigEntry<float> cThrust;

		private static ConfigEntry<float> cDrag;

		private static ConfigEntry<float> cDrAoA;

		private static ConfigEntry<float> cStockDrAoA;

		private static ConfigEntry<float> cStockDrFade0;

		private static ConfigEntry<float> cStockDrFade1;



		private static ConfigEntry<float> cDrTrans;


		private static ConfigEntry<float> cYawPri;

		private static ConfigEntry<bool> cSlipCredit;

		private static ConfigEntry<bool> cPairPass;

		private static ConfigEntry<bool> cSlipBandOn;

		private static ConfigEntry<float> cSlipBand;

		private static ConfigEntry<float> cSlipBandSpan;

		private static ConfigEntry<float> cSlipBandGain;




		private static ConfigEntry<float> cSlipCreditR0, cSlipCreditSpan, cSlipCreditA0;

		private static ConfigEntry<float> cYawFF;

		private static ConfigEntry<float> cRollLead;

		private static ConfigEntry<float> cFlareScaleStock;

		private static ConfigEntry<float> cFlareScaleBuffed;

		private static ConfigEntry<float> cEwCapStock;

		private static ConfigEntry<float> cEwCapBuffed;

		private static ConfigEntry<float> cEwRateStock;

		private static ConfigEntry<float> cEwRateBuffed;

		private static ConfigEntry<float> cLowYaw;

		private static ConfigEntry<float> cLowYawIas;

		private static ConfigEntry<float> cLowYawFall;

		private static ConfigEntry<float> cTvcMove;

		private static ConfigEntry<float> cRollTvc;

		private static ConfigEntry<float> cRollWash;

		private static ConfigEntry<float> cRollTvcBudget;

		private static ConfigEntry<float> cRollTvcBudgetCost;


		private static ConfigEntry<bool> cNwsFbw;

		private static ConfigEntry<bool> cGndStock;

		private static ConfigEntry<bool> cGearDownSlip;

		private static ConfigEntry<bool> cGndIndi;

		private static ConfigEntry<float> cGndIndiK;

		private static ConfigEntry<bool> cGndLightNose;

		private static ConfigEntry<float> cGndPedalBrake;

		private static ConfigEntry<bool> cGndIndiBrake;

		private static ConfigEntry<float> cGndIndiBrakeMax;

		private static ConfigEntry<float> cGndIndiBrakeFull;

		private static ConfigEntry<float> cGndIndiBrakeZero;

		private static ConfigEntry<float> cGndIndiTau;

		private static ConfigEntry<float> cGndDriftAllow;

		private static ConfigEntry<float> cGndDriftGain;

		private static ConfigEntry<float> cGndDriftLead;

		private static ConfigEntry<float> cGndDriftFade;

		private static ConfigEntry<float> cGndYawRate;

		private static ConfigEntry<float> cGndLatAcc;

		private static ConfigEntry<float> cGndLine;



		private static ConfigEntry<float> cAsymPitchMargin;

		private static ConfigEntry<bool> cAsymRollCoupling;

		private static ConfigEntry<bool> cRollPitchPriority;

		private static ConfigEntry<float> cRollFightCost;


		private static ConfigEntry<float> cPitchFight;

		private static ConfigEntry<float> cRollPitchReserve;



		private static ConfigEntry<float> cGunAmmoStock;

		private static ConfigEntry<float> cGunAmmoBuffed;

		private static ConfigEntry<float> cFuelUse;

		private static ConfigEntry<float> cRotRate;

		private static ConfigEntry<float> cAsymReserve;

		private static ConfigEntry<float> cAsymFloor;

		private static ConfigEntry<float> cAsymFloorNeg;

		private static ConfigEntry<float> cAsymTighten;

		private static ConfigEntry<float> cAsymRelax;

		private static ConfigEntry<float> cAsymRec;

		private static ConfigEntry<float> cAsymMinIas;

		private static ConfigEntry<float> cAsymRollAcc;

		private static ConfigEntry<float> cAsymYawAcc;

		private static ConfigEntry<float> cAsymBoost;

		private static ConfigEntry<float> cAsymDead;

		private static ConfigEntry<float> cAsymFF;

		private static ConfigEntry<float> cAsymFFTau;

		private static ConfigEntry<float> cAsymRollTau;

		private static ConfigEntry<float> cAsymRelaxBoost;

		private static ConfigEntry<float> cAsymStopTime;

		private static ConfigEntry<float> cAsymAoAWeight;

		private static ConfigEntry<float> cRollPitchFF;

		private static ConfigEntry<float> cRollPitchFFAoA;

		private static ConfigEntry<float> cAsymFFBeta;

		private static ConfigEntry<float> cAsymFFAoA;



		private static ConfigEntry<float> cLowQReg;

		private static ConfigEntry<float> cRollStop;

		private static ConfigEntry<float> cRollAccUse;

		private static ConfigEntry<float> cRollAccUseLow;






		private static ConfigEntry<float> cHlMaxAoA;

		private static ConfigEntry<float> cHlHighAoA;


		private static ConfigEntry<float> cRotMinSpeed;

		private static ConfigEntry<float> cSlatStart;

		private static ConfigEntry<float> cSlatFull;


		private static ConfigEntry<float> cGUnder;

		private static ConfigEntry<float> cFFMax;

		private static ConfigEntry<float> cFFStabCost;

		private static ConfigEntry<bool> cGndBrakeSplit;

		private static ConfigEntry<bool> cThrIndepBrake;

		public static bool ThrottleIndependentAirbrake => cThrIndepBrake == null || cThrIndepBrake.Value;

		// Vortex 0.2.8: STOVL release guard (ReleaseGuard.cs)
		public static bool ReleaseGuardOn => cRelGuard == null || cRelGuard.Value;

		public static float ReleaseGuardHold => (cRelGuardHold == null) ? 1f : Mathf.Clamp(cRelGuardHold.Value, 0.3f, 5f);

		public static float ReleaseGuardSecondHold => (cRelGuardHold2 == null) ? 1.7f : Mathf.Clamp(cRelGuardHold2.Value, 0.3f, 10f);

		public static float ReleaseGuardCooldown => (cRelGuardCd == null) ? 10f : Mathf.Clamp(cRelGuardCd.Value, 0f, 120f);

		private static ConfigEntry<bool> cRelGuard;

		private static ConfigEntry<float> cRelGuardHold;

		private static ConfigEntry<float> cRelGuardHold2;

		private static ConfigEntry<float> cRelGuardCd;




		public static bool Enabled => cEnabled.Value && MpPolicy.FcsAllowed;

		public static bool TelemetryEnabled => cTelemetry.Value;

		// 3.0.4
		public static bool EstimatorThreaded => cEstThreaded == null || cEstThreaded.Value;

		public static bool TickTimerLog => cTickTimer == null || cTickTimer.Value;

		// 3.1.0
		public static bool ParallelTables => cParTables == null || cParTables.Value;

		public static bool ParallelAllocator => cParAlloc == null || cParAlloc.Value;

		// 3.1.2
		public static bool ParallelSweep => cParSweep == null || cParSweep.Value;

		// 1.1.0 (the F-22E 4.0.0 - 4.1.3 performance work): the native core, the sweep's geometry cache, the slow-tick log
		public static int NativeAllocator => (cNativeAlloc == null) ? 2 : cNativeAlloc.Value;

		public static int NativeAllocatorCheck => (cNativeAllocCheck == null) ? 0 : cNativeAllocCheck.Value;

		public static bool NativeSweep => cNativeSweep == null || cNativeSweep.Value;

		public static bool NativeCoupledCap => cNativeCpl == null || cNativeCpl.Value;

		public static bool NativeTables => cNativeTables == null || cNativeTables.Value;

		public static bool SweepGridCache => cSweepGrid == null || cSweepGrid.Value;

		public static float SlowTickLogUs => (cSlowTick == null) ? 1500f : cSlowTick.Value;

		// 3.1.3
		public static bool ParallelEnvelope => cParEnv == null || cParEnv.Value;

		public static bool AIFlightControl => cAIFcs.Value;

		public static bool AIFlightLog => cAILog.Value;

		public static bool AIDirectRoll => cAIDirectRoll.Value;

		public static float StabRangeTEUp => Mathf.Clamp(cStabUp.Value, 0f, 80f);

		public static float StabRangeTEDown => Mathf.Clamp(cStabDn.Value, 0f, 80f);

		public static float RudderRange => Mathf.Clamp((35f), 0f, 45f);   // Vortex 0.3.12 (Tom, 17 Nov 17:09): +-35 (0.1.0-0.3.11: +-30; stock yaw 20 + toe 40)

		public static float FlaperonRange => Mathf.Clamp(cFlapRange.Value, 0f, 40f);

		public static float TvcRange => Mathf.Clamp(cTvcRange.Value, 0f, 20f);

		public static float TvcSlew => (cTvcSpeed.Value > 0f) ? Mathf.Clamp(cTvcSpeed.Value, 10f, 300f) : (70f * ActuatorScale);

		public static bool AIGunRollFade => cAIGunRollFade.Value;

		// 2.0.8: the debug AoALimit test switch overrides the positive limit (Full65 = 65, Cap36 = 36 at most)
		public static float AoAPos => AoAFull65 ? 65f : (AoACap36 ? Mathf.Min(cAoAPos.Value, NoTvcAoACapPos) : cAoAPos.Value);

		// 2.0.9: AoA caps while the nozzles do not cover pitch (DisableTVC or T/W under LowThrustFixedAoATW), debug-adjustable
		public static float NoTvcAoACapPos => (cNoTvcCapPos != null) ? Mathf.Clamp(cNoTvcCapPos.Value, 10f, 65f) : 45f;

		public static float NoTvcAoACapNeg => (cNoTvcCapNeg != null) ? Mathf.Clamp(Mathf.Abs(cNoTvcCapNeg.Value), 10f, 45f) : 45f;

		// 2.2.2: with DisableTVC, the caps when the thrust is up (positive: from T/W NoTvcThrustCapTW0, full at NoTvcThrustCapTW1)
		public static float NoTvcThrCapPos => (cNoTvcThrCapPos != null) ? Mathf.Clamp(cNoTvcThrCapPos.Value, 10f, 65f) : 45f;

		public static float NoTvcThrCapNeg => (cNoTvcThrCapNeg != null) ? Mathf.Clamp(Mathf.Abs(cNoTvcThrCapNeg.Value), 10f, 45f) : 45f;

		public static bool DisableTVC => cTvcOff != null && cTvcOff.Value;

		public static bool AoAFull65 => cAoATest != null && cAoATest.Value == AoALimitTest.Full65;

		public static bool AoACap36 => cAoATest != null && cAoATest.Value == AoALimitTest.Cap36;

		public static float StabDownwash => Mathf.Clamp((0f), 0f, 0.7f);

		public static float StabPeakMargin => (cStabPeakMargin != null) ? Mathf.Clamp(cStabPeakMargin.Value, 0f, 90f) : 90f;

		public static bool StovlEnabled => (cStovlEnabled == null) ? true : cStovlEnabled.Value;
		public static float StovlConversionMaxIas => (cStovlConversionMaxIas == null) ? 460f : cStovlConversionMaxIas.Value;
		public static float StovlReconversionMinIas => (cStovlReconversionMinIas == null) ? 240f : cStovlReconversionMinIas.Value;
		public static float StovlCenterDetent => (cStovlCenterDetent == null) ? 0.5f : cStovlCenterDetent.Value;
		// Vortex 0.4.1: ship-relative STOVL speeds and its toggle key; the hover regime hand-over speeds
		// Vortex 0.4.11 (Tom, 18 Nov 01:05: "when i toggle it off with a hot key, and i join a different match, it must be on
		// again"): the key switches it for this match only (shipRelOverride); the config value is the default every match starts
		// with and the key no longer writes it. 0.4.1-0.4.10: the key wrote the config, so off stayed off into the next match
		// and the next game.
		public static bool StovlShipRelative => ShipRelOn(cShipRel != null && cShipRel.Value);

		private static bool ShipRelOn(bool cfgOn)
		{
			return (shipRelOverride >= 0) ? (shipRelOverride == 1) : cfgOn;
		}

		// (-1: the config value; 0 / 1: switched off / on with the key in this match)
		private static int shipRelOverride = -1;

		public static float StovlShipRelRange => (cShipRelRange != null) ? Mathf.Clamp(cShipRelRange.Value, 100f, 5000f) : 800f;

		public static float StovlRegimeShiftKt => (cRegimeShift != null) ? Mathf.Clamp(cRegimeShift.Value, 0f, 40f) : 30f;

		private static ConfigEntry<bool> cShipRel;

		private static ConfigEntry<float> cShipRelRange;

		private static ConfigEntry<float> cRegimeShift;

		internal static ConfigEntry<KeyboardShortcut> cShipRelKey;

		internal static ConfigEntry<KeyboardShortcut> cPirouetteKey;

		internal static void ToggleShipRelative()
		{
			shipRelOverride = StovlShipRelative ? 0 : 1;
		}

		// Vortex 0.4.11: a new match: back to the config value
		internal static bool ResetShipRelative()
		{
			bool was = shipRelOverride >= 0;
			shipRelOverride = -1;
			return was;
		}

		public static float StovlAoALimit => (cStovlAoALim != null) ? Mathf.Clamp(cStovlAoALim.Value, 0f, 90f) : 24f;

		public static float StovlAftDetent => (cStovlAftDetent == null) ? 0.20f : cStovlAftDetent.Value;

		public static float StovlBackGroundSpeed => (cStovlBackGs == null) ? 30f : Mathf.Max(0f, cStovlBackGs.Value);
		public static float StovlDetentWidth => (cStovlDetentWidth == null) ? 0.02f : cStovlDetentWidth.Value;
		public static float StovlCenterGroundSpeed => (cStovlCenterGroundSpeed == null) ? 30f : cStovlCenterGroundSpeed.Value;
		public static float StovlAccelMax => (cStovlAccelMax == null) ? 6f : cStovlAccelMax.Value;
		public static float StovlDecelMax => (cStovlDecelMax == null) ? 4f : cStovlDecelMax.Value;
		public static float StovlClimbRate => (cStovlClimbRate == null) ? 6f : cStovlClimbRate.Value;
		public static float StovlSinkRate => (cStovlSinkRate == null) ? 5f : cStovlSinkRate.Value;
		public static float StovlPathRate => (cStovlPathRate == null) ? 10f : cStovlPathRate.Value;
		public static float StovlHoverAttitude => (cStovlHoverAttitude == null) ? 3f : cStovlHoverAttitude.Value;
		public static float StovlWingAoAMax => (cStovlWingAoAMax == null) ? 12f : cStovlWingAoAMax.Value;
		public static float StovlTrcSpeed => (cStovlTrcSpeed == null) ? 15f : cStovlTrcSpeed.Value;
		public static float StovlBankMax => (cStovlBankMax == null) ? 45f : cStovlBankMax.Value;

		public static float StovlTrcBankRate => (cStovlTrcBankRate == null) ? 10f : Mathf.Clamp(cStovlTrcBankRate.Value, 2f, 60f);
		public static float StovlRollRateMax => (cStovlRollRateMax == null) ? 90f : cStovlRollRateMax.Value;

		public static float StovlFreestylePitchRate => (cStovlFreePitch == null) ? 30f : Mathf.Max(1f, cStovlFreePitch.Value);
		public static float StovlHoverYawRate => (cStovlHoverYawRate == null) ? 30f : cStovlHoverYawRate.Value;
		public static float StovlPedalSideslip => (cStovlPedalSideslip == null) ? 10f : cStovlPedalSideslip.Value;
		public static float StovlGroundSwivel => (cStovlGroundSwivel == null) ? 0.95f : cStovlGroundSwivel.Value;

		public static bool StovlLowMarginHover => cStovlLowMarg == null || cStovlLowMarg.Value;
		public static bool SlipSignLatchSlow => cSlipSignSlow == null || cSlipSignSlow.Value;
		public static bool StovlStiffDynScale => cStovlStiffQ == null || cStovlStiffQ.Value;
		public static bool StovlCtolNoseHold => cStovlCtolNose == null || cStovlCtolNose.Value;
		public static bool StovlLowTwPedal => cStovlLowTwPed == null || cStovlLowTwPed.Value;
		public static bool StovlSharedBudget => cStovlSharedBud == null || cStovlSharedBud.Value;
		public static bool StovlLeverStopModel => cStovlLeverStop == null || cStovlLeverStop.Value;
		public static bool StovlTiltAllRegimes => cStovlTiltAll == null || cStovlTiltAll.Value;
		public static bool StovlFreeWingAxes => cStovlFreeWing == null || cStovlFreeWing.Value;
		public static bool StovlEngineCondition => cStovlEngCond == null || cStovlEngCond.Value;

		public static bool StovlTouchdownAccel => cStovlTdAcc == null || cStovlTdAcc.Value;

		public static bool StovlYawRateMeanAuthority => cStovlYawMean == null || cStovlYawMean.Value;

		public static float StovlYawStopTime => (cStovlYawStop != null) ? Mathf.Clamp(cStovlYawStop.Value, 0f, 5f) : 1.25f;

		public static float StovlTrcLagMargin => (cStovlTrcLag != null) ? Mathf.Clamp(cStovlTrcLag.Value, 0.05f, 1f) : 0.32f;

		public static bool StovlGroundNoAutoRotate => cStovlNoAutoRot == null || cStovlNoAutoRot.Value;

		// Vortex 0.4.0 (Vortex_STOVL_authority_TRC_plan.md, groups B-D and item A10)
		public static float StovlTiltStopTime => (cStovlTiltStop != null) ? Mathf.Clamp(cStovlTiltStop.Value, 0f, 3f) : 0.42f;

		public static float StovlTrcStopTime => (cStovlTrcStop != null) ? Mathf.Clamp(cStovlTrcStop.Value, 0f, 10f) : 0.75f;

		public static bool StovlPhysicsBlends => cStovlPhysBlends == null || cStovlPhysBlends.Value;

		public static bool StovlFitWeights => cStovlFitWeights == null || cStovlFitWeights.Value;

		public static bool StovlClimbGainFromLead => cStovlClimbLead == null || cStovlClimbLead.Value;

		public static float StovlPosCatchSpeed => (cStovlPosCatch != null) ? cStovlPosCatch.Value : 0.5f;

		public static float StovlPosKeepSpeed => (cStovlPosKeep != null) ? cStovlPosKeep.Value : 0f;

		public static bool StovlBankGainFromMargin => cStovlBankFF != null && cStovlBankFF.Value;

		public static bool StovlTrcLagFromLoop => cStovlLagLoop == null || cStovlLagLoop.Value;

		public static bool StovlTiltRollFull => cStovlTiltRollFull == null || cStovlTiltRollFull.Value;

		// Vortex 0.4.7
		public static bool StovlOriginShiftFix => cStovlOriginFix == null || cStovlOriginFix.Value;

		public static bool StovlFwdCapFromNozzle => cStovlFwdCap == null || cStovlFwdCap.Value;

		public static bool StovlGovTiltCredit => cStovlGovCredit == null || cStovlGovCredit.Value;

		public static bool StovlTurnCoordinate => cStovlTurnCoord == null || cStovlTurnCoord.Value;

		public static bool StovlYawSideFF => cStovlYawSideFF == null || cStovlYawSideFF.Value;

		private static ConfigEntry<bool> cStovlYawSideFF;

		public static bool StovlYawPivot => cStovlYawPivot == null || cStovlYawPivot.Value;

		private static ConfigEntry<bool> cStovlYawPivot;

		public static bool StovlClimbShare => cStovlClimbShare == null || cStovlClimbShare.Value;

		private static ConfigEntry<bool> cStovlClimbShare;

		// Vortex 0.5.16
		public static bool StovlLiftCorrect => cStovlLiftCorrect == null || cStovlLiftCorrect.Value;

		private static ConfigEntry<bool> cStovlLiftCorrect;

		public static bool StovlWingLiftBudget => cStovlWingLift == null || cStovlWingLift.Value;

		private static ConfigEntry<bool> cStovlWingLift;

		public static bool StovlMinSpeedFloor => cStovlMinSpeed == null || cStovlMinSpeed.Value;

		public static float StovlManeuverTilt => (cStovlManTilt == null) ? 20f : Mathf.Clamp(cStovlManTilt.Value, 0f, 40f);

		private static ConfigEntry<float> cStovlManTilt;

		private static ConfigEntry<bool> cStovlMinSpeed;

		public static bool StovlSmoothTouchdown => cStovlSmoothTd == null || cStovlSmoothTd.Value;

		private static ConfigEntry<bool> cStovlSmoothTd;

		public static bool CautionBoxOn => cCautionBox == null || cCautionBox.Value;

		private static ConfigEntry<bool> cCautionBox;

		public static CautionColor CautionBoxColor => (cCautionColor == null) ? CautionColor.Hud : cCautionColor.Value;

		private static ConfigEntry<CautionColor> cCautionColor;

		public static bool StovlTurnYawLimit => cStovlTurnYawLim == null || cStovlTurnYawLim.Value;

		public static bool StovlPedalPirouette => cStovlPirouette != null && cStovlPirouette.Value;

		public static float StovlPirouetteSlipShare => (cStovlPirSlip != null) ? cStovlPirSlip.Value : 0.8f;

		public static float StovlTurnShare => (cStovlTurnShare != null) ? cStovlTurnShare.Value : 0.85f;

		// Vortex 0.5.8 (Tom, 18 Nov: the pirouette as "an assignable key-bind off by default in the dll, i can either tap or
		// hold to toggle"): StovlPedalPirouetteKey (unassigned by default) switches the pedal pirouette the other way from
		// StovlPedalPirouette (false by default, so the key turns it on); a tap latches, a hold (StovlPirouetteKeyHold s or
		// longer) has it the other way only while held. Off at load and at every new mission. FcsStandalonePlugin reads the key;
		// Stovl uses StovlPedalPirouette != PirouetteKeyOn (inline: the bench's Plugin stub turns properties into fields).
		internal static bool PirouetteKeyOn;

		internal const float StovlPirouetteKeyHold = 0.35f;

		// Vortex 0.4.9
		public static bool TeUpFromGeometry => cTeUpGeo == null || cTeUpGeo.Value;

		public static float StovlTiltLiftDip => (cStovlTiltDip != null) ? cStovlTiltDip.Value : 0.2f;

		// Vortex 0.5.0: the lift fan's vane box (Stovl.cs, VaneBox.cs)
		public static bool StovlVanes => cStovlVanes == null || cStovlVanes.Value;

		public static bool StovlVaneModel => cStovlVaneModel == null || cStovlVaneModel.Value;

		public static float StovlVaneMin => (cStovlVaneMin != null) ? Mathf.Clamp(cStovlVaneMin.Value, -30f, 0f) : -10f;

		public static float StovlVaneMax => (cStovlVaneMax != null) ? Mathf.Clamp(cStovlVaneMax.Value, 1f, 60f) : 42f;

		public static float StovlVaneRate => (cStovlVaneRate != null) ? Mathf.Clamp(cStovlVaneRate.Value, 2f, 200f) : 30f;

		public static float StovlVaneTurning => (cStovlVaneTurning != null) ? Mathf.Clamp(cStovlVaneTurning.Value, 0.5f, 1f) : 0.9f;

		public static float StovlVaneLoss => (cStovlVaneLoss != null) ? Mathf.Clamp(cStovlVaneLoss.Value, 0f, 0.3f) : 0.05f;

		public static float StovlVaneStopLag => (cStovlVaneStopLag != null) ? Mathf.Clamp(cStovlVaneStopLag.Value, 0.1f, 0.5f) : 0.25f;

		public static bool StovlVaneStopJerk => cStovlVaneStopJerk != null && cStovlVaneStopJerk.Value;

		private static ConfigEntry<bool> cStovlVanes;

		private static ConfigEntry<bool> cStovlVaneModel;

		private static ConfigEntry<float> cStovlVaneMin;

		private static ConfigEntry<float> cStovlVaneMax;

		private static ConfigEntry<float> cStovlVaneRate;

		private static ConfigEntry<float> cStovlVaneTurning;

		private static ConfigEntry<float> cStovlVaneLoss;

		private static ConfigEntry<float> cStovlVaneStopLag;

		private static ConfigEntry<bool> cStovlVaneStopJerk;

		public static bool StovlTiltLiftLimit => cStovlTiltLift == null || cStovlTiltLift.Value;

		public static float StovlThrottleDetentHold => (cStovlThrDetentHold == null) ? 200f : Mathf.Clamp(cStovlThrDetentHold.Value, 0f, 2000f);

		public static bool StovlThrottleDetentIndicator => cStovlThrDetentInd == null || cStovlThrDetentInd.Value;

		public static bool StovlAutoTransition => cStovlAuto != null && cStovlAuto.Value;

		public static float StovlAutoCtolIas => (cStovlAutoCtol == null) ? 370f : Mathf.Clamp(cStovlAutoCtol.Value, 150f, 600f);

		public static float StovlAutoStovlIas => (cStovlAutoStovl == null) ? 350f : Mathf.Min(cStovlAutoStovl.Value, StovlAutoCtolIas - 20f);

		public static float StovlFanDoorDragArea => (cStovlDoorDrag == null) ? 0.6f : Mathf.Max(0f, cStovlDoorDrag.Value);

		public static float StovlTeDroop => (cStovlTeDroop == null) ? 25f : cStovlTeDroop.Value;

		public static float StovlRudderFlare => (cStovlRudderFlare == null) ? 20f : cStovlRudderFlare.Value;

		private static ConfigEntry<bool> cStovlEnabled;
		private static ConfigEntry<float> cStovlConversionMaxIas;
		private static ConfigEntry<float> cStovlReconversionMinIas;
		private static ConfigEntry<float> cStovlCenterDetent;
		private static ConfigEntry<float> cStovlAftDetent;

		private static ConfigEntry<float> cStovlAoALim;

		private static ConfigEntry<float> cStovlFreePitch;

		private static ConfigEntry<float> cStovlBackGs;
		private static ConfigEntry<float> cStovlDetentWidth;
		private static ConfigEntry<float> cStovlCenterGroundSpeed;
		private static ConfigEntry<float> cStovlAccelMax;
		private static ConfigEntry<float> cStovlDecelMax;
		private static ConfigEntry<float> cStovlClimbRate;
		private static ConfigEntry<float> cStovlSinkRate;
		private static ConfigEntry<float> cStovlPathRate;
		private static ConfigEntry<float> cStovlHoverAttitude;
		private static ConfigEntry<float> cStovlWingAoAMax;
		private static ConfigEntry<float> cStovlTrcSpeed;
		private static ConfigEntry<float> cStovlBankMax;

		private static ConfigEntry<float> cStovlTrcBankRate;
		private static ConfigEntry<float> cStovlRollRateMax;
		private static ConfigEntry<float> cStovlHoverYawRate;

		private static ConfigEntry<int> cVortexRev;
		private static ConfigEntry<float> cStovlPedalSideslip;
		private static ConfigEntry<float> cStovlGroundSwivel;

		private static ConfigEntry<bool> cStovlNoAutoRot;

		private static ConfigEntry<float> cStovlTiltStop;

		private static ConfigEntry<float> cStovlTrcStop;

		private static ConfigEntry<bool> cStovlPhysBlends;

		private static ConfigEntry<bool> cStovlFitWeights;

		private static ConfigEntry<bool> cStovlClimbLead;

		private static ConfigEntry<float> cStovlPosCatch;

		private static ConfigEntry<float> cStovlPosKeep;

		private static ConfigEntry<bool> cStovlBankFF;

		private static ConfigEntry<bool> cStovlTiltLift;

		private static ConfigEntry<float> cStovlTiltDip;

		private static ConfigEntry<bool> cStovlLagLoop;

		private static ConfigEntry<bool> cStovlTiltRollFull;

		private static ConfigEntry<bool> cStovlOriginFix;

		private static ConfigEntry<bool> cStovlFwdCap;

		private static ConfigEntry<bool> cStovlGovCredit;

		private static ConfigEntry<bool> cStovlYawMean;

		private static ConfigEntry<bool> cStovlTdAcc;

		private static ConfigEntry<bool> cStovlLowMarg;
		private static ConfigEntry<bool> cSlipSignSlow;
		private static ConfigEntry<bool> cStovlStiffQ;
		private static ConfigEntry<bool> cStovlCtolNose;
		private static ConfigEntry<bool> cStovlLowTwPed;
		private static ConfigEntry<bool> cStovlSharedBud;
		private static ConfigEntry<bool> cStovlLeverStop;
		private static ConfigEntry<bool> cStovlTiltAll;
		private static ConfigEntry<bool> cStovlFreeWing;
		private static ConfigEntry<bool> cStovlEngCond;

		private static ConfigEntry<bool> cStovlTurnCoord;

		private static ConfigEntry<bool> cStovlTurnYawLim;

		private static ConfigEntry<bool> cStovlPirouette;

		private static ConfigEntry<float> cStovlPirSlip;

		private static ConfigEntry<int> cVortexGenRev;

		private static ConfigEntry<float> cStovlTurnShare;

		private static ConfigEntry<bool> cTeUpGeo;

		private static ConfigEntry<float> cStovlTrcLag;

		private static ConfigEntry<float> cStovlYawStop;

		private static ConfigEntry<float> cStovlThrDetentHold;

		private static ConfigEntry<bool> cStovlThrDetentInd;

		private static ConfigEntry<bool> cStovlAuto;

		private static ConfigEntry<float> cStovlAutoCtol;

		private static ConfigEntry<float> cStovlAutoStovl;

		private static ConfigEntry<float> cStovlDoorDrag;

		private static ConfigEntry<float> cStovlTeDroop;

		private static ConfigEntry<float> cStovlRudderFlare;

		public static bool BodyLiftShaping => cVortexLift == null || cVortexLift.Value;

		public static string ForeLiftSchedule => (cForeLiftSched != null) ? cForeLiftSched.Value : "0:1.22,8:1.22,12:1.23,16:1.26,20:1.32,24:1.40,28:1.45,32:1.49,36:1.50,40:1.50,44:1.43,48:1.31,52:1.21,56:1.10,60:1.00";

		private static ConfigEntry<bool> cVortexLift;

		private static ConfigEntry<string> cForeLiftSched;

		public static float BodyLiftFore => Mathf.Clamp((0.9f), 0.3f, 2f);

		public static float BodyLiftAft => Mathf.Clamp((1.8f), 0.3f, 3f);

		public static float AoANeg => AoACap36 ? Mathf.Max(0f - Mathf.Abs(cAoANeg.Value), 0f - NoTvcAoACapNeg) : (0f - Mathf.Abs(cAoANeg.Value));

		public static float GPos => cGPos.Value;

		public static float GNeg => 0f - Mathf.Abs(cGNeg.Value);

		public static float MpoGPos => cMpoGPos.Value;

		public static float MpoGNeg => 0f - Mathf.Abs(cMpoGNeg.Value);

		public static float MaxPitchRate => cMaxPitch.Value;

		public static float RotationRate => cRotRate.Value;

		public static float ProtRate => cProt.Value;

		public static float MaxRollRate => cMaxRoll.Value;

		public static float MaxYawRate => cMaxYaw.Value;

		public static float RollGainLowIAS => cRollLo.Value;

		public static float RollGainHighIAS => cRollHi.Value;

		public static float RollAuthorityUse => cRollAuth.Value;

		public static float RollBrakeUse => cRollBrake.Value;

		public static float StabTrimCost => cStabTrim.Value;

		public static float LerxEffectiveness => (1f);

		public static bool StallStagger => cStagger == null || cStagger.Value;

		public static float LowThrustTW => cLowThrustTW.Value;

		public static bool RateCommandOnly => cRateOnly.Value;

		public static float StickLeadGain => cLeadK == null ? 0f : Mathf.Clamp(cLeadK.Value, 0f, 3f);

		public static float StickLeadTau => cLeadTau == null ? 0.15f : Mathf.Clamp(cLeadTau.Value, 0.03f, 0.5f);

		public static bool RollGovPredictiveOn => cGovPred == null || cGovPred.Value;

		public static bool SplitFlaperonYaw => cSplitFlapYaw == null || cSplitFlapYaw.Value;

		public static float SplitFlaperonYawUse => cSplitFlapYawUse == null ? 1f : Mathf.Clamp(cSplitFlapYawUse.Value, 0f, 1f);

		public static float SplitFlaperonRollYield => cSplitFlapRollYield == null ? 0f : Mathf.Clamp(cSplitFlapRollYield.Value, 0f, 1f);

		public static float PullRollTrade => cPullRollTrade == null ? 0.7f : Mathf.Clamp(cPullRollTrade.Value, 0f, 1f);

		public static float RollStopSideslip => cRollStopSlip == null ? 4f : Mathf.Clamp(cRollStopSlip.Value, 1f, 15f);

		public static float PushoverSideslip => cPushSlip == null ? RollStopSideslip : (cPushSlip.Value < 0f ? RollStopSideslip : Mathf.Clamp(cPushSlip.Value, 0f, 20f));

		public static bool RollGovernorPitch => cRollGovPitch == null || cRollGovPitch.Value;

		public static bool RollStopSlipProbe => cRollStopSlipProbe == null || cRollStopSlipProbe.Value;

		public static float RollPaceRateBlend => (cRollPaceRateBlend == null) ? 1f : Mathf.Clamp01(cRollPaceRateBlend.Value);

		public static float RollPaceRateSlip => (cRollPaceRateSlip == null) ? 1f : Mathf.Clamp(cRollPaceRateSlip.Value, 0.5f, 3f);

		public static bool YawSlewLead => cYawSlewLead == null || cYawSlewLead.Value;

		public static bool StabsPitchOnly => cStabsPitchOnly != null && cStabsPitchOnly.Value;

		public static float RollPitchLead => cRollPitchLead == null ? 0.25f : Mathf.Clamp(cRollPitchLead.Value, 0f, 0.6f);

		public static bool SlatRollReliefOn => cSlatRelief != null && cSlatRelief.Value;

		public static bool CoupGateAtLimitOn => cCoupGateLim == null || cCoupGateLim.Value;

		public static bool LoadedRollSteadyOn => cLoadedRollSteady == null || cLoadedRollSteady.Value;

		public static bool SlipYawWeightOn => cSlipYawW == null || cSlipYawW.Value;

		public static bool CapLagOn => cCapLag == null || cCapLag.Value;

		public static float FinCamberK => Mathf.Clamp((0.5f), 0f, 1f);   // Vortex 0.1.0 (Tom 16 Nov: camber coupling for the rudders): the F-22E's 0.5

		public static float WingCamberK => false ? 0f : Mathf.Clamp((0f), 0f, 1f);

		public static bool AsymSharedLatOn => cAsymShared == null || cAsymShared.Value;

		public static bool LowCapRollFix => cLowCapRoll == null || cLowCapRoll.Value;

		public static bool GRateLaw => cPitchLaw == null || cPitchLaw.Value == PitchCommandLaw.GAndPitchRate;

		public static bool AoAThenRateLaw => cPitchLaw != null && cPitchLaw.Value == PitchCommandLaw.AoAThenRateHold;

		public static bool FuelBalanceOn => cFuelBal.Value;

		public static bool FuelAttachedOnly => cFuelAttached.Value;

		public static bool FuelBalanceLog => cFuelBalLog.Value;

		public static float FuelTransferRate => Mathf.Max(0f, cFuelRate.Value);

		public static bool FuelTrim => cFuelTrim.Value;

		public static float FuelTrimRate => Mathf.Max(0f, cFuelTrimRate.Value);

		public static float FuelBalanceTolLat => Mathf.Max(0.001f, cFuelTolLat.Value);

		public static float FuelBalanceTolLong => Mathf.Max(0.001f, cFuelTolLong.Value);

		public static bool RudderPitchAid => (true);   // Vortex 0.1.0 (Tom 16 Nov: "make rudders aid in pitch"): the F-22E's symmetric rudder split

		public static float RudderToeCost => Mathf.Max(0.001f, (0.05f));

		public static float RudderToeMax => Mathf.Clamp((20f), 0f, 35f);

		public static float RudderToeGain => Mathf.Max(0f, (2f));

		public static bool GroundRateLaw => cGroundRate.Value;

		public static float GroundRotationRamp => Mathf.Max(1f, cGroundRotRamp.Value);

		public static float StabStallAoA => (25f);

		public static float InnerFlaperonStallAoA => (cStallInner != null) ? Mathf.Clamp(cStallInner.Value, 20f, 80f) : 40f;

		public static float OuterFlaperonStallAoA => (cStallOuter != null) ? Mathf.Clamp(cStallOuter.Value, 10f, 80f) : 55f;

		public static float StallStaggerLiftSlope => 0f;

		public static bool RollFightReleased => cRollFightRel == null || cRollFightRel.Value;

		// Ifrit: the F-22E's speedbrake fade (2.2.16) works on Aryx's Airbrake components; the Ifrit has none
		public static bool AirbrakeAoAFade => false;

		public static bool TeBrakeAoAFade => cTeBrakeFade == null || cTeBrakeFade.Value;

		public static float AirbrakeFadeShareFull => 0.4f;

		public static float AirbrakeFadeShareZero => 0.15f;

		public static float FinLossAoACap => false ? 40f : Mathf.Clamp((0f), 0f, 65f);

		public static bool StabParkEscape => cStabPark == null || cStabPark.Value;

		public static float SettledStallMargin => (cSettledMargin == null) ? 6f : Mathf.Clamp(cSettledMargin.Value, 0f, 20f);

		public static float SettledRateOn => (cSettledOn == null) ? 10f : Mathf.Clamp(cSettledOn.Value, 1f, 60f);

		public static float SettledRateOff => (cSettledOff == null) ? 20f : Mathf.Max(SettledRateOn + 1f, cSettledOff.Value);

		public static float SettledWalkRate => (cSettledWalk == null) ? 30f : Mathf.Clamp(cSettledWalk.Value, 1f, 100f);

		public static float SettledSlopeFrac => (cSettledSlope == null) ? 0.15f : Mathf.Clamp(cSettledSlope.Value, 0f, 1f);

		public static float SettledYawUse => (cSettledYawUse == null) ? 0.4f : Mathf.Clamp(cSettledYawUse.Value, 0f, 5f);

		public static float TvcNeutralHold => cTvcNeutral == null ? 1000f : Mathf.Max(0f, cTvcNeutral.Value);

		public static float StabCLScale => false ? 1f : Mathf.Clamp((1f), 0.5f, 2f);

		public static float StabLiftSlope => false ? -1f : Mathf.Clamp((-1f), -1f, 1f);

		public static float CgShiftAft => (0f);

		public static float CouplingFF => cCoupFF.Value;

		public static float ReleasedPitchPriority => cRelPitchPrio.Value;

		public static float RollNozzleWashoutSlew => Mathf.Max(0f, cWashSlew.Value);

		public static float ReleasedMomentLead => Mathf.Clamp(cMomLead.Value, 0f, 0.15f);

		public static float PitchOnsetGps => cOnset.Value;

		public static float StabRollCost => cStabRoll.Value;

		public static float StickSmoothing => cStickSmooth.Value;

		public static float ActuatorDamping => cActDamp.Value;

		public static float YawStopFactor => cYawStop.Value;

		public static float LowIasYawBoost => cLowYaw.Value;

		public static float LowIasYawBoostFullIas => cLowYawIas.Value;

		public static float LowIasYawBoostFalloff => cLowYawFall.Value;

		public static float TvcMoveCost => cTvcMove.Value;

		public static float RollNozzleHold => cRollTvc.Value;

		public static float RollNozzleWashout => cRollWash.Value;

		public static float RollTvcBudget => cRollTvcBudget.Value;

		public static float RollTvcBudgetCost => cRollTvcBudgetCost.Value;

		public static float RudderSideslipYawCap => (2f);

		public static bool NoseWheelFbw => cNwsFbw.Value;

		public static bool GroundStockMatch => cGndStock == null || cGndStock.Value;

		public static bool GearDownSlipProtect => cGearDownSlip == null || cGearDownSlip.Value;

		public static bool GroundIndi => cGndIndi == null || cGndIndi.Value;

		public static float GroundIndiGain => (cGndIndiK == null) ? 15f : Mathf.Max(0f, cGndIndiK.Value);

		public static float GroundPedalBrake => (cGndPedalBrake == null) ? 0.5f : Mathf.Clamp01(cGndPedalBrake.Value);

		public static bool GroundIndiLightNoseHold => cGndLightNose == null || cGndLightNose.Value;

		public static bool GroundIndiBrake => cGndIndiBrake == null || cGndIndiBrake.Value;

		public static float GroundIndiBrakeMax => (cGndIndiBrakeMax == null) ? 1f : Mathf.Clamp01(cGndIndiBrakeMax.Value);

		public static float GroundIndiBrakeFullSpeed => (cGndIndiBrakeFull == null) ? 7.7f : Mathf.Max(0f, cGndIndiBrakeFull.Value);

		public static float GroundIndiBrakeZeroSpeed => (cGndIndiBrakeZero == null) ? 12.9f : Mathf.Max(0f, cGndIndiBrakeZero.Value);

		public static float GroundIndiTau => (cGndIndiTau == null) ? 0.04f : cGndIndiTau.Value;

		public static float GroundDriftAllow => (cGndDriftAllow == null) ? 8f : cGndDriftAllow.Value;

		public static float GroundDriftGain => (cGndDriftGain == null) ? 0.2f : cGndDriftGain.Value;

		public static float GroundDriftLead => (cGndDriftLead == null) ? 0.25f : cGndDriftLead.Value;

		public static float GroundDriftFadeSpeed => (cGndDriftFade == null) ? 15f : cGndDriftFade.Value;

		public static float GroundYawRateMax => cGndYawRate.Value;

		public static float GroundLateralAccel => cGndLatAcc.Value;

		public static float GroundLineHold => cGndLine.Value;

		public static bool NozzleAnimation => (false);

		public static bool NozzleAnimationAI => (false);

		public static float AsymPitchMargin => cAsymPitchMargin.Value;

		public static bool AsymRollCoupling => cAsymRollCoupling.Value;

		public static bool RollPitchPriority => cRollPitchPriority.Value;

		public static float RollFightCost => cRollFightCost.Value;

		public static float NozzleSeamShade => (0.07f);

		public static float RollPitchFightCost => cPitchFight.Value;

		public static float RollPitchReserve => cRollPitchReserve.Value;

		public static bool NozzleIdleOpen => (true);

		public static float NozzleAreaRate => (1.2f);

		public static float YawPriority => cYawPri.Value;

		// Vortex 0.3.4: the velocity-frame residual's roll credit fades with the roll shortfall (FcsController.slipCreditK)
		public static bool SlipCreditFade => cSlipCredit == null || cSlipCredit.Value;

		public static bool SlipBandAlloc => cSlipBandOn == null || cSlipBandOn.Value;

		public static float SlipBand => (cSlipBand != null) ? Mathf.Clamp(cSlipBand.Value, 0f, 20f) : 2f;

		public static float SlipBandSpan => (cSlipBandSpan != null) ? Mathf.Clamp(cSlipBandSpan.Value, 0.5f, 20f) : 3f;

		public static float SlipBandGain => (cSlipBandGain != null) ? Mathf.Clamp(cSlipBandGain.Value, 0f, 10f) : 6f;

		public static bool AllocPairPass => cPairPass == null || cPairPass.Value;




		public static float SlipCreditRoll0 => (cSlipCreditR0 != null) ? Mathf.Max(0f, cSlipCreditR0.Value) : 40f;

		public static float SlipCreditSpan => (cSlipCreditSpan != null) ? Mathf.Max(1f, cSlipCreditSpan.Value) : 60f;

		public static float SlipCreditAoA0 => (cSlipCreditA0 != null) ? cSlipCreditA0.Value : 20f;

		public static float RollLead => cRollLead.Value;

		public static float YawRateFF => cYawFF.Value;

		public static float OuterFlapShare => cOuterFlap.Value;

		public static float StabRollShare => cStabRollShare.Value;

		public static float CoupleAoA => cCouple.Value;

		public static float PedalBeta => cPedalBeta.Value;

		public static float KBeta => cKBeta.Value;

		public static float KRoll => cKRoll.Value;

		public static float KPitch => cKPitch.Value;

		public static float KYaw => cKYaw.Value;

		public static float KpAoA => cKpAoA.Value;

		public static bool StabTvcPairedSearch => cPairTrim.Value;

		public static float LateralStabReserve => cLatRes.Value;

		public static bool StabBranchSwitch => cBranch.Value;

		public static bool OvershootPitchPriority => cOvPrio.Value;

		public static float CouplingRollLimit => cCoupRoll.Value;

		public static float StabBranchMaxAoA => cBranchMaxAoA.Value;

		public static float StabBranchTarget => cBranchTarget.Value;

		public static float StabBranchPitchTol => cBranchTol.Value;

		public static float PitchAntiWindup => Mathf.Max(0f, cAntiWind.Value);

		public static bool AoACaptureThrustBrake => cThrBrake.Value;

		public static float AoACaptureBrakeBlend => cBrakeBlend.Value;

		public static float KiAoA => cKiAoA.Value;

		// Revoker 0.1.0: the single centreline rudder keeps its yaw authority to ~75 deg AoA (bench: rudder 25 deg = -40
		// deg/s^2 of stability-axis yaw at 500 km/h from 0 to 75 deg; reversed by 80), unlike the F-22E's canted fins (50-60)
		public static float RudderFadeStart => (75f);

		public static float RudderFadeEnd => (85f);

		public static float UnloadDecel => cUnloadDecel.Value;

		public static float UnloadRiseGain => cUnloadRise.Value;

		public static float MaxRollAccel => cRollAcc.Value;

		private static ConfigEntry<float> cDecelLook;

		private static ConfigEntry<float> cDecelStart;

		private static ConfigEntry<float> cDecelFull;

		private static ConfigEntry<float> cAsymCapDemand;
		private static ConfigEntry<float> cAsymCplResMax;

		private static ConfigEntry<float> cAsymCplResMaxNt;

		private static ConfigEntry<float> cAsymCapSmooth;

		private static ConfigEntry<bool> cHiAoABrake;
		private static ConfigEntry<bool> cCmdShape;

		private static ConfigEntry<float> cShapeASpan;

		private static ConfigEntry<float> cShapeAKSmall;

		private static ConfigEntry<float> cShapeAKLarge;

		private static ConfigEntry<float> cShapeOnset;

		private static ConfigEntry<float> cShapeGSpan;

		private static ConfigEntry<float> cShapeGKSmall;

		private static ConfigEntry<float> cShapeGKLarge;

		private static ConfigEntry<bool> cRateBlend;

		private static ConfigEntry<float> cRateBlendAoA;

		private static ConfigEntry<float> cRateBlendStick;

		private static ConfigEntry<bool> cFuelTrim;

		private static ConfigEntry<float> cFuelTrimRate;

		private static ConfigEntry<bool> cTeBrake;

		private static ConfigEntry<bool> cTeBrakeFade;

		private static ConfigEntry<float> cGbElev, cGbInb, cGbCan, cAbElevUp, cAbInbDn;

		private static ConfigEntry<float> cEngOutCap;

		private static ConfigEntry<float> cEngDmgCap;

		private static ConfigEntry<float> cThrResp;

		private static ConfigEntry<bool> cEngAbGate;

		private static ConfigEntry<int> cStickRev;

		private static ConfigEntry<float> cStick24;

		private static ConfigEntry<float> cStick36;

		private static ConfigEntry<float> cRateBlendAoAMin;

		private static ConfigEntry<float> cRateBlendLiftG;

		private static ConfigEntry<float> cRateBlendBand;

		private static ConfigEntry<bool> cRateBlendNeg;

		private static ConfigEntry<float> cRateBlendAoANeg;

		private static ConfigEntry<float> cRateBlendAoAMinNeg;

		private static ConfigEntry<bool> cAuthCap;

		private static ConfigEntry<float> cAuthCapMargin;

		private static ConfigEntry<float> cAuthCapFloor;

		private static ConfigEntry<float> cHybridIndi;

		private static ConfigEntry<float> cHybridHiLead;

		private static ConfigEntry<bool> cAllocNozzleNewton;


		public static float DecelLookahead => Mathf.Max(0f, cDecelLook.Value);

		public static float DecelStart => cDecelStart.Value;

		public static float DecelFull => Mathf.Max(cDecelStart.Value + 0.5f, cDecelFull.Value);

		public static float AsymCapDemand => Mathf.Clamp(cAsymCapDemand.Value, 0.5f, 2f);
		public static float AsymCoupledReserveMax => Mathf.Clamp(cAsymCplResMax.Value, 0.2f, 0.9f);

		public static float AsymCoupledReserveMaxNoTvc => (cAsymCplResMaxNt == null) ? 0.8f : Mathf.Clamp(cAsymCplResMaxNt.Value, 0.2f, 0.9f);

		public static float AsymCapSmoothing => Mathf.Max(0.05f, cAsymCapSmooth.Value);

		public static bool HighAoABrakeFloor => cHiAoABrake.Value;

		public static bool CmdShaping => cCmdShape.Value;

		public static float ShapeAoASpan => Mathf.Max(1f, cShapeASpan.Value);

		public static float ShapeAoAGainSmall => Mathf.Max(0.3f, cShapeAKSmall.Value);

		public static float ShapeAoAGainLarge => Mathf.Max(0.3f, cShapeAKLarge.Value);

		public static float ShapeOnsetSmall => Mathf.Clamp(cShapeOnset.Value, 0f, 1f);

		public static float ShapeGSpan => Mathf.Max(0.2f, cShapeGSpan.Value);

		public static float ShapeGGainSmall => Mathf.Max(0.3f, cShapeGKSmall.Value);

		public static float ShapeGGainLarge => Mathf.Max(0.3f, cShapeGKLarge.Value);

		public static bool HighAoARateBlend => cRateBlend.Value;

		public static float RateBlendAoA => Mathf.Clamp(cRateBlendAoA.Value, 10f, 64f);

		public static float RateBlendStick => Mathf.Clamp(cRateBlendStick.Value, 0.2f, 0.9f);

		// 2.0.12 stick curve: stick position of 24 and 36 deg AoA (defaults on the 72 deg per stick line). 24 within
		// 0.333 +-0.25, 36 within 0.5 +-0.2, and 36 at least 0.1 of stick past 24 (36 is pushed out if they come closer).
		public static float EngineOutAoACap => Mathf.Clamp(cEngOutCap.Value, 10f, 65f);

		public static float EngineDamagedAoACap => (cEngDmgCap != null) ? Mathf.Clamp(cEngDmgCap.Value, 10f, 65f) : 40f;

		public static float ThrottleResponseScale => (cThrResp != null) ? Mathf.Clamp(cThrResp.Value, 0.25f, 4f) : 1f;

		public static bool EngineAbAoAGate => cEngAbGate == null || cEngAbGate.Value;

		public static float StickAoA24 => Mathf.Clamp(cStick24.Value, 0.0833f, 0.7f);

		public static float StickAoA36 => Mathf.Clamp(Mathf.Max(Mathf.Clamp(cStick36.Value, 0.3f, 0.8f), StickAoA24 + 0.1f), 0.3f, 0.95f);

		public static float RateBlendAoAMin => Mathf.Clamp(cRateBlendAoAMin.Value, 10f, 64f);

		public static float RateBlendLiftG => Mathf.Max(1.1f, cRateBlendLiftG.Value);

		public static float RateBlendBand => Mathf.Clamp(cRateBlendBand.Value, 0.5f, 15f);

		public static bool HighAoARateBlendNeg => cRateBlendNeg.Value;

		public static float RateBlendAoANeg => Mathf.Clamp(Mathf.Abs(cRateBlendAoANeg.Value), 10f, 64f);

		public static float RateBlendAoAMinNeg => Mathf.Clamp(Mathf.Abs(cRateBlendAoAMinNeg.Value), 10f, 64f);

		public static bool LowThrustAoACap => cAuthCap.Value;

		public static float AuthCapMargin => cAuthCapMargin.Value;

		public static float AuthCapFloor => Mathf.Clamp(cAuthCapFloor.Value, 5f, 65f);

		public static float HybridIndiGain => Mathf.Clamp(cHybridIndi.Value, 0f, 1.5f);

		public static float HybridIndiHighAoALead => (cHybridHiLead != null) ? Mathf.Clamp(cHybridHiLead.Value, 0f, 0.3f) : 0.15f;

		public static bool AllocNozzleNewton => cAllocNozzleNewton == null || cAllocNozzleNewton.Value;


		private static ConfigEntry<float> cYawRefKmh;

		private static ConfigEntry<float> cYawMargStart;

		private static ConfigEntry<float> cYawMargFull;

		private static ConfigEntry<float> cYawMargLow;

		public static float YawBrakeRefKmh => Mathf.Max(50f, cYawRefKmh.Value);

		public static float YawMarginStartKmh => cYawMargStart.Value;

		public static float YawMarginFullKmh => Mathf.Min(cYawMargFull.Value, cYawMargStart.Value - 1f);

		public static float YawMarginLowIas => Mathf.Max(1f, cYawMargLow.Value);

		private static ConfigEntry<float> cYawMidF;

		private static ConfigEntry<float> cYawMidLo;

		private static ConfigEntry<float> cYawMidHi;

		private static ConfigEntry<float> cYawMidRamp;

		public static float YawMidFactor => Mathf.Clamp(cYawMidF.Value, 0.2f, 2f);

		public static float YawMidLoKmh => cYawMidLo.Value;

		public static float YawMidHiKmh => Mathf.Max(cYawMidHi.Value, cYawMidLo.Value);

		public static float YawMidRampKmh => Mathf.Max(1f, cYawMidRamp.Value);

		// Ifrit 0.4.0 multiplayer: the Local* values are this machine's settings; the plain ones are what applies here, which
		// as a client is the host's policy (MpPolicy): the host's variant when it locks it, stock otherwise.
		public static bool HostAllowFcs => cHostAllowFcs == null || cHostAllowFcs.Value;

		public static bool HostLockVariant => cHostLock != null && cHostLock.Value;

		public static bool LocalStockPerformance => cProfile != null && cProfile.Value == PerformanceProfile.Stock;

		public static float LocalThrustScale => LocalStockPerformance ? 1f : cThrust.Value;

		public static float LocalFlareCountScale => LocalStockPerformance ? cFlareScaleStock.Value : cFlareScaleBuffed.Value;

		public static float LocalEWCapacityScale => LocalStockPerformance ? cEwCapStock.Value : cEwCapBuffed.Value;

		public static float LocalEWRechargeScale => LocalStockPerformance ? cEwRateStock.Value : cEwRateBuffed.Value;

		public static float LocalGunAmmoScale => LocalStockPerformance ? cGunAmmoStock.Value : cGunAmmoBuffed.Value;

		public static float LocalFuelUseScale => LocalStockPerformance ? 1f : cFuelUse.Value;

		public static float LocalDragScale => LocalStockPerformance ? 1f : cDrag.Value;

		public static float LocalDragReliefAoAHigh => LocalStockPerformance ? 0f : Mathf.Clamp(cDrAoA.Value, 0f, 0.5f);

		public static float LocalDragReliefTransonic => LocalStockPerformance ? 0f : Mathf.Clamp(cDrTrans.Value, 0f, 0.5f);

		public static bool StockPerformance => MpPolicy.StockForced || (MpPolicy.VariantLocked ? MpPolicy.Host.stock : LocalStockPerformance);

		public static float ThrustScale => MpPolicy.VariantLocked ? MpPolicy.Host.thrust : (MpPolicy.StockForced ? 1f : LocalThrustScale);

		public static float FlareCountScale => MpPolicy.VariantLocked ? MpPolicy.Host.flare : (MpPolicy.StockForced ? 1f : LocalFlareCountScale);

		public static float EWCapacityScale => MpPolicy.VariantLocked ? MpPolicy.Host.ewCap : (MpPolicy.StockForced ? 1f : LocalEWCapacityScale);

		public static float EWRechargeScale => MpPolicy.VariantLocked ? MpPolicy.Host.ewRate : (MpPolicy.StockForced ? 1f : LocalEWRechargeScale);

		public static float GunAmmoScale => MpPolicy.VariantLocked ? MpPolicy.Host.gun : (MpPolicy.StockForced ? 1f : LocalGunAmmoScale);

		public static float FuelUseScale => MpPolicy.VariantLocked ? MpPolicy.Host.fuel : (MpPolicy.StockForced ? 1f : LocalFuelUseScale);

		public static float DragScale => MpPolicy.VariantLocked ? MpPolicy.Host.drag : (MpPolicy.StockForced ? 1f : LocalDragScale);

		// 2.2.3 (rev 2): AoA drag relief as a curve over each part's local AoA. Up to DragReliefFadeStart it is DragReliefAoA
		// (LowAoADragRelief, both profiles); across the fade band it goes over to DragReliefAoAHigh: none in Stock (post-stall
		// drag is Aryx's), the old flat AoADragRelief in Buffed.
		// Vortex 0.1.0: the low-AoA relief is part of the Buffed set only (on with the post-stall relief); Stock keeps the game's polar
		public static float DragReliefAoA => (MpPolicy.FcsAllowed && DragReliefAoAHigh > 0f) ? Mathf.Clamp(cStockDrAoA.Value, 0f, 0.5f) : 0f;

		public static float DragReliefAoAHigh => MpPolicy.VariantLocked ? MpPolicy.Host.drAoAHigh : (MpPolicy.StockForced ? 0f : LocalDragReliefAoAHigh);

		public static float DragReliefFadeStart => Mathf.Clamp(cStockDrFade0.Value, 5f, 60f);

		public static float DragReliefFadeEnd => Mathf.Clamp(Mathf.Max(cStockDrFade1.Value, cStockDrFade0.Value + 1f), 6f, 70f);

		public static float HighAoALatMargin => (cLatMargin != null) ? Mathf.Clamp(cLatMargin.Value, 0.5f, 1.5f) : 0.86f;

		public static float HighAoALatMarginStart => (cLatMargin0 != null) ? Mathf.Clamp(cLatMargin0.Value, 0f, 89f) : 25f;

		public static float HighAoALatMarginFull => (cLatMargin1 != null) ? Mathf.Clamp(cLatMargin1.Value, HighAoALatMarginStart + 0.5f, 90f) : 30f;

		private static ConfigEntry<float> cLatMargin, cLatMargin0, cLatMargin1;

		public static bool DeceleronSplitBudget => cSplitBudget == null || cSplitBudget.Value;

		private static ConfigEntry<bool> cSplitBudget;

		public static float AoADragBump => (cDragBump != null) ? Mathf.Clamp(cDragBump.Value, 0f, 1f) : 0.3f;

		public static float AoADragBumpStart => (cDragBump0 != null) ? Mathf.Clamp(cDragBump0.Value, 0f, 89f) : 18f;

		public static float AoADragBumpPeak => (cDragBump1 != null) ? Mathf.Clamp(cDragBump1.Value, AoADragBumpStart, 89f) : 24f;

		public static float AoADragBumpEnd => (cDragBump2 != null) ? Mathf.Clamp(cDragBump2.Value, AoADragBumpPeak + 1f, 90f) : 35f;

		private static ConfigEntry<float> cDragBump, cDragBump0, cDragBump1, cDragBump2;

		public static float DragReliefTransonic => MpPolicy.VariantLocked ? MpPolicy.Host.drTrans : (MpPolicy.StockForced ? 0f : LocalDragReliefTransonic);


		public static float KpG => cKpG.Value;

		public static float KiG => cKiG.Value;

		public static float FlaperonEffectiveness => (1f);

		public static float StabilatorEffectiveness => (1f);

		public static float ActuatorScale => cActuator.Value;

		public static bool AsymLimiter => cAsymOn.Value;

		public static bool AsymRollHeadroom => cAsymRollHead.Value;

		public static float AsymReserve => cAsymReserve.Value;

		public static float AsymCapFloor => Mathf.Abs(cAsymFloor.Value);

		public static float AsymCapFloorNeg => Mathf.Abs(cAsymFloorNeg.Value);

		public static float AsymTighten => Mathf.Max(0.1f, cAsymTighten.Value);

		public static float AsymRelax => Mathf.Max(0.1f, cAsymRelax.Value);

		public static float AsymRecoveryRate => Mathf.Max(1f, cAsymRec.Value);

		public static float AsymMinIas => cAsymMinIas.Value;

		public static float AsymRollReserveAccel => Mathf.Max(0f, cAsymRollAcc.Value);

		public static float AsymYawReserveAccel => Mathf.Max(0f, cAsymYawAcc.Value);

		public static float AsymAxisBoost => cAsymBoost.Value;

		public static float AsymDeadband => cAsymDead.Value;

		public static float AsymFeedForward => cAsymFF.Value;

		public static float AsymFFWashout => cAsymFFTau.Value;

		public static float AsymFFMaxBeta => cAsymFFBeta.Value;

		public static float AsymFFMaxAoA => cAsymFFAoA.Value;



		public static float LowQRegRelief => cLowQReg.Value;

		public static float RollStopFactor => cRollStop.Value;

		public static float RollAccelUse => Mathf.Clamp(cRollAccUse.Value, 0.1f, 0.95f);

		public static float RollAccelUseLowIas => Mathf.Clamp(cRollAccUseLow.Value, 0.1f, 0.95f);

		// Revoker 0.2.8 (TrailingEdgeBrakeConfig): the F-22E's brake surface configurations, in the air and on the runway.
		// Off with ThrottleIndependentAirbrake off (the airbrake is then fully stock).
		public static bool TeBrakeConfig => cTeBrake == null || cTeBrake.Value;

		public static float GroundAeroBrake => Mathf.Clamp01((1f));

		public static float GroundBrakeMinSpeed => (0f);

		public static float GroundBrakeFlap => (cGbElev != null) ? Mathf.Clamp(cGbElev.Value, 0f, 30f) : 30f;

		public static float GroundBrakeInboard => (cGbInb != null) ? Mathf.Clamp(cGbInb.Value, 0f, 25f) : 25f;

		public static float GroundBrakeStab => (cGbCan != null) ? Mathf.Clamp(cGbCan.Value, 0f, 80f) : 80f;

		public static float GroundBrakeRudder => (25f);

		public static float HighLiftMaxAoA => cHlMaxAoA.Value;

		public static float SlatAoAStart => cSlatStart.Value;

		public static float SlatAoAFull => cSlatFull.Value;

		public static float HighLiftHighAoA => Mathf.Clamp01(cHlHighAoA.Value);

		public static float GroundBrakeTvc => (10f);   // Vortex 0.1.0: the F-22E ground aero-brake (nozzle trailing edge up)

		public static float AirBrakeElevonUp => (cAbElevUp != null) ? Mathf.Clamp(cAbElevUp.Value, 0f, 30f) : 20f;

		public static float AirBrakeInboardDown => (cAbInbDn != null) ? Mathf.Clamp(cAbInbDn.Value, 0f, 25f) : 25f;

		// Vortex 0.2.10: the rudders' split (deg each, trailing edges outboard) in the in-flight set, as the F-22E's speedbrake shows on its rudders
		public static float AirBrakeRudderFlare => (cAbRudToe != null) ? Mathf.Clamp(cAbRudToe.Value, 0f, 30f) : 25f;

		private static ConfigEntry<float> cAbRudToe;

		// Vortex 0.4.1: the brake's rudder flare held inside the rudders' attached window (FcsController.BrakeFlareCapStep)
		public static bool AirbrakeLiftCompensation => cAbLiftFF == null || cAbLiftFF.Value;

		public static bool AirbrakeRudderFlareWindow => cAbRudWin == null || cAbRudWin.Value;

		private static ConfigEntry<bool> cAbRudWin;



		private static ConfigEntry<bool> cAbLiftFF;
		public static float GroundBrakeThrottle => (-1f);

		public static bool GroundBrakeLog => (false);

		public static bool AirbrakeSplit => cGndBrakeSplit.Value;

		public static string VentDoors => ("Bypass");

		public static float RotationMinSpeed => Mathf.Max(20f, cRotMinSpeed.Value);

		public static bool RevFlowRollSense => cRevRoll.Value;
		public static bool RevFlowNoseAxis => cRevNose == null || cRevNose.Value;

		public static float AsymRollTau => cAsymRollTau.Value;

		public static float AsymRelaxBoost => cAsymRelaxBoost.Value;

		// shared asymmetric-load code (Su-30 0.5.1 / 0.5.2: AsymStopTime, AsymAoAWeight)
		public static float AsymStopTime => Mathf.Clamp(cAsymStopTime.Value, 0f, 5f);

		// shared asymmetric-load code (AsymShared.cs): per-jet settings this jet has no config entry for
		public static bool EngineOutZeroThrust => false;

		public static bool AsymCoupledCheck => true;

		public static float AsymAoAWeight => Mathf.Clamp(cAsymAoAWeight.Value, 0.25f, 4f);

		// shared asymmetric-load code: the canards are a pitch pair only (the Su-30's setting); no canards on this jet
		public static bool CanardSymmetric => false;

		public static float RollPitchFF => cRollPitchFF.Value;

		public static float RollPitchFFAoA => cRollPitchFFAoA.Value;

		public static float GUndershootCapture => cGUnder.Value;

		public static float RollPitchFFMax => Mathf.Max(1f, cFFMax.Value);

		public static float RollPitchFFStabCost => cFFStabCost.Value;

		public static void Init(ConfigFile cfg, ConfigFile dev, bool devUnlocked, bool devFresh, ManualLogSource log)
		{
			Log = log;
			cProfile = cfg.Bind("1. General", "PerformanceProfile", PerformanceProfile.Stock, "Stock (default) = the game's FS-20 Vortex thrust, parasitic drag and fuel burn. Buffed = the F-22E FCS's buffed profile without its transonic / supersonic drag relief, thrust raised: thrust x1.12 (dry and afterburner), parasitic drag x0.885, the high-AoA drag relief (AoADragRelief 0.25, LowAoADragRelief 0.35), fuel burn x0.75 at every throttle setting (25 % less). Countermeasures and gun ammo come from the matching Countermeasures section below. The flight control is the same in both. Switch before spawning: thrust, drag, countermeasures and ammo are set when a jet spawns and stay until it respawns; fuel burn follows the setting live.");
			cThrIndepBrake = cfg.Bind("1. General", "ThrottleIndependentAirbrake", defaultValue: true, "What opens the Vortex's airbrake. true = the brake key / axis only, in the air and on the ground; idle or zero throttle never opens it. false = 0.2.6: the stock rule only: throttle at zero opens it; the brake key is the wheel brake only (it no longer opens it, so Brake and Countermeasures can share a controller button). Only while the FCS flies the jet.");
			cTeBrake = cfg.Bind("1. General", "TrailingEdgeBrakeConfig", defaultValue: true, "the brake also sets the control surfaces as the F-22E does. In the air, with the airbrake out (Vortex 0.2.10): the rudders split trailing edges outboard (AirbrakeRudderFlare, 25 deg) and the wing trailing-edge pair goes trailing edge up by the share that balances the rudders' pitch, in over 1 s and closed with AoA (15 -> 35 deg); yaw and roll stay on the differentials; off in the STOVL hover schedule. On the runway (main wheels down, brake held, stick not pulled back, from touchdown on): the elevons trailing edge up (inboard GroundBrakeInboardElevonTEUp, outboard GroundBrakeOutboardElevon) to dump the wing lift, and the nozzle trailing edge up (GroundBrakeTvc) for nose-wheel load. Needs ThrottleIndependentAirbrake = true (with it false the airbrake is fully stock). false = the dedicated airbrake only (0.2.7). Takes effect at once.");
			cEnabled = cfg.Bind("1. General", "Enabled", defaultValue: true, "Replace the stock fly-by-wire with the Vortex FCS (player aircraft only).");
			cHostAllowFcs = cfg.Bind("8. Multiplayer (host)", "HostAllowFcs", defaultValue: true, "Read on the HOST (or dedicated server). true = players who run this plugin may fly their Vortexes with its control laws on this server. false = every client flies the stock fly-by-wire. A client whose host does not run this plugin always flies stock (fly-by-wire, thrust, drag, fuel burn, countermeasures and ammo), whatever its own settings. The host's own jet is bound by this switch too. While this or HostLockVariant is on, the server is flagged as modded (server browser / NOMM 'Modded' tag).");
			// Ifrit 0.4.7: HostLockVariant on by default. A player file written by an older build still says "# Default value:
			// false" above the key; if it also still holds false (the old default), it is moved to true once. The file is then
			// saved, which rewrites the default line, so a later false set by the player stays.
			bool hostLockOldFile = HostLockOldDefault(cfg.ConfigFilePath);
			cHostLock = cfg.Bind("8. Multiplayer (host)", "HostLockVariant", defaultValue: true, "Read on the HOST. On by default. true = every client gets the host's variant: the host's PerformanceProfile with its thrust, drag and fuel-burn scaling and the host's countermeasure and gun-ammo capacities from the matching Countermeasures section. false = every client flies the Stock variant with stock countermeasures and ammo (their own profile and capacity settings are ignored while connected); the host flies Stock on its own server as well. Changes are sent to the clients at once and apply at the next spawn.");
			if (hostLockOldFile)
			{
				if (!cHostLock.Value)
				{
					cHostLock.Value = true;
				}
				cfg.Save();
				Log.LogInfo("Ifrit 0.4.7 defaults: HostLockVariant on.");
			}
			cRateOnly = cfg.Bind("1. General", "PitchRateCommandOnly", defaultValue: false, "2.0.6: true = the pitch stick is a pitch-rate command in every mode (gear up or down, on the runway, Stability Assist on or off), like the gear-down law. With Stability Assist on, the AoA and G limits and AoA protection still apply; with it off (MPO) the MPO limits apply as before. false = AoA/G command with the gear up (default). Takes effect at once.");
			cStovlThrDetentHold = cfg.Bind("1. General", "StovlThrottleDetentHoldMs", 200f, "Vortex 0.1.2: STOVL mode only, keyboard / button throttle: the throttle stops at the zero-ground-speed point (the aft detent, 25 %) the way the Nuclear Option Detents mod stops it at idle and afterburner; a key that runs into it stops there; keep holding it this long (ms), or let go and press again for this long, to go through. 0 = no hold (the FCS still treats 23-27 % as the detent). Analog throttle axes are not held (none of this applies to them). Works alongside the Detents mod; if that mod's own FS-20 profile is enabled with dry detents, this one stands aside. Takes effect at once.");
			cRelGuard = cfg.Bind("1. General", "ReleaseGuard", defaultValue: true, "Vortex 0.2.8: STOVL release guard. While the STOVL laws fly (in the air, not MPO) a weapon whose release would leave more sideways weight imbalance than the jet can trim in roll (75 % of its roll authority at that moment) is not released: a short warning, then let the trigger go and hold it ReleaseGuardHold seconds to drop it anyway. Once per weapon selection. Jettison is never blocked. The same check blocks a STOVL conversion when the jet is already that lopsided (warning names the heavy side; 0.2.11: no override, drop or jettison stores to convert). false = weapons release as stock and no conversion check.");
			cRelGuardHold = cfg.Bind("1. General", "ReleaseGuardHold", 1f, new ConfigDescription("STOVL release guard: how long (s) the trigger is held, after letting it go, to override a block (the first store comes off then). Vortex 0.3.7: 1 (Tom, 17 Nov; 1.5 before, moved once).", new AcceptableValueRange<float>(0.3f, 5f)));
			cRelGuardHold2 = cfg.Bind("1. General", "ReleaseGuardSecondHold", 1.7f, new ConfigDescription("Vortex 0.3.7: STOVL release guard: with the trigger still held after an override, the next store comes off this long (s) after the press (0.3.6: 0.7 s after the first, i.e. 2.2 s with the 1.5 s hold). Not less than ReleaseGuardHold + 0.1.", new AcceptableValueRange<float>(0.3f, 10f)));
			cRelGuardCd = cfg.Bind("1. General", "ReleaseGuardCooldown", 10f, new ConfigDescription("STOVL release guard: with no shot of the overridden weapon for this long (s) the guard re-arms. 0 = never (only cycling weapons or a situation change re-arms it).", new AcceptableValueRange<float>(0f, 120f)));
			cCautionBox = cfg.Bind("1. General", "CautionBox", defaultValue: true, "Vortex 0.5.16 (Tom, 19 Nov): the FCS's own caution box, directly under the game's message line at the top of the HUD and looking like it (a copy of the game's box). A caution stays up for as long as its condition lasts and clears at once when it ends; the game's own line drops every message after a few seconds. First caution: LOW HVR T/W: STO/RVL ONLY (too heavy to hover: short take-off or rolling landing only; see StovlMinSpeedFloor in the dev file). false = no box; a caution then shows once, as an ordinary message on the game's line.");
			cCautionColor = cfg.Bind("1. General", "CautionColor", CautionColor.Hud, "Vortex 0.5.16: the caution box's text colour. Hud (default; Tom, 19 Nov: \"green then\") = the colour of the game's own message line, which follows the HUD colour setting, as everything on the real jet's helmet display is one colour. Amber = the head-down convention for a caution.");
			cStovlThrDetentInd = cfg.Bind("1. General", "StovlThrottleDetentIndicator", defaultValue: true, "Vortex 0.1.2: show STOP HOLD n% / STOP LOCK under the HUD throttle gauge for the STOVL zero-ground-speed detent (styled as the Detents mod's indicator).");
			cPitchLaw = cfg.Bind("1. General", "PitchCommandLaw", PitchCommandLaw.GAndPitchRate, "2.1.0: the gear-up pitch law (Stability Assist on). GAndPitchRate (default) = the stick commands pitch rate where full G is out of reach (low IAS) and G where it is in reach, blended as before; no AoA command - the AoA limits, the low-thrust / asymmetric-load caps and AoA protection still apply as limits. GAoAAndPitchRate = the 1.9-2.0 law: AoA command at low IAS (stick curve in section 5), G command at high IAS, pitch-rate command past the hand-over AoA. AoAThenRateHold (2.1.8) = as GAoAAndPitchRate, but once the stick is steady and the AoA has settled on its command, the pitch rate the jet has then is held instead of the AoA (the AoA drifts with speed); moving the stick goes back to AoA command. PitchRateCommandOnly = true overrides all of them. Takes effect at once.");
			cFuelBal = dev.Bind("6. Fuel", "FuelBalancing", defaultValue: true, "2.0.6: move fuel between the healthy tanks to hold the centre of mass where the clean jet has it (centreline; stock fore/aft position without stores), against store asymmetry, a part shot off or a leaking tank. Only between attached, non-leaking, non-burning tanks; mass is conserved. All Ifrits simulated on this machine.");
			cEngOutCap = dev.Bind("Asymmetric load", "EngineOutAoACap", 22f, "Ifrit 0.4.0: 22 (was 30; the Ifrit's engines sit 1.19 m off the centreline, one at AB is 24 deg/s^2 of yaw against 10-24 from the tails at 200-300 km/h and 20 deg AoA; bench one-engine pulls from 300 km/h: 18-25 hold without the throttle cut, 30 bleeds to 50-85 m/s and departs). 2.0.13: with an engine out the AoA limit is this, both signs, steady (plus whatever an aerodynamic asymmetry from damage needs on top). The dead engine's thrust yaw is left to the VMC protection (throttle, last resort) instead of the AoA cap, which made the limit follow speed and throttle (12 <-> 65 deg within seconds in 2.0.12).");
			cEngDmgCap = dev.Bind("Asymmetric load", "EngineDamagedAoACap", 40f, "Ifrit 0.4.2: 40 (first 0.4.1 / 2.4.1 cut: 30, the engine-out cap; a running engine's split is a third to a half of a dead one's, and below the speed where the fins could not hold it the thrust match now takes over on prediction, so the cap only says where the full split is traded for matched thrust). Ifrit 0.4.1: with one engine running weak (damaged, not destroyed: under 85 % of the other's thrust, released above 92 %, 1 s filter) the AoA limit is this fixed value, both signs, instead of the dynamic thrust cap (which fell to 12 deg within 5 s in the 22 Oct 22:08 log and then followed IAS). The dead-engine case keeps EngineOutAoACap. The game caps a damaged engine at 50-100 % of its RPM range by its condition (no afterburner below condition ~0.6), so the weak engine makes 35-75 % of the good one's thrust at AB. Bench, full pulls from 300-400 km/h with the left engine at condition 0 / 0.5: 26-30 hold with 5-8 deg peak sideslip, 36 saturates the yaw once the speed is gone (throttle cut a third of the time, one departure), 45 departs.");
			cThrResp = dev.Bind("5. Airframe", "ThrottleResponseScale", 1f, "Ifrit 0.4.1: throttle response (Ifrit 0.4.5: back to the game's own, 1) for every variant (Stock included): the engines' spool rate (RPM per second toward the throttle's target) and the afterburner's light-up / fade speed are multiplied by this at the airframe setup. 1 = the mod's own values.");
			cEngAbGate = dev.Bind("Asymmetric load", "EngineAbAoAGate", true, "Ifrit 0.4.1: thrust-match limiter. With one engine down on thrust (predicted from the engines' damage state: an engine's reachable RPM is minRPM + (maxRPM - minRPM) x (0.5 x condition + 0.5), the dry thrust follows the RPM and the afterburner needs 85 % of maxRPM, i.e. condition above 0.55) and the AoA above EngineDamagedAoACap, the throttle input is held where the two engines still make the same thrust, so there is no thrust split and no AoA cap for it: you keep the full AoA, and the thrust comes back once you bring the AoA down to the cap yourself (on above cap + 2, off under cap + 0.5). Below the cap the throttle is free; the thrust split then brings the fixed EngineDamagedAoACap. With an engine OUT there is nothing to match: above EngineOutAoACap the throttle is held at military power (the afterburner's start). The VMC throttle protection (measured sideslip run-away) stays as the last resort. false = 0.4.0 (dynamic cap from the thrust split, nose pushed down).");
			cFuelTrim = dev.Bind("6. Fuel", "FuelTrim", defaultValue: true, "2.0.12: when the yaw or roll authority runs short (an engine out, a fin or wing section shot off), the balancing moves the CG sideways with fuel instead of holding it on the centreline: toward the live engine to shorten its thrust lever arm, or against an aerodynamic asymmetry. Only once the yaw or roll usage passes 0.75, only as far as evens out the roll and yaw usage (a sideways CG costs roll: ~15 deg/s^2 per g per 10 cm), at most 30 cm; back to the centreline once both are under 0.5 for 2 s. false = centreline always (2.0.6-2.0.11).");
			cFuelTrimRate = dev.Bind("6. Fuel", "FuelTrimRate", 240f, "2.0.12: fuel moved per second (kg/s, in total) while the fuel trim works (normal balancing: FuelTransferRate). Ifrit 0.4.1: 240 (was 120).");
			cFuelRate = dev.Bind("6. Fuel", "FuelTransferRate", 80f, "2.0.6: total fuel the balancing moves per second (kg/s). Ifrit 0.4.1: 80 (was 40).");
			cFuelTolLat = dev.Bind("6. Fuel", "FuelBalanceLateralTolerance", 0.01f, "2.0.6: lateral CG error (m) the balancing weighs as one unit; it leaves the fuel alone inside half of it.");
			cFuelTolLong = dev.Bind("6. Fuel", "FuelBalanceLongitudinalTolerance", 0.03f, "2.0.6: fore/aft CG error (m) the balancing weighs as one unit; it leaves the fuel alone inside half of it.");
			cFuelAttached = dev.Bind("6. Fuel", "FuelFromAttachedTanksOnly", defaultValue: true, "2.0.6: engines draw fuel only from tanks still attached, and the fuel gauge / fuel checks count only those (against the full original capacity); a tank on a part that has been shot off is not refuelled. false = stock (a shot-off tank's fuel keeps feeding the engines and showing on the gauge).");
			cFuelBalLog = dev.Bind("0. Debug", "FuelBalanceLog", defaultValue: false, "2.0.6: log the fuel balancing's plan (CG error and the fuel it moves) when it starts and every 10 s while it works.");
			cFlareScaleStock = cfg.Bind("3. Countermeasures - Stock profile", "FlareCountScale", 1f, "Flare capacity multiplier (the load at spawn and what rearming refills to). Used when PerformanceProfile = Stock. 1 = stock. All Vortexes, applied at spawn.");
			cGunAmmoStock = cfg.Bind("3. Countermeasures - Stock profile", "GunAmmoScale", 1f, "Gun ammunition multiplier: magazine capacity, so the load at spawn and what rearming refills to. Used when PerformanceProfile = Stock. 1 = stock. All Vortexes, applied at spawn.");
			cEwCapStock = cfg.Bind("3. Countermeasures - Stock profile", "EWCapacityScale", 1f, "Electrical storage multiplier for the EW/jammer power supply (more jamming time per charge). Used when PerformanceProfile = Stock. 1 = stock. All Vortexes, applied at spawn.");
			cEwRateStock = cfg.Bind("3. Countermeasures - Stock profile", "EWRechargeScale", 1f, "Recharge-rate multiplier for the EW/jammer power supply (engine-driven charging). Used when PerformanceProfile = Stock. 1 = stock. All Vortexes, applied at spawn.");
			cFlareScaleBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "FlareCountScale", 10f, "Flare capacity multiplier (the load at spawn and what rearming refills to). Used when PerformanceProfile = Buffed. 1 = stock. All Vortexes, applied at spawn.");
			cGunAmmoBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "GunAmmoScale", 2f, "Gun ammunition multiplier: magazine capacity, so the load at spawn and what rearming refills to. Used when PerformanceProfile = Buffed. 1 = stock. All Vortexes, applied at spawn.");
			cEwCapBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "EWCapacityScale", 1.5f, "Electrical storage multiplier for the EW/jammer power supply (more jamming time per charge). Used when PerformanceProfile = Buffed. 1 = stock. All Vortexes, applied at spawn.");
			cEwRateBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "EWRechargeScale", 1.2f, "Recharge-rate multiplier for the EW/jammer power supply (engine-driven charging). Used when PerformanceProfile = Buffed. 1 = stock. All Vortexes, applied at spawn.");
			cTelemetry = dev.Bind("0. Debug", "Telemetry", defaultValue: false, "Write a 50 Hz CSV of the FCS state to BepInEx/Ifrit_FCS_<time>.csv. 1.1.0: switching it off in flight stops the rows (the file stays open) and switching it on resumes them, so its cost can be timed in flight.");
			cAIFcs = cfg.Bind("2. AI", "AIFlightControl", defaultValue: false, "Off by default (every AI Vortex in a mission would otherwise fly this FCS). true = AI-flown Vortexes simulated on this machine (no human pilot) get the full FCS with a pitch-rate command law (the AI's stick is a pitch-rate command; AoA/G protection, coordination and the allocator as for a player). Takes effect at the next spawn. Experimental.");
			cAILog = dev.Bind("0. Debug", "AIFlightLog", defaultValue: false, "Every 20 s, log one summary line per AI F-22E the FCS is flying (mode, IAS, AoA and g range, time in AoA protection), so AI jets can be checked from BepInEx/LogOutput.log after any mission. Each F-22E also logs once which control path it takes when it first reaches the flight-control hook.");
			cEstThreaded = dev.Bind("0. Debug", "EstimatorThreaded", defaultValue: true, "1.0.0: run the capacity estimator's allocator probes on a worker thread instead of the game's main thread (the laws already read the previous tick's estimate, so the flying is identical). false = on the main thread. Takes effect at once.");
			cTickTimer = dev.Bind("0. Debug", "TickTimerLog", defaultValue: true, "1.0.0: every 10 s, log the FCS's main-thread time per physics tick to BepInEx/LogOutput.log (player jet: mean / median / p95 / max and the split by phase; AI jets together; the airframe force hooks). Drives nothing.");
			cParTables = dev.Bind("0. Debug", "ParallelTables", defaultValue: true, "1.0.0: the surfaces' moment tables (rebuilt every physics tick) are computed on several cores: the game's objects are read on the main thread, the table maths runs on the shared thread pool. Identical results either way. false = one thread. Takes effect at once.");
			cParAlloc = dev.Bind("0. Debug", "ParallelAllocator", defaultValue: true, "1.0.0: when the allocator's two-horizon pick is active, its third solve (the reach plan) runs on a second core while the main thread does the other two. Identical results either way. false = one thread. Takes effect at once.");
			cParSweep = dev.Bind("0. Debug", "ParallelSweep", defaultValue: true, "1.0.0: the asymmetric-load sweep (an AoA sweep of the whole airframe, a few points per physics tick) reads the parts' state once and computes its points on several cores. Identical results either way. false = one thread. Takes effect at once.");
			cSlowTick = dev.Bind("0. Debug", "SlowTickLogUs", 1500f, "1.1.0 (diagnostic, from the F-22E 4.1.3): log any of the player's FCS ticks slower than this (us) on its own line, with the tick's 13 segments, whether a garbage collection ran during it and the managed heap's growth over it (at most 10 lines per 10 s). 0 = off. Drives nothing. Takes effect at once.");
			cNativeAlloc = dev.Bind("0. Debug", "NativeAllocator", 2, new ConfigDescription("1.1.0: the FCS's native core, Vortex_FcsCore.dll, carried inside this plugin's DLL (1.1.1) (Rust, ported from the F-22E FCS 4.1.0): the control allocator's solves (the main ones each tick and the capacity estimator's probes), and with NativeSweep / NativeTables the asymmetric-load sweep's inner loop and the surface tables' maths. 2 = use it in the game: several times faster, the same laws, the bench's float arithmetic instead of Mono's (Unity runs Mono with floats carried in double, so the C# code differs from any compiled code in the last digits there, and the allocator's picks turn some of those into a different, mostly equal-cost answer for a tick). 1 = use it only where it returns exactly what the C# code returns, bit for bit (the bench; in the game that means C#). 0 = never: the C# code, as 1.0.1. With the DLL missing or failing its checks the C# code runs. Takes effect at once; the tick timer's log line reports what is in use.", new AcceptableValueList<int>(0, 1, 2)));
			cNativeAllocCheck = dev.Bind("0. Debug", "NativeAllocatorCheck", 0, new ConfigDescription("1.1.0: when the native allocator is in use, how it is compared with the C# one while flying. 0 = not at all: every solve is the native one. 1 = each allocator's first 64 solves and every 256th after them are solved both ways, and the C# result is the one used on those. 2 = every solve both ways, the C# result used on all of them (a comparison flight: the jet flies C# and pays for both). The tick timer's log line reports the counts: identical, close (commands within 0.001 deg), an equal-cost alternative (costs within 0.1 %), different. Where float arithmetic is strict, anything but identical is a fault: one warning in BepInEx/LogOutput.log and the native solve is switched off until the game restarts. Takes effect at once.", new AcceptableValueList<int>(0, 1, 2)));
			cSweepGrid = dev.Bind("0. Debug", "SweepGridCache", defaultValue: true, "1.1.0 (F-22E 4.0.1): the asymmetric-load sweep evaluates every grid deflection of every surface in each AoA point's airflow. On: what depends on the deflection alone (the surface's rotation at that deflection, its span axis and moment arm, and the same for the section it cambers) is worked out once per sweep tick instead of once per point. Same maths, same order; byte-identical on the bench. Off = the 1.0.1 loop. Takes effect at once.");
			cNativeSweep = dev.Bind("0. Debug", "NativeSweep", defaultValue: true, "1.1.0 (F-22E 4.1.0): with the native core in use (NativeAllocator) and SweepGridCache on, the asymmetric-load sweep's capacity loop runs in Vortex_FcsCore.dll: the rotations are still worked out here, the flow's part (one atan2, the airfoil table, the force and its moment, the min / max kept) there. Byte-identical on the bench. Off = the C# loop. Takes effect at once.");
			cNativeCpl = dev.Bind("0. Debug", "NativeCoupledCap", defaultValue: true, "1.3.0 (from the Ternion 0.3.0): with the native core in use (NativeAllocator), the asymmetric-load check's coupled solves (the LP that finds the roll left each way with pitch held) run in the native core. Byte-identical on the bench. Off = the C# solver. Takes effect at once.");
			cNativeTables = dev.Bind("0. Debug", "NativeTables", defaultValue: true, "1.1.0 (F-22E 4.1.0): with the native core in use (NativeAllocator), the surfaces' moment tables (rebuilt every physics tick) get their per-deflection maths from Vortex_FcsCore.dll: rotations here, the flow's part there; the live point and the table's post-processing stay here. Byte-identical on the bench. Off = the C# loop. Takes effect at once.");
			cParEnv = dev.Bind("0. Debug", "ParallelEnvelope", defaultValue: true, "1.0.0: the lift envelope (25 AoA points over every airframe part) and the hybrid pitch slopes, refreshed every 0.1 s, read the parts' state once and compute their points on several cores. Identical results either way. false = one thread. Takes effect at once.");
			cAIDirectRoll = cfg.Bind("2. AI", "AIDirectRoll", defaultValue: true, "AI Vortexes only: the AI's roll stick drives the velocity-vector roll rate almost directly (up to the jet's roll authority, still capped by the roll limits).");
			cAIGunRollFade = cfg.Bind("2. AI", "AIGunRollFade", defaultValue: true, "AI Vortexes only: while the AI points its nose (guns) at a moving target, its roll stick is faded out when the aim error is small, so it does not wobble the gun solution.");
			cAoAPos = dev.Bind("2. Envelope", "AoALimitPositive", 65f, "AoA limit (deg) in AoA/G command and gear-down rate command. Vortex 0.3.15: 65 (Tom, 17 Nov 20:17; a file still holding 60 is moved once). Vortex 0.3.6: 60 (Tom, 17 Nov; moved once). Vortex 0.1.0: 80, the highest usable on the bench (FS-20, full aft 27 s from 200-500 km/h with full left and right roll at the limit, then full push): 70 / 75 / 80 / 85 hold the limit in the rolls with under 1 deg of sideslip; the sideslip peak over the top of the slow-speed loop that follows is 6 / 10 / 12 / 17 deg; recovery to 10 deg in about 2 s at every limit. 85 goes past 15 deg of sideslip, so 80. Stock FS-20 limiter: 25.");
			cAoANeg = dev.Bind("2. Envelope", "AoALimitNegative", 55f, "Negative AoA limit magnitude (deg). Vortex 0.3.6: 55 (Tom, 17 Nov; moved once). Vortex 0.1.0: 80, the highest usable on the bench (full push 27 s from 300 km/h with full roll both ways): -60 / -70 / -80 hold with at most 2 deg of sideslip and 25-30 deg/s velocity-vector roll, back to -10 deg in 1.4-1.8 s; at -90 the roll drops to 9 deg/s. Faster than about 400 km/h the negative load limit governs first.");
			cStovlEnabled = dev.Bind("9. STOVL", "StovlEnabled", true, "Vortex 0.1.0: the F-35B STOVL mode (hold Flight Assist to convert / reconvert; the unified STOVL control laws). false = the game's own swivel logic (Custom Axis 1 / auto modes), with the FCS flying the surfaces only.");
			cStovlConversionMaxIas = dev.Bind("9. STOVL", "StovlConversionMaxIas", 460f, "Highest IAS (km/h) at which the STOVL conversion is accepted (F-35B: 250 kt = 463 km/h; 0.1.8: 460, was 470). The STOVL laws then hold the jet under ~460 km/h themselves (the game swings the nozzle aft above 500 km/h).");
			cStovlReconversionMinIas = dev.Bind("9. STOVL", "StovlReconversionMinIas", 240f, "Lowest IAS (km/h) at which the reconversion to CTOL is accepted (the wing must carry the jet; stall ~220 km/h).");
			cStovlCenterDetent = dev.Bind("9. STOVL", "StovlCenterDetent", 0.5f, "Throttle position of the centre detent: holds the speed in the accelerating regime (F-35B left inceptor). Forward of it = acceleration, aft = deceleration, full travel = the most the jet has.");
			cStovlAoALim = cfg.Bind("1. General", "StovlAoALimit", 24f, "Vortex 0.3.5: AoA limit (deg, each way) from a STOVL conversion request until the jet is back in CTOL; a conversion is not started above it. 13:29 log: with the doors open the jet bled speed twice as fast as in CTOL and departed at 50 deg AoA, 63 m/s. 0 = the CTOL limits.");
			cShipRel = cfg.Bind("1. General", "StovlShipRelative", true, "Vortex 0.4.6 (Tom, 17 Nov 23:26): STOVL ground speeds read relative to the nearest friendly ship within StovlShipRelativeRange, as the game's auto-hover does (its speed match, AutoHover.CheckNearbyShip: within 500 m, all at once, a 3 s old copy). Covers the lever's ground-speed command, the hover regime hand-overs and the released-stick position hold. Blended, never stepped: the ship's speed is read every tick, faded in over the outer quarter of the range, and the reference moves to a new value (entering or leaving the range, a change of nearest ship, the switch) by at most its own size in 3 s. true (default since 0.4.6; 0.4.1-0.4.5 defaulted to false, below vanilla) = on; false = over the ground at every distance. Toggle in flight with StovlShipRelativeKey. Takes effect at once. Vortex 0.4.11: this is the setting every match starts with; StovlShipRelativeKey switches it for the current match only and no longer changes this value.");
			// (0.4.11: a change of the setting itself, in the file or a configuration manager, takes effect at once over the key's)
			cShipRel.SettingChanged += delegate { shipRelOverride = -1; };
			cShipRelRange = cfg.Bind("1. General", "StovlShipRelativeRange", 800f, "Vortex 0.4.1: distance (m) from the nearest friendly ship inside which StovlShipRelative takes the ship's speed as the zero (full inside 3/4 of it, faded out to zero at it).");
			cShipRelKey = cfg.Bind("1. General", "StovlShipRelativeKey", KeyboardShortcut.Empty, "Vortex 0.4.1: key that switches StovlShipRelative on and off in flight (a HUD message says which). Unbound by default; set it here or in a configuration manager, e.g. RightBracket or LeftControl + S.");
			cPirouetteKey = cfg.Bind("1. General", "StovlPedalPirouetteKey", KeyboardShortcut.Empty, "Vortex 0.5.8 (Tom, 18 Nov): key that switches the STOVL pedal pirouette (StovlPedalPirouette, off by default) the other way: with the setting off, the key turns the pirouette on. Tap = latch on / off; hold (0.35 s or longer) = the other way only while held. Unassigned by default; back to the setting at load and at every new mission. A HUD message says which (STOVL PEDAL: PIROUETTE / COORDINATED). Pirouette: below the stick and pedal hand-over, while the pedal is held the nose turns at the yaw axis's full rate and the jet keeps flying the way it was going, with whatever skid it had when the pedal came in (bench, 13.8 t, full pedal at 45 kt: 23-27 deg/s against 8.4 coordinated); let go, and the skid is taken out along the nose at once while the yaw stops. Coordinated: the FCS takes the skid out and the nose turns as fast as the sideways budget can bring the flight path round.");
			bool genRevOld = !FileHasKey(cfg.ConfigFilePath, "VortexGeneralRevision");
			cRegimeShift = cfg.Bind("1. General", "StovlRegimeShift", 30f, "Vortex 0.5.6 (Tom, 18 Nov: the CTOL-like control and the lever's acceleration command 10 kt later): default 20 -> 30, so the lever's ground-speed command hands over to the speed command at 55-65 kt, the stick and pedals' hover laws at 60-70, the released-stick side-velocity hold at 45-60 and the CTOL blend at 70-90 kt airspeed (a file still holding 20 is moved once). Vortex 0.4.1 (Tom, 17 Nov 20:58: the lever went to ACCEL after ~30 kt, too slow to catch a ship): how many knots later the STOVL hover hand-overs come, all together: the lever's ground-speed command to speed command (25-35 kt in 0.4.0, now 45-55), the stick and pedals' hover laws (30-40 -> 50-60 kt), the released-stick side-velocity hold (15-30 -> 35-50 kt) and the CTOL blend (40-60 -> 60-80 kt airspeed). 0 = 0.4.0. Allowed 0-40.");
			cVortexGenRev = cfg.Bind("1. General", "VortexGeneralRevision", 2, "Internal: the Vortex player-config default revision this file was last updated to. Leave it alone.");
			if (cVortexGenRev.Value < 1)
			{
				genRevOld = true;
			}
			if (genRevOld && cVortexGenRev.Value <= 1)
			{
				// 0.5.6: StovlRegimeShift default 20 -> 30; a file written before (no VortexGeneralRevision line) still holding 20 is moved once
				if (Mathf.Abs(cRegimeShift.Value - 20f) < 0.01f)
				{
					cRegimeShift.Value = 30f;
					Log?.LogInfo("Vortex FCS: StovlRegimeShift 20 -> 30 (0.5.6 default; set it back in the config if you want 20).");
				}
				cVortexGenRev.Value = 1;
			}
			cStovlVanes = cfg.Bind("1. General", "StovlVanes", true, "Vortex 0.5.0 (Tom, 18 Nov): a vane box under the lift fan, as the F-35B's (VAVBN): the FCS turns the fan's thrust fore and aft (StovlVaneMin 10 deg forward .. StovlVaneMax 42 deg aft of the fan's axis, dev cfg), so the jet accelerates forward from the hover at a level attitude with the fan pushing too. The game's lift fan has a fixed thrust line 10 deg forward; that is the vanes' forward stop, where they rest whenever the STOVL laws are not flying. Only while the FCS flies the jet in STOVL mode. false = 0.4.11 (the fan's thrust line fixed). Takes effect at once.");
			cStovlVaneModel = cfg.Bind("1. General", "StovlVaneModel", true, "Vortex 0.5.0: draw the vane box under the lift fan: a frame round the duct's exit with a thin centre keel and eight pairs of vanes that turn with the fan's thrust line (0.5.1: redrawn; 0.5.0's stood inside the duct). Render only (no collision, no weight); drawn with the fan doors open, in the airframe's own level-of-detail group (near level only). Drawn on every FS-20 this PC shows; on other players' jets the vanes stand at their rest angle. false = not drawn (the vanes still work if StovlVanes is on).");
			cStovlAftDetent = dev.Bind("9. STOVL", "StovlAftDetent", 0.20f, "Throttle position of the aft detent: zero ground speed in the hover regime (below ~25-35 kt); at speed, a deceleration; in the CTOL blend (above ~60 kt, lever = thrust) idle, with idle .. full dry from the detent forward (0.3.3). Forward of it, ground speed up to StovlCenterGroundSpeed at the centre detent; behind it, backwards up to StovlBackGroundSpeed at the stop. 0.2.1: 0.20 (0.2.0: 0.10, 0.1.x: 0.25; a file still holding 0.25 or 0.10 is moved once).");
			cStovlBackGs = dev.Bind("9. STOVL", "StovlBackGroundSpeed", 30f, "Vortex 0.2.1: backward ground speed (kt) with the lever at the aft stop in the hover regime (scaled over the travel behind the aft detent).");
			cStovlDetentWidth = dev.Bind("9. STOVL", "StovlDetentWidth", 0.02f, "Half width of each detent (throttle fraction): the dead band around the aft (zero ground speed) and centre (hold speed) detents. Vortex 0.1.5: 0.02 (was 0.04; a file still holding 0.04 is moved once).");
			cStovlCenterGroundSpeed = dev.Bind("9. STOVL", "StovlCenterGroundSpeed", 30f, "Ground speed (kt) the throttle at the centre detent asks for in the hover regime (and aft at full aft throttle).");
			cStovlAccelMax = dev.Bind("9. STOVL", "StovlAccelMax", 6f, "Longitudinal acceleration (m/s^2) at full forward throttle in the accelerating regime (more than the jet has = maximum performance).");
			cStovlDecelMax = dev.Bind("9. STOVL", "StovlDecelMax", 4f, "Longitudinal deceleration (m/s^2) at full aft throttle in the accelerating regime.");
			cStovlClimbRate = dev.Bind("9. STOVL", "StovlClimbRate", 6f, "Vertical speed (m/s) at full aft stick in the hover regime (height-rate command).");
			cStovlSinkRate = dev.Bind("9. STOVL", "StovlSinkRate", 5f, "Vertical speed (m/s) at full forward stick in the hover regime.");
			cStovlPathRate = dev.Bind("9. STOVL", "StovlPathRate", 10f, "Flight-path-angle rate (deg/s) at full stick above ~40 kt ground speed (released stick holds the climb / dive angle).");
			cStovlHoverAttitude = dev.Bind("9. STOVL", "StovlHoverAttitude", 3f, "Pitch attitude (deg) the FCS holds in the hover (the pilot does not fly pitch in STOVL mode).");
			cStovlWingAoAMax = dev.Bind("9. STOVL", "StovlWingAoAMax", 12f, "Highest AoA (deg) the FCS uses to let the wing carry the jet in semi-jet-borne flight; the propulsive lift takes the rest.");
			cStovlTrcSpeed = dev.Bind("9. STOVL", "StovlTrcSpeed", 15f, "Lateral ground speed (kt) at full lateral stick in the hover (translational rate command); released = hold the spot.");
			cStovlBankMax = dev.Bind("9. STOVL", "StovlBankMax", 45f, "Bank angle (deg) at full lateral stick from ~30 kt to ~100 kt (bank-angle command; released = wings level).");
			cStovlTrcBankRate = dev.Bind("9. STOVL", "StovlTrcBankRate", 10f, "Vortex 0.1.6: fastest change (deg/s) of the bank the hover's translational rate command asks for, so the thrust keeps up with the lift the bank tilts away (the height stays put through a translation or a skid stop).");
			cStovlRollRateMax = dev.Bind("9. STOVL", "StovlRollRateMax", 90f, "Roll rate (deg/s) at full lateral stick above ~130 kt (released = bank hold).");
			cStovlFreePitch = dev.Bind("9. STOVL", "StovlFreestylePitchRate", 30f, "Vortex 0.2.2: pitch rate (deg/s) at full stick in STOVL freestyle (STOVL on, flight assist off); roll uses StovlRollRateMax and the pedals StovlHoverYawRate.");
			cStovlHoverYawRate = dev.Bind("9. STOVL", "StovlHoverYawRate", 30f, "Vortex 0.1.5: 30 (0.1.1-0.1.4: 45; a file still holding 45 is moved once). Yaw rate (deg/s) at full pedal in the hover (released = heading hold). Vortex 0.1.1: 30 -> 45 (0.1.0 felt slow in flight); a file still holding 30 is moved once.");
			cStovlPedalSideslip = dev.Bind("9. STOVL", "StovlPedalSideslip", 10f, "Sideslip (deg) at full pedal above ~40 kt ground speed.");
			cStovlGroundSwivel = dev.Bind("9. STOVL", "StovlGroundSwivel", 0.95f, "Nozzle swivel (0 aft .. 1 down) held on the wheels in STOVL mode with the engine at idle (ready for a vertical take-off).");
			cStovlNoAutoRot = dev.Bind("9. STOVL", "StovlGroundNoAutoRotate", true, "Vortex 0.3.7 (Tom, 14:54 log, 39-41 s: short take-off, the nose came up at ~47 kt with the stick centred): with a wheel touching in STOVL mode and the stick centred or forward, the CTOL blend (from 40 kt) asks for no pitch rate: its pitch law is held at its start and the AoA walk to the STOVL reference is off; its 1 g / AoA hold flew the nose up against the propulsion carrying 90 % of the weight (qCmd 0 -> 1.2 deg/s from 39.25 s with no stick). Pulling the stick (over 0.05) starts it as on a mode change and the pilot rotates. false = 0.3.6.");
			cStovlTrcLag = dev.Bind("9. STOVL", "StovlTrcLagMargin", 0.32f, "Vortex 0.3.9 (Tom, 17 Nov: start and braking margin for sideways skid 20 % less): the bank loop's lag (s) the hover's side-velocity profile leaves room for when it starts and stops a sideways translation (acceleration a with a^2 / (2 j) + a x this = speed error; linear end 1 / (2 x this)). 0.1.x-0.3.8: 0.4.");
			cStovlYawStop = dev.Bind("9. STOVL", "StovlYawStopTime", 1.25f, "Vortex 0.3.9 / 0.3.10 / 0.3.11 (Tom, 17 Nov 16:28: 1.25; Tom, 17 Nov: no fixed yaw-rate limit in STOVL; the real F-35B yaws 90 deg in 6-7 s in the hover at low fuel, UK and Italian demos): full pedal asks for the yaw rate the yaw axis can start (and stop) within this time (s) at the authority it has now, so the limit follows mass, fuel, stores and thrust. 1.09: at 20 % fuel (9 640 kg on the bench, 15.3 deg/s^2 of hover yaw) a held pedal and release turns 94 deg in 6.55 s at 17 deg/s; full fuel 101 deg, 18 deg/s. 0.3.9: 1.84 (30 deg/s with full fuel). 0 = the fixed StovlHoverYawRate (0.3.8).");
			cStovlYawMean = dev.Bind("9. STOVL", "StovlYawRateMeanAuthority", true, "Vortex 0.5.18 (Tom, 20 Nov: \"the control surfaces wobbles when i do turning in stovl\"): the pedal's full yaw rate (yaw authority x StovlYawStopTime) uses the yaw authority's mean over StovlYawStopTime instead of this tick's. The authority estimate moves with what the roll axis does with the same surfaces and posts, and in a pedal turn the turn's bank target follows the full-pedal rate, so the bank moved its own target: Tom's 00:49 log, 48 kt, 12.55 t, full pedal: 1.2-1.5 Hz of roll for 25 s, the roll posts end to end, the surfaces +-21 deg. A stop sized for StovlYawStopTime spends that long on the authority, so the mean over that time is the one that counts. The yaw loop's stops still use the authority now. false = 0.5.17.");
			cStovlTdAcc = dev.Bind("9. STOVL", "StovlTouchdownAccel", true, "Vortex 0.5.19 (Tom, 20 Nov: \"It worked but not 100%, sometimes it will still bounce, check it if it is the fcs or the physical limit\"): the touchdown braking envelope counts the descent's downward acceleration. Pushed down from a climb the jet reaches the envelope still accelerating downward, and the thrust has to take that out before it can brake; the envelope took the descent as steady, so the braking started late and the jet touched faster than the gear takes (his 01:19 log: the three bounces entered at -1.4 to -3.5 m/s^2 and touched at 2.2-2.7 m/s for 1.9; the steady entries touched at 1.45-1.57). With StovlSmoothTouchdown only. false = 0.5.18.");
			cStovlTiltStop = dev.Bind("9. STOVL", "StovlTiltStopTime", 0.42f, "Vortex 0.4.0 (Tom, 17 Nov: fixed schedules replaced with authority-based logic): the hover's translation tilts (the bank for sideways, the pitch tilt for fore-aft) move at StovlTrcBankRate at most, and no faster than their axis can start and stop within this time (s) at its angular-acceleration budget now, so a jet short of roll or pitch authority (light on thrust, a one-sided load, damage) tilts slower. 0.42: a clean jet with full fuel is at the 10 deg/s ceiling on both axes; at 20 % fuel the bank moves at about 8.5 deg/s. 0 = StovlTrcBankRate alone.");
			cStovlTrcStop = dev.Bind("9. STOVL", "StovlTrcStopTime", 0.75f, "Vortex 0.5.10 (Tom, 18 Nov: faster sideways start and stop for less side speed): default 1.06 -> 0.75, so full side stick asks for 0.75 / 1.06 = 71 % of the speed (Buffed clean full fuel at sea level 15 -> about 10.6 kt; a file still holding 1.06 is moved once). Vortex 0.4.0 (Tom, 17 Nov): full lateral stick in the hover asks for the ground speed the jet can stop from within this time (s) at its own stopping acceleration (what the full-dry lift leaves sideways once the weight is carried; no more than StovlBankMax gives), so the speed follows mass, fuel, stores, thrust and altitude. Set so the Buffed profile, clean with full fuel at sea level, gets 15 kt (0.1.0-0.3.15's fixed speed); the Stock profile gets about 9 kt there, 5.5 kt at 12.8 t; Buffed at 20 % fuel about 20 kt. 0 = the fixed StovlTrcSpeed.");
			cStovlPhysBlends = dev.Bind("9. STOVL", "StovlPhysicsBlends", true, "Vortex 0.4.0 (Tom, 17 Nov): STOVL fades on physical shares instead of airspeed bands. The pitch attitude's hand-over from the hover attitude to the wing goes on the wing's share of the weight (15-45 m/s on a clean jet with full fuel at sea level, as before; higher for a heavier jet or thinner air); the velocity-vector roll and the sideslip loop come in on the same ratio (airspeed over the speed at which the wing alone carries the jet; 10-25 m/s on that jet, as before). false = 0.3.13's airspeed bands.");
			cStovlFitWeights = dev.Bind("9. STOVL", "StovlFitWeights", true, "Vortex 0.4.0 (Tom, 17 Nov: 15 : 1): in the hover the thrust fit weighs the vertical force against the fore-aft one by what each can tolerate: the spare lift against the sideways / fore-aft acceleration the hover has (the pitch tilt makes up a fore-aft miss). 15 : 1 on a clean Stock jet with full fuel, 7 : 1 Buffed, 30 : 1 at 12.8 t, never under 4 : 1; 4 : 1 on the wheels and in the lever's accelerating regime. false = the fixed 4 : 1 (0.1.0-0.3.13).");
			cStovlClimbLead = dev.Bind("9. STOVL", "StovlClimbGainFromLead", true, "Vortex 0.4.0: the gains for a commanded climb or sink and its stop use the measured thrust lead (as the height hold's own), not the 0.35 s of 0.2.1-0.3.15. false = 0.35 s.");
			cStovlPosCatch = dev.Bind("9. STOVL", "StovlPosCatchSpeed", 0.5f, "Vortex 0.4.0: ground speed (m/s) under which the hover's position hold takes a spot after a translation (lever in the aft detent, lateral stick released); once it has one it keeps it up to 3 m/s, as before. 0.1.0-0.3.15 took it at 3 m/s: after a short side step the jet stopped past the spot it had picked and slid back (bench: 0.7 to 5 m). 3 = 0.3.15.");
			cStovlPosKeep = dev.Bind("9. STOVL", "StovlPosKeepSpeed", 0f, "Vortex 0.4.2 (Tom, 17 Nov: the 3 m/s gust that ended 5 m off its spot): ground speed (m/s) above which the hover's position hold lets go of the spot it holds. 0 = never: with the lever in the aft detent and the lateral stick released the jet comes back to its spot whatever pushed it off (the way back is flown at 2 m/s at most, as before); the spot is given up only by a lever or lateral-stick command, or on the wheels. 3 = 0.1.0-0.4.0.");
			cStovlLagLoop = dev.Bind("9. STOVL", "StovlTrcLagFromLoop", true, "Vortex 0.4.2 (Tom, 17 Nov: the sideways stop that ran 0.24 m/s past zero): the side-velocity stop profile's allowance for the bank's lag is never less than the bank loop's own lag behind a moving target, 1 / its gain (0.4 s at 2.5 / s). StovlTrcLagMargin (0.32 since 0.3.9) is under that, so the profile planned on a bank that came out sooner than it does and the jet was still braking as it passed zero. Bench, Buffed 11.9 t, 15 kt stop: 0.24 -> 0.14 m/s past zero, settled in 4.1 s (5.1), the stop to 0.3 m/s 0.12 s longer. false = StovlTrcLagMargin alone (0.3.9-0.4.0).");
			cStovlBankFF = dev.Bind("9. STOVL", "StovlBankGainFromMargin", false, "Vortex 0.4.2, off by default: the other way to make the profile's margin and the bank loop agree: the hover's translation bank loop runs at 1 / StovlTrcLagMargin (3.1 / s at 0.32 s) instead of 2.5 / s. Bench, Buffed 11.9 t, 15 kt stop: 0.11 m/s past zero and the stop 0.09 s quicker than StovlTrcLagFromLoop gives, but the roll posts sit at their stop 0.72 s (0.22 s) and the heading moves +-1.1 deg (0.1), 4.5 deg on a short side step (1.8): the loop asks for more roll acceleration than the budget has at every corner of the bank target.");
			cStovlTiltLift = dev.Bind("9. STOVL", "StovlTiltLiftLimit", true, "Vortex 0.4.2 (Tom, 17 Nov: the height lost in a fore-aft stop on a jet with little spare lift): the fore-aft tilt's rate counts only the pitch authority the vertical axis can spare. In the hover the pitch is the lift fan's, so stopping a pitch rate takes lift away (a fixed amount per deg/s, however it is done); the fan authority counted is the lift step the thrust can make up within StovlTiltLiftDip of height (at the spare lift over the engine's lag), times StovlTiltStopTime. Buffed with full fuel keeps 10 deg/s; the Stock jet and heavy jets tilt slower. false = 0.4.0 (the whole pitch authority).");
			cStovlTiltDip = dev.Bind("9. STOVL", "StovlTiltLiftDip", 0.2f, "Vortex 0.4.2: the height (m) a fore-aft tilt's pitch-rate change may cost by the thrust-recovery model (StovlTiltLiftLimit): the lift step d the thrust makes up at r = spare lift / 1 s costs (2/3) d^3 / r^2. Set so the Buffed profile, clean with full fuel, keeps the 10 deg/s tilt (as 0.4.0; its fore-aft stop moves the height -0.08 / +0.16 m on the bench). Smaller = slower tilt and less height moved on every jet (0.05: Buffed full fuel 6.5 deg/s, -0.05 / +0.11 m, stop 0.35 s longer).");
			cStovlTiltRollFull = dev.Bind("9. STOVL", "StovlTiltRollFull", true, "Vortex 0.4.2 (Tom, 17 Nov: the one-sided jet's slower bank): with a one-sided load the sideways tilt's rate comes from sqrt(the weak side's stopping power x the roll authority over the effectors' whole travel) x StovlTiltStopTime. 0.4.0 used the authority around the posts' trim, which the load had already cut, and counted the load twice (500 kg on a wing: 3.5 deg/s; this gives 4.0; 0.3.15 had 4.9). The same on a balanced jet. false = 0.4.0.");
			cStovlOriginFix = dev.Bind("9. STOVL", "StovlOriginShiftFix", true, "Vortex 0.4.7 (Tom, 17 Nov 22:54 log: the jet slid off sideways at 2.8 m/s right after a take-off from a moving ship): the game moves its whole world back to the middle now and then (FloatingOrigin, once the player is more than about 1 km from it), and every position jumps by that much in one tick. The hover's held spot and held height are positions, so they are moved by the same jump (any change of position in one tick that the jet's own speed does not explain, 8 m or more). Before, the held spot stayed behind, the hold read it as a position error of hundreds of metres and flew toward it at its 2 m/s limit on both axes until a stick input dropped it. false = 0.4.6.");
			cStovlFwdCap = dev.Bind("9. STOVL", "StovlFwdCapFromNozzle", true, "Vortex 0.4.7 (Tom, 17 Nov 22:56 log: with the stick held to the side the jet sat level for 2 s while it accelerated forward): the forward part of the hover's translation budget is what the nozzle can give with the lift still carried, taken from the nozzle's own force table (the lift fan's force stays vertical, so turning the nozzle aft gives less forward force than tilting the whole lift vector would). 0.2.6-0.4.6 budgeted a forward acceleration as if the whole lift vector tilted, asked for more than the jet has, the thrust sat at its limit and the tilt governor, which meters every bank increase by the thrust still in hand, held the bank at zero. With this the forward and sideways demands share what there really is, so a side command gets its share during a forward acceleration. The cost: the forward acceleration from the hover now stays inside the same 95 % thrust and thrust-lag reserve as the sideways one (0.4.6 ran it at about 97 % thrust, which is what shut the governor): bench, hover to 30 kt, 4.0 -> 3.7 m/s^2 Buffed 11.9 t, 3.2 -> 2.4 at 13.6 t, 2.7 -> 2.2 Stock, 1.8 -> 1.25 Stock 12.8 t; no change with 20 % fuel. false = 0.4.6.");
			cStovlGovCredit = dev.Bind("9. STOVL", "StovlGovTiltCredit", true, "Vortex 0.4.7 (same log, 33 s: side stick during a fore-aft stop, the bank waited 0.4 s at level): the tilt governor counts the lift a tilt gives back while it comes out (the pitch tilt levelling after a stop, a bank rolling out) as budget for the other tilt coming in. Before, only spare thrust counted, so with the thrust at its limit a new tilt waited until the thrust had come back down. false = 0.4.6.");
			cStovlVaneMin = dev.Bind("9. STOVL", "StovlVaneMin", -10f, "Vortex 0.5.0: the lift-fan vanes' forward limit: the fan's thrust line (deg from the fan's axis, negative = the exhaust turned forward, which pushes the jet back). -10 (Tom, 18 Nov) is the game's own fixed thrust line, so the hover trim and a stop are as in 0.4.11. Allowed -30..0.");
			cStovlVaneMax = dev.Bind("9. STOVL", "StovlVaneMax", 42f, "Vortex 0.5.0: the lift-fan vanes' aft limit (deg from the fan's axis, the exhaust turned aft, which pushes the jet forward). 42 (Tom, 18 Nov; the F-35B's vane box vectors through 54 deg in all). Allowed 1..60.");
			cStovlVaneRate = dev.Bind("9. STOVL", "StovlVaneRate", 30f, "Vortex 0.5.0: how fast the vanes turn the thrust line (deg/s). The F-35B's go through their 54 deg in under 2 s.");
			cStovlVaneTurning = dev.Bind("9. STOVL", "StovlVaneTurning", 0.9f, "Vortex 0.5.0: flow deviation: the exhaust turns this share of the vanes' own angle, so the drawn vanes stand at the thrust line's angle divided by this (10 / 42 deg of thrust line = 11 / 47 deg of vane). Looks only; the limits above are the thrust line's.");
			cStovlVaneLoss = dev.Bind("9. STOVL", "StovlVaneLoss", 0.05f, "Vortex 0.5.0: the share of the fan's thrust the vane cascade loses at StovlVaneMax (growing with the square of the aft angle; none forward of the fan's axis, where the game's own fixed thrust line has none). The fan is driven harder to make it up, which the game counts as engine thrust (its parasitic loss, 25 kN with the nozzle down): that term is raised by 1 / (1 - loss)^1.5 (a fan's thrust goes with its power to the 2/3), +8 % = 2 kN at the aft limit. An estimate: no figure for the real vane box was found. 0 = no loss.");
			cStovlVaneStopLag = dev.Bind("9. STOVL", "StovlVaneStopLag", 0.25f, "Vortex 0.5.0: the lag the lever's speed profile (acceleration to a speed, and the stop) plans its end on while the vanes fly: they take out at once what the attitude still brakes or pushes as the speed arrives, so the end no longer waits for the pitch loop's 0.5 s. Bench, stop from 30 kt at 13.8 t: 7.17 s (0.4.11) -> 6.53 s to 0.3 m/s, 0.01 m/s past zero (0.07), height band unchanged; hover to 30 kt 4.62 -> 4.35 s. 0.5 = 0.4.11's profile (with the vanes on that stops 0.3 s later than 0.4.11, 1.7 s later on Stock 12.8 t). Allowed 0.1..0.5.");
			cStovlVaneStopJerk = dev.Bind("9. STOVL", "StovlVaneStopJerk", false, "Vortex 0.5.0: the speed profile also takes its acceleration off as fast as the vanes turn the fan's force (the fan's share of the lift x g x StovlVaneRate) where that is faster than the pitch tilt coming off. Bench, 13.8 t: stop from 30 kt 6.53 -> 5.90 s, from 45 kt 7.95 -> 7.33 s; Stock 12.8 t from 14 kt 5.68 -> 4.60 s. The price: the nose is higher when the jet stops and comes level faster after it, and the height sags more there (13.8 t: 6.5 cm instead of 4 cm; Buffed 11.9 t: 12 cm instead of 9 cm; 0.4.11: 3 and 8 cm). Off by default for that.");
			cStovlTurnCoord = dev.Bind("9. STOVL", "StovlTurnCoordinate", true, "Vortex 0.4.7 (same log, 70-80 s: left pedal at 45 kt, the skid grew to 13 m/s whatever the stick did): in the hover laws the sideways and fore-aft acceleration demands carry the turn itself (forward speed x heading rate sideways, side speed x heading rate fore-aft), so the bank comes in with the yaw and the velocity turns with the nose instead of a skid having to build up first for the side-velocity loop to see. false = 0.4.6.");
			cStovlYawSideFF = dev.Bind("9. STOVL", "StovlYawSideFF", true, "Vortex 0.5.12 (Tom, 19 Nov: the yaw's start and stop, and every change the FCS makes to the yaw rate, leave a side skid): the nozzle's yaw bearing pushes the whole jet sideways while it accelerates the yaw (side acceleration = yaw acceleration x yaw inertia / (mass x the nozzle's arm), worked out from the nozzle's geometry and thrust). true = one force balance for both axes: the bank is asked for the push that cancels it, and the pedal's yaw rate changes only as fast as the bank the jet actually has cancels the nozzle's push. No gains of its own: it uses the bank profile's tilt rate and lag margin. false = 0.5.10: the side-speed loop banks once the skid is there.");
			cStovlClimbShare = dev.Bind("9. STOVL", "StovlClimbShare", true, "Vortex 0.5.14 (Tom, 19 Nov: a side stick, a lever or a pedal turn at speed asked just after a climb waited ~3 s for the climb to let go of the thrust): a stick climb's acceleration and the translation's ask share the thrust budget by one factor, as fore-aft and sideways already do, so the tilt comes in while the jet goes on climbing more slowly. Stopping a sink and holding a height are not shared. false = 0.5.13 (the climb takes the thrust first).");
			cStovlLiftCorrect = dev.Bind("9. STOVL", "StovlLiftCorrect", true, "Vortex 0.5.16 (Tom, 19 Nov, on the side speed after a 30 kt pedal turn with a climb: \"is it a physical trade off or control laws\"): the conversions between a tilt and the acceleration it gives use the lift the jet carries (g + the vertical acceleration) instead of the weight. With the lift above the weight (a climb accelerating, a sink being stopped) the bank and the fore-aft tilt are planned on the lift the height loop is asking for. With it below the weight (a climb being stopped, a descent starting: deep, but over in ~1.5 s, before a bank could be added for it) the bank target stays and the nose's turn rate is cut to the lift's share (measured); with the pedal let go the rate only falls. No new gain; with no vertical acceleration nothing changes. Bench, 13.8 t, 30 kt pedal turn with a climb: side speed at the pivot 0.31 / 0.82 -> 0.06 / 0.26 m/s (climb accelerating / after the release). false = 0.5.15 (the weight).");
			cStovlWingLift = dev.Bind("9. STOVL", "StovlWingLiftBudget", true, "Vortex 0.5.16 (Tom, 19 Nov: \"also the wing lift term, would it be beneficial to that?\"): the STOVL translation budget counts the lift the wing carries. It took the weight as the thrust's to carry at every speed; now the measured aerodynamic lift (the same measurement the transition makes: mass x (acceleration - gravity) - the propulsive force, vertical part, 0.3 s low-pass, never below zero and never more than the wing at StovlWingAoAMax could make at this airspeed) comes off it. Level flight at speed has more thrust to share (bench, 13.8 t: 45 kt pedal turn 9.1 -> 10.7 deg/s, full lever from the hover to 100 km/h 6.2 -> 5.8 s, stops from ~65 kt harder with the nose up to 5 deg higher). A climbing turn gains almost nothing: in a climb the wing's AoA is negative. In the hover the wing carries nothing and nothing changes. false = 0.5.15.");
			cStovlMinSpeed = dev.Bind("9. STOVL", "StovlMinSpeedFloor", true, "Vortex 0.5.16 (Tom, 19 Nov): too heavy to hover by the laws' own count (95 % of full dry thrust, nozzle at its hover trim, the lift fan balancing it, over the weight: hover T/W < 1.00, latched until it is back over 1.02), STOVL mode stays available but, in the air, the lever's deceleration stops at the lowest airspeed at which the wing carries what the thrust cannot, with 2 % of the weight kept in hand (the margin the latch lets go at, so the floor is zero when the caution clears), and holds it: a short take-off or a rolling landing. The caution LOW HVR T/W: STO/RVL ONLY shows while it is latched, from the conversion request on (CautionBox, player cfg), and the throttle gauge reads MIN nn KT while the floor holds the lever. Bench, Buffed: 16.5 t held at 39 kt, 17.5 t at 56 kt. false = 0.5.15 (the lever slows the jet as far as it goes: 17.5 t sank at 0.1 m/s from 15 kt; 16.5 t still hovered with no thrust in hand).");
			cStovlManTilt = dev.Bind("9. STOVL", "StovlManeuverTilt", 20f, "Vortex 0.5.17 (Tom, 20 Nov, on 0.5.16's speed floor: \"towards the end of the log i can barely maneuver\"; \"I want the min speed to leave useful maneuvering authority\"): the manoeuvring margin the STOVL laws keep, as the tilt (deg) the lift can be given while it still holds the height. Every STOVL manoeuvre (side stick, the lever's starts and stops, the pedal's coordinated turn at speed) is the lift tilted, so this is how much of all of them there is: 20 deg = 0.36 g of sideways or fore-aft acceleration, before the thrust-lag reserve. Too heavy to hover (caution LOW HVR T/W: STO/RVL ONLY) when the hover cannot give this tilt (hover T/W x cos(tilt) under 1.00, cleared over 1.02), and the speed floor (StovlMinSpeedFloor) is the lowest airspeed at which the wing takes enough of the weight that it can. 0 = 0.5.16 (the hover just holding its height: no tilt left at the floor). 0.5.20 (StovlLowMarginHover, on): short of this tilt the jet still hovers, with the caution LOW HVR T/W: REDUCED MNVR; the speed floor only comes on when the hover cannot hold its height at all, and this tilt then sizes it. Takes effect at once.");
			cStovlLowMarg = dev.Bind("9. STOVL", "StovlLowMarginHover", true, "Vortex 0.5.20 (Tom, 20 Nov, 15.15 t: \"i cant go vertical?\"; his choice: hover allowed, with a warning): the speed floor (StovlMinSpeedFloor) comes on only when the hover cannot hold its height with the floor's own 2 % climb reserve (hover T/W under 1.02 on the laws' scale, off over 1.04; 0.5.20: 1.00 / 1.02), and on the wheels a vertical take-off's climb is worked out with the nozzle at its hover trim (0.5.21), and is then sized so the jet has StovlManeuverTilt of margin at it. Where the hover holds but not with that tilt, the jet hovers and goes vertical with the caution LOW HVR T/W: REDUCED MNVR. false = 0.5.17-0.5.19 (the floor on whenever the margin is short: his 15.15 t jet, which holds height up to 18.4 deg of tilt, was sent to the 34 kt floor with the lever at the zero-speed detent).");
			cSlipSignSlow = dev.Bind("9. STOVL", "SlipSignLatchSlow", true, "Vortex 0.5.22 (Tom, 20 Nov, 021138 log: full pedal at 78 kt after a vertical take-off, the jet went 45 deg sideways and fell below the speed floor): the one-time check of which way the airframe's yaw moment restores the sideslip (the sign the directional-stiffness probe needs) is made at the probe's own airspeed (over 30 m/s, AoA under 10 deg) instead of over 60 m/s. A flight that never passed 60 m/s had no stiffness all flight (read as 0 = the nozzle beats the weathervane easily), so the STOVL pedal stayed the hover yaw-rate command at any speed with the sideslip loop off. false = 0.5.21.");
			cStovlStiffQ = dev.Bind("9. STOVL", "StovlStiffDynScale", true, "Vortex 0.5.22: the STOVL laws read the directional stiffness scaled by the dynamic pressure since it was last probed (the probe runs over 30 m/s only; under that the last value was held, so slowing from a fast flight left the weathervane read as strong as at the probe's speed). false = 0.5.21.");
			cStovlCtolNose = dev.Bind("9. STOVL", "StovlCtolNoseHold", true, "Vortex 0.5.22 (Tom, 20 Nov, 015017 log: \"i release pitch input instead of holding zero pitch rate with in the aoa limit, the nose drifted down\"): in the STOVL CTOL blend the nose is no longer walked to the fixed 10 deg wing AoA; the pitch rate is the CTOL law's plus the rate that keeps the pitch attitude in a banked turn (yaw rate x tan(bank)), and the propulsion's path follows the same rate. Released stick holds the attitude; the AoA stays where it is (the wing's share still brings it back by the path, the nose stays). 015017, 34 deg bank: the nose fell 4 deg/s (1.2 from the walk, 2.7 from the bank geometry). false = 0.5.21.");
			cStovlLowTwPed = dev.Bind("9. STOVL", "StovlLowTwPedal", true, "Vortex 0.5.22 (Tom, 20 Nov: \"for the pirouette logic, block it when the jet cant hover at zero airspeed\"): where the hover cannot hold its height (the speed floor's own test, hover T/W under 1.02 on the laws' scale) the pedal pirouette does not start, and at speed the pedal is the sideslip command with the turn coordinated, never the hover's uncoordinated yaw-rate command. 021138 log (15.9 t, 78 kt): full pedal with a pull took the sideslip to -45 deg, the yaw ran on after the pedal was let go and the jet slid sideways below the floor. false = 0.5.21.");
			cStovlSharedBud = dev.Bind("9. STOVL", "StovlSharedBudget", true, "Vortex 0.5.25 (Tom, 20 Nov: \"i want the fcs has to share the budget for every axis when needed\"; the released-stick height hold stays protected): one rule for the STOVL thrust budget in place of a branch per case. Kept whole: the height hold, the deceleration the ground needs (touchdown envelope), the envelope's own and the integral. Every other vertical demand (a climb, easing a commanded sink, stopping a released descent) shares the thrust with the sideways and fore-aft commands: each gets the same share of what it could have alone and may use what the other leaves. Replaces 0.5.24's StovlSinkStopShare. false = 0.5.23 (only a stick climb shares, StovlClimbShare).");
			cStovlLeverStop = dev.Bind("9. STOVL", "StovlLeverStopModel", true, "Vortex 0.5.26 (Tom, 20 Nov: \"when i command accel and then right back to deccel, the jet tends to wiggle for deccel\"): where the lever is an acceleration command, the aft detent asks for the stop the jet can make (the fore-aft profile's demand for the zero-ground-speed command inside the translation budget, what the speed command asks there), so the demand no longer steps from StovlDecelMax to the budget as the lever hands over to the speed command. false = StovlDecelMax.");
			cStovlTiltAll = dev.Bind("9. STOVL", "StovlTiltAllRegimes", true, "Vortex 0.5.26: the hover's fore-aft tilt (nose up to brake past the nozzle's forward limit) is worked out in every regime and taken into the attitude by the thrust's share of the lift alone (1 - wing share), not gated on the lever / stick regime (lamV, lamT). false = 0.5.25.");
			cStovlFreeWing = dev.Bind("9. STOVL", "StovlFreeWingAxes", true, "Vortex 0.6.0 (Tom, 20 Nov: departure after Stability Assist went off at 50 m/s with a pull at -55 deg of bank): with Stability Assist off, the stick's pitch and the released heading hold fade from Euler rates (nose up against the horizon: in a bank partly a body yaw) to the airframe's own axes as the wing takes the weight, by the same share as the velocity-vector roll: the stick is a body pitch rate, the released pedal holds zero sideslip instead of the heading, and the yaw rate is the one that keeps the sideslip still (g cos(pitch) sin(bank) / (V cos(AoA))). The hover is unchanged. false = 0.5.26.");
			cStovlEngCond = dev.Bind("9. STOVL", "StovlEngineCondition", true, "Vortex 0.6.1 (Tom, 20 Nov: engine damaged by hard landings, the jet could not lift off and no LOW HVR T/W caution came): what the lift system can make counts the engine's damage by the game's own engine law (the reachable RPM falls with the engine's condition, and with it the dry thrust): the hover T/W behind the LOW HVR T/W cautions and the speed floor, the thrust budgets and the thrust fit's ceiling. false = 0.6.0 (the static thrust).");
			cStovlSmoothTd = dev.Bind("9. STOVL", "StovlSmoothTouchdown", true, "Vortex 0.5.16 (Tom, 19 Nov: on a vertical landing the jet comes down as fast as the gear survives and bounces): the descent is slowed, just before the wheels touch, to the highest sink rate at which no wheel that has touched leaves the ground again: from a drop of the whole jet on its legs in heave and pitch (the game's spring and damper rates, the mass, the pitch inertia, the CG's place between the legs, the hover's pitch attitude, the engine spooling down from the hover's thrust), ~2.0 m/s at 11-15 t. Where the slow-down starts follows from the spare lift, as the gear-break limit's did (a heavy jet starts higher), with the vertical axis's own thrust response time as the lag in place of 1 s. Bench, 13.8 t: contact 5.0 -> 1.9 m/s, 3.4 -> 1.3 g, no bounce (0.24 m before). false = 0.5.15 (touchdown at the gear-break limit, or StovlSinkRate if lower).");
			cStovlYawPivot = dev.Bind("9. STOVL", "StovlYawPivot", true, "Vortex 0.5.13 (Tom, 19 Nov: the real F-35B yaws snappily in the hover with no visible bank): the hover is held at the yaw's own pivot instead of the CG. The nozzle's yaw bearing turns the jet about the point yaw inertia / (mass x nozzle arm) ahead of the CG (3.2 m at 13.8 t) by itself, so the side-speed loop and the position hold look at that point and nothing is banked for the yaw; the tail swings wide and the nose stays nearly in place. At speed the heading rate still rises only as the bank for the turn arrives. false = 0.5.12 (the CG held, the bank cancelling the nozzle's push, the yaw waiting for it).");
			cStovlTurnYawLim = dev.Bind("9. STOVL", "StovlTurnYawLimit", true, "Vortex 0.4.7 (Tom's rule: the FCS does not let the pilot command what the jet cannot hold): in the hover laws the pedal's yaw rate is no more than the hover's sideways acceleration can turn the velocity with (StovlTurnShare x sideways acceleration available / ground speed), it builds and decays no faster than the bank can follow (g x bank rate / ground speed), and the yaw follows the bank's plan by the bank loop's lag so the two arrive together. Nothing changes in a hover or at a few knots; at 13.8 t full pedal gives about 12 deg/s at 30 kt and 7.5 at 45 kt instead of 20, without the skid. false = the yaw axis's own limit at every speed (0.3.9-0.4.6).");
			cStovlTurnShare = dev.Bind("9. STOVL", "StovlTurnShare", 0.85f, "Vortex 0.5.7: default 0.707 -> 0.85 (a file still holding 0.707 is moved once; bench, 13.8 t, full pedal: 30 kt 11.3 -> 12.9 deg/s with the skid 0.75 -> 1.25 m/s, 45 kt 7.2 -> 8.4 with 0.52 -> 0.82, the bank the other way after release unchanged; 1.0 gives 13.0 / 9.7 deg/s but 10 deg of bank the other way after a 45 kt turn). Vortex 0.4.7: the share of the sideways acceleration a full-pedal turn may take (StovlTurnYawLimit). 0.707 = the sideways half of a budget divided equally between sideways and fore-aft; the rest stays free to take out a skid. 1 = all of it: a higher yaw rate at speed (about 10.5 deg/s at 45 kt, 13.8 t, for 7.5), with about 1.5 m/s of skid held through the turn and the bank swinging 9 deg the other way when the pedal is released (bench). Allowed 0.3-1.");
			cStovlPirouette = cfg.Bind("1. General", "StovlPedalPirouette", false, "Vortex 0.5.7: default off (Tom, 18 Nov 11:53: \"i dont command side skid so the jet must correct for it\"; a file still holding true from 0.5.6 is moved once): the pedal turn corrects the skid again, the nose as fast as the sideways budget can bring the flight path round with it (StovlTurnShare, now 0.85). Vortex 0.5.6 (Tom, 18 Nov: at 40-50 kt the pedal followed a curve instead of yawing at the full rate; he chose the flat pirouette): where the STOVL laws' hover pedal holds (up to the stick and pedal hand-over, 60-70 kt with StovlRegimeShift 30), the pedal turns the nose at the yaw axis's full rate and the jet keeps flying the way it was going while it is held (the lever's speed stays along the direction the nose had when the pedal came in; the side stick still acts across the nose). Let go, the yaw stops and the lever's speed runs along the new nose again. With airspeed the nose is held short of the sideslip where the airframe's weathervane would take most of the yaw authority (StovlPirouetteSlipShare in the dev file). false = 0.4.7-0.5.5: the pedal's rate is capped at what the bank can turn the velocity with (a coordinated curve, ~7.5 deg/s at 45 kt on a 13.8 t jet).");
			if (cVortexGenRev.Value < 2)
			{
				// 0.5.7: StovlPedalPirouette default true -> false; a file written by 0.5.6 (revision 1) still holding true is moved once
				if (cStovlPirouette != null && cStovlPirouette.Value)
				{
					cStovlPirouette.Value = false;
					Log?.LogInfo("Vortex FCS: StovlPedalPirouette true -> false (0.5.7 default: the pedal turn corrects the skid again).");
				}
				cVortexGenRev.Value = 2;
			}
			cStovlPirSlip = dev.Bind("9. STOVL", "StovlPirouetteSlipShare", 0.8f, "Vortex 0.5.6: in the pedal pirouette, the share of the yaw authority the airframe's weathervane (directional stiffness x sideslip, from its own tables) may take before the pedal stops turning the nose further off the flight path; the rest is kept to stop the yaw there. Lower = less sideslip at speed. Allowed 0.2-0.95.");
			cTeUpGeo = dev.Bind("5. Airframe", "TeUpFromGeometry", true, "Vortex 0.4.9 (Tom, 18 Nov 00:33: after a carrier take-off the trailing-edge surfaces went up where the STOVL pose droops them): which way a control surface's deflection raises its trailing edge is taken from the hinge's geometry. Before, it was learned from the first airflow over 10 m/s and kept for the flight, which reads backwards when that flow comes from behind (a jet spawned tail to the wind over a carrier's deck) and at random in a crosswind: the droop, the flap droop and the ground brake then went the wrong way on those surfaces until the jet was respawned. false = 0.4.8 and earlier.");
			cStovlDoorDrag = dev.Bind("9. STOVL", "StovlFanDoorDragArea", 0.6f, "Vortex 0.1.4 / 0.1.7: drag area (Cd x A, m^2) of the lift fan's upper (inlet) door fully open: drag = 1/2 rho v^2 x this x sin(door angle) / sin(full angle), at its hinge, so it grows smoothly as it opens and is 0 shut (not computed then). 0.1.7: the door opens with the fan's lower doors (the game opened it with the nozzle swivel only) and replaces the game's own swivel-scaled drag on it; 0.1.4-0.1.6 put this drag on the lower doors. 0 = no drag on the door (the game's term is still taken out).");
			cStovlAuto = dev.Bind("9. STOVL", "StovlAutoTransition", false, "Vortex 0.1.3 (0.1.8: off by default, as the F-35B, where the pilot presses the STOVL button both ways and nothing converts by itself; with it off, Flight Assist hold converts at StovlConversionMaxIas or less and reconverts at StovlReconversionMinIas or more, or on the wheels). On: Flight Assist hold is a STOVL switch. With it on, the jet converts to CTOL by itself at StovlAutoCtolIas (370 km/h; doors shut, nozzle aft, the normal laws) and back to STOVL at StovlAutoStovlIas (350 km/h); turned on faster than that it is armed and converts once the jet slows. Turned off: reconversion (not below StovlReconversionMinIas in the air), CTOL from then on. false = 0.1.2's manual toggle.");
			cStovlAutoCtol = dev.Bind("9. STOVL", "StovlAutoCtolIas", 370f, "Vortex 0.1.3 / 0.1.6 (0.1.7: 370, was 350): with the STOVL switch on, the jet reconverts to CTOL by itself at this IAS (km/h) or more, held 1 s (doors shut, nozzle aft, the normal laws). 0.1.4-0.1.5 used the wing's level-flight AoA instead (StovlAutoCtolAoA, no longer read).");
			cStovlAutoStovl = dev.Bind("9. STOVL", "StovlAutoStovlIas", 350f, "Vortex 0.1.3 / 0.1.6 (0.1.7: 350, was 300): with the STOVL switch on, the jet converts back to STOVL by itself at this IAS (km/h) or less, held 2 s in calm flight (not while pulling, over 1.4 g or with the pitch stick past 0.3, nor for 3 s after). Kept at least 20 km/h under StovlAutoCtolIas.");
			cStovlTeDroop = dev.Bind("9. STOVL", "StovlTeDroop", 25f, "Vortex 0.1.1: trailing-edge droop (deg, each surface, trailing edge down) of the elevator and aileron pairs in STOVL mode, like the F-35B's flaperons and tails in the hover. Faded out as the surfaces gain authority (their share of the jet's total control authority 25 -> 60 %) and with the doors closing; the differential stays free for roll. 0 = off.");
			cStovlRudderFlare = dev.Bind("9. STOVL", "StovlRudderFlare", 20f, "Vortex 0.1.1: rudder flare (deg, each rudder, trailing edge outboard) in STOVL mode, faded out with the same authority share and door position as StovlTeDroop. The differential stays free for yaw. 0 = off.");
			cVortexLift = dev.Bind("5. Airframe", "VortexLift", true, "Vortex 0.1.0 (Tom 16 Nov: vortex lift similar to the F-22E's): forebody chine / LERX vortex lift. The sections ahead of the CG (wingRoot_L/R, intake, cockpit) get their lift multiplied by ForeLiftSchedule at their local AoA (drag unchanged), which takes most of the stock FS-20's strong nose-down moment away from 0 to 40 deg (relaxed stability, a small stable margin kept) and adds lift. Every variant and AI jets. Applied at spawn. false = the stock forebody.");
			cForeLiftSched = dev.Bind("5. Airframe", "ForeLiftSchedule", "0:1.22,8:1.22,12:1.23,16:1.26,20:1.32,24:1.40,28:1.45,32:1.49,36:1.50,40:1.50,44:1.43,48:1.31,52:1.21,56:1.10,60:1.00", "Vortex 0.1.0: |local AoA| (deg) : forebody lift multiplier pairs for VortexLift, linear between points, held past the last one, mirrored on the negative side. Default: 1.22 at low AoA rising to 1.50 at 36-40 deg, back to 1.00 by 60 deg (0.3.1: raised 16-52 deg so the wing vortex lift, which sits aft of the CG, leaves the 0.2.11 pitch balance within 0.3 deg/s^2 from 0 to 55 deg AoA, bench cmsweep).");
			cStagger = dev.Bind("5. Airframe", "ElevonStallStagger", true, "Revoker 0.2.20 (0.2.22: knee-preserving): staggered stall of the two trailing-edge elevon pairs. Both use the wing foil (Fighter1_wing: lift knee 17 deg, peak 44.6 deg local AoA), so without this both reverse for velocity-vector roll at ~45 deg together. Each pair gets its own copy of the curve with the peak moved to InnerElevonReversalAoA / OuterElevonReversalAoA (both signs); the game's aero (the parts' charts) and the FCS's models use the same curves. The inboard pair's camber share on wing2 is applied as a force on that curve, so it reverses with the elevon (wing2's own clean lift keeps the wing curve). Takes effect on the next spawn. false = 0.2.19.");
			cStallInner = dev.Bind("5. Airframe", "InnerElevonReversalAoA", 40f, "Vortex 0.3.1: 40 (0.2.11: 26; moved once if it was exactly 26 / 45): with the elevons as flaps on the wing (ElevonFlapModel) and no manoeuvre droop, the bench's stagger sweep (inner 20-45 x outer 30-60; trimmed authority 0-70 deg at 300 / 500 km/h, asymmetric-load rolls, falling leaf) put 40 / 55 at no hole in the lateral authority, the most asymmetric-load left roll and the quickest leaf reversal. Revoker 0.2.22: 26 (0.2.21: 37, 0.2.20: 32; migrated once). Inboard elevon (flap_L/R) lift-peak local AoA (deg, 20-80) with ElevonStallStagger. 0.2.22 moves only the plateau above the wing foil's 17.2 deg knee (the curve below it and the 1.10 peak CL are untouched). Model, outboard at 45, both pairs + camber, measured arms: 26 maximises the worst-case velocity-vector roll authority over 10-55 deg (0.26 small / 0.18 full deflection, vs 0.03 / 0.01 unstaggered); the inboard pair reverses at ~27.7 (33.2 at full deflection) and is flown reversed from there. Next spawn.");
			cStallOuter = dev.Bind("5. Airframe", "OuterElevonReversalAoA", 55f, "Vortex 0.3.1: 55 (0.2.11: 45; see InnerElevonReversalAoA). Revoker 0.2.20: outboard elevon (aileron_L/R) lift-peak local AoA (deg) with ElevonStallStagger. 45 = the wing foil's own (44.6). Next spawn.");
			cFlapCamber = dev.Bind("5. Airframe", "FlapCamberCoupling", 0.23f, "Revoker 0.2.0: the inboard elevon (flap_L/R) also cambers the wing2 section ahead of it: wing2's lift normal turns by K x the flap deflection (the stock HLD turns it 4 deg for 25, K 0.16). Thin-airfoil effectiveness of the 0.24-chord flap (1.2 of 4.9 m) is 0.60; host share (0.60 x 5.2 - 1.4) / 3.8 = 0.46. 0 = off.");
			cElevonFlapModel = dev.Bind("5. Airframe", "ElevonFlapModel", true, "Vortex 0.3.1 (Tom 17 Nov): the elevons act as flaps on the wing. Each elevon plate keeps the game's area (ElevonPlateLiftScale 1) and the rest of the thin-airfoil flap effect (chord fraction ~0.21, effectiveness 0.56) is a camber term on the wing strip ahead of it (ElevonFlapK over InnerStripArea / OuterStripArea x wing2b), on the WING's own lift curve, with plain-flap deflection effectiveness (full to 10 deg, 0.8 at 20, 0.6 at 30). About a third more camber lift per degree at low AoA than 0.2.11, less at large deflections; the flap effect now stalls with the wing, not at AoA + deflection. Next spawn. false = 0.2.11 (FlapCamberCoupling / AileronCamberCoupling on wing2b, on the elevon's staggered curve).");
			cElevonFlapK = dev.Bind("5. Airframe", "ElevonFlapK", 0.21f, "Vortex 0.3.1: with ElevonFlapModel, the wing strip's camber per degree of (effective) elevon deflection: K x strip = 0.56 x (strip + plate geometry) - the plate's game area 2.0 -> 0.21, both pairs. 0 = no camber term.");
			cInnerStripArea = dev.Bind("5. Airframe", "InnerStripArea", 1.48f, "Vortex 0.3.1: with ElevonFlapModel, the wing area ahead of the inboard elevon as a multiple of wing2b's 2.7 m^2 (wing2b, wing2a and the outboard ends of wing1a / wing1b over its span: ~4.0 m^2).");
			cOuterStripArea = dev.Bind("5. Airframe", "OuterStripArea", 1.44f, "Vortex 0.3.1: with ElevonFlapModel, the wing area ahead of the outboard elevon as a multiple of wing2b's 2.7 m^2 (wing2b, wing2a and the wingtip over its span: ~3.9 m^2).");
			cElevonPlateLift = dev.Bind("5. Airframe", "ElevonPlateLiftScale", 1f, "Vortex 0.3.1: with ElevonFlapModel, the elevon plates' lift and drag scale. 1 = the game's 2.0 m^2 (twice their ~1.05 m^2 of geometry; that area is also the trailing edge's share of the wing's lift at neutral, and 0.53 left the jet 16 deg/s^2 nose-up unstable at 15 deg AoA on the bench). Next spawn.");
			cWingVortexLift = dev.Bind("5. Airframe", "WingVortexLift", true, "Vortex 0.3.1 (Tom 17 Nov): delta-wing vortex lift on the wing panels (wing1a/1b, wing2a/2b, wingtip): their lift is multiplied by WingLiftSchedule at their local AoA, the extra lift's drag as a normal force (dCL x tan AoA). Next spawn. false = the stock wing foil (flat from 17 deg).");
			cWingLiftSched = dev.Bind("5. Airframe", "WingLiftSchedule", "0:1,15:1,17:1.02,20:1.13,25:1.30,30:1.41,33:1.43,36:1.36,40:1.21,45:1.08,50:1.02,55:1", "Vortex 0.3.1: |local AoA| (deg) : wing-panel lift multiplier pairs for WingVortexLift (linear between points, held past the last). Default on the stock foil: CL 0.92 at 17, 1.20 at 25, 1.33 at 30, 1.37 at 33 (peak), 1.20 at 40, 1.0 by 50-55 deg.");
			cPostStallFlap = dev.Bind("5. Airframe", "PostStallFlapEff", 0.35f, "Vortex 0.3.1: with ElevonFlapModel, the elevons' camber term past the wing's lift peak keeps at least this fraction of its attached lift per degree (to 60 deg AoA, fading out by 90) instead of reversing with the wing's falling lift curve. Next spawn.");
			cFlapCamberHi = dev.Bind("5. Airframe", "FlapCamberHiAoA", 45f, "Vortex 0.3.1: with ElevonFlapModel, from this AoA (deg) the elevons' camber term blends over 15 deg back to 0.2.11's (the wing fully separated; the high-AoA yaw and falling leaf the FCS is tuned on). 90 = the 0.3.0 term everywhere. Next spawn.");
			cElevonDroop = dev.Bind("5. Airframe", "ElevonManeuverDroop", false, "Vortex 0.3.1 (Tom 17 Nov: no flap bias): false = the elevons carry no manoeuvre / high-lift droop in CTOL (0.2.11 drooped them up to 23 deg TE-down with AoA and load). The STOVL droop is separate and unchanged. true = 0.2.11.");
			cAilCamber = dev.Bind("5. Airframe", "AileronCamberCoupling", 0.23f, "Revoker 0.1.0: the outboard elevon (aileron_L/R) also cambers the wingtip section ahead of it: the wingtip's lift normal turns by K x the deflection. Thin-airfoil effectiveness of the 0.30-chord surface (1.02 of 3.41 m) is 0.66; host share (0.66 x 5.25 - 2.0) / 3.25 = 0.45. 0 = off.");
			cLatMargin = cfg.Bind("1. General", "HighAoALateralBrakeMargin", 0.86f, new ConfigDescription("High-AoA lateral braking margin. Above HighAoALateralMarginFull (30 deg AoA, dev cfg) the roll- and yaw-rate limits are multiplied by this, blended in smoothly from HighAoALateralMarginStart (25). 0.86 = 14 % less allowed stop time: 14 % less roll and yaw rate for the same authority, the rotation stops sooner. 1 = 0.3.0. Takes effect at once.", new AcceptableValueRange<float>(0.5f, 1.5f)));
			cMpoSep = cfg.Bind("1. General", "MpoRollMode", MpoRollMode.SeparateRollYaw, "The lateral law with Stability Assist off (MPO). VelocityVectorRoll = as with it on: the stick rolls about the velocity vector, the sideslip is held and the pedals work as they do with it on. SeparateRollYaw (default) = Revoker 0.2.7: the stick rolls about the velocity vector at every AoA (sideslip held while the stick is in and the pedals centred) and the pedals command pure body yaw rate at every AoA (no sideslip limit on them; with both centred, below the coupling AoA, sideslip washes out slowly as before). Before 0.2.7 the stick was body roll, coordinated below the coupling AoA by a sideslip limit on the yaw axis. Takes effect at once.");
			cMpoWash = dev.Bind("3. Lateral-directional", "MpoSideslipWashout", 0.5f, "Revoker 0.2.7: pedals centred only (the pedals themselves are pure body yaw, with no sideslip limit). Ifrit 0.2.0, separate axes: with the pedals centred and below the coupling AoA, the yaw axis lets sideslip wash out at this rate (deg/s of yaw rate per deg of sideslip), like the airframe's own weathervane. Sideslip is kept inside +-PedalSideslip there. 0 = the yaw rate is held exactly (sideslip stays where the roll left it).");
			cIfritRev = dev.Bind("2. Envelope", "IfritConfigRevision", 8, "Internal: config revision (inherited from the Ifrit FCS). Leave it alone.");
			cIfritSplit = dev.Bind("3. Lateral-directional", "SplitFlapYaw", defaultValue: true, "Ifrit: the split-flap decelerons are allocator effectors (drag yaw; the game splits a flap from its job's yaw input). The stock zero-throttle airbrake split is suppressed while the FCS flies. false = the flaps never split while the FCS is engaged. Applied when the jet spawns.");
			cIfritTvcRoll = dev.Bind("3. Lateral-directional", "RollTvc", defaultValue: true, "Ifrit: differential (roll) nozzle deflection about the pitch angle, the Ifrit's stock roll vectoring (+-15 deg), as an allocator effector. false = pitch-only TVC like the F-22E law. Applied when the jet spawns.");
			cSplitMu = dev.Bind("4. Gains", "SplitFlapCost", 1E-03f, "Ifrit: allocator cost of split-flap use (drag). Bench: roll/yaw results unchanged from 1e-4 to 5e-3.");
			cSlipTvc = dev.Bind("3. Lateral-directional", "SlipTvcAssist", true, "Ifrit 0.3.0: the sideslip correction may use body roll up to what the wing surfaces plus the differential TVC can brake, instead of being held to the aero yaw-rate limit. At high AoA the correction is mostly body roll (x sin AoA), and the nozzles roll the jet at any IAS. false = 0.2.2.");
			cIfritFlaperons = dev.Bind("5. Airframe", "IfritFlaperons", true, "Ifrit 0.3.0: the decelerons (inner) and ailerons (outer) work as flaperons on the F-22E flap schedule: up to 23 deg (inner) and OuterFlaperonDroop x that (outer) TE-down with AoA, load and speed (the high-lift schedule), held off the stall on each surface's own flow, out of the way of full roll stick, faded out from 20 to 30 deg AoA. false = 0.2.2 (no droop).");
			cTvcRollNeutral = dev.Bind("4. Gains", "TvcRollNeutralHold", false, "Ifrit 0.3.0, default OFF: on the bench any weight here makes the released-stick pitch hold sluggish (q -0.3 deg/s bleeding off over ~7 s in level flight at 400 km/h, the stabs held still), an interaction with the roll-fight cost. TvcNeutralHold for the differential (roll) nozzles. With the lateral stick and pedals centred, the roll on its command and the sideslip under 2 deg, the nozzles go back to symmetric and the wing surfaces hold the roll trim; any roll input, roll error or sideslip frees them at once. false = 0.2.2.");
			cTvcRollNeutralW = dev.Bind("4. Gains", "TvcRollNeutralWeight", 30f, "Ifrit 0.3.0: cost weight of TvcRollNeutralHold.");
			cSlipGainHi = dev.Bind("3. Lateral-directional", "SlipGainHighAoA", 4f, "Ifrit 0.3.0 (SlipTvcAssist): the sideslip gain (KBeta) ramps to this from 25 to 45 deg AoA, where the correction is mostly body roll from the nozzles. Bench, 65 deg corkscrew, 1 s full-pedal kick: back under 3 deg in 4.3-4.7 s at 1.3, 0.5-0.8 s at 4. Below 25 deg KBeta applies as before.");
			cSlipGainLoops = dev.Bind("3. Lateral-directional", "SlipGainFromLoops", true, "Revoker 0.2.14: the sideslip-hold gain is what the rate loops carrying the correction can follow, instead of the fixed SlipGainHighAoA ramp. The correction rS is flown as body yaw rS cos(AoA) through the yaw-rate loop (YawRateGain, 2.5/s = 0.4 s) and as body roll -rS sin(AoA) through the roll-rate loop (RollRateGain, 6/s = 0.17 s), each plus the actuator lag (ActuatorLagT). The sideslip loop closes through them as gain/s x lag, so its phase margin is gain x lag: the gain is SideslipGain x (yaw-loop lag / the cos^2-sin^2 weighted lag of the two paths), and a path that would have to deliver its share beyond the spare authority of its axis counts the time that takes instead. 0 deg AoA: exactly SideslipGain (1.3); -50: ~1.85; 70: ~2.3. The 30 Oct 22:48 log flew the Revoker's push + roll at the fixed 4 through the 0.4 s yaw loop: ~15 deg of phase margin, sideslip -3.4 <-> +3.6 at 1 Hz, yaw rate +-12 deg/s about its command, rudder stop to stop, the roll governor cutting twice (the hitch); the F-22E, at 1.3 there, recovers -6.8 deg in one move. false = the SlipGainHighAoA ramp (0.2.13).");
			cActLag = dev.Bind("3. Lateral-directional", "ActuatorLagT", 0.07f, "Revoker 0.2.14: command-to-surface lag (s) the SlipGainFromLoops gain counts on top of each rate loop's 1/gain (30 Oct 22:48 log: rudder and canard command lead their position by 0.07 s, the yaw nozzle by 0.02).");
			cSlipGovLead = dev.Bind("3. Lateral-directional", "SlipGovLeadFromLoops", true, "Revoker 0.2.15: the push + roll sideslip governor looks ahead as far as the lateral loops take to answer (the SlipGainFromLoops lag, 0.33-0.47 s) instead of a fixed 0.8 s. 30 Oct 23:45 log (0.2.14), push + roll at -42 deg AoA: the sideslip crossing zero at +11 deg/s on its way back from -3.3 deg (the hold's own recovery, peak +2.4) read as +9 deg 0.8 s out and cut the roll target 79 -> 46 deg/s; the roll braked, the yaw command fell with the yaw nozzle and rudder on their stops, the yaw rate overran by 6 deg/s and the nozzle went stop to stop, then the release 0.35 s later re-built the roll 46 -> 85 and it went round again. Over the loops' own lag that crossing projects to 3.6 deg: no cut. false = 0.8 s (0.2.14). Needs SlipGainFromLoops.");
			cSlipLimTas0 = dev.Bind("3. Lateral-directional", "RollSlackMinTAS", 25f, "Ifrit 0.3.9: below this true airspeed (m/s) the roll is not held back for sideslip or yaw coordination (sideslip guard / governor, yaw-anchored roll limit, roll-acceleration coordination cap); they come back in over RollSlackTASSpan (smoothstep) and are off past 120 deg |AoA|. Higher = more roll slack at low speed, more sideslip.");
			cSlipLimTasSpan = dev.Bind("3. Lateral-directional", "RollSlackTASSpan", 50f, "Ifrit 0.3.9: m/s over which the roll limiters come back in above RollSlackMinTAS (default: full from 75 m/s TAS, 270 km/h). Bench, full stick from near zero airspeed accelerating to 250 km/h: 25/50 holds 21-44 deg/s where 20/30 sat at 11 deg/s; corkscrew from 150 km/h and slow rolls unchanged.");
			cSlipHoldTas0 = dev.Bind("3. Lateral-directional", "SlipHoldMinTAS", 15f, "Ifrit 0.3.7: the sideslip hold is off below this true airspeed (m/s) and fades in over SlipHoldTASSpan; it also fades out from 100 to 120 deg |AoA| (flying backwards). Pedals keep commanding yaw there. In tail slides and near zero airspeed the hold asked for yaw it had no authority for: the jet swayed left/right and the roll stick was scaled to nothing. 0 = 0.3.6 at speed (reversed-flow fade still applies).");
			cSlipHoldTasSpan = dev.Bind("3. Lateral-directional", "SlipHoldTASSpan", 20f, "Ifrit 0.3.7: m/s over which the sideslip hold fades in above SlipHoldMinTAS (default: full from 35 m/s TAS).");
			cDecHldFold = dev.Bind("5. Airframe", "DeceleronHldFold", true, "Ifrit 0.3.6: the stock high-lift device also rotates each deceleron's inner hinge 20 deg TE-down (the hinge carrying the deceleron's lift and both split halves), on top of the FCS deflection and the split: a split half reached 50 deg on the runway with the airbrake out (80 worst case). With this on, that hinge stays put and the decelerons fly the same 20 deg x deploy as droop instead, so everything stays inside the +-30 deg deceleron travel and the split budget. The airbrake has priority over that droop (it retracts while the splits are out). false = 0.3.5.");
			cSlipPredT = dev.Bind("3. Lateral-directional", "SlipPredictTime", 0.3f, "Ifrit 0.3.5: sideslip predictor window (s), the inner roll/yaw rate loops' lag in game (~0.3 s in the 20 Oct logs). The sideslip correction works on the sideslip minus the yaw (stability-axis) rate it has already commanded over this window, which has not reached the sideslip yet. Fixes the 0.7 Hz sideslip / roll oscillation at the high-AoA sideslip gain (20 Oct 22:37 and 23:10 logs). 0 = off (0.3.2). Replaces 0.3.3's SlipRateLead.");
			cSlipPredRollGate = dev.Bind("3. Lateral-directional", "SlipPredictRollGate", 0f, "Ifrit 0.4.0: roll-stick fraction over which the sideslip predictor fades OUT (0.3.5-0.3.9: 0.2, i.e. the predictor was off whenever roll stick was in). 0 = the predictor runs with roll stick in too. 22 Oct 00:30 log, 65 deg AoA corkscrew at 55 m/s with full roll stick held: without the predictor the high-AoA sideslip loop (gain 4) rang against the ~0.34 s inner-loop lag, sideslip +-12 deg and body roll +-50 deg/s on a 1.6 s cycle, every roll surface and the nozzles bang-bang on their stops. A delay-loop model with that lag and 60-100 deg/s^2 of roll authority reproduces the +-10-12 deg cycle without the predictor and settles 12 -> -1.5 -> 0.5 deg in 1.6 s with it.");
			cSplitYawTier = dev.Bind("3. Lateral-directional", "SplitYawTier", true, "Ifrit 0.4.0: least drag first with an engine out or a damaged yaw axis. The canted tails carry the yaw as long as they can; the inner/outer flaperon crossing comes next (the allocator finds it), the split decelerons open only for the yaw still out of reach (a gate on measured yaw saturation that leaks shut once the yaw is met, the split then having priority over its deceleron's deflection for their shared travel), the VMC throttle limit last. Bench, yaw per kN of added drag at 250 km/h and 20 deg AoA: tails +-10 deg 4.8 deg/s^2, crossing 1.1-1.5, one split at 30 deg 0.4 (17.7 kN of drag), the throttle cut 0.17, false = 0.3.9 (tails and splits one tier, the split free: with the right engine alone at 140 m/s the left split sat at 15-24 deg while the tails held +-2 deg; at the 30 deg cap the flaperons crossed with the splits shut and the engine cut to 30-40 %). Both engines running: no change.");
			cStabYawRelax = dev.Bind("3. Lateral-directional", "StabYawRelax", 1f, "Ifrit 0.4.0 (SplitYawTier): with an engine out or a damaged yaw axis, how far (0-1) the roll-fight cost is relaxed for the canted tails, so their differential may buy yaw with the wing taking the roll back. 0 = the 1.8.41 fight cost in full (the allocator then prefers sideslip or the splits to a tail difference).");
			cSlipPredW = dev.Bind("3. Lateral-directional", "SlipPredictWashout", 1.5f, "Ifrit 0.3.5: washout (s) on the predictor's command history, so a held correction (e.g. a yaw trim against a load) leaves no extra steady sideslip. 0 = no washout.");
			cRevS0 = dev.Bind("3. Lateral-directional", "ReversedFlowSenseStart", 120f, "Ifrit 0.3.4 (ReversedFlowRollSense): |AoA| (deg) where the roll stick sense starts to turn over for reversed flow. It blends smoothly to fully flipped at ReversedFlowSenseEnd, passing through zero. 0.3.2 flipped at once at 100 deg (released at 80), next to 90 deg, where the velocity-vector roll is nearly all body yaw, so the flip reversed the rotation mid-manoeuvre.");
			cRevS1 = dev.Bind("3. Lateral-directional", "ReversedFlowSenseEnd", 150f, "Ifrit 0.3.4: |AoA| (deg) where the roll stick sense is fully flipped for reversed flow (see ReversedFlowSenseStart).");
			cSlipGainA0 = dev.Bind("3. Lateral-directional", "SlipGainAoAStart", 5f, "Ifrit 0.3.1: AoA (deg) where the SlipGainHighAoA ramp starts (0.3.0: 25); it reaches SlipGainHighAoA 20 deg higher. Pedals centred only. Bench, corkscrew entry at 475 km/h: from 25 the sideslip built at 15-20 deg AoA and the rolling-pull governor held the AoA there (the 20 deg hitch).");
			cFullStickQ = dev.Bind("2. Envelope", "FullStickPitchRate", 60f, "Ifrit 0.3.1: pitch-rate limit (deg/s) at full aft stick; the limit rises from MaxPitchRate (48) over the last 20 % of aft stick, so up to 0.8 stick and the push side map as before. MPO unchanged. 0 or <= MaxPitchRate = off.");
			cSplitBrakeHold = dev.Bind("7. Airbrake", "SplitBrakeHoldCost", 0.5f, "Ifrit 0.3.0: 0.5 (0.2.2: 0.05). With the airbrake out, the allocator cost of bringing both splits in together (the brake symmetric part). With the 30 deg split and 100 deg/s servos, 0.05 let the allocator trade split for pitch and stall there (bench, 700 km/h at 3 km: splits held at 27, pitch rate drifting to -0.6 deg/s, no retraction on release). Higher keeps the full brake through pulls; one-sided retraction for yaw is free, as on the stock Ifrit.");
			cTvcDiffMu = dev.Bind("4. Gains", "RollTvcCost", 0.003f, "Ifrit 0.3.1: 0.003 (was 5e-5). Allocator cost of differential (roll) nozzle deflection. At 5e-5 the roll TVC was nearly free, so on a pitch release the allocator could run the decelerons/ailerons unevenly and cancel their roll with the nozzles (one nozzle back to neutral before the other, 20 Oct 14:03 log; bench 330-420 km/h: up to 12 deg nozzle split, pitch-rate overshoot -3 deg/s). At 0.003: under 1 deg, overshoot -0.2; corkscrew and rolling-pull sideslip within 0.1-0.3 deg of before.");
			cYawTvc = dev.Bind("3. Lateral-directional", "YawTvc", false, "Revoker 0.1.0: the nozzle's stock yaw axis (+-15 deg, controlInputs.yaw x thrustVectoring.y) is an allocator effector, in the first yaw tier with the rudder. The game zeroes it under 3 m radar altitude and above 250 m/s (with pitch). false = pitch-only TVC (F-22E principle).");
			cTvcYawMu = dev.Bind("4. Gains", "YawTvcCost", 0.0005f, "Revoker 0.1.0: allocator cost of yaw nozzle deflection.");
			cFlapFlaperons = dev.Bind("5. Airframe", "FlapFlaperons", false, "Revoker 0.2.0: the inboard trailing-edge surfaces (flap_L/R; in the game moved only by the high-lift device, 25 deg TE-down below 90-150 m/s) are inboard elevons: allocator effectors for pitch and lateral (the inboard pair, roll group 3, so they can also cross the ailerons for drag yaw as split elevons), driven at SurfaceServoSpeed. The FCS drives the hinge and turns the wing2 section's lift normal by FlapCamberCoupling x the deflection. No flap schedule (FlapHighLiftDroop 0). false = HLD flaps as stock.");
			cFlapHinge = dev.Bind("5. Airframe", "FlapHingeRange", 25f, "Revoker 0.2.0: travel each way of the inboard elevons (deg). The stock HLD goes 25 TE-down.");
			cFlapHiLift = dev.Bind("5. Airframe", "FlapHighLiftDroop", 0f, "Revoker 0.2.0: default 0. TE-down droop (deg) the inboard elevons are drawn toward at full HLD deployment (the game's low-speed flap schedule: out below 90 m/s, in above 150). A canard-delta's trailing edges are elevons with no flap schedule (unlike the F-22E / Ifrit flaperons), so 0 = no droop: the allocator alone sets them.");
			cCanardPitch = dev.Bind("4. Gains", "CanardPitchPriority", true, "Revoker 0.1.0: during rolls the roll-time nozzle budget (RollTvcBudget) keeps the nozzle within its band of NEUTRAL (F-22E: of the pitch-only trim) at every thrust (F-22E: open from T/W 0.7 to 1.1), so the canards cancel the roll's nose-up / nose-down and carry the pitch trim; the nozzle still goes past the band where the canards cannot (their stall band, AoA protection, AoA/G capture braking).");
			cFlapRelief = dev.Bind("5. Airframe", "FlaperonLoadRelief", false, "Revoker 0.1.0: off (F-22E/Ifrit: on). The flap schedule's high-g term that turns the trailing-edge flaperons up to 9.6 deg TE-up from 3 to 9 g below 16-24 deg AoA. Off, the trailing edges stay at neutral in a held turn and the canards carry the pitch.");
			cYawTvcNeutral = dev.Bind("4. Gains", "YawTvcNeutralHold", 300f, "Revoker 0.1.0: while the lateral stick and pedals are steady, the roll on its command and the sideslip under 2 deg (a held turn), the nozzle's yaw axis is drawn back to neutral over ~1 s and the rudder and canards carry the steady yaw; the nozzle still takes every change. Cost weight; 0 = off. The pitch axis has the same (TvcNeutralHold, canards carrying the steady pitch).");
			cYawTvcNeutralYawGate = dev.Bind("4. Gains", "YawTvcNeutralYawGate", true, "Revoker 1.4.2 (Tom 16 Nov 2026: in a falling-leaf reversal the yaw nozzle stops the yaw, sits at neutral for a split second, then yaws the other way). YawTvcNeutralHold only comes in while the yaw rate is on a steady command: the hold fades out as the yaw-rate error grows from 2 to 8 deg/s (as it already does for the roll error) and as the yaw-rate command's own rate grows from 5 to 20 deg/s^2. Without it the hold's steady-stick test (the stick against a copy that follows it at 2 per s) passed ~0.95 s after a full stick reversal and ~0.5 s after a release, in the middle of the yaw stop: 16 Nov 09:21 log, 69 deg AoA, nozzle -15 -> -1.8 deg for 0.4 s with the yaw rate 20-28 deg/s off its command, yaw acceleration 84 -> 38 deg/s^2; the same in most high-AoA stops and reversals of that flight. false = 1.4.1. Takes effect at once.");
			cVisHinge = dev.Bind("5. Airframe", "VisualHingeFix", false, "Revoker 0.2.3: draw the trailing-edge surfaces turning about their modelled hinge lines (render only; aero unchanged). Off: the game's own visuals.");
			cCorkTol = dev.Bind("3. Lateral-directional", "CorkscrewSlipTol", 0f, "Revoker 0.2.2: with roll stick held above ~35-50 deg AoA (pedals centred), sideslip (deg) the sideslip hold lets build before it takes the yaw back; the corkscrew keeps rotating. 0 = 0.2.1.");
			cHiAoARollW = dev.Bind("3. Lateral-directional", "HighAoARollWeight", 1f, "Revoker 0.2.7: 1 (F-22E/Ifrit 0.35). The allocator's roll-axis weight from 40 deg AoA (blended in from 20) while the nozzles do not cover pitch (TVC off or the low-thrust T/W); with them covering it stays 0.35. With the roll target out of reach the pitch (weight 8) and yaw (YawPriority) axes took everything at 0.35 and the roll went the wrong way for moments, which with the game's surface lag set up a ~0.6 Hz cycle of every surface stop to stop (28 Oct 20:31 log, TVC off at the 50 deg cap). Bench, TVC off, 50 deg loaded rolls with a game-like 4-frame command delay: surface slew peaks 35-70 -> 10-45 deg/s, sideslip 6.2-6.9 -> 2.9-4.6 deg, roll 24-27 -> 29-33 deg/s. Takes effect at once.");
			cCanWin = dev.Bind("3. Lateral-directional", "CanardAttachedWindow", true, "Revoker 0.2.7: above 30 deg AoA either sign, with lateral input in, the canards are walked into the deflections where their own flow still has lift slope (|local AoA| <= the foil's knee + CanardWindowMargin), when their travel reaches them. Past the knee the allocator sees no roll / yaw gradient on a canard and parked them stalled (28 Oct 21:18 log, full push + roll at -50 deg: both canards at -1 deg, local AoA -48..-50, for 20 s). false = 0.2.6. Takes effect at once.");
			cCanWinMargin = dev.Bind("3. Lateral-directional", "CanardWindowMargin", 12f, "Revoker 0.2.7: local AoA (deg) past the canard foil's lift-slope knee (15.8 deg) that CanardAttachedWindow allows. Bench: 6 cost up to 5 deg of AoA in 50 deg roll reversals, 12 held it within 2-4.");
			cFinTvcHold = dev.Bind("3. Lateral-directional", "FinLossTvcHold", true, "Revoker 0.2.7: with the fin gone (yawHealth under 0.5) the engines keep vectoring above their 250 m/s cut-off (raised to 1000 m/s on this jet, put back when the fin is whole or the FCS lets go). The yaw nozzle is the only yaw axis then; at the cut-off the game zeroes the nozzles and the jet yawed round (28 Oct 21:22 log, fin + wings gone, 247 m/s). false = the game's cut-off always. Takes effect at once.");
			cFinNoTvcCap = dev.Bind("2. Envelope", "FinLossNoTvcAoACap", 30f, "Revoker 0.2.7: positive AoA limit (deg) with the fin gone and no nozzle yaw (TVC off, or the thrust under the low-thrust T/W), phased in from no fin damage to the fin gone. Bench, fin gone, TVC off, held pull + roll: 45-50 departs, 40 departs with a game-like command delay, 35 and below hold. 0 = off. Takes effect at once.");
			cSlipYawTvc = dev.Bind("3. Lateral-directional", "SlipYawTvcAssist", true, "Revoker 0.2.1: with the yaw nozzle active, the sideslip gain ramps to SlipGainHighAoA at high AoA (Ifrit: only with roll TVC, which the Revoker has none of).");
			cGroundElevonPitch = dev.Bind("5. Airframe", "GroundElevonPitch", 1f, "Revoker 0.2.1: on the ground, the share of each trailing-edge elevon's travel the pitch stick moves (0-1; full aft = TE up). In the air the allocator uses them for pitch (ElevonPitch).");
			cCanardYawTier = dev.Bind("3. Lateral-directional", "CanardYawTier", false, "Revoker 0.1.0, default off: with the fin or rudder damaged, count the canards' yaw (differential drag) in the first yaw tier with the rudder and the yaw TVC. Off: the canards are still full pitch / roll / yaw effectors in the allocator, but their yaw is not counted as free reach (one canard at 40 deg makes ~45 deg/s^2 of yaw at a pitch and roll cost the allocator will not pay, so counting it kept the split-flaperon tier shut: bench, fins gone at 700 km/h, a pedal kick departed with nothing moving for 1.3 s).");
			cElevonPitch = dev.Bind("5. Airframe", "ElevonPitch", true, "Revoker 0.2.0: default on. The ailerons (outboard elevons) are pitch effectors as well as roll, with their true pitch moment in the allocator; the inboard elevons (FlapFlaperons) always are. false = ailerons for roll/yaw only, pitch from canards + TVC + inboard elevons.");
			cElevonSymCost = dev.Bind("4. Gains", "ElevonSymCost", 2f, "Revoker 0.1.0: allocator cost of the symmetric (pitch) part of the flaperon pair (F-22E/Ifrit: 2).");
			cAoANegRev = dev.Bind("2. Envelope", "AoALimitNegativeRevision", 1, "Internal: the AoALimitNegative default revision this file was last updated to. Leave it alone.");
			if (cAoANegRev.Value < 1)
			{
				// 2.2.4: default 45 -> 50. Runs once: a file still holding the old default 45 is moved; any other value is the
				// player's own and is kept. Afterwards the file is at revision 1 and 45 set later stays.
				if (Mathf.Abs(Mathf.Abs(cAoANeg.Value) - 45f) < 0.001f)
				{
					cAoANeg.Value = 50f;
					Log.LogInfo("Vortex FCS 2.2.4: AoALimitNegative moved from the old default 45 to the new 50 (once).");
				}
				cAoANegRev.Value = 1;
				dev.Save();
			}
			// Vortex 0.3.6 (Tom, 17 Nov: "set aoa limit to +60/-55 as default"): moved once whatever the file holds; a value set
			// by hand after this is kept
			if (cAoANegRev.Value < 2)
			{
				float wasP = cAoAPos.Value;
				float wasN = cAoANeg.Value;
				cAoAPos.Value = 60f;
				cAoANeg.Value = 55f;
				cAoANegRev.Value = 2;
				Log.LogInfo($"Vortex FCS 0.3.6 defaults: AoALimitPositive {wasP:F0} -> 60, AoALimitNegative {wasN:F0} -> 55, once.");
			}
			cGPos = dev.Bind("2. Envelope", "GLimitPositive", 9.5f, "Positive load limit (g), normal law.");
			cGNeg = dev.Bind("2. Envelope", "GLimitNegative", 3.5f, "Negative load limit magnitude (g), normal law.");
			cMpoGPos = dev.Bind("2. Envelope", "MPOGLimitPositive", 12.5f, "Positive load limit (g) with Stability Assist OFF (MPO).");
			cMpoGNeg = dev.Bind("2. Envelope", "MPOGLimitNegative", 3.5f, "Negative load limit magnitude (g) in MPO.");
			cMaxPitch = dev.Bind("2. Envelope", "MaxPitchRate", 60f, "Pitch-rate budget ceiling (deg/s). Revoker 1.4.1: default 48 -> 60 (the F-22E's and Su-30's); a dev file still holding 48 is moved once.");
			cProt = dev.Bind("2. Envelope", "AoARecoveryRate", 48f, "AoA-protection recovery rate (deg/s), independent of thrust and weight.");
			cMaxRoll = dev.Bind("3. Lateral-directional", "MaxRollRate", 250f, "Absolute body roll-rate ceiling (deg/s). Below ~250 m/s IAS the original-aircraft schedule and RollAuthorityUse set the rate; above it this ceiling holds the rate flat.");
			cMaxYaw = dev.Bind("3. Lateral-directional", "MaxYawRate", 90f, "Body yaw-rate limit (deg/s). Revoker 0.2.2: 90 (was 75); bench, 70 deg loaded rolls 68-70 -> 75-80 deg/s velocity-vector roll at the same sideslip, corkscrews body yaw 64 -> 70 deg/s.");
			cRollLo = dev.Bind("3. Lateral-directional", "RollRateGainLowIAS", 1.2f, "Roll-rate ceiling vs the ORIGINAL aircraft's roll rate below ~120 m/s IAS (blends to the high-IAS gain by 170 m/s).");
			cRollHi = dev.Bind("3. Lateral-directional", "RollRateGainHighIAS", 1.1f, "Roll-rate ceiling vs the original aircraft's roll rate above ~170 m/s IAS.");
			cRollBrake = dev.Bind("3. Lateral-directional", "RollBrakeUse", 0.8f, "Fraction of the full roll authority (wings and stabilators) used to stop a roll, on top of roll damping. Before 1.8.26 this was a fraction of the wing-only authority.");
			cRollAuth = dev.Bind("3. Lateral-directional", "RollAuthorityUse", 1f, "Steady roll never needs more than this fraction of the available roll control moment (roll rate is given up instead of saturating the surfaces).");
			cCouple = dev.Bind("3. Lateral-directional", "CoupledAxisAoA", 36f, "Above this |AoA| the pedals blend into the stick's velocity-vector roll (full coupling 14 deg higher).");
			cPedalBeta = dev.Bind("3. Lateral-directional", "PedalSideslip", 15f, "Sideslip commanded by full pedal below the coupling AoA (deg).");
			cRollAcc = dev.Bind("3. Lateral-directional", "MaxRollAccel", 720f, "Ceiling on the shaped velocity-vector roll acceleration (deg/s^2); below it the measured authority sets the rate.");
			cKBeta = dev.Bind("4. Gains", "SideslipGain", 1.3f, "Stability-axis yaw rate per degree of sideslip error (1/s).");
			cKRoll = dev.Bind("4. Gains", "RollRateGain", 6f, "Roll-rate loop bandwidth (1/s).");
			cKPitch = dev.Bind("4. Gains", "PitchRateGain", 5f, "Pitch-rate loop bandwidth (1/s).");
			cKYaw = dev.Bind("4. Gains", "YawRateGain", 2.5f, "Yaw-rate loop bandwidth (1/s).");
			cBrakeBlend = dev.Bind("4. Gains", "AoACaptureBrakeBlend", 0.3f, "How far the capture braking plan leans from the authority at the target AoA (0) toward what is available right now (1). Lower = earlier braking, less overshoot, slower AoA build; higher = faster AoA build, more overshoot below afterburner. 0.3 is the bench balance.");
			cThrBrake = dev.Bind("4. Gains", "AoACaptureThrustBrake", defaultValue: true, "AoA capture plans its braking with the nose-down authority it can count on at the TARGET AoA: the nozzles' full authority at the live thrust (so less below afterburner and much less at part throttle) plus the stabs' authority at the target AoA from the surface model (almost none past ~45 deg, where they stall). The approach follows sqrt(2 x 0.7 x that x distance), the same deceleration the pitch trajectory can deliver. false = pre-1.8.20.");
			cAntiWind = dev.Bind("4. Gains", "AoAGAntiWindup", 4f, "Back-calculation anti-windup for the AoA and G integrators (1/s): whenever a limiter (AoA/G capture braking, AoA limit, pitch-rate budget) clips the pitch-rate command below what the AoA/G loop asks for, the integrator is pulled back toward the command at this rate. Without it the integrator winds up during every capture and then holds AoA about 0.2 deg above the command until it unwinds (several seconds). 0 = pre-1.8.20.");
			cLatRes = dev.Bind("4. Gains", "LateralStabReserve", 0.7f, "While you hold pitch stick AND lateral stick (roll or pedal) together, the pitch law builds pitch rate with the nozzles' authority plus only (1 - this x |lateral stick|) of the stabs' remaining authority, and the allocator's cost for symmetric (pitch) stab deflection rises with it, so the nozzles take the pitch and the stabs stay for roll/yaw. Braking toward the commanded AoA/G, the AoA/G limits and protection always use full authority. No lateral stick, or pitch stick released = unchanged. 0 = pre-1.8.19.");
			cPairTrim = dev.Bind("4. Gains", "StabTvcPairedSearch", defaultValue: true, "The allocator also tries moving each stabilator with the nozzles taking up its pitch change, and keeps the pair move when it lowers the total error. Without it, a stab parked on a stalled, flat part of its curve for pitch cannot be moved to where it makes roll, because moving it alone breaks pitch and moving the nozzles alone breaks it the other way. Lets the nozzles carry the pitch trim whenever they have room, freeing the stabs for roll/yaw. false = pre-1.8.18.");
			cBranch = dev.Bind("4. Gains", "StabBranchSwitch", defaultValue: true, "When a stabilator has been driven deep past its stall (local AoA more than 10 deg past the peak) during roll/yaw at moderate AoA, move it to the attached-flow side of its lift peak instead (trailing edge up, where the lift curve is steep), hold it there, and re-allocate everything else around it, the nozzles taking up the pitch. Undone when the modelled pitch error would grow by more than StabBranchPitchTol. false = pre-1.8.21.");
			cOvPrio = dev.Bind("4. Gains", "OvershootPitchPriority", defaultValue: true, "While the nose is pitching up faster than the pitch law commands above ~15-25 deg AoA (an AoA run-away), or AoA protection is active: (1) the allocator keeps pitch priority instead of handing stalled stabs to roll/yaw, and (2) it models the nose-down a stab really loses when it is driven deep past its stall for roll/yaw (the monotone stab pitch model hides that loss). false = pre-1.8.23.");
			cLeadK = dev.Bind("4. Gains", "StickLeadGain", 0f, "2.1.7: G + pitch-rate law only. Extra pitch-rate command on stick movement, washed out over StickLeadTau: a quick small input moves the nose a little at once, like an AoA step, then the plain rate response takes over; a held stick gets none. 0 = off (default). Around 1 = noticeable crispness; much above 1.5 small inputs can feel twitchy. Takes effect at once.");
			cLeadTau = dev.Bind("4. Gains", "StickLeadTau", 0.15f, "2.1.7: wash-out time (s) of StickLeadGain. Longer = the extra nose movement lasts longer and is larger for the same gain.");
			cGovPred = dev.Bind("3. Lateral-directional", "PredictiveRollGovernor", true, "2.1.5: the roll governor (1.8.41, keeps nozzle margin for pitch during rolls) looks at where the nozzle margin is heading 0.4 s ahead and eases the roll off early and in proportion, instead of cutting it once the nozzles are already pinned and restoring it a second later (the roll hitch). It comes back only when the margin is there and no longer falling. false = 2.1.4. Takes effect at once.");
			cYawSlewLead = dev.Bind("3. Lateral-directional", "YawSlewLead", true, "F-22E 2.4.18 (ported in Vortex FCS 0.3.1): the yaw-rate loop looks ahead along the measured yaw acceleration by the time the rudders need to swing back through neutral (their deflection / servo rate), so it starts braking the yaw before the yaw rate reaches its command instead of overshooting it (F-22E 22:36 log, 52 s: yaw rate 42 against 28, sideslip -12 on the reversal). No effect with the rudders near neutral; fades out from 36 to 45 deg AoA. false = off. Takes effect at once.");
			cSplitFlapYaw = dev.Bind("3. Lateral-directional", "SplitFlaperonYaw", true, "2.2.7: decelerons. When the yaw needed is past what the rudders can give (both fins gone, one fin gone, or one engine out with the other's thrust more than the rudders hold), the inner and outer flaperons on one wing open against each other (their rolls cancel, their drag yaws the jet), up to SplitFlaperonYawUse of the drag yaw they have. Only the part the rudders cannot cover; the engine-out VMC throttle limit acts after it. false = 2.2.6 (yaw target limited to the rudders). Takes effect at once.");
			cSplitFlapYawUse = dev.Bind("3. Lateral-directional", "SplitFlaperonYawUse", 1f, "2.2.19: default 1 (was 0.7). Bench, both fins gone, full roll held at 25-40 deg AoA: worst sideslip mean 12.4 -> 10.8 deg, roll tracking unchanged. 2.2.7: share (0-1) of the flaperons' remaining drag yaw the deceleron split may ask for. Higher = more yaw, more roll and pitch disturbance from the split. Takes effect at once.");
			cSplitFlapRollYield = dev.Bind("3. Lateral-directional", "SplitFlaperonRollYield", 0f, "2.2.8: how much of the deceleron split gives way to lateral stick (the split's reach shrinks by this x |lateral stick|, held 1 s after the stick returns). 0 = split kept through rolls (as 2.2.7; least sideslip). 1 = no split while rolling (roll tracks more evenly, but with both fins gone the sideslip in rolls runs much higher, e.g. 15 -> 27 deg replaying your 19 Oct 338-362 s inputs on the bench). Takes effect at once.");
			cRollPitchLead = dev.Bind("4. Gains", "RollPitchLead", 0.25f, "2.2.5: pull + lateral stick (the stabs handed to the roll, the nozzles carrying the pitch). The pitch-rate loop reads the measured pitch rate this many seconds ahead along the measured pitch acceleration, scaled by the nozzles' share of the pitch authority and by how much of the stab reserve the lateral stick has taken, so the slow nozzles stop driving stop to stop and the pitch rate stops swinging about its command (the hitch with an AoA overshoot in a full pull with roll added mid-pull, worse at high IAS). Pulls only. 0 = off (2.2.3). Bench: 0.15 / 0.25 / 0.35 / 0.5 all helped; 0.25 had the least AoA overshoot. Takes effect at once.");
			cSlatRelief = dev.Bind("3. Lateral-directional", "SlatRollRelief", false, "2.1.9 (off by default, experimental): during a roll at 12-45 deg AoA, when the nozzles' nose-down margin is heading for the roll governor's threshold, the slats retract (they are wing area ahead of the CG, so out they push the nose up) to give the pitch axis nose-down moment before the governor has to slow the roll. Costs ~7 % of the lift while they are in (bench: 0.05-0.4 g less in the roll); they come back out when the margin returns or the roll ends. Bench gain is modest: peak roll rate +4..+28 deg/s in 20-30 deg AoA rolls with thrust, no change to the first second of the roll. false = 2.1.8. Takes effect at once.");
			cCoupGateLim = dev.Bind("3. Lateral-directional", "CouplingLimitAtAoALimit", true, "2.1.10: rolls at an AoA limit. (1) While the stick holds the jet within 4 deg of an AoA limit, the roll-rate limit that keeps the roll's inertial pitch-up within the nose-down authority stays on and eases off over 0.6 s; it used to follow the pitch error on and off every ~0.6 s, so the roll rate and the pitch rate bounced. (2) While a velocity-vector roll runs, the AoA-limit capture plans its braking with what is left for pitch less the roll's inertial pitch-up, so it no longer overshoots the limit, hits protection and drops back several degrees in a cycle. In fast rolls the AoA can sit 1-3 deg under the limit. false = 2.1.9. Takes effect at once.");
			cLoadedRollSteady = dev.Bind("3. Lateral-directional", "LoadedRollSteady", true, "2.2.3: on by default (2.2.2: off). full aft + roll. The roll-rate limit that keeps the roll's inertial pitch-up within the nose-down authority stays on for the whole loaded roll (not only within 4 deg of the AoA limit), and the roll governor reads the nozzles' margin as it is instead of projecting it 0.3 s ahead. Both switched on and off every ~0.45 s in the 13 Oct 11:08 log (690 km/h pull + roll: roll target 98 <-> 172 deg/s, nozzles stop to stop, AoA 41..48). Bench: the pitch-rate and nozzle oscillation in the roll about halve; bank after 4 s 0-11 % lower. Only with CouplingLimitAtAoALimit on. Takes effect at once.");
			cSlipYawW = dev.Bind("3. Lateral-directional", "HighAoASideslipAllocation", true, "2.1.10: above ~20 deg AoA, while sideslip is building (from 2 deg, full by 8, counting where it is heading 0.3 s ahead), the control allocator stops trading roll accuracy for yaw (yaw weight 8 -> 2 at 40+ deg AoA): at high AoA a body roll that runs past its command makes sideslip as fast as a yaw-rate error does. No change while the sideslip is small. false = 2.1.9. Takes effect at once.");
			cCapLag = dev.Bind("2. Envelope", "AoACaptureEffectorLag", true, "2.2.1: the AoA-limit capture plans its stop with the pitch effectors' lag (0.3 s x the nozzles' share of the nose-down authority - they slew ~70 deg/s and carry most of it at low IAS), not as if the jet could start braking at once. A re-pull from ~38 deg at ~200 km/h overshot a 45 deg limit by 1.5 deg (3.5 with a roll entry). Where the nozzles carry the authority the AoA can stop 1-2 deg short of the limit and creep the rest. false = 2.2.0. Takes effect at once.");
			cAsymShared = dev.Bind("Asymmetric load", "AsymSharedStabLateral", true, "2.1.4: with an asymmetric load, past the rudder fade the roll and yaw rate limits also give up the stab deflection that trims the load's roll (the stabs are both the roll and the yaw source there), not only the yaw part of the load; past the rudder fade the yaw anchor and sideslip lead also act at positive AoA; and (2.1.6) a roll brake or reversal accelerates no faster than the yaw left after the load can follow. Only the load beyond the deadbands / a lateral CG offset over 4 cm counts; a clean jet is unchanged. false = 2.1.3. Takes effect at once.");
			cLowCapRoll = dev.Bind("3. Lateral-directional", "LowThrustCapRollFix", true, "2.1.3: rolls at the low-thrust AoA cap (36 deg at idle / low T-W). Within 4 deg of the cap the roll-coupling limit stays on instead of waiting for the AoA to rise, and the roll governor judges the nozzle margin only by the nozzles' share of the nose-down authority (pinned weak nozzles no longer read as no pitch reserve). Aimed at the hitches where the roll was cut and restored as the AoA overshot the cap. Only acts at the low-thrust cap. false = 2.1.2. Takes effect at once.");
			cCoupRoll = dev.Bind("4. Gains", "CouplingRollLimit", 0.5f, "While AoA is building past 20-30 deg (or running away), cap the velocity-vector roll rate so the inertial pitch-up it causes, (Izz-Ixx)/Iyy * p * r, stays within this fraction of the nose-down authority the jet has at that AoA (aero + stabs + nozzles at live thrust). This is what let full-aft + full-roll pulls overshoot to 70-77 deg below afterburner. Held-AoA rolls are not affected. 0 = off (pre-1.8.23).");
			cBranchMaxAoA = dev.Bind("4. Gains", "StabBranchMaxAoA", 42f, "Body AoA (deg) above which StabBranchSwitch is not used. Above ~44 deg the stalled side of the stab is the better one for roll/yaw.");
			cBranchTarget = dev.Bind("4. Gains", "StabBranchTarget", 20f, "Local AoA (deg) StabBranchSwitch moves the stab to, on the attached side of its lift peak.");
			cBranchTol = dev.Bind("4. Gains", "StabBranchPitchTol", 5f, "Largest growth in modelled pitch-acceleration error (deg/s^2) StabBranchSwitch accepts. Protects AoA holding when the nozzles cannot take up the stab's pitch.");
			cKpAoA = dev.Bind("4. Gains", "AoAGain", 5f, "AoA command: deg/s of pitch rate per deg of AoA error.");
			cKiAoA = dev.Bind("4. Gains", "AoAIntegral", 3f, "AoA command integral gain (1/s) near the target.");
			cStick24 = cfg.Bind("5. Pitch stick curve", "StickFor24degAoA", 0.6f, "2.0.12 (default 0.6 since 2.0.15; 0.45 in 2.0.13-2.0.14): pull-stick position (0..1) that commands 24 deg AoA. 0.333 = the standard 72 deg per full stick line. Allowed 0.083-0.7: lower = more AoA early in the pull (sharper), higher = less (softer, finer control at low AoA). The curve through 0, this point and the 36 deg point is smooth (monotone cubic), so the gain changes gradually instead of at a corner. Takes effect at once.");
			cStick36 = cfg.Bind("5. Pitch stick curve", "StickFor36degAoA", 0.8f, "2.0.12 (default 0.8 since 2.0.15; 0.65 in 2.0.13-2.0.14): pull-stick position (0..1) that commands 36 deg AoA (on the standard line: 0.5). Allowed 0.3-0.8, and always at least 0.1 of stick past StickFor24degAoA (if set closer it is moved out to 24's point + 0.1, so 36 can never come at or before 24). Past 36 the curve continues to the AoA limit at full stick as before (the high-AoA rate hand-over is unchanged). Applies to the pull side; the push side keeps the 72 deg per stick line. Takes effect at once.");
			cStickRev = cfg.Bind("5. Pitch stick curve", "StickCurveRevision", 2, "Internal: the stick-curve default revision this file was last updated to. Leave it alone.");
			// Vortex FCS 0.3.1 (F-22E 2.6.0): the lateral reference governor's knobs live in the player config; PullRollTrade moves here from the Dev file (its value carried over once)
			cPullRollTrade = cfg.Bind("6. Lateral governor", "PullRollTrade", 0.7f, "2.2.8 (2.2.23: default 0 -> 0.7; 2.2.26: 1; 2.2.27: back to 0.7): in a rolling pull the yaw cannot keep up with, how much of the shortfall is paid by slowing the roll and how much by slowing the AoA rise (the sideslip a rising AoA makes goes as roll rate x AoA rate). 0 = roll kept, AoA rise limited. 1 = roll slowed (to no less than 30 deg/s) until the asked AoA rise fits; in between, the roll limit goes that share of the way and the pitch covers the rest (F-22E 2.6.4/2.6.5). The roll comes back once the AoA rise is no longer limited. Takes effect at once.");
			cRollStopSlip = cfg.Bind("6. Lateral governor", "RollStopSideslip", 4f, "Vortex 0.3.14 (Tom, 17 Nov 19:53: \"I want the margin to be the same as the raptor\"): 4, the F-22E's (a file still holding 6 is moved once). default 8 -> 6 (Tom, 5 Nov 01:10: roll reversals at low to mid AoA made 7-9 deg, the margin spent in every one; 2 deg less, at some roll rate there). Vortex FCS 0.4.0: default 8 (the F-22E flies 4). From your 30-31 Oct logs the old lateral stack ran this jet at 7-8 deg (95th percentile) of sideslip above 45 deg AoA and 2.6-3.3 at 15-45, and the rates you were used to came with that; the governor at 4 kept the sideslip to 2-4 and took the yaw rate in the falling leaf from 77 to 54 and the moderate-AoA roll from 64 to 46 deg/s (Revoker 0.3.0 flight). 8 is where this jet was; lower = cleaner stops, slower rolls. F-22E 2.4.17 (2.5.0: 3 -> 4): sideslip (deg) a roll stop or reversal is allowed to make; the lateral governor caps the commanded roll rate (never the roll acceleration or braking) to the rate whose full stick reversal the yaw can follow within it. The one margin at every AoA (no schedule); the airframe probe (RollStopSlipProbe) can only lower it. A handling (sway) number: lower = slower rolls at moderate AoA, cleaner stops. Allowed 1-15. Takes effect at once.");
			cGovRev = cfg.Bind("6. Lateral governor", "GovernorRevision", 0, "Internal: the lateral-governor default revision this file was last updated to. Leave it alone.");
			cRollGovPitch = cfg.Bind("6. Lateral governor", "RollGovernorPitch", true, "F-22E 2.5.2, rebuilt 2.5.3-2.6.6: roll first, then pitch, both ways. With the roll already held to its cap, the AoA rate the pitch law may command under the roll is held to what the yaw can pay for: an AoA sweep changes the yaw the pair needs at roll rate x cos AoA x AoA rate. Nose-up at positive AoA: 0.8 x the rudders' braking yaw not yet in use on the roll's side, faded in with the rudders' braking yaw and only as the forecast sideslip heads past 75 % of the margin - a rolling pull that pins the rudders but holds its sideslip keeps its pitch rate. Every other sweep (nose-down anywhere, nose-up from negative AoA): 0.5 x the yaw authority plus half the roll's, from the same rule, at every IAS; a sweep toward zero AoA under a body yaw rate is paced so the yaw that will have nothing to coordinate at zero AoA is shed first (weighted by 1 - cos AoA, so the nose is let through near zero). Never under 6 deg/s of AoA rate; released over 0.3 s. false = the trade alone. Takes effect at once.");
			cRollPaceRateSlip = cfg.Bind("6. Lateral governor", "RollPaceRateSlip", 1f, new ConfigDescription("F-22E 2.5.10: how much of the sideslip margin the roll-rate cap spends - the roll rate / sideslip compromise. 1 = the cap aims a full stick reversal at the margin as set. Above 1 the cap aims past the margin and lets the roll run faster (F-22E bench, margin 15: worst / mean sideslip and mean roll rate 5.8 / 3.9 at 52 deg/s for 1.0, 8.3 / 4.8 at 59 for 1.25, 10.7 / 6.3 at 67 for 1.67, 13.0 / 7.6 at 74 for 2.0, against 10.9 / 8.0 at 83 with no cap); the game's worst held-roll reversal reads about 1.4x the bench's. Below 1 the cap is stricter than the margin. Allowed 0.5-3. Takes effect at once.", new AcceptableValueRange<float>(0.5f, 3f)));
			cLeafFadeLo = cfg.Bind("6. Lateral governor", "LeafRateCapFadeLo", 45f, new ConfigDescription("Vortex 0.3.1 (Tom 17 Nov: 30-35 deg/s in a steady falling leaf): the roll-pace rate cap's share (RollPaceRateBlend) fades out from this AoA (deg) to LeafRateCapFadeHi. Bench, 70 deg leaf: 22 -> 34 deg/s, stop 1.6 -> 2.0 s, sideslip 2.6 -> 4.7 deg. Set Hi <= Lo to turn it off (0.2.11). Takes effect at once.", new AcceptableValueRange<float>(20f, 90f)));
			cLeafFadeHi = cfg.Bind("6. Lateral governor", "LeafRateCapFadeHi", 45f, new ConfigDescription("Vortex 0.3.14: 45 = off (Hi <= Lo), as the F-22E, which has no such fade (0.3.1-0.3.13: 60; a file still holding 45 / 60 is moved once). Vortex 0.3.1: AoA (deg) where the rate cap's share has faded to zero (see LeafRateCapFadeLo). Takes effect at once.", new AcceptableValueRange<float>(20f, 91f)));
			cRollPaceRateBlend = cfg.Bind("6. Lateral governor", "RollPaceRateBlend", 0.4084507f, new ConfigDescription("Vortex 0.3.14: 0.4084507, the F-22E's (a file still holding 0.65 is moved once). default 0.4 -> 0.65 (Tom, 5 Nov 01:40: low to mid AoA roll sideslip somewhere between 0.5.1 and 0.8.0). F-22E 2.5.11: how the pace governor splits its work between its two laws. 0 = pacing only (the yaw-reach window holds the roll target while the yaw catches up and the trajectory's acceleration / braking is capped at the yaw's authority: reversals are paced, the margin does not matter). 1 = rate cap only (the target is capped at the rate whose stick reversal lands on the sideslip margin x RollPaceRateSlip; acceleration and braking stay as tuned: reversals are crisp, the rate is lower). Between: the proportional mix of the two targets, and the acceleration cap gives way to the tuned acceleration in the same proportion. The F-22E flies at ~0.4. Allowed 0-1. Takes effect at once.", new AcceptableValueRange<float>(0f, 1f)));
			cRollStopSlipProbe = cfg.Bind("6. Lateral governor", "RollStopSlipProbe", true, "F-22E 2.5.15/2.5.16: the airframe's own sideslip tolerance, measured from its aero tables in flight - no number entered. Twice a second the FCS evaluates the whole airframe (every aero part at the game's per-part law, the surfaces where they are) at the present AoA and a sweep of hypothetical sideslip to 20 deg each way, and finds the sideslip where the restoring yaw moment stops growing (directional stiffness gone - the departure boundary) and where the yaw effectors' or the roll surfaces' authority has fallen to half of its zero-sideslip value. The smaller, x 0.7, caps RollStopSideslip. On an airframe with nothing inside 20 deg the cap never binds and RollStopSideslip rules; on one with a soft fin it binds where the fin gives out. Telemetry: slipTol / slipDep / slipAuth / slipStiff. false = RollStopSideslip alone. Takes effect at once.");
			cPushSlip = cfg.Bind("6. Lateral governor", "PushoverSideslip", -1f, "Vortex 0.3.14: -1 (follow RollStopSideslip), as the F-22E (a file still holding 8 is moved once). Revoker 0.6.0 (the F-22E 3.0.0 push governor and push plan): 8, set explicitly (Tom, 4 Nov: the stock jets' RollStopSideslip, not the F-22E's 4); -1 = follow RollStopSideslip. F-22E 2.9.4: sideslip (deg) a push-over from high AoA may make at the zero crossing from the yaw rate still on board. The sweep is paced so the yaw above what that allowance can absorb (r = sqrt(2 x measured shed x allowance)) is shed before the AoA reaches zero; the rest is left to the rudders at low AoA. Higher = faster nose-down with a high yaw rate on, more sideslip at the bottom. -1 = RollStopSideslip. 0-20. Takes effect at once.");
			if (cStickRev.Value < 1)
			{
				// 2.0.13: new defaults 0.45 / 0.65; a file still holding 2.0.12's 0.333 / 0.5 is moved once
				if (Mathf.Abs(cStick24.Value - 0.3333f) < 0.001f)
				{
					cStick24.Value = 0.45f;
				}
				if (Mathf.Abs(cStick36.Value - 0.5f) < 0.001f)
				{
					cStick36.Value = 0.65f;
				}
				cStickRev.Value = 1;
				cfg.Save();
			}
			if (cStickRev.Value < 2)
			{
				// 2.0.15: new defaults 0.6 / 0.8. Runs once, on the first start of 2.0.15 or later: a file still holding
				// the 2.0.13-2.0.14 defaults (both 0.45 and 0.65) is moved; any other pair is the player's own and is kept.
				// From then on the file is at revision 2 and is never touched again, so 0.45 / 0.65 set later stays.
				if (Mathf.Abs(cStick24.Value - 0.45f) < 0.001f && Mathf.Abs(cStick36.Value - 0.65f) < 0.001f)
				{
					cStick24.Value = 0.6f;
					cStick36.Value = 0.8f;
					Log.LogInfo("Vortex FCS 2.0.15: pitch stick curve moved from the old defaults 0.45 / 0.65 to the new 0.6 / 0.8 (once).");
				}
				cStickRev.Value = 2;
				cfg.Save();
			}
			cStickSmooth = cfg.Bind("1. General", "StickSmoothing", 0.06f, "Time constant (s) of the first-order smoothing on pitch/roll/pedal inputs. 0 = off.");
			cActDamp = dev.Bind("5. Airframe", "ActuatorDamping", 0.06f, "Actuator smoothing (s): time for a servo command to accelerate to full servo speed (and brake into its target on a matching curve). Servo speed is unchanged and a steady slew runs at full speed. 0 = off.");
			cYawStop = dev.Bind("3. Lateral-directional", "YawStopFactor", 0.7f, "Above 15-25 deg AoA the body yaw rate is capped at this x measured yaw authority (so the yaw can be stopped again). Higher = more responsive moderate-AoA rolls, more sideslip on stops.");
			cRollLead = dev.Bind("3. Lateral-directional", "RollLagCompensation", 0.008f, "Roll command lead per unit of roll damping (s per 1/s above 2.5/s): cancels the roll loop's tracking lag at high dynamic pressure so roll stops end when the command does, without a slow tail. 0 = off.");
			cYawPri = dev.Bind("4. Gains", "YawAxisPriority", 8f, "Allocator weight of the yaw axis relative to roll below ~20 deg AoA. Higher = rudders keep the roll coordinated even when their roll side-effect costs some roll acceleration.");
			cSlipCredit = dev.Bind("4. Gains", "SlipCreditFade", true, "Vortex 0.3.4: the allocator weighs the lateral axes in the velocity frame (sideslip acceleration = roll x sin AoA - yaw x cos AoA), so a roll it cannot deliver counts as yaw it no longer needs. With the roll far past its reach that credit cancels the sideslip correction (13:29 log, 92-98 s: roll 150-230 deg/s^2 short at 10 deg AoA, yaw 30-45 short, sideslip-axis error ~0; the canted rudders stayed off yaw and 8 deg of sideslip held). This fades the frame toward the body frame as the roll shortfall grows (SlipCreditRoll0 .. +SlipCreditSpan deg/s^2), below SlipCreditAoA0 .. +10 deg AoA. false = 0.3.3.");
			cPairPass = dev.Bind("4. Gains", "AllocatorPairPass", true, "Vortex 0.3.9 (managed and native solves) (Tom, 14:21 log, 448-452 s: full aft then roll, 1.2-2.5 s before the velocity-vector roll started; the two pairs of wing trailing edges opposed each other only once it did): the allocator's solve also searches every pair of surfaces over both their grids together. Its other passes are local (Gauss-Newton) or global in one surface at a time, so a move that only pays when two surfaces go together (one across its stall to a drag plate for yaw, the one beside it taking out the roll that makes; the canted rudders crossing to their reversed set) was found late. false = 0.3.6.");
			cSlipBandOn = dev.Bind("4. Gains", "SlipBandAllocation", true, "Vortex 0.3.9 (Tom, 17 Nov: some sideslip allowed but managed, similar to the Raptor): with the velocity-frame allocator, the high-AoA yaw (sideslip-acceleration) weight is flat inside +-SlipBand of sideslip and rises past it, instead of the 2.1.10 gate lowering it as sideslip grows (backwards in the velocity frame: 15:36 log, 7-8 deg held at 55 deg AoA for 8 s while it fell 8 -> 2.2). false = 0.3.8.");
			cSlipBand = dev.Bind("4. Gains", "SlipBand", 2f, "Vortex 0.3.9: sideslip (deg, with 0.3 s of lead) the allocator spends freely for velocity-vector roll at high AoA. With SlipBandSpan 3 and SlipBandGain 6 the bench's high-AoA rolls peak at 2-5.5 deg (mean 4.1), the Raptor's range (falling leaf 4.6-5.7, corkscrews ~3.7).");
			cSlipBandSpan = dev.Bind("4. Gains", "SlipBandSpan", 3f, "Vortex 0.3.9: deg past SlipBand over which the sideslip weight rises to its full (1 + SlipBandGain) x.");
			cSlipBandGain = dev.Bind("4. Gains", "SlipBandGain", 6f, "Vortex 0.3.9: how much the sideslip weight rises past the band (x (1 + gain) at SlipBand + SlipBandSpan).");
			cSlipCreditR0 = dev.Bind("4. Gains", "SlipCreditRoll0", 40f, "Vortex 0.3.4: roll shortfall (deg/s^2) where SlipCreditFade starts.");
			cSlipCreditSpan = dev.Bind("4. Gains", "SlipCreditSpan", 60f, "Vortex 0.3.4: roll shortfall (deg/s^2) over which SlipCreditFade reaches the body frame.");
			cSlipCreditA0 = dev.Bind("4. Gains", "SlipCreditAoA0", 20f, "Vortex 0.3.4: AoA (deg) above which SlipCreditFade starts to stand down (gone 10 deg higher).");
			cYawFF = dev.Bind("4. Gains", "YawRateFeedForward", 0.5f, "Fraction of the yaw-rate command's rate of change fed forward to the yaw loop at low AoA below 200 m/s IAS (blends to 1.0 by 300 m/s IAS, and by 28 deg AoA, where the body yaw is the velocity-vector roll). Lower = calmer rudders at low speed, slightly more sideslip.");
			cLowYaw = dev.Bind("3. Lateral-directional", "LowIasYawBoost", 1.7f, "Above 15-25 deg AoA, widens the yaw-rate cap (YawStopFactor x yaw authority, and its 20 deg/s floor) by this factor at very low IAS (falling leaf). It fades out along a curve above LowIasYawBoostFullIAS (see LowIasYawBoostFalloff). 1 = off.");
			cLowYawIas = dev.Bind("3. Lateral-directional", "LowIasYawBoostFullIAS", 70f, "IAS (m/s) up to which the full LowIasYawBoost applies.");
			cLowYawFall = dev.Bind("3. Lateral-directional", "LowIasYawBoostFalloff", 20f, "Above LowIasYawBoostFullIAS the extra margin decays exponentially with this IAS scale (m/s): 10 = ~50 % of it left at +7 m/s, ~10 % at +23 m/s, gone by 120 m/s.");
			cTvcMove = dev.Bind("4. Gains", "TvcMoveCost", 2E-05f, "Allocator cost of moving the TVC nozzles. 2e-5 = nozzles take pitch changes first; ~0.02 = stabs do the quick pitch changes and the nozzles slowly take over the sustained (trim) share. Post-stall the nozzles are always fast.");
			cTvcNeutral = dev.Bind("4. Gains", "TvcNeutralHold", 1000f, "2.2.14: while the pitch rate is held steady (no change of pitch-rate command, no pitch error, no roll in progress) the nozzles are drawn back to neutral over ~1 s and the stabs carry the steady pitch moment; the nozzles still take every change. Cost weight: 1000 (default); 0 = off (2.2.13). Takes effect at once.");
			cStabPark = dev.Bind("4. Gains", "StabParkEscape", true, "0.4.3 (the F-22E's 2.3.0/2.3.1 settled stall window): with the lateral stick and pedals centred and the lateral rates settled (under SettledRateOn deg/s, off again above SettledRateOff or with lateral input), the stabilators and rudders may not rest past the smaller of (lift peak - SettledStallMargin) and the foil's lift-slope knee (SettledSlopeFrac) of local AoA, at any AoA where such a range exists. This jet's stabilator foil has no stall before 45 deg (its lift keeps rising from 20 to 45 deg at a fifth of the linear slope, the drag four times), so a stab parked out there had next to no gradient for the allocator and made its maximum lift: 23 Oct 00:51 log, left stab on its stop for 10 s after a push through zero AoA at 58 m/s, the nozzle and the crossed wing against it. 0.4.2 and earlier only acted below 30 deg AoA and allowed the peak itself. false = off. Takes effect at once.");
			cSettledMargin = dev.Bind("4. Gains", "SettledStallMargin", 6f, "0.4.3: how far below the lift peak (deg of local AoA) the settled stall window keeps the stabilators and rudders (see StabParkEscape); the foil's slope knee applies where it is lower. Larger = further onto the attached slope, less trim deflection available from them. Takes effect at once.");
			cSettledSlope = dev.Bind("4. Gains", "SettledSlopeFrac", 0.15f, "0.4.3: the settled stall window's limit is also where the foil's lift slope has fallen to this fraction of its steepest (the knee of a plateau foil: 26 deg on the stabilator foil at 0.15, where the slope is 0.66 /rad against 4.4 at zero; 0.25 = 17 deg, 0.1 = 31 deg). 0 = the peak - SettledStallMargin only. Takes effect at once.");
			cSettledOn = dev.Bind("4. Gains", "SettledRateOn", 10f, "0.4.3: the settled stall window (StabParkEscape) comes on when the body roll and yaw rates are both under this (deg/s). Takes effect at once.");
			cSettledYawUse = dev.Bind("4. Gains", "SettledYawUse", 0.4f, "0.4.3: the settled stall window is held off while the thrust yaw (an engine out or down on thrust) uses more than this fraction of the fins' yaw authority at the present AoA (|asymAy| / asymCy, filtered 0.5 s, back on under this - 0.1): this jet has no rudders, and with an engine out a stabilator run out past its lift knee is its drag rudder (bench, engine-out pull at 250 km/h: windowed, the yaw saturated and the VMC cut the throttle). The 00:51 log's parked set had 0.29; the engine-out trims run 0.5-1.3. Takes effect at once.");
			cSettledOff = dev.Bind("4. Gains", "SettledRateOff", 20f, "0.4.3: the settled stall window goes off again above this body roll / yaw rate (deg/s), or with lateral input. Takes effect at once.");
			cSettledWalk = dev.Bind("4. Gains", "SettledWalkRate", 30f, "0.4.3: deg/s at which the settled window walks a surface resting past it back in (the window's edge moves, the allocator re-solves around each step). Takes effect at once.");
			cRollFightRel = dev.Bind("4. Gains", "RollFightReleased", true, "2.2.16: the roll-fight cost (RollFightCost) also acts while the lateral stick is centred, not only with the pedals in. With the stick released, a crossed set of surfaces - one stab split against the flaperons, ailerons and rudders, all near their stops, their roll cancelling - could hold for 10-15 s (19 Oct 18:05 / 18:32 logs, TVC off). false = 2.2.15. Takes effect at once.");
			cGndBrakeSplit = dev.Bind("7. Airbrake", "AirbrakeSplitInFlight", defaultValue: false, "In the AIR, the brake control deploys the airframe's split surfaces (the stock speedbrake mechanism). On the ground they stay stowed and the airbrake is the rollout surface configuration alone. The stock rule deployed them whenever the throttle was at idle, airborne as well as on the ground; the FCS drives them now, so idle throttle deploys nothing. false = split surfaces never deploy.");
			cAbElevUp = dev.Bind("7. Airbrake", "AirbrakeOutboardElevonUp", 20f, "Revoker 0.2.8 (TrailingEdgeBrakeConfig): outboard elevons trailing edge up with the airbrake out in the air, as the F-22E's ailerons. 0.2.9: the deflection is solved every frame so that the set adds no lift (with the canards' re-trim of its pitch counted): 14-19 deg on the bench at 400-1200 km/h. This key only switches the outboard share on (> 0) or off (0); 0.2.8 used it as a fixed deflection. The differential stays free for roll.");
			cTeBrakeFade = dev.Bind("7. Airbrake", "TrailingEdgeBrakeAoAFade", true, "Revoker 0.2.10 (TrailingEdgeBrakeConfig): the in-flight elevon brake set closes with AoA as the F-22E's speedbrake does (its AirbrakeAoAFade: full to ~15 deg AoA, half at ~25, stowed past ~35 on the clean jet): full to 15 deg |AoA|, closing linearly to stowed at 35, reopening as the AoA comes back. false = 0.2.9 (always the full set with the brake held). Takes effect at once.");
			cAbInbDn = dev.Bind("7. Airbrake", "AirbrakeInboardElevonDown", 25f, "Revoker 0.2.8 (TrailingEdgeBrakeConfig): inboard elevon trailing-edge-down deflection (deg, of 25) with the airbrake out in the air, as the F-22E's flaperons.");
			cAbRudToe = dev.Bind("7. Airbrake", "AirbrakeRudderFlare", 25f, "Vortex 0.2.10 (TrailingEdgeBrakeConfig): with the airbrake out in the air both rudders split by this much (deg, trailing edges outboard; drag without yaw, as the F-22E's rudders show in its speedbrake). On the canted fins the flare pitches nose down and balances the outboard elevons' trailing-edge-up set (AirbrakeOutboardElevonUp) on an airframe without an inboard elevon set. The yaw differential is still the allocator's (it gives toe back when the yaw needs the travel). Faded with AoA as the elevon set; off in the STOVL hover schedule. 0 = off.");
			cAbLiftFF = dev.Bind("7. Airbrake", "AirbrakeLiftCompensation", true, "Vortex 0.4.10 (Tom, 17 Nov 23:13: the g dip as the brake opens): the in-flight brake set sheds lift on this jet (the rudder flare lifts the tail, and the trailing edges that hold the nose against it lose more than that), and with the stick released the law holds pitch rate, so the jet sank until the AoA had come up by itself (bench, 900-1200 km/h: 0.67-0.76 g lowest). The pitch law now flies that AoA in as the brake opens and out again as it closes. The size comes from the surfaces' own force models and the airbrake panel's drag model (the panel sits above the CG and gives part of the lift back), no schedule; the panel's pitch moment is also given to the pitch loop as a known change. The set moves on an S-curve so the pitch rate can follow it. Bench: 0.88-0.93 g lowest at 900-1200 km/h, 0.95 at 700. false = 0.4.9 (no compensation, straight ramp).");
			cAbRudWin = dev.Bind("7. Airbrake", "AirbrakeRudderFlareWindow", true, "Vortex 0.4.1 (Tom, 17 Nov 21:05: brake at 500-600 kt, the nose pitched down and the jet rolled left): the rudder flare of the in-flight brake set (AirbrakeRudderFlare) is held to what both rudders reach inside their attached window (the settled window's local-AoA limit: lift peak less SettledStallMargin or the slope knee, the lower) less one grid step each way for yaw; the outboard elevons' trailing-edge-up share is solved against that flare. Above about 800 km/h the full 25 deg put both rudders on their window edge with the pair held to the set, so no yaw differential was left and the allocator missed its roll and yaw targets (bench, 900 / 1138 km/h, stick centred: down to 0.3 / 0.4 g, 22 / 19 deg of left bank in 6 s). false = 0.4.0 (the flare as set at any speed). Takes effect at once.");
			cGbElev = dev.Bind("7. Airbrake", "GroundBrakeOutboardElevon", 20f, "Revoker 0.2.8 (TrailingEdgeBrakeConfig): outboard elevon trailing-edge-up deflection (deg, of 30) in the rollout aero-brake.");
			cGbInb = dev.Bind("7. Airbrake", "GroundBrakeInboardElevon", 25f, "Revoker 0.2.8 (TrailingEdgeBrakeConfig): inboard elevon trailing-edge-up deflection (deg, of 25) in the rollout aero-brake.");
			cGbCan = dev.Bind("7. Airbrake", "GroundBrakeInboardElevonTEUp", 26f, "Vortex 0.1.0 (TrailingEdgeBrakeConfig, the F-22E's rollout aero-brake): inboard elevon (elevator_L/R, the stabilator-role pair) trailing-edge-up deflection (deg) on the runway with the brake. F-22E stabs: 26. The outboard elevons use GroundBrakeOutboardElevon (20), the rudders toe in 25 deg and the nozzle goes 10 deg trailing edge up.");
			cRotMinSpeed = dev.Bind("Ground", "RotationMinSpeed", 45f, "The take-off/landing rotation law (closed-loop pitch with the mains on the ground) only arms above this TAS (m/s). It used to arm at 15 m/s, an ordinary taxi speed, where it flew closed-loop pitch against the ground constraint and threw the surfaces about. It disarms 8 m/s below this.");
			cGroundRate = dev.Bind("Ground", "GroundRateLaw", defaultValue: true, "2.0.6: on the runway above RotationMinSpeed, pulling the stick hands pitch to the same rate law as gear-down flight, held through the rotation and lift-off (the rate command, its limits and stick scaling are the airborne ones). Stick neutral or forward with all three wheels down for 0.3 s goes back to the open-loop ground mode (steering, aero-brake). false = 2.0.5 behaviour (open-loop stick until the nose is up, then RotationRate).");
			cGroundRotRamp = dev.Bind("Ground", "GroundRotationRamp", 600f, "Revoker 0.2.9: 600 (F-22E 60). At 60 the nose stayed on the wheel for 0.7-0.85 s after a full pull at 200-320 km/h on the bench (in game, 29 Oct 00:25 log at 283 km/h: two full pulls of 0.45 s each did not lift it); at 600 it lifts in 0.16-0.20 s, with the same or smaller pitch-rate overshoot after lift-off. 2.0.6: while the nose wheel still touches in the ground rate law, the nose-up pitch moment the FCS asks for may grow by at most this much per second (in deg/s^2 of pitch acceleration). Stops the surfaces winding to full deflection against the nose leg and over-rotating when it lifts.");
			cNwsFbw = dev.Bind("Ground", "NoseWheelFbw", true, "1.8.36: fly-by-wire ground steering. Pedal commands a yaw rate (GroundYawRateMax at taxi speed, limited to GroundLateralAccel of turn at speed); pedal released = yaw braking to zero rate, then the jet holds a straight line along the track it had (a crab/drift at touchdown is steered out, the heading lined up with the track). The nose wheel is steered by the FCS (kinematic feed-forward + yaw-rate feedback) and, in the ground roll, the rudders join the yaw-rate loop. Below 1.5 m/s the pedal steers the wheel directly (pivoting). false = stock: pedal sets the nose-wheel angle, rudders follow the pedal.");
			cGndYawRate = dev.Bind("Ground", "GroundYawRateMax", 15f, "Full-pedal yaw rate on the ground (deg/s) at taxi speed.");
			cGndLatAcc = dev.Bind("Ground", "GroundLateralAccel", 2.5f, "Full pedal never asks for more than this lateral acceleration (m/s^2) in a ground turn: the yaw-rate command is min(GroundYawRateMax, this / ground speed). 2.5 = full pedal gives 2 deg/s at 260 km/h.");
			cGndLine = dev.Bind("Ground", "GroundLineHold", 1f, "Pedal released: how strongly the jet steers back onto the straight line it was on (1 = full, 0 = hold the track direction only and accept the sideways offset a gust or touchdown drift left).");
			cGndStock = dev.Bind("Ground", "GroundStockMatch", true, "0.5.2: with NoseWheelFbw on, stock steering with a drift limit and the straight-line correction. While the pedal is in (and after release until the yaw has stopped) the nose wheel, the rudders and the differential brake get the stock fly-by-wire's yaw, computed with the jet's own stock parameters (yaw-rate command with yaw damping; raw pedal below the stock minimum speed), so the turn is stock; GroundDrift* limit the sideslip on top. Once the yaw has stopped the FCS holds the straight line along the track until the pedal moves; at touchdown it holds the approach track. GroundYawRateMax and GroundLateralAccel are not used. false = the capped yaw-rate law of 1.8.36-0.4.9.");
			cGearDownSlip = dev.Bind("Ground", "GearDownSlipProtect", true, "0.5.3: the negative-AoA push + roll limiters (sideslip lead and yaw-anchored roll, 2.1.0) also act with the gear down in the air (not with a wheel on the ground). false = off with the gear down, as before.");
			cGndIndi = dev.Bind("Ground", "GroundIndi", true, "0.5.4: INDI on the nose wheel after the pedal is released - the release braking (stop the yaw) and the straight-line / touchdown hold. The wheel effectiveness comes from the game each tick (nose-leg load, tyre friction and slope, CG and yaw inertia); the measured yaw acceleration covers the rest. While the pedal is in the steering stays stock. false = 2.4.13 (stock law on release, PI line hold).");
			cGndIndiK = dev.Bind("Ground", "GroundIndiGain", 15f, "0.5.4: INDI nose wheel: wanted yaw acceleration per deg/s of yaw-rate error (1/s).");
			cGndPedalBrake = dev.Bind("Ground", "GroundPedalBrake", 0.5f, "Revoker 1.3.1: while the pedal is in, the main on the pedal's side is braked by |pedal| x this (stock: 0.3, and only at 3-30 m/s; this works at any ground speed). The jet pivots harder and the mains break loose, so it drifts. Bench, 3 s full pedal from 15 kt at low thrust: peak sideslip 7.1 deg (1.3.0; also 0.25 and below, where the stock 0.3 underneath wins) -> 9.3 (0.5); speed after 3 s 15.0 -> 12.6 kt. Never below your own brake. 0 = 1.3.0.");
			cGndLightNose = dev.Bind("Ground", "GroundIndiLightNoseHold", true, "Revoker 1.3.1: when the nose leg goes light during a release stop or the straight-line hold (the jet rocks on its suspension when a hard turn is released), the nose wheel keeps the angle INDI had relative to the contact velocity (at most 1 s) instead of dropping to the stock law's angle (full opposite lock) or 0. The game slews the wheel toward its command even off the ground, so 1.3.0 met the ground again tens of degrees out. Bench, nose unloaded 0.3-0.85 s into a 10 kt release stop (with GroundIndiBrake): stop 1.47 -> 1.25 s. false = 1.3.0.");
			cGndIndiBrake = dev.Bind("Ground", "GroundIndiBrake", true, "Revoker 1.3.1: the main-wheel brakes as a second yaw effector for the INDI after the pedal is released (release stop and straight-line hold). The nose wheel goes first; the brake on the outside main takes only the yaw acceleration beyond the nose tyre's grip, read from each main's load, friction and contact velocity every tick. Your own brake is never reduced and the stock differential brake stays underneath. Each main's brake is written directly, so it does not depend on the stock 3-30 m/s window. Costs speed: bench, release from a full-pedal turn at 10 kt: stop 1.0 -> 0.87 s, 2.8 kt lost. false = 1.3.0.");
			cGndIndiBrakeMax = dev.Bind("Ground", "GroundIndiBrakeMax", 1f, "Revoker 1.3.1: most brake (0-1) the yaw effector may put on one main. Lower = less speed lost per stop, slower stop.");
			cGndIndiBrakeFull = dev.Bind("Ground", "GroundIndiBrakeFullSpeed", 7.7f, "Revoker 1.3.1: full brake authority at and below this ground speed (m/s; 7.7 = 15 kt).");
			cGndIndiBrakeZero = dev.Bind("Ground", "GroundIndiBrakeZeroSpeed", 12.9f, "Revoker 1.3.1: brake authority fades to zero at this ground speed (m/s; 12.9 = 25 kt). Above it the ground steering is 1.3.0.");
			cGndIndiTau = dev.Bind("Ground", "GroundIndiTau", 0.04f, "0.5.4: INDI nose wheel: low-pass time constant (s) on the measured yaw acceleration and the tyre-force estimate.");
			cGndDriftAllow = dev.Bind("Ground", "GroundDriftAllow", 8f, "0.5.2: sideslip (deg) the jet may drift to on the ground before the steering counters it (stock steering does not limit it). 8 = about half of the Ifrit stock-pedal drift above 50 kt (27 Oct logs: 17-25 deg peaks, 42 in a slalom). 0 = hold the nose on the track; large = no limit.");
			cGndDriftGain = dev.Bind("Ground", "GroundDriftGain", 0.2f, "0.5.2: counter-yaw per degree of sideslip past GroundDriftAllow (1 = full pedal; 0.2 = full counter-yaw 5 deg past the allowance). 0 = drift limit off.");
			cGndDriftLead = dev.Bind("Ground", "GroundDriftLead", 0.25f, "0.5.2: the drift limit acts on the sideslip predicted this far ahead (s) from its rate, so a fast skid is caught before it overshoots.");
			cGndDriftFade = dev.Bind("Ground", "GroundDriftFadeSpeed", 15f, "0.5.2: the drift limit fades in from this ground speed (m/s) to 10 m/s above it (default 15-25 m/s = 29-49 kt), so tight taxi turns keep their natural sideslip.");
			cPitchFight = dev.Bind("4. Gains", "RollPitchFightCost", 1f, "1.8.42: while you roll, a stabilator pitching the nose up (trailing edge up) while the nozzles pitch it down to cancel costs the allocator this x its pitch acceleration squared (rad/s^2), so it rolls with less stab trailing-edge up and the nozzles keep their nose-down travel instead of the roll rate. Near the stabs' roll reversal (the trailing-edge-down stab stalled) one stab went fully trailing-edge up with the other near neutral and the nozzles pinned nose-down. Only that pattern counts (stabs nose-down with nozzles nose-up, the roll-pitch-priority trim, is left alone); off while the pitch-rate command is moving, while the nose comes up faster than commanded, in AoA protection and on AoA run-away. Bench vs 1.8.41: reversals at 19 deg AoA / 300 km/h nozzle min -20 -> -13, AoA over 1.2 -> 0.5 deg; roll stop at 200 km/h -14 -> -10; 2.5 t store at 600 km/h -20 -> -12, over 2.9 -> 0.9; at 26 deg a stab past 35 deg local AoA 32 % -> 0 % of the roll. Costs: reversal at 13 deg / 600 km/h over 3.9 -> 8.3 deg, 10 deg / 300 km/h 1.1 -> 2.7; bank at 1 s at 19-22 deg AoA 42 -> 36 and 28 -> 20 deg. 0 = off (1.8.41).");
			cRotRate = dev.Bind("2. Envelope", "RotationRate", 8f, "Only with GroundRateLaw = false: takeoff / landing rotation (main wheels on the ground, nose wheel up): full stick commands this pitch rate (deg/s) and neutral stick holds the nose attitude.");
			cRollTvc = dev.Bind("4. Gains", "RollNozzleHold", 0f, "1.8.31: default 0 (the handover kept both stabs trailing-edge down for whole roll cycles and bounced the nozzles in roll spam; with ±20 deg the nozzles now take the roll trim). Low-AoA rolls with no pitch command: the stabs take over the roll surfaces' pitch-moment change so the TVC stays near its pre-roll trim. Below ~150 m/s IAS the nozzles still catch the fast part and hand the sustained part to the stabs as far as the stabs have travel to spare; above it the nozzles simply hold still. 1 = full, 0 = off (nozzles trim the roll alone, 1.7.0 behaviour).");
			cAsymPitchMargin = dev.Bind("4. Gains", "AsymPitchMargin", 20f, "1.8.37: with an asymmetric load (lateral CG offset above ~8 cm) the asymmetric-load AoA cap is also held below the AoA where the jet runs out of nose-down authority - full nose-down stabs plus the nozzles at the thrust you have - minus this margin (deg/s^2), less 2 deg. On Aryx's airframe that point is only ~18-19 deg AoA at idle (the stabs stall trailing-edge down), so at idle the old cap (up to ~21 deg) sat right on the edge and any overshoot pitched up into a departure. Throttle up and the nozzles raise it again. Never below 10 deg. 0 = off (1.8.36).");
			cAsymRollCoupling = dev.Bind("4. Gains", "AsymRollCoupling", true, "1.8.37: with an asymmetric load, the roll-coupling limit (CouplingRollLimit) also works at a held AoA, not just while AoA is building past 12-22 deg, and pitch gets priority over roll as soon as AoA overshoots the asymmetric-load cap. Rolling toward the heavy wing at 12-20 deg AoA and idle used to reach 100-160 deg/s and then pitch up past the cap it could not stop in. Clean jets are unaffected. false = 1.8.36.");
			cRollPitchPriority = dev.Bind("4. Gains", "RollPitchPriority", true, "1.8.40: while you roll (below ~18-26 deg AoA), the nozzles stay within RollTvcBudget of their pre-roll trim and the pitch moment the roll makes is taken by the stabilators, with roll rate given up where they cannot. The allocation is re-solved with the flaperons' true pitch moment and the stabs kept on the attached side of their lift peak (past it the down-going stab turns nose-down into pitch-up), pitch and yaw weighted 10x over roll. Accepted only if the modelled pitch error stays within 4 deg/s^2 of what free nozzles would give; otherwise the band widens (x3, x8) or the normal allocation is kept. The nozzles are let go in AoA protection / overshoot past the AoA or asymmetric-load limit and briefly on roll stops and reversals. Bench cost: up to ~20 % roll rate at 15-20 deg AoA, ~35 % in a 1 g roll at 250 km/h (19 deg AoA); none above ~350 km/h at 1 g. RollTvcBudget sets the band: raise it to trade nozzle travel back for roll rate. false = 1.8.39.");
			cRollFightCost = dev.Bind("4. Gains", "RollFightCost", 5f, "1.8.41: while the pedals are in (and 1 s after), the allocator may not split the horizontal surfaces against each other: a group (stabilators, outboard flaperons, inboard flaperons) whose roll opposes the net roll of the others costs this x its roll acceleration squared. With full pedal the rudders reach their stops and the allocator used to buy a few deg/s^2 of yaw from the flaperons' drag by rolling them one way and the stabs the other (roll cancelled) - the large, reversing stab/flaperon deflections on pedal inputs, and all of the yaw once the vertical tails are shot off. Rolls without pedal are allocated exactly as in 1.8.40. Bench, full pedal at 340 km/h: stab-vs-flaperon roll fight mean 24 -> 4 deg/s^2; tails shot off: 79 -> 9. 0 = off (1.8.40).");
			cRollPitchReserve = dev.Bind("4. Gains", "RollPitchReserve", 0.15f, "1.8.41: while you roll, the roll-rate limit and roll-entry acceleration yield when the TVC nozzles have less than this fraction of their nose-down travel (nose-up at negative AoA) left at the live thrust, and come back as the margin returns. Above the RollPitchPriority band (18-26 deg AoA) the stabs lose nose-down authority when they roll (the down-going stab stalls) and the nozzles carry the roll's pitch-up alone: they sat on the -20 deg stop for whole rolls at 22-32 deg AoA, with or without a store. Bench, full roll at 26 deg AoA, AB, 450 km/h: nozzles on the stop 85 % -> 26 % of the roll, bank at 2 s 110 -> 87 deg; with an 800 kg store at the 22 deg cap: 35 % -> 18 %, 87 -> 77 deg. No change below ~18 deg AoA. Higher = more nozzle margin, less roll rate. 0 = off (1.8.40).");
			cRollTvcBudget = dev.Bind("4. Gains", "RollTvcBudget", 1.5f, "1.8.34: 1.5 (1.8.33: 2, 1.8.32: 4). While you roll or yaw, the TVC nozzles stay within this many degrees of where they were before the lateral input (the pitch trim for your own pitch command), and the stabilators take the pitch moment the roll itself makes (mostly the flaperons' lift change). It is an allocator cost, not a clamp: if the stabs cannot supply it (on a stop), the nozzles still go further. The limit opens up to full nozzle travel between 14 and 20 deg AoA (the down-going stab is then near its stall and nose-down from it costs roll rate), between T/W 0.7 and 1.1 (with afterburner the roll trim is only a few degrees and the nozzles stay the fast pitch effector), in AoA protection / run-away and while the pitch-rate command is moving. Cost on the bench: 3-10 % roll rate in 300-350 km/h part-throttle roll spam, about 1 % bank at 1 s in single rolls. 0 = off (1.8.31: the nozzles take the whole roll trim, down to -20 deg at part throttle).");
			cRollTvcBudgetCost = dev.Bind("4. Gains", "RollTvcBudgetCost", 8f, "1.8.34: 8 (1.8.33: 6, 1.8.32: 2). Allocator cost per (degree / 10)^2 of nozzle travel beyond RollTvcBudget. Higher = a harder budget.");
			cRollWash = dev.Bind("4. Gains", "RollNozzleWashout", 0.4f, "Time constant (s) separating 'fast' (nozzles) from 'sustained' (stabs) in RollNozzleHold. Lower = the stabs take over sooner.");
			cSlatStart = dev.Bind("5. Airframe", "SlatAoAStart", 2f, "AoA (deg) at which the slats start coming down. The high-lift device has no aero model beyond the wing area it adds, and the stock AoA term is a step rather than a schedule, so the FCS drives it off AoA directly.");
			cSlatFull = dev.Bind("5. Airframe", "SlatAoAFull", 20f, "AoA (deg) at which the slats are fully down. They then stay down to HighLiftMaxAoA.");
			cHlMaxAoA = dev.Bind("5. Airframe", "HighLiftMaxAoA", 70f, "The slats (high-lift device) stay deployed up to this |AoA|, fading over the 10 deg below it. They used to be tied to the flaperon's trailing-edge-down schedule and so retracted by 30 deg AoA. The device's mechanical TE-down swing is subtracted by the flap drive, so the net flaperon bias is unchanged: only the slats and the wing area they add stay out.");
			cHlHighAoA = dev.Bind("5. Airframe", "HighLiftHighAoAShare", 1f, "How much of the slat deployment is kept at high AoA (0..1). 0 = pre-1.8.0 behaviour (slats retract with the flap schedule).");
			cOuterFlap = dev.Bind("5. Airframe", "OuterFlaperonDroop", 0f, "Revoker 0.1.0: 0 (F-22E/Ifrit 0.6). How far the trailing-edge flaperons droop TE-down with the flap schedule (fraction of the inboard flap angle, stall-capped, roll-biased). 0 = the trailing edges stay at neutral except for roll, and the canards carry a held turn's pitch (with the nozzle drawn to neutral by TvcNeutralHold).");
			cStabRoll = dev.Bind("4. Gains", "StabilatorRollCost", 0.0005f, "Allocator cost of differential (roll) stabilator use at attached flow. Lower = stabs do more of the roll.");
			cStabRollShare = dev.Bind("4. Gains", "StabilatorRollShare", 0.55f, "Share of the differential-stab roll moment counted as usable roll authority (sets roll rate ceiling / acceleration).");
			cOnset = dev.Bind("4. Gains", "PitchOnsetLimit", 150f, "High-speed pitch pacing: pitch-rate command acceleration <= this / lift slope (g per deg). Lower = gentler at high dynamic pressure.");
			cRollPitchFF = dev.Bind("4. Gains", "RollPitchFeedForward", 1f, "Corrects the pitch target by the pitch moment the roll surfaces are about to make (their pitch side-effect is not in the allocator's model), so the stabilators and nozzles move with a roll instead of chasing it afterwards. Mostly visible at low IAS, where the nozzles are the fast pitch effector and the chase shows as nozzle twitch in rolls. 0 = off (pre-1.7.12 behaviour).");
			cLowQReg = dev.Bind("4. Gains", "LowQAllocatorRelief", 1f, "Scales the allocator's regularisation (neutral preference, move cost, pair costs) with dynamic pressure squared, capped at the high-speed value. The cost's error term grows with q^2 and the regularisation did not, so below ~150 m/s the surfaces were held near neutral and near their last position: weak roll and sluggish reversals at low IAS. 0 = pre-1.8.0 behaviour.");
			cRollStop = dev.Bind("3. Lateral-directional", "RollStopFactor", 1f, "Roll-rate ceiling is also limited to this many seconds of the measured roll acceleration (the roll equivalent of YawStopFactor). Since roll acceleration is moment / inertia, this is what makes the roll-rate limit follow a heavy load, especially one far from the CG. 0 = off.");
			cRollAccUse = dev.Bind("3. Lateral-directional", "RollAccelUse", 0.5f, "Share of the roll authority planned for acceleration at and above ~110 m/s IAS (the rest is reserve for regulation against roll damping).");
			cRollAccUseLow = dev.Bind("3. Lateral-directional", "RollAccelUseLowIAS", 0.8f, "Same at and below 60 m/s IAS, blending to RollAccelUse by 110. Roll damping is small down there and the surfaces are far from saturation, so more of the authority can go into the manoeuvre - this is what makes a low-speed reversal crisp.");
			cFFMax = dev.Bind("4. Gains", "RollPitchFeedForwardMax", 30f, "Ceiling on the roll->pitch feed-forward, in deg/s^2 of pitch acceleration. It is an anticipation rather than a measurement, and at high dynamic pressure the roll surfaces' pitch moment is large enough that an error in it moves real g.");
			cFFStabCost = dev.Bind("4. Gains", "RollPitchFeedForwardStabCost", 1f, "During the feed-forward pass, symmetric (pitch) stabilator deflection costs this much more, so the correction lands on the nozzles instead of showing up as a large mean stabilator deflection under the roll's differential. 1 = no preference.");
			cRollPitchFFAoA = dev.Bind("4. Gains", "RollPitchFeedForwardMaxAoA", 35f, "RollPitchFeedForward fades out over the 15 deg below this |AoA| and is off above it: post-stall the stabilators have no pitch authority to redistribute and the pass would only compete for the surfaces holding the jet in yaw.");
			cCoupFF = dev.Bind("4. Gains", "InertialCouplingFF", 0f, "Feed-forward of the gyroscopic roll/pitch/yaw coupling (w x Iw) between commanded and present rates. 0 = off (default since 1.8.14: evaluated at the commanded rates it arrives as a nose-down step the instant you roll, before the jet has the rates; the INDI loop already measures the real coupling).");
			cRelPitchPrio = dev.Bind("4. Gains", "ReleasedStickPitchPriority", 30f, "Stick released (zero-rate hold): when the pitch rate starts to leave the hold, the allocator weights pitch this many times harder against roll/yaw for the stabilators, so a roll cannot pull them into a nose-down shove. Scales in with the pitch-rate error (1 to 9 deg/s) and out with stick (fully off from 18% stick) and with stabilator stall. 1 = off (pre-1.8.14).");
			cWashSlew = dev.Bind("4. Gains", "RollNozzleWashoutSlew", 5f, "RollNozzleHold hands the nozzles' pitch trim to the stabs during rolls. While a stab is on its stop that handover is blocked and builds up; this caps how fast it is then applied (deg of nozzle per second), so it cannot all land in one frame the moment the stabs come off the stop - which is when they are braking the roll. 0 = uncapped (pre-1.8.15).");
			cMomLead = dev.Bind("4. Gains", "ReleasedStickMomentLead", 0.035f, "1.8.31: 0.035 (was 0.07; with any control delay 0.07 rang for 1-2 s after a pitch stop). Stick released, zero-rate hold established: the INDI pitch loop leads its measured effector moment by this many seconds of its own rate of change, covering the servo/filter lag when the trim moment is ramping (AoA bleeding off after a hard pull). Fades in over 0.2 s; off with stick, while the pitch-rate command is moving, in AoA protection and with stab stall. 0 = off (pre-1.8.16). Above ~0.1 s it starts to wobble.");
			cStabTrim = dev.Bind("4. Gains", "StabilatorTrimCost", 0.0002f, "Revoker 0.1.0: 0.0002 (F-22E/Ifrit 0.004): the canards carry the steady pitch (held turns, rolls) and the nozzle the transients. Allocator cost of symmetric (pitch) stabilator deflection relative to the nozzles. Higher = steady trim / sustained pitch rate carried by TVC, stabs kept for transients and whatever TVC cannot hold.");
			cUnloadDecel = dev.Bind("4. Gains", "UnloadDecel", 20f, "Easing the stick: pitch-rate deceleration (deg/s^2) toward the rate the aircraft will sustain at the new AoA/G. Lower = smoother.");
			cUnloadRise = dev.Bind("4. Gains", "UnloadRiseGain", 1.5f, "Easing the stick: if AoA still rises during the decel, the pitch-rate target drops by this x AoA rate (to zero at most).");
			cGUnder = dev.Bind("4. Gains", "GUndershootCapture", 1f, "Easing out of a hard pull at high dynamic pressure, the pitch rate is bounded so the load is captured at the commanded G instead of continuing past it into negative g - the same kinematic capture the load limiter uses, applied from above the target. Lower = a softer bound (0.5 allows twice the nose-down rate); 0 = off (pre-1.8.10 behaviour). Only in the G-command regime.");
			cKpG = dev.Bind("4. Gains", "GGain", 2.5f, "G command bandwidth (1/s): load error is converted to AoA error through the live lift slope.");
			cKiG = dev.Bind("4. Gains", "GIntegral", 1f, "G command integral gain (1/s^2), removes steady load error.");
			cActuator = dev.Bind("5. Airframe", "ActuatorSpeedScale", 1f, "Servo speed multiplier for every control surface and the TVC nozzles (Aryx's servos run at 70 deg/s; 1.428571 = 100 deg/s).");
			cStabUp = dev.Bind("5. Airframe", "StabilatorRangeTEUp", 40f, "Vortex 0.3.12 (Tom, 17 Nov 17:09: wing surfaces 40): the inboard trailing-edge surfaces (elevator_L / _R, the stabilator kind on this jet), trailing edge up travel 40 deg (0.1.0-0.3.11: 30; stock pitch range 40; a file still holding 30 is moved once). Revoker 0.2.19: 60 (0.2.17-0.2.18: 70; 0.2.16: 80; 0.2.7-0.2.15: 45; stock 40; migrated once). Canard travel, trailing edge up = leading edge down (deg, up to 80). The + side of the canard telemetry (canL / canR): at +45..70 deg AoA a +40 deg canard sits at ~+8 deg local AoA. On the runway full push and the rollout brake each go to this stop. 0 = the stock travel.");
			cStabDn = dev.Bind("5. Airframe", "StabilatorRangeTEDown", 40f, "Vortex 0.3.12: the inboard trailing-edge surfaces, trailing edge down travel 40 deg (0.1.0-0.3.11: 30; a file still holding 30 is moved once). Revoker 0.2.7: 45 (stock 40; 0.2.14-0.2.15 had 80 here, the wrong side). Canard travel, trailing edge down = leading edge up (deg, up to 80): the - side of the canard telemetry, the side the canards sit on in a push (at -45..-56 deg AoA a -40 deg canard sits at ~-14 deg local AoA). AoALimitNegative 55 was set with this at 45 (stock 40: -50). 0 = the stock travel.");
			cFlapRange = dev.Bind("5. Airframe", "FlaperonRange", 40f, "Vortex 0.3.12: the outboard trailing-edge surfaces (aileron_L / _R) +-40 deg (0.1.0-0.3.11: 30; stock pitch range 40; a file still holding 30 is moved once). Ifrit 0.3.0: travel each way for the ailerons (outer) and decelerons (inner) (deg). Stock: ailerons 30, decelerons 20. 0 = stock.");
			cServoSpeed = dev.Bind("5. Airframe", "SurfaceServoSpeed", 100f, "Ifrit 0.3.0: servo speed of every control surface (deg/s), the F-22E FCS's 100. Stock: stabilators 60, ailerons 45, decelerons 70. 0 = the stock speeds x ActuatorSpeedScale. TVC: TvcSlewRate.");
			cSplitRange = dev.Bind("5. Airframe", "SplitRange", 0f, "Ifrit 0.3.0: deceleron split limit (deg), for the airbrake and for split yaw. 0 = the decelerons' own travel (FlaperonRange, 30); the stock limit is 25. Split drag is split x splitDrag x rho x v^2, so 30 is 20 % more than 25.");
			cTvcRange = dev.Bind("5. Airframe", "TvcPitchRange", 15f, "Vortex 0.3.13 (Tom, 17 Nov 19:05): 15 (0.1.0-0.3.12: 0; a file still holding 0 is moved once). Pitch thrust-vectoring travel each way (deg, game maximum 20: the game's nozzle clamps its angle at 20, so 15 moves the thrust and the nozzle mesh without hitting it). 0 = Aryx's original (10). Next spawn.");
			cTvcSpeed = dev.Bind("5. Airframe", "TvcSlewRate", 75f, "TVC nozzle slew rate (deg/s). 0 = follow ActuatorSpeedScale like the control surfaces (70 x scale).");
			cStabPeakMargin = dev.Bind("5. Airframe", "StabPeakMargin", 90f, "2.0.11: default 90 (off, 2.0.8 behaviour). 2.0.10: below 40 deg body AoA a stab is not commanded more than this many degrees past its lift peak (local AoA, either sign); the limit opens up from 40 to 50 deg AoA (deep-stall use above, e.g. falling leaf, unchanged). Stops the stabs swinging through deep stall in rolls at 36 deg and parking past the peak after a roll (10:00 flight). Bench, full roll at 36 deg with TVC off: stab rate 40 -> 16 deg/s rms, roll rate 27-33 -> 19-25 deg/s; with TVC on the roll rate is within 5 %. 90 = off.");
			cTvcOff = dev.Bind("0.1 Debug - Airframe test", "DisableTVC", defaultValue: false, "2.0.8 test switch: nozzles locked at 0 deg pitch at every throttle, and the FCS flies without them (pitch from the stabs, flaperons and rudders only). With AoALimit Normal the low-thrust caps (NoTvcAoACapPositive / Negative, +-36) then apply (the nozzles never cover pitch). All Ifrits on this machine. Takes effect at once.");
			cAoATest = dev.Bind("0.1 Debug - Airframe test", "AoALimit", AoALimitTest.Normal, "2.0.8 test switch for the AoA limits. Normal = AoALimitPositive / Negative (+65 / -45), capped at NoTvcAoACapPositive / Negative when the nozzles do not cover pitch (low thrust or DisableTVC). Full65 = +65 / -45 and those caps off (the swept authority cap, which stops the FCS pulling past where the stabs can still push the nose down, stays on; LowThrustAoACap = false removes that too). Cap36 = the NoTvcAoACap values in every condition. Takes effect at once.");
			cStabsPitchOnly = dev.Bind("0.1 Debug - Airframe test", "StabsPitchOnly", false, "2.2.5 test switch: the stabilators move symmetrically only (pitch), with no roll or yaw job, so roll and yaw come from the flaperons and rudders alone. For checking what the flaperons can do. The lateral rate limits still count the stabs' authority, so at high AoA the FCS will ask for more roll/yaw than the jet then has (expect slower rolls and more sideslip there). Off for AI. Takes effect at once.");
			cNoTvcCapPos = dev.Bind("0.1 Debug - Airframe test", "NoTvcAoACapPositive", 45f, "2.2.3: default 45 (2.0.9-2.2.2: 36). Positive AoA cap (deg) while the nozzles do not cover pitch (T/W under LowThrustFixedAoATW, or DisableTVC). Also the positive limit with AoALimit = Cap36. Takes effect at once.");
			cNoTvcCapNeg = dev.Bind("0.1 Debug - Airframe test", "NoTvcAoACapNegative", 45f, "2.2.3: default 45 (2.0.9-2.2.2: 36). Negative AoA cap magnitude (deg) under the same conditions (2.0.8 had none: -45 always). Also the negative limit with AoALimit = Cap36. Takes effect at once.");
			cNoTvcThrCapPos = dev.Bind("0.1 Debug - Airframe test", "NoTvcThrustAoACapPositive", 50f, "Revoker 0.2.5: 50 (canards + elevons; bench, TVC off, full push from the cap on afterburner recovers below 20 deg in 2.1-2.3 s at 180-300 km/h; a fixed 50 at idle did not recover, so NoTvcAoACapPositive stays 45; 55+ did not recover at 220 km/h). F-22E: 2.2.2: with DisableTVC, the positive AoA cap (deg) once the thrust is up: NoTvcAoACapPositive (36) up to T/W 0.8, this value from T/W 1.0 (afterburner), linear between. Bench: with afterburner the stabs alone hold 45 deg at 160-750 km/h, rolls and reversals included (sideslip 6 deg or less); at part or idle throttle, rolling as the speed bleeds through ~200-250 km/h, 45 departed to 48-62 deg in 5 of 20 cases where 36 held. Takes effect at once.");
			cNoTvcThrCapNeg = dev.Bind("0.1 Debug - Airframe test", "NoTvcThrustAoACapNegative", 45f, "2.2.2: with DisableTVC, the negative AoA cap magnitude (deg) at every thrust (NoTvcAoACapNegative still applies at low thrust with the nozzles working). Bench: -45 held at 200-450 km/h, idle to afterburner, with a roll, sideslip 5.4 deg or less. Takes effect at once.");
			cAirframeRev = dev.Bind("5. Airframe", "AirframeRevision", 13, "Internal: airframe-default revision (inherited from the Ifrit FCS). Leave it alone.");
			cFuelUse = dev.Bind("5. Airframe", "FuelConsumptionScale", 0.75f, "Fuel burn multiplier at every throttle setting, afterburner included (0.65 = 35 % less fuel used per throttle setting). Buffed PerformanceProfile only.");
			cThrust = dev.Bind("5. Airframe", "ThrustScale", 1.12f, "Engine thrust multiplier (dry staticThrust and afterburner thrust alike). Vortex 0.1.1: 1.12 (0.1.0: 1.08; a file still holding 1.08 is moved once). Buffed PerformanceProfile only. Fuel flow per power setting is unchanged by this; see FuelConsumptionScale.");
			cDrag = dev.Bind("5. Airframe", "ParasiticDragScale", 0.885f, "Multiplier on every part's parasitic drag area (1 = stock). Revoker 0.1.0: 0.90 (10 % less parasitic drag). Buffed PerformanceProfile only.");
			cDrAoA = dev.Bind("5. Airframe", "AoADragRelief", 0.25f, "Revoker 0.1.0: 0 (off). Buffed profile only: post-stall AoA drag relief (F-22E 2.2.3 feature for Aryx's airfoils). The Revoker keeps its stock drag polar.");
			cDrTrans = dev.Bind("5. Airframe", "TransonicDragRelief", 0f, "Revoker 0.1.0: 0 (off). Buffed profile only: Mach drag relief (F-22E 1.8.38 feature). The Revoker keeps its stock transonic drag.");
			cStockDrAoA = dev.Bind("5. Airframe", "LowAoADragRelief", 0.35f, "Revoker 0.1.0: 0 (off). Both profiles: fraction of each part's AoA-dependent drag removed at low AoA (F-22E 2.2.3, a fix for Aryx's airfoil drag). The Revoker keeps its stock drag polar.");
			cStockDrFade0 = dev.Bind("5. Airframe", "LowAoADragReliefFadeStart", 25f, "2.2.3: local AoA (deg) up to which LowAoADragRelief applies in full (both profiles).");
			cLatMargin0 = dev.Bind("3. Lateral-directional", "HighAoALateralMarginStart", 25f, "Ifrit 0.3.1: AoA (deg) where HighAoALateralBrakeMargin (main cfg) starts to blend in.");
			cLatMargin1 = dev.Bind("3. Lateral-directional", "HighAoALateralMarginFull", 30f, "Ifrit 0.3.1: AoA (deg) from which HighAoALateralBrakeMargin applies in full (smoothstep in between).");
			cSplitBudget = dev.Bind("5. Airframe", "DeceleronSplitBudget", true, "Ifrit 0.3.1: the deceleron's deflection and its split share one travel: the game turns the two halves to deflection +- split, so a 30 deg deflection with a 30 deg split put one half at 60. Now |deflection| + split <= the deceleron travel (FlaperonRange); the deflection comes first and the split (airbrake and split yaw) gets what is left. false = 0.3.0 (they add).");
			cDragBump = dev.Bind("5. Airframe", "AoADragBump", 0f, "Revoker 0.1.0: 0 (off). Ifrit 0.3.1 feature: extra drag over a band of body AoA.");
			cDragBump0 = dev.Bind("5. Airframe", "AoADragBumpStart", 18f, "Ifrit 0.3.1: body |AoA| (deg) where the AoADragBump starts rising.");
			cDragBump1 = dev.Bind("5. Airframe", "AoADragBumpPeak", 24f, "Ifrit 0.3.1: body |AoA| (deg) of the full AoADragBump.");
			cDragBump2 = dev.Bind("5. Airframe", "AoADragBumpEnd", 35f, "Ifrit 0.3.1: body |AoA| (deg) by which the AoADragBump is gone.");
			cStockDrFade1 = dev.Bind("5. Airframe", "LowAoADragReliefFadeEnd", 30f, "2.2.3: local AoA (deg) by which the relief has gone over to its post-stall value: none in Stock, AoADragRelief in Buffed.");
			cAsymOn = dev.Bind("Asymmetric load", "AsymLoadAoALimit", defaultValue: true, "Cap the AoA when an asymmetric load (e.g. one heavy store on one wing) would use more of the roll/yaw control authority than AsymLateralReserve leaves free. The cap is the stick's full-travel AoA (the stick is remapped onto it). A load within the margin is simply trimmed out by the control loop (no cap). Not applied in MPO.");
			cAsymRollHead = dev.Bind("Asymmetric load", "AsymRollHeadroom", defaultValue: true, "The roll-rate and roll-acceleration limits count only the roll authority left in each direction after the measured asymmetric trim (asymmetric store, wing damage). The trim is learned from the measured non-control roll moment minus roll damping while the jet is not rolling (under 20 deg/s) and held during rolls; it only ever lowers the limit on the loaded side. With the left wing shot off it stops a full right roll at the AoA cap from saturating the stabs and rudders, building sideslip and departing. No effect on a symmetric jet. false = pre-1.8.17.");
			cAsymReserve = dev.Bind("Asymmetric load", "AsymLateralReserve", 0.35f, "Share of the roll/yaw control authority kept free for the pilot's roll and pedal commands (0..1). Used together with AsymRollReserveAccel / AsymYawReserveAccel: whichever asks for more sets the cap. Higher = lower cap, more roll/yaw left.");
			cAsymDead = dev.Bind("Asymmetric load", "AsymDeadbandAccel", 5f, "Asymmetric roll acceleration (deg/s^2) ignored before any cap is considered - the airframe is never perfectly symmetric even clean (off-centre gun and ammunition, engines never exactly matched). The yaw deadband is a quarter of this. A balanced loadout stays well inside it; one 300 kg store is far outside it.");
			cAsymRollAcc = dev.Bind("Asymmetric load", "AsymRollReserveAccel", 80f, "Roll acceleration (deg/s^2) that must stay available to the pilot after the asymmetric load is trimmed out, whatever AsymLateralReserve works out to. Post-stall a share of the remaining authority is almost nothing, so this absolute floor is what sets the cap up high and at low speed. Higher = lower cap.");
			cAsymYawAcc = dev.Bind("Asymmetric load", "AsymYawReserveAccel", 20f, "Same for yaw acceleration (deg/s^2).");
			cAsymFF = dev.Bind("Asymmetric load", "AsymFeedForward", 0f, "DEFAULT 0 (off) since 1.8.17. Hands the CHANGE in the modelled asymmetric moment straight to the allocator, washed out over AsymFeedForwardWashout seconds. Inside the INDI loop an added moment is not a one-off kick: the loop measures the acceleration it causes and settles with a steady roll-rate error of about FF/(I x KRoll) for as long as the term lasts, and while the load keeps changing (pulling, levelling off) it lasts the whole time. With a wing shot off it gave a 20-35 deg/s roll drift in pulls and level-offs and helped depart the jet. The loop already measures the asymmetric moment. 1 = pre-1.8.17.");
			cAsymFFTau = dev.Bind("Asymmetric load", "AsymFeedForwardWashout", 1f, "Time constant (s) over which the feed-forward above hands the load back to the control loop.");
			cAsymFFBeta = dev.Bind("Asymmetric load", "AsymFeedForwardMaxSideslip", 6f, "AsymFeedForward fades out over the 12 deg of sideslip above this and is off beyond it: the asymmetric moment it is built from is modelled at zero sideslip, so at large sideslip (falling leaf) it is not a measurement of anything.");
			cRevRoll = dev.Bind("3. Lateral-directional", "ReversedFlowRollSense", defaultValue: true, "Flying backwards: the velocity-vector roll maps to body roll through cos(AoA), which flips sign past 90 deg, so the stick would roll the jet the other way from the pilot's seat. This flips the roll command with it, blended smoothly from ReversedFlowSenseStart to ReversedFlowSenseEnd (0.3.4; 0.3.2 flipped at once at 100 deg AoA, released at 80). Pitch and pedal are unaffected - the control loop already handles reversed flow on the surfaces themselves.");
			cRevNose = dev.Bind("3. Lateral-directional", "ReversedFlowNoseAxis", defaultValue: true, "1.4.0 (F-22E 4.3.0): flying backwards, the lateral stick and the pedals turn the nose the way they point (left = nose left and roll left, seen from the seat), on one axis that blends with the AoA: mostly yaw near 90 deg AoA, mostly roll near 180 - forward flight mirrored about the wings, continuous through 90 deg (replaces the ReversedFlowSenseStart/End blend). The sideslip hold no longer fades out in reversed flow at speed (its low-TAS fade stays). Needs ReversedFlowRollSense on. False = 1.3.1. Takes effect at once.");
			cAsymFFAoA = dev.Bind("Asymmetric load", "AsymFeedForwardMaxAoA", 40f, "AsymFeedForward fades out over the 15 deg of AoA above this and is off beyond it. Post-stall the lateral authority it would spend is what the manoeuvre needs.");
			cAsymRollTau = dev.Bind("Asymmetric load", "AsymRollTau", 0.5f, "The roll reserve also has to cover the roll rate the law allows at this speed, reached in this many seconds. At high IAS that is what sets the reserve (250 deg/s in 0.5 s = 500 deg/s^2), not AsymRollReserveAccel. 0 = off (fixed reserve only).");
			cAsymRelaxBoost = dev.Bind("Asymmetric load", "AsymCapRelaxBoost", 4f, "The cap relaxes faster the further the demand is below the limit: rate = AsymCapRelaxRate x (1 + this x margin). With the load gone the cap comes back at (1 + this) x the base rate. 0 = fixed rate.");
			cAsymAoAWeight = dev.Bind("Asymmetric load", "AsymAoAWeight", 1f, "0.5.2: with an asymmetric load the AoA limit is where AoA^AsymAoAWeight x the roll the jet can still use (the law's roll, held to what the weaker side can stop) is largest. Higher = keep more AoA and accept less roll into the heavy side; lower = give up more AoA for roll. No load: no effect.");
			cAsymStopTime = dev.Bind("Asymmetric load", "AsymStopTime", 0f, "Per-jet default (shared asymmetric-load code, option 3): 0.8 on the Su-30, 0 (off) elsewhere. 0.5.1: with a lateral CG offset (or a damage asymmetry) the flight-path roll rate the stick may ask toward each side is held to what the roll reserve on its braking side (from the coupled check, at the present AoA) can stop in this many seconds, 10 deg/s at least. Lower = slower rolls into the heavy side at high AoA and low speed, and a roll that stops sooner once the stick is centred. 0 = off (0.5.0).");
			cAsymBoost = dev.Bind("Asymmetric load", "AsymAxisPriorityBoost", 0f, "With more than half the lateral authority taken by an asymmetric load, the allocator's roll and yaw weight is raised by up to this factor (1 = up to double) so the surfaces hold the jet straight before they fine-tune pitch. DEFAULT 0 (off) since 1.7.11: it starves the pitch axis, which showed up as the nozzles running nose-down in rolling pulls.");
			cAsymFloor = dev.Bind("Asymmetric load", "AsymMinAoACap", 10f, "Per-jet default: 10 on the Vortex (0.3.2; 12 in 0.3.0-0.3.1, moved once if it was exactly 12), 12 on the other jets. The asymmetric-load cap never goes below this positive AoA (deg). Vortex bench, 1760 kg on one wing, full aft + full roll away from the load at 470 km/h: 12 = yaw saturated, 19 deg sideslip, 124 deg of bank in 5 s; 10 = 4 deg, 316 deg. Cost: the slowest speed the loaded jet holds 1 g on the cap rises ~257 -> ~275 km/h.");
			cAsymFloorNeg = dev.Bind("Asymmetric load", "AsymMinAoACapNegative", 8f, "The asymmetric-load cap never goes closer to 0 than this on the negative side (deg, magnitude).");
			cAsymTighten = dev.Bind("Asymmetric load", "AsymCapTightenRate", 10f, "How fast the cap comes down when the asymmetry grows (deg/s).");
			cAsymRelax = dev.Bind("Asymmetric load", "AsymCapRelaxRate", 8f, "How fast the cap goes back up when the asymmetry shrinks (store released, fuel, speed) (deg/s).");
			cAsymRec = dev.Bind("Asymmetric load", "AsymCapRecoveryRate", 15f, "Above the cap (it just came down), AoA is brought back to it at up to this rate (deg/s). Not cancelled by full stick.");
			cAsymMinIas = dev.Bind("Asymmetric load", "AsymMinIAS", 35f, "Below this IAS (m/s) the cap is held at its last value instead of being recomputed (no aero authority to judge by).");
			cAsymCapDemand = dev.Bind("Asymmetric load", "AsymCapDemand", 1.08f, "1.8.45: lateral demand (load share of the authority left after the reserve) at which the asymmetric-load AoA cap sits (1.8.44 and before: 1.0). With a light mixed load (6.5 cm lateral CG offset) the demand rises ~0.025 per degree above the cap, so 1.08 lets ~3 deg more AoA; heavier loads, whose demand rises faster with AoA, gain less.");
			cAsymCplResMax = dev.Bind("Asymmetric load", "AsymCoupledReserveMax", 0.7f, "1.2.0: with a lateral CG offset, vectoring on and the thrust up (T/W over LowThrustFixedAoATW) the AoA cap comes from the coupled check: at each AoA, the most flight-path roll each way with the load held, pitch held and no sideslip acceleration, every surface and both nozzles counted. This is the largest share of that roll range the pilot's reserve may claim; the cap sits where the weaker side is down to about this share of the mean of the two sides. Bench, 30 cm of offset at 14.2 t, full aft stick on afterburner, 240-610 km/h: 0.8 = cap 21-22 deg, no drift; 0.7 = 28 deg, drift under 1 deg/s; 0.65 = 31-36 deg, 0-4 deg/s; 0.6 = 32-38 deg, 1-3 deg/s; 0.5 = 36-45 deg, 2-4.5 deg/s. Higher = lower cap, more roll left. Takes effect at the next sweep (0.2 s).");
			cAsymCplResMaxNt = dev.Bind("Asymmetric load", "AsymCoupledReserveMaxNoTvc", 0.8f, "1.3.0: the same share for the coupled check with the nozzles locked, cut out by speed or the thrust low (T/W under LowThrustFixedAoATW). There the surfaces also make the pitch-in, so more of the roll range is kept. Bench, 30 cm of offset, nozzles locked, full aft stick on afterburner: 0.7 = cap 21-25 deg, but a full pull at 500-550 km/h ran 6-9 deg past it with 13 deg of sideslip; 0.8 = 15-21 deg, at most 2 deg past it, sideslip 1.4 deg or less; 0.85 = 12-18 deg. Higher = lower cap, more roll left. Takes effect at the next sweep (0.2 s).");
			cAsymCapSmooth = dev.Bind("Asymmetric load", "AsymCapSmoothing", 1f, "1.8.45: time constant (s) with which the asymmetric-load cap may rise. Each 0.2 s sweep's cap goes through a median of the last three and then this low-pass; a lower cap is followed with 0.3 s. The raw cap moved 1-8 deg every sweep (4.4 Hz) with a 6.5 cm lateral offset at 35-40 deg AoA, which is what made full-aft stick twitch.");
			cDecelLook = dev.Bind("Asymmetric load", "DecelLookahead", 1f, "1.8.45: seconds of IAS decay projected ahead (filtered IAS rate, rapid decelerations only, see DecelStart/DecelFull). The asymmetric-load cap is judged, and the high-AoA roll/yaw rate limits are set, at the dynamic pressure of that predicted IAS, so they come down before the surfaces lose the authority. 0 = off.");
			cDecelStart = dev.Bind("Asymmetric load", "DecelStart", 6f, "1.8.45: IAS deceleration (m/s^2) below which nothing is anticipated. Between this and DecelFull the deceleration is weighted by the square of where it sits, so ordinary turning bleed (and most of a J-turn's tail) predicts almost nothing and a collapsing corkscrew predicts nearly all of it.");
			cDecelFull = dev.Bind("Asymmetric load", "DecelFull", 20f, "1.8.45: IAS deceleration (m/s^2) from which it is anticipated in full.");
			cHiAoABrake = dev.Bind("3. Lateral-directional", "HighAoABrakeFloor", true, "1.8.45: from 25 to 35 deg AoA up, the roll and yaw rate limits may not exceed what the stop-side authority (at the IAS predicted by DecelLookahead) brakes in the stop time of the reference manoeuvre, full pedal + full lateral stick at 65 deg AoA and 280 km/h (RollStopFactor for roll, YawStopFactor x the yaw boost at 280 km/h for yaw). Replaces there the fixed 30 deg/s roll and 20 x boost yaw floors, which outran the authority below ~65 m/s IAS. Costs yaw rate in slow post-stall yaw: bench, 65 deg AoA at 200 km/h 34 -> 23 deg/s, 300 km/h 37 -> 32. false = 1.8.44.");
			cCmdShape = dev.Bind("4. Gains", "CommandShaping", true, "1.9.0: AoA/G command pre-filter. The AoA and G commands are shaped before the loop: a small request (a few degrees or a fraction of a g from where the jet is) builds up gently, a large one runs as before. The shaping speed (1/s) grows with the step from ShapingAoAGainSmall to ShapingAoAGainLarge over ShapingAoASpan (G: ShapingG*), and small steps also ramp their onset over ShapingOnset. The same idea as the self-adjusting pre-filter gain of the T-50 control law (60 % gain below 1 g of error, 100 % above 3 g). false = 1.8.45.");
			cShapeASpan = dev.Bind("4. Gains", "ShapingAoASpan", 15f, "1.9.0: AoA step (deg) from which the AoA command is no longer slowed.");
			cShapeAKSmall = dev.Bind("4. Gains", "ShapingAoAGainSmall", 1.5f, "1.9.0: shaping speed (1/s) of the AoA command for a very small step. Lower = gentler fine tracking.");
			cShapeAKLarge = dev.Bind("4. Gains", "ShapingAoAGainLarge", 10f, "1.9.0: shaping speed (1/s) of the AoA command at and above ShapingAoASpan (fast enough to be transparent).");
			cShapeOnset = dev.Bind("4. Gains", "ShapingOnset", 0.25f, "1.9.0: onset time (s) of a very small AoA/G step (the command's rate builds up over this); fades to 0.03 s at the span.");
			cShapeGSpan = dev.Bind("4. Gains", "ShapingGSpan", 3f, "1.9.0: load step (g) from which the G command is no longer slowed.");
			cShapeGKSmall = dev.Bind("4. Gains", "ShapingGGainSmall", 1.5f, "1.9.0: shaping speed (1/s) of the G command for a very small step (onset and unload).");
			cShapeGKLarge = dev.Bind("4. Gains", "ShapingGGainLarge", 10f, "1.9.0: shaping speed (1/s) of the G command at and above ShapingGSpan.");
			cRateBlend = dev.Bind("2. Envelope", "HighAoARateBlend", true, "1.9.1: AoA command hands over to pitch-rate command past RateBlendAoA. The stick scale is fixed: RateBlendAoA at RateBlendStick (36 deg at 0.5 = 72 deg per full stick); the threshold's stick point moves with the threshold (24 deg -> 0.333). Up to that point the stick is AoA command; past it, still AoA command, on to the AoA limit at full stick (full stick goes through the threshold at the rate budget). Holding the threshold's point holds the threshold AoA. When the jet crosses the threshold with the stick past its point, the stick becomes a pitch-rate command starting from the pitch rate at the crossing, up to the budget at full stick; a full pull or a release switches it to 0..full stick = 0..budget, and a release holds zero rate. Easing below the point stays rate command until the jet is back at the threshold. If the jet comes back to the threshold with the stick still past the point, that stick position is the threshold: easing from it holds the live threshold (soft knee) until the stick meets the threshold's own point on the line. false = AoA command to the limit (1.8.45).");
			cRateBlendStick = dev.Bind("2. Envelope", "RateBlendStick", 0.5f, "1.9.1: stick position of RateBlendAoA; sets the AoA per stick below the threshold (36 / 0.5 = 72 deg per full stick). It stays fixed when the threshold moves.");
			cRateBlendAoA = dev.Bind("2. Envelope", "RateBlendAoA", 36f, "1.9.0: AoA (deg) where the AoA command hands over to pitch-rate command when the wings still lift at least RateBlendLiftG times the weight.");
			cRateBlendAoAMin = dev.Bind("2. Envelope", "RateBlendAoAMin", 25f, "1.9.0: the handover AoA when the wings can lift only the weight (1 g, thrust not counted); in between it is interpolated by the lift-to-weight ratio (dynamic pressure x wing lift / mass). Slower = lower threshold.");
			cRateBlendLiftG = dev.Bind("2. Envelope", "RateBlendLiftG", 2f, "1.9.0: wing lift-to-weight ratio (g, thrust not counted) from which the full RateBlendAoA applies. ~330 km/h clean at combat weight.");
			cRateBlendBand = dev.Bind("2. Envelope", "RateBlendBand", 4f, "1.9.0: AoA band (deg) below the threshold over which rate command and AoA command are blended once the rate command is engaged (pitch rate stays continuous across it).");
			cRateBlendNeg = dev.Bind("2. Envelope", "HighAoARateBlendNegative", true, "1.9.7: the same AoA -> pitch-rate hand-over on the negative side, mirrored: RateBlendAoANegative at -RateBlendStick (-36 deg at -0.5 = 72 deg per full push, the same scale as the positive side), full push = the negative AoA limit, rate command (nose-down) past the threshold, floating hold point. RateBlendStick, RateBlendLiftG and RateBlendBand are shared. false = linear AoA command to the negative limit (1.9.6).");
			cRateBlendAoANeg = dev.Bind("2. Envelope", "RateBlendAoANegative", 36f, "1.9.7: negative hand-over AoA magnitude (deg) while the wings can still push at least RateBlendLiftG times the weight nose-down (negative lift).");
			cRateBlendAoAMinNeg = dev.Bind("2. Envelope", "RateBlendAoAMinNegative", 25f, "1.9.7: negative hand-over AoA magnitude when the wings' negative lift is only the weight; interpolated by the negative lift-to-weight ratio in between.");
			cAuthCap = dev.Bind("2. Envelope", "LowThrustAoACap", true, "1.9.0: the AoA limit is held below the AoA where the nose-down authority you have now runs out: stabilators full nose-down, flaperons at their trim, plus the nozzles at the present thrust (none above the thrust-vectoring cut-off, 450 m/s true airspeed - the high-altitude supersonic case where the nozzles freeze). Bench, 350 km/h: zero thrust 23 deg, ~0.1 T/W 26 deg, 30 % throttle 34 deg, full afterburner no cap; a full pull then full push recovers in every case (1.8.45 departed to 73-90 deg at the first two). The stick is remapped onto the capped envelope like the asymmetric-load cap. false = 1.8.45.");
			cAuthCapMargin = dev.Bind("2. Envelope", "LowThrustCapMargin", 8f, "1.9.0: nose-down pitch acceleration (deg/s^2) that must be left at the cap. Higher = lower cap.");
			cAuthCapFloor = dev.Bind("2. Envelope", "LowThrustCapFloor", 20f, "1.9.0: the low-thrust AoA cap never goes below this (deg).");
			cLowThrustTW = dev.Bind("2. Envelope", "LowThrustFixedAoATW", 0.3f, "2.0.5: below this thrust-to-weight the nozzles are treated as not covering pitch and the AoA limit is fixed at NoTvcAoACapPositive / Negative (+-36 by default; 2.0.8 and before: +36 only, released 0.03 above). The swept LowThrustAoACap still applies under it.");
			cHybridIndi = dev.Bind("4. Gains", "HybridIndi", 1f, "1.9.0: pitch-loop lag compensation. The incremental (INDI) pitch loop estimates the airframe's own moment from filtered sensors; at high dynamic pressure that moment moves fast with AoA and pitch rate and the filter lag made the pitch rate overshoot (bench, 2 g command at 1300 km/h: 2.8 g peak then 1.8 g, a slow G oscillation). The lag is now predicted from the airframe model (moment slope with AoA and pitch rate x the filter lag). 0 = off (1.8.45).");
			cHybridHiLead = dev.Bind("4. Gains", "HybridIndiHighAoALead", 0.15f, "0.4.9 (from F-22E 2.4.7/2.4.8): disturbance-rate lead, seconds. Above 25-40 deg AoA with the pitch stick released, the pitch loop takes the airframe's own pitch acceleration (what the effectors are not doing) forward by its rate x this, so a steady fall in it as the AoA unwinds after a corkscrew release is met when it arrives instead of ~0.1 s late. Pitch damping is left out. Fades out with T/W 0.4 -> 0.2 (the horizon is the nozzles' response). 0 = off (0.4.8).");
			cAllocNozzleNewton = dev.Bind("4. Gains", "AllocNozzleNewton", true, "0.4.9 (from F-22E 2.4.7/2.4.8): with the pitch stick released and T/W at least 0.25, the allocator's last pass also tries a local Newton step for the nozzles, not only their grid points (every 4 deg). Without it the nozzles could sit between two grid points with a pitch miss while the joint steps were rejected (stabs stalled on their stops, large yaw miss): the post-corkscrew nose dip. false = 0.4.8.");
			cYawRefKmh = dev.Bind("3. Lateral-directional", "YawBrakeRefIAS", 270f, "1.9.2: IAS (km/h) of the reference manoeuvre for the high-AoA yaw braking floor (HighAoABrakeFloor): full pedal + full lateral stick at max AoA. The yaw-rate limit may not exceed what the yaw authority brakes in YawStopFactor x the low-IAS yaw boost at this IAS (1.8.45: 280).");
			cYawMargStart = dev.Bind("3. Lateral-directional", "YawStopTimeStartIAS", 210f, "1.9.3: below this IAS (km/h) the allowed yaw stop time grows smoothly (smoothstep) to YawStopTimeLowIAS x at YawStopTimeFullIAS; flat above and below.");
			cYawMargFull = dev.Bind("3. Lateral-directional", "YawStopTimeFullIAS", 150f, "1.9.3: IAS (km/h) at and below which the full YawStopTimeLowIAS applies.");
			cYawMargLow = dev.Bind("3. Lateral-directional", "YawStopTimeLowIAS", 1.5f, "1.9.3: allowed yaw stop time at low IAS relative to the reference: 1.5 = 50 % more yaw rate for the same yaw authority (slower braking). 1 = flat. (1.9.2's YawBrakeMargin* keys worked the other way and are no longer read.)");
			cYawMidF = dev.Bind("3. Lateral-directional", "YawStopTimeMidFactor", 1f, "Revoker 0.2.4: 1 (no mid-IAS band; the F-22E rudder rule cut the corkscrew entry yaw limit 77 -> 54 deg/s from 330 to 290 km/h). F-22E: 2.0.12: 0.75 (1.9.4-2.0.11: 0.8). high-AoA yaw stop time (so yaw-rate limit) factor between YawStopTimeMidLoIAS and YawStopTimeMidHiIAS: 0.8 = 20 % less yaw rate for the same authority, faster braking. Smoothstep ramps of YawStopTimeMidRamp km/h outside the band. 1 = off.");
			cYawMidLo = dev.Bind("3. Lateral-directional", "YawStopTimeMidLoIAS", 280f, "2.0.12: 280 (1.9.4: 290). lower edge (km/h) of the mid-IAS yaw band (full factor from here up).");
			cYawMidHi = dev.Bind("3. Lateral-directional", "YawStopTimeMidHiIAS", 380f, "2.0.12: 380 (1.9.4: 330). upper edge (km/h) of the mid-IAS yaw band (full factor up to here).");
			cYawMidRamp = dev.Bind("3. Lateral-directional", "YawStopTimeMidRamp", 30f, "1.9.4: width (km/h) of the smooth ramps outside the mid-IAS yaw band (250-280 and 380-410 by default).");
			DevUnlocked = devUnlocked;
			// Revoker 0.2.0: one-time move of the 0.1.0 defaults that changed (AoALimitPositive 65 -> 70, ElevonPitch false ->
			// true). A dev file written by 0.1.0 has no RevokerConfigRevision line; values set by hand to anything else stay.
			// Vortex 0.1.1: StovlHoverYawRate default 30 -> 45; a dev file written by 0.1.0 (no VortexConfigRevision line) still
			// holding exactly 30 is moved once, a value set by hand stays
			bool vortexOld = devUnlocked && !devFresh && !FileHasKey(dev.ConfigFilePath, "VortexConfigRevision");
			cVortexRev = dev.Bind("9. STOVL", "VortexConfigRevision", 13, "Internal: the Vortex config revision this file was last updated to. Leave it alone.");
			if (vortexOld)
			{
				if (Mathf.Abs(cThrust.Value - 1.08f) < 0.0001f)
				{
					cThrust.Value = 1.12f;
				}
				cVortexRev.Value = 1;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.1.1 defaults: ThrustScale {cThrust.Value} (1.08 -> 1.12), unless set by hand.");
			}
			if (devUnlocked && cVortexRev.Value < 2)
			{
				// Vortex 0.1.5: the throttle detents' dead band +-2 % (was +-4 %)
				if (Mathf.Abs(cStovlDetentWidth.Value - 0.04f) < 0.0001f)
				{
					cStovlDetentWidth.Value = 0.02f;
				}
				// and the hover yaw rate back to 30 deg/s (0.1.1-0.1.4: 45)
				if (Mathf.Abs(cStovlHoverYawRate.Value - 45f) < 0.001f)
				{
					cStovlHoverYawRate.Value = 30f;
				}
				cVortexRev.Value = 2;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.1.5 defaults: StovlDetentWidth {cStovlDetentWidth.Value} (0.04 -> 0.02), StovlHoverYawRate {cStovlHoverYawRate.Value} (45 -> 30), unless set by hand.");
			}
			if (devUnlocked && cVortexRev.Value < 3)
			{
				// Vortex 0.1.7: the automatic changeover at 370 / 350 km/h (0.1.6: 350 / 300); values set by hand stay
				if (Mathf.Abs(cStovlAutoCtol.Value - 350f) < 0.01f)
				{
					cStovlAutoCtol.Value = 370f;
				}
				if (Mathf.Abs(cStovlAutoStovl.Value - 300f) < 0.01f)
				{
					cStovlAutoStovl.Value = 350f;
				}
				cVortexRev.Value = 3;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.1.7 defaults: StovlAutoCtolIas {cStovlAutoCtol.Value} (350 -> 370), StovlAutoStovlIas {cStovlAutoStovl.Value} (300 -> 350), unless set by hand.");
			}
			if (devUnlocked && cVortexRev.Value < 4)
			{
				// Vortex 0.1.8: button-only STOVL as the F-35B (automatic changeover off) and the conversion limit at 460 km/h (470)
				cStovlAuto.Value = false;
				if (Mathf.Abs(cStovlConversionMaxIas.Value - 470f) < 0.01f)
				{
					cStovlConversionMaxIas.Value = 460f;
				}
				cVortexRev.Value = 4;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.1.8 defaults: StovlAutoTransition false (button only, as the F-35B), StovlConversionMaxIas {cStovlConversionMaxIas.Value} (470 -> 460, unless set by hand).");
			}
			if (devUnlocked && cVortexRev.Value < 5)
			{
				// Vortex 0.2.0: the zero-ground-speed detent near the bottom of the lever (0.25 -> 0.10)
				if (Mathf.Abs(cStovlAftDetent.Value - 0.25f) < 0.0001f)
				{
					cStovlAftDetent.Value = 0.10f;
				}
				cVortexRev.Value = 5;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.2.0 defaults: StovlAftDetent {cStovlAftDetent.Value} (0.25 -> 0.10, unless set by hand).");
			}
			if (devUnlocked && cVortexRev.Value < 6)
			{
				// Vortex 0.2.1: the aft detent at 0.20 with a 30 kt backward range behind it
				if (Mathf.Abs(cStovlAftDetent.Value - 0.10f) < 0.0001f || Mathf.Abs(cStovlAftDetent.Value - 0.25f) < 0.0001f)
				{
					cStovlAftDetent.Value = 0.20f;
				}
				cVortexRev.Value = 6;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.2.1 defaults: StovlAftDetent {cStovlAftDetent.Value} (-> 0.20, unless set by hand), StovlBackGroundSpeed {cStovlBackGs.Value} kt.");
			}
			if (devUnlocked && cVortexRev.Value < 7)
			{
				// Vortex 0.3.10: StovlYawStopTime 1.84 (0.3.9) -> 1.09 (the real F-35B's 90 deg in 6-7 s at low fuel), unless set by hand
				if (cStovlYawStop != null && Mathf.Abs(cStovlYawStop.Value - 1.84f) < 0.0001f)
				{
					cStovlYawStop.Value = 1.09f;
				}
				cVortexRev.Value = 7;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.3.10 defaults: StovlYawStopTime {(cStovlYawStop != null ? cStovlYawStop.Value : 1.09f)} (1.84 -> 1.09 unless set by hand).");
			}
			if (devUnlocked && cVortexRev.Value < 8)
			{
				// Vortex 0.3.11 (Tom, 17 Nov 16:28): StovlYawStopTime -> 1.25 from the 0.3.9 / 0.3.10 defaults, unless set by hand
				if (cStovlYawStop != null && (Mathf.Abs(cStovlYawStop.Value - 1.09f) < 0.0001f || Mathf.Abs(cStovlYawStop.Value - 1.84f) < 0.0001f))
				{
					cStovlYawStop.Value = 1.25f;
				}
				cVortexRev.Value = 8;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.3.11 defaults: StovlYawStopTime {(cStovlYawStop != null ? cStovlYawStop.Value : 1.25f)} (-> 1.25 unless set by hand).");
			}
			if (devUnlocked && cVortexRev.Value < 9)
			{
				// Vortex 0.3.12 (Tom, 17 Nov 17:09: "wings, do 40 and rudders do 35"): the four wing trailing-edge surfaces +-40
				// (were +-30), unless set by hand; the rudders' +-35 is not a key (RudderRange)
				if (Mathf.Abs(cStabUp.Value - 30f) < 0.001f)
				{
					cStabUp.Value = 40f;
				}
				if (Mathf.Abs(cStabDn.Value - 30f) < 0.001f)
				{
					cStabDn.Value = 40f;
				}
				if (Mathf.Abs(cFlapRange.Value - 30f) < 0.001f)
				{
					cFlapRange.Value = 40f;
				}
				cVortexRev.Value = 9;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.3.12 defaults: StabilatorRangeTEUp {cStabUp.Value}, StabilatorRangeTEDown {cStabDn.Value}, FlaperonRange {cFlapRange.Value} (30 -> 40 unless set by hand); rudders +-35.");
			}
			if (devUnlocked && cVortexRev.Value < 10)
			{
				// Vortex 0.3.13 (Tom, 17 Nov 19:05, 19:01 log 100-116 s: full right stick at 60 deg AoA, the jet rolled left about
				// its velocity vector for 15 s): AllocatorPairPass was off by default in 0.3.7-0.3.8 and turned on by default in
				// 0.3.9 without moving a file that still held false, so the pair pass never ran in this file. Without it the solve
				// stayed in the reversed rudder set (rudders +35 / -35, inboard trailing edges +27 / +40) the pass exists to leave.
				// And the pitch nozzle's travel 10 -> 15 deg (TvcPitchRange 0 -> 15), unless set by hand.
				if (cPairPass != null && !cPairPass.Value)
				{
					cPairPass.Value = true;
				}
				if (cTvcRange.Value < 0.001f)
				{
					cTvcRange.Value = 15f;
				}
				cVortexRev.Value = 10;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.3.13 defaults: AllocatorPairPass {(cPairPass == null || cPairPass.Value)} (moved on once), TvcPitchRange {cTvcRange.Value} (0 -> 15 unless set by hand).");
			}
			if (devUnlocked && cVortexRev.Value < 11)
			{
				// Vortex 0.3.15 (Tom, 17 Nov 20:17: "set default aoa limit to +65 migrate settings"): AoALimitPositive 60 -> 65,
				// only while the file still holds the 0.3.6 default; a value set by hand is kept
				float wasP = cAoAPos.Value;
				if (Mathf.Abs(cAoAPos.Value - 60f) < 0.001f)
				{
					cAoAPos.Value = 65f;
				}
				cVortexRev.Value = 11;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.3.15 defaults: AoALimitPositive {wasP:F0} -> {cAoAPos.Value:F0} (60 -> 65 unless set by hand).");
			}
			if (devUnlocked && cVortexRev.Value < 12)
			{
				// Vortex 0.5.7: StovlTurnShare 0.707 -> 0.85, only while the file still holds the 0.4.7 default
				float wasS = cStovlTurnShare.Value;
				if (Mathf.Abs(cStovlTurnShare.Value - 0.707f) < 0.0005f)
				{
					cStovlTurnShare.Value = 0.85f;
				}
				cVortexRev.Value = 12;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.5.7 defaults: StovlTurnShare {wasS:F3} -> {cStovlTurnShare.Value:F3} (0.707 -> 0.85 unless set by hand).");
			}
			if (devUnlocked && cVortexRev.Value < 13)
			{
				// Vortex 0.5.10: StovlTrcStopTime 1.06 -> 0.75, only while the file still holds the 0.4.0 default
				float wasT = cStovlTrcStop.Value;
				if (Mathf.Abs(cStovlTrcStop.Value - 1.06f) < 0.0005f)
				{
					cStovlTrcStop.Value = 0.75f;
				}
				cVortexRev.Value = 13;
				dev.Save();
				Log.LogInfo($"Vortex FCS 0.5.10 defaults: StovlTrcStopTime {wasT:F2} -> {cStovlTrcStop.Value:F2} (1.06 -> 0.75 unless set by hand).");
			}
			bool revOld = devUnlocked && !devFresh && !FileHasKey(dev.ConfigFilePath, "RevokerConfigRevision");
			cRevokerRev = dev.Bind("2. Envelope", "RevokerConfigRevision", 16, "Internal: the Revoker config revision this file was last updated to. Leave it alone.");
			if (revOld)
			{
				cRevokerRev.Value = 0;
			}
			if (cRevokerRev.Value < 1)
			{
				if (Mathf.Abs(cAoAPos.Value - 65f) < 0.001f)
				{
					cAoAPos.Value = 70f;
				}
				if (!cElevonPitch.Value)
				{
					cElevonPitch.Value = true;
				}
				cRevokerRev.Value = 1;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.0 defaults: AoALimitPositive 70, ElevonPitch on (moved from the 0.1.0 defaults once).");
			}
			// Revoker 0.2.2: MaxYawRate 75 -> 90 once (a 0.2.0 / 0.2.1 dev file holds the old default)
			if (cRevokerRev.Value < 2)
			{
				if (Mathf.Abs(cMaxYaw.Value - 75f) < 0.001f)
				{
					cMaxYaw.Value = 90f;
				}
				cRevokerRev.Value = 2;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.2 defaults: MaxYawRate 90 (moved from 75 once).");
			}
			// Revoker 0.2.4: YawStopTimeMidFactor 0.75 -> 1 once
			if (cRevokerRev.Value < 3)
			{
				if (Mathf.Abs(cYawMidF.Value - 0.75f) < 0.001f)
				{
					cYawMidF.Value = 1f;
				}
				cRevokerRev.Value = 3;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.4 defaults: YawStopTimeMidFactor 1 (moved from 0.75 once).");
			}
			// Revoker 0.2.5: NoTvcThrustAoACapPositive 45 -> 50 once
			if (cRevokerRev.Value < 4)
			{
				if (Mathf.Abs(cNoTvcThrCapPos.Value - 45f) < 0.001f)
				{
					cNoTvcThrCapPos.Value = 50f;
				}
				cRevokerRev.Value = 4;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.5 defaults: NoTvcThrustAoACapPositive 50 (moved from 45 once).");
			}
			// Revoker 0.2.7: canard travel 40 -> 45 (StabilatorRangeTEUp/Down 0 -> 45) and AoALimitNegative 50 -> 55, once
			if (cRevokerRev.Value < 5)
			{
				if (cStabUp.Value < 0.001f && cStabDn.Value < 0.001f)
				{
					cStabUp.Value = 45f;
					cStabDn.Value = 45f;
				}
				if (Mathf.Abs(cAoANeg.Value - 50f) < 0.001f)
				{
					cAoANeg.Value = 55f;
				}
				cRevokerRev.Value = 5;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.7 defaults: canard travel 45 (StabilatorRangeTEUp/Down, from 0 = stock 40) and AoALimitNegative 55 (from 50), once.");
			}
			// Revoker 0.2.9: GroundRotationRamp 60 -> 600, once (only if it was exactly 60)
			if (cRevokerRev.Value < 6)
			{
				if (Mathf.Abs(cGroundRotRamp.Value - 60f) < 0.001f)
				{
					cGroundRotRamp.Value = 600f;
				}
				cRevokerRev.Value = 6;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.9 defaults: GroundRotationRamp 600 (from 60), once.");
			}
			// Revoker 0.2.13: PullSideslipMargin 15 -> 9, once (only if it was exactly 15)
			if (cRevokerRev.Value < 7)
			{
				// Vortex FCS 0.3.1: PullSideslipMargin is gone (the reference governor replaced the rolling-pull governor); the revision still advances
				cRevokerRev.Value = 7;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.13 defaults: (PullSideslipMargin retired in 0.3.0).");
			}
			// Revoker 0.2.14: canard travel trailing edge down (leading edge up) 45 -> 80, once (only if it was exactly 45)
			if (cRevokerRev.Value < 8)
			{
				if (Mathf.Abs(cStabDn.Value - 45f) < 0.001f)
				{
					cStabDn.Value = 80f;
				}
				cRevokerRev.Value = 8;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.14 defaults: StabilatorRangeTEDown 80 (from 45), once.");
			}
			// Revoker 0.2.16: 0.2.14 extended the wrong side. Leading edge DOWN (trailing edge up, the + side) gets the 80, leading
			// edge up goes back to 45, once (only from the exact 0.2.14/0.2.15 pair 45 / 80; anything else set by hand is kept)
			if (cRevokerRev.Value < 9)
			{
				if (Mathf.Abs(cStabUp.Value - 45f) < 0.001f && Mathf.Abs(cStabDn.Value - 80f) < 0.001f)
				{
					cStabUp.Value = 80f;
					cStabDn.Value = 45f;
				}
				cRevokerRev.Value = 9;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.16 defaults: canard travel leading edge down 80 / leading edge up 45 (StabilatorRangeTEUp 80, StabilatorRangeTEDown 45; was 45 / 80), once.");
			}
			// Revoker 0.2.17: canard travel leading edge down 80 -> 70, once, leading edge up kept at 45 (from the exact 0.2.16 pair 80 / 45,
			// or 70 / 35 from a withdrawn first 0.2.17 build; anything else set by hand is kept)
			if (cRevokerRev.Value < 11)
			{
				bool from216 = Mathf.Abs(cStabUp.Value - 80f) < 0.001f && Mathf.Abs(cStabDn.Value - 45f) < 0.001f;
				bool fromWithdrawn = Mathf.Abs(cStabUp.Value - 70f) < 0.001f && Mathf.Abs(cStabDn.Value - 35f) < 0.001f;
				if (from216 || fromWithdrawn)
				{
					cStabUp.Value = 70f;
					cStabDn.Value = 45f;
				}
				cRevokerRev.Value = 11;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.17 defaults: canard travel leading edge down 70 / leading edge up 45 (StabilatorRangeTEUp 70, StabilatorRangeTEDown 45), once.");
			}
			// Revoker 0.2.18: GroundBrakeCanard 45 -> 80 (= the full leading-edge-down travel), once (only if it was exactly 45)
			if (cRevokerRev.Value < 12)
			{
				if (Mathf.Abs(cGbCan.Value - 45f) < 0.001f)
				{
					cGbCan.Value = 80f;
				}
				cRevokerRev.Value = 12;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.18 defaults: GroundBrakeCanard 80 (full leading-edge-down travel; from 45), once.");
			}
			// Revoker 0.2.19: canard leading-edge-down travel 70 -> 60, once (only if it was exactly 70)
			if (cRevokerRev.Value < 13)
			{
				if (Mathf.Abs(cStabUp.Value - 70f) < 0.001f)
				{
					cStabUp.Value = 60f;
				}
				cRevokerRev.Value = 13;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.19 defaults: canard travel leading edge down 60 (StabilatorRangeTEUp; from 70), once.");
			}
			// Revoker 0.2.21: inboard elevon reversal AoA 32 -> 37, once (only if it was exactly 32)
			if (cRevokerRev.Value < 14)
			{
				if (Mathf.Abs(cStallInner.Value - 32f) < 0.001f)
				{
					cStallInner.Value = 37f;
				}
				cRevokerRev.Value = 14;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.21 defaults: InnerElevonReversalAoA 37 (from 32), once.");
			}
			// Revoker 0.2.22: knee-preserving stagger; inboard elevon reversal AoA 37 -> 26, once (only if it was exactly 37)
			if (cRevokerRev.Value < 15)
			{
				if (Mathf.Abs(cStallInner.Value - 37f) < 0.001f)
				{
					cStallInner.Value = 26f;
				}
				cRevokerRev.Value = 15;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 0.2.22 defaults: InnerElevonReversalAoA 26 (from 37; knee-preserving stagger), once.");
			}
			// Revoker 1.4.1: MaxPitchRate 48 -> 60 (Tom, 12 Nov: "make the revoker do 60deg/s max pitch rate as default"), once, only if the
			// dev file still holds 48
			if (cRevokerRev.Value < 16)
			{
				bool movedQ = Mathf.Abs(cMaxPitch.Value - 48f) < 0.001f;
				if (movedQ)
				{
					cMaxPitch.Value = 60f;
				}
				cRevokerRev.Value = 16;
				if (devUnlocked)
				{
					dev.Save();
				}
				Log.LogInfo("Vortex FCS 1.4.1 defaults: " + (movedQ ? "MaxPitchRate 60 (from 48)" : "MaxPitchRate kept (not 48)") + ", once.");
			}
			MigrateOldConfig(cfg, dev, devUnlocked && devFresh);
			MigrateGovernorKeys(cfg, devUnlocked ? dev : null);
			// Vortex FCS 0.4.0: RollStopSideslip default 4 -> 8; a player file written by 0.3.0 still holding exactly 4 is moved once, a value set by hand is kept
			if (cGovRev.Value < 1)
			{
				if (Mathf.Abs(cRollStopSlip.Value - 4f) < 0.001f)
				{
					cRollStopSlip.Value = 8f;
				}
				cGovRev.Value = 1;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.4.0 defaults: RollStopSideslip {cRollStopSlip.Value} (4 -> 8 unless set by hand).");
			}
			// Vortex FCS 0.7.0: RollStopSideslip default 8 -> 6; a file still holding exactly 8 is moved once, a value set by hand is kept
			if (cGovRev.Value < 2)
			{
				if (Mathf.Abs(cRollStopSlip.Value - 8f) < 0.001f)
				{
					cRollStopSlip.Value = 6f;
				}
				cGovRev.Value = 2;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.7.0 defaults: RollStopSideslip {cRollStopSlip.Value} (8 -> 6 unless set by hand).");
			}
			// Vortex 0.3.1: stagger 26 / 45 -> 40 / 55 (the 0.3.0 airframe's sweep); a pair still at exactly 26 / 45 is moved once
			if (cStallInner != null && cStallOuter != null && Mathf.Abs(cStallInner.Value - 26f) < 0.001f && Mathf.Abs(cStallOuter.Value - 45f) < 0.001f)
			{
				cStallInner.Value = 40f;
				cStallOuter.Value = 55f;
				Log.LogInfo("Vortex FCS 0.3.1 defaults: InnerElevonReversalAoA 26 -> 40, OuterElevonReversalAoA 45 -> 55, once.");
			}
			// Vortex 0.3.1: ForeLiftSchedule moved with the wing vortex lift; a file still holding exactly the 0.1.0 default is moved once
			if (cForeLiftSched != null && cForeLiftSched.Value == "0:1.22,8:1.22,12:1.23,16:1.26,20:1.30,24:1.35,28:1.37,32:1.39,36:1.41,40:1.44,44:1.40,48:1.30,52:1.20,56:1.10,60:1.00")
			{
				cForeLiftSched.Value = "0:1.22,8:1.22,12:1.23,16:1.26,20:1.32,24:1.40,28:1.45,32:1.49,36:1.50,40:1.50,44:1.43,48:1.31,52:1.21,56:1.10,60:1.00";
				Log.LogInfo("Vortex FCS 0.3.1 defaults: ForeLiftSchedule moved to the 0.3.0 default (the wing vortex lift's pitch balance), once.");
			}
			// Vortex FCS 0.8.1: RollPaceRateBlend default 0.4 -> 0.65; a file still holding exactly 0.4 is moved once, a value set by hand is kept
			if (cGovRev.Value < 3)
			{
				if (Mathf.Abs(cRollPaceRateBlend.Value - 0.4f) < 0.0001f)
				{
					cRollPaceRateBlend.Value = 0.65f;
				}
				cGovRev.Value = 3;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.8.1 defaults: RollPaceRateBlend {cRollPaceRateBlend.Value} (0.4 -> 0.65 unless set by hand).");
			}
			// Vortex 0.3.2: AsymMinAoACap 12 -> 10 (Tom, 17 Nov: the heavy-load roll away from the load was yaw-saturated at 12);
			// a file still holding exactly 12 is moved once, a value set by hand afterwards is kept
			if (cGovRev.Value < 4)
			{
				if (cAsymFloor != null && Mathf.Abs(cAsymFloor.Value - 12f) < 0.0001f)
				{
					cAsymFloor.Value = 10f;
				}
				cGovRev.Value = 4;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.3.2 defaults: AsymMinAoACap {(cAsymFloor != null ? cAsymFloor.Value : 10f)} (12 -> 10 unless set by hand).");
			}
			// Vortex 0.3.7 (Tom, 17 Nov: "make the default into 1s"): ReleaseGuardHold 1.5 -> 1, once, unless set by hand
			if (cGovRev.Value < 5)
			{
				if (cRelGuardHold != null && Mathf.Abs(cRelGuardHold.Value - 1.5f) < 0.0001f)
				{
					cRelGuardHold.Value = 1f;
				}
				cGovRev.Value = 5;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.3.7 defaults: ReleaseGuardHold {(cRelGuardHold != null ? cRelGuardHold.Value : 1f)} (1.5 -> 1 unless set by hand).");
			}
			// Vortex 0.3.14 (Tom, 17 Nov 19:53: "I want the margin to be the same as the raptor. Corkscrews and falling leafs at max aoa
			// feels decently fast but mushy"): the lateral governor's section as the F-22E flies it - RollStopSideslip 6 -> 4,
			// PushoverSideslip 8 -> -1 (follows RollStopSideslip), RollPaceRateBlend 0.65 -> 0.4084507, and the 0.3.1 high-AoA fade of
			// the rate cap off (LeafRateCapFadeHi 60 -> 45, Hi <= Lo); each only if the file still holds the old default
			if (cGovRev.Value < 6)
			{
				if (Mathf.Abs(cRollStopSlip.Value - 6f) < 0.0001f)
				{
					cRollStopSlip.Value = 4f;
				}
				if (cPushSlip != null && Mathf.Abs(cPushSlip.Value - 8f) < 0.0001f)
				{
					cPushSlip.Value = -1f;
				}
				if (Mathf.Abs(cRollPaceRateBlend.Value - 0.65f) < 0.0001f)
				{
					cRollPaceRateBlend.Value = 0.4084507f;
				}
				if (cLeafFadeLo != null && cLeafFadeHi != null && Mathf.Abs(cLeafFadeLo.Value - 45f) < 0.0001f && Mathf.Abs(cLeafFadeHi.Value - 60f) < 0.0001f)
				{
					cLeafFadeHi.Value = 45f;
				}
				cGovRev.Value = 6;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.3.14 defaults (lateral governor as the F-22E): RollStopSideslip {cRollStopSlip.Value}, PushoverSideslip {(cPushSlip != null ? cPushSlip.Value : -1f)}, RollPaceRateBlend {cRollPaceRateBlend.Value}, LeafRateCapFade {(cLeafFadeLo != null ? cLeafFadeLo.Value : 45f)}..{(cLeafFadeHi != null ? cLeafFadeHi.Value : 45f)} (off when Hi <= Lo), unless set by hand.");
			}
			// Vortex 0.4.6 (Tom, 17 Nov 23:26): StovlShipRelative default false -> true (vanilla's auto-hover matches the ship's speed);
			// a file still holding the old default is moved once, a hand-set value afterwards is kept
			if (cGovRev.Value < 7)
			{
				if (cShipRel != null && !cShipRel.Value)
				{
					cShipRel.Value = true;
				}
				cGovRev.Value = 7;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.4.6 defaults: StovlShipRelative {(cShipRel != null && cShipRel.Value)} (false -> true once).");
			}
			// Vortex 0.4.11: 0.4.1-0.4.10's key wrote StovlShipRelative into the file, so a false there is most likely the key's, not
			// a choice; moved back to true once (the key now switches it for the match only; set false by hand to start every
			// match off)
			if (cGovRev.Value < 8)
			{
				if (cShipRel != null && !cShipRel.Value)
				{
					cShipRel.Value = true;
				}
				cGovRev.Value = 8;
				cfg.Save();
				Log.LogInfo($"Vortex FCS 0.4.11 defaults: StovlShipRelative {(cShipRel != null && cShipRel.Value)} (false -> true once; the key now switches it for the current match only).");
			}
			// Ifrit 0.2.0: 0.1.0 ran the F-22E 2.0.3 airframe migration and wrote 1.2 into the (unused) effectiveness keys; reset
			// them once. The Ifrit airframe is never effectiveness-scaled.
			// Ifrit 0.2.1: the rollout aero-brake is off; the split decelerons are the airbrake on the ground too
			if (cIfritRev.Value < 2)
			{
				if (cIfritRev.Value >= 1)
				{
					cIfritRev.Value = 2;
					dev.Save();
				}
			}
			// Ifrit 0.2.2: the Buffed profile's parasitic drag 0.885 -> 0.95 and transonic relief 0.2 -> 0.1; a file still on the
			// old defaults is moved once (a value set by hand is left alone)
			if (cIfritRev.Value < 3)
			{
				if (Mathf.Abs(cDrag.Value - 0.885f) < 0.0005f)
				{
					cDrag.Value = 0.95f;
				}
				if (Mathf.Abs(cDrTrans.Value - 0.2f) < 0.0005f)
				{
					cDrTrans.Value = 0.1f;
				}
				if (cIfritRev.Value >= 1)
				{
					cIfritRev.Value = 3;
					dev.Save();
				}
			}
			// Ifrit 0.3.0: FlaperonRange stock (0) -> 30 (ailerons and decelerons +-30); the servo speed and split limit are new keys
			if (cIfritRev.Value < 4)
			{
				if (cFlapRange.Value <= 0f)
				{
					cFlapRange.Value = 30f;
				}
				if (Mathf.Abs(cSplitBrakeHold.Value - 0.05f) < 0.0001f)
				{
					cSplitBrakeHold.Value = 0.5f;
				}
				if (cIfritRev.Value >= 1)
				{
					cIfritRev.Value = 4;
					dev.Save();
				}
			}
			// Ifrit 0.3.1: roll TVC cost 5e-5 -> 0.003 (uneven nozzles on pitch release), rolling-pull sideslip margin 9 -> 15 and
			// the high-AoA sideslip gain ramp from 5 deg (was 25): the corkscrew-entry AoA hitch near 20 deg
			if (cIfritRev.Value < 5)
			{
				if (cIfritRev.Value >= 1)
				{
					if (Mathf.Abs(cTvcDiffMu.Value - 5E-05f) < 1E-07f)
					{
						cTvcDiffMu.Value = 0.003f;
					}
					// Vortex FCS 0.3.1: PullSideslipMargin is gone (the reference governor replaced the rolling-pull governor); the revision still advances
					if (Mathf.Abs(cSlipGainA0.Value - 25f) < 0.001f)
					{
						cSlipGainA0.Value = 5f;
					}
					cIfritRev.Value = 5;
					dev.Save();
				}
			}
			// Ifrit 0.3.7: MPO lateral law default VelocityVectorRoll -> SeparateRollYaw; a file still holding the old default
			// is moved once (a SeparateRollYaw / later choice is never touched)
			if (cIfritRev.Value < 6)
			{
				if (cIfritRev.Value >= 1)
				{
					if (cMpoSep.Value == MpoRollMode.VelocityVectorRoll)
					{
						cMpoSep.Value = MpoRollMode.SeparateRollYaw;
						cfg.Save();
					}
					cIfritRev.Value = 6;
					dev.Save();
				}
			}
			// Ifrit 0.4.0: the engine-out AoA cap 30 -> 22 (bench, one engine at AB: at 30 the pull bleeds to 50-85 m/s where the
			// tails, splits and crossing are all spent and the live engine gets cut; 22 holds without the throttle cut from
			// 300 km/h). A file still holding 30 is moved once.
			if (cIfritRev.Value < 7)
			{
				if (cIfritRev.Value >= 1)
				{
					if (Mathf.Abs(cEngOutCap.Value - 30f) < 0.01f)
					{
						cEngOutCap.Value = 22f;
					}
					cIfritRev.Value = 7;
					dev.Save();
				}
			}
			// Ifrit 0.4.1: the buffed variant's fuel burn 0.75 -> 0.65 (35 % less). A file still holding 0.75 is moved once.
			if (cIfritRev.Value < 8)
			{
				if (cIfritRev.Value >= 1)
				{
					if (Mathf.Abs(cFuelUse.Value - 0.75f) < 0.001f)
					{
						cFuelUse.Value = 0.65f;
					}
					cIfritRev.Value = 8;
					dev.Save();
				}
			}
			if (cIfritRev.Value < 1)
			{
				if (cAirframeRev.Value < 3)
				{
					cAirframeRev.Value = 3;
				}
				cIfritRev.Value = 8;
				dev.Save();
			}
			// 2.0.12: the high-AoA mid-IAS yaw band is 280-380 km/h at 0.75 (25 % less yaw braking margin); a file still on
			// 1.9.4's 290-330 / 0.8 is moved once
			if (cAirframeRev.Value < 6)
			{
				if (Mathf.Abs(cYawMidF.Value - 0.8f) < 0.001f)
				{
					cYawMidF.Value = 0.75f;
				}
				if (Mathf.Abs(cYawMidLo.Value - 290f) < 0.01f)
				{
					cYawMidLo.Value = 280f;
				}
				if (Mathf.Abs(cYawMidHi.Value - 330f) < 0.01f)
				{
					cYawMidHi.Value = 380f;
				}
			}
			if (cAirframeRev.Value < 6)
			{
				cAirframeRev.Value = 6;
			}
			// 2.2.23: rolling-pull governor defaults 6 / 0 -> 9 / 0.7; a file still on the old pair is moved once, anything
			// set by hand is kept
			if (cAirframeRev.Value < 7)
			{
				// Vortex FCS 0.3.1: PullSideslipMargin is gone (the reference governor replaced the rolling-pull governor); the revision still advances
				if (Mathf.Abs(cPullRollTrade.Value) < 0.0001f)
				{
					cPullRollTrade.Value = 0.7f;
				}
				Log.LogInfo($"Vortex FCS 0.3.1 (F-22E 2.2.23) defaults: PullRollTrade {cPullRollTrade.Value} (PullSideslipMargin retired).");
				cAirframeRev.Value = 7;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.2.26 (revision 8) moved PullRollTrade 0.7 -> 1; 2.2.27 undoes it
			// 2.2.27: PullRollTrade back to 0.7. A file that 2.2.26 moved to 1 (revision 8 with the value 1) goes back once;
			// a file never touched by 2.2.26 keeps its value
			if (cAirframeRev.Value < 9)
			{
				if (cAirframeRev.Value == 8 && Mathf.Abs(cPullRollTrade.Value - 1f) < 0.001f)
				{
					cPullRollTrade.Value = 0.7f;
				}
				Log.LogInfo($"Vortex FCS 0.3.1 (F-22E 2.2.27) defaults: PullRollTrade {cPullRollTrade.Value}.");
				cAirframeRev.Value = 9;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.2.29: ReversedFlowRollSense on (a file holding false is set to true once)
			if (cAirframeRev.Value < 10)
			{
				cRevRoll.Value = true;
				Log.LogInfo("Vortex FCS 0.3.1 (F-22E 2.2.29) defaults: ReversedFlowRollSense true.");
				cAirframeRev.Value = 10;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// Ifrit 0.4.1: fuel moved twice as fast, balancing 40 -> 80 kg/s and trim 120 -> 240; files still on the old values are moved once
			if (cAirframeRev.Value < 11)
			{
				if (Mathf.Abs(cFuelRate.Value - 40f) < 0.01f)
				{
					cFuelRate.Value = 80f;
				}
				if (Mathf.Abs(cFuelTrimRate.Value - 120f) < 0.01f)
				{
					cFuelTrimRate.Value = 240f;
				}
				Log.LogInfo("Ifrit 0.4.1 defaults: FuelTransferRate 80, FuelTrimRate 240.");
				cAirframeRev.Value = 11;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// Ifrit 0.4.2: the damaged-engine cap 30 -> 40; files still on 30 are moved once
			if (cAirframeRev.Value < 12)
			{
				if (Mathf.Abs(cEngDmgCap.Value - 30f) < 0.01f)
				{
					cEngDmgCap.Value = 40f;
				}
				Log.LogInfo("Ifrit 0.4.2 defaults: EngineDamagedAoACap 40.");
				cAirframeRev.Value = 12;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// Ifrit 0.4.5: throttle response back to the game's own (1.3 -> 1); files still on 1.3 are moved once
			if (cAirframeRev.Value < 13)
			{
				if (Mathf.Abs(cThrResp.Value - 1.3f) < 0.001f)
				{
					cThrResp.Value = 1f;
				}
				Log.LogInfo("Ifrit 0.4.5 defaults: ThrottleResponseScale 1.");
				cAirframeRev.Value = 13;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// Ifrit 0.3.0: keys this build no longer reads (the F-22E-only settings, keys that moved section) are dropped from
			// both files instead of being carried as orphans
			DropOrphans(cfg, true);
			DropOrphans(dev, devUnlocked);
		}

		// true when the player file exists and was written with HostLockVariant's old default (false)
		// Revoker 0.2.0: whether a config file already has a key line
		private static bool FileHasKey(string path, string key)
		{
			try
			{
				if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
				{
					return false;
				}
				foreach (string l in System.IO.File.ReadAllLines(path))
				{
					if (l.TrimStart().StartsWith(key + " ") || l.TrimStart().StartsWith(key + "="))
					{
						return true;
					}
				}
			}
			catch (System.Exception)
			{
			}
			return false;
		}

		private static bool HostLockOldDefault(string path)
		{
			try
			{
				if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
				{
					return false;
				}
				string[] lines = System.IO.File.ReadAllLines(path);
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].TrimStart().StartsWith("HostLockVariant"))
					{
						for (int k = i - 1; k >= 0 && k >= i - 4; k--)
						{
							string l = lines[k].Trim();
							if (l.StartsWith("# Default value:"))
							{
								return l.EndsWith("false", System.StringComparison.OrdinalIgnoreCase);
							}
						}
						return false;
					}
				}
			}
			catch (System.Exception)
			{
			}
			return false;
		}

		public static bool DevUnlocked { get; private set; }

		// Vortex FCS 0.3.1 (F-22E 2.6.0): PullRollTrade moved from the Dev file to the player file (section "6. Lateral governor"); the
		// retired rolling-pull devices' keys (AoASweepYawLimit, AoASweepYawLimitPositive, PullSideslipMargin, PullFFBoostMax,
		// PullGovSmooth) are gone. Once, the Dev file's leftover value of the moved key is carried into the player file; the
		// leftovers are then dropped with the other orphans.
		private static readonly string[] GovMovedKeys = new string[] { "PullRollTrade", "RollStopSideslip", "RollGovernorPitch", "RollPaceRateSlip", "RollPaceRateBlend", "RollStopSlipProbe" };
		// 2.44.1: Dev keys of devices that are gone, dropped from the Dev file once (no value carried): section / key
		private static readonly string[][] RetiredDevKeys = new string[][] { new string[] { "4. Gains", "ReleasedStickStabRateBound" } };
		private static void MigrateGovernorKeys(ConfigFile cfg, ConfigFile dev)
		{
			if (dev == null)
			{
				return;
			}
			try
			{
				var prop = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (!(prop?.GetValue(dev) is Dictionary<ConfigDefinition, string> orph) || orph.Count == 0)
				{
					return;
				}
				int n = 0;
				int dropped = 0;
				int retired = 0;
				foreach (KeyValuePair<ConfigDefinition, string> o in new List<KeyValuePair<ConfigDefinition, string>>(orph))
				{
					bool isRetired = false;
					foreach (string[] rk in RetiredDevKeys)
					{
						if (o.Key.Section == rk[0] && o.Key.Key == rk[1]) { isRetired = true; break; }
					}
					if (isRetired)
					{
						orph.Remove(o.Key);
						retired++;
						continue;
					}
					if (o.Key.Section != "3. Lateral-directional")
					{
						continue;
					}
					if (Array.IndexOf(GovMovedKeys, o.Key.Key) >= 0)
					{
						foreach (ConfigDefinition def in cfg.Keys)
						{
							if (def.Section == "6. Lateral governor" && def.Key == o.Key.Key)
							{
								try
								{
									ConfigEntryBase e = cfg[def];
									e.BoxedValue = TomlTypeConverter.ConvertToValue(o.Value, e.SettingType);
									n++;
								}
								catch
								{
								}
							}
						}
					}
					orph.Remove(o.Key);
					dropped++;
				}
				if (retired > 0 && dropped == 0)
				{
					dev.Save();
				}
				if (retired > 0)
				{
					Log.LogInfo($"F-22E FCS: {retired} retired device key(s) dropped from the dev file.");
				}
				if (dropped > 0)
				{
					dev.Save();
					cfg.Save();
					Log.LogInfo($"Vortex FCS 0.3.1: lateral governor keys moved to the player config ({n} values carried over, {dropped} old lateral keys dropped from the dev file).");
				}
			}
			catch (Exception ex)
			{
				Log.LogWarning("Vortex FCS 0.3.1 governor key migration: " + ex.Message);
			}
		}

		private static void DropOrphans(ConfigFile f, bool save)
		{
			try
			{
				var prop = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (f == null || !(prop?.GetValue(f) is Dictionary<ConfigDefinition, string> orph) || orph.Count == 0)
				{
					return;
				}
				int n = orph.Count;
				orph.Clear();
				if (save)
				{
					f.Save();
				}
				Log.LogInfo($"Vortex FCS: dropped {n} setting(s) this build no longer uses from {System.IO.Path.GetFileName(f.ConfigFilePath)}.");
			}
			catch (Exception ex)
			{
				Log.LogWarning("Vortex FCS: could not tidy " + ((f != null) ? System.IO.Path.GetFileName(f.ConfigFilePath) : "config") + ": " + ex.Message);
			}
		}

		// 1.8.44: the config was split into the player file (this plugin's) and the dev/debug file. Keys left in the player
		// file from older builds are orphans: the ones that moved section inside the player file (AI toggles) and, when the
		// dev file is being created now, the dev settings are carried over by key name once; then the orphans are dropped.
		private static void MigrateOldConfig(ConfigFile cfg, ConfigFile dev, bool toDev)
		{
			try
			{
				var prop = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (!(prop?.GetValue(cfg) is Dictionary<ConfigDefinition, string> orph) || orph.Count == 0)
				{
					return;
				}
				var byKey = new Dictionary<string, string>();
				foreach (KeyValuePair<ConfigDefinition, string> o in orph)
				{
					if (o.Key.Section.StartsWith("Countermeasures (all") || !byKey.ContainsKey(o.Key.Key))
					{
						byKey[o.Key.Key] = o.Value;
					}
				}
				int n = 0;
				void Carry(ConfigFile f, bool skipStockCm)
				{
					foreach (ConfigDefinition def in new List<ConfigDefinition>(f.Keys))
					{
						if (skipStockCm && def.Section.StartsWith("3. Countermeasures"))
						{
							continue;
						}
						if (!byKey.TryGetValue(def.Key, out string v))
						{
							continue;
						}
						ConfigEntryBase e = f[def];
						try
						{
							e.BoxedValue = TomlTypeConverter.ConvertToValue(v, e.SettingType);
							n++;
						}
						catch
						{
						}
					}
				}
				Carry(cfg, skipStockCm: true);
				if (toDev)
				{
					Carry(dev, skipStockCm: false);
					dev.Save();
				}
				orph.Clear();
				cfg.Save();
				Log.LogInfo($"Vortex FCS: old config keys migrated ({n} values carried over{(toDev ? ", dev file created" : "")}, leftovers removed).");
			}
			catch (Exception ex)
			{
				Log.LogWarning("Vortex FCS config migration: " + ex.Message);
			}
		}

		public static void ApplyPatches(Harmony h)
		{
			MpPolicy.Install(h, (string what, Action a) => TryPatch(h, what, a));
			TryPatch(h, "ControlsFilter.Filter", delegate
			{
				h.Patch(AccessTools.Method(typeof(ControlsFilter), "Filter"), new HarmonyMethod(typeof(Hooks), "FilterPrefix"));
			});
			TryPatch(h, "AutopilotPlane.AutoAim", delegate
			{
				h.Patch(AccessTools.Method(typeof(AutopilotPlane), "AutoAim", new Type[9]
				{
					typeof(GlobalPosition),
					typeof(bool),
					typeof(bool),
					typeof(bool),
					typeof(float),
					typeof(float),
					typeof(bool),
					typeof(float),
					typeof(Vector3)
				}), new HarmonyMethod(typeof(Hooks), "AutoAimPrefix"), new HarmonyMethod(typeof(Hooks), "AutoAimPostfix"));
			});
			TryPatch(h, "ControlSurface.UpdateJobFields", delegate
			{
				h.Patch(AccessTools.Method(typeof(ControlSurface), "UpdateJobFields"), null, new HarmonyMethod(typeof(Hooks), "SurfacePostfix"));
			});
			TryPatch(h, "PowerSupply.ModifyCapacitance", delegate
			{
				h.Patch(AccessTools.Method(typeof(PowerSupply), "ModifyCapacitance"), new HarmonyMethod(typeof(Countermeasures), "ModifyCapacitancePrefix"));
			});
			TryPatch(h, "Aircraft.UseFuel", delegate
			{
				MethodInfo useFuel = AccessTools.Method(typeof(Aircraft), "UseFuel", new Type[1] { typeof(float) });
				h.Patch(useFuel, new HarmonyMethod(typeof(FuelBalance), "UseFuelScalePrefix") { priority = Priority.First });
				h.Patch(useFuel, new HarmonyMethod(typeof(FuelBalance), "UseFuelPrefix") { priority = Priority.Last });
			});
			TryPatch(h, "Aircraft.GetFuelLevel", delegate
			{
				h.Patch(AccessTools.Method(typeof(Aircraft), "GetFuelLevel"), null, new HarmonyMethod(typeof(FuelBalance), "GetFuelLevelPostfix"));
			});
			TryPatch(h, "Aircraft.GetFuelQuantity", delegate
			{
				h.Patch(AccessTools.Method(typeof(Aircraft), "GetFuelQuantity"), null, new HarmonyMethod(typeof(FuelBalance), "GetFuelQuantityPostfix"));
			});
			TryPatch(h, "FuelTank.Refuel", delegate
			{
				h.Patch(AccessTools.Method(typeof(FuelTank), "Refuel"), new HarmonyMethod(typeof(FuelBalance), "RefuelPrefix"));
			});
			TryPatch(h, "Hardpoint.ModifyMass", delegate
			{
				h.Patch(AccessTools.Method(typeof(Hardpoint), "ModifyMass"), null, new HarmonyMethod(typeof(FuelBalance), "HardpointMassPostfix"));
			});
			TryPatch(h, "AeroJobSettings.Schedule", delegate
			{
				h.Patch(AccessTools.Method(typeof(AeroJobSettings), "Schedule"), new HarmonyMethod(typeof(StallStaggerJobs), "SchedulePrefix"));
			});
			TryPatch(h, "HighLiftDevice.FixedUpdate", delegate
			{
				h.Patch(AccessTools.Method(typeof(HighLiftDevice), "FixedUpdate"), new HarmonyMethod(typeof(Hooks), "FlapDevicePrefix"));
			});
			TryPatch(h, "LandingGear.FixedUpdate", delegate
			{
				h.Patch(AccessTools.Method(typeof(LandingGear), "FixedUpdate"), new HarmonyMethod(typeof(Hooks), "GearPrefix"), new HarmonyMethod(typeof(Hooks), "GearPostfix"));
			});
			TryPatch(h, "Turbojet.FixedUpdate", delegate
			{
				// Revoker 0.1.0: after the KR-67 Ifrit FCS's transpiler on the same method (it chains onto its calls; see Hooks)
				HarmonyMethod tr = new HarmonyMethod(typeof(Hooks), "TurbojetTranspiler");
				tr.priority = Priority.Last;
				tr.after = new string[1] { Hooks.IfritHarmonyId };
				h.Patch(AccessTools.Method(typeof(Turbojet), "FixedUpdate"), new HarmonyMethod(typeof(Hooks), "TurbojetPrefix"), null, tr);
			});
			// Vortex 0.1.0: the lift system (swivel nozzle, lift fan, roll posts), the engine throttle in STOVL and the Flight
			// Assist hold (conversion) for the STOVL laws
			StovlHooks.Install(h, (string what, Action a) => TryPatch(h, what, a));
			ReleaseGuard.Install(h, (string what, Action a) => TryPatch(h, what, a));
			ThrottleDetent.Install(h, (string what, Action a) => TryPatch(h, what, a));
			HudThrottle.Install(h, (string what, Action a) => TryPatch(h, what, a));
			// Vortex 0.4.8
			HudShipMatch.Install(h, (string what, Action a) => TryPatch(h, what, a));
			// Revoker 0.1.0: the Revoker's dedicated airbrake (game Airbrake component, opened by the game at zero throttle)
			// is commanded by the FCS's brake logic instead (F-22E 2.2.6 / 2.2.16 hooks)
			TryPatch(h, "Airbrake.Update", delegate
			{
				h.Patch(AccessTools.Method(typeof(Airbrake), "Update"), new HarmonyMethod(typeof(Hooks), "AirbrakeUpdatePrefix"), new HarmonyMethod(typeof(Hooks), "AirbrakeUpdatePostfix"));
			});
			Log.LogInfo($"FS-20 Vortex FCS {FcsStandalonePlugin.Version} (yaw TVC {(YawTvc ? "on" : "off")}, elevon pitch {(ElevonPitch ? "on" : "off")}; {(StockPerformance ? "stock" : "buffed")} performance{(DevUnlocked ? ", dev/debug settings unlocked" : "")}) ready (AoA +{AoAPos}/{AoANeg}, G +{GPos}/{GNeg}, MPO +{MpoGPos}/{MpoGNeg}, " + string.Format("q<= {0}, p<= {1}, r<= {2}; asym-load AoA cap {3}; flaperon camber K {4:F2}, MPO separate axes {8}, actuators x{5}, thrust x{6}, parasitic drag x{7}, fuel burn x{9:F2}).", MaxPitchRate, MaxRollRate, MaxYawRate, AsymLimiter ? ("on, reserve " + AsymReserve.ToString("0.00")) : "off", AileronCamberK, ActuatorScale, ThrustScale, DragScale, MpoSeparateAxes, FuelUseScale));
		}

		private static void TryPatch(Harmony h, string what, Action a)
		{
			try
			{
				a();
				Log.LogInfo("Installed hook: " + what);
			}
			catch (Exception ex)
			{
				Log.LogError("Failed to install hook " + what + ": " + ex);
			}
		}
	}
}
