#!/bin/bash
R=$1; tag=$2; shift 2; B=$R/bench; O=${AOAOUT:-$B/out/lowtw}/$tag; rm -rf $O; mkdir -p $O
export BENCH_DIR=$B DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${JOBS:-2}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=2000 "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
P="pitch=0:0,1:0,1.02:1,11:1"
for th in 0.1 0.3 0.5 1; do
 for v in 200 300 450; do
  go lr_t${th}_$v thr=$th ias=$v T=11 $P roll=0:0,5:0,5.02:1,9:1,9.02:0 "$@"
 done
 go rev_t${th}_300 thr=$th ias=300 T=11 $P roll=0:0,5:0,5.02:1,7:1,7.02:-1,9.5:-1,9.52:0 "$@"
 go ped_t${th}_300 thr=$th ias=300 T=11 $P yaw=0:0,5:0,5.02:1,8:1,8.02:0 "$@"
done
wait
python3 $(dirname "$0")/aoascore.py $O
