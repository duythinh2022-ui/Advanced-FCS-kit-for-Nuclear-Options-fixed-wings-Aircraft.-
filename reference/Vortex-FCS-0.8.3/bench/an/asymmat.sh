#!/bin/bash
# bench/an/asymmat.sh <tag> [Name=value ...]  - Revoker 1.2.0: the asymmetric-load AoA-limit matrix (25 ifly cases; store on wing1b_L:
# 2000 kg = 26-30 cm of lateral CG offset at 14.2 t, 1160 kg = 16 cm, 560 kg = 8 cm). Full-aft pulls on afterburner / half / idle,
# throttle steps on the limit, roll reversals on the limit, nozzles locked (DisableTVC), pushes, one clean pull.
# Outputs bench/out/mat/<tag>/*.csv; summary: python3 bench/an/asymmat.py <tagA> [<tagB> ...]
# 1.1.1 laws: asymmat.sh off CoupledCap=false
tag=$1; shift; A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/mat/$tag; rm -rf $O; mkdir -p $O
export BENCH_DATA=$B/extract/rev_airframe.json BENCH_DMG=$B/data/revoker_damage.json DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${ASYM_JOBS:-4}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=3000 storePart=wing1b_L "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
PULL="pitch=0:0,3:0,3.02:1,40:1"; PUSH="pitch=0:0,3:0,3.02:-1,40:-1"
for v in 250 300 400 500 600; do go pull_ab_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=2000 "$@"; done
for v in 300 450; do go pull_half_$v thr=0.5 ias=$v T=22 $PULL roll=0:0 storeKg=2000 "$@"; go pull_idle_$v thr=0.1 ias=$v T=18 $PULL roll=0:0 storeKg=2000 "$@"; done
for v in 300 450; do go pull_ab_k1160_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=1160 "$@"; go pull_ab_k560_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=560 "$@"; done
go thr_step_250 ias=250 T=24 thrp=0:0.1,8:0.1,8.2:1,15:1,15.2:0.1 $PULL roll=0:0 storeKg=2000 "$@"
go thr_step_350 ias=350 T=24 thrp=0:1,9:1,9.2:0.1,15:0.1,15.2:1 $PULL roll=0:0 storeKg=2000 "$@"
for v in 300 450; do for d in 1 -1; do go roll_${v}_$([ $d = 1 ] && echo R || echo L) thr=1 ias=$v T=22 $PULL roll=0:0,9:0,9.02:$d,12:$d,12.02:$((-d)),15:$((-d)),15.02:0 storeKg=2000 "$@"; done; done
for v in 300 450; do go notvc_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=2000 DisableTVC=true "$@"; go push_$v thr=1 ias=$v T=16 $PUSH roll=0:0 storeKg=2000 "$@"; done
go notvc_roll_400 thr=1 ias=400 T=22 $PULL roll=0:0,9:0,9.02:1,12:1,12.02:-1,15:-1,15.02:0 storeKg=2000 DisableTVC=true "$@"
go clean_pull_300 thr=1 ias=300 T=16 $PULL roll=0:0 storeKg=0 "$@"
wait; echo "done $tag: $(ls $O/*.csv | wc -l) csv"
