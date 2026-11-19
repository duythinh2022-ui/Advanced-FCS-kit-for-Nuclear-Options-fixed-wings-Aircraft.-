#!/bin/bash
# sw.sh tag ias sign(1 pull,-1 push) case(none|step|ramp) [extra]
tag=$1; ias=$2; s=$3; c=$4; shift 4
case $c in
 none) P="0:$s,8:$s"; R="0:1,8:1";;
 step) P="0:0,1.49:0,1.5:$s,8:$s"; R="0:1,8:1";;
 ramp) P="0:0,1.5:0,3.0:$s,8:$s"; R="0:1,8:1";;
esac
dotnet bin/bench.dll one AoANeg=-50 iasKmh=$ias throttle=1 trimT=3 hold=8 post=0 prof="$P" rprof="$R" rollFromPull=0 rollT=8 csv=runs/$tag.csv "$@" >/dev/null 2>runs/$tag.err
