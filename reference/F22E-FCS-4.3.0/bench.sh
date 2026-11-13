#!/bin/bash
# bench.sh : convenience wrapper. Run from anywhere.
#   bench.sh build                      - sync the FCS sources into bench/fcs and build bench/bin/bench.dll (= regen.sh)
#   bench.sh suite <tag> [Name=value]   - the full suite -> bench/out/out_*_<tag>.txt
#   bench.sh diff <tagA> <tagB>         - diff the five output files of two tags
#   bench.sh replay <log.csv> <t0> <t1> <out.csv> [Name=value]  - replay a flight log window from its logged state
#   bench.sh one <args...>              - a scripted case, e.g. one iasKmh=560 alt=1500 prof=0:0,0.05:1,8:1 rprof=... csv=out.csv
R=$(cd "$(dirname "$0")"; pwd); export BENCH_DATA=${BENCH_DATA:-$R/bench/data/airframe_cal.json}
case "$1" in
  build) bash "$R/regen.sh" ;;
  suite) shift; bash "$R/bench/an/suite.sh" "$@" ;;
  diff)  for k in rev cork reg util dmg; do echo "=== $k"; diff "$R/bench/out/out_${k}_$2.txt" "$R/bench/out/out_${k}_$3.txt"; done ;;
  replay) shift; log=$1; t0=$2; t1=$3; out=$4; shift 4; (cd "$R/bench" && dotnet bin/bench.dll replay "log=$log" "t0=$t0" "t1=$t1" "out=$out" "$@") ;;
  one)   shift; (cd "$R/bench" && dotnet bin/bench.dll one "$@") ;;
  replaycheck) shift; tag=$1; logdir=$2; shift 2; python3 -W ignore "$R/bench/an/replaycheck.py" "$R/bench" "$logdir" "${CASES:-$R/bench/calib/leaf_cases.json}" "$tag" "$@" ;;
  *) sed -n 2,8p "$0" ;;
esac
