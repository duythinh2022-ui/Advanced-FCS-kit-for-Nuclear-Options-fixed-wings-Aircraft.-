# F-22E Strike Raptor FCS 4.8.16 (Nuclear Option)

Base: 4.8.15. Bench only, not flown. Asked for (26 Nov 16:08 / 17:09): "57 is a little too much"; "it should only cost roll when the 57 cases happen".

## The 57
- **It is not a stop overshoot.** It's a roll held at the +55 limit, with the AoA riding 1.5-2 deg high.
  - Bench, window at 975 s in the 10:34 log, CG +5 cm: 57.0.
  - The game in the same spot: 57.5. 4.8.13 does the same.
- **Why:** the roll's coupling allowance comes from a live reserve that runs about 2× high at the limit.
  - So the roll kept everything while the allocator left pitch 20-60 deg/s² short.
  - Its stop-need term only counts the AoA rate, which is zero when the jet sits past the limit at a steady AoA.

## Change (`FcsController`, coupling split)
- **`RollYieldsPitchShortfall`** (dev, on): the pitch's need in the split also takes the nose-down the allocator couldn't deliver, with two limits:
  - **at most the inertial pitch-up the roll itself is making.** The roll can give back what it takes, never be stopped for a shortfall it doesn't cause. Without this cap the yield turned into a roll stop that took more stab and made a new 58.2 overshoot.
  - **in the stabs' share of the braking.** With the nozzles carrying it, a shortfall is mostly their slew. Without this weighting the TVC reversal at the 30 deg cap went from 3.2 to 4.7 deg of sideslip.
- **The roll yields only while pitch is short.** No model, no gate.
- `false` = 4.8.15 (bench: identical in all 57 windows).
- No settings, airframe or Rust core change otherwise.

## Bench
10:34 log, 57 no-TVC approaches past 40 deg; 29.2 t, bench stabs 0.85.

| CG | Worst peak, 4.8.15 | Worst peak, 4.8.16 | Roll change in the other approaches (mean / worst) |
|---|---|---|---|
| 0.0 | 55.8 | 55.8 | 0.00 / −1.1 deg/s |
| −5 cm | 55.4 | 55.4 | −0.09 / −1.2 deg/s |
| +5 cm | 57.0 | 55.9 | −0.11 / −1.5 deg/s |

- The 57 case: 57.0 → 55.9 with 1.5 deg/s less roll in that approach.
- None of the other approaches loses more than 1.5 deg/s.
- The remaining 55.6-55.9 peaks are rolls started right at the limit, unchanged from 4.8.15.

### Batteries (CG 0, against 4.8.15)
- **No TVC:** every line within single-run scatter.
  - reversal roll 30/36/45/50: 65.0/63.5/43.5/43.8 → 63.5/63.3/43.8/43.8
  - loaded roll 36/45: 141.9/96.7 → 141.6/97.1; its sideslip at 45: 7.1 → 4.7
- **TVC:**
  - reversal and loaded roll the same
  - reversal sideslip at 30: 5-speed mean 3.2 → 3.1
  - slow-roll pull at 45 (idle, 320 km/h): 5-speed mean 4.3 → 5.1, all from one speed (3.6 → 10.2 at 320 exactly; the other four are the same or better); roll 44.2 → 44.6
- **Low-AoA roll-stop matrix:** worst 6.6 → 6.8, mean 3.4 → 3.5.
- **Pitch-rate curves:** unchanged.

## DisableTVC in the player file too (Tom 26 Nov 17:34, rebuilt as 4.8.16)
- **`DisableTVC` is now in both files:** the player file (main DLL, `1. General`) and the dev file (`0.1 Debug - Airframe test`). They are one setting: changing either in ConfigurationManager sets the other at once, and both files are saved.
- **First start:** the new player key copies the dev value, so an existing no-TVC setup stays as it is (your dev file says `true`).
- **If the two files disagree at start** (one edited with the game closed), the file written last wins.
- **Without the dev DLL,** the player key is the setting.
- **Checked** with the real BepInEx `ConfigFile` in a test harness: first start, change from each side, restart after an offline edit, no dev DLL.
- **Flight law unchanged** (bench replays identical).

## Going back
Set `RollYieldsPitchShortfall = false` in the dev file (= 4.8.15), or reinstall `Aryx_F22E_StrikeRaptor_FCS.dll.4.8.15.bak`.
