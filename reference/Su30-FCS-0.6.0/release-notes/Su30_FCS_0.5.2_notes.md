# Su-30VXN Gandiva FCS 0.5.2: asymmetric load, the AoA limit from the roll the jet can still use

Base: 0.5.1. Tom, 11 Nov: "Dont make it a gate or switch. Make it estimate the remaining useful authority to limit aoa" (18:26), then after flying 0.5.1 (18:53 log): "the roll to the heavy side is very slow, i'd rather have my aoa a bit lower to get more roll. But dont make it pessimistic" (18:56).

**Changelog:** With an asymmetric load the AoA limit is now where the roll the jet can still use, times the AoA, is largest. "The roll it can use" is the law's roll, held to what the weaker side can stop. On this airframe that is about 45° with a 16-31 cm load (0.5.1: 50-52°), where the weak side has 18-40 % more roll reserve, so the roll into the heavy side is 30-50 % faster. With no load the limit is unchanged. The negative limit no longer collapses to its 8° floor. New dev setting AsymAoAWeight (1.0): higher keeps more AoA, lower gives up more AoA for roll.

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,487,872 bytes, SHA-1 b72cc39745cf6482bece5e67baf2b26bc3ac1d12). Native core unchanged.
- Benched (.NET 8): the 31-case load matrix, a 24-case stop matrix at 16 and 20 cm, replays of the 18:53 flight, the 88 loaded reversals, the suites.
- Run on the game's Mono under Wine: plugin loads, kernel self-test 200 / 200, solver check passes.
- **Not flown.**

## What the 18:53 flight showed (0.5.1, 20 cm of offset, 25.6 t, nozzles locked)
- **Heavy-side roll at the 50° limit, 100-125 kt:** held to 10-16 deg/s. That is 0.8 s × a weak-side reserve of 13-20 deg/s² (`stopRateP`); the law itself allowed 26-46. The other way: 21-36.
- **At 26-28° and 128-138 kt:** 15-23 deg/s. Lower AoA bought little at that speed.
- **Negative limit:** on its 8° floor (−8.6°) for most of the loaded flight. The old relative demand read the load as over the line at every negative AoA.

## Why the limit was where it was
The weak side's remaining roll on this airframe, by AoA (bench, nozzles locked):

| AoA | 15° | 20° | 30° | 40° | 45° | 50° | 55° |
|---|---|---|---|---|---|---|---|
| 16 cm, 122 kt (deg/s²) | 44 | 33 | 30 | 31 | 32 | 27 | 5 |
| 16 cm, 310 kt | 365 | 301 | 233 | 233 | 229 | 188 | 47 |

- **Flat, then a cliff:** flat from about 20° to 47°, falling past 50°. The 0.5.1 limit at 50° sat on the start of the cliff.
- **Crossing searches flip:** any limit found where a demand crosses a line jumps between the bottom and the top of the flat stretch. A first build of this release did exactly that with 20 cm: 48 → 20 → 47° in a second, and the jet pitched down 16° mid-roll.
- **What lowering AoA buys:** going from 50° to 45° buys 18-40 % reserve. Going lower buys almost nothing until about 15°.

## Changes
1. **Usable lateral capacity** (`CplKnee`, replaces the crossing search while loaded). For each sweep AoA:
   - `roll(a)` = the law's roll limit, held to what the weak side's reserve stops in the clean jet's stopping time at its own limit (law roll / clean reserve there) and in `AsymStopTime`. It fades in with the load as the coupled check does.
   - `C(a) = a^AsymAoAWeight × roll(a)`. The limit is the AoA of the largest `C`, smoothed between grid points.
   - No load: `roll(a)` is the law's roll everywhere, so the largest `C` is at the clean limit and nothing changes.
   - With a load that eats the reserve, the maximum sits at the knee before the cliff. It cannot drop to the bottom of the flat stretch: 20° would need 2.3× the reserve at 45°.
2. **Clean reference** (`CplRefAuth`): at the two sweep points around the clean limit, the coupled check also solves the same airframe without the load's roll and yaw. It uses the same effectors, pitch load and design sideslip.
3. **Roll rate toward each side:** the law's roll × (weak-side reserve / clean reserve at the clean limit), and `AsymStopTime` (0.8 s, as 0.5.1) × the weak-side reserve, 10 deg/s at least.
4. **Dev setting `AsymAoAWeight`** ("Asymmetric load", 1.0, 0.25-4).
5. Telemetry column added at the end: `cplRef` (the clean reserve at the clean limit).

## Bench results
Bench jet 22.8 t; store on a mid wing: 800 kg = 16 cm, 1,000 kg = 20 cm, 1,600 kg = 31 cm.

### AoA limit
| Case | 0.5.1 | **0.5.2** |
|---|---|---|
| Nozzles locked, 16 / 20 / 31 cm | 51.3-51.8 / about 50 / 46-49° | **45.2 / 44.8 / 44.0°**, steady through the bleed |
| TVC on, 16-20 cm | 52°, opening to 75° below about 100 kt | **45-49°** at every speed |
| Negative limit | −65°, dropping to −9…−21° in rolls (flight: −8.6°) | **−65°** |
| No load | 75 / −65° | identical |
| High speed with the load (over about 300 kt) | 51-52° | 75° (the reserve is large; the G limit binds first) |

### Heavy-side roll on the limit (stop matrix, nozzles locked, full roll 1.5 s)
| Entry | 0.5.1: release speed, roll | **0.5.2** |
|---|---|---|
| 16 cm, late | 78 kt, 10 deg/s | 99 kt, **15 deg/s** |
| 16 cm, mid | 104 kt, 17 deg/s | 120 kt, **27 deg/s** |
| 16 cm, early | 163 kt, 44 deg/s | 180 kt, **62 deg/s** |
| 20 cm, mid / early | | 123 kt 27 / 183 kt 64 deg/s |

The jet keeps more speed at 45°, so the release speeds differ. Replays of your 18:53 heavy-side rolls (logged state and sticks) at the same speeds: 21-22 deg/s → **28-31** (×1.3-1.45). The bench rolls about 1.4× faster than the flown jet, so in the game expect roughly 15-16 → 21-23 deg/s.

Load matrix, roll each way (first roll left / right): nozzles locked, 16 cm, 243 kt: 10 / 42 → 15 / 49 deg/s, and −39 / −15 → −49 / −24. With 31 cm: 17 / 46 → 30 / 61. TVC on, 243 kt: 68 / −65 → 91 / −88.

### Stops after the stick is centred
| | 0.5.1 (16 cm) | 0.5.2, 16 cm | 0.5.2, 20 cm | Clean jet |
|---|---|---|---|---|
| Heavy side, pitch held: stop time | 1.05-1.90 s | 0.88-1.76 s | 0.90-1.45 s | 1.38-1.78 s |
| Heavy side, both sticks centred: bank after release | 33-75° | 37-107° | 38-76° | 69-89° |
| Both centred: worst sideslip | 12.5° | 11.7° | 14.5° | 7.9° |

- Load matrix peak sideslip: 6.7° or less (0.5.1: 5.0° or less) with nozzles locked; 6.2° or less (0.5.1: 8.4°) with TVC on.
- `AsymStopTime` 1.0 instead of 0.8 gave 10-15 % more roll but 21-22° of sideslip in the both-sticks-centred stop at 123 kt; not taken. It is the dev setting if you want to try it.

### Loaded reversals (54 roll-spam runs, TVC on), better / same / worse than 0.5.1
- Roll behind command, rms: 49 / 1 / 4. Max: 37 / 7 / 10.
- Bank travelled: 43 / 3 / 8 (faster rolls).
- Peak sideslip: 27 / 4 / 23.
- **AoA above the limit:** worse in 51 runs. Mean of the maxima 1.0 → 2.4°, worst 5.0 → 9.3°, cases over 2°: 11 → 44.
  - Peak AoA itself went down: mean 61.9 → 49.4°. The worst case peaked at 57.6° (0.5.1: 56.6°).
  - It is the capture of a 300 kt pull-in with roll spam (pitch rate to 90 deg/s) overshooting a limit that is now 5° lower. The limit itself held steady.

### Regression and cost
- Suites (clean jet), new switches on or off, against 0.5.1: 87 / 87 per-case files and 4 / 4 tables identical.
- Sweep while loaded, bench, one case alone, median of 3: 329 → 399 µs per tick (99th percentile 842 → 1,054). The extra is the clean-reference solves.

## Not done, open
- **Not flown.** Bench jet 2.8 t lighter than the flown one.
- **TVC-on low-speed release is gone:** with the nozzles working and the load on, 0.5.1 let the limit open to 75° below about 100 kt. 0.5.2 holds 45-49° there, because the weak side's reserve at 75° is well under that at 45°. Raise `AsymAoAWeight` (2-3) to keep more AoA there.
- The negative side of the sweep still ends at −40°.
- Damage asymmetry not benchable (no Su-30 damage data).

## Ground steering (asked: "seems like there is no nose gear indi")
- **It is there and running:** the nose-wheel INDI was on for 71 % of the ground time in the 18:53 log and 49 % in the 18:55 one. It stops the yaw once you let go of the pedal (18:55, 25.9-27 s: 38 deg/s of yaw at 12 kt stopped by the nose wheel) and holds the straight line.
- **With the pedal held, the steering is stock by design** (your rule for the F-22E and Ifrit: stock steering while the pedal is in). Stock steering asks for the full 60° of nose-wheel lock. The wheel slews there at about 22 deg/s and is far past the tyre's grip, so taxi turns at 6-20 kt slide with 7-13° of sideslip (18:53, 38-46 s). INDI on the pedal too would stop that slide but change the stock feel.
- **The 18:55 crash** was the touchdown, not the steering: 155 kt, 12° AoA at 5° pitch, about 10 m/s of sink (nz 2.6 → 9 within 0.25 s).

## Install
- **Main:** `plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.5.2 + meta.json. 0.5.1 kept as `Su30_VXN_FCS.dll.0.5.1.bak`.
- **Dev:** unchanged file. `AsymAoAWeight` appears in "Asymmetric load" after the first start.
- **Release:** `C:\Games\NO mods\Su30-FCS-0.5.2-release`.
- **Mod manager:** update the version there. Restart the game.

## Bench tools
- `bench/an/stopmat.sh <tag> [Name=value ...]` (`STOREKG=1000` for 20 cm), summary `bench/an/stopmat.py`.
- 0.5.1 laws in this build: `CplRefAuth=false CplKneeOn0=false`.

Previous notes: `claude/Su30_FCS_0.5.1_notes.md`.
