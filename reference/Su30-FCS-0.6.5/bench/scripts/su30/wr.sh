#!/bin/bash
# wall + roll set: tag, then Name=value
cd /home/claude/su30/v10/src/bench; export BENCH_DATA=$PWD/extract/su30_airframe.json
tag=$1; shift; G="SlipTvcAssist=false SlipGainHighAoA=0 CorkscrewSlipTol=5 AsymFFMaxBeta=12"
L=/home/claude/su30/logs/Su30_FCS_20261109_102540.csv
timeout 900 dotnet bin/bench.dll replay log=$L t0=193.8 t1=200.5 out=runs/${tag}_rp.csv $G "$@" >/dev/null 2>&1 &
for ias in 350 480 600; do
 timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="0:0,0.3:0,0.4:1,12:1" roll="0:0" T=7 fcslog=runs/${tag}_wl_${ias}.csv $G "$@" >/dev/null 2>&1 &
 timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="0:0,0.3:0,0.4:1,12:1" roll="0:0,0.3:0,0.4:-1,12:-1" T=8 fcslog=runs/${tag}_wc_${ias}.csv $G "$@" >/dev/null 2>&1 &
done
for tr in 1.6 2.0; do
 timeout 900 dotnet bin/bench.dll ifly ias=520 alt=3000 pitch="0:0,0.3:0,0.4:1,12:1" roll="0:0,$tr:0,$(echo "$tr+0.1"|bc):-1,12:-1" T=6 fcslog=runs/${tag}_wr_${tr}.csv $G "$@" >/dev/null 2>&1 &
done
timeout 900 dotnet bin/bench.dll ifly ias=350 alt=3000 pitch="0:0,0.3:0,0.35:1,12:1" roll="0:0,0.3:0,0.35:1,6.3:1,6.35:0" T=9.5 fcslog=runs/${tag}_k350.csv $G "$@" >/dev/null 2>&1 &
wait
