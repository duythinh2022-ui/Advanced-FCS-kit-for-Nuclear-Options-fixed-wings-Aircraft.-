#!/bin/bash
# regression set: L value in $1
cd /home/claude/bench/bench; export BENCH_DATA=~/w/bdata/ifrit_airframe.json; L=$1; O=/tmp/claude-0/b/reg_${TAG:-$L}; mkdir -p $O
r(){ tag=$1; shift; dotnet bin/bench.dll ifly alt=3000 csv=$O/$tag.csv SlipRateLead=$L "$@" >/dev/null 2>&1; }
r cork150 ias=150 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r cork250 ias=250 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r cork350 ias=350 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r cork450 ias=450 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
wait
r entry380 ias=380 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r entry475 ias=475 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r entry520 ias=520 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r rpull300 ias=300 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1,2:1,2.1:0" &
wait
r rpull600 ias=600 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1,2:1,2.1:0" &
r pedal65 ias=150 thr=1 T=9 "pitch=0:0,0.2:1" "yaw=0:0,4:0,4.05:1,5:1,5.05:0" &
r pedal250hi ias=250 thr=1 T=5 "pitch=0:0,0.2:1" "yaw=0:0,1.5:0,1.7:1" &
r rev400 ias=400 thr=1 T=6 "pitch=0:0,0.2:0.6" "roll=0:0,0.2:1,1.5:1,1.6:-1,3:-1,3.1:0" &
wait
r level400roll ias=400 thr=1 T=4 "roll=0:0,0.3:1,1.5:1,1.7:0" &
r level700roll ias=700 thr=1 T=4 "roll=0:0,0.3:1,1.5:1,1.7:0" &
r idlecork300 ias=300 thr=0 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r idlerpull450 ias=450 thr=0 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1,3:1,3.1:0" &
wait
