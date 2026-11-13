#!/bin/bash
# hiroll.sh tag extra...  : 4 cases x (350,450)
cd /home/claude/su30/v10/src/bench; export BENCH_DATA=$PWD/extract/su30_airframe.json
tag=$1; shift
run(){ c=$1; ias=$2; P=$3; Rr=$4; T=$5; shift 5; timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="$P" roll="$Rr" T=$T fcslog=runs/${tag}_${c}_${ias}.csv "$@" > runs/${tag}_${c}_${ias}.out 2>&1; }
for ias in 350 450; do
 run pr  $ias "0:0,0.5:0,0.6:1,12:1" "0:0,0.5:0,0.6:-1,6:-1,6.05:0" 10 "$@" &    # pull + roll together (game 67 s)
 run hr  $ias "0:0,0.5:0,0.6:1,14:1" "0:0,4:0,4.05:-1,8:-1,8.05:0" 12 "$@" &     # hold 70 then roll, release
 run rev $ias "0:0,0.5:0,0.6:1,14:1" "0:0,4:0,4.05:-1,5.5:-1,5.55:1,7:1,7.05:0" 10 "$@" &  # reversal at 70
done; wait
