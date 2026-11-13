# Revoker FCS bench — portable (Revoker_FS12.FCS 0.4.3 source + the damage bench)

The F-22E bench driving the stock Revoker airframe: the real `Revoker_FS12.FCS` control laws (synced into `bench/fcs/` by `bench/rsync.sh`,
which renames the namespace and strips the game-only bits), the rigid-body plant with the game's aero / control-surface / engine
force model, the airframe extracted from the game's assets (`bench/extract/rev_airframe.json`), the part-damage model
(`bench/sim/Damage.cs`, `bench/data/revoker_damage.json`) and the analysis scripts. FCS source in this folder: 0.4.3 (the latest).
Yaw on this jet: one rudder between two fins (vstab1 / vstab2), hinged to the fuselage as well.

Calibration and what the bench is and is not: `bench/CALIBRATION.md`, `bench/DAMAGE.md`, and the project docs
(`Revoker_bench_damage.md`, Revoker_FCS_0.4.3_notes.md, CHANGELOG.md). Use it for direction and A/B regressions, not absolutes.

## Setup (Windows → WSL2 Ubuntu; one time — the same as the F-22E bench)

```
sudo apt update && sudo apt install -y dotnet-sdk-8.0 python3 python3-pandas python3-numpy python3-scipy
```

## Run

```
cd <this folder>                  # on /mnt/c or a copy under ~ (faster)
bash bench.sh build               # sync Revoker_FS12.FCS -> bench/fcs, (re)generate the Plugin stub from bench/tomrev.cfg, build bench/bin/bench.dll
bash bench.sh suite base          # regression set + stop/reversal sweep + corkscrews + damage suite -> bench/out/out_{reg,sw,cork,dmg}_base.txt
bash bench.sh suite x PaceLeadT=0.2   # the same with any internal static of FcsController / Plugin / Allocator / Airframe / Plant overridden
bash bench.sh diff base x
```

The suite's four blocks: `reg` (`an/sreg.py`: rolling pulls, releases, push-overs, reversals — `betaPk` is the number),
`sw` (`an/ssweep.py`: roll stops and reversals over IAS × pull), `cork` (`an/cork.py`: corkscrews scored for smoothness),
`dmg` (`an/sdmgsuite.py revoker`: yaw-surface loss, surfaces damaged mid-pull, roll surface lost, wing at 20 HP, an engine
killed by a critical part — each next to its clean twin). Per-tick CSVs land under `bench/out/{reg,sw,cork,sdmg}/<tag>/`.
`BENCH_OUT=<dir>` puts the outputs elsewhere. Every case is its own process, so the suite scales with cores.

One case (ifly mode; stick profiles `t:value,...`; damage keys `dmg=part:hp+...`, `dmgAt=t:part:hp`, `detach=part+part`,
`detachAt=t:part+part` — part names as in the airframe json: rudder, vstab1, vstab2, aileron_R, wing1a_L, wing1b_L, wing2_L, wingtip_L, intake_R …):

```
bash bench.sh fly ias=400 alt=1500 thr=1 T=6 pitch=0:0,0.2:1 roll=0:0,0.2:1 fcslog=bench/out/cork400.csv
bash bench.sh fly ias=370 alt=1500 thr=1 T=9 pitch=0:0,0.05:1,9:1 roll=0:0,2:0,2.05:1,5:1,5.05:0,9:0 detach=rudder fcslog=bench/out/leafstop_norudder.csv
bash bench.sh eqerr log=<FCS flight log csv> out=bench/out/eq.csv     # equation-error dump: bench vs logged accelerations along a real flight
```

(There is no `replay` from a logged state on the stock-jet benches yet — `eqerr` is the log-driven mode.)

## Changing the law

Edit `Revoker_FS12.FCS/*.cs`, then `bash bench.sh build` again — the bench compiles the synced copy in `bench/fcs/`; without the build it
runs the old law ("unknown key X" for a static you just added = the sync was skipped). `bench/tomrev.cfg` is the player + Dev config
the Plugin stub is generated from; refresh it from the game's `BepInEx/config/` when you change settings in game, then build.
`build.sh` builds the game DLL (Revoker_FS12_FCS.dll) and needs the game's `Managed/` and `BepInEx/core` folders (paths at its top).

## Leaf check (3 Nov 2026) — `bench/CALIBRATION_leaf.md`

The logged alpha / beta / tas are the root rigidbody's, not the CG's: `eqerr` now poses the root body at the logged velocity
(`LogRefRoot=1`; 0 = the old pose). New bench-only statics, all inactive by default: `MisStabHi MisAilHi MisFlapHi MisRudHi MisFinHi
MisWingHi MisBodyHi` (-1; blended in over `MisHiA0`..`MisHiA1` body AoA), `DClbHi DCnbHi` (0; roll / yaw per deg of sideslip per 1e4 Pa).
```
cd bench && python3 calib/derivs_generic.py out/eq_*.csv --bands=-45,0,20,40,50,60,70     # per-band derivatives game vs bench, kB
python3 calib/eval_leaf.py <bench> <airframe json> <logdir> <ids> <tag> --prefix=XXX_FCS_ --every=2 --amin=40 Name=val ...
python3 calib/fit_leaf.py  <bench> <airframe json> <logdir> <train ids> <val ids> --prefix=XXX_FCS_ --params=.. --x0=.. --amin=40
```
