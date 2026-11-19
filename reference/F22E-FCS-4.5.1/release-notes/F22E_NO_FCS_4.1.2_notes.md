# F-22E Strike Raptor FCS 4.1.2: a loaded reversal toward the load's side builds once and keeps one acceleration

Base: 4.1.1. One control-law change, in the roll trajectory's reversal branch. It acts only with an asymmetric load, in a reversal toward the side the load pulls, at an AoA where the yaw is what bounds the reversal. `F22E_FcsCore.dll` is the same file as 4.1.0's.

**Changelog:** With an asymmetric load, a roll reversal toward the heavy side no longer brakes gently with the surfaces near neutral and then kicks into the new roll: the roll acceleration now builds once, from the stick flip, while the rudders cross over, and holds one value through the zero crossing up to the new rate.

**Status:**
- Compiled against the game's assemblies.
- Benched: 34 single loaded reversals and 54 loaded roll-spam runs against 4.1.1; the suite byte-identical to 4.1.1.
- Run on the game's own Mono runtime (under Wine): the plugin and the native DLL load, the kernels' self-test passes, the allocator returns the same bits as 4.1.0. The law change itself is not exercised there.
- Flown 22:50 (F22E_FCS_20261110_225014.csv); see the port handoff for the read-out.

## Tom (10 Nov)
- 19:34, after flying 4.1.1: "still not fully fixed, it is just smoother"

## The 4.1.1 flight (19:33 log, 52 s, 3,161 ticks; 4.1.1 confirmed in the game log)
Five reversals with full aft stick and the load on (lateral CG offset 0.22 m). Acceleration = the slope of the roll-rate command, deg/s².

| t (s) | kt | AoA | direction | braking (the 40 % limit) | new roll's peak, when after zero | peak / brake | ailerons: brake → peak | rudder command: brake → peak | sideslip max |
|---|---|---|---|---|---|---|---|---|---|
| 34.02 | 325 | 22.7° | with the load | 132 (147) | 261, 0.18 s | 1.97 | 6.4° → 24.6° | 7° → 35° (stop) | 3.2° |
| 45.60 | 304 | 18.9° | with the load | 141 (149) | 249, 0.20 s | 1.76 | 4.3° → 25° (stop) | 3° → 32° | 3.1° |
| 49.65 | 277 | 16.3° | with the load | 134 (139) | 222, 0.25 s | 1.66 | 4.6° → 25° (stop) | 3° → 24° | 3.9° |
| 44.08 | 309 | 15.7° | against the load | 191 (206) | 221, 0.17 s | 1.16 | 24.5° → 25° | 7° → 35° (stop) | 3.8° |
| 47.15 | 291 | 18.7° | against the load | 128 (138) | 182, 0.23 s | 1.43 | 23.6° → 25° | 20° → 35° (stop) | 2.9° |

- **What you saw is the first three rows.** Reversing toward the heavy side, the load and the roll damping stop the old roll by themselves: the ailerons sit at 4 – 6° for the whole brake (0.35 – 0.5 s). Then the new roll's acceleration is built after the zero crossing and the ailerons and flaps go to their stops within 0.2 s.
- 4.1.1 made that rise continuous (4.1.0 jumped in four ticks). It did not remove it: the brake still ran at 132 – 141 and the new roll still reached 222 – 261.
- Against the load there is no such pause: the roll surfaces are at 24 – 25° from the stick flip to the new rate.
- **My 4.1.1 bench table missed this.** It measured the acceleration in the 0.1 s after the crossing (149 – 203, "ratio 1.2 – 1.5"). The rise kept going and peaked at 0.18 – 0.22 s (264 – 278, 2.05 – 2.14 x the brake). The table now has the peak, its time, and the ailerons' and flaps' deflection while braking and at their largest; with those columns the bench shows what the flight shows (brake 123 – 136 against the flight's 132 – 141, peak 264 – 278 against 222 – 261, ailerons 6 – 8° → 24 – 25° against 4 – 6° → 25°).
- Timing in this flight: FCS tick 427 µs median.

## Cause
- The 2.1.6 limit holds a loaded brake / reversal to 40 % of the yaw left after the load. What the 40 % stands for is the rudders' travel: the load and the old roll hold them on one side, and they need 0.5 – 0.7 s at their servo rate to reach the other side's stop.
- That limit acted on the brake only. So the trajectory waited at 40 % while the rudders crossed, and built the new roll's acceleration afterwards. Toward the heavy side, where the brake needs almost no surface, that reads as "stop, then spike".

## The change
In `FcsController.cs`, for a reversal with a load, toward the side the load pulls:

| | 4.1.1 | 4.1.2 |
|---|---|---|
| the reversal's acceleration | 40 % of the yaw left, flat, during the brake; then free (the entry's) | one figure for brake and new roll: the acceleration the new roll starts with (the entry's), within 60 % of the yaw left |
| how it is reached | in 0.18 s (the trajectory's normal build-up), then held at 40 % | built over the time the rudders need to cross from where they are (measured positions, their servo rate): 0.5 – 0.7 s |
| after the zero crossing | rises to the entry's acceleration by half the new rate | stays at the same figure up to the new rate |

- **60 %** is not a new number: it is the trajectory's own yaw figure (0.6 of the yaw authority, `num16`), here taken of the yaw left after the load.
- **Where it acts:** the 2.1.6 load gate (lateral CG offset over 4 cm, full by 12 cm); the stick's side is the side the load takes no roll authority from (fades out as the load's trim takes a tenth of the roll authority on that side); the yaw figure is under the law's own reversal acceleration (at low AoA a roll asks little yaw and nothing changes); at least one rudder left.
- **Unchanged:** reversals against the load, loaded stops, every roll entry, clean jets, loads under 4 cm. No setting, no telemetry column, no stored state added. The bench switch `RevLoadRamp` (false = 4.1.1) is for comparisons only.
- The run-out onto the new rate keeps the trajectory's normal jerk (only the build-up is paced).

**This is still the 2.1.6 device, reshaped. It is still a limit on roll acceleration for the yaw's sake, which your rule of 31 Oct / 2 Nov says the governor should not have** (slow the commanded rate, not the acceleration). I benched removing it and four other shapes first; each cost more than this one:

| variant (2,000 kg, toward the load; then all 54 spam runs) | peak / brake | sideslip | stick flip → 90 % of new rate | spam: sideslip max, mean (worst) | what went wrong |
|---|---|---|---|---|---|
| 4.1.1 | 2.05 – 2.14 | 2.5 – 2.8° | 0.80 – 1.02 s | 6.66° (11.6) | the pause, then the rise |
| limit deleted (your rule as written) | 0.87 – 1.05 | 4.7 – 6.3° | 0.70 – 0.87 s | 8.72° (14.5) | the roll 44 – 46 deg/s behind its command; 2.2 s above 5° per spam run against 0.8 |
| flat at the entry's acceleration, both directions | 1.00 – 1.04 | 5.4 – 5.8° | 0.60 – 0.72 s | 8.40° (17.5) | against the load 0.8 – 2.3° → 3.0 – 5.1°; at 235 kt with 1,100 kg the governor held the roll at the crossing (a hitch), 8.1° |
| 40 % on the whole loaded trajectory, both directions | 0.99 – 1.00 | 1.6 – 2.2° | 1.10 – 1.35 s | 5.73° (11.0) | 0.3 s slower; 11 % less roll done in the spam runs |
| 55 % on the whole loaded trajectory, both directions | 1.00 | 2.0 – 2.4° | 0.80 – 0.98 s | 7.00° (12.7) | governor retreats 47 → 89; one 1,100 kg case 8.0° |
| **4.1.2** | **1.00 – 1.05** | **2.0 – 2.7°** | **0.75 – 0.93 s** | **6.34° (11.05)** | 1,100 kg at 26° AoA: +1° sideslip (below) |
| 4.1.2 with 80 % instead of 60 % | 1.11 – 1.23 | 3.4 – 4.1° | 0.63 – 0.80 s | 6.68° (11.5) | +1.4° per reversal to gain 0.12 s; no longer one acceleration |

- The reason is physical: the rudders start on the wrong side and need their travel time. A reversal that brakes hard from the first tick outruns the yaw by 16 – 24 deg/s and leaves 5 – 6° of sideslip (bench).
- The clean jet's own reversal does exactly that on the same bench (20 – 22° AoA, 305 – 315 kt: brake 590 – 640, sideslip 7.0 – 8.8°, the governor holding the roll mid-reversal). I did not touch it.

## Bench (.NET 8), 4.1.1 → 4.1.2
**Reversals toward the load's side, full aft stick.**

| | 2,000 kg (0.21 m), 294 – 311 kt, AoA 21 – 22°, 3 cases | 1,500 kg (0.16 m), 239 – 255 kt, AoA 20.5°, 4 cases, settled pull | 1,100 kg (0.12 m), 204 – 233 kt, AoA 26°, 4 cases, settled pull |
|---|---|---|---|
| braking (average) | 123 – 136 → 185 – 192 | 88 – 98 → 133 – 146 | 54 – 72 → 81 – 102 |
| at the zero crossing | 123 – 136 → 185 – 202 | 88 – 98 → 131 – 147 | 55 – 73 → 64 – 75 |
| rise in the 0.2 s after the crossing | +129 … +141 → −4 … 0 | +62 … +75 → −1 … +1 | +42 … +62 → +10 … +20, one case +99 |
| new roll's peak / brake | 2.05 – 2.14 → 1.00 – 1.05 | 2.05 – 2.11 → 0.99 – 1.01 | 2.30 – 2.61 → 0.99 – 1.03, one case 1.82 (the hitch below) |
| ailerons at the crossing | 7.7 – 8.6° → 16 – 18° | 8.3 – 8.4° → 16 – 17° | 3.6 – 8.0° → 19° |
| sideslip peak | 2.5 – 2.8° → 2.0 – 2.7° | 2.2 – 2.4° → 2.2 – 2.5° | 2.3 – 5.5° → **3.3 – 6.5°** |
| roll behind its command, max | 11 – 12 → 9 deg/s | 12 – 14 → 9 – 10 | 10 – 13 → 10 – 11, one case 20 |
| stick flip → roll rate zero | 0.48 – 0.65 → 0.42 – 0.53 s | 0.85 – 0.92 → 0.65 – 0.70 s | 1.05 – 1.23 → 0.87 – 1.02 s |
| stick flip → 60 % of the new rate | 0.70 – 0.90 → 0.63 – 0.78 s | 1.23 – 1.32 → 1.05 – 1.13 s | 1.37 – 1.55 → 1.23 – 1.50 s |
| stick flip → 90 % | 0.80 – 1.02 → 0.75 – 0.93 s | 1.57 – 1.75 → 1.33 – 1.42 s | 1.53 – 1.67 → 1.45 – 1.82 s, one not reached in the run |

- **The curve, 2,000 kg at 311 kt (`s1_600_R`), every 0.05 s from the stick flip:** 45, 70, 95, 121, 150, 176, 199, 203, 202 (zero crossing), 201, 200, 199, 198, 196, 184, 170, 156, 142 (running out onto the new rate). 4.1.1: 45, 84, 122, 138, 137, 137, 136, 135, 135, 134 (crossing), 153, 189, 233, 278, 251, 204.
- **The ailerons in the same case:** −18, −13, −8, −3, +2, +7, +12, +16, +18 (crossing), +19, +19, +21, +22, +23, +25, +25, +25, +25, then back to +5 at the steady rate: one sweep from side to side. 4.1.1: −18, −13, −8, −3, +2, +7, +8, +8, +8, +8, +9 (crossing), +12, +17, +22, +25.
- **The rudders:** a steady sweep from +15° to the other stop at about 80 deg/s, reaching it near the end. 4.1.1: 0.5 s to −12°, then to the stop during the rise.
- **1,100 kg at 26° AoA is worse in sideslip: +0.9 … +1.0° in all four settled cases** (2.3 / 3.7 / 3.8 / 5.5 → 3.3 / 4.7 / 4.7 / 6.5°). There the rudders are already at 25 – 35° before the flip and the yaw has little left; they then sit at a stop for 0.5 – 0.9 s of the reversal against 0.1 – 0.2 s.
- **One of those four has a hitch 4.1.1 did not have** (233 kt): the yaw falls 16 deg/s behind, the governor holds the roll target for 0.4 s at the crossing, and the acceleration goes 107 → 77 → 185. In 4.1.1 that case was the smooth 71 → 166. It is 1 of the 17 toward-the-load cases.
- Part aft stick (2,000 kg, 365 kt, AoA 11°): brake 391 → 434, peak / brake 1.13 → 1.01, sideslip 4.2 → 4.2°. At 443 kt, AoA 8°: byte-identical (the yaw does not bound it).
- 700 kg at AoA 51° (2 cases): sideslip 4.1 / 5.4 → 4.0 / 5.2°, the rest within noise.

**Reversals against the load: 17 of 17 cases byte-identical to 4.1.1.**

**Roll spam, 54 runs** (1,100 / 1,500 / 2,000 kg; three speeds; three flip timings; both starting directions; full aft held). Mean (worst):

| | 4.1.1 | 4.1.2 | better / same / worse, run by run |
|---|---|---|---|
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 29 (48) | 19 (54) | 48 / 2 / 4 |
| sideslip max | 6.66° (11.6) | 6.34° (11.05) | 22 / 16 / 16 |
| sideslip 95th percentile | 5.71° (10.5) | 5.50° (10.2) | 24 / 11 / 19 |
| seconds above 5° sideslip | 0.82 (2.35) | 0.68 (2.02) | 21 / 22 / 11 |
| roll behind its command, rms | 8.54 (10.9) | 8.35 (10.9) deg/s | 20 / 27 / 7 |
| governor retreats of the rate target with the stick held (total) | 47 | 49 | 10 / 34 / 10 |
| bank angle travelled | 289.5° | 296.2° | 33 / 10 / 11 |
| AoA above the asymmetric cap, max | 8.43° (20.0) | 8.40° (20.0) | 1 / 53 / 0 |

- Sideslip max by load: 1,100 kg 7.90 → 7.19°, 1,500 kg 7.29 → 6.78°, 2,000 kg 4.80 → 5.07°.
- Largest single worsening 3.1 → 6.4° (2,000 kg, one run); largest improvement 11.6 → 8.6°.
- Loaded spam still sits at 6 – 11° of sideslip against a 4° margin. Not addressed here.

**Identity:**
| check | result |
|---|---|
| suite, 4.1.2 against 4.1.1 | 153 / 153 per-case files identical; 5 / 5 tables identical apart from the tag line |
| the 72 loaded runs of 4.1.1's set with `RevLoadRamp = false`, against the 4.1.1 build | 72 / 72 identical |
| clean reversal sequences at 350 / 450 / 520 / 700 km/h | 4 / 4 identical |
| loaded stop (2,000 kg), 300 kg store (offset 0.040 m), loaded reversal with the 2.1.6 limit off | identical |
| plugin built twice | same checksum |

**Game's Mono (Wine), the shipped plugin DLL with 4.1.0's `F22E_FcsCore.dll`:** loads; kernels' self-test passes; C# allocator on 26,700 recorded solves: every output bit equal to 4.1.0; native allocator 26,700 / 26,700 identical to the bench's C#.

## Bench tool
- `bench/an/loadrev.sh <tag> [Name=value]`: now 34 single reversals (16 new: 1,100 / 1,500 kg from a settled pull, the roll 6 s after it) and the 54 spam runs. `_R` cases reverse toward the load's side, `_L` against it.
- `loadrev.py`: new columns (peak, when, peak / brake, ailerons and flaps while braking and at their largest).
- `bench/an/loadrev_cmp.py <dirA> <dirB>`: run-by-run better / same / worse counts.
- Still not part of `suite.sh`.

## Open
- 1,100 kg at 26° AoA: +1° sideslip in a reversal toward the load, and the governor's hold in one case (above). The lever for that corner is to let the 60 % come down with how far out the rudders already are when the stick flips; not done, it needs one stored value per reversal.
- Reversals against the load keep 4.1.1's shape (flight: peak / brake 1.16 – 1.43, surfaces at their stops throughout).
- Your rule on roll acceleration: the load's limit stays, in the new shape. Removing it needs the governor's rate cap to carry a loaded reversal (bench, limit deleted: 8.7° mean in spam).
- From the earlier flight checks, unchanged: loaded roll spam at 6 – 11° sideslip; the bench overshooting the asymmetric cap in the pull before the roll; the command ripple in the held post-stall roll (cause not found); the held-roll aileron alternation near 30° AoA; the pitch-rate overshoots (parked).
- The five other FCS mods (Ifrit 1.0.0, Revoker 1.0.0, Compass 0.2.0, Ternion 0.2.0, Su-30 0.3.3) and the NO-FCS-Kit 2.0 reference sources carry the 4.1.0 reversal code. Not ported.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.1.2 + `meta.json`. `F22E_FcsCore.dll` is unchanged (4.1.0's file). 4.1.1 is kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.1.1.bak`.
- **Dev:** unchanged (the 3,584-byte DLL). No Dev default changed, no migration.
- **Release:** `C:\Games\NO mods\F22E-FCS-4.1.2-release`. The mod zip holds the plugin DLL, `F22E_FcsCore.dll` and `meta.json`.
- **Mod manager:** update the version there, or its modpack may put an old DLL back.
- **To check in flight:** an asymmetric load, full aft, roll away from the heavy side, reverse. The ailerons should sweep straight through to the new side (16 – 18° at the moment the roll stops) instead of resting near neutral and then jumping; in the CSV the roll-rate command's slope should build once and stay level through its zero crossing.
