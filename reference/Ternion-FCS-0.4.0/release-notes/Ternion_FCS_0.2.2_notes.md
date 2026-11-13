# FS-3 Ternion FCS 0.2.2: heavy asymmetric load, no more 7° AoA drops or pitch wobble

Base: 0.2.1. Fixes what Tom's 11 Nov 09:38 flight showed: the Piledriver (2,500 kg) on the right inner pylon, pitch wobbling, and the AoA limit suddenly falling to 7° when it had been 20–30° at about the same airspeed.

**Changelog:** With a heavy asymmetric load, the AoA limit no longer collapses to 7° or wobbles in pitch. The limit now comes from what the jet can actually do at that moment:
- roll it can still deliver each way while holding pitch, using only surfaces that are not stalled;
- nose-down it has in hand, with a safety margin that scales with airspeed.

The slats follow AoA instead of g. Thrust offset from the centre of gravity (CG) counts as part of the load.

**Status:** compiled against the game's assemblies and benched. **Not flown.**

## Why it happened (and why the F-22E didn't)
- **0.1.10's `LiveAuthCap` was the culprit.** It guessed the AoA limit by extrapolating the measured nose-down authority, i.e. how hard the jet can still push the nose down. It had a 7° floor.
  - Reconstructed offline from the 09:38 log: it was the binding limit 48% of the time with full aft stick, and sat at its 7° floor 21% of that time. Those are the 7° drops.
  - Each time the measured authority recovered, the limit jumped back up. That is the pitch wobble.
- **The F-22E has none of the no-TVC add-ons.** No LiveAuthCap and no 0.5× capture brake, and its pitch-cap floor is 10°. Tom's Raptor log (TVC off, same kind of load) held a flat 12° at 83–98 m/s.
- **LiveAuthCap off alone is not enough.** On the bench the jet then departs once a wing's surfaces are shot off, because LiveAuthCap was what caught that case. So 0.2.2 replaces it with a check that is correct in both cases.

## The changes (all bench switches; with all of them set back, 0.2.1 is reproduced exactly)
| Switch (default) | What it does |
|---|---|
| `CoupledCap` (on) | A small linear program (LP, an optimiser that finds the best mix of surface deflections under limits) is solved at each point of the asymmetric-load AoA sweep. It finds the most flight-path roll available each way with pitch held (no nose-up acceleration) and no sideslip build-up, plus the nose-down left over while holding the load. The AoA limit is where that roll reserve runs out. |
| `CplAttachedOnly` (on) | The LP may only use stabilator / canard positions the allocator would itself use: elevators and stabs within their stall AoA, and canards inside the attached window above 30° AoA. Before this, the LP counted stalled elevator and windowed-out canard deflections. It promised 18–60 deg/s² of roll the allocator could not deliver (`gUnmet` −35 to −47), so it let the jet into the slow, heavy corner. |
| `CplBetaDesign` (3°) | Each side's roll reserve is solved with 3° of adverse sideslip already acting. |
| `CplLateral` (on) | The coupled check (not the one-axis check) sets the lateral cap. With it off, the damaged-wing pulls depart at 500 km/h. |
| `LiveAuthCap` (off) | The 0.1.10 extrapolated limit. Replaced by the above. |
| `AsymThrustCgLoad` (on) | The engines' yaw and roll about the real CG (thrust × lateral CG offset) count in the load all the time, not only with an engine out. The F-22E already does this through its nozzle model. This closed the yaw residual on the bench calibration. |
| `AsymPitchMarginIasRef` 130 m/s, `AsymPitchMarginMin` 6, `AsymPitchAtPredQ` (on) | The 20 deg/s² nose-down margin of the pitch cap scales with dynamic pressure (q, roughly "how hard the air pushes", ∝ IAS²) below 130 m/s IAS, judged at the q predicted a moment ahead. The floor is 6 deg/s². Before, a fixed 20 deg/s² at low speed made the pitch limit fall from 45° to 7° as IAS bled (168–176 s in the 09:38 log). |
| `SlatAoASchedule` / `SweepSlatSchedule` (on) | In flight with the gear up, the slats follow AoA only, like a fighter's leading-edge flaps. Before, they went through a g gate, so at 65 m/s they tracked load factor and every pull/release stepped lift and pitching moment. Ground and gear-down schedule unchanged. The AoA sweep uses the same schedule. |
| `CplDebug` (off) | Dev only: prints the LP's surface mix against the real surfaces. |

New telemetry columns: `cplU`, `cplRp`, `cplRn`, `cplDn`, `slatSched`, `pitchMarg`, `thrYLoad`, `cplVbP`, `cplVbN`.

## Bench first (calibrated against Tom's logs)
Before judging anything, the bench was checked against the game at high AoA:
- **Camber coupling:** the bench now uses the game's per-family stall table (flaperons 26°, rollervons 45°).
  - Roll equation error at 40–50° AoA: 10.3 → 3.4 deg/s² RMS.
  - At 50–60°: 6.0 → 1.8.
- **Thrust yaw:** added from the CG offset.
- **Stores** fitted from the blueprint (TBM on `wing_2a_R`).
- **Result:** 0.2.1 on the bench reproduces the 09:38 flight. The cap drops to 7° at 118–122 s, and the AoA sticks at 7–10° at 156–182 s and 236–260 s.

## Bench results (0.2.1 → 0.2.2)
**09:38 log windows replayed (82–260 s, fitted store layout):**
- 0.2.1: the 7° drops and pushes as in the game.
- 0.2.2: no 7° drops and no pushes. The AoA holds steady at its cap (21–35°), with |β| ≤ 2.1°.

**22:53 log, phase 1 replay:** pushes 3 → 0, cap mean 18.1°, β 1.6°.

**22:53 log, phase 2 replay (slow, heavy):**
- Cap about 46–47°.
- Full left stick gives 13–30 °/s of flight-path roll. The wip without `CplAttachedOnly` gave 5–16 °/s, and the game went the wrong way.

**Damage, full pulls 400–700 km/h with flaperon_R + rollervon_R + elevator_R gone:**
| Build | Result |
|---|---|
| 0.2.1 | 12.9–15.8° |
| LiveAuthCap off only | departs |
| 0.2.2 | flat 12°, β ≤ 1.3° |

**Loaded reversals, store on the left wing (0.2.1's 34-case set):**
- **Slow, heavy (s4) cases:**
  - AoA at the flip 33–43° (0.2.1: 10–50°, depending on where its limit happened to sit).
  - Brake 50–218 deg/s².
  - Flip → roll stopped 0.42–0.83 s (0.2.1: 0.42–0.93).
- **s1 / s2 cases at 450–520 km/h:**
  - Reach the flip with more speed (122–143 m/s vs 96–124) and a lower AoA.
  - Brake 124–261 deg/s² (0.2.1: 94–203).
  - Stop in 0.33–0.43 s (0.2.1: 0.47–0.55).
- **Fast cases (580–650 km/h):** unchanged.
- **Curves:** no hitches in either build.

**Roll spam, 54 runs, full aft held (mean / worst):**
| | 0.2.1 | 0.2.2 |
|---|---|---|
| roll behind its command, max (deg/s) | 27.4 / 52.1 | **24.4 / 43.3** |
| roll behind its command, rms | 8.3 / 13.4 | **7.4 / 10.3** |
| sideslip max (deg) | 2.63 / 7.91 | **2.50 / 5.25** |
| seconds above 5° sideslip | 0.02 / 0.61 | **0.01 / 0.22** |
| bank angle travelled (deg) | 264 / 107 | **296 / 133** |
| AoA above the asymmetric cap, max (deg) | 8.4 / 45.8 | **−0.6 / 1.1** |
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 47 / 109 | 51 / 111 (slightly worse) |
| governor retreats, stick held | 8 | 8 |

**Clean regression suite** (regression set, stop/reversal sweep, corkscrews, damage set):
- Changes are small, mostly within ±0.3° of sideslip.
- The engine-out case "held max AoA 300 km/h + R intake off" departs on 0.2.1 as well (84.3° vs 85.6°). Its sideslip excursion goes 8.98 → 1.72°. This is not a regression; it is an old case still open.

**Identity:** with `CoupledCap=false LiveAuthCap=true AsymThrustCgLoad=false SlatAoASchedule=false SweepSlatSchedule=false AsymPitchAtPredQ=false AsymPitchMarginIasRef≈0`, a 09:38 window replays identical to 0.2.1 on all 960 ticks (timing columns excluded). The plugin built twice gives the same bytes.

## Cost
The asymmetric-load sweep, measured on the bench, same window, run alone:
| | mean (µs per tick) | p95 | p99 |
|---|---|---|---|
| 0.2.1 | 178 | 422 | 622 |
| 0.2.2 | 391 | 725 | 1,620 |

- The in-game 0.2.1 timer shows the sweep at about 165 µs. Expect roughly +200 µs per tick in game. The 10 s timer line in LogOutput.log will show it.
- Where the extra goes: the LP itself, plus the per-side re-solve for the 3° design sideslip. In an earlier timing run the re-solve was about a quarter of the sweep (325 µs mean without it vs 442 with it). `CplBetaDesign=0` is the cheap fallback if the game timer shows it matters.

## Install
- Main DLL replaced, with 0.2.1 kept as `Ternion_FS3_FCS.dll.0.2.1.bak`. The dev DLL is unchanged (the 0.2.1 file, no code).
- **Mod manager:** update the version there, or its modpack may put the old DLL back.
- **To check in flight:**
  1. Same load as 09:38. Pull to the limit and bleed speed.
  2. The AoA should ride the limit smoothly and come down progressively as speed falls, with no snap to 7° and no pitch bobbing.
  3. Full lateral stick while slow and heavy should still roll the flight path both ways.
  4. Watch `asymCapPitch`, `cplU`, `cplRp`/`cplRn` and `pitchMarg` in the log.
