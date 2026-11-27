# F-22E Strike Raptor FCS 5.0.2 (Nuclear Option)

Base: 5.0.1. **Bench-checked, installed, not flown.**

Asked for (26 Nov 21:22): "idk if i have the settings right but the stuck happened again" (log `F22E_FCS_20261126_212151.csv`).

## What happened in the 21:21 log
- **Settings.** As intended (TVC off, `BasinScreen` on, `TwoZoneStabClampPitchMargin` off), except `PairAllocator` was still **on**. That doesn't matter here: the pair search can't free this pose.
- **The stuck pose.** 23.9–25.7 s, about 229 kt. Left stab up to +31, right stab to −30, roll the allocator couldn't make up to 281 deg/s², AoA to −2.5. It freed only when AoA went negative.
- **The screen saw it but never switched.** Its score of what was left of the target rose to 200,000, and `basinSw` stayed 0.

## Why the 5.0.1 screen failed
I replayed the log pinned to the game's state, with the game's roll commands and the game's stab and rudder positions; the bench's target then matches the game's.
- **The same allocator cost really is far lower on the other branch.** At 25.0 s the trap costs 8.4 and the other branch (left −5, right +10, everything else re-solved) costs 0.006, about 1,400× lower. At 25.4 s it's 30 against 0.02.
- **The screen did find that pair and ran its real check.** But the check used the allocator as it normally runs, with two later stages that look at where the stabs are on the way rather than where they're going:
  - **The two-horizon pick** (2.42). It scores the trip there equally with being there. For a trip across a stab's lift peak, the stabs pass through bad poses on the way, so it always chose to stay.
  - **The reach pass** (2.41). It scores the plan with the stabs where they can get to in 0.1 s, so the seeded plan cost 58 against the trap's 8.4.
- **So the check always rejected the switch.**
- **Why the 20:10 bench case still passed.** There the bench's own target and stab positions differed from the game's.

## Fix
In the screen's check, both plans (stay, and the other branch) are now judged where they are going, without the two-horizon pick and the reach pass. If the switch is taken, one more solve places the other surfaces against where the stabs will be on the way (the reach pass, from the new commands), and the stabs are commanded to the other branch.

These are per-allocator flags (`noTwoHorizon`, `noReach`), passed to the native core through its existing `twoHorizon` / `reachPass` inputs. No Rust change; native and C# stay byte-identical on the bench.

## Bench
**Replay from the game's stuck state.** Pinned to the log until 24.3 s, then flown freely with the game's roll command:

| | 5.0.1 or screen off | 5.0.2 |
|---|---|---|
| Stabs | stuck at L +18…+31 / R −28…−30 until AoA goes negative | switch at 24.9 s, R stab +7, L −3 by 25.4 s |
| Roll the allocator can't make | up to 336 deg/s² | 0 from 25.4 s |
| Roll rate | 151 → 98 deg/s | dips to 135, back to 160 deg/s |
| AoA at 27 s | −16.7 | −7.9 |
| Peak sideslip | 12.7 | 4.0 |

The commands wobble for about 0.3 s during the switch (left stab +7.5 → −6 → +1 → −3).

**Closed-loop reproduction** (`tools/grel.py`, `TwoZoneStabClampPitchMargin` off): no case gets stuck. The 540 km/h, 1.2 s, 60% case: sideslip 4.1 → 2.7, AoA min −8.0 → +1.6, roll 168.6 → 166.3 deg/s. The others are within ±1.5%.

**Battery against 5.0.0 (defaults):**
- **TVC off:** sideslip worse by more than 1° in 1 case (proll36 CG +5, 2.1 → 3.1), better in none. Roll rate −3.8 … +7.2%.
- **TVC on:** worse in none, better in 4 (rev30 5.8 → 2.1, 4.6 → 2.1, …). Roll −4.3 … +5.1%.

**Full suite against 5.0.0.** Most rolling cases change a little.

| Group | Worse > 1° | Better > 1° | Mean |
|---|---|---|---|
| Reversals | 2 | 1 | −0.1° |
| Corkscrew entries | 1 | 0 | +0.2° |
| Regression set | 0 | 0 | −0.1° |

- **Worse:** pull+roll reversal at 450 km/h 13.1 → 15.7; at 556 km/h 10.9 → 12.4; pull-then-roll corkscrew entry at 400 km/h full thrust 12.1 → 14.9.
- **Better:** full-aft roll reversal at 500 km/h 11.7 → 7.0.
- **Unchanged:** utilisation pass/fail (same count). Damage suite within about 2.5°; the only departure is the existing one (fins + R flaperon, 890 km/h).

**CPU:** as 5.0.1, plus one more allocator solve (about 45 µs native) on the ticks where the screen runs its check, and one more on the ticks it switches. In the reproduction: 5 checks, 4 switches over 11 s.

## Settings for the next test (TVC off)
- **Main cfg** (`Aryx_F22E_StrikeRaptor.FCS.cfg`): `1. General` `DisableTVC = true`.
- **Dev cfg** (`Aryx_F22E_StrikeRaptor.FCS.Dev.cfg`):
  - `0. Debug` `Telemetry = true`
  - `4. Gains` `BasinScreen = true`
  - `4. Gains` `PairAllocator = **false**`
  - `5. Airframe` `TwoZoneStabClampPitchMargin = false`

The startup line in LogOutput.log should read `F-22E 5.0.2 basin screen on, slats segmented; pair allocator off (4.8.17); … no-TVC stab clamp margin 8 deg (4.8.16)`.

## Caveats
- **Bench only.** Nothing here has been flown.
- **The switch ignores the cost of the trip.** It's judged at the destination, with the 2× screen and 30% real margins standing in for the trip.
- **The bench's own target diverges from the game's in these rolls.** It differs by up to 30 deg/s of roll command just before the stuck pose. The replay evidence above uses the game's own commands and stab positions.
