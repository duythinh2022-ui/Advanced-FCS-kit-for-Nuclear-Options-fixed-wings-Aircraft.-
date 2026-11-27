#!/bin/bash
# onefin.sh <tree> <tag> <ias> [sets] : one-fin held push-over (the damage suite's case) at one entry speed
T=$(cd $1; pwd); tag=$2; ias=$3; shift 3
cd $T/bench && BENCH_DATA=data/airframe_cal.json dotnet bin/bench.dll one iasKmh=$ias alt=1500 trimT=1 pull=0 hold=0 roll=0 post=9 throttle=1 FinLossAoACap=65 prof=0:0,0.05:1,4:1,4.05:-1,7:-1,7.05:0 rprof=0:0,1.5:0,1.55:1,7:1,7.05:0 csv=/home/claude/work/runs/onefin/${tag}_$ias.csv detach=Vstab_L "$@" >/dev/null 2>&1
