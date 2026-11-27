#!/bin/bash
# bench/an/asymmat.sh <tag> [Name=value ...]  - F-22E 4.2.0: the asymmetric-load AoA-limit matrix (the Revoker 1.2.0 matrix on this
# bench's 'one' runs; store on WingRear_L: 3000 / 2000 / 1000 kg). Full-aft pulls held 20 s on afterburner / half / idle, roll reversals
# on the limit, nozzles locked (DisableTVC), pushes, one clean pull. Outputs bench/out/mat/<tag>/*.csv; summary: asymmat.py
tag=$1; shift; A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/mat/$tag; rm -rf $O; mkdir -p $O
export BENCH_DATA=${BENCH_DATA:-$B/data/airframe_cal.json} DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${ASYM_JOBS:-4}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll one alt=3000 storePart=WingRear_L "$@" csv=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
P="pull=1 hold=20 post=0.2 trimT=1"
for v in 250 300 400 500 600; do go pull_ab_$v throttle=1 iasKmh=$v $P storeKg=3000 "$@"; done
for v in 300 450; do go pull_half_$v throttle=0.5 iasKmh=$v $P storeKg=3000 "$@"; go pull_idle_$v throttle=0.05 iasKmh=$v $P storeKg=3000 "$@"; done
for v in 300 450; do go pull_ab_k2000_$v throttle=1 iasKmh=$v $P storeKg=2000 "$@"; go pull_ab_k1000_$v throttle=1 iasKmh=$v $P storeKg=1000 "$@"; done
for v in 300 450; do for d in 1 -1; do go roll_${v}_$([ $d = 1 ] && echo R || echo L) throttle=1 iasKmh=$v pull=1 hold=12 rollFromPull=6 roll=$d rollT=6 rollRev=3 post=0.3 trimT=1 storeKg=3000 "$@"; done; done
for v in 300 450; do go notvc_$v throttle=1 iasKmh=$v $P storeKg=3000 DisableTVC=true "$@"; go push_$v throttle=1 iasKmh=$v pull=-1 hold=13 post=0.2 trimT=1 storeKg=3000 "$@"; done
go notvc_roll_400 throttle=1 iasKmh=400 pull=1 hold=12 rollFromPull=6 roll=1 rollT=6 rollRev=3 post=0.3 trimT=1 storeKg=3000 DisableTVC=true "$@"
go notvc_idle_300 throttle=0.05 iasKmh=300 $P storeKg=3000 DisableTVC=true "$@"
go clean_pull_300 throttle=1 iasKmh=300 $P storeKg=0 "$@"
wait; echo "done $tag: $(ls $O/*.csv | wc -l) csv"
