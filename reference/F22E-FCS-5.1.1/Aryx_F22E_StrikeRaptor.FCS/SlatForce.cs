using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 4.6.0 (SlatPhysics): game-side slat aerodynamics (see SlatAero). Every physics step, for each slat: the wing panel behind
	// it gets its force change for the slat's droop away from the schedule (at the panel's centre of lift), and the slat plate
	// its own (at the slat's hinge, on the panel's rigidbody). The FCS's FlapSystem sets the droops; without it (FCS off) the
	// game's own slat model runs and this does nothing.
	internal class SlatForce : MonoBehaviour
	{
		private Aircraft ac;

		internal FlapSystem flaps;

		private readonly List<AeroPart> hosts = new List<AeroPart>();

		private readonly List<AirfoilTable> tables = new List<AirfoilTable>();

		private readonly List<Transform> slats = new List<Transform>();

		private readonly List<int> segs = new List<int>();

		public static int Attach(Aircraft a)
		{
			if (a == null)
			{
				return 0;
			}
			GameObject go = ((Component)(object)a).gameObject;
			if (go.GetComponent<SlatForce>() != null)
			{
				return 0;
			}
			AircraftParameters prm = a.GetAircraftParameters();
			Dictionary<string, AeroPart> byName = new Dictionary<string, AeroPart>();
			foreach (UnitPart p in a.partLookup)
			{
				if (p is AeroPart ap)
				{
					byName[ap.gameObject.name.Replace("Aryx_KingRaptor_", "")] = ap;
				}
			}
			Dictionary<string, Transform> slatT = new Dictionary<string, Transform>();
			foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
			{
				string n = t.name.Replace("Aryx_KingRaptor_", "");
				if (n.StartsWith("Slat_") && !slatT.ContainsKey(n))
				{
					slatT[n] = t;
				}
			}
			SlatForce c = null;
			for (int i = 0; i < SlatAero.Segs.Length; i++)
			{
				SlatAero.Seg s = SlatAero.Segs[i];
				if (!byName.TryGetValue(s.host, out var host))
				{
					continue;
				}
				int idx = Acc.AP_airfoil(host);
				Airfoil af = (idx >= 0 && prm != null && prm.airfoils != null && idx < prm.airfoils.Length) ? prm.airfoils[idx] : null;
				if (af == null || af.liftCoef == null || af.dragCoef == null)
				{
					continue;
				}
				if (c == null)
				{
					c = go.AddComponent<SlatForce>();
					c.ac = a;
				}
				c.hosts.Add(host);
				c.tables.Add(StallStagger.TableFor(host, af));
				slatT.TryGetValue(s.slat, out var st);
				c.slats.Add(st);
				c.segs.Add(i);
			}
			return (c != null) ? c.hosts.Count : 0;
		}

		private void FixedUpdate()
		{
			if ((Object)(object)ac == null)
			{
				Destroy(this);
				return;
			}
			if (!SlatAero.On || flaps == null || !flaps.Active || !ac.LocalSim || Time.fixedTime - flaps.slatStamp > 0.1f)
			{
				return;
			}
			float rho = ac.airDensity;
			if (rho <= 0f)
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
			for (int k = 0; k < hosts.Count; k++)
			{
				AeroPart host = hosts[k];
				if (host == null || host.rb == null || host.IsDetached())
				{
					continue;
				}
				AirfoilTable t = tables[k];
				if (t == null || t.Generic)
				{
					continue;
				}
				SlatAero.Seg s = SlatAero.Segs[segs[k]];
				float droop = flaps.segDroop[segs[k]];   // 4.6.1: per slat
				float droopRef = flaps.segRef[segs[k]];
				if (Mathf.Abs(droop - droopRef) < 0.01f)
				{
					continue;
				}
				Transform lt = Acc.AP_liftNormal(host);
				if (lt == null)
				{
					continue;
				}
				float area = Acc.AP_wingArea(host) * Acc.AP_wingEff(host);
				float cov = SlatAero.Cover(s.idx);
				Vector3 v = host.rb.velocity - wind;
				float v2 = v.sqrMagnitude;
				if (v2 < 25f)
				{
					continue;
				}
				Vector3 dF = SlatAero.HostDelta(v, lt.rotation, t.ClTable, t.CdTable, 0.5f * rho * v2 * area, cov, droop - droopRef);
				if (dF.sqrMagnitude > 0f)
				{
					host.rb.AddForceAtPosition(dF, host.rb.worldCenterOfMass + lt.rotation * Acc.AP_centerOfLift(host));
				}
				Transform st = slats[k];
				if (st != null && Plugin.SlatChordFrac > 0f)
				{
					Vector3 vp = host.rb.GetPointVelocity(st.position) - wind;
					Vector3 dP = SlatAero.PlateDelta(vp, lt.rotation, t.ClTable, t.CdTable, 0.5f * rho * vp.sqrMagnitude * area * cov * Plugin.SlatChordFrac, droop, droopRef);
					if (dP.sqrMagnitude > 0f)
					{
						host.rb.AddForceAtPosition(dP, st.position);
					}
				}
			}
		}
	}
}
