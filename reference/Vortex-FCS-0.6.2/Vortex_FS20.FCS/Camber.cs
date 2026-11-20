using UnityEngine;

namespace Vortex_FS20.FCS
{
	// 2.2.0: flap-camber coupling. Nuclear Option flies each control surface as its own plate: a deflected rudder changes
	// only its own lift, and the fixed fin ahead of it does not see the camber. On a real flapped section the deflection
	// cambers the whole fin + rudder (or wing + flaperon) section, so the fixed part carries a share of the extra lift
	// (thin-airfoil flap effectiveness ~0.75 for a half-chord rudder, ~0.55 for the ~0.35-chord flaperons). Modelled as
	// the host part's airfoil force at its local AoA plus K x deflection (about the control surface's hinge axis), minus
	// its force at its local AoA - zero with the surface centred, so the trimmed centre of lift does not move.
	// Shared by the game-side CamberForce, the FCS surface models and the bench.
	internal static class Camber
	{
		// Vortex 0.3.1 (ElevonFlapModel, Tom 17 Nov: "the elevons should act as flaps on the wing"). Strip theory from the part
		// geometry (collider spans, panel areas): the inboard elevon (span ~2.0-3.2 m, chord 0.85) sits behind wing2b, wing2a and
		// the outboard end of wing1a/wing1b, ~4.0 m^2 of wing ahead of it; the outboard one (3.2-4.6 m, chord 0.76) behind
		// wing2b, wing2a and the wingtip, ~3.9 m^2. Chord fraction ~0.21 for both, thin-airfoil flap effectiveness
		// tau = 1 - (theta - sin theta) / pi = 0.56. The elevon plates carry wingArea 2.0 against ~1.05 m^2 of geometry (the
		// game's own allowance for the section effect), so the camber term supplies the rest of tau x (strip + plate):
		// K x (2.7 m^2 x strip factor) = 0.56 x 5.05 - 2.0 -> K 0.21 with the host area scaled by 1.48 (inboard) / 1.44 (outboard). The camber increment follows the host wing's own curve (the wing stalls, the flap
		// goes with it) instead of the elevon plate's staggered one.
		public static bool FlapModel => Plugin.ElevonFlapModel;

		public static bool IsElevon(string surfaceName)
		{
			string n = (surfaceName ?? "").Replace("Aryx_KingRaptor_", "");
			return n.StartsWith("elevator_") || n.StartsWith("aileron_");
		}

		// host lift-area factor for a surface's camber term (1 for the rudders / without the flap model)
		public static float HostAreaK(string surfaceName)
		{
			if (!FlapModel)
			{
				return 1f;
			}
			string n = (surfaceName ?? "").Replace("Aryx_KingRaptor_", "");
			if (n.StartsWith("elevator_"))
			{
				return Plugin.InnerStripArea;
			}
			if (n.StartsWith("aileron_"))
			{
				return Plugin.OuterStripArea;
			}
			return 1f;
		}

		public static bool TaperFor(string surfaceName)
		{
			return FlapModel && IsElevon(surfaceName);
		}

		// plain-flap effective deflection: the camber a deflection makes grows linearly to 10 deg, then at 1 - 0.02 (d - 10) per
		// degree to 30 deg (0.8 at 20, 0.6 at 30, DATCOM-style plain-flap effectiveness), then at 0.6: 30 deg -> 26 effective
		public static float Teff(float delta, bool taper)
		{
			if (!taper)
			{
				return delta;
			}
			float x = Mathf.Abs(delta);
			float e;
			if (x <= 10f)
			{
				e = x;
			}
			else if (x <= 30f)
			{
				float u = x - 10f;
				e = 10f + u - 0.01f * u * u;
			}
			else
			{
				e = 26f + 0.6f * (x - 30f);
			}
			return (delta < 0f) ? (0f - e) : e;
		}

		public static string HostOf(string surfaceName)
		{
			string n = surfaceName.Replace("Aryx_KingRaptor_", "");
			// Vortex 0.1.0: both FS-20 elevons (elevator_X inboard, aileron_X outboard) are children of wing2b_X and sit on its
			// trailing edge; the rudders are children of vstab_X (the canted fin ahead of them).
			if (n.StartsWith("elevator_") || n.StartsWith("aileron_"))
			{
				return "wing2b_" + n.Substring(n.IndexOf('_') + 1);
			}
			if (n.StartsWith("rudder_"))
			{
				return "vstab_" + n.Substring(7);
			}
			return null;
		}

		public static float K(SurfaceKind kind)
		{
			switch (kind)
			{
			case SurfaceKind.Rudder:
				return Plugin.FinCamberK;
			case SurfaceKind.Stabilator:
				return FlapModel ? Plugin.ElevonFlapK : Plugin.FlapCamberK;
			case SurfaceKind.Flaperon:
				return FlapModel ? Plugin.ElevonFlapK : Plugin.FlapCamberK;
			case SurfaceKind.Aileron:
				return FlapModel ? Plugin.ElevonFlapK : Plugin.AileronCamberK;
			default:
				return 0f;
			}
		}

		public static float KForName(string surfaceName)
		{
			string n = surfaceName.Replace("Aryx_KingRaptor_", "");
			if (n.StartsWith("elevator_"))
			{
				return FlapModel ? Plugin.ElevonFlapK : Plugin.FlapCamberK;
			}
			if (n.StartsWith("aileron_"))
			{
				return FlapModel ? Plugin.ElevonFlapK : Plugin.AileronCamberK;
			}
			if (n.StartsWith("rudder_"))
			{
				return Plugin.FinCamberK;
			}
			return 0f;
		}

		// lift + airfoil drag of a part with lift-normal rotation rot in flow v (qS = 0.5 rho v^2 S eff)
		public static Vector3 Foil(Vector3 v, Quaternion rot, AirfoilTable t, float qS)
		{
			Vector3 vl = Quaternion.Inverse(rot) * v;
			t.Get(Mathf.Atan2(vl.y, vl.z), out var cl, out var cd);
			Vector3 n = Vector3.Cross(v, rot * Vector3.right);
			float m = n.magnitude;
			if (m < 1E-06f || v.sqrMagnitude < 1E-06f)
			{
				return Vector3.zero;
			}
			return -n / m * (cl * qS) - v.normalized * (cd * qS);
		}

		// F-22E 4.0.1: Foil with the rotation's terms (Quaternion.Inverse(rot), rot * Vector3.right) and the flow's terms
		// (v.normalized, v.sqrMagnitude < 1E-06f) worked out by the caller, for callers that evaluate many rotations in one flow
		// and the same rotations in several flows (SurfaceEffector.EvalFlowGrid). Same expressions in the same order as Foil.
		public static Vector3 FoilPre(Vector3 v, Quaternion rotInv, Vector3 rotRight, AirfoilTable t, float qS, Vector3 vN, bool vSmall)
		{
			Vector3 vl = rotInv * v;
			t.Get(Mathf.Atan2(vl.y, vl.z), out var cl, out var cd);
			Vector3 n = Vector3.Cross(v, rotRight);
			float m = n.magnitude;
			if (m < 1E-06f || vSmall)
			{
				return Vector3.zero;
			}
			return -n / m * (cl * qS) - vN * (cd * qS);
		}

		// host force increment for a control-surface deflection delta (deg) about the world hinge axis
		public static Vector3 Delta(Vector3 v, Quaternion hostRot, Vector3 hingeAxis, float k, float delta, AirfoilTable t, float qS)
		{
			if (k <= 0f || Mathf.Abs(delta) < 0.01f)
			{
				return Vector3.zero;
			}
			Quaternion r1 = Quaternion.AngleAxis(k * delta, hingeAxis) * hostRot;
			return Foil(v, r1, t, qS) - Foil(v, hostRot, t, qS);
		}

		// F-22E 3.1.3: Delta with its undeflected half (Foil(v, hostRot, t, qS)) passed in, for callers that evaluate several
		// deflections in one flow; the caller has applied Delta's zero test (k <= 0 or |delta| < 0.01)
		public static Vector3 DeltaFrom(Vector3 baseF, Vector3 v, Quaternion hostRot, Vector3 hingeAxis, float k, float delta, AirfoilTable t, float qS)
		{
			Quaternion r1 = Quaternion.AngleAxis(k * delta, hingeAxis) * hostRot;
			return Foil(v, r1, t, qS) - baseF;
		}

		// a control surface's current deflection about its hinge (deg), from the visible mesh vs its resting rotation
		public static float DeflectionOf(Transform visible, Quaternion resting)
		{
			Quaternion d = Quaternion.Inverse(resting) * visible.localRotation;
			float a = 2f * Mathf.Atan2(d.x, d.w) * 57.29578f;
			if (a > 180f)
			{
				a -= 360f;
			}
			else if (a < -180f)
			{
				a += 360f;
			}
			return a;
		}
	}
}
