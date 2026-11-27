# J-20B Weilong FCS 0.1.3: the fins count as a pitch effector

Base: 0.1.2. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov)
- 02:58: "find the airframe cause and fix"
- 02:59: "is it some sort similar wall happened with the flanker"
- 03:04: "also check latest log, the jet still struggles to hold max aoa when rolling. Find cause, either fcs or airframe, and fix"

## The 03:00 log (0.1.2, confirmed in LogOutput.log)
Full aft + full roll segments (AoA after the first capture):

| Time (s) | IAS (km/h) | AoA during the roll |
|---|---|---|
| 18.8-29.1 | 230-335 | 35.6-39.9 |
| 32.5-47.2 | 225-355 | 37.5-40.5 |
| 52.9-62.4 | 300-450 | 36.9-40.1 |
| 66.3-70.6 | 275-365 | 30.3-40.0 |

The worst dip, 66.5-70.5 s:
- The jet goes over the top (pitch attitude 86 deg) at 275-340 km/h while rolling. The FCS's nose-up authority estimate `aUp` is down to 8-22 deg/s^2.
- At 68.8 s the AoA reaches 39.5 against a 38.5 command at 20 deg/s, and the capture brakes hard: the canards go +25 LE down (local AoA 39 -> 14) for about 0.3 s.
- Then the nose-up isn't there to come back: q 7-10 deg/s against 23 asked. AoA falls to 30.3, with the right fin pinned at -30 for roll / yaw.

## Not a Flanker-style wall
- The Su-30's wall (10 Nov) was the airframe: past 55 deg its stabs stalled, and above about 350-400 km/h the airframe's nose-down beat every pitch effector combined.
- **The J-20B has no such corner.** A per-part pitch breakdown at +-12/20/28/36 deg (300 km/h) shows a mirror airframe: every part's +AoA and -AoA moments cancel to within 1-2 deg/s^2. That leaves only the LE flap (12 deg at negative AoA, 24 at positive) and the wing camber, together under 2 deg/s^2.
- **Static authority is about symmetric.** Best net acceleration at 300 km/h:
  - nose-up +25..+32 at +12..+28 deg;
  - nose-down -19..-23 at -12..-28 deg.
- Net authority stays on the right side of zero everywhere inside the limits.

## What was wrong: the fins were pitch-blind
- **Toe is a strong pitch effector.** The J-20B's fins are canted and all-moving. Toeing them makes pitch. Bench plant, 336 km/h, elevons and canards fixed:

  | Toe | -25 deg AoA | +25 deg AoA |
  |---|---|---|
  | L+10 / R-10 (mod angles) | -8.5 deg/s^2 | -4.7 |
  | L-10 / R+10 | +5.0 | +9.8 |

  The same angle on both fins (L+10 / R+10) is mostly roll: 33 deg/s^2 of roll and 12 of yaw.
- **The FCS ignored that pitch.** The shared fin model counts the fins' pitch only with `RudderPitchAid`, and the J-20B had it hard-coded off (inherited from the Revoker). So the allocator moved the fins for roll and yaw without seeing the pitch moment that came with it. Bench:
  - **Full aft at 400 km/h:** the fins sat toed about 20 / -20 the nose-down way. The elevons ran 19-25 deg TE up against it.
  - **Full push at 300 km/h:** at -25 deg the fins were at -28.6 / +17.6, which is a nose-up toe plus some roll. Against all four elevons fully TE down, that cost 11.7 deg/s^2 of nose-down. The elevons were also crossed to cancel the roll: the plant gives +3 deg/s^2 net for that pose, against -22 for all four elevons TE down.

## Changed
| Setting | 0.1.2 | 0.1.3 |
|---|---|---|
| `RudderPitchAid` (dev, 4. Gains; was hard-coded) | off | **on** |

**What `RudderPitchAid` does** (the F-22E 2.0.6/2.0.7 rudder pitch aid, shared code):
- The allocator sees the fins' pitch.
- The fins toe with the pitch demand, up to `RudderToeMax` (20) each way. The allocator may use more when everything else runs short.
- They go back to roll and yaw with lateral stick or pedal (`toeLatGate`).

The setting is new as a key, so no migration is needed.

## Bench (0.1.2 vs 0.1.3)
3000 m.

**Full aft + full roll at 3.1 s** (throttle 1). Roll angle in 3.9 s, AoA min / max during the roll:

| IAS (km/h) | 0.1.2 | 0.1.3 |
|---|---|---|
| 250 | 58, 39.4 / 40.6 | 57, 39.8 / 40.8 |
| 325 | 70, 39.1 / 40.6 | 61, 40.2 / 40.9 |
| 400 | 73, 38.6 / 41.0 | 75, 40.3 / 40.7 |
| 475 | 69, 38.5 / 40.4 | 76, 40.1 / 40.9 |
| 550 | 75, 38.0 / 40.7 | 82, 40.0 / 40.9 |
| 625 | 87, 34.2 / 40.1 | 98, 38.2 / 40.7 |
| mean roll | 72 | 75 |

**With your config's pitch-only canards** (`CanardSymmetricOnly` on: both canards identical all through the 03:00 log). Same full aft + full roll cases:

| IAS (km/h) | 0.1.2 | 0.1.3 |
|---|---|---|
| 250 | 27, 32.0 / 39.5 | 56, 38.5 / 40.5 |
| 325 | 48, 38.3 / 40.2 | 65, 38.8 / 40.9 |
| 400 | 49, 36.3 / 39.7 | 69, 39.3 / 40.9 |
| 475 | 77, 31.0 / 39.5 | 81, 40.0 / 40.8 |
| 550 | 52, 36.4 / 39.7 | 82, 39.9 / 40.8 |
| 625 | 44, 29.8 / 39.7 | 103, 38.1 / 40.8 |
| mean roll | 50 | 76 |

0.1.2 with pitch-only canards dips to 30-36 deg, which matches what you saw.
- At 40 deg a roll around the flight path needs yaw. Elevon roll comes with almost as much adverse yaw: 78 roll vs -72 yaw deg/s^2 at full differential.
- The fins are the yaw effector.
- In 0.1.2 they spent part of their travel on a toe the FCS couldn't see, and the elevons were already near their stops holding pitch against it.

**Climbing rolling pull like the log's 66 s case** (340 km/h, full aft, full roll entered during the pull-up):

| Roll entry | 0.1.2 | 0.1.3 |
|---|---|---|
| at 1.5 s | AoA 33.5-40.1, roll 92 deg | AoA 36.9-40.8, roll 106 deg |
| at 2.5 s (pitch attitude to 84) | AoA 37.5-40.2, roll 64 deg | AoA 39.7-40.7, roll 63 deg |

**Full push** (throttle 0.5, limit -30). AoA after 2.5 s, min / max / mean:

| IAS (km/h) | 0.1.2 | 0.1.3 |
|---|---|---|
| 200 | -29 / -17 / -26 | -31 / -26 / -30 |
| 225 | -28 / -14 / -22 | -31 / -28 / -30 |
| 250 | -29 / -19 / -27 | -33 / -21 / -26 |
| 275 | -28 / -15 / -20 | -31 / -28 / -30 |
| 300 | -27 / -9 / -16 | -31 / -27 / -30 |
| 350 | -27 / -9 / -19 | -30 / -13 / -26 |
| mean | -21.6 | -28.7 |

The 250 km/h push now overshoots to -33, 3 deg past the limit.

**Steady full aft, 250-625 km/h, before the roll:**
- Elevons: mean |deflection| 19-25 -> 0-5 deg (11 at 625).
- Fins: toed 27-29 deg.
- Canards: about 17 LE down. With the fins carrying nose-up, the canards trim the rest out.
- Speed at the end of the roll cases: 2-18 km/h more (less elevon drag).

**Unchanged:**
- **Hands-off at 500 km/h:** AoA 2.2-2.9 (0.1.2: 1.8-2.7).
- **1 g full roll at 600 km/h:** 161 deg/s, 155 deg at 1.3 s.
- **900 km/h full pull:** 9.34 g.
- **Engine-out full pull at 300 km/h:** 39.8-40.3, sideslip 1.2 deg max (0.1.2: 0.4).

## Limits of this check
- **The bench and the game disagree in the steady hold.** At 367 km/h, full aft, no roll (log 64.5-66.5 s), the game had the fins near neutral (effector -3 / +9) and the elevons at about 5 deg. The bench at the same condition toed the fins about 20 deg and ran the elevons at 19-25.
  - So part of the bench's 0.1.2 -> 0.1.3 gain may be removing a bench-only fin toe.
  - The gain in game is unverified.
- **No calibrated replay yet.** A closed-loop replay of the log's 64-72 s window diverges at once: the bench loses speed faster (94 vs 101 m/s at 65 s), and the log's T/W column reads 0. Both versions sag in it; 0.1.3 less (lowest AoA 36.2 against 32.5). Treat that as qualitative.
- **The capture braking at 68.8 s** (canards +25 LE down for 0.3 s, then the undershoot) is not changed directly in 0.1.3.
