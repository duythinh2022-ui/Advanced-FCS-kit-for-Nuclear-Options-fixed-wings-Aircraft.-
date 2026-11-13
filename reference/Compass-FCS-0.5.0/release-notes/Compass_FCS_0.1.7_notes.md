# T/A-30 Compass FCS 0.1.7: the asymmetric-load pitch cap now measures the nose-up its model misses (damaged-tail departures)

Base: 0.1.6. Patch bump: internal changes, active only with the asymmetric-load AoA cap in force and no pitch TVC. Compiled; benched; **not flown**. 0.1.6 is kept as `.dll.0.1.6.bak`.

## Tom (5 Nov)
"check new logs for damaged cases if it can be improved" (`Compass_FCS_20261105_191835.csv`, 0.1.6)

## What the log shows
| Time | Event |
|---|---|
| 58.5 s | −250 kg: right outer wing (`wing2_R`, `aileron_R`, `wingtip_R`). The asymmetric-load AoA cap comes down to 12°. |
| 60–138 s | Many full-aft pulls and rolls held at 12° (0.1.6 working). |
| 101–103.5 s | **AoA overshoot to 23.4°**, recovered. Full aft at 100 m/s, full right roll at 105 °/s, then a reversal to full left. |
| 111 s | −100 kg: left fin (`vstab2_L` + `rudder_L`). `rudder_L` frozen at −4.2 from here; `yawHealth` 0.5. |
| 133 s | −75 kg: left stab (`elevator_L`), frozen at −9.1. The FCS dropped it: `aUp` / `aDn` 295 / 183 → 210 / 106 deg/s². |
| 136–141 s | **Final departure.** From 1 g at 130 m/s: full aft (3.3–4.6 g), roll left, then reversal to full right at 138.3 s. AoA 12 → 46°, sideslip 55–62°, spin. |

**At 138 s, every pitch surface was out of nose-down:**
- Right stab at −30.
- Flaps at full trailing-edge-up.
- `aDn` at its floor of 5.

Yet the cap's pitch part (`asymCapPitch`) read 25–29°. In other words, the model said there was nose-down to spare up to ~27° when the jet had none at 12°.

**At 101 s:**
- The roll-coupling limit allowed 104–109 °/s at the 12° cap.
- After the reversal both stabs were at their nose-down end:
  - left at −30;
  - right at −18, because without the outer wing's downwash the right slab hits its lift peak at less deflection.

## Why: the cap's nose-down model is optimistic once the jet is damaged
The asymmetric-load pitch cap (inherited, 1.8.37) holds the AoA below the point where the nose-down margin falls under 20 deg/s². It works out that margin as:

> swept bare airframe + stabs at full nose-down

**It leaves out the other surfaces.** Bench: right outer wing + left fin + left stab gone, full aft at 330 km/h (the log's final damage):
- **The other surfaces add nose-up.** The ailerons, flaps and rudder holding the lost wing's roll pitched the nose **up 7–24 deg/s²**.
- **The airframe model under-reads nose-up.** The bare airframe was 7–10 deg/s² more nose-up than modelled.
- **With one stab gone, the lone slab is weaker than modelled.** Its live table reads 8–12 deg/s² less nose-down than the sweep at 5–8°.
- **Net result at 12°:** the model said 22–24 deg/s² of margin. The jet had about 0 (the stab sat on its lift peak, with the pitch acceleration ≈ 0 at full nose-down).
- **So the cap held 12.4°** and the jet departed from 12–13°.

**In game the gap was larger still.** The cap read 25–29° there, against ~12 on the bench for the same damage.

## Changes (all internal; only with the asymmetric-load cap in force, no pitch TVC)
### 1. `AsymPitchBias`: measured nose-up the model misses
- **What it measures:** the pitch acceleration the jet actually has, (I ω̇ + ω × Iω − the stabs' moment) / I<sub>yy</sub>, minus the modelled bare airframe at the present AoA.
  - With a stab gone, it also takes in the stabs' live nose-down end against the swept one (`AsymPitchBiasStab`).
- **How it's filtered:**
  - 0.5 s low-pass.
  - Held at its peak, then let down at 2 deg/s² per second.
  - Only the nose-up side counts, and only from 0–30° AoA.
- **Where it's subtracted:**
  1. the pitch cap's nose-down margin;
  2. the AoA capture's braking at the cap (`AsymCapBrake`);
  3. the roll-coupling limit's nose-down authority.
- **Effect on the coupling limit:** at the 12° cap at 400 km/h with the outer wing gone, the roll rate is held to ~80 °/s, against 110 in 0.1.6.

### 2. `AsymPitchCapFloorNoTvc` 7
- The pitch cap may come down to 7° without pitch TVC. 10 was the F-22E's floor.
- **Why:** with a stab gone, the remaining slab reaches its lift peak at 9–10° body AoA at 280 km/h. A cap held at 10 departed on rolls there.
- **What 7° gives you:** about 1.3 g at 280 km/h and about 4 g at 470.

**New telemetry column:** `asymPitchBias` (deg/s²).

## Bench
### Damage matrix: 7 damage sets × 19 manoeuvres = 133 cases
**Manoeuvres:**
- Full aft with left roll, right roll and no roll, at 280 / 350 / 430 / 500 km/h.
- Push to −8° then full aft, at 300 / 380 / 460.
- Roll spam with full aft, at 330 / 450.
- 1 g → full aft → full left 1 s → full right, at 360 / 470.

**"Departs"** means AoA > 30° or sideslip > 15°.

| Damage | 0.1.6 departs | **0.1.7 departs** |
|---|---|---|
| Right outer wing + left fin + left stab (the log's final state) | 8 | **0** |
| Right outer wing + left stab | 7 | **0** |
| Left outer wing + right stab (mirror) | 1 | **0** |
| Right outer wing | 1 | 0 |
| Left outer wing | 0 | 0 |
| Right outer wing + left fin | 0 | 0 |
| Whole right wing (inner + flap too) | 6 | **1** (4 fixed; 350 km/h left roll newly departs) |
| **Total** | **23 / 133** | **1 / 133** |

- **Overshoots past 16° AoA:** 25 → 1.
- **The cost, with a stab gone:** the cap sits at 7–10° instead of 10–12°, so less g is available.

### Game-like timeline (damage mid-flight, 3 s before each pull)
- **Sequence:**
  - Outer wing lost at 0.3 s, left fin at 7 s, left stab at 11 s.
  - Full aft and roll reversals between them.
  - Run at 330–540 km/h, both roll directions.
- **After the stab loss:**
  - 0.1.6: 3 departures and 3 more overshoots to 21–36° (of 8).
  - **0.1.7: none; AoA ≤ 11°.**
- **Before the stab loss** (wing and fin phases): the same in both, AoA ≤ 13°.

### The 101 s reversal (outer wing gone)
- **Manoeuvre:** full aft, full right for 0.4 / 0.8 / 1.2 s, then full left, at 300–450 km/h.
- **Result:** 0.1.6 departs 4 of 15; **0.1.7 departs 1 of 15** (400 km/h, 0.4 s hold).
- **With the fin also gone:** 0 of 15 in both.

### Undamaged jet
- **Clean:** bit-identical to 0.1.6. That covers the 9 regression cases (pulls, rolls after a pull, push / pull reversals at the limits) and the 9 roll cases (1 g, mid-AoA, max-AoA).
- **Asymmetric stores** (870 kg on `wing1_L` or `wing2_L`, 435 kg on `wing2_R`; full aft, with and without roll, at 250–450 km/h):
  - The bias stays small: median 4 deg/s², max 11.
  - Caps within ±2° of 0.1.6.
  - No departures.

## Still open
- **Losing a wing panel at max AoA under full aft still departs, in both versions** (sideslip 50–89°).
  - The lost panel's roll moment at 35° is beyond any authority.
  - Losing a stab alone at 35° holds.
- **A full pull less than ~1 s after losing a wing at 1 g departs, in both versions.**
  - **Why:** the cap takes ~2 s to come down from 35 to 12°, because `AsymCapTightenRate` is 10 °/s.
  - **Bench at 30 °/s:** 4 of 4 of these held.
  - **Not changed here:** it's a dev setting, and moving it needs a config migration. It can be the next step if you want it.
- **The whole-wing case:** 1 of 19 still departs, and sideslip still reaches 11–14° on rolls. It is a lateral limit, not pitch.
- **Not yet checked in game:** the bias is measured in flight, so it should cover the game's larger model gap, but this is unflown. Watch the `asymPitchBias` column.
  - With the log's final damage, expect it at ~25–50 deg/s² and the cap at 7–10°.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.7; 0.1.6 kept as `.dll.0.1.6.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.7.
- **Config:** no changes or moves.
