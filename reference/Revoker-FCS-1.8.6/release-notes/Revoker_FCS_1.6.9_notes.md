# FS-12 Revoker FCS 1.6.9: roll rate cap reads half the yaw span (with the F-22E and Su-30)

Base: 1.6.8. The law is the same as 1.6.7's, now shared with the F-22E (4.6.7) and Su-30 (0.6.6) so the jets stay on the same law.

**Status:** compiled, bench suite A/B run (below). **Not flown.**

Tom (24 Nov 02:57): "do that fix then for flanker, revoker and raptor".

## Change (the same in the F-22E 4.6.7, Su-30 0.6.6 and Revoker 1.6.9 FCS)
- The roll rate cap (the reversal model behind `RollPaceRateBlend`) now reads **half the yaw span**, (`capZeP` + `capZeN`) / 2, for the yaw the effectors can make on a reversal. Before, it read the reversal side's figure alone. The passive stiffness is added as before.
- Each side's figure is the headroom from where the surfaces sit this tick, plus the measured and committed yaw acceleration. In the sum the measured and committed terms cancel exactly, and a move of the surfaces toward one side cancels to first order. Where the surfaces sit is still counted through the model's crossing time (`tCross`).
- **Why:** the side figure moved with the surfaces the roll loop was moving, so the cap, the roll target and the surfaces swung together in held loaded rolls. In the Revoker's 24 Nov 02:21 log the figure swung 65-110 deg/s^2 at ~2.3 Hz, the cap 34-48 deg/s, the flaperons ±20 deg and the rudders ±14 deg. The F-22E's and Su-30's logs show the same surface activity in their loaded rolls.
- New dev key **`6. Lateral governor / RollRateCapSpan`** (true). false = the previous law exactly, switchable in flight for an A/B.

## Held loaded roll, all three jets
460 km/h, 0.45 aft stick held, full roll held from 3 s. "Surfaces" = mean std of each surface command's 0.1-1 s band (deg); roll rate in deg/s.

| Jet | TVC | Surfaces, old -> new | Roll rate, old -> new | Peak sideslip, old -> new |
|---|---|---|---|---|
| F-22E | on | 1.69 -> 1.52 | 113 -> 112 | 1.1 -> 1.1 |
| F-22E | off | 0.18 -> 0.20 | 110 -> 109 | 1.6 -> 1.5 |
| Su-30 | on | 5.34 -> 3.24 | 139 -> 125 | 3.5 -> 2.5 |
| Su-30 | off | 4.53 -> 5.27 | 145 -> 133 | 5.2 -> 5.3 |
| Revoker | on | 1.14 -> 0.78 | 64 -> 59 | 1.1 -> 1.2 |
| Revoker | off | 2.90 -> 2.46 | 45 -> 44 | 2.0 -> 1.8 |

The bench is much calmer than the game: the Revoker log's flaperon band is 7.6 deg against ~1 deg in its bench replay. So these figures give the direction, not the in-game size.

## Revoker suite, old law -> 1.6.9 (`RollRateCapSpan` false -> true; same as the 1.6.7 run)
- **held0.4+roll 350 (22 deg AoA): peak / mean roll rate 124 / 79 -> 91 / 66 deg/s. The largest cost.**
- roll-then-pull-rev 400: peak sideslip 3.8 -> 4.6.
- fullaft+roll 250: 2.9 -> 2.3. fullaft+roll+rev3s 320: 2.7 -> 2.4. push+roll -55 corkscrew 170: 0.6 -> 1.0.
- Roll-stop / reversal sweep: worst 4.0 / 6.2 -> 4.0 / 6.4.
- Damage suite and corkscrews: within 0.4 deg.
