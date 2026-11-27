#!/bin/bash
# rhold.sh <tree> <tag> [sets] : Revoker held loaded rolls (0.45 aft or full aft, full roll from 3 s), TVC on / off
TREE=$(cd $1; pwd); tag=$2; shift 2; X=("$@"); B=$TREE/bench; R=/home/claude/work/runs; T=/home/claude/work/tools
export BENCH_DIR=$B BENCH_DATA=$B/extract/rev_airframe.json BENCH_DMG=$B/data/revoker_damage.json
for c in "300 0.45 false" "460 0.45 false" "400 1 false" "300 0.45 true" "460 0.45 true" "400 1 true"; do set -- $c
  f=$R/rh_${tag}_$1_$2_$3.csv; (cd $B && dotnet bin/bench.dll ifly ias=$1 alt=1500 thr=1 T=12 pitch=0:0,0.05:$2,12:$2 roll=0:0,3:0,3.05:1,12:1 DisableTVC=$3 "${X[@]}" fcslog=$f >/dev/null 2>&1)
  python3 $T/rs1.py "$tag $1 km/h pull $2 tvcOff=$3" $f 3.6 11.8
done
