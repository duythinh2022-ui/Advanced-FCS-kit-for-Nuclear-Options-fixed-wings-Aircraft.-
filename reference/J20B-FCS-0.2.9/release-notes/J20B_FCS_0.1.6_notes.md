# J-20B Weilong FCS 0.1.6: fins no longer kick sideways with pitch stick on the runway

Base: 0.1.5. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov)
- "on ground before i pickup enough airspeed. I pitch up, the rudders move to the left, down the rudder move to the right. After enough airspeed, they are normal again"

## What was wrong (0.1.3-0.1.5)
**The runway pitch split put the same raw angle on both fins.**
- Since 0.1.3 (`RudderPitchAid` on), the ground law adds a fin split with the pitch stick for rotation: `gToe = -stick x RudderToeMax` (20 deg), the same value on both fins.
- That is a toe only where the fin hinges are mirrored (F-22E, Revoker: there the two fins are a paired toe set).
- The J-20B's hinged fins are not mirrored: raw +d on both fins moves both trailing edges the same way. Bench pose at 250 km/h, both fins +10: roll 26.5, yaw -10.1 deg/s², pitch almost nothing.
- So full pull put both fins at +20 (a left rudder kick), full push at -20 (right).
- It stopped once the FCS left its ground mode, because the airborne toe only acts on a paired fin set, and the J-20B's fins are not paired (the pairing log reads `aileron_L+aileron_R` only).

## Changed
- On the runway, hinged fins (the J-20B's) now split by their own geometry. Each fin already carries a `toeOut` sign (which raw direction moves its trailing edge outboard; the runway air brake already uses it).
- Pull = trailing edges **in**, push = trailing edges **out**, 20 deg each at full stick.
- Toe-in is the nose-up toe on these out-canted fins. A trailing edge moving inboard pushes the fin outboard, and on a fin leaning outboard that force points down at the tail. Bench pose:

| Fin pose (deg) | 250 km/h, 0 deg AoA: pitch | 250 km/h, 10 deg AoA: pitch |
|---|---|---|
| toe-in 10 each | **+5.3** deg/s² | **+6.2** |
| toe-out 10 each | -5.1 | -4.0 |

- No new setting, and no config migration.

**Shared code:**
- The change is inside the ground law, gated on `SurfaceEffector.Hinged`. Only J20Bridge hinge surfaces set `Hinged`, so jets without hinged fins take the old line unchanged.
- Nothing to port.

## Bench
Takeoff mode (`takeoff`, new `finL` / `finR` csv columns), stick pull then push on the runway in the ground mode:

| | tail_L | tail_R | yaw, 100 km/h |
|---|---|---|---|
| 0.1.5, full pull (from the code; both fins same raw value) | +20 | +20 | -2.3 deg/s² (with roll 6.1) |
| **0.1.6, full pull** | **-20.0** | **+19.9** | 0.0 |
| **0.1.6, full push** | **+19.9** | **-20.1** | 0.0 |

- Pitch at 100 km/h: toe-in +1.3 deg/s² against neutral fins. It is small at rotation speeds, but it is now the right sign and makes no yaw.
- Full-throttle takeoff (rotate at 6 s, stick 0.6): fin sum |L + R| in the ground mode is at most 0.07 deg (it was 40 at full stick).
- Airborne regressions (hands-off 500 km/h, 1 g roll 600, 9 g pull 900, engine-out pull 300) are **bit-identical** to 0.1.5 (alpha and phi traces).

## Not checked
- In game. The bench takeoff speed trace differs from the game's (the throttle setting barely changes the roll here), so rotation feel is unverified.
- The airborne fin window still opens to the toe target on both fins when it is non-zero (`toeTgtF`). That is inert for allocation on unpaired fins, and you reported the air behaviour as normal, so it was left alone.
