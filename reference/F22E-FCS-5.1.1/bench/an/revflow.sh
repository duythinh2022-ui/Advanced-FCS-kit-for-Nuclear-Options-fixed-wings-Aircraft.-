#!/bin/bash
# 4.3.0: reversed-flight lateral cases (MPO tail slides). revflow.sh <tag> [Name=value]; RevFlowNoseAxis=false = the 4.2.0 law. Then: python3 an/revflow_an.py <tagA> <tagB>
# reversed-flight lateral cases; $1 = tag, rest = Name=value
cd "$(dirname "$0")/.."; export BENCH_DATA=$PWD/data/airframe_cal.json
tag=$1; shift
B="iasKmh=300 alt=4000 throttle=0.25 trimT=1 hold=0 prof=0:0 roll=0 post=25 FlightAssist=false"
run(){ n=$1; shift; dotnet bin/bench.dll one $B "$@" csv=/tmp/rv_${tag}_$n.csv > /dev/null 2>&1; }
run A gamma0=60 rprof=0:0,18:0,18.05:1,19.5:1,19.55:0 "$@" &
run B gamma0=60 rprof=0:0,21:0,21.05:-1,22.5:-1,22.55:0 "$@" &
wait
run C gamma0=60 yprof=0:0,18:0,18.05:-1,19.5:-1,19.55:0 "$@" &
run D gamma0=80 rprof=0:0,17:0,17.05:1,18.5:1,18.55:0 "$@" &
wait
run E gamma0=60 rprof=0:0,15.5:0,15.55:1,17:1,17.05:0 "$@" &
run F gamma0=60 yprof=0:0,21:0,21.05:1,22.5:1,22.55:0 "$@" &
wait
