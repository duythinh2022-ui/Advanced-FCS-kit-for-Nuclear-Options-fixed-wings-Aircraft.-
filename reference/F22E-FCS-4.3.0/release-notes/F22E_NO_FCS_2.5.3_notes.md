# F-22E FCS 2.5.3

Base: **2.5.2**. The governor gets the pace job: one rule - the yaw axis's capacity - now sets how fast a lateral move may run, not only how fast the roll may go. Built against the 31 Oct 01:40 log (2.5.2, level 1 to ~150 s, then level 3; margins 4/6/8, then 4/5/5 from ~217 s). Bench on the calibrated plant, not flown.

## What the 01:40 log showed
28 excursions past 5 deg, four kinds:
1. **Stops and reversals at 20-30 deg AoA, 140-195 m/s, level 3** (the worst: 10.5 at 195.4 s, 14.2 at 198.8 s, 7.5 at 177 s, 6.7 at 207 s, stops 5.5-6.4). With the yaw caps off the trajectory braked at the `MaxRollAccel` clamp (720 deg/s^2; body roll decelerations of 425-670 measured) where level 1 allowed 300-400. At 22 deg AoA that asks the yaw for 270 deg/s^2; the rudders' braking yaw was 85-100 and in a reversal they first cross from the old stop (0.45 s). The yaw sat 25-50 deg/s behind for half a second. The fitted stop model (tau 0.054) was fitted at level 1; these ran tau 0.2-0.56, so the margin cap predicted 4-8 where the game gave 7-14. A rate cap alone cannot hold these: with the 720 brake the rate for 4.6 deg at 22 deg AoA is ~17-25 deg/s.
2. **Rolling pulls at 25-45 deg AoA, 105-160 m/s (level 1; 57 / 80 / 105 / 143 s): 5.9-8.5 against 5-5.8.** The pitch cap and the reserve hold both fired, 0.3 s after the rudders pinned; the AoA was already rising at 50 deg/s, which alone needs ~55 deg/s^2 of yaw.
3. **Stick released at 40-65 deg AoA, 90-110 m/s (214 s: 15.5 deg for 5 s; 234 s 8.3; 321 s 14.5; level 3).** A 62 deg/s yaw rotation told to stop in 0.3 s; the yaw managed 17-29 deg/s^2 with the rudders pinned. This was `CoordinatedStop`'s case, which level 3 had switched off with nothing in its place.
4. **Deep stall under 50 m/s IAS (43 / 116 s, 65 deg AoA, 27-45 m/s, 11-16 deg).** `aYaw` 8 deg/s^2. Nothing to govern.

## Changes
1. **Yaw-reach (pace) governor** (`RollPaceGovernor`, new, default true; acts from `RollGovernorReplaces` 2). Two parts, both from the effector tables and the measured state:
   - *Open loop (level 3):* the roll trajectory's acceleration or braking may not ask the yaw for more than `PaceYawUse` (0.85) of what the rudders can still add toward the side the move needs, from where they are (roll acceleration x sin AoA is yaw acceleration). Where the rudders have it the tuned acceleration and braking stand; where they do not the move runs at the yaw's authority; past the rudder fade the faded tables pace the roll by themselves (that is what cleans the 40-48 deg AoA rolling pulls on the bench). Judged at the AoA 0.15 s ahead (`PaceHorizon`), so a push + roll sees the demand reverse before the AoA crosses zero.
   - *Closed loop (levels 2-3):* as the rudders run out of travel (50-75 % either side, `PaceLoad0/1`) or the allocator reports unmet yaw (`yawSat`), the pair's body-yaw demand (pS sin AoA at the horizon's AoA, plus the sideslip correction's cos AoA share) may not lead the measured yaw rate by more than the yaw can close within the horizon: its acceleration now times the horizon, plus the rudders' slew-limited gain from their present deflection, plus `PaceFloor` 4 deg/s. The body-roll demand is bounded the same way against the roll axis (its acceleration plus `PaceRollUse` 1.0 of its authority). The roll-rate target is held inside the window; the trajectory runs toward it at its own acceleration. It never asks for a roll against the one in progress: when the yaw the jet has only fits a roll the other way (the AoA swept through zero under a roll), the target is zero.
   - Replaces the trajectory's yaw caps (`RollEntryCoord`, `BrakeYawCap`, `RevYawCap`, `CoordJerk`) at level 3 and the push + roll limiters (yaw anchor, sideslip-rate lead, sweep limit) at level 2. **`CoordinatedStop` stays at every level** (it is the released-stick pacer at high AoA; the 214 s case).
   - Dead ends on the way, for the record: an ungated window clamped on the trajectory itself flipped the roll when the AoA crossed zero (window on the other side of zero), and throttled the yaw loop's own drive at 700 km/h (a reversal the rudders handled at 40 % travel ran 0.2 s slower for nothing - the yaw loop's bandwidth is 2.5 1/s, it needs the error). A window gated on rudder saturation alone was too late (the trajectory had dropped 100 deg/s before the rudders pinned). Open loop from the tables plus the gated window is what works.
2. **Roll first, then pitch - both ways** (`RollGovernorPitch`, rebuilt). The AoA rate under a held roll is limited by what the yaw can pay for (pS cos AoA x AoA rate). Nose-up at positive AoA: 0.8 x the rudders' braking yaw *not yet in use* on the roll's side (so the pull yields as the rudders fill, not 0.3 s after they pin), faded in with the rudders' braking yaw (40-55 deg/s^2) as before, and **only as the sideslip heads for the margin** (from half of it; measured sideslip plus 0.3 s of its kinematic rate) - a rolling pull that pins the rudders but holds its sideslip keeps its pitch rate (bench, full aft + roll at 500 km/h: 3.4 of a 4.9 margin; the cap without the margin test cost 10 deg of AoA at 2 s). Every other sweep (nose-down anywhere, nose-up from negative AoA): the 2.1.8 sweep yaw limit's capacity (0.5 x yaw authority + half the roll's) from the same rule, at every IAS and every level >= 1. The governor block no longer has the |sin AoA| > 0.05 gate (the cap let go for 0.15 s through zero AoA and the push stepped from -24 to -60 deg/s).
3. Telemetry: `paceLead` (yaw lead the window allows, deg/s), `paceHold` (deg/s taken off the roll target by the window). FCS visualizer / readout bind by name.

## Bench (calibrated plant, 31 Oct calibration)
**Stop / reversal sweep, 32 cases, 450-850 km/h, 5-23 deg AoA (full roll 0.9 s from a held pull, then release or reverse):**

| | Stop worst / mean | Reversal worst / mean | Mean roll | Stop time | Reversal time (+50 % to -50 %) |
|---|---|---|---|---|---|
| 2.5.2 L1 | 3.4 / 2.1 | 4.9 / 2.9 | 107 | 0.70 | 0.43 |
| 2.5.2 L3 | 7.2 / 2.9 | 10.8 / 5.3 | 118 | 0.49 | 0.29 |
| **2.5.3 L3** | **3.2 / 2.1** | **3.9 / 2.6** | 108 | 0.69 | 0.44 |
| 2.5.3 L1 | 3.4 / 2.2 | 4.9 / 2.8 | 107 | 0.70 | 0.43 |

By AoA band (mean sideslip / reversal time, L1 2.5.2 -> L3 2.5.3): 3-10 deg 3.0/0.30 -> 2.6/0.32 with a faster roll (171 -> 175); 10-17 deg 3.2/0.50 -> 2.8/0.65; 17-24 deg 2.3/0.52 -> 2.4/slower (one case did not complete the reversal in 1.3 s; roll 29 -> 25). **The reversals at 10-24 deg AoA are 0.1-0.3 s slower than level 1's** - that is the yaw-paced reversal from pinned rudders, the trade.

**Scenarios** (peak sideslip; L1 = 2.5.2 level 1, L3 = 2.5.2 level 3, new = 2.5.3 level 3):

| Case | L1 | L3 | new |
|---|---|---|---|
| Full aft, roll 0.3 s in, 250 / 320 / 400 km/h | 4.5 / 3.2 / 4.7 | 9.9 / 11.1 / 8.8 | 3.5 / 2.9 / **7.8** |
| Full aft + full roll 250 | 3.9 | 6.5 | 2.4 |
| ... + reversal at 3 s, 320 / 500 | 6.7 / 3.8 | 6.1 / 3.8 | 3.2 / 2.9 |
| Roll, then full pull, reversal 400 | 3.5 | 5.7 | 3.3 |
| Held 0.4 pull + full roll 350 | 3.4 | 6.9 | 0.9 |
| 65 deg AoA roll (+ pedal) then release, 300, full throttle | 3.4 / 3.5 | - | 3.5 / 3.5 |
| Full push + full roll 200 / 300 | 7.9 / 15.5 | 32.6 / **74.7** | 3.8 / 9.9 |
| 1 g roll reversal 500 | 1.9 | 1.9 | 1.9 |

The one case worse than level 1 is full aft + roll from 1 g at 400 km/h (7.8 vs 4.7): at 44 deg AoA the window held the roll to ~10 deg/s while the yaw never built (rudders pinned but faded). Level 1 at 2.5.3 is 2.5.2's level 1 except the pitch cap (more AoA kept in rolling pulls: roll-then-pull-reversal 400 reaches 34 deg at 2 s against 20.5; push + roll 200 7.9 -> 3.0).

**Log replays** (01:40 log, the pilot's sticks; bench peak sideslip, game in brackets; the bench under-reads these by ~1.5x): 195.4 s reversal 6.9 -> 6.0 (10.5); 198.8 s 16.6 -> 5.4 (14.2); 177 s 5.7 -> 5.4 (7.5); 207 s 7.2 -> 6.4 (6.7); 175 s stop 3.9 -> 1.9 (5.5); 214 s released-stick 13.8 -> 5.7 (15.5); 220 s 7.3 -> 4.6 (6.6); 80 s rolling pull 4.0 -> 3.8 (5.9); 105 s 8.6 -> 3.9 (7.6). In 177 / 207 s the roll was also held (88 -> 67, 72 -> 52 deg/s peak) - the governor refusing a reversal the yaw could not follow.

## Not verified
- Not flown. The reversal feel at 10-30 deg AoA with the rudders pinned is slower by design; whether 0.85 / 0.15 s / 50-75 % is the right place for the trade is yours.
- The window's constants (`PaceHorizon` 0.15 s, `PaceFloor` 4, `PaceLoad0/1` 0.5/0.75, `PaceYawUse` 0.85) were chosen on the bench sweep (4 / 6 / 8 deg/s floors and 0.7-1.0 shares tried); not fitted to the game.
- The bench's yaw at 30-45 deg AoA with large sideslip is still weaker than the game's.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.5.3. Backup: 2.5.2 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.5.2.bak`. Release: `C:\Games\NO mods\F22E-FCS-2.5.3-release` (source zip carries the calibrated bench and the sweep / regression / log scripts in `bench/an/`).
- Dev DLL unchanged. New key `RollPaceGovernor` appears at its default; no migration. Your Dev.cfg holds `RollGovernorReplaces` 3 and margins 4/5/5 from the 01:40 flight; both stay as you left them. LogOutput: `F-22E FCS 2.5.3`.
- To A/B in flight: `RollPaceGovernor` false (2.5.2's level 3), `RollGovernorPitch` false (no AoA-rate caps), `RollGovernorReplaces` 1 (yaw caps back on).
