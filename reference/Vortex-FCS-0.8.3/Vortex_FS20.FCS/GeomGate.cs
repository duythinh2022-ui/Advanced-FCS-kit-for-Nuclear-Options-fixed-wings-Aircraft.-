using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// Vortex 0.7.0 (FS-20 Vortex Family support; Tom, 23 Nov 21:30: the FCS's airframe changes were tuned on the stock FS-20B, so on the
	// family's A / C / T / NA they apply only "where geometry matches B"). A part is the stock B's when its name is in the B table
	// (GeomGateData.cs, generated from the extracted SmallFighter1 asset) and its wing area, origin in the aircraft frame, centre of
	// lift and (for the slat-carrying wing sections) its high-lift area are the B's. The family's C widens the outer wing (areas and
	// positions change), the T / NA replace it with an integrated delta (areas, force points and visible elevons change), the C
	// stretches the fins; the A keeps the B's wing and fins. On those reworked parts the FCS leaves the game's own aerodynamics
	// alone (no stall stagger, wing / forebody vortex lift, camber coupling or hinge visuals) and models them as they are.
	// The stock B (jsonKey SmallFighter1) is never checked: every part counts as stock, exactly as before 0.7.0.
	internal static partial class GeomGate
	{
		internal const float PosTol = 0.05f;     // m

		internal const float AreaTol = 0.015f;   // fraction

		internal const float ColTol = 0.01f;     // m

		private static Dictionary<string, int> refIdx;

		private static Dictionary<string, Vector3> refVis;

		// per AeroPart / ControlSurface instance: stock or not (decided once per jet, at its airframe setup)
		private static readonly Dictionary<int, bool> partStock = new Dictionary<int, bool>();

		private static readonly HashSet<int> prepared = new HashSet<int>();

		private static readonly AccessTools.FieldRef<HighLiftDevice, float> HL_deployedArea = SafeRef<HighLiftDevice, float>("deployedArea");

		private static AccessTools.FieldRef<TO, TF> SafeRef<TO, TF>(string f)
		{
			try
			{
				return (AccessTools.Field(typeof(TO), f) != null) ? AccessTools.FieldRefAccess<TO, TF>(f) : null;
			}
			catch
			{
				return null;
			}
		}

		private static void BuildIndex()
		{
			if (refIdx != null)
			{
				return;
			}
			refIdx = new Dictionary<string, int>();
			for (int i = 0; i < Ref.Length; i++)
			{
				refIdx[Ref[i].name] = i;
			}
			refVis = new Dictionary<string, Vector3>();
			foreach (var v in RefVis)
			{
				refVis[v.name] = v.vis;
			}
		}

		private static string Clean(string n)
		{
			return (n ?? "").Replace("(Clone)", "").Trim();
		}

		// Decides every part of this jet once, before the FCS changes anything on it (Registry.EnsureSetup). Logged per jet.
		internal static void Prepare(Aircraft a)
		{
			if (a == null)
			{
				return;
			}
			int id = a.GetInstanceID();
			if (!prepared.Add(id))
			{
				return;
			}
			BuildIndex();
			bool stockJet = FamilyCompat.VariantOf(a) == FamilyCompat.Variant.B;
			Transform root = a.transform;
			var hldOf = new Dictionary<int, HighLiftDevice>();
			foreach (HighLiftDevice d in Acc.Collect<HighLiftDevice>(a))
			{
				AeroPart hp = Acc.HL_part(d);
				if (hp != null)
				{
					hldOf[hp.GetInstanceID()] = d;
				}
			}
			var kept = new List<string>();
			var changed = new List<string>();
			foreach (AeroPart ap in Acc.Collect<AeroPart>(a))
			{
				if (ap == null)
				{
					continue;
				}
				string why = null;
				bool ok = stockJet || IsStockPart(ap, root, hldOf, out why);
				partStock[ap.GetInstanceID()] = ok;
				if (!stockJet)
				{
					string n = Clean(ap.gameObject.name);
					if (refIdx.TryGetValue(n, out int ri))
					{
						if (ok)
						{
							kept.Add(n);
						}
						else
						{
							// what changed, for the bench model of this variant: area ratio to the B's and how far the part moved
							float ar = (Ref[ri].area > 0.01f) ? (Acc.AP_wingArea(ap) / Ref[ri].area) : 1f;
							float dp = (root.InverseTransformPoint(ap.transform.position) - Ref[ri].pos).magnitude;
							changed.Add($"{n} ({why}: area x{ar:F2}, moved {dp:F2} m)");
						}
					}
				}
			}
			foreach (ControlSurface cs in Acc.Collect<ControlSurface>(a))
			{
				if (cs == null)
				{
					continue;
				}
				bool ok = stockJet;
				if (!ok)
				{
					AeroPart ap = Acc.CS_attached(cs) as AeroPart;
					ok = ap != null && partStock.TryGetValue(ap.GetInstanceID(), out bool s) && s;
					GameObject vis = Acc.CS_visible(cs);
					if (ok && vis != null && refVis.TryGetValue(Clean(cs.gameObject.name), out Vector3 rv))
					{
						ok = (root.InverseTransformPoint(vis.transform.position) - rv).magnitude <= PosTol;
					}
				}
				partStock[cs.GetInstanceID()] = ok;
			}
			if (!stockJet)
			{
				Plugin.Log.LogInfo($"Vortex FCS ({FamilyCompat.VariantOf(a)}): B-tuned airframe changes kept on the {kept.Count} parts that match the stock FS-20B [{string.Join(", ", kept.ToArray())}]; left to the game's own aerodynamics on {changed.Count} reworked parts [{string.Join(", ", changed.ToArray())}].");
			}
		}

		private static bool IsStockPart(AeroPart ap, Transform root, Dictionary<int, HighLiftDevice> hldOf, out string why)
		{
			why = null;
			if (!refIdx.TryGetValue(Clean(ap.gameObject.name), out int i))
			{
				why = "not a B part";
				return false;
			}
			var r = Ref[i];
			if ((root.InverseTransformPoint(ap.transform.position) - r.pos).magnitude > PosTol)
			{
				why = "moved";
				return false;
			}
			if ((Acc.AP_centerOfLift(ap) - r.col).magnitude > ColTol)
			{
				why = "force point moved";
				return false;
			}
			if (hldOf.TryGetValue(ap.GetInstanceID(), out HighLiftDevice d))
			{
				// the wing area of a slat-carrying section moves with the slats (the FCS and the game both write it); its retracted
				// area and the slats' own increment are fixed
				float ret = Acc.HL_areaRet(d);
				if (ret > 0.01f && Mathf.Abs(ret - r.area) > AreaTol * Mathf.Max(0.1f, r.area))
				{
					why = "area";
					return false;
				}
				if (HL_deployedArea != null && r.hldDep > 0f && Mathf.Abs(HL_deployedArea(d) - r.hldDep) > AreaTol * r.hldDep)
				{
					why = "slat area";
					return false;
				}
				if (ret <= 0.01f && Mathf.Abs(Acc.AP_wingArea(ap) - r.area) > Mathf.Max(AreaTol * r.area, r.hldDep + 0.01f))
				{
					why = "area";
					return false;
				}
			}
			else if (Mathf.Abs(Acc.AP_wingArea(ap) - r.area) > AreaTol * Mathf.Max(0.1f, r.area))
			{
				why = "area";
				return false;
			}
			return true;
		}

		// true = the stock B's geometry (or the jet is the stock B, or it was never prepared: pre-0.7.0 behaviour)
		internal static bool Stock(Component c)
		{
			if (c == null)
			{
				return true;
			}
			return !partStock.TryGetValue(c.GetInstanceID(), out bool s) || s;
		}

		internal static void Forget(Aircraft a)
		{
			if (a != null)
			{
				prepared.Remove(a.GetInstanceID());
			}
		}
	}
}
