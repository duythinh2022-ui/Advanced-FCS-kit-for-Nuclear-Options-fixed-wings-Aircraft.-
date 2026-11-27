# FS-12 Revoker FCS 1.4.2 (16 Nov 2026): the yaw nozzle no longer parks at neutral in the middle of a yaw stop or reversal

Base: 1.4.1. Patch bump: one gate on an existing hold.

Tom, 16 Nov 09:37: "the falling leaf reversal, the tvc stops the yaw then stays at neutral for a split sec then it reverse the yaw direction"; 09:55: "fix the revoker".

## What was wrong (16 Nov 09:21 log)
- **The moment:** reversal at 39.2 s (69 deg AoA, ~220 km/h, yaw rate -75 -> +77 deg/s). The yaw nozzle reached -15 deg at 39.55 s. From 40.15 s it was pulled back to -1.8 deg, sat there until 40.55 s, then went back to -15.
- **Yaw acceleration:**
  - ~84 deg/s^2 before (39.6-40.1 s);
  - ~38 during the pause (40.15-40.55 s);
  - ~65 after (40.6-41.0 s).
  - Through the pause the yaw rate was 20-28 deg/s off its command.
- **Cause: `YawTvcNeutralHold` (weight 300, since Revoker 0.1.0).** The hold is meant for a held turn: it parks the yaw nozzle at neutral and lets the rudder and canards carry the steady yaw.
  - Its "stick steady" test compares the stick with a copy (`srHeldY`) that follows it at 2 per second. After a full stick reversal the copy catches up ~0.95 s later; after a release, ~0.5 s later. Both land in the middle of the yaw stop.
  - Its other conditions all pass at 60-75 deg AoA near the zero crossing: roll error small, sideslip under 2 deg, |p| under 5-15 deg/s.
  - Nothing checked the yaw rate.
- **Not a one-off:** the same thing happens in most high-AoA stops and reversals of that flight: 44.0, 46.2, 58.3, 62.8, 70.0, 75.8, 80.7, 81.5 and 131.3 s. At 79.5-80.0 s it fires during a yaw build-up. Probably also the "nozzles stop the roll, go to neutral, then yaw the other way" of 5 Nov.

## Change (`YawTvcNeutralYawGate`, dev, on)
- **Rule:** the hold only comes in while the yaw rate is on a steady command. It fades out:
  - as the yaw-rate error grows from 2 to 8 deg/s (the same shape as the roll-error gate beside it);
  - as the yaw-rate command's own rate (50 ms filtered) grows from 5 to 20 deg/s^2.
- **Why the second gate:** in a paced reversal the yaw rate tracks a command that is sweeping through zero, with a small error. Without it the hold still came in at the zero crossing in the bench (weight up to 0.22).
- **Held turns:** yaw on command, command steady, so the hold works as before.
- **Dev key:** `4. Gains / YawTvcNeutralYawGate` (true). false = 1.4.1. Takes effect at once. No migration (new key).

## Bench (your current config; the bench fixed on 16 Nov, see "Bench fixes" below)
### Closed-loop replay of your reversal (09:21 log, from 39.55 s, 3 s lead-in)
With the gate off, the bench reproduces what 1.4.1 did in the game.
- **Hold:** arms at 40.15 s (game 40.15) with weight up to 0.27 (game 0.19).
- **Nozzle:** pulled to -0.7 deg (game -1.8).
- **Rudder:** goes to the opposite stop at 40.15-40.20 s (game 40.15).
- **Yaw rate:** within 1.9 deg/s RMS of the game (3.9 max) from 39.55 to 40.6 s.

| 40.15-40.55 s, then the build-up | game (1.4.1) | bench 1.4.1 | bench 1.4.2 |
|---|---|---|---|
| Yaw acceleration 40.15-40.55 s | 38 deg/s^2 | 34 | 72 |
| Yaw nozzle 40.15-40.55 s (min / mean) | 1.8 / -5.3 deg | 0.7 / -4.7 | 15 / -15 |
| Yaw rate through zero | 40.55 s | 40.67 s | 40.38 s |
| Yaw rate +30 deg/s | 41.05 s | 41.28 s | 40.82 s |
| Peak sideslip 39.55-41.6 s | 0.7 deg | 1.2 | 0.7 |

**Where the replay holds:**
- Started at 38.5 s, the bench's rudder flips to its far stop 0.55 s early (39.60 s): that decision is a near-tie in the allocator, tipped by small earlier differences (the left aileron runs ~5 deg off the game's from 39.50 s).
- Started at 39.55 s it reproduces the flip; started at 39.80 s it misses it.
- So the replay's limit is ~1 s ahead of such a tipping point.

### Falling leaf (ifly)
Full aft from 360 km/h, full left roll at 4.0 s, reversed at 7.0-7.25 s, released at 11.3-11.5 s. Times from each stick move.

| | 1.4.1 (gate off) | 1.4.2 |
|---|---|---|
| Yaw build-up to -60 deg/s | 1.63 s | 1.22 s |
| Reversal: rate through zero | 2.07 s | 1.83 s |
| Reversal: rate to +30 deg/s | 4.62 s | 2.63 s |
| Reversal: rate to +55 deg/s | not reached | 3.77 s |
| Release stop to under 3 deg/s | 1.52 s | 1.12 s |
| Hold active (> 0.05) with the yaw rate > 8 deg/s off command | 1.83 s | 0 s |
| Peak sideslip | 2.5 deg | 4.0 deg (RollStopSideslip 6) |

### Regression suite (reg, sweep, corkscrews, damage), fixed bench, gate off vs on
- **Damage suite, clean jet:**
  - leaf stop at 296 km/h: 1.87 -> 1.22 s;
  - leaf stop at 370 km/h: 1.83 -> 1.25 s;
  - leaf reversal at 296 km/h: 2.10 -> 1.42 s, peak sideslip 1.6 -> 2.6 deg;
  - push-overs at 560 / 600 km/h: peak sideslip 4.2-4.5 -> 1.2-1.4 deg.
  - Damaged configurations unchanged.
- **Leaf reversal at 370 km/h: 1.23 -> 1.37 s.** This is not slower braking.
  - In 1.4.1 the hold capped the yaw build-up before the reversal at ~46 deg/s (nozzle at -1). 1.4.2 reaches 65, so it has more to reverse; it brakes at ~65 deg/s^2 against 1.4.1's ~53.
  - After the zero crossing 1.4.1's hold arms again (to 0.18) and stalls the yaw at -7 deg/s for 0.3 s; 1.4.2 doesn't.
- **Corkscrews:**
  - **Full throttle:** within noise.
  - **Throttle 0.6, 350 km/h:**
    - velocity-roll mean 36 -> 38 deg/s;
    - its standard deviation 3.1 -> 4.4 deg/s (the rate climbing to its command; 1.4.1's hold held it under);
    - rudder direction reversals 31 -> 23.
  - **Throttle 0.6, 450 km/h:** unchanged within noise; rudder direction reversals 20 -> 24.
- **Regression set and sweep:** sideslip peaks unchanged to 0.1 deg; only the pace / yaw-saturation columns move.

## Bench fixes (16 Nov; bench only, the FCS is untouched)
The first replay of the log left the game's flight at once (yaw -47 vs -75 deg/s). Three causes.

**1. A cold FCS at the replay start.**
- **Before:** the replay settled the FCS on a frozen pose, so its rate-command trajectories, rate caps and measured-authority / capacity estimators started cold. At 38.5 s the yaw command was -21 vs the game's -69 deg/s and the yaw cap 3 vs 81, and the jet braked at once.
- **Now (`lead=2`, default):** for 2 s before t0 the plant is posed from the log every tick and the FCS flies the logged sticks on it, its effectors reset to their logged positions after each tick. It reaches t0 with the game's history (yaw command -67 vs -68.5).
- `lead=0` = the old settle.

**2. Thrust (`thrustLog=1`, default).**
- **Before:** the engine model only runs inside the plant step, so through the settle / lead-in the FCS read zero thrust and no yaw-nozzle authority. In the closed loop the throttle-from-T/W guess left the measured yaw authority ~11 % under the game's.
- **Now:** each nozzle's thrust is the logged one (tw x logged mass x g), in the lead-in and the closed loop, as eqerr already does.
- **Also:** the airbrake follows the logged abOpen in the closed loop.

**3. The FCS's reading of the rudder's flow (`RudFlowKr / RudFlowKp`, -1.35 / +0.45, default on).**
- **The offset:** the game FCS reads the rudder's local flow with a rate-proportional offset the rigid model doesn't make: bench - game = Kr x r/V + Kp x p/V (deg; deg/s; m/s). Equation error on four Revoker flights (11 Nov 13:16 / 14:19, 12 Nov 17:26, 16 Nov 09:21): pooled -1.35 / +0.45. Leave-one-out at |AoA| > 40:
  - 16 Nov: 0.92 -> 0.47 deg;
  - 12 Nov: 0.36 -> 0.13;
  - 11 Nov 14:19: 0.92 -> 0.19;
  - 11 Nov 13:16: 0.31 -> 0.30.
- **How it's applied:** only the FCS's view of the rudder carries it (its AeroPart.rb is a view body posed with the turned flow); the plant's aero is unchanged (equation error identical: roll 5.5 / pitch 3.6 / yaw 3.2 deg/s^2 RMS, R^2 0.98-0.99).
- **Tried and rejected:** evaluating the rudder on vstab1's body (the aero or the FCS's view). The yaw equation error went 3.2 -> 4.0, and the FCS-view rudder residual to 6.8 deg.
- **Cause:** not identified (fin joint compliance is a candidate).
- **Effect on the suite:** the calibration moves 1.4.1's own numbers (e.g. leaf stop 296: 1.43 -> 1.87 s); the tables above are all on the fixed bench.
- `RudFlowKr=0 RudFlowKp=0` = the old bench.

**New replay options:** `lead=<s>`, `leadOut=1` (also writes the lead-in's FCS rows to `<out>.lead.csv`), `thrustLog=0`.

**Files:** `bench/sim/Replay.cs`, `Airframe.cs` (RudFlow*), `Plant.cs` (view body in Pose, RudFlowSign), `Program.cs` (keys); `PORTABLE_REPLAY.md`.

## Not changed / seen
- **Rudder at high AoA:** in the same window the rudder command jumps to the opposite stop (+24 -> -25 deg) and back at the zero crossing, with its local AoA sweeping from -17 to +25. The bench reproduces it, and it is a near-tie in the allocator (see the replay above). Not changed.
- **Other jets:** the Compass and Ternion FCS were ported from the Revoker and may carry the same hold. Not checked.

## Status
- **Compiled** against the game's assemblies. Built twice, same bytes: 1,351,168 bytes, SHA-1 9f9ca6118c3c1a322aff33c6124560148d3210c8, md5 4c34c89421ef90b84cfe258a500a21a0.
- **Source check:** the 1.4.1 source rebuilt here gives the released 1.4.1 DLL byte for byte.
- **Not flown.**

## Install
- **Main:** `BepInEx\plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` 1.4.2. 1.4.1 is kept as `Revoker_FS12_FCS.dll.1.4.1.bak`. `meta.json` updated.
- **Dev DLL:** unchanged.
- **LogOutput:** shows "FS-12 Revoker FCS 1.4.2 ... ready".
- **Release folder:** `C:\Games\NO mods\Revoker-FCS-1.4.2-release`.
- **Mod manager:** update its record, or its modpack may put 1.4.1 back. Restart the game to load it.
