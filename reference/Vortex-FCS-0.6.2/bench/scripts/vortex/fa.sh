#!/bin/bash
# Vortex 0.5.4: Stability Assist off in STOVL flight (Tom's jet, tom.sh). usage: fa.sh <tag>   (BENCH_BIN picks the build; fam.py <tag> analyses)
v=$1; T=/home/claude/vortex/tom.sh; export OUT=/tmp/fa; mkdir -p $OUT
( $T H$v T=40 "thr=0:0" "mpo=20,40" ) &                                   # hover at 16 m, lever 0, off at 20 s
( $T F$v T=40 "thr=0:0,14:0,14.1:0.5" "mpo=24,40" ) &                     # ~30 kt, lever at the centre detent, off at 24 s
( $T M$v T=40 "thr=0:0,25:0,27:1,28:1,30:0.3" "mpo=20,40" ) &             # analog-style pick-up: lever 0 -> 1 -> 0.3 after the switch
( $T I$v T=45 "thr=0:0,20:0,20.1:0.5,32:0.5,32.1:0.20" ) &                # Stability Assist on (identity)
( $T G$v T=40 "thr=0:0,25:0,25.1:0.3" "mpo=2,40" ) &                      # off from the wheels (identity)
wait
