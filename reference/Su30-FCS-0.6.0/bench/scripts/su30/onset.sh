#!/bin/bash
cd /home/claude/su30/v10/src/bench; export BENCH_DATA=$PWD/extract/su30_airframe.json
tag=$1; shift
for ias in 350 450 550 650 750 850; do
 timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="0:0,0.3:0,0.4:1,12:1" roll="0:0" T=4 fcslog=runs/${tag}_on_${ias}.csv "$@" >/dev/null 2>&1 &
 timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="0:0,0.3:0,0.5:0.6,12:0.6" roll="0:0" T=4 fcslog=runs/${tag}_o6_${ias}.csv "$@" >/dev/null 2>&1 &
done; wait
