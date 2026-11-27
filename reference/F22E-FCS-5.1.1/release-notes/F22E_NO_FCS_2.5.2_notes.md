# F-22E FCS 2.5.2

Base: **2.5.1**. The governor's saturation side is rebuilt around one rule — the yaw axis is the shared resource of the lateral pair; roll is traded first, pitch second — with inputs that port to any craft (effector tables, measured rudder usage, kinematics). Bench on the **calibrated** plant (31 Oct calibration), not flown.

## Why (31 Oct 01:11 log, 143.2-145 s: 21 deg)
Full push to -16 deg, then full pull + full left stick, stick reversed at 143.95 with the pull held; AoA -4 → 64 in 1.5 s. The calibrated bench replays the entry and the reversal to within 1 deg of AoA and a few deg/s on every rate (α RMS 1.3 deg over the window); it under-reads the peak (12 vs 21) because the game's yaw holds at -40 deg/s for 0.3 s longer at 30-45 deg AoA with 5-15 deg of sideslip — a corner the calibration data does not cover.

What the replay settles: from the state at the reversal (yaw -42 deg/s, rudders pinned the old way, 165 m/s) the stop is committed — cos α × (r × rudder slew time + r² / 2 aB) ≈ 20 deg — whatever any governor does from there. The only time to act was the 0.2 s of entry before the pin, and 2.5.0/2.5.1 deliberately did not act there (0.7 s load filter, no action while the roll was building).

I also tried replacing the fitted stop model with that "physical" formula outright and it fails the logs: the benign 124 deg/s stop at 29 deg AoA (14:35 log, rudders at 22 deg) would predict 43 deg where the game gave 1.1, because with the rudders off their stop the airframe's damping and the wing's split yaw brake the yaw at once. So the data's pinned / not-pinned distinction stays; what became physical is the input.

## Changes
1. **Yaw-reserve governor** (`RollStopYawHold`, rebuilt; replaces the 2.5.0 filtered pinned-rudder term and the 2.5.0 entry hold). While the rudders are used past **80 %** of their yaw (0.08 s filter — pinned is pinned, no entry exemption), the roll-rate reference may not exceed the roll rate in progress; loaded for the roll's own yaw it is trimmed down from there at the coordinating rate (0.35 × rudder braking yaw / sin AoA, 20-100 deg/s per s); it lets go below **65 %** and recovers at half the rate it fell, so the pin / unpin cycle damps. Never a step below the rate the jet has (2.4.18's gate did that). The usage is read **either side**: after a reversal the rudders are still pinned the old way and the new roll waits for them to cross — that was invisible to 2.5.0/2.5.1, whose signed load read 0 the moment the stick flipped.
2. **Roll first, then pitch** (`RollGovernorPitch`, new, default true; only with `RollGovernorReplaces` ≥ 1). With the roll already held to the reserve cap and the rudders past 60 % and climbing, the nose-up AoA-rise rate the pitch law may command is held to what the rudders' braking yaw can coordinate at this roll: 0.3 × aB / (roll rate × cos AoA), never under 6 deg/s, fading in from 60 to 80 % usage so the pull slows *before* the yaw pins, released over 0.3 s. This is 2.2.4 / 2.2.7's job from the same yaw-capacity rule as the roll, after the roll has been traded. A first version that engaged only once pinned and let go at the reversal made the replay worse (12 → 20: a pitch surge at the reversal cost the stabs' yaw); the early fade-in and the either-side usage fixed that.
3. **Gate by rudder authority, not IAS.** The pinned-rudder term and the reserve governor fade in with the rudders' braking yaw (40-55 deg/s² from the tables, ≈ 110-130 m/s on this jet at 24 deg AoA) instead of an IAS band, so a craft with weaker or stronger rudders gates itself.
4. The margin cap (4 / 6 / 8 deg schedule, τ 0.054 s, gain 1.3) is unchanged; the fitted pinned term (0.22 s) now only drives the `stopSlipPred` telemetry.
5. Telemetry: `stopSlipHold` = reserve governor weight (0-1), new `yawSatPitch` = pitch-rate command taken off by the pitch cap (deg/s).

## Calibrated bench
**The 143.9 s replay** (peak sideslip, bench): 2.4.16-equivalent n/a; L0 8.7 (AoA parked at 16-35); 2.5.1 12.2; **2.5.2 11.9**, hold off 12.2, pitch cap off 12.1. As above: committed before the reversal; the reserve trim had 0.2 s. What would prevent it is a slower pull-roll entry (lower `RollStopSideslip` at low AoA, or a tighter trade via `PullSideslipMargin`) — your tuning, not a rule.

**Stop / reversal sweep, 144 cases, 3-45 deg AoA** (calibrated plant):

| | Stop worst / mean | Reversal worst / mean | Mean roll | Bank at 1 s |
|---|---|---|---|---|
| 2.4.16-equivalent | 15.3 / 3.8 | 20.7 / 5.9 | 132 | 113 |
| **2.5.2** | **6.7 / 2.3** | **8.4 / 3.5** | 103 | 102 |
| 2.5.2, `RollStopYawHold` off | 6.7 / 2.6 | 11.8 / 4.4 | 109 | 105 |

By AoA band, mean sideslip / mean roll: 3-10 deg 2.0/163 → 1.7/146; 10-17 4.4/175 → 3.2/128; 17-24 7.3/124 → 3.0/96; 24-31 7.9/86 → 2.2/52; 31-36 4.0/78 → 3.8/57.

**Held full-stick roll, 565 km/h, 19-21 deg AoA:** settles at 112 deg/s with the rudders at ~50 % (the game's steady load at that yaw rate is ~45 %); the reserve governor touched it for 0.1 s in the entry (usage peaked 85 %). No cycling.

**Rolling pulls** (peak sideslip / AoA at 2 s; L1 = default, L1np = pitch cap off, L0 = 2.5.0's limiters, v16 = 2.4.16-equivalent):

| Case | km/h | L1 | L1np | L0 | v16 |
|---|---|---|---|---|---|
| Pull, then full roll | 250 | 9.6 / 62 | 9.6 / 62 | 7.3 / 37 | 7.4 / 38 |
| | 320 | 7.7 / 58 | 12.8 / 63 | 4.8 / 36 | 5.6 / 34 |
| | 400 | 6.1 / 57 | 8.2 / 62 | 4.1 / 37 | 4.2 / 37 |
| Full aft + full roll | 250 | 3.6 / 23 | 5.9 / 27 | 2.5 / 13 | 2.4 / 14 |
| ... + reversal at 3 s | 320 | 6.9 | 9.0 | 2.9 | 3.1 |
| | 500 | 4.5 | 5.0 | 4.0 | 16.5 |
| Roll, then full pull, reversal | 400 | 4.9 | 9.6 | 3.8 | 6.3 |
| Held 0.4 pull + full roll | 350 | 1.6 / 102 deg/s | same | same | 3.9 / 154 |
| Stop / reversal, 565 km/h, 23 deg | | 2.9 / 3.5 | same | same | 7.8 / 13.8 |

The pitch cap is what brings the rolling pulls from the L1np column to the L1 column while the AoA still goes through (57-62 at 2 s vs L0's 36-37). Push + roll, push reversals, plain pulls, 1 g rolls: unchanged.

## Not verified
- Not flown. The bench's yaw at 30-45 deg AoA with large sideslip is weaker than the game's (the 143.9 s replay reads 12 for the game's 21).
- The pitch cap's constants (0.3 × aB, 60-80 % fade) are chosen, not fitted; they moved the bench the right way in every rolling-pull case and are the first thing to look at in the logs (`yawSatPitch` column).

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.5.2. Backup: 2.5.1 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.5.1.bak`. Release: `C:\Games\NO mods\F22E-FCS-2.5.2-release` (source zip carries the calibrated bench).
- Dev DLL unchanged. New key `RollGovernorPitch` appears at its default; no migration. LogOutput: `F-22E FCS 2.5.2`.
- To A/B: `RollGovernorPitch` false (pitch never touched), `RollStopYawHold` false (margin cap only), `RollGovernorReplaces` 0 (2.5.0's limiters back).
