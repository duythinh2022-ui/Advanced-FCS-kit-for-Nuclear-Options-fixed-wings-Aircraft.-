using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace J20B_Weilong.FCS
{
	// J-20B 0.1.0: bridge to Reasonix.J20.AeroControl (the J-20B Weilong mod's own surface system).
	// The J-20B has no stock ControlSurface components. Its canards, trailing-edge elevons, leading-edge flaps and all-moving fins
	// are hinge transforms that NativeAeroSystem.Step turns every physics tick (hinge.localRotation = rest x AngleAxis(angle, right),
	// +angle = leading edge down), with each surface's AeroPart lift normal a child of its hinge (identity local rotation, so the
	// hinge frame is the lift frame). Step also turns the inner wing panels' lift normals for its own flap camber, sets wing1's area
	// for the LE-flap high-lift, and AeroPart.UpdateJobFields (its postfix) moves each part's force point with its lift normal.
	//
	// While the FCS flies the jet it owns the canards, both elevon pairs, the fins and the camber normals: it drives them in its own
	// tick (ControlsFilter.Filter, before the aero schedule) and this bridge puts its poses back after the mod's Step (a Harmony
	// postfix on NativeAeroSystem.StepFromNative, which the mod calls from its AeroJobSettings.Schedule prefix for local jets and
	// from LateUpdate for remote ones). The LE flaps stay on the mod's own AoA / speed schedule. With the FCS off (disabled, not
	// allowed by the host, failed), the mod flies the surfaces as shipped.
	//
	// The mod is an optional dependency: its types are looked up once, by Assembly.GetType on its own assembly, and a miss is
	// cached (no lookups in any per-frame path, nothing retried).
	internal static class J20Bridge
	{
		public const string ModGuid = "reasonix.j20.aerocontrol";

		public const string AsmName = "Reasonix.J20.AeroControl";

		// 0.2.8: the J-20B mod from 0.0.5 on is one DLL (Eroge_J20B_Weilong_0.0.5.dll) whose plugin loads the AeroControl assembly
		// from an embedded resource and only then registers the old reasonix.j20.aerocontrol identity
		public const string SingleDllGuid = "reasonix.j20.single-dll";

		private static Harmony harmony;

		private static bool waitingForLoad;

		private static bool resolved;

		public static bool Present;

		private static Type tSys;

		private static FieldInfo fAircraft, fSurfaces, fCamberPairs, fWingCamber, fHighLift;

		private static FieldInfo fSpec, fOwner, fHinge, fNormal, fRest, fAngle, fFlightAngle, fTarget, fFlightTarget;

		private static FieldInfo fId, fRole, fSide, fMin, fMax, fServo, fPitchGain, fRollGain, fYawGain;

		private static FieldInfo fCamNormal, fCamRest, fCamAngle, fCamOwner;

		private static FieldInfo fHlOwner;

		// aircraft instance id -> the mod's NativeAeroSystem for it (learnt in the Step postfix)
		private static readonly Dictionary<int, object> systems = new Dictionary<int, object>();

		// wing1 area scaling: the area this bridge last wrote, per part (the mod rewrites the area from its spec every Step; if it has
		// not, the part still holds our value and is left alone, so the scale never compounds)
		private static readonly Dictionary<int, float> areaWritten = new Dictionary<int, float>();

		internal sealed class Surf
		{
			public object raw;

			public string id;

			public string role;

			public string side;

			public AeroPart owner;

			public Transform hinge;

			public Transform normal;

			public Quaternion rest;

			public float min;

			public float max;

			public float servo;

			public float pitchGain;

			public float rollGain;

			public float yawGain;

			public string PartName => (owner != null) ? owner.gameObject.name : id;
		}

		internal sealed class CamberNormal
		{
			public object raw;

			public AeroPart owner;

			public Transform normal;

			public Quaternion rest;
		}

		public static void Init(Harmony h)
		{
			if (resolved)
			{
				return;
			}
			resolved = true;
			harmony = h;
			try
			{
				Assembly asm = FindAsm();
				if (asm == null)
				{
					// 0.2.8: not loaded yet (a packaging that loads it after us): wait for it on the assembly-load event, once.
					// Event-driven, nothing polled in any frame path; if it never loads the FCS just stays idle.
					waitingForLoad = true;
					AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
					Plugin.Log.LogInfo("J-20B FCS: " + AsmName + " not loaded yet - will hook it when the J-20B mod loads it.");
					return;
				}
				Hook(asm);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("J-20B FCS: could not hook " + AsmName + ": " + ex);
			}
		}

		private static Assembly FindAsm()
		{
				Assembly asm = null;
				if (Chainloader.PluginInfos.TryGetValue(ModGuid, out var pi) && pi != null && pi.Instance != null)
				{
					asm = pi.Instance.GetType().Assembly;
					// 0.2.8: the 0.0.5 single DLL registers this identity itself; take it only if it really is the AeroControl assembly
					if (asm.GetName().Name != AsmName)
					{
						asm = null;
					}
				}
				if (asm == null)
				{
					foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
					{
						if (a.GetName().Name == AsmName)
						{
							asm = a;
							break;
						}
					}
				}
				return asm;
		}

		private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs e)
		{
			try
			{
				if (!waitingForLoad || e.LoadedAssembly == null || e.LoadedAssembly.GetName().Name != AsmName)
				{
					return;
				}
				waitingForLoad = false;
				AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
				Hook(e.LoadedAssembly);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("J-20B FCS: could not hook " + AsmName + " on load: " + ex);
			}
		}

		private static void Hook(Assembly asm)
		{
			Harmony h = harmony;
			try
			{
				tSys = asm.GetType("Reasonix.J20.AeroControl.NativeAeroSystem");
				Type tSurf = (tSys != null) ? tSys.GetNestedType("Surface", BindingFlags.Public | BindingFlags.NonPublic) : null;
				Type tCam = (tSys != null) ? tSys.GetNestedType("Camber", BindingFlags.Public | BindingFlags.NonPublic) : null;
				Type tHl = (tSys != null) ? tSys.GetNestedType("HighLift", BindingFlags.Public | BindingFlags.NonPublic) : null;
				Type tSpec = asm.GetType("Reasonix.J20.AeroControl.SurfaceSpec");
				if (tSys == null || tSurf == null || tSpec == null || tCam == null || tHl == null)
				{
					Plugin.Log.LogError("J-20B FCS: " + AsmName + " has an unexpected layout (NativeAeroSystem / Surface / SurfaceSpec not found) - the FCS stays idle.");
					return;
				}
				fAircraft = AccessTools.Field(tSys, "aircraft");
				fSurfaces = AccessTools.Field(tSys, "surfaces");
				fCamberPairs = AccessTools.Field(tSys, "camberPairs");
				fWingCamber = AccessTools.Field(tSys, "wingCamber");
				fHighLift = AccessTools.Field(tSys, "highLift");
				fSpec = AccessTools.Field(tSurf, "spec");
				fOwner = AccessTools.Field(tSurf, "owner");
				fHinge = AccessTools.Field(tSurf, "hinge");
				fNormal = AccessTools.Field(tSurf, "normal");
				fRest = AccessTools.Field(tSurf, "rest");
				fAngle = AccessTools.Field(tSurf, "angle");
				fFlightAngle = AccessTools.Field(tSurf, "flightAngle");
				fTarget = AccessTools.Field(tSurf, "target");
				fFlightTarget = AccessTools.Field(tSurf, "flightTarget");
				fId = AccessTools.Field(tSpec, "id");
				fRole = AccessTools.Field(tSpec, "role");
				fSide = tSpec.GetField("side", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);   // absent in 0.66.0: side from the id (canardL / trailingInnerR / tailL)
				fMin = AccessTools.Field(tSpec, "minDegrees");
				fMax = AccessTools.Field(tSpec, "maxDegrees");
				fServo = AccessTools.Field(tSpec, "servoDegreesPerSecond");
				fPitchGain = AccessTools.Field(tSpec, "pitchGain");
				fRollGain = AccessTools.Field(tSpec, "rollGain");
				fYawGain = AccessTools.Field(tSpec, "yawGain");
				fCamNormal = AccessTools.Field(tCam, "normal");
				fCamRest = AccessTools.Field(tCam, "rest");
				fCamAngle = AccessTools.Field(tCam, "angle");
				fCamOwner = AccessTools.Field(tCam, "owner");
				fHlOwner = AccessTools.Field(tHl, "owner");
				MethodInfo step = AccessTools.Method(tSys, "StepFromNative");
				if (fAircraft == null || fSurfaces == null || fSpec == null || fOwner == null || fHinge == null || fRest == null || fAngle == null || fId == null || fRole == null || step == null)
				{
					Plugin.Log.LogError("J-20B FCS: " + AsmName + " fields changed (aircraft / surfaces / hinge / rest / angle / StepFromNative) - the FCS stays idle.");
					return;
				}
				h.Patch(step, null, new HarmonyMethod(typeof(J20Bridge), "StepPostfix"));
				Present = true;
				// 0.2.9: the mod's 0.0.5 mass table (MassProperties069) - put 0.0.4's centres of mass back right after it writes its own
				try
				{
					Type tMass = asm.GetType("Reasonix.J20.AeroControl.MassProperties069");
					MethodInfo after = (tMass != null) ? AccessTools.Method(tMass, "AfterComplexPhysics") : null;
					if (after != null)
					{
						MassLayout004.ModHasTable = true;
						h.Patch(after, null, new HarmonyMethod(typeof(MassLayout004), "AfterModPostfix"));
						Plugin.Log.LogInfo("J-20B FCS: the J-20B mod's 0.0.5 mass table found; " + (Plugin.RestoreMassLayout004 ? "0.0.4's centres of mass are put back after it (RestoreMassLayout004)." : "left as the mod has it (RestoreMassLayout004 off)."));
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning("J-20B FCS: could not hook the J-20B mod's mass table: " + ex.Message);
				}
				Plugin.Log.LogInfo("J-20B FCS: hooked " + AsmName + " " + asm.GetName().Version + " (NativeAeroSystem.StepFromNative postfix).");
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("J-20B FCS: could not hook " + AsmName + ": " + ex);
			}
		}

		public static object SystemOf(Aircraft a)
		{
			if (!Present || (UnityEngine.Object)(object)a == null)
			{
				return null;
			}
			systems.TryGetValue(((UnityEngine.Object)(object)a).GetInstanceID(), out var s);
			return s;
		}

		public static List<Surf> Surfaces(Aircraft a)
		{
			List<Surf> list = new List<Surf>();
			object sys = SystemOf(a);
			if (sys == null || !(fSurfaces.GetValue(sys) is Array arr))
			{
				return list;
			}
			foreach (object o in arr)
			{
				if (o == null)
				{
					continue;
				}
				object spec = fSpec.GetValue(o);
				if (spec == null)
				{
					continue;
				}
				Surf s = new Surf
				{
					raw = o,
					id = fId.GetValue(spec) as string,
					role = fRole.GetValue(spec) as string,
					side = SideOf((fSide != null) ? (fSide.GetValue(spec) as string) : null, fId.GetValue(spec) as string),
					owner = fOwner.GetValue(o) as AeroPart,
					hinge = fHinge.GetValue(o) as Transform,
					normal = (fNormal != null) ? (fNormal.GetValue(o) as Transform) : null,
					rest = (Quaternion)fRest.GetValue(o),
					min = (fMin != null) ? Convert.ToSingle(fMin.GetValue(spec)) : -25f,
					max = (fMax != null) ? Convert.ToSingle(fMax.GetValue(spec)) : 25f,
					servo = (fServo != null) ? Convert.ToSingle(fServo.GetValue(spec)) : 100f,
					pitchGain = (fPitchGain != null) ? Convert.ToSingle(fPitchGain.GetValue(spec)) : 0f,
					rollGain = (fRollGain != null) ? Convert.ToSingle(fRollGain.GetValue(spec)) : 0f,
					yawGain = (fYawGain != null) ? Convert.ToSingle(fYawGain.GetValue(spec)) : 0f
				};
				if (s.owner != null && s.hinge != null)
				{
					list.Add(s);
				}
			}
			return list;
		}

		// J-20B 0.1.0 (first in-game run, 24 Nov 01:53): the mod's camberPairs holds Camber[] pairs, not Camber; its flat list is wingCamber.
		// Read wingCamber, else walk camberPairs one level down; one entry per lift normal.
		public static List<CamberNormal> CamberNormals(Aircraft a)
		{
			List<CamberNormal> list = new List<CamberNormal>();
			object sys = SystemOf(a);
			if (sys == null || fCamNormal == null || fCamRest == null)
			{
				return list;
			}
			object src = (fWingCamber != null) ? fWingCamber.GetValue(sys) : null;
			if (!(src is System.Collections.IEnumerable))
			{
				src = (fCamberPairs != null) ? fCamberPairs.GetValue(sys) : null;
			}
			if (!(src is System.Collections.IEnumerable en))
			{
				return list;
			}
			foreach (object o in en)
			{
				if (o == null)
				{
					continue;
				}
				if (o is System.Collections.IEnumerable inner && !(o is string))
				{
					foreach (object c in inner)
					{
						AddCamber(list, c);
					}
				}
				else
				{
					AddCamber(list, o);
				}
			}
			return list;
		}

		private static void AddCamber(List<CamberNormal> list, object o)
		{
			if (o == null || !fCamNormal.DeclaringType.IsInstanceOfType(o))
			{
				return;
			}
			Transform n = fCamNormal.GetValue(o) as Transform;
			if (n == null || list.Exists((CamberNormal x) => x.normal == n))
			{
				return;
			}
			list.Add(new CamberNormal
			{
				raw = o,
				normal = n,
				rest = (Quaternion)fCamRest.GetValue(o),
				owner = (fCamOwner != null) ? (fCamOwner.GetValue(o) as AeroPart) : null
			});
		}

		private static string SideOf(string side, string id)
		{
			if (!string.IsNullOrEmpty(side))
			{
				return side;
			}
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}
			char c = id[id.Length - 1];
			return (c == 'L' || c == 'l') ? "L" : ((c == 'R' || c == 'r') ? "R" : null);
		}

		public static List<AeroPart> HighLiftOwners(Aircraft a)
		{
			List<AeroPart> list = new List<AeroPart>();
			object sys = SystemOf(a);
			if (sys == null || fHighLift == null || fHlOwner == null)
			{
				return list;
			}
			object v = fHighLift.GetValue(sys);
			if (v is System.Collections.IEnumerable en)
			{
				foreach (object o in en)
				{
					if (o != null && fHlOwner.GetValue(o) is AeroPart p && !list.Contains(p))
					{
						list.Add(p);
					}
				}
			}
			return list;
		}

		// keep the mod's own surface state where the FCS put the hinge, so it carries on from there when the FCS lets go
		public static void SyncAngle(object raw, float angle)
		{
			if (raw == null)
			{
				return;
			}
			fAngle.SetValue(raw, angle);
			if (fFlightAngle != null)
			{
				fFlightAngle.SetValue(raw, angle);
			}
			if (fTarget != null)
			{
				fTarget.SetValue(raw, angle);
			}
			if (fFlightTarget != null)
			{
				fFlightTarget.SetValue(raw, angle);
			}
		}

		public static float AngleOf(object raw)
		{
			return (raw != null) ? ((float)fAngle.GetValue(raw)) : 0f;
		}

		public static void StepPostfix(object __instance)
		{
			try
			{
				if (__instance == null)
				{
					return;
				}
				Aircraft a = fAircraft.GetValue(__instance) as Aircraft;
				if ((UnityEngine.Object)(object)a == null)
				{
					return;
				}
				int id = ((UnityEngine.Object)(object)a).GetInstanceID();
				if (!systems.ContainsKey(id))
				{
					systems[id] = __instance;
					if (Registry.IsF22(a))
					{
						Registry.EnsureSetup(a);
					}
				}
				// airframe (every J-20B the setup ran on, FCS engaged or not): wing1's lift area x Wing1LiftScale on top of the mod's LE-flap
				// high-lift area, which it writes from its spec every Step
				if (Registry.SetupDone(a) && Mathf.Abs(Plugin.Wing1LiftScale - 1f) > 0.0001f)
				{
					foreach (AeroPart p in Registry.J20Wing1(a))
					{
						if (p == null)
						{
							continue;
						}
						int pid = p.GetInstanceID();
						float now = Acc.AP_wingArea(p);
						if (areaWritten.TryGetValue(pid, out float w) && Mathf.Abs(now - w) < 1E-05f)
						{
							continue;
						}
						float scaled = now * Plugin.Wing1LiftScale;
						Acc.AP_wingArea(p) = scaled;
						areaWritten[pid] = scaled;
					}
				}
				FcsController c = Registry.Find(a);
				if (c != null)
				{
					c.J20AfterModStep();
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("J-20B FCS bridge: " + ex.Message);
			}
		}

		public static void Forget(Aircraft a)
		{
			if ((UnityEngine.Object)(object)a != null)
			{
				systems.Remove(((UnityEngine.Object)(object)a).GetInstanceID());
			}
		}
	}
}
