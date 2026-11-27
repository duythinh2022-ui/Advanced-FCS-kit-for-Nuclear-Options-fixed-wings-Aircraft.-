#!/bin/bash
# ls8.sh tag [sets]: the 8 low-speed sideslip events of the 16:16 log
tag=$1; shift; O=/home/claude/su30/rp/ls8/$tag; mkdir -p $O; L="/mnt/user-data/uploads/Nuclear Option/BepInEx/Su30_FCS_20261110_161649.csv"
cd /home/claude/su30/src
for t in 78.3 124.4 129.8 613.4 858.5 950.1 1046.9 1049.8; do t0=$(python3 -c "print(round($t-3+${OFF:-0},2))"); t1=$(python3 -c "print(round($t+8,2))"); bash bench.sh replay "$L" $t0 $t1 $O/$t.csv AoAPos=75 "$@" >/dev/null 2>&1 & done; wait
