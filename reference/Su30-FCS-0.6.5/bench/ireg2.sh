#!/bin/bash
# Ifrit regression set. usage: TAG=name ./ireg.sh key=val ...   (runs land in runs/reg_$TAG)
cd /tmp/claude-0/-home-claude/49c90bd0-1fa5-5aa1-9e6b-3b1f8a22c307/scratchpad/ifr048/bz/benchx; export BENCH_DATA=/root/w/ifrit/ifrit_airframe.json; O=runs/reg_${TAG:-x}; mkdir -p $O; EX="$@"
B="FuelFrac=1 ThrustScale=1.08 DragScale=0.95"
r(){ tag=$1; shift; dotnet bin/bench.dll ifly alt=600 $B csv=$O/$tag.csv $EX "$@" >/dev/null 2>$O/$tag.err; }
r cork150 ias=150 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r cork250 ias=250 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r cork350 ias=350 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r cork450 ias=450 thr=1 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
wait
r entry380 ias=380 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r entry475 ias=475 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r rpull300 ias=300 thr=1 T=6 "pitch=0:0,0.2:1" "roll=0:0,0.2:1,2:1,2.1:0" &
r rpull600 ias=600 thr=1 T=5 "pitch=0:0,0.2:1" "roll=0:0,0.2:1,2:1,2.1:0" &
wait
r kick1 ias=300 thr=1 T=10 "pitch=0:0,0.2:1" "roll=0:0,0.2:-1" "yaw=0:0,0.6:0,0.65:1,1.4:1,1.45:0" &
r pedal65 ias=150 thr=1 T=9 "pitch=0:0,0.2:1" "yaw=0:0,4:0,4.05:1,5:1,5.05:0" &
r rev400 ias=400 thr=1 T=6 "pitch=0:0,0.2:0.6" "roll=0:0,0.2:1,1.5:1,1.6:-1,3:-1,3.1:0" &
r rev300hi ias=300 thr=1 T=9 "pitch=0:0,0.2:1" "roll=0:0,4:0,4.01:-1,5.5:-1,5.51:1,7:1,7.01:0" &
wait
r level400roll ias=400 thr=1 T=4 "roll=0:0,0.3:1,1.5:1,1.7:0" &
r idlecork300 ias=300 thr=0 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1" &
r idlerpull450 ias=450 thr=0 T=8 "pitch=0:0,0.2:1" "roll=0:0,0.2:1,3:1,3.1:0" &
r corkkick ias=300 thr=1 T=10 "pitch=0:0,0.2:1" "roll=0:0,0.2:-1" "yaw=0:0,5:0,5.05:1,6:1,6.05:0" &
wait
