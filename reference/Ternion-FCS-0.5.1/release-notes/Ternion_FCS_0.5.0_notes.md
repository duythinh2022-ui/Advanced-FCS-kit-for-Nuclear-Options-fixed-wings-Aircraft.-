# FS-3 Ternion FCS 0.5.0: the fleet's current governor and allocator laws

Base: 0.4.2. Minor bump: law change. **Status:** compiled against the game's assemblies, benched (your config: your current
`Ternion_FS3.FCS.cfg` + `.Dev.cfg` match the bench config except Telemetry, ReversedFlowNoseAxis and EngineOutAoACap, which
these cases don't touch). **Not flown.**

Tom (24 Nov): "do ternion and update it to the latest governor and allocator fixes".

The Ternion was last updated on 17 Nov, so it was missing everything since: the F-22E 4.5.2 / 4.5.3 governor pieces, the roll-cap
fix, the three pitch-law changes from the AoA-dip work, the pull-trade fix, the reach-model key and the push-governor stick rule.
They went in with the same patch scripts as the Compass (0.6.0 -> 0.7.2), whose 0.6.0 code is the Ternion's nearest relative. Every
patch applied cleanly. Apart from the comment labels, the Ternion's difference from the Compass is the same as before.

## Changes (all shared code; the same text as the other jets)
- **F-22E 4.5.2 `PaceRollSatDelivered`** (6. Lateral governor, on): when the allocator reports the roll saturated, the pace window
  uses the roll capacity actually being delivered, not the remembered floor (a decaying memory of earlier roll accelerations).
- **F-22E 4.5.3 `LatShareNoGate`** (on): the lateral stab share follows the lateral stick alone. Before, it was gated on the pitch
  stick being held, so it vanished the instant the stick was released. The released-pitch priority now yields to it.
- **F-22E 4.5.3 `PlanFollowsJet`** (on): a braking pitch plan stays within 0.1 s of its own deceleration (at most 8 deg/s) of the
  jet's actual rate, so it can't run ahead of a jet whose surfaces are busy rolling. New log column `planHeld`.
- **Roll-cap fix `RateCapFromHold`** (Revoker 1.7.0 / F-22E 4.7.0, on): the roll-rate cap reads its yaw capacity from the hold
  allocation (the surface positions that give zero angular acceleration at the present rates) instead of this tick's command. The
  command followed the roll target, so the old read fed back into the cap. New log columns `ceRevP`, `ceRevN`, `ceHoldX`, `ceHoldZ`.
- **`TradeRollHave`** (Revoker 1.8.1, on): pulling or pushing under a roll, the pull trade's pitch share is reckoned against the
  roll the jet actually has, not the roll it was asked for.
- **`PushGovStickCap`** (Revoker 1.8.5 / F-22E 4.7.1, on): the push governor may slow a change in the roll but never raises the
  roll above what the stick asks for. This is the "roll keeps going after release, then pitch kicks in" fix.
- **In the code, off by default on this jet:**
  - `LateralReserveBacked` and `PlanHoldNozzleShare`: off, as on the Compass and the other non-Revoker ports.
  - `ReachHeldStick`: off, a dev key, as on every jet.
  - `RateBudgetOnAoARate`: off on this jet only. See below.

With every new switch off, the bench is identical to 0.4.2: every column of all 20 regression cases matches, except the tick-timing
columns. 0.4.2 rebuilt from its source zip is byte-identical to the shipped 0.4.2 DLL. The native core is unchanged (the same
prebuilt `Ternion_FcsCore.dll` as 0.4.2).

## Why `RateBudgetOnAoARate` is off on the Ternion
This law (Revoker 1.8.0) stops the pitch-rate budget from holding the AoA rate below 6 deg/s. It is the fix for the AoA sinking over
the top of a slow loop. "AoA rate" here means the body pitch rate minus the flight path's own turn rate. On the Ternion's bench it
does harm at the 55 deg limit:
- At low speed (full aft, 200-230 km/h after the entry) the nose-up authority is already used up. The pitch command sits 7-10 deg/s
  above the pitch rate the jet is actually making.
- The floor raises that command further (qCmd ~35 vs ~32 deg/s), to a pitch rate the jet can't make.
- The allocator moves roll surfaces over to pitch to chase it. The roll reversal stalls, and the AoA dips further anyway.

That breaks the governor rule (lower the command where an axis can't keep up, never raise it), so the switch is off on this jet.

The test: full aft held at the 55 deg limit, full roll 2.05-5 s, then a full reversal (damage-suite "leaf" cases, 1500 m). Velocity-
vector roll rate (p cos a + r sin a) 3 s after the reversal starts, and the lowest AoA after it:

| entry | 0.4.2 | 0.5.0 with it on | 0.5.0 as shipped (off) |
|---|---|---|---|
| 296 km/h reversal | -13.1 deg/s, reverses in 2.88 s, AoA min 49.8 | -3.4, never reverses in the 3 s window, AoA min 48.5 | -10.9, 2.97 s, AoA min 50.5 |
| 370 km/h reversal | -15.5, 2.75 s, AoA min 49.4 | -4.9, never, 49.8 | -14.8, 2.82 s, 50.6 |
| 296 km/h stop (to under 3 deg/s) | 1.47 s | 1.68 s | 1.53 s |
| 370 km/h stop | 1.15 s | 1.53 s | 1.15 s |

With it on, the regression cases reach 1-4 deg more AoA in the window while the pull and roll overlap (pull-then-roll 400: max 47.1
-> 50.3; full aft + roll + reversal 500: 48.7 -> 52.9). That's still under the 55 limit, and those cases aren't holding the limit.
Turn it on in the dev config ("2. Envelope") to compare in the air. Where this probably applies elsewhere: any jet flown at an AoA
limit its nose-up authority can't hold at low speed. The J-20B bench (nose-up runs out at 47-48 deg against your 55) is the obvious
one. Not checked on that jet in this release.

## Bench, 0.4.2 -> 0.5.0
**Regression set (20 cases):**
- Mean peak sideslip 2.19 -> 1.93 deg. Mean roll 52.5 -> 56.4 deg/s; mean peak roll 99 -> 104.
- Pull-then-pushover + roll 350: sideslip 4.9 -> 2.9, roll 103 -> 112 deg/s.
- Pull-then-pushover + roll 450: 3.1 -> 3.6, roll 140 -> 154.
- Push-then-pull-through + roll 350: sideslip 4.2 -> 2.9.
- Push-then-pull-through + roll 450: sideslip 4.6 -> 3.7, roll 80 -> 109 deg/s.
- Pull-then-roll 320 / 400: peak roll 39 / 45 -> 55 / 66 deg/s, sideslip 1.4 / 1.3 -> 1.8 / 1.7.

**The trade you'll feel (`PlanFollowsJet`):** under full roll and full aft together, the pull now gives way to the roll more than it
did. Push-then-pull-through + roll 450 over the whole run:
- AoA at 3 s / 4 s: 28.6 / 43.6 -> 18.8 / 33.1 deg. Within 0.5 s of the roll release (at 4.05 s) it's back within 2 deg of 0.4.2 (45.2 vs 43.6 at 4.5 s).
- Roll at 2.5 s: 68 -> 132 deg/s.
- Peak sideslip 4.7 deg (at 2.3 s, during the roll) -> 5.6 deg (at 4.6 s, as the late pull catches up after the roll release).

The 500 km/h full-aft roll and reversal behaves the same way: AoA at 2 s 38.0 -> 31.7, the same by 3 s. With `PlanFollowsJet` off
these return to 0.4.2. It's on on every other jet.

**Push governor stick rule** (full roll at low AoA, then roll released and full push at the same moment, 1500 m):

| entry | push governor holding / raising the roll target | peak roll target | roll under 20 deg/s after | peak sideslip |
|---|---|---|---|---|
| 300 km/h | 0.32 -> 0.05 s | 178 -> 104 deg/s | 0.72 -> 0.48 s | 2.6 -> 2.2 |
| 450 / 600 km/h | 0 -> 0 (governor not engaged) | 131 / 158, unchanged | 0.37 / 0.43, unchanged | 1.1 / 0.4, unchanged |

**Other sets:**
- Stop / reversal sweep (33 cases): stop sideslip mean 1.40 -> 1.42, reversal 2.22 -> 2.27, worst 2.4 / 4.3 -> 2.4 / 4.4.
- Corkscrews (20 cases): within +/- 1.5 deg sideslip case by case, no trend.
- Damage suite:
  - Leaf cases as in the table above.
  - Pushovers at 560 km/h: velocity roll 90 -> 114 deg/s, sideslip 2.0-2.2 -> 2.7.
  - The intake-off engine case departs on both builds (AoA 88).
  - The suite's fin / rudder removal doesn't take effect on this bench (those rows match the clean ones on both builds). It's a
    bench damage-key mismatch, already there in 0.4.2.

## Install
- `BepInEx\plugins\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` 0.5.0; 0.4.2 kept as `Ternion_FS3_FCS.dll.0.4.2.bak`. `meta.json` updated.
- New dev keys take their defaults the first time 0.5.0 runs. None of your existing values change.
- Dev unlock DLL unchanged (the 0.4.2 one).
