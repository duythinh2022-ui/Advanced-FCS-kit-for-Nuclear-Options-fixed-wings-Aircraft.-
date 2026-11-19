#!/bin/bash
# Vortex 0.5.0 vane-box set on Tom's jet (tom.sh: Buffed 13 824 kg, his settings). usage: vn.sh <tag> [bench args]   (BENCH_BIN picks the build)
tag=$1; shift; o=/tmp/vn/$tag; mkdir -p $o; export OUT=$o
T="./tom.sh"
( $T P30 T=45 "thr=0:0,20:0,20.1:0.5,32:0.5,32.1:0.20" "$@" ) &          # hover -> 30 kt, then stop
( $T P45 T=50 "thr=0:0,20:0,20.1:0.64,35:0.64,35.1:0.20" "$@" ) &        # hover -> 45 kt, then stop
wait
( $T P2 T=40 "thr=0:0,20:0,20.1:0.64" "roll=0:0,23.1:0,23.2:1,30:1,30.1:0" "$@" ) &   # side stick 3 s into the 45 kt acceleration
( $T PS T=45 "thr=0:0,20:0,20.1:0.5,30:0.5,30.1:0.20" "roll=0:0,30.6:0,30.7:1,34:1,34.1:0" "$@" ) &   # side stick during the fore-aft stop
wait
( $T PB T=50 "thr=0:0,20:0,20.1:0.5,28:0.5,28.1:0.20" "roll=0:0,20:0,20.1:1,28:1,28.1:0" "$@" ) &   # side + fore-aft together 8 s, both released
( $T PBK T=45 "thr=0:0,5:0,5.5:0.20,20:0.20,20.1:0.02,30:0.02,30.1:0.20" "$@" ) &   # backwards and stop (the lever leaves the hover detent downward)
wait
( $T Y30 T=50 "thr=0:0,20:0,20.1:0.5" "yaw=0:0,30:0,30.1:-1,38:-1,38.1:0" "$@" ) &   # full left pedal 8 s at 30 kt
( $T Y45 T=55 "thr=0:0,20:0,20.1:0.64" "yaw=0:0,35:0,35.1:-1,43:-1,43.1:0" "$@" ) &  # at 45 kt
wait
echo "$tag done"
