#!/bin/bash
# aoaset.sh <benchroot> <tag>  : high-AoA usability set at the current bench config (AoA limit from tomifr.cfg)
R=$1; tag=$2; shift 2; B=$R/bench; O=${AOAOUT:-$B/out/aoa}/$tag; rm -rf $O; mkdir -p $O
export BENCH_DIR=$B DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${JOBS:-2}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=2000 "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
P="pitch=0:0,1:0,1.02:1,11:1"
for v in 200 300 450; do
  go hold_$v thr=1 ias=$v T=11 $P roll=0:0 "$@"
  go lr_$v   thr=1 ias=$v T=11 $P roll=0:0,5:0,5.02:1,9:1,9.02:0 "$@"
  go rev_$v  thr=1 ias=$v T=11 $P roll=0:0,5:0,5.02:1,7:1,7.02:-1,9.5:-1,9.52:0 "$@"
  go ped_$v  thr=1 ias=$v T=11 $P roll=0:0 yaw=0:0,5:0,5.02:1,8:1,8.02:0 "$@"
  go rel_$v  thr=1 ias=$v T=11 pitch=0:0,1:0,1.02:1,6:1,6.02:0 roll=0:0 "$@"
done
go idle_lr_300 thr=0.1 ias=300 T=11 $P roll=0:0,5:0,5.02:1,9:1,9.02:0 "$@"
go nt_lr_300 thr=1 ias=300 T=11 $P roll=0:0,5:0,5.02:1,9:1,9.02:0 DisableTVC=true "$@"
go push_300 thr=1 ias=300 T=10 pitch=0:0,1:0,1.02:-1,10:-1 roll=0:0,5:0,5.02:1,8:1,8.02:0 "$@"
wait
python3 $(dirname "$0")/aoascore.py $O
