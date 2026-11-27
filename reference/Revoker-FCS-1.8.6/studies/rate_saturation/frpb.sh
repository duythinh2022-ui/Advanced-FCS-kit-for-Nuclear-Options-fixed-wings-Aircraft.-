#!/bin/bash
# frpb.sh <tree> <tag> [Name=value...] : replay F-22E loaded-roll windows of the 23 Nov 21:09 and 24 Nov 00:42 logs
TREE=$1; tag=$2; shift 2; X=("$@"); L="/mnt/user-data/uploads/Nuclear Option/BepInEx"; R=/home/claude/work/runs; T=/home/claude/work/tools
i=0
for win in "F22E_FCS_20261123_210930 80.8 84.6" "F22E_FCS_20261123_210930 50.5 54" "F22E_FCS_20261123_210930 20.7 24.4" "F22E_FCS_20261123_210930 31.6 38.2" "F22E_FCS_20261123_210930 114.4 117.6" "F22E_FCS_20261124_004203 68.7 72.4"; do set -- $win; i=$((i+1))
  bash $TREE/bench.sh replay "$L/$1.csv" $2 $3 $R/frb_${tag}_$i.csv "${X[@]}" 2>&1 | grep -i "exception\|unknown"
  python3 $T/frs.py "$tag w$i" $R/frb_${tag}_$i.csv
done
