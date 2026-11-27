# F-22E Strike Raptor FCS 4.1.3: diagnostic build for the performance measurement

Base: 4.1.2. **No control-law change.** Measurement only.

**Changelog:** The tick timer now accounts for the whole FCS tick in 13 segments, reports managed-heap growth and garbage collections, logs slow ticks with their breakdown, and records which performance switches were on; the Telemetry setting now takes effect in flight.

**Status:**
- Compiled against the game's assemblies; built twice, same checksum.
- Bench (.NET 8): suite 153 / 153 files and 5 / 5 tables identical to 4.1.2; the 88 loaded-reversal runs identical; the game-format CSV identical to 4.1.2's in every column except the timers and the 16 new ones.
- Game's Mono runtime (under Wine): loads, allocator bits unchanged. The new timer code runs only in the game's hook, so it has **not run anywhere yet**. It is wrapped so an error in it logs a warning and cannot switch the FCS off.

## Tom (10 Nov)
- 23:35: "whats the measurement step" → 23:38: "do it"

## What is added
**1. The whole tick in 13 consecutive segments** (checkpoints in `FcsController.Tick`; they sum to the tick except the hook's own call and timer overhead, which is reported):

| segment | what is in it |
|---|---|
| join + last row | waiting for last tick's estimator job, finishing last tick's CSV row |
| mass props | mass properties (the old `mass props` timer) and the setup before it |
| air data + gear | air data, rates, filters, the gear loop |
| flaps | flaps (old timer) |
| surface prep | rudder authority, the surfaces' per-tick loop, sideslip rudder, roll-pitch gate |
| tables | table rebuild (old `tables`) |
| pre-law | INDI filters, effector sums, roll / yaw authority, pitch need, allocator weights, fight terms, envelope |
| asym step | asymmetric-load step (old timer: moment, slip probe, sweep inside) |
| mode + stick | mode logic, gear states, stick filters, airbrake |
| ground steering | (old timer) |
| laws + allocator | the air or ground law block: laws, allocator solves, estimator kick inside |
| apply | writing the commands to the effectors |
| csv capture | the CSV row capture |

The old "still unaccounted 93 – 99 µs" is spread over join, air data, surface prep, pre-law, mode and apply.

**2. Garbage:** the managed heap is read before and after each tick (`GC.GetTotalMemory`, about 10 ns a read), and the collection count. On the game's Mono the heap is counted in 4 KB blocks, so one tick's figure is coarse; the 10 s average over ticks without a collection is the allocation rate. Ticks during which a collection ran are counted with their mean time. (`GC.GetAllocatedBytesForCurrentThread` exists on that runtime but returns 0, tested.)

**3. Slow ticks:** any of the player's ticks over `SlowTickLogUs` (Dev, "0. Debug", default 1,500 µs; 0 = off) gets its own log line: the 13 segments, VmcApply, whether a collection ran, heap growth, tables / allocator / sweep / estimator wait. At most 10 lines per 10 s.

**4. Switch state:** each 10 s line names the switches (`Telemetry`, `NativeAllocator`, `NativeSweep`, `NativeTables`, `SweepGridCache`, `ParallelTables`, `ParallelSweep`, `ParallelAllocator`, `EstimatorThreaded`) and says **"CHANGED during this window: mixed"** if one was flipped inside it. The CSV has them per row as a 9-digit `swMask` in the same order.

**5. `Telemetry` takes effect at once:** off in flight stops the CSV rows (the file stays open), on resumes them. Needed to time the CSV's own cost.

**New CSV columns** (after `ppFloor`): `sgJoin, sgMass, sgAir, sgFlap, sgSurf, sgTab, sgPre, sgAsym, sgMode, sgGnd, sgLaw, sgApply, sgRow` (µs), `heapB` (bytes), `gcRan`, `swMask`.

**New log line** (every 10 s, player): `F-22E FCS timing, whole tick in segments (us per tick, N ticks): … | sum, VmcApply, call + timer overhead | heap growth … bytes per tick …; … ticks with a collection … | switches: …`

## The flight test
**Setup:** `TickTimerLog` on (default), `Telemetry` on. Same mission and jet each flight, no AI F-22Es if possible, other apps as usual (note anything unusual).

**Routine** (repeat it through the flight, so every setting sees similar flying): a full pull, a few full-stick rolls and reversals, a corkscrew, a push-over, about 30 s of level flight. An asymmetric load is optional; if used, keep it the same in every flight.

**Toggles** (Configuration Manager, "0. Debug"; hold each at least 30 s so it gets one clean 10 s window, then set it back):

| step | change | measures |
|---|---|---|
| 1 | all defaults | baseline |
| 2 | `Telemetry` off | CSV cost (log lines only for this step) |
| 3 | `ParallelTables` off | threading of the tables |
| 4 | `ParallelSweep` off | threading of the sweep |
| 5 | `ParallelAllocator` off | threading of the allocator |
| 6 | `EstimatorThreaded` off | the estimator's worker |
| 7 | `NativeTables` off | Rust tables vs C# |
| 8 | `NativeSweep` off | Rust sweep vs C# |
| 9 | `NativeAllocator` 0 | Rust allocator vs C# (A/B for the Discord question) |
| 10 | back to defaults | baseline again (drift check) |

- Two or three flights. The order can stay the same; I compare like windows across flights.
- No need to note times: the log and the CSV record the switches.
- `NativeAllocator 0` and the threading switches change the arithmetic or ordering only in the last digits (equal-cost picks); the flying is the same law.

**What I give back:** per segment and per setting, mean / median / p95 and the spread across flights; heap growth per tick and collections; what the slow ticks were. From that, a measured list of what is worth optimising.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.1.3 + `meta.json`. `F22E_FcsCore.dll` unchanged (4.1.0's). 4.1.2 kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.1.2.bak`.
- **Dev:** the same DLL; one new key, `SlowTickLogUs`, appears by itself. No migration.
- **Release:** `C:\Games\NO mods\F22E-FCS-4.1.3-release`.
- **Mod manager:** update the version there, or its modpack may put an old DLL back.
- When the measurement is done, 4.1.3 can stay (the extra cost is a few dozen nanoseconds per checkpoint and two heap reads per tick) or go back to 4.1.2.
