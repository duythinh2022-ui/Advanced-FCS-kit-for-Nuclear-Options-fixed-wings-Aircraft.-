# T/A-30 Compass FCS 0.1.6: wing-damage departure, take two (AoA capture and stab priority under the asymmetric-load cap)

Base: 0.1.5. Patch bump: two internal changes, active only with an asymmetric load. Compiled; benched; **not flown**. 0.1.5 is kept as `.dll.0.1.5.bak`.

## Tom (5 Nov, 19:09)
"it still departs, also why no wings have a bit better roll rate at max aoa"

## Why 0.1.5 didn't catch it (`Compass_FCS_20261105_190457.csv`)
**Different departure from 18:52.** The 0.1.5 roll governor handles a roll's pitch-up. This one had no roll input.

**At 96 s, right outer wing gone (250 kg).** The asymmetric-load AoA cap came down to 12°.

**104–110 s: full push to −8°, then full aft, no lateral stick:**
1. **The pull came through 0° AoA at 28–30 °/s pitch rate.**
   - The AoA capture planned its stop at the 12° cap leaning 30 % toward the nose-down it had at −8° (`aDn` 160 deg/s²).
   - So it counted on ~75 deg/s² of braking.
2. **The lost wing's roll moment flips sign through zero AoA** and grows with AoA: −350 at −8° → +550 deg/s² at 12°.
3. **Past ~8° the stabs were split for roll:**
   - The ailerons and rudders were already at their stops holding that moment, so the allocator took the stabs' differential for roll.
   - The pitch was left with nothing: `aDn` 5, pitch rate still 27–30 °/s at 17°.
4. **AoA 12 → 47°, sideslip 50°, spin.** The roll went 0 → 166 °/s with the stick centred.

**Bench reproduction (right outer wing detached, same push-pull):**
- 300 km/h departs, 330 overshoots to 19°.
- The 0.1.5 fix (roll governor) doesn't help: there's no roll command for it to cut.

## Changes
### 1. `AsymCapBrake` (internal, on)
- **When it applies:** the asymmetric-load cap is in force and there is no pitch TVC.
- **What it does:** the AoA capture plans the stop at the cap with half the modelled nose-down there (`AsymCapBrakeK` 0.5), and does not lean toward the nose-down available now.
- **Effect:** it arrives at the cap slower, so it can stop there.

### 2. `AsymStabAntiCost` 0 → 0.1 (internal)
- **When it applies:** with an asymmetric load, once the AoA reaches the cap and the pitch can't follow the command.
- **What it does:** the stabs' split for roll is priced, so the stabs stay on pitch.
- **What it gives up:** the roll toward the dead wing is held only by the ailerons and rudders. With the AoA held at 12° that moment stays small enough for them.
- **Why both changes are needed:**
  - With the capture fix alone, full aft + full left roll at 280 km/h departed: AoA 39°. The capture's slower arrival left the stabs on the roll at the cap.
  - With both, AoA held 12.4°.
- **Already in the code:** the mechanism was inherited, with a cost of 0 (off).

## Bench, full aft + full lateral (or the push-pull), 8 s
| Damage | Case | 0.1.5 | **0.1.6** |
|---|---|---|---|
| Right outer wing | push −1 → full aft, 300 km/h | **departs** (AoA 38, sideslip 65) | AoA 12.6, sideslip 1.9 |
| | same, 330 | AoA 19.2 | 12.7 |
| | same, 380 | 13.7 | 13.6 |
| | full aft + left roll, 280 / 330 / 400 | 12.6 / 12.2 / 12.2 | 12.4 / 12.3 / 12.2 |
| | full aft + right roll, all | ok | ok |
| | roll spam, 330 | 12.4 | 12.4 |
| Left outer wing (mirror) | all 10 cases | ok | ok (push-pull 12.1–12.2) |
| Whole right wing (inner + flap too) | 10 cases | 3 depart | **2 depart** (280 km/h left roll, roll spam) |

- **The whole-wing case still departs.** With full aft held, the jet speeds up at the 12° cap. The g and the lift asymmetry grow with speed (roll moment 300 → 1600 deg/s²) until the rudders and the remaining aileron lose. The cap works in AoA, not in g; a g-based cap is the next step there.
- **Undamaged jet:** every regression case is identical to 0.1.5. Both changes need an asymmetric load to act.

## Why the jet rolls better at max AoA with no outer wings
From the same log, 35° AoA, full lateral stick (both outer wings gone from 157 s; ailerons with them):

| IAS | Intact: roll rate (velocity-vector) | Intact: sideslip-margin rate cap | No outer wings: roll rate | No outer wings: rate cap |
|---|---|---|---|---|
| 250–330 km/h | 36 °/s | 22 | 45 °/s | 25 |
| 330–400 km/h | 46 °/s | 27 | **76 °/s** | 55 |

**Bench, 35° AoA:**
- 300 km/h: 28 → 38 °/s.
- 380 km/h: 29 → 35 °/s.
- Sideslip 1.4 → 0.6°, even though the FCS's roll-authority estimate halved (101 → 52 deg/s²).

**What limits the roll at max AoA:** not roll authority. It is how fast the roll can be stopped without exceeding the sideslip margin, and how much sideslip the roll makes. The outer panels hurt both:
1. **Inertia.** `wing2`, `aileron` and `wingtip` sit 4.5–5.8 m out.
   - Roll inertia I<sub>xx</sub> 27,560 → 15,820 kg·m² (−43 %); yaw I<sub>zz</sub> 86,000 → 74,000 (−14 %).
   - The same rudder and stab moments start and stop the rotation much faster, so the sideslip-margin rate cap (a forward model of the reversal) rises.
2. **They're stalled, and stalled panels only cost.** At 35° the outer panels (`Trainer_wing`, stall 17°) make almost no extra lift. They still add:
   - roll damping, which grows with span squared;
   - the adverse yaw and dihedral effect that turn body roll into sideslip.
   - Without them, the roll creates less sideslip, so the cap allows more rate.
3. **The ailerons were already near useless there.** The FCS held them to ~12–14° (the down-going one is on a stalled tip), so losing them costs little roll at 35°.

**The real jets do the same thing for the same reasons:**
- Short-span, low-roll-inertia wings.
- Velocity-vector roll at high AoA limited by yaw, not roll: the X-31 rolled 30–50 °/s post-stall against 240 °/s at low AoA (NASA TM-104322).

**On the intact Compass the lever is yaw.** Rudder effectiveness or the sideslip margin, not more roll surface.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.6; 0.1.5 kept as `.dll.0.1.5.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.6.
- **Config:** no changes or moves.
