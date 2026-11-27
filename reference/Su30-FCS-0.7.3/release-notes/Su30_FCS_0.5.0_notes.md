# Su-30VXN Gandiva FCS 0.5.0: asymmetric load, one steady AoA limit

Base: 0.4.1. Tom, 11 Nov: "For flanker with asymmetric load, when i roll aoa limit rises", then "flanker asymmetric load is not fixed."

**Changelog:** With an asymmetric load the AoA limit now comes from a coupled check (every surface and, with TVC on, each nozzle counted; roll, pitch and sideslip held together) and no longer moves with the stick: about 52° with 16 cm of lateral CG offset and 46-51° with 31 cm, TVC on or locked, where 0.4.1 swung between 12° and 75° and pushed the nose down each time it dropped. With TVC on the limit opens smoothly toward 75° as the speed falls below about 145 kt. The nose-down limit reads the same check, and the thrust's moment about the offset CG is part of the load when the nozzles are locked. New dev setting AsymCoupledReserveMax (0.8).

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,482,240 bytes, SHA-1 187b7fdd27d7a0bc814f8b5501dd3e9b62a70099). The native core inside is unchanged from 0.4.1 (cache folder 4eb022886d1a978f).
- Benched (.NET 8): a 31-case asymmetric-load matrix, the 88 loaded reversals, the regression suites, engine-out cases and a replay of Tom's own stick inputs; numbers below.
- Run on the game's Mono under Wine (`-O=-float32`): plugin loads, native core written and loaded, kernel self-test 200 / 200; the new solver gives the right answer on a hand case (60 / 40), no bad result on 2,001 random problems, and its one-side solve equals the two-side solve on all 2,001.
- **Not flown.**

## What the 0.4.1 logs showed
`Su30_FCS_20261111_161855.csv`: TVC locked the whole flight, 16.4 cm of offset at 25.5 t, 103-229 kt. `..._125937.csv`: 19.5 cm at 25.7 t, TVC on until 77 s.

| | 16:18 log (TVC locked) | 12:59 log |
|---|---|---|
| Time loaded in the normal law | 61 s | |
| Raw lateral limit | 12-75°; median 26° wings-level, 52° while rolling | 15-18° wings-level with TVC on |
| Jumps of the limit over 10° inside 0.5 s | 42 | 68 |
| Forced nose-downs with full aft stick | 4 in 48 s, pitch rate to −50 deg/s, AoA 1-50° | 7 |
| AoA above the limit | up to 17.9° | |

What the jet could actually hold, same log: at 47° AoA and 146-169 kt it carried the load with 6-9° of flaperon differential (of 25°), 3° of stabilator differential and −17° of rudder (of 35°), drifting 1.8 deg/s. At 50° and 103-111 kt a reversal toward the heavy-wing-up side made only about 8 deg/s². The model's roll load was at or above the measured one (measured 60-100 % of the model), yaw about equal.

**Cause.** The 0.4.1 check worked one axis at a time with the surfaces frozen at their present symmetric deflection. Its answer therefore moved with the stick and the AoA: about 20° wings-level, 52-75° in a roll. Each drop left the jet above its own limit and the AoA law pitched it down.

## Changes
1. **Coupled lateral check** (`CoupledCap.cs`, from the Compass 0.4.0 / Ternion 0.2.3). At each sweep AoA a small linear programme (an exact optimiser over every effector's moment points) finds the most flight-path roll (roll about the velocity vector) each way with the load held, no nose-up acceleration and no sideslip acceleration. It does not read the live stick or the present surface positions. Runs with a lateral CG offset over 4 cm or a damage asymmetry; clean jet: the old code path, untouched.
2. **Su-30 effectors.**
   - With TVC on and both engines running, **each canted nozzle is its own effector** (its moment table per engine, `TvcEffector.Mn`); the thrust at the present nozzle angle is then taken out of the load so it is not counted twice. With TVC locked, or with an engine out, the nozzles are not in the check.
   - The **canard pair is one effector** (symmetric only, as the allocator flies it).
   - **Stabilators and canards** are offered the same range the allocator uses (`ClampStabToPeak` rule; on this jet that is the full travel, stalled or not). The Compass rule (attached flow only) made the stabs vanish at 50-55° here, which the real allocator does not do.
   - Rudders over their full ±35°.
3. **Top-edge rule** (`AsymCap.FindTop`). On this jet the demand curve is not monotonic: a hump at 10-30° AoA, a flat stretch at 40-50°, then a cliff past 50-55°. Taking the first crossing put the limit at 12° whenever the hump touched the line (51 → 12° in one step, nose pushed from 50° to 0° in the bench). The limit is now the lower edge of the over-the-line stretch that reaches the top of the envelope, with a latch (hysteresis) so a flat stretch sitting near the line cannot toggle it.
4. **Nose-down limit reads the same check** (`PitchHeld`): the nose-down acceleration left with the load and sideslip held, no dependence on the live roll command. Its margin scales with dynamic pressure under 253 kt (`AsymPitchMarginIasRef` 130 m/s, floor 6 deg/s²).
5. **Thrust about the offset CG is in the load** when the nozzles are fixed (`AsymThrustCgLoad`, from the Ternion 0.2.2), faded in over the CG offset so a symmetric jet gets exactly zero.
6. **Dev setting `AsymCoupledReserveMax`** (default 0.8, "Asymmetric load" section): the roll reserve share. Higher = lower limit, more roll left on it.
7. **Cost control:** one side solved per sideslip case (4 solver passes per AoA point instead of 9, same results on all 31 matrix cases and 264 / 264 loaded-reversal rows); while loaded the sweep does 2 AoA points per tick instead of 3 (a full pass every 11 ticks).
8. Telemetry columns added at the end: `cplU, cplRp, cplRn, cplDn, pitchMarg`.
9. Bench: the engine-failure option now finds this jet's engines (names start with "Left" / "Right"; before, `engout=` did nothing here); new `bench/an/asymmat.sh|py` (the matrix below).

## Bench results
Bench jet 22.8 t (2.7 t lighter than the logged one). Store of 800 kg on the left mid wing = 16 cm of offset, 1,600 kg = 31 cm. Table values while above 87 kt (45 m/s).

### AoA limit
| Case | 0.4.1 | **0.5.0** |
|---|---|---|
| 16 cm, TVC on, pulls and rolls at speed | 12-75° | **51.3-52.7°** |
| 16 cm, TVC locked | 12-75° | **51.3-51.8°** (the 50° no-TVC AoA limit governs) |
| 31 cm | 12-75° | **46-51°** |
| Jumps over 10° in 0.5 s, per roll case | 3-14 | **0** at speed |
| AoA above the limit | 11-20° | none in pulls; table below for reversals |
| Pitch rate with full aft stick held | down to −29 deg/s | no forced nose-down |
| Throttle steps, 16 cm | 15.9-67°, 11 jumps | **51.9-53.6°, 0 jumps** |
| Negative limit | −29 to −65°, moving | **−65°, steady** |
| Clean jet | | identical to 0.4.1 |

- **Capture, 16 cm, TVC on, from 310 kt:** pitch rate to 59 deg/s, AoA onto the limit with no overshoot (52.3° against 52.1-52.3°). During the pitch-in the roll dips to −8.6 deg/s for about 0.5 s; sideslip 0.6° or less.
- **Capture, 31 cm:** a ±2° AoA wobble for about 1.5 s (up to 2.5° over) and roll dips to −15 and −11 deg/s. Not clean.
- **Pull then push to −64.6°:** clean, sideslip 1.1° or less.
- **Replay of Tom's stick, 16:18 log, 70-98 s** (same store side, TVC locked, bench mass): 0.4.1 laws reproduce the flight (limit 27-56°, AoA 50 → 8 → 49°...); 0.5.0 holds the limit at 51.3° for the whole 28 s, AoA 50°, sideslip 5° or less in the rolls.

### Rolls on the limit
| | 16 cm, TVC on | 31 cm, TVC on | 16 cm, TVC locked |
|---|---|---|---|
| Roll rate, strong / weak side | 70-78 / 54-78 deg/s | 72-74 / 43-67 deg/s | 43-53 / 38-50 deg/s |
| Sideslip | 7.6° or less | | 6.6° or less |
| Stop after release | 0.8-1 s | 1.6 s | |
| Drift, stick centred | none | none at 154 kt | none |

- **Weak-side reversal at very high AoA and low speed** (61-74° AoA, 89-119 kt, TVC on): only 20-28 deg/s², surfaces on their stops. It reverses, slowly.

### Low speed, TVC on
The limit opens smoothly from about 52° to the full 75° as the speed falls through roughly 145 → 100 kt: there the nozzles carry the load and the check finds enough roll both ways. Stick centred the flight-path roll stays near zero and sideslip 0.8° or less. With TVC locked there is no such release (51.3-51.8° throughout).

### The reserve share (16 cm, TVC on, at speed)
| Share | Limit | Note |
|---|---|---|
| 0.6 | 55-57° | opens to 75° early, drift 5-12 deg/s stick centred |
| 0.75 | 52.8-53.1° | −3 deg/s drift at 97 kt / 52° with 31 cm |
| **0.8** | **51.9-52.1°** | chosen |
| 0.85 | 51.0-51.2° | opens later |

Why the limit sits there: reserve each way (weak / strong side) with 16 cm, TVC on at 212 kt: 115 / 144 deg/s² at 50°, 50 / 108 at 55°, 44 / 105 at 60°. TVC locked at 185 kt: 67 / 88 at 50°, 16 / 60 at 55°. The cliff past 50-55° is the airframe, not the rule.

### Loaded reversals (88 cases, 8-21 cm, TVC on)
| | 0.4.1 | 0.5.0 |
|---|---|---|
| AoA above the limit, mean of maxima / worst | 31.3° / 52.5° | **1.1° / 4.6°** |
| Roll-rate step at zero crossing, mean / worst | 7.95 / 43.1 deg/s | **4.08 / 28.4** |
| Sideslip, mean of maxima / worst | 10.15° / 28.6° | 8.90° / 28.0° |
| Roll behind command, mean max / worst / rms | 40.6 / 75.3 / 16.8 deg/s | 40.4 / 52.3 / 18.4 |
| Bank travelled | 278° | 214° |
| Single reversals (34): median AoA | 34.1° | 49.1° |
| Single reversals: "no reversal found" | 11 | 14 |

- Less bank travelled and three more "no reversal" cases: the jet now sits at about 50° where 0.4.1 was repeatedly pushed down to low AoA, and it rolls slower there.
- One case (`s2_1100_580_L`, a rolling pull) shows a 4.8 deg/s roll fall-back during the AoA capture, with 19.8° of sideslip (0.4.1: 16.5°, no fall-back).

### Regression and engine out
- Suites, new laws switched off, against 0.4.1: 87 / 87 files and 4 / 4 tables identical. With the defaults: the same (the suites have no working asymmetric case, so this shows only that the clean jet is untouched).
- Engine out (left engine failed at 0.5 s): the fixed 22° limit (`EngineOutAoACap`) in both builds; clean case identical, with 16 cm near-identical. **Not changed here**, though it does not match Tom's engine-out rule (no AoA cap, match thrust).

### Cost (bench, one thread)
- Sweep while loaded: about 100 µs → 400-490 µs per tick (99th percentile 700-900 µs). Clean jet: unchanged (74 against 69 µs).
- The solver is C#. The native (Rust) port is not done on any of the three jets that have it.

## Not done, open
- **Not flown.** The bench jet is 2.7 t lighter than the one in the logs.
- **Damage asymmetry** could not be benched: there is no Su-30 damage data in the bench (20 of 34 damage-suite rows fail in both builds). The code path is the same one the CG offset uses.
- **Negative side:** the sweep reaches only −40°, so between −40° and −65° nothing is checked. The one pull-push benched went to −64.6° cleanly.
- Weak-side roll at 60-75° AoA under about 120 kt is slow (above); the 31 cm capture wobble; the one reversal case with a fall-back.
- The Dev DLL still reports "0.3.10" (unchanged file).

## Install
- **Main:** `plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.5.0 + meta.json. 0.4.1 kept as `Su30_VXN_FCS.dll.0.4.1.bak`.
- **Dev:** unchanged. `AsymCoupledReserveMax` appears in the "Asymmetric load" section after the first start.
- **Release:** `C:\Games\NO mods\Su30-FCS-0.5.0-release`.
- **Mod manager:** update the version there, or its modpack may put the old DLL back. Restart the game.

## Bench tools
- `bench/an/asymmat.sh <tag> [Name=value ...]`: the 31-case matrix; `VMIN=45 python3 bench/an/asymmat.py <tag> [<tag> ...]` prints the summary.
- 0.4.1 laws in this build: `CoupledCap=false AsymThrustCgLoad=false AsymPitchMarginIasRef=0`.
- Other switches: `CplTopEdge`, `CplStabPeakRule`, `CplTvc`, `CplPts=9`, `CplPointsPerStep=2`, `CplBetaDesign=3`, `CplDebug`.

Previous notes: `claude/Su30_FCS_0.4.1_notes.md`. Port status across jets: `claude/Asym_coupled_cap_port_handoff_2026-11-11.md`.
