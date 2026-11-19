# F-22E FCS 3.1.0: table rebuild and allocator on several cores, PassiveStep archived, ejection fix, per-phase timer

Base: 3.0.4. Minor bump, at Tom's request ("bump to 3.1.0").

**Status:**
- Compiled against the game's assemblies.
- Benched byte-identical to 3.0.4, with every switch on and with every switch off.
- **Not flown.** 3.0.4 is kept as `.dll.3.0.4.bak`.

## Tom (5 Nov, 22:48–22:56)
- "archive passive step then"
- "also make allocator and rebuild table multithreaded"
- "make sure that all of the multithreaded fcs mods acts nicely together"
- "bump to 3.1.0"
- Relayed from Discord: "your fcs runs even after I eject"

## 1. Table rebuild on several cores (`ParallelTables`, Dev "0. Debug", default on)
- **The split:** `SurfaceEffector.Evaluate` = `EvalGather` + `EvalCompute`.
  - **EvalGather (main thread):** reads the game's objects (transforms, rigidbodies, the game's job fields) into the effector's own fields.
  - **EvalCompute:** pure maths on those fields — the ~31-point table, the live moment, the pitch post-processing, the live node.
- **How it runs:**
  - The surfaces' EvalCompute runs on up to `FcsPool.Helpers` pool threads plus the main thread.
  - The nozzles' `Evaluate` stays whole on the main thread meanwhile, because it reads the engines' transforms all the way through.
  - The sum into the total moment then runs in the same order as before, so it's bit-identical.
- **Thread safety:** Quaternion's native `AngleAxis`, `Inverse` and `EulerToQuaternion` are marked `IsThreadSafe` in the game's `UnityEngine.CoreModule`, checked in its metadata. No Unity object is touched off the main thread.

## 2. Allocator: the two-horizon reach plan on a second core (`ParallelAllocator`, default on)
- **Why it's independent:** when the two-horizon pick is active, `Solve` runs three core solves. The third (the reach plan) depends only on the commands from before the solve.
- **How it runs:** it starts on a mirror (copies of the effectors and an allocator) while the main thread does the destination solve and the reach pass. The result is taken at the point where the sequential version runs its third solve: same cost, residual, commands and pick.
- **When it starts:** only if the previous solve needed the pick. The pick comes in runs during manoeuvres; otherwise it would be wasted work on other cores.
- **The rest of the allocator stays sequential.** Each solve feeds the next, and the per-surface scan is sequential by design.

## 3. Estimator (3.0.4) moved onto the shared pieces
- Now uses the shared `EffectorMirror` and `Claimable`.
- If no pool thread has started the job by the time the next tick needs it, the main thread runs it inline. That tick costs what 3.0.3 cost; it never stalls.

## 4. Working together: several FCS mods (F-22E, Ifrit, Revoker, Compass) and several jets (`ParallelFcs.cs`)
- **One shared pool:** everything runs on the process-wide .NET thread pool, with no private threads per mod. The CSV writer is the one exception: one background thread, player jet only, while logging.
- **Capped fan-out:** each jet uses at most `FcsPool.Helpers` helpers (a quarter of the logical cores, 1–3; 3 on a 5950X).
- **The main thread always takes part:**
  - The fork-join hands out items with an atomic counter (`Interlocked`), so no surface is computed twice.
  - A job no pool thread has started is run inline by whichever thread needs it.
  - So a pool kept busy by other jets or other mods can delay the speed-up, never stall a tick.
- **Core choice:** the OS scheduler places the threads; there is no pinning. Unity's own job workers (32 PhysX workers in Tom's log) and render threads share the same cores.

## 5. PassiveStep archived
- **What was removed:** the 2.31.2 telemetry-only passive-airframe probe — the method, its fields and its tick call.
- **Where it went:** `C:\Games\NO mods\F22E-FCS-retired-passive-step\PassiveStep_archived_3.0.4.cs`.
- **The CSV:** its columns (`psAX..psNr`, `cePassXP..cePassSN`) are kept and write 0, so the log columns don't shift.
- **Kept:** `AirframeMomentAt`, because the bench's `sliptruth` tool calls it.
- **Check:** the suite was identical without it (153/153 files), confirming it drove nothing.

## 6. Ejection
- **The bug:** after ejection `aircraft.Player` is null, so the hook treated the empty jet as an AI jet, and with `AIFlightControl` on the FCS kept flying it.
- **The fix:** once `aircraft.HasEjected()`, the hook disengages the FCS and the stock fly-by-wire takes over, the same as when the FCS is disabled.
- **Other jets:** the Ifrit, Revoker and Compass hooks need the same line.
- **Status:** not bench-testable (game hook); not flown.

## 7. Per-phase timer
- **CSV:** new columns `tRebUs`, `tAllocUs`, `tKickUs`, `tRowUs` after `estThr`:
  - table rebuild;
  - main-allocator solves;
  - estimator hand-off (or the whole estimator when single-threaded);
  - CSV capture.
- **Log:** the 10 s `TickTimerLog` line now shows the same split plus "rest", which switches are on, and how many estimator joins waited or ran inline.

## Bench identity (all switches on, and all off)
| check | result |
|---|---|
| Suite per-case files vs 3.0.4 | **153 / 153 identical** (on and off) |
| Replays vs 3.0.3 (10 released, 10 held, 25 other pushes, 7 free windows from the 5 Nov logs) | **52 / 52 identical** (on and off) |
| 40 s corkscrew case: bench CSV md5, on / off / 3.0.4 | identical |
| Game CSV, on vs off vs overlapped (no early join) | 0 / 2316 rows differ |
| Game CSV vs 3.0.4, aside from the archived passive columns and the timer columns | 0 / 2316 rows differ |
| Clone completeness (reflection, every Effector / non-scratch Allocator field; clean and parts shot off mid-flight) | 0 mismatches |
| Four benches at once on 2 cores, all parallel, no early join | 4 × 2316 rows identical, no deadlock |
| The reach-plan path was exercised | 25 starts, 16 used, 9 picked the reach plan in the corkscrew case |

## Bench timing (main-thread Tick + VmcApply, µs; telemetry off; 2-core VM, ±15 %)
| | mean | median | p95 |
|---|---|---|---|
| all off (≈ 3.0.3) | 719–732 | 622–727 | 1205–1619 |
| estimator threaded only (≈ 3.0.4) | 531–538 | 526–547 | 1130 |
| **all on (3.1.0)** | **429–503** | **430–529** | **823–999** |

- **Per phase, all on:**
  - table rebuild: 94–104 µs (112–122 single-threaded);
  - allocator: ~80 µs;
  - estimator: hand-off ~7 µs (~250 µs when single-threaded);
  - rest: ~200 µs.
- **The estimator thread is still the large gain.** The parallel rebuild and reach plan add about 5–15 % on this VM, close to its noise.
- **The table fork has overhead:** with only 1 helper here, one run measured slower (141 vs 119 µs). On a 16-core machine it may do better. The game's per-phase timer will show; `ParallelTables` can be switched off mid-flight.

## Not done (Tom's call pending)
- **AI one tick behind** (Dev switch, off by default).
  - **Why it isn't in:** the allocation section is 2–5 dependent solves interleaved with ~400 lines of controller state that the next tick reads. Doing it cleanly means splitting `Tick` into gather / compute / apply for AI jets.
  - **What it needs:** a real restructure, plus the bench AI manoeuvre set before shipping.

## Not checked
- Not flown. Multi-jet in game not tested; the bench has one plant, and the 4-process run is the stand-in.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 `94fb6438b4518dfb1357f37b79a8b325`) + `meta.json` 3.1.0; 3.0.4 kept as `.dll.3.0.4.bak`.
- **Dev unlock:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.1.0 (md5 `c592e600c27e78332fd3189b5a0fd491`); 3.0.4 kept as `.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.1.0-release`.
  - The mod zip holds the main DLL only; the Dev DLL ships separately as `(Optional)Aryx_F22E_StrikeRaptor_FCS_Dev.zip`.
  - `meta.json` gameVersion 0.34.6.
