using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	internal static class Hooks
	{
		// 2.2.6: Airbrake.Update opens the panel while the aircraft's controlInputs.throttle == 0 and never reads the brake.
		// For F-22Es under the FCS the throttle it sees is swapped, for that one call, for 0 (speedbrake commanded: in the
		// air with the brake held, or zero throttle with ThrottleIndependentAirbrake off; never on the main wheels, where the
		// brake is the rollout aero-brake) or 1 (not), and
		// put back right after; the game's own code still does the opening rate, visuals and drag.
		private static readonly AccessTools.FieldRef<Airbrake, Aircraft> AB_aircraft = AccessTools.FieldRefAccess<Airbrake, Aircraft>("attachedAircraft");

		private static readonly AccessTools.FieldRef<Airbrake, ControlInputs> AB_inputs = AccessTools.FieldRefAccess<Airbrake, ControlInputs>("controlInputs");

		// 2.2.16: the brake's opening (0..1). The game steps it toward 1 at openSpeed while the throttle input reads 0, toward 0
		// otherwise; the postfix stops it at the FCS's opening target so it can sit part-open (AirbrakeAoAFade).
		internal static readonly AccessTools.FieldRef<Airbrake, float> AB_open = AccessTools.FieldRefAccess<Airbrake, float>("openAmount");

		public static void AirbrakeUpdatePrefix(Airbrake __instance, out float __state)
		{
			__state = float.NaN;
			try
			{
				if ((UnityEngine.Object)(object)__instance == null)
				{
					return;
				}
				Aircraft a = AB_aircraft(__instance);
				ControlInputs ci = AB_inputs(__instance);
				if ((UnityEngine.Object)(object)a == null || ci == null || !Registry.IsF22(a))
				{
					return;
				}
				FcsController c = Registry.Find(a);
				if (c == null || !c.Engaged || c.Failed)
				{
					return;
				}
				__state = ci.throttle;
				// 2.2.16: open toward the FCS's target (1 = full, less when the high-AoA drag already dominates), close otherwise
				float want = c.AirbrakeOpenTarget;
				ci.throttle = (want > 0.001f && AB_open(__instance) < want) ? 0f : 1f;
			}
			catch
			{
				__state = float.NaN;
			}
		}

		public static void AirbrakeUpdatePostfix(Airbrake __instance, float __state)
		{
			if (float.IsNaN(__state))
			{
				return;
			}
			try
			{
				ControlInputs ci = AB_inputs(__instance);
				if (ci != null)
				{
					FcsController c = Registry.Find(AB_aircraft(__instance));
					if (c != null)
					{
						float want = c.AirbrakeOpenTarget;
						ref float open = ref AB_open(__instance);
						// stop at the target from either side (opening: no further than it; closing: no lower than it)
						open = (ci.throttle == 0f) ? Mathf.Min(open, want) : Mathf.Max(open, want);
						c.airbrakeOpenNow = open;
					}
					ci.throttle = __state;
				}
			}
			catch
			{
			}
		}

		public static readonly Type VentType = AccessTools.TypeByName("Aryx_F22E_StrikeRaptor.Aryx_OverpressureVentController");

		private static readonly FieldInfo fVentAircraft = ((VentType != null) ? AccessTools.Field(VentType, "aircraft") : null);

		private static bool aimValid;

		private static GlobalPosition aimDest;

		private static bool aimVelocityCtx;

		private static bool aimMovingCtx;

		public static void AutoAimPrefix(GlobalPosition destination, bool aimVelocity, Vector3 targetVel)
		{
			aimValid = true;
			aimDest = destination;
			aimVelocityCtx = aimVelocity;
			aimMovingCtx = targetVel.sqrMagnitude > 900f;
		}

		public static void AutoAimPostfix()
		{
			aimValid = false;
		}

		private static float GunRollFade(Aircraft aircraft, FcsController c)
		{
			c.aiGunTrack = false;
			if (!aimValid || aimVelocityCtx || !aimMovingCtx || (UnityEngine.Object)(object)aircraft.cockpit == null)
			{
				return 1f;
			}
			c.aiGunTrack = true;
			if (!Plugin.AIGunRollFade)
			{
				return 1f;
			}
			Vector3 vector = aircraft.cockpit.xform.forward;
			WeaponStation weaponStation = ((aircraft.weaponManager != null) ? aircraft.weaponManager.currentWeaponStation : null);
			if (weaponStation != null && weaponStation.WeaponInfo != null && weaponStation.WeaponInfo.gun)
			{
				foreach (Weapon weapon in weaponStation.Weapons)
				{
					if ((UnityEngine.Object)(object)weapon != null)
					{
						vector += weapon.transform.forward * 1000f;
					}
				}
			}
			float num = Vector3.Angle(vector, aimDest - aircraft.GlobalPosition());
			return Mathf.Clamp01((num - GunFadeStart) / GunFadeSpan);
		}

		internal static float GunFadeStart = 1f;

		internal static float GunFadeSpan = 5f;

		public static bool FilterPrefix(ControlsFilter __instance, ControlInputs inputs, Vector3 rawInputs, Rigidbody rb, float gForce, bool flightAssist)
		{
			Aircraft aircraft = Acc.CF_aircraft(__instance);
			if ((UnityEngine.Object)(object)aircraft == null || !Registry.IsF22(aircraft))
			{
				return true;
			}
			// 2.0.6 fuel balancing runs for every locally simulated F-22E, FCS engaged or not (2.4.0: if the host allows the FCS)
			try
			{
				if (MpPolicy.FcsAllowed)
				{
					FuelBalance.Step(aircraft, Time.fixedDeltaTime);
				}
			}
			catch (Exception ex)
			{
				FuelBalance.Forget(aircraft);
				Plugin.Log.LogWarning("F-22E fuel balance: " + ex.Message);
			}
			if (!Plugin.Enabled)
			{
				// (also when the host does not allow the FCS: hand the surfaces back to the stock fly-by-wire)
				Registry.Disengage(aircraft);
				return true;
			}
			// 3.1.0: after the pilot ejects, the jet goes back to the stock fly-by-wire. Before, Player == null made the empty jet look
			// like an AI jet, and with AIFlightControl on the FCS kept flying it (reported on Discord, 5 Nov).
			if (aircraft.HasEjected())
			{
				Registry.Disengage(aircraft);
				return true;
			}
			bool flag = (UnityEngine.Object)(object)aircraft.Player == null;
			Registry.NoteHook(aircraft, flag);
			if (!aircraft.LocalSim || (flag && !Plugin.AIFlightControl))
			{
				Registry.Disengage(aircraft);
				return true;
			}
			FcsController orCreate = Registry.GetOrCreate(aircraft);
			if (orCreate == null || orCreate.Failed)
			{
				return true;
			}
			try
			{
				if (orCreate.isAI != flag)
				{
					orCreate.isAI = flag;
					if (flag)
					{
						Registry.ReadStockRateGain(__instance, orCreate);
					}
				}
				if (flag)
				{
					float fade = GunRollFade(aircraft, orCreate);
					orCreate.aiRollFade = fade;
					inputs.roll *= fade;
				}
				orCreate.stockCf = __instance;
				TickStats.MemStart(orCreate);   // 4.1.3: managed heap and garbage-collection count before the tick
				long tickT0 = TickStats.Start();   // 3.0.4 tick timer
				orCreate.Tick(inputs, flag || flightAssist);
				long vmcT0 = TickStats.Start();   // 3.1.1: VmcApply on its own
				orCreate.VmcApply(inputs, Time.fixedDeltaTime);
				TickStats.Stop(orCreate, tickT0, TickStats.Start() - vmcT0);
				return false;
			}
			catch (Exception e)
			{
				Registry.Fail(orCreate, e);
				return true;
			}
		}

		// 1.8.36 nose-wheel FBW: LandingGear.FixedUpdate turns the steerable wheel toward controlInputs.yaw x steeringLock at
		// steeringSpeed. For the F-22E's steering leg the FCS's wheel command is swapped in for the pedal during that call
		// and the pilot's pedal restored afterwards (the main legs' differential brake keeps reading the real pedal).
		// 4.1.5: what GearPrefix swapped on this leg's (shared) ControlInputs, for GearPostfix to put back
		public struct GearSwap
		{
			public float yaw;

			public float brake;

			public bool yawSet;

			public bool brakeSet;
		}

		public static void GearPrefix(LandingGear __instance, out GearSwap __state)
		{
			__state = default(GearSwap);
			try
			{
				if (!Plugin.Enabled)
				{
					return;
				}
				bool steerLeg = Acc.LG_steering(__instance);
				Aircraft aircraft = Acc.LG_aircraft(__instance);
				if ((UnityEngine.Object)(object)aircraft == null || !Registry.IsF22(aircraft))
				{
					return;
				}
				FcsController fcsController = Registry.Find(aircraft);
				if (fcsController == null || fcsController.Failed)
				{
					return;
				}
				ControlInputs controlInputs = Acc.LG_inputs(__instance);
				if (controlInputs == null)
				{
					return;
				}
				float yaw = 0f;
				if (steerLeg ? fcsController.NoseSteerOverride(out yaw) : fcsController.StockBrakeYaw(out yaw))
				{
					__state.yaw = controlInputs.yaw;
					__state.yawSet = true;
					controlInputs.yaw = yaw;
				}
				// 4.1.5 main brakes as a yaw effector: the game sums brake + Clamp01(yaw x 0.3 x dbf) (3-30 m/s) for a braked leg;
				// write the brake so that sum is the FCS's target (never below your own brake)
				if (!steerLeg && fcsController.MainBrakeTarget(__instance, out float target))
				{
					float dbf = Acc.LG_dbf(__instance);
					float gsp = Acc.LG_groundSpeed(__instance);
					float stock = (dbf != 0f && gsp > 3f && gsp < 30f) ? Mathf.Clamp01(controlInputs.yaw * 0.3f * dbf) : 0f;
					__state.brake = controlInputs.brake;
					__state.brakeSet = true;
					controlInputs.brake = Mathf.Max(controlInputs.brake, target - stock);
				}
			}
			catch (Exception)
			{
				GearPostfix(__instance, __state);
				__state = default(GearSwap);
			}
		}

		public static void GearPostfix(LandingGear __instance, GearSwap __state)
		{
			if (!__state.yawSet && !__state.brakeSet)
			{
				return;
			}
			try
			{
				ControlInputs controlInputs = Acc.LG_inputs(__instance);
				if (controlInputs != null)
				{
					if (__state.yawSet)
					{
						controlInputs.yaw = __state.yaw;
					}
					if (__state.brakeSet)
					{
						controlInputs.brake = __state.brake;
					}
				}
			}
			catch (Exception)
			{
			}
		}

		public static void SurfacePostfix(ControlSurface __instance)
		{
			try
			{
				Aircraft aircraft = Acc.CS_aircraft(__instance);
				if ((UnityEngine.Object)(object)aircraft != null && Registry.IsF22(aircraft))
				{
					Registry.EnsureSetup(aircraft);
				}
				if (!Acc.JobCreated(__instance))
				{
					return;
				}
				ref ControlSurfaceFields reference = ref Acc.Job(__instance);
				if ((UnityEngine.Object)(object)aircraft != null && Registry.IsF22(aircraft))
				{
					reference.servoSpeed = Acc.CS_servo(__instance);
				}
				FcsController fcsController = Registry.Find(aircraft) ?? Registry.FindAnySurface(__instance);
				if (fcsController != null && fcsController.Engaged && !fcsController.Failed)
				{
					reference.brakeRange = 0f;
					reference.controlInputs.brake = 0f;
					reference.controlInputs.throttle = (fcsController.SplitDeploy ? 0f : 1f);
				}
				if (!Registry.TrySurface(__instance, out var s, out var c))
				{
					return;
				}
				if (c.Engaged && !c.Failed && s.active && !Acc.CS_locked(__instance))
				{
					if (!s.overriding)
					{
						float currentPitch = reference.currentPitch + reference.currentRoll + reference.currentYaw;
						reference.currentPitch = currentPitch;
						reference.currentRoll = 0f;
						reference.currentYaw = 0f;
						s.overriding = true;
					}
					reference.pitchRange = s.Span;
					reference.rollRange = 0f;
					reference.yawRange = 0f;
					reference.controlInputs.pitch = Mathf.Clamp(s.cmdOut / s.Span, -1f, 1f);
					reference.controlInputs.roll = 0f;
					reference.controlInputs.yaw = 0f;
				}
				else if (s.overriding)
				{
					reference.pitchRange = s.origPitchRange;
					reference.rollRange = s.origRollRange;
					reference.yawRange = s.origYawRange;
					reference.brakeRange = Acc.CS_brakeRange(__instance);
					s.overriding = false;
					if (c.Failed)
					{
						Registry.ForgetSurface(__instance);
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("F-22E surface hook: " + ex.Message);
			}
		}

		public static void VentPostfix(MonoBehaviour __instance, ref float __result)
		{
			try
			{
				string mode = Plugin.VentDoors;
				if (mode == "Stock" || fVentAircraft == null || __instance == null)
				{
					return;
				}
				Aircraft a = fVentAircraft.GetValue(__instance) as Aircraft;
				FcsController c = Registry.Find(a);
				if (c == null)
				{
					return;
				}
				if (mode == "Airbrake")
				{
					if (c.Engaged && !c.Failed)
					{
						__result = Mathf.Clamp01(c.airBrake);
					}
					return;
				}
				__result = BypassDoorTarget(c);
			}
			catch
			{
			}
		}

		// 1.8.37: the six upper-fuselage gills behave as inlet bypass doors. They dump the inlet air the
		// engines don't take: captured airflow grows with Mach, engine demand with spool, so they open at
		// speed with the throttle back, close as the engines spool up, and close at high AoA (the inlet
		// captures less). No brake input.
		internal static float BypassDoorTarget(FcsController c)
		{
			Aircraft a = c.ac;
			if (a == null)
			{
				return 0f;
			}
			float rho = Mathf.Clamp(a.airDensity, 0.05f, 1.4f);
			float h = (1f - Mathf.Pow(rho / 1.225f, 0.23496f)) / 2.2558e-5f;
			float T = Mathf.Max(216.65f, 288.15f - 0.0065f * h);
			float mach = c.tas / Mathf.Sqrt(401.87f * T);
			float demand = 0f;
			int n = 0;
			foreach (Turbofan e in c.tvc.engines)
			{
				if (e == null)
				{
					continue;
				}
				float r = 0f;
				try
				{
					r = Mathf.Clamp01(e.GetRPMRatio());
				}
				catch
				{
				}
				demand += (r < 0.25f) ? 0f : r;
				n++;
			}
			demand = (n > 0) ? (demand / n) : 0f;
			float open0 = 0.35f + 0.95f * demand;
			float x = Mathf.Clamp01((mach - open0) / 0.45f);
			float aoa = 1f - Mathf.InverseLerp(12f, 24f, Mathf.Abs(c.alpha));
			return Mathf.SmoothStep(0f, 1f, x) * aoa;
		}

		public static bool FlapDevicePrefix(MonoBehaviour __instance)
		{
			try
			{
				if (__instance != null && Registry.TryDevice(__instance, out var c) && c.Engaged && !c.Failed && c.flaps.Active)
				{
					return false;
				}
			}
			catch
			{
			}
			return true;
		}

		public static void TurbofanPrefix(Turbofan __instance)
		{
			Aircraft aircraft = Acc.TF_aircraft(__instance);
			if ((UnityEngine.Object)(object)aircraft != null && Registry.IsF22(aircraft))
			{
				Registry.EnsureSetup(aircraft);
			}
		}

		public static float TvcPitch(ControlInputs ci)
		{
			if (Plugin.DisableTVC && ci != null && Registry.TryInputs(ci, out var _))
			{
				return 0f;
			}
			if (ci != null && Registry.TryInputs(ci, out var c) && c.Engaged && !c.Failed)
			{
				return c.tvcPitchInput;
			}
			return ci?.pitch ?? 0f;
		}

		public static float NozzleSlew(Turbofan t)
		{
			if (t == null)
			{
				return 70f;
			}
			Aircraft aircraft = Acc.TF_aircraft(t);
			if (!((UnityEngine.Object)(object)aircraft != null) || !Registry.IsF22(aircraft))
			{
				return 70f;
			}
			return Plugin.TvcSlew;
		}

		public static float NozzleSlewNeg(Turbofan t)
		{
			return 0f - NozzleSlew(t);
		}

		public static IEnumerable<CodeInstruction> TurbofanTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			FieldInfo fPitch = AccessTools.Field(typeof(ControlInputs), "pitch");
			MethodInfo mTvc = AccessTools.Method(typeof(Hooks), "TvcPitch");
			MethodInfo mSlew = AccessTools.Method(typeof(Hooks), "NozzleSlew");
			MethodInfo mSlewNeg = AccessTools.Method(typeof(Hooks), "NozzleSlewNeg");
			int nPitch = 0;
			int nSlew = 0;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo fieldInfo && fieldInfo == fPitch)
				{
					CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Call, mTvc);
					codeInstruction.labels.AddRange(instruction.labels);
					codeInstruction.blocks.AddRange(instruction.blocks);
					nPitch++;
					yield return codeInstruction;
				}
				else if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float fv && (fv == 70f || fv == -70f))
				{
					CodeInstruction codeInstruction2 = new CodeInstruction(OpCodes.Ldarg_0);
					codeInstruction2.labels.AddRange(instruction.labels);
					codeInstruction2.blocks.AddRange(instruction.blocks);
					yield return codeInstruction2;
					yield return new CodeInstruction(OpCodes.Call, (fv > 0f) ? mSlew : mSlewNeg);
					nSlew++;
				}
				else
				{
					yield return instruction;
				}
			}
			Plugin.Log.LogInfo($"Turbofan transpiler: {nPitch} pitch read(s) routed to FCS, {nSlew} nozzle slew constant(s) scaled.");
		}
	}
}
