#!/bin/bash
# cs.sh tag neg ias gamma0 throttle T roll  [extra]
tag=$1; neg=$2; ias=$3; g=$4; thr=$5; T=$6; rl=$7; shift 7
dotnet bin/bench.dll one AoANeg=$neg iasKmh=$ias gamma0=$g throttle=$thr trimT=3 hold=$T post=0 prof="0:-1,$T:-1" rprof="0:$rl,$T:$rl" rollFromPull=0 rollT=$T csv=runs/$tag.csv "$@" >/dev/null 2>runs/$tag.err
