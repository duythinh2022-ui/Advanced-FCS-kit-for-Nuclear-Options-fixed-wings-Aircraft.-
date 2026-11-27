# Vortex FCS 0.4.5: airbrake at 500–600 kt, the root cause

17 Nov 2026. Built on 0.4.4 (the STOVL and HUD-text builds 0.4.2–0.4.4 from the other chat are carried unchanged). One fix, in the allocator (C# and the native core). **Benched, not flown.**

The airbrake code and the allocator are the same in 0.4.1–0.4.4, and the bench's CTOL sets come out line for line the same on 0.4.1 and 0.4.4, so "0.4.4" below also stands for the 0.4.1 the log was flown on.

Tom's reports:
- 21:05: "i brake at 500-600kt+, the nose pitched down and the jet rolled to the left."
- 21:51, on 0.4.1: "it cycles between straight and roll and nose down. Visually, the control surfaces deflection look symmetrical but it then they moves a slight bit causing the roll and nose down."
- 22:07: "fix the vortex speedbrake issue".

## 1. Cause

A bug in the allocator's roll-fight cost.

- **What the cost is for.** With the stick centred the allocator charges for trailing-edge pairs that roll against each other (`RollFightCost`, 5). For a stabilator-kind pair it is meant to count only the pair's **split** (differential), not its common pitch deflection (`FightStabSplitOnly`, from the F-22E 2.2.21).
- **The bug.** The pair link is stored one way, left → right. The split-only rule therefore held for the left surface alone. The right surface's whole roll from zero deflection was counted, its pitch deflection included.
- **Why it only shows with the brake.** On the Vortex the inboard elevons are the stabilator-kind pair. In normal flight at speed they sit near 0.4°, so the error is tiny. With the brake set in they carry the rudder flare's pitch: +5 to +7° on both.
  That read as **189 deg/s² of roll that is not there**, priced at 54.6 when a whole solve costs about 1.4.
- **Why it comes and goes.** The phantom roll is charged in full while the roll target is more than 15 deg/s² to the left of the surfaces' zero-deflection roll (`FightTargetMin`). Nearer zero it is not charged itself but still decides which pair is charged. With the brake out the game's roll differs a little from the FCS model (about 18–24 deg/s²), which puts the target right at that threshold.
- **What the allocator then did** (the 21:46 log):

| | "rolls / nose down" | "flies straight" |
|---|---|---|
| Inboard elevons | 2.6 / 1.0 (pitch share dropped) | 7.4 / 4.3 |
| Outboard elevons | 9.9 / 9.9 (set target is 6.0: the hold is broken) | 5.0 / 7.0 (crossed against the inboard pair) |
| Rudders | −15.7 / −14.6 | −16.5 / −16.2 |
| Its own targets missed | roll 20–28, pitch ~5, yaw ~6 deg/s² | none |
| Jet | rolls left to −3.5 deg/s, 0.6 g | p ≈ 0, 0.9 g |

It left the first set when the roll error had pushed the target back over the threshold (~0.6 s), and fell back in when the crossed set had unwound (~1.7 s).

**Fix:** the fight cost uses the pair link both ways (`Allocator.sidx`), so both surfaces of the pair count their split only. Same change in the native core (`native/fcs_core/src/lib.rs`). No new setting, no change to the laws or to the brake set.

## 2. Bench: the FCS posed on the log's own states

The bench FCS is put on the logged state (attitude, speed, rates, surfaces at their logged positions, brake in) and solves from there.

| Log time | 0.4.4 | 0.4.5 |
|---|---|---|
| 195.4 s (rolling) | stays where it is: inboard 2.65 / 0.99, outboard 9.91 / 9.87; misses roll / pitch / yaw by 20.3 / 4.7 / 4.7 deg/s² | inboard 6.64 / 5.85, outboard 6.01 / 5.99, rudders −16.33 / −16.09; nothing missed |
| 194.6 s (straight) | holds the crossed set: inboard 7.43 / 4.34, outboard 5.02 / 6.98 | inboard 6.12 / 5.76, outboard 6.02 / 5.98; nothing missed |

Cost of the 195.4 s solve in 0.4.4: 1.37 where it sat (target error 0.27, brake hold 1.04, fight 0.05); the set 0.4.5 picks was priced 54.6 (fight 54.59). With `RollFightCost=0` 0.4.4 also solves it, which is how the term was found.

## 3. Bench: closed loop

Tom's config. Log-2 condition: 1125 km/h, 900 m, idle, 6.5° climb, brake at 2 s, stick centred, 14 s. The bench plant equals the FCS model, so the game's roll mismatch is added as a steady roll offset (`rollExt`). `bench/scripts/vortex/brakecycle.sh` runs this.

| Roll offset | | 0.4.4 | 0.4.5 |
|---|---|---|---|
| +25 deg/s² | Bank after 14 s | 0 → **−19.0°** | 0.4° |
| | Switches between the two sets | 17 | 0 |
| | Roll target missed by > 10 deg/s² | 35 % of the time | 0 % |
| | Command reversals (> 0.3° steps) | 738 | 0 |
| | g | 0.87–1.19 | 0.94–0.96 |
| −25 deg/s² | Bank after 14 s | −0.4° | −0.4° |
| 0 | Bank after 14 s | 0.0° | 0.0° |

The −25 row is why it always went left, and why the earlier disturbance test (the other sign) showed nothing.

**Brake opened, stick centred, no offset** (300 / 500 / 700 / 900 / 1138 / 1200 km/h at 250 m): bank 0.0°, sideslip 0.00° in both builds. Largest missed roll target 3.5 → 0.3 deg/s² at 1138, 4.7 → 0.3 at 1200.

**Replay from the log's state at 190.0 s** (the opening itself; run on the 0.4.1 source and the fix): the old build forms the crossed set (inboard 6.85 / 3.79 against outboard 4.32 / 6.68); the fix keeps both pairs symmetric within 0.02°. Lowest g 0.39 → 0.53.

**Brake out, then manoeuvre:**

| | 0.4.4 → 0.4.5 |
|---|---|
| 900 km/h full roll 1.4 s: peak rate | 250.2 → 250.2 deg/s (onset curve the same within 1 deg/s) |
| Roll stop: lowest rate after release | −5.7 → **+1.0 deg/s** (no reversal) |
| Roll stop: under 5 deg/s | 0.43 → 0.47 s |
| Roll: missed roll target, max | 54 → 16 deg/s² |
| 900 km/h full pedal 2 s: lowest g | 0.47 → **0.80** |
| Pedal: bank drift | −10.6 → −1.4° |
| Pedal: missed roll / yaw target, max | 84 / 30 → 46 / 11 deg/s² |
| 500 km/h full pedal: roll rate excursion | −6.2 → −2.5 deg/s; command reversals 71 → 21 |
| 500 km/h roll, 900 / 500 pull to 6.3–6.7 g, brake in a held 5 g pull, brake in a turn at 19° AoA, 650 kg on the left wingtip at 700 km/h | same within rounding |

## 4. Bench: everything else

The fight cost acts in every stick-centred phase, so the whole set was run again.

- **The suite is very sensitive.** A 0.2 % fuel change on the unfixed build alone moves the regression set's peak sideslip by 0.48° rms (1.0° max). So each build was run at several fuel states.

| Block | 0.4.4 → 0.4.5 |
|---|---|
| Regression set, 20 cases × 4 fuel states: mean peak sideslip | 3.75 → 3.88° |
| The four cases that moved most, × 11 fuel states: mean peak sideslip | 3.9 → 4.0, 6.0 → 6.1, 4.3 → 4.3, 4.0 → 4.3° (the unfixed build's own spread: up to 3.1–6.0°) |
| Peak roll rate in those cases | 138 → 138, 130 → 130, 80 → 80, 78 → 79 deg/s |
| Roll stops (sweep): sideslip mean / worst, time | 3.38 → 3.40 / 5.0 → 5.0°, 0.65 → 0.63 s |
| Roll reversals (sweep): sideslip mean / worst, time | 4.39 → 4.34 / 7.2 → 6.9°, 0.32 → 0.31 s |
| Corkscrews, 20 cases: peak sideslip mean, velocity-roll mean | 4.09 → 3.99°, 45.3 → 46.0 deg/s |
| Falling-leaf stop 296 km/h × 9 fuel states: to under 5 / 3 deg/s | 2.96 → 2.92 s / 3.95 → 3.97 s; leaf rate 22.4 → 24.5 deg/s |
| Falling-leaf stop 370 km/h × 9 fuel states: to under 5 / 3 deg/s | 2.54 → 2.55 s / 2.99 → 3.17 s; leaf rate 26.5 → 25.7 deg/s |
| STOVL set (mission, VTO, hover pedal / crab at four weights, touchdowns), Tom's present config | every summary line identical; surfaces differ by up to 2° for moments |
| Native solve against the C# solve | 20,062 solves, all bit-identical |

I read these as no change outside the brake. The bench cannot resolve differences smaller than its own scatter.

## 5. Not fixed, not checked

- **Flown in game: nothing.**
- **The opening dip stays.** On the bench g drops to 0.69–0.77 for about 0.6 s as the set comes in at 900 km/h and up (0.84 at 700, 0.94 at 500), then holds 0.93–0.97 while the jet slows. Same as 0.4.1–0.4.4. In the 21:46 log the first second also had the allocator fault (0.20 g lowest), so the game's dip should be smaller now; I can't say by how much.
- **`AirbrakeRudderFlareWindow` (0.4.1) stays on.** With it off and this fix in, the 25° flare no longer rolls the jet (bank within 1.7°; 0.4.0: 17–26°), but one rudder still reaches its window edge before the other and sideslip builds to 2.3–4.8° at 900 km/h and up. The alternative without that logic is `AirbrakeRudderFlare` 14.
- **The bench's brake is three to four times the game's** at this speed (bench ~1.1 g of deceleration, log ~0.3 g). The airbrake panel's drag model is unverified; brake-out speeds and times on the bench are not the game's.
- **The other six FCS mods carry the same lines** (checked in the F-22E 4.5.1 source: same one-way link, same rule). Not changed. On them the stabilator pair is near zero at speed with the stick centred, so the error is small there; it grows with the pair's pitch deflection.
- **Damage block:** only its clean rows run on the Vortex bench (the damaged configurations name Revoker parts and stop with an error in both builds).
- `gInt` at 0 in the first log (open question 3 of the findings): not looked at.

## 6. Build and files

- **Native core:** rebuilt from `native/fcs_core` with Rust 1.97.1 (`native/build.sh`). The same toolchain rebuilds the 0.4.0–0.4.4 core from its source to within 9 bytes (time stamp and checksum). ABI unchanged (2013001); the plugin unpacks the library by its hash, so the old copy is not picked up.
- **Bench:** `replay` now feeds the logged brake (`abTgt`) and airbrake position; `REPLAYDBG=1` prints the posed solve and prices alternative sets (`ALLOCALT`); `bench/scripts/vortex/brakecycle.sh`.

## 7. Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.4.5 and `meta.json`; 0.4.4 kept as `Vortex_FS20_FCS.dll.0.4.4.bak`.
- Dev DLL unchanged. No config change.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.4.5-release`.

## 8. To check in flight

500–600 kt, idle, stick centred, brake held 15 s (the 21:46 run). Expect both elevon pairs symmetric and steady, no roll, g back near 0.9 after the first second. Then brake out with a roll and a pedal input.
