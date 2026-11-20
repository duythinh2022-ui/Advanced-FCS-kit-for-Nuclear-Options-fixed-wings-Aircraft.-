#!/bin/bash
# k30run.sh <tag> [args] : K30 / K30C / K45 / K45C on Tom's jet
tag=$1; shift; cd /home/claude/vortex; export OUT=/tmp/ax; mkdir -p $OUT
K='thr=0:0,20:0,20.1:0.5'; Y='yaw=0:0,30:0,30.1:1,38:1,38.1:0'; PC='pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0,29.5:0,29.6:1,38:1,38.1:0'
K4='thr=0:0,20:0,20.1:0.64'; Y4='yaw=0:0,35:0,35.1:1,43:1,43.1:0'; PC4='pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0,34.5:0,34.6:1,43:1,43.1:0'
( ./tom.sh K30_$tag T=50 "$K" "$Y" "$@" ) & ( ./tom.sh K30C_$tag T=50 "$K" "$Y" "$PC" "$@" ) &
( ./tom.sh K45_$tag T=55 "$K4" "$Y4" "$@" ) & ( ./tom.sh K45C_$tag T=55 "$K4" "$Y4" "$PC4" "$@" ) & wait
PH='pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0,29.5:0,29.6:1,34:1,34.1:0'
( ./tom.sh KH30_$tag T=50 "$K" "$Y" "$PH" "$@" ) &   # pedal held, climb released mid-turn
( ./tom.sh KD30_$tag T=50 "$K" "$Y" "pitch=0:0,8:0,8.3:1,16:1,16.3:0,32:0,32.1:-1,35:-1,35.1:0" "$@" ) &   # pedal held, descent and its stop mid-turn
wait
