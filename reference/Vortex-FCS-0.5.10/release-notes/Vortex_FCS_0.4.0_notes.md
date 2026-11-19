# FS-20 Vortex FCS 0.4.0 (17 Nov 2026)

On 0.3.15. The build from `Vortex_STOVL_authority_TRC_plan.md` after phase 0 (`Vortex_STOVL_authority_phase0_results.md`).

The work started on 0.3.11. 0.3.12 to 0.3.15 came out of another chat while it ran, and it was rebased on each. Everything in them is carried unchanged: wing trailing edges ±40° and rudders ±35° (0.3.12), the pair pass migrated on and the pitch nozzle at 15° (0.3.13), the F-22E's lateral governor margins (0.3.14), `AoALimitPositive` 65 with `VortexConfigRevision` 11 (0.3.15). 0.4.0 adds no migration and no revision of its own.

Every table below was run on the 0.3.15 base. The numbers for the rejected variants in 2.2 to 2.4, and the two lines marked so, are from the 0.3.12 base and were not run again.

Tom, 17 Nov 17:25, on the six decisions: "1. Drop get rid of the group. 2/ i thought it would be a non issue, since the fcs would work even with the thrust difference? 3/ one for both axis. 4/ extend the probe to bellow 30m/s if it is beneficial when port this to other stovl capable jets. 5/build 15:1 6/ do it".

**Benched, not flown.** STOVL only: two CTOL cases (pull + roll at 400 and 700 km/h) are identical to 0.3.15 on every flight column (only the code timers differ). With every new switch off, the build is identical to 0.3.15: the five-case set on the reference jet and the 12 cases of the STOVL regression set, every column, largest difference 0.

## 1. What is in

| # | Change | Dev key (9. STOVL) | Off = 0.3.15 |
|---|---|---|---|
| 1 | Full side stick asks for the speed the hover can stop from in a fixed time | `StovlTrcStopTime` 1.06 s | 0 |
| 2 | The translation tilt slows down when its axis is short of authority | `StovlTiltStopTime` 0.42 s | 0 |
| 3 | Three fades run on a speed ratio instead of fixed speeds | `StovlPhysicsBlends` true | false |
| 4 | Thrust-fit weights follow the spare lift (15 : 1 on the stock jet) | `StovlFitWeights` true | false |
| 5 | Commanded climb / sink gain uses the measured thrust lead | `StovlClimbGainFromLead` true | false |
| 6 | Position hold takes its spot at 0.5 m/s, not 3 (not asked for; see 2.6) | `StovlPosCatchSpeed` 0.5 | 3 |

New telemetry columns after `stYawLim`: `stTiltRateA`, `stTiltRate`, `stTiltRateU`, `stAStop`, `stTrcVmax`, `stLamL`, `stLamB`, `stWz`.

**Not in:** the loop gains (group A, dropped as decided), the two airbrake items, the short take-off wheel-load ramp (unchanged).

## 2. Each change

### 2.1 Side speed (decision 2)

Full lateral stick = the hover's stopping acceleration × `StovlTrcStopTime`. The stopping acceleration is what the full-dry lift leaves sideways once the weight is carried, low-passed 1 s, and no more than `StovlBankMax` gives.

On the question in decision 2: yes, the law works the same on either thrust. The choice only fixes one number, which jet gets exactly 15 kt. The config is on `PerformanceProfile = Buffed`, so the stop time is set there:

| Jet (bench) | Spare lift | Full-stick speed, 0.3.15 | 0.4.0 |
|---|---|---|---|
| Buffed, 11 880 kg (reference) | 2.75 m/s² | 14.9 kt | 14.9 kt |
| Buffed, 20 % fuel, 9 640 kg | 6.12 | 14.9 | 19.5 (the bank-limit cap is 20.2) |
| Buffed, 13 640 kg | 1.45 | 15.0 | 10.5 |
| Buffed, 500 kg on one wing, 12 380 kg | 2.25 | 14.6 | 12.8 |
| Stock, 11 880 kg | 1.09 | 14.9 | 8.9 |
| Stock, 12 800 kg | 0.47 | 14.8 | 5.5 |

### 2.2 Tilt rate (decision 3): not what was picked

Picked: one stop time for both tilt axes, which makes the fore-aft tilt about 45 % faster. **That is not what ships, because it cost height on the bench.**

- **Authority alone, one stop time:** on the Buffed jet the last part of a deceleration pitched at 13-16 deg/s instead of 10 and lost 0.24 m of height instead of 0.08, with 2.9 s outside 0.2 m/s instead of 1.7. The authority is larger at speed (the surfaces add to it), so the tilt got faster exactly where the thrust was busiest.
- **With a thrust term added** (two forms of the reserve): still 0.19-0.24 m. The lift-loss model says small tilts are cheap, and the bench says they are not.
- **Shipped:** `StovlTrcBankRate` (10 deg/s) stays as the ceiling on both axes, as since 0.1.6. Under it, each axis may move no faster than its authority × `StovlTiltStopTime`. So the rule only ever slows the tilt.

Result: both axes at the ceiling on a clean jet (bank 9.1-9.5 deg/s, pitch tilt 10); 8.3 deg/s of bank at 20 % fuel; 3.5 deg/s with 500 kg on one wing (was 4.9). The authority is low-passed 3 s, so for the first seconds after a take-off the bank rate reads about 5 % low.

To try the faster fore-aft tilt in game anyway: raise `StovlTrcBankRate`.

### 2.3 Fades (decision 4)

All three now run on airspeed ÷ the speed at which the wing alone carries the jet at `StovlWingAoAMax` (70.5 m/s on the reference). On the reference the curves are identical to 0.3.15; a heavier jet or thinner air moves them up with √(mass / density).

- Pitch attitude's hand-over to the wing: 15-45 m/s on the reference.
- Velocity-vector roll and the sideslip loop: 10-25 m/s on the reference.

**The probe below 30 m/s: tried, not shipped.** I worked the sideslip stiffness out below 30 m/s with the probe's own model and it agrees with the probe above 30 m/s (0.129 against 0.117 at 30 m/s, 0.437 against 0.428 at 45). But between 10 and 25 m/s it is small and not monotonic: √share 0.16 / 0.31 / 0.23 / 0.30 at 12 / 15 / 20 / 25 m/s, then 0.61 at 30, as the rudder flare fades out and the AoA moves. A fade on it would come and go during a deceleration. So it is not a benefit here or on a port, and the code is not in. The speed ratio ports by itself: it only needs the jet's mass and wing lift slope.

### 2.4 Thrust-fit weights (decision 5)

Fore-aft weight = (spare lift ÷ the hover's stopping acceleration)², vertical weight 1, never less vertical priority than 0.3.15's 4 : 1. On the wheels and in the lever's accelerating regime it stays 4 : 1.

| Jet | 0.3.15 | 0.4.0 |
|---|---|---|
| Stock, 11 880 kg | 4 : 1 | 15 : 1 |
| Stock, 12 800 kg | 4 : 1 | 29 : 1 |
| Buffed, 13 640 kg | 4 : 1 | 12 : 1 |
| Buffed, 500 kg one side | 4 : 1 | 8 : 1 |
| Buffed, 11 880 kg | 4 : 1 | 7 : 1 |
| Buffed, 20 % fuel | 4 : 1 | 4 : 1 |

Two earlier forms were wrong the same way: they let the fore-aft weight rise to 1 : 1 just when the vertical axis was short (thrust swinging 0.35 ↔ 0.88 at the end of a deceleration). The shipped form moves with neither the tilt nor the vertical demand; its largest one-tick step on the bench is 0.001.

### 2.5 Climb / sink gain (decision 6)

Time to settle under 0.1 m/s after releasing a full-stick climb, no reversal in any case:

| Jet | 0.3.15 | 0.4.0 |
|---|---|---|
| Buffed, 11 880 kg | 2.88 s | 2.57 s |
| Stock, 11 880 kg | 3.25 | 2.03 |
| Buffed, 20 % fuel | 2.68 | 2.32 |
| Stock, 12 800 kg | 3.70 | 2.20 |
| Buffed, 13 640 kg | 3.18 | 2.23 |
| Buffed, 500 kg one side | 3.02 | 2.22 |

Sink release: 2.23 → 1.98 s Buffed reference, 3.10 → 1.75 s at 20 % fuel; 3.07 → 3.40 s at 13 640 kg. The rebound after a sink stop on the Buffed reference is 0.08 m/s (was 0.04). Vertical landings: touchdown sink 5.0-5.8 m/s as before, largest gear compression 0.418 m (was 0.419; a leg breaks at 0.5).

### 2.6 Position hold (not asked for)

0.3.15 takes its hold spot as soon as the speed is under 3 m/s. After a short side step the jet is still banked into the move at that moment, the estimated rest point comes out short, and the jet stops past it and slides back. Bench, 0.4 stick for 1.5 s, 0.3.15: 0.7 m back on the Buffed reference, 5.2 m at up to 1.97 m/s on the Stock jet, 4.6 m at 12.8 t.

With the side speed now scaling, that showed up on full stick too (12.8 t: 1.5 m back), so it is fixed here: the hold takes a spot under 0.5 m/s. Once it has one it keeps it up to 3 m/s as before.

| Jet | Tap: slide back, 0.3.15 → 0.4.0 | Tap: settled under 0.15 m/s |
|---|---|---|
| Buffed, 11 880 kg | 0.69 → 0.00 m | 5.9 → 4.8 s |
| Stock, 11 880 kg | 5.19 → 0.00 | 8.4 → 2.8 |
| Buffed, 20 % fuel | 0.72 → 0.00 | 5.9 → 5.5 |
| Stock, 12 800 kg | 4.55 → 0.00 | 8.4 → 2.0 |
| Buffed, 13 640 kg | 1.06 → 0.00 | 6.4 → 3.0 |
| Buffed, 500 kg one side | 2.63 → 1.84 | 8.4 → 8.4 |

Fore-aft stop from 13.8 kt on the Buffed reference: back-run 0.18 → 0.08 m/s, 0.77 → 0.31 m, settled 8.3 → 5.0 s.

## 3. Worse than 0.3.15 (bench)

- **Full-stick side stop, Buffed reference:** the speed runs 0.24 m/s past zero (was 0.15) and takes 5.1 s to stay under 0.15 m/s (was 4.0). Time to 0.3 m/s is the same (3.85 against 3.83 s) and there is no slide back. It comes from 2.6: the early hold was masking the velocity loop's own overshoot. Stock: 0.17 (was 0.04). 20 % fuel: 0.29 (was 0.23), and the stop from 19.5 kt takes 5.1 s (was 3.8 from 14.9).
- **A gust of 3 m/s or more:** the jet now holds where it stops, 5.0 m from the spot on the Buffed reference, instead of sliding 2 m back toward it (2.7 m off). Five of the six jets do this (5.0-8.4 m off, were 2.7-5.0); the 13.6 t jet stays under the 3 m/s limit and comes back to the spot (was 3.1 m off). Under 3 m/s nothing changes: a 2.5 m/s gust comes back to the spot in both (0.01 m; run on the 0.3.12 base).
- **Side and fore-aft together on a jet with little spare lift:** height −0.37 m at 12.8 t Stock (was −0.08), −0.21 m on the Stock reference (was −0.11), −0.15 m at 13.6 t Buffed (was −0.08). The Buffed reference is unchanged (−0.08). The side command is smaller now, so the fore-aft move gets more of the shared budget, and the fore-aft move alone already loses 0.45-0.54 m at 12.8 t in both builds.
- **One-sided load (500 kg):** the bank moves at 3.5 deg/s (was 4.9), so the full-stick stop takes 7.65 s (was 6.68). The slide back is smaller (0.44 against 0.66 m).
- **Short take-off:** lift-off 0.01-0.04 s later, ground run 0.3-0.7 m shorter; 81 kt and 83 m at 20 s on the Buffed reference (was 80 kt, 84 m).

- **30 kt bank at 20 % fuel:** the same stick gives 19.0° of bank (was 21.7°). The other five jets are within 0.2°.

Unchanged within the bench's resolution: pedal turns (20.9 deg/s, 61.9° in the 3 s case, 0.25° overshoot), the 30 kt pedal case, the deceleration and its last stop (height −0.08 m both, pitch 26.0° both), side-stick height (within 0.03 m).

## 4. Seen on the way, same in 0.3.15, not touched

- **20 % fuel, hands-off deceleration after a conversion at 380 km/h:** the jet never slows. It settles into a descent of 1-12 m/s holding 300-390 km/h (still 161 kt ground speed after 60 s). At full fuel the same case loses 50-57 m before it levels (0.3.12 base). Bench only; not checked against a log.
- **Fore-aft step at 12.8 t Stock:** 0.45-0.54 m of height lost, stop in 9 s.
- **Bank above the hover regime (lever at 0.6, 52-62 kt and accelerating, 0.3 stick for 3 s, then released):** every jet loses height in the bank: 4.6-4.8 m at 18° of bank on four jets, 8.0-8.2 m with 500 kg on one wing. **At 20 % fuel the same stick gives 42° of bank, the nose goes from +10° to −6°, and the jet loses 20.7 m and hits the ground at 10.5 m/s** (started from 22 m). Same in both builds to 0.15 m. Bench only.
- **Full-stick sink at 12.8 t Stock:** the case starts from 7 m and reaches the ground in both builds (the stop takes more height than it has).

## 5. Not benched

2 000 m, store loads other than the two above, damage, the game itself. The curve checks (reversals of roll rate, pitch rate, bank command, nozzle, vertical speed) found none in the hover, 30 kt, short take-off and deceleration cases on either build, apart from one vertical-speed reversal after the sink stop on the Buffed reference in both (the 0.04 / 0.08 m/s rebound in 2.5) and the two ground contacts in section 4. The throttle at the end of the Buffed deceleration reverses 6 times in 0.3.15 and 0 times here.

## 6. Bench

`bench/scripts/vortex/b4.sh` (the case set), `matrix.sh` (six jets, old against new), `b4an.py`, `final.py`, `dend.py` (the tables above). Old = this build with every switch off.
