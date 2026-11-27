using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Revoker_FS12.FCS
{
	// Revoker 0.2.3: render-only hinge correction for the trailing-edge surfaces. The game (ControlSurface job, and the HLD
	// for the inboard flaps) turns each surface's visible mesh about its own local x axis through the mesh origin; on the
	// Revoker the modelled hinge lines are not on that axis, so a deflected surface opens a gap at one end and clips into the
	// wing at the other. This draws a copy of the mesh turned about the measured hinge line instead and hides the original
	// renderer. Physics is untouched: the visible transform (also the part's lift normal) keeps the game's rotation, so the
	// aero, the FCS's effector models and the damage model are exactly as before. The deflection is read back every frame
	// from the visible transform, so whatever moves the surface (game, HLD, FCS) is followed.
	internal sealed class VisualHinge : MonoBehaviour
	{
		// per surface: hinge axis and a point on it, both in the visible mesh's local frame at rest (measured from the meshes)
		internal struct Hinge
		{
			public string vis;

			public Vector3 axis;

			public Vector3 pivot;

			// 1.6.1b: the wing part the surface hangs on; its mesh being switched off tells another mod has replaced the model
			public string wing;
		}

		// Measured from the game meshes (fighter1_aileron_L/R, resources.assets.resS): the aileron's leading edge, and the
		// wingtip's cove lip 2.4-5.2 cm behind it, run along z = 0.033 -/+ 0.161 x (9.15 deg sweep to the mesh x axis, 0.5 deg
		// dihedral); the nose centre (hinge) is 4.5 cm behind the leading edge, 1.4 cm from the mesh origin. Rotating about the
		// mesh x axis at 30 deg moved the leading-edge ends +8.2 / -5.0 cm (into the wing at one end, out of the cove at the
		// other); about this line both move 2.2 cm, inside the 3-7 cm cove. The inboard flaps' leading edge runs along the
		// mesh x axis (z 0.058, nose on y 0), so the game's hinge is already right there and they are not listed.
		internal static readonly List<Hinge> Table = new List<Hinge>
		{
			new Hinge
			{
				vis = "aileron_L_visible",
				wing = "wingtip_L",
				axis = new Vector3(1f, 0.0086f, -0.1611f),
				pivot = new Vector3(-0.0072f, 0f, -0.0114f)
			},
			new Hinge
			{
				vis = "aileron_R_visible",
				wing = "wingtip_R",
				axis = new Vector3(1f, -0.0086f, 0.1611f),
				pivot = new Vector3(0.0072f, 0f, -0.0114f)
			}
		};

		private sealed class Entry
		{
			public Transform vis;

			public Quaternion rest;

			public Vector3 restPos;

			public Vector3 axis;

			public Vector3 pivot;

			public Transform proxy;

			public MeshRenderer orig;

			public MeshRenderer prox;

			public MeshFilter origF;

			public MeshFilter proxF;

			// 1.6.1b: the wing part the surface hangs on (aileron_L -> wingtip_L): its own renderer
			public Renderer wing;
		}

		private float syncT = 1f;

		// 1.6.1: the base airframe's own renderers (level-of-detail 0, without the surfaces copied here), to tell when another mod
		// has replaced the model (e.g. the EF-2000 Typhoon mod hides the Revoker's meshes with forceRenderingOff and draws its
		// own flaperons on the game's surface transforms): the copies drawn here are then hidden too
		private readonly List<Renderer> baseRenderers = new List<Renderer>();

		private bool baseHidden;

		private float diagT;

		private bool diagDone;

		private readonly List<Entry> entries = new List<Entry>();

		public static int Attach(Aircraft a)
		{
			if (a == null || Table.Count == 0 || !Plugin.VisualHingeFix)
			{
				Plugin.Log.LogInfo($"Revoker FCS hinge visuals: off (table {Table.Count}, VisualHingeFix {Plugin.VisualHingeFix})");
				return 0;
			}
			GameObject go = ((Component)(object)a).gameObject;
			if (go.GetComponent<VisualHinge>() != null)
			{
				return -1;
			}
			Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
			foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
			{
				byName[t.name] = t;
			}
			Dictionary<Transform, Quaternion> rests = new Dictionary<Transform, Quaternion>();
			foreach (ControlSurface cs in Acc.Collect<ControlSurface>(a))
			{
				GameObject v = Acc.CS_visible(cs);
				if (v == null)
				{
					continue;
				}
				// 0.2.3a: the surface's own visible mesh, wherever the part sits in the scene (0.2.3 only searched under the
				// aircraft root and found nothing in game)
				byName[v.name] = v.transform;
				if (Acc.JobCreated(cs))
				{
					rests[v.transform] = Acc.Job(cs).restingRotation;
				}
			}
			VisualHinge vh = null;
			foreach (Hinge h in Table)
			{
				if (!byName.TryGetValue(h.vis, out Transform vis) || vis.parent == null)
				{
					Plugin.Log.LogInfo("Revoker FCS hinge visuals: " + h.vis + " not found");
					continue;
				}
				MeshFilter mf = vis.GetComponent<MeshFilter>();
				MeshRenderer mr = vis.GetComponent<MeshRenderer>();
				if (mf == null || mr == null || mf.sharedMesh == null)
				{
					Plugin.Log.LogInfo($"Revoker FCS hinge visuals: {h.vis} has no mesh renderer (filter {mf != null}, renderer {mr != null}, mesh {(mf != null && mf.sharedMesh != null)})");
					continue;
				}
				// the HLD-driven inboard flaps rest at identity (anglesRetracted 0,0,0); the ailerons at the job's resting pose
				Quaternion rest = rests.TryGetValue(vis, out Quaternion r) ? r : Quaternion.identity;
				GameObject p = new GameObject(vis.name + "_hingeVis");
				p.layer = vis.gameObject.layer;
				p.transform.SetParent(vis.parent, false);
				p.transform.localPosition = vis.localPosition;
				p.transform.localRotation = rest;
				p.transform.localScale = vis.localScale;
				MeshFilter pf = p.AddComponent<MeshFilter>();
				pf.sharedMesh = mf.sharedMesh;
				MeshRenderer pr = p.AddComponent<MeshRenderer>();
				pr.sharedMaterials = mr.sharedMaterials;
				pr.shadowCastingMode = mr.shadowCastingMode;
				pr.receiveShadows = mr.receiveShadows;
				pr.lightProbeUsage = mr.lightProbeUsage;
				pr.reflectionProbeUsage = mr.reflectionProbeUsage;
				mr.forceRenderingOff = true;
				if (vh == null)
				{
					vh = go.AddComponent<VisualHinge>();
					vh.lod = go.GetComponent<LODGroup>();
				}
				// the airframe's LODGroup switches the LOD0 renderers off at range: the copy takes the original's slot
				vh.SwapLod(mr, pr);
				vh.entries.Add(new Entry
				{
					vis = vis,
					rest = rest,
					restPos = vis.localPosition,
					axis = h.axis.normalized,
					pivot = h.pivot,
					proxy = p.transform,
					orig = mr,
					prox = pr,
					origF = mf,
					proxF = pf,
					wing = WingRenderer(a, h.wing, vis)
				});
			}
			if (vh != null)
			{
				vh.CollectBase(go);
			}
			return (vh != null) ? vh.entries.Count : 0;
		}

		private void LateUpdate()
		{
			// a damage state may swap the mesh or the materials: the copy follows (checked twice a second)
			syncT += Time.deltaTime;
			bool sync = syncT > 0.5f;
			if (sync)
			{
				syncT = 0f;
			}
			if (!diagDone)
			{
				diagT += Time.deltaTime;
				if (diagT > 3f)
				{
					diagDone = true;
					int n = 0;
					int off = 0;
					foreach (Renderer r in baseRenderers)
					{
						if (r != null)
						{
							n++;
							off += Off(r) ? 1 : 0;
						}
					}
					string w = "";
					foreach (Entry e in entries)
					{
						w += (e.wing != null) ? $" {e.wing.name}: forceRenderingOff {e.wing.forceRenderingOff}, enabled {e.wing.enabled}, active {e.wing.gameObject.activeInHierarchy};" : " (no wing renderer);";
					}
					Plugin.Log.LogInfo($"Revoker FCS hinge visuals check: base renderers {off}/{n} off;{w} copies {(baseHidden ? "hidden" : "drawn")}.");
				}
			}
			if (sync)
			{
				bool hid = BaseHidden();
				if (hid != baseHidden)
				{
					baseHidden = hid;
					Plugin.Log.LogInfo(hid ? "Revoker FCS hinge visuals: the base airframe's meshes are hidden (another mod draws its own model): the hinge copies are hidden too." : "Revoker FCS hinge visuals: the base airframe is drawn again: hinge copies back on.");
				}
			}
			for (int i = 0; i < entries.Count; i++)
			{
				Entry e = entries[i];
				if (e.vis == null || e.proxy == null)
				{
					continue;
				}
				// the game's deflection: its rotation about local x from the rest pose
				Quaternion d = Quaternion.Inverse(e.rest) * e.vis.localRotation;
				float delta = 2f * Mathf.Atan2(d.x, d.w) * 57.29578f;
				if (delta > 180f)
				{
					delta -= 360f;
				}
				else if (delta < -180f)
				{
					delta += 360f;
				}
				// 1.6.3: with the base model hidden by another mod the copy is parked at the surface's resting pose: that mod may
				// have hung its own panels under it (the EF-2000 Typhoon mod parents each panel to the smallest base mesh around
				// it), and a copy still turning about the Revoker's hinge carried them along (the floating outer flaperons)
				if (baseHidden)
				{
					delta = 0f;
				}
				Quaternion q = Quaternion.AngleAxis(delta, e.axis);
				e.proxy.localRotation = e.rest * q;
				// about the hinge point, not the mesh origin: x' = p + q (x - p)
				e.proxy.localPosition = (baseHidden ? e.restPos : e.vis.localPosition) + e.rest * (e.pivot - q * e.pivot);
				e.prox.enabled = !baseHidden && e.orig.enabled && e.vis.gameObject.activeInHierarchy;
				if (sync)
				{
					if (e.origF != null && e.proxF != null && e.proxF.sharedMesh != e.origF.sharedMesh)
					{
						e.proxF.sharedMesh = e.origF.sharedMesh;
					}
					if (e.orig.sharedMaterial != e.prox.sharedMaterial)
					{
						e.prox.sharedMaterials = e.orig.sharedMaterials;
					}
				}
			}
		}

		private LODGroup lod;

		private void CollectBase(GameObject go)
		{
			baseRenderers.Clear();
			HashSet<Renderer> mine = new HashSet<Renderer>();
			foreach (Entry e in entries)
			{
				mine.Add(e.orig);
				mine.Add(e.prox);
			}
			if (lod != null)
			{
				LOD[] lods = lod.GetLODs();
				if (lods.Length > 0 && lods[0].renderers != null)
				{
					foreach (Renderer r in lods[0].renderers)
					{
						if (r != null && !mine.Contains(r))
						{
							baseRenderers.Add(r);
						}
					}
				}
			}
			if (baseRenderers.Count == 0)
			{
				foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
				{
					if (!mine.Contains(r))
					{
						baseRenderers.Add(r);
					}
				}
			}
		}

		// the wing part's own renderer: the part found by name among the aircraft's parts (in game the parts need not sit under
		// the aircraft root), else the surface part's parent
		private static Renderer WingRenderer(Aircraft a, string wingName, Transform vis)
		{
			if (!string.IsNullOrEmpty(wingName) && a.partLookup != null)
			{
				foreach (UnitPart up in a.partLookup)
				{
					if (up != null && up.name == wingName)
					{
						Renderer r = WingMesh(up.transform, vis);
						if (r != null)
						{
							return r;
						}
					}
				}
			}
			Transform w = (vis.parent != null) ? vis.parent.parent : null;
			return (w != null) ? WingMesh(w, vis) : null;
		}

		// the wing part's largest full-detail mesh: its own renderer or one below it, not the surface part's, not a _lod copy
		private static Renderer WingMesh(Transform wing, Transform vis)
		{
			Renderer own = wing.GetComponent<MeshRenderer>();
			if (own != null)
			{
				return own;
			}
			Transform surf = (vis.parent != null) ? vis.parent : vis;
			Renderer best = null;
			float bestSize = 0f;
			foreach (MeshRenderer r in wing.GetComponentsInChildren<MeshRenderer>(true))
			{
				Transform t = r.transform;
				if (t == surf || t.IsChildOf(surf) || t.name.IndexOf("_lod", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.name.EndsWith("_hingeVis") || UnderStore(t, wing))
				{
					continue;
				}
				float sz = r.bounds.size.sqrMagnitude;
				if (best == null || sz > bestSize)
				{
					best = r;
					bestSize = sz;
				}
			}
			return best;
		}

		// a hardpoint, pylon or the store on it (kept drawn by a replacement model)
		private static bool UnderStore(Transform t, Transform top)
		{
			for (Transform x = t; x != null && x != top; x = x.parent)
			{
				string n = x.name;
				if (n.IndexOf("hardpoint", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("pylon", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("rail", System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		private static bool Off(Renderer r)
		{
			return r.forceRenderingOff || !r.enabled || !r.gameObject.activeInHierarchy;
		}

		// 1.6.1b: the base airframe is hidden by another mod when the wing part a copied surface hangs on (the Revoker's own
		// wingtip mesh) is switched off, or when more than half of its level-of-detail-0 renderers are. 1.6.1 used only the
		// second test, and it did not trip in game with the Typhoon mod (the grey aileron copies stayed).
		private bool BaseHidden()
		{
			foreach (Entry e in entries)
			{
				if (e.wing != null && Off(e.wing))
				{
					return true;
				}
			}
			return BaseFractionHidden();
		}

		private bool BaseFractionHidden()
		{
			int n = 0;
			int off = 0;
			foreach (Renderer r in baseRenderers)
			{
				if (r == null)
				{
					continue;
				}
				n++;
				if (Off(r))
				{
					off++;
				}
			}
			return n >= 4 && off * 2 > n;
		}

		private void SwapLod(Renderer from, Renderer to)
		{
			if (lod == null || from == null || to == null)
			{
				return;
			}
			LOD[] lods = lod.GetLODs();
			bool changed = false;
			for (int i = 0; i < lods.Length; i++)
			{
				Renderer[] rs = lods[i].renderers;
				if (rs == null)
				{
					continue;
				}
				for (int j = 0; j < rs.Length; j++)
				{
					if (rs[j] == from)
					{
						rs[j] = to;
						changed = true;
					}
				}
			}
			if (changed)
			{
				lod.SetLODs(lods);
			}
		}

		private void OnDestroy()
		{
			foreach (Entry e in entries)
			{
				if (e.orig != null)
				{
					e.orig.forceRenderingOff = false;
					SwapLod(e.prox, e.orig);
				}
				if (e.proxy != null)
				{
					Destroy(e.proxy.gameObject);
				}
			}
		}
	}
}
