# FS-12 Revoker FCS 1.6.0: F-22E 4.5.2/4.5.3 port, canard window replaced by an allocator price, surfaces paired by position

Base: 1.5.0. Minor bump: this is a law change.

**Status:** compiled against the game's assemblies and benched. **Not flown.** 1.5.0 is kept as `.dll.1.5.0.bak`.

## Tom (23 Nov)
- 21:09: "should the canards code be get rid off, and make the allocator figure it out and still at least as well with the canards code".
- 21:16: "replace what can be replaced, improve the canards law, and port the allocator fix from raptor 4.5.3". Scope picked: Ifrit + Revoker, 4.5.2 + 4.5.3.

## Changes
1. **F-22E 4.5.2 `PaceRollSatDelivered`** and **4.5.3 `LatShareNoGate` + `PlanFollowsJet`**, as on the Raptor.
   - `LatShareNoGate` stays on here: the Revoker's stabilators are its canards, and the share does what it does on the Raptor (frees them for roll/yaw while the nozzle takes the pitch).
   - New telemetry column `planHeld`. The `LateralStabReserve` text is updated to the 4.5.3 wording.
2. **The canard attached-flow window (0.2.7) is replaced by a price in the allocator (`StallCost`).**
   - The old window walked the canards into a window above 30° AoA with lateral input in (negative AoA always, positive with TVC off).
   - Now, each tick, each canard's band is the deflection range that keeps its local AoA within the foil's knee (lift slope at 0.3 of its steepest) at this tick's flow. Outside it, every degree costs `StallCostK` (0.03) × (deg / range)² through the allocator's anchor/band term, the native core's own (as the nozzle budget uses).
   - It acts at every AoA, with no AoA, stick or TVC condition. When the moment is worth it, the allocator still pays it. No efficient range in the travel (deep stall): no price.
   - The same law flies the Ifrit FCS's canards (Multirole MTD).
   - `CanardAttachedWindow` and `CanardWindowMargin` in the dev config are retired (no effect, kept so old files load). The asymmetric-load model's canard assumption follows the window (off).
   - Bench switch `CanardWindowOn` = true brings the window back.
3. **Surfaces paired by position (`GeoPairing`) and canards by position (`GeoCanard`).**
   - Partners are found by mirror position, not the `_L` / `_R` name ending.
   - A stabilator ahead of the mean station of the wing's trailing-edge surfaces is a canard.
   - On this jet both find exactly what the names did: `canard_L+canard_R (canard), aileron_L+aileron_R`. Log line: `Revoker FCS surface pairs (by position): …`.

New telemetry column `stallCost`: the worst canard excess past its band this tick (deg).

## Bench: 1.5.0 → 1.6.0 (full suite)

**Regression set and corkscrews:** unchanged (all 19 + 18 within 0.5°).

**Stop/reversal sweep:**
- better: 400/0.55 stop 1.6 → 0.8°; 300/0.55 stop 0.9 → 0.2°; 300/0.40 reversal 1.6 → 1.0°; 400/0.55 reversal 3.1 → 2.5°; 400/0.70 stop 1.5 → 0.9°.
- worse: 500/0.70 stop 0.8 → 1.9°.

**Damage suite:**
- better: leaf reversal 296 clean 2.6 → 1.3°; with fins and/or rudder gone, leaf reversals 296 / 370 by 0.8–1.1°, leaf stops 296 by 0.6–0.7°; released push-over 560 2.3 → 1.4°.
- worse: held push-over 600 clean 1.2 → 1.9°; leaf stop 370 clean 1.2 → 1.8°.

## Bench: canard cases (the window's own territory), 1.5.0 (window) → 1.6.0 (price)

| Case | Sideslip | Velocity-vector roll | Canard stalled* | Canards back together** |
|---|---|---|---|---|
| No-TVC full aft + roll + reversal, 250 | 4.0 → **3.6°** | 8 → **10 °/s** | 0.33 → 0.44 | 2.87 → **1.00 s** |
| same, 340 | 4.4 → **4.2°** | 9 → **12 °/s** | 0.28 → 0.43 | 0 → 0.78 s |
| No-TVC push + roll, 300 | 2.9 → **2.7°** | 59 → **65 °/s** (40 °/s 0.33 s sooner) | 0.07 → 0.01 | — |
| TVC full aft + roll + reversal, 300 | 3.8 → 3.8° | 38 → 40 | 0.50 → **0.25** | 1.77 → **1.10 s** |
| same, 450 | 3.5 → 3.6° | 40 → 41 | 0.49 → **0.24** | 1.68 → **1.05 s** |
| Push + roll, 200 / 300 | 0.9 / 0.8 → 1.1 / 0.9° | 39 / 43 → 40 / 47 | 0.04 / 0.21 → 0.04 / 0.05 | 0.90 / 0.83 → 0.52 / 0.82 s |
| Released roll at low AoA, 300 | 2.3 → 2.3° | 40 → 38 | 0.14 → 0.04 | same |
| Released roll at low AoA, 450 | 3.9 → **4.6°** | 60 → 61 | 0.10 → 0.01 | 0.33 → 1.22 s |

\* fraction of the run with a canard past 28° of local AoA.
\*\* s after the last stick input until the canards are within 3° of each other.

- The no-TVC rolls are faster with the price. The allocator now spends canard flow where the moment pays (more time past 28° there), where the window held them inside it whatever the cost.
- **No-TVC push + roll curve:** smooth. Largest dip before the peak is 2.4 °/s, against 0.6 with the window, at a 99–101 °/s plateau. The canards stay within ±15° of local AoA, where the window let one reach −46°.
- **The regression is the low-AoA released roll at 450 km/h (+0.7°).** The canards stay in attached flow through the roll (local 10–14° instead of 30–48°). The roll builds and stops on the same curve (peak 137 vs 139 °/s). The extra sideslip is in the stop's tail.
- **Knee and weight benched:** slope 0.5 / K 0.01–0.03 (tighter band) cost roll-then-pull reversal 400 3.6 → 4.8°, held 0.4 + roll 1.7 → 2.2° and a sweep stop 0.7 → 1.9–2.3°. Slope 0.2 cost a clean leaf stop 1.3 → 2.2° and two rudder-gone push-overs. Slope 0.3 / K 0.03 is the shipped set.

## Native core
Unchanged. `NativeAllocatorCheck` 2 on the no-TVC roll with the canard bands active: 8,874 / 8,874 solves identical, 0 mismatches.

## Install
- `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 `b58277503b7e6e568632759d71a1d274`, read back from the game folder) and `meta.json`, version 1.6.0. 1.5.0 is kept as `.dll.1.5.0.bak`.
- Player zip sha256 `f5f96cf3cf839a9f894b2311221d0006611d1727fbb9cbb3e12d7bfd705112fa` (in the installed NOMM `meta.json`; the GitHub release is yours to make).
- Release: `C:\Games\NO mods\Revoker-FCS-1.6.0-release`.
