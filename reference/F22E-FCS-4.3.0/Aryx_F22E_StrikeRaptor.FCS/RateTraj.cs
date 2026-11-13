using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	internal struct RateTraj
	{
		public float x;

		public float v;

		public void Reset(float x0)
		{
			x = x0;
			v = 0f;
		}

		public void Step(float target, float A, float J, float dt)
		{
			Step(target, A, J, J, dt);
		}

		public void Step(float target, float A, float J, float Jend, float dt)
		{
			Step(target, A, J, Jend, dt, 0f);
		}

		// 3.1.8: vT = the target's own rate of change. The capture lands on a moving target with the target's slope instead of with zero
		// (vT = 0 is the stationary capture, bit for bit)
		public void Step(float target, float A, float J, float Jend, float dt, float vT)
		{
			if (vT != 0f)
			{
				StepMoving(target, A, J, Jend, dt, vT);
				return;
			}
			float num = target - x;
			if (num * v < 0f)
			{
				v = 0f;
			}
			float num2 = J * dt;
			float num4 = Jend * dt;
			float target2 = Mathf.Sign(num) * Mathf.Min(A, Mathf.Sqrt(0.25f * num4 * num4 + 2f * Jend * Mathf.Abs(num)) - 0.5f * num4);
			v = Mathf.MoveTowards(v, target2, J * dt);
			float num3 = x + v * dt;
			if ((target - num3) * num <= 0f)
			{
				num3 = target;
				v = 0f;
			}
			x = num3;
		}
	
		private void StepMoving(float target, float A, float J, float Jend, float dt, float vT)
		{
			vT = Mathf.Clamp(vT, 0f - A, A);
			float num = target - x;
			float vRel = v - vT;
			if (num * vRel < 0f)
			{
				// moving away from the target relative to it: drop the relative part (as the stationary capture drops v)
				v = vT;
				vRel = 0f;
			}
			float num4 = Jend * dt;
			float rel = Mathf.Sign(num) * (Mathf.Sqrt(0.25f * num4 * num4 + 2f * Jend * Mathf.Abs(num)) - 0.5f * num4);
			float target2 = Mathf.Clamp(vT + rel, 0f - A, A);
			v = Mathf.MoveTowards(v, target2, J * dt);
			float num3 = x + v * dt;
			float tNext = target + vT * dt;
			if ((tNext - num3) * num <= 0f)
			{
				num3 = tNext;
				v = vT;
			}
			x = num3;
		}
	}
}
