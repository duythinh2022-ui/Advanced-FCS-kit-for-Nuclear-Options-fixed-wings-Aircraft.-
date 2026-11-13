# AGENTS.md: operating manual for AI models working on this FCS

You are working on a flight control system for the game Nuclear Option (Unity, BepInEx 5 + Harmony plugins, C# 9 on Mono).
This file is the compact, rule-first version of the full manual, `MANUAL.md` (`README.md` is only the short front page;
every "README §N" below means `MANUAL.md` §N). When this file and a source comment disagree, the source is the
truth for *what the code does*; this file is the truth for *how to work on it*. Section numbers like "README §9.1" point into
`README.md`.

## 1. Orientation (read once)

- Six complete reference FCS plugins live in `reference/`, forked in this order (each from the one before):
  - `F22E-FCS-4.3.0/Aryx_F22E_StrikeRaptor.FCS/`: modded F-22E, rudders, stabs, flaperons, pitch-only TVC, engine class `Turbofan`;
    relaxed-to-neutral airframe X2e (README §8.15); `studies/` holds the airframe study kit, push scoring and the
    loaded-reversal hand-off (excluded from the build).
  - `Ifrit-FCS-1.3.0/Ifrit_KR67.FCS/`: stock KR-67, canted tails = pitch + yaw (no rudders), split decelerons, pitch + roll TVC, `Turbojet`.
  - `Revoker-FCS-1.4.1/Revoker_FS12.FCS/`: stock FS-12, canards (classified `Stabilator`), one rudder, delta elevons (outer
    `ControlSurface`, inner `HingeFlapEffector`), pitch + yaw TVC, `Turbojet`.
  - `Compass-FCS-0.5.0/Compass_TA30.FCS/`: stock T/A-30 trainer, tail slabs, strakes, ailerons + flaps, **no TVC**, fixed-nozzle twin.
  - `Ternion-FCS-0.4.0/Ternion_FS3.FCS/`: Blueprinter FS-3, canards **and** aft elevators (two `Stabilator` pairs, `isCanard`),
    twin rudders, no TVC; `StructureFix`, engine-out zero-thrust match, spin recovery.
  - `Su30-FCS-0.6.0/Su30_VXN.FCS/`: kyleisred's Blueprinter Su-30VXN, canards (pitch-only) + stabs + rudders, canted single-axis
    nozzles (`CantedTvc`), `ThrustLineAtCg`, low-speed TVC roll, measured AoA/g capture, soft AoA limit.
- **What changed since kit 1 (5 Nov) is README §19**, one entry per problem: §19.1–19.7 up to kit 2.0 (push-overs, the F-22E
  airframe, threading, fuel, no-TVC jets), §19.8–19.14 up to kit 3.0 (native core, coupled asymmetric-load check, loaded
  reversals, flying backwards, ground brakes, Su-30 0.2.4–0.6.0). Read the relevant part before touching any of those.
- **Every jet carries a native (Rust) core** `<Jet>_FcsCore` (`native/fcs_core/`, exports `f22e_fcs_` / `ifr_fcs_` / `rev_fcs_` /
  `cmp_fcs_` / `tern_fcs_` / `su30_fcs_`): the allocator's solve, the surface-table grid kernels and the coupled check's LP.
  It is embedded in the plugin DLL (`native/prebuilt/<Lib>.dll`), extracted to `BepInEx/cache/<Lib>/<sha16>/` and loaded by full
  path; ABI number check and self-tests against the C# twins at load; C# fallback (README §2.18, `native/README.md`).
- Each tree = plugin source + `DevUnlock/` + `bench/` (offline 6-DOF sim that compiles the real FCS sources against stubs) +
  `build.sh` (real DLL) + `bench.sh` (bench front end) + `native/` (Rust core, prebuilt DLL, `verify/` game-runtime checks) +
  `release-notes/`.
- The source in this kit rebuilds the shipped release DLLs byte for byte (README §17). Keep it that way unless a change is
  intended.
- `docs/MODULE_MAP.md` gives every file a layer: LAW, MODEL, AERO, SYSTEM, GLUE, EXTRA, and how far each jet moved from its
  parent. `docs/CONFIG_REFERENCE.md` lists all 492 config keys with the six jets' defaults. Regenerate both with
  `tools/gen_*.py` after changing sources.
- README Part IV: §21 is a control theory primer (AoA, static margin, PID limits, velocity-vector roll, INDI, allocation,
  capture); §22 the workflow every change went through and the tools; §23 the caveats of porting by hand; §24 airframe aerodynamics recommendations (margin, surfaces,
  strakes, thrust line, ground). If a user is new to
  flight control, point them to §21 before explaining code.
- `NO-FCS-Kit-Manual.pdf` is built from `MANUAL.md` by `tools/build_manual_pdf.py` (pandoc + Playwright Chromium). Rebuild it
  whenever MANUAL.md changes, or the two will disagree. Never build it from the short `README.md`.

- Why it exists: the game's stock `ControlsFilter.FlyByWire` is a pitch-rate PID with an open-loop g target, a soft AoA
  limiter that never pushes the nose down, a fixed-rate body-axis roll loop and a yaw-rate damper, driving fixed surface
  mixing with no stall, saturation or damage awareness (README §1, read from the game's IL).

## 2. Architecture in 12 lines

1. `Hooks.FilterPrefix` (prefix on `ControlsFilter.Filter`) → `Registry.GetOrCreate` → `FcsController.Tick(inputs, flightAssist)` → `VmcApply`. Returning false replaces stock FBW.
2. `Tick`: mass props → air data **at the CG** → effector tables (`eff.Evaluate`) → INDI filters → authority reads → allocator weights/costs → envelope & damage sweeps → mode → stick shaping → `PitchLaw` → `LateralLaw` (+`PushPlanStep`) → INDI moment request + clamps → `alloc.Solve` → `capEst.Step` (since 3.0.4 kicked to a worker, joined at the top of the next tick) → telemetry (captured on the main thread, written by a background thread).
3. INDI: M_req = Σ Mnow + I·(ν − ω̇_f), ω̇_f filtered with `IndiTau` 0.04 s. Unmodelled airframe effects cancel; only effector models must be right.
4. Allocator: box-constrained Gauss–Newton/LM over every `Effector`'s moment table; costs μ, λ (scaled by `regScale` ∝ q̄²), pair (sym/anti), fight (roll/pitch groups), budget (nozzles), stall windows, reach pass + two-horizon pick (`reachTau` 0.1 s).
5. `Effector`: `min/max/lo/hi/now/cmd/rate`, grid `M[N]` (x roll, y pitch, z yaw, aero frame), `Mnow`, `SetLiveNode` (live point as a table node for roll/yaw).
6. `SurfaceEffector`: game plate equation at the surface's local AoA across its travel; stall/reversal-aware; stabs use a monotone pitch column (`linearPitch`); flaperons/ailerons/rudders `noPitch` unless a feature says otherwise; camber/stagger/downwash mirrored.
7. Pitch law: default `GAndPitchRate` (rate where full g is out of reach, g where in reach), released stick = zero-rate hold, AoA/g limits, kinematic capture, low-thrust cap, protection with `StickCancelsLimit`.
8. Lateral law: velocity-vector (stability-axis) roll, coupled axis past `CoupledAxisAoA` 36°, sideslip loop, roll/yaw ceilings from live authority; flying backwards the roll share is |cos α| (`ReversedFlowNoseAxis`, `LatAxisAlpha()`).
9. Governor = capacity estimator (measured + committed within 0.15 s + allocator probe, per axis and side, read next tick) + consumers: trade (`PullRollTrade` 0.7), steady-rate cap (`RollPaceRateBlend`, `RollStopSideslip`), pace window (allocator unmet), released-stop yaw brake, push plan (`PushPlanK0/K`, `PushoverSideslip`; F-22E floor `PushOverMinRate` 23, `PushPlanRise` 40), push governor (sweep cap faded in 5→15 °/s of roll, body-rate floor, 3.1.7 hand-off switches), fin-loss devices.
10. Damage: asymmetric-load AoA cap (coupled check `CoupledCap.cs`: LP for flight-path roll left each way with load and pitch held; off on the F-22E), yaw health, yaw-target clamp, decelerons, fin-loss cap, engine-out/damaged caps, thrust match, VMC, fuel balance/trim.
11. Output: surfaces via `Hooks.SurfacePostfix` (`ControlSurface.UpdateJobFields`), nozzles via engine transpiler/prefix hooks, hinged parts moved directly by the FCS.
12. Glue: `Registry.IsF22` = "is this my aircraft" (historical name), `MpPolicy` host checks, `FcsApi` discovery contract (`ApiId "duythinh2022.fcs"`), `Telemetry` CSV.

## 3. Hard rules (binding; they come from the author's flight testing)

1. **No on-top fixes.** Do not add a special-case device, gate or "if manoeuvre X then" branch for a symptom. Find the cause and
   change the model or the single device that owns it. If asked "is this an on-top fix?", answer honestly.
2. **Measured state over schedules.** No new gain schedules keyed on AoA/IAS for authority; read authority from the models and
   measurements. Feel tuning that is deliberate is allowed only when the user asks for it.
3. **Count everything on the sideslip axis at every AoA.** Any capacity, limit or clamp that sums yaw sources must include every
   effector (all surfaces, every TVC kind, splits, hinged surfaces, canards) and consider passive terms. Score on
   Mz·cos α − Mx·sin α. Never let a `Group()`-style function return "ignored" for a lateral effector.
4. **The governor slows the commanded rate only,** never roll acceleration/deceleration. Low–mid AoA: slow the velocity-vector
   roll. High AoA: slow the pitch. Gravity-induced sideslip is the only acceptable failure. Releasing an axis must stop that axis.
5. **A positive pitch command with the stick full forward is a hitch.** Not acceptable at any size.
6. **Never read a measured quantity where the logic means a command** (caused the uneven low-IAS roll).
7. **Never judge saturation against a target already clamped to the reach** (hid VMC saturation).
8. **Every allocator cost term:** units consistent with the error term (scale with q̄² where the error does), an explicit state
   where it must not act, and a test with a deliberately wrong model.
9. **Game aero changes and FCS models must mirror each other** (stagger table, camber host + K, downwash, lift shape).
10. **Don't change the user's tuning values** (player-file governor keys, envelope, gains) unless asked. If a default must
    change for existing users, implement a one-time migration ("dev migration") and say so.
11. **Versioning:** law change → minor bump; fix / telemetry / cleanup → patch bump. Update `FcsStandalonePlugin.Version`.
12. **Never overstate verification.** Report "compiled", "bench-only" or "flown", with numbers. Bench numbers are relative.
    The bench approximates the game and may not be accurate; the releases in this kit were flown and passed in game by the
    author (12 Nov 2026), so where the bench and the game disagree, the game is the reference (README §17).
13. **Judge the response curve on every manoeuvre** (Tom, 6 Nov): instant response, smooth rate, no spike, hitch, sudden
    slow-down, chatter or hesitation. Run the curve-quality checks (F-22E `bench/an/curveq.py`, README §10.3) and look at the
    time histories, not only peaks.
14. **Roll-coupled bench cases are chaotic.** Use 5-member ensembles (entry speed ±0.1/0.2 %, or a gain ×0.999…1.001) before
    calling a regression or an improvement.
15. **Threading must not change the flying.** Anything moved off the main thread must be byte-identical with its Dev switch on
    and off; never touch a Unity object off the main thread; worker copies must copy every field and every per-tick allocator
    setting (the Ifrit port's bug); the main thread always takes part (README §2.16).
16. **Airframe or FCS?** Decide with a fit to the game's own accelerations before changing either, and say which one a fix is in.
    An airframe change can remove something a limit spends (F-22E X2 removed the nose-down the coupling roll limit pays from).
17. **Gate on the state a rule is for, never on a symptom or a `Mathf.Sign(0)`** (+1 in Unity): F-22E 3.0.2, Compass 0.1.8,
    Ternion 0.1.12. Fade over a band instead of switching at a threshold the state can hover around (F-22E 3.1.6).
18. **Harmony prefixes from other mods run too.** HarmonyX runs every prefix even after one returns false. If two can do the
    same work (fuel draw), split into "modify at Priority.First" and "act at Priority.Last only if `__runOriginal`".
19. **The native core must stay the C# twin, bit for bit on the bench.** Any change to code a core routine mirrors (allocator
    solve, `Effector.MAt`, `SurfaceEffector.Eval*`, camber/airfoil reads, the sweep's capacity loop, the coupled LP) is made in
    `native/fcs_core/src/*.rs` expression for expression (same order, same float/double conversions, no FMA, no maths library
    except `atan2` as C# writes it). New shared field → bump the ABI on both sides. Pass mark: `bench.sh suite` identical with
    `NativeAllocator=0` and with `NativeAllocatorCheck=2` (0 differences). In the game, Mono's `-O=-float32` means C# and Rust
    differ in the last bits; that is expected and not a reason to change either.
20. **A model inside the FCS counts only what the allocator will really fly** (coupled LP: attached-flow surfaces, the
    allocator's own stab-peak rule, nozzles as columns when they move). Compare the LP's mix with the flown mix (`CplDebug`).
21. **A roll reversal is one acceleration** from the stick flip to the new rate; any limit on it lives inside the reversal
    branch, never in a block after it (F-22E 4.1.1).
22. **Flying backwards, stick and pedals point the nose** (Tom, 12 Nov, every jet): roll share |cos α|, yaw sin α, one axis.
23. **Config revisions are bound before `MigrateOldConfig`** (it clears unbound keys and saves; Su-30 0.3.4).


## 4. Procedures

### 4.1 Port to a new aircraft (README §7)
1. `python3 tools/new_airframe.py --from <closest reference> --out <dir> --ns <Ns.FCS> --title ... --short ... --file-prefix ... --prefab ... --match ...`
   Pick: rudders + stabs → f22e; no rudders / roll TVC / splits → ifrit; canards / delta / single fin / yaw TVC / hinged → revoker;
   no TVC, conventional tail, strakes → compass; two pitch pairs, Blueprinter jet, no-TVC twin engine-out → ternion; canted or
   single-axis nozzles, thrust line off the CG → su30; unsure → revoker or a later fork. `--prefab` is optional (match by
   aircraftName only). Reset the source jet's `*Revision` migration keys in the clone.
2. Open `<dir>/PORT_TODO.md`; work it top to bottom. Order: aircraft match → `Classify` → constructor effector list and minimum
   count → `Registry.EnsureSetup` (keep only intended airframe changes; fix the completeness check) → envelope defaults
   (start conservative) → aero mirrors (rewrite name maps or disable) → `CapacityEstimator.Group` (six-group version) → every
   `SurfaceKind.Rudder` / `rudderAuth` / `yawHealth` use → engine hooks (`Turbojet` vs `Turbofan`) → airbrake/ground config →
   per-jet governor constants → telemetry column names. Keep `FcsApi` and `MpPolicy`.
3. Build the bench model (§4.3), then `bash bench.sh build` and the bring-up cases (README §7.5).
4. Build the real DLL (§4.5). Fly with `Telemetry` on, replay the log on the bench, calibrate, then tune.

### 4.2 Add an effector type (README §9.1.2)
Subclass `Effector` (constructor: `name, min, max, lo, hi, regNorm, mu, lam, AllocGrid(N)`; `Evaluate`: `active, now, rate,
lo/hi`, every `M[i]`, `Mnow`, via the game's force equation at local flow and `ToAero`) → add to `effs` in the constructor →
write its output into the game (surface hook, engine hook, or drive the transform yourself and take ownership from the game
component) → add to `CapacityEstimator.Group` → add it to every yaw/roll source loop that should count it (search the
controller) → telemetry columns → plant support in the bench → suite A/B.

### 4.3 Bench model of a new aircraft (README §9.2)
Extract the airframe JSON (mod: cut `UnityFS` bundle from the mod DLL → `bench/extract/dump_mbs.py`, `extract_mbs.py`,
`extract2.py`, `extract_hulls.py`, `extract_damage.py`; stock: `bench/calib/stock_extract.py <Prefab> <out.json>` after fixing its
`D` path) → `BENCH_DATA=<json>` → `dotnet bench/bin/bench.dll info` → add a branch in `bench/sim/Airframe.cs` for the root
name (surface order, part names, mass defaults, which setup to apply) → make the plant apply the same airframe changes as
`Registry.EnsureSetup` → calibrate with `eqerr` against a flight log (fix root causes first, then few named factors, hold out a
log). **Check that the poser actually posed every surface** (the Su-30 `eqerr` matched none for days, README §9.2.4).
Blueprinter jets: `extract_ternion.py` / `extract_su30.py` + `build_json.py`; check part names (`Names.Canon`), transform
parents (`StructureFix`) and scripts living outside the `Aircraft` hierarchy. Never publish an airframe file extracted from
someone else's mod (the F-22E, Ternion and Su-30 files are not in this kit).

### 4.4 Change the law
1. Reproduce the reported behaviour on the bench first (`bench.sh replay <log> <t0> <t1> <out>`; `pinPitch=1` to isolate the
   lateral axis). If the bench can't reproduce a flight problem, find out why and upgrade the bench before changing the law.
2. Make the change; keep a temporary `internal static bool` switch while benching so A/B runs are one command; delete the
   switch before release.
3. `bash bench.sh build`; `bash bench.sh suite base` vs `suite new` (or `Name=value` on one build); `bash bench.sh diff base new`.
4. Re-run flagged cases with several samples (altitude 1500/1510/1490/1520 or entry speed ±3–4 km/h) before calling a
   difference real; null-perturbation noise is ~0.5° of sideslip; some cases are bistable.
5. Replays of the motivating log events: a before → after table.
6. "Without trade-offs" = no suite case or replay metric worse beyond noise. If there are trade-offs, list every one.
7. Release note: what, why, user quote, bench tables, status (compiled / bench-only / flown), install info.

### 4.5 Build the real DLL
`NO_MANAGED=<game>/NuclearOption_Data/Managed NO_BEPINEX_CORE=<game>/BepInEx/core bash build.sh <srcdir> <AssemblyName>.dll`.
The assembly name is taken from the output file name: use exactly the release name (`Aryx_F22E_StrikeRaptor_FCS.dll`,
`Ifrit_KR67_FCS.dll`, `Revoker_FS12_FCS.dll`, `Compass_TA30_FCS.dll`, `Ternion_FS3_FCS.dll`, `Su30_VXN_FCS.dll`, or your
namespace with `.` → `_`). Source dir: the **plugin folder** for all six releases (from `.` the md5 differs and the core is not
embedded). `build.sh` embeds `native/prebuilt/<Lib>.dll`. The Dev DLL is compiled from `DevUnlock/FcsDevPlugin.cs` directly.

### 4.5b Change or rebuild the native core (README §15.3)
`bash native/build.sh linux` (bench `.so`; `bench.sh build` does it) and `bash native/build.sh` (game DLL; needs Rust with the
`x86_64-pc-windows-gnu` target, or Ubuntu rustc-1.97 + rust-1.97-src with build-std, and mingw-w64 gcc). Copy the new DLL into
`native/prebuilt/`, rebuild the plugin, run the identity suites (rule 19) and, if you can, `native/verify/` under Wine with the
game's `mono-2.0-bdwgc.dll` (`jetcheck`, `multicheck`). Ship the core you tested (two builds differ in time-stamp bytes).

### 4.5c Lateral-law or asymmetric-load change: extra bench sets
The suite has no loaded reversal and no tail slide. Also run `bench/an/loadrev.sh` (88 loaded reversals / roll spam),
`bench/an/asymmat.sh` (and `asymnotvc.sh` on TVC jets; Ternion `negload.sh`; Su-30 `stopmat.sh`), and `bench/an/revflow_jet.py`
for anything past 90° AoA. Ground changes: `bench/groundsim/` and the bench's `gyaw` take-off mode.

### 4.6 Refactor / cleanup / performance work
Must be **byte-identical** on the bench suite and replays. Reordering floating-point sums, float↔double, fewer iterations,
early exits, coarser grids, cross-tick caching with tolerance, or probing less often are **not** behaviour-identical; they go
through §4.4 as law changes.

## 5. Where things are (search by name; line numbers drift)

| Need | Look for |
|---|---|
| aircraft match | `Registry.IsF22` |
| one-time airframe changes | `Registry.EnsureSetup` |
| surface roles | `FcsController.Classify` |
| effector list, pairs, costs | `public FcsController(Aircraft aircraft)` |
| per-tick order | `FcsController.Tick` |
| pitch law / lateral law / push plan | `PitchLaw`, `LateralLaw`, `PushPlanStep` |
| envelope sweeps | `ComputeEnvelope`, `AuthCapStep`, `AsymStep`, `YawHealthStep` |
| engine out | `VmcApply`, `ThrustMatchApply`, `EngineOutAoACap` |
| allocator | `Allocator.Solve`, `axisWeight`, `fightK`, `pitchFightK`, `ReachPass`, `TwoHorizon`, `regScale` |
| capacity | `CapacityEstimator.Step`, `Group`, `capZeP/N`, `capTrZeP/N`, gains `CapGain*` (F-22E) / `GainZ`,`GainX` (stock) |
| effector table / live node | `Effector.MAt`, `Effector.SetLiveNode`, `SurfaceEffector.Evaluate`, `AnchorLateral` |
| aero mirrors | `StallStagger.TableFor`, `StallStaggerJobs.SchedulePrefix`, `Camber.HostOf`, `CamberForce`, `Downwash` |
| config keys | `Plugin.Init` (`cfg.Bind` = player file, `dev.Bind` = Dev file) |
| Harmony patches | `Plugin.ApplyPatches`, `MpPolicy.Install` |
| output to game | `Hooks.SurfacePostfix`, `Hooks.Turbojet*`/`Turbofan*`, `FlapSystem` |
| telemetry | `WriteHeader`, `CaptureRow` / `FinishRow`, `TelemetryRow`, `Telemetry` |
| native core | `FcsNative` (load, cache, ABI, self-tests), `Allocator.NativeSolve`, `SurfaceEffector.GridMomentsNative` / `GridRangeNative`, `CoupledCap` native LP; dev switches `NativeAllocator` (0 C# / 1 auto / 2 native), `NativeAllocatorCheck`, `NativeTables`, `NativeSweep`, `NativeCoupledCap`, `SweepGridCache`, `CplGridCache`, `CplOneSide` |
| asymmetric-load limit | `CoupledCap.cs` (LP, `FindTop`), `AsymCoupledCheck`, `AsymCoupledReserveMax` / `...NoTvc`, `CplAttachedOnly`, `CplNoTvc`, `CplNoTvNeutral`, `CplNegAlways`, `AsymThrustCgLoad`, `AsymPitchMarginIasRef`, `CplDebug` / `CplDbgT`; Su-30 `AsymStopTime`, `CplKnee`, `AsymAoAWeight` |
| loaded reversal / backwards | `RevOneAccel`, `RevLoadRamp` (roll trajectory's reversal branch); `ReversedFlowNoseAxis`, `LatAxisAlpha()` |
| ground brakes | `GroundIndiBrake`, `GroundIndiBrakeMax`, `GroundIndiLightNoseHold`, `GroundPedalBrake`, `LandingGear` prefix in `Hooks` |
| threading | `EstimatorJob`, `ParallelFcs` (`FcsPool.Helpers`, `IndexFork`, `Claimable`, `EffectorMirror`), `EvalGather` / `EvalCompute`, `SwGather`, `TickStats` |
| push-over | `PushPlanStep`, `PushOverMinRate`, `PushPlanRise`, push governor (`PushGov*`, `SweepFloorPushMin`), 3.1.7 statics (`PushPlanDoneOpen`, `SweepSideKept`, `PushNoRetreat`, `PushPlanThrough`, ...) |
| F-22E airframe X2e | `BodyLift.Interp` / `Sched` / `BuildCharts`, `ForeLiftSchedule`, `AftLiftSchedule`, `StabilatorStallAoA`, `AirframeRevision` |
| fuel hook | `UseFuelScalePrefix` (Priority.First), `UseFuelPrefix` (Priority.Last) |
| no-TVC / damage (Compass lineage) | `noVecJet`, `StickBypass`, `CoupNoTvcAlways`, `StabRollGovernor`, `AsymPitchBias`, `AsymDamageGate`, `AsymCapBrake`, `AsymCapGapTighten`, `VmcPredict`, `LiveAuthCap` |
| Ternion / Su-30 specifics | `StructureFix`, `EngineOutZeroThrust`, `SpinRecovery`, `EngineOutSlipTrim`; `CantedTvc`, `ThrustLine`, `Names.Canon`, `LowSpeedTvcRoll`, `CaptureMeasured`, `SoftAoALimit`, `FuelCgHold` |
| bench static overrides | `bench/sim/Program.cs` `ApplySets` (Plugin, FcsController, Allocator, SurfaceEffector, Airframe, Plant, CapacityEstimator, ...) |

## 6. Known per-jet differences that bite when copying code between jets

- F-22E `CapacityEstimator.Group` drops non-`SurfaceEffector`s (−1); the stock jets' six-group version counts everything. Copy the stock one for any new jet with non-surface lateral effectors.
- F-22E has no `SlipFrameAlloc` (it regressed there); both stock jets run it on.
- `RevSim` (forward-model rate cap) exists on the stock jets but is **off**; they use the F-22E 3.0.0 reversal-swing rate cap.
- `PushPlanK0/K`: F-22E 0 / 0.028, Ifrit 9.6 / 0.0144, Revoker 40 / 0. They are fits, not constants.
- `RollStopSideslip` F-22E 4, Ifrit 6, Revoker 6, Compass 6, Ternion 6, Su-30 10; `RollPaceRateBlend` 0.408 / 0.4 / 0.65 /
  0.65 / 0.45 / 0.4; `PushoverSideslip` −1 / 8 / 8 / 8 / 8 / 8.
- F-22E only: `PushOverMinRate` 23 and the "just go" push parts on. The Ifrit has them ported but **off** (its rule: no more
  sideslip than 0.15.0); the Ifrit 0.17.0 hand-off switches are on.
- F-22E only: the X2e fore/aft lift schedule and stab peak 36°. Refit with `studies/x2_study_kit/scripts/fitsched.py`, never
  hand-edit the knots.
- Ifrit only: `TvcYawOffload` (yaw outranks pitch when unmet; nozzles take the pitch off the tails), `yawEffs` with a synthetic tail-yaw effector.
- Revoker only: `ElevonPitch`, `ElevonStallStagger` (26 knee-preserving / 45), `CanardAttachedWindow`, `TrailingEdgeBrakeConfig`, `VisualHinge`.
- Compass / Ternion / Su-30: no `NoTvcAoACap*` on a `noVecJet`; the stick never cancels the AoA limit without pitch TVC.
- Su-30: canards pitch-only (`CanardSymmetricOnly`), `TradeCapTau` 0.45 (nozzle transit), part names via `Names.Canon`.
- Bench namespaces: every bench compiles the synced FCS copy in `Aryx_F22E_StrikeRaptor.FCS` and renames `Turbojet` → `Turbofan` (stubs). Sync scripts: F-22E `bench/sync.sh` (+ `regen.sh`, which rewrites `bench/stubs/Plugin.cs`), Ifrit `bench/isync.sh`, Revoker / Compass / Ternion / Su-30 `bench/rsync.sh`. Single runs: F-22E `bench.sh one`, the rest `bench.sh fly`.
- Asymmetric-load share `AsymCoupledReserveMax`: F-22E 0.7 (check **off**: `AsymCoupledCheck` false, README §8.23), Ifrit 0.75,
  Revoker 0.7, Compass 0.75, Su-30 0.8; the Ternion (the first port) uses `AsymLateralReserve` 0.35 capped at
  `AsymResMaxFrac` 0.8 instead; `...NoTvc` 0.8. Su-30 only: `AsymStopTime` 0.8, `CplKnee`, `StopHardOnReversal`,
  `SoftPitchHold`, `GyroRollTrade`, `HighAoAPitchBump`, `FlaperonFlapSchedule`. Ifrit only: `MpoFreeRoll`.
- `MaxPitchRate`: 60 on the F-22E, Revoker (1.4.1) and Su-30 (0.5.7); 48 elsewhere.
- F-22E mains: `differentialBrakeFactor` −1 on both (no stock differential braking); stock jets −1 / +1.
- Clones: `tools/new_airframe.py` renames the native library (`<FilePrefix>_FcsCore`) in sources, build scripts and Cargo
  files, and renames `native/prebuilt/<Lib>.dll`. Two FCS mods must never share a core name (each binds its own by module name).
- No reference jet exists for VTOL / STOVL, swing wings, tailless designs, propeller aircraft, thrust reversers or 3+ engines
  (README §24.10). Treat a port to one of those as new law work, and say so to the user.
- Airframe files not in the kit: Ifrit, Revoker, Compass (stock-game data), F-22E (Aryx), Ternion (someone else's blueprint), Su-30 (kyleisred). Every bench builds but needs `BENCH_DATA` (and `BENCH_DMG`) to fly.
- Known stock-bench bugs (README §10.7): `IfritTest.Defaults()` overrides drag relief / stagger / LERX; Buffed `ThrustScale` / `DragScale` not applied on the Ifrit / Revoker (and `DragScale` on the Ternion) bench paths; no turbofan altitude lapse on the F-22E / Ternion benches. Fix before quoting performance numbers.

## 7. Reporting format the author expects
- Plain language, technical terms kept with a short plain-English explanation.
- Quantitative: tables of before → after with units; name the log and time stamp of each event.
- State status explicitly: compiled / bench-only / flown; md5 of anything installed. Remind the user to update the version in
  their mod manager (a modpack has rolled DLLs back).
- Say what is still open. Don't call a partial fix fixed.
