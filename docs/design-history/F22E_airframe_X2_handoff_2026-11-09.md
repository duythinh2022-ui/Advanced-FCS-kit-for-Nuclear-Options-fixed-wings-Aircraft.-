# F-22E FCS: trim-free airframe "X2" — build handoff (9 Nov 2026)

You are picking up Tom's F-22E Strike Raptor FCS (Nuclear Option, Aryx's F-22E mod) to **build airframe X2 as F-22E FCS 3.2.0**, then a control-law pass for the roll behaviour. Everything below was benched in the previous chat; **nothing has been flown**.

Read first:
- the project memory/preferences, and this project's `areas/no-f22-fcs-mod.md`;
- `claude/F22E_trimfree_airframe_study_2026-11-09.md` (the study this comes from);
- `claude/F22E_NO_FCS_3.1.9_notes.md` (the base).

## 1. What Tom wants (his words, 9 Nov)
- 13:39: "How about the simulated lerx vortex, getting rid of downwash modeling, 36 aoa as max lift curve for stabs. And the relaxed to neutral margin i talked about"
- 13:44: "The goal is, roll behavior as i stated. And tvc with stabs only need to give the jet a nudge to put it into a turn then go back to neutral or at least close to it, as close as possible. That behavior holds up to 36 aoa, then the jet must maintain useful nose up and down authority upto 45 aoa aerodynamically with the stabs doesn use the majority of its deflection to hold that aoa."
- **"Roll behavior as I stated"** comes from real-jet footage of a loaded roll at 36° AoA:
  - one stab stays near neutral, riding its maximum lift;
  - the other goes trailing-edge down into stall;
  - the roll rate is good at low airspeed;
  - the TVC hardly does anything for pitch.
- 13:57: "Make handoff so this can be built in a new chat" (asked right after the study recommended X2).

**The three goals:**
- **G1:** roll behaviour as in the footage.
- **G2:** up to 36°, stabs and TVC give a nudge into the turn, then sit at or near neutral.
- **G3:** from 36 to 45°, useful nose-up and nose-down authority *aerodynamically*, with the stabs not using most of their travel to hold the AoA.

## 2. Decision state
**T (the package exactly as asked) is rejected by the bench.** T means no downwash, stab peak 36, plus a trim-free schedule.
- It meets G2.
- It fails G3 badly: full nose-down stab gives only −5 deg/s² at 36–48°.
  - It cannot hold 45° at idle and departs to 60° with full stab.
  - A push from 45° at idle or with TVC off does not recover.
- **Why:** with no downwash the stabs see the full body AoA. Aryx's F22E_Wing normal force is flat (about 1.16–1.2) from 35 to 65° local, so a stab at its peak has nothing left to push the nose down, and past the peak its control reverses.
- **G1 and G3 conflict literally at 36°.** A neutral stab at maximum lift has no nose-down left.

**X2 is the recommended build.** It keeps the 0.6 downwash, puts the stab peak at 36 and adds the trim-free fore/aft lift schedule (the "LERX vortex" redistribution).
- It meets G2 and G3.
- It costs 4–11 % of roll rate at 36°.

**G1 is control-law work** (phase 2), not airframe work. With thrust, no airframe tried made the FCS put the second stab trailing-edge down into stall: it always unloads that stab trailing-edge up, which gives 3–10× more roll per degree. The roll-onset nozzle kick (−11 … −20° for about 0.3 s) is the same on every airframe.

If Tom pushes back toward T, show him the G3 numbers in §6. A middle ground is a smaller downwash. X1 (downwash 0.3, peak 36) gives nose-down −14 at 36° and −6 at 44° (`results/static_X1.txt`).

## 3. Base and conventions
- **Source:** `C:\Games\NO mods\F22E-FCS-3.1.9-release\F22E_FCS_3.1.9_src.zip`. It unzips to `f22/`: `Aryx_F22E_StrikeRaptor.FCS/`, `bench/`, `DevUnlock/`, `analysis/`.
  - 3.1.9 = 3.1.8 + the fuel hook fix.
  - `StallStagger.cs` and `BodyLift.cs` are identical to 3.1.8, and the bench `fcs/` copies are identical to the game files.
- **Version:** 3.2.0 (minor bump: an airframe change).
- **Release:** `C:\Games\NO mods\F22E-FCS-3.2.0-release`, containing:
  - the main zip, the Dev unlock, the source zip and the notes;
  - notes in the house format (look at `claude/F22E_NO_FCS_3.1.8_notes.md`).
  - Also write the notes to the project as `claude/F22E_NO_FCS_3.2.0_notes.md`.
- **Install:**
  - `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json`; keep 3.1.9 as `.dll.3.1.9.bak` and commit that backup **first**.
  - Also the Dev unlock `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll`.
  - If the game is running, the DLL commit fails ("fetch or write failed"); retry with force.
- **Tom's mod manager** once rolled the FCS back to 3.0.3 from its modpack (3.1.9 notes). Remind him to update the version there.
- **Bench data:** `bench/data/airframe_cal.json`. Set `BENCH_DATA` to it; the default path in `Program.cs` does not exist.
- **Tom's rules:**
  - Evaluate the response *curve* on every manoeuvre: instant response, smooth rate, no spike, hitch, sudden slow-down, chatter or hesitation.
  - Don't overstate verification ("not flown", "single run", "bench chaos").
  - Minimal targeted changes.
  - Roll-coupled bench cases are chaotic (±0.1 % in a gain can swing results), so use 5-member ensembles before calling a regression.

## 4. Phase 1: build X2 (game code)
### 4a. Scheduled fore/aft body-lift balance
The new code is in `bench_patch/BodyLift.cs`; the diff against 3.1.9 is in `bench_patch/airframe_study.diff`. **Port only the schedule parts**:
- `Interp` and `Sched`;
- the line `if (a < 0f) cl[i] *= Sched(fn, 0f - deg);` in `BodyLift.BuildCharts`;
- the schedule strings in `KeyPart`.

Leave out the study-only parts of that file and of the stub: the `Lerx` family, `VortexCl`, `AftBump`, and in `StallStagger.cs` the `Fam.Lerx` and `StabPost*`. They need Plugin fields the game doesn't have.

How the schedule behaves:
- Multipliers on the BodyFore family (LERX, intakes, fore, cockpit, centre body) and the BodyAft family (engine bays, tail booms, tail).
- **Positive body AoA only.** A positive body AoA is a negative chart angle; negative AoA is untouched.
- Linear between knots.
- Faded back to 1 from 45 to 60° (`FadeStart` / `FadeEnd`), so post-stall is unchanged.
- It applies **on top of** the existing `BodyLiftFore` 0.9 / `BodyLiftAft` 1.8 and the 2.4.6 lift shaping.

Plugin config (dev, "5. Airframe"):
- `ForeLiftSchedule` and `AftLiftSchedule`, strings `"a:m,a:m,..."`. Empty = off (3.1.9 airframe).
- The game reads charts at spawn (`StallStaggerJobs` → `StallStagger.BuildFor` → `BodyLift.BuildCharts`), so this is "applied at spawn", like the other airframe keys.

**X2 defaults** (fitted on the calibrated bench; `configs/X2.cfg`):
```
ForeSched=0:1.0000,4:1.0843,8:1.0814,12:1.0795,16:1.0738,20:1.0577,24:1.0466,28:1.0359,32:1.0318,36:1.0399,40:1.0553,44:1.0644,48:1.1335
AftSched =0:1.0000,4:0.8823,8:0.8862,12:0.8888,16:0.8984,20:0.9232,24:0.9402,28:0.9554,32:0.9617,36:0.9529,40:0.9347,44:0.9260,48:0.8287
```
- The fit keeps the lift at every AoA unchanged (fore and aft move together) and makes the stabs-neutral, TVC-0 pitching moment zero from 4 to 48°.
- The result is static margin ≈ 0 (±0.03 m) over 4–36°: "relaxed to neutral".
- The CG stays at `CGShiftAft` 0.05. A CG shift tops out at about 0.18 m anyway (`Registry` limits the moved mass to 0.6 × the Fore part's mass).
- **Refit on the 3.1.9 bench before shipping:**
  ```
  python3 scripts/fitsched.py X2 StabStallAoA=36
  ```
  - Run it with `BENCH_DIR` = the bench folder and `OUT_DIR` = scratch, after the patch is applied.
  - It should reproduce these knots. The patched 3.1.9 bench reproduces the X2 push run byte for byte.

### 4b. Stab lift peak 25 → 36
- `StabilatorStallAoA` default 36 (`Plugin.cs` line ~1595, `cStallStab`).
- Tom's Dev cfg has `StabilatorStallAoA = 25` and `AirframeRevision = 25`. Add a revision-26 migration in the `cAirframeRev` block (~line 1942 has the rev-25 one) that forces the following once:
  - `StabilatorStallAoA` 36;
  - the two schedules to their defaults.
- Update the config descriptions. The 2.0.8 text says the stabs reach their peak near 48–50° body AoA; with downwash 0.6 and peak 36, a neutral stab sees 0.4 × AoA up to 45°, then the downwash fades out from 45 to 60°, so the neutral stab reaches its peak at about 52° body AoA.
- **Lift slope:** `StallStaggerLiftSlope` is 0 (keep peak CL, scale the slope), so the stab's small-angle slope drops to about 0.69× the current one.
  - Benched 1 g full-stick roll (bank at 1.0 s): 350 km/h 57 → 55°, 550 km/h 129 → 128°, 800 km/h 171 → 171°.
  - That is within noise, so `StabilatorCLScale` stays 1. (1.4 gave 64 / 137 / 175 if Tom wants the low-speed snap back.)
- **FCS stab logic** reads the peak from the table (`se.stallAoA`): `FcsController` around lines 2058, 4830, 4866, 5509–5526, 5967, 6299. Check the constants tuned around a 25° peak:
  - `StabPeakRef` (`BranchScaleToPeak`), `BranchDeepMargin`, `RpStallMargin`, `RpMainStallMargin`, `SettledStallMargin`;
  - `StabPeakMargin` (off at 90).

### 4c. What does not need porting
The FCS's own surface and body models read the same tables (`StallStagger.TableFor`), so the plant and the FCS stay consistent automatically.

## 5. Phase 1 acceptance (bench; calibrated plant; compare against unpatched 3.1.9)
Bench setup:
```
cd f22/bench
patch -p1 < airframe_study.diff
dotnet build -c Release -o bin
export BENCH_DATA=$PWD/data/airframe_cal.json BENCH_DIR=$PWD OUT_DIR=<scratch>
```
- The patched default must be byte-identical to unpatched 3.1.9 (it was: loaded roll at 400 km/h).
- Copy `configs/*.cfg` into `OUT_DIR`.
- `python3 scripts/flt.py` then `python3 scripts/fsum.py` runs the four airframes (cur / N / T / X2) on the cases below.

The X2 targets, from `results/`:

| test | current 3.1.9 | X2 target |
|---|---|---|
| hold 20 / 30 / 45° at AB (stab / TVC) | +4.5/+1.0, +5.9/+1.0, +9.4/+1.0 | +0.2/+0.9, +0.7/+1.0, +0.1/+0.7 |
| hold 45° idle | +7.4 / +6.8 | −0.7 / −1.3 (overshoots to 46.7) |
| full nose-down stab at 85 m/s, 36 / 40 / 44° (static) | −34 / −33.5 / −33.6 (incl. −18 … −23 natural) | −30 / −28 / −24 (moment with stabs neutral = 0) |
| push from 45° → 20° AoA: idle / TVC off / AB | 1.95 / 1.42 / 0.88 s | 2.05 / 1.53 / 0.90 s |
| loaded roll at 36°, deg/s: 400 AB / 250 AB / 250 0.75 / 300 idle / 250 idle | 69 / 51 / 46 / 29 / 27 | 66 / 47 / 44 / 26 / 24 |
| 1 g roll, bank at 1 s: 350 / 550 / 800 km/h | 57 / 129 / 171 | 55 / 128 / 171 |

Then the full regression set used for 3.1.x:
- the suite, the regression and damage sets, falling leaf;
- the 87 planned push-overs from 5–6 Nov, the low set (48 cases) and the 01:23 log replays (3.1.8 notes);
- the release matrix.

Report every change honestly. The push-over work (3.1.4–3.1.8) was tuned on the current airframe's natural nose-down; expect the planned push-overs to change.

**Open items found in the study:**
1. **Unexplained.** X2 held only 26.8° on the "hold 30° at idle" case (300 km/h entry, idle); cur, N and T reached 30. Investigate first.
2. **Roll rate at 36° follows the FCS's available nose-down acceleration** (`aDn`), because the roll's inertial pitch-up is paid from it (coupling roll limit, 2.6.9).
   - `aDn` at 400 km/h AB / 300 km/h idle: cur 187/33, X2 187/31, N 138/18.5, T 113/5.
   - The trim-free airframe has no natural nose-down to spend, hence X2's −4 … −11 %.
3. **The airframe is now neutrally stable from 4 to 36°** (no restoring moment). With a stab or nozzle shot off it will not point into the wind by itself. Run the damage suite: one stab off, nozzles off, both fins off.
4. **The 0 → 4° knot step** (1.0 → 1.08 fore, 1.0 → 0.88 aft) is a slope break near zero AoA, and negative AoA keeps the old balance. Check pushes through 0° for a moment step: look at the q curve, Tom's smoothness rule.
5. **Static figures are at 85 m/s, no thrust.** The game samples charts every 2.8125°, so the 4° knots land between chart points.

## 6. Why not T, and what the other variants did (all bench, calibrated plant)
| | stabs | fore × / aft × on top of 0.9 / 1.8 | G2 (holds) | G3 (nose-down at 36 / 44, push from 45 at idle) | loaded roll 400 AB / 300 idle |
|---|---|---|---|---|---|
| cur | downwash 0.6, peak 25 | – | stab +4.5 … +9.4 | −34 / −34, 1.95 s | 69 / 29 |
| N | as cur | 1.11–1.17 / 0.76–0.87 | ✓ | −16 / −10.5, 2.95 s (bottoms at 19°) | 59 / 21 |
| T | downwash 0, peak 36 (base 1 / 1) | 1.02–1.10 / 0.73–0.94 | ✓ to 36; 45 needs +14.5 (AB), departs (idle) | −5 / −5, **no recovery** | 51 / 20 |
| **X2** | downwash 0.6, peak 36 | 1.03–1.08 / 0.88–0.96 | ✓ | −30 / −24, 2.05 s | 66 / 26 |

Notes on the study:
- Flaperons and symmetric rudder give no nose-down at 36–45° (+4 … +9 nose-up or about 0), so G3 rests on the stabs.
- The explicit Polhamus LERX vortex (`LerxVortexKv`) on the no-downwash base made the jet nose-up past 24°, untrimmable at 32–40°. The fitted schedule does the vortex's job (more forward lift as AoA grows) in a controlled way.
- An earlier attempt the same day (H3/H4, `claude/F22E_lerx_vortex_airframe_prototype_2026-11-09.md`) used CG 0.22 + an aft-body bump.
  - H3 also added a stab post-peak plateau and drag ×1.5; in flight it ran away to 68–69° at idle and with TVC off.
  - The FCS's stab logic works from the lift peak (`se.stallAoA`). It would need to work from the stab's body-normal force before any post-peak shaping can be used.

## 7. Phase 2: the roll behaviour (G1), control law
Goal: at 36° in a loaded roll:
- one stab near its maximum lift;
- the other trailing-edge down into stall, instead of unloading trailing-edge up;
- TVC near zero;
- no onset kick;
- roll rate kept or better.

Facts to start from:
- **Allocator choice.** With thrust the allocator always picks "one stab trailing-edge down at about its peak, the other trailing-edge up" (bench: L −8 … −11 at local 22–25, R +8 … +11). At idle it sometimes picks the stalled-stab branch (R −28 at local 44.5).
- **Static moments at 36°, 85 m/s** (roll / pitch / yaw, deg/s²; `claude/F22E_loaded_roll_36_stabs_2026-11-09.md`):
  - one at peak + one stalled: +4 / −31 / +9;
  - ours: +44 / −11 / −3.
  - The stalled stab's drag gives yaw in the roll's direction (useful at 36°, where about 60 % of a velocity-vector roll is yaw), but almost no roll.
  - A deeper post-peak lift drop (stab post-stall study) raises its roll to +17, but did not change flight results.
- **Onset kick.** It is the allocator cancelling the stab's one-sided pitch curve at the knee (2.2.11 notes), not a reaction to a pitch-up that already happened.
- **The 2.0.10 trap.** A stab parked past its peak can get stuck (`StabPeakMargin` clamp, now off). Any "stall the second stab" logic must not bring that back.
- **Idle roll rate is yaw-limited** (rudder power falls with dynamic pressure).

## 8. Files in this kit
- `bench_patch/airframe_study.diff`: against 3.1.9 `bench/` (fcs/StallStagger.cs, fcs/BodyLift.cs, stubs/Plugin.cs). Applies with `patch -p1` inside `bench/`. Everything is off by default, and the output is byte-identical to 3.1.9.
- **Bench switches:**
  - `ForeSched` / `AftSched`: the schedule.
  - `LerxVortexKv`, `LerxBurst0/1`: Polhamus LERX lift.
  - `BodyAftBump`, `BumpA0..3`: aft-body window.
  - `StabPostN/W/Drop/Back/Cd`: stab post-peak shape.
  - Existing ones used: `StabDownwash`, `StabStallAoA`, `StabCLScale`, `BodyLiftFore/Aft`, `CgShiftAft`, `AoAPos`, `DisableTVC`.
- `bench_patch/BodyLift.cs`, `StallStagger.cs`: the patched bench files in full.
- **Scripts** (set `BENCH_DIR` and `OUT_DIR`):
  - `fitsched.py`: fits the schedules.
  - `af.py`: static table (NP, stabs-0 moment, trim stab, full nose-down / nose-up).
  - `flt.py` + `fsum.py`: the flight set.
  - `sum36.py` / `show36.py`: loaded-roll summary and time history.
  - `mg.py`, `np.py`: margin curves.
  - `basis.py` / `fit.py`: least-squares knob fit (superseded by `fitsched.py`).
  - `curve.sh`: stab CL samples.
- **Configs:** `X2.cfg` (build target), `N.cfg`, `T.cfg`, `H3.cfg`, `H4.cfg`, plus the fitted `sched_*.json`.
- **Results:** static tables, fit logs and the flight summary for cur / N / T / X2.

Related project docs:
- `claude/F22E_static_margin_2026-11-09.md` (margins; the roll pitch-up is inertial coupling);
- `claude/F22E_loaded_roll_36_stabs_2026-11-09.md`;
- `claude/F22E_stab_poststall_shape_study_2026-11-09.md`;
- `claude/F22E_lerx_vortex_airframe_prototype_2026-11-09.md`;
- `claude/F22E_trimfree_airframe_study_2026-11-09.md`;
- the 2.0.8 / 2.0.9 / 2.0.10 / 2.0.11 / 2.2.11 / 2.2.13 notes (downwash, body balance, stab peak history).
