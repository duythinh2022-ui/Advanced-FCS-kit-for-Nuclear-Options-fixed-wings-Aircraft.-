using System;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	internal static class AsymCap
	{
		// F-22E 4.2.0: the hump-tolerance band FindTop uses (Su-30 value; this jet's Find has no flat band of its own).
		public static float FlatBand = 0.15f;

		// F-22E 4.2.0 (CplTopEdge, from the Su-30 0.5.0): the cap from the coupled curve. On the Su-30 that curve is not monotonic: with a lateral CG offset
		// the weaker side's share of the roll range is lowest at 10-30 deg AoA (a hump), recovers to 40-50 deg (a plateau), then
		// collapses past 50-55 (the cliff). Find's first crossing from zero put the cap at 12 deg the moment the hump touched the
		// threshold (bench, 16 cm, nozzles locked, afterburner, 65 m/s: 51 -> 12 deg in one sweep, the nose pushed from 50 deg to 0
		// through the hump, to an AoA with no more roll toward the weak side than at 45 deg). A cap can only take the jet down, so:
		//   - the threshold is uMax, or the lowest demand anywhere between the floor and the limit plus FlatBand if that is higher
		//     (where the whole usable range is over uMax, cutting AoA buys nothing until the curve rises FlatBand above its best);
		//   - the cap is the lower edge of the stretch that is over that threshold all the way up to the limit (scanned down from
		//     the limit): a hump below a stretch that is under the threshold does not set it;
		//   - a plateau that is itself over the threshold, above a hump, is kept (the cap goes to the cliff above it, at the
		//     threshold + FlatBand) until it is more than FlatBand over; then the cap is the strict lower edge, under the hump, and it
		//     stays there until the plateau is back within half of FlatBand ('low', one latch per side: the one place the cap moves
		//     in a step, and it does so once).
		// A curve that rises with AoA (every curve of the Compass and the Ternion) gives the same cap as Find.
		public static float FindTop(float[] alphas, float[] u, int n, int side, float limit, float floor, float uMax, ref bool low)
		{
			float lim = Math.Abs(limit);
			if (n < 2)
			{
				low = false;
				return limit;
			}
			float flo = Math.Min(Math.Abs(floor), lim);
			float uRef = Math.Min(Interp(alphas, u, n, side * flo), Interp(alphas, u, n, side * lim));
			for (int i = 0; i < n; i++)
			{
				float ai = side * alphas[i];
				if (ai > flo && ai < lim)
				{
					uRef = Math.Min(uRef, u[i]);
				}
			}
			float thr = Math.Max(uMax, uRef + FlatBand);
			float capA = TopEdge(alphas, u, n, side, lim, flo, thr, flo);
			// the lowest point of the over-threshold stretch above capA, and whether a higher point (a hump) lies under it
			int kMin = -1;
			float uLow = float.MaxValue;
			bool hump = false;
			float uBelowMax = float.MinValue;
			for (int k = 0; k < n; k++)
			{
				int i = (side > 0) ? k : (n - 1 - k);
				float ai = side * alphas[i];
				if (ai <= capA || ai >= lim)
				{
					continue;
				}
				if (u[i] <= uLow)
				{
					uLow = u[i];
					kMin = i;
					hump = uBelowMax > u[i] + 0.02f;
				}
				uBelowMax = Math.Max(uBelowMax, u[i]);
			}
			if (kMin < 0 || !hump)
			{
				low = false;
				return side * capA;
			}
			low = low ? !(uLow < thr + 0.5f * FlatBand) : (uLow > thr + FlatBand);
			if (low)
			{
				return side * capA;
			}
			return side * TopEdge(alphas, u, n, side, lim, flo, Math.Max(thr + FlatBand, uLow), side * alphas[kMin]);
		}

		// the lower edge of the stretch over thr that reaches up to lim, scanned down from lim, not below aStop
		private static float TopEdge(float[] alphas, float[] u, int n, int side, float lim, float flo, float thr, float aStop)
		{
			float aHi = lim;
			float uHi = Interp(alphas, u, n, side * lim);
			if (uHi <= thr)
			{
				return lim;
			}
			for (int k = 0; k < n; k++)
			{
				int i = (side > 0) ? (n - 1 - k) : k;
				float ai = side * alphas[i];
				if (ai >= lim)
				{
					continue;
				}
				if (ai < aStop)
				{
					break;
				}
				if (u[i] <= thr)
				{
					return Math.Max(flo, Cross(ai, u[i], aHi, uHi, thr));
				}
				aHi = ai;
				uHi = u[i];
			}
			float uF = Interp(alphas, u, n, side * aStop);
			return (uF <= thr) ? Math.Max(flo, Cross(aStop, uF, aHi, uHi, thr)) : Math.Max(flo, aStop);
		}

		public static float Find(float[] alphas, float[] u, int n, int side, float limit, float floor, float uMax)
		{
			if (n < 2)
			{
				return limit;
			}
			float num = Math.Abs(limit);
			float val = Math.Min(Math.Abs(floor), num);
			float a = 0f;
			float u2 = Interp(alphas, u, n, 0f);
			if (side > 0)
			{
				for (int i = 0; i < n; i++)
				{
					float num2 = alphas[i];
					if (!(num2 <= 0f))
					{
						if (num2 > num)
						{
							num2 = num;
						}
						float num3 = ((num2 == alphas[i]) ? u[i] : Interp(alphas, u, n, num2));
						if (num3 > uMax)
						{
							return Math.Max(val, Cross(a, u2, num2, num3, uMax));
						}
						a = num2;
						u2 = num3;
						if (num2 >= num)
						{
							break;
						}
					}
				}
				return num;
			}
			for (int num4 = n - 1; num4 >= 0; num4--)
			{
				float num5 = 0f - alphas[num4];
				if (!(num5 <= 0f))
				{
					if (num5 > num)
					{
						num5 = num;
					}
					float num6 = ((num5 == 0f - alphas[num4]) ? u[num4] : Interp(alphas, u, n, 0f - num5));
					if (num6 > uMax)
					{
						return 0f - Math.Max(val, Cross(a, u2, num5, num6, uMax));
					}
					a = num5;
					u2 = num6;
					if (num5 >= num)
					{
						break;
					}
				}
			}
			return 0f - num;
		}

		private static float Cross(float a0, float u0, float a1, float u1, float thr)
		{
			if (u0 >= thr)
			{
				return a0;
			}
			float val = (thr - u0) / Math.Max(1E-06f, u1 - u0);
			return a0 + (a1 - a0) * Math.Min(1f, Math.Max(0f, val));
		}

		public static float Interp(float[] alphas, float[] u, int n, float a)
		{
			if (n == 0)
			{
				return 0f;
			}
			if (a <= alphas[0])
			{
				return u[0];
			}
			if (a >= alphas[n - 1])
			{
				return u[n - 1];
			}
			for (int i = 1; i < n; i++)
			{
				if (a <= alphas[i])
				{
					float num = (a - alphas[i - 1]) / (alphas[i] - alphas[i - 1]);
					return u[i - 1] + (u[i] - u[i - 1]) * num;
				}
			}
			return u[n - 1];
		}

		public static float Slew(float eff, float target, int side, float downRate, float upRate, float dt)
		{
			float num = (((target - eff) * (float)side > 0f) ? upRate : downRate) * dt;
			if (Math.Abs(target - eff) <= num)
			{
				return target;
			}
			return eff + (float)Math.Sign(target - eff) * num;
		}
	}
}
