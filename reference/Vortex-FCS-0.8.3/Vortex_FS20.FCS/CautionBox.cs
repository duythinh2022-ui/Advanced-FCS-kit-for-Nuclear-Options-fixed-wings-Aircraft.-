using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex_FS20.FCS
{
	// Vortex 0.5.16 (Tom, 19 Nov: "i need it to be the dialog on top of the screen like the stovl conv messages"; "then make a
	// mod built into the fcs mod that allows that"; "ofc no message overlapping"): persistent HUD cautions.
	//
	// The game's own message line (AircraftActionsReport) cannot hold a message for as long as a condition lasts: ReportText
	// appends a line and starts a timer, and each timer deletes the FIRST line of the text when it runs out, whichever message
	// that is. A long-lived line would be deleted by the next 4 s message's timer.
	//
	// So the FCS keeps a box of its own: a copy of the game's box (its background image and its text label, cloned, so font,
	// size and background are the game's) placed directly under it. A caution is a line in that box for as long as its owner
	// says it is on, and it is gone the frame the owner says it is off.
	//   - Set(id, text, on): called by a caution's owner every frame. One line per id, in the order they were raised.
	//   - The box is placed every frame from the game box's rendered bounds (its rectangle and its text's bounds, whichever
	//     reaches lower), one line's height below it, so it moves out of the way as the game's box grows; while the game's box
	//     is empty it takes that box's place.
	//   - Shown only while a caution is on and the game's own box is in the hierarchy (it follows the game's HUD on / off).
	//   - Rebuilt when the HUD is rebuilt (new mission, aircraft change); the text is rebuilt only when it changes.
	// Plugin.CautionBoxOn false = no box (the owners then send one ordinary message at onset). Plugin.CautionBoxColor: the
	// game label's colour (default) or amber.
	internal static class CautionBox
	{
		private static readonly FieldInfo fBackground = AccessTools.Field(typeof(AircraftActionsReport), "background");

		private static readonly FieldInfo fText = AccessTools.Field(typeof(AircraftActionsReport), "actionsText");

		private struct Entry
		{
			public string id;

			public string text;
		}

		private static readonly List<Entry> entries = new List<Entry>(4);

		private static readonly StringBuilder sb = new StringBuilder(128);

		private static bool dirty;

		// the game's box this copy was made from, and the copy
		private static AircraftActionsReport rep;

		private static Image srcBg;

		private static TextMeshProUGUI srcTxt;

		private static GameObject root;

		private static RectTransform rootRt;

		private static GameObject txtGo;

		private static Image bg;

		private static TextMeshProUGUI txt;

		private static readonly Vector3[] corners = new Vector3[4];

		private static bool failLogged;

		private static readonly Color Amber = new Color(1f, 0.75f, 0f, 1f);

		// Vortex 0.5.17 (Tom, 20 Nov: "also the message overlaps the hud", screenshots: the game's bordered heading box, the
		// Bearing HUD app, sits directly under the message line): the game's HUD apps on the same canvas, each with its drawn
		// parts, rescanned every few seconds (apps come and go with the HUD settings and the aircraft); a small one in the
		// caution box's column pushes the box below it. Big ones (the pitch ladder, the tapes) are not boxes to step round and
		// are left out by size.
		private sealed class AppRef
		{
			public HUDApp app;

			public Graphic[] parts;
		}

		private static readonly List<AppRef> apps = new List<AppRef>(32);

		private static float nextScan;

		private const float ScanEvery = 3f;

		// a caution's owner calls this every frame (text may change while it is on)
		public static void Set(string id, string text, bool on)
		{
			int at = -1;
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].id == id)
				{
					at = i;
					break;
				}
			}
			if (!on)
			{
				if (at >= 0)
				{
					entries.RemoveAt(at);
					dirty = true;
				}
				return;
			}
			if (at < 0)
			{
				entries.Add(new Entry
				{
					id = id,
					text = text
				});
				dirty = true;
			}
			else if (!string.Equals(entries[at].text, text, StringComparison.Ordinal))
			{
				entries[at] = new Entry
				{
					id = id,
					text = text
				};
				dirty = true;
			}
		}

		public static void ClearAll()
		{
			if (entries.Count > 0)
			{
				entries.Clear();
				dirty = true;
			}
		}

		public static bool Any => entries.Count > 0;

		// once a frame, after the owners' Set calls (the plugin's LateUpdate)
		public static void Tick()
		{
			try
			{
				Run();
			}
			catch (Exception ex)
			{
				if (!failLogged)
				{
					failLogged = true;
					Plugin.Log.LogWarning("Vortex FCS: caution box failed: " + ex.Message);
				}
				Hide();
			}
		}

		private static void Hide()
		{
			if (root != null && root.activeSelf)
			{
				root.SetActive(false);
			}
		}

		private static void Run()
		{
			if (!Plugin.CautionBoxOn || entries.Count == 0)
			{
				Hide();
				return;
			}
			AircraftActionsReport r = SceneSingleton<AircraftActionsReport>.i;
			if ((UnityEngine.Object)(object)r == null || !Ensure(r))
			{
				Hide();
				return;
			}
			// with the game's own box out of the hierarchy (HUD off, a view the game hides it in) the caution box is hidden too
			if (!srcTxt.gameObject.activeInHierarchy || !srcTxt.enabled)
			{
				Hide();
				return;
			}
			if (dirty)
			{
				dirty = false;
				sb.Length = 0;
				for (int i = 0; i < entries.Count; i++)
				{
					if (i > 0)
					{
						sb.Append('\n');
					}
					sb.Append(entries[i].text);
				}
				txt.text = sb.ToString();
			}
			// the look follows the game's label every frame (HUD colour and size settings can change in flight)
			txt.font = srcTxt.font;
			txt.fontSharedMaterial = srcTxt.fontSharedMaterial;
			txt.fontSize = srcTxt.fontSize;
			txt.fontStyle = srcTxt.fontStyle;
			txt.alignment = srcTxt.alignment;
			Color c = srcTxt.color;
			if (Plugin.CautionBoxColor == CautionColor.Amber)
			{
				c = new Color(Amber.r, Amber.g, Amber.b, c.a);
			}
			txt.color = c;
			if (bg != null && srcBg != null)
			{
				bg.sprite = srcBg.sprite;
				bg.color = srcBg.color;
				bg.material = srcBg.material;
				bg.enabled = true;
			}
			if (!root.activeSelf)
			{
				root.SetActive(true);
			}
			Place();
		}

		// lowest and highest world y of a rectangle
		private static void SpanY(RectTransform rt, out float lo, out float hi)
		{
			rt.GetWorldCorners(corners);
			lo = Mathf.Min(Mathf.Min(corners[0].y, corners[1].y), Mathf.Min(corners[2].y, corners[3].y));
			hi = Mathf.Max(Mathf.Max(corners[0].y, corners[1].y), Mathf.Max(corners[2].y, corners[3].y));
		}

		// the union of an app's drawn parts this frame (world), false if nothing of it is drawn
		private static bool AppBounds(AppRef a, out float xLo, out float xHi, out float yLo, out float yHi)
		{
			xLo = float.MaxValue;
			xHi = float.MinValue;
			yLo = float.MaxValue;
			yHi = float.MinValue;
			if ((UnityEngine.Object)(object)a.app == null || !a.app.isActiveAndEnabled)
			{
				return false;
			}
			bool any = false;
			for (int i = 0; i < a.parts.Length; i++)
			{
				Graphic g = a.parts[i];
				if (g == null || !g.isActiveAndEnabled || g.color.a <= 0.01f)
				{
					continue;
				}
				if (g is TMP_Text t && (t.text == null || t.text.Length == 0))
				{
					continue;
				}
				g.rectTransform.GetWorldCorners(corners);
				for (int k = 0; k < 4; k++)
				{
					xLo = Mathf.Min(xLo, corners[k].x);
					xHi = Mathf.Max(xHi, corners[k].x);
					yLo = Mathf.Min(yLo, corners[k].y);
					yHi = Mathf.Max(yHi, corners[k].y);
				}
				any = true;
			}
			return any;
		}

		private static void ScanApps()
		{
			if (Time.unscaledTime < nextScan)
			{
				return;
			}
			nextScan = Time.unscaledTime + ScanEvery;
			apps.Clear();
			Canvas root = (srcTxt != null && srcTxt.canvas != null) ? srcTxt.canvas.rootCanvas : null;
			if (root == null)
			{
				return;
			}
			HUDApp[] all = UnityEngine.Object.FindObjectsOfType<HUDApp>();
			for (int i = 0; i < all.Length; i++)
			{
				HUDApp a = all[i];
				if ((UnityEngine.Object)(object)a == null)
				{
					continue;
				}
				Canvas c = a.GetComponentInParent<Canvas>();
				if (c == null || c.rootCanvas != root)
				{
					continue;
				}
				apps.Add(new AppRef
				{
					app = a,
					parts = a.GetComponentsInChildren<Graphic>(true)
				});
			}
		}

		private static void Place()
		{
			// the game's box: its background rectangle when there is one, else its label's; and how far down its text reaches
			RectTransform srcRt = ((srcBg != null) ? srcBg.rectTransform : srcTxt.rectTransform);
			SpanY(srcRt, out float gLo, out float gHi);
			bool gameShown = srcTxt.text != null && srcTxt.text.Length > 0;
			float line = Mathf.Max(1f, srcTxt.fontSize * Mathf.Abs(srcTxt.transform.lossyScale.y));
			float topWant;
			if (gameShown)
			{
				float tLo = gLo;
				// (the text's rendered bounds, in case the background does not follow the number of lines)
				Bounds tb = srcTxt.textBounds;
				if (tb.size.y > 0f)
				{
					tLo = Mathf.Min(tLo, srcTxt.transform.TransformPoint(tb.min).y);
				}
				topWant = tLo - line;
			}
			else
			{
				// the game's box is empty: the caution box takes its place
				topWant = gHi;
			}
			// the copy starts each frame from the game box's own place (its position follows the HUD's layout) and is then moved
			// down by what its own top is above where it should be
			rootRt.position = srcRt.position;
			SpanY(rootRt, out float _, out float cHi);
			if (txt != null)
			{
				Bounds cb = txt.textBounds;
				if (cb.size.y > 0f)
				{
					cHi = Mathf.Max(cHi, txt.transform.TransformPoint(cb.max).y);
				}
			}
			Vector3 p = rootRt.position;
			p.y += topWant - cHi;
			rootRt.position = p;
			// (0.5.17) step below any small HUD app in the box's column, as many times as it takes (stacked apps)
			ScanApps();
			if (apps.Count == 0)
			{
				return;
			}
			Canvas rc = srcTxt.canvas.rootCanvas;
			RectTransform crt = rc.transform as RectTransform;
			float canvasW = float.MaxValue;
			if (crt != null)
			{
				crt.GetWorldCorners(corners);
				canvasW = Mathf.Abs(corners[2].x - corners[0].x);
			}
			for (int pass = 0; pass < 4; pass++)
			{
				SpanY(rootRt, out float bLo, out float bHi);
				rootRt.GetWorldCorners(corners);
				float bxLo = Mathf.Min(corners[0].x, corners[2].x);
				float bxHi = Mathf.Max(corners[0].x, corners[2].x);
				if (txt != null)
				{
					Bounds tb2 = txt.textBounds;
					if (tb2.size.y > 0f)
					{
						bLo = Mathf.Min(bLo, txt.transform.TransformPoint(tb2.min).y);
						bHi = Mathf.Max(bHi, txt.transform.TransformPoint(tb2.max).y);
					}
				}
				float drop = 0f;
				for (int i = 0; i < apps.Count; i++)
				{
					if (!AppBounds(apps[i], out float axLo, out float axHi, out float ayLo, out float ayHi))
					{
						continue;
					}
					// (not a box: the ladder, the tapes)
					if (ayHi - ayLo > 4f * line || axHi - axLo > 0.4f * canvasW)
					{
						continue;
					}
					// (above the game's box: not in the way of a box that only ever goes down)
					if (ayHi > gHi + 0.5f * line)
					{
						continue;
					}
					if (axHi <= bxLo || axLo >= bxHi || ayLo >= bHi || ayHi <= bLo)
					{
						continue;
					}
					drop = Mathf.Max(drop, bHi - (ayLo - 0.5f * line));
				}
				if (drop <= 0f)
				{
					break;
				}
				p = rootRt.position;
				p.y -= drop;
				rootRt.position = p;
			}
		}

		private static bool Ensure(AircraftActionsReport r)
		{
			if ((UnityEngine.Object)(object)rep == (UnityEngine.Object)(object)r && root != null && txt != null && srcTxt != null)
			{
				return true;
			}
			if (root != null)
			{
				UnityEngine.Object.Destroy(root);
			}
			root = null;
			rootRt = null;
			txtGo = null;
			bg = null;
			txt = null;
			rep = r;
			dirty = true;
			srcBg = fBackground?.GetValue(r) as Image;
			srcTxt = fText?.GetValue(r) as TextMeshProUGUI;
			if (srcTxt == null)
			{
				return false;
			}
			// what to copy: the background with the label under it when the label is its child; the label with the background
			// under it the other way round; else the two separately under a holder of our own next to the game's
			Transform tT = srcTxt.transform;
			Transform tB = (srcBg != null) ? srcBg.transform : null;
			GameObject srcRoot;
			if (tB != null && tT.IsChildOf(tB))
			{
				srcRoot = tB.gameObject;
			}
			else if (tB != null && tB.IsChildOf(tT))
			{
				srcRoot = tT.gameObject;
			}
			else
			{
				srcRoot = null;
			}
			// copies are made under an inactive holder so nothing on them wakes up (the game's AircraftActionsReport component,
			// a scene singleton, may sit on what is copied: it is taken off the copy before the copy is switched on)
			GameObject holder = new GameObject("VortexFCS_CautionBoxHolder");
			holder.SetActive(false);
			try
			{
				if (srcRoot != null)
				{
					Transform parent = srcRoot.transform.parent;
					if (parent == null)
					{
						return false;
					}
					holder.transform.SetParent(parent, worldPositionStays: false);
					root = UnityEngine.Object.Instantiate(srcRoot, holder.transform);
					Strip(root);
					root.transform.SetParent(parent, worldPositionStays: false);
					CopyRect(srcRoot.transform as RectTransform, root.transform as RectTransform);
				}
				else
				{
					Transform parent = (tB != null) ? tB.parent : tT.parent;
					if (parent == null)
					{
						return false;
					}
					holder.transform.SetParent(parent, worldPositionStays: false);
					RectTransform frame = (tB != null) ? (tB as RectTransform) : (tT as RectTransform);
					root = new GameObject("VortexFCS_CautionBox", typeof(RectTransform));
					root.transform.SetParent(holder.transform, worldPositionStays: false);
					if (tB != null)
					{
						GameObject b = UnityEngine.Object.Instantiate(tB.gameObject, root.transform);
						Strip(b);
						Fill(b.transform as RectTransform);
					}
					GameObject t = UnityEngine.Object.Instantiate(tT.gameObject, root.transform);
					Strip(t);
					if (tB != null && tT.parent == tB.parent)
					{
						// (the label keeps its place relative to the background)
						RectTransform trt = t.transform as RectTransform;
						CopyRect(tT as RectTransform, trt);
						trt.anchoredPosition = (tT as RectTransform).anchoredPosition - frame.anchoredPosition;
					}
					else
					{
						Fill(t.transform as RectTransform);
					}
					root.transform.SetParent(parent, worldPositionStays: false);
					CopyRect(frame, root.transform as RectTransform);
				}
			}
			finally
			{
				UnityEngine.Object.Destroy(holder);
			}
			root.name = "VortexFCS_CautionBox";
			root.SetActive(false);
			rootRt = root.transform as RectTransform;
			if (rootRt == null)
			{
				UnityEngine.Object.Destroy(root);
				root = null;
				return false;
			}
			txt = root.GetComponentInChildren<TextMeshProUGUI>(true);
			if (txt == null)
			{
				UnityEngine.Object.Destroy(root);
				root = null;
				return false;
			}
			txtGo = txt.gameObject;
			txtGo.SetActive(true);
			txt.enabled = true;
			txt.raycastTarget = false;
			txt.richText = false;
			txt.text = string.Empty;
			foreach (Image im in root.GetComponentsInChildren<Image>(true))
			{
				im.raycastTarget = false;
				if (bg == null)
				{
					bg = im;
				}
			}
			if (bg != null)
			{
				bg.gameObject.SetActive(true);
			}
			// drawn after the game's own box
			root.transform.SetAsLastSibling();
			return true;
		}

		// the copy is a picture only: every script on it that is not part of drawing or laying it out comes off
		private static void Strip(GameObject go)
		{
			foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (mb == null || mb is Graphic || mb is LayoutGroup || mb is ContentSizeFitter || mb is LayoutElement)
				{
					continue;
				}
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}

		private static void CopyRect(RectTransform from, RectTransform to)
		{
			if (from == null || to == null)
			{
				return;
			}
			to.anchorMin = from.anchorMin;
			to.anchorMax = from.anchorMax;
			to.pivot = from.pivot;
			to.sizeDelta = from.sizeDelta;
			to.anchoredPosition = from.anchoredPosition;
			to.localRotation = from.localRotation;
			to.localScale = from.localScale;
		}

		private static void Fill(RectTransform rt)
		{
			if (rt == null)
			{
				return;
			}
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = Vector2.zero;
			rt.anchoredPosition = Vector2.zero;
			rt.localScale = Vector3.one;
		}
	}
}
