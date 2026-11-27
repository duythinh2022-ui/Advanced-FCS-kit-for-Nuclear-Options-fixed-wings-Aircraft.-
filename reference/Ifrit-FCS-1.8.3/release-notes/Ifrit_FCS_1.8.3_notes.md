# KR-67 Ifrit FCS 1.8.3: the AoA law's flight-path rate without the filter leak

Base: 1.8.2. **Status:** compiled against the game's assemblies, benched (your config). **Not flown.**

Tom (24 Nov 17:25): "fix it" / "all of them". This is the shared fix from the J-20B 0.2.7 (J20B_FCS_0.2.7_notes.md).

## What was wrong (every FCS jet)
At the AoA limit the law commands:

> pitch rate = flight-path rate + gain × AoA error

The flight-path rate was taken as the pitch rate minus the AoA rate. The AoA rate goes through a 50 ms filter; the pitch rate did not.
- At 1-2 Hz about half of any pitch-rate wobble was left in the "flight-path rate", slightly ahead of it.
- That share was fed straight back into the pitch command: positive feedback.
- It also added a small accidental lead (pitch acceleration × 0.05 s) to every pitch-up and pitch-down at the limit.

On the J-20B with its right wing shot off (pitch on the canards alone) it ran as a 1.7 Hz pitch limit cycle at the damage AoA cap. With the pitch margin intact jets have it stays small: the F-22E's damaged 17:12 log shows the leak at about 3x the real flight-path-rate wobble, but only 0.3 deg/s.

## Change
**`FlightPathRateMatched`** ("2. Envelope", dev, **on**): the pitch rate takes the same 50 ms filter as the AoA rate before the subtraction. false = 1.8.2 exactly (bench: identical with the key off).

## Bench (1.8.2 -> 1.8.3, the new key off vs on)
**Regression set (20 cases):**
- Mean peak sideslip 4.86 -> 4.91 deg, mean roll 50.0 -> 49.8 deg/s.
- Sideslip better by 0.5 deg or more: fullaft+roll+rev3s 320 4.9 -> 4.4; pullthrough, roll released at zero 350 6.6 -> 3.9.
- Sideslip worse by 0.5 deg or more: pull-then-pushover+roll 350 7.1 -> 7.6; push-then-pullthrough+roll 350 6.2 -> 9.8; push-then-pullthrough+roll 450 5.4 -> 6.4.

**Stop / reversal sweep:**
- 1.8.2: stop: betaEv worst 7.6 mean 4.38 | entry beta mean 4.51 | mean roll 118 | tRev mean 0.60
- 1.8.2: reversal: betaEv worst 8.3 mean 4.96 | entry beta mean 4.51 | mean roll 118 | tRev mean 0.32
- 1.8.3: stop: betaEv worst 7.6 mean 4.43 | entry beta mean 4.54 | mean roll 118 | tRev mean 0.57
- 1.8.3: reversal: betaEv worst 8.3 mean 4.96 | entry beta mean 4.54 | mean roll 118 | tRev mean 0.35

## Install
`BepInEx\plugins\<plugin>\`: the DLL 1.8.3; 1.8.2 kept as `<dll>.1.8.2.bak`. `meta.json` updated. No setting of yours changes.
