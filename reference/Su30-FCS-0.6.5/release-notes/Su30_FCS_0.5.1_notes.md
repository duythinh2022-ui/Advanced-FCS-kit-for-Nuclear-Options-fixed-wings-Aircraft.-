# Su-30VXN Gandiva FCS 0.5.1: asymmetric load, a roll that stops when the stick is centred

Base: 0.5.0. Tom, 11 Nov 17:26, after flying 0.5.0 (`Su30_FCS_20261111_172204.csv`): "u did not really fixed it, now the cap is at 50 aoa without tvc instead of swinging like before. And the jet still tends to tips to one side. I release roll and then the roll rate drifts a lot".

**Changelog:** With an asymmetric load a roll now stops when the stick is centred: the roll rate the stick may ask toward each side is held to what the roll reserve on its braking side can stop in 0.8 s (new dev setting AsymStopTime), and a released roll is no longer held or sped up while the AoA unloads. Replays of the 17:22 flight's four slow stops: bank travelled after the release 47-117° → 27-44°. The price is a slower roll into the heavy side at the limit: 59 → 44 deg/s at 163 kt, 32 → 17 deg/s at 104 kt, nozzles locked.

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,484,288 bytes, SHA-1 e3af4a608eccf66a1217b19de0e6345ea521d47f). Native core unchanged (cache folder 4eb022886d1a978f).
- Benched (.NET 8): replays of the 17:22 flight's stops, a 24-case stop matrix, the 31-case load matrix, the 88 loaded reversals, the suites; numbers below.
- Run on the game's Mono under Wine (`-O=-float32`): plugin loads, kernel self-test 200 / 200, solver check passes (hand case, 2,001 problems, one-side against two-side).
- **Not flown.**

## What the 17:22 flight showed (0.5.0, nozzles locked the whole flight, 16 → 10 cm of offset, 25.3-25.5 t)
- **The limit itself held:** 50.0-52.8° once the load was on, no jumps, no forced nose-downs. With full aft stick and the roll stick centred at 107-117 kt (96-106 s) the jet sat at 50° with zero flight-path roll and 0.03° of sideslip.
- **The roll did not stop.** Flight-path roll after the stick was centred, rolls into the heavy side:

| Release | State | Roll at release | 1 s later | Bank travelled after release |
|---|---|---|---|---|
| 109.0 s | 29° AoA, 142 kt, pitch stick centred | 36 deg/s | 29 | 71° in 2.6 s |
| 117.8 s | 35° AoA, 157 kt, pitch centred | 44 deg/s | 43 | 90° in 2.3 s |
| 125.3 s | 50° AoA, 172 kt, pitch released with it | 46 deg/s | 40 | 67° in 1.9 s, still rolling |
| 155.4 s | 50° AoA, 131 kt, pitch released with it | 36 deg/s (39 half a second later) | 35 | 79° in 2.8 s, still rolling |
| 69.3 s | 50° AoA, 130 kt, full aft held | 33 deg/s | 17 | 30° in 1.5 s |
| the other way: 95.5 s / 134.6 s | 50° held / 31° unloading | −25 / −56 deg/s | −7 / −14 | 19° / 44°, 1.1 s |

Three causes, each reproduced on the bench (replays from the logged state with the logged sticks):

1. **The roll the law allowed was more than the jet could stop.** The coupled check places the AoA limit where the weaker side keeps its share of the roll; it does not ask how much roll that share can stop. At 50° and 131 kt the reserve toward the side that brakes a roll into the heavy side was 29 deg/s² (22 at 128 kt; bench, 10-17 at 86-100 kt) and the law let that roll reach 32-50 deg/s. Bench, 103 kt, 32 deg/s held at 50°: not stopped in 3.5 s; measured brake 9.3 deg/s², the check's figure 10-17.
2. **The push governor planned the stop without the stabilators.** That governor paces a roll while the AoA sweeps toward zero so the yaw can follow. Since the F-22E it reads the yaw capacity without the stabs, because in a push the stabs go to pitch. With the pitch stick centred nothing is pushing: the AoA comes down at the flight path's own 12-15 deg/s and the stabs are free (the allocator used 6-16° of differential for yaw). Read without them the yaw toward the heavy side was 0-12 deg/s², the sweep alone took it, and the roll was not allowed to brake (109.0 and 117.8 s: allowed brake 0-8 deg/s²).
3. **Where that read was zero the governor made the released roll grow** (its rule: the roll follows the yaw down; up to 1.5 × the body roll at the sweep's start, 15 deg/s at least). 155.4 s: 35.9 → 39.0 deg/s in the half second after the release.

The bench plant's yaw matched this flight closely (equation error against the log, 6,287 samples with the load: yaw acceleration rms 1.3-3.0 deg/s² in every AoA band up to 55°, correlation 0.99; roll rms 3.8-11 deg/s²), so the bench stops below are worth reading. Per event the bench's bank after the release (0.5.0 laws) was 0.76 to 1.58 times the game's, so read the bench figures as sizes, not as exact values.

## Changes
1. **Stoppable roll rate** (`AsymStopTime`, dev setting, "Asymmetric load", default 0.8 s). With a lateral CG offset over 4 cm or a damage asymmetry, the flight-path roll the stick may ask toward each side is the coupled check's reserve on its braking side at the present AoA × AsymStopTime, 10 deg/s at least. Lower = slower rolls into the heavy side, sooner stops. 0 = off (0.5.0). It does not move the AoA limit.
2. **Push governor, roll stick released:** the no-stab discount is weighted by the nose-down body rate the pitch law commands (none at zero, whole from 10 deg/s). With the roll stick held it is unchanged (`PushGovNsByPitchCmd`).
3. **Push governor, roll stick released:** it may slow the roll's brake but not make the roll grow, and the brake keeps 10 deg/s² (`PushGovRelNoGrow`, `PushGovRelBrakeMin`).
4. Telemetry columns added at the end: `stopRateP, stopRateN` (the roll-rate limit each way, 999 = none).

Changes 2 and 3 act on the clean jet too (they are in the general roll law); change 1 only with a load.

## Bench results
Bench jet 22.8 t; 800 kg on the right mid wing = 16 cm. All with the nozzles locked unless noted.

### Replays of the 17:22 flight's releases (logged state and sticks)
| Release | 0.5.0: roll at release → bank after, stop time | **0.5.1** |
|---|---|---|
| 109.0 s | 42 deg/s → 68°, 2.15 s | **27 deg/s → 27°, 1.50 s** |
| 117.8 s | 51 deg/s → 96°, 2.22 s | **46 deg/s → 44°, 1.47 s** |
| 125.3 s | 53 deg/s → 47°, 1.41 s | 37 deg/s → 38°, 1.31 s |
| 155.4 s | 51 deg/s → 117°, not stopped in 2.6 s | **26 deg/s → 31°, 1.44 s** |
| 69.3 s (pitch held) | 38 deg/s → 34°, 1.48 s | **22 deg/s → 12°, 0.85 s** |
| 95.5 s (other way, pitch held) | −27 deg/s → 20°, 1.18 s | −23 deg/s → 13°, 0.90 s |
| 134.6 s (other way, unloading) | −61 deg/s → 37°, 0.94 s | −60 deg/s → 34°, 0.86 s |

Sideslip in these stops: 4.8° or less in both builds.

### Stop matrix (`bench/an/stopmat.sh`): full aft to the 50° limit, full roll stick 1.5 s, then released
| Case | 0.5.0 | **0.5.1** |
|---|---|---|
| Heavy side, 163 kt, pitch held | 59 deg/s, 72°, 2.16 s | 44 deg/s, 50°, 1.90 s |
| Heavy side, 104 kt, pitch held | 32 deg/s, not stopped in 3.5 s (60°) | **17 deg/s, 13°, 1.05 s** |
| Heavy side, 163 kt, both sticks centred | 59 → 71 deg/s, 195°, sideslip 20° | 44 → 47 deg/s, 75°, 2.5 s, sideslip 5.8° |
| Heavy side, 104 kt, both centred | 32 → 44 deg/s, 118°, sideslip 22° | 17 → 20 deg/s, 47°, 3.4 s, sideslip 12.5° |
| Other side, 172 / 105 kt, pitch held | 60 / 22 deg/s, 69° / 24° | 53 / 22 deg/s, 51° / 18° |
| Clean jet, 179 / 110 kt, pitch held | 66 / 34-37 deg/s, 45° / 33-34° | the same |
| Clean jet, both centred | 66-89°, 2.2-3.2 s | 69-89°, 2.1-3.3 s |

- At 78-80 kt and 50° (0.46 g, the jet is dropping) nothing stops a roll into the heavy side inside 3 s in either build; the rate limit holds it to 10 deg/s on entry there.

### The price: roll rate on the limit with the load
31-case load matrix, store on the left wing, full aft stick then full roll stick; peak flight-path roll, first roll to the left / to the right.

| | 0.5.0 | 0.5.1 |
|---|---|---|
| Nozzles locked, 16 cm, from 243 kt | 53 / 51 deg/s | 39 / 42 |
| Nozzles locked, 16 cm, from 189 kt | 48 / 44 | 31 / 36 |
| Nozzles locked, 31 cm, from 243 kt | 53 / 50 | 42 / 46 |
| TVC on, 16 cm, from 243 kt | 77 / 78 | 65 / 68 |
| TVC on, 16 cm, from 189 kt | 71 / 70 | 55 / 60 |
| TVC on, 31 cm, from 243 kt | 74 / 72 | 68 / 72 |

- AoA limits: unchanged in all 31 cases (51.3-51.8° locked, 49.6-50.2° at 31 cm with TVC on, opening to 75° with TVC on as before). The 18 cases without a roll input fly identically to 0.5.0.
- Peak sideslip in the 14 roll cases: lower in 7 (locked, 16 cm: 6.6 → 3.3°, 6.4 → 4.4°), within 0.2° in 5, higher in two TVC-on cases (5.8 → 7.5°, 6.4 → 8.4°).

### Regression
- Three switches off (`PushGovNsByPitchCmd=false PushGovRelNoGrow=false AsymStopTime=0`) against 0.5.0: 87 / 87 per-case files and 4 / 4 tables identical.
- Defaults against 0.5.0, clean suites: the per-case logs differ; of the tables' 105 rows 5 changed. The two released-roll cases at 65° improved in peak sideslip (13.4 → 12.0°, 16.2 → 14.6°); three rows of the damage table moved slightly (two leaf stops by 0.1; one released push-over: lowest AoA −26.1 → −24.8°). Stop / reversal and corkscrew tables: unchanged.
- An earlier form of change 2 that also acted with the roll stick held made one clean case worse (push-then-pull-through + roll at 243 kt: peak sideslip 12.9 → 16.1°). It was not kept.
- Loaded reversals (54 roll-spam runs, TVC on), better / same / worse than 0.5.0: peak sideslip 14 / 33 / 7; roll behind command (max) 20 / 34 / 0; AoA above the limit 10 / 39 / 5; bank travelled 6 / 13 / 35 (the lower rates).

## Not fixed, open
- **Both sticks centred at the 50° limit, nozzles locked:** the roll still runs 47-75° into the heavy side and takes 2.5-3.4 s (0.5.0: 118-195°, not stopped). The clean jet does 69-89° in the same manoeuvre in both builds, so this is the no-TVC roll stop from 50° itself, not the load. Not touched here.
- The 50° no-TVC limit was not lowered. `AsymStopTime` trades roll rate, not AoA.
- Not flown. Damage asymmetry still not benchable. Bench jet 2.5 t lighter than the flown one.
- `EngineOutAoACap` 22° still does not match the engine-out rule.

## Install
- **Main:** `plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.5.1 + meta.json. 0.5.0 kept as `Su30_VXN_FCS.dll.0.5.0.bak`.
- **Dev:** unchanged file. `AsymStopTime` appears in "Asymmetric load" after the first start.
- **Release:** `C:\Games\NO mods\Su30-FCS-0.5.1-release`.
- **Mod manager:** update the version there, or its modpack may put the old DLL back. Restart the game.

## Bench tools
- `bench/an/stopmat.sh <tag> [Name=value ...]`, summary `python3 bench/an/stopmat.py <tag> [<tag> ...]`.
- Replay of a log window: `bench.sh replay <log.csv> <t0> <t1> <out.csv> StoreKg=800 StorePart=Right_wing_mid DisableTVC=true`.

Previous notes: `claude/Su30_FCS_0.5.0_notes.md`.
