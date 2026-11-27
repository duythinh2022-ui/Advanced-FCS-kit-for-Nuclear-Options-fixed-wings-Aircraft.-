using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ternion_FS3.FCS
{
	// Ternion 0.1.10: the right (left) tail stays on the jet when the engine on that side is destroyed.
	//
	// The game hangs every part off ONE parent: AttachInfo.parentPart, the part above it in the transform hierarchy (UnitPart.Awake).
	// Merged parts (simplePhysics) are transform children of that parent on the aircraft's rigidbody, and when a part detaches
	// (AeroPart.Detach: SetParent(null), own rigidbody) its transform children leave with it, and UnitPart_OnParentDetached flags each
	// of them detachedFromUnit - a flagged control surface is frozen by the game (ControlSurfaceJob_Math.Execute returns on IsDetached).
	// In the FS-3 Ternion 1.0.1 blueprint the wing root panel behind each engine (wing_1c_L / wing_1c_R) is a transform child of the
	// engine nacelle (engine_L_aero / engine_R_aero), and the elevator, fin and rudder on that side hang off wing_1c. So destroying an
	// engine took the elevator, fin and rudder with it: 9 Nov 07:46 and 09:11 logs, mass -3.5 t = engine 3007 + wing_1c 232 +
	// elevator 103 + fin 102 + rudder 51 kg, elevator and rudder frozen, the FCS's fin health 0.5 (the 30 deg fin-loss limit) and a
	// 7 deg aerodynamic-asymmetry AoA floor at 55-75 m/s (the -2 deg/s pitch-downs at neutral stick).
	// The blueprint itself joins wing_1c to BOTH the engine nacelle and the inboard panel wing_1b. Here a part whose attach parent is an
	// engine nacelle and which is also joined to a non-engine part that does not itself hang off that engine is re-homed onto that part
	// (transform parent and AttachInfo, through the game's own CreateAttachInfo), so it is carried by the airframe like its joints say.
	// The old parent's detach notification is ignored for re-homed parts (it is still subscribed; the game has no public way to remove it).
	internal static class StructureFix
	{
		internal static bool Enabled = true;

		private static readonly FieldInfo fAttach = AccessTools.Field(typeof(UnitPart), "attachInfo");

		private static readonly FieldInfo fParent = (fAttach != null) ? AccessTools.Field(fAttach.FieldType, "parentPart") : null;

		private static readonly FieldInfo fJoints = AccessTools.Field(typeof(AeroPart), "joints");

		private static readonly MethodInfo mCreate = AccessTools.Method(typeof(UnitPart), "CreateAttachInfo", new Type[1] { typeof(UnitPart) });

		private static readonly HashSet<UnitPart> rehomed = new HashSet<UnitPart>();

		internal static bool Ready => fAttach != null && fParent != null && fJoints != null && mCreate != null;

		private static UnitPart ParentOf(UnitPart u)
		{
			if (u == null)
			{
				return null;
			}
			object ai = fAttach.GetValue(u);
			return (ai != null) ? (fParent.GetValue(ai) as UnitPart) : null;
		}

		private static bool IsEngine(UnitPart u)
		{
			return u != null && u.gameObject.name.IndexOf("engine", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		// does u hang (through attach parents) off p?
		private static bool HangsOff(UnitPart u, UnitPart p)
		{
			UnitPart c = u;
			for (int i = 0; i < 32 && c != null; i++)
			{
				if (c == p)
				{
					return true;
				}
				c = ParentOf(c);
			}
			return false;
		}

		public static int Rehome(Aircraft ac)
		{
			if (!Enabled || ac == null || !Ready || !Plugin.EngineKeepsTail)
			{
				return 0;
			}
			int n = 0;
			foreach (AeroPart p in Acc.Collect<AeroPart>(ac))
			{
				try
				{
					if (p == null || p.IsDetached())
					{
						continue;
					}
					UnitPart par = ParentOf(p);
					if (par == null || !IsEngine(par) || IsEngine(p))
					{
						continue;
					}
					PartJoint[] js = fJoints.GetValue(p) as PartJoint[];
					if (js == null)
					{
						continue;
					}
					UnitPart np = null;
					foreach (PartJoint j in js)
					{
						UnitPart c = j?.connectedPart;
						if (c != null && c != par && c != p && !IsEngine(c) && !c.IsDetached() && !HangsOff(c, par) && !HangsOff(c, p))
						{
							np = c;
							break;
						}
					}
					if (np == null)
					{
						continue;
					}
					Transform t = p.transform;
					if (t.parent == par.transform)
					{
						t.SetParent(np.transform, worldPositionStays: true);
					}
					mCreate.Invoke(p, new object[1] { np });
					rehomed.Add(p);
					n++;
					Plugin.Log.LogInfo("Ternion FCS: " + p.gameObject.name + " re-homed from " + par.gameObject.name + " to " + np.gameObject.name + " (it is joined to both; an engine loss no longer takes it)");
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning("Ternion FCS: re-home of " + ((p != null) ? p.gameObject.name : "?") + " failed: " + ex.Message);
				}
			}
			rehomed.RemoveWhere((UnitPart u) => u == null);
			return n;
		}

		// UnitPart / AeroPart.UnitPart_OnParentDetached prefix: a re-homed part ignores the detach of a part that is no longer its parent
		public static bool OnParentDetachedPrefix(UnitPart __instance, UnitPart parentPart)
		{
			if (__instance == null || !rehomed.Contains(__instance))
			{
				return true;
			}
			UnitPart cur = ParentOf(__instance);
			return cur == null || cur == parentPart;
		}
	}
}
