#!/bin/bash
# fl.sh <tag> <mass> [args]: airborne conversion at 250 km/h, lever at 0 GS; full left pedal 60-70 s; full left stick 80-86 s; Stock
tag=$1; m=$2; shift 2; cd /home/claude/vortex; mkdir -p /tmp/fl
timeout 1500 ./b.sh stovl ias=250 alt=300 T=100 tog=1 mass=$m ThrustScale=1 DragScale=1 StovlTrcBankRate=30 StovlBankGainFromMargin=true StovlTrcLagMargin=0.32 "thr=0:0.15,6:0.15,7:0.20" "yaw=0:0,60:0,60.1:-1,70:-1,70.1:0" "roll=0:0,80:0,80.1:-1,86:-1,86.1:0" print=100 csv=/tmp/fl/$tag.csv "$@" > /tmp/fl/$tag.txt 2>&1
