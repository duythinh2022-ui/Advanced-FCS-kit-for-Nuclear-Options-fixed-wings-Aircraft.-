#!/bin/bash
cd /home/claude/bench/bench; export BENCH_DATA=~/w/bdata/ifrit_airframe.json; O=/tmp/claude-0/b/r2_$1; mkdir -p $O; shift; EX="$@"
r(){ tag=$1; shift; dotnet bin/bench.dll ifly alt=1000 csv=$O/$tag.csv $EX "$@" >/dev/null 2>&1; }
r slow250 ias=250 thr=0.6 T=8 "pitch=0:0,0.3:0.25" &
r slow250roll ias=250 thr=0.6 T=6 "pitch=0:0,0.3:0.25" "roll=0:0,1:0,1.05:1,2.5:1,2.6:0" &
r slow300brake ias=300 thr=0.6 T=6 brake=0:1 "roll=0:0,1:0,1.05:1,2.5:1,2.6:0" &
r cork150 ias=150 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
wait
r pull350 ias=350 thr=1 T=6 "pitch=0:0,0.2:1" &
r rpull300 ias=300 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1,2:1,2.1:0" &
r level400roll ias=400 thr=1 T=4 "roll=0:0,0.3:1,1.5:1,1.7:0" &
r idle45 ias=650 thr=0 T=12 "pitch=0:0,0.3:1,8:0.85" "roll=0:0,8.5:0,8.55:1,8.85:1,8.9:0" &
wait
