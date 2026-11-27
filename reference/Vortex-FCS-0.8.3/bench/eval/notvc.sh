#!/bin/bash
# usage: notvc.sh OUTDIR TAG [Key=val ...] -- no-TVC / idle envelope set at the 45 setting
O=$1; T=$2; shift 2; B=/home/claude/w/fcs2016/bench/bin/bench; mkdir -p $O
for ias in 200 300 450 600 750; do
 $B one iasKmh=$ias throttle=1 hold=6 post=4 prof="0:1,6:1,6.1:0" AoAPos=45 DisableTVC=true "$@" csv=$O/${T}_pull_$ias.csv >/dev/null 2>&1 &
 $B one iasKmh=$ias throttle=1 hold=6 post=4 prof="0:1,3:1,3.1:-1,4.5:-1,4.6:0" AoAPos=45 DisableTVC=true "$@" csv=$O/${T}_pullpush_$ias.csv >/dev/null 2>&1 &
done; wait
for ias in 250 350 450 600; do
 $B one iasKmh=$ias throttle=1 hold=8 post=2 AoAPos=45 DisableTVC=true prof="0:1,8:1,8.1:0" rprof="0:0,2:0,2.1:1,6:1,6.1:0" "$@" csv=$O/${T}_roll_$ias.csv >/dev/null 2>&1 &
 $B one iasKmh=$ias throttle=1 hold=8 post=2 AoAPos=45 DisableTVC=true prof="0:1,8:1,8.1:0" rprof="0:0,2:0,2.1:-1,4.5:-1,4.6:0,4.8:0,4.9:1,7:1,7.1:0" "$@" csv=$O/${T}_rev_$ias.csv >/dev/null 2>&1 &
done; wait
for ias in 250 400 550; do
 $B one iasKmh=$ias throttle=1 hold=6 post=3 AoAPos=45 DisableTVC=true prof="0:-1,5:-1,5.1:0" "$@" csv=$O/${T}_push_$ias.csv >/dev/null 2>&1 &
 $B one iasKmh=$ias throttle=1 hold=6 post=3 AoAPos=45 DisableTVC=true prof="0:-1,5:-1,5.1:0" rprof="0:0,1.5:0,1.6:1,4.5:1,4.6:0" "$@" csv=$O/${T}_pushroll_$ias.csv >/dev/null 2>&1 &
 $B one iasKmh=$ias throttle=0 hold=6 post=4 AoAPos=45 prof="0:1,5:1,5.1:0" "$@" csv=$O/${T}_idle_$ias.csv >/dev/null 2>&1 &
 $B one iasKmh=$ias throttle=0 hold=6 post=4 AoAPos=45 prof="0:1,6:1,6.1:0" rprof="0:0,2:0,2.1:1,5:1,5.1:0" "$@" csv=$O/${T}_idleroll_$ias.csv >/dev/null 2>&1 &
done; wait
for ias in 300 450; do
 $B one iasKmh=$ias throttle=1 hold=8 post=3 AoAPos=45 DisableTVC=true fuelBal=3 burn=0.6 prof="0:1,6:1,6.1:0" rprof="0:0,2:0,2.1:1,4.5:1,4.6:0" "$@" csv=$O/${T}_fuelroll_$ias.csv >/dev/null 2>&1 &
done; wait
