# Handoff: FCS CPU cost and the threading plan (5 Nov 2026, 02:15)

Start a new chat from this for the performance work. **Nothing is built yet.** Tom, 02:00: "no build yet". When he says go, the first build is §4.
It applies to all three FCS mods: the F-22E, the Ifrit and the Revoker. The governor stack they share is described in
`claude/RefGov_port_Ifrit_Revoker_handoff.md` §1.

## 0. Where things stand

**Installed:**
- F-22E **3.0.0**: md5 `096faf106b9d8edfc3a8bf9ef070823c`.
- Ifrit **0.11.0** and Revoker **0.8.1**, built in another chat. Since Ifrit 0.9 / Revoker 0.6 the estimator **drives** their law (see their notes).

**Tom's questions tonight, in order:**
1. "I thought replacing 13 devices with 1 would reduce the DLL size?"
2. "How much frametime loss?"
3. "Just some calculus and it costs the CPU a few ms?"
4. "2 is the safer option": moving work to another thread, which he picked over probing less often and over caching tables.
5. "No build yet."
6. "That would scale in a lobby, right?"
7. "Future AI optimisation?"
8. "Would real model-based FBW be harder to run?"
9. "The FCS seems very deterministic."
10. "Can the allocator and governor be optimised while universal with zero behaviour changes?"
11. "If they move to a different thread this is not worth it, right?"

The answers below are what he was told. Keep to them unless new measurements say otherwise.

## 1. Measurements (bench, F-22E 3.0.0 law)

**Setup:**
- 2-core 2.8 GHz Xeon VM, .NET 8, so timing noise is about ±30 %.
- `one iasKmh=350 …` with 40 s of corkscrews, released push-overs and reversals; plus level flight, reversals at 450 km/h, negative AoA, and 800 km/h.
- Median of 2–3 runs.

### DLL size: why it didn't shrink

Measured with `dnfile`:

| | 2.4.16 | 2.6.7 | 3.0.0 |
|---|---|---|---|
| file | 615,936 B | 627,200 B | 659,456 B |
| string literals (`#US`) | 358 KB | 362 KB | 366 KB |
| compiled code (all methods) | 166 KB | 172 KB | 194 KB |
| `FcsController` code | 86 KB | 91 KB | 106 KB |

- About 55 % of the DLL is string literals: config descriptions, log text, CSV names.
- The 12 devices deleted in 2.6.0 were small. The 2.5.x governor went in during the same span.

**Code growth, 2.6.7 → 3.0.0:**

| part | growth |
|---|---|
| `LateralLaw` | +7.5 KB |
| `CapacityEstimator` | +4.1 KB (new) |
| allocator reach pass + two-horizon pick | +1.7 KB |
| `PushPlanStep` | +0.8 KB |
| `PitchLaw` | +0.8 KB |
| telemetry: `WriteRow` | +1.9 KB |
| telemetry: passive probe `AirframeMomentAt` + `PassiveStep` | +2.8 KB |

### FCS time per physics tick (60 Hz, main thread)

| config | median | p95 | worst |
|---|---|---|---|
| as shipped, logging off | ~0.85–0.95 ms | ~2–2.6 ms | 5–17 ms (VM noise + GC) |
| as shipped, logging on | ~0.9–1.2 ms | ~2.3 ms | |
| `CapEstOn=false` (sizing only, changes the law) | ~0.65 ms | ~1.6 ms | |

**Where it goes, per tick:**

| part | time |
|---|---|
| table rebuild (`eff.Evaluate`, FcsController ~4079) | 9 effectors, **239 aero evaluations**, ~0.15–0.2 ms |
| allocator solves | **7.0–9.3 per tick**: 7.0 hands-off, 7.8 in corkscrews and push-overs, 9.3 in reversals; ~70 µs each; ~0.55–0.95 ms |
| … of which optimisation iterations | ~0.2–0.3 ms |
| … of which the per-surface scan | ~0.3–0.5 ms |
| … of which the paired search | ~0.05 ms |
| estimator (`capEst.Step`, ~6 of those solves) | **~0.42–0.6 ms** |
| laws and everything else | ~0.15 ms |

- That's about 2.4 M cycles per tick, about 5 % of one core at 60 Hz. All of it currently lands on the frame.
- **The game estimate** (unverified): 1–2 ms per physics tick for the player's jet. That allows for Mono being ~1.5–2.5× slower than .NET 8 (an assumption) and his 5950X being faster than the VM.

### Who pays

- The FCS attaches only to `aircraft.LocalSim` jets (`Hooks.cs:170`, `Registry.cs:328`): the player's jet, plus AI when `AIFlightControl` is on.
- **Other players' F-22Es cost nothing:** their own machines run them.
- **A host pays per AI F-22E,** about the player's cost each. The estimator isn't gated for AI; only `PassiveStep` and the CSV are player-only.
- **Open:** do the aero hooks (LiftShape, BodyLift, Camber, Downwash, StallStagger) also run for remote players' F-22E parts? They aren't in the timer. If they do, that part scales with lobby size. Check it in the same pass.

## 2. Determinism (what Tom was told)

- **Output: deterministic.** Same input gives the same commands, bit for bit. The identity checks rely on this.
- **Timing: not deterministic.**
  - The allocator iterates until it converges.
  - Some branches (reach re-solves, two-horizon pick) run only sometimes.
  - Managed GC and JIT pause at uncontrolled times.

  That's fine for a game, where a slow tick only delays a frame. In a certified FCC it would be a missed frame deadline.
- **Real model-based FBW** (F-35: dynamic inversion, onboard airframe/engine model, effector blending & limiting; sources: Bosworth, IEEE CSS
  IoCT; D. Nixon, Lockheed / MathWorks slides): the same structure, but cheaper per frame.
  - It reads coefficient tables, not 239 part-by-part aero evaluations.
  - It allocates once per frame with bounded methods. There are no "what-if" probes.
  - It is much harder overall: redundancy and voting, certification, proven worst-case timing on slow hardware.

## 3. The plan Tom chose: option 2, threading the estimator (behaviour-identical by construction)

**Why it is behaviour-identical** (verified in 3.0.0's code):
- Every reader of the estimator's output (`capEst.*`) is inside `PitchLaw` (6260–) or `LateralLaw` (8207–). Those run at `Tick` lines 4720–4721.
- The estimator runs later in the same tick, at 5317, after the allocator.
- **So the laws already use the previous tick's estimate.**
- Running `capEst.Step` on a worker from the end of tick N, and collecting it at the start of tick N+1 before the laws, gives the same numbers.
- The other `capEst` readers are lines 7046 (PitchLaw); 8595, 8596, 8600, 8659, 9194 and 9299 (LateralLaw); and 9586 (telemetry).

**What moves, and what stays:**

| part | where | time | why |
|---|---|---|---|
| estimator probes | worker | ~0.45 ms | result is used next tick anyway |
| table rebuild | **main** | ~0.15–0.2 ms | needed this tick; moving it adds a 16.7 ms loop delay and changes the law |
| the 2 real solves | **main** | ~0.15–0.2 ms | same |
| laws, including the governor logic | **main** | ~0.15 ms | same |

The governor's logic is cheap. Its cost is the estimator's probing.

**Expected:** main thread about 0.85 → 0.4 ms per jet on the bench. The probes run in parallel across cores, one job per jet.

## 4. The first build, when Tom says go (patch bump: F-22E 3.0.1; the stock jets get the same once ported)

**Step 1: a tick timer, first and alone.**
- A `Stopwatch` around `FcsController.Tick` (plus `VmcApply`).
- Every 10 s the log gets one line per jet: mean, p95 and max µs, the jet count, and AI vs player.
- A CSV column `fcsUs` when logging is on.
- He flies one log of the current build. That gives the real in-game baseline.

**Step 2: thread the estimator.**
- **The worker owns a copy of everything the probes touch.** `CapacityEstimator.Step` calls `alloc.Solve` and writes effector commands, then restores
  them. Sharing the live `effs` / `alloc` with the next tick would race.
  - Snapshot each effector's grid (`grid`, `M[]`, `N`), `now`, `cmd`, `lo`/`hi`, rate, `reachTau`, `twoHorizon` and the regularisation fields.
  - Give the worker its own `Allocator`, with the same statics, `axisWeight`, `fightK` / `pitchFightK`, fight groups, inverse inertia (`SetInverseInertia`)
    and `PairedTrimSearch`.
  - Buffers are allocated once and reused: double-buffered, no per-tick garbage.
- **The inputs to Step** are `effs`, `alloc`, `vector7`, `IaInv`, `zero4`, `num` (dt), `unmetRaw`, `alpha` and the rudW product. Copy them by value at
  the call site.
- **Output:** the estimator's fields (`capZP/N`, `capZeP/N`, `capTrZeP/N`, `capZeNsP/N`, `wdZNs`, the `ce*` telemetry). Publish them as a struct swapped
  at the join.
- **Join** at the start of the next `Tick`, before `PitchLaw`. Wait if not finished: identical behaviour, just slower that tick. Log the count of waits.
- **Threads:** plain `System.Threading` (Unity's Mono supports it). One persistent worker, or ThreadPool items with a `ManualResetEventSlim`. No Unity
  API calls in the job.
  - `UnityEngine.Vector3` / `Mathf` are plain structs and static maths, safe off the main thread.
  - **Check** that no effector `MAt` / `Lookup` path calls into the game's objects. It shouldn't: `M[]` is precomputed by `Evaluate` on the main
    thread.
- **Telemetry:**
  - The CSV row for tick N currently reads tick N's `capEst` values (`WriteRow`, 9586). Async, those aren't ready at line 5385.
  - Either snapshot the row's other fields at tick N and append the `ce*` columns at the join, or write row N at the start of N+1 from a snapshot.
    The CSV content must stay identical.
  - `PassiveStep` (2.31.2, telemetry only) calls `AirframeMomentAt`. Check whether that touches game objects before moving it; if unsure, leave it on
    the main thread.
- **Switch:** `EstimatorThreaded` (static, default on in the release), so the bench can compare threaded and synchronous.

**Verification (Tom's rules: identity is byte-identical, say "bench-only" until flown):**
- Bench suite (rev / cork / reg / util / dmg) plus the replays (hybrid released and held, free): threaded vs synchronous vs 3.0.0, **byte-identical**.
  Use the eval / idrun / regcheck tooling of the 2.44.1 identity check (`F22E_NO_FCS_2.44.1_notes.md`).
- The timer's numbers, bench and game, before and after.
- AI: one bench run with several controllers ticking (or an AI-hosting flight) to confirm per-jet jobs run in parallel.

## 5. Later options (not chosen yet; the order Tom was given)

1. **Behaviour-identical code optimisation** (same arithmetic, same order). Tom's conclusion (02:14): **not worth it once the estimator is off-thread**.
   - On the main thread it only speeds the 2 real solves, saving ~0.03–0.06 ms per jet.
   - Do it only if AI-heavy hosting still hurts after §4.
   - **Prototyped tonight** on a scratch copy of the bench (not in any tree; the container is ephemeral). In the allocator's per-surface scan
     (`SolveCore`, the loop over `act` with `effector5.grid`):
     - the group roll and pitch sums (`GroupRoll`, `GroupPitch`) and the surface's own `MAt(x[j])` were memoised per surface, because they don't
       change during that surface's scan (non-split case only; the split case sets `x[j]`);
     - the three `Mathf.Sqrt(axisWeight.*)` in `E()` were cached, keyed on exact component equality.
   - **Result:** per-tick output byte-identical on 4 manoeuvres (1,232–2,432 rows each). Gain maybe 10–20 % of the scan, inside the VM's ±30 %
     noise. **The real hot spots need a sampling profiler** (e.g. `dotnet-trace`), not stopwatches on this VM.
   - **What does NOT qualify as zero-change** (it goes through the no-trade-off bench rule instead):
     - reordering sums (SIMD);
     - float/double changes;
     - fewer iterations or early convergence exits;
     - coarser grids;
     - caching tables across ticks with a tolerance;
     - probing less often.
2. **AI F-22E FCS one tick behind, fully off the main thread.** The main thread only snapshots the state and applies last tick's commands.
   - It removes nearly all AI cost from the frame, at 16.7 ms of extra loop delay for the AI only.
   - Must pass the bench AI manoeuvre set first (`bench ai`; `bench/scripts/aimatrix.py`, whose paths point at a dead `/tmp/claude-0/bench` and need
     fixing; 1.8.22 scored 23/24 settled).
3. **AI estimator probes every Nth tick** when nothing is near a limit. A small extra saving, and it changes what the AI governor sees.
4. **Last resort:** distance or level-of-detail switching (risk of transients when the law changes), or `AIFlightControl = false` (exists; stock AI,
   zero cost).

**Rough host scaling** (bench numbers, 8 AI F-22Es):
- today ~7.7 ms per tick on the main thread;
- with §4 ~3.6 ms;
- with the AI one-tick-behind option ~0.4–0.6 ms.

## 6. Recreating the bench timing

- The bench already prints `FCS Tick: N us/tick` with `timing=1` (`Plant.TickSw` around `fcs.Tick` + `VmcApply`). It includes start-up; use runs
  of ≥ 20 s.
- **Tonight's breakdown came from a scratch instrumentation**, gone with the container:
  - `PerfT` static stopwatches around `capEst.Step`, `Allocator.Solve` (count and time), `eff.Evaluate` (count and grid points) and the three
    `SolveCore` phases;
  - a per-tick sample list in `Plant.Tick` with the first 2 s dropped;
  - printed as `PERF median / p95 / max`.

  Re-add it in the bench's synced copy (`bench/fcs/`, then `dotnet build -c Release -o binX`), never in the source.
- **Kill the compiler server before timing:** `dotnet build-server shutdown; pkill -f VBCSCompiler`. A lingering VBCSCompiler at ~70 % CPU skewed the
  first runs.
- `TelemetryEnabled=false` and `CapEstOn=false` are settable on the bench (`Name=value`).

## 7. Rules

- **"Zero behaviour change" means byte-identical bench output.** Anything else is a law change (minor bump) and goes through the no-trade-off rule.
- Measure before and after in game: the tick timer comes first.
- Don't overstate. Say "compiled / bench-only / flown", with the numbers.
- Patch bump for behaviour-identical work. Release workflow as in `claude/F22E_FCS_handoff_2.35.3.md` §6. Install with `.dll.<prev>.bak`, and check
  the md5.
- **Bridge note:** a DLL write can fail with "fetch or write failed" while other files in the same commit succeed. Re-check every file after
  installing, and retry once.
