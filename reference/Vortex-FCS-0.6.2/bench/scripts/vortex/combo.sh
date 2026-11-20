#!/bin/bash
# hover, then full right stick (15 kt sideways) + lever to 30 kt forward together for 8 s, both released / back to the aft
# detent; then a sideways stop with full climb stick in the middle of it
o=$1; B=${2:-/home/claude/vortex/b.sh}
timeout 900 $B stovl ground=1 ias=0 T=70 tog=0.5 "thr=0:0,5:0,5.5:0.20,25:0.20,25.3:0.5,33:0.5,33.3:0.20" "pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0,49:0,49.2:1,52:1,52.2:0" "roll=0:0,25:0,25.3:1,33:1,33.3:0,45:0,45.3:1,48:1,48.3:0" print=1 csv=$o > ${o%.csv}.txt 2>&1
