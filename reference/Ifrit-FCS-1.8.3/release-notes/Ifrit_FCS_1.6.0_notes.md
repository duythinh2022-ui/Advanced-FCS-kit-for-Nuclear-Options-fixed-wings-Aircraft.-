# KR-67 Ifrit FCS 1.6.0: F-22E 4.5.2/4.5.3 port, surfaces paired by position, canard law (Multirole MTD)

Base: 1.5.0. Minor bump: this is a law change.

**Status:** compiled against the game's assemblies and benched (stock Ifrit, plus a Multirole MTD airframe built from the blueprint). **Not flown.** 1.5.0 is kept as `.dll.1.5.0.bak`.

## Tom (23 Nov)
- 20:46: with the Ifrit MTD installed, "does the fcs reach out to the canards and figure it out, or the canards uses the vanilla pid".
- 20:55: "sometimes, the canards are stuck in roll for a few sec after the jet leveled off".
- 21:09: "should the canards code be get rid off, and make the allocator figure it out and still at least as well with the canards code".
- 21:16: "replace what can be replaced, improve the canards law, and port the allocator fix from raptor 4.5.3". Scope picked: Ifrit + Revoker, 4.5.2 + 4.5.3.

## What was found
- **The FCS already flies the MTD.** `Registry` attaches on an aircraft name that contains "KR-67". The MTD's canards are copies of the tails named `elevator_L (1)` / `elevator_R (1)`, so they come in as stabilators with their own moment tables. The allocator learns the forward surface's opposite pitch sign from geometry: in the 21 Nov 12:42 log, symmetric deflection against qCmd correlates −0.42 for the canards and +0.43 for the tails.
- **The MTD is more than canards.** Inner wing panels 10 → 22 m², outer 7 → 10, tails 6 → 10, canards 15 m² each (blueprint diff).
- **The stuck canard (log 39–42 s and 77–80 s).** With the stick centred and the roll stopped, one canard held 15–40° of deflection at local AoA −19 to −30° for 2–3 s.
  - The settled window was on, but its edge for this foil is near ±39° local (`Multirole1_elevators` has no stall before 45°). So the canard sat inside it, on the low-slope part of the curve: a fifth of the linear lift slope from 20 to 45°.
  - There, deflection barely changes the moment, so the allocator had almost no reason to move it. Only the 2e-4 deflection cost pulled it home.
- **The stock tails were never paired.** Pairing required opposite roll-range signs. The Ifrit's ruddervators have roll range 0 on both, and `Mathf.Sign(0)` = +1 on both sides. So the stab-pair costs (trim cost, differential cost, lateral share) have never run on this jet. Its tails have always been two independent effectors. This build keeps it that way (see below).

## Changes
1. **F-22E 4.5.2 `PaceRollSatDelivered`.** With the allocator reporting the roll saturated, the governor's roll capacity is what is being delivered, not the decaying memory of earlier roll accelerations.
2. **F-22E 4.5.3 `PlanFollowsJet`.** Braking a pitch rate toward the command, the plan may not run further ahead of the jet than 0.1 s of its own deceleration (at most 8 deg/s). New telemetry column `planHeld`.
3. **F-22E 4.5.3 `LatShareNoGate`: ported but OFF on this jet (airframe reason).** The share frees the stabilators for roll while the nozzles take the pitch. On the Ifrit the tails are the yaw, and the roll is the wing and the roll TVC. On vs off (bench): push-then-pullthrough + roll 350 5.5 → 8.2°, pullthrough with the roll released 5.6 → 6.8°, full aft + roll + reversal 320 4.1 → 4.8°. Better in one case (push-then-pullthrough 450, 7.9 → 7.3°). The `LateralStabReserve` text is updated to the 4.5.3 wording.
4. **Surfaces paired by position (`GeoPairing`).** Partners are found by mirror position (same kind, opposite side, same station), not by the `_L` / `_R` name ending. The pair sign comes from roll where the roll ranges have opposite signs. Yaw is used only for rudders, or with `PairYawSign` / `PairYawCanard` (both off).
   - On the stock Ifrit this finds exactly the old pairs (ailerons, flaps): the suite is identical with or without it.
   - Pairing the tails by yaw sign was benched and **not** shipped. It gave one-ruddervator damage cases 10 → 2° but clean push-overs 5.6 → 10.3°.
   - Pairing the MTD's canards by yaw sign was also worse (sideslip +0.2–0.6° in no-TVC rolls).
   - Log line: `Ifrit FCS surface pairs (by position): …`.
5. **Canards by position (`GeoCanard`).** A stabilator ahead of the mean station of the wing's aileron/flap surfaces is a canard (`isCanard`). The name rule ("canard" in the name) remains only where a jet has no wing surfaces to compare with.
6. **Canard law: `StallCost` (0.03, knee at lift slope 0.3 of its steepest, canards only).** Each tick, each canard's band is the deflection range that keeps its local AoA within the foil's knee at this tick's flow. Outside it, every degree costs `StallCostK × (deg / range)²` through the allocator's anchor/band term, the native core's own (as the nozzle budget uses).
   - It is a price, not a window, gate or walk: when the moment is worth it, the allocator still pays it. No efficient range in the travel (deep stall at the body AoA): no price.
   - Not on the tails: on every stabilator it cost sideslip in every corkscrew (0.6–1.3°). The Ifrit runs its tails deep on purpose, as yaw and drag surfaces.
   - New telemetry column `stallCost`: the worst excess this tick (deg).

Internal statics (bench switches, not config keys): `PaceRollSatDelivered`, `PlanFollowsJet` (on), `LatShareNoGate` (off), `PlanLeadT` 0.1, `PlanLeadMax` 8, `GeoPairing`, `GeoCanard` (on), `PairYawSign`, `PairYawCanard` (off), `StallCostK` 0.03, `StallCostSlope` 0.3, `StallCostAllStabs` (off).

## Bench: stock Ifrit, 1.5.0 → 1.6.0 (full suite)
The stock jet has no canards, so only changes 1–3 act on it.

**Regression set:**
- better: pullthrough with the roll released at zero 11.6 → 5.6°; push-then-pullthrough + roll 350 10.1 → 5.5°, 450 10.5 → 7.9°; roll-then-pull reversal 400 8.2 → 5.9°; pull-then-push-over + roll 350 8.6 → 7.1°.
- worse: held 0.4 + roll 350 5.6 → 7.7°. The roll is also faster here (peak 116 vs 105 °/s; 151 vs 109 °/s at 2.8 s). A 30 °/s dip at 2.2 s is in both builds, deeper in 1.5.0. `planHeld` is 0 through the roll: the two runs differ in how the pull enters it.

**Stop/reversal sweep:**
- better: 650/0.70 stop 7.2 → 5.3°; 500/0.70 reversal 4.5 → 3.3°; 650/0.70 reversal 8.5 → 7.8°.
- worse: 300/0.40 reversal 5.5 → 7.9°; 300/0.55 reversal 3.7 → 4.3°.

**Corkscrews:**
- better: pull 1.0 + roll at 0, 350, 3.6 → 2.5°; pull 0.6 + roll at 0.3, 350, 6.5 → 5.6°.
- worse: roll-then-pull 1.0, 450, 3.9 → 5.0°; pull 1.0 + roll at 0, 450, 5.7 → 6.3°.

**Damage suite:** 9 better, 0 worse. Held push-over 560 with one ruddervator 14.8 → 10.0°, clean 8.3 → 5.5°; released push-over 600 clean 6.2 → 4.1°; held 600 with one ruddervator 4.9 → 3.1°; the rest by 0.6–1.5°.

**Which change does what** (each switched off alone on 1.6.0):
- `PlanFollowsJet` carries most of the gain. Off: 6 regression and 8 damage cases worse by up to 4.6°. It is also what costs the held 0.4 + roll and the 300/0.40 reversal.
- `PaceRollSatDelivered` is neutral to slightly better (300/0.40 reversal 6.9° with it off).

## Bench: Multirole MTD (canard case set, 1.5.0 law + port → 1.6.0)

| Case | Sideslip | Velocity-vector roll | Canard stalled* | Canards back together** |
|---|---|---|---|---|
| No-TVC full aft + roll + reversal, 250 | 4.4 → **3.1°** | 17 → 16 °/s | 0.27 → **0.11** | 1.72 → **0.90 s** |
| same, 340 | 3.8 → 3.9° | 18 → 18 | 0.24 → **0.12** | 1.58 → 1.78 s |
| Released roll at low AoA, 300 | 4.4 → 4.2° | 93 → 93 | 0 | 1.00 → **0.63 s** |
| TVC full aft + roll + reversal, 300 / 450 | 6.6 / 8.9 → 6.6 / 8.9° | same | 0.74 / 0.69 → 0.72 / 0.68 | same |
| Push + roll, pedal kick, release at 450 | same within 0.1° | same | — | same |

\* fraction of the run with a canard past 28° of local AoA.
\*\* s after the last stick input until the canards are within 3° of each other.

- Curve roughness (roll acceleration, tick to tick) is the same within 2 % in every case.
- At 65° AoA (TVC loaded rolls) the canards cannot be unstalled within their travel, so the price does nothing there.
- **Not reproduced on the bench:** the two log windows replayed closed-loop (free and pitch-pinned) do not get the canard stuck with either law. This bench MTD airframe is uncalibrated: it is built from the blueprint with the stock Ifrit's calibration. The fix rests on the log analysis above and on the case set, not on a replay of the event.

## Native core
Unchanged. The stall cost uses the existing anchor/band term. `NativeAllocatorCheck` 2 on the MTD no-TVC roll with the canard bands active: 8,675 / 8,675 solves identical, 0 mismatches.

## Bench tooling (in the Source zip)
- `bench/sim/Airframe.cs`: surfaces beyond the stock six are loaded by their game names (MTD canards).
- `tools/mtd_build.py`: builds `bench/extract/mtd_airframe.json` from `Multirole_MTD_0.0.7.nobp` and the stock Ifrit airframe. The JSON itself is not included (third-party data).
- `tools/canset.sh` / `canscore.py` / `cancmp.py`: the canard case set. `tools/cmp.py`: suite comparison.
- `bench/stubs/Game.cs`: `BENCH_ECHO=1` prints the FCS log lines.

## What to look for in game
- `LogOutput.log`: `Ifrit FCS surface pairs (by position): aileron_L+aileron_R, flap_L+flap_R.` On the MTD the canards are not listed (left unpaired on purpose). The attach line lists 8 surfaces.
- MTD: the canards should come back together within about a second after a roll, and not hang at large deflection with the stick centred. Telemetry `stallCost` > 0 means a canard is being priced back toward efficient flow.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `192036283dc08e5ce027a8406246812e`, read back from the game folder) and `meta.json`, version 1.6.0. 1.5.0 is kept as `.dll.1.5.0.bak`.
- Player zip sha256 `ed8a1a9cbcb1fe0e5d955751ed90658630ac57c5446535ddb73b5b9b7509abb9` (in the installed NOMM `meta.json`; the GitHub release is yours to make).
- Release: `C:\Games\NO mods\Ifrit-FCS-1.6.0-release`.
