# FS-20 Vortex FCS 0.2.9 — STOVL at speed flies like CTOL (short take-off fix)

Build of 17 Nov 2026, 11:03. **Benched, not flown.**

## Why (10:24 log, STO from the runway)

The jet was converted on the runway, then given full throttle; the stick was pulled to full aft at 50 kt and held for 8.7 s.

| What Tom saw | What the log shows |
|---|---|
| Lifts off like a VTO with some forward speed; accelerates very slowly | Above 40 kt GS the stick commanded flight-path rate (10 deg/s). The path reference wound up to its 60 deg clamp. The thrust fit puts vertical first, so full dry thrust went into the climb: vertical speed up to 24 m/s, path up to 46 deg, ground speed stuck at 54-58 kt for 13 s. |
| Zero pitch control | The pitch attitude was the FCS's: a blend of the 3 deg hover attitude and path + AoA, slewed at 1.5 deg/s. Nose 3 -> 15 deg while the path reached 46 deg: **AoA -32.7 deg** (37.9 s). |
| Dropped like a brick | A full push held 3.4 s (44.4-47.8 s) commanded -34 deg of path. The law asked for -1 g (aDesV -10.9), so thrust went to idle. The released stick then held that dive. At 52 s the lever went to the aft detent (maximum deceleration). The log ends at -29.5 m/s. |
| (ground roll) | With the lever at the stop, full lever meant "6 m/s^2". The engine was throttled back from 0.88 to 0.62-0.79 once that was reached. |

Real F-35B short take-off (PPRuNe, Wg Cdr Tony Carter / BF-1 ship trials): the throttle goes to Mil or Max STO. The control laws move the nozzle. The pilot rotates with the stick, or with the button / auto STO, to the fly-out attitude.

## What changed

### 1. CTOL blend (task 12)

In STOVL mode, `StovlSystem.CtolW` goes from 0 to 1 over **40 to 60 kt** of airspeed in the plane of symmetry (`VhCtolV0` / `VhCtolV1` = 20.6 / 30.9 m/s). 40 kt is where the hover's stick and pedal handover (lamV / lamY) ends.

**Laws**
- `FcsController` runs `PitchLaw` and `LateralLaw` in the mode the jet would be in with the doors shut: `stovlCtolMode` = airMode, so GearDownRate with the gear down, or AoAG / GAndPitchRate with the gear up.
- They are reset on entry, as on a mode change.
- Their q / p / r commands and FFs are blended with the STOVL laws' by CtolW.
- `qTraj` is no longer reset by `StovlLaw` while the CTOL laws run.

**Lever = thrust.** Throttle = idle + lever x (full dry - idle). There is still no afterburner in STOVL mode. HUD reads `THR nn%`.

**The lift system as the wing's helper (`Stovl.cs`, CTOL blend block)**
- Lift direction l = normal to the airspeed in the plane of symmetry.
- Required load: N = m (V gammaDot + g up.l).
  - gammaDot = the CTOL law's pitch-rate command (the path turns with the nose).
  - With the stick released (in the air), plus 1.5 x (gamma_hold - gamma), within +-5 deg: the path is held.
- sPr = 1 - L_wing(alpha_ref) / W is the share of 1 g the propulsion carries with the wing at the reference AoA.
  - alpha_ref = PitchHoldAoA0 = 10 deg, within StovlWingAoAMax.
  - 12 t: sPr is 0.87 at 55 kt, 0.57 at 100 kt, and about 0 at 150 kt.
- Propulsive share asked: F = sPr (N - L_measured) + (1 - sPr)(N_1g - L_ref).
  - Slow: the path is flown exactly.
  - As the wing takes over: only the 1 g the wing does not give at alpha_ref. Manoeuvres are the wing's, so the AoA settles at alpha_ref + m V q / (q S CLa).
- AoA management: `ctolQAoA` = sPr x clamp(2 (alpha_ref - alpha), -3, +1.5) deg/s is added to the nose-rate command. These are the STOVL laws' own AoA-reference rates.
- Nozzle: the first swivel where (thrust now) x (lift-direction component) reaches F.
- Thrust floor: what the nozzle fully down needs for F (95 %). Lever back at speed therefore never drops the jet; the speed bleeds off instead.
- On the wheels: F = 90 % of the load minus the wing at its actual AoA, as the STOVL laws' own take-off roll. Rotation (q) lifts the jet off.

**Bank limit.** The CTOL roll-rate command is stopped at phiMax, the bank the lift can hold: wing at StovlWingAoAMax plus propulsive lift, 75 deg at most. Without it, half stick for 2 s at 110 km/h gave 88 deg of bank and -46 m/s.

**Handback below 60 kt.** The STOVL references (thetaRefF, thetaHold, phiG, phiHoldRef, gamRef, hRef) follow the jet in proportion to CtolW, so the STOVL laws pick up from where the jet is. The auto airbrake is off under the blend.

### 2. Fixes in the STOVL laws themselves (both apply with or without the blend)

- **Path reference anti-windup.** `gamRef` stays within +-5 deg of the flown path, the path loop's own error window.
- **Full lever = all the thrust.** The lever's last 2 x detent width ramps the acceleration command from StovlAccelMax to full-dry thrust / mass.
- **No translation budget on the wheels.** `hScale` is not applied on the wheels: the tilt budget had held a full-lever ground roll to about 4 m/s^2.

## Bench (0.2.8 installed source vs 0.2.9; `stocase.sh`, `stovlall2.sh`, `axcase.sh`)

**Short take-off.** Full lever at 4-5.3 s on the runway.

| Case | Build | Engine on the ground, min | Lift-off | 100 kt GS reached | AoA after lift-off |
|---|---|---|---|---|---|
| S2: half pull 2 s at 46 kt, released | 0.2.8 | 0.31 | 42 kt | 27.4 s | -8.1 .. 10.0 |
| S2 | 0.2.9 | 0.81 | 48 kt | **20.1 s** | 1.7 .. 10.0 |
| S3: full pull 4 s, released | 0.2.8 | 0.31 | 41 kt | 26.1 s | -20.7 .. 11.5 |
| S3 | 0.2.9 | 0.81 | 47 kt | 34.5 s | -2.3 .. 10.0 |
| S1: the 10:24 log's inputs, incl. the 3.4 s full push | both | | | | crash in both |

- **S2, 0.2.9:** path held at 9-10 deg after release. The nose comes up at about 1.2 deg/s to put the AoA at 10. The nozzle runs aft from 0.77 to 0 by 135 kt, where the wing carries the jet, then flies as CTOL.
- **S3, 0.2.9:** the 4 s full pull leaves a 20 deg climb, held after release, so it reaches 100 kt later. That is the climb the pilot asked for, at full dry thrust.
- **Gear-up check** (`gearup=12`, AoAG / GAndPitchRate) behaves the same.
  - A 1 s half pull at 150 kt is flown by the wing: AoA up to 15.2, propulsive share 0.
  - Before the 1 g / manoeuvre split, the nozzle swung down to 0.25 during the pull.

**Hover / VL regression.** Hover at 4 weights, vertical landing at 5, and VTO v1: **identical output** to 0.2.8.

**Mission** (CTOL 250 km/h -> STOVL -> decel -> hover -> inputs -> accel -> CTOL)
- Decel with the lever at 0 after the conversion: 0.2.8 reached the hover at 39 s. 0.2.9 reaches it at 47 s.
  - Lever = thrust: the floor keeps the nozzle at about 0.78, and the airbrake is not automatic.
- Acceleration at lever 0.8: the CTOL attitude hold climbs as the speed rises; 0.2.8 held the path.

**At speed** (axis cases, 110-220 km/h, lever 0.75)
- Pedal: +-13.5 deg of sideslip (CTOL), against -42 .. -75 deg in 0.2.8.
- Roll: 50-60 deg/s, stopped at phiMax.
- Pull 0.5 for 1.5 s: q 23 deg/s.

## Open / not checked

- Not flown.
- The mid-band handback (60 -> 40 kt) shows a -6 deg nose swing before the hover's deceleration attitude comes in (bench mission, 37-39 s).
- With the lever = thrust, the speed protection under the swivel cut-off only applies to the STOVL part. Full lever under the blend runs to the game's swivel limit, and the existing overspeed reconversion to CTOL takes over there.
- Rolling landings in STOVL mode with the lever above the aft detent carry 90 % of the weight on thrust on the wheels, as 0.2.8 did.
- The asymmetric-load unification is still in the handoff (`claude/FCS_asym_unification_handoff.md`). This build is on the installed 0.2.8 source; the WIP tree is stashed in `/home/claude/wip/unify_tree/`.
