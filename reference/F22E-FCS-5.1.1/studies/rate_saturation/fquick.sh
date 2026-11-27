#!/bin/bash
# fquick.sh <tree> <tag> [Name=value...] : F-22E regression set + corkscrew entries only; outputs under /home/claude/work/fquick/<tag>
TREE=$(cd $1; pwd); tag=$2; shift 2; O=/home/claude/work/fquick/$tag; mkdir -p $O; B=$TREE/bench
export BENCH_DIR=$B REG_S=$O BENCH_DATA=$B/data/airframe_cal.json
cd $B/an; python3 reg.py $tag "$@" 2>&1 | grep -v Warn > $O/reg.txt; python3 corkentry.py $B $tag "$@" 2>&1 | grep -v Warn > $O/cork.txt
