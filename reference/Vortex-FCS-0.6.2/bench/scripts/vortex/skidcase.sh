#!/bin/bash
# skid case: VTO, hover in the aft detent, a sideways kick at t 25, stick released
o=$1; shift
timeout 900 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=70 tog=0.5 "thr=0:0,5:0,5.5:0.25" "pitch=0:0,10:0,10.3:0.6,16:0.6,16.3:0" print=100 csv=$o "$@" > ${o%.csv}.txt 2>&1
