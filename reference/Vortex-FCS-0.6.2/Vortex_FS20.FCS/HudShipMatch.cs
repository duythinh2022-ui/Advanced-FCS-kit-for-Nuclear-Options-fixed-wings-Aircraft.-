using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex_FS20.FCS
{
	// Vortex 0.4.8 (Tom, 18 Nov 00:26: "when the ship speed matching is engaged, show the vanilla H inside the shield symbol on
	// the hud, make it flash when it is in the blends in region"): the game's own auto-hover icon (AutopilotIndicator.icon, the
	// H in the shield, which the game shows while its auto-hover is on) is shown while the STOVL laws fly the jet on a ship's
	// speed (StovlShipRelative, a ship within range). Steady once the reference is fully the ship's; flashing while it is
	// being blended in or out (the outer quarter of the range, the 3 s after a switch, a change of nearest ship). Green
	// always: the game's own orange above 27 m/s is its auto-hover's speed limit, which does not apply here. On the Vortex the
	// game's auto-hover is refused (the Flight Assist hold is the STOVL conversion), so the icon is otherwise never lit; on
	// any other jet, and with the STOVL laws off, the icon is the game's.
	internal static class HudShipMatch
	{
		private static readonly FieldInfo fIcon = AccessTools.Field(typeof(AutopilotIndicator), "icon");

		private static readonly FieldInfo fAircraft = AccessTools.Field(typeof(AutopilotIndicator), "aircraft");

		// (the indicators this hook has lit, so the game's own state is put back when it lets go)
		private static readonly HashSet<int> lit = new HashSet<int>();

		private static bool failLogged;

		// flash: half a second, half of it on
		private const float FlashPeriod = 0.5f;

		public static void Install(Harmony h, Action<string, Action> tryPatch)
		{
			tryPatch("AutopilotIndicator.Refresh (STOVL ship speed match icon)", delegate
			{
				h.Patch(AccessTools.Method(typeof(AutopilotIndicator), "Refresh"), null, new HarmonyMethod(typeof(HudShipMatch), "Postfix"));
			});
		}

		public static void Postfix(AutopilotIndicator __instance)
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
					Plugin.Log.LogWarning("Vortex FCS: STOVL ship speed match icon failed: " + ex.Message);
				}
			}
		}

		private static void Run(AutopilotIndicator ind)
		{
			Graphic icon = fIcon?.GetValue(ind) as Graphic;
			if (icon == null)
			{
				return;
			}
			Aircraft a = fAircraft?.GetValue(ind) as Aircraft;
			bool show = false;
			bool blending = false;
			if ((UnityEngine.Object)(object)a != null && Registry.IsF22(a))
			{
				FcsController c = Registry.Find(a);
				StovlSystem s = c?.stovl;
				if (s != null && s.owns && c.Engaged && !c.Failed && s.state == StovlState.On && !s.freestyle && s.hw is SwivelLink link && link.ShipFrameLive)
				{
					show = link.ShipFrameWeight > 0.001f;
					blending = link.ShipFrameBlending;
				}
			}
			int id = ind.GetInstanceID();
			if (!show)
			{
				if (lit.Remove(id))
				{
					// back to the game's own state
					bool game = false;
					if ((UnityEngine.Object)(object)a != null)
					{
						ControlsFilter cf = a.GetControlsFilter();
						game = cf != null && cf.IsAutoHoverEnabled();
					}
					icon.enabled = game;
				}
				return;
			}
			lit.Add(id);
			bool on = !blending || Mathf.Repeat(Time.unscaledTime, FlashPeriod) < 0.5f * FlashPeriod;
			if (icon.enabled != on)
			{
				icon.enabled = on;
			}
			icon.color = Color.green;
		}
	}
}
