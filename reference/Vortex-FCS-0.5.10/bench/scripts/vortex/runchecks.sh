#!/bin/bash
cd /home/claude/vortex
o=/tmp/chk; mkdir -p $o
( for I in 400 700; do timeout 600 ./b.sh ifly ias=$I alt=3000 thr=1 T=6 "pitch=0:0,0.5:0,0.6:1,3:1,3.1:0" csv=$o/ps$I.csv >/dev/null 2>&1; done
  for I in 300 500 800; do timeout 600 ./b.sh ifly ias=$I alt=3000 thr=1 T=5 "roll=0:0,0.5:0,0.6:1,2.5:1,2.6:0" csv=$o/rs$I.csv >/dev/null 2>&1; done; echo steps done > $o/steps.done ) &
( for a in "70 300" "75 300" "80 300" "85 300" "80 200" "80 400" "80 500" "85 400"; do ./longpull.sh $a; done > $o/pull.txt 2>&1 ) &
( for a in "60 300" "70 300" "80 300" "90 300"; do ./longpush30.sh $a; done > $o/push.txt 2>&1 ) &
( timeout 1500 ./b.sh stovl ias=250 alt=300 T=150 tog=1,128 "thr=0:0.15,6:0.15,7:0,30:0,31:0.25,85:0.25,86:0.5,95:0.5,96:0.8" "roll=0:0,60:0,60.3:0.5,65:0.5,65.3:0" "yaw=0:0,70:0,70.3:1,73:1,73.3:0" "pitch=0:0,78:0,78.3:0.5,82:0.5,82.3:0" print=5 csv=$o/s3.csv > $o/s3.txt 2>&1 ) &
( timeout 900 ./b.sh stovl ground=1 ias=0 T=60 tog=0.5 "thr=0:0,5:0,5.5:0.25" "pitch=0:0,10:0,10.3:0.6,16:0.6,16.3:0,30:0,30.3:-0.6,48:-0.6,48.3:0" print=2 csv=$o/v1.csv > $o/v1.txt 2>&1
  timeout 900 ./b.sh stovl ground=1 ias=0 T=40 tog=0.5 "thr=0:0,6:0,6.3:1" "pitch=0:0,10:0,10.3:0.5,13:0.5,13.3:0" print=2 csv=$o/v2.csv > $o/v2.txt 2>&1 ) &
wait
