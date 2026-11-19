#!/bin/bash
# H case only. usage: hrun.sh <outdir> [bench args]
o=$1; shift; mkdir -p $o
B=/home/claude/vortex/b.sh
timeout 900 $B stovl ground=1 ias=0 T=125 tog=0.5 "thr=0:0,65:0,65.1:0.35,78:0.35,78.1:0.20,90:0.20,90.1:0.35,95:0.35,95.1:0.20" "pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0" "roll=0:0,20:0,20.1:1,25:1,25.1:0,40:0,40.1:-0.4,41.5:-0.4,41.6:0,90:0,90.1:1,95:1,95.1:0" "yaw=0:0,50:0,50.1:1,53:1,53.1:0" kick=110,3,0 print=100 csv=$o/H.csv "$@" > $o/H.txt 2>&1
