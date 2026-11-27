#!/bin/bash
# quick.sh <tree> <tag> [Name=value...] : sweep subset + the regression set; outputs under /home/claude/work/quick/<tag>
TREE=$(cd $1; pwd); tag=$2; shift 2; O=/home/claude/work/quick/$tag; mkdir -p $O; B=$TREE/bench
export BENCH_DIR=$B BENCH_DATA=$B/extract/rev_airframe.json BENCH_DMG=$B/data/revoker_damage.json AN_OUT=$O
cd $B/an; SW_IAS=${SW_IAS:-300,400,500,650} SW_PULL=${SW_PULL:-0.4,0.55} python3 ssweep.py $tag "$@" 2>&1 | grep -v Warn > $O/sw.txt; python3 sreg.py $tag "$@" 2>&1 | grep -v Warn > $O/reg.txt
tail -2 $O/sw.txt
