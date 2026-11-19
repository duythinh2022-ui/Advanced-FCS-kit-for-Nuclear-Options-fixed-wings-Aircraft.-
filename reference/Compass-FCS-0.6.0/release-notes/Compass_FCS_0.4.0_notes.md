# T/A-30 Compass FCS 0.4.0: asymmetric load, coupled AoA limit and the low-speed 7° limit

Base: 0.3.1. Tom, 11 Nov: "For compass it works well, at least for the most part. Towards the end i was flying at around 130-140kt, aoa limit was at 7degs, then i turned on mpo and the thing seems to be stable at around maybe a bit slower than that while i pulled quite a bit more aoa and still have roll left and right authority. Not until i get towards 30aoa then the jet started to tip to 1 side". Then: "fix compas then".

**Changelog:** With an asymmetric load the AoA limit now comes from a coupled check (every surface counted, roll, pitch and sideslip held together): about 22-25° with 32 cm of lateral CG offset where 0.3.1 gave 12°, and the 7° limit under about 120 kt is gone (the nose-down limit now reads the same check and its margin scales with airspeed). The thrust's moment about the offset CG is now part of the load, which brings the limit down to its 12° floor under about 100 kt on afterburner. Negative limit −30° (was −14 to −27°). New dev setting AsymCoupledReserveMax (0.75).

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,335,296 bytes, SHA-1 a2b2db49397e4eb9193528e9dd4be2f8894721db). The native core inside is unchanged from 0.3.1 (hash 263999dfa54dc4c9).
- Benched (.NET 8): the suites, the loaded-reversal set, a 24-case asymmetric-load matrix and engine-out cases; numbers below.
- Run on the game's Mono under Wine (`-O=-float32`): plugin loads, native core written and loaded, kernel self-test 200 / 200; the new solver gives the right answer on a hand case and agrees with a reference on 2,001 random problems.
- **Not flown.**

## What the 0.3.1 logs showed (32.5 cm of offset, 10.0 t)
| | 0.3.1 in flight |
|---|---|
| Lateral limit | 12.0° for 66 % of the time (the law's floor) |
| Aileron differential used holding 12° | 3.8° of 30° |
| Limit under about 120 kt (62 m/s) | 7.0-7.5° for 14 % of the time |
| Drops of the limit by more than 4° inside 0.5 s | 6 |
| With MPO (limit off) at 119 kt | held 25-30° with 21° of aileron differential, drift 0.4 deg/s |
| With MPO near 82 kt | tips at 35-45° |

Two separate causes:
1. **The 12° limit** came from the one-axis check: it adds a roll share and a yaw share, each against that axis's authority alone, and keeps 80 % of the roll authority in reserve. On this jet that sum is over the line at every AoA, so the law sat on its 12° floor while the surfaces were barely used.
2. **The 7° limit** was the nose-down limit (the AoA where the tail can still push the nose down while the roll load is held). It demanded a fixed 20 deg/s² of nose-down acceleration; under about 120 kt the jet cannot make 20 deg/s² at any AoA, so the limit fell to its 7° floor. It also read the tail's roll use from the live roll command, so it moved with roll inputs (12 → 7° in 1 s at −6 deg/s of roll).

## Changes
1. **Coupled lateral check** (`CoupledCap.cs`, from Ternion 0.2.3). At each sweep AoA a small linear programme (an exact optimiser over every surface's moment points) finds the most flight-path roll (roll about the velocity vector) each way with the load held, no nose-up acceleration and no sideslip acceleration. The limit sits where the weaker side is down to the reserve share of the mean of the two sides.
   - Runs when there is a lateral CG offset over 4 cm, a damage asymmetry or an engine asymmetry. Clean jet: the old code path, untouched.
   - Stabilators are counted only up to their stall angle (attached flow), as on the Ternion.
2. **Nose-down limit reads the same check** (`PitchHeld`): the nose-down acceleration left with the load and the sideslip held, with no dependence on the live roll command. Its margin scales with dynamic pressure under 253 kt (`AsymPitchMarginIasRef` 130 m/s, floor 6 deg/s²), judged at the predicted speed.
3. **Thrust about the offset CG is in the load** (`AsymThrustCgLoad`). The engines are fixed; with the CG off the centreline their thrust yaws and rolls the jet, and that moment does not fall with airspeed while the surfaces' authority does. Faded in over 4-12 cm of offset so a symmetric jet gets exactly zero.
4. **Dev setting `AsymCoupledReserveMax`** (default 0.75, "Asymmetric load" section): the roll reserve share. Higher = lower limit, more roll on it.
5. Telemetry columns added at the end: `cplU, cplRp, cplRn, cplDn, pitchMarg`.
6. `README.md`: the build line corrected to `build.sh Compass_TA30.FCS out.dll` (the form that embeds the native core; the release DLLs were always built that way).

## Bench results
Store of 900 kg on the left outer wing = 32 cm of offset at 7.9 t (the bench jet is 2.1 t lighter than the logged one). Full aft stick from the speed shown, afterburner unless noted; table values while above 87 kt (45 m/s).

### AoA limit
| Case | 0.3.1 | **0.4.0** |
|---|---|---|
| 32 cm, 135-324 kt entries | 12° (7° under 113 kt) | **22-25°** above 117 kt |
| 32 cm, half throttle / idle | 12° / 12° (7° slow) | **21-25° / 25°** |
| 21 cm | 7-23° | **23-27°** |
| 12 cm | 7-28° | **29-32°** |
| Negative limit, 32 cm | −14 to −27° | **−30°** |
| Nose-down limit, 97-272 kt | 27-32°, falling to 7° under 100-120 kt | **28.0-28.3°** |
| Raw limit jumps over 10° in 0.5 s | 2 (in roll reversals) | **0** |
| Clean jet | 35° | 35°, identical |

- **Sideslip in the pulls:** 0.8° or less. **Drift** (flight-path roll with the stick centred): 0.1 deg/s mean.
- **Capture:** at 162 kt the AoA rises onto the limit (24.2° against 23.3-24.3°) with no overshoot; pitch rate to 27 deg/s. During the pitch-in the roll dips to −3.6 deg/s for about 0.75 s, then returns to zero.
- **The reserve share:** 0.8 → 20-21.6°; **0.75 → 22-23.5°**; 0.7 → 23-25.5°; 0.6 → 25-27°; 0.5 → 27.5-28.2° (the nose-down limit takes over). No drift at any of them. 0.75 was chosen on the slow end: a zoom to 58 kt reached 11.6° of sideslip at 0.7 and 1.5° at 0.75.

### Low speed
- On afterburner the limit comes down from 20° at 113 kt to the 12° floor by 87-97 kt, at every reserve share. This is the thrust term: with it switched off the limit stays at 26° and the same slow pull reaches 15° of sideslip and 17 deg/s of drift under 117 kt. At half throttle the limit holds 22-23° down to 97 kt and reaches 13° at 58 kt.
- The limit falls at about 3°/s as the speed bleeds. In a pull alone the AoA follows within 0.5°. In a full roll reversal during the bleed (136 → 97 kt) the AoA ran up to 4° above the falling limit for about 3 s with both elevators on their −30° stop; sideslip stayed under 1.8°.
- At the top of a zoom with the stick held full aft, both builds run out of pitch authority and fall through: 0.3.1 at 35-43 kt, 0.4.0 at 47-52 kt in the same half-throttle case (10 kt earlier). That is a stall at the top of a zoom; this law does not protect it in either version.

### Rolls on the limit (full stick, reversal after 3 s)
| | 0.3.1 | **0.4.0** |
|---|---|---|
| AoA during the rolls | 7-18° | 20-25° (13° at the end of the bleed case) |
| Peak roll rate | 70-82 deg/s | **38-49 deg/s** (29 deg/s in the slow case at 0.6 throttle) |
| Sideslip max | 2.5-3.4° | 1.6-1.8° (2.4° in the slow case) |
| AoA past the limit | up to 10.3° | up to 4.0° (the bleed case above) |

The rolls now happen on the limit at about twice the AoA, with the roll range that is left there. The reserve share sets that trade: at 0.8 the limit is 20-22° and the same rolls reach 48-60 deg/s. Rolls with the stick eased to a lower AoA were not compared.

### Regression
| Set | Result |
|---|---|
| Suites with the three new switches off, against 0.3.1 | 125 / 125 per-case files identical |
| Suites with the defaults, against 0.3.1 | 121 / 125 files, 4 / 4 tables identical; the 4 files are two damage cases (roll differs by 2.6 deg/s or less, AoA by 0.01°) |
| Loaded reversals (88 cases, 8-21 cm) | median AoA 20.4 → 27.2°; sideslip 2.7 → 2.8°; acceleration step at the zero crossing 10.5 → 4.5 deg/s² in 3 ticks; hitches 0 |
| Roll spam, loaded | sideslip max mean 4.0 → 4.2°, worst 7.6 → 7.9°; zero-crossing step mean 30 → 13, worst 55 → 38 deg/s² in 3 ticks |
| Engine out (4 cases, left engine out at 0.5 s) | limit 22° in both (`EngineOutAoACap`); the 243 kt pull is the same to 0.1°; the three slow climbs depart at the top in both, within 0.7 s of each other |

### Cost
The sweep's time per tick on the bench: 173 µs → 525 µs while loaded (the solver is C#, not yet in the native core); clean jet unchanged at 179 µs. While loaded the sweep does 2 AoA points per tick in place of 3, so a full pass of the 22 points takes 11 ticks (0.22 s) in place of 8.

## Not done / open
- **Not flown.** The bench jet is 7.9 t against 10.0 t in the logs, and the bench's thrust-to-weight reading (`tw`) is 0 on this jet in the logs, so the log could not be replayed against the new law.
- **MPO comparison:** your MPO test held 25-30° at 119 kt. The new limit there is about 20-21° on afterburner, 23° at half throttle. The gap is the roll reserve (the limit keeps 75 % of the roll range for the stick) plus the thrust term.
- **The slow end on afterburner** returns to 12° under about 97 kt. That is the model's answer, not a floor artefact; whether it is too cautious in the game needs a flight.
- **The solver is not in the native core** (same on the Revoker 1.2.0).
- **Flanker:** not ported yet.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` 0.4.0 + meta.json. 0.3.1 kept as `Compass_TA30_FCS.dll.0.3.1.bak`.
- **Dev:** unchanged (`Compass_TA30_FCS_Dev.dll`). `AsymCoupledReserveMax` appears in `Compass_TA30.FCS.Dev.cfg` after the first start.
- **Release:** `C:\Games\NO mods\Compass-FCS-0.4.0-release`.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.

## Bench tools
- `bench/an/asymmat.sh <tag> [Name=value ...]` runs the 24-case matrix; `[VMIN=45] python3 bench/an/asymmat.py <tag> ...` prints the table.
- 0.3.1 laws: `CoupledCap=false AsymThrustCgLoad=false AsymPitchMarginIasRef=0`.

Previous notes: `claude/Compass_FCS_0.3.1_notes.md`. Port background: `claude/Asym_coupled_cap_port_handoff_2026-11-11.md`.
