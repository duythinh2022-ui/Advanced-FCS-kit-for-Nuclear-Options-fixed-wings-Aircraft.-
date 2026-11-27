# Vortex FCS 0.5.25: one sharing rule for the STOVL thrust budget (replaces the 0.5.24 branch)

20 Nov 2026. On 0.5.24.

**Status: benched, not flown.**

Tom, 20 Nov, on 0.5.24:

> is this an on top fix? i want the fcs has to share the budget for every axis when needed

He chose to keep the released-stick height hold protected.

## Was 0.5.24 a bolt-on? Yes, partly
The splitting itself was model-based: `ClimbPart` scales both asks by one factor until they fit, and each may use what the other leaves. But *which* vertical demands took part was decided case by case:
- 0.5.14 added a branch for a stick climb;
- 0.5.24 added a second branch for a descent's stop, with its own conditions.

A third case would have needed a third branch.

## Change (`StovlSharedBudget`, dev, on; replaces 0.5.24's `StovlSinkStopShare`)
One rule decides what the vertical axis keeps whole and what it shares.

- **Protected, kept whole:**
  - the height hold: released stick holding a height, including flying a dip or a deficit back (your choice);
  - the deceleration the ground needs: the descent brought down to the touchdown rate in the height left above the touchdown envelope, and all of it once the sink is at or past what the envelope allows;
  - the envelope's own deceleration and the height integral.
- **Shared:** everything else the vertical asks, whatever asks it: a climb, easing a commanded sink, stopping a released descent. It is one demand beside the translation's (side and fore-aft, already one vector). When both together ask for more than the thrust holds, each gets the same share of what it could have alone and may use what the other leaves.
- **Before both:** the weight the wing doesn't carry and the thrust-lag reserve.
- **Nothing shared:** on the wheels, or when the vertical is giving lift back.
- The tilt governor's headroom and the translation budget read the shared demand through the same path for every case. There is no per-case condition left.

**Outside this budget, unchanged:**
- **Yaw:** the yaw nozzle doesn't take tilt budget. Its authority is cut when spare lift with the nozzle centred falls under 1 m/s² (`yawLiftK`), which protects the hold. In the logs a sink stop doesn't slow yaw (428.2 s in your 10:50 log).
- **Roll post / fan pitch bursts:** capped by the thrust's rate headroom so a burst can't dip the height. That also protects the hold.
- Both stay as they are because of your choice. The 1 m/s² reference in `yawLiftK` is a fixed number; replacing it with a lift-based term is a possible follow-up.

## Bench (12.15 t, ~72 m, −5 m/s descent released, input 0.1 s later; values 1.8 s after input)

| Case | 0.5.23 | 0.5.24 | 0.5.25 |
|---|---|---|---|
| Full side stick: bank / side speed | 0.5° / 0.14 m/s | 13.6° / 2.05 m/s | 13.6° / 2.05 m/s |
| Full side stick: height lost | 5.42 m | 6.30 m | 6.30 m |
| Lever 60 %: forward speed | 0.40 m/s | 5.91 m/s | 5.91 m/s |
| Lever 60 %: height lost | 5.40 m | 6.66 m | 6.66 m |
| Full pedal: yaw rate | 25.5 °/s | 26.0 °/s | 26.0 °/s |
| Stick eased from −5 to −2 m/s + full side: bank | 7.0° | 13.7° | 13.7° |

**Other sets:**
- **12-case set:** 10 cases identical to 0.5.23. The two climb cases differ only in the second decimal:
  - climb + 30 kt pedal turn: same yaw rate, bank, climb rate and heights;
  - climb + lever: same forward speed and heights.

  The climb's floor is now the ground's need only, not "do not sink".
- **Regression set** (vertical landings, hovers, vertical and short take-off, mission): unchanged.

## Not checked
- Not flown.
- Translating during a sink stop **close to the ground** is not benched as its own case. The envelope rule keeps the ground's need whole, and the landings in the regression set are unchanged.

## Release

Main DLL 0.5.25. Release folder `C:\Games\NO mods\Vortex-FCS-0.5.25-release`. Dev switch `StovlSharedBudget` (on); false = 0.5.23 (only a stick climb shares).
