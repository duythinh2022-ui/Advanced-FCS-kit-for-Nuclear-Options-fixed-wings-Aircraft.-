# Ifrit FCS 0.8.3: roll and yaw tables anchored to the live moment (engine-out roll creep)

Base: 0.8.2 (rebuilt here to the installed md5 `a1a94039…` before changing anything).

**Status:**
- Compiled against the game's assemblies.
- Benched (suite + noise runs below).
- **Not flown.** 0.8.2 is kept as `.dll.0.8.2.bak`.

Numbered 0.8.3 rather than 0.9.0, because 0.9.0 is reserved for the governor law step of the port.

## Tom (4 Nov, 22:24 / 22:30 / 22:32)
"at the end of the log, the vmc allowed the healthy engine go max ab again, but it still rolled right by around 1deg/s, i thought indi would easily cover that?" … "it is 1deg/s that is tiny compared to the available budget, why wont it correct for that?" … "make the structural fix in 1 go"

## What the 22:20 log shows
With the stick released and the thrust asymmetric (engine damaged, the other at max AB), there is a standing lateral error:

| Window | Roll rate | Yaw rate vs its command | Roll authority |
|---|---|---|---|
| 39.8–50.1 s | **+0.94 °/s for 10 s** (pCmd 0) | +0.5 against −0.1…−0.5 | 300–900 °/s² |
| 74.5–80.6 s | **+0.9 °/s** (pCmd 0) | +0.5 against −0.4…0 | 300–900 °/s² |

- In released **symmetric** segments (18.8–20.8 s, 67.4–69 s) the roll rate is 0.0–0.09 °/s.
- Pitch holds within 0.02–0.05 °/s throughout.

## The cause (my first explanation was wrong)
I first blamed the allocator's costs (the roll-fight cost, the nozzle neutral hold) for leaving part of the moment unmade. That was wrong. On the bench the allocator meets its target to **0.00 °/s²**, and turning those costs off changes nothing. I built a "moment-first" allocator for that theory and threw it away: it changed nothing (shift 0.00).

The real cause is a **model mismatch inside INDI**:
- **INDI's base is the live moment.** It builds the target on the surfaces' live moment (`Mnow`, the full aero evaluation at the surface's actual deflection, filtered as `mNowF`).
- **The allocator answers from a table.** It uses the moment table `M[]`, linearly interpolated between grid points (`MAt`).
- **The pitch column was already anchored.** It is shifted every tick so the table passes through the live value at the current deflection (`SurfaceEffector.Evaluate`, the `linearPitch` block).
- **The roll and yaw columns were not.**
- **The mismatch behaves like a pilot request.** Wherever the interpolated table and the live value disagree at the current deflection, the difference is added to every tick's target. The rate loop settles where its proportional term balances it:

$$e_{ss} = \frac{\text{mismatch}}{I\,K}$$

with K_roll = 6 /s and K_yaw = 2.5 /s.

- **Symmetric flight hides it.** The left and right surfaces' errors cancel in roll and yaw.
- **An engine-out trim exposes it.** It puts differential deflection on the tails and flaps, so the errors stop cancelling.

Bench, one engine dead, 550 km/h, stick released (0.8.1/0.8.2 law):

| | roll | yaw |
|---|---|---|
| standing demand of the rate loop (ν) | **3.759 °/s²** | −0.872 |
| table − live at the current deflections | **3.755 °/s²** | −0.836 |
| allocator's unmet moment | 0.00 | 0.00 |
| result | **−0.6 °/s roll creep**, bank −3.5 → −8.4° in 8 s | yaw 0.35 °/s off its command |

## The change (`SurfaceEffector.Evaluate`, `AnchorLateral`)
- **What it does:** each tick, after the table is built, its roll and yaw columns are shifted by (live − table) at the surface's current deflection. This is the same anchoring the pitch column already had.
- **What it leaves alone:** the shift is a constant over the table. Every moment *difference* the allocator trades between deflections is unchanged; only the base now agrees with INDI's.
- **No new gate, schedule or tuning value.**
- `AnchorLateral` is an internal static, used only for the bench A/B. It is not a config key.

## Bench (Ifrit port-base bench with the 0.8.2 sources; A/B = `AnchorLateral` false / true)

**Engine out, stick released** (one engine killed at 0.5 s, full throttle; last 8 s of 16):

| IAS (km/h) | roll rate 0.8.2 → 0.8.3 | yaw-rate error | bank drift | sideslip |
|---|---|---|---|---|
| 450 | −0.37 → **0.00** °/s | 0.26 → 0.01 | −3.1° → −0.1° | 0.45 → 0.27° |
| 550 | −0.59 → **0.00** | 0.35 → 0.00 | −4.9° → −0.1° | 0.49 → 0.23° |
| 700 | −0.36 → **0.00** | 0.14 → 0.03 | −2.9° → 0.0° | 0.30 → 0.16° |

- New telemetry, measured with the fix: the tables were off by −3.4 °/s² (roll) and +0.7 (yaw) before anchoring, and the loop's standing demand is 0.005 °/s².
- On the 0.8.1-base bench: engine-out roll at 350 km/h (full roll both ways) identical (|p| max 115, peak sideslip 1.7°).
- Engine-out full pull at 300 km/h: AoA max 22.0 → 21.9°, peak sideslip 10.1 → 10.6°.

**Regression suite**, judged against the bench's own sensitivity: each case was re-run at 1480 / 1490 / 1510 / 1520 / 1530 m alongside 1500, because several cases flip between two outcomes on a 10 m change.

| Block | Samples per variant | Result |
|---|---|---|
| sweep (32 stops / reversals) | 4 | Mean roll rate 101.0 → 100.9 °/s. 155 comparisons, **1** beyond noise: the 300 km/h, 0.55-pull **roll stop**, sideslip 2.05 → **2.68°** (+0.6°). The yaw saturates a little more in the stop (yawSat 0.23 → 0.37); the roll stop is unchanged. |
| reg (20) | 6 | No metric beyond noise at \|t\| > 2.5. Two borderline at \|t\| ≈ 2.1–2.2: the **pull-then-push-over + roll at 350**, peak sideslip 3.4 → **4.3°** with AoA min −18.0 → −16.2° (it holds the push command instead of overshooting it); and the **push-then-pull-through + roll at 450**, peak roll 106 → 101 °/s. With ~120 comparisons, about this many are expected by chance. |
| cork (20) | 2 (0.8.1 base) + 1 | Nothing beyond noise. |
| dmg (32) | 6 (0.8.1 base) | Nothing beyond the six-sample spread. |

**Not reproducible on the bench:** the damaged-engine case of the log (engine running at reduced thrust). The bench's engine condition doesn't reduce thrust promptly, so the bench evidence is the dead-engine case. The mechanism is the same: a differential trim on the surfaces.

## Telemetry
New columns at the end, after `betaCg`, before the per-surface columns:
- `nuR`, `nuY`: what the roll / yaw rate loops ask for (°/s²). With the stick centred and the jet holding its command, these should sit near 0. In the 22:20 log the standing values would have been about 5 and 2.
- `anchR`, `anchY`: how far the surfaces' roll / yaw tables were off their live moment this tick before anchoring, summed (°/s²).

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `3b95b575827193e982b8b0bda603f10f`) and `meta.json` 0.8.3. 0.8.2 is kept as `.dll.0.8.2.bak`.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.8.3-release`. The source zip includes the bench (the 0.8.2 port-base bench).

## Same issue on the other jets (not changed here)
- The F-22E and the Revoker FCS were ported from the same code, so their `SurfaceEffector` tables probably have the same pitch-only anchoring. **Not checked.**
- If they do, they would creep the same way with a differential trim (an engine out, an asymmetric load, damage).
