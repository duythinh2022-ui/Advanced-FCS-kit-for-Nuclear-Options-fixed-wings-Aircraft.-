# F-22E Strike Raptor FCS 4.1.1: a loaded roll reversal at one acceleration (no stop, then spike)

Base: 4.1.0. One control-law change, inside the roll trajectory's reversal branch. It acts only in a roll reversal with an asymmetric load. `F22E_FcsCore.dll` is the same file as 4.1.0's.

**Changelog:** With an asymmetric load, a roll reversal no longer brakes gently and then jumps to full roll acceleration where the roll rate crosses zero: the reversal's acceleration now includes the load's yaw limit, so the new roll starts where the brake left off and builds smoothly.

**Status:**
- Compiled against the game's assemblies.
- Benched: 18 single loaded reversals and 54 loaded roll-spam runs against 4.1.0; the suite byte-identical to 4.1.0.
- Run on the game's own Mono runtime (under Wine): the plugin and the native DLL load, the kernels' self-test passes, the allocator returns the same bits as 4.1.0. The law change itself is not exercised there.
- **Not flown.**

## Tom (10 Nov)
- 18:14: "when i roll while holding pitch, then i reverse roll, the control surfaces stop the roll then they suddenly spiked in deflection to reverse the roll."
- 18:53: "Build the fix"
- 19:05: "make inherent fix not on top"

## Cause (flight check `claude/F22E_FCS_4.1.0_flight_check_2026-11-10.md`)
Two rules of the roll trajectory lived in two places:
- **1.7.4:** a reversal is flown at one roll acceleration, `revA`, "capped by what the yaw axis can follow", and the new roll starts at that acceleration, blending to the normal entry acceleration by half the new rate.
- **2.1.6:** with an asymmetric load, a brake or reversal is limited to 0.4 x the yaw left after the load / sin AoA. This sat in a block after the reversal branch and acted only while braking; `revA` was stored before it.

So a loaded reversal braked at the limit and started its new roll at the unlimited `revA`. In the 18:10 flight: 138 – 168 deg/s² braking, 328 – 414 within 0.1 s of the zero crossing, the ailerons from 7 – 13° to their stops in four ticks.

## The change
The load's limit is now part of the reversal's one acceleration, as 1.7.4 describes it. In `FcsController.cs`, the brake / reversal branch:

| | 4.1.0 | 4.1.1 |
|---|---|---|
| the load's limit on a brake (a stop's or a reversal's) | applied in a separate block after the branch | applied inside the branch; the same value |
| `revA`, the reversal's acceleration carried into the new roll | stored before the load's limit | the acceleration the brake flies, limit included |
| the carry (new roll starts at `revA`, blends to the entry acceleration by half the new rate) | could only raise the entry acceleration (`max`) | blends from `revA` from either side |

- Nothing is added: no setting, no state, no constant, no telemetry column. The separate block is gone.
- The bench switch `RevOneAccel` (a static, false = 4.1.0) is for comparisons only.
- My first build of this was an add-on (the limit extended past zero with its own state, blend span and a Dev setting). Replaced after 19:05; it never left the bench. The version above is also the smoother one (acceleration step at zero 13 – 44 against the add-on's 32 – 46).

**Unchanged:** the brake of a loaded reversal, a loaded stop, every roll entry, clean jets, loads under 4 cm of lateral CG offset.

## Bench (.NET 8), 4.1.0 → 4.1.1
**Single reversals, full aft stick, store on the left rear wing station, both directions.** Acceleration = the roll trajectory's, deg/s².

| | 2,000 kg (offset 0.21 m), 151 – 162 m/s, AoA 21 – 24°, 6 cases | 1,100 kg (0.12 m), 120 – 130 m/s, AoA 26 – 27°, 4 cases |
|---|---|---|
| braking (unchanged) | 123 – 136 | 76 – 89 |
| 0.1 s after the zero crossing | 272 – 383 → 149 – 203 | 160 – 269 → 87 – 120 |
| largest rise in 3 ticks at the crossing | 119 – 131 → 13 – 44 | 70 – 85 → 8 – 21 |
| largest rise in 3 ticks anywhere up to 95 % of the new rate | 119 – 131 → 14 – 52 | 70 – 85 → 10 – 28 |
| stabs' differential travel in the 0.1 s after the crossing | 6.5 – 9.1° → 1.9 – 3.6° | 1.6 – 7.6° → 1.4 – 2.6° |
| sideslip peak | 1.2 – 3.2° → 0.8 – 2.8° | 1.7 – 3.5° → 1.3 – 3.5° |
| roll behind its command, max | 13 – 20 → 6 – 12 deg/s | 8 – 19 → 7 – 12 deg/s |
| stick flip to 60 % of the new rate | 0.67 – 1.07 s, +0.00 … +0.05 s | 0.90 – 1.33 s, −0.02 … +0.09 s |
| stick flip to 90 % | 0.77 – 1.53 s, −0.05 … +0.04 s | 1.13 – 1.83 s, −0.02 … +0.05 s |

- The curve, 520 km/h case, one direction (`s1_520_R`): 130 (brake) → 133 → 151 → 171 → 195 → 221 → 252 → 278 (0.2 s after zero) → 253 → 227 → 200 … ; 4.1.0: 130 → 151 → 234 → 317 → 371 → 312 → 260.
- The other direction (`s1_520_L`): 4.1.0 spiked to 278 and then stood still (rate command 43.8 / 43.2 / 43.4 / 43.6 over 0.1 s, roll rate 38.7 / 39.1 / 38.6 / 38.7); 4.1.1 goes 126 → 133 → 142 → 152 → 161 → 153 → 135 → 116 → 96 with the roll rate rising throughout.
- 2,000 kg at part aft stick (AoA 11 – 12°, 188 m/s, 2 cases): 377 / 639 → 289 / 411 after zero; the rise at the crossing 31 / 176 → −4 / 16.
- 2,000 kg at 228 m/s, AoA 8° (2 cases): the limit is above the 720 deg/s² ceiling; 706 before and after in both, 60 % of the new rate 0.02 s later in one case.
- 700 kg at AoA 51° (4 cases): the limit does not bind; byte-identical.

**Roll spam, 54 runs** (1,100 / 1,500 / 2,000 kg; 480 / 520 / 600 km/h; three flip timings; both starting directions; full aft held). Mean (worst):

| | 4.1.0 | 4.1.1 | better / same / worse |
|---|---|---|---|
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 107 (137) | 29 (48) | 54 / 0 / 0 |
| sideslip max | 6.4° (10.9) | 6.7° (11.6) | 17 / 12 / 25 |
| sideslip 95th percentile | 5.5° (9.8) | 5.7° (10.5) | 23 / 7 / 24 |
| seconds above 5° sideslip | 0.70 (2.31) | 0.82 (2.35) | 15 / 18 / 21 |
| roll behind its command, rms | 9.4 (11.7) | 8.5 (10.9) deg/s | 44 / 4 / 6 |
| governor retreats of the rate target with the stick held (total) | 57 | 47 | 13 / 35 / 6 |
| bank angle travelled | 297° | 290° | 3 / 35 / 16 |
| AoA above the asymmetric cap, max | 8.4° (20.0) | 8.4° (20.0) | 2 / 48 / 4 |

- **Sideslip in rapid spam is not better: +0.3° on the mean, +0.7° on the worst case.** With no spike the governor cuts the rate target less often (57 → 47 retreats), so the roll carries a little more rate into the next flip. By load: 1,100 kg 7.4 → 7.9°, 1,500 kg 6.7 → 7.3°, 2,000 kg 5.0 → 4.8°.
- Loaded spam sits at 6 – 11° of sideslip in both versions, against a 4° margin (`RollStopSideslip`). Not touched here.

**Identity:**
| check | result |
|---|---|
| suite, 4.1.1 against 4.1.0 | 153 / 153 per-case files and 5 / 5 tables identical (the suite has no loaded reversal) |
| the 72 loaded runs with `RevOneAccel = false`, against the 4.1.0 build | 72 / 72 identical |
| clean reversal sequences at 350 / 450 / 520 / 700 km/h | 4 / 4 identical |
| loaded stop (2,000 kg), 300 kg store (offset 0.040 m), loaded reversal with the limit off | identical |

**Game's Mono (Wine), the shipped plugin DLL with 4.1.0's `F22E_FcsCore.dll`:** loads; kernels' self-test passes; C# allocator on 26,700 recorded solves: every output bit equal to 4.1.0; native allocator 26,700 / 26,700 identical to the bench's C#.

## Your rule on roll acceleration
- 31 Oct and 2 Nov: the governor slows the commanded roll rate only, never the roll acceleration or deceleration, to wait for yaw.
- The load's limit of 2.1.6 is older than that rule and is an acceleration limit for the yaw. 4.1.1 keeps it and makes it one figure through the reversal; it does not remove it.
- Benched without it (`LoadRevYawK = 0`), same 54 spam runs: sideslip max 6.4 → 8.7° (worst 14.5), seconds above 5° 0.70 → 2.23, roll behind its command 26 → 40 deg/s (worst 68). Single reversals at 2,000 kg: sideslip 3.3 – 6.3° against 0.8 – 2.8°, the roll up to 46 deg/s behind.
- So with the governor as it is, the load's limit does real work. Removing it by the rule needs the governor's rate cap to carry the load in a reversal first. Not attempted here.

## Open
- Loaded roll spam at 6 – 11° sideslip (above).
- The bench overshoots the asymmetric cap in the pull before the roll (43° against a 27° cap at 1,100 kg; same in 4.1.0). The 18:10 flight held the cap within −1.4 … +0.75°. Bench fidelity with a store; not looked into.
- The suite has no loaded reversal, which is why this defect passed it since 2.1.6. `bench/an/loadrev.sh <tag>` (the 72 runs above, tables by `loadrev.py`) is new in the source; it is not yet part of `suite.sh`.
- From the 4.1.0 flight check, unchanged: the command ripple in the held post-stall roll (cause not found; `NativeTables` / `NativeSweep` switch test), sideslip 17.6 – 18.8° in two coupled manoeuvres, the held-roll aileron alternation near 30° AoA, the pitch-rate overshoots (parked).

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.1.1 + `meta.json`. `F22E_FcsCore.dll` is unchanged (4.1.0's file). 4.1.0 is kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.1.0.bak`.
- **Dev:** unchanged (the 3,584-byte DLL). No Dev default changed, no migration.
- **Release:** `C:\Games\NO mods\F22E-FCS-4.1.1-release`. The mod zip holds the plugin DLL, `F22E_FcsCore.dll` and `meta.json`.
- **Mod manager:** update the version there, or its modpack may put an old DLL back.
- **To check in flight:** an asymmetric load, full aft, roll, reverse. In the CSV the roll-rate command's slope should rise smoothly after its zero crossing instead of doubling within 0.1 s.
