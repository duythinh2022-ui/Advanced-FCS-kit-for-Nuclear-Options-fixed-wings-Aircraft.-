#!/bin/bash
# bench/an/asymnotvc.sh <tag> [Name=value ...]  - F-22E 4.2.0 (from the Revoker 1.3.0): the no-TVC / low-thrust half of the asymmetric-load matrix on
# this bench's 'one' runs (store on WingRear_L, 3000 / 2000 / 1000 kg). Nozzles locked (DisableTVC) at afterburner / half / idle, the TVC on
# at idle (low thrust), roll reversals on the limit, pushes, one clean pull. Outputs bench/out/matn/<tag>/*.csv (summary: asymmat.py with
# OUTSUB=matn)
tag=$1; shift; A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/matn/$tag; rm -rf $O; mkdir -p $O
export BENCH_DATA=${BENCH_DATA:-$B/data/airframe_cal.json} DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${ASYM_JOBS:-4}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll one alt=3000 storePart=WingRear_L "$@" csv=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
P="pull=1 hold=20 post=0.2 trimT=1"; L=DisableTVC=true
for v in 250 300 400 500 600; do go nt_ab_$v throttle=1 iasKmh=$v $P storeKg=3000 $L "$@"; done
for v in 300 450; do go nt_half_$v throttle=0.5 iasKmh=$v $P storeKg=3000 $L "$@"; go nt_idle_$v throttle=0.05 iasKmh=$v $P storeKg=3000 $L "$@"; done
for v in 300 450; do go lowthr_tvc_$v throttle=0.05 iasKmh=$v $P storeKg=3000 "$@"; done
for v in 300 450; do go nt_k2000_$v throttle=1 iasKmh=$v $P storeKg=2000 $L "$@"; go nt_k1000_$v throttle=1 iasKmh=$v $P storeKg=1000 $L "$@"; done
for v in 300 450; do for d in 1 -1; do go nt_roll_${v}_$([ $d = 1 ] && echo R || echo L) throttle=1 iasKmh=$v pull=1 hold=12 rollFromPull=6 roll=$d rollT=6 rollRev=3 post=0.3 trimT=1 storeKg=3000 $L "$@"; done; done
for v in 300 450; do go nt_push_$v throttle=1 iasKmh=$v pull=-1 hold=13 post=0.2 trimT=1 storeKg=3000 $L "$@"; done
go nt_clean_300 throttle=1 iasKmh=300 $P storeKg=0 $L "$@"
wait; echo "done $tag: $(ls $O/*.csv | wc -l) csv"
