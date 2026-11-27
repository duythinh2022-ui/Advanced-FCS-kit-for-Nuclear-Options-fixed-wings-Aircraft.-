using System;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 2.4.6: positive-AoA lift shaping of the main wing panels and the fuselage. Lift only: the drag charts are Aryx's
	// (as before), the control surfaces (flaperons, stabs, rudders) keep their own curves, and negative AoA is untouched.
	//
	// Sign: the game reads a part's chart at a = atan2(v.y, v.z) of the part's own velocity in its lift-normal frame, so a
	// positive body AoA (flow from below) is a NEGATIVE chart angle (CL < 0 there, lift = -n x CL). Every change below is
	// made on chart angles a < 0 only, i.e. on the positive-AoA side, as a larger |CL|.
	//
	// Wing panels (WingFore / Wingmid / WingRear / Wingtip, both sides): Aryx's F22E_Wing curve (peak 35 deg, CL 1.10) up to
	// WingLiftRampStart (12; 2.4.6: 17), then a smooth increment (cubic, zero value and slope there) to a peak at WingLiftPeakAoA (36)
	// worth WingLiftPeakGain (1.239) x the old curve's lift at WingLiftRefAoA (30), with zero slope there (the peak), then a
	// smooth return to Aryx's curve by 60 deg, the same angle the 2.0.8 body-lift balance fades out (post-stall yaw).
	// 1.239 on the panels is what makes the whole main wing (panels + the unchanged flaperons at neutral) peak at 1.20 x
	// its old 30 deg lift. The game samples a chart every 2.8125 deg, so in game the peak lands on the 36.56 deg node.
	//
	// Fuselage (centre body, intakes, fore, cockpit, LERX, engine bays, tail booms, tail, rear, back): the existing
	// curves (with the 2.0.8 fore / aft balance) x a gain that rises smoothly from 1 at BodyLiftBoostStart (12; 2.4.6: 17) to
	// BodyLiftBoostGain (1.20) at BodyLiftBoostPeakAoA (42, the fuselage's lift peak), holds to 45 and fades back to 1 by 60.
	//
	// BodyLiftPitchNeutral (default): the fuselage's extra lift is split fore / aft (more on the engine bays, booms and tail)
	// so that the airframe's pitching moment is 2.4.5's at every AoA; total fuselage lift is unchanged by the split.
	//
	// The camber coupling (CamberForce, SurfaceEffector host foil) keeps the unshaped wing table, so the flaperons' control
	// power is unchanged.
	internal static class LiftShape
	{
		public const float FadeHold = 45f;

		public const float FadeEnd = 60f;

		public static bool WingOn => Plugin.WingLiftShaping;

		public static bool BodyOn => Plugin.BodyLiftBoost;

		public static bool PitchNeutral => Plugin.BodyLiftPitchNeutral;

		// Pitch-neutral split (BodyLiftPitchNeutral): the fuselage's extra lift is shared fore / aft so that, with the wing's
		// extra lift, the airframe's pitching moment at every AoA is 2.4.5's. Bench solve (stabs, nozzles, flaperons neutral,
		// slats on their schedule, default CG): the fore sections act ~4.08 m ahead of the CG, the aft ~3.22 m behind, Rear /
		// Back ~0.2 m behind. Per body AoA (deg, 12..60, 1 deg steps; 2.4.6: 17..60) the fore / aft lift gain - 1 is
		//   ForeW x w + ForeB x b, AftW x w + AftB x b
		// with w = the wing increment relative to the default (WingLiftPeakGain 1.239 at 36 / 30 deg) and b = the boost relative
		// to the default (BodyLiftBoostGain 1.20, shape 12 / 42 / 45 / 60). Both terms are linear (lift is linear in the gains
		// and the arms are constant to 1 %), so other gains scale exactly; another ramp / peak AoA is approximate. Rear / Back
		// keep the uniform BodyGain. Total fuselage lift is BodyGain x 2.4.5's at every AoA either way.
		private const float NeutralA0 = 12f;

		private static readonly float[] ForeW = { -0.0003f, -0.0007f, -0.0011f, -0.0026f, -0.0040f, -0.0055f, -0.0077f, -0.0097f, -0.0119f, -0.0141f, -0.0162f, -0.0184f, -0.0206f, -0.0226f, -0.0247f, -0.0268f, -0.0287f, -0.0306f, -0.0325f, -0.0343f, -0.0357f, -0.0372f, -0.0384f, -0.0393f, -0.0402f, -0.0406f, -0.0403f, -0.0400f, -0.0390f, -0.0376f, -0.0363f, -0.0344f, -0.0323f, -0.0303f, -0.0278f, -0.0253f, -0.0228f, -0.0202f, -0.0175f, -0.0149f, -0.0124f, -0.0099f, -0.0078f, -0.0058f, -0.0038f, -0.0025f, -0.0014f, -0.0003f, -0.0002f };

		private static readonly float[] AftW = { 0.0005f, 0.0010f, 0.0015f, 0.0036f, 0.0056f, 0.0077f, 0.0107f, 0.0136f, 0.0166f, 0.0197f, 0.0227f, 0.0257f, 0.0287f, 0.0316f, 0.0345f, 0.0374f, 0.0401f, 0.0428f, 0.0454f, 0.0479f, 0.0499f, 0.0519f, 0.0537f, 0.0550f, 0.0562f, 0.0567f, 0.0562f, 0.0558f, 0.0545f, 0.0526f, 0.0507f, 0.0480f, 0.0452f, 0.0423f, 0.0396f, 0.0367f, 0.0339f, 0.0313f, 0.0285f, 0.0256f, 0.0228f, 0.0195f, 0.0162f, 0.0129f, 0.0091f, 0.0063f, 0.0038f, 0.0009f, 0.0004f };

		private static readonly float[] ForeB = { 0.0000f, 0.0005f, 0.0020f, 0.0043f, 0.0074f, 0.0113f, 0.0159f, 0.0211f, 0.0268f, 0.0330f, 0.0396f, 0.0466f, 0.0538f, 0.0612f, 0.0687f, 0.0763f, 0.0839f, 0.0914f, 0.0988f, 0.1060f, 0.1129f, 0.1194f, 0.1256f, 0.1312f, 0.1363f, 0.1408f, 0.1446f, 0.1476f, 0.1498f, 0.1512f, 0.1516f, 0.1515f, 0.1514f, 0.1513f, 0.1482f, 0.1417f, 0.1321f, 0.1193f, 0.1052f, 0.0901f, 0.0747f, 0.0597f, 0.0457f, 0.0330f, 0.0218f, 0.0127f, 0.0059f, 0.0015f, 0.0000f };

		private static readonly float[] AftB = { 0.0000f, 0.0009f, 0.0034f, 0.0074f, 0.0129f, 0.0197f, 0.0276f, 0.0367f, 0.0466f, 0.0574f, 0.0689f, 0.0811f, 0.0936f, 0.1066f, 0.1198f, 0.1331f, 0.1464f, 0.1596f, 0.1726f, 0.1852f, 0.1974f, 0.2090f, 0.2199f, 0.2300f, 0.2391f, 0.2472f, 0.2541f, 0.2598f, 0.2640f, 0.2666f, 0.2677f, 0.2678f, 0.2679f, 0.2680f, 0.2675f, 0.2607f, 0.2491f, 0.2358f, 0.2182f, 0.1975f, 0.1745f, 0.1493f, 0.1221f, 0.0940f, 0.0669f, 0.0411f, 0.0198f, 0.0054f, 0.0000f };

		// Aryx's F22E_Wing CL at +30 / +36 deg body AoA, and the default wing increment at the peak
		private const float WingCl30 = 1.0531f;

		private const float WingCl36 = 1.0968f;

		private const float WingBpkDefault = 1.239f * WingCl30 - WingCl36;

		private static float WingScale()
		{
			if (!WingOn)
			{
				return 0f;
			}
			return (Plugin.WingLiftPeakGain * WingCl30 - WingCl36) / WingBpkDefault;
		}

		private static float BodyScale()
		{
			return BodyOn ? ((Plugin.BodyLiftBoostGain - 1f) / 0.2f) : 0f;
		}

		private static float Tab(float[] t, float x)
		{
			float f = x - NeutralA0;
			if (f <= 0f || f >= t.Length - 1)
			{
				return 0f;
			}
			int i = (int)f;
			return t[i] + (t[i + 1] - t[i]) * (f - i);
		}

		// lift gain of a fuselage family at positive body AoA x (deg)
		public static float BodyGainFor(StallStagger.Fam f, float x)
		{
			if (!PitchNeutral || f == StallStagger.Fam.BodyMid)
			{
				return BodyGain(x);
			}
			float w = WingScale();
			float b = BodyScale();
			if (f == StallStagger.Fam.BodyFore)
			{
				return 1f + Tab(ForeW, x) * w + Tab(ForeB, x) * b;
			}
			if (f == StallStagger.Fam.BodyAft)
			{
				return 1f + Tab(AftW, x) * w + Tab(AftB, x) * b;
			}
			return BodyGain(x);
		}

		public static bool IsWingPanel(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}
			string n = name.Replace("(Clone)", "").Trim().Replace("Aryx_KingRaptor", "");
			switch (n)
			{
			case "_WingFore_L":
			case "_WingFore_R":
			case "_Wingmid_L":
			case "_Wingmid_R":
			case "_WingRear_L":
			case "_WingRear_R":
			case "_Wingtip_L":
			case "_Wingtip_R":
				return true;
			default:
				return false;
			}
		}

		public static string WingKey()
		{
			return "w" + Plugin.WingLiftRampStart.ToString("F2") + "/" + Plugin.WingLiftPeakAoA.ToString("F2") + "/" + Plugin.WingLiftRefAoA.ToString("F2") + "/" + Plugin.WingLiftPeakGain.ToString("F3");
		}

		public static string BodyKey()
		{
			return (BodyOn ? ("b" + Plugin.BodyLiftBoostStart.ToString("F2") + "/" + Plugin.BodyLiftBoostPeakAoA.ToString("F2") + "/" + Plugin.BodyLiftBoostGain.ToString("F3")) : "b-") + (PitchNeutral ? ("|n" + WingScale().ToString("F3") + "/" + BodyScale().ToString("F3")) : "|n-");
		}

		private static float Smooth(float t)
		{
			t = Mathf.Clamp01(t);
			return t * t * (3f - 2f * t);
		}

		// fuselage lift gain at positive body AoA x (deg)
		public static float BodyGain(float x)
		{
			if (!BodyOn)
			{
				return 1f;
			}
			float g = Plugin.BodyLiftBoostGain - 1f;
			float a0 = Plugin.BodyLiftBoostStart;
			float pk = Mathf.Max(a0 + 1f, Plugin.BodyLiftBoostPeakAoA);
			float hold = Mathf.Max(pk, FadeHold);
			if (x <= a0 || x >= FadeEnd)
			{
				return 1f;
			}
			if (x <= pk)
			{
				return 1f + g * Smooth((x - a0) / (pk - a0));
			}
			if (x <= hold)
			{
				return 1f + g;
			}
			return 1f + g * (1f - Smooth((x - hold) / Mathf.Max(1f, FadeEnd - hold)));
		}

		private static float Herm(float x, float x0, float x1, float y0, float y1, float m0, float m1)
		{
			float h = x1 - x0;
			float t = (x - x0) / h;
			float t2 = t * t;
			float t3 = t2 * t;
			return (2f * t3 - 3f * t2 + 1f) * y0 + (t3 - 2f * t2 + t) * h * m0 + (-2f * t3 + 3f * t2) * y1 + (t3 - t2) * h * m1;
		}

		// 128-point wing-panel charts (the game's sampling); clOfRad / cdOfRad = Aryx's airfoil
		public static void BuildWing(Func<float, float> clOfRad, Func<float, float> cdOfRad, float[] cl, float[] cd)
		{
			// lift coefficient magnitude at positive body AoA x (deg)
			Func<float, float> clPos = x => 0f - clOfRad((0f - x) * Mathf.Deg2Rad);
			float a0 = Mathf.Clamp(Plugin.WingLiftRampStart, 0f, 40f);
			float pk = Mathf.Clamp(Plugin.WingLiftPeakAoA, a0 + 2f, FadeEnd - 5f);
			float bPk = Plugin.WingLiftPeakGain * clPos(Plugin.WingLiftRefAoA) - clPos(pk);
			// slope of the increment at the peak so that the shaped curve has zero slope there
			float mPk = 0f - (clPos(pk + 0.01f) - clPos(pk - 0.01f)) / 0.02f;
			for (int i = 0; i < StallStagger.ChartN; i++)
			{
				float a = (i - 64) * StallStagger.ChartStep;
				float c = clOfRad(a);
				if (a < 0f)
				{
					float x = (0f - a) * Mathf.Rad2Deg;
					float b = 0f;
					if (x > a0 && x < FadeEnd)
					{
						b = (x <= pk) ? Herm(x, a0, pk, 0f, bPk, 0f, mPk) : Herm(x, pk, FadeEnd, bPk, 0f, mPk, 0f);
					}
					c -= b;
				}
				cl[i] = c;
				cd[i] = cdOfRad(a);
			}
		}
	}
}
