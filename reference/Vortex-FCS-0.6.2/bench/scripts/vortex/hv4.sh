#!/bin/bash
# hv4.sh <tag> <mass> [args] : heavy jet: short take-off, climb, lever to 0 GS at 25 s; descent (0.3 stick) from 70 s to the runway, lever untouched
tag=$1; m=$2; shift 2; cd /home/claude/vortex; export OUT=/tmp/hv
MASS=$m ./tom.sh V${m}_$tag T=200 "thr=0:0,6:0,6.3:1,25:1,25.1:0.20" "pitch=0:0,10:0,10.3:0.5,16:0.5,16.3:0,70:0,70.3:-0.3,200:-0.3" "$@"
