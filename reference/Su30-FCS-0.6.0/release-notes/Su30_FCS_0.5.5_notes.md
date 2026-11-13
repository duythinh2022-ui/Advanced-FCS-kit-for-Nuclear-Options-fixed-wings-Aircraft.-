# Su-30VXN Gandiva FCS 0.5.5: flaperon flaps on the F-22E schedule, gear lever out of it

Base: 0.5.4. Tom, 11 Nov: "the landing gears state effects flaps logic, can u get rid of that, and make the flaperons work like on the raptor".

**Changelog:** The flaperons' flap droop no longer depends on the landing gear. It now follows the F-22E FCS's flap schedule at every gear state:
- speed and AoA droop, plus up to 8° of manoeuvre droop, 23° at most;
- held under the flaperon's stall margin;
- dropped when you roll (roll priority);
- dumped when you brake on the ground.

Before, the droop was the game's landing flap: 15° whenever the gear was down, 0 when it was up. On the bench, at 1 g the droop is about 20° at 119 kt, 14° at 140 kt, 10° at 162 kt and 2° at 216 kt. Holding 2.5–3.4 g at 135–270 kt it droops 4–15°, so the same g needs 0.5–1.2° less AoA. In full rolls it stays near 0, and roll rate is unchanged. New dev settings: FlaperonFlapSchedule (on; off = 0.5.4 exactly) and FlaperonFlapMax (23°).

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,500,160 bytes, SHA-1 e508117fce9f310c526f20a2fc8df6fb16a8b485. Native core unchanged.
- Run on the game's Mono under Wine:
  - the flap path JIT-compiles against the game's Assembly-CSharp: `LandingFlapJob`, `GameFlapStep`, `Hooks.SurfacePostfix` and `FlapSystem.Update`. That includes `ControlSurfaceFields.gearState` (a GearState), `pitchRange` and `currentPitch`;
  - GearState values are Uninitialized 0 … LockedExtended 2 … Extending 4;
  - the gear patch still applies.
- Benched with a new model of the game's landing-flap hinge (below). That is an approximation of the game's aero response to the hinge; the hinge logic itself is the game's.
- **Not flown.**

## How it works
- **The hinge.** In the game the Su-30's flaperon sits on the landing-flap hinge: `Flaperon_L/R_LandingBias` → `Left/Right_flaps_hinge` → the flaperon lift normal. Rotating the landing flap therefore rotates the flaperon and its aero.
- **The game's flap logic** (`ControlSurfaceJob_Math`, flap surface):
  - when the job's `gearState` is LockedExtended or Extending, it slews `currentPitch` down at the servo speed (40 deg/s);
  - otherwise it slews it up;
  - then it clamps the result to [−pitchRange, 0].
- **0.5.5.** Every tick, `Hooks.SurfacePostfix` (after the game copies the gear state into the job) does two things:
  - sets the job's `gearState` to LockedExtended, always;
  - sets `pitchRange` to the FCS's wanted droop for this tick, stepped toward the target at 40 deg/s.

  The game's own clamp then lands the surface exactly on that angle. The gear lever no longer enters it.
- **The target** is the F-22E's flap schedule, already running in `FlapSystem` (`flapBias`):
  - the speed schedule × the AoA/load terms;
  - plus manoeuvre droop up to 8° at 10° AoA, faded out from 20° to 30° AoA;
  - 23° at most (FlaperonFlapMax);
  - slewing out at 20 deg/s and in at 60 deg/s;
  - times (1 − FlaperonRollPriority × roll demand), as before.
- **Stall cap, as on the F-22E.** With no separate flap drive, the cap is worked out on the flaperon itself: the droop is held to the angle the flaperon's stall margin allows (the present droop removed first), leaving 12° × |stick roll| for the roll. This is the F-22E's `DroopCap`.
- **On the ground** (weight on wheels and under 110 m/s), the schedule gives the F-22E's ground setting, and braking dumps it. That is wheel contact, not the gear lever, as on the F-22E.
- **FlaperonFlapSchedule off** puts back the 0.5.4 path: the game's gear-driven 15°, cut by roll.

## Bench
New in the plant: the two `LandingBias` hinges, driven by the game's rule (0.5.4 path) or by the FCS's `GameFlapStep` (0.5.5). Air cases have the gear up, so 0.5.4 shows no droop there.

### Schedule off = 0.5.4
Reg 20, cork 20 and sweep 32 cases with FlaperonFlapSchedule off are bit-identical to the 0.5.4 / 0.5.2 suite runs (max difference in alpha, beta, p, q, r, IAS = 0).

### What the droop does in the air (1,500 m, full thrust, mean over the hold)
| Case | Droop 0.5.5 (mean / max) | AoA 0.5.4 → 0.5.5 | g | Roll rate (mean) |
|---|---|---|---|---|
| 1 g, 119 / 140 / 162 / 216 kt | 19.6 / 14.2 / 9.7 / 1.7° | — | 1.0 | — |
| Pull 0.3, 135 kt | 15.3 / 16.9° | 11.6 → 10.4 | 2.54 | — |
| Pull 0.3, 189 kt | 6.5 / 7.5° | 9.0 → 8.0 | 3.18 → 3.13 | — |
| Pull 0.6, 270 kt | 8.0 / 8.0° | 12.2 → 11.9 | 6.09 → 6.15 | — |
| Pull 1.0, 378 kt | 7.7 / 8.0° | 10.4 → 9.8 | 8.86 | — |
| Pull 0.6, 135 kt (43° AoA) | 0.6 / 8.1° (stall cap) | 43.6 → 42.6 | 2.2 | — |
| Full pull, 135–270 kt (69–72° AoA) | 0 | same | same | — |
| 1 g full roll, 135 / 189 / 270 / 378 kt | 1.7 / 0.5 / 0.1 / 0° | same | — | 83→81, 116→115, 161, 194 deg/s |
| Pull 0.5 + full roll, 135 / 189 / 270 kt | 0.3–0.5° | 26.9→24.5, 15.2→14.0, 11.5→11.4 | — | 60, 97→105, 143→142 deg/s |

Speed at 6 s: within 3 km/h of 0.5.4 in every case.

### Air suites, 0.5.4 → 0.5.5 (the droop is now there in the air, so these move)
- **Sweep, 32 roll stops and reversals:** worst sideslip in a reversal 15.7 → 13.9°; entry sideslip 4.83 → 4.57°; roll and reversal time unchanged.
- **Reg, 20 cases:**
  - mean peak sideslip 8.47 → 7.96°, worst 18.9 → 12.7° (pull-then-pushover + roll, 350 km/h);
  - worse in 8 / 20. The largest of these:
    - push-then-pull-through + roll at 350 km/h: 7.6 → 10.3°;
    - roll-then-push at 200 km/h: 5.8 → 6.8°.
  - Rudder pitch saturation (ysp) mean 10.1 → 13.1, worst 36 → 43.
- **Cork, 20 high-AoA rolls:**
  - mean peak sideslip 9.8 → 9.7°;
  - worst 18.2 → 22.0° (full pull + roll at 550 km/h, the chaotic departure case);
  - AoA hold and roll rate about the same.
- **Damage suite (the Revoker's; only its clean rows run on this airframe, the damaged configs error in both versions):**
  - pushover at 560 km/h: peak sideslip 10.1 → 14.0° released, 15.2 → 14.0° held; AoA min −24.8 → −37.4° released;
  - pushover at 600 km/h: 13.5 → 8.8° released, 13.5 → 12.5° held.

Net: no clear gain or loss in departure resistance; the roll rate is kept.

### Take-off (gear down, so 0.5.4 had the 15° landing flap)
- **Stick centred, full throttle:** neither version lifts off in 40 s. On the roll 0.5.4 settled 2.9° nose-down and 0.5.5 stays at 0°. 0.5.5 is also faster at 40 s: 1,420 vs 1,284 km/h (bench, no gear drag limit).
- **Rotation at 6.5 s full pull, 7.5 s half pull, 9 s 0.3 pull:** nose-off, mains-off and theta > 8° land on the same ticks, ±0.08 s.
- **Climbing out with the gear left down:** 0.5.5 retracts the droop on schedule; 0.5.4 kept 15°.

## Files
- `Plugin.cs`: FlaperonFlapSchedule, FlaperonFlapMax ("5. Airframe").
- `FcsController.cs`: `LandingFlapJob` (new branch), `GameFlapStep`, `gameFlapNow`, `landFlapDbg` (now also the droop with the schedule on); `flaps.gameFlapSchedule` set before `flaps.Update`.
- `FlapSystem.cs`: `gameFlapDroop` / `gameFlapSchedule`, and the flaperon stall cap on the game flap.
- Bench: `sim/Plant.cs` `GameFlaps()`; `stubs/Game.cs` GearState in the game's order, `ControlSurfaceFields.gearState`.
- `native/verify/flapcheck.cs`: the Mono JIT check above.
