# KR-67 Ifrit FCS 1.7.0: canard travel 60, canard-jet AoA limit 70, low-thrust cap from real nose-down, bounded full-aft override, governor yaw pairs

Base: 1.6.0. Minor bump: this is a law change.

**Status:** compiled against the game's assemblies and benched (stock Ifrit, and the Multirole MTD airframe built from the blueprint). **Not flown.** 1.6.0 is kept as `.dll.1.6.0.bak`.

## Tom (23 Nov)
- 22:20: "see how much more useful aoa can be juiced from the ifrit mtd".
- 22:23: "see if the canards can be used for even more sideslip correction especially when t/w is low and the roll tvc cannot correct for as much aos, no on top fixes".
- 22:42: "just increase the negative canards deflection to 60 and build 1/2/3/4" (proposals 1-4 of `Ifrit_MTD_AoA_and_lowTW_canard_study_2026-11-23.md`).
- 22:43: "also fix the governor bug".
- 22:54: "also find max usable low thrust/no tvc aoa".

"Negative" was read as **canard leading edge down** (the nose-down side, the one on its stop at idle). Leading edge up stays 45. Swap the two keys if you meant the other side.

## What applies to which jet
- **Jet with canards (found by position, i.e. the MTD):** all of the changes below.
- **Stock Ifrit:** only 3 (bounded full-aft override) and 5 (governor yaw pairs). Changes 1, 2, 4 and 6 are per-airframe values for a canard airframe. Change 4's share was benched on the stock jet too and cut its idle AoA from 45 to 20 deg with nothing to fix (sideslip already 2-5 deg), so it is 0 there.

## Changes
1. **Canard travel (new keys, "5. Airframe"): `CanardRangeLEDown` 60, `CanardRangeLEUp` 45.** Applied to every stabilator found as a canard, after the geometric canard check. Asymmetric travel uses the existing path: the grid is regridded once the trailing-edge-up sign is known. Log line: `Ifrit FCS: elevator_L (1) travel -45/+60 deg (trailing edge up is +).` 0 = the stabilator travel.
2. **AoA limit on a jet with canards (new key, "2. Envelope"): `AoALimitPositiveCanards` 70.** Replaces `AoALimitPositive` wherever the law reads the positive limit (an instance property `AoAPosLim`). The stock Ifrit keeps `AoALimitPositive`. The `AoALimit` debug switch (Full65 / Cap36) still overrides it.
   - 70 rather than 75: on the low-thrust set, mean peak sideslip is 9.4 at 70 vs 10.3 at 75. At AB, 75 makes 10 deg of sideslip at 200 km/h (70: 7.3; 65: 4.6), the release undershoot grows to -21 deg, and the 200 km/h roll rate drops to 11.6 deg/s.
3. **Full-aft override bounded by lateral control (`StickCancelLateral`, shared).** The 2.44.0 rule (full aft scales the AoA protection's nose-down by 1 - sp/0.9) now holds only while the allocator delivers the roll and yaw it is asked for. The cancel fades as the filtered unmet roll / yaw acceleration (`allocUnmet`) grows from 2 to 12 deg/s². Applied in all four spots: the protection's positive and negative branches, and the capture's `flU` / `flD`. With the roll and yaw delivered it is the 2.44.0 rule unchanged.
4. **Low-thrust authority cap counts the pitch surfaces' nose-down after their lateral share (`StabLatShare`).** `AuthCapFromSweep` subtracts share x the stabilators' nose-down (`asymStabLo`) from `authNetDn` before finding where the nose-down runs out. The cap then sits where the tails and canards still have travel both ways, not where all of it holds the nose. Per airframe: `StabLatShareCanards` 0.5 (best of 0.3 / 0.4 / 0.5 / 0.6 / 0.7 on the MTD), `StabLatShare` 0 on the stock Ifrit.
5. **Governor bug: `PairYawGeo` on.** The governor's yaw effectors (`yawEffs`) now come from every mirrored stabilator pair (geometric partners), not only from surfaces with a name partner. Since 0.6.0 that had left the Ifrit with just the two splits. On the MTD the canard pair is a yaw effector of its own.
6. **Pitch envelope's nose-down authority on a canard jet (`ADnStabShareCanards`).** `aDn` (all effectors at their nose-down stop) also takes off the share from 4. Without it, the 60 deg canard travel made the idle pull run past the 45 deg no-TVC cap into the float: hold 62 deg, peak 75, roll rate 2-3 deg/s at 300 km/h. The envelope let the pitch rate grow on nose-down that the canards, busy with roll and yaw, never delivered.
   - Off on the stock Ifrit (`ADnStabShare` false): there it softened every push (pullthrough alphaMin -30.5 -> -23.8 deg) and raised the regression set's mean sideslip by 0.3 deg.
   - Also tried: a reach horizon on `aDn` (count only the deflection each surface reaches within 0.2-0.3 s). It fixed the idle overshoot too but cut the MTD's full-AB roll rate by a quarter. Not kept.

New config keys: `CanardRangeLEDown` 60, `CanardRangeLEUp` 45, `AoALimitPositiveCanards` 70. Internal statics: `StickCancelLateral` (on), `CancelLatUnmet0` 2, `CancelLatUnmetSpan` 10, `StabLatShare` 0, `StabLatShareCanards` 0.5, `ADnStabShare` (off), `ADnStabShareCanards` (on), `PairYawGeo` (on).

## Bench: Multirole MTD, 1.6.0-equivalent -> 1.7.0
Same build, 1.7.0 changes off (canards 45, limit 65, share 0, override unbounded, governor pairs off) vs defaults.

**Low-thrust set** (idle / 0.3 / 0.5 / AB; loaded roll 200-450, reversal, pedal): mean peak sideslip 16.3 -> 10.2 deg, worst 76.2 -> 29.5.
| case | 1.6.0 eq. (sideslip / AoA max / roll) | 1.7.0 |
|---|---|---|
| idle roll 450 | 76.2 / 45 / 13 | 29.5 / 48 / 23 |
| idle roll 300 | 11.6 / 55 / 6.4 | 11.4 / 51 / 18.5 |
| half thr. roll 200 | 9.4 / **83** / 16.6 | 8.0 / 74 / 16.8 |
| half thr. roll 300 | 28.6 / **87** / 12.9 | 24.3 / 70 / 13.2 |
| half thr. roll 450 | 48.5 / 65 / 12.4 | 7.4 / 70 / 15.6 |
| half thr. reversal 300 | 22.7 / **101** / 10.7 | 18.4 / 71 / 11.5 |
| half thr. pedal 300 | 30.1 / **88** / — | 20.8 / 70 / — |
| AB roll 300 | 5.2 / 65 / 17.4 | 3.8 / 70 / 16.5 |

- The half-throttle departures through the AoA limit (83-101 deg) are gone: the jet stops at the limit.
- **Still open:** half-throttle rolls / pedal at 300 km/h still make 18-24 deg of sideslip (from 23-30), and the idle 450 km/h roll 29.5 (from 76). Idle and 0.3 T/W rolls are 2-3x faster (6-15 -> 11-25 deg/s).

**AB high-AoA set, limit 65 -> 70:** no departures. Holds 68-69 deg (was 63-64). Loaded-roll sideslip 7.3 / 3.8 / 7.0 at 200 / 300 / 450 km/h (was 4.6 / 5.2 / 7.1). Roll rate 13.5 / 16.5 / 21.5 deg/s (was 15.1 / 17.4 / 22.8). Release undershoot -16.7 / -15.9 / -9.6 (was -14.9 / -14.2 / -9.0). Recovery to 30 deg about 0.1 s slower.

**Canard set:**
- No-TVC loaded rolls and reversals at the cap: roll rate 16-18 -> 31-32 deg/s, sideslip 3.1-3.9 -> 3.7-3.8 deg, canard stall cost lower (0.11 -> 0.05).
- TVC loaded rolls: sideslip 6.6 / 8.9 -> 6.0 / 9.5. Settle time at 450 is longer (1.9 -> 2.7 s).
- Push + roll, released roll, pedal: unchanged.

## Bench: stock Ifrit, 1.6.0-equivalent -> 1.7.0 (full suite, same build)
Only `StickCancelLateral` and `PairYawGeo` act here.
- Regression set: mean / worst sideslip 4.92 / 10.0 -> 4.87 / 10.0. Better: push-then-pullthrough + roll 450, 7.9 -> 5.4. Worse: pullthrough with the roll released 350, 5.6 -> 6.6; full aft + roll + reversal 320, 4.1 -> 4.9; push-then-pullthrough + roll 350, 5.5 -> 6.2. These are `PairYawGeo`'s cost. `StickCancelLateral` alone is identical to off on this set.
- Curves on those cases: alpha at 2 s, AoA min / max and roll rate within 1 deg / 3 deg/s.
- Stop / reversal sweep: 4.75 -> 4.67, one better (300 0.40 rev 7.9 -> 6.2).
- Corkscrews: identical.
- Damage: 5.30 / 13.6 -> 5.24 / 15.5, 3 better / 1 worse. Worse: held max AoA with the left intake off, 13.6 -> 15.5.
- Low-thrust set: mean 5.18 -> 5.03. The half-throttle AoA overshoot is gone (roll 200: 79.5 -> 67 deg, reversal 300: 77 -> 66). Idle unchanged at 45.

## Max usable AoA with the nozzles not covering pitch (Tom 22:54)
Swept the fixed no-TVC cap (`NoTvcAoACapPositive` / `NoTvcThrustAoACapPositive`) at 35 / 45 / 55 / 65 (MTD also 70). Each step flew a full-aft pull, loaded rolls at 200 / 300 / 450 km/h, a reversal, a pedal kick and a release, at idle, at 0.3 throttle (T/W under `LowThrustTW`), and with DisableTVC at half throttle and at AB. "Usable" here = no departure, sideslip under about 10 deg, and a roll rate worth having.

**Stock Ifrit (1.7.0):**
- **Idle and 0.3 T/W: 55 is the most.** At 45: holds 45, sideslip at most 4.8, roll 5-13 deg/s. At 55: holds 55, sideslip at most 8.3, roll 4-9 deg/s (about 25 % slower). At 65 the pure pull runs away (98 deg at idle, 118 at 0.3), and the 0.3 T/W roll at 450 km/h makes 24 deg of sideslip.
- **DisableTVC:** the fixed cap stops mattering above about 50. The swept authority cap rules: AB 45-54 deg, half throttle 42-51, identical at 55 and 65. Clean (sideslip at most 4 outside the pedal kick's 13).

**Multirole MTD (1.7.0):**
- **0.3 T/W: 45.** At 45: holds 45, roll 8-15 deg/s, no departure. At 55: holds 53-55, but roll falls to 5-7 deg/s, the 450 km/h roll departs (30 deg of sideslip), and the pedal kick makes 15.
- **Idle:** the cap above 45 changes nothing. The jet holds about 45-47 (peak 50.6) whatever the fixed cap. The 450 km/h idle roll makes 29.5 deg of sideslip at every cap (the open case above).
- **DisableTVC:** authority-limited, as on the stock jet. AB holds 40, half throttle 55, identical at 45 and 55.

So the shipped `NoTvcAoACapPositive` 45 is right for the MTD. The stock Ifrit has about 10 deg more at idle / low T/W if you want it (55), at a quarter of its roll rate there. **Not changed in this build:** the key is shared by both airframes, and the choice is yours.

(A release at idle flies down to about 110 km/h and holds 1 g there at 40-45 deg AoA on both jets: that is the 1 g hold at that speed, not a hang.)

## Files
- `FcsController.cs`: canard travel after the geometric canard check, `hasCanards`, `AoAPosLim`, `StickCancelLateral` / `CancelKeep`, `aDn` share, `PairYawGeo` on.
- `AsymShared.cs`: `AoAPosLim`, `StabLatShare` / `StabLatShareCanards` / `StabShareNow` in `AuthCapFromSweep`.
- `SurfaceEffector.cs`: `SetCanardTravel`.
- `Plugin.cs`: the three keys.
- `FcsStandalonePlugin.cs`: 1.7.0.
- Bench: `bench/stubs/Plugin.cs` (new keys). New scripts in `tools/canard/`: `lowcap.sh` / `lowcapscore.py` (the usable-AoA sweep), `lowtw.sh`, `ltcmp.py`, `aoaset.sh`, `aoascore.py`, `aoacmp.py`. The MTD airframe JSON is built from the blueprint with `tools/canard/mtd_build.py` (not included: third-party).
- The Revoker is not changed. Changes 3 and 5 are shared logic: say if you want them ported.
