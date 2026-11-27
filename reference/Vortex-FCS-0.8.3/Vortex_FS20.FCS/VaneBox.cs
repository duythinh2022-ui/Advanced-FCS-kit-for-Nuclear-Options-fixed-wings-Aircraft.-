using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vortex_FS20.FCS
{
	// Vortex 0.5.0: the lift fan's vane box, drawn (render only: no collider, no mass, no aero; the physics of the vanes is the
	// fan thrust transform SwivelLink.VaneStep turns). The stock jet has an open duct under the fan and nothing that shows the
	// fan's thrust line turning.
	//
	// Vortex 0.5.1: second design. 0.5.0's box stood inside the round duct: a rectangle inscribed in the circle under a
	// shroud, with a keel channel 0.34 m wide wrapped round the fan's hub fairing (Tom, 18 Nov: "Thats ugly, why the middle is
	// so thick, why is it just a square in a circle. Fix that"). Now everything hangs at the duct's exit and below it, at the
	// level of the hub fairing's tip and under it, so the keel is a thin plate and the vanes run from it to the frame over the whole round exit.
	//
	// What the space is, measured on the game's own meshes (fan part "liftFan", 3720 vertices; airframe frame, x right, y up,
	// z forward, metres). The duct is a 32-sided cylinder of radius 0.6188 about x 0, z 2.316 that ends at y -1.139; the hub
	// fairing's cone hangs down the middle to y -1.126. Under the exit the bay is only a bevel: its ceiling falls away from
	// the exit's edge to y -1.17 at the sides and -1.25 fore and aft, and the closed lower doors' inner skin lies right
	// under it, at y -1.25 under the middle of the fan, rising outboard (-1.23 at x 0.52, -1.19 at x 0.60). So there is no
	// room for a box wider than the duct, and outboard of x 0.5 none for a wall or a vane 10 cm deep: the frame is round
	// with two flat sides.
	//  - Frame: a skirt 25 mm thick round the exit (inner radius 0.598; its outer face 4 mm behind the duct wall above the
	//    exit), with flat sides at x +/-0.480 (18 mm thick), from y -1.100 inside the duct down to -1.214 at its inner lip;
	//    the bottom rim is bevelled up to -1.202 outboard, where the doors' skin rises. 75 mm proud of the exit.
	//  - Shelves: the two slivers of the exit outboard of the flat sides are closed by a plate just under the exit plane
	//    (y -1.141 .. -1.147), in the duct wall's tone; its rim lies above the bay's bevel, inside the airframe.
	//  - Keel: a plate 18 mm thick down the middle, y -1.140 (14 mm under the hub fairing's tip) to -1.240, rising to -1.203
	//    at its ends, which stand inside the frame's wall.
	//  - Eight vane pairs (one pair = the left and the right vane of one hinge line: one mesh, one renderer), 140 mm apart:
	//    chord 90 mm, 10 mm thick, hinged at mid chord at y -1.167, on 9 mm pivot pins that end inside the keel and the
	//    frame. Each runs from 10 mm off the keel to 10 mm off the frame; under the round parts of the frame the tip is cut
	//    back towards the leading and trailing edge, which swing fore and aft with the vane (three meshes for the eight
	//    pairs), and the inner top corner is cut back 30 mm so the two pairs next to the fan's axis stay 26 mm clear of
	//    the hub fairing's cone. They turn about the hinge by the thrust line's angle divided by StovlVaneTurning
	//    (-11.1 .. +46.7 deg for the -10 .. +42 deg thrust line): 93 mm or more between neighbours over the whole travel.
	// Clear of the closed doors by 22 mm (frame), 28 mm (vanes), 37 mm (keel), more as they open.
	// Materials are the fan part's own: aircraft_structure (the bay structure shader, tinted by vertex colour: 255 on the
	// frame and keel as the doors' inner structure, 99 on the shelves as the duct wall) and jet_mechanical for the vanes (the
	// dark metal patch of its atlas that the fan's own stators and struts use). 1308 triangles, 9 renderers, in the
	// airframe's own LOD group next to the fan part (level 0 only), switched off while the fan doors are closed, no shadow
	// casting.
	// The vane angle is read from the fan thrust transform itself, so whatever turns it is followed; on jets this PC does not
	// simulate (other players) the transform is at rest and so are the vanes.
	internal sealed partial class VaneBox : MonoBehaviour
	{
		private static readonly FieldInfo fDoors = AccessTools.Field(typeof(SwivelDuctSystem), "thrustDoorPosition");

		private static readonly FieldInfo fLiftFans = AccessTools.Field(typeof(SwivelDuctSystem), "liftFans");

		private static Mesh frameMesh;

		// Vortex 0.5.2: made in Attach, not in a static initializer. 0.5.1 had "= new Mesh[VaneP.Length]" here: VaneP is in the
		// class's other file (VaneBoxData.cs), whose static initializers run after this file's, so VaneP was still null, the
		// type initializer threw, and the model was never attached (game log: "The type initializer for
		// 'Vortex_FS20.FCS.VaneBox' threw an exception"; Tom, 18 Nov: "i dont see it in game"). Nothing static in this file
		// may read the data file's fields; tools/smoke runs the type initializer offline to catch it.
		private static Mesh[] vaneMesh;

		private Transform part;

		private MeshRenderer fanMr;

		private SwivelDuctSystem sds;

		private Transform thrustT;

		private MeshRenderer frameR;

		private readonly Transform[] slatT = new Transform[HingeLocal.Length];

		private readonly MeshRenderer[] slatR = new MeshRenderer[HingeLocal.Length];

		private LODGroup lod;

		private bool ownLod;

		private bool shown = true;

		private float drawn = float.NaN;

		private float syncT;

		private Material matStruct;

		private Material matMech;

		// Vortex 0.5.5: the fan part's skin (its livery), for the frame's outward faces; its slot in the fan renderer's materials
		// too, in case a livery swaps it for a material of another name
		private Material matSkin;

		private int skinSlot = -1;

		// split > 0: two submeshes, indices [0, split) and [split, end) (0.5.5: the frame's structure and skin faces)
		private static Mesh Build(string name, float[] p, float[] n, float[] uv, byte[] c, byte cAll, ushort[] t, int split = 0)
		{
			int nv = p.Length / 3;
			Vector3[] vp = new Vector3[nv];
			Vector3[] vn = new Vector3[nv];
			Vector2[] vu = new Vector2[nv];
			Color32[] vc = new Color32[nv];
			for (int i = 0; i < nv; i++)
			{
				vp[i] = new Vector3(p[3 * i], p[3 * i + 1], p[3 * i + 2]);
				vn[i] = new Vector3(n[3 * i], n[3 * i + 1], n[3 * i + 2]).normalized;
				vu[i] = new Vector2(uv[2 * i], uv[2 * i + 1]);
				byte g = (c != null) ? c[i] : cAll;
				vc[i] = new Color32(g, g, g, byte.MaxValue);
			}
			int[] tr = new int[t.Length];
			for (int i = 0; i < t.Length; i++)
			{
				tr[i] = t[i];
			}
			Mesh m = new Mesh();
			m.name = name;
			m.hideFlags = HideFlags.HideAndDontSave;
			m.vertices = vp;
			m.normals = vn;
			m.uv = vu;
			m.colors32 = vc;
			if (split > 0 && split < tr.Length)
			{
				int[] t0 = new int[split];
				int[] t1 = new int[tr.Length - split];
				Array.Copy(tr, 0, t0, 0, split);
				Array.Copy(tr, split, t1, 0, t1.Length);
				m.subMeshCount = 2;
				m.SetTriangles(t0, 0);
				m.SetTriangles(t1, 1);
			}
			else
			{
				m.triangles = tr;
			}
			m.RecalculateTangents();
			m.RecalculateBounds();
			m.UploadMeshData(true);
			return m;
		}

		private static int Slot(Renderer r, string name)
		{
			if (r == null)
			{
				return -1;
			}
			Material[] ms = r.sharedMaterials;
			for (int i = 0; i < ms.Length; i++)
			{
				if (ms[i] != null && ms[i].name.StartsWith(name, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		// the frame's two materials: structure (matte copy) for the inner faces, the skin for the outward ones; without a skin
		// the outward faces take the structure too (as 0.5.3)
		private static Material[] FrameMats(Material ms, Material mk)
		{
			Material a = Matte(ms);
			return new Material[] { a, mk ?? a };
		}

		private static Material Find(Renderer r, string name)
		{
			if (r == null)
			{
				return null;
			}
			Material[] ms = r.sharedMaterials;
			for (int i = 0; i < ms.Length; i++)
			{
				if (ms[i] != null && ms[i].name.StartsWith(name, StringComparison.Ordinal))
				{
					return ms[i];
				}
			}
			return null;
		}

		// Vortex 0.5.3: the frame's material is a matte copy of the fan part's aircraft_structure (Tom, 18 Nov, first look in
		// game: "the material looks too shiny, so the rim stands out more"). That material is the shader graph
		// "Shader Graphs/Lit_vertexcolor"; it has no smoothness value, only a metallic-smoothness texture
		// (aircraft_structure_m: R metallic, A smoothness, flagged sRGB). Where the box samples it, it averages metallic 0.53-0.67
		// and smoothness 0.35-0.59: polished metal, where the painted skin round the bay is about 0.03 and 0.19 (its own texture x
		// _Smoothness 0.5). The copy gets a flat texture of its own instead: metallic MatteMetal, smoothness MatteSmooth.
		// Colour, normal map, ambient occlusion and the vertex colours are the original's; only the shine changes. The vanes keep
		// jet_mechanical as it is (the fan's own stators use the same patch). One copy per source material, shared by all jets.
		internal const float MatteMetal = 0.55f;

		internal const float MatteSmooth = 0.2f;

		private const string MaskProp = "_MetallicSmoothness";

		private static Texture2D matteMask;

		private static readonly Dictionary<Material, Material> matte = new Dictionary<Material, Material>();

		private static Material Matte(Material src)
		{
			if (src == null || !src.HasProperty(MaskProp))
			{
				return src;
			}
			if (matte.TryGetValue(src, out Material m) && m != null)
			{
				return m;
			}
			if (matteMask == null)
			{
				// sRGB like the texture it stands in for, so the bytes read the same way (alpha is never converted)
				matteMask = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false, linear: false)
				{
					name = "VortexVaneBox_matte_m",
					hideFlags = HideFlags.HideAndDontSave,
					wrapMode = TextureWrapMode.Repeat,
					filterMode = FilterMode.Point
				};
				byte mb = (byte)Mathf.RoundToInt(MatteMetal * 255f);
				Color32[] px = new Color32[16];
				for (int i = 0; i < px.Length; i++)
				{
					px[i] = new Color32(mb, mb, mb, (byte)Mathf.RoundToInt(MatteSmooth * 255f));
				}
				matteMask.SetPixels32(px);
				matteMask.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			}
			// copies of materials that no longer exist go (a source destroyed with its airframe)
			List<Material> dead = null;
			foreach (KeyValuePair<Material, Material> kv in matte)
			{
				if (kv.Key == null)
				{
					(dead ?? (dead = new List<Material>())).Add(kv.Key);
				}
			}
			if (dead != null)
			{
				foreach (Material k in dead)
				{
					if (matte[k] != null)
					{
						UnityEngine.Object.Destroy(matte[k]);
					}
					matte.Remove(k);
				}
			}
			m = new Material(src)
			{
				name = src.name + " (VortexVaneBox matte)",
				hideFlags = HideFlags.HideAndDontSave
			};
			m.SetTexture(MaskProp, matteMask);
			matte[src] = m;
			return m;
		}

		// returns what happened, for the log
		public static string Attach(Aircraft a)
		{
			if (a == null)
			{
				return "no aircraft";
			}
			SwivelDuctSystem s = ((Component)(object)a).GetComponentInChildren<SwivelDuctSystem>(true);
			if (s == null)
			{
				return "no lift system on this jet";
			}
			Array fans = fLiftFans?.GetValue(s) as Array;
			object fan0 = (fans != null && fans.Length > 0) ? fans.GetValue(0) : null;
			FieldInfo fT = (fan0 != null) ? AccessTools.Field(fan0.GetType(), "thrustTransform") : null;
			Transform tt = fT?.GetValue(fan0) as Transform;
			if (tt == null || tt.parent == null || fDoors == null)
			{
				return "lift fan thrust transform not found";
			}
			Transform p = tt.parent;
			if (p.Find(RootName) != null)
			{
				return null;
			}
			MeshFilter mf = p.GetComponent<MeshFilter>();
			MeshRenderer mr = p.GetComponent<MeshRenderer>();
			if (mf == null || mr == null || mf.sharedMesh == null)
			{
				return "the fan thrust transform's parent (" + p.name + ") has no mesh";
			}
			// the box is sized on this mesh: on another one (a game update that remodels the fan bay) it is left out, not guessed
			if (mf.sharedMesh.vertexCount != FanMeshVerts)
			{
				return $"the fan part's mesh is not the measured one ({mf.sharedMesh.vertexCount} vertices, measured {FanMeshVerts}): not drawn";
			}
			Material ms = Find(mr, "aircraft_structure");
			Material mm = Find(mr, "jet_mechanical");
			int skinSlot = Slot(mr, "SmallFighter1_skin");
			Material mk = (skinSlot >= 0) ? mr.sharedMaterials[skinSlot] : null;
			if (ms == null || mm == null)
			{
				return $"the fan part's materials not found (structure {ms != null}, mechanical {mm != null}): not drawn";
			}
			if (frameMesh == null)
			{
				frameMesh = Build("VortexVaneBox_frame", FrameP, FrameN, FrameUV, FrameC, 0, FrameT, FrameSkinStart);
			}
			if (vaneMesh == null)
			{
				vaneMesh = new Mesh[VaneP.Length];
			}
			for (int i = 0; i < vaneMesh.Length; i++)
			{
				if (vaneMesh[i] == null)
				{
					vaneMesh[i] = Build("VortexVaneBox_vanes" + i, VaneP[i], VaneN[i], VaneUV[i], null, VaneC, VaneT[i]);
				}
			}
			GameObject root = new GameObject(RootName);
			root.layer = p.gameObject.layer;
			root.transform.SetParent(p, false);
			VaneBox vb = root.AddComponent<VaneBox>();
			vb.part = p;
			vb.fanMr = mr;
			vb.sds = s;
			vb.thrustT = tt;
			vb.matStruct = ms;
			vb.matMech = mm;
			vb.matSkin = mk;
			vb.skinSlot = skinSlot;
			vb.frameR = vb.Make(root.transform, "frame", frameMesh, Matte(ms), Vector3.zero);
			vb.frameR.sharedMaterials = FrameMats(ms, mk);
			for (int i = 0; i < HingeLocal.Length; i++)
			{
				vb.slatR[i] = vb.Make(root.transform, "vanes" + (i + 1), vaneMesh[PairMesh[i]], mm, HingeLocal[i]);
				vb.slatT[i] = vb.slatR[i].transform;
			}
			// the airframe's LOD group holds the fan part's renderer in its near level: the box joins it there, so it is switched
			// with the rest of the detailed airframe. Not found (another hierarchy): a small group of its own, culled when the box
			// is under 3 % of the screen's height.
			LODGroup[] groups = ((Component)(object)a).GetComponentsInChildren<LODGroup>(true);
			LODGroup up = p.GetComponentInParent<LODGroup>();
			int level = -1;
			LODGroup g = null;
			if (up != null && (level = LevelOf(up, mr)) >= 0)
			{
				g = up;
			}
			for (int i = 0; g == null && i < groups.Length; i++)
			{
				if ((level = LevelOf(groups[i], mr)) >= 0)
				{
					g = groups[i];
				}
			}
			string how;
			if (g != null)
			{
				LOD[] lods = g.GetLODs();
				List<Renderer> rs = new List<Renderer>(lods[level].renderers);
				rs.Add(vb.frameR);
				rs.AddRange(vb.slatR);
				lods[level].renderers = rs.ToArray();
				g.SetLODs(lods);
				vb.lod = g;
				how = $"in the airframe's LOD group (level {level} of {lods.Length}, with the fan part)";
			}
			else
			{
				LODGroup own = root.AddComponent<LODGroup>();
				List<Renderer> rs = new List<Renderer> { vb.frameR };
				rs.AddRange(vb.slatR);
				own.SetLODs(new LOD[] { new LOD(0.03f, rs.ToArray()) });
				own.RecalculateBounds();
				vb.lod = own;
				vb.ownLod = true;
				how = "in a LOD group of its own (the airframe's does not list the fan part)";
			}
			vb.Apply(true);
			int tris = FrameT.Length / 3;
			for (int i = 0; i < PairMesh.Length; i++)
			{
				tris += VaneT[PairMesh[i]].Length / 3;
			}
			// 0.5.2: the line also says what can be checked from a log alone: the part's scale (the meshes are in the part's
			// frame at scale 1) and whether the box is drawn right now
			Vector3 sc = p.lossyScale;
			string st = !Plugin.StovlVaneModel ? "off (StovlVaneModel = false)" : (!mr.enabled || mr.forceRenderingOff) ? "hidden (the fan part's renderer is off)" : vb.shown ? "shown" : "hidden until the fan doors open";
			return $"{HingeLocal.Length} vane pairs + frame on {p.name}, {tris} triangles, {how}; part scale ({sc.x:0.###}, {sc.y:0.###}, {sc.z:0.###}); {st}; frame material {(vb.frameR.sharedMaterials[0] != ms ? $"matte (metallic {MatteMetal:0.##}, smoothness {MatteSmooth:0.##})" : "the fan part's own (no " + MaskProp + " on it)")}, rim {(mk != null ? mk.name + " (slot " + skinSlot + ", the bay bevel's paint)" : "structure (the fan part's skin not found)")}";
		}

		internal const string RootName = "VortexVaneBox";

		private static int LevelOf(LODGroup g, Renderer r)
		{
			if (g == null)
			{
				return -1;
			}
			LOD[] lods = g.GetLODs();
			for (int i = 0; i < lods.Length; i++)
			{
				Renderer[] rs = lods[i].renderers;
				if (rs != null && Array.IndexOf(rs, r) >= 0)
				{
					return i;
				}
			}
			return -1;
		}

		private MeshRenderer Make(Transform parent, string name, Mesh mesh, Material mat, Vector3 at)
		{
			GameObject go = new GameObject(RootName + "_" + name);
			go.layer = parent.gameObject.layer;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = at;
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer r = go.AddComponent<MeshRenderer>();
			r.sharedMaterial = mat;
			r.shadowCastingMode = ShadowCastingMode.Off;
			r.receiveShadows = fanMr.receiveShadows;
			r.lightProbeUsage = fanMr.lightProbeUsage;
			r.reflectionProbeUsage = fanMr.reflectionProbeUsage;
			r.renderingLayerMask = fanMr.renderingLayerMask;
			r.motionVectorGenerationMode = fanMr.motionVectorGenerationMode;
			return r;
		}

		// the thrust line's angle from the fan's axis in the fan part's frame (deg, + = exhaust aft), from the transform the
		// game applies the fan's force along
		private float ThrustAngle()
		{
			Vector3 f = part.InverseTransformDirection(thrustT.forward);
			return Mathf.Atan2(f.z, f.y) * 57.29578f;
		}

		private void Apply(bool force)
		{
			bool vis = Plugin.StovlVaneModel && fanMr != null && fanMr.enabled && !fanMr.forceRenderingOff && sds != null && (float)fDoors.GetValue(sds) > 0.002f;
			if (vis != shown || force)
			{
				shown = vis;
				if (frameR != null)
				{
					frameR.enabled = vis;
				}
				for (int i = 0; i < slatR.Length; i++)
				{
					if (slatR[i] != null)
					{
						slatR[i].enabled = vis;
					}
				}
			}
			if (!vis || thrustT == null)
			{
				return;
			}
			float d = Mathf.Clamp(ThrustAngle() / Plugin.StovlVaneTurning, -70f, 70f);
			if (float.IsNaN(drawn) || Mathf.Abs(d - drawn) > 0.02f)
			{
				drawn = d;
				// about the hinge (the pair's own x axis): + turns the vanes' lower edges aft
				Quaternion q = Quaternion.AngleAxis(d, Vector3.right);
				for (int i = 0; i < slatT.Length; i++)
				{
					if (slatT[i] != null)
					{
						slatT[i].localRotation = q;
					}
				}
			}
		}

		private void LateUpdate()
		{
			if (part == null)
			{
				return;
			}
			Apply(false);
			// the fan part's materials may be swapped (a damage state, another mod): the box follows (checked twice a second)
			syncT += Time.deltaTime;
			if (syncT < 0.5f)
			{
				return;
			}
			syncT = 0f;
			Material ms = Find(fanMr, "aircraft_structure") ?? matStruct;
			Material mm = Find(fanMr, "jet_mechanical") ?? matMech;
			matStruct = ms;
			matMech = mm;
			Material mk = Find(fanMr, "SmallFighter1_skin");
			if (mk == null && skinSlot >= 0)
			{
				Material[] fm = fanMr.sharedMaterials;
				mk = (skinSlot < fm.Length && fm[skinSlot] != null) ? fm[skinSlot] : matSkin;
			}
			matSkin = mk ?? matSkin;
			if (frameR != null)
			{
				Material[] cur = frameR.sharedMaterials;
				Material a = Matte(ms);
				Material b = matSkin ?? a;
				if (cur.Length != 2 || cur[0] != a || cur[1] != b)
				{
					frameR.sharedMaterials = new Material[] { a, b };
				}
			}
			for (int i = 0; i < slatR.Length; i++)
			{
				if (slatR[i] != null && slatR[i].sharedMaterial != mm)
				{
					slatR[i].sharedMaterial = mm;
				}
			}
		}

		private void OnDestroy()
		{
			if (lod == null || ownLod)
			{
				return;
			}
			// out of the airframe's LOD group again (the fan part destroyed with the airframe still there)
			LOD[] lods;
			try
			{
				lods = lod.GetLODs();
			}
			catch (Exception)
			{
				return;
			}
			bool changed = false;
			for (int i = 0; i < lods.Length; i++)
			{
				Renderer[] rs = lods[i].renderers;
				if (rs == null)
				{
					continue;
				}
				List<Renderer> keep = new List<Renderer>(rs.Length);
				for (int j = 0; j < rs.Length; j++)
				{
					bool mine = rs[j] != null && ReferenceEquals(rs[j], frameR);
					for (int k = 0; !mine && rs[j] != null && k < slatR.Length; k++)
					{
						mine = ReferenceEquals(rs[j], slatR[k]);
					}
					if (!mine)
					{
						keep.Add(rs[j]);
					}
				}
				if (keep.Count != rs.Length)
				{
					lods[i].renderers = keep.ToArray();
					changed = true;
				}
			}
			if (changed)
			{
				lod.SetLODs(lods);
			}
		}
	}
}
