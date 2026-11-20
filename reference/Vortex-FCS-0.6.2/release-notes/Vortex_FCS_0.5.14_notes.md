# Vortex FCS 0.5.14: a climb shares the thrust with the translation (no more wait after a climb command)

19 Nov 2026. On 0.5.13.

**Status: benched on one jet (13.8 t), not flown.**

**Known downside, not solved:** with a side stick and a climb together, the nose swings during the bank reversals: up to 10.7 deg/s and 6.5-7.6 deg off the heading before the heading hold brings it back. 0.5.13 does the same in those manoeuvres at 4.6-6.2 deg/s and 1.9-3.2 deg. See "Heading kick" below.

Tom, 19 Nov:

> when i command positive vertical speed and then a short moment later i command yaw, yaw took a while to kick in ... Check if there is any other similar problem to that and fix

> Fix the other ones for the delay control

> No on top fix

## What was wrong

A climb on the stick took the thrust first, in two places:

- **The translation budget.** The climb's whole demand was kept back, so the side stick, the lever and the pedal turn got what was left, which is nothing while the climb accelerates.
- **The governor** (the logic that lets a tilt in only as fast as spare thrust can make up the lift it takes). With the climb asking for full thrust it saw no spare thrust and held every tilt at level.

Both let go only once the climb had its rate, about 3 s later.

## What 0.5.14 does (`StovlClimbShare`, dev cfg, `[9. STOVL]`, on)

It is a change to the budget itself, not a layer on top:

1. **One budget for three demands.** The stick's climb acceleration and the translation's asked acceleration divide the lift at full dry thrust, as fore-aft and sideways already do. Each has its share (the same factor on what each could have alone), and each may use what the other leaves.
2. **The height loop asks for the climb's part only**, so the thrust is there for the tilt.
3. **The governor counts lift going into a climb's acceleration as lift in hand.** A tilt that takes it slows the climb's acceleration and sinks nothing, so the headroom is counted from the thrust the vertical floor needs.
4. **The pedal's turn-rate limit plans on the translation's share**, not on what the climb leaves before the turn has asked for anything.

- **Not shared:** stopping a sink, holding a height and flying a height deficit back keep their whole demand. Only a climb commanded on the stick gives way.
- **No new numbers.** The split is geometry on the existing budget circle.
- The roll posts' burst limit stays on the real thrust headroom, as before.

## Bench (13.8 t). "Climb before" = full climb on the stick 0.5 s before the other command

| | Alone | Climb before, 0.5.13 | Climb before, 0.5.14 |
|---|---|---|---|
| Full side stick: bank after 1 s | 7.0 deg | 0.2 deg | **7.3 deg** |
| Full side stick: time to 1 m/s | 1.38 s | 4.22 s | **1.30 s** |
| Full side stick: time to 3 m/s | 2.45 s | 5.33 s | **2.22 s** |
| 0.4 side stick: time to 1 m/s | 1.43 s | 4.18 s | **1.18 s** |
| Lever forward: time to 2 m/s | 0.93 s | 3.45 s | **0.98 s** |
| Lever forward: time to 8 m/s | 2.28 s | 4.83 s | 2.88 s |
| Pedal at 30 kt: time to half the yaw rate | 1.32 s | 4.48 s | **1.35 s** |
| Pedal at 30 kt: side speed at the pivot | 0.09 m/s | 2.65 m/s | 0.80 m/s |
| Pedal in the hover: time to half the yaw rate | 0.80 s | 0.70 s | 0.68 s |

Climb and side stick at the same moment, and side stick 0.5 s before the climb, give the same as "climb before" (1.32 s and 1.33 s to 1 m/s).

**What the climb gives up** (time from the climb command to 2.5 m/s of climb, and height gained in 3 s):

| | 0.5.13 | 0.5.14 |
|---|---|---|
| Climb alone | 1.77 s, 6.37 m | 1.77 s, 6.37 m (unchanged) |
| With a full side stick | 1.77 s, 6.34 m | 2.25 s, 4.74 m |
| With the lever forward | 1.75 s, 6.40 m | 2.28 s, 4.68 m |

- The vertical speed never went below zero in any of these (lowest -0.01 m/s).
- At 30 kt the turn rate during the climb is about 10 deg/s instead of 13.4 (what the shared budget holds); it rises to 13.4 once the climb has its rate, so 90 % comes at 5.1 s.

## Still open at 30 kt

- The 0.80 m/s of pivot side speed in the 30 kt pedal turn with a climb (0.09 without one). Not looked into further.
- 0.4 side stick with a climb: the pivot's side speed peaks at 1.67 m/s for about 1.5 commanded (alone: 1.57).

## Heading kick (found, cause not isolated)

- When the bank reverses with the thrust at its limit, the allocator's yaw command goes to its stop together with the roll posts for about 0.3 s, against the heading hold's own command, and the nose swings.
- Without a climb the roll posts reach the same stop and the yaw command stays at zero (0.8 deg/s at most).
- 0.5.14 makes it larger because the bank now moves while the climb holds the thrust at the limit: 9.3-10.7 deg/s and 6.5-7.6 deg, against 4.6-6.2 deg/s and 1.9-3.2 deg in 0.5.13 for the same stick inputs.
- The lever with a climb shows none (0.0 deg/s).
- It is in the control allocation, not in the budget; not changed here.

## Checks

- **`StovlClimbShare` false:** byte-identical to 0.5.13 on 11 cases.
- **On, with no climb command** (same 11 cases, after the take-off): within 0.06 deg of bank, 0.04 deg of pitch and heading, 0.008 m/s and 2 mm of height of 0.5.13.
- **Not tested:** other weights, Stock thrust, lopsided loads, ship-relative mode, a climb command during a stop, descents (a descent command is not shared and was not changed). Not flown.
- **Build:** offline smoke test passes; the source zip rebuilds the DLL byte for byte.
- **Bench script:** `bench/scripts/vortex/ax2.py`. Bench CSV: `vShare`.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.14 and `meta.json`. 0.5.13 kept as `Vortex_FS20_FCS.dll.0.5.13.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.14-release`.
- No config migration. Restart the game to load it.
