# FS-3 Ternion FCS 0.1.1: AoA limits above 45 are no longer capped at 45

Base: 0.1.0. Compiled and benched. **Not flown.** 0.1.0 is kept as `.dll.0.1.0.bak`.

## Tom (7 Nov, 00:07)
"seems like when i make aoa limit to 50, the jet can only do 45 max and it wont do more, same for mpo. But seems like it handles just fine at 45. Would making the stabs stall earlier and make the canards a handle the potential pitch up improves roll authority at high aoa?"

## What the "latest log" holds
- **No flight data:** there is no Ternion telemetry CSV, because `Telemetry` is false in `Ternion_FS3.FCS.Dev.cfg`. `LogOutput.log` only has the start-up lines.
- **Your config:** `AoALimitPositive` 60, `PerformanceProfile` Buffed.
- **Blueprint version:** Blueprinter loaded `FS-3.Ternion_1.0.1.nobp` from `plugins\com.nikkorap.blueprinter\addons\blueprinter.ternion`. 0.1.0 was extracted from 1.0.0.
- **1.0.1 vs 1.0.0:** I re-extracted 1.0.1 and diffed it. Parts, masses, areas, surfaces, ranges, servos, engines, tanks, HLDs, airfoils and the stock FBW are identical. The only differences are float noise in the drag areas and a `streamlining` field that now reads 0.

## Why it stopped at 45
The fixed no-TVC cap, inherited from the F-22E.

- **The rule:** when the nozzles cannot cover the nose-down need, the limit becomes a fixed `NoTvcAoACapPositive` (45). The test is vectorable T/W below `LowThrustTW`.
- **On the FS-3:** that T/W is always 0, so the fixed cap was always on. Every limit above 45 flew to 45, MPO included.
- **Why the 0.1.0 bench missed it:** its sweeps overrode `NoTvcAoACapPositive` to 65.
- **Reproduced on the bench (old rule):** `AoALimitPositive` 60, full aft from 250 / 450 km/h → exactly 45.0 in both.

## Change
`noVecJet`: a jet with no vectorable nozzle at all. Its engines have no vectoring transform, or no pitch vectoring.

- **Positive side:** the fixed cap is not applied on such a jet.
- **Negative side:** the fixed `NoTvcAoACapNegative` (45) is not applied either.
- **What limits the AoA instead:**
  - `AoALimitPositive` / `AoALimitNegative`;
  - the swept authority cap (`authCap`): the highest AoA at which the canards and elevators can still bring the nose down, re-swept as the speed changes;
  - the fin-loss and engine-out caps, as before.
- **Bench switch:** `FcsController.NoVecUncapped`. false = 0.1.0's behaviour.

Bench, `AoALimitPositive` 60, full aft:

| From | 0.1.0 | 0.1.1 |
|---|---|---|
| 450 km/h | 45.0 flat | 54.1 peak; 47.7 at 6 s; 42.3 at 181 km/h, where the authority cap is 42.3 |
| 250 km/h | 45.0 | 49.6 peak; 41.5 at 172 km/h (cap 41.4) |

- **What you'll see:** above 45 you get what the canards can still stop. As the speed bleeds, the authority cap walks the limit down: ~42° at 170–180 km/h.
- **Defaults are unchanged.** `AoALimitPositive` stays 40 in a fresh config, which is under 45, so a fresh install flies exactly as 0.1.0. Bench: full aft from 450 km/h → 40.0.

## Your question: earlier elevator stall, canards taking the pitch-up
**Short answer from the bench: no.** It does not improve high-AoA roll, and it makes the pitch side harder.

I added `ElevatorStallAoA` (dev, default 0 = the game's curve, peak 45°) to test it. It gives the aft elevators the flaperons' knee-preserving staggered curve, with the peak moved earlier. Each case is a full-aft pull, then full roll and a reversal, at limits 45 / 50 / 55 and 250 / 350 / 450 km/h, with the 0.1.1 cap fix.

Steady velocity-vector roll (4.6–7 s), deg/s, range over 250 / 350 / 450 km/h:

| Elevator peak | Limit 45 | 50 | 55 | Canard + elevator activity (rms deg/s) |
|---|---|---|---|---|
| Stock (45) | 25.6–26.3 | 22.6–24.3 | 21.4–22.3 | 75–81 |
| 35 | 24.8–26.5 | 22.5–23.9 | 21.6–23.2 | 56–76 |
| 30 | 24.0–25.1 | 21.1–22.7 | 19.9–23.5 | 54–72 |
| 26 | 24.3–24.9 | 21.0–23.5 | 21.4–23.5 | 57–71 |

AoA held and sideslip (≤ 2–3.5°) are the same within noise.

Why it doesn't help (the 50° limit at 350 km/h, stock elevators):
- **The roll is held back by the yaw side, not by roll authority or the elevators.**
  - The commanded roll is at the governor's rate cap (`rateCap` ~18 deg/s), set by the yaw-capacity estimate: ~25 deg/s² against ~187 for roll.
  - The roll command is met (p 15.4 vs pCmd 16.3).
  - The rudders average ±13–15° of their 35 and never reach the stop.
- **The canards are already the pitch limit.** They sit on their 60° leading-edge-down stop 41–46 % of the time in that roll.
  - Taking the tail's lift away by stalling it earlier is more pitch-up for the canards to absorb.
  - Their nose-down travel is already what the authority cap runs out of at 45–55°.
- **What it does buy:** it slightly calms the pitch surfaces (activity 56–72 vs 75–81 deg/s rms), with a 1–2 deg/s lower roll rate.

**If the goal is more high-AoA roll,** the lever is the yaw side:
- the rudders' camber coupling (0.54);
- `FinCamberCoupling`;
- the canards' yaw (their anhedral already gives some);
- or how the capacity estimator counts the rudders at 45–50°: it reads 25 deg/s² while the rudders use under half their travel.

That last one is worth a look before any airframe change. `ElevatorStallAoA` stays in the dev file at 0 if you want to try it in game.

## Not verified
- **Not flown.** The bench is uncalibrated for this jet.
- **No flight data:** for the next flight, set `Telemetry = true` in `Ternion_FS3.FCS.Dev.cfg` ("0.1 Debug") so there is a `Ternion_FCS_<time>.csv` to read.
- **Above 45 at low speed:** how it feels in game is unknown. The bench's authority cap brings the limit down to ~40–42 by 170–180 km/h.

## Install (done on your PC)
- **Main:** `plugins\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` (md5 `4d738249917bc99052ab6b1e4787ab31`), with `meta.json` 0.1.1. 0.1.0 is kept as `Ternion_FS3_FCS.dll.0.1.0.bak`.
- **Dev unlock:** `plugins\Ternion_FS3_FCS_Dev.dll` 0.1.1 (`ElevatorStallAoA` is a new key in it).
- **`LogOutput.log`:** "FS-3 Ternion FCS 0.1.1 … ready".
- **Release folder:** `C:\Games\NO mods\Ternion-FCS-0.1.1-release`.
