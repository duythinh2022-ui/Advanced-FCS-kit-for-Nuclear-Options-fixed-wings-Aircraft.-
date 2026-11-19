# F-22E FCS 2.3.0: settled stall window, coordinated stop

Base: 2.2.29. Logs: 21 Oct 21:28 (2.2.29) and 21:38 (2.0.5), same Dev cfg. Minor-version bump: two control-law changes, both structural, both replacing gates rather than adding them. No default values changed; your Dev cfg is left alone.

## What the logs showed (releases from the 65 deg corkscrew, 8 events on 2.2.29, 12 on 2.0.5; mean / worst)

| | 2.2.29 | 2.0.5 |
|---|---|---|
| Pitch dip after release | -2.6 / -5.1 deg/s | -2.4 / -4.2 |
| Yaw decel, first 0.8 s | 17.5 deg/s^2 | 22.5 |
| Sideslip 0.3-2 s after release | 3.6 / 6.6 deg | 2.8 / 5.1 |
| Residual roll 2-3 s after release, old direction | 5.2 / 11.6 deg/s | 1.0 / 7.3 |
| Bank picked up after the roll is under 8 deg/s | 5.7 / 11.1 deg | 0.8 / 3.9 |
| Calm released flight with the nozzles at their stop | 3.0 s | 0.3 s |
| Calm released flight with a rudder past 20 deg | 4.6 s | 0.5 s |

- **The pitch dip is the same in both.** The yaw/roll stop pins one stab at -30 for 1-1.5 s; the nozzles move 1-2 deg. Not touched here.
- **The roll back the old way is the sideslip law.** The roll braked faster than the yaw (decel ratio 0.62 against 0.47 coordinated), sideslip built to 4-6.6 deg, and its correction commanded roll and full rudder in the old direction 1-3 s after the release.
- **The stuck surfaces are a crossed set.** 30.8 s: from 2.5 to 8.8 s after the release the jet is still (q -0.1, p 0.4, r -1.7, beta 0.2) while the nozzles ramp 3 -> 20 (stop), the stabs walk to -11/-27 and the rudders from a +-6 toe to +14/+35. Stab R sits at local AoA 27-29 (the 2.2.15 window edge, peak 25 + 3) the whole time. **The bench reproduces it** (replay of your 56-74 s stick): a stab resting on its lift peak has no modelled gradient in pitch or roll, so nothing can move it, while it makes its maximum lift; the other stab has to match that lift to cancel the roll (both at maximum nose-down), and the nozzles cancel the pitch with nose-up to their stop as the dynamic pressure rises.

Full analysis: `F22E_release_regression_2.2.29_vs_2.0.5.md` in the project.

## 1. Settled stall window (`StabParkEscape`, `SettledStallMargin` 6, Dev "4. Gains")
Replaces the 2.2.15/2.2.18 park escape (which only acted below 30 deg AoA and allowed the peak itself).
- With the lateral stick and pedals centred and the lateral rates settled (body roll and yaw under 4 deg/s; off again above 10 or with lateral input), the stabilators and rudders may not rest past (lift peak - 6) deg of local AoA, at any AoA where such an attached range exists. No range (deep post-stall): the window stays open.
- A surface already past the window is walked in at 15 deg/s (the window's edge moves, not the surface), so the allocator re-solves around a small step each frame. A snap-in rolled the bench jet 5 deg/s and sent the wing to its stops.
- Once on the attached slope the surfaces have a gradient again and the ordinary deflection cost unwinds the set.

**Bench, replays of the seven 2.2.29-log releases** (calm phase, 1.5 s after release until the next stick input): nozzle max 20 / 6.2 / 4.4 -> 3.5 / 2.6 / 3.8 deg in the three stuck cases (68.1 / 130.5 / 158.9 s); stabs at the end 0 / +1 instead of -10 / -20; rudders in the calm phase 19-30 -> 3-23 (the remaining rudder excursions are the yaw stop finishing, not a held set). Sideslip in the stop within 2.0 deg in all seven (2.2.29: 0.5-3.0).

## 2. Coordinated stop (`CoordinatedStop`, Dev "3. Lateral-directional")
- With the lateral stick and pedals released above 15-30 deg AoA, the lateral trajectory's deceleration is scaled down (to zero) as either the roll or the yaw falls 3-10 deg/s behind its command (roll error in velocity-vector terms). The command then holds until the slower axis has caught up, so the roll and yaw commands stay coordinated with the rates the jet actually has, and the stop is paced by the slower axis instead of by the modelled authority.
- With the stick in nothing changes (gate 0). Reversals are stick-in by definition.
- **What it does not do:** it does not slave the roll to the measured yaw. That was tried first; on the bench the yaw stops faster than the wing can follow, and the slaved roll then over-rolled (sideslip 1.2 -> 2.9 deg). The pacing works for both cases.
- The bench's yaw tracks its command, so the governor barely acts there (sideslip in the stops 0.5-3.0 -> 0.8-3.0). Your jet's yaw lags by 10-20 deg/s in the stops; the effect is only measurable in game.

## Regression
- **Suite:** identical to 2.2.29 within rounding (S1 -4.3 / -2.2 both; S4 bank at 1 s 42 -> 41 in one case; S6 alphaMin 39 -> 38, 30 -> 31).
- **Held corkscrews replayed from the log (54, 97, 176, 246 s):** byte-identical AoA, roll, pitch rate and sideslip.
- **Full-aft + full-lateral stops at 200 / 280 / 350 km/h, with and without pedal:** stop times 3.6 -> 3.5 s, sideslip and roll bump unchanged.

## Telemetry
Two new columns at the end of the FCS row: `coordStop` (the stop governor's hold, 0-1) and `settledWin` (1 while the window is on).

## Not verified
- Not flown. The roll-bump change can only show in game (the bench's yaw does not lag).
- Fins-gone / engine-out sets and the DisableTVC logs were not re-run; the settled window applies there too (rudder window included), so the TVC-off crossed set of 2.2.15/2.2.16 should be covered by it rather than by `RollFightReleased`. Those gates are left in place; A/B them in the Dev cfg if you want to retire them.

## Files
- `F22E-StrikeRaptor-FCS-2.3.0.zip` (`Aryx_F22E_StrikeRaptor_FCS.dll`), `Optional.F22E-FCS-2.3.0-DevUnlock.zip`, `F22E_FCS_2.3.0_src.zip` (with the bench, without Aryx's airframe data).

Changed source: FcsController.cs (settled window, SettledWindow(), stop governor, telemetry), Plugin.cs (SettledStallMargin, CoordinatedStop, StabParkEscape description), FcsStandalonePlugin.cs / DevUnlock (version), bench/sim/Program.cs (allocator debug columns).
