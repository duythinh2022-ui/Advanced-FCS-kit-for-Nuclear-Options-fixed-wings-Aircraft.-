using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex_FS20.FCS
{
	// Vortex 0.1.2 (StovlThrottleDetentHold): a hold detent at the STOVL zero-ground-speed throttle (the aft detent,
	// StovlAftDetent), for the keyboard / button throttle, in STOVL mode only. It works as baanish's Nuclear Option Detents
	// mod (0.5.1) does for its idle and afterburner detents: a relative throttle running into the detent stops just short of
	// it on the side it came from (0.0001); holding the Increase / Decrease key StovlThrottleDetentHold ms carries it through,
	// letting go earlier resets the hold; once through, the detent re-arms when the throttle has moved 0.02 away from it.
	// The HUD shows "STOP HOLD n%" while held and "STOP LOCK" while parked on it, in the throttle label's style, under the
	// throttle gauge (one line lower if the Detents mod's own indicator is showing).
	//
	// Living with the Detents mod: this is a postfix on the same method it observes (PilotPlayerState.
	// PlayerThrottleAxis1Controls), at normal priority, so it runs before the Detents mod's last-priority postfix. When it
	// holds it writes the public throttle (ControlInputs.throttle) and the game's integrator (simulatedThrottle) as the same
	// position, so the Detents mod's foreign-control test (the two disagreeing) never trips and its idle / afterburner holds
	// keep working; when it does not hold it changes nothing. If the Detents mod has its own custom profile for the FS-20
	// enabled with dry detents, this one stands aside (the pilot has set his own there; two holds would stack).
	// Analog throttle axes are left alone, as the Detents mod does: there the FCS's +-StovlDetentWidth band is the detent.
	internal static class ThrottleDetent
	{
		private static readonly AccessTools.FieldRef<PilotPlayerState, Player> fPlayer = AccessTools.FieldRefAccess<PilotPlayerState, Player>("player");

		private static readonly AccessTools.FieldRef<PilotPlayerState, float> fSim = AccessTools.FieldRefAccess<PilotPlayerState, float>("simulatedThrottle");

		private static readonly AccessTools.FieldRef<PilotPlayerState, bool> fCollective = AccessTools.FieldRefAccess<PilotPlayerState, bool>("collective");

		private static readonly AccessTools.FieldRef<PilotBaseState, ControlInputs> fInputs = AccessTools.FieldRefAccess<PilotBaseState, ControlInputs>("controlInputs");

		private static readonly AccessTools.FieldRef<PilotBaseState, Pilot> fPilot = AccessTools.FieldRefAccess<PilotBaseState, Pilot>("pilot");

		private const float Inward = 0.0001f;

		private const float Rearm = 0.02f;

		private static bool hasLast;

		private static float last;

		private static bool holding;

		private static int holdDir;

		private static float holdT;

		private static bool unlocked;

		private static bool parked;

		private static bool active;

		private static float otherCheckT = -99f;

		private static bool otherOwns;

		public static void Install(Harmony h, Action<string, Action> tryPatch)
		{
			harmony = h;
			tryPatch("PilotPlayerState.PlayerThrottleAxis1Controls (STOVL throttle detent)", delegate
			{
				h.Patch(AccessTools.Method(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls"), null, new HarmonyMethod(typeof(ThrottleDetent), "Postfix"));
			});
			TryHookDetents();
		}

		// Vortex 0.2.1: the Detents mod (com.baanish.nuclearoption.detents) stands its idle and afterburner detents down while
		// the game's auto-hover is on, reading Aircraft.IsAutoHoverEnabled() through a delegate it keeps in
		// RuntimeCompatibility.AircraftAutoHoverEnabled. In STOVL there is no afterburner (dry thrust only) and the lever's
		// bottom is the backward range, so its afterburner and idle stops only get in the way: this wraps that delegate so it
		// also reads true while the Vortex's STOVL laws own the jet. The game's own IsAutoHoverEnabled is not touched (the
		// game's swivel system enters its Hover mode on it). Hooked at install, or on the first throttle frame if the Detents
		// assembly loads after this one.
		private static Harmony harmony;

		private static bool detentsHookDone;

		private static Func<Aircraft, bool> detentsOrig;

		private static Func<Aircraft, bool> detentsWrapped;

		private static void TryHookDetents()
		{
			if (detentsHookDone || harmony == null)
			{
				return;
			}
			try
			{
				Type rc = AccessTools.TypeByName("NuclearOptionDetents.Compatibility.RuntimeCompatibility");
				if (rc == null)
				{
					return;
				}
				detentsHookDone = true;
				System.Reflection.PropertyInfo pi = AccessTools.Property(rc, "AircraftAutoHoverEnabled");
				if (pi == null || pi.PropertyType != typeof(Func<Aircraft, bool>) || pi.GetGetMethod(true) == null)
				{
					Plugin.Log.LogInfo("Vortex FCS: the Detents mod's auto-hover accessor has a different shape (" + ((pi == null) ? "missing" : pi.PropertyType.Name) + "); its detents stay on in STOVL.");
					return;
				}
				harmony.Patch(pi.GetGetMethod(true), null, new HarmonyMethod(typeof(ThrottleDetent), "DetentsHoverPostfix"));
				Plugin.Log.LogInfo("Vortex FCS: the Detents mod's idle / afterburner detents stand down while the STOVL laws fly (its auto-hover suspend).");
			}
			catch (Exception ex)
			{
				detentsHookDone = true;
				Plugin.Log.LogWarning("Vortex FCS: Detents mod STOVL suspend not hooked: " + ex.Message);
			}
		}

		public static void DetentsHoverPostfix(ref Func<Aircraft, bool> __result)
		{
			if (__result == null || (object)__result == detentsWrapped)
			{
				return;
			}
			if ((object)__result != detentsOrig || detentsWrapped == null)
			{
				Func<Aircraft, bool> orig = __result;
				detentsOrig = orig;
				detentsWrapped = (Aircraft a) => StovlSuspendsDetents(a) || orig(a);
			}
			__result = detentsWrapped;
		}

		// the Detents mod's afterburner detent for the FS-20 (its built-in preset: 0.90), less its 0.02 reset hysteresis and a margin
		internal static float DetentsAbRelease = 0.86f;

		private static bool StovlSuspendsDetents(Aircraft a)
		{
			try
			{
				if ((UnityEngine.Object)(object)a == null || !Registry.IsF22(a))
				{
					return false;
				}
				FcsController c = Registry.Find(a);
				StovlSystem s = c?.stovl;
				if (s == null)
				{
					return false;
				}
				if (s.owns && c.Engaged && !c.Failed && s.state != StovlState.Off && !s.freestyle)
				{
					s.detentsAbLatch = true;
					return true;
				}
				// Vortex 0.3.6 (Tom, 17 Nov, 14:21 log 405 s): back in CTOL with the lever still in the afterburner range, the Detents
				// mod comes out of its suspend with its afterburner detent reset to locked, so the next throttle-up press held the
				// throttle at its 90 % boundary (1.00 -> 0.90, then back up after the 200 ms hold). The suspend is kept until the
				// lever is under DetentsAbRelease, so its detent re-arms on the side it guards.
				if (s.detentsAbLatch)
				{
					ControlInputs ci = a.GetInputs();
					if (ci != null && Mathf.Clamp01(ci.throttle) >= DetentsAbRelease)
					{
						return true;
					}
					s.detentsAbLatch = false;
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		private static void Reset()
		{
			hasLast = false;
			holding = false;
			holdT = 0f;
			unlocked = false;
			parked = false;
		}

		public static void Postfix(PilotPlayerState __instance)
		{
			try
			{
				Run(__instance);
			}
			catch (Exception ex)
			{
				Reset();
				active = false;
				Indicator.Hide();
				if (!failLogged)
				{
					failLogged = true;
					Plugin.Log.LogWarning("Vortex FCS: STOVL throttle detent failed open: " + ex.Message);
				}
			}
		}

		private static bool failLogged;

		private static string lastWhy = "-";

		private static string DescribeSources(Player p)
		{
			try
			{
				var src = p.GetCurrentInputSources("Throttle");
				if (src == null || src.Count == 0)
				{
					return "none active";
				}
				var parts = new System.Collections.Generic.List<string>();
				for (int i = 0; i < src.Count; i++)
				{
					InputActionSourceData d = src[i];
					parts.Add(d.controllerType + (d.actionElementMap != null ? (" " + d.actionElementMap.elementType + " " + d.actionElementMap.elementIdentifierName) : ""));
				}
				return string.Join(", ", parts);
			}
			catch
			{
				return "?";
			}
		}

		private static void Run(PilotPlayerState st)
		{
			if (!detentsHookDone)
			{
				TryHookDetents();
			}
			ControlInputs ci = fInputs(st);
			Pilot pilot = fPilot(st);
			Aircraft a = (pilot != null) ? pilot.aircraft : null;
			Player player = fPlayer(st);
			float holdMs = Plugin.StovlThrottleDetentHold;
			// 0.1.3: no PlayerSettings.throttleUseRelative test (0.1.2 had one and so never held with the game's absolute
			// throttle setting): a keyboard / button throttle integrates the same way in both, and the Detents mod takes any
			// digital throttle as relative too
			bool stovl = ci != null && player != null && (UnityEngine.Object)(object)a != null && InStovl(a);
			bool on = false;
			string why = null;
			if (stovl)
			{
				if (holdMs <= 0f)
				{
					why = "StovlThrottleDetentHoldMs is 0";
				}
				else if (fCollective(st))
				{
					why = "the game flies this jet's throttle as a collective";
				}
				else if (!Digital(player))
				{
					why = "the throttle has an analog axis bound (" + DescribeSources(player) + "); only keyboard / button throttles are held, as in the Detents mod";
				}
				else if (OtherModOwns())
				{
					why = "the Detents mod's own FS-20 profile has dry detents";
				}
				else
				{
					on = GameManager.flightControlsEnabled && Time.timeScale > 0f && !player.GetButton("Axis Modifier");
				}
			}
			if (stovl && why != lastWhy)
			{
				lastWhy = why;
				Plugin.Log.LogInfo("Vortex FCS: STOVL throttle detent " + ((why == null) ? ("active at " + (Plugin.StovlAftDetent * 100f).ToString("0") + " % (hold " + holdMs.ToString("0") + " ms)") : ("off: " + why)) + ".");
			}
			if (stovl && why != null)
			{
				// (no lever to move: an analog axis, a collective, the Detents mod's own profile; the laws' latch reads it as the
				// aft detent until it is moved)
				StovlSystem sNo = Registry.Find(a)?.stovl;
				if (sNo != null)
				{
					sNo.leverPark = false;
				}
			}
			{
				// 0.2.8: STOVL switched off with the lever in the aft detent (zero on the STOVL scale): the lever goes to 0 once,
				// so the CTOL throttle is at idle (it stayed at the detent's 20 %, which CTOL reads as thrust)
				StovlSystem sZ = ((UnityEngine.Object)(object)a != null) ? Registry.Find(a)?.stovl : null;
				if (sZ != null && sZ.leverZero)
				{
					sZ.leverZero = false;
					if (ci != null && player != null && !fCollective(st) && Digital(player))
					{
						float wasZ = Mathf.Clamp01(ci.throttle);
						Pin(st, ci, 0f, PlayerSettings.throttleUseNegative);
						hasLast = false;
						Plugin.Log.LogInfo($"Vortex FCS: throttle lever set to 0 % as STOVL was switched off (was {wasZ * 100f:F2} %).");
					}
				}
			}
			{
				// Vortex 0.5.4: Stability Assist switched off in STOVL flight: a keyboard / button lever goes once to the throttle
				// the engine was running at (StovlSystem.HoldThrottle), so it holds the jet up and the keys go on from there. An
				// analog axis is not touched (the game takes its position back as soon as it moves): StovlSystem holds the engine
				// there until the axis moves.
				StovlSystem sF = ((UnityEngine.Object)(object)a != null) ? Registry.Find(a)?.stovl : null;
				if (sF != null && sF.freeThrPin)
				{
					sF.freeThrPin = false;
					if (ci != null && player != null && !fCollective(st) && Digital(player))
					{
						float wasF = Mathf.Clamp01(ci.throttle);
						Pin(st, ci, sF.freeThrVal, PlayerSettings.throttleUseNegative);
						hasLast = false;
						Plugin.Log.LogInfo($"Vortex FCS: throttle lever set to {sF.freeThrVal * 100f:F1} % as Stability Assist went off in STOVL (was {wasF * 100f:F1} %).");
					}
					else
					{
						Plugin.Log.LogInfo("Vortex FCS: Stability Assist off in STOVL: lever not moved (" + ((player == null || ci == null) ? "no player input" : fCollective(st) ? "collective throttle" : "analog throttle axis") + $"); the engine holds {sF.freeThrVal * 100f:F1} % until the throttle is brought there.");
					}
				}
			}
			if (!on)
			{
				if (active)
				{
					active = false;
					Reset();
					Indicator.Hide();
				}
				return;
			}
			if (!active)
			{
				events = 0;
			}
			active = true;
			bool neg = PlayerSettings.throttleUseNegative;
			float b = Mathf.Clamp(Plugin.StovlAftDetent, 0.01f, 0.99f);
			{
				// 0.2.6: at the end of a conversion the lever goes to the aft detent (zero ground speed on the STOVL scale),
				// passed: the reduce key then goes straight on into the backward range, the increase key up the forward one.
				// 0.1.3-0.2.5 left it where it was (often at 0, so the reduce key did nothing: the lever had to be pushed up to
				// the detent and brought back down to go backwards).
				StovlSystem sP = Registry.Find(a)?.stovl;
				// 0.2.8: on the wheels, a lever set above the detent stays where the pilot put it: full throttle held through a
				// conversion on the runway (a short take-off) was pulled back to the zero-ground-speed detent. (StovlSystem
				// does not latch the throttle there either.)
				if (sP != null && sP.leverPark && sP.WheelsNow && Mathf.Clamp01(ci.throttle) > b + Plugin.StovlDetentWidth)
				{
					sP.leverPark = false;
					Event($"lever left at {Mathf.Clamp01(ci.throttle) * 100f:F0} % at the STOVL conversion on the wheels (above the detent)");
				}
				if (sP != null && sP.leverPark)
				{
					sP.leverPark = false;
					float was = Mathf.Clamp01(ci.throttle);
					Pin(st, ci, b, neg);
					last = b;
					hasLast = true;
					holding = false;
					holdT = 0f;
					unlocked = true;
					Event($"lever set to {b * 100f:F0} % at the STOVL conversion (was {was * 100f:F2} %)");
					ShowState(b, b, holdMs);
					return;
				}
			}
			float thr = Mathf.Clamp01(ci.throttle);
			float raw = player.GetAxisRaw("Throttle");
			int cmd = (raw > 0.1f) ? 1 : ((raw < -0.1f) ? (-1) : 0);
			if (!hasLast)
			{
				hasLast = true;
				last = thr;
				ShowState(thr, b, holdMs);
				return;
			}
			if (unlocked && Mathf.Abs(thr - b) > Rearm)
			{
				unlocked = false;
			}
			if (holding)
			{
				if (cmd == -holdDir)
				{
					// backing away from the detent: free again
					holding = false;
					holdT = 0f;
					last = thr;
					ShowState(thr, b, holdMs);
					return;
				}
				if (cmd == 0)
				{
					// key let go: the throttle stays on the approach side and the next press in the same direction starts
					// the hold
					holdT = 0f;
					Pin(st, ci, b - holdDir * Inward, neg);
					last = b - holdDir * Inward;
					ShowState(last, b, holdMs);
					return;
				}
				// 0.1.5: the key that ran into the detent goes through if it is kept held StovlThrottleDetentHoldMs (400), as a
				// fresh press does (0.1.4 needed a let-go and a new press)
				holdT += Time.deltaTime;
				if (holdT * 1000f < holdMs)
				{
					Pin(st, ci, b - holdDir * Inward, neg);
					last = b - holdDir * Inward;
					Indicator.Show("STOP HOLD " + Mathf.RoundToInt(Mathf.Clamp01(holdT * 1000f / holdMs) * 100f) + "%");
					return;
				}
				holding = false;
				holdT = 0f;
				unlocked = true;
				Event("passed " + ((holdDir > 0) ? "upward" : "downward") + " after " + holdMs.ToString("0") + " ms");
			}
			else if (!unlocked && cmd != 0 && ((cmd > 0 && last <= b && thr > b) || (cmd < 0 && last >= b && thr < b)))
			{
				// ran into the detent this frame: stop on the approach side until the key is let go
				holding = true;
				holdDir = cmd;
				holdT = 0f;
				Event($"stopped at {(b - cmd * Inward) * 100f:F2} % coming from {((cmd > 0) ? "below" : "above")} (lever {thr * 100f:F2} %, raw {raw:F2})");
				Pin(st, ci, b - cmd * Inward, neg);
				last = b - cmd * Inward;
				Indicator.Show("STOP HOLD 0%");
				return;
			}
			else if (!unlocked && (last - b) * (thr - b) < 0f)
			{
				Event($"lever crossed {b * 100f:F0} % without a stop (from {last * 100f:F2} to {thr * 100f:F2} %, raw {raw:F2}, command {cmd})");
			}
			last = thr;
			ShowState(thr, b, holdMs);
		}


		private static int events;

		// the first few detent events of a session go to the log (to see what happened in a flight)
		private static void Event(string what)
		{
			if (events < 12)
			{
				events++;
				Plugin.Log.LogInfo("Vortex FCS: STOVL throttle detent " + what + ".");
			}
		}

		private static void ShowState(float thr, float b, float holdMs)
		{
			parked = Mathf.Abs(thr - b) <= Plugin.StovlDetentWidth;
			if (parked && Plugin.StovlThrottleDetentIndicator)
			{
				Indicator.Show(holding ? "STOP" : "STOP LOCK");
			}
			else
			{
				Indicator.Hide();
			}
		}

		// the public throttle and the game's integrator set to the same position (the game's own mapping: simulated -1..1
		// with throttleUseNegative, 0..1 otherwise)
		private static void Pin(PilotPlayerState st, ControlInputs ci, float pub, bool neg)
		{
			pub = Mathf.Clamp01(pub);
			ci.throttle = pub;
			fSim(st) = neg ? (2f * pub - 1f) : pub;
		}

		private static bool InStovl(Aircraft a)
		{
			if (!Registry.IsF22(a))
			{
				return false;
			}
			FcsController c = Registry.Find(a);
			StovlSystem s = c?.stovl;
			return s != null && s.owns && c.Engaged && !c.Failed && s.state == StovlState.On && !s.freestyle;
		}

		// the Detents mod's own test: any throttle source that is not a keyboard button counts as an analog axis
		private static bool Digital(Player p)
		{
			var src = p.GetCurrentInputSources("Throttle");
			if (src == null || src.Count == 0)
			{
				return lastDigital;
			}
			for (int i = 0; i < src.Count; i++)
			{
				InputActionSourceData d = src[i];
				if ((int)d.controllerType != 0)
				{
					ActionElementMap m = d.actionElementMap;
					if (m == null || (int)m.elementType != 1)
					{
						lastDigital = false;
						return false;
					}
				}
			}
			lastDigital = true;
			return true;
		}

		private static bool lastDigital = true;

		// the Detents mod (com.baanish.nuclearoption.detents) with a custom FS-20 profile enabled that has dry detents of its
		// own: read from its config file every 5 s
		private static bool OtherModOwns()
		{
			if (Time.unscaledTime - otherCheckT < 5f)
			{
				return otherOwns;
			}
			otherCheckT = Time.unscaledTime;
			bool was = otherOwns;
			otherOwns = false;
			try
			{
				string path = Path.Combine(Paths.ConfigPath, "com.baanish.nuclearoption.detents.cfg");
				if (File.Exists(path))
				{
					string sec = "";
					bool genOn = true;
					bool profOn = false;
					string dry = "";
					foreach (string line0 in File.ReadAllLines(path))
					{
						string line = line0.Trim();
						if (line.StartsWith("[") && line.EndsWith("]"))
						{
							sec = line.Substring(1, line.Length - 2);
							continue;
						}
						if (line.Length == 0 || line.StartsWith("#"))
						{
							continue;
						}
						int eq = line.IndexOf('=');
						if (eq < 0)
						{
							continue;
						}
						string k = line.Substring(0, eq).Trim();
						string v = line.Substring(eq + 1).Trim();
						if (sec == "General" && k == "Enabled")
						{
							genOn = v.Equals("true", StringComparison.OrdinalIgnoreCase);
						}
						else if (sec == "Custom Aircraft Profile SmallFighter1")
						{
							if (k == "Enabled")
							{
								profOn = v.Equals("true", StringComparison.OrdinalIgnoreCase);
							}
							else if (k == "DryDetentPercentages")
							{
								dry = v;
							}
						}
					}
					otherOwns = genOn && profOn && dry.Length > 0;
				}
			}
			catch
			{
				otherOwns = false;
			}
			if (otherOwns != was)
			{
				Plugin.Log.LogInfo(otherOwns ? "Vortex FCS: the Detents mod's FS-20 profile has its own dry detents; the STOVL throttle detent stands aside." : "Vortex FCS: STOVL throttle detent active (the Detents mod has no FS-20 dry detents of its own).");
			}
			return otherOwns;
		}

		// a copy of the HUD throttle label under the throttle gauge, as the Detents mod draws its indicator
		private static class Indicator
		{
			private static readonly System.Reflection.FieldInfo fLabel = AccessTools.Field(typeof(ThrottleGauge), "throttleLabel");

			private static FlightHud hud;

			private static TextMeshProUGUI src;

			private static GameObject go;

			private static TextMeshProUGUI lbl;

			private static Transform center;

			private static string text;

			public static void Hide()
			{
				if (go != null && go.activeSelf)
				{
					go.SetActive(false);
				}
			}

			public static void Show(string t)
			{
				if (!Plugin.StovlThrottleDetentIndicator)
				{
					Hide();
					return;
				}
				FlightHud h = SceneSingleton<FlightHud>.i;
				if (h == null || !Ensure(h))
				{
					return;
				}
				if (text != t)
				{
					lbl.text = t;
					text = t;
				}
				lbl.font = src.font;
				lbl.fontSharedMaterial = src.fontSharedMaterial;
				lbl.color = src.color;
				lbl.fontStyle = src.fontStyle;
				float fs = Mathf.Max(12f, src.fontSize * 0.65f);
				lbl.fontSize = fs;
				lbl.transform.localScale = src.transform.localScale;
				float step = Mathf.Max(22f, src.fontSize * 0.65f);
				// one line further down while the Detents mod's indicator is up
				Transform other = center.Find("NuclearOptionDetents_HudIndicator");
				int lines = (other != null && other.gameObject.activeInHierarchy) ? 2 : 1;
				lbl.transform.localPosition = center.InverseTransformPoint(src.transform.position) + new Vector3(0f, -step * lines, 0f);
				bool vis = src.enabled && src.gameObject.activeInHierarchy;
				if (go.activeSelf != vis)
				{
					go.SetActive(vis);
				}
			}

			private static bool Ensure(FlightHud h)
			{
				if (hud == h && go != null && lbl != null && src != null && center != null)
				{
					return true;
				}
				if (go != null)
				{
					UnityEngine.Object.Destroy(go);
				}
				go = null;
				lbl = null;
				text = null;
				hud = h;
				ThrottleGauge g = h.GetComponentInChildren<ThrottleGauge>(true);
				src = (g != null && fLabel != null) ? (fLabel.GetValue(g) as TextMeshProUGUI) : null;
				center = h.GetHUDCenter();
				if (src == null || center == null)
				{
					return false;
				}
				go = UnityEngine.Object.Instantiate(src.gameObject, center);
				go.name = "VortexFCS_StovlDetentIndicator";
				lbl = go.GetComponent<TextMeshProUGUI>();
				if (lbl == null)
				{
					UnityEngine.Object.Destroy(go);
					go = null;
					return false;
				}
				lbl.raycastTarget = false;
				lbl.enabled = true;
				lbl.richText = false;
				lbl.enableAutoSizing = false;
				lbl.enableWordWrapping = false;
				lbl.alignment = TextAlignmentOptions.Center;
				RectTransform rt = go.GetComponent<RectTransform>();
				rt.pivot = new Vector2(0.5f, 0.5f);
				rt.anchorMin = new Vector2(0.5f, 0.5f);
				rt.anchorMax = new Vector2(0.5f, 0.5f);
				rt.sizeDelta = new Vector2(300f, 60f);
				go.transform.localRotation = Quaternion.identity;
				return true;
			}
		}
	}
}
