using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// Vortex 0.1.0: the game-side hooks of the STOVL laws (Stovl.cs).
	//  - SwivelDuctSystem.FixedUpdate: while the FCS owns the lift system, the swivel command (customAxis1) is the FCS's, the
	//    game's auto modes are held in Manual (Forward above the swivel speed limit, where the game forces it anyway), and the
	//    pitch / roll / yaw inputs the lift fan, roll posts and nozzle yaw bearing read are swapped for the allocator's commands
	//    for the duration of the call.
	//  - Turbojet.FixedUpdate: the throttle reads (RPM target and afterburner) take the STOVL laws' throttle while they command
	//    it; the customAxis1 read (afterburner lock) takes the FCS's swivel command.
	//  - ControlsFilter.ToggleAutoHover (the Flight Assist hold): conversion / reconversion; SetAutoHover(true) is refused.
	internal static class StovlHooks
	{
		public struct Swap
		{
			public ControlInputs ci;

			public float pitch;

			public float roll;

			public float yaw;
		}

		private static StovlSystem Owned(Aircraft a)
		{
			if ((UnityEngine.Object)(object)a == null || !Registry.IsF22(a))
			{
				return null;
			}
			FcsController c = Registry.Find(a);
			StovlSystem s = c?.stovl;
			return (s != null && s.owns && c.Engaged && !c.Failed) ? s : null;
		}

		public static void SwivelPrefix(SwivelDuctSystem __instance, out Swap __state)
		{
			__state = default(Swap);
			try
			{
				Aircraft a = SwivelLink.AircraftOf(__instance);
				StovlSystem s = Owned(a);
				// Vortex 0.5.0: the lift fan's vane box moves here, before the game sizes and applies the fan's force (back to
				// the game's fixed thrust line whenever the STOVL laws are not commanding it)
				if ((UnityEngine.Object)(object)a != null && Registry.IsF22(a))
				{
					StovlSystem sv = s ?? Registry.Find(a)?.stovl;
					if (sv != null && sv.hw is SwivelLink vl)
					{
						vl.VaneStep(s != null && s.vaneLive && !s.freestyle, (s != null) ? s.vaneOut : 0f, Time.fixedDeltaTime);
					}
				}
				if (s == null)
				{
					return;
				}
				ControlInputs ci = a.GetInputs();
				if (ci == null)
				{
					return;
				}
				__state.ci = ci;
				__state.pitch = ci.pitch;
				__state.roll = ci.roll;
				__state.yaw = ci.yaw;
				ci.pitch = s.fanIn;
				ci.roll = s.postIn;
				ci.yaw = s.yawIn;
				// (0.2.2 freestyle: the pilot's Custom Axis 1, built by the game on the value the FCS left there)
				if (!s.freestyle)
				{
					ci.customAxis1 = s.axis1Out;
				}
				// Forward where the game forces it (no mode-change message every frame), Manual otherwise (no auto modes)
				bool fwd = a.speed > s.hw.MaxSwivelAirspeed;
				SwivelLink.fMode.SetValue(__instance, fwd ? SwivelLink.ForwardMode : SwivelLink.ManualMode);
			}
			catch (Exception ex)
			{
				__state = default(Swap);
				Plugin.Log.LogWarning("Vortex FCS STOVL swivel hook: " + ex.Message);
			}
		}

		// Vortex 0.1.7: the lift fan's upper (inlet) door. The game animates it as a moving panel with the nozzle swivel, so with
		// the fan's lower doors open and the nozzle aft (the STOVL laws at speed) it stayed shut, and puts its drag on it as
		// swivel x rho x v^2 on the fan part. Here it opens with the lower doors (never less than the game's swivel opening),
		// on every client (both positions are computed from the synced inputs), and on jets simulated here its drag follows the
		// door: the game's swivel term for this panel is taken back out and StovlFanDoorDragArea x sin(door angle) / sin(full
		// angle) x 1/2 rho v^2 put in at the hinge. Nothing is computed while the door is shut.
		// (0.1.4-0.1.6 put that drag on the two lower doors, the game's "thrust doors"; they now carry none, as in the game.)
		private sealed class TopDoor
		{
			public Transform t;

			public Vector3 minPos, maxPos, minRot, maxRot;

			public float fullAngle;

			public bool gameDrag;

			public Rigidbody rb;

			public Aircraft ac;

			public float aPrev = -1f;

			public float swPrev = -1f;
		}

		private static readonly Dictionary<int, TopDoor> topDoors = new Dictionary<int, TopDoor>();

		private static readonly FieldInfo fPanels = AccessTools.Field(typeof(SwivelDuctSystem), "movingPanels");

		private static readonly FieldInfo fDoorPos = AccessTools.Field(typeof(SwivelDuctSystem), "thrustDoorPosition");

		private static readonly FieldInfo fSwivelPos = AccessTools.Field(typeof(SwivelDuctSystem), "swivelPosition");

		private static TopDoor FindTopDoor(SwivelDuctSystem sds)
		{
			TopDoor td = null;
			Aircraft owner = SwivelLink.AircraftOf(sds);
			if ((UnityEngine.Object)(object)owner == null || !Registry.IsF22(owner))
			{
				return null;
			}
			if (fPanels?.GetValue(sds) is Array arr)
			{
				foreach (object o in arr)
				{
					if (o == null)
					{
						continue;
					}
					Type pt = o.GetType();
					Transform t = AccessTools.Field(pt, "transform")?.GetValue(o) as Transform;
					if (t == null || t.name.IndexOf("door", StringComparison.OrdinalIgnoreCase) < 0)
					{
						continue;
					}
					td = new TopDoor
					{
						t = t,
						minPos = (Vector3)AccessTools.Field(pt, "minPosition").GetValue(o),
						maxPos = (Vector3)AccessTools.Field(pt, "maxPosition").GetValue(o),
						minRot = (Vector3)AccessTools.Field(pt, "minRotation").GetValue(o),
						maxRot = (Vector3)AccessTools.Field(pt, "maxRotation").GetValue(o),
						gameDrag = (float)AccessTools.Field(pt, "extraDrag").GetValue(o) > 0f
					};
					td.fullAngle = Mathf.Max(1f, (td.maxRot - td.minRot).magnitude);
					object up = AccessTools.Field(pt, "unitPart")?.GetValue(o);
					td.rb = (up != null) ? (Traverse.Create(up).Field("rb").GetValue() as Rigidbody) : null;
					if (td.rb == null)
					{
						td.rb = t.GetComponentInParent<Rigidbody>();
					}
					break;
				}
			}
			if (td != null)
			{
				td.ac = owner;
				Plugin.Log.LogInfo($"Vortex FCS: lift-fan upper door '{td.t.name}' opens with the fan doors ({td.fullAngle:F0} deg); drag area {Plugin.StovlFanDoorDragArea:F2} m^2 fully open" + ((td.rb == null) ? " - no rigidbody found, no drag" : "") + ".");
			}
			return td;
		}

		private static void UpperDoor(SwivelDuctSystem sds)
		{
			if (fDoorPos == null || fSwivelPos == null)
			{
				return;
			}
			int id = sds.GetInstanceID();
			if (!topDoors.TryGetValue(id, out var td))
			{
				td = FindTopDoor(sds);
				topDoors[id] = td;
			}
			if (td == null || td.t == null)
			{
				return;
			}
			float sw = (float)fSwivelPos.GetValue(sds);
			float a = Mathf.Clamp01(Mathf.Max(sw, (float)fDoorPos.GetValue(sds)));
			// the game re-poses the panel whenever the swivel moves: put it back where the door is
			if (a != td.aPrev || sw != td.swPrev)
			{
				td.t.localPosition = Vector3.Lerp(td.minPos, td.maxPos, a);
				td.t.localEulerAngles = Vector3.Lerp(td.minRot, td.maxRot, a);
				td.aPrev = a;
				td.swPrev = sw;
			}
			if (a <= 0f || td.rb == null || (UnityEngine.Object)(object)td.ac == null || !td.ac.LocalSim)
			{
				return;
			}
			float rho = td.ac.airDensity;
			Vector3 wind = td.ac.GetWindVelocity();
			if (td.gameDrag && sw > 0f)
			{
				// the game's own term for this panel (MovingPanel.ApplyDrag: swivel x rho x v^2 on the part), taken back out
				Vector3 vg = td.rb.velocity - wind;
				td.rb.AddForce(sw * rho * vg.sqrMagnitude * vg.normalized);
			}
			float area = Plugin.StovlFanDoorDragArea;
			if (area <= 0f)
			{
				return;
			}
			Vector3 p = td.t.position;
			Vector3 v = td.rb.GetPointVelocity(p) - wind;
			float v2 = v.sqrMagnitude;
			if (v2 < 1f)
			{
				return;
			}
			float full = Mathf.Min(td.fullAngle, 90f) * 0.0174532924f;
			float k = Mathf.Sin(a * full) / Mathf.Sin(full);
			td.rb.AddForceAtPosition(v * (-0.5f * rho * Mathf.Sqrt(v2) * area * k), p);
		}

		public static void SwivelPostfix(SwivelDuctSystem __instance, Swap __state)
		{
			try
			{
				UpperDoor(__instance);
			}
			catch
			{
			}
			if (__state.ci != null)
			{
				__state.ci.pitch = __state.pitch;
				__state.ci.roll = __state.roll;
				__state.ci.yaw = __state.yaw;
			}
		}

		public static float EngineThrottle(ControlInputs ci)
		{
			if (ci != null && Registry.TryInputs(ci, out var c) && c.stovl != null && c.stovl.owns && c.stovl.throttleOverride && c.Engaged && !c.Failed)
			{
				return c.stovl.throttleOut;
			}
			return (ci != null) ? ci.throttle : 0f;
		}

		public static float EngineAxis1(ControlInputs ci)
		{
			if (ci != null && Registry.TryInputs(ci, out var c) && c.stovl != null && c.stovl.owns && c.Engaged && !c.Failed && !c.stovl.freestyle)
			{
				return c.stovl.axis1Out;
			}
			return (ci != null) ? ci.customAxis1 : 1f;
		}

		public static IEnumerable<CodeInstruction> TurbojetThrottleTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			FieldInfo fThr = AccessTools.Field(typeof(ControlInputs), "throttle");
			FieldInfo fAx = AccessTools.Field(typeof(ControlInputs), "customAxis1");
			MethodInfo mThr = AccessTools.Method(typeof(StovlHooks), "EngineThrottle");
			MethodInfo mAx = AccessTools.Method(typeof(StovlHooks), "EngineAxis1");
			int nThr = 0;
			int nAx = 0;
			foreach (CodeInstruction ins in instructions)
			{
				if (ins.opcode == OpCodes.Ldfld && ins.operand is FieldInfo f && (f == fThr || f == fAx))
				{
					CodeInstruction rep = new CodeInstruction(OpCodes.Call, (f == fThr) ? mThr : mAx);
					rep.labels.AddRange(ins.labels);
					rep.blocks.AddRange(ins.blocks);
					if (f == fThr)
					{
						nThr++;
					}
					else
					{
						nAx++;
					}
					yield return rep;
				}
				else
				{
					yield return ins;
				}
			}
			Plugin.Log.LogInfo($"Vortex FCS STOVL Turbojet transpiler: {nThr} throttle and {nAx} customAxis1 read(s) routed to the STOVL laws.");
		}

		public static bool ToggleAutoHoverPrefix(ControlsFilter __instance)
		{
			try
			{
				Aircraft a = Acc.CF_aircraft(__instance);
				if ((UnityEngine.Object)(object)a == null || !Registry.IsF22(a))
				{
					return true;
				}
				FcsController c = Registry.Find(a);
				StovlSystem s = c?.stovl;
				if (s == null || !s.owns || !c.Engaged || c.Failed)
				{
					Plugin.Log.LogInfo($"Vortex FCS STOVL: Flight Assist hold passed to the game (STOVL {(s == null ? "not set up" : (s.owns ? "owned" : "not owned"))}, FCS {(c == null ? "none" : (c.Failed ? "failed" : (c.Engaged ? "engaged" : "not engaged")))}).");
					return true;
				}
				Plugin.Log.LogInfo($"Vortex FCS STOVL: Flight Assist hold (state {s.state}).");
				s.massSeen = c.MassKg;
				s.cgSeen = c.CgNow;
				s.RequestToggle(Time.timeSinceLevelLoad, c.ias);
				return false;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Vortex FCS STOVL toggle hook: " + ex.Message);
				return true;
			}
		}

		public static bool SetAutoHoverPrefix(ControlsFilter __instance, bool enabled)
		{
			if (!enabled)
			{
				return true;
			}
			try
			{
				Aircraft a = Acc.CF_aircraft(__instance);
				return Owned(a) == null;
			}
			catch
			{
				return true;
			}
		}

		public static void ReportText(Aircraft a, string s)
		{
			if ((UnityEngine.Object)(object)a != null && (UnityEngine.Object)(object)SceneSingleton<CombatHUD>.i?.aircraft == (UnityEngine.Object)(object)a)
			{
				SceneSingleton<AircraftActionsReport>.i?.ReportText(s, 4f);
			}
		}

		public static void Install(Harmony h, Action<string, Action> tryPatch)
		{
			StovlSystem.HardwareFactory = SwivelLink.TryCreate;
			StovlSystem.Report = ReportText;
			tryPatch("SwivelDuctSystem.FixedUpdate", delegate
			{
				h.Patch(AccessTools.Method(typeof(SwivelDuctSystem), "FixedUpdate"), new HarmonyMethod(typeof(StovlHooks), "SwivelPrefix"), new HarmonyMethod(typeof(StovlHooks), "SwivelPostfix"));
			});
			tryPatch("Turbojet.FixedUpdate (STOVL throttle)", delegate
			{
				HarmonyMethod tr = new HarmonyMethod(typeof(StovlHooks), "TurbojetThrottleTranspiler");
				h.Patch(AccessTools.Method(typeof(Turbojet), "FixedUpdate"), null, null, tr);
			});
			tryPatch("ControlsFilter.ToggleAutoHover", delegate
			{
				h.Patch(AccessTools.Method(typeof(ControlsFilter), "ToggleAutoHover"), new HarmonyMethod(typeof(StovlHooks), "ToggleAutoHoverPrefix"));
			});
			tryPatch("ControlsFilter.SetAutoHover", delegate
			{
				h.Patch(AccessTools.Method(typeof(ControlsFilter), "SetAutoHover"), new HarmonyMethod(typeof(StovlHooks), "SetAutoHoverPrefix"));
			});
		}
	}
}
