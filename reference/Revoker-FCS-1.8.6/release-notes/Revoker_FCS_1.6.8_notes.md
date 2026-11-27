# FS-12 Revoker FCS 1.6.8: back to the shared roll-rate-cap law (1.6.7 reverted)

Base: 1.6.7. The source is 1.6.6's apart from the version number.

**Status:** compiled. **Not flown.**

## Tom (24 Nov 02:47-02:48)
"why doesnt it happen on the raptor and flanker is the model wrong?" / "if the model is wrong fix it and same laws as the raptor, flanker".

## What the logs show
Loaded rolls (|p| > 25 deg/s, AoA 18-45 deg, roll rate within 8 deg/s of the target, T/W > 0.7) in Tom's own logs, std of each surface command's 0.1-1 s band (deg):

| Jet, log | TVC | Surfaces | Roll-target swing | Reversal-side capacity swing |
|---|---|---|---|---|
| F-22E, 5 logs 22-24 Nov | on | 6.1-8.7 (rudders 8.6-8.8, flaperons 8.4-8.9 in the 23 Nov 21:09 log) | 4.1-5.8 | 3.8-7.0 |
| Su-30, 5 logs 14-16 Nov | on | 7.0-13.5 | 2.3-9.2 | 2.1-12.9 |
| Revoker, 24 Nov 02:21 | off | 7.2 (canards 8.1-8.5, flaperons 5.9-6.5, rudder 7.0) | 2.7 | 12.8 |

- The Raptor's and the Flanker's surfaces swing as much in their loaded rolls as the Revoker's do in the 02:21 log. Their logs have no TVC-off loaded rolls, so this is not a like-for-like TVC-off comparison.
- The Su-30 counts its canards and flaperons in that yaw capacity too. So the explanation given with 1.6.6 / 1.6.7 ("only the Revoker leans on canards and flaperons for yaw") was wrong.
- Nothing here shows the Revoker's model to be wrong. The behaviour comes from the shared law, which all three jets run.

## Change
- 1.6.7's half-span read for the roll rate cap and its dev key `RollRateCapSpan` are removed. The Revoker runs the same rate-cap law as the F-22E and Su-30 again.
- The TVC-off AoA limit fix from 1.6.6 stays.

## Open
The surface activity in loaded rolls is a property of the shared law, on all the jets. A fix would go into every FCS together.
