# T/A-30 Compass FCS 0.1.8: damage protections no longer drop out, a negative-AoA mirror, and the roll keeps going through a push

Base: 0.1.7. Patch bump: internal changes only. Compiled; benched; **not flown**. 0.1.7 is kept as `.dll.0.1.7.bak`.

## Tom (5 Nov, 20:39 and 20:51)
- "Compass had the same damage as the previous log, it was more stable but still departed a few times" (`Compass_FCS_20261105_202527.csv`, 0.1.7)
- "I held roll and pitch, then I move the stick to the other side while holding roll, the roll stops when I get to low AoA then starts again when AoA starts to build up"

## What the log shows
**The damage:**
| Time | Lost |
|---|---|
| 67.5 s | `wingtip_R` |
| 69.5 s | `wing2_R` + `aileron_R` |
| 91.5 s | `elevator_L` |
| 95 s | `vstab2_L` (the `rudder_L` stayed on, hanging from its fuselage hinge) |

The gear was down from ~118 s, so the FCS was in GearDownRate: pitch-rate command, no AoA command, but the AoA caps still apply.

**Most of the time it worked.** From 100 s the cap sat at 7° and held through many pulls.

**Departures:**
| Time | What happened | Cap |
|---|---|---|
| 193–195 s | Full aft + roll reversal at 120–130 m/s: AoA 3 → 42°, sideslip 57° | **12°** |
| 214–223 s | Full aft at 90–110 m/s: the cap rose 7.8 → 11.5° and AoA followed to 12.7°. The lone right slab was then at its lift peak (local AoA 28–29°). When the cap came back to 7° there was no nose-down left: AoA 55°, sideslip 65° | 7.8 → 11.5 → 7 |
| 243.5–248 s | Full push + roll: AoA −2 → −24° with full aft stick from 244.5 s. Nose-up authority ran out at −7° (`aUp` at its 5 floor, the stab at its nose-up end), sideslip 33° | −8° (lateral) |

## Why the cap rose to 12
**The gate.** Since 1.8.37 (F-22E) the asymmetric-load pitch protections have been gated on the lateral CG offset `asymDy`, opening over 4–12 cm. That covers four things:
- the pitch cap and its bias;
- the pitch priority at the cap;
- the price on the stabs' roll split;
- the held roll-coupling gate.

**How `asymDy` is measured.** From the midpoints of the surviving left/right surface pairs.

**What went wrong.** With the aileron, elevator and fin pairs broken, only the rudders were left as a pair, and the left one was hanging loose. `asymDy` swung **−0.28 ↔ 0.00 m every 15–20 s**. At each swing to 0:
- the pitch cap switched off, so the cap went 7 → 12°;
- the stabs' roll price and the pitch priority switched off too.

Both positive-AoA departures came in those windows.

## Changes (all internal)
### 1. `AsymDamageGate`
- **What it does:** a control surface lost, while the asymmetric-load cap is in force, opens the same gates as the CG offset does.
  - Detected from the FCS's own surface list: one or more surfaces not live.
  - Held 1 s after the condition clears.
- **Pure stores:** no surface is lost, so they keep the CG-offset gate exactly as before.

### 2. Negative-AoA mirror (`AsymPitchBiasNeg`, `AsymPitchCapFloorNegNoTvc` 4)
- **The bias, mirrored:** nose-down the model leaves out at negative AoA. With a stab gone, it also includes the stabs' live nose-up end against the swept one.
- **A negative pitch cap:** held where the nose-up margin falls under `AsymPitchMargin`, 2° short of it, never closer to zero than −4°.

### 3. `PushPlanStopsRoll` off
- **What it was:** the push-over plan bleeding a *held* roll to zero. It came over from the Revoker (your 0.7.0 request there).
- **In the 20:25 log at 29.5 s:**
  - The plan came on at 20° AoA and held the roll target at 0 all the way to 0° AoA.
  - The roll went 52 → −3 °/s, then back to 110 at −1°.
- **Now:** the held stick gets the plan's paced roll (`PushPlanHeld`), as the F-22E does.
- **A released stick still bleeds as before.**

New telemetry columns: `asymPitchBiasNeg`, `asymCapPitchNeg`, `dmgGate`.

## Bench
### The gate (133 damaged cases: 7 damage sets × full aft L/R/no roll, push-pull, roll spam, 1 g → pull → reversal)
| Setup | 0.1.7 | **0.1.8** |
|---|---|---|
| As benched before (`asymDy` steady at 31 cm, gate open) | 1 departs | 1 departs (identical results) |
| Gate shut as in game (CG-offset gate forced shut, the game's `asymDy` = 0 windows) | **27 depart**, 30 overshoots past 16° | **2 depart**, 2 overshoots |

**In the forced-shut run, by damage set:**
| Damage set | 0.1.7 departs | **0.1.8 departs** |
|---|---|---|
| Right outer wing + left fin + left stab | 9 | 0 |
| Right outer wing + left stab | 10 | 0 |
| Mirror (left outer wing + right stab) | 4 | 1 |
| Whole wing | 4 | 1 |

### Negative mirror (full push from 1 g, held 2 s, with left / right / no roll, 330–470 km/h, 4 damage sets)
- **Outcomes:** identical to 0.1.7 in all 36 cases.
  - The bench holds the −8° lateral cap here.
  - Its nose-up does not run out at −7° as it did in game.
- **What the new cap computes:** −7 to −13° with a stab gone, so it isn't binding on the bench.
- **Not yet checked in game.**

### Roll through a push (clean jet, full aft + full roll, then full / 0.6 push with the roll held, 250–430 km/h)
| | `PushPlanStopsRoll` on (0.1.7) | **Off (0.1.8)** |
|---|---|---|
| Lowest velocity-vector roll before 0° AoA | **−2 … −17 °/s** (reversing) | **+15 … +34 °/s** |
| Sideslip | 1.6–4.8° | 1.4–3.8° |
| Time to 0° AoA | 1.4–3.0 s | 1.5–2.3 s |

### Undamaged jet
- **Regression and roll cases:** bit-identical to 0.1.7 (9 regression + 9 roll). They include the push / pull reversals; there is no roll in those.
- **Asymmetric stores** (pulls, rolls and pushes with rolls, 3 loads × 3 speeds): identical except one 250 km/h push, −7.2° vs −7.0° minimum AoA.

## Still open
- **The whole-wing case:** the lateral departure is unchanged (1 of 19).
- **The negative mirror is unflown.** Watch `asymPitchBiasNeg` and `asymCapPitchNeg` on a push with this damage.
- **GearDownRate has no AoA command,** so the caps act only through the capture and recovery. All three departures came with the gear down.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.8; 0.1.7 kept as `.dll.0.1.7.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.8.
  - Its version string had been left at 0.1.6 since 0.1.6; the code was current.
- **Config:** no changes.
