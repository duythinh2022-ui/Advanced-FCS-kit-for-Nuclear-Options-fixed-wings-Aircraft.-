# F-22E FCS 2.11.0 — the sideslip guard and the yaw-saturation guard deleted

Base: 2.10.0. **Compiled + bench-checked, not flown.** 2.10.0 kept as `.dll.2.10.0.bak`. Removal only, but a control-law
removal (the yaw-saturation term was still acting), hence the minor bump.

## From the 17:47 / 17:54 flights (2.10.0)
Push-as-you-release from 65°: 247 kt / 74 deg/s → 6.3°, 213 / 72 → 9.6°, 226 / 70 → 10.8°, 195 / 58 → 4.0° (trajectory
still at 49–55 at the push, the case that made 24.6° on 2.9.4). Held pushes 2.4–3.5°, the crossing's largest per-tick
command step 4.5–8 deg/s (16–17 in the two released pushes with the big yaw rate). Tom: "the release push-over is where I
like it, the held push-over is smoother and the hitching is small."

## What was still acting, and what was idle (17:33 / 17:47 / 17:54 logs, 35 000 ticks)
Active, kept: the 41 % rate cap (`paceHold` > 2 on 18–43 % of ticks), the pace window, the trade (`tradeAlpha` 2–18 %), the
sweep / shed caps (`yawSatPitch` 5–18 %), the hold (`pitchNeedDn`, `sweepWin`, `stabRateBound` 25–48 %), `coupRollLim`
(binding 8–14 % — still the table version, the next thing to put on the estimator), the TVC pitch-reserve governor
`rollGov` (0.6–3.8 %), `pitchPrio` / `pitchNeed` (5–18 %), the stab stall windows (`settledWin` 27–49 %).
Idle in every clean flight by construction (asymmetric-load gate, `negGate` 0 %): the sideslip-rate lead `slipGov` and the
yaw anchor — kept until a damage flight says the estimator covers that case too.
Idle and dead: `sweepLim` (the 2.1.8 sweep yaw limit — the column was written 0 since 2.5.3), `coordStop` (written 0),
`stopSlipHold` (a filter nothing read since 2.10.0).

**The guard** (`guard` column, 2–8 % of ticks < 0.8): with `SideslipGuardStart` at your 40 the sideslip term read 1.0; what
was left was the yaw-saturation term (`YawSaturationGuard` 0.7 × yawSat above 8–18° AoA), and every tick it cut (× 0.66–0.78)
with the stick asking for roll was a reversal or a stop with the roll near zero (|pS| 6–20) where the pace window was already
holding the target at 5–17 against a demand of 30–80 (`paceHold` > 2 on 67–100 % of those ticks). Two devices doing one job;
the window is the one built for it.

## Change
Deleted: the 2.0.15 sideslip guard and the yaw-saturation guard (`x` in the lateral law is now 1 unless the asymmetric-load
sideslip lead scales it), the Dev keys **`SideslipGuardStart`** and **`YawSaturationGuard`** (section 3; the lines in your cfg
are ignored), the dead `stopSlipHoldF` filter, and the three dead telemetry columns `sweepLim`, `coordStop`, `stopSlipHold`
(game and bench CSVs: 299 → 296 columns; the old `dump.py` / `ev.py` / `stops.py` / `rp.py` scripts updated).

## Bench (sideslip ~2× high; the bench config had the guard at 4, so it shows both terms going)
Reversals: new-roll peaks up 10–60 % (pull+roll rev 556 km/h 61 → 84 deg/s, 650 103 → 167 — the guard had been cutting the
reversal's new roll), sideslip +0.3…+3.1° (556: 18.2 → 18.9, 650: 16.7 → 19.8, fullaft 300: 4.1 → 5.6). Regression set: fullaft
+roll+rev3s 320 3.7 → 5.8, pull-then-pushover 450 5.5 → 7.2, the rest within ±0.3. Corkscrew entries and the utilisation
invariant unchanged. In your flights the sideslip term was already off; expect reversals a touch crisper and the sideslip in
pinned-rudder reversals the same or slightly up — that regime is the rate cap's reversal model, next.

## Next
1. `coupRollLim` on the estimator (the 400 kt corkscrew-entry dip binder, still rudder tables).
2. The rate cap's reversal model and the window (§4 a/b): the 300 kt / 28° rolling and the pinned-rudder reversals.
3. `slipGov` + yaw anchor after a damage flight (shoot a fin off with the part-damage tool, reverse at 40–50° AoA).
4. Delete the old lateral governor code outright; Ifrit / Revoker.
