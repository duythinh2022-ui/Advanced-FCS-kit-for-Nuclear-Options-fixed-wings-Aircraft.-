#!/bin/bash
# pw.sh tag [env assignments as ENV:K=V] extra... : pitch-onset / pitch-wall set
cd /home/claude/su30/v10/src/bench; export BENCH_DATA=$PWD/extract/su30_airframe.json
tag=$1; shift
run(){ c=$1; ias=$2; P=$3; R=$4; T=$5; shift 5; timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="$P" roll="$R" T=$T fcslog=runs/${tag}_${c}_${ias}.csv "$@" > runs/${tag}_${c}_${ias}.out 2>&1; }
for ias in 450 600; do
 run ck $ias "0:0,0.3:0,0.4:1,12:1" "0:0,0.3:0,0.4:-1,12:-1" 8 "$@" &
 run pl $ias "0:0,0.3:0,0.4:1,12:1" "0:0" 6 "$@" &
done
run ck 300 "0:0,0.3:0,0.4:1,12:1" "0:0,0.3:0,0.4:-1,12:-1" 8 "$@" &
run p5 450 "0:0,0.3:0,0.4:0.5,12:0.5" "0:0" 5 "$@" &
wait
