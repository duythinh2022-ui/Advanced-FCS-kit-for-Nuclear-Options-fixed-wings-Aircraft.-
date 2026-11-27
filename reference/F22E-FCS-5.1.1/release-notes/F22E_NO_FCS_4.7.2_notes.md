# F-22E Strike Raptor FCS 4.7.2: the AoA law's flight-path rate without the filter leak

Base: 4.7.1. **Status:** compiled against the game's assemblies, benched (your config). **Not flown.**

Tom (24 Nov 17:25): "fix it" / "all of them". This is the shared fix from the J-20B 0.2.7.

## What was wrong (every FCS jet)
At the AoA limit the law commands:

> pitch rate = flight-path rate + gain × AoA error

The flight-path rate was taken as the pitch rate minus the AoA rate. The AoA rate goes through a 50 ms filter; the pitch rate did not.
- At 1-2 Hz about half of any pitch-rate wobble was left in the "flight-path rate", slightly ahead of it, and fed back into the pitch command.
- It also added a small accidental lead (pitch acceleration × 0.05 s) to pitch moves at the limit.

On the J-20B with its right wing shot off this ran as a 1.7 Hz pitch limit cycle at the damage AoA cap. Your Raptor log with damage (17:12) was clean: the leak was about 3x the real flight-path-rate wobble, but only 0.3 deg/s, because two tail stabs plus TVC leave far more pitch authority (900 / 550 deg/s^2).

## Change
**`FlightPathRateMatched`** ("2. Envelope", dev, **on**): the pitch rate takes the same 50 ms filter as the AoA rate before the subtraction. false = 4.7.1 exactly.

## Bench (4.7.1 -> 4.7.2, regression set 18 cases + corkscrew entries)
- Mean peak sideslip 4.02 -> 3.54 deg.
- Better by 0.5 deg or more: pull-then-roll 250 3.2 -> 2.5; pull-then-roll 320 3.1 -> 2.5; pull-then-pushover+roll 350 6.6 -> 5.4; pull-then-pushover+roll 450 6.8 -> 5.8; push-then-pullthrough+roll 450 9.8 -> 5.9.
- Worse by 0.5 deg or more: none.
- Corkscrew entries: within normal spread (same AoA and roll figures to a few percent).

## Install
`BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.7.2; 4.7.1 kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.7.1.bak`. `meta.json` updated.
