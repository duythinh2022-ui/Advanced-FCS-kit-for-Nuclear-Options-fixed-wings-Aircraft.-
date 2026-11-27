# J-20B Weilong FCS 0.2.7: no pitch wobble at the AoA limit with a wing shot off

Base: 0.2.6. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 16:58): "at the end i ripped of almost the entire right wings, aoa is properly limited but pitch is wobbly and steppy". Then: "fix it", "all of them", "but fix and install j20 first".

## What the 16:55 log showed
- **The damage:** wing2_R destroyed at 107.5 s, taking the right wingtip and the outer right elevon with it; flap_R destroyed at ~109 s.
- **The wobble:** full aft from 110.5 to 133 s. The AoA held the damage cap (asymmetric-load limit, ~26 deg), but pitch ran a steady **1.7 Hz limit cycle** the whole time:
  - pitch rate 3-15 deg/s, load 1.2-4.4 g at 150 m/s, AoA ±0.6 deg;
  - canards 7-25 deg at full servo speed, fins ±12 deg;
  - roll rocking ±3.5 deg/s in step with the pitch.

## Cause
At the limit the AoA law commands:

> pitch rate = flight-path rate + gain × AoA error

It took the flight-path rate as the pitch rate minus the AoA rate. The AoA rate goes through a 50 ms filter; the pitch rate did not.
- At 1.7 Hz a 50 ms lag is about 30 deg of phase, so roughly half of any pitch-rate wobble was left in the "flight-path rate", slightly ahead of it.
- That share was fed straight back into the pitch command: positive feedback.
- In the log, the FCS's flight-path rate wobbled 2.86 deg/s against 1.69 for the real one (accelerometer), and correlated with the pitch rate (r = 0.46).

**Why it only showed with the wing off:** with the right elevon gone, pitch rests on the canards alone.
- The canards sit ahead of the CG, so their own lift adds to the turn.
- Pitch authority is a fraction of the intact jet's.
- The intact jet, and the damaged Raptor (two tail stabs + TVC, 900 / 550 deg/s² of pitch authority, whose stabs' lift opposes the turn), have the margin to swallow the leak. The damaged Raptor's 17:12 log holds its cap with a pitch-rate wobble of 0.16-0.65 deg/s.

The same expression is in every FCS jet, so the fix goes into all of them as shared code.

## Change
**`FlightPathRateMatched`** ("2. Envelope", dev, **on**): the pitch rate takes the same 50 ms filter as the AoA rate before the subtraction, so both halves of the flight-path rate carry the same lag. false = 0.2.6 exactly.

## Bench
The bench had no J-20 damage data. A minimal one was built from the airframe's part hierarchy (bench only, not shipped).

**Damaged case** (`detach=wing2_R+flap_R`): entry 350 km/h, throttle 0.3, full aft, 1500 m. Speeds below in IAS, m/s. Pitch-rate wobble (sd) and canard swing per 2 s window:

| window | IAS | 0.2.6 | 0.2.7 |
|---|---|---|---|
| 6-8 s | 95 | 2.1 deg/s, 24.5 deg (at servo rate) | 0.4 deg/s, 2.4 deg |
| 8-10 s | 85 | 3.7, 39.2 | 0.4, 2.9 |
| 10-12 s | 75 | 5.3, 46.0 | 0.5, 5.6 |
| 12-14 s | 65 | 4.7, 49.6 | 1.0, 23.3 |
| 14-16 s | 55 | 4.0, 52.2 | 2.4, 50.9 |

**Same damage with a plain AoA limit of 20 deg** (damage limiter off): 0.2.6 cycles (q sd 1.95, canards swinging 2-23 deg). 0.2.7 holds 20.0 within 0.02 deg with the canards still (5.3-5.5 deg).

**Not fixed: below ~65 m/s IAS (~235 km/h) with this damage.** Pitch authority is down to ~33 deg/s². The allocator then swings the canards between full leading edge down and -6 deg and the fins stop to stop, with the left elevon near its stop.
- That is the damage cap allowing an AoA where almost no pitch margin is left: a separate problem, still open.
- In the game log the wobble started at 160 m/s, so most of what you flew is in the fixed range.

**Intact regression set (20 cases, your config):**
- Mean sideslip 8.57 -> 8.16 deg, mean roll 59.8 -> 60.4 deg/s.
- Better: 65 deg AoA roll release 5.2 -> 3.4 deg; pull-then-pushover + roll 350: 11.0 -> 8.0.
- Worse: push-then-pull-through + roll 450: 14.2 -> 16.1.
- Stop / reversal sweep mean sideslip: 5.49 / 9.67 -> 5.71 / 9.50.

These figures are from the experiment tree, which has the same arithmetic. With the key off, 0.2.7 reproduces 0.2.6's damaged-case run exactly (q sd 4.45, the same canard and nz figures).

## Install
- `BepInEx\plugins\J20B_Weilong.FCS\J20B_Weilong_FCS.dll` 0.2.7; 0.2.6 kept as `J20B_Weilong_FCS.dll.0.2.6.bak`. `meta.json` updated.
- No setting of yours changes. The new key takes its default (on).
