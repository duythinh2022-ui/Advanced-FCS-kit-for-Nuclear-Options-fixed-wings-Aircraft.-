#!/bin/bash
# 00:24 log repro: hover, lever 0.53 (~17 m/s fwd), full left roll 3.2 s, released 2 s, full right 2 s, full left 2 s, released
o=$1; shift
timeout 900 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=75 tog=0.5 "thr=0:0,5:0,5.5:0.20,25:0.20,25.5:0.53" "pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0" "roll=0:0,45:0,45.2:-1,48.2:-1,48.4:0,50.4:0,50.6:1,52.6:1,52.8:-1,54.8:-1,55:0" print=1 csv=$o "$@" > ${o%.csv}.txt 2>&1
