# T/A-30 Compass FCS 0.1.5: roll-biased flaps, roll governor on the stabs (wing-damage departure)

Base: 0.1.4. Patch bump. Compiled; benched; **not flown**. 0.1.4 is kept as `.dll.0.1.4.bak`.

## Tom (5 Nov, 18:49–18:54)
- "then make the flaps roll biased like the raptor"
- "also the jet depart after half the wing is destroyed" (`Compass_FCS_20261105_185257.csv`)

## 1. Roll-biased flaps (`FlapRollBias`, new, dev, 1)
**Before.**
- **Where the droop pins the flaps:** below ~430 km/h the high-lift schedule holds the flaps at 20° trailing-edge down. That is the bottom of their ±20 travel.
- **Effect on the roll:** the down-going flap had nowhere to go, so the pair rolled on the up-going flap alone.
- **18:12 log, full-stick rolls:** flap differential median 3°, against 13° for the ailerons; common droop 12–16°.

**Now.**
- **Like the F-22E's flaperons:** in the air, the droop comes off with lateral stick or a roll command still running.
  - Starts at 10 % of full lateral demand; all of it is off by 60 %.
  - Out over ~0.3 s, back over ~0.7 s.
  - It stays on the wheels.
- **The flaps then roll with their full ±20.**

**Bench (no damage).**

| Case | 0.1.4 | 0.1.5 |
|---|---|---|
| 200 km/h roll: flap differential | 6.5° | **17°** |
| 200 km/h roll: aileron median | 18° | **12°** |
| 200 km/h roll: roll rate | 180 °/s | 176 °/s |
| 200 km/h roll: time to 30 °/s | 0.22 s | 0.22 s |
| Roll at 15° AoA, 250 km/h | 65 °/s | **59 °/s** (cost: AoA +1.3° without the flap lift, so the coupling limit binds earlier) |
| 240–270 km/h rolls after a pull: both elevators at the stop | 0–7 % | **4 %** (the droop's nose-up comes off in the roll) |
| 300 km/h and up | no change: no droop there anyway | |

- **Honest result: the roll rate doesn't go up.**
  - The flaps are inboard. A full flap is worth roughly half a full aileron in roll.
  - Low-AoA roll rate is set by the FCS's roll ceiling, not by surface authority.
- **What you get instead:**
  - The ailerons do a third less work, which leaves more margin.
  - The flap droop's nose-up comes off during rolls.
- **If low-speed rolls at 15–20° AoA feel worse,** set `FlapRollBias` to 0 for 0.1.4's behaviour.

## 2. The departure after wing damage
### What the 18:52 log shows
**The damage, 24.5–25.5 s.** 250 kg gone (`wing2_R`, `aileron_R`, `wingtip_R`: the right outer wing).
- The asymmetric-load AoA cap came down to 12°, correctly.
- `aileron_R` read 0 from then on.

**The departure chain, from 30 s.** Full aft + full left roll, 90 m/s:
1. **Rolling 85–95 °/s at 12–14° AoA.** The roll's inertial pitch-up, (I<sub>zz</sub> − I<sub>xx</sub>)/I<sub>yy</sub> · p · r, was ~40 deg/s².
2. **That took the last of the stabs' nose-down:**
   - Both slabs were at their peak lift (local AoA 28° and 42–51°).
   - The FCS's estimate of nose-down authority left (`aDn`) was 5 deg/s².
   - The allocator already weighted pitch 8 against roll 1. It had nothing left to give.
3. **The AoA ran 12 → 24° in 1 s, twice (34–36 s and 37.5–39 s).**
   - The lost wing's lift asymmetry grows with AoA: the roll moment the jet needed to cancel went 64 → 470 deg/s².
   - That took over the roll.
4. **Third time, at 41–43 s: AoA 47°, sideslip 37–47°, spin.**

**Why nothing stopped it.**
- **The roll governor that cuts the roll when pitch margin runs out only reads the nozzles' margin,** so on the T/A-30 it never ran.
- **The roll-coupling limit allowed 82–91 °/s:**
  - It works from modelled authority and an ideal velocity-vector roll.
  - The actual yaw rate was higher than an ideal roll's: r/p 0.29 at 13° AoA, against tan 13° = 0.23.

### Fix: roll governor on the stabs (`StabRollGovernor`, internal, on)
- **What it reads:** without pitch TVC, the governor reads the stabs' nose-down margin: where the slabs are sent against their full nose-down moment, from their live moment tables.
- **What it does:** below the 0.35 reserve (`RollPitchReserve`) it eases the roll-rate limit, down to its 35 % / 30 °/s floor. It gives the rate back when the margin returns.
- **From 3° AoA.**

### Bench: right outer wing detached (`wing2_R + aileron_R + wingtip_R`), full aft, full lateral, 8 s
| Case | 0.1.4 (with 0.1.5's flaps) | 0.1.5 |
|---|---|---|
| Left roll, 280 km/h | **departs**: AoA 36, sideslip 48 | AoA 12.6, sideslip 1.6, roll 60 °/s |
| Left roll, 330 | ok (12.2) | ok (12.2), roll 82 °/s |
| Left roll, 400 | **departs**: AoA 38, sideslip 51 | AoA 12.2, sideslip 1.7, roll 82 °/s |
| Right roll, 280 / 330 / 400 | ok | identical |
| Roll spam, 330 | AoA 17.5 | AoA 12.4 |
| Left outer wing (mirror), all | ok (spam 14.7) | ok (spam 12.4) |

- **Undamaged jet:** every case is identical with and without the governor: full-aft pulls, loaded rolls, pushes, AoA-limit reversals, 1 g rolls, reversals, pedals, engine out.

### Still departs: a whole wing gone (inner `wing1_R` + flap too)
| Case | 0.1.4 | 0.1.5 |
|---|---|---|
| Left roll, 400 km/h | departs | holds |
| Left roll, 280 km/h | departs | **departs** |
| Roll spam, 330 km/h | departs | **departs** |

**Why the whole-wing case still departs.**
- **With full aft held, the jet speeds up in the roll** (85 → 120 m/s) at the 12° cap. The g and the lift asymmetry grow with speed (roll moment needed 300 → 1600 deg/s²).
- **The roll authority loses:** the rudders sit at their stop, sideslip builds, and the roll reverses into the dead side.
- **That is the asymmetric-load cap:** it works in AoA, not in g, so at 12° the asymmetry still grows as the jet accelerates. A g-based cap is the next step if you want that case.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.5; 0.1.4 kept as `.dll.0.1.4.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.5.
- **Dev setting:** new `5. Airframe / FlapRollBias` (1). No other config moves.
- **Telemetry:** `rollGov` now also shows the stab governor (1 = not limiting).
