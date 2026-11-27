# J-20B Weilong FCS 0.1.7: no more bump at 36 deg on the way to max AoA

Base: 0.1.6. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov)
- 11:01: "when i pitch to max aoa, the aoa rises but it kinda meet a bump at around 36 then it go to 40. Also find more ways to increase roll authority aerodynamically"
- 11:05: "no on top fixes"

## Your 10:58 log (0.1.6)
Pure pull-ups (no roll), AoA every 0.1 s from 25 deg:

| Time | Speed | AoA (deg) |
|---|---|---|
| 45.6 s | 163 m/s | 24.9 28.3 31.1 32.6 33.2 33.6 34.6 36.1 37.3 **37.7 37.8** 38.1 38.7 39.1 ... |
| 50.5 s | 128 m/s | 24.9 28.9 32.3 34.5 35.8 **36.2 36.0 36.0 36.3** 37.0 37.9 38.6 ... |

The bench shows the same thing (250-350 km/h: the AoA sits at 36.5-36.7 for 0.27-0.38 s, then creeps to 40 over 1.3-1.5 s).

## Cause: the AoA-limit capture law, not the canards or the airframe
The capture bounds the AoA rate on the way to the limit at `min(sqrt(1.4 a e), 3 e)`, where:
- e = 0.9 x (40 - AoA) - 0.15 x AoA rate;
- a = the nose-down braking the FCS can count on.

On this jet (no thrust vectoring), two things go wrong:

**1. The fixed 3 /s slope takes over for the last ~9 deg.**
- `sqrt(1.4 a e)` is the stop curve built from the real braking. It is smaller than `3 e` only further than ~9 deg from the limit (a ≈ 60 deg/s²).
- So inside ~31 deg the approach is a plain proportional law.

**2. The lead term subtracts 0.15 x the AoA rate from the distance.**
- At 33.5 deg with the AoA rising at 24 deg/s, it takes 3.6 deg off the 5.9 deg left.
- Bench, 250 km/h, the FCS's own numbers: at 33.5 deg the bound asked for an AoA rate of 6.6 deg/s while the jet was doing 24.
- The jet braked hard, overshot the brake through the surfaces' lag, and the AoA rate hit zero at 36.7 deg.
- With the AoA rate at zero the lead term vanished, the bound opened again (to about 9 deg/s), and the AoA started climbing a second time. That is the bump.

**Why those two terms exist.** Both stand in for lag that isn't modelled. The capture already has a proper lag model, a stopping distance of `q tau + q^2 / 2a` (F-22E 2.2.1), but it counts only thrust-vectoring nozzles (`CapTvcShare`), so on the J-20B tau was 0.

## Fix: the capture's stop includes the pitch surfaces' own servo lag
- **tau from the surfaces.** For each pitch surface (canards, both elevon pairs, fins): from where it is now to the most nose-down point of its moment table, at its servo rate (canards and outboard elevons 120 deg/s, inboard elevons and fins 100). Weighted by the moment it gains on the way, and halved, because the moment ramps in over the swing. The nose-down limit uses the most nose-up point. On the bench it is 0.10-0.25 s during a pull.
- **The stop curve alone bounds the approach.** With the lag modelled, the fixed 3 /s slope and the AoA-rate lead no longer apply. The lag curve has its own finite slope at the limit (1 / tau).
- **Unchanged:** the bound past the limit, the capture integrators and the AoA protection.

New dev key `4. Gains / CaptureSurfaceLag` (default **on**). Off = 0.1.6. It takes effect at once. No changed defaults, so no config migration.

**Shared code:** `CapLagSurfaces` (default off) and `CapSurfLagT()`. Other jets are unchanged. Port to other jets on the next shared sync; it matters most on jets without thrust vectoring (Revoker with TVC off, Ternion, Compass).

## Bench (0.1.6 law vs 0.1.7), FCS AoA

**Full aft from trim, 3000 m, throttle 1:**

| IAS (km/h) | 30 -> 39.3 deg | Peak | AoA rate under 2 deg/s between 33 and 39 |
|---|---|---|---|
| 130 | 1.43 -> **1.00** s | 40.07 -> 39.99 | 0.02 -> 0 s |
| 250 | 1.50 -> **0.68** s | 40.06 -> 40.02 | **0.27 -> 0** s |
| 350 | 1.33 -> **0.55** s | 40.01 -> 40.05 | **0.38 -> 0** s (0.75 deg dip -> none) |
| 500 | 1.28 -> **0.43** s | 40.10 -> 40.20 | 0 -> 0 |
| 650 | 1.15 -> **0.42** s | 39.80 -> 40.27 | 0 -> 0 |

**Full push 200 / 275 km/h:** -20 -> -29 deg in 1.38 / 1.35 s -> **0.68 / 0.60** s. No overshoot past -30. 350-450 km/h are G-limited and unchanged.

**Roll at the limit:**
- Full aft + full roll from 3.15 s (the roll starts during the capture), 250-625 km/h:
  - roll angle in 4.9 s: 120 -> 118 deg mean;
  - AoA band: 39.5-40.5 -> 39.6-40.6 (worst 0.51 -> 0.60, at 625 km/h, where the jet is already at 40 when the roll starts).
- The same with the roll from 4.65 s (settled at the limit first):
  - 106 -> 104 deg;
  - peaks 40.1-40.2 -> 40.1-40.4.
  - Both laws dip to 38.5-38.6 at 250 km/h, so that dip predates this change.
  - The overshoot comes from the roll entry (pitch rate 1-2 deg/s over its target while the elevons go to roll), not from the capture bound, which is the same at and past the limit.
- Roll and release (200-625): worst 0.49 -> 0.60 (the same 625 km/h case), roll 56 -> 53 deg mean.

**Airborne regressions** (hands-off 500, 1 g roll 600, 9 g pull 900, engine-out pull 300): alpha the same. Engine-out bank peak 1 -> 3 deg.

**Not met:** the ±0.5 band in rolls at 625 km/h (0.60), and the pre-existing 250 km/h dip when rolling from a settled limit (1.5 deg).

## Roll authority: what limits it (findings, nothing built)
**Bench lateral map at 400 km/h.** Stability-axis (velocity-vector) roll and yaw accelerations for full deflection, deg/s²:

| AoA | Elevons roll / yaw | Fins (yaw right) roll / yaw | Best coordinated roll, elevons + fins | + differential canards |
|---|---|---|---|---|
| 0 | 149 / 2 | -112 / 43 | 157 | |
| 20 | 129 / -48 | -93 / 77 | 72 | 109 |
| 28 | 91 / -55 | -84 / 89 | 39 | |
| 36 | 58 / -55 | -72 / 97 | 17 | 49 |
| 40 | 42 / -52 | -66 / 99 | **8** | **45** |

"Coordinated" means zero stability-axis yaw acceleration.

- **Above ~28 deg the elevons are not the limit; the yaw is.**
  - At 40 deg a velocity-vector roll needs body yaw of about 0.84 x the body roll.
  - The canted fins are the only yaw source, and their yaw comes with opposite roll (the vertical part of their force, 2.15 m out, on a jet with a small roll inertia).
  - Coordinating the elevons' roll with the fins costs almost all of it: 42 - 0.67 x 52 ≈ 8.
- **Scaling the fins doesn't help.** Their roll and yaw scale together, so the coordinated figure is the same; they only reach 52 % of their travel anyway.
- **A drag rudder from scissored elevons on one wing gives 2-4 deg/s² of yaw.** That is too small to matter.
- **The levers that change it (canards stay pitch only, Tom 11:17):**
  - **Elevon effectiveness below ~28 deg.** Coordinated roll is about 0.55 x the elevon figure at 20 deg, so camber coupling / `ElevonLiftScale` count there. 0.1.5 already did the coupling from geometry.
  - **Above ~28 deg nothing aerodynamic on this airframe moves it much.** It is set by the ratio of the fins' roll to their yaw, which comes from their cant and position.
  - **Yaw from thrust vectoring.** The J-20B mod's engines have 0 deg of vectoring. The FCS supports TVC yaw / roll as on the F-22E and Su-30, but that is propulsive, not aerodynamic.
  - For reference only: differential canards would give 8 -> 45 at 40 deg (not used: pitch only).
