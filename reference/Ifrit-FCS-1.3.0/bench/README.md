# Ifrit (KR-67) FCS bench — 31 Oct 2026 calibration build

This is the F-22E bench (README below) driving the stock Ifrit airframe. Sync the Ifrit FCS sources with `./isync.sh <dir containing Ifrit_KR67.FCS>`
(it renames namespaces, strips the MonoBehaviour from DragRelief and patches the camber host lookup for the bench's renamed surfaces; `stubs/Plugin.cs`
is the config stub — regenerate it with `genplugin2.py` if the FCS adds fields, keeping the hand-patched `SplitRange` line), then
`BENCH_DATA=extract/ifrit_airframe.json dotnet build -c Release -o bin`.

Calibration against the flight logs: see `CALIBRATION.md`. New modes / tools:
- `dotnet bin/bench.dll eqerr log=<FCS log csv> out=<csv> [every=1] [Name=value ...]` — equation-error dump (bench vs logged accelerations).
- `calib/`: `eqcmp.py` (RMS / lag / error maps), `derivs_stock.py <eqerr-dump csv> ifrit` (per-AoA-band derivatives game vs bench), `fit.py`, `evalset.py`,
  `multi_eval.py` (`FCS_LOGDIR` = folder with the logs, `EQ_OUT` = output folder), `stock_extract.py <prefab> <out.json>` (airframe JSON from the game assets).
- Calibrated defaults live in `sim/IfritTest.Defaults` (mass, fuel, `CgAftShift`); every `Airframe`/`Plant` static is overridable as `Name=value`.

---

# F-22E FCS bench (1.8.20)

Real FcsController / Allocator / effector sources (copied into `fcs/` by `sync.sh <srcdir>`) compiled against
managed Unity + Nuclear Option stubs (`stubs/`), flying a rigid-body plant (`sim/Plant.cs`) built from Aryx's
F-22E asset data (`extract/bench_airframe.json`) with Nuclear Option's own AeroJob / ControlSurfaceJob / Turbofan
force model.

Build (offline, .NET 8 SDK):  `./sync.sh .. && dotnet build -c Release -o bin`
(`sim/Program.cs` has the airframe path hard-coded as `dataPath` — point it at `extract/bench_airframe.json`.)

Modes:
- `dotnet bin/bench.dll info` — mass properties, parts, surfaces, engines
- `dotnet bin/bench.dll one iasKmh=550 pull=0.5 hold=0.35 roll=1 throttle=1 csv=out.csv` — one run + CSV
- `dotnet bin/bench.dll matrix iasList=450,500,550,600 holdList=0.2,0.35 delayList=0,0.15 pullList=0.5,1 rollList=1`
- `dotnet bin/bench.dll suite` — the regression suite used for 1.8.14
- Any `Plugin` config field or FcsController static can be overridden: `CouplingFF=1 ReleasedPitchPriority=1 ...`
- `gyro=none|unity|full` selects the plant's gyroscopic model.
- `detach=WingFore_L+WingRear_L+Wingmid_L+Wingtip_L+Aileron_L+Flap_L` flies with those parts broken off (mass, fuel and aero removed, surfaces and flap drives marked detached). `info detachInfo=...` prints the damaged mass properties.
- `gamma0=85` starts in a climb (vertical-climb manoeuvres); `dumpAt=<t>` prints every effector's modelled roll/pitch/yaw across its travel at that time after release.
- `pullRamp=0.3 rideAmp=0.02 rideT=1 rideNoise=0.004` rides the stick in small steps (and optional jitter) during the hold, for AoA-tracking tests.

Re-extracting the airframe: `extract/dump_mbs.py` (MonoBehaviour dump -> mbs.json), `extract/extract_mbs.py`, `extract2.py` (hierarchy/parts),
`hulls.py` (convex-hull COM/inertia) — need UnityPy + scipy and the UnityFS bundle cut out of
Aryx_F22E_StrikeRaptor_1.0.4.dll at the `UnityFS` signature.

The airframe JSON is Aryx's data — don't publish it.
