# FS-12 Revoker FCS 1.6.7: the roll rate cap reads half the yaw span

Base: 1.6.6.

**Status:** compiled, bench suite run (A/B below). **Not flown.** The fix is for in-game behaviour that the bench does not reproduce at full size, so the game is the real test.

## Tom (24 Nov 02:25, 02:40)
"when rolling with no tvc in the log, the control surfaces tweaks a lot". Then: "do it" (to the half-span fix proposed after 1.6.6).

## Cause (02:21 log, held TVC-off loaded roll, 460 km/h, 30 deg, ~48 deg/s)
- The roll rate cap (the reversal model behind `RollPaceRateBlend`) needs the yaw acceleration the effectors can make toward the reversal side. Since F-22E 2.8.1 it read the capacity estimator's figure for that side, `capZeP` / `capZeN`. That figure is the headroom from where the effectors sit this tick, plus the measured yaw acceleration, plus the committed increment.
- On the Revoker with TVC off, most of that figure comes from the surfaces the roll loop is moving: canards 57 and flaperons 41 deg/s^2, against the rudders' 24.
- So the figure swung 65-110 deg/s^2 at ~2.3 Hz. The cap swung 34-48 deg/s with it, and the roll target ±5 deg/s. The flaperons swung ±20 deg and the rudders ±14 deg, while the roll rate held within 2 deg/s.

## Change
- The cap's capacity is now **half the yaw span**, (`capZeP` + `capZeN`) / 2, plus the passive stiffness as before.
- In that sum the measured and committed yaw acceleration cancel exactly. A move of the effectors toward one side adds to one figure what it takes from the other, so that cancels to first order too.
- Where the effectors sit stays in the model through its crossing time (`tCross`), as in the original 2.5.8 reversal model, which used a half-range for the same quantity.
- In the 02:21 log the span swings about half as much as the side figure (band std 6.3 vs 11.8 deg/s^2).
- New dev key **`6. Lateral governor / RollRateCapSpan`** (true). false = 1.6.6 behaviour, for an A/B in game.
- Revoker only so far. The same law is in the F-22E and its ports; port it once the game confirms it.

## Bench
TVC off, caps 30/30, held full-aft + full roll ("chatter" = std of the 0.1-1 s band of each command, deg):

| Case | Roll rate | Cap swing | Flaperon | Rudder | Canard | Peak sideslip |
|---|---|---|---|---|---|---|
| 460 km/h, 1.6.6 | 40 | 1.1 | 1.5 | 5.3 | 1.9 | 1.2 |
| 460 km/h, 1.6.7 | 37 | 0.6 | 1.5 | 4.1 | 1.7 | 2.2 |
| 300 km/h, 1.6.6 | 33 | 1.5 | 1.9 | 6.2 | 2.4 | 1.6 |
| 300 km/h, 1.6.7 | 32 | 1.0 | 1.9 | 6.2 | 1.4 | 1.2 |
| Replay of the log, 100-108 s, 1.6.6 | 39 | 1.3 | 1.0 | 3.2 | 1.1 | 1.1 |
| Replay of the log, 100-108 s, 1.6.7 | 38 | 0.7 | 0.8 | 2.8 | 1.0 | 0.8 |

The bench is much calmer than the game (game flaperon band 7.6 vs 1.0 in the replay), so these show direction, not the in-game size.

Regression suite (TVC on), 1.6.6 -> 1.6.7, the changes worth naming:
- **held 0.4 stick + roll, 350 km/h (22 deg AoA):** peak / mean roll rate 124 / 79 -> 91 / 66 deg/s, peak sideslip 2.1 -> 1.6. **The largest cost: mid-AoA rolls are slower.**
- roll-then-pull-rev 400: peak sideslip 3.8 -> 4.6.
- fullaft+roll 250: 2.9 -> 2.3; fullaft+roll+rev3s 320: 2.7 -> 2.4; push+roll -55 corkscrew 170: 0.6 -> 1.0.
- Roll-stop / reversal sweep: worst 4.0 / 6.2 -> 4.0 / 6.4, means 2.07 / 3.02 -> 2.06 / 2.98, roll rates within 1-4 deg/s.
- Damage suite and corkscrews: within 0.4 deg of sideslip.
