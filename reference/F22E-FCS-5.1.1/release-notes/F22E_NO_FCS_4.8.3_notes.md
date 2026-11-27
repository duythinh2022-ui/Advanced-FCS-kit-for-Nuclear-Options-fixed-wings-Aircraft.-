# F-22E Strike Raptor FCS 4.8.3 (26 Nov 2026)

Asks: Tom 25 Nov 23:38 / 23:40 / 23:54 / 23:55 / 23:58. Bench only; nothing in this build has been flown.

## What changed

### 1. No-TVC AoA cap is the limit at every thrust (law)
- Symptom: `NoTvcAoACapPositive` 35 sometimes allowed 45, sometimes 35. Above 45 it always complied.
- Cause: with the nozzles off the cap was raised toward `NoTvcThrustAoACapPositive` (45) as thrust/weight went 0.8 -> 1.0. 23:10 log: authority cap 35 <-> 45 following the throttle.
- Fix: `NoTvcCapIsHardLimit` (2. Envelope, default true): the cap is `NoTvcAoACapPositive`, whatever the thrust. false = 4.8.2.
- Not changed: the negative side with DisableTVC still uses the thrust cap (45).

### 2. Pitch reversals (law)
- 23:10 / 23:26 logs: after a full-stick reversal the pitch rate crossed zero 0.45-1.4 s after the stick and reached half the new demand at 1.35-2.5 s. The jet followed the planned rate within a few deg/s; the plan turned at 0.7 x the measured nose-down acceleration (43 of 62 deg/s^2) and was held near the jet's own rate.
- Fix, one rule for every command (no reversal latch; a first version with one was removed on Tom's 23:55 note):
  - `PitchPlanAccelShare` (4. Gains) 0.7 -> 0.95.
  - `PlanFollowsJetOnlyWhenBraking` (4. Gains, true): the 4.5.3 plan-hold applies only while a rate is braked to a smaller one on the same side (a stop), not through a reversal. Removing the hold altogether was benched and was worse (two-zone, no TVC: reversal roll at 30 deg 60.8 vs 65.0 deg/s; coast after release at 50 deg 27.9 vs 23.3 deg).
- Bench (two-zone, full aft -> full forward -> full aft at the 55 deg limit): time to 20 deg/s the new way, nozzles on 0.55 s (0.62), nozzles off 1.03-1.35 s (1.12-1.40). The nozzles-off bench case is limited by stab authority (peak 31-45 deg/s^2), so it cannot show what the log showed; the game gain is an estimate.
- AoA-limit overshoot in the batteries: unchanged or smaller (X2e 320 km/h idle roll at 45 deg 0.3 deg both builds).

### 3. Two-zone stabs stall at 25 deg body AoA, whole-stab average (airframe)
- New two-zone settings (5. Airframe): `TwoZoneStabStallAoA` 33, `TwoZoneStabCLScale` 1.09, `TwoZoneStabUpwash` 0.24 (was fixed 0.2). `StabilatorStallAoA` / `StabilatorCLScale` are X2e's now and are not used with TwoZoneAirframe on.
- Whole-stab first lift peak (half inboard zone in 0.6 downwash, half outboard zone in upwash, neutral stab): 25.0 deg body AoA (4.8.2 at 36 / 0.2: 28.0).
- Other ways to 25 that were benched: 36 + upwash 0.35 + CL 1.06 = more push but a 9.8 deg sideslip after the 30 deg reversal with nozzles on.

### 4. Two-zone: unstable at low AoA, neutral 20-28 deg, unchanged past 28 (airframe)
- `TwoZoneLowAoAUnstable` (5. Airframe, default true). A pitching couple on the fore / aft body (fore lift coefficient down, aft up, no net lift), largest at zero AoA, zero from 28 deg. New code: `BodyLift.Offset`, tables `TwoZoneForeOffset` / `TwoZoneAftOffset` in Plugin.cs, fitted with tools `sm.py` / `fitoff.py`.
- Static, stabs neutral, log-calibrated bench (neutral point aft of CG, m; + = stable):

| AoA | 4.8.2 | 4.8.3 |
|---|---|---|
| 2 | +0.15 | -0.08 |
| 6 | +0.06 | -0.10 |
| 10 | +0.07 | -0.11 |
| 14 | +0.08 | -0.11 |
| 18 | +0.15 | -0.09 |
| 20 | +0.22 | -0.04 |
| 22 | +0.28 | -0.01 |
| 24 | +0.36 | 0.00 |
| 26 | +0.34 | +0.16 |
| 28 | +0.30 | -0.21 |
| 30 | +0.35 | -0.13 |
| 32 | -0.06 | -0.17 |
| 34 | -0.30 | -0.17 |
| 36 | -0.64 | -0.22 |
| 38 | -1.26 | -0.52 |

  The 28-38 column differs from 4.8.2 because of change 3 (the stab stalls 3 deg earlier), not because of the couple: with the couple off, 4.8.3 reads the same from 28 deg up.
- The cost, which cannot be avoided: with the moment past 28 deg unchanged, an airframe that is unstable below has to sit nose-down at low AoA. Here -42 deg/s^2 at zero AoA and 400 km/h (stabs make +-108 there), -36 at 10 deg, -30 at 20 deg. Level-flight stab trim goes from +3...+6 to +9.5...+11 deg trailing-edge up.
- Bench against the same build with the couple off:
  - Full-aft pull from level flight: pitch rate after 0.3 s 12.8 vs 14.0 deg/s (330 km/h), 21.5 vs 25.0 (450); time to 20 / 30 / 45 deg AoA within 0.02 s; peak rate +1 to +3 %.
  - Pull back from a push-over at 450 km/h, no TVC: rate after 0.5 s 22.3 vs 31.2 deg/s, time to zero AoA 0.77 vs 0.75 s.
  - Rolls, pushes, reversals, everything past 28 deg: within run-to-run scatter.
- Negative AoA: the couple is held to -10 deg and fades out by -30; 0 to -10 keeps its old margin (+0.4 m), -12 to -28 is more stable (+0.8 to +1.1 m).
- Not measured: trim drag / top speed with the extra stab trim.

## Bench, 4.8.3 against 4.8.2 (two-zone, 450 km/h, limits 30/36/45/50 deg)

| | 4.8.2 no TVC | 4.8.3 no TVC | 4.8.2 TVC | 4.8.3 TVC |
|---|---|---|---|---|
| Reversal roll, deg/s | 64.9/55.3/54.7/51.5 | 65.0/57.4/56.4/51.9 | 78.2/69.3/57.0/51.5 | 77.9/72.3/56.7/51.2 |
| Limit overshoot, deg | 0.6/0.5/0.4/0.7 | 0.6/0.7/0.5/0.4 | 1.0/0.9/0.8/0.8 | 0.9/0.8/0.9/1.0 |
| Push from 36/45, deg/s in 1 s | -24.8/-20.7 | -32.7/-26.8 | -46.6/-40.8 | -45.7/-42.0 |
| Stab trim, deg TE-up | +9.2/+11.1/+16.0/+20.1 | +10.7/+12.9/+14.7/+21.9 | +7.9/+10.9/+15.2/+19.8 | +10.1/+12.3/+15.5/+20.5 |
| Loaded roll from 36/45, deg/s | 148.2/94.3 | 142.9/105.6 | 150.5/117.3 | 148.8/116.5 |

X2e (TwoZoneAirframe off) 4.8.3 vs 4.8.2: reversal roll 48.2/36.8/30.5/28.1 vs 48.2/37.3/30.6/28.1; push -32.4/-22.4 vs -26.7/-20.0; overshoot equal or smaller.

## Known regression (not fixed)
- Two-zone, 320 km/h idle, rolling at the 36 deg limit, both sticks released together: sideslip peaks 11-12 deg about 2 s after release (4.8.2: 8.4 no TVC, 5.3 TVC). During it the left stab sits at +35 with the nozzles at -20 against it for about 1.2 s. The result is two-valued across small setting changes (6 or 11-12 deg), so it is sensitive rather than tied to one change. Same family as the "stuck pose" of 21:58.

## Not fixed: AoA sag at the 65 deg limit, nozzles off
- 23:26 log, 225-375 s, 7 dips (65 -> 56.7-60.2 deg) with full roll stick.
- Measured nose-up authority at 63-66 deg: 20-32 deg/s^2 (223 at 45-50 deg); nose-down 28-49. Stab local AoA 41-60 deg: past its peak on both zones.
- Dip at 234 s (205 km/h): both stabs on the +35 stop, pitch demand unmet. That one is plain authority.
- Dips at 275 s and 286 s (250-310 km/h): stabs not on a stop, allocator reports no unmet pitch demand, pitch rate still 4-9 deg/s under the command. Not explained yet; needs a bench replay of those seconds.
- The commanded rate there is the limit's approach bound, which allows about 5 deg/s of closure 5 deg below the limit, the same size as the tracking error. That is why the AoA parks low instead of creeping back.

## Files
- Law: `FcsController.cs` (NoTvcCapIsLimit, PitchTrajAccK 0.95, PlanFollowsJetRevOff, StabOutUpwash 0.24).
- Airframe: `BodyLift.cs` (Offset tables), `Plugin.cs` (binds, tables, two-zone stab settings), `Registry.cs` (spawn log line).
- Bench only: `bench/sim/EqErr.cs` (`parts` prints a FORCES line from Plant.Forces).
