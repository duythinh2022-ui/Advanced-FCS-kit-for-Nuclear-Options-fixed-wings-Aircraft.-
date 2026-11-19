#!/bin/bash
# bench/an/stopmat.sh <tag> [Name=value ...]  - Su-30 0.5.1: released-roll stops at the AoA limit, nozzles locked (24 ifly cases).
# R = 800 kg on Right_wing_mid (16 cm of lateral CG offset at 22.8 t), N = clean; P / M = roll to + / -; full aft stick from 450 km/h at
# 3 s, full roll stick for 1.5 s from 6 / 9 / 12 s (about 165 / 105 / 80 kt at the release), then the roll stick centred with the pitch
# stick held (hold) or centred too (rel). Outputs bench/out/stop/<tag>/*.csv; summary: python3 bench/an/stopmat.py <tagA> [<tagB> ...]
# 0.5.0 laws: stopmat.sh off PushGovNsByPitchCmd=false PushGovRelNoGrow=false AsymStopTime=0
tag=$1; shift; A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/stop/$tag; rm -rf $O; mkdir -p $O
export BENCH_DATA=$B/extract/su30_airframe.json DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${MAT_JOBS:-3}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=3500 "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
for ld in R N; do
 [ $ld = R ] && L="storePart=Right_wing_mid storeKg=${STOREKG:-800}" || L="storeKg=0"
 for d in 1 -1; do for tr in 6 9 12; do
  te=$(python3 -c "print($tr+1.5)"); te2=$(python3 -c "print($tr+1.52)"); tr2=$(python3 -c "print($tr+0.02)")
  R="roll=0:0,$tr:0,$tr2:$d,$te:$d,$te2:0"
  dn=$([ $d = 1 ] && echo P || echo M)
  go ${ld}_${dn}_${tr}_hold thr=1 ias=450 T=$(python3 -c "print($tr+5.5)") pitch=0:0,3:0,3.02:1,40:1 $R $L DisableTVC=true "$@"
  go ${ld}_${dn}_${tr}_rel  thr=1 ias=450 T=$(python3 -c "print($tr+5.5)") pitch=0:0,3:0,3.02:1,$te:1,$te2:0 $R $L DisableTVC=true "$@"
 done; done
done; wait; echo "done $tag: $(ls $O/*.csv | wc -l)"
