# Vortex FCS 0.5.22: no hover pedal or pirouette when the jet can't hover; stiffness measured after a vertical take-off; nose and bank held on release in the CTOL blend

20 Nov 2026. On 0.5.21.

**Status: benched, not flown.**

Tom, 20 Nov:

> check latest log, somehow i got slower than the min speed and i cant really maneuver, the jet has barely enough thrust to hover and maybe no thrust left to maneuver. In the log before that, in stovl at airspeed with ctol control, i release pitch input instead of holding zero pitch rate with in the aoa limit, the nose drifted down.

> for the pirouette logic, block it when the jet cant hover at zero airspeed

The question about raising the AoA limits (both the 24° STOVL cap and the 10–12° wing AoA, made model-determined and smooth) is **not in this build**. It comes next.

## 1. The departure below the speed floor (log 021138)

15.9 t, hover T/W 0.999 on the laws' scale (too heavy to hover, speed floor on).

1. **108 s, ~40 m/s (78 kt):** full right pedal with full aft stick. The FCS took the pedal as the **hover yaw-rate command**: the nose turns, the flight path does not, nothing coordinates the turn. Sideslip went 0 → −45° in 2.5 s, with the AoA at 18–20°.
2. **Past ~−25° of sideslip the rudders' yaw was gone.** Yaw authority fell from 22 to 4 deg/s². With the pedal let go, the yaw-rate command could only wind down at that rate, so the yaw kept going at 15–19 deg/s.
3. **110–160 s:** the jet slid sideways and backwards at ~15 m/s. The "AoA" of 47–170° there is airflow from the side and rear. The wing carried nothing. At T/W 1.00 the translation budget was 0, so nothing could stop the slide but drag. It recovered at 162 s, once fuel burn and the store drop gave the thrust some margin.

**Why the pedal was the hover command at 78 kt:** that choice rests on the airframe's **directional stiffness** (how hard the tail turns the nose back into the wind). The stiffness probe needs a one-time check of which way the yaw moment restores the sideslip, and that check needed **over 60 m/s** at low AoA. After a vertical take-off that never passed 43 m/s, the stiffness read **0 all flight**. Zero means "the nozzle beats the weathervane easily", so the pedal was the hover yaw command at any speed, with the sideslip loop off. In the 015017 flight (fast enough) it was measured from 316 s on.

### Changes
- **`SlipSignLatchSlow`** (dev): the sign check runs at the probe's own airspeed (over 30 m/s, AoA under 10°).
- **`StovlStiffDynScale`** (dev): the STOVL laws scale the stiffness by the dynamic pressure since its last probe. The probe only runs over 30 m/s; under that, the last value was held, so slowing from a fast flight left the weathervane read as strong as at the probe's speed.
- **`StovlLowTwPedal`** (dev, your rule): when the hover cannot hold its height (the speed floor's own test, hover T/W under 1.02 on the laws' scale):
  - at speed the pedal is always the **sideslip command** with the turn coordinated, never the hover's uncoordinated yaw-rate command;
  - the **pedal pirouette does not start**, even with the hotkey on.

  The stiffness fix alone was not enough: on the bench the stiffness reads ~0 at 22° AoA anyway (the airframe goes directionally neutral there), which re-opened the hover pedal mid-manoeuvre.

### Bench: 16.1 t (hover T/W 1.006, floor on), ~45 m/s, full aft + full pedal for 2 s, then a push

| | 0.5.21 | 0.5.22 |
|---|---|---|
| Max sideslip | 43° (59° with only the stiffness fix) | **11.7°** (10° is what full pedal asks) |
| After | AoA to 160°+, flying backwards, 35 km/h at 65 s | Recovered in ~2 s, 132 km/h lowest |

**Pirouette, 16.1 t, 50 kt, pedal 4 s, hotkey on:** 0.5.21 pirouetted to −24° of sideslip and 11 m/s of side speed. 0.5.22 makes a coordinated turn (sideslip under 0.5°, side speed 0.2 m/s).

## 2. Nose drop on stick release in the CTOL blend (log 015017)

381–386 s, ~58 m/s, 34° bank, stick released: the nose fell about 4°/s.
- **~1.2°/s:** the blend walked the nose down to its fixed 10° wing AoA.
- **~2.7°/s:** bank geometry. At zero body pitch rate in a turning bank, the pitch attitude falls by r·sin(bank). The kinematics check out against the log: θ' = q cos φ − r sin φ.

### Change (`StovlCtolNoseHold`, dev)
- **Pitch:** the CTOL law's pitch rate plus **r·tan(bank)**, the rate that keeps the pitch attitude in the turn. The propulsion's flight path follows the same rate.
- **Bank:** plus **−ψ'·sin(pitch)**, the body roll rate that keeps the bank with the nose up in a turn. Without it, holding the nose up made the released bank creep 35° → 57° in 10 s on the bench.
- **AoA:** the nose is **no longer walked down** to the 10° reference. Above it, the wing carries more than its share and the path rises until the AoA is back, with the nose held. The walk **up** to the reference stays. It is how the load passes from the nozzle to the wing as the speed grows. Without it, a full-lever short take-off held 5° AoA and sank from 16 to 2 m at 300–400 km/h.
- **Bounded by the AoA limit (24° in STOVL) only.** Holding the nose in a steep bank costs speed the way any jet holding a hard turn does. The AoA limit work (next build) is where that gets a model limit.

### Bench: 12 t, ~270–300 km/h, ~35–40° bank, 2 s pull, then release

| Lever | 0.5.21 nose after release | 0.5.22 nose | 0.5.22 bank |
|---|---|---|---|
| 0.38 | −2.6°/s | **−0.1°/s** | −3.6° in 5.5 s |
| 0.44 | −3.2°/s | **+0.05°/s** | −2.4° in 5.5 s |

**What you will notice:**
- **A pull now reaches the AoA limit sooner,** because the down-walk no longer subtracts from it (24° vs 16° at the end of the same 2 s pull).
- **With the nose held 30° up, the jet climbs and slows.** 270 → 130 km/h in 14 s at lever 0.38, where 0.5.21 let the nose fall and gained speed.

## Other sets
- **12-case set:** identical to 0.5.21 except the full-lever short take-off. That case differs by at most 2 (0.3 m of height, 0.1° AoA) over 60 s.
- **Regression set** (vertical landings, hovers, vertical take-off, short take-off, mission): unchanged.

## Not checked
- Not flown.
- The stiffness probe reads noisily while the rudder is deflected (0.44 → 0.12 → 0.47 within 1 s at the start of a pedal input). Already true in 0.5.21. With the jet able to hover, that can still flip the pedal's meaning for a moment.

## Release

Main DLL 0.5.22. Release folder `C:\Games\NO mods\Vortex-FCS-0.5.22-release`. Four new dev switches, all on: `SlipSignLatchSlow`, `StovlStiffDynScale`, `StovlLowTwPedal`, `StovlCtolNoseHold`. Each set false = 0.5.21.
