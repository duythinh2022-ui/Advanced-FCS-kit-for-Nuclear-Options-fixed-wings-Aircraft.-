using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// Vortex 0.7.0: FS-20 Vortex Family (Jpgamer4045d, plugin id "fs20vortexfamily", 0.7.2 read) support. Tom, 23 Nov 21:30: the FCS
	// flies all five family jets (B, A, C, T, NA) and owns the jet while it is engaged: the family's own flight code steps aside for
	// that jet and comes back whenever the FCS lets go (disabled, ejected, AI with AIFlightControl off, simulated elsewhere, failed).
	// Everything else of the family (bays, hook, paint, vapor, NA sensors, multiplayer) is untouched.
	//
	// What the family runs on the flight controls, and what happens to it while the FCS flies the jet:
	// - ControlsFilter.Filter prefix (Fs20FlightController.PrepareEnvelope): A / C / T / NA pitch governor / load envelope that rewrites
	//   the stick, the FBW G limit and the FBW pitch bias. Skipped. The FCS's own prefix runs first (Priority.First, before the family)
	//   so it always reads the pilot's stick, whatever the plugin load order.
	// - ControlSurface.UpdateJobFields postfix (Fs20FlightController.ElevonJob): C / T / NA elevon remap (pitchRange 40, the family's
	//   own pitch / roll mix and flap). Skipped for the surfaces the FCS drives; the FCS's postfix runs last (Priority.Last, after the
	//   family) in any case, so it can never be reversed by load order (17 Nov finding).
	// - Aircraft.LocalSimFixedUpdate prefix (Fs20FlightController.Sample): C's per-tick TVC limit (20 deg below 250 m/s -> 0 by
	//   550 m/s) and the inputs of the elevon remap. Skipped.
	// - FlyByWire.Filter hooks (T load envelope gains, NA yaw allocator, the pitch-derivative transpiler): the stock fly-by-wire does not
	//   run while the FCS flies the jet (its Filter prefix returns false), so they are idle by construction.
	// The lookups run once, at the FCS's Start (the chainloader has loaded every plugin by then); a miss is cached and the family's
	// absence costs nothing (Tom's 19 Nov rule: no TypeByName, nothing in hot paths, the not-installed case thought through).
	internal static class FamilyCompat
	{
		internal enum Variant
		{
			B,
			A,
			C,
			T,
			NA,
			Other
		}

		internal const string FamilyGuid = "fs20vortexfamily";

		private const string Ns = "Japensou.NuclearOption.VortexConventional.";

		private static bool installTried;

		internal static bool FamilyPresent;

		private static readonly Dictionary<int, Variant> variants = new Dictionary<int, Variant>();

		internal static Variant VariantOf(Aircraft a)
		{
			if (a == null)
			{
				return Variant.Other;
			}
			int id = a.GetInstanceID();
			if (variants.TryGetValue(id, out Variant v))
			{
				return v;
			}
			string key = null;
			try
			{
				key = (a.definition != null) ? a.definition.jsonKey : null;
			}
			catch
			{
				key = null;
			}
			switch (key)
			{
			case "SmallFighter1":
				v = Variant.B;
				break;
			case "JP_FS20_A":
				v = Variant.A;
				break;
			case "JP_FS20_CTOL":
				v = Variant.C;
				break;
			case "JP_FS20_T":
				v = Variant.T;
				break;
			case "JP_FS20_NA":
				v = Variant.NA;
				break;
			default:
				// no definition key yet (or another FS-20 based jet): the stock prefab name means the B
				v = ((Component)(object)a).gameObject.name.StartsWith("SmallFighter1", StringComparison.Ordinal) ? Variant.B : Variant.Other;
				break;
			}
			variants[id] = v;
			return v;
		}

		// ---- Vortex 0.7.1: the two FCS plugins (Tom, 23 Nov 22:40). The FS-20 Vortex FCS flies every FS-20 Vortex; the FS-20 Vortex Family FCS
		// (the same source, FAMILY_FCS) flies only the family mod's A / C / T / NA. With both loaded:
		// - their family parts match (FcsBuildInfo.FamilyFingerprint, a hash of every source file that can act on a family jet, i.e. all of
		//   them except the version and the fingerprint files): the FS-20 Vortex FCS flies the family jets too and the Family FCS stands by;
		//   a FS-20 Vortex FCS that changed only what never runs on a family jet (files listed in family-fingerprint-exclude.txt) still matches;
		// - they differ: each flies its own jets - the FS-20 Vortex FCS the stock B, the Family FCS the family's A / C / T / NA;
		// - a FS-20 Vortex FCS without a fingerprint (0.7.0 or older, which flies every FS-20 regardless): the Family FCS stands by.
		// Decided once (both plugins have loaded by the first Start or flight), the same way on both sides, and logged by both.
		// 0.7.3: superseded by hand-over rule 2 (see Decide): the Family FCS flies the family jets whenever it is loaded.
#if FAMILY_FCS
		internal const bool IsFamilyBuild = true;

		private const string OtherGuid = "Vortex_FS20.FCS";

		internal const string OtherNs = "Vortex_FS20.FCS.";
#else
		internal const bool IsFamilyBuild = false;

		private const string OtherGuid = "Vortex_FS20.FCS.Family";

		internal const string OtherNs = "Vortex_FS20.FamilyFCS.";
#endif

		private static bool decided;

		// FS-20 Vortex FCS: it flies the family jets. Family FCS: it flies them.
		internal static bool OwnsFamilyJets = !IsFamilyBuild;

		// Vortex 0.7.3 (Tom, 23 Nov 23:21-23:24): the family jets fly on the Family FCS's own settings whenever it is loaded (hand-over rule 2):
		// with matching fingerprints the code is the same, so "the FS-20 Vortex FCS's code with the Family FCS's settings" is the Family FCS
		// flying them; its native core falls back to the FS-20 Vortex FCS's (FcsNative.ExtractFromOg). Each side reads the other's rule
		// (FcsBuildInfo.HandOverRule; none = 1 with a fingerprint, 0 without) and follows the older rule against an older plugin, so a
		// family jet is never flown by both or by neither:
		//                              FS-20 Vortex FCS rule 0 (<=0.7.0)   rule 1 (0.7.1-0.7.2)          rule 2 (0.7.3+)
		//   Family FCS rule 1           -                                   match: OG, else Family        match: OG, else Family
		//   Family FCS rule 2           OG (Family stands by)               match: OG, else Family        Family
		internal static void Decide()
		{
			if (decided)
			{
				return;
			}
			decided = true;
			try
			{
				string mine = FcsBuildInfo.FamilyFingerprint;
				if (!Chainloader.PluginInfos.TryGetValue(OtherGuid, out var other) || other == null || other.Instance == null)
				{
					OwnsFamilyJets = true;
					Plugin.Log.LogInfo(IsFamilyBuild ? "Vortex Family FCS: the FS-20 Vortex FCS is not loaded; this plugin flies the family's A / C / T / NA (the stock B stays with the game)." : "Vortex FCS: the FS-20 Vortex Family FCS is not loaded; this plugin flies every FS-20 Vortex.");
					return;
				}
				string theirs = null;
				int rule = 0;
				Type bi = other.Instance.GetType().Assembly.GetType(OtherNs + "FcsBuildInfo", false);
				if (bi != null)
				{
					FieldInfo fi = bi.GetField("FamilyFingerprint", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					theirs = (fi != null) ? (fi.GetValue(null) as string) : null;
					FieldInfo fr = bi.GetField("HandOverRule", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					rule = (fr != null && fr.GetValue(null) is int r) ? r : ((theirs != null) ? 1 : 0);
				}
				bool match = theirs != null && theirs == mine;
				string fp = "fingerprints " + (theirs == null ? "(theirs none)" : (match ? ("match, " + mine.Substring(0, 12)) : ("differ, theirs " + theirs.Substring(0, Math.Min(12, theirs.Length)) + ", this " + mine.Substring(0, 12))));
				string ver = other.Metadata.Version.ToString();
				if (IsFamilyBuild)
				{
					OwnsFamilyJets = rule >= 2 || (rule == 1 && !match);
					Plugin.Log.LogInfo("Vortex Family FCS: the FS-20 Vortex FCS " + ver + " is loaded (hand-over rule " + rule + ", " + fp + "); " + (OwnsFamilyJets ? "this plugin flies the family's A / C / T / NA with its own settings, the FS-20 Vortex FCS the stock B." : ((rule == 0) ? "it predates the hand-over (0.7.1) and flies every FS-20 Vortex, so this plugin stands by. Update the FS-20 Vortex FCS." : "it flies the family jets with its own settings (its 0.7.1-0.7.2 rule); this plugin stands by. Update the FS-20 Vortex FCS to 0.7.3+ for the Family FCS's settings on the family jets.")));
				}
				else
				{
					OwnsFamilyJets = rule == 0 || (rule == 1 && match);
					Plugin.Log.LogInfo("Vortex FCS: the FS-20 Vortex Family FCS " + ver + " is loaded (hand-over rule " + rule + ", " + fp + "); " + (OwnsFamilyJets ? "this plugin flies every FS-20 Vortex (the Family FCS's 0.7.1-0.7.2 rule), the Family FCS stands by. Update it to 0.7.3+ for its own settings on the family jets." : "this plugin flies the stock B, the Family FCS the family's A / C / T / NA with its own settings."));
				}
			}
			catch (Exception ex)
			{
				OwnsFamilyJets = !IsFamilyBuild;
				Plugin.Log.LogWarning("Vortex FCS: FCS hand-over check failed (" + ex.Message + "); " + (IsFamilyBuild ? "this plugin stands by." : "this plugin flies every FS-20 Vortex."));
			}
		}

		// whether this plugin flies this jet (Registry.IsF22)
		internal static bool Responsible(Aircraft a)
		{
			Decide();
			Variant v = VariantOf(a);
			bool family = v == Variant.A || v == Variant.C || v == Variant.T || v == Variant.NA;
			if (IsFamilyBuild)
			{
				return family && OwnsFamilyJets;
			}
			return !family || OwnsFamilyJets;
		}

		// Vortex 0.7.1: per-jet envelope (see FcsController.jetGBase). The FS-20 Vortex Family jets are built for 11 G, the family's own
		// fly-by-wire keeping the load (11 G x reference mass) above a reference mass: A 13.5 t (Fs20AEnergyEnvelope), C 12 t
		// (Fs20FlightEnvelope), T and NA 16 t (Fs20TStructure, whose wing joints carry 11 G x 1.35 at 16 t). MPO 14.5 G on the same budget.
		// The NA's AoA limits are its own (NaAoAPos / NaAoANeg, bench: see the 0.7.1 notes).
		internal static float FamilyG = 11f;

		internal static float FamilyMpoG = 14.5f;

		internal static float NaAoAPos = 70f;

		internal static float NaAoANeg = 55f;

		internal static void JetLimits(Aircraft a, out float aoaPos, out float aoaNeg, out float g, out float mpoG, out float refMass)
		{
			aoaPos = 0f;
			aoaNeg = 0f;
			g = 0f;
			mpoG = 0f;
			refMass = 0f;
			switch (VariantOf(a))
			{
			case Variant.A:
				refMass = 13500f;
				break;
			case Variant.C:
				refMass = 12000f;
				break;
			case Variant.T:
				refMass = 16000f;
				break;
			case Variant.NA:
				refMass = 16000f;
				aoaPos = NaAoAPos;
				aoaNeg = NaAoANeg;
				break;
			default:
				return;
			}
			g = FamilyG;
			mpoG = FamilyMpoG;
		}

		internal static void Install(Harmony h)
		{
			if (installTried)
			{
				return;
			}
			installTried = true;
			Decide();
			try
			{
				if (!Chainloader.PluginInfos.TryGetValue(FamilyGuid, out var info) || info == null || info.Instance == null)
				{
					Plugin.Log.LogInfo("Vortex FCS: FS-20 Vortex Family not installed; nothing to coordinate with.");
					return;
				}
				FamilyPresent = true;
				Assembly asm = info.Instance.GetType().Assembly;
				int n = 0;
				n += Gate(h, asm, "Fs20FlightController+PrepareEnvelope", "Prefix", nameof(EnvelopeGate));
				n += Gate(h, asm, "Fs20FlightController+ElevonJob", "Postfix", nameof(ElevonGate));
				n += Gate(h, asm, "Fs20FlightController", "Sample", nameof(SampleGate));
				Plugin.Log.LogInfo($"Vortex FCS: FS-20 Vortex Family {info.Metadata.Version} found; {n}/3 of its flight-control hooks step aside on jets the FCS is flying (B / A / C / T / NA).");
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Vortex FCS: FS-20 Vortex Family coordination not installed: " + ex.Message);
			}
		}

		private static int Gate(Harmony h, Assembly asm, string type, string method, string gate)
		{
			Type t = asm.GetType(Ns + type, false);
			MethodInfo m = (t != null) ? AccessTools.Method(t, method) : null;
			if (m == null)
			{
				Plugin.Log.LogWarning("Vortex FCS: FS-20 Vortex Family " + type + "." + method + " not found (family version changed?); that hook is not coordinated.");
				return 0;
			}
			h.Patch(m, new HarmonyMethod(typeof(FamilyCompat), gate) { priority = Priority.First });
			return 1;
		}

		internal static bool FcsFlying(Aircraft a)
		{
			FcsController c = Registry.Find(a);
			return c != null && c.Engaged && !c.Failed;
		}

		// family: static void Prefix(ControlsFilter __instance, ControlInputs __0, Vector3 __1, Rigidbody __2, bool __4, Aircraft ___aircraft, ...)
		// (argument 5 is the aircraft)
		public static bool EnvelopeGate(Aircraft __5)
		{
			try
			{
				return !FcsFlying(__5);
			}
			catch
			{
				return true;
			}
		}

		// family: static void Postfix(ControlSurface __instance, ref PtrAllocation<ControlSurfaceFields> ___JobFields) (argument 0)
		public static bool ElevonGate(ControlSurface __0)
		{
			try
			{
				if (__0 == null || !Registry.TrySurface(__0, out var s, out var c))
				{
					return true;
				}
				return !(c.Engaged && !c.Failed && s.active);
			}
			catch
			{
				return true;
			}
		}

		// family: internal void Fs20FlightController.Sample() (a MonoBehaviour on the aircraft)
		public static bool SampleGate(Component __instance)
		{
			try
			{
				return __instance == null || !FcsFlying(__instance.GetComponent<Aircraft>());
			}
			catch
			{
				return true;
			}
		}

		// A jet whose design has no rudders (the NA: fins removed, outer elevons split as drag rudders, engine yaw vectoring): the FCS
		// keeps the engine's yaw vectoring and the split limit the airframe was built with (on the B these are its own design choices:
		// pitch-only engine TVC, the swivel nozzle's bearing being the yaw nozzle, no split surfaces).
		internal static bool Finless(Aircraft a)
		{
			if (a == null)
			{
				return false;
			}
			foreach (ControlSurface cs in Acc.Collect<ControlSurface>(a))
			{
				if (cs != null && cs.gameObject.name.ToLowerInvariant().StartsWith("rudder"))
				{
					return false;
				}
			}
			foreach (AeroPart ap in Acc.Collect<AeroPart>(a))
			{
				if (ap != null && ap.gameObject.name.ToLowerInvariant().StartsWith("vstab"))
				{
					return false;
				}
			}
			return true;
		}
	}
}
