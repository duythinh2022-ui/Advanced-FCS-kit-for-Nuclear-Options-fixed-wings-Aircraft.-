using System.Reflection;
﻿using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 1.8.43: one DLL for both builds. PerformanceProfile picks stock or buffed performance at runtime.
	public enum PerformanceProfile
	{
		Buffed,
		Stock
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

		private static ConfigEntry<int> cNativeAlloc;

		private static ConfigEntry<bool> cSweepGrid;

		private static ConfigEntry<bool> cNativeSweep;

		private static ConfigEntry<bool> cNativeCpl;

		private static ConfigEntry<float> cAsymCplResMax;

		private static ConfigEntry<bool> cAsymCplOn;

		private static ConfigEntry<float> cAsymCplResMaxNt;

		private static ConfigEntry<bool> cNativeTables;

		private static ConfigEntry<int> cNativeAllocCheck;

		private static ConfigEntry<bool> cParSweep;

		private static ConfigEntry<bool> cParEnv;

		private static ConfigEntry<bool> cAIFcs;

		private static ConfigEntry<bool> cAILog;

		private static ConfigEntry<bool> cAIDirectRoll;

		private static ConfigEntry<float> cStabUp;

		private static ConfigEntry<float> cStabDn;

		private static ConfigEntry<float> cRudRange;

		private static ConfigEntry<float> cFlapRange;

		private static ConfigEntry<float> cTvcRange;

		private static ConfigEntry<float> cTvcSpeed;

		private static ConfigEntry<bool> cAIGunRollFade;

		private static ConfigEntry<bool> cAsymOn;

		private static ConfigEntry<bool> cAsymRollHead;

		private static ConfigEntry<bool> cRevRoll;
		private static ConfigEntry<bool> cRevNose;

		private static ConfigEntry<bool> cGndBrakeLog;


		private static ConfigEntry<float> cAoAPos;

		private static ConfigEntry<float> cAoANeg;

		private static ConfigEntry<int> cAoANegRev;

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

		private static ConfigEntry<float> cFlaperonEff;

		private static ConfigEntry<float> cStabEff;

		private static ConfigEntry<float> cActuator;

		private static ConfigEntry<float> cRudStart;

		private static ConfigEntry<float> cRudEnd;

		private static ConfigEntry<float> cUnloadDecel;

		private static ConfigEntry<float> cUnloadRise;

		private static ConfigEntry<float> cRollLo;

		private static ConfigEntry<float> cRollHi;

		private static ConfigEntry<float> cRollAuth;

		private static ConfigEntry<float> cRollBrake;

		private static ConfigEntry<float> cStabTrim;

		private static ConfigEntry<float> cLerxEff;

		private static ConfigEntry<bool> cStagger;

		private static ConfigEntry<float> cDownwash;


		private static ConfigEntry<float> cStabPeakMargin;

		private static ConfigEntry<bool> cBodyLift;

		private static ConfigEntry<bool> cWingLift;

		private static ConfigEntry<float> cWingLiftPeak;

		private static ConfigEntry<float> cWingLiftGain;

		private static ConfigEntry<float> cWingLiftRef;

		private static ConfigEntry<float> cWingLiftStart;

		private static ConfigEntry<bool> cBodyBoost;

		private static ConfigEntry<float> cBodyBoostGain;

		private static ConfigEntry<float> cBodyBoostStart;

		private static ConfigEntry<float> cBodyBoostPeak;

		private static ConfigEntry<bool> cBodyNeutral;

		private static ConfigEntry<float> cBodyFore;

		private static ConfigEntry<float> cBodyAft;

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


		private static ConfigEntry<float> cRollStopSlip;

		private static ConfigEntry<float> cPushSlip;


		private static ConfigEntry<bool> cYawSlewLead;






		private static ConfigEntry<bool> cRollGovPitch;


		private static ConfigEntry<bool> cRollStopSlipProbe;
		private static ConfigEntry<float> cRollPaceRateSlip;
		private static ConfigEntry<float> cRollPaceRateBlend;


		private static ConfigEntry<int> cGovRev;



		private static ConfigEntry<float> cSplitFlapRollYield;

		private static ConfigEntry<bool> cSplitFlapYaw;

		private static ConfigEntry<float> cSplitFlapYawUse;

		private static ConfigEntry<float> cRollPitchLead;

		private static ConfigEntry<bool> cStabsPitchOnly;

		private static ConfigEntry<bool> cSlatRelief;

		private static ConfigEntry<bool> cCoupGateLim;

		private static ConfigEntry<bool> cLoadedRollSteady;

		private static ConfigEntry<bool> cSlipYawW;

		private static ConfigEntry<float> cFinCamber;

		private static ConfigEntry<bool> cCapLag;

		private static ConfigEntry<float> cWingCamber;

		private static ConfigEntry<float> cLeadK;

		private static ConfigEntry<float> cLeadTau;

		private static ConfigEntry<bool> cFuelBal;

		private static ConfigEntry<bool> cFuelAttached;

		private static ConfigEntry<bool> cFuelBalLog;

		private static ConfigEntry<float> cFuelRate;

		private static ConfigEntry<float> cFuelTolLat;

		private static ConfigEntry<float> cFuelTolLong;

		private static ConfigEntry<bool> cRudPitchAid;

		private static ConfigEntry<float> cRudToeCost;

		private static ConfigEntry<float> cRudToeMax;

		private static ConfigEntry<float> cRudToeGain;

		private static ConfigEntry<bool> cGroundRate;

		private static ConfigEntry<float> cGroundRotRamp;

		private static ConfigEntry<float> cStallStab;

		private static ConfigEntry<string> cForeLiftSched;

		private static ConfigEntry<string> cAftLiftSched;

		private static ConfigEntry<float> cStallInner;

		private static ConfigEntry<float> cStallOuter;

		private static ConfigEntry<float> cStabSlope;

		private static ConfigEntry<float> cStabCL;

		private static ConfigEntry<float> cTvcNeutral;

		private static ConfigEntry<bool> cStabPark;

		private static ConfigEntry<float> cSettledMargin;
		private static ConfigEntry<float> cPushMinRate;


		private static ConfigEntry<float> cSettledOn;

		private static ConfigEntry<float> cSettledOff;

		private static ConfigEntry<float> cSettledWalk;

		private static ConfigEntry<float> cRateCapLag;

		private static ConfigEntry<float> cFinLossCap;

		private static ConfigEntry<bool> cRollFightRel;

		private static ConfigEntry<bool> cAbFade;

		private static ConfigEntry<float> cAbShareFull;

		private static ConfigEntry<float> cAbShareZero;

		private static ConfigEntry<float> cStallSlope;

		private static ConfigEntry<int> cAirframeRev;

		private static ConfigEntry<float> cCgShift;

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

		private static ConfigEntry<float> cRudSideCap;

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

		private static ConfigEntry<bool> cNozAnimAI;

		private static ConfigEntry<bool> cNozAnim;

		private static ConfigEntry<float> cAsymPitchMargin;

		private static ConfigEntry<bool> cAsymRollCoupling;

		private static ConfigEntry<bool> cRollPitchPriority;

		private static ConfigEntry<float> cRollFightCost;

		private static ConfigEntry<float> cNozSeamShade;

		private static ConfigEntry<float> cPitchFight;

		private static ConfigEntry<float> cRollPitchReserve;

		private static ConfigEntry<bool> cNozIdleOpen;

		private static ConfigEntry<float> cNozRate;

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

		private static ConfigEntry<float> cRollPitchFF;

		private static ConfigEntry<float> cRollPitchFFAoA;

		private static ConfigEntry<float> cAsymFFBeta;

		private static ConfigEntry<float> cAsymFFAoA;



		private static ConfigEntry<float> cLowQReg;

		private static ConfigEntry<float> cRollStop;

		private static ConfigEntry<float> cRollAccUse;

		private static ConfigEntry<float> cRollAccUseLow;

		private static ConfigEntry<float> cGndBrake;

		private static ConfigEntry<float> cGndBrakeMin;

		private static ConfigEntry<float> cGndBrakeFlap;

		private static ConfigEntry<float> cGndBrakeStab;

		private static ConfigEntry<float> cGndBrakeRud;

		private static ConfigEntry<float> cHlMaxAoA;

		private static ConfigEntry<float> cHlHighAoA;

		private static ConfigEntry<float> cGndBrakeTvc;

		private static ConfigEntry<float> cRotMinSpeed;

		private static ConfigEntry<float> cSlatStart;

		private static ConfigEntry<float> cSlatFull;

		private static ConfigEntry<float> cGndBrakeThr;

		private static ConfigEntry<float> cGUnder;

		private static ConfigEntry<float> cFFMax;

		private static ConfigEntry<float> cFFStabCost;

		private static ConfigEntry<bool> cGndBrakeSplit;

		private static ConfigEntry<bool> cThrIndepBrake;

		public static bool ThrottleIndependentAirbrake => cThrIndepBrake == null || cThrIndepBrake.Value;


		private static ConfigEntry<string> cVentDoors;


		public static bool Enabled => cEnabled.Value && MpPolicy.FcsAllowed;

		public static bool TelemetryEnabled => cTelemetry.Value;

		// 3.0.4
		public static bool EstimatorThreaded => cEstThreaded == null || cEstThreaded.Value;

		public static bool TickTimerLog => cTickTimer == null || cTickTimer.Value;

		// F-22E 4.1.3: log a tick slower than this (us) with its breakdown; 0 = off
		public static float SlowTickLogUs => (cSlowTick == null) ? 1500f : cSlowTick.Value;

		private static ConfigEntry<float> cSlowTick;

		// 3.1.0
		public static bool ParallelTables => cParTables == null || cParTables.Value;

		public static bool ParallelAllocator => cParAlloc == null || cParAlloc.Value;

		// 4.0.0
		public static int NativeAllocator => (cNativeAlloc == null) ? 2 : cNativeAlloc.Value;

		public static int NativeAllocatorCheck => (cNativeAllocCheck == null) ? 0 : cNativeAllocCheck.Value;

		// 4.1.0
		public static bool NativeSweep => cNativeSweep == null || cNativeSweep.Value;

		public static bool NativeCoupledCap => cNativeCpl == null || cNativeCpl.Value;

		public static bool AsymCoupledCheck => (cAsymCplOn == null) ? false : cAsymCplOn.Value;

		public static float AsymCoupledReserveMax => (cAsymCplResMax == null) ? 0.7f : Mathf.Clamp(cAsymCplResMax.Value, 0.2f, 0.9f);

		public static float AsymCoupledReserveMaxNoTvc => (cAsymCplResMaxNt == null) ? 0.8f : Mathf.Clamp(cAsymCplResMaxNt.Value, 0.2f, 0.9f);

		public static bool NativeTables => cNativeTables == null || cNativeTables.Value;

		// 3.1.2
		public static bool ParallelSweep => cParSweep == null || cParSweep.Value;

		// 4.0.1
		public static bool SweepGridCache => cSweepGrid == null || cSweepGrid.Value;

		// 3.1.3
		public static bool ParallelEnvelope => cParEnv == null || cParEnv.Value;

		public static bool AIFlightControl => cAIFcs.Value;

		public static bool AIFlightLog => cAILog.Value;

		public static bool AIDirectRoll => cAIDirectRoll.Value;

		public static float StabRangeTEUp => Mathf.Clamp(cStabUp.Value, 0f, 45f);

		public static float StabRangeTEDown => Mathf.Clamp(cStabDn.Value, 0f, 45f);

		public static float RudderRange => Mathf.Clamp(cRudRange.Value, 0f, 45f);

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

		public static float StabDownwash => Mathf.Clamp(cDownwash.Value, 0f, 0.7f);

		public static float StabPeakMargin => (cStabPeakMargin != null) ? Mathf.Clamp(cStabPeakMargin.Value, 0f, 90f) : 90f;

		public static bool BodyLiftShaping => cBodyLift.Value;

		public static float BodyLiftFore => Mathf.Clamp(cBodyFore.Value, 0.3f, 2f);

		public static float BodyLiftAft => Mathf.Clamp(cBodyAft.Value, 0.3f, 3f);

		public static string ForeLiftSchedule => cForeLiftSched.Value;

		public static string AftLiftSchedule => cAftLiftSched.Value;

		// 2.4.6: positive-AoA lift shaping of the wing panels and the fuselage (LiftShape.cs); lift only
		public static bool WingLiftShaping => cWingLift != null && cWingLift.Value;

		public static float WingLiftPeakAoA => (cWingLiftPeak != null) ? Mathf.Clamp(cWingLiftPeak.Value, 20f, 50f) : 36f;

		public static float WingLiftPeakGain => (cWingLiftGain != null) ? Mathf.Clamp(cWingLiftGain.Value, 0.8f, 1.5f) : 1.239f;

		public static float WingLiftRefAoA => (cWingLiftRef != null) ? Mathf.Clamp(cWingLiftRef.Value, 10f, 50f) : 30f;

		public static float WingLiftRampStart => (cWingLiftStart != null) ? Mathf.Clamp(cWingLiftStart.Value, 0f, 35f) : 12f;

		public static bool BodyLiftBoost => cBodyBoost != null && cBodyBoost.Value;

		public static float BodyLiftBoostGain => (cBodyBoostGain != null) ? Mathf.Clamp(cBodyBoostGain.Value, 0.8f, 1.5f) : 1.2f;

		public static float BodyLiftBoostStart => (cBodyBoostStart != null) ? Mathf.Clamp(cBodyBoostStart.Value, 0f, 40f) : 12f;

		public static float BodyLiftBoostPeakAoA => (cBodyBoostPeak != null) ? Mathf.Clamp(cBodyBoostPeak.Value, 20f, 45f) : 42f;

		public static bool BodyLiftPitchNeutral => cBodyNeutral == null || cBodyNeutral.Value;

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

		public static float LerxEffectiveness => cLerxEff.Value;

		public static bool StallStagger => cStagger.Value;

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

		public static float FinCamberK => (cFinCamber == null) ? 0.5f : Mathf.Clamp(cFinCamber.Value, 0f, 1f);

		public static float WingCamberK => (cWingCamber == null) ? 0f : Mathf.Clamp(cWingCamber.Value, 0f, 1f);

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

		public static bool RudderPitchAid => cRudPitchAid.Value;

		public static float RudderToeCost => Mathf.Max(0.001f, cRudToeCost.Value);

		public static float RudderToeMax => Mathf.Clamp(cRudToeMax.Value, 0f, 35f);

		public static float RudderToeGain => Mathf.Max(0f, cRudToeGain.Value);

		public static bool GroundRateLaw => cGroundRate.Value;

		public static float GroundRotationRamp => Mathf.Max(1f, cGroundRotRamp.Value);

		public static float StabStallAoA => cStallStab.Value;

		public static float InnerFlaperonStallAoA => cStallInner.Value;

		public static float OuterFlaperonStallAoA => cStallOuter.Value;

		public static float StallStaggerLiftSlope => cStallSlope.Value;

		public static bool RollFightReleased => cRollFightRel != null && cRollFightRel.Value;

		public static bool AirbrakeAoAFade => cAbFade == null || cAbFade.Value;

		public static float AirbrakeFadeShareFull => cAbShareFull == null ? 0.4f : Mathf.Clamp01(cAbShareFull.Value);

		public static float AirbrakeFadeShareZero => cAbShareZero == null ? 0.15f : Mathf.Clamp01(cAbShareZero.Value);

		public static float FinLossAoACap => cFinLossCap == null ? 65f : Mathf.Clamp(cFinLossCap.Value, 0f, 65f);

		public static bool StabParkEscape => cStabPark == null || cStabPark.Value;

		// 3.1.4 (push-over minimum rate; 3.1.5: 23 deg/s, no sideslip guard)
		public static float PushOverMinRate => (cPushMinRate == null) ? 23f : Mathf.Clamp(cPushMinRate.Value, 0f, 60f);
		public static float SettledStallMargin => (cSettledMargin == null) ? 6f : Mathf.Clamp(cSettledMargin.Value, 0f, 20f);


		public static float SettledRateOn => (cSettledOn == null) ? 10f : Mathf.Clamp(cSettledOn.Value, 1f, 60f);

		public static float SettledRateOff => (cSettledOff == null) ? 20f : Mathf.Max(SettledRateOn + 1f, cSettledOff.Value);

		public static float SettledWalkRate => (cSettledWalk == null) ? 30f : Mathf.Clamp(cSettledWalk.Value, 1f, 100f);

		public static float RateCaptureLag => (cRateCapLag == null) ? 1f : Mathf.Clamp(cRateCapLag.Value, 0f, 3f);

		public static float TvcNeutralHold => cTvcNeutral == null ? 1000f : Mathf.Max(0f, cTvcNeutral.Value);

		public static float StabCLScale => cStabCL == null ? 1f : Mathf.Clamp(cStabCL.Value, 0.5f, 2f);

		public static float StabLiftSlope => cStabSlope == null ? -1f : Mathf.Clamp(cStabSlope.Value, -1f, 1f);

		public static float CgShiftAft => cCgShift.Value;

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

		public static float RudderSideslipYawCap => cRudSideCap.Value;

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

		public static bool NozzleAnimation => cNozAnim.Value;

		public static bool NozzleAnimationAI => cNozAnimAI.Value;

		public static float AsymPitchMargin => cAsymPitchMargin.Value;

		public static bool AsymRollCoupling => cAsymRollCoupling.Value;

		public static bool RollPitchPriority => cRollPitchPriority.Value;

		public static float RollFightCost => cRollFightCost.Value;

		public static float NozzleSeamShade => cNozSeamShade.Value;

		public static float RollPitchFightCost => cPitchFight.Value;

		public static float RollPitchReserve => cRollPitchReserve.Value;

		public static bool NozzleIdleOpen => cNozIdleOpen.Value;

		public static float NozzleAreaRate => cNozRate.Value;

		public static float YawPriority => cYawPri.Value;

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

		public static float RudderFadeStart => cRudStart.Value;

		public static float RudderFadeEnd => cRudEnd.Value;

		public static float UnloadDecel => cUnloadDecel.Value;

		public static float UnloadRiseGain => cUnloadRise.Value;

		public static float MaxRollAccel => cRollAcc.Value;

		private static ConfigEntry<float> cDecelLook;

		private static ConfigEntry<float> cDecelStart;

		private static ConfigEntry<float> cDecelFull;

		private static ConfigEntry<float> cAsymCapDemand;

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

		private static ConfigEntry<float> cEngOutCap;

		private static ConfigEntry<float> cEngDmgCap;

		private static ConfigEntry<float> cThrResp;

		private static ConfigEntry<float> cMilThrust;

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

		public static float EngineDamagedAoACap => (cEngDmgCap != null) ? Mathf.Clamp(cEngDmgCap.Value, 10f, 65f) : 50f;

		public static float ThrottleResponseScale => (cThrResp != null) ? Mathf.Clamp(cThrResp.Value, 0.25f, 4f) : 1f;

		public static float MilThrustScale => (cMilThrust != null) ? Mathf.Clamp(cMilThrust.Value, 0.5f, 2f) : 1.05f;

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

		// 2.4.0 multiplayer: the Local* values are this machine's settings; the plain ones are what applies here, which as a
		// client is the host's policy (MpPolicy): the host's variant when it locks it, stock otherwise.
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
		public static float DragReliefAoA => MpPolicy.FcsAllowed ? Mathf.Clamp(cStockDrAoA.Value, 0f, 0.5f) : 0f;

		public static float DragReliefAoAHigh => MpPolicy.VariantLocked ? MpPolicy.Host.drAoAHigh : (MpPolicy.StockForced ? 0f : LocalDragReliefAoAHigh);

		public static float DragReliefFadeStart => Mathf.Clamp(cStockDrFade0.Value, 5f, 60f);

		public static float DragReliefFadeEnd => Mathf.Clamp(Mathf.Max(cStockDrFade1.Value, cStockDrFade0.Value + 1f), 6f, 70f);

		public static float DragReliefTransonic => MpPolicy.VariantLocked ? MpPolicy.Host.drTrans : (MpPolicy.StockForced ? 0f : LocalDragReliefTransonic);


		public static float KpG => cKpG.Value;

		public static float KiG => cKiG.Value;

		public static float FlaperonEffectiveness => cFlaperonEff.Value;

		public static float StabilatorEffectiveness => cStabEff.Value;

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

		public static float GroundAeroBrake => Mathf.Clamp01(cGndBrake.Value);

		public static float GroundBrakeMinSpeed => cGndBrakeMin.Value;

		public static float GroundBrakeFlap => cGndBrakeFlap.Value;

		public static float GroundBrakeStab => cGndBrakeStab.Value;

		public static float GroundBrakeRudder => cGndBrakeRud.Value;

		public static float HighLiftMaxAoA => cHlMaxAoA.Value;

		public static float SlatAoAStart => cSlatStart.Value;

		public static float SlatAoAFull => cSlatFull.Value;

		public static float HighLiftHighAoA => Mathf.Clamp01(cHlHighAoA.Value);

		public static float GroundBrakeTvc => cGndBrakeTvc.Value;

		public static float GroundBrakeThrottle => cGndBrakeThr.Value;

		public static bool GroundBrakeLog => cGndBrakeLog.Value;

		public static bool AirbrakeSplit => cGndBrakeSplit.Value;

		public static string VentDoors => cVentDoors.Value;

		public static float RotationMinSpeed => Mathf.Max(20f, cRotMinSpeed.Value);

		public static bool RevFlowRollSense => cRevRoll.Value;
		public static bool RevFlowNoseAxis => cRevNose == null || cRevNose.Value;

		public static float AsymRollTau => cAsymRollTau.Value;

		public static float AsymRelaxBoost => cAsymRelaxBoost.Value;

		public static float RollPitchFF => cRollPitchFF.Value;

		public static float RollPitchFFAoA => cRollPitchFFAoA.Value;

		public static float GUndershootCapture => cGUnder.Value;

		public static float RollPitchFFMax => Mathf.Max(1f, cFFMax.Value);

		public static float RollPitchFFStabCost => cFFStabCost.Value;

		public static void Init(ConfigFile cfg, ConfigFile dev, bool devUnlocked, bool devFresh, ManualLogSource log)
		{
			Log = log;
			cProfile = cfg.Bind("1. General", "PerformanceProfile", PerformanceProfile.Stock, "Stock (default) = Aryx's original thrust, parasitic drag and fuel burn; from 2.2.3 (rebuilt) 35 % of the AoA-dependent drag is removed up to 25 deg AoA, faded out by 30 (post-stall drag is Aryx's), see StockAoADragRelief. Buffed = thrust x1.08, parasitic drag x0.885, the same low-AoA drag relief going over to 0.25 post-stall, transonic drag relief 0.2, fuel burn x0.75. Countermeasures and gun ammo come from the matching Countermeasures section below. The flight control is the same in both. Switch before spawning: thrust, drag, countermeasures and ammo are set when a jet spawns and stay until it respawns; fuel burn follows the setting live.");
			cThrIndepBrake = cfg.Bind("1. General", "ThrottleIndependentAirbrake", defaultValue: true, "2.2.6: what commands the brake. true = the brake key / axis only; idle throttle is just idle. In the AIR the brake key is the stock speedbrake (the airframe's split / speedbrake panels, opened only as far as AirbrakeAoAFade allows); on the GROUND (main wheels down) the rollout aero-brake (Ground section of the dev file: flaperons and ailerons up, stabilators up, nozzles up, rudders toed in) and never the speedbrake. false = 2.4.15: fully stock: throttle at zero opens the panels fully, in the air and on the ground; the brake key is the wheel brake only (it no longer opens the airbrake, so Brake and Countermeasures can share a controller button); no rollout aero-brake. Only while the FCS flies the jet.");
			cEnabled = cfg.Bind("1. General", "Enabled", defaultValue: true, "Replace the stock fly-by-wire with the F-22E FCS (player aircraft only).");
			cHostAllowFcs = cfg.Bind("8. Multiplayer (host)", "HostAllowFcs", defaultValue: true, "2.4.0: read on the HOST (or dedicated server). true = players who run this plugin may fly their F-22Es with its control laws and airframe changes on this server. false = every client flies Aryx's stock F-22E and fly-by-wire. A client whose host does not run this plugin always flies stock (fly-by-wire, airframe, thrust, drag, fuel burn, countermeasures and ammo), whatever its own settings. 2.4.1: the host's own jet is bound by this switch too (false = the host flies stock as well). While this or HostLockVariant is on, the server is flagged as modded (server browser / NOMM 'Modded' tag).");
			// 2.4.6: HostLockVariant on by default. A player file written by an older build still says "# Default value: false"
			// above the key; if it also still holds false (the old default), it is moved to true once. The file is then saved,
			// which rewrites the default line, so a later false set by the player stays.
			bool hostLockOldFile = HostLockOldDefault(cfg.ConfigFilePath);
			cHostLock = cfg.Bind("8. Multiplayer (host)", "HostLockVariant", defaultValue: true, "2.4.0: read on the HOST. 2.4.6: on by default. true = every client gets the host's variant: the host's PerformanceProfile with its thrust, drag and fuel-burn scaling and the host's countermeasure and gun-ammo capacities from the matching Countermeasures section. false = every client flies the Stock variant with stock countermeasures and ammo (their own profile and capacity settings are ignored while connected). 2.4.1: with the lock off the host flies the Stock variant on its own server as well; the host's PerformanceProfile is used in multiplayer only through this lock. Changes are sent to the clients at once and apply at the next spawn.");
			if (hostLockOldFile)
			{
				if (!cHostLock.Value)
				{
					cHostLock.Value = true;
				}
				cfg.Save();
				Log.LogInfo("2.4.6 defaults: HostLockVariant on.");
			}
			cRateOnly = cfg.Bind("1. General", "PitchRateCommandOnly", defaultValue: false, "2.0.6: true = the pitch stick is a pitch-rate command in every mode (gear up or down, on the runway, Stability Assist on or off), like the gear-down law. With Stability Assist on, the AoA and G limits and AoA protection still apply; with it off (MPO) the MPO limits apply as before. false = AoA/G command with the gear up (default). Takes effect at once.");
			cPitchLaw = cfg.Bind("1. General", "PitchCommandLaw", PitchCommandLaw.GAndPitchRate, "2.1.0: the gear-up pitch law (Stability Assist on). GAndPitchRate (default) = the stick commands pitch rate where full G is out of reach (low IAS) and G where it is in reach, blended as before; no AoA command - the AoA limits, the low-thrust / asymmetric-load caps and AoA protection still apply as limits. GAoAAndPitchRate = the 1.9-2.0 law: AoA command at low IAS (stick curve in section 5), G command at high IAS, pitch-rate command past the hand-over AoA. AoAThenRateHold (2.1.8) = as GAoAAndPitchRate, but once the stick is steady and the AoA has settled on its command, the pitch rate the jet has then is held instead of the AoA (the AoA drifts with speed); moving the stick goes back to AoA command. PitchRateCommandOnly = true overrides all of them. Takes effect at once.");
			cFuelBal = dev.Bind("6. Fuel", "FuelBalancing", defaultValue: true, "2.0.6: move fuel between the healthy tanks to hold the centre of mass where the clean jet has it (centreline; stock fore/aft position without stores), against store asymmetry, a part shot off or a leaking tank. Only between attached, non-leaking, non-burning tanks; mass is conserved. All F-22Es simulated on this machine.");
			cEngOutCap = dev.Bind("Asymmetric load", "EngineOutAoACap", 30f, "2.0.13: with an engine out the AoA limit is this, both signs, steady (plus whatever an aerodynamic asymmetry from damage needs on top). The dead engine's thrust yaw is left to the VMC protection (throttle, last resort) instead of the AoA cap, which made the limit follow speed and throttle (12 <-> 65 deg within seconds in 2.0.12).");
			cAsymCplOn = dev.Bind("Asymmetric load", "AsymCoupledCheck", false, "4.2.0: with a lateral CG offset, take the AoA cap from the coupled check (roll each way with the load held, pitch held, no sideslip acceleration; every surface and, while vectoring, the nozzles counted) instead of the one-axis check. Off by default in 4.2.0: on the bench it held 16-23 deg with 30 cm where 4.1.5 gave 12, but with 16 cm a roll spam at 600 km/h went to 50 deg AoA and 22-26 deg of sideslip, and with 10 cm the limit opened to 65 deg and dipped to 21 deg twice per pull below 55 m/s (4.1.5: 27-30 deg, steady). Takes effect at the next sweep (0.2 s).");
			cAsymCplResMax = dev.Bind("Asymmetric load", "AsymCoupledReserveMax", 0.7f, "4.2.0: with a lateral CG offset the AoA cap comes from the coupled check: at each AoA, the most flight-path roll each way with the load held, pitch held and no sideslip acceleration, every surface (and with vectoring on and the thrust up, the nozzles) counted. This is the largest share of that roll range the pilot's reserve may claim; the cap sits where the weaker side is down to about this share of the mean of the two sides. Higher = lower cap, more roll left. Takes effect at the next sweep (0.2 s).");
			cAsymCplResMaxNt = dev.Bind("Asymmetric load", "AsymCoupledReserveMaxNoTvc", 0.8f, "4.2.0: the same share for the coupled check with the nozzles locked, cut out by speed or the thrust low (T/W under LowThrustFixedAoATW). There the surfaces also make the pitch-in, so more of the roll range is kept. Higher = lower cap, more roll left. Takes effect at the next sweep (0.2 s).");
			cEngDmgCap = dev.Bind("Asymmetric load", "EngineDamagedAoACap", 50f, "2.4.2: 50 (first 0.4.1 / 2.4.1 cut: 30, the engine-out cap; a running engine's split is a third to a half of a dead one's, and below the speed where the fins could not hold it the thrust match now takes over on prediction, so the cap only says where the full split is traded for matched thrust). 2.4.1 (Ifrit 0.4.1): with one engine running but down on thrust (the measured weak / strong thrust split under 0.85, release above 0.92), the AoA limit is this, both signs, instead of the dynamic thrust cap (22 Oct 23:18 log: the dynamic cap went 65 <-> 12 at 40-65 m/s and pushed the nose down each time). The game derates a damaged engine's whole throttle-to-RPM map (reachable RPM = minRPM + (maxRPM - minRPM) x (0.5 x condition + 0.5)); the afterburner needs 85 % of maxRPM, so it is lost below condition 0.55. Default = EngineOutAoACap; a running engine's split is smaller than a dead one's, so this can sit higher.");
			cThrResp = dev.Bind("5. Airframe", "ThrottleResponseScale", 1f, "2.4.1: throttle response (2.4.6: back to the game's own, 1) for every variant (Stock included): the engines' spool rate (RPM per second toward the throttle's target) and the afterburner's light-up / fade speed are multiplied by this at the airframe setup. 1 = the mod's own values.");
			cMilThrust = dev.Bind("5. Airframe", "MilThrustScale", 1.05f, "2.4.4: military (dry) thrust multiplier for every variant (Stock included), applied on top of the variant's ThrustScale at the airframe setup; the afterburner's own thrust is unchanged, so max-AB total rises by the dry share only. Fuel used per throttle setting is unchanged (the game's fuel flow follows the engine RPM, not its thrust). 1 = 2.4.3. Takes effect on the next spawn.");
			cEngAbGate = dev.Bind("Asymmetric load", "EngineAbAoAGate", true, "2.4.1 (Ifrit 0.4.1): thrust match. With one engine down on thrust (predicted from the engines' damage state) and the AoA above EngineDamagedAoACap, the healthy engine is held down to the damaged one's thrust at whatever throttle you set (its parasitic thrust loss, unused on this jet, set each tick from the measured RPMs), and if the damaged engine cannot light its afterburner the throttle is held at military power: no thrust split, so no AoA cap for it - you keep the full AoA, and the thrust comes back once you bring the AoA down to the cap yourself (on above cap + 2, off under cap + 0.5). Below the cap the throttle is free; the thrust split then brings EngineDamagedAoACap. With an engine OUT there is nothing to match: above EngineOutAoACap the throttle is held at military power. The VMC throttle protection (measured sideslip run-away) stays as the last resort. false = 2.4.0.");
			cFuelTrim = dev.Bind("6. Fuel", "FuelTrim", defaultValue: true, "2.0.12: when the yaw or roll authority runs short (an engine out, a fin or wing section shot off), the balancing moves the CG sideways with fuel instead of holding it on the centreline: toward the live engine to shorten its thrust lever arm, or against an aerodynamic asymmetry. Only once the yaw or roll usage passes 0.75, only as far as evens out the roll and yaw usage (a sideways CG costs roll: ~15 deg/s^2 per g per 10 cm), at most 30 cm; back to the centreline once both are under 0.5 for 2 s. false = centreline always (2.0.6-2.0.11).");
			cFuelTrimRate = dev.Bind("6. Fuel", "FuelTrimRate", 240f, "2.0.12: fuel moved per second (kg/s, in total) while the fuel trim works (normal balancing: FuelTransferRate). 2.4.1: 240 (was 120).");
			cFuelRate = dev.Bind("6. Fuel", "FuelTransferRate", 80f, "2.0.6: total fuel the balancing moves per second (kg/s). 2.4.1: 80 (was 40).");
			cFuelTolLat = dev.Bind("6. Fuel", "FuelBalanceLateralTolerance", 0.01f, "2.0.6: lateral CG error (m) the balancing weighs as one unit; it leaves the fuel alone inside half of it.");
			cFuelTolLong = dev.Bind("6. Fuel", "FuelBalanceLongitudinalTolerance", 0.03f, "2.0.6: fore/aft CG error (m) the balancing weighs as one unit; it leaves the fuel alone inside half of it.");
			cFuelAttached = dev.Bind("6. Fuel", "FuelFromAttachedTanksOnly", defaultValue: true, "2.0.6: engines draw fuel only from tanks still attached, and the fuel gauge / fuel checks count only those (against the full original capacity); a tank on a part that has been shot off is not refuelled. false = stock (a shot-off tank's fuel keeps feeding the engines and showing on the gauge).");
			cFuelBalLog = dev.Bind("0. Debug", "FuelBalanceLog", defaultValue: false, "2.0.6: log the fuel balancing's plan (CG error and the fuel it moves) when it starts and every 10 s while it works.");
			cFlareScaleStock = cfg.Bind("3. Countermeasures - Stock profile", "FlareCountScale", 1f, "Flare capacity multiplier (the load at spawn and what rearming refills to). Used when PerformanceProfile = Stock. 1 = stock. All F-22Es, applied at spawn.");
			cGunAmmoStock = cfg.Bind("3. Countermeasures - Stock profile", "GunAmmoScale", 1f, "Gun (20 mm) ammunition multiplier: magazine capacity, so the load at spawn and what rearming refills to. Used when PerformanceProfile = Stock. 1 = stock. All F-22Es, applied at spawn.");
			cEwCapStock = cfg.Bind("3. Countermeasures - Stock profile", "EWCapacityScale", 1f, "Electrical storage multiplier for the EW/jammer power supply (more jamming time per charge). Used when PerformanceProfile = Stock. 1 = stock. All F-22Es, applied at spawn.");
			cEwRateStock = cfg.Bind("3. Countermeasures - Stock profile", "EWRechargeScale", 1f, "Recharge-rate multiplier for the EW/jammer power supply (engine-driven charging). Used when PerformanceProfile = Stock. 1 = stock. All F-22Es, applied at spawn.");
			cFlareScaleBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "FlareCountScale", 10f, "Flare capacity multiplier (the load at spawn and what rearming refills to). Used when PerformanceProfile = Buffed. 1 = stock. All F-22Es, applied at spawn.");
			cGunAmmoBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "GunAmmoScale", 2f, "Gun (20 mm) ammunition multiplier: magazine capacity, so the load at spawn and what rearming refills to. Used when PerformanceProfile = Buffed. 1 = stock. All F-22Es, applied at spawn.");
			cEwCapBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "EWCapacityScale", 1.5f, "Electrical storage multiplier for the EW/jammer power supply (more jamming time per charge). Used when PerformanceProfile = Buffed. 1 = stock. All F-22Es, applied at spawn.");
			cEwRateBuffed = cfg.Bind("4. Countermeasures - Buffed profile", "EWRechargeScale", 1.2f, "Recharge-rate multiplier for the EW/jammer power supply (engine-driven charging). Used when PerformanceProfile = Buffed. 1 = stock. All F-22Es, applied at spawn.");
			cTelemetry = dev.Bind("0. Debug", "Telemetry", defaultValue: false, "Write a 50 Hz CSV of the FCS state to BepInEx/F22E_FCS_<time>.csv. 4.1.3: switching it off in flight stops the rows (the file stays open) and switching it on resumes them, so its cost can be timed in flight.");
			cEstThreaded = dev.Bind("0. Debug", "EstimatorThreaded", defaultValue: true, "3.0.4: run the capacity estimator's allocator probes (about half of the FCS's CPU time per tick) on a worker thread instead of the game's main thread. The flying is identical either way: the control laws read the previous tick's estimate in both cases, and the worker solves on its own copies of the surfaces. false = the 3.0.3 single-thread path. Takes effect at once.");
			cSlowTick = dev.Bind("0. Debug", "SlowTickLogUs", 1500f, "4.1.3 (diagnostic): log any of the player's FCS ticks slower than this (us) on its own line, with the tick's 13 segments, whether a garbage collection ran during it and the managed heap's growth over it (at most 10 lines per 10 s). 0 = off. Drives nothing. Takes effect at once.");
			cTickTimer = dev.Bind("0. Debug", "TickTimerLog", defaultValue: true, "3.0.4: every 10 s, log one line to BepInEx/LogOutput.log with the FCS's main-thread time per physics tick (mean / p95 / max in microseconds, player and AI F-22Es separately) and how often the main thread had to wait for the estimator's worker. Drives nothing.");
			cParTables = dev.Bind("0. Debug", "ParallelTables", defaultValue: true, "3.1.0: the surfaces' moment tables (rebuilt every physics tick) are computed on several cores: the game's objects are read on the main thread, the table maths runs on the thread pool and this thread together. Identical tables either way. false = one thread (3.0.4). Takes effect at once.");
			cParAlloc = dev.Bind("0. Debug", "ParallelAllocator", defaultValue: true, "3.1.0: when the allocator's two-horizon pick is active (stabs commanded further than they can travel in their reach time, mostly during manoeuvres), its reach-plan solve runs on a second core while the main thread does the destination solve. Same pick, same commands either way. false = one thread (3.0.4). Takes effect at once.");
			cNativeAlloc = dev.Bind("0. Debug", "NativeAllocator", 2, new ConfigDescription("4.1.0: default 2 (4.0.x: 1). The FCS's native core, F22E_FcsCore.dll, carried inside this plugin's DLL (4.1.4) (Rust): the control allocator's solves (the main ones each tick and the capacity estimator's probes), and with NativeSweep / NativeTables the asymmetric-load sweep's inner loop and the surface tables' maths. 2 = use it in the game: several times faster, the same laws, the bench's float arithmetic instead of Mono's (Unity runs Mono with floats carried in double, so the C# code differs from any compiled code in the last digits there, and the allocator's picks turn some of those into a different, mostly equal-cost answer for a tick). 1 = use it only where it returns exactly what the C# code returns, bit for bit (the bench; in the game that means C#). 0 = never: the C# code, as 3.2.1. With the DLL missing or failing its checks the C# code runs. Takes effect at once; the tick timer's log line reports what is in use.", new AcceptableValueList<int>(0, 1, 2)));
			cNativeAllocCheck = dev.Bind("0. Debug", "NativeAllocatorCheck", 0, new ConfigDescription("4.1.0: default 0 (4.0.x: 1). When the native allocator is in use, how it is compared with the C# one while flying. 0 = not at all: every solve is the native one. 1 = each allocator's first 64 solves and every 256th after them are solved both ways, and the C# result is the one used on those (in the 10 Nov 16:48 flight those ticks carried a one-tick surface blip about three times as often as the rest). 2 = every solve both ways, the C# result used on all of them (a comparison flight: the jet flies C# and pays for both). The tick timer's log line reports the counts: identical, close (commands within 0.001 deg), an equal-cost alternative (costs within 0.1 %), different; in the game about 0.3 % different is normal (Mono's arithmetic against the bench's). Where float arithmetic is strict, anything but identical is a fault: one warning in BepInEx/LogOutput.log and the native solve is switched off until the game restarts. Takes effect at once.", new AcceptableValueList<int>(0, 1, 2)));
			cParSweep = dev.Bind("0. Debug", "ParallelSweep", defaultValue: true, "3.1.2: the asymmetric-load sweep (an AoA sweep of the whole airframe, three points per physics tick; the largest single FCS cost in the 23:31 log) reads the parts' state once per tick and computes its points on several cores. Identical results either way. false = one thread. Takes effect at once.");
			cSweepGrid = dev.Bind("0. Debug", "SweepGridCache", defaultValue: true, "4.0.1: the asymmetric-load sweep evaluates every grid deflection of every surface in each AoA point's airflow (about 230 evaluations per point, three points per sweep tick). On: what depends on the deflection alone (the surface's rotation at that deflection, its span axis and moment arm, and the same for the fin or wing section it cambers) is worked out once per sweep tick instead of once per point, and what depends on the airflow alone once per surface and point. Same maths, same order; byte-identical on the bench. Off = the 4.0.0 loop. Takes effect at once.");
			cNativeSweep = dev.Bind("0. Debug", "NativeSweep", defaultValue: true, "4.1.0: with the native core in use (NativeAllocator) and SweepGridCache on, the asymmetric-load sweep's capacity loop (every grid deflection of every surface in each AoA point's airflow) runs in F22E_FcsCore.dll: the rotations are still worked out here, the flow's part (one atan2, the airfoil table, the force and its moment, the min / max kept) there. Byte-identical on the bench. Off = the C# loop of 4.0.1. Takes effect at once.");
			cNativeCpl = dev.Bind("0. Debug", "NativeCoupledCap", defaultValue: true, "4.2.0 (from the Ternion 0.3.0): with the native core in use (NativeAllocator), the asymmetric-load check's coupled solves (the LP that finds the roll left each way with pitch held) run in the native core. Byte-identical on the bench. Off = the C# solver. Takes effect at once.");
			cNativeTables = dev.Bind("0. Debug", "NativeTables", defaultValue: true, "4.1.0: with the native core in use (NativeAllocator), the surfaces' moment tables (rebuilt every physics tick, 26-31 deflections per surface) get their per-deflection maths from F22E_FcsCore.dll: rotations here, the flow's part there; the live point and the table's post-processing stay here. Byte-identical on the bench. Off = the C# loop. Takes effect at once.");
			cParEnv = dev.Bind("0. Debug", "ParallelEnvelope", defaultValue: true, "3.1.3: the lift envelope (25 AoA points over every airframe part) and the hybrid pitch slopes, refreshed every 0.1 s, read the parts' state once and compute their points on several cores. Identical results either way. false = one thread. Takes effect at once.");
			cAIFcs = cfg.Bind("2. AI", "AIFlightControl", defaultValue: true, "AI-flown F-22Es (simulated on this machine, no human pilot) also fly the FCS: everything the player gets (allocator, TVC, flaps, limits, damage protection) except the pitch law, which is pitch-rate command with the stock fly-by-wire's own stick-to-rate gain (so the AI's autopilot tuning still fits), inside the FCS AoA/G limits. Telemetry is never written for AI jets. false = AI F-22Es fly the stock fly-by-wire on the modified airframe (pre-1.8.22).");
			cAILog = dev.Bind("0. Debug", "AIFlightLog", defaultValue: false, "Every 20 s, log one summary line per AI F-22E the FCS is flying (mode, IAS, AoA and g range, time in AoA protection), so AI jets can be checked from BepInEx/LogOutput.log after any mission. Each F-22E also logs once which control path it takes when it first reaches the flight-control hook.");
			cAIDirectRoll = cfg.Bind("2. AI", "AIDirectRoll", defaultValue: true, "AI F-22Es only: the AI's roll stick drives the velocity-vector roll rate almost directly (up to the jet's roll authority, still capped by the yaw authority at high AoA), like the stock fly-by-wire, instead of through the player's smooth roll trajectory. The AI's roll autopilot is a PID tuned for the stock response; the extra lag of the smooth trajectory made it overshoot and roll back and forth. false = 1.8.24 behaviour.");
			cAIGunRollFade = cfg.Bind("2. AI", "AIGunRollFade", defaultValue: true, "AI F-22Es only: while the AI points its nose (guns) at a moving target, its roll stick is faded out when the aim error is under ~6 deg (none under 1 deg). Near the aim point the AI's 'put the lift vector on the target' roll logic has no stable answer and flips between rolling left and right every second; pitch and rudder close the last few degrees instead. false = off.");
			cAoAPos = dev.Bind("2. Envelope", "AoALimitPositive", 65f, "AoA limit (deg) in AoA/G command and gear-down rate command.");
			cAoANeg = dev.Bind("2. Envelope", "AoALimitNegative", 50f, "Negative AoA limit magnitude (deg), with the nozzles covering pitch. 2.2.4: default 50 (was 45). Beyond about 50 the canted fins sit past their stall at negative AoA (fin local AoA -52..-59 at -55, -74..-79 at -65) and the flaperons/stabs lose their sideslip authority; in slow inverted corkscrews -55 departed with a heavy jet and -60/-65 with a light one. The low-thrust / DisableTVC cap (NoTvcAoACapNegative) still applies on top.");
			cAoANegRev = dev.Bind("2. Envelope", "AoALimitNegativeRevision", 0, "Internal: the AoALimitNegative default revision this file was last updated to. Leave it alone.");
			if (cAoANegRev.Value < 1)
			{
				// 2.2.4: default 45 -> 50. Runs once: a file still holding the old default 45 is moved; any other value is the
				// player's own and is kept. Afterwards the file is at revision 1 and 45 set later stays.
				if (Mathf.Abs(Mathf.Abs(cAoANeg.Value) - 45f) < 0.001f)
				{
					cAoANeg.Value = 50f;
					Log.LogInfo("F-22E FCS 2.2.4: AoALimitNegative moved from the old default 45 to the new 50 (once).");
				}
				cAoANegRev.Value = 1;
				dev.Save();
			}
			cGPos = dev.Bind("2. Envelope", "GLimitPositive", 9.5f, "Positive load limit (g), normal law.");
			cGNeg = dev.Bind("2. Envelope", "GLimitNegative", 3.5f, "Negative load limit magnitude (g), normal law.");
			cMpoGPos = dev.Bind("2. Envelope", "MPOGLimitPositive", 12.5f, "Positive load limit (g) with Stability Assist OFF (MPO).");
			cMpoGNeg = dev.Bind("2. Envelope", "MPOGLimitNegative", 3.5f, "Negative load limit magnitude (g) in MPO.");
			cMaxPitch = dev.Bind("2. Envelope", "MaxPitchRate", 60f, "Pitch-rate budget ceiling (deg/s). Default 60 since 2.4.9 (was 48); a file still holding an older value is moved to 60 once.");
			cProt = dev.Bind("2. Envelope", "AoARecoveryRate", 48f, "AoA-protection recovery rate (deg/s), independent of thrust and weight.");
			cMaxRoll = dev.Bind("3. Lateral-directional", "MaxRollRate", 250f, "Absolute body roll-rate ceiling (deg/s). Below ~250 m/s IAS the original-aircraft schedule and RollAuthorityUse set the rate; above it this ceiling holds the rate flat.");
			cMaxYaw = dev.Bind("3. Lateral-directional", "MaxYawRate", 75f, "Body yaw-rate limit (deg/s).");
			cRollLo = dev.Bind("3. Lateral-directional", "RollRateGainLowIAS", 1.2f, "Roll-rate ceiling vs the ORIGINAL aircraft's roll rate below ~120 m/s IAS (blends to the high-IAS gain by 170 m/s).");
			cRollHi = dev.Bind("3. Lateral-directional", "RollRateGainHighIAS", 1.1f, "Roll-rate ceiling vs the original aircraft's roll rate above ~170 m/s IAS.");
			cRollBrake = dev.Bind("3. Lateral-directional", "RollBrakeUse", 0.8f, "Fraction of the full roll authority (wings and stabilators) used to stop a roll, on top of roll damping. Before 1.8.26 this was a fraction of the wing-only authority.");
			cRollAuth = dev.Bind("3. Lateral-directional", "RollAuthorityUse", 1f, "Steady roll never needs more than this fraction of the available roll control moment (roll rate is given up instead of saturating the surfaces).");
			cCouple = dev.Bind("3. Lateral-directional", "CoupledAxisAoA", 36f, "Above this |AoA| the pedals blend into the stick's velocity-vector roll (full coupling 14 deg higher).");
			cPedalBeta = dev.Bind("3. Lateral-directional", "PedalSideslip", 15f, "Sideslip commanded by full pedal below the coupling AoA (deg).");
			cRudStart = dev.Bind("3. Lateral-directional", "RudderFadeStartAoA", 50f, "Rudders start fading out above this |AoA| (deg). 2.2.0: default 35 -> 50 (with RudderFadeEndAoA 50 -> 60). The game does not model vertical-tail blanking - the fins' local AoA is ~0.6 deg at 45 deg body AoA - so the old 35-50 fade only took yaw away: at a 45 deg AoA limit the rudders were held to a third of their travel (bench: sideslip in rolls / reversals at 45 deg 6-13 -> 3-7 deg with 50-60). Above 60 the rudders are faded as before.");
			cRudEnd = dev.Bind("3. Lateral-directional", "RudderFadeEndAoA", 60f, "Rudders fully faded (held neutral) above this |AoA| (deg), apart from RudderSideslipYawCap. 2.2.0: default 50 -> 60.");
			cRudSideCap = dev.Bind("3. Lateral-directional", "RudderSideslipYawCap", 2f, "1.8.35: past the rudder fade, the canted rudders' inverse roll is used to correct sideslip (at high AoA a body roll moves sideslip; from ~55 deg AoA the rudders make >3x more sideslip than velocity-vector roll, where the flaperons and stabs have lost theirs). They may only deflect in the sideslip-reducing direction, scaled in from 0.5 to 3 deg of sideslip error, and only as far as keeps the body yaw acceleration they make within this many deg/s^2 (their yaw per degree falls to ~0 by 80 deg AoA, so the travel opens up as AoA rises). 0 = off (1.8.34: rudders neutral above RudderFadeEndAoA).");
			cRudPitchAid = dev.Bind("3. Lateral-directional", "RudderPitchAid", defaultValue: true, "2.0.6: the canted rudders also work as a pitch effector (symmetric split; on canted fins the split's force has a vertical part, as the F/A-18 uses for its take-off rotation): toe one way pitches the nose up, the other way down. The allocator uses the symmetric split when the stabs and nozzles fall short of the pitch demand (rotation, low speed, low thrust), at RudderToeCost per unit of split, on top of the rudders' yaw use. Applied when the jet spawns. false = 2.0.5 (rudders are yaw/roll only).");
			cRudToeCost = dev.Bind("3. Lateral-directional", "RudderToeCost", 0.05f, "2.0.6: allocator cost of a symmetric rudder split (full split = this; 2.0.5 used 2 to keep them out of pitch). Lower = the rudders join the pitch more readily.");
			cRudToeMax = dev.Bind("3. Lateral-directional", "RudderToeMax", 20f, "2.0.7: largest scheduled symmetric rudder split (deg each rudder). In flight the split follows the pitch demand (share of the pitch authority x RudderToeGain); on the runway it follows the pitch stick (full pull = this much nose-up split). Both rudders at the same value make no net roll, so it is allowed above the rudder fade too. 0 = no schedule (the allocator still splits them when the stabs and nozzles run short).");
			cRudToeGain = dev.Bind("3. Lateral-directional", "RudderToeGain", 2f, "2.0.7: scheduled split = RudderToeMax x clamp(this x desired pitch acceleration / pitch authority that way).");
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
			cYawSlewLead = dev.Bind("3. Lateral-directional", "YawSlewLead", true, "2.4.18: the yaw-rate loop looks ahead along the measured yaw acceleration by the time the rudders need to swing back through neutral (their deflection / servo rate), so it starts braking the yaw before the yaw rate reaches its command instead of overshooting it (22:36 log, 52 s: yaw rate 42 against 28, sideslip -12 on the reversal). No effect with the rudders near neutral; fades out from 36 to 45 deg AoA. false = 2.4.17. Takes effect at once.");
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
			cStickRev = cfg.Bind("5. Pitch stick curve", "StickCurveRevision", 0, "Internal: the stick-curve default revision this file was last updated to. Leave it alone.");
			// 2.6.0: the lateral governor's knobs live in the player config (they were Dev keys 2.4.17-2.5.16; values carried over once)
			cPullRollTrade = cfg.Bind("6. Lateral governor", "PullRollTrade", 0.7f, "2.2.8 (2.2.23: default 0 -> 0.7; 2.2.26: 1; 2.2.27: back to 0.7): in a rolling pull held back by the sideslip governor, how much of the AoA rise you ask for is bought back by slowing the roll (the sideslip a rising AoA makes goes as roll rate x AoA rate). 0 = roll kept, AoA rise limited (2.2.7). 1 = roll slowed (to no less than 30 deg/s) until the asked AoA rise fits the sideslip margin; in between, the roll limit goes that share of the way. The roll comes back once the AoA rise is no longer limited. 2.2.11: above ~180-230 kt (2.2.12; 2.2.11: 230-280) the roll limit is also set ahead of time from the AoA rate the stick asks for, so the roll does not build past what the yaw can coordinate before the governor reacts. Takes effect at once.");
			cRollStopSlip = cfg.Bind("6. Lateral governor", "RollStopSideslip", 4f, "2.4.17 (2.5.0: 3 -> 4; the margin at low AoA, up to 10 deg): sideslip (deg) a roll stop or reversal is allowed to make; RollStopSideslipGovernor caps the commanded roll rate to it. Lower = slower rolls at moderate AoA, cleaner stops. Allowed 1-15. Takes effect at once.");
			cPushSlip = cfg.Bind("6. Lateral governor", "PushoverSideslip", -1f, "2.9.4: sideslip (deg) a push-over from high AoA may make at the zero crossing from the yaw rate still on board. The sweep is paced so the yaw above what that allowance can absorb (r = sqrt(2 x measured shed x allowance)) is shed before the AoA reaches zero; the rest is left to the rudders at low AoA. Higher = faster nose-down with a high yaw rate on, more sideslip at the bottom. -1 = RollStopSideslip. 0-20. Takes effect at once.");
			cRollGovPitch = cfg.Bind("6. Lateral governor", "RollGovernorPitch", true, "2.5.2, rebuilt 2.5.3: roll first, then pitch, both ways. With the roll already held to its cap, the AoA rate the pitch law may command under the roll is held to what the yaw can pay for: an AoA sweep changes the yaw the pair needs at roll rate x cos AoA x AoA rate. Nose-up at positive AoA: 0.8 x the rudders' braking yaw not yet in use on the roll's side (so the pull yields as the rudders fill instead of 0.3 s after they pin), faded in with the rudders' braking yaw (40-55 deg/s^2) and only as the sideslip heads for the margin (from half of it, measured sideslip plus 0.3 s of its kinematic rate) - a rolling pull that pins the rudders but holds its sideslip keeps its pitch rate. Every other sweep (nose-down anywhere, nose-up from negative AoA): the 2.1.8 sweep yaw limit's capacity (0.5 x the yaw authority, plus half the roll's) from the same rule, at every IAS - this is what keeps a full push + full roll from sweeping the yaw demand through zero faster than the rudders can swing. Never under 6 deg/s of AoA rate; released over 0.3 s. false = the trade alone. Takes effect at once.");
			cRollPaceRateSlip = cfg.Bind("6. Lateral governor", "RollPaceRateSlip", 1f, new ConfigDescription("2.5.10 (RollPaceRateMode): how much of the sideslip margin the roll-rate cap spends - the roll rate / sideslip compromise. 1 = the cap aims a full stick reversal at the margin as set (2.5.8). Above 1 the cap aims past the margin and lets the roll run faster: the bench reversal sweep (450-700 km/h, 10-29 deg AoA, margin 15) gives worst / mean sideslip and mean roll rate of 5.8 / 3.9 at 52 deg/s for 1.0, 8.3 / 4.8 at 59 for 1.25, 9.4 / 5.4 at 62 for 1.43, 10.7 / 6.3 at 67 for 1.67, 13.0 / 7.6 at 74 for 2.0, against 10.9 / 8.0 at 83 with no cap at all; the game's worst held-roll reversal reads about 1.4x the bench's worst. Below 1 the cap is stricter than the margin. Allowed 0.5-3. Takes effect at once.", new AcceptableValueRange<float>(0.5f, 3f)));
			cRollPaceRateBlend = cfg.Bind("6. Lateral governor", "RollPaceRateBlend", 0.4084507f, new ConfigDescription("2.5.11 (2.6.9: default 0.4084507 again, and the rate cap only counts where the rudders are the yaw axis - it fades out with the rudders past ~50 deg AoA, so the falling-leaf yaw rate is not taxed): how the pace governor splits its work between the two laws. 0 = 2.5.7 pacing only (the yaw-reach window holds the roll target while the yaw catches up, and the trajectory's acceleration / braking is capped at the yaw's authority: reversals are paced, the margin does not matter). 1 = 2.5.8 rate cap only (the target is capped at the rate whose stick reversal lands on the sideslip margin x RollPaceRateSlip; acceleration and braking stay as tuned: reversals are crisp, the rate is lower). Between: the target is the proportional mix of the two laws' targets and the acceleration cap gives way to the tuned acceleration in the same proportion. Allowed 0-1. Takes effect at once.", new AcceptableValueRange<float>(0f, 1f)));
			cRollStopSlipProbe = cfg.Bind("6. Lateral governor", "RollStopSlipProbe", true, "2.5.15: the airframe's own sideslip tolerance, measured from its aero tables in flight - no number entered. Twice a second the FCS evaluates the whole airframe (every aero part at the game's per-part law, the surfaces where they are) at the present AoA and a sweep of hypothetical sideslip to 20 deg each way, and finds the sideslip where the restoring yaw moment stops growing (directional stiffness gone - the departure boundary) and where the rudders' or the roll surfaces' authority has fallen to half of its zero-sideslip value. The smaller, x 0.7, caps RollStopSideslip. On an airframe with nothing inside 20 deg (the F-22E at 25-45 deg AoA, by the 31 Oct pedal flight) the cap never binds and RollStopSideslip rules; on one with a soft fin it binds where the fin gives out. Telemetry: slipTol / slipDep / slipAuth / slipStiff. false = RollStopSideslip alone. Takes effect at once.");
			cGovRev = cfg.Bind("6. Lateral governor", "GovernorRevision", 0, "Internal: the lateral-governor default revision this file was last updated to. Leave it alone.");
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
					Log.LogInfo("F-22E FCS 2.0.15: pitch stick curve moved from the old defaults 0.45 / 0.65 to the new 0.6 / 0.8 (once).");
				}
				cStickRev.Value = 2;
				cfg.Save();
			}
			cStickSmooth = cfg.Bind("1. General", "StickSmoothing", 0.06f, "Time constant (s) of the first-order smoothing on pitch/roll/pedal inputs. 0 = off.");
			cActDamp = dev.Bind("5. Airframe", "ActuatorDamping", 0.06f, "Actuator smoothing (s): time for a servo command to accelerate to full servo speed (and brake into its target on a matching curve). Servo speed is unchanged and a steady slew runs at full speed. 0 = off.");
			cYawStop = dev.Bind("3. Lateral-directional", "YawStopFactor", 0.7f, "Above 15-25 deg AoA the body yaw rate is capped at this x measured yaw authority (so the yaw can be stopped again). Higher = more responsive moderate-AoA rolls, more sideslip on stops.");
			cRollLead = dev.Bind("3. Lateral-directional", "RollLagCompensation", 0.008f, "Roll command lead per unit of roll damping (s per 1/s above 2.5/s): cancels the roll loop's tracking lag at high dynamic pressure so roll stops end when the command does, without a slow tail. 0 = off.");
			cYawPri = dev.Bind("4. Gains", "YawAxisPriority", 8f, "Allocator weight of the yaw axis relative to roll below ~20 deg AoA. Higher = rudders keep the roll coordinated even when their roll side-effect costs some roll acceleration.");
			cYawFF = dev.Bind("4. Gains", "YawRateFeedForward", 0.5f, "Fraction of the yaw-rate command's rate of change fed forward to the yaw loop at low AoA below 200 m/s IAS (blends to 1.0 by 300 m/s IAS, and by 28 deg AoA, where the body yaw is the velocity-vector roll). Lower = calmer rudders at low speed, slightly more sideslip.");
			cLowYaw = dev.Bind("3. Lateral-directional", "LowIasYawBoost", 1.7f, "Above 15-25 deg AoA, widens the yaw-rate cap (YawStopFactor x yaw authority, and its 20 deg/s floor) by this factor at very low IAS (falling leaf). It fades out along a curve above LowIasYawBoostFullIAS (see LowIasYawBoostFalloff). 1 = off.");
			cLowYawIas = dev.Bind("3. Lateral-directional", "LowIasYawBoostFullIAS", 70f, "IAS (m/s) up to which the full LowIasYawBoost applies.");
			cLowYawFall = dev.Bind("3. Lateral-directional", "LowIasYawBoostFalloff", 20f, "Above LowIasYawBoostFullIAS the extra margin decays exponentially with this IAS scale (m/s): 10 = ~50 % of it left at +7 m/s, ~10 % at +23 m/s, gone by 120 m/s.");
			cTvcMove = dev.Bind("4. Gains", "TvcMoveCost", 2E-05f, "Allocator cost of moving the TVC nozzles. 2e-5 = nozzles take pitch changes first; ~0.02 = stabs do the quick pitch changes and the nozzles slowly take over the sustained (trim) share. Post-stall the nozzles are always fast.");
			cTvcNeutral = dev.Bind("4. Gains", "TvcNeutralHold", 1000f, "2.2.14: while the pitch rate is held steady (no change of pitch-rate command, no pitch error, no roll in progress) the nozzles are drawn back to neutral over ~1 s and the stabs carry the steady pitch moment; the nozzles still take every change. Cost weight: 1000 (default); 0 = off (2.2.13). Takes effect at once.");
			cStabPark = dev.Bind("4. Gains", "StabParkEscape", true, "2.3.0: with the lateral stick and pedals centred and the lateral rates settled (under 8 deg/s, off again above 15 or with lateral input), the stabilators and rudders may not rest past their lift peak: their allocator window is the attached side up to (peak - SettledStallMargin) deg of local AoA, at any AoA where such a range exists. A stab resting on its peak has no gradient the allocator can use and makes its maximum lift; the other stab then matched it to cancel the roll and the nozzles cancelled the pitch to their stop (21 Oct logs, stuck surfaces after high-AoA releases). 2.2.15-2.2.29 only acted below 30 deg AoA and allowed the peak itself. false = off. Takes effect at once.");
			cSettledMargin = dev.Bind("4. Gains", "SettledStallMargin", 6f, "2.3.0: how far below the lift peak (deg of local AoA) the settled stall window keeps the stabilators and rudders (see StabParkEscape). Larger = further onto the attached slope, less trim deflection available from them. Takes effect at once.");
			cPushMinRate = dev.Bind("4. Gains", "PushOverMinRate", 23f, "3.1.4: the slowest nose-down body pitch rate (deg/s) a planned push-over (a push out of a high-AoA rotation) may be held to. A plan already at or above this rate (the typical push-overs) is untouched: it only lifts the very slow plans of deep-stall, very low airspeed pushes, and the stabs and nozzles give it what they can. 3.1.5: 23 deg/s (was 20), with no sideslip guard (3.1.4 faded it out from 12 to 15 deg of predicted sideslip); forced once over older dev files (AirframeRevision 25). 0 = the 3.1.3 plan alone. Takes effect at once.");
			cSettledOn = dev.Bind("4. Gains", "SettledRateOn", 10f, "2.3.1: the settled stall window (StabParkEscape) comes on when the body roll and yaw rates are both under this (deg/s). 2.3.0 used 4, which left a stab parked past its peak for a further 1-1.5 s after the stop while the sideslip correction yawed the jet at 5-8 deg/s. Takes effect at once.");
			cSettledOff = dev.Bind("4. Gains", "SettledRateOff", 20f, "2.3.1: the settled stall window goes off again above this body roll / yaw rate (deg/s), or with lateral input. Takes effect at once.");
			cSettledWalk = dev.Bind("4. Gains", "SettledWalkRate", 30f, "2.3.1: deg/s at which the settled window walks a surface resting past it back in (the window's edge moves, the allocator re-solves around each step). 2.3.0 used 15; at 15 the nozzles had climbed 8 -> 14 deg against the parked stab before it was back (22:51 log, 113 s). Takes effect at once.");
			cRateCapLag = dev.Bind("2. Envelope", "RateCaptureEffectorLag", 1f, "2.3.3: a pitch-rate capture toward zero (stick released from a pull) ends its deceleration early enough for the pitch effectors to be back at trim when the rate reaches zero: the end jerk is a3 / (this x the nozzles' |command| / slew, or the stabs' |command| / servo, whichever is longer, + 0.06 s). 21 Oct 23:44 log, 255 s: release from a 35 deg/s pull overshot to -10.6 deg/s with the nozzles still at -19 deg when the rate crossed zero. 1 = the effectors' own unwind time; 0 = off (2.3.2, the decel comes off over 0.15 s regardless). Not in AoA protection or an unload. Takes effect at once.");
			cFinLossCap = dev.Bind("5. Airframe", "FinLossAoACap", 65f, "2.2.19: positive AoA cap (deg) with both vertical tails gone, phased in from one fin lost (no effect with the fins on). 65 = the normal limit, no extra cap. 2.35.3: default 40 -> 65 (forced once over older dev files, AirframeRevision 24): the push-over and roll laws pace the no-fin sideslip themselves since 2.12-2.20, and 65 is what the bench damage runs and Tom's flights use. 2.2.19 bench, both fins detached, full aft with full roll held: 35-45 kept the sideslip within 8-12 deg, 50-65 with the nozzles on let it grow to 14-22 deg (19 Oct 20:36 log: departed at 45 with the nozzles off) - lower it if a fin-less jet keeps departing in held rolls. 0 = off. Takes effect at once.");
			cRollFightRel = dev.Bind("4. Gains", "RollFightReleased", false, "2.2.16: the roll-fight cost (RollFightCost) also acts while the lateral stick is centred, not only with the pedals in. It was added for a crossed set seen with TVC off (19 Oct 18:05 / 18:32 logs) and turned out to be what holds the stuck sets after high-AoA releases with TVC on: with the wing forbidden to cancel a parked stab's roll, the other stab had to (both nose-down) and the nozzles cancelled the pitch to their stop. Bench, 21 Oct log replays: nozzles 17-20 -> 4-6 deg with it off; 2.0.9 (before 2.2.16) flew with no stuck sets. 2.3.2: off by default (a file holding true is set to false once, revision 12). true = 2.2.16-2.3.1. Takes effect at once.");
			cGndBrake = dev.Bind("Ground", "GroundAeroBrake", 1f, "Aerodynamic braking on the rollout: flaperons and ailerons up, stabilators up, rudders split symmetrically for drag without yaw. Needs weight on the main wheels and either the wheel brake or throttle below 25 %, so it never appears during a take-off roll. 0 = off.");
			cGndBrakeMin = dev.Bind("Ground", "GroundAeroBrakeMinSpeed", 0f, "TAS (m/s) below which the aero-brake stays stowed. 0 = it is out whenever the other conditions are met, so you can see it at a standstill; it still scales up with speed.");
			cGndBrakeThr = dev.Bind("Ground", "GroundAeroBrakeThrottle", -1f, "Optional second trigger: throttle below this (0..1) also deploys the aero-brake. NEGATIVE (the default) disables it, so only the wheel brake deploys it, at any throttle.");
			cGndBrakeSplit = dev.Bind("7. Airbrake", "AirbrakeSplitInFlight", defaultValue: true, "In the AIR, the brake control deploys the airframe's split surfaces (the stock speedbrake mechanism). On the ground they stay stowed and the airbrake is the rollout surface configuration alone. The stock rule deployed them whenever the throttle was at idle, airborne as well as on the ground; the FCS drives them now, so idle throttle deploys nothing. false = split surfaces never deploy.");
			cAbFade = dev.Bind("7. Airbrake", "AirbrakeAoAFade", true, "2.2.16: in the air the speedbrake opens only as far as it matters: fully while its drag is AirbrakeFadeShareFull of the total drag or more, closing to stowed as the airframe's own (high-AoA) drag takes over, at AirbrakeFadeShareZero. The brake is Aryx's 8 drag points (wing, fins, stabs) plus the ailerons turned 15 deg; the inner flaperons and rudders only show the brake on their meshes. Clean jet: full open to ~15 deg AoA, half at ~25, stowed past ~35. false = 2.2.15 (always full). Takes effect at once.");
			cAbShareFull = dev.Bind("7. Airbrake", "AirbrakeFadeShareFull", 0.4f, "2.2.16: the brake's share of the total drag at and above which it opens fully (AirbrakeAoAFade). Clean jet: 0.4 = ~15 deg AoA.");
			cAbShareZero = dev.Bind("7. Airbrake", "AirbrakeFadeShareZero", 0.15f, "2.2.16: the brake's share of the total drag at and below which it stays stowed (AirbrakeAoAFade). Clean jet: 0.15 = ~35 deg AoA.");
			cVentDoors = dev.Bind("6. Visuals", "VentDoors", "Bypass", new ConfigDescription("The six gill doors on the upper fuselage. Bypass (1.8.37 default) = inlet bypass doors: they dump the inlet air the engines are not taking - open with speed when the throttle is back (from about Mach 0.35 with engines off, Mach 0.7-1.1 at idle, only above ~Mach 1.3 at MIL/AB), close as the engines spool up and above ~12-24 deg AoA; the brake does nothing to them. Airbrake = they open with the brake (1.8.36). Stock = Aryx's throttle/speed rule.", new AcceptableValueList<string>("Bypass", "Airbrake", "Stock")));
			cGndBrakeLog = dev.Bind("0. Debug", "GroundAeroBrakeLog", defaultValue: false, "Log one line a second while on the ground showing every gate the aero-brake checks (mode, wheels, speed, brake, throttle) and the resulting blend. Turn it off once it behaves.");
			cGndBrakeFlap = dev.Bind("Ground", "GroundAeroBrakeFlaperon", 20f, "Flaperon / aileron trailing-edge-up deflection (deg) in the aero-brake, clamped to their travel.");
			cGndBrakeStab = dev.Bind("Ground", "GroundAeroBrakeStabilator", 26f, "Stabilator trailing-edge-up deflection (deg) in the aero-brake - it also holds the nose up while the mains carry the jet.");
			cGndBrakeTvc = dev.Bind("Ground", "GroundAeroBrakeTVC", 10f, "Nozzle deflection (deg) in the aero-brake, in the same sense as aft stick (thrust up at the tail), so the nozzles sit up with the stabilators. Clamped to the nozzle travel.");
			cRotMinSpeed = dev.Bind("Ground", "RotationMinSpeed", 45f, "The take-off/landing rotation law (closed-loop pitch with the mains on the ground) only arms above this TAS (m/s). It used to arm at 15 m/s, an ordinary taxi speed, where it flew closed-loop pitch against the ground constraint and threw the surfaces about. It disarms 8 m/s below this.");
			cGroundRate = dev.Bind("Ground", "GroundRateLaw", defaultValue: true, "2.0.6: on the runway above RotationMinSpeed, pulling the stick hands pitch to the same rate law as gear-down flight, held through the rotation and lift-off (the rate command, its limits and stick scaling are the airborne ones). Stick neutral or forward with all three wheels down for 0.3 s goes back to the open-loop ground mode (steering, aero-brake). false = 2.0.5 behaviour (open-loop stick until the nose is up, then RotationRate).");
			cGroundRotRamp = dev.Bind("Ground", "GroundRotationRamp", 60f, "2.0.6: while the nose wheel still touches in the ground rate law, the nose-up pitch moment the FCS asks for may grow by at most this much per second (in deg/s^2 of pitch acceleration). Stops the surfaces winding to full deflection against the nose leg and over-rotating when it lifts.");
			cGndBrakeRud = dev.Bind("Ground", "GroundAeroBrakeRudder", 25f, "Symmetric rudder split (deg) in the aero-brake. Negative flips which way they toe.");
			cNwsFbw = dev.Bind("Ground", "NoseWheelFbw", true, "1.8.36: fly-by-wire ground steering. Pedal commands a yaw rate (GroundYawRateMax at taxi speed, limited to GroundLateralAccel of turn at speed); pedal released = yaw braking to zero rate, then the jet holds a straight line along the track it had (a crab/drift at touchdown is steered out, the heading lined up with the track). The nose wheel is steered by the FCS (kinematic feed-forward + yaw-rate feedback) and, in the ground roll, the rudders join the yaw-rate loop. Below 1.5 m/s the pedal steers the wheel directly (pivoting). false = stock: pedal sets the nose-wheel angle, rudders follow the pedal.");
			cGndYawRate = dev.Bind("Ground", "GroundYawRateMax", 15f, "Full-pedal yaw rate on the ground (deg/s) at taxi speed.");
			cGndLatAcc = dev.Bind("Ground", "GroundLateralAccel", 2.5f, "Full pedal never asks for more than this lateral acceleration (m/s^2) in a ground turn: the yaw-rate command is min(GroundYawRateMax, this / ground speed). 2.5 = full pedal gives 2 deg/s at 260 km/h.");
			cGndLine = dev.Bind("Ground", "GroundLineHold", 1f, "Pedal released: how strongly the jet steers back onto the straight line it was on (1 = full, 0 = hold the track direction only and accept the sideways offset a gust or touchdown drift left).");
			cGndStock = dev.Bind("Ground", "GroundStockMatch", true, "2.4.12: with NoseWheelFbw on, stock steering with a drift limit and the straight-line correction. While the pedal is in (and after release until the yaw has stopped) the nose wheel, the rudders and the differential brake get the stock fly-by-wire's yaw, computed with the jet's own stock parameters (yaw-rate command with yaw damping; raw pedal below the stock minimum speed), so the turn is stock; GroundDrift* limit the sideslip on top. Once the yaw has stopped the FCS holds the straight line along the track until the pedal moves; at touchdown it holds the approach track. GroundYawRateMax and GroundLateralAccel are not used. false = the capped yaw-rate law of 1.8.36-2.4.9.");
			cGearDownSlip = dev.Bind("Ground", "GearDownSlipProtect", true, "2.4.13: the negative-AoA push + roll limiters (sideslip lead and yaw-anchored roll, 2.1.0) also act with the gear down in the air (not with a wheel on the ground). false = off with the gear down, as before.");
			cGndIndi = dev.Bind("Ground", "GroundIndi", true, "2.4.14: INDI on the nose wheel after the pedal is released - the release braking (stop the yaw) and the straight-line / touchdown hold. The wheel effectiveness comes from the game each tick (nose-leg load, tyre friction and slope, CG and yaw inertia); the measured yaw acceleration covers the rest. While the pedal is in the steering stays stock. false = 2.4.13 (stock law on release, PI line hold).");
			cGndIndiK = dev.Bind("Ground", "GroundIndiGain", 15f, "2.4.14: INDI nose wheel: wanted yaw acceleration per deg/s of yaw-rate error (1/s).");
			cGndPedalBrake = dev.Bind("Ground", "GroundPedalBrake", 0.5f, "F-22E 4.1.5: while the pedal is in, the main on the pedal's side is braked by |pedal| x this (stock: none - both mains read the left pedal (differentialBrakeFactor -1 on both), so it brakes both and steers nothing). The jet pivots harder and the mains break loose, so it drifts. Bench, 3 s full pedal from 15 kt at low thrust: peak sideslip 2.6 deg (4.1.4) -> 5.5 (0.25) -> 9.3 (0.5) -> 11.3 (1.0); speed after 3 s 19.9 -> 13.9 -> 9.5 -> 4.8 kt. Never below your own brake. 0 = 4.1.4.");
			cGndLightNose = dev.Bind("Ground", "GroundIndiLightNoseHold", true, "F-22E 4.1.5: when the nose leg goes light during a release stop or the straight-line hold (the jet rocks on its suspension when a hard turn is released), the nose wheel keeps the angle INDI had relative to the contact velocity (at most 1 s) instead of dropping to the stock law's angle (full opposite lock) or 0. The game slews the wheel toward its command even off the ground, so 4.1.4 met the ground again tens of degrees out. Bench, nose unloaded 0.3-0.85 s into a 10 kt release stop (with GroundIndiBrake): stop 1.85 -> 1.5 s. false = 4.1.4.");
			cGndIndiBrake = dev.Bind("Ground", "GroundIndiBrake", true, "F-22E 4.1.5: the main-wheel brakes as a second yaw effector for the INDI after the pedal is released (release stop and straight-line hold). The nose wheel goes first; the brake on the outside main takes only the yaw acceleration beyond the nose tyre's grip, read from each main's load, friction and contact velocity every tick. Your own brake is never reduced and the stock differential brake stays underneath. Each main's brake is written directly, so it does not depend on the stock 3-30 m/s window or on the stock differential factor (both -1 on this jet: no stock differential steering). Costs speed: bench, release from a full-pedal turn at 10 kt: stop 1.38 -> 1.12 s, 5.2 kt lost. false = 4.1.4.");
			cGndIndiBrakeMax = dev.Bind("Ground", "GroundIndiBrakeMax", 1f, "F-22E 4.1.5: most brake (0-1) the yaw effector may put on one main. Lower = less speed lost per stop, slower stop. Bench, 10 kt: 0.4 = stop 1.25 s / 1.9 kt lost, 0.6 = 1.20 s / 3.1 kt, 1.0 = 1.12 s / 5.2 kt (4.1.4: 1.38 s).");
			cGndIndiBrakeFull = dev.Bind("Ground", "GroundIndiBrakeFullSpeed", 7.7f, "F-22E 4.1.5: full brake authority at and below this ground speed (m/s; 7.7 = 15 kt).");
			cGndIndiBrakeZero = dev.Bind("Ground", "GroundIndiBrakeZeroSpeed", 12.9f, "F-22E 4.1.5: brake authority fades to zero at this ground speed (m/s; 12.9 = 25 kt). Above it the ground steering is 4.1.4.");
			cGndIndiTau = dev.Bind("Ground", "GroundIndiTau", 0.04f, "2.4.14: INDI nose wheel: low-pass time constant (s) on the measured yaw acceleration and the tyre-force estimate.");
			cGndDriftAllow = dev.Bind("Ground", "GroundDriftAllow", 8f, "2.4.12: sideslip (deg) the jet may drift to on the ground before the steering counters it (stock steering does not limit it). 8 = about half of the Ifrit stock-pedal drift above 50 kt (27 Oct logs: 17-25 deg peaks, 42 in a slalom). 0 = hold the nose on the track; large = no limit.");
			cGndDriftGain = dev.Bind("Ground", "GroundDriftGain", 0.2f, "2.4.12: counter-yaw per degree of sideslip past GroundDriftAllow (1 = full pedal; 0.2 = full counter-yaw 5 deg past the allowance). 0 = drift limit off.");
			cGndDriftLead = dev.Bind("Ground", "GroundDriftLead", 0.25f, "2.4.12: the drift limit acts on the sideslip predicted this far ahead (s) from its rate, so a fast skid is caught before it overshoots.");
			cGndDriftFade = dev.Bind("Ground", "GroundDriftFadeSpeed", 15f, "2.4.12: the drift limit fades in from this ground speed (m/s) to 10 m/s above it (default 15-25 m/s = 29-49 kt), so tight taxi turns keep their natural sideslip.");
			cNozAnim = dev.Bind("6. Visuals", "NozzleAnimation", true, "1.8.37/1.8.40: the two nozzle flaps move separately (visual only - thrust and vectoring physics are unchanged). Each flap is a root piece under the shroud, a forward segment (the flap's own cowl, hinged at its front edge) and an end segment; seam strips close the joints and nothing stretches. Vectoring: cowl and end segment move as one; the flap swinging outward is pulled in (up to 3 cm) and pivots at the inner surface of its root joint, so its cowl rides over the root; the one swinging inward rotates about the same point without the pull-in. Afterburner: flaps open 1.5 deg. MIL: flaps rotated 7.7 deg in. Applied when a jet spawns. false = Aryx's original one-piece nozzle.");
			cNozSeamShade = dev.Bind("6. Visuals", "NozzleSeamShade", 0.07f, "1.8.42: brightness (0-1, sRGB grey) of the two seam bands in each nozzle flap (the gaps between the root, the floating cowl and the end flap). Plain material like the cavity parts: 0.035 = the black liners, 0.085 = the dark grey beams. Applied when a jet spawns. 1.8.40/41 used Aryx's atlas grey (reads about 0.2-0.3 through the livery).");
			cPitchFight = dev.Bind("4. Gains", "RollPitchFightCost", 1f, "1.8.42: while you roll, a stabilator pitching the nose up (trailing edge up) while the nozzles pitch it down to cancel costs the allocator this x its pitch acceleration squared (rad/s^2), so it rolls with less stab trailing-edge up and the nozzles keep their nose-down travel instead of the roll rate. Near the stabs' roll reversal (the trailing-edge-down stab stalled) one stab went fully trailing-edge up with the other near neutral and the nozzles pinned nose-down. Only that pattern counts (stabs nose-down with nozzles nose-up, the roll-pitch-priority trim, is left alone); off while the pitch-rate command is moving, while the nose comes up faster than commanded, in AoA protection and on AoA run-away. Bench vs 1.8.41: reversals at 19 deg AoA / 300 km/h nozzle min -20 -> -13, AoA over 1.2 -> 0.5 deg; roll stop at 200 km/h -14 -> -10; 2.5 t store at 600 km/h -20 -> -12, over 2.9 -> 0.9; at 26 deg a stab past 35 deg local AoA 32 % -> 0 % of the roll. Costs: reversal at 13 deg / 600 km/h over 3.9 -> 8.3 deg, 10 deg / 300 km/h 1.1 -> 2.7; bank at 1 s at 19-22 deg AoA 42 -> 36 and 28 -> 20 deg. 0 = off (1.8.41).");
			cNozAnimAI = cfg.Bind("2. AI", "NozzleAnimationAI", false, "1.8.40: animate AI jets' nozzles too. false = AI F-22Es keep Aryx's one-piece nozzle (no skinning or per-frame posing for them); player jets, including other players online, are animated.");
			cNozIdleOpen = dev.Bind("6. Visuals", "NozzleIdleOpen", true, "Nozzle area schedule in dry power: true = open at idle and closing toward MIL (how fighter engine nozzles are scheduled, to spoil idle thrust); false = closed from idle to MIL. Afterburner opens it either way.");
			cNozRate = dev.Bind("6. Visuals", "NozzleAreaRate", 1.2f, "How fast the nozzle area moves (full travel, closed to full-afterburner, per second).");
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
			cOuterFlap = dev.Bind("5. Airframe", "OuterFlaperonDroop", 0.6f, "Outer flaperons droop with the flap schedule by this fraction of the inboard flap angle (stall-capped, roll-biased).");
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
			cStabTrim = dev.Bind("4. Gains", "StabilatorTrimCost", 0.004f, "Allocator cost of symmetric (pitch) stabilator deflection relative to the nozzles. Higher = steady trim / sustained pitch rate carried by TVC, stabs kept for transients and whatever TVC cannot hold.");
			cUnloadDecel = dev.Bind("4. Gains", "UnloadDecel", 20f, "Easing the stick: pitch-rate deceleration (deg/s^2) toward the rate the aircraft will sustain at the new AoA/G. Lower = smoother.");
			cUnloadRise = dev.Bind("4. Gains", "UnloadRiseGain", 1.5f, "Easing the stick: if AoA still rises during the decel, the pitch-rate target drops by this x AoA rate (to zero at most).");
			cGUnder = dev.Bind("4. Gains", "GUndershootCapture", 1f, "Easing out of a hard pull at high dynamic pressure, the pitch rate is bounded so the load is captured at the commanded G instead of continuing past it into negative g - the same kinematic capture the load limiter uses, applied from above the target. Lower = a softer bound (0.5 allows twice the nose-down rate); 0 = off (pre-1.8.10 behaviour). Only in the G-command regime.");
			cKpG = dev.Bind("4. Gains", "GGain", 2.5f, "G command bandwidth (1/s): load error is converted to AoA error through the live lift slope.");
			cKiG = dev.Bind("4. Gains", "GIntegral", 1f, "G command integral gain (1/s^2), removes steady load error.");
			cFlaperonEff = dev.Bind("5. Airframe", "FlaperonEffectiveness", 1.2f, "Aerodynamic effectiveness multiplier on the flaperons and ailerons (scales their lifting area). Applied at spawn. 2.0.0: 1.2 (1.5 before).");
			cStabEff = dev.Bind("5. Airframe", "StabilatorEffectiveness", 1.2f, "Aerodynamic effectiveness multiplier on the stabilators. Applied at spawn. 2.0.3: 1.2 (1.1 before).");
			cActuator = dev.Bind("5. Airframe", "ActuatorSpeedScale", 1.428571f, "Servo speed multiplier for every control surface and the TVC nozzles (Aryx's servos run at 70 deg/s; 1.428571 = 100 deg/s).");
			cStabUp = dev.Bind("5. Airframe", "StabilatorRangeTEUp", 35f, "Stabilator travel, trailing edge up (deg). 0 = Aryx's original (30, symmetric).");
			cStabDn = dev.Bind("5. Airframe", "StabilatorRangeTEDown", 30f, "Stabilator travel, trailing edge down (deg). 0 = Aryx's original (30, symmetric).");
			cRudRange = dev.Bind("5. Airframe", "RudderRange", 35f, "Rudder travel each way (deg). 0 = Aryx's original (28).");
			cFlapRange = dev.Bind("5. Airframe", "FlaperonRange", 25f, "Travel each way for both flaperon pairs, outer (Aileron_*) and inner (Flap_*) (deg). 0 = Aryx's original (20 outer, 14 inner).");
			cTvcRange = dev.Bind("5. Airframe", "TvcPitchRange", 20f, "Pitch thrust-vectoring travel each way (deg, game maximum 20). 0 = Aryx's original (10).");
			cTvcSpeed = dev.Bind("5. Airframe", "TvcSlewRate", 75f, "TVC nozzle slew rate (deg/s). 0 = follow ActuatorSpeedScale like the control surfaces (70 x scale).");
			cLerxEff = dev.Bind("5. Airframe", "LERXEffectiveness", 1.2f, "Aerodynamic effectiveness multiplier on the LERX (lifting area). Forward lift: moves the neutral point forward (less static stability, less trim) and adds nose-up moment that grows with AoA (the LERX curve peaks at 45 deg). 2.0.3: 1.2 (2.0.0: 1.3, before: 1.6).");
			cCgShift = dev.Bind("5. Airframe", "CGShiftAft", 0.05f, "Moves the centre of mass aft by this many metres (mass moved from the forward fuselage to the aft fuselage, total mass unchanged). Main gear is ~0.3 m behind the stock CG: keep this small.");
			cStagger = dev.Bind("5. Airframe", "StallStagger", defaultValue: true, "2.0.0: staggered stall of the horizontal control surfaces. Each family gets its own copy of Aryx's surface airfoil with the lift peak moved to StabilatorStallAoA / InnerFlaperonStallAoA / OuterFlaperonStallAoA (local AoA, both signs), so the three do not sit on their lift peaks at the same AoA. The game's aero and the FCS's surface models use the same curves. Applied at spawn. false = Aryx's curve on all of them (peak 35).");
			cStallStab = dev.Bind("5. Airframe", "StabilatorStallAoA", 36f, "2.0.0: stabilator lift-peak local AoA (deg) with StallStagger. 3.2.0: default 36 (25 before; forced once over older dev files, AirframeRevision 26). With StabDownwash 0.6 a neutral stab sees 0.4 x body AoA up to 45 deg, then the downwash fades out by 60, so it reaches this peak near 52 deg body AoA (inner flaperons ~33, outer ~44: still staggered). With StallStaggerLiftSlope 0 the stab keeps its peak CL and its small-angle lift slope is about 0.69x the 25 deg stab's (bench, 1 g full-stick roll, bank at 1 s: 350 km/h 57 -> 55 deg, 550 km/h 129 -> 128, 800 km/h 171 -> 171, so StabilatorCLScale stays 1).");
			cDownwash = dev.Bind("5. Airframe", "StabDownwash", 0.6f, "2.0.8: wing/LERX downwash at the stabilators, d eps / d alpha. The flow reaching the stabs is turned down by eps = this x AoA (faded out from 45 to 60 deg AoA; free stream above 60 as before), so a neutral stab sees AoA x (1 - this): 16 deg at 40 deg AoA with 0.6 instead of 40, below its lift peak (StabilatorStallAoA) with room to push the nose down. 0.6 is at the top of the real-world range (0.3-0.5 typical). 3.2.0 bench: with no downwash a stab at its peak has nothing left to push the nose down (full nose-down stab -5 deg/s^2 at 36-48 deg AoA, 85 m/s; -30 / -28 / -24 at 36 / 40 / 44 with 0.6 and the 36 deg peak). Applied in the game's aero as a correction force on the stabs and in the FCS's stab models. 0 = off (2.0.7). Respawn after changing. (2.0.9-2.0.10 tapered it to 0.3 above 32 deg; 2.0.11 is back to 2.0.8's airframe.)");
			cFinCamber = dev.Bind("5. Airframe", "FinCamberCoupling", 0.5f, "2.2.0: a rudder deflection also cambers the fixed fin ahead of it, as on a real flapped fin: the fin gets the lift change of K x the rudder deflection at its own AoA (0.5 ~ a half-chord rudder with ~0.75 flap effectiveness over the whole section, of which the game's rudder plate already carries the rudder's own share). Zero with the rudders centred, so trim and the pitch centre of lift do not change. 0 = 2.1.10. Takes effect at once.");
			cWingCamber = dev.Bind("5. Airframe", "WingCamberCoupling", 0.3f, "2.2.0 (on at 0.3 since 2.4.9; a file holding any other value is moved to 0.3 once): the same for the flaperons and ailerons on the wing sections ahead of them (WingRear / Wingmid); 0.3 ~ a 0.35-chord flap. At 0.3 it is a roll buff across the envelope (bench: 1 g roll at 450 km/h, bank at 1 s 66 -> 86 deg; reversal peak rate 118 -> 157 deg/s at 450 km/h) and adds little at 45+ deg AoA, where the wing sections are past their lift peak. 0 = 2.1.10. Takes effect at once.");
			cStabPeakMargin = dev.Bind("5. Airframe", "StabPeakMargin", 90f, "2.0.11: default 90 (off, 2.0.8 behaviour). 2.0.10: below 40 deg body AoA a stab is not commanded more than this many degrees past its lift peak (local AoA, either sign); the limit opens up from 40 to 50 deg AoA (deep-stall use above, e.g. falling leaf, unchanged). Stops the stabs swinging through deep stall in rolls at 36 deg and parking past the peak after a roll (10:00 flight). Bench, full roll at 36 deg with TVC off: stab rate 40 -> 16 deg/s rms, roll rate 27-33 -> 19-25 deg/s; with TVC on the roll rate is within 5 %. 90 = off.");
			cBodyLift = dev.Bind("5. Airframe", "BodyLiftBalance", defaultValue: true, "2.0.8: fore/aft body-lift balance. The fuselage sections ahead of the CG (LERX, intakes, cockpit, centre body) get BodyLiftFore x their lift, the ones behind it (engine bays, tail booms, tail) BodyLiftAft x; drag is unchanged. Together with StabDownwash this takes most of the trim off the stabs and nozzles from 0 to 40 deg AoA (default: a small stable margin, about a fifth of 2.0.7's pitch-down moment at 10 deg) and leaves the jet nose-down (stable) above ~45. Lift per AoA matches 2.0.7's trimmed lift. Applied at spawn. false = Aryx's body lift (2.0.7).");
			cBodyFore = dev.Bind("5. Airframe", "BodyLiftFore", 0.9f, "2.0.8: lift multiplier on the fuselage sections ahead of the CG (with BodyLiftBalance), up to 45 deg AoA, faded back to 1 by 60. With BodyLiftAft it moves the body's lift aft and makes up the stab lift the downwash takes away (bench: trimmed G at a given AoA and IAS within 0.02 g of 2.0.7 from 5 to 35 deg). 0.86 / 1.40 = neutral pitch with stabs and nozzles at 0 (but ~4 % less G per AoA); 0.90 / 1.80 (default) leaves a small stable margin, which kept the pull-release(-roll) pitch transients as clean as 2.0.7 on the bench.");
			cBodyAft = dev.Bind("5. Airframe", "BodyLiftAft", 1.8f, "2.0.8: lift multiplier on the fuselage sections behind the CG (engine bays, tail booms, tail) with BodyLiftBalance, up to 45 deg AoA, faded back to 1 by 60. See BodyLiftFore.");
			cForeLiftSched = dev.Bind("5. Airframe", "ForeLiftSchedule", "0:1.0000,4:1.0843,8:1.0814,12:1.0795,16:1.0738,20:1.0577,24:1.0466,28:1.0359,32:1.0319,36:1.0391,40:1.0607,44:1.0334,48:0.9673,52:0.9270,56:0.5670", "3.2.0 (airframe X2): positive-AoA lift multipliers on the fuselage sections ahead of the CG (LERX, intakes, fore, cockpit, centre body), on top of BodyLiftFore and the 2.4.6 boost: \"AoA:multiplier,...\", linear between knots, faded back to 1 from 45 to 60 deg AoA, negative AoA untouched. Fitted with AftLiftSchedule so the lift at each AoA is unchanged and the stabs-neutral, TVC-0 pitching moment is zero from 4 to 40 deg (bench, calibrated plant): static margin about 0 over 4-36 deg, the held AoA needs almost no stab or nozzle. 3.2.1 (airframe X2e): from 44 deg the schedule gives the nose-down moment back (-6.5 / -18.4 / -31 / -30 deg/s^2 at 44 / 48 / 52 / 56 deg, 85 m/s, stabs neutral), so the roll-coupling limit has nose-down to spend at 45-55 deg (3.2.0 X2 had about none there and the velocity-vector roll dipped); full nose-down stab at 51-57 deg is 7-10 deg/s^2 more than 3.1.9. Empty = off (the 3.1.9 airframe). Applied at spawn.");
			cAftLiftSched = dev.Bind("5. Airframe", "AftLiftSchedule", "0:1.0000,4:0.8823,8:0.8862,12:0.8888,16:0.8984,20:0.9232,24:0.9402,28:0.9554,32:0.9616,36:0.9537,40:0.9287,44:0.9596,48:1.0419,52:1.0889,56:1.9918", "3.2.0 (airframe X2): the same for the fuselage sections behind the CG (engine bays, tail booms, tail), on top of BodyLiftAft. See ForeLiftSchedule. Empty = off. Applied at spawn.");
			cWingLift = dev.Bind("5. Airframe", "WingLiftShaping", defaultValue: true, "2.4.6: positive-AoA lift curve of the main wing panels (WingFore / Wingmid / WingRear / Wingtip; not the flaperons): Aryx's curve up to WingLiftRampStart, then raised smoothly to a peak at WingLiftPeakAoA worth WingLiftPeakGain x the old curve's lift at WingLiftRefAoA, and back to Aryx's curve by 60 deg. Lift only: drag is unchanged, negative AoA is unchanged, the flaperons' control power (camber coupling) is unchanged. Every variant (Stock and Buffed) and AI jets. The game samples the curve every 2.8 deg, so the peak lands on the 36.6 deg point. Applied at spawn. false = 2.4.5's wing.");
			cWingLiftPeak = dev.Bind("5. Airframe", "WingLiftPeakAoA", 36f, "2.4.6: AoA (deg) of the wing panels' lift peak with WingLiftShaping (Aryx: 35).");
			cWingLiftGain = dev.Bind("5. Airframe", "WingLiftPeakGain", 1.239f, "2.4.6: the wing panels' peak lift as a multiple of their own lift at WingLiftRefAoA on Aryx's curve. 1.239 makes the whole main wing (panels + flaperons at neutral, whose curves are not changed) peak at 1.20 x its old 30 deg lift (bench: 56.19 m^2 CL*S at 36.6 deg against 46.82 at 30). First 2.4.6 cut: 1.115 (+10 %).");
			cWingLiftRef = dev.Bind("5. Airframe", "WingLiftRefAoA", 30f, "2.4.6: reference AoA (deg) for WingLiftPeakGain.");
			cWingLiftStart = dev.Bind("5. Airframe", "WingLiftRampStart", 12f, "2.4.6: AoA (deg) where the wing panels' extra lift starts (zero value and slope there, Aryx's curve below). 2.4.7: 12 (2.4.6: 17).");
			cBodyBoost = dev.Bind("5. Airframe", "BodyLiftBoost", defaultValue: true, "2.4.6: positive-AoA lift boost on every fuselage section (centre body, intakes, fore, cockpit, LERX, engine bays, tail booms, tail, rear, back), on top of the 2.0.8 fore / aft balance: x1 up to BodyLiftBoostStart, rising smoothly to BodyLiftBoostGain at BodyLiftBoostPeakAoA (the fuselage's lift peak), held to 45 deg and faded back to x1 by 60. Lift only: drag and negative AoA unchanged. Every variant and AI jets. Applied at spawn. false = 2.4.5's fuselage.");
			cBodyBoostGain = dev.Bind("5. Airframe", "BodyLiftBoostGain", 1.2f, "2.4.6: fuselage lift multiplier at its peak with BodyLiftBoost (1.2 = 20 % more peak lift than 2.4.5). First 2.4.6 cut: 1.1.");
			cBodyBoostStart = dev.Bind("5. Airframe", "BodyLiftBoostStart", 12f, "2.4.6: AoA (deg) where the fuselage's extra lift starts. 2.4.7: 12 (2.4.6: 17).");
			cBodyBoostPeak = dev.Bind("5. Airframe", "BodyLiftBoostPeakAoA", 42f, "2.4.6: AoA (deg) where the fuselage boost reaches BodyLiftBoostGain (the fuselage's lift peak, 42 deg).");
			cBodyNeutral = dev.Bind("5. Airframe", "BodyLiftPitchNeutral", defaultValue: true, "2.4.6: share the fuselage's extra lift fore / aft (more on the engine bays, tail booms and tail, less on the forward sections) so that the wing's and fuselage's extra lift add no pitching moment: with the surfaces neutral the airframe pitches as 2.4.5 at every AoA. Total fuselage lift is the same either way (BodyLiftBoostGain at its peak). Bench (300 km/h, 36 deg): -8.6 deg/s^2 (2.4.5) / +6.1 with a uniform boost / -8.6 with this. Fore gain peaks ~x1.12, aft ~x1.32 at 41-44 deg. false = uniform boost on every section (adds nose-up pitch at 26-44 deg AoA). Applied at spawn.");
			cTvcOff = dev.Bind("0.1 Debug - Airframe test", "DisableTVC", defaultValue: false, "2.0.8 test switch: nozzles locked at 0 deg pitch at every throttle, and the FCS flies without them (pitch from the stabs, flaperons and rudders only). With AoALimit Normal the low-thrust caps (NoTvcAoACapPositive / Negative, +-36) then apply (the nozzles never cover pitch). All F-22Es on this machine. Takes effect at once.");
			cAoATest = dev.Bind("0.1 Debug - Airframe test", "AoALimit", AoALimitTest.Normal, "2.0.8 test switch for the AoA limits. Normal = AoALimitPositive / Negative (+65 / -45), capped at NoTvcAoACapPositive / Negative when the nozzles do not cover pitch (low thrust or DisableTVC). Full65 = +65 / -45 and those caps off (the swept authority cap, which stops the FCS pulling past where the stabs can still push the nose down, stays on; LowThrustAoACap = false removes that too). Cap36 = the NoTvcAoACap values in every condition. Takes effect at once.");
			cStabsPitchOnly = dev.Bind("0.1 Debug - Airframe test", "StabsPitchOnly", false, "2.2.5 test switch: the stabilators move symmetrically only (pitch), with no roll or yaw job, so roll and yaw come from the flaperons and rudders alone. For checking what the flaperons can do. The lateral rate limits still count the stabs' authority, so at high AoA the FCS will ask for more roll/yaw than the jet then has (expect slower rolls and more sideslip there). Off for AI. Takes effect at once.");
			cNoTvcCapPos = dev.Bind("0.1 Debug - Airframe test", "NoTvcAoACapPositive", 45f, "2.2.3: default 45 (2.0.9-2.2.2: 36). Positive AoA cap (deg) while the nozzles do not cover pitch (T/W under LowThrustFixedAoATW, or DisableTVC). Also the positive limit with AoALimit = Cap36. Takes effect at once.");
			cNoTvcCapNeg = dev.Bind("0.1 Debug - Airframe test", "NoTvcAoACapNegative", 45f, "2.2.3: default 45 (2.0.9-2.2.2: 36). Negative AoA cap magnitude (deg) under the same conditions (2.0.8 had none: -45 always). Also the negative limit with AoALimit = Cap36. Takes effect at once.");
			cNoTvcThrCapPos = dev.Bind("0.1 Debug - Airframe test", "NoTvcThrustAoACapPositive", 45f, "2.2.2: with DisableTVC, the positive AoA cap (deg) once the thrust is up: NoTvcAoACapPositive (36) up to T/W 0.8, this value from T/W 1.0 (afterburner), linear between. Bench: with afterburner the stabs alone hold 45 deg at 160-750 km/h, rolls and reversals included (sideslip 6 deg or less); at part or idle throttle, rolling as the speed bleeds through ~200-250 km/h, 45 departed to 48-62 deg in 5 of 20 cases where 36 held. Takes effect at once.");
			cNoTvcThrCapNeg = dev.Bind("0.1 Debug - Airframe test", "NoTvcThrustAoACapNegative", 45f, "2.2.2: with DisableTVC, the negative AoA cap magnitude (deg) at every thrust (NoTvcAoACapNegative still applies at low thrust with the nozzles working). Bench: -45 held at 200-450 km/h, idle to afterburner, with a roll, sideslip 5.4 deg or less. Takes effect at once.");
			cStallInner = dev.Bind("5. Airframe", "InnerFlaperonStallAoA", 35f, "2.0.0: inner flaperon (Flap_*) lift-peak local AoA (deg) with StallStagger.");
			cStallOuter = dev.Bind("5. Airframe", "OuterFlaperonStallAoA", 45f, "2.0.0: outer flaperon (Aileron_*) lift-peak local AoA (deg) with StallStagger.");
			cStallSlope = dev.Bind("5. Airframe", "StallStaggerLiftSlope", 0f, "2.0.0: 0 (default) = every surface keeps Aryx's peak CL (1.10) and the lift slope scales instead: the stabs (peak 36) are 0.97x as steep at small angles (peak 25: 1.4x), the outer flaperons (peak 45) 0.78x. 1 = each surface keeps the small-angle lift slope and the peak CL scales with the stall AoA (peak 25 = 0.71x CL, 45 = 1.29x); on the bench that cost 15-30 % of the roll rate at every AoA and 4 deg of the idle AoA cap. In between blends.");
			cStabSlope = dev.Bind("5. Airframe", "StabilatorLiftSlope", -1f, "2.2.13: StallStaggerLiftSlope for the stabilators alone (-1 = use StallStaggerLiftSlope). 1 = the stabs keep Aryx's small-angle lift slope and their peak CL scales with StabilatorStallAoA. Applied at spawn.");
			cStabCL = dev.Bind("5. Airframe", "StabilatorCLScale", 1f, "2.2.13: lift multiplier on the stabilators' remapped curve (whole curve, both signs). With StabilatorStallAoA raised this keeps their small-angle lift slope: 25 -> 30 deg with 1.2, 25 -> 35 with 1.4. Applied at spawn.");
			cAirframeRev = dev.Bind("5. Airframe", "AirframeRevision", 0, "Internal: the airframe-default revision this file was last updated to (3 = 2.0.3's flaperon 1.2 / LERX 1.2 / stab 1.2, 5 = 2.0.11, 6 = 2.0.12, 7 = 2.2.23, 8 = 2.2.26, 9 = 2.2.27, 10 = 2.2.29, 11 = 2.3.1, 12 = 2.3.2, 13 = 2.4.1, 14 = 2.4.2, 15 = 2.4.5, 16 = 2.4.6, 17 = 2.4.7, 21 = 2.4.9, 22 = 2.4.17, 23 = 2.5.0, 24 = 2.35.3, 25 = 3.1.5, 26 = 3.2.0, 27 = 3.2.1, 28 = 4.1.0; 18-20 were used by the experimental 2.4.9-beta / 2.4.10-beta builds). Leave it alone.");
			cFuelUse = dev.Bind("5. Airframe", "FuelConsumptionScale", 0.75f, "Fuel burn multiplier for every engine at every throttle setting, afterburner included (0.75 = 25 % less fuel used). All F-22Es.");
			cThrust = dev.Bind("5. Airframe", "ThrustScale", 1.08f, "Engine thrust multiplier (dry staticThrust and afterburner thrust alike, so max-AB total scales the same). Fuel flow per power setting is unchanged.");
			cDrag = dev.Bind("5. Airframe", "ParasiticDragScale", 0.885f, "Multiplier on every part's parasitic drag area (1 = stock). 0.885 = about -10 % total straight-line drag (parasitic is 85-93 % of it).");
			cDrAoA = dev.Bind("5. Airframe", "AoADragRelief", 0.25f, "2.2.3: Buffed profile only, the AoA drag relief above the LowAoADragRelief fade band (post-stall); below it LowAoADragRelief applies. 1.8.38-2.2.2: fraction of the drag that comes from AoA (each part's airfoil drag above that airfoil's minimum) removed at every AoA. A pure scaling of the AoA-dependent drag, so the drag-vs-AoA curve keeps the game's shape with no bump or dip anywhere (a band limited to 5-27 deg would need hand-over zones that make the drag slope dip or hump). 0.25 = drag -20 % averaged over 5-27 deg AoA from this relief alone (about -11 % at 5, -18 % at 10, -22 % at 20, -23 % at 27; -4 % at 2, 0 at 0), on top of TransonicDragRelief; nothing changes at the minimum-drag AoA. Given back per part with the game's own drag model, as a force along each part's airflow. 0 = off. Ignored in the Stock PerformanceProfile.");
			cDrTrans = dev.Bind("5. Airframe", "TransonicDragRelief", 0.2f, "1.8.38: Mach drag relief. The slope of drag vs Mach is scaled from the game's (below Mach 0.2) down to 1 - 1.1 x this (above Mach 0.7) and held there, never coming back, so there is no dip or hump in the drag curve. With 0.2: total drag -6 % at Mach 0.5, -12 % at 0.7, -16 % at 0.9, -17 % at 1.0, -19 % at 1.2, -20 % at 1.5, -20.5 % at 1.8. 0 = off. Ignored in the Stock PerformanceProfile.");
			cStockDrAoA = dev.Bind("5. Airframe", "LowAoADragRelief", 0.35f, "2.2.3: both profiles. This fraction of each part's AoA-dependent drag (its airfoil drag above that airfoil's minimum) is removed from low AoA up to LowAoADragReliefFadeStart; across the fade band it goes over to none in the Stock profile (post-stall drag is Aryx's) or to AoADragRelief in the Buffed profile. Stock thrust, parasitic drag and fuel burn are unchanged. Why: Aryx's airfoil drag rises steeply with AoA, so with no relief the best sustained turn sat at ~520 km/h IAS and 5.8 g (3 km, clean, 50 % fuel). Bench, Stock profile, same case: 0.15 -> 20.3 deg/s at 560 km/h, 6.6 g; 0.25 -> 21.2 at 600, 7.4 g; 0.35 -> 22.2 at 640, 8.2 g (no relief: 19.1). Instantaneous turn and 1 g top speed unchanged. 0 = Aryx's drag below the fade band.");
			cStockDrFade0 = dev.Bind("5. Airframe", "LowAoADragReliefFadeStart", 25f, "2.2.3: local AoA (deg) up to which LowAoADragRelief applies in full (both profiles).");
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
			cRevRoll = dev.Bind("3. Lateral-directional", "ReversedFlowRollSense", defaultValue: true, "Flying backwards (past 100 deg AoA, released below 80): the velocity-vector roll maps to body roll through cos(AoA), which flips sign there, so the stick would roll the jet the other way from the pilot's seat. This flips the roll command with it. Pitch and pedal are unaffected - the control loop already handles reversed flow on the surfaces themselves.");
			cRevNose = dev.Bind("3. Lateral-directional", "ReversedFlowNoseAxis", defaultValue: true, "4.3.0: flying backwards, the lateral stick and the pedals turn the nose the way they point (left = nose left and roll left, seen from the seat), on one axis that blends with the AoA: mostly yaw near 90 deg AoA, mostly roll near 180 - forward flight mirrored about the wings, so it is continuous through 90 deg (no flip). The sideslip hold steps aside from 100 to 120 deg AoA, since that axis is not the flight path. Needs ReversedFlowRollSense on. False = 4.2.0: the velocity-vector roll with its roll sense flipped past 100 deg AoA (released below 80), which left the nose yawing against the stick between 90 and ~135 deg. Takes effect at once.");
			cAsymFFAoA = dev.Bind("Asymmetric load", "AsymFeedForwardMaxAoA", 40f, "AsymFeedForward fades out over the 15 deg of AoA above this and is off beyond it. Post-stall the lateral authority it would spend is what the manoeuvre needs.");
			cAsymRollTau = dev.Bind("Asymmetric load", "AsymRollTau", 0.5f, "The roll reserve also has to cover the roll rate the law allows at this speed, reached in this many seconds. At high IAS that is what sets the reserve (250 deg/s in 0.5 s = 500 deg/s^2), not AsymRollReserveAccel. 0 = off (fixed reserve only).");
			cAsymRelaxBoost = dev.Bind("Asymmetric load", "AsymCapRelaxBoost", 4f, "The cap relaxes faster the further the demand is below the limit: rate = AsymCapRelaxRate x (1 + this x margin). With the load gone the cap comes back at (1 + this) x the base rate. 0 = fixed rate.");
			cAsymBoost = dev.Bind("Asymmetric load", "AsymAxisPriorityBoost", 0f, "With more than half the lateral authority taken by an asymmetric load, the allocator's roll and yaw weight is raised by up to this factor (1 = up to double) so the surfaces hold the jet straight before they fine-tune pitch. DEFAULT 0 (off) since 1.7.11: it starves the pitch axis, which showed up as the nozzles running nose-down in rolling pulls.");
			cAsymFloor = dev.Bind("Asymmetric load", "AsymMinAoACap", 12f, "The asymmetric-load cap never goes below this positive AoA (deg).");
			cAsymFloorNeg = dev.Bind("Asymmetric load", "AsymMinAoACapNegative", 8f, "The asymmetric-load cap never goes closer to 0 than this on the negative side (deg, magnitude).");
			cAsymTighten = dev.Bind("Asymmetric load", "AsymCapTightenRate", 10f, "How fast the cap comes down when the asymmetry grows (deg/s).");
			cAsymRelax = dev.Bind("Asymmetric load", "AsymCapRelaxRate", 8f, "How fast the cap goes back up when the asymmetry shrinks (store released, fuel, speed) (deg/s).");
			cAsymRec = dev.Bind("Asymmetric load", "AsymCapRecoveryRate", 15f, "Above the cap (it just came down), AoA is brought back to it at up to this rate (deg/s). Not cancelled by full stick.");
			cAsymMinIas = dev.Bind("Asymmetric load", "AsymMinIAS", 35f, "Below this IAS (m/s) the cap is held at its last value instead of being recomputed (no aero authority to judge by).");
			cAsymCapDemand = dev.Bind("Asymmetric load", "AsymCapDemand", 1.08f, "1.8.45: lateral demand (load share of the authority left after the reserve) at which the asymmetric-load AoA cap sits (1.8.44 and before: 1.0). With a light mixed load (6.5 cm lateral CG offset) the demand rises ~0.025 per degree above the cap, so 1.08 lets ~3 deg more AoA; heavier loads, whose demand rises faster with AoA, gain less.");
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
			cHybridHiLead = dev.Bind("4. Gains", "HybridIndiHighAoALead", 0.15f, "2.4.7: disturbance-rate lead, seconds. Above 25-40 deg AoA with the pitch stick released, the pitch loop takes the airframe's own pitch acceleration (what the effectors are not doing) forward by its rate x this, so a steady fall in it (a corkscrew release: the fuselage loses ~15 deg/s^2 of nose-up as the AoA unwinds 57 -> 48 deg) is met when it arrives instead of ~0.1 s late. Pitch damping is left out (it is the rate loop's own doing). 0 = off (2.4.6).");
			cAllocNozzleNewton = dev.Bind("4. Gains", "AllocNozzleNewton", true, "2.4.7: the allocator's last pass also tries a local Newton step for the nozzles, not only their grid points (every 4 deg). Without it the nozzles could sit between two grid points with a 2-10 deg/s^2 pitch miss while the joint steps were rejected (stabs stalled on their stops, large yaw miss): the post-corkscrew nose dip. false = 2.4.6.");
			cYawRefKmh = dev.Bind("3. Lateral-directional", "YawBrakeRefIAS", 270f, "1.9.2: IAS (km/h) of the reference manoeuvre for the high-AoA yaw braking floor (HighAoABrakeFloor): full pedal + full lateral stick at max AoA. The yaw-rate limit may not exceed what the yaw authority brakes in YawStopFactor x the low-IAS yaw boost at this IAS (1.8.45: 280).");
			cYawMargStart = dev.Bind("3. Lateral-directional", "YawStopTimeStartIAS", 210f, "1.9.3: below this IAS (km/h) the allowed yaw stop time grows smoothly (smoothstep) to YawStopTimeLowIAS x at YawStopTimeFullIAS; flat above and below.");
			cYawMargFull = dev.Bind("3. Lateral-directional", "YawStopTimeFullIAS", 150f, "1.9.3: IAS (km/h) at and below which the full YawStopTimeLowIAS applies.");
			cYawMargLow = dev.Bind("3. Lateral-directional", "YawStopTimeLowIAS", 1.5f, "1.9.3: allowed yaw stop time at low IAS relative to the reference: 1.5 = 50 % more yaw rate for the same yaw authority (slower braking). 1 = flat. (1.9.2's YawBrakeMargin* keys worked the other way and are no longer read.)");
			cYawMidF = dev.Bind("3. Lateral-directional", "YawStopTimeMidFactor", 0.75f, "2.0.12: 0.75 (1.9.4-2.0.11: 0.8). high-AoA yaw stop time (so yaw-rate limit) factor between YawStopTimeMidLoIAS and YawStopTimeMidHiIAS: 0.8 = 20 % less yaw rate for the same authority, faster braking. Smoothstep ramps of YawStopTimeMidRamp km/h outside the band. 1 = off.");
			cYawMidLo = dev.Bind("3. Lateral-directional", "YawStopTimeMidLoIAS", 280f, "2.0.12: 280 (1.9.4: 290). lower edge (km/h) of the mid-IAS yaw band (full factor from here up).");
			cYawMidHi = dev.Bind("3. Lateral-directional", "YawStopTimeMidHiIAS", 380f, "2.0.12: 380 (1.9.4: 330). upper edge (km/h) of the mid-IAS yaw band (full factor up to here).");
			cYawMidRamp = dev.Bind("3. Lateral-directional", "YawStopTimeMidRamp", 30f, "1.9.4: width (km/h) of the smooth ramps outside the mid-IAS yaw band (250-280 and 380-410 by default).");
			DevUnlocked = devUnlocked;
			MigrateOldConfig(cfg, dev, devUnlocked && devFresh);
			MigrateGovernorKeys(cfg, devUnlocked ? dev : null);
			// 2.0.0: the flaperon and LERX buffs were lowered (1.5 -> 1.2, 1.6 -> 1.3). A dev file written by an older build
			// still holds the old values, which BepInEx would keep; move it to the new ones once.
			// 2.0.11: back to 2.0.8's airframe: a dev file updated by 2.0.9/2.0.10 (revision 4) gets 2.0.8's values back once
			if (cAirframeRev.Value == 4)
			{
				if (Mathf.Abs(cAuthCapMargin.Value - 16f) < 0.01f)
				{
					cAuthCapMargin.Value = 8f;
				}
				if (Mathf.Abs(cStabPeakMargin.Value - 3f) < 0.01f)
				{
					cStabPeakMargin.Value = 90f;
				}
				Log.LogInfo("F-22E FCS 2.0.11 airframe defaults: back to 2.0.8 (LowThrustCapMargin 8, StabPeakMargin off).");
			}
			if (cAirframeRev.Value < 3)
			{
				Log.LogInfo($"F-22E FCS 2.0.3 airframe defaults: FlaperonEffectiveness {cFlaperonEff.Value} -> 1.2, LERXEffectiveness {cLerxEff.Value} -> 1.2, StabilatorEffectiveness {cStabEff.Value} -> 1.2.");
				if (cAirframeRev.Value < 2)
				{
					cFlaperonEff.Value = 1.2f;
				}
				cLerxEff.Value = 1.2f;
				cStabEff.Value = 1.2f;
				cAirframeRev.Value = 3;
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
				// 2.6.0: PullSideslipMargin is gone; only the trade default moves
				if (Mathf.Abs(cPullRollTrade.Value) < 0.0001f)
				{
					cPullRollTrade.Value = 0.7f;
				}
				Log.LogInfo($"F-22E FCS 2.2.23 defaults: PullRollTrade {cPullRollTrade.Value}.");
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
				Log.LogInfo($"F-22E FCS 2.2.27 defaults: PullRollTrade {cPullRollTrade.Value}.");
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
				Log.LogInfo("F-22E FCS 2.2.29 defaults: ReversedFlowRollSense true.");
				cAirframeRev.Value = 10;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.3.1: CoordinatedStop off (a file that 2.3.0 wrote with true is set to false once)
			if (cAirframeRev.Value < 11)
			{
				// 2.6.0: CoordinatedStop is gone; the revision still advances
				Log.LogInfo("F-22E FCS 2.3.1 defaults: (CoordinatedStop retired in 2.6.0).");
				cAirframeRev.Value = 11;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.3.2: RollFightReleased off (a file holding true is set to false once)
			if (cAirframeRev.Value < 12)
			{
				cRollFightRel.Value = false;
				Log.LogInfo("F-22E FCS 2.3.2 defaults: RollFightReleased false.");
				cAirframeRev.Value = 12;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.4.1: fuel moved twice as fast, balancing 40 -> 80 kg/s and trim 120 -> 240; files still on the old values are moved once
			if (cAirframeRev.Value < 13)
			{
				if (Mathf.Abs(cFuelRate.Value - 40f) < 0.01f)
				{
					cFuelRate.Value = 80f;
				}
				if (Mathf.Abs(cFuelTrimRate.Value - 120f) < 0.01f)
				{
					cFuelTrimRate.Value = 240f;
				}
				Log.LogInfo("2.4.1 defaults: FuelTransferRate 80, FuelTrimRate 240.");
				cAirframeRev.Value = 13;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.4.2: the damaged-engine cap 30 -> 50; files still on 30 are moved once
			if (cAirframeRev.Value < 14)
			{
				if (Mathf.Abs(cEngDmgCap.Value - 30f) < 0.01f)
				{
					cEngDmgCap.Value = 50f;
				}
				Log.LogInfo("2.4.2 defaults: EngineDamagedAoACap 50.");
				cAirframeRev.Value = 14;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.4.6: throttle response back to the game's own (1.3 -> 1); files still on 1.3 are moved once
			if (cAirframeRev.Value < 15)
			{
				if (Mathf.Abs(cThrResp.Value - 1.3f) < 0.001f)
				{
					cThrResp.Value = 1f;
				}
				Log.LogInfo("2.4.6 defaults: ThrottleResponseScale 1.");
				cAirframeRev.Value = 15;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.4.6 (final): lift +20 % (wing group at its 36 deg peak vs the old 30 deg lift, fuselage at its peak); a file
			// written by the first 2.4.6 cut (+10 %) and still on its values is moved once
			if (cAirframeRev.Value < 16)
			{
				if (Mathf.Abs(cWingLiftGain.Value - 1.115f) < 0.0005f)
				{
					cWingLiftGain.Value = 1.239f;
				}
				if (Mathf.Abs(cBodyBoostGain.Value - 1.1f) < 0.0005f)
				{
					cBodyBoostGain.Value = 1.2f;
				}
				Log.LogInfo("2.4.6 defaults: WingLiftPeakGain 1.239, BodyLiftBoostGain 1.2 (+20 % lift).");
				cAirframeRev.Value = 16;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.4.7: the wing and fuselage lift buffs start at 12 deg AoA (2.4.6: 17); files still on 17 are moved once
			if (cAirframeRev.Value < 17)
			{
				if (Mathf.Abs(cWingLiftStart.Value - 17f) < 0.001f)
				{
					cWingLiftStart.Value = 12f;
				}
				if (Mathf.Abs(cBodyBoostStart.Value - 17f) < 0.001f)
				{
					cBodyBoostStart.Value = 12f;
				}
				Log.LogInfo("2.4.7 defaults: WingLiftRampStart 12, BodyLiftBoostStart 12.");
				cAirframeRev.Value = 17;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.4.9 (built from 2.4.8): MaxPitchRate 60 (was 48) and WingCamberCoupling 0.3 (was 0), forced over whatever an
			// older dev file holds. Revision 21, not 18: the experimental 2.4.9-beta / 2.4.10-beta builds stamped 18-20, and a
			// file they touched must be moved too.
			if (cAirframeRev.Value < 21)
			{
				cMaxPitch.Value = 60f;
				cWingCamber.Value = 0.3f;
				Log.LogInfo("2.4.9 defaults: MaxPitchRate 60, WingCamberCoupling 0.3 (overriding the dev file).");
				cAirframeRev.Value = 21;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.4.17: PullSideslipMargin default 9 -> 6; a file still holding the old default is moved once, a value set by hand is kept
			if (cAirframeRev.Value < 22)
			{
				// 2.6.0: PullSideslipMargin is gone; the revision still advances
				Log.LogInfo("2.4.17 defaults: (PullSideslipMargin retired in 2.6.0).");
				cAirframeRev.Value = 22;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.5.0: RollStopSideslip default 3 -> 4 (the low-AoA margin of the rebuilt governor); a file still holding 3 is moved once
			if (cAirframeRev.Value < 23)
			{
				if (Mathf.Abs(cRollStopSlip.Value - 3f) < 0.001f)
				{
					cRollStopSlip.Value = 4f;
				}
				Log.LogInfo($"2.5.0 defaults: RollStopSideslip {cRollStopSlip.Value}.");
				cAirframeRev.Value = 23;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.35.3: FinLossAoACap default 40 -> 65 (no extra cap with the fins gone), forced over whatever an older dev file holds
			if (cAirframeRev.Value < 24)
			{
				cFinLossCap.Value = 65f;
				Log.LogInfo("2.35.3 defaults: FinLossAoACap 65 (overriding the dev file).");
				cAirframeRev.Value = 24;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 3.1.5: PushOverMinRate 20 -> 23 (Tom 6 Nov 23:29: "no guard, just a plan floor of 23deg/s"), forced over a 3.1.4 dev file
			if (cAirframeRev.Value < 25)
			{
				cPushMinRate.Value = 23f;
				Log.LogInfo("3.1.5 defaults: PushOverMinRate 23 (overriding the dev file).");
				cAirframeRev.Value = 25;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 3.2.0: airframe X2 (Tom 9 Nov: relaxed to neutral, stab lift peak 36). Forced once over older dev files: the stab peak
			// and the two schedules; 3.1.9's airframe is StabilatorStallAoA 25 with both schedules empty
			if (cAirframeRev.Value < 26)
			{
				cStallStab.Value = 36f;
				cForeLiftSched.Value = (string)cForeLiftSched.DefaultValue;
				cAftLiftSched.Value = (string)cAftLiftSched.DefaultValue;
				Log.LogInfo("3.2.0 defaults: airframe X2, StabilatorStallAoA 36 and the fore / aft lift schedules (overriding the dev file).");
				cAirframeRev.Value = 26;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 3.2.1: airframe X2e (Tom 9 Nov: the 45-55 deg roll dip). Forced once over older dev files: the two lift schedules only
			if (cAirframeRev.Value < 27)
			{
				cForeLiftSched.Value = (string)cForeLiftSched.DefaultValue;
				cAftLiftSched.Value = (string)cAftLiftSched.DefaultValue;
				Log.LogInfo("3.2.1 defaults: airframe X2e fore / aft lift schedules (overriding the dev file).");
				cAirframeRev.Value = 27;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 4.1.0: the native core on by default (Tom 10 Nov 17:06: "Set always rust allocator on by default and check 0 by default").
			// NativeAllocator 1 -> 2 and NativeAllocatorCheck 1 -> 0, forced once over whatever a 4.0.x dev file holds
			if (cAirframeRev.Value < 28)
			{
				cNativeAlloc.Value = 2;
				cNativeAllocCheck.Value = 0;
				Log.LogInfo("4.1.0 defaults: NativeAllocator 2, NativeAllocatorCheck 0 (overriding the dev file).");
				cAirframeRev.Value = 28;
				if (devUnlocked)
				{
					dev.Save();
				}
			}
			// 2.6.8: RollPaceRateBlend 0.4 -> 0. The blend splits the paced roll-rate target between the yaw-reach
			// window and the kinematic rate cap, and on this jet the cap is the pre-RevSim formula over the
			// hand-picked yaw set - far more pessimistic than the airframe. At 0.4 it held the COMMANDED roll rate
			// to 68-71 % of its own cap from 15 deg AoA up, with the sideslip at 1.3-2.0 deg of the margin: bench,
			// 65 deg AoA / 300 km/h, command 26.3 against a cap of 38.4 (paceHold 11.1), and the 1 Nov flight logs
			// put the falling-leaf yaw at 29.9 deg/s against 40.8 with the pacing absent. At 0 the window alone
			// governs (as on the Ifrit since 0.7.0, for the same reason) and the command runs at 93 % of the cap.
			// The blend is not a monotonic trade: 0.2 is worse than either end (14 regression cases worse against
			// 8 at 0, worst sideslip 7.5 deg against 6.7).
			// A file still holding exactly the old 0.4 default is moved once; a value set by hand is kept.
			if (cGovRev.Value < 1)
			{
				if (Mathf.Abs(cRollPaceRateBlend.Value - 0.4f) < 0.0001f)
				{
					cRollPaceRateBlend.Value = 0f;
				}
				cGovRev.Value = 1;
				cfg.Save();
				Log.LogInfo($"2.6.8 defaults: RollPaceRateBlend {cRollPaceRateBlend.Value} (0.4 -> 0 unless set by hand).");
			}
			// 2.6.9: RollPaceRateBlend back to Tom's 0.4084507. 2.6.8 moved it to 0 to get the falling-leaf yaw back, which also
			// took away the crisp reversals the blend paid for (23:37 log, 122 s: a reversal at 301 kt braked at 20 deg/s^2 for a
			// second with 2-2.7 deg of a 4 deg margin used). 2.6.9 gets the falling-leaf yaw back another way (the cap fades out
			// with the rudders), so the blend returns. A file holding exactly 2.6.8's 0 is moved once; anything else is kept.
			if (cGovRev.Value < 2)
			{
				if (Mathf.Abs(cRollPaceRateBlend.Value) < 0.0001f)
				{
					cRollPaceRateBlend.Value = 0.4084507f;
				}
				cGovRev.Value = 2;
				cfg.Save();
				Log.LogInfo($"2.6.9 defaults: RollPaceRateBlend {cRollPaceRateBlend.Value} (0 -> 0.4084507 unless set by hand).");
			}
		}

		// true when the player file exists and was written with HostLockVariant's old default (false)
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
								return l.EndsWith("false", StringComparison.OrdinalIgnoreCase);
							}
						}
						return false;
					}
				}
			}
			catch (Exception)
			{
			}
			return false;
		}

		public static bool DevUnlocked { get; private set; }

		// 1.8.44: the config was split into the player file (this plugin's) and the dev/debug file. Keys left in the player
		// file from older builds are orphans: the ones that moved section inside the player file (AI toggles) and, when the
		// dev file is being created now, the dev settings are carried over by key name once; then the orphans are dropped.
		// 2.6.0: the governor's keys moved from the Dev file to the player file; the retired devices' keys are gone. Once, the
		// Dev file's leftover values for the moved keys are carried into the player file, then every leftover (moved or retired)
		// is dropped from the Dev file so it no longer shows anywhere.
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
					Log.LogInfo($"F-22E FCS 2.6.0: lateral governor keys moved to the player config ({n} values carried over, {dropped} old lateral keys dropped from the dev file).");
				}
			}
			catch (Exception ex)
			{
				Log.LogWarning("F-22E FCS governor key migration: " + ex.Message);
			}
		}

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
				Log.LogInfo($"F-22E FCS: old config keys migrated ({n} values carried over{(toDev ? ", dev file created" : "")}, leftovers removed).");
			}
			catch (Exception ex)
			{
				Log.LogWarning("F-22E FCS config migration: " + ex.Message);
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
			TryPatch(h, "AeroJobSettings.Schedule", delegate
			{
				h.Patch(AccessTools.Method(typeof(AeroJobSettings), "Schedule"), new HarmonyMethod(typeof(StallStaggerJobs), "SchedulePrefix"));
			});
			TryPatch(h, "ControlSurface.UpdateJobFields", delegate
			{
				h.Patch(AccessTools.Method(typeof(ControlSurface), "UpdateJobFields"), null, new HarmonyMethod(typeof(Hooks), "SurfacePostfix"));
			});
			TryPatch(h, "Airbrake.Update", delegate
			{
				h.Patch(AccessTools.Method(typeof(Airbrake), "Update"), new HarmonyMethod(typeof(Hooks), "AirbrakeUpdatePrefix"), new HarmonyMethod(typeof(Hooks), "AirbrakeUpdatePostfix"));
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
			TryPatch(h, "HighLiftDevice.FixedUpdate", delegate
			{
				h.Patch(AccessTools.Method(typeof(HighLiftDevice), "FixedUpdate"), new HarmonyMethod(typeof(Hooks), "FlapDevicePrefix"));
			});
			if (FlapSystem.ResponderType != null)
			{
				TryPatch(h, "AryxAlphaResponder.FixedUpdate", delegate
				{
					h.Patch(AccessTools.Method(FlapSystem.ResponderType, "FixedUpdate"), new HarmonyMethod(typeof(Hooks), "FlapDevicePrefix"));
				});
			}
			else
			{
				Log.LogWarning("AryxAlphaResponder type not found (original F-22E mod not loaded?) - flap takeover limited to the high-lift devices.");
			}
			if (Hooks.VentType != null)
			{
				TryPatch(h, "Aryx_OverpressureVentController.CalculateDeployment", delegate
				{
					h.Patch(AccessTools.Method(Hooks.VentType, "CalculateDeployment"), null, new HarmonyMethod(typeof(Hooks), "VentPostfix"));
				});
			}
			else
			{
				Log.LogInfo("Aryx_OverpressureVentController not found - no vent doors to take over.");
			}
			TryPatch(h, "LandingGear.FixedUpdate", delegate
			{
				h.Patch(AccessTools.Method(typeof(LandingGear), "FixedUpdate"), new HarmonyMethod(typeof(Hooks), "GearPrefix"), new HarmonyMethod(typeof(Hooks), "GearPostfix"));
			});
			TryPatch(h, "Turbofan.FixedUpdate", delegate
			{
				h.Patch(AccessTools.Method(typeof(Turbofan), "FixedUpdate"), new HarmonyMethod(typeof(Hooks), "TurbofanPrefix"), null, new HarmonyMethod(typeof(Hooks), "TurbofanTranspiler"));
			});
			Log.LogInfo($"F-22E FCS {FcsStandalonePlugin.Version} ({(StockPerformance ? "stock" : "buffed")} performance{(DevUnlocked ? ", dev/debug settings unlocked" : "")}) ready (AoA +{AoAPos}/{AoANeg}, G +{GPos}/{GNeg}, MPO +{MpoGPos}/{MpoGNeg}, " + string.Format("q<= {0}, p<= {1}, r<= {2}; asym-load AoA cap {3}; flaperon x{4}, stab x{5}, actuators x{6}, thrust x{7}, parasitic drag x{8}).", MaxPitchRate, MaxRollRate, MaxYawRate, AsymLimiter ? ("on, reserve " + AsymReserve.ToString("0.00")) : "off", FlaperonEffectiveness, StabilatorEffectiveness, ActuatorScale, ThrustScale, DragScale));
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
