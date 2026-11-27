# F-22E FCS bench — portable (2.29.0 source, leaf calibration 3 Nov)

The offline flight-control bench that every release is checked on, in a folder that runs anywhere with .NET 8 and Python 3.
It is the full source tree (`Aryx_F22E_StrikeRaptor.FCS/` = the plugin's control laws, `bench/` = the simulator, stubs for
the Unity/game types, analysis scripts), so the bench always runs the law that is in this folder.

What it is and is not: a calibrated rigid-body sim of the F-22E with the game's aero tables (`bench/data/airframe_cal.json`,
the 2.4.18 calibration plus the 3 Nov leaf calibration), the real allocator and the real FCS code. Calibrated against the 3 Nov
logs at 40-70 deg AoA (`bench/CALIBRATION_leaf.md`): yaw / roll equation error 2.1 / 9.1 deg/s^2 held out, the 2x sideslip of
older benches traced to a reference-point bug and fixed; the plant's pitch through a fast 65 -> 25 deg push is still 7-9 deg/s^2
short of nose-down (the open item). Use it for direction and regressions (A vs B), never for absolutes.

## Setup (Windows → WSL2 Ubuntu; one time)

```
wsl --install -d Ubuntu            # from an admin PowerShell, then reboot / open Ubuntu
sudo apt update
sudo apt install -y dotnet-sdk-8.0 python3 python3-pandas python3-numpy python3-scipy
```

Your `C:\Games\NO mods` is `/mnt/c/Games/NO\ mods` inside WSL. Working on `/mnt/c` is fine but slower than a copy inside
the Linux filesystem; for many runs copy the folder: `cp -r "/mnt/c/Games/NO mods/F22E-bench-portable" ~/bench && cd ~/bench`.

(Native Windows also works for the C# part: `dotnet build bench\bench.csproj -c Release -o bench\bin` after a `bash regen.sh`
done once in WSL or Git Bash — the sync step that copies the FCS sources into `bench/fcs/` is a bash + python script. The suite
scripts assume a POSIX shell.)

## Run

```
cd ~/bench                                   # or the folder on /mnt/c
bash bench.sh build                          # sync FCS sources -> bench/fcs, regenerate the Plugin stub from tomcfg.cfg, build bench/bin/bench.dll
bash bench.sh suite base                     # the full suite with the law as it is -> bench/out/out_{rev,cork,reg,util,dmg}_base.txt
bash bench.sh suite k1 SweepYawK=1           # the same with a static overridden (any internal static of FcsController / Plugin / Allocator / Airframe / Plant / CapacityEstimator)
bash bench.sh diff base k1                   # diff the five files; "identical" means a byte-identical diff
```

The suite is five blocks: `rev` (roll reversals: time to zero / 90 %, peak sideslip), `cork` (corkscrew entries: roll
profile, AoA/g overshoot, sideslip), `reg` (the regression set: rolling pulls, releases, push-overs — `betaPk` is the number),
`util` (the rate-hold utilisation invariant, PASS/FAIL per AoA band), `dmg` (the damage suite: no fins / one fin / clean twins
of leaf stops, reversals, push-overs). Each case's per-tick CSV lands under `bench/out/{revs,cork,reg,util,dmg}/<tag>/`.
On a 2-core machine a suite takes ~10 minutes; the cases are independent processes, so it scales with cores.

A flight log replayed from its own state (the plant posed from the logged state at t0, the logged sticks played in):

```
bash bench.sh replay "/mnt/c/Games/Nuclear Option/BepInEx/F22E_FCS_20261103_111911.csv" 46.9 49.6 bench/out/rp4707.csv
bash bench.sh replay <log> <t0> <t1> <out.csv> SweepYawK=1       # with a static overridden
```

The replay CSV carries the bench's and the game's state side by side (`*_b` / `*_g` columns, `q_cmd` vs `q_cmd_g`, `aYawShed`,
`capSwp` / `capShd` = the two nose-down AoA-rate caps, whichever is lower binds).

One scripted case (what the suites run underneath; `prof` = pitch stick `t:value,...`, `rprof` = roll stick):

```
bash bench.sh one iasKmh=560 alt=1500 trimT=1 pull=0 hold=0 roll=0 post=9 throttle=1 FinLossAoACap=65 \
     prof=0:0,0.05:1,8:1 rprof=0:0,1.5:0,1.55:1,4:1,4.05:0,8:0 csv=bench/out/push560.csv
```

## Changing the law

Edit `Aryx_F22E_StrikeRaptor.FCS/*.cs`, then `bash bench.sh build` again (the bench compiles a *synced copy* in `bench/fcs/`;
without the build step it runs the old law — "unknown key X" from a `Name=value` you just added means the sync was skipped).
`tomcfg.cfg` is the player + Dev config the Plugin stub is generated from; refresh it from the game's
`BepInEx/config/*.cfg` when you change settings in game, then build.

`build.sh` builds the game DLL and needs the game's `Managed/` and `BepInEx/core` folders (paths at its top) — not needed for the bench.

## Leaf calibration tools (3 Nov)

```
bash bench.sh replaycheck leafcal "/mnt/c/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx"
                                             # the sixth block: every push of bench/calib/leaf_cases.json replayed closed-loop vs the game
                                             # -> bench/out/out_replaycheck_leafcal.txt (PASS/FAIL per push; other builds' logs = info)
CASES=my_cases.json bash bench.sh replaycheck tag <logdir>    # another case list
cd bench && dotnet bin/bench.dll slipprobe log=<csv> out=sp.csv every=20   # the FCS's own SlipMomentAt probe at logged states (kB per 1e4 Pa)
dotnet bin/bench.dll eqerr log=<csv> out=eq.csv [LogRefRoot=0] [DCnbHi=0] # equation error; LogRefRoot=0 = the 31 Oct pose, DCnbHi=0 = no leaf term
python3 calib/eval_leaf.py <bench> data/airframe_cal.json <logdir> <ids> <tag> --every=2 Name=val ...   # leaf-band error of a parameter set
python3 calib/fit_leaf.py  <bench> data/airframe_cal.json <logdir> <train ids> <val ids> --params=.. --x0=.. --amin=40   # Nelder-Mead at |a|>=amin
```
New bench-only statics (all `Name=value`): `LogRefRoot` (1), `DCnbHi` (-0.4), `MisFinHi MisRudHi MisWingHi MisBodyHi MisStabHi MisAilHi
MisFlapHi` (-1 = inactive; blended in over `MisHiA0`..`MisHiA1` = 35..45 deg body AoA).
