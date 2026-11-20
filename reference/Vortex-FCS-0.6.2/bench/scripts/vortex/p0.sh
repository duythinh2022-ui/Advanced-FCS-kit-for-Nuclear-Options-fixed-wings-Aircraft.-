#!/bin/bash
# phase 0 measurement set. usage: p0.sh <outdir> [bench args]   (BENCH_BIN picks the build)
o=$1; shift; mkdir -p $o
B=/home/claude/vortex/b.sh
# H: VTO, hover, full side stick 5 s + release, short roll tap, pedal 3 s, lever forward step and back
( timeout 900 $B stovl ground=1 ias=0 T=100 tog=0.5 "thr=0:0,65:0,65.1:0.35,78:0.35,78.1:0.20" "pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0" "roll=0:0,20:0,20.1:1,25:1,25.1:0,40:0,40.1:-0.4,41.5:-0.4,41.6:0" "yaw=0:0,50:0,50.1:1,53:1,53.1:0" print=100 csv=$o/H.csv "$@" > $o/H.txt 2>&1 ) &
# M: VTO, lever to the centre detent (30 kt), bank stick, pedal, lever steps
( timeout 900 $B stovl ground=1 ias=0 T=90 tog=0.5 "thr=0:0,18:0,18.1:0.5,70:0.5,70.1:0.6,74:0.6,74.1:0.5" "pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0" "roll=0:0,40:0,40.1:0.3,43:0.3,43.1:0,50:0,50.1:-0.3,51:-0.3,51.1:0" "yaw=0:0,58:0,58.1:0.5,60:0.5,60.1:0" print=100 csv=$o/M.csv "$@" > $o/M.txt 2>&1 ) &
wait
# D: airborne conversion at 380 km/h, lever in the aft detent, decel to the hover
( timeout 900 $B stovl ias=380 alt=300 T=80 tog=1 "thr=0:0.3,0.5:0.20" print=100 csv=$o/D.csv "$@" > $o/D.txt 2>&1 ) &
# S: full-lever short take-off
( timeout 900 $B stovl ground=1 ias=0 T=40 tog=0.5 "thr=0:0,6:0,6.3:1" "pitch=0:0,10:0,10.3:0.5,13:0.5,13.3:0" print=100 csv=$o/S.csv "$@" > $o/S.txt 2>&1 ) &
wait
