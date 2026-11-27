using System;
using UnityEngine;

namespace J20B_Weilong.FCS
{
	// J-20B 0.1.8 (Tom 24 Nov 11:58, "do it": rework the vortex-lift scheduling): the inboard wing panels (wing1_L/R), the part of the
	// wing that sits in the LERX / canard vortex, keep building lift past the plain foil's knee. The mod's wing foil rises at ~0.054/deg
	// to ~15 deg and then goes nearly flat (CL 0.87 at 15, 0.90 at 20, 0.95 at 30, peak 1.0 at 44); with the chines and canards still
	// gaining lift ahead of the CG, that flat top is what flips the jet from ~22 % stable at 10 deg to ~30 % unstable at 22-34 deg.
	// Here the panel's normal force gets a leading-edge-vortex increment on top of its own curve (Polhamus form):
	//   dCN = Kv sin^2 a x on(a) x (1 - off(a)),  on: smoothstep Wing1VortexOnset .. Wing1VortexFull,  off: smoothstep over the
	//   breakdown window Wing1VortexBreakdownStart .. End;  CL += dCN cos a,  CD += dCN sin a.
	// Below the onset (the attached-flow range, all low-AoA stability and trim) the panel is unchanged. Reversed flow (|a| > 90): unchanged.
	internal static class WingVortex
	{
		public static string KeyPart()
		{
			return Plugin.Wing1VortexLift.ToString("F3") + "|" + Plugin.Wing1VortexOnset.ToString("F1") + "|" + Plugin.Wing1VortexFull.ToString("F1") + "|" + Plugin.Wing1VortexBreakdownStart.ToString("F1") + "|" + Plugin.Wing1VortexBreakdownEnd.ToString("F1");
		}

		private static float Smooth(float x)
		{
			x = Mathf.Clamp01(x);
			return x * x * (3f - 2f * x);
		}

		// the increment's normal-force coefficient at |a| (deg)
		public static float DCN(float aDeg)
		{
			if (aDeg >= 90f)
			{
				return 0f;
			}
			float on = Smooth((aDeg - Plugin.Wing1VortexOnset) / Mathf.Max(0.5f, Plugin.Wing1VortexFull - Plugin.Wing1VortexOnset));
			float off = Smooth((aDeg - Plugin.Wing1VortexBreakdownStart) / Mathf.Max(0.5f, Plugin.Wing1VortexBreakdownEnd - Plugin.Wing1VortexBreakdownStart));
			float s = Mathf.Sin(aDeg * Mathf.Deg2Rad);
			return Plugin.Wing1VortexLift * s * s * on * (1f - off);
		}

		public static void BuildCharts(Func<float, float> clOfRad, Func<float, float> cdOfRad, float[] cl, float[] cd)
		{
			for (int i = 0; i < StallStagger.ChartN; i++)
			{
				float a = (i - 64) * StallStagger.ChartStep;
				float ad = Mathf.Abs(a) * Mathf.Rad2Deg;
				float dn = DCN(ad);
				cl[i] = clOfRad(a) + Mathf.Sign(a) * dn * Mathf.Cos(Mathf.Abs(a));
				cd[i] = cdOfRad(a) + dn * Mathf.Sin(Mathf.Abs(a));
			}
		}
	}
}
