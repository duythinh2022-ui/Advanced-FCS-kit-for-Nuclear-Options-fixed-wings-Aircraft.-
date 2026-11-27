#!/bin/bash
# stop.sh <tag> <rollstart> [Name=value...]  : pitch-released full roll, 450 km/h, cap 36, no TVC
tag=$1; st=$2; shift 2
e=$(python3 -c "print($st+3)")
B=${BENCH_DIR:-$(cd "$(dirname "$0")/../../bench" && pwd)}; RUNS=${RUNS:-$(cd "$(dirname "$0")/../.." && pwd)/runs}; mkdir -p "$RUNS"
cd $B && BENCH_DATA=${BENCH_DATA:-$B/data/airframe_cal.json} dotnet bin/bench.dll one iasKmh=450 trimT=3 throttle=-1 thr2=1 thr2T=0 "prof=0:1,2:1,2.01:0,9:0" "rprof=0:0,$st:0,$(python3 -c "print($st+0.01)"):1,$e:1,$(python3 -c "print($e+0.01)"):0" hold=$e post=3 DisableTVC=true AoAPos=36 NoTvcAoACapPos=36 NoTvcThrCapPos=36 "$@" csv=$RUNS/stop_$tag.csv >/dev/null 2>&1
python3 -W ignore -c "
import pandas as pd;d=pd.read_csv('$RUNS/stop_$tag.csv');a=d[d.t>=0]
print('%-28s beta max %5.1f  alpha@rel %4.1f'%('$tag',a.beta.abs().max(),d.iloc[d.t.abs().argmin()].alpha))"
