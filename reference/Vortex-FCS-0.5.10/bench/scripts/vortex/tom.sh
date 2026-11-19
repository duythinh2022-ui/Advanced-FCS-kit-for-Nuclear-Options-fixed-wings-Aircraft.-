#!/bin/bash
# Tom's 17 Nov 22:56 settings and jet (Buffed, 13 824 kg, tilt ceiling off, bank gain from the margin); usage: tom.sh <name> <bench args>
n=$1; shift
timeout 900 /home/claude/vortex/b.sh stovl ground=1 ias=0 tog=0.5 mass=${MASS:-13824} ThrustScale=1.12 DragScale=0.885 StovlTrcBankRate=30 StovlBankGainFromMargin=true StovlTrcLagMargin=0.32 "pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0" print=100 csv=${OUT:-/tmp/r45}/$n.csv "$@" > ${OUT:-/tmp/r45}/$n.txt 2>&1
