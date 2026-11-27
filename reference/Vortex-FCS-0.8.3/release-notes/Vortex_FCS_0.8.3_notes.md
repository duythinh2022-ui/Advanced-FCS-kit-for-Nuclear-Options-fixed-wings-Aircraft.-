# FS-20 Vortex FCS (and Vortex Family FCS) 0.8.3: the AoA law's flight-path rate without the filter leak

Base: 0.8.2. **Status:** compiled against the game's assemblies, benched (your config). **Not flown.**

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
**`FlightPathRateMatched`** ("2. Envelope", dev, **on**): the pitch rate takes the same 50 ms filter as the AoA rate before the subtraction. false = 0.8.2 exactly (bench: identical with the key off).

## Bench (0.8.2 -> 0.8.3, the new key off vs on)
**Regression set (20 cases):**
- Mean peak sideslip 3.52 -> 3.47 deg, mean roll 41.6 -> 41.6 deg/s.
- Sideslip better by 0.5 deg or more: 65AoA roll release 300 2.4 -> 1.8; pullthrough, roll released at zero 350 4.0 -> 2.5.
- Sideslip worse by 0.5 deg or more: push+roll 300 3.3 -> 4.1; push-then-pullthrough+roll 450 4.8 -> 5.3.

**Stop / reversal sweep:**
- 0.8.2: stop: betaEv worst 4.8 mean 3.54 | entry beta mean 3.62 | mean roll 101 | tRev mean 0.64
- 0.8.2: reversal: betaEv worst 7.0 mean 4.50 | entry beta mean 3.62 | mean roll 101 | tRev mean 0.30
- 0.8.3: stop: betaEv worst 5.1 mean 3.62 | entry beta mean 3.71 | mean roll 101 | tRev mean 0.65
- 0.8.3: reversal: betaEv worst 6.8 mean 4.56 | entry beta mean 3.71 | mean roll 101 | tRev mean 0.31

## Install
`BepInEx\plugins\<plugin>\`: the DLL 0.8.3; 0.8.2 kept as `<dll>.0.8.2.bak`. `meta.json` updated. No setting of yours changes.
