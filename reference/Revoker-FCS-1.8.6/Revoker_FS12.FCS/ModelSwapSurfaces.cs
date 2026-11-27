using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Revoker_FS12.FCS
{
	// Revoker 1.6.4: the EF-2000 Typhoon mod's (TyphoonMod 1.0.0) moving panels posed from the Revoker's own surfaces at render
	// time, one surface per panel (the one under it), from the game's resting pose, about the panel's own hinge. Visual only:
	// no game transform the physics reads is touched, nothing in the Typhoon mod's files or code is changed. Was the separate
	// RevokerTyphoonCompat 1.0.0 plugin; see its notes.
	internal sealed class PanelPoser : MonoBehaviour
	{
		private sealed class Panel
		{
			public Transform pivot;

			public Transform driver;

			public Vector3 axisLocal;

			public Vector3 axisRoot;

			public Vector3 localPos0;

			public Vector3 localScale0;

			public string name;

			// 1.7.1: every Revoker surface the Typhoon mod lists for this panel (the choice among them is made in TryBind)
			public List<Transform> cands;
		}

		private Aircraft ac;

		private Component tv;

		private readonly Dictionary<Transform, Quaternion> gameRest = new Dictionary<Transform, Quaternion>();

		private readonly Dictionary<Transform, Quaternion> convRest = new Dictionary<Transform, Quaternion>();

		private readonly List<Panel> panels = new List<Panel>();

		private bool bound;

		private bool hooked;

		private float findT;

		private float since;

		private float diagT;

		private int diagN;

		// 1.6.4: the RevokerTyphoonCompat 1.0.0 plugin folded into the FCS (Tom: one DLL). Added at airframe setup; it waits
		// (twice a second, up to 60 s) for the Typhoon mod's TyphoonVisual on the jet and then binds. Skipped when the separate
		// compat plugin is loaded, so the two never pose the same panels.
		internal static void Attach(Aircraft a)
		{
			if (a == null || !Plugin.ModelSwapSurfaceFix || BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("tom.revoker.typhooncompat"))
			{
				return;
			}
			GameObject go = ((Component)(object)a).gameObject;
			if (go.GetComponent<PanelPoser>() != null)
			{
				return;
			}
			PanelPoser p = go.AddComponent<PanelPoser>();
			p.ac = a;
			p.CollectRests();
		}

		private void CollectRests()
		{
			List<ControlSurface> list = new List<ControlSurface>(((Component)(object)ac).GetComponentsInChildren<ControlSurface>(true));
			if (ac.partLookup != null)
			{
				foreach (UnitPart up in ac.partLookup)
				{
					if (up != null)
					{
						list.AddRange(up.GetComponentsInChildren<ControlSurface>(true));
					}
				}
			}
			foreach (ControlSurface cs in list)
			{
				GameObject v = Acc.CS_visible(cs);
				if (v == null || gameRest.ContainsKey(v.transform))
				{
					continue;
				}
				Quaternion rest = v.transform.localRotation;
				try
				{
					if (Acc.JobCreated(cs))
					{
						rest = Acc.Job(cs).restingRotation;
					}
				}
				catch (Exception)
				{
				}
				gameRest[v.transform] = rest;
			}
		}

		private List<Transform> Candidates(IList drivers)
		{
			List<Transform> l = new List<Transform>();
			foreach (object d in drivers)
			{
				if (d is Transform t && gameRest.ContainsKey(t) && !l.Contains(t))
				{
					l.Add(t);
				}
			}
			return l;
		}

		private static object Get(object o, string field)
		{
			FieldInfo f = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return (f != null) ? f.GetValue(o) : null;
		}

		private void Update()
		{
			if (bound)
			{
				return;
			}
			since += Time.deltaTime;
			findT += Time.deltaTime;
			if (findT < 0.5f)
			{
				return;
			}
			findT = 0f;
			try
			{
				TryBind();
			}
			catch (Exception ex)
			{
				bound = true;
				Plugin.Log.LogWarning("Panel binding failed: " + ex);
			}
			if (since > 60f)
			{
				bound = true;
			}
		}

		// x extent (aircraft-root frame) of the renderers on/under a transform
		private bool SpanX(Transform t, bool children, Quaternion rootInv, Vector3 root, out float lo, out float hi)
		{
			lo = float.MaxValue;
			hi = float.MinValue;
			Renderer[] rs = children ? t.GetComponentsInChildren<Renderer>(true) : t.GetComponents<Renderer>();
			foreach (Renderer r in rs)
			{
				Bounds b = r.bounds;
				for (int i = 0; i < 8; i++)
				{
					Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) != 0 ? 1f : -1f, (i & 2) != 0 ? 1f : -1f, (i & 4) != 0 ? 1f : -1f));
					float x = (rootInv * (c - root)).x;
					lo = Mathf.Min(lo, x);
					hi = Mathf.Max(hi, x);
				}
			}
			return lo <= hi;
		}

		private bool waitedForFlaps;

		private void TryBind()
		{
			if (ac == null)
			{
				bound = true;
				return;
			}
			if (tv == null)
			{
				tv = ((Component)(object)ac).gameObject.GetComponent("TyphoonVisual");
				if (tv == null)
				{
					return;
				}
			}
			if (!(Get(tv, "Surfaces") is IList list) || list.Count == 0)
			{
				return;
			}
			bound = true;
			Transform rt = ((Component)(object)ac).transform;
			Quaternion rootInv = Quaternion.Inverse(rt.rotation);
			// the inner flaps are not ControlSurfaces on the Revoker (HLD-driven): any driver named flap_*_visible rests at the pose
			// the FCS's own flap effector holds as its rest (1.7.1), else at its modelled (identity) pose
			foreach (object s in list)
			{
				if (Get(s, "Drivers") is IList ds)
				{
					foreach (object d in ds)
					{
						if (d is Transform t && !gameRest.ContainsKey(t) && t.name.StartsWith("flap_") && t.name.EndsWith("_visible"))
						{
							gameRest[t] = HingeFlapEffector.RestByVis.TryGetValue(t, out Quaternion fr) ? fr : Quaternion.identity;
						}
					}
				}
			}
			foreach (object s in list)
			{
				if (s == null)
				{
					continue;
				}
				Transform pivot = Get(s, "Pivot") as Transform;
				IList drivers = Get(s, "Drivers") as IList;
				IList rests = Get(s, "Rest") as IList;
				if (drivers != null && rests != null)
				{
					for (int i = 0; i < drivers.Count && i < rests.Count; i++)
					{
						if (drivers[i] is Transform dt && rests[i] is Quaternion rq && gameRest.ContainsKey(dt) && !convRest.ContainsKey(dt))
						{
							convRest[dt] = rq;
						}
					}
				}
				bool skip = pivot == null || drivers == null || drivers.Count == 0 || (Get(s, "FullRotation") is bool f1 && f1) || (Get(s, "IsStick") is bool f2 && f2) || (Get(s, "IsThrottle") is bool f3 && f3) || Get(s, "AirbrakeRef") != null || !string.IsNullOrEmpty(Get(s, "Input") as string);
				if (skip || !(Get(s, "AxisLocal") is Vector3 axL) || !(Get(s, "AxisRoot") is Vector3 axR))
				{
					continue;
				}
				// the driver under the panel: largest span overlap, else nearest span centre
				bool havePanel = SpanX(pivot, true, rootInv, rt.position, out float plo, out float phi);
				Transform best = null;
				float bestScore = float.MinValue;
				bool ours = true;
				foreach (object d in drivers)
				{
					Transform t = d as Transform;
					if (t == null || !gameRest.ContainsKey(t))
					{
						ours = false;
						break;
					}
					float score;
					// 1.7.1: a driver whose mesh sits on a child (the inner flaps) is measured through its children, so both candidates
					// are scored the same way (the overlap). 1.6.4 scored such a driver by distance, on another scale, and the aileron won
					// the inner panel
					if (havePanel && (SpanX(t, false, rootInv, rt.position, out float dlo, out float dhi) || SpanX(t, true, rootInv, rt.position, out dlo, out dhi)))
					{
						float overlap = Mathf.Min(phi, dhi) - Mathf.Max(plo, dlo);
						score = overlap - 0.001f * Mathf.Abs((plo + phi) * 0.5f - (dlo + dhi) * 0.5f);
					}
					else
					{
						score = -(rootInv * (t.position - pivot.position)).sqrMagnitude;
					}
					if (best == null || score > bestScore)
					{
						bestScore = score;
						best = t;
					}
				}
				if (!ours || best == null)
				{
					continue;
				}
				panels.Add(new Panel
				{
					pivot = pivot,
					driver = best,
					axisLocal = axL,
					axisRoot = axR,
					localPos0 = pivot.localPosition,
					localScale0 = pivot.localScale,
					name = pivot.name.Replace("_hinge", ""),
					cands = Candidates(drivers)
				});
			}
			// 1.7.1: one surface per panel. Where two panels took the same surface and one of them lists another surface that no
			// panel took, that panel moves to it. The Typhoon mod lists the aileron and the inner flap for its inner flaperon and
			// the aileron alone for the outer one; 1.6.4 - 1.7.0 bound both to the aileron (24 Nov 12:32 log), so the inner panel
			// followed the outer pair whatever the inner flaps did (stall stagger, airbrake, flaps down).
			// 1.8.2: that did not hold in the game (14:33 log on 1.8.1: both panels still on the aileron). The Typhoon mod finds its
			// drivers through ControlSurface.visibleMesh, and the Revoker's inner flaps are not ControlSurfaces (the high-lift device
			// moves them), so at run time its inner flaperon lists the aileron alone and there was no second candidate. Where two
			// panels took the same surface, the Revoker's own inner-flap visuals on that side are candidates too, and the panels on
			// one wing keep the spanwise order of the surfaces they follow (inboard panel on the inboard surface).
			System.Func<Transform, float> rootX = (Transform t) => (rootInv * (t.position - rt.position)).x;
			System.Func<Transform, bool, float> spanMid = (Transform t, bool own) =>
			{
				if (SpanX(t, own, rootInv, rt.position, out float lo, out float hi) || SpanX(t, !own, rootInv, rt.position, out lo, out hi))
				{
					return 0.5f * (lo + hi);
				}
				return rootX(t);
			};
			// the inner flaps' visuals as the FCS drives them: the transforms its inner-flap effectors move (HingeFlapEffector.RestByVis),
			// on this jet. 1.8.2 looked them up by the name flap_*_visible and found none in the game (15:16 log: no candidate added).
			List<Transform> flapVis = new List<Transform>();
			Transform acT = ((Component)(object)ac).transform;
			foreach (KeyValuePair<Transform, Quaternion> kv in HingeFlapEffector.RestByVis)
			{
				Transform t = kv.Key;
				if (t != null && t.IsChildOf(acT) && !flapVis.Contains(t))
				{
					flapVis.Add(t);
					gameRest[t] = kv.Value;
				}
			}
			for (int i = 0; i < panels.Count; i++)
			{
				for (int j = 0; j < panels.Count; j++)
				{
					if (i == j || panels[i].driver != panels[j].driver)
					{
						continue;
					}
					float px = rootX(panels[i].pivot);
					foreach (Transform fl in flapVis)
					{
						if (!panels[i].cands.Contains(fl) && rootX(fl) * px > 0f)
						{
							panels[i].cands.Add(fl);
						}
					}
				}
			}
			for (int i = 0; i < panels.Count; i++)
			{
				for (int j = 0; j < panels.Count; j++)
				{
					if (i == j || panels[i].driver != panels[j].driver)
					{
						continue;
					}
					foreach (Transform c in panels[i].cands)
					{
						bool taken = false;
						foreach (Panel q in panels)
						{
							taken |= q.driver == c;
						}
						if (!taken)
						{
							panels[i].driver = c;
							break;
						}
					}
				}
			}
			for (int i = 0; i < panels.Count; i++)
			{
				for (int j = i + 1; j < panels.Count; j++)
				{
					Panel a = panels[i];
					Panel b = panels[j];
					if (a.driver == b.driver || !a.cands.Contains(b.driver) || !b.cands.Contains(a.driver))
					{
						continue;
					}
					bool panelIn = Mathf.Abs(spanMid(a.pivot, true)) < Mathf.Abs(spanMid(b.pivot, true));
					bool drvIn = Mathf.Abs(spanMid(a.driver, false)) < Mathf.Abs(spanMid(b.driver, false));
					if (panelIn != drvIn)
					{
						Transform sw = a.driver;
						a.driver = b.driver;
						b.driver = sw;
					}
				}
			}
			// 1.8.3: the FCS creates its inner-flap effectors when it engages on the jet, which can be after the Typhoon mod has built its
			// panels (15:31 log: bound with no inner-flap visual known yet). While two panels still share one surface and the 60 s window is
			// open, wait and bind again.
			bool shared = false;
			for (int i = 0; i < panels.Count; i++)
			{
				for (int j = i + 1; j < panels.Count; j++)
				{
					shared |= panels[i].driver == panels[j].driver;
				}
			}
			if (shared && since < 60f)
			{
				panels.Clear();
				bound = false;
				waitedForFlaps = true;
				return;
			}
			if (panels.Count > 0 && !hooked)
			{
				Application.onBeforeRender += Pose;
				hooked = true;
			}
			string map = "";
			foreach (Panel p in panels)
			{
				string chain = "";
				int n = 0;
				for (Transform t = p.pivot.parent; t != null && n < 4; t = t.parent, n++)
				{
					chain += ((n > 0) ? " < " : "") + t.name;
				}
				string cl = "";
				foreach (Transform ct in p.cands)
				{
					cl += ((cl.Length > 0) ? ", " : "") + ct.name + " at " + spanMid(ct, false).ToString("F2");
				}
				map += $"\n  {p.name} (span centre {spanMid(p.pivot, true):F2}) <- {p.driver.name} (candidates: {cl}; parent: {chain})";
			}
			Plugin.Log.LogInfo($"Revoker FCS: Typhoon panels on {((UnityEngine.Object)(object)ac).name}: {panels.Count} posed from the Revoker's surfaces (visual only; inner-flap visuals known on this jet: {flapVis.Count}, all jets: {HingeFlapEffector.RestByVis.Count}{(waitedForFlaps ? ", waited for them" : "")}, {since:F1} s after spawn):{map}");
		}

		private void Pose()
		{
			if (ac == null || this == null || !Plugin.ModelSwapSurfaceFix)
			{
				return;
			}
			Quaternion rootInv = Quaternion.Inverse(((Component)(object)ac).transform.rotation);
			bool diag = true && diagN < 6;
			if (diag)
			{
				diagT += Time.deltaTime;
				diag = diagT > 1f;
				if (diag)
				{
					diagT = 0f;
					diagN++;
				}
			}
			string dl = "";
			for (int i = 0; i < panels.Count; i++)
			{
				Panel p = panels[i];
				if (p.pivot == null || p.driver == null || p.driver.parent == null)
				{
					continue;
				}
				Quaternion rest = gameRest[p.driver];
				Quaternion d = Quaternion.Inverse(rest) * p.driver.localRotation;
				// 1.8.3: the driver's turn about whatever axis it turns (the inner flaps' lift-normal transform is not hinged on its local x
				// as the ailerons' visuals are), projected on the panel's hinge; for a turn about local x this is the 1.6.4 reading
				d.ToAngleAxis(out float dAng, out Vector3 dAx);
				if (dAng > 180f)
				{
					dAng -= 360f;
				}
				Vector3 hingeRoot = rootInv * (p.driver.parent.rotation * rest * dAx);
				float a = (dAx.sqrMagnitude > 0.5f && !float.IsNaN(dAng)) ? Mathf.Clamp(dAng * Vector3.Dot(hingeRoot.normalized, p.axisRoot), -45f, 45f) : 0f;
				Quaternion turn = Quaternion.AngleAxis(a, p.axisLocal);
				Transform par = p.pivot.parent;
				if (par == null)
				{
					p.pivot.localRotation = turn;
					continue;
				}
				Matrix4x4 rp = Matrix4x4.identity;
				bool moving = false;
				for (Transform t = par; t != null; t = t.parent)
				{
					Quaternion q;
					if (convRest.TryGetValue(t, out q) || gameRest.TryGetValue(t, out q))
					{
						moving |= Quaternion.Angle(q, t.localRotation) > 0.01f;
					}
					else
					{
						q = t.localRotation;
					}
					rp = Matrix4x4.TRS(t.localPosition, q, t.localScale) * rp;
				}
				if (!moving)
				{
					p.pivot.localPosition = p.localPos0;
					p.pivot.localRotation = turn;
				}
				else
				{
					Matrix4x4 local = par.worldToLocalMatrix * rp * Matrix4x4.TRS(p.localPos0, turn, p.localScale0);
					Vector3 fwd = local.GetColumn(2);
					Vector3 up = local.GetColumn(1);
					if (fwd.sqrMagnitude > 1E-08f && up.sqrMagnitude > 1E-08f)
					{
						p.pivot.localPosition = local.GetColumn(3);
						p.pivot.localRotation = Quaternion.LookRotation(fwd, up);
					}
				}
				if (diag)
				{
					Vector3 parRot = (rootInv * par.rotation).eulerAngles;
					dl += $" {p.name} ({p.driver.name}): driver {dAng:F1}, panel {a:F1}, parent rot {parRot.x:F1}/{parRot.y:F1}/{parRot.z:F1}{(moving ? " (moving parent held)" : "")};";
				}
			}
			if (diag)
			{
				Plugin.Log.LogInfo("Revoker FCS Typhoon panels:" + dl);
			}
		}

		private void OnDestroy()
		{
			if (hooked)
			{
				Application.onBeforeRender -= Pose;
				hooked = false;
			}
		}
	}
}
