#!/bin/bash
# Vortex 0.5.7: Stability Assist off in the hover (Tom's 11:51 flip), and the pedal turn's share. usage: fs.sh <tag>   (BENCH_BIN picks the build)
v=$1; T=/home/claude/vortex/tom.sh; export OUT=/tmp/py; mkdir -p $OUT
( $T F$v T=45 "thr=0:0" "mpo=24,45" "roll=0:0,28:0,28.1:1,29.6:1,29.7:-1,30.2:-1,30.3:0" "yaw=0:0,26:0,26.1:1,29.8:1,29.9:0" ) &   # the log's inputs: pedal, full right stick 1.5 s, full left
( $T G$v T=45 "thr=0:0" "mpo=24,45" "roll=0:0,28:0,28.1:0.5,30:0.5,30.1:0" ) &      # half right stick 2 s
( $T K30$v T=55 "thr=0:0,20:0,20.1:0.5" "yaw=0:0,30:0,30.1:1,38:1,38.1:0" ) &       # pedal turn, 30 kt
( $T K45$v T=60 "thr=0:0,20:0,20.1:0.64" "yaw=0:0,35:0,35.1:1,43:1,43.1:0" ) &      # pedal turn, 45 kt
wait
