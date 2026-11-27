using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 4.8.0 (two-zone airframe): game-side factors on the airfoil force of fixed parts, as correction forces (the game's aero job
	// gives each part its chart force; this adds (K - 1) x that airfoil lift and drag at the same point, every physics step;
	// parasitic drag is not scaled).
	//  - Fin blanking (FcsController.FinBlankMin < 1): the fins and rudders (Vstab_*, Rudder_*), K = Downwash.FinBlankK(body AoA).
	//  - Slat-coupled aft lift (FcsController.SlatAftGain): the parts SlatAero.AftSide names, K = SlatAero.AftK for the slats'
	//    mean droop away from their schedule on that side.
	// The same factors are in the FCS's models (rudder tables, slat effectors, the authority sweep) and the bench plant.
	// CamberForce scales a host part's camber increment by the same K (ScaleOf).
	internal class AirfoilScaleForce : MonoBehaviour
	{
		private Aircraft ac;

		private readonly List<AeroPart> parts = new List<AeroPart>();

		private readonly List<AirfoilTable> tables = new List<AirfoilTable>();

		private readonly List<bool> fin = new List<bool>();

		private readonly List<int> aft = new List<int>();   // -1 / +1 side, 0 centre, 9 none

		private readonly Dictionary<int, int> index = new Dictionary<int, int>();

		private SlatForce slatForce;

		private bool slatLooked;

		// this physics step's values (ScaleOf)
		private float stepTime = -1f, finK = 1f, devL, devR;

		public static int Attach(Aircraft a)
		{
			if (a == null)
			{
				return 0;
			}
			GameObject go = ((Component)(object)a).gameObject;
			if (go.GetComponent<AirfoilScaleForce>() != null)
			{
				return 0;
			}
			AircraftParameters prm = a.GetAircraftParameters();
			AirfoilScaleForce c = null;
			foreach (UnitPart p in a.partLookup)
			{
				if (!(p is AeroPart ap))
				{
					continue;
				}
				string n = ap.gameObject.name;
				bool isFin = Downwash.IsFinName(n);
				int side = SlatAero.AftSide(n, out int s0) ? s0 : 9;
				if (!isFin && side == 9)
				{
					continue;
				}
				int idx = Acc.AP_airfoil(ap);
				Airfoil af = (idx >= 0 && prm != null && prm.airfoils != null && idx < prm.airfoils.Length) ? prm.airfoils[idx] : null;
				if (af == null || af.liftCoef == null || af.dragCoef == null)
				{
					continue;
				}
				if (c == null)
				{
					c = go.AddComponent<AirfoilScaleForce>();
					c.ac = a;
				}
				c.index[ap.GetInstanceID()] = c.parts.Count;
				c.parts.Add(ap);
				c.tables.Add(StallStagger.TableFor(ap, af));
				c.fin.Add(isFin);
				c.aft.Add(side);
			}
			return (c != null) ? c.parts.Count : 0;
		}

		// the factor on a part's airfoil force right now (1 for a part this component does not scale)
		public float ScaleOf(AeroPart ap)
		{
			if (ap == null || !index.TryGetValue(ap.GetInstanceID(), out int i))
			{
				return 1f;
			}
			Refresh();
			return K(i);
		}

		private float K(int i)
		{
			float k = fin[i] ? finK : 1f;
			if (aft[i] != 9 && FcsController.SlatAftGain != 0f)
			{
				k *= SlatAero.AftK(aft[i], devL, devR);
			}
			return k;
		}

		private void Refresh()
		{
			if (stepTime == Time.fixedTime)
			{
				return;
			}
			stepTime = Time.fixedTime;
			finK = 1f;
			devL = 0f;
			devR = 0f;
			if ((Object)(object)ac == null || ac.rb == null)
			{
				return;
			}
			if (FcsController.FinBlankMin != 1f)
			{
				Vector3 wind = Vector3.zero;
				try
				{
					wind = ac.GetWindVelocity();
				}
				catch
				{
				}
				finK = Downwash.FinBlankFor(ac.rb.velocity - wind, ((Component)(object)ac).transform.rotation);
			}
			if (FcsController.SlatAftGain != 0f && SlatAero.On)
			{
				if (!slatLooked)
				{
					slatLooked = true;
					slatForce = ((Component)(object)ac).gameObject.GetComponent<SlatForce>();
				}
				FlapSystem flaps = (slatForce != null) ? slatForce.flaps : null;
				// as SlatForce: only while the FCS drives the slats
				if (flaps != null && flaps.Active && Time.fixedTime - flaps.slatStamp <= 0.1f)
				{
					for (int s = 0; s < 6; s++)
					{
						float d = (flaps.segDroop[s] - flaps.segRef[s]) / 3f;
						if (SlatAero.Segs[s].side < 0)
						{
							devL += d;
						}
						else
						{
							devR += d;
						}
					}
				}
			}
		}

		private void FixedUpdate()
		{
			if ((Object)(object)ac == null)
			{
				Destroy(this);
				return;
			}
			if (!ac.LocalSim || (FcsController.FinBlankMin == 1f && FcsController.SlatAftGain == 0f))
			{
				return;
			}
			float rho = ac.airDensity;
			if (rho <= 0f)
			{
				return;
			}
			Refresh();
			if (finK == 1f && devL == 0f && devR == 0f)
			{
				return;
			}
			Vector3 wind = Vector3.zero;
			try
			{
				wind = ac.GetWindVelocity();
			}
			catch
			{
			}
			for (int i = 0; i < parts.Count; i++)
			{
				float k = K(i);
				if (k == 1f)
				{
					continue;
				}
				AeroPart ap = parts[i];
				if (ap == null || ap.rb == null || ap.IsDetached())
				{
					continue;
				}
				Transform lt = Acc.AP_liftNormal(ap);
				if (lt == null)
				{
					continue;
				}
				Vector3 v = ap.rb.velocity - wind;
				float v2 = v.sqrMagnitude;
				if (v2 < 25f)
				{
					continue;
				}
				Quaternion rot = lt.rotation;
				Vector3 vl = Quaternion.Inverse(rot) * v;
				tables[i].Get(Mathf.Atan2(vl.y, vl.z), out var cl, out var cd);
				float qS = 0.5f * rho * v2 * Acc.AP_wingArea(ap) * Acc.AP_wingEff(ap);
				Vector3 n = Vector3.Cross(v, rot * Vector3.right).normalized;
				Vector3 com = ap.rb.worldCenterOfMass;
				float mach = Mathf.Sqrt(v2) / DragReliefMath.SpeedOfSound(com.y);
				Vector3 f = -n * (cl * qS) - v.normalized * (cd * qS * DragReliefMath.Transonic(mach));
				ap.rb.AddForceAtPosition(f * (k - 1f), com + rot * Acc.AP_centerOfLift(ap));
			}
		}
	}
}
