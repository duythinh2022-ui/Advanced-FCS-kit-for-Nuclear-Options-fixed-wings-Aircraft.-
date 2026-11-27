#!/bin/bash
# canset.sh <benchroot> <tag> [Name=value ...]   canard / pitch-surface case set (23 Nov): no-TVC loaded rolls + reversals at the cap,
# full push + roll at negative AoA, TVC loaded rolls / reversals, a roll released at low AoA (the MTD stuck case shape), pedal kicks.
R=$1; tag=$2; shift 2; B=$R/bench; O=${CANOUT:-$B/out/can}/$tag; rm -rf $O; mkdir -p $O
export BENCH_DIR=$B BENCH_DATA=${BENCH_DATA:-$(ls $B/extract/*_airframe.json | grep -v mtd | head -1)} DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${JOBS:-2}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=1500 "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
PULL="pitch=0:0,1:0,1.02:1,14:1"; PUSH="pitch=0:0,1:0,1.02:-1,14:-1"
ROLLREV="roll=0:0,5:0,5.02:1,8:1,8.02:-1,11:-1,11.02:0"
for v in 250 340; do go nt_lr_$v thr=1 ias=$v T=14 $PULL $ROLLREV DisableTVC=true "$@"; done
for v in 300 450; do go tv_lr_$v thr=1 ias=$v T=14 $PULL $ROLLREV "$@"; done
for v in 200 300; do go push_roll_$v thr=1 ias=$v T=12 $PUSH roll=0:0,4:0,4.02:1,8:1,8.02:0 "$@"; done
for v in 300 450; do go relroll_$v thr=1 ias=$v T=10 pitch=0:0,1:0,1.02:0.6,3:0.6,3.02:0 roll=0:0,3:0,3.02:-1,4.2:-1,4.22:0 "$@"; done
for v in 350; do go pedal_$v thr=1 ias=$v T=8 pitch=0:0 roll=0:0 yaw=0:0,1:0,1.02:1,3:1,3.02:0 "$@"; done
for v in 300; do go nt_push_$v thr=1 ias=$v T=12 $PUSH roll=0:0,4:0,4.02:1,8:1,8.02:0 DisableTVC=true "$@"; done
wait
python3 "$(dirname "$0")/canscore.py" $O
