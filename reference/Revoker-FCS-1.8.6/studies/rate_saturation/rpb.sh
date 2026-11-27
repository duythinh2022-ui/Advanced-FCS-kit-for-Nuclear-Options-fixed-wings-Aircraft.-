#!/bin/bash
# rpb.sh <tree> <tag> [Name=value...] : replay the Revoker 02:21 log's three loaded-roll windows (TVC off) and print one line each
TREE=$1; tag=$2; shift 2; X=("$@"); L="/mnt/user-data/uploads/Nuclear Option/BepInEx"; R=/home/claude/work/runs; T=/home/claude/work/tools; G="$L/Revoker_FCS_20261124_022123.csv"
i=0
for win in "46.2 57 35" "96.7 103.2 30" "120.3 126.8 30"; do set -- $win; i=$((i+1))
  bash $TREE/bench.sh replay "$G" $1 $2 $R/rb_${tag}_$i.csv DisableTVC=true NoTvcAoACapPos=$3 NoTvcThrCapPos=$3 "${X[@]}" 2>&1 | grep -i "exception\|unknown"
  python3 $T/rs1.py "$tag w$i" $R/rb_${tag}_$i.csv $1 $2
done
