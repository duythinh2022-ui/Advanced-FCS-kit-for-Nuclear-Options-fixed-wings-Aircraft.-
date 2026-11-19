# F-22E FCS 3.1.2: the asymmetric-load sweep reads the parts once per tick and computes its points on several cores

Base: 3.1.1. Patch bump: no behaviour change. Compiled; benched byte-identical to 3.1.1; **not flown**. 3.1.1 kept as `.dll.3.1.1.bak`.

## Tom (5 Nov, 23:35)
"do it": the asymmetric-load AoA sweep, the largest single cost in the 23:31 log.
- ~468 µs per tick on average, out of ~1180 in the air.
- It runs on 62 % of ticks, at ~760 µs median when it runs.
- 3 AoA points per tick, ~250 µs each.

## What changed (`AsymSweepPoints`, Dev setting `ParallelSweep`, default on)
1. **`SwGather` (main thread, once per sweep tick)** reads each aero part's Unity state into a snapshot:
   - lift-normal rotation;
   - `worldCenterOfMass + rotation × centerOfLift − cg`;
   - drag area;
   - foil and `0.5·ρ·S·eff` (only for the non-surface parts that have one).

   Every point used to read these again (3× per tick). Nothing moves the parts inside a tick, so the values are the same. The expressions are the same too.
2. **`AsymSweepPoint(i)`** is the per-point body, unchanged apart from reading the snapshot.
   - The surface terms (`EvalFlow` over the grids) are pure maths on what the table rebuild gathered.
   - Each point writes only index *i* of the sweep arrays.
3. **The tick's points run in parallel** (`IndexFork`: the shared pool, at most `FcsPool.Helpers` helpers, the main thread taking part), joined before anything reads the arrays.
   - The laws and the cap filter read the arrays in the same tick, so a cross-tick deferral was not an option.

**Thread safety:** `PartForce`, `Downwash.Apply`, `RudderAuthAt`, `AccRoll`/`AccYaw`, `EvalFlow` and `AirfoilTable.Get` only read. Quaternion's native maths is `IsThreadSafe`. No Unity object is touched off the main thread.

## Bench identity
| check | result |
|---|---|
| Suite per-case files vs 3.1.0 | 153 / 153 identical |
| Replays vs 3.0.3 | 52 / 52 identical |
| Corkscrew bench CSV md5 (sweep on / off / 3.1.1) | identical |
| Game CSV, sweep on / off / 4 benches at once on 2 cores (timer columns aside) | 0 / 2316 rows differ each |

## Bench timing: not resolvable
- The same 3.1.1 build measured the sweep at 104–225 µs across runs on this VM.
- The VM's stub Unity makes the part reads that the gather removes nearly free.
- With 2 cores there is 1 helper.
- **The in-game timer decides.** Watch the "asym sweep" figure in the second timing line, and `tSweepUs` in the CSV. `ParallelSweep` can be switched off mid-flight for an A/B.

## Install
- **Main:** md5 `408870f6aee47396bd95295d356ea8b2`; 3.1.1 kept as `.dll.3.1.1.bak`.
- **Dev:** md5 `ae30c433c26c7b65bff35181e1f1335c`.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.1.2-release`. The mod zip holds the main DLL only.
