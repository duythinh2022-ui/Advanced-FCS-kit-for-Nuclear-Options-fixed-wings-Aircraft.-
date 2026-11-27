#!/bin/bash
# usage: set45.sh OUTDIR TAG [Key=val ...] -- runs the 45-cap lateral set + 65 cases + falling leaf
O=$1; T=$2; shift 2; B=/home/claude/w/fcs2016/bench/bin/bench; mkdir -p $O
p(){ python3 -c "print($1)"; }
for ias in 330 420 520; do for gap in 0 0.6; do $B one iasKmh=$ias pull=1 hold=10 post=1 throttle=1 AoAPos=45 rollFromPull=1.5 rprof="0:0,2:0,2.1:-1,4.5:-1,4.6:0,$(p 4.6+$gap):0,$(p 4.7+$gap):1,8:1,8.1:0" "$@" csv=$O/${T}_rev45_${ias}_$gap.csv >/dev/null 2>&1 & done
 $B one iasKmh=$ias pull=1 hold=8 post=1 throttle=1 AoAPos=45 rprof="0:0,2:0,2.1:1,6:1,6.1:0" "$@" csv=$O/${T}_roll45_${ias}.csv >/dev/null 2>&1 & done; wait
for ias in 260 600 700; do $B one iasKmh=$ias pull=1 hold=10 post=1 throttle=1 AoAPos=45 rollFromPull=1.5 rprof="0:0,2:0,2.1:-1,4.5:-1,4.6:0,4.8:0,4.9:1,8:1,8.1:0" "$@" csv=$O/${T}_rev45_${ias}_0.2.csv >/dev/null 2>&1 & done
for ias in 330 450; do $B one iasKmh=$ias pull=1 hold=10 post=1 throttle=0.6 AoAPos=45 rprof="0:0,2:0,2.1:1,6:1,6.1:0" "$@" csv=$O/${T}_rolltrim45_${ias}.csv >/dev/null 2>&1 & done
for ias in 250 330 450; do $B one iasKmh=$ias pull=1 hold=10 post=1 throttle=1 rollFromPull=1.5 rprof="0:0,2:0,2.1:-1,4.5:-1,4.6:0,4.7:1,8:1,8.1:0" "$@" csv=$O/${T}_rev65_${ias}.csv >/dev/null 2>&1 & done
wait
for ias in 160 220; do $B one iasKmh=$ias pull=1 hold=12 post=1 throttle=1 rollFromPull=2 roll=0 yprof="0:1,1.5:1,1.6:-1,3.0:-1,3.1:1,4.5:1,4.6:-1,6:-1,6.1:0" "$@" csv=$O/${T}_leaf_${ias}.csv >/dev/null 2>&1 & done
for ias in 250 350; do $B one iasKmh=$ias pull=1 hold=8 post=1 throttle=1 rprof="0:0,2:0,2.1:1,6:1,6.1:0" "$@" csv=$O/${T}_roll65_${ias}.csv >/dev/null 2>&1 & done
for ias in 150 250 380; do $B one iasKmh=$ias throttle=0.8 hold=6 post=2 prof="0:-1,4:-1,4.1:0" rprof="0:1,4:1,4.1:0" "$@" csv=$O/${T}_pushroll_${ias}.csv >/dev/null 2>&1 & done
wait
