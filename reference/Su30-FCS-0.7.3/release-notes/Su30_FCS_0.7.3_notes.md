# Su-30VXN FCS 0.7.3: the AoA law's flight-path rate without the filter leak

Base: 0.7.2. **Status:** compiled against the game's assemblies, benched (your config). **Not flown.**

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
**`FlightPathRateMatched`** ("2. Envelope", dev, **on**): the pitch rate takes the same 50 ms filter as the AoA rate before the subtraction. false = 0.7.2 exactly (bench: identical with the key off).

## Bench (0.7.2 -> 0.7.3, the new key off vs on)
**Regression set (20 cases):**
- Mean peak sideslip 8.73 -> 7.83 deg, mean roll 48.3 -> 49.5 deg/s.
- Sideslip better by 0.5 deg or more: pull-then-roll 250 8.6 -> 8.1; pull-then-roll 400 13.7 -> 12.7; fullaft+roll 250 10.4 -> 8.2; fullaft+roll+rev3s 320 14.0 -> 10.0; fullaft+roll+rev3s 500 10.8 -> 9.3; push+roll 300 6.2 -> 5.7; pull-then-pushover+roll 450 13.8 -> 9.0; push-then-pullthrough+roll 450 14.5 -> 10.4; pullthrough, roll released at zero 350 6.2 -> 4.9.
- Sideslip worse by 0.5 deg or more: roll-then-pull-rev 400 7.6 -> 9.9; 65AoA roll release 300 5.5 -> 6.0.

**Stop / reversal sweep:**
- 0.7.2: stop: betaEv worst 12.9 mean 5.78 | entry beta mean 4.99 | mean roll 102 | tRev mean 0.59
- 0.7.2: reversal: betaEv worst 14.9 mean 8.05 | entry beta mean 4.99 | mean roll 102 | tRev mean 0.40
- 0.7.3: stop: betaEv worst 12.9 mean 6.02 | entry beta mean 4.94 | mean roll 102 | tRev mean 0.59
- 0.7.3: reversal: betaEv worst 14.5 mean 8.09 | entry beta mean 4.94 | mean roll 102 | tRev mean 0.40

## Install
`BepInEx\plugins\<plugin>\`: the DLL 0.7.3; 0.7.2 kept as `<dll>.0.7.2.bak`. `meta.json` updated. No setting of yours changes.
