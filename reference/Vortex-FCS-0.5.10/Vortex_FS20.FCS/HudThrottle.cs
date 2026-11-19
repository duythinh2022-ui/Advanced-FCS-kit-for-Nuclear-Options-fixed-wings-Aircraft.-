using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

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

		private sealed class GaugeState
		{
			public GameObject tick;

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
			if ((UnityEngine.Object)(object)a != null && Registry.IsF22(a))
			{
				FcsController c = Registry.Find(a);
				StovlSystem s = c?.stovl;
				if (s != null && s.owns && c.Engaged && !c.Failed && s.state == StovlState.On && !s.freestyle)
				{
					text = s.hudText ?? "0 GS";
				}
			}
			int id = g.GetInstanceID();
			if (!gauges.TryGetValue(id, out var gs))
			{
				if (text == null)
				{
					return;
				}
				gs = new GaugeState();
				gauges[id] = gs;
			}
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
	}
}
