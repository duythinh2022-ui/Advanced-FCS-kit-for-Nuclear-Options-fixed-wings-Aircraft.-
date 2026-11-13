#!/bin/bash
# bench/an/asymmat.sh <tag> [Name=value ...]  - Su-30 0.5.0: the asymmetric-load AoA-limit matrix (31 ifly cases; store on
# Left_wing_middle: 800 kg = 16 cm of lateral CG offset at 22.8 t, 1600 kg = 31 cm). TVC on and nozzles locked (DisableTVC):
# full-aft pulls on afterburner / half / idle, a throttle step, roll reversals on the limit, pushes, a pull-then-push, slow
# entries, two clean cases. Outputs bench/out/mat/<tag>/*.csv; summary: [VMIN=45] python3 bench/an/asymmat.py <tagA> [<tagB> ...]
# 0.4.1 laws: asymmat.sh off CoupledCap=false AsymThrustCgLoad=false AsymPitchMarginIasRef=0
tag=$1; shift; A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/mat/$tag; rm -rf $O; mkdir -p $O
export BENCH_DATA=$B/extract/su30_airframe.json DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${MAT_JOBS:-3}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=3500 storePart=Left_wing_middle "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
PULL="pitch=0:0,3:0,3.02:1,40:1"; PUSH="pitch=0:0,3:0,3.02:-1,40:-1"
rl(){ echo "roll=0:0,$1:0,$(python3 -c "print($1+0.02)"):$2,$(python3 -c "print($1+2.5)"):$2,$(python3 -c "print($1+2.52)"):$((-$2)),$(python3 -c "print($1+5)"):$((-$2)),$(python3 -c "print($1+5.02)"):0"; }
OFF="DisableTVC=true"
# TVC on, 800 kg (16 cm)
for v in 350 450 600; do go on_pull_$v thr=1 ias=$v T=16 $PULL roll=0:0 storeKg=800 "$@"; done
for v in 350 450; do for d in 1 -1; do go on_roll_${v}_$([ $d = 1 ] && echo R || echo L) thr=1 ias=$v T=16 $PULL $(rl 6 $d) storeKg=800 "$@"; done; done
go on_half_450 thr=0.5 ias=450 T=16 $PULL roll=0:0 storeKg=800 "$@"
go on_idle_450 thr=0.1 ias=450 T=14 $PULL roll=0:0 storeKg=800 "$@"
go on_thrstep_450 ias=450 T=18 thrp=0:1,6:1,6.2:0.1,10:0.1,10.2:1 $PULL roll=0:0 storeKg=800 "$@"
go on_push_450 thr=1 ias=450 T=12 $PUSH roll=0:0 storeKg=800 "$@"
go on_pullpush_450 thr=1 ias=450 T=16 pitch=0:0,3:0,3.02:1,7:1,7.02:-1,11:-1,11.02:0 roll=0:0 storeKg=800 "$@"
go on_slow_250 thr=1 ias=250 T=16 $PULL $(rl 7 1) storeKg=800 "$@"
# TVC on, 1600 kg (31 cm)
go on_k16_pull_450 thr=1 ias=450 T=16 $PULL roll=0:0 storeKg=1600 "$@"
for d in 1 -1; do go on_k16_roll_450_$([ $d = 1 ] && echo R || echo L) thr=1 ias=450 T=16 $PULL $(rl 6 $d) storeKg=1600 "$@"; done
# TVC off, 800 kg
for v in 350 450; do go off_pull_$v thr=0.6 ias=$v T=16 $PULL roll=0:0 storeKg=800 $OFF "$@"; done
for v in 350 450; do for d in 1 -1; do go off_roll_${v}_$([ $d = 1 ] && echo R || echo L) thr=0.6 ias=$v T=16 $PULL $(rl 6 $d) storeKg=800 $OFF "$@"; done; done
go off_ab_450 thr=1 ias=450 T=16 $PULL roll=0:0 storeKg=800 $OFF "$@"
go off_idle_450 thr=0.1 ias=450 T=14 $PULL roll=0:0 storeKg=800 $OFF "$@"
go off_push_450 thr=0.6 ias=450 T=12 $PUSH roll=0:0 storeKg=800 $OFF "$@"
go off_slow_250 thr=0.6 ias=250 T=16 $PULL $(rl 7 1) storeKg=800 $OFF "$@"
# TVC off, 1600 kg
go off_k16_pull_450 thr=0.6 ias=450 T=16 $PULL roll=0:0 storeKg=1600 $OFF "$@"
for d in 1 -1; do go off_k16_roll_450_$([ $d = 1 ] && echo R || echo L) thr=0.6 ias=450 T=16 $PULL $(rl 6 $d) storeKg=1600 $OFF "$@"; done
# clean
go clean_on_450 thr=1 ias=450 T=14 $PULL $(rl 6 1) storeKg=0 "$@"
go clean_off_450 thr=0.6 ias=450 T=14 $PULL $(rl 6 1) storeKg=0 $OFF "$@"
wait; echo "done $tag: $(ls $O/*.csv | wc -l)"
