#!/bin/bash
# trig2.sh tag [Name=value..]: roll started at a fixed AoA, sweep over IAS and AoA; output runs/trig2
cd /home/claude/su30/src/bench; export BENCH_DATA=$PWD/extract/su30_airframe.json
G="SlipTvcAssist=false SlipGainHighAoA=0 CorkscrewSlipTol=5 AsymFFMaxBeta=12"
tag=$1; shift; mkdir -p runs/trig2
for ias in 400 460 520 560; do
  timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="0:0,0.3:0,0.4:1,12:1" roll="0:0" T=5 fcslog=runs/trig2/${tag}_p_${ias}.csv $G "$@" >/dev/null 2>&1 &
  [ $((++n % 2)) = 0 ] && wait
done; wait
for ias in 400 460 520 560; do
 for a in 38 45 52 58; do
  tr=$(python3 -c "
import pandas as pd
d=pd.read_csv('runs/trig2/${tag}_p_${ias}.csv'); x=d[d.alpha>=$a]; print(round(float(x.t.iloc[0]),3) if len(x) else 'NA')")
  [ "$tr" = NA ] && continue
  t2=$(python3 -c "print($tr+0.05)")
  timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 pitch="0:0,0.3:0,0.4:1,12:1" roll="0:0,$tr:0,$t2:-1,12:-1" T=$(python3 -c "print($tr+4)") fcslog=runs/trig2/${tag}_r${a}_${ias}.csv $G "$@" >/dev/null 2>&1 &
  echo "$tag $ias $a $tr" >> runs/trig2/times.txt
  [ $((++n % 2)) = 0 ] && wait
 done
done; wait
