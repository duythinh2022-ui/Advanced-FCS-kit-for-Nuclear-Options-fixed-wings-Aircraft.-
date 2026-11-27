# J-20B Weilong FCS 0.2.6: air brake in flight, fins toe out, no roll drift

Base: 0.2.5. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 16:39): "ur fix is not good, the rudder direction is wrong, they should toe out, and roll now drifts by a few deg/s to either side ... the canards are there to trim the pitch. On the raptor, i can see the stabs trim for it instantly with no visible drift in any axis."

## What 0.2.5 got wrong
- **Toe direction.** 0.2.5 flipped the fins to toe in. 0.2.6 goes back to toe-out, and the `AirBrakeFinToeIn` setting is removed.
- **Roll drift.** 0.2.5 held the toe with an allocator anchor: a cost on every degree a fin moved away from its toe position. That cost also applied to the fins' yaw, so after a roll stop with the brake out, the sideslip and yaw rate were left uncorrected for seconds. In the 16:36 log at 60-62 s and 72-74 s, with the stick centred and the brake out, roll rate was 5-8 deg/s, yaw rate 2-3 deg/s and sideslip 1-1.7 deg.

## Changes
1. **The fins' toe is now part of their model; it is no longer a target the allocator steers toward** (`TeBrakeFinToeAlloc`, shared, default off; on for the J-20B).
   - As in 0.2.4, the toe is still added at the hinge drive.
   - Each fin's position and moment tables are evaluated about this tick's toe (new `SurfaceEffector.modelOff`). The allocator's fin command is the deflection about the toe.
   - The toe's pitch moment is therefore in every table and in the measured-moment estimate, and the canards answer it in the same solve.
   - The fins keep their whole travel about the toe for yaw, at no cost.
   - The log's fin columns still show the physical deflection.
2. **The set ramps no faster than the outer elevons can follow** (`TeBrakeOutRateK` 0.6, shared, 0 = off).
   - The lift balance runs the outer pair ahead of the set: it covers its 30 deg in the first half of the ramp, about 170 deg/s at 580 km/h against a 120 deg/s servo.
   - The allocator plans the canards against where the surfaces are commanded to be, so the lagging outer pair left the jet dipping.
   - Limiting only the outer pair let the set's lift through instead (800 km/h: 0.74-1.53 g). Slowing the whole set keeps the two in step.
3. **The set stays lift-neutral at speed** (new setting `AirBrakeLiftNeutral`, 7. Airbrake, on; shared logic `TeBrakeInnerEase`).
   - Once the outer pair is at its 30 deg stop, the inner pair goes only as far trailing edge down as the outer pair can still cancel.
   - Off = the inner pair always at its full 15 deg (see the trade-off below).

Port `TeBrakeFinToeAlloc`, `TeBrakeOutRateK` and `TeBrakeInnerEase` to the other jets on the next shared sync (all default off).

## Why it isn't free on a canard jet
- **Raptor:** its stabs are behind the CG. To trim the speedbrake's nose-down moment they push *down*, which cancels the brake's lift as well. Attitude and g both hold with no extra work.
- **J-20B:** its canards are ahead of the CG. To trim the same nose-down moment (inner elevons TE down, fins toed out) they push *up*, which adds to the brake's lift.
- **The limit:** at speed, that lift is more than the outer elevons can take away inside their 30 deg TE-up travel. At 800 km/h they hit the stop at 43 % open.
- **What's left:** you can hold attitude and g together only by using less inner flap. With full inner flap you get one of two things:
  - the attitude holds and the jet balloons;
  - 1 g holds and the nose eases down.

## Bench: brake opened in level flight, 3000 m, stick free, AoA/g mode
| IAS (km/h) | min pitch rate, 0.2.4 -> 0.2.6 (deg/s) | g range | nose change after 3 s (deg) |
|---|---|---|---|
| 300 | -2.10 -> **-0.48** | 1.05-1.34 -> 1.05-1.07 | -2.11 -> -0.56 |
| 400 | -3.17 -> **-0.38** | 0.97-1.42 -> 0.97-1.01 | -2.71 -> -0.73 |
| 580 | -5.73 -> **-0.49** | 0.83-1.68 -> 1.00-1.06 | -4.12 -> -0.28 |
| 800 | -9.16 -> **-0.86** | 0.30-1.19 -> 0.81-1.06 | -6.50 -> -0.66 |

**Not perfect.** There is still a dip of 0.4-0.9 deg/s while the set moves. It comes from the surfaces lagging their commands (servo rate and actuator smoothing), so the realized pose trails the planned one.

**Closing the brake:** 0.2.4 dipped to 0.46 / -0.02 / -0.79 g at 400 / 580 / 800 km/h. 0.2.6 holds 0.89-1.32 g, with a small nose-up instead (pitch rate up to +0.3 / +0.6 / +1.2 deg/s).

**Brake out, full roll for 1 s, then release** (1.5-4 s after the release):

| IAS (km/h) | roll rate max | sideslip max | bank drift |
|---|---|---|---|
| 450 | 2.4 -> **0.05** deg/s (no brake: 0.11) | 2.7 -> 0.40 deg | 3.6 -> -0.2 deg |
| 550 | 2.7 -> **0.07** deg/s (no brake: 0.07) | 2.2 -> 0.38 deg | 2.7 -> -0.3 deg |

0.2.4 had a drift too, a smaller one from the toe being outside the model.

**Brake out, then half-aft pull** (550 km/h): 7.4 g in both. Peak AoA 18.7 -> 20.1 deg (watch the brake's AoA fade in flight).

**Runs without the brake:** byte-identical CSV to 0.2.4.

## Trade-off: AirBrakeLiftNeutral costs drag at speed
Speed lost over 3 s compared with the same run without the brake:

| IAS (km/h) | 0.2.4 | 0.2.6 (lift-neutral) | 0.2.6, AirBrakeLiftNeutral off |
|---|---|---|---|
| 400 | 19.1 | 7.4 | 14.1 |
| 580 | 31.4 | 14.2 | 22.8 |
| 800 | 49.1 | 22.2 | 37.9 |

- **Lift-neutral off:** the jet balloons with the attitude held (peak 1.47 / 1.93 / 2.56 g at 400 / 580 / 800 km/h). The pitch dip is the same size (-0.37 / -0.80 / -0.87 deg/s).
- **Why 0.2.4 had more drag:** part of it came from the allocator, not knowing about the toe, pushing the fins further out against nose-up canards.
