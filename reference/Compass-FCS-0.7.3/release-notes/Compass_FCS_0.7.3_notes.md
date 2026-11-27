# T/A-30 Compass FCS 0.7.3: the AoA law's flight-path rate without the filter leak

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
- Mean peak sideslip 4.10 -> 4.21 deg, mean roll 81.3 -> 82.0 deg/s.
- Sideslip better by 0.5 deg or more: none.
- Sideslip worse by 0.5 deg or more: fullaft+roll+rev3s 320 3.0 -> 3.5; fullaft+roll+rev3s 500 3.5 -> 5.4.

**Stop / reversal sweep:**
- 0.7.2: stop: betaEv worst 3.2 mean 2.00 | entry beta mean 2.10 | mean roll 158 | tRev mean 0.47
- 0.7.2: reversal: betaEv worst 4.8 mean 3.10 | entry beta mean 2.10 | mean roll 158 | tRev mean 0.32
- 0.7.3: stop: betaEv worst 3.3 mean 2.01 | entry beta mean 2.11 | mean roll 159 | tRev mean 0.47
- 0.7.3: reversal: betaEv worst 4.8 mean 3.11 | entry beta mean 2.11 | mean roll 159 | tRev mean 0.32

## Install
`BepInEx\plugins\<plugin>\`: the DLL 0.7.3; 0.7.2 kept as `<dll>.0.7.2.bak`. `meta.json` updated. No setting of yours changes.
