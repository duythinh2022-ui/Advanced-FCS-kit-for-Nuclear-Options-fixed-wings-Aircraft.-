#!/bin/bash
# hv3.sh <tag> <mass> [args] : heavy jet in the air: 250 km/h at 300 m, conversion at 1 s, lever at the 0 GS detent from 7 s to the end (T=200)
tag=$1; m=$2; shift 2; cd /home/claude/vortex; mkdir -p /tmp/hv
timeout 1500 ./b.sh stovl ias=250 alt=300 T=${T:-200} tog=1 mass=$m ThrustScale=1.12 DragScale=0.885 StovlTrcBankRate=30 StovlBankGainFromMargin=true StovlTrcLagMargin=0.32 "thr=0:0.15,6:0.15,7:0.20" print=100 csv=/tmp/hv/A${m}_$tag.csv "$@" > /tmp/hv/A${m}_$tag.txt 2>&1
