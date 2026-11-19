using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// Vortex 0.2.8 (Tom, 17 Nov 2026: claude/Vortex_Release_Guard_spec.md). STOVL release guard: while the STOVL laws fly
	// the FCS does not release a weapon whose release would leave more sideways weight imbalance than the jet can trim in
	// roll. A short HUD warning instead; letting the trigger go and then holding it ReleaseGuardHold seconds drops it anyway.
	//
	// Revised 02:45 after the first flight (02:21 log):
	//   - the limit is AuthorityShare (75 %) of the roll moment the effectors can make now (roll posts, plus the surfaces
	//     as the airspeed gives them any), at the weight after the release. First built with the 25 % the laws do not
	//     budget with, posts only: it blocked one Tusko-B (650 kg) off an inner pylon (2.57 m out, 61 % of the posts), which
	//     Tom's jet holds; one 500 kg store off an outer pylon (4.07 m, 76 %+) rolls it over.
	//   - on at any speed in STOVL, not only in the hover regime: the surfaces' authority is in the limit, so it relaxes
	//     by itself as the jet speeds up. With the hover-regime gate, a blocked Tusko pair came off 0.25 s apart at 43 kt
	//     the moment the jet accelerated out of the regime with the trigger still held.
	// Revised 03:00 after the 02:38 and 02:39 flights:
	//   - the roll authority is the posts' at the thrust a hover at the weight after the release takes (StovlSystem.
	//     HoverRollTrim), plus what the roll surfaces add now. Taken from the thrust of the moment it came out 26.4 kN m in
	//     one flight and 19.7 in the next, and let one Tusko-B go at full fuel that it blocked at half.
	//   - two tests. At once: the trim needed right after the release, against TransientShare (90 %) of that authority;
	//     past that the posts cannot stop the roll. Settled: the trim left once the fuel balancing has moved what it can
	//     (FuelBalance.LateralShiftCapacity; nothing with full tanks), against AuthorityShare (75 %). One Tusko-B off an
	//     inner pylon is 79 % at once: blocked at full fuel (it stays 79 %), let go at half fuel (about 10 % left).
	//   - an override releases one store: the same press releases nothing more for NextDelay (0.7 s), then goes on as
	//     usual (the store on the other side restores the balance, so it is safe and needs no warning); a new press
	//     releases at once.
	// Revised 03:40 after the 02:53 and 02:55 flights:
	//   - a store's mass is taken where the game puts it, at the centre of mass of the part its hardpoint belongs to
	//     (MassPoint), not at the pylon: one Tusko-B off an inner pylon is 20.9 kN m, not 16.4 (the figures above), and
	//     went through at full fuel as 75 % where the posts then sat at 92 % and at their stop 44 % of the time.
	//   - the CG is the FCS's in the root's frame (FcsController.CgNow): sliding sideways at 8-10 kt the estimate for a
	//     release that left the jet balanced read 78 %, and one that left it at ~170 % read 115 %.
	//   - one limit, GuardShare (90 %) of the roll authority, right after the release. The laws now plan the bank on what
	//     the load leaves of the posts (Stovl.cs, rollWeak), and with that the jet keeps its bank and its height with the
	//     posts at 81-95 % (bench: full side stick both ways, bank within 15 deg, level and stopped again), so a load the
	//     fuel cannot take back no longer needs the lower 75 % limit. The trim left once the fuel has balanced is still
	//     logged.
	//
	// The game's path (read from its code): PilotPlayerState calls pilot.Fire() every frame the Fire button is down ->
	// WeaponManager.Fire(): the selected station (currentWeaponStation); guns and fireInterval == 0 weapons fire through
	// WeaponStation.Fire, the rest one at a time through WeaponStation.LaunchMount(Weapons[weaponIndex]), or a salvo of one
	// per target when several targets are selected. A prefix on WeaponManager.Fire returning false blocks all of it.
	// Jettison does not go through WeaponManager.Fire and is untouched. A released missile takes info.massPerRound off its
	// hardpoint's part (MountedMissile -> Hardpoint.ModifyMass -> UnitPart.ModifyMass).
	internal static class ReleaseGuard
	{
		private static AccessTools.FieldRef<WeaponManager, Aircraft> fAircraft;

		private static AccessTools.FieldRef<WeaponManager, List<Unit>> fTargets;

		private static AccessTools.FieldRef<WeaponStation, int> fIndex;

		private static bool ready;

		private static bool failed;

		// the state (one jet: the local player's; reset whenever the manager changes)
		private static WeaponManager lastWm;

		private static WeaponStation lastStation;

		private static int lastEpoch = -1;

		private static bool armed;

		private static bool blocked;

		private static float approved;

		private static float lastCall = -10f;

		private static float pressT;

		private static float lastShot = -1000f;

		private static int pressId;

		private static int blockPress;

		private static int overridePress = -1;

		private static float overrideT = -100f;

		// after an override release, the time the same (still held) press releases nothing more. Vortex 0.3.7: the next store
		// comes ReleaseGuardSecondHold after the press (1.7 s), so this is that less the hold (0.3.6: a fixed 0.7 s)
		private static float NextDelay => Mathf.Max(0.1f, Plugin.ReleaseGuardSecondHold - Plugin.ReleaseGuardHold);

		// the share of the roll authority the trim right after a release may take (the rest stops the roll it starts)
		private const float TransientShare = StovlSystem.GuardShare;

		// the Fire button counts as let go when WeaponManager.Fire has not been called for this long (it is called every
		// frame the button is down)
		private const float Gap = 0.15f;

		// Vortex 0.3.7 (Tom, 17 Nov: a release and an instant re-press did not override; he had to wait a moment first): the game
		// calls WeaponManager.Fire from PilotPlayerState.PlayerControls (Update) every rendered frame the button is down, so one
		// frame without a call is a release. 0.3.6 needed 0.15 s without a call, and a quick re-press read as the same, still
		// blocked press; the override hold then never started.
		private static int lastFrame = -10;

		public static void Install(Harmony h, Action<string, Action> tryPatch)
		{
			tryPatch("WeaponManager.Fire (STOVL release guard)", delegate
			{
				fAircraft = AccessTools.FieldRefAccess<WeaponManager, Aircraft>("aircraft");
				fTargets = AccessTools.FieldRefAccess<WeaponManager, List<Unit>>("targetList");
				fIndex = AccessTools.FieldRefAccess<WeaponStation, int>("weaponIndex");
				h.Patch(AccessTools.Method(typeof(WeaponManager), "Fire"), new HarmonyMethod(typeof(ReleaseGuard), "FirePrefix"));
				ready = true;
			});
		}

		public static bool FirePrefix(WeaponManager __instance)
		{
			if (!ready || failed || !Plugin.ReleaseGuardOn)
			{
				return true;
			}
			try
			{
				return Allow(__instance);
			}
			catch (Exception e)
			{
				failed = true;
				Plugin.Log.LogWarning("Vortex FCS release guard switched off (weapons release as stock): " + e.Message);
				return true;
			}
		}

		private static bool Allow(WeaponManager wm)
		{
			Aircraft a = fAircraft(wm);
			if ((UnityEngine.Object)(object)a == null || !Registry.IsF22(a))
			{
				return true;
			}
			FcsController c = Registry.Find(a);
			StovlSystem s = c?.stovl;
			// on: the STOVL laws flying a human pilot's jet (not MPO / freestyle), in the air
			if (s == null || !s.owns || !c.Engaged || c.Failed || !s.GuardRegime || a.remoteSim)
			{
				return true;
			}
			float now = Time.time;
			if (wm != lastWm)
			{
				lastWm = wm;
				lastStation = null;
				lastCall = -10f;
				lastFrame = -10;
				armed = false;
				blocked = false;
			}
			int frame = Time.frameCount;
			bool newPress = now - lastCall > Gap || frame - lastFrame > 1;
			lastCall = now;
			lastFrame = frame;
			if (newPress)
			{
				pressId++;
				pressT = now;
			}
			WeaponStation st = wm.currentWeaponStation;
			if (st != lastStation || s.guardEpoch != lastEpoch)
			{
				// another weapon selected (cycling back to one already overridden too), the hover regime left and re-entered,
				// STOVL or MPO switched: the next unsafe press warns and blocks again
				lastStation = st;
				lastEpoch = s.guardEpoch;
				armed = false;
				blocked = false;
			}
			float cd = Plugin.ReleaseGuardCooldown;
			if (armed && cd > 0f && now - lastShot > cd)
			{
				armed = false;
				blocked = false;
			}
			if (st == null || st.WeaponInfo == null || a.weaponStations.Count == 0 || st.SafetyIsOn(a))
			{
				return true;
			}
			WeaponInfo wi = st.WeaponInfo;
			// guns, rockets and the like (no fire interval), slings and troops: no single store's weight comes off one side
			if (wi.gun || wi.fireInterval == 0f || wi.sling || wi.troops || !st.Ready() || st.SalvoInProgress)
			{
				return true;
			}
			if (pressId == overridePress && now - overrideT < NextDelay)
			{
				// the press that just overrode a block: one store only; the next comes NextDelay later or on a new press
				return false;
			}
			List<Unit> tl = fTargets(wm);
			int n = (tl != null && tl.Count > 1) ? tl.Count : 1;
			float need = TrimNeeded(a, c, s, st, n, out float auth, out float dm, out float yOff, out float settled);
			bool safe = auth <= 0f || need <= TransientShare * auth;
			if (safe || (armed && need <= approved * 1.02f + 1f))
			{
				lastShot = now;
				return true;
			}
			if (armed)
			{
				// more asymmetry than the override approved
				armed = false;
				blocked = false;
			}
			if (!blocked)
			{
				blocked = true;
				blockPress = pressId;
				float hold = Plugin.ReleaseGuardHold;
				StovlSystem.Report?.Invoke(a, "UNSAFE WPN RELEASE - HOLD " + hold.ToString("0.#") + "s");
				Plugin.Log.LogInfo($"Vortex FCS release guard: blocked {wi.weaponName} x{n} ({dm:F0} kg): roll trim needed {need:F0} N m at once ({100f * need / auth:F0} % of the {auth:F0} N m of roll authority in the hover at that weight; limit {TransientShare * 100f:F0} %), {settled:F0} N m once the fuel has balanced ({100f * settled / auth:F0} %). CG {yOff:F3} m off the lift line after the release.");
				return false;
			}
			if (pressId == blockPress)
			{
				// still the press that was blocked: the trigger has to be let go first
				return false;
			}
			if (now - pressT >= Plugin.ReleaseGuardHold)
			{
				armed = true;
				approved = need;
				blocked = false;
				lastShot = now;
				overridePress = pressId;
				overrideT = now;
				Plugin.Log.LogInfo($"Vortex FCS release guard: override, {wi.weaponName} released (roll trim {need:F0} N m approved).");
				return true;
			}
			return false;
		}

		// the roll moment (N m) the hover has to trim right after the release (returned) and once the fuel balancing has
		// moved what it can (settled), and the roll authority (N m) to hold it with: the jet's mass and CG as the FCS has
		// them, less the stores this press would release at where they hang
		// 0.2.8: where a store's mass sits. The game adds it to the part its hardpoint belongs to (Hardpoint.ModifyMass ->
		// UnitPart.ModifyMass), so it acts at that part's centre of mass, not at the pylon: the FS-20's inner pylon (2.57 m
		// out) hangs from wing section 2a, centre of mass 3.29 m out, the outer one (4.07 m) from the wingtip part at 4.26 m.
		// 0.2.8's first guard used the weapon's own position: a Tusko-B off the inner pylon came to 16.4 kN m where the jet
		// then showed 20.9 (02:53 log, lateral CG 0.154 m at 13.86 t, posts at 92 % with 44 % of the time at the stop), and it
		// went through as 75 %.
		private static readonly AccessTools.FieldRef<Weapon, Hardpoint> fHardpoint = AccessTools.FieldRefAccess<Weapon, Hardpoint>("hardpoint");

		private static Vector3 MassPoint(Weapon w)
		{
			try
			{
				Hardpoint hp = fHardpoint(w);
				Rigidbody rb = (hp != null && (UnityEngine.Object)(object)hp.part != null) ? hp.part.rb : null;
				if (rb != null)
				{
					// (through the transform, not rb.worldCenterOfMass: between physics steps the transforms are the
					// interpolated pose the rest of this estimate is taken in)
					return rb.transform.TransformPoint(rb.centerOfMass);
				}
			}
			catch (Exception)
			{
			}
			return ((Component)(object)w).transform.position;
		}

		private static float TrimNeeded(Aircraft a, FcsController c, StovlSystem s, WeaponStation st, int n, out float auth, out float dm, out float yOff, out float settled)
		{
			auth = 0f;
			dm = 0f;
			yOff = 0f;
			settled = 0f;
			float M = c.MassKg;
			// (0.2.8: the CG in the root's frame, placed with the root as it is now; see FcsController.CgLocal)
			Vector3 cg = c.CgNow;
			Transform root = ((Component)(object)a).transform;
			if (M < 1000f || s.hw == null)
			{
				return 0f;
			}
			int idx = fIndex(st);
			Vector3 mr = Vector3.zero;
			for (int k = 0; k < n && idx + k < st.Weapons.Count; k++)
			{
				Weapon w = st.Weapons[idx + k];
				if ((UnityEngine.Object)(object)w == null || w.info == null || !w.IsAttached())
				{
					continue;
				}
				float m = Mathf.Max(0f, w.info.massPerRound);
				dm += m;
				mr += m * MassPoint(w);
			}
			if (dm <= 0f || M - dm < 1000f)
			{
				return 0f;
			}
			Vector3 cgNew = (M * cg - mr) / (M - dm);
			// the posts at the thrust the hover takes at the new weight, and what the roll surfaces add at this airspeed
			if (!s.HoverRollTrim(M - dm, cgNew, out float need, out float lim75, out yOff))
			{
				return 0f;
			}
			float postLive = 0f;
			if (s.post != null && s.post.active && s.post.M != null)
			{
				for (int i = 0; i < s.post.M.Length; i++)
				{
					postLive = Mathf.Max(postLive, Mathf.Abs(s.post.M[i].x));
				}
			}
			auth = lim75 / StovlSystem.AuthorityShare + Mathf.Max(0f, c.RollMomentAuthority() - postLive);
			// the fuel can move the CG back toward the light side by this much (kg m)
			float fuelKgM = FuelBalance.LateralShiftCapacity(a, root.position, root.right, (yOff > 0f) ? -1f : 1f);
			settled = Mathf.Max(0f, need - fuelKgM * 9.81f);
			return need;
		}
	}
}
