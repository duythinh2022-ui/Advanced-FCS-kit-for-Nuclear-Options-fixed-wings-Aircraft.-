#!/bin/bash
# bench/an/asymmat.sh <tag> [Name=value ...]  - Compass 0.4.0: the asymmetric-load AoA-limit matrix (24 ifly cases; store on wing1_L:
# 900 kg = 32 cm of lateral CG offset at 7.9 t, 570 kg = 21 cm, 320 kg = 12 cm). Full-aft pulls on afterburner / half / idle,
# a throttle step on the limit, roll reversals on the limit, pushes, a pull-then-push, two slow entries, one clean pull.
# Outputs bench/out/mat/<tag>/*.csv; summary: [VMIN=45] python3 bench/an/asymmat.py <tagA> [<tagB> ...]
# 0.3.1 laws: asymmat.sh off CoupledCap=false AsymThrustCgLoad=false AsymPitchMarginIasRef=0
tag=$1; shift; A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/mat/$tag; rm -rf $O; mkdir -p $O
export BENCH_DATA=$B/extract/compass_airframe.json BENCH_DMG=$B/data/compass_damage.json DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${ASYM_JOBS:-4}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=3500 storePart=wing1_L "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
PULL="pitch=0:0,3:0,3.02:1,40:1"; PUSH="pitch=0:0,3:0,3.02:-1,40:-1"
for v in 250 300 400 500 600; do go pull_ab_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=900 "$@"; done
for v in 300 450; do go pull_half_$v thr=0.5 ias=$v T=22 $PULL roll=0:0 storeKg=900 "$@"; go pull_idle_$v thr=0.1 ias=$v T=18 $PULL roll=0:0 storeKg=900 "$@"; done
for v in 300 450; do go pull_ab_k570_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=570 "$@"; go pull_ab_k320_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=320 "$@"; done
go thr_step_300 ias=300 T=24 thrp=0:1,9:1,9.2:0.1,15:0.1,15.2:1 $PULL roll=0:0 storeKg=900 "$@"
for v in 300 450; do for d in 1 -1; do go roll_${v}_$([ $d = 1 ] && echo R || echo L) thr=1 ias=$v T=22 $PULL roll=0:0,9:0,9.02:$d,12:$d,12.02:$((-d)),15:$((-d)),15.02:0 storeKg=900 "$@"; done; done
for v in 300 450; do go push_$v thr=1 ias=$v T=16 $PUSH roll=0:0 storeKg=900 "$@"; done
go pullpush_450 thr=1 ias=450 T=20 pitch=0:0,3:0,3.02:1,9:1,9.02:-1,15:-1,15.02:0 roll=0:0 storeKg=900 "$@"
go slow_pull_220 thr=1 ias=220 T=22 $PULL roll=0:0 storeKg=900 "$@"
go slow_roll_250 thr=0.6 ias=250 T=22 $PULL roll=0:0,9:0,9.02:1,12:1,12.02:-1,15:-1,15.02:0 storeKg=900 "$@"
go clean_pull_300 thr=1 ias=300 T=16 $PULL roll=0:0 storeKg=0 "$@"
wait; echo "done $tag: $(ls $O/*.csv | wc -l) csv"
