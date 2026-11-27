#!/bin/bash
# Vortex 0.5.6: pedal pirouette and regime shift (Tom's jet, tom.sh). usage: py.sh <tag>   (BENCH_BIN picks the build)
# analyse: python3 pym.py C45<tag>:35.1:8:60 C30<tag>:30.1:8:55 T45<tag>:35.1:2:60 S45<tag>:35.1:5:60   (gs column is kt)
v=$1; T=/home/claude/vortex/tom.sh; export OUT=/tmp/py; mkdir -p $OUT
( $T C45$v T=60 "thr=0:0,20:0,20.1:0.64" "yaw=0:0,35:0,35.1:1,43:1,43.1:0" ) &        # 45 kt, full right pedal 8 s
( $T C30$v T=55 "thr=0:0,20:0,20.1:0.5" "yaw=0:0,30:0,30.1:1,38:1,38.1:0" ) &         # 30 kt, full right pedal 8 s
( $T T45$v T=60 "thr=0:0,20:0,20.1:0.64" "yaw=0:0,35:0,35.1:0.5,37:0.5,37.1:0" ) &    # 45 kt, half pedal 2 s
( MASS=12800 $T S45$v T=60 "thr=0:0,20:0,20.1:0.64" "yaw=0:0,35:0,35.1:1,40:1,40.1:0" ThrustScale=1 DragScale=1 ) &   # Stock 12.8 t, 45 kt, full pedal 5 s
( $T H$v T=40 "yaw=0:0,20:0,20.1:1,23:1,23.1:0" ) &                                   # hover pedal 3 s
( $T X$v T=60 "thr=0:0,20:0,20.1:1" ) &                                                # full lever from the hover: lever hand-over, CTOL blend
wait
