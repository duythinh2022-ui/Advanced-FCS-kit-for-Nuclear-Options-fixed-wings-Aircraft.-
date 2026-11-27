# FS-12 Revoker FCS 1.8.6: the AoA law's flight-path rate without the filter leak

Base: 1.8.5. **Status:** compiled against the game's assemblies, benched (your config). **Not flown.**

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
**`FlightPathRateMatched`** ("2. Envelope", dev, **on**): the pitch rate takes the same 50 ms filter as the AoA rate before the subtraction. false = 1.8.5 exactly (bench: identical with the key off).

## Bench (1.8.5 -> 1.8.6, the new key off vs on)
**Regression set (20 cases):**
- Mean peak sideslip 2.29 -> 2.25 deg, mean roll 43.0 -> 43.1 deg/s.
- Sideslip better by 0.5 deg or more: none.
- Sideslip worse by 0.5 deg or more: none.

**Stop / reversal sweep:**
- 1.8.5: stop: betaEv worst 4.6 mean 2.25 | entry beta mean 2.24 | mean roll 90 | tRev mean 0.57
- 1.8.5: reversal: betaEv worst 6.6 mean 3.16 | entry beta mean 2.24 | mean roll 90 | tRev mean 0.42
- 1.8.6: stop: betaEv worst 4.6 mean 2.25 | entry beta mean 2.24 | mean roll 90 | tRev mean 0.57
- 1.8.6: reversal: betaEv worst 6.6 mean 3.19 | entry beta mean 2.24 | mean roll 90 | tRev mean 0.42

## Install
`BepInEx\plugins\<plugin>\`: the DLL 1.8.6; 1.8.5 kept as `<dll>.1.8.5.bak`. `meta.json` updated. No setting of yours changes.
