using System.Collections.Generic;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	internal sealed class TvcEffector : Effector
	{
		public readonly List<Turbojet> engines = new List<Turbojet>();

		public float totalThrust;

		public float speedFade = 1f;

		public static float CutoffFadeBand = 0.05f;

		public float NozzleLimit => max;

		// Revoker 0.1.0: the yaw angle is its own effector (TvcYawEffector); the pitch grid is then taken at yaw 0
		public bool yawSeparate;

		// shared asymmetric-load code (from the Su-30 0.5.0, CplTvc): each engine's nozzle as one effector of the coupled check's
		// LP. This jet's nozzles move together on one grid, so the whole table is one effector (Mn[0] = M); MnNow is its deflection.
		public Vector3[][] Mn;

		public bool[] MnOn;

		// (the Su-30's nozzles locked off-centre; never on this jet)
		public bool fixedNozzles;

		// each Mn table carries all of its nozzle's axes (the Su-30's canted nozzles): the differential / yaw nozzles are not added
		// as separate effectors. Here the one table is the pitch grid only.
		public bool MnAllAxes => false;

		public float MnNow(int e)
		{
			return now;
		}

		public bool SharedTablesOn()
		{
			SharedTables();
			return true;
		}

		public void SharedTables()
		{
			if (Mn == null || Mn.Length != 1)
			{
				Mn = new Vector3[1][];
				MnOn = new bool[1];
			}
			Mn[0] = M;
			MnOn[0] = active;
		}

		public TvcEffector(IEnumerable<Turbojet> fans)
		{
			name = "TVC";
			engines.AddRange(fans);
			float num = 10f;
			foreach (Turbojet engine in engines)
			{
				num = Mathf.Max(0.1f, Mathf.Abs(Acc.TF_vec(engine).x));
			}
			min = 0f - num;
			max = num;
			lo = min;
			hi = max;
			regNorm = 10f;
			mu = 5E-05f;
			lam = 2E-05f;
			AllocGrid(11);
			for (int i = 0; i < N; i++)
			{
				grid[i] = min + (float)i * (max - min) / (float)(N - 1);
			}
		}

		public override void Evaluate(ref EvalContext ctx)
		{
			active = false;
			for (int i = 0; i < N; i++)
			{
				M[i] = Vector3.zero;
			}
			Mnow = Vector3.zero;
			totalThrust = 0f;
			bool flag = false;
			float num = 0f;
			int num2 = 0;
			speedFade = 1f;
			if (Plugin.DisableTVC)
			{
				// 2.0.8 debug: nozzles locked at 0, not an effector. 2.0.12: the thrust is still summed, so the FCS's T/W
				// (envelope thrust term, pitch-rate limit, low-thrust caps) sees the engines as they are (2.0.8-2.0.11 read 0)
				foreach (Turbojet engine0 in engines)
				{
					if (engine0 == null || !Acc.TF_operable(engine0))
					{
						continue;
					}
					JetNozzle[] nz0 = Acc.TF_nozzles(engine0);
					if (nz0 == null)
					{
						continue;
					}
					foreach (JetNozzle jn0 in nz0)
					{
						if (jn0 != null)
						{
							float th0 = Mathf.Max(0f, Acc.JN_totalThrust(jn0));
							totalThrust += th0;
							Transform tt0 = Acc.JN_thrustT(jn0);
							if (tt0 != null && th0 > 1f)
							{
								// thrust moment as it is (nozzles at 0): the asymmetric-load model's engine-out yaw
								Mnow += Effector.ToAero(ctx.rootInv, Vector3.Cross(tt0.position - ctx.cg, tt0.forward * th0));
							}
						}
					}
				}
				now = 0f;
				lo = 0f;
				hi = 0f;
				cmd = 0f;
				return;
			}
			foreach (Turbojet engine in engines)
			{
				if (engine == null || !Acc.TF_operable(engine))
				{
					continue;
				}
				Transform[] array = Acc.TF_vecT(engine);
				if (array == null || array.Length == 0)
				{
					continue;
				}
				Aircraft aircraft = Acc.TF_aircraft(engine);
				if ((Object)(object)aircraft != null && aircraft.speed > Acc.TF_vecMaxSpeed(engine))
				{
					continue;
				}
				if ((Object)(object)aircraft != null && CutoffFadeBand > 0f)
				{
					float vmax = Acc.TF_vecMaxSpeed(engine);
					speedFade = Mathf.Min(speedFade, Mathf.Clamp01((vmax - aircraft.speed) / Mathf.Max(1f, CutoffFadeBand * vmax)));
				}
				Transform transform = array[0];
				// Vortex 0.1.0: the pivot is the vectoring transform the nozzle's thrust transform hangs from (the FS-20's list is
				// two vanes and then the thrust transform itself, at the end of the swivel chain); the first one otherwise
				JetNozzle[] nzP = Acc.TF_nozzles(engine);
				Transform ttP = (nzP != null && nzP.Length > 0 && nzP[0] != null) ? Acc.JN_thrustT(nzP[0]) : null;
				if (ttP != null)
				{
					for (int iP = 0; iP < array.Length; iP++)
					{
						if (array[iP] != null && (ttP == array[iP] || ttP.IsChildOf(array[iP])))
						{
							transform = array[iP];
							break;
						}
					}
				}
				if (transform == null)
				{
					continue;
				}
				Quaternion quaternion = ((transform.parent != null) ? transform.parent.rotation : Quaternion.identity);
				Vector3 vector = Acc.TF_nozzleAngles(engine);
				float y = vector.y;
				num += vector.x;
				num2++;
				JetNozzle[] array2 = Acc.TF_nozzles(engine);
				if (array2 == null)
				{
					continue;
				}
				JetNozzle[] array3 = array2;
				foreach (JetNozzle jetNozzle in array3)
				{
					Transform transform2 = ((jetNozzle != null) ? Acc.JN_thrustT(jetNozzle) : null);
					if (transform2 == null)
					{
						continue;
					}
					float num3 = Acc.JN_totalThrust(jetNozzle);
					if (num3 <= 1f)
					{
						continue;
					}
					totalThrust += num3;
					Quaternion rotation = transform.rotation;
					Quaternion quaternion2 = Quaternion.Inverse(rotation) * transform2.rotation;
					Vector3 vector2 = Quaternion.Inverse(rotation) * (transform2.position - transform.position);
					for (int k = 0; k <= N; k++)
					{
						float value = ((k < N) ? Mathf.Clamp(grid[k], min * speedFade, max * speedFade) : vector.x);
						Quaternion quaternion3 = quaternion * Quaternion.Euler(Mathf.Clamp(value, -20f, 20f), (yawSeparate && k < N) ? 0f : y, 0f);
						Vector3 vector3 = quaternion3 * quaternion2 * Vector3.forward;
						Vector3 vector4 = transform.position + quaternion3 * vector2;
						Vector3 vector5 = Effector.ToAero(ctx.rootInv, Vector3.Cross(vector4 - ctx.cg, vector3 * num3));
						if (k < N)
						{
							M[k] += vector5;
						}
						else
						{
							Mnow += vector5;
						}
					}
					flag = true;
				}
			}
			now = ((num2 > 0) ? (num / (float)num2) : 0f);
			rate = Plugin.TvcSlew;
			active = flag;
			lo = min * speedFade;
			hi = max * speedFade;
			if (!active)
			{
				cmd = 0f;
			}
		}
	}
}
