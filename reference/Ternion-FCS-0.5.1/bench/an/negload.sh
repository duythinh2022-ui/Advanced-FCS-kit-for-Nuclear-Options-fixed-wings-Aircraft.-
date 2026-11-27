#!/bin/bash
# bench/an/negload.sh <tag> [Name=value ...] - Ternion 0.2.3: negative-AoA set with an asymmetric store (the loaded-reversal set
# only pulls). Full forward stick held with a roll and a reversal each way, and pull -> pushover + roll. Outputs
# bench/out/negload/<tag>/*.csv (FCS log format) and out_negload_<tag>.txt (negload.py).
DATA=ternion_airframe.json; PART=wing_2a_L
tag=$1; shift
A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/negload/$tag; rm -rf "$O"; mkdir -p "$O"
export BENCH_DATA=$B/extract/$DATA DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${NEG_JOBS:-4}; i=0
go(){ local n=$1 pitch=$2 roll=$3 T=$4 k=$5; shift 5
  ( (cd $B && dotnet bin/bench.dll ifly alt=3000 thr=1 T=$T pitch=$pitch roll=$roll storeKg=$k storePart=$PART fcslog=$O/$n.csv "$@" >/dev/null 2>&1) || echo "FAILED $n" ) &
  i=$((i+1)); if [ $((i % N)) = 0 ]; then wait; fi; }
for k in 770 1030 1370 1800; do for ias in 220 260 300 340; do for d in 1 -1; do s=$([ $d = 1 ] && echo R || echo L)
  # push: full forward from 1 s, roll d at 4 s, reverse at 6 s, centre at 8 s, release the push at 9 s
  go push_k${k}_v${ias}_$s "0:0,1:0,1.017:-1,9:-1,9.017:0" "0:0,4:0,4.017:$d,6:$d,6.017:$((-d)),8:$((-d)),8.017:0" 10 $k ias=$ias "$@"
done; done; done
for k in 1030 1370 1800; do for ias in 380 450 520; do for d in 1 -1; do s=$([ $d = 1 ] && echo R || echo L)
  # pull 4 s, then straight to full forward with full roll d, reverse 2 s later
  go pp_k${k}_v${ias}_$s "0:0,1:0,1.017:1,5:1,5.017:-1,10:-1,10.017:0" "0:0,5:0,5.017:$d,7.5:$d,7.517:$((-d)),9.5:$((-d)),9.517:0" 11 $k ias=$ias "$@"
done; done; done; wait
python3 -W ignore $A/negload.py "$O" > "$O/../out_negload_$tag.txt"; echo "done $tag: $(ls $O/*.csv | wc -l) csv"
