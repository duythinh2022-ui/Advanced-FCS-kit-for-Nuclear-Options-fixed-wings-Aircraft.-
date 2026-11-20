#!/bin/bash
# log 00:14 repro: hover, lever to 0.67 (forward ~17 m/s), full right pedal 2.4 s, released, lever 0.44
o=$1; shift
timeout 900 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=75 tog=0.5 "thr=0:0,5:0,5.5:0.20,25:0.20,25.5:0.67,40:0.67,40.3:0.44" "pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0" "yaw=0:0,35:0,35.2:1,37.6:1,37.8:0" print=1 csv=$o "$@" > ${o%.csv}.txt 2>&1
