#!/bin/bash
# engine-out set. usage: TAG=x ./eo.sh key=val ...
cd /tmp/claude-0/-home-claude/49c90bd0-1fa5-5aa1-9e6b-3b1f8a22c307/scratchpad/ifr048/bz/benchx; export BENCH_DATA=/root/w/ifrit/ifrit_airframe.json; O=runs/eo_${TAG:-x}; rm -rf $O; mkdir -p $O; EX="$@"
B="FuelFrac=1 ThrustScale=1.08 DragScale=0.95 alt=600"
r(){ tag=$1; shift; dotnet bin/bench.dll ifly $B csv=$O/$tag.csv $EX "$@" >/dev/null 2>$O/$tag.err; }
r cruise500 ias=500 thr=1 T=12 engout=0.5 &
r cruise350 ias=350 thr=0.8 T=12 engout=0.5 &
r cruise250 ias=250 thr=1 T=12 engout=0.5 &
r roll350 ias=350 thr=1 T=10 engout=0.5 "roll=0:0,2:0,2.05:1,3.5:1,3.55:0,5:0,5.05:-1,6.5:-1,6.55:0" &
wait
for cap in 18 22 25 30; do r p250_c$cap ias=250 thr=1 T=14 "pitch=0:0,0.3:1" engout=0.0 EngineOutAoACap=$cap & done; wait
for cap in 18 22 25 30; do r p300_c$cap ias=300 thr=1 T=14 "pitch=0:0,0.3:1" engout=0.0 EngineOutAoACap=$cap & done; wait
for cap in 18 22 25 30; do r d350h_c$cap ias=350 thr=1 T=14 "pitch=0:0,0.3:0.5" engout=3 EngineOutAoACap=$cap & done; wait
for cap in 22 30; do r d350_c$cap ias=350 thr=1 T=14 "pitch=0:0,0.3:1" engout=3 EngineOutAoACap=$cap & done
r p300m_c22 ias=300 thr=0.9 T=14 "pitch=0:0,0.3:1" engout=0.0 EngineOutAoACap=22 &
r p300m_c30 ias=300 thr=0.9 T=14 "pitch=0:0,0.3:1" engout=0.0 EngineOutAoACap=30 &
wait
