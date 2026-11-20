using UnityEngine;

namespace Vortex_FS20.FCS
{
	public static class FcsApi
	{
		// 4 (Revoker 0.2.5): discovery contract for tools (flight-data HUD, FCS visualizer), additions only. A tool finds every
		// public static class named FcsApi in any loaded plugin and asks each about the local aircraft; nothing in the tool
		// names a particular FCS mod. Members:
		//   ApiId        "duythinh2022.fcs" - marks a class that follows this contract (v4+)
		//   Version      contract version (int)
		//   FcsName      display name of this FCS ("FS-20 Vortex FCS")
		//   FcsVersion   plugin version ("0.2.5")
		//   Owns(Aircraft)             true when this FCS is attached to that aircraft (engaged or not)
		//   GetController(Aircraft)    that aircraft's controller object (its fields are what the visualizer reads), or null
		//   GetPitchCommand(Aircraft, float[10])   unchanged since v3
		public const string ApiId = "duythinh2022.fcs";

		public const string FcsName = "FS-20 Vortex FCS";

		public static readonly string FcsVersion = FcsStandalonePlugin.Version;

		public static bool Owns(Aircraft aircraft)
		{
			return Registry.Find(aircraft) != null;
		}

		public static object GetController(Aircraft aircraft)
		{
			return Registry.Find(aircraft);
		}

		public const int Version = 5; // 2 (1.9.1): code 7 = high-AoA pitch-rate command, v[3] = commanded pitch rate
		// 3 (2.1.0): code 8 = pitch-rate command of the G + pitch-rate law (PitchCommandLaw = GAndPitchRate, full G out of
		// reach); code 4 = its G command. v[1] (AoA command) is not meaningful under that law (it tracks the AoA).

		public static bool GetPitchCommand(Aircraft aircraft, float[] v)
		{
			if (v == null || v.Length < 10)
			{
				return false;
			}
			FcsController fcsController = Registry.Find(aircraft);
			if (fcsController == null || fcsController.Failed || !fcsController.Engaged)
			{
				return false;
			}
			v[0] = fcsController.Mode switch
			{
				FcsMode.Ground => 1f, 
				FcsMode.GearDownRate => 2f, 
				FcsMode.AoAG => (Mathf.Abs(fcsController.sp) < 0.04f && !fcsController.prot) ? 6f : (Plugin.GRateLaw ? ((fcsController.betaG < 0.5f) ? 8f : 4f) : ((fcsController.hiRate && fcsController.rateW > 0.5f) ? 7f : (fcsController.capOn ? 8f : ((fcsController.betaG < 0.5f) ? 3f : 4f)))), 
				FcsMode.MPO => 5f, 
				FcsMode.AIRate => 2f, 
				FcsMode.Rate => 2f, 
				_ => 0f, 
			};
			v[1] = fcsController.alphaCmd;
			v[2] = fcsController.nCmd;
			v[3] = fcsController.qT;
			v[4] = fcsController.alpha;
			v[5] = fcsController.nz;
			v[6] = (fcsController.prot ? 1f : 0f);
			v[7] = (fcsController.unload ? 1f : 0f);
			v[8] = Plugin.AoAPos;
			v[9] = ((fcsController.Mode == FcsMode.MPO) ? Plugin.MpoGPos : Plugin.GPos);
			return true;
		}

		// v5 (Vortex 0.2.10): the STOVL lever scale, for HUD mods. True while the STOVL laws own the throttle (STOVL mode on,
		// not freestyle). v[0] aft detent (zero ground speed) position 0-1, v[1] the detents' half width, v[2] centre detent,
		// v[3] commanded ground speed (kt; meaningful in the hover regime), v[4] regime: 0 hover (lever = ground speed),
		// 1 at speed (lever = accelerate / hold / decelerate), 2 CTOL blend (lever = thrust), v[5] the CTOL blend weight,
		// v[6] 1 with the lever in the zero-ground-speed detent. The readout text the FCS shows on the game's gauge:
		// GetStovlThrottleText.
		public static bool GetStovlThrottle(Aircraft aircraft, float[] v)
		{
			if (v == null || v.Length < 7)
			{
				return false;
			}
			StovlSystem s = StovlFor(aircraft);
			if (s == null)
			{
				return false;
			}
			float ta = Plugin.StovlAftDetent;
			float tw = Plugin.StovlDetentWidth;
			v[0] = ta;
			v[1] = tw;
			v[2] = Plugin.StovlCenterDetent;
			v[3] = s.dbgGsCmd * 1.944f;
			v[4] = (s.CtolW >= 0.5f) ? 2f : (s.SlowRegime ? 0f : 1f);
			v[5] = s.CtolW;
			v[6] = (Mathf.Abs(s.LeverSeen - ta) <= tw) ? 1f : 0f;
			return true;
		}

		public static string GetStovlThrottleText(Aircraft aircraft)
		{
			StovlSystem s = StovlFor(aircraft);
			return (s == null) ? null : (s.hudText ?? "0 GS");
		}

		private static StovlSystem StovlFor(Aircraft aircraft)
		{
			FcsController c = Registry.Find(aircraft);
			StovlSystem s = c?.stovl;
			if (s == null || !s.owns || !c.Engaged || c.Failed || s.state != StovlState.On || s.freestyle)
			{
				return null;
			}
			return s;
		}
	}
}
