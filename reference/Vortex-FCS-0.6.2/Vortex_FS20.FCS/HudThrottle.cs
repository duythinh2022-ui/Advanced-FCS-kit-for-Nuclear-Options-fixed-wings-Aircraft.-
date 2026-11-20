using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex_FS20.FCS
{
	// Vortex 0.1.5: the game's throttle gauge (HUD and cockpit alike: every ThrottleGauge) in STOVL mode. Its readout shows
	// what the lever commands there ("0 GS" in the zero-ground-speed detent, "+12 KT" / "-8 KT" in the hover regime,
	// "HOLD SPD" / "ACCEL" / "DECEL" at speed) instead of the dry-range percentage, and a tick marks the zero-ground-speed
	// detent on the arc (a copy of the gauge's own afterburner tick). Back to the game's readout and no tick in CTOL.
	internal static class HudThrottle
	{
		private static readonly FieldInfo fAircraft = AccessTools.Field(typeof(ThrottleGauge), "aircraft");

		private static readonly FieldInfo fReading = AccessTools.Field(typeof(ThrottleGauge), "throttleReading");

		private static readonly FieldInfo fBoundary = AccessTools.Field(typeof(ThrottleGauge), "throttleBoundaryPivot");

		private static readonly FieldInfo fPrev = AccessTools.Field(typeof(ThrottleGauge), "throttlePrev");

		private static readonly FieldInfo fLabel = AccessTools.Field(typeof(ThrottleGauge), "throttleLabel");

		// (Vortex 0.5.17: the HUD gauge's MIN label is up: the STOVL detent indicator, which goes under the same gauge label,
		// moves down a line for it)
		public static bool HudMinShown;

		private sealed class GaugeState
		{
			public GameObject tick;

			// Vortex 0.5.17: the minimum airspeed label under the gauge's own label (a copy of it)
			public GameObject minGo;

			public TextMeshProUGUI minLbl;

			public string minText;

			public bool shown;

			public string text;
		}

		private static readonly Dictionary<int, GaugeState> gauges = new Dictionary<int, GaugeState>();

		private static bool failLogged;

		public static void Install(Harmony h, Action<string, Action> tryPatch)
		{
			tryPatch("ThrottleGauge.Refresh (STOVL throttle readout)", delegate
			{
				h.Patch(AccessTools.Method(typeof(ThrottleGauge), "Refresh"), null, new HarmonyMethod(typeof(HudThrottle), "Postfix"));
			});
		}

		public static void Postfix(ThrottleGauge __instance)
		{
			try
			{
				Run(__instance);
			}
			catch (Exception ex)
			{
				if (!failLogged)
				{
					failLogged = true;
					Plugin.Log.LogWarning("Vortex FCS: STOVL throttle readout failed: " + ex.Message);
				}
			}
		}

		private static void Run(ThrottleGauge g)
		{
			Aircraft a = fAircraft?.GetValue(g) as Aircraft;
			string text = null;
			float minKt = 0f;
			if ((UnityEngine.Object)(object)a != null && Registry.IsF22(a))
			{
				FcsController c = Registry.Find(a);
				StovlSystem s = c?.stovl;
				if (s != null && s.owns && c.Engaged && !c.Failed && s.state == StovlState.On && !s.freestyle)
				{
					text = s.hudText ?? "0 GS";
				}
				if (s != null && c.Engaged && !c.Failed && !s.freestyle)
				{
					minKt = s.MinAirspeedKt;
				}
			}
			int id = g.GetInstanceID();
			if (!gauges.TryGetValue(id, out var gs))
			{
				if (text == null && minKt <= 0f)
				{
					return;
				}
				gs = new GaugeState();
				gauges[id] = gs;
			}
			MinLabel(g, gs, minKt);
			TextMeshProUGUI reading = fReading?.GetValue(g) as TextMeshProUGUI;
			if (text == null)
			{
				if (gs.shown)
				{
					// back to the game's readout: its next Refresh redraws (throttlePrev forced stale)
					gs.shown = false;
					gs.text = null;
					fPrev?.SetValue(g, -1f);
					if (gs.tick != null)
					{
						gs.tick.SetActive(false);
					}
				}
				return;
			}
			gs.shown = true;
			if (reading != null && reading.text != text)
			{
				reading.text = text;
			}
			gs.text = text;
			// the zero-ground-speed tick: the gauge's own afterburner tick, copied and turned to the detent (the game turns the
			// reading pivot by throttle x 26 - 13 deg)
			Transform bp = fBoundary?.GetValue(g) as Transform;
			if (gs.tick == null && bp != null && bp.parent != null)
			{
				gs.tick = UnityEngine.Object.Instantiate(bp.gameObject, bp.parent);
				gs.tick.name = "VortexFCS_ZeroGsTick";
			}
			if (gs.tick != null)
			{
				gs.tick.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Clamp01(Plugin.StovlAftDetent) * 26f - 13f);
				bool vis = bp != null && bp.gameObject.activeInHierarchy;
				if (gs.tick.activeSelf != vis)
				{
					gs.tick.SetActive(vis);
				}
			}
		}

		// Vortex 0.5.17 (Tom, 20 Nov: "also the cockpit hud, can you add min airspeed to the throttle scale side"): while the jet
		// is too heavy to hover, every throttle gauge (the HUD's and the cockpit's) shows the minimum airspeed the speed floor
		// holds, "MIN 50 KT", one line under the gauge's own label, in its style (the slot the STOVL detent indicator uses;
		// that one moves down a line). The text is rebuilt only when the number changes.
		private static void MinLabel(ThrottleGauge g, GaugeState gs, float minKt)
		{
			TextMeshProUGUI src = fLabel?.GetValue(g) as TextMeshProUGUI;
			bool want = minKt > 0f && src != null;
			bool isHud = SceneSingleton<FlightHud>.i != null && ((Component)(object)g).transform.IsChildOf(((Component)(object)SceneSingleton<FlightHud>.i).transform);
			if (!want)
			{
				if (gs.minGo != null && gs.minGo.activeSelf)
				{
					gs.minGo.SetActive(false);
				}
				if (isHud)
				{
					HudMinShown = false;
				}
				return;
			}
			if (gs.minGo == null || gs.minLbl == null)
			{
				gs.minGo = UnityEngine.Object.Instantiate(src.gameObject, src.transform.parent);
				gs.minGo.name = "VortexFCS_MinAirspeed";
				gs.minLbl = gs.minGo.GetComponent<TextMeshProUGUI>();
				foreach (MonoBehaviour mb in gs.minGo.GetComponents<MonoBehaviour>())
				{
					if (mb != null && !(mb is Graphic))
					{
						UnityEngine.Object.Destroy(mb);
					}
				}
				for (int i = gs.minGo.transform.childCount - 1; i >= 0; i--)
				{
					UnityEngine.Object.Destroy(gs.minGo.transform.GetChild(i).gameObject);
				}
				if (gs.minLbl == null)
				{
					UnityEngine.Object.Destroy(gs.minGo);
					gs.minGo = null;
					return;
				}
				gs.minLbl.raycastTarget = false;
				gs.minLbl.richText = false;
				gs.minLbl.enableWordWrapping = false;
				gs.minText = null;
			}
			string t = "MIN " + Mathf.RoundToInt(minKt) + " KT";
			if (gs.minText != t)
			{
				gs.minLbl.text = t;
				gs.minText = t;
			}
			gs.minLbl.font = src.font;
			gs.minLbl.fontSharedMaterial = src.fontSharedMaterial;
			gs.minLbl.color = src.color;
			gs.minLbl.fontStyle = src.fontStyle;
			// (the detent indicator's size and line step, so the two stack: 65 % of the gauge label's size)
			gs.minLbl.fontSize = Mathf.Max(12f, src.fontSize * 0.65f);
			gs.minLbl.alignment = src.alignment;
			RectTransform sr = src.rectTransform;
			RectTransform mr = gs.minLbl.rectTransform;
			mr.anchorMin = sr.anchorMin;
			mr.anchorMax = sr.anchorMax;
			mr.pivot = sr.pivot;
			mr.sizeDelta = new Vector2(Mathf.Max(sr.sizeDelta.x, src.fontSize * 7f), sr.sizeDelta.y);
			mr.localRotation = sr.localRotation;
			mr.localScale = sr.localScale;
			mr.anchoredPosition = sr.anchoredPosition + new Vector2(0f, 0f - Mathf.Max(22f, src.fontSize * 0.65f));
			bool vis = src.gameObject.activeInHierarchy;
			if (gs.minGo.activeSelf != vis)
			{
				gs.minGo.SetActive(vis);
			}
			if (isHud)
			{
				HudMinShown = vis;
			}
		}
	}
}
