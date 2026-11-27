using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 2.0.8: wing/LERX downwash at the stabilators. Nuclear Option gives every AeroPart the free-stream flow, so a stab
	// at 0 deflection sees the body AoA 1:1 and is stalled at any AoA above its peak. Real tails sit in the wing's
	// downwash: the flow reaching them is turned down by eps = k * alpha (k = d eps / d alpha, 0.3-0.5 on fighters).
	// Apply turns a part's airflow velocity about the aircraft's pitch axis so its AoA in the aircraft's symmetry plane
	// drops from alpha to alpha - eps; sideslip and speed are kept. eps = k * alpha up to FadeStart (45), then fades
	// smoothly to 0 at FadeEnd (60): above that the stabs see the free stream as in 2.0.7. Bench: kept to 90 deg it cost
	// a fifth of the post-stall yaw rate at 65 deg AoA (falling leaf), because the differential stab then works on the
	// shoulder of its lift curve instead of deep in stall.
	// Shared by the game-side correction force (DownwashForce), the FCS's stab models and the bench.
	internal static class Downwash
	{
		// 4.6.0: settings (StabDownwashFadeStart / End; 45 / 60 before). With the wake-exit airframe the tail leaves the wing's
		// trailing vortex sheet as AoA rises (the sheet trails off above a coplanar tail), so the downwash fades out early.
		public static float FadeStart => Plugin.StabDownwashFadeStart;

		public static float FadeEnd => Mathf.Max(Plugin.StabDownwashFadeEnd, Plugin.StabDownwashFadeStart + 1f);

		public static float Eps(float alphaDeg)
		{
			float k = Plugin.StabDownwash;
			if (k <= 0f)
			{
				return 0f;
			}
			float x = Mathf.Abs(alphaDeg);
			if (x >= FadeEnd)
			{
				return 0f;
			}
			float w = 1f;
			if (x > FadeStart)
			{
				float t = (x - FadeStart) / (FadeEnd - FadeStart);
				w = 1f - t * t * (3f - 2f * t);
			}
			return k * alphaDeg * w;
		}

		private static readonly Dictionary<int, bool> stabCache = new Dictionary<int, bool>();

		// a stabilator AeroPart (cached: gameObject.name allocates)
		public static bool IsStab(AeroPart ap)
		{
			if (ap == null || Plugin.StabDownwash <= 0f)
			{
				return false;
			}
			int id = ap.GetInstanceID();
			if (!stabCache.TryGetValue(id, out bool r))
			{
				r = StallStagger.SurfaceFamilyOf(ap.gameObject.name) == StallStagger.Fam.Stab;
				stabCache[id] = r;
			}
			return r;
		}

		// 4.7.0 fin blanking (FcsController.FinBlank*): factor on a fin / rudder part's airfoil force at body AoA alphaDeg
		public static float FinBlankK(float alphaDeg)
		{
			float m = FcsController.FinBlankMin;
			if (m == 1f)
			{
				return 1f;
			}
			float t = Mathf.Clamp01((alphaDeg - FcsController.FinBlankAoA0) / Mathf.Max(1f, FcsController.FinBlankAoA1 - FcsController.FinBlankAoA0));
			t = t * t * (3f - 2f * t);
			return 1f + (m - 1f) * t;
		}

		public static float FinBlankFor(Vector3 vRef, Quaternion rootRot)
		{
			if (FcsController.FinBlankMin == 1f)
			{
				return 1f;
			}
			Vector3 b = Quaternion.Inverse(rootRot) * vRef;
			if (b.y * b.y + b.z * b.z < 1f)
			{
				return 1f;
			}
			return FinBlankK(Mathf.Atan2(0f - b.y, b.z) * 57.29578f);
		}

		// 4.7.0 stab-fin interference (FcsController.StabFinShield): factor on the fin's and rudder's airfoil force for its side's
		// stab deflection (deg, + trailing-edge up) at body AoA alphaDeg
		public static float ShieldK(float stabDefl, float alphaDeg)
		{
			float s = FcsController.StabFinShield;
			if (s == 0f)
			{
				return 1f;
			}
			float g = Mathf.Clamp01((alphaDeg - FcsController.StabFinShieldA0) / Mathf.Max(1f, FcsController.StabFinShieldA1 - FcsController.StabFinShieldA0));
			g = g * g * (3f - 2f * g);
			return Mathf.Clamp(1f + s * g * stabDefl / 25f, 0.1f, 1.6f);
		}

		public static float AlphaOf(Vector3 vRef, Quaternion rootRot)
		{
			Vector3 b = Quaternion.Inverse(rootRot) * vRef;
			if (b.y * b.y + b.z * b.z < 1f)
			{
				return 0f;
			}
			return Mathf.Atan2(0f - b.y, b.z) * 57.29578f;
		}

		public static bool IsFinName(string name)
		{
			return name != null && (name.Contains("Vstab") || name.Contains("Rudder"));
		}

		private static readonly Dictionary<int, bool> finCache = new Dictionary<int, bool>();

		public static bool IsFin(AeroPart ap)
		{
			if (ap == null)
			{
				return false;
			}
			int id = ap.GetInstanceID();
			if (!finCache.TryGetValue(id, out bool r))
			{
				r = IsFinName(ap.gameObject.name);
				finCache[id] = r;
			}
			return r;
		}

		public static bool IsStabName(string name)
		{
			return Plugin.StabDownwash > 0f && StallStagger.SurfaceFamilyOf(name) == StallStagger.Fam.Stab;
		}

		// eps (deg) for a reference airflow (the aircraft's own air velocity, world): the wing's AoA in the symmetry plane
		public static float EpsFor(Vector3 vRef, Quaternion rootRot)
		{
			if (Plugin.StabDownwash <= 0f)
			{
				return 0f;
			}
			Vector3 b = Quaternion.Inverse(rootRot) * vRef;
			if (b.y * b.y + b.z * b.z < 1f)
			{
				return 0f;
			}
			return Eps(Mathf.Atan2(0f - b.y, b.z) * 57.29578f);
		}

		// turns a part's airflow v (world) by eps (deg) about the aircraft's pitch axis: its AoA in the symmetry plane
		// drops by eps, sideslip and speed are kept. The stab's own pitch-rate AoA is kept (eps comes from the wing's
		// AoA, not the stab's), so the tail's pitch damping is not cut.
		public static Vector3 Turn(Vector3 v, Quaternion rootRot, float eps)
		{
			if (eps == 0f)
			{
				return v;
			}
			Vector3 b = Quaternion.Inverse(rootRot) * v;
			float r = Mathf.Sqrt(b.y * b.y + b.z * b.z);
			if (r < 1f)
			{
				return v;
			}
			float a2 = Mathf.Atan2(0f - b.y, b.z) - eps * (Mathf.PI / 180f);
			b.y = (0f - Mathf.Sin(a2)) * r;
			b.z = Mathf.Cos(a2) * r;
			return rootRot * b;
		}

		// 4.7.0: the flow at the outboard zone of a two-zone stab: no wing turning, StabOutUpwash x the reference AoA of upwash
		public static Vector3 ApplyOut(Vector3 v, Quaternion rootRot, Vector3 vRef)
		{
			float u = FcsController.StabOutUpwash;
			if (u == 0f)
			{
				return v;
			}
			Vector3 b = Quaternion.Inverse(rootRot) * vRef;
			if (b.y * b.y + b.z * b.z < 1f)
			{
				return v;
			}
			float a = Mathf.Atan2(0f - b.y, b.z) * 57.29578f;
			return Turn(v, rootRot, (0f - u) * Mathf.Clamp(a, -60f, 60f));
		}

		// the flow v turned by the downwash of a reference flow (for a flow that is its own reference, vRef = v)
		public static Vector3 Apply(Vector3 v, Quaternion rootRot, Vector3 vRef)
		{
			return Turn(v, rootRot, EpsFor(vRef, rootRot));
		}
	}
}
