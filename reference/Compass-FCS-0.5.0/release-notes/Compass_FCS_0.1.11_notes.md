# T/A-30 Compass FCS 0.1.11: the VMC protection sees the live engine and cuts the throttle before the yaw runs out

Base: 0.1.10. Patch bump: internal changes only; no config entries changed. Compiled; benched; **not flown**. 0.1.10 is kept as `.dll.0.1.10.bak`.

## Tom (5 Nov, 22:27)
"check latest log, vmc did not work very well" (`Compass_FCS_20261105_221842.csv`, 0.1.10)

## What the log shows (202–232 s)
**Before the departure:**
- One engine went out at 202.3 s (`engOut` 1), at 61 m/s.
- From 208.8 s: full aft in AoA/G mode. AoA sat at the 22° engine-out cap and the jet climbed to 67° pitch at full throttle.
- The speed bled from 76 to 36 m/s in 10 s.

**The departure:**
- At 217 s (44 m/s) the rudder holding the live engine was on its stop. Then the yaw ran out.
- Sideslip went −2 → −43° at 38–28 m/s, AoA 22 → 87°, then a spin.
- A second identical climb-and-depart came at 250–260 s, and a third at 283–290 s.

**What the VMC protection did:**
- Its throttle cut began only at 218.8 s (`vmcLim` 0.91), after the sideslip had passed 15°.
- At 0.8 /s it reached 0 at 225 s, 6 s after the departure.

## Why
### 1. The FCS never saw the live engine's thrust
The T/A-30's engines have no vectoring transform. The thrust/TVC effector skipped them before it summed anything, so on this jet:
- **Thrust moment:** 0. With an engine out, the live engine's yaw was missing from the asymmetric-load model.
- **VMC yaw usage:** 0 as well. That is the protection's predictive part, the live engine's yaw against the yaw authority.
- **`asymThrY`:** 0 in every Compass log.

So the only trigger left was the last-resort one: yaw saturated, sideslip past 5° and growing, lateral stick quiet for 1 s. Your lateral stick at 217 s held that off until the sideslip passed 15°.

### 2. The cut was reactive only
Even with the trigger working, it starts after the sideslip runs away. At 44 m/s that gives about 1.5 s, and the engine takes longer than that to spool down.

## Changes
1. **Fixed-nozzle thrust moment.** Engines with no vectoring transform now count: their thrust moment is taken as it is, as the `DisableTVC` path already did. (`TvcEffector.FixedNozzleMoment`.)
   - It enters the asymmetric-load model's roll and yaw only, and only with an engine out or down on thrust.
   - The pitch model and the both-engines case are left exactly as they were.
   - Bench: with an engine out the live engine reads −20.4 deg/s² of yaw. The engine-out AoA cap is unchanged; it leaves the thrust out by design.
2. **`VmcPredict`: the throttle comes back before the yaw runs out.**
   - **What it watches:** with an engine out, the live engine's yaw against the yaw authority there. The authority is judged at the predicted q of a decelerating climb (`qPredK`).
   - **The cut:** past 70% of the authority (`VmcPredUseMax`), the throttle limit comes down at up to 1 /s, starting from your throttle.
   - **The release:** back at 0.25 /s once the usage is 10% under the limit.
   - **Your throttle is never raised.**
   - The runaway cut stays as the last resort.

**New telemetry columns:** `vmcPred` (the predictive throttle limit) and `vmcPredUse` (the live engine's yaw ÷ yaw authority). `asymThrY` now reads the engine yaw.

## Bench (left engine out at 0.5 s, full throttle)
### Climbs: full or 0.6 aft from 220 / 250 / 300 km/h
**0.1.10:** all 6 depart while still climbing, at 25–31 m/s: sideslip past 15°, 4 of them on into spins (AoA 158–180°).

**0.1.11:**
- **The throttle cut starts** at 46–47 m/s.
- **The yaw holds:** sideslip stays under 1° until the jet runs out of airspeed at the top (7–15 m/s). That stall at the top is its own; any jet does it.
- **From 220 km/h** it falls through and picks up 16° of sideslip nose-down at 40 m/s.

### The same climbs with a full roll reversal at 7–10 s (28–36 m/s)
| | 0.1.10 | **0.1.11** |
|---|---|---|
| Sideslip passes 15° | at 9.9 s | at 10.1–10.4 s |
| Peak sideslip | 48–89°, AoA to 146–180° (spin) | **16–24°, AoA held at 23–24°** |

**What that means:** full-stick rolls that slow are past what the rudders can hold with one engine, whatever the throttle does. 0.1.11 keeps it from becoming a spin.

### Low and slow: 0.25 pull held from 180 / 200 km/h
| | 0.1.10 | **0.1.11** |
|---|---|---|
| 180 km/h | departs, sideslip 53° | sideslip 0.6° |
| 200 km/h | departs, sideslip 35° | sideslip 1.9° |

**The cost:** the throttle comes back to 0–25%, so the jet keeps slowing (it ends at 21–31 m/s). The nose has to come down to get the speed back.

### Unchanged
- **Engine out at 200–400 km/h** (level, full rolls both ways, turns): the limit never acts. Sideslip is the same to within 0.9°.
- **Both engines running:**
  - Clean regression set: identical.
  - 133-case damage matrix: identical (0.0° difference).
  - Asymmetric stores (27 cases): identical.

## Found on the way, not changed: the FCS's T/W reads 0 on this jet
`tw` is the FCS's thrust/weight (`tvc.totalThrust`). It comes from the same skipped sum, so it has read 0 in every Compass version, on the bench and in game. Everything that reads it has been running as if at idle:
- the low-thrust gates;
- the pitch-rate envelope's thrust term;
- the disturbance-lead fade.

All Compass tuning so far was done with it at 0. Fixing it is easy (`fixedThrust` now carries the sum), but it would move several laws at once, so I left it alone. It needs its own bench pass if you want it.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.11; 0.1.10 kept as `.dll.0.1.10.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.11.
- **Release:** `C:\Games\NO mods\Compass-FCS-0.1.11-release`.
