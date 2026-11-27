#!/bin/bash
# lowcap.sh <benchroot> <tag> [Name=value ...]  : max usable AoA with the nozzles not covering pitch (low thrust / DisableTVC).
# Conditions: idle (thr 0.1), thr 0.3 (T/W under LowThrustTW), DisableTVC at full AB and at half throttle. Per condition:
# full-aft pull held, loaded roll, roll reversal, pedal kick, release from the pull.
R=$1; tag=$2; shift 2; B=$R/bench; O=${LCOUT:-$B/out/lowcap}/$tag; rm -rf $O; mkdir -p $O
export BENCH_DIR=$B DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${JOBS:-2}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=2000 "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
P="pitch=0:0,1:0,1.02:1,11:1"
for c in "i:thr=0.1" "t3:thr=0.3" "nt:thr=1 DisableTVC=true" "nh:thr=0.5 DisableTVC=true"; do
  k=${c%%:*}; a=${c#*:}
  for v in 200 300 450; do go ${k}_lr_$v $a ias=$v T=11 $P roll=0:0,5:0,5.02:1,9:1,9.02:0 "$@"; done
  go ${k}_hold_300 $a ias=300 T=11 $P roll=0:0 "$@"
  go ${k}_rev_300 $a ias=300 T=11 $P roll=0:0,5:0,5.02:1,7:1,7.02:-1,9.5:-1,9.52:0 "$@"
  go ${k}_ped_300 $a ias=300 T=11 $P roll=0:0 yaw=0:0,5:0,5.02:1,8:1,8.02:0 "$@"
  go ${k}_rel_300 $a ias=300 T=11 pitch=0:0,1:0,1.02:1,6:1,6.02:0 roll=0:0 "$@"
done
wait
python3 $(dirname "$0")/lowcapscore.py $O
