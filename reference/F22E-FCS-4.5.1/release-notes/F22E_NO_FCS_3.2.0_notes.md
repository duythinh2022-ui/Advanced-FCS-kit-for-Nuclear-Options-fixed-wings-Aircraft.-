# F-22E Strike Raptor FCS 3.2.0: airframe X2 (relaxed to neutral, stab lift peak 36°)

Base: 3.1.9. Minor bump: an airframe change. No control-law code changed.

**Status:**
- Compiled against the game's assemblies.
- Benched on the calibrated plant (below). Single runs unless an ensemble is stated; the ensembles are 5 members (entry speed ±0.1 / 0.2 %).
- **Not flown.**

## Tom (9 Nov)
- 13:39: "How about the simulated lerx vortex, getting rid of downwash modeling, 36 aoa as max lift curve for stabs. And the relaxed to neutral margin i talked about"
- 13:44: "The goal is, roll behavior as i stated. And tvc with stabs only need to give the jet a nudge to put it into a turn then go back to neutral or at least close to it, as close as possible. That behavior holds up to 36 aoa, then the jet must maintain useful nose up and down authority upto 45 aoa aerodynamically with the stabs doesn use the majority of its deflection to hold that aoa."
- This is phase 1 of `claude/F22E_airframe_X2_handoff_2026-11-09.md`: X2 = downwash kept at 0.6, stab peak 36, plus the trim-free fore/aft lift schedule. The no-downwash package (T) failed the 36–45° nose-down goal on the bench (see the handoff §2/§6).

## Read this first: two regressions the X2 target list did not cover
Both hold up in 5-member ensembles.

**1. Low-throttle AoA overshoot past the 45° cap.** In a hard pull at low throttle from ~400 km/h, 3.1.9 stops at 45°. 3.2.0 runs to 54–56°, then comes back to 45 within about 1.5 s.

| case (max AoA) | 3.1.9 | 3.2.0 |
|---|---|---|
| full pull + full roll, throttle 0.15, 400 km/h (cork set) | 45.2 (45.2–45.2) | **53.9** (53.4–55.2) |
| pull then roll, 400 km/h (reg set) | 45.4 (45.0–46.1) | **55.9** (54.9–56.5) |
| full pull, idle, 400 km/h, no roll | 45.1 | 49.2 |
| hold 45 at idle, 300 km/h | 45.5 | 50.4 (holds 46–47 after) |

Your rule was that ~46 is acceptable at the 45 limit, so this breaks it.

Why it happens (bench traces):
- **The stab's small-angle lift slope is 0.69× 3.1.9's.** It keeps its peak CL at 36 instead of 25. So the pull puts the stabs at +29 instead of +21, and reversing them to nose-down takes about 0.1 s longer at 100°/s. The pitch rate at 30° AoA is 64°/s instead of 54.
- **No natural nose-down to help the brake.** That is what "trim-free" means. 3.1.9's airframe adds −6 to −18°/s² at 85 m/s from 20 to 36°, and −31 at 48°. X2 adds nothing up to 48°.
- **Past 45° X2's nose-down authority drops off.** Full nose-down stab at 85 m/s is −24 at 44° and −16 at 48° (3.1.9: −34 / −31). An overshoot lands where there is the least brake.
- **The same overshoot causes the late roll start at idle.** Loaded roll at 36°, 300 / 250 km/h idle: the jet is at 42–43° when the roll starts and is still pushing the nose down. The roll rate 0.5 s after the stick is 2 / 0°/s (3.1.9: 17 / 12). Full rate comes by about 1.3 s.

**2. Lower roll rate at moderate AoA.** This is your 41 %-style mid-AoA region, not the 36° loaded roll the study measured.

| body roll rate before the reversal (ensemble median, deg/s) | AoA | 3.1.9 | 3.2.0 |
|---|---|---|---|
| half pull + roll, 700 km/h | 11° | 204 | **156** (−24 %) |
| half pull + roll, 556 km/h | 17° | 140 | **116** (−17 %) |
| full pull + roll, reversed at 0.7 s, 650 km/h | 14° | 153 | 139 (−9 %) |
| half pull + roll, 400 km/h | 27° | 71 | 69 |
| 0.4 pull held + roll, 350 km/h | 35° | 30 | **22** (−27 %) |
| pull then roll, 320 km/h: roll-onset peak | 44° | 34 | **18.5** |

**Which part does it (single runs, 3.2.0 code):**

| case | 3.1.9 airframe | only the stab peak 36 | only the schedule | X2 (both) |
|---|---|---|---|---|
| half pull 556 | 139 | 136 | 140 | 116 |
| half pull 700 | 204 | 168 | 182 | 140 |
| 0.4 pull + roll 350 | 31 | 29 | 25 | 22 |

- Each part costs roll rate on its own (most at 700 km/h: −18 % from the stab peak alone); together they cost more.
- This fits the handoff's open item 2: the roll's coupling limit pays the roll's pitch-up out of the available nose-down acceleration. The neutral airframe brings none of its own.
- Stab lift slope alone is not it: StabilatorCLScale 1.44 (the 25° stab's slope) left these within 1 % except the chaotic 700 km/h case.
- 1 g rolls are unchanged (1 g roll rev 500 / 800: 210 / 250 deg/s both).

## Change
1. **Fore/aft lift schedule** (`BodyLift.cs`):
   - **Where:** AoA-scheduled lift multipliers on the fuselage sections ahead of the CG (LERX, intakes, fore, cockpit, centre body) and behind it (engine bays, tail booms, tail).
   - **How it applies:** on top of `BodyLiftFore` 0.9 / `BodyLiftAft` 1.8 and the 2.4.6 boost. Positive AoA only; negative AoA is untouched. Linear between knots, faded back to 1 from 45 to 60°.
   - **Robustness:** a malformed string counts as off.
   - **Fit:** fore and aft move together, so the lift at each AoA is unchanged. The stabs-neutral, TVC-0 pitching moment is zero from 4 to 48°, which makes the static margin ≈ 0 over 4–36°. This schedule does the "LERX vortex" job (more forward lift as AoA grows) in a controlled way.
   - **Code ported from the study:** `Interp`, `Sched`, the multiply in `BuildCharts`, and the schedule strings in the chart key. The study-only LERX vortex, aft bump and stab post-peak shapes are not ported.
2. **`StabilatorStallAoA` 25 → 36.** A neutral stab now reaches its peak near 52° body AoA (downwash 0.6 to 45°, then faded out by 60).
3. **Dev config ("5. Airframe"):**
   - `ForeLiftSchedule` and `AftLiftSchedule`, strings `"AoA:multiplier,..."`, applied at spawn.
   - Empty = off (with `StabilatorStallAoA` 25 that is 3.1.9's airframe).
   - Defaults (X2):
     ```
     ForeLiftSchedule = 0:1.0000,4:1.0843,8:1.0814,12:1.0795,16:1.0738,20:1.0577,24:1.0466,28:1.0359,32:1.0318,36:1.0399,40:1.0553,44:1.0644,48:1.1335
     AftLiftSchedule  = 0:1.0000,4:0.8823,8:0.8862,12:0.8888,16:0.8984,20:0.9232,24:0.9402,28:0.9554,32:0.9617,36:0.9529,40:0.9347,44:0.9260,48:0.8287
     ```
4. **AirframeRevision 26** forces `StabilatorStallAoA` 36 and both schedules once over older Dev files.
   - Log line: `3.2.0 defaults: airframe X2, StabilatorStallAoA 36 and the fore / aft lift schedules (overriding the dev file).`
   - Tested on a copy of your Dev cfg with BepInEx's own `ConfigFile`, using a harness that copies the migration block (the plugin itself was not run):
     - it moved 25 → 36, wrote both strings and set rev 26;
     - a value set afterwards (30) was kept on the next load;
     - the strings round-trip exactly.
5. **Config descriptions** updated: `StabilatorStallAoA`, `StabDownwash`, `StallStaggerLiftSlope`, `AirframeRevision`.

**Not changed: the FCS's stab logic.** It reads the peak from the table, so the constants tuned around 25° move with it automatically (these were already in the study's X2 runs; none retuned):
- `stabStallShift` 8.75 → 0: the roll-pitch pass and the TVC budget fade back at their Aryx-foil AoAs.
- `BranchScaleToPeak` factor 0.74 → 1.07.
- Settled stall window: local 19 → 30.
- Main stall window: local 28 → 39.

## The port equals the study's X2
- The refit (`fitsched.py X2 StabStallAoA=36` on the patched 3.1.9 bench) reproduces every knot. Largest leftover moment: 0.15°/s² at 85 m/s.
- On all 14 flight-set cases, the 3.2.0 defaults are byte-identical to the study bench + `X2.cfg`.
- The bench stub regenerated from the new `Plugin.cs` differs from 3.1.9's only by the two strings and `StabStallAoA` 36.

## Bench: the handoff's X2 targets (all reproduced)
| test | 3.1.9 | 3.2.0 |
|---|---|---|
| hold 20 / 30 / 45° at AB (stab / TVC) | +4.5/+1.0, +5.9/+1.0, +9.4/+1.0 | +0.2/+0.9, +0.7/+1.0, +0.1/+0.7 |
| hold 45° idle (stab / TVC) | +7.4 / +6.8 | −0.7 / −1.3, mean AoA 46.7, **peak 50.4** |
| push from 45 → 20° AoA: idle / TVC off / AB | 1.95 / 1.42 / 0.88 s | 2.05 / 1.53 / 0.90 s |
| loaded roll at 36°, deg/s: 400 AB / 250 AB / 250 0.75 / 300 idle / 250 idle | 69 / 51 / 46 / 29 / 27 | 66 / 47 / 44 / 26 / 24 |
| full nose-down stab, 36 / 40 / 44° (static, from the study, not re-run) | −34 / −33.5 / −33.6 | −30 / −28 / −24 |

**Curves:**
- **Push from 45:**
  - Time for the nose to reach −10°/s: idle 0.83 s (3.1.9: 0.85), TVC off **1.12** (0.95), AB 0.48 (0.47).
  - No rate take-back, and no reversal of the pitch rate, nozzles or stabs in any of the three.
- **Loaded roll at 36° with thrust:**
  - No dip after the peak (400 AB: 2.2°/s, 3.1.9: 4.3).
  - Slower start at 250 km/h: 0.5 s after the stick, 24°/s (3.1.9: 30) at AB and 17 (22) at 0.75.
  - The roll-onset nozzle kick (−11 … −20°) is the same as 3.1.9's. That is phase 2.

## Open items from the handoff
1. **"Hold 30 at idle" sat at 26.8°: explained.**
   - X2 overshoots the 30° test cap to 36.3. More than 5° past the limit with the stick past 0.85, the full-aft protection latch (`protHold`) takes over: zero pitch rate until the AoA falls below **limit − 5** or the stick comes back under 0.7.
   - So the nose holds attitude while the AoA bleeds down to 25.5.
   - Releasing the latch at the limit instead of limit − 5 gives 30.0 on this case. Tried on the bench, not shipped, not run on the suite. It is a law change; your call.
2. **Roll at 36° follows the available nose-down:** as predicted (−4 … −11 %). At moderate AoA it is bigger (regression 2 above).
3. **Neutral stability with damage:**
   - Cases: one stab off; nozzles locked; both; fins off. Each at 300 / 500 / 800 km/h, with a 0.6 pull for 2 s then release, and a full pull + roll for 1.5 s then release.
   - Nothing diverged, and no pitch oscillation after release (pitch-rate std over the last 2 s ≤ 0.1°/s in every case; 3.1.9 ≤ 0.27).
   - The AoA comes back more slowly after a release. Fins off, 500 km/h: still 16.9° two seconds later (3.1.9: 7.7), and the full pull peaked at 63.9 (53.1). Nozzles locked, 300 km/h: 11.8 (9.2).
   - The 45° no-TVC cap still holds in the nozzles-locked pulls (43.0 / 33.5 max at 300 / 500).
4. **Through zero AoA:**
   - **Static moment** (85 m/s, stabs and TVC at 0): no step.
     - At −4° the restoring moment is +1.1°/s² (3.1.9: +2.8), from the stab's lower slope. The handoff's "negative AoA untouched" is true for the schedule, not for the stab.
     - From 0 to 2.8° it is slightly nose-up (+0.15 at 1°, +0.32 at 2°: the first chart point is at 2.81°), and neutral above that.
   - **Slow push through zero** (300 / 500 / 700 km/h): pitch-rate error 2.3 / 1.8 / 0.3°/s (3.1.9: 2.6 / 1.8 / 0.5). No new stab or nozzle reversals.
   - **Full push through zero:** the stabs swing more within ±0.5 s of the crossing (500 km/h: 20.5° peak to peak, 3.1.9: 7.2), because they need more travel for the same moment. The pitch-rate error is the same ±3°/s.
5. **The game samples charts every 2.81°.** The 4° knots land between chart points, and the static figures are at 85 m/s with no thrust. Unchanged; noted.

## Planned push-overs: 139 free replays
130 from the 5–6 Nov logs plus the 9 planned ones in the 7 Nov 01:23 log (onset = rising edge of `ppOn`, window t − 0.5 … t + 3). The 3.1.x scoring script was not in the source zip, so this is a re-written one; its numbers are not comparable with the 3.1.8 notes, only A against B.

| | 3.1.9 | 3.2.0 |
|---|---|---|
| sideslip median / worst | 5.3 / 22.5 | 5.4 / 23.9 |
| time to 20° AoA, median | 1.12 s | 1.10 s (per push −0.45 … +0.17) |
| nozzle reversals (sum) | 452 | 450 |
| pitch-rate reversals | 3 | 1 |
| command spikes | 0 | 0 |

- Sideslip: 38 pushes worse by more than 1°, 21 better.
- Largest increases:
  - 5 Nov 21:09 log, 34.8 s: 2.1 → 9.7 (from 61°);
  - 5 Nov 13:51 log, 70.7 s: 2.3 → 8.2;
  - 6 Nov 22:22 log, 100.1 s: 6.8 → 12.4.

## Low set (48 cases, sub-100 kt)
- Time to target: same (39.9 / 39.8 s in total).
- Sideslip: total 174 → 183, worst 17.9 → 22.9. 8 cases worse by more than 1°, 6 better.
- Pitch-command retreat: total 174 → 212. Worst case unchanged at 13.8.

## Suite (same tree, 3.1.9 vs 3.2.0)
- **Reversals:**
  - Reversals take longer to reach 90 % at 400 km/h half pull (1.18 → 1.98 s) and are quicker at 556 (1.10 → 0.82).
  - Sideslip lower at full aft (4.6 → 1.7, 7.9 → 2.7) and at pull + roll 650 (21.6 → 15.5).
  - Falling-leaf reversals: zero crossing 1.05 → 1.58 s at 300 km/h.
- **Corkscrew entries:**
  - Sideslip lower in all 20 (e.g. 10.3 → 5.2, 10.1 → 4.7).
  - Early roll peak lower in most (700 km/h, 0.7 pull: 223 → 162).
  - Max AoA: the low-throttle 400 km/h case is regression 1.
- **Regression set (peak sideslip):**
  - Rolling pulls are lower: pull-then-roll 7.2 → 2.3; full-aft roll + reversal 6.6 → 4.1 and 6.1 → 3.6; roll-then-pull reversal 10.0 → 7.6 (ensemble 10.0 → 7.5).
  - Push-then-pull-through + roll 450 is higher: 5.6 → 11.5 (ensemble 6.5 → 10.4).
  - 1 g roll reversal 500: 2.3 → 4.0 (ensemble 2.3 → 3.8).
  - Roll rates lower (regression 2).
- **Utilisation:** the same 4 FAIL bands as 3.1.9, ratios within 3 points.
- **Damage:**
  - **No-fin leaf and push-overs: better.** Released push 560 sideslip 17.3 → 5.5; leaf stops complete 1.6–1.7 s (2.0–2.3).
  - **One-fin push 600: worse.** Released 5.0 → 7.2, held 5.0 → 8.5.
  - **Clean push-overs:** 6.6–6.7 → 6.9–8.6.
  - **LERX shot off at 65°:** 5.5 → 8.3.
  - **Fins + right flaperon roll reversal at 890 km/h:** departs in both.

## Tried and not shipped (bench only)
| idea | low-throttle overshoot (cork 400 / pull-roll 400 / idle pull / hold 45 idle) | why not |
|---|---|---|
| `StabilatorCLScale` 1.4 | 45.2 / 45.9 / 45.1 / **63.8** | fixes the entries, but the idle 45° hold runs to 60+; the schedule was fitted at 1.0 (1.2: 51.7 / 52.3 / 45.1 / 61.9) |
| X2b: same fit, moment target −5 at 44° and 3.1.9's −14.8 at 48° (X2 below 40°, back to 3.1.9's nose-down by 48°) | 51.9 / 52.5 / 47.4 / 48.2 | halves the overshoot, idle 45 hold settles at 45.1 (+1.9 stab), hold 45 AB needs +3.2 stab; everything below 40° identical to X2. Not the handed-off target, so offered, not shipped. `analysis/x2_acceptance/X2b.args` |
| `protHold` released at the limit | no change | fixes the 26.8° hold only |

## Not done
- The "release matrix": no definition in the source tree or the 3.1.x notes, so not run. Covered instead: suite, damage, low set, push-over replays, ensembles.
- The four unplanned push-overs of the 01:23 log were not replayed separately.
- Not flown.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 3.2.0 + `meta.json`. 3.1.9 is kept as `.dll.3.1.9.bak` (committed first).
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.2.0. The 3.1.9 one is kept as `Aryx_F22E_StrikeRaptor_FCS_Dev.dll.3.1.9.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.2.0-release`.
- **Mod manager:** it once put 3.0.3 back from its modpack. Update the version there or it may do it again.
- **To go back:** rename `.dll.3.1.9.bak` to `.dll`. Your Dev cfg keeps the X2 values: set `StabilatorStallAoA` 25 and empty both schedules for 3.1.9's airframe on 3.2.0.
