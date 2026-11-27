using System;
using System.Collections.Generic;
using UnityEngine;

namespace J20B_Weilong.FCS
{
	// J-20B 0.2.9 (Tom 25 Nov 18:28: "overriding the 0.0.5 aero and cg changes, and just use the result we tuned for the previous ver"):
	// the J-20B mod 0.0.5 (AeroControl 0.68) rebuilt the damage colliders and, through MassProperties069.AfterComplexPhysics (a postfix on
	// the game's Aircraft setup), writes fixed rigidbody centres of mass on the root, cockpit, rear fuselage, both intakes and both engines.
	// It keeps the aircraft CG but spreads the mass differently (root 3.0 m and cockpit 0.5 m further aft, intakes ~5 m further forward,
	// engines ~2.5 m further aft than 0.0.4's): about +3 % pitch inertia. The aero is not affected (the mod pins every force point to a fixed
	// aircraft-frame marker, the same in both versions; lift areas, airfoils and surface specs are unchanged).
	//
	// This puts each of those parts' centre of mass back where 0.0.4 had it (0.0.4's automatic centre from its own colliders; each intake
	// also takes the side-bay part 0.0.5 folded into it). Then the FCS's CG move (CgAheadOfMains) and fuel balancing run on 0.0.4's mass
	// layout as before. Only when the mod has the 0.0.5 mass table (MassProperties069): on 0.0.4 nothing is touched.
	//
	// Values: part-local (every one of these parts is unrotated and unscaled under the root), from the 0.0.4 bundle's convex collider hulls
	// (the same method reproduces the 0.0.5 table's own "uncorrected" centres to 1-5 mm). Engines: 0.0.4 used one of the game's collider
	// meshes, not in the bundle; their z is the value that fits the mod's 0.0.4 baseline CG and the 0.77 / 0.64 m CG the FCS logged on 0.0.4
	// (aircraft z -3.85, +-0.1 m), x / y from the engine pivot and 0.0.5's collider.
	internal static class MassLayout004
	{
		public static bool ModHasTable;

		private static readonly Dictionary<string, Vector3> Local004 = new Dictionary<string, Vector3>
		{
			{ "J20NativeMinimum", new Vector3(0.0007f, 0.2253f, 3.0002f) },
			{ "cockpit", new Vector3(0.0022f, -0.3255f, -0.7674f) },
			{ "fuselage_R", new Vector3(-0.0004f, -0.0340f, -0.6690f) },
			{ "intake_L", new Vector3(0.0216f, 0.5724f, -5.0369f) },
			{ "intake_R", new Vector3(-0.0194f, 0.5741f, -5.0256f) },
			{ "engine_L", new Vector3(0f, 0.0063f, -0.1954f) },
			{ "engine_R", new Vector3(0f, 0.0063f, -0.1954f) }
		};

		private static readonly HashSet<int> logged = new HashSet<int>();

		// Harmony postfix on Reasonix.J20.AeroControl.MassProperties069.AfterComplexPhysics(Aircraft __instance): runs right after the mod
		// writes its centres, so ours win whatever order the FCS's own airframe setup runs in
		public static void AfterModPostfix(object[] __args)
		{
			try
			{
				if (__args != null && __args.Length > 0 && __args[0] is Aircraft a)
				{
					Restore(a);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("J-20B FCS 0.0.4 mass layout: " + ex.Message);
			}
		}

		public static void Restore(Aircraft a)
		{
			if (!ModHasTable || !Plugin.RestoreMassLayout004 || (UnityEngine.Object)(object)a == null || a.partLookup == null)
			{
				return;
			}
			int id = ((UnityEngine.Object)(object)a).GetInstanceID();
			bool log = logged.Add(id);
			List<string> done = log ? new List<string>() : null;
			foreach (UnitPart up in a.partLookup)
			{
				if (up == null || up.rb == null || !Local004.TryGetValue(up.gameObject.name, out Vector3 v))
				{
					continue;
				}
				Vector3 was = up.rb.centerOfMass;
				up.rb.centerOfMass = v;
				done?.Add($"{up.gameObject.name} z {was.z:+0.00;-0.00} -> {v.z:+0.00;-0.00}");
			}
			if (log && done.Count > 0)
			{
				Plugin.Log.LogInfo("J-20B FCS: J-20B mod 0.0.5 mass table overridden with 0.0.4's centres of mass (part-local, m): " + string.Join(", ", done.ToArray()) + ".");
			}
		}
	}
}
