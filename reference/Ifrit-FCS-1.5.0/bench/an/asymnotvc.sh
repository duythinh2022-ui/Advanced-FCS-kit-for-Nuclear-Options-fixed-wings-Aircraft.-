#!/bin/bash
# bench/an/asymnotvc.sh <tag> [Name=value ...]  - Ifrit 1.2.0 (from the Revoker 1.3.0): the no-TVC / low-thrust half of the asymmetric-load matrix (store on
# wing1_L, 2450 kg ~ 30 cm, 1280 kg ~ 16 cm, 630 kg ~ 8 cm). Nozzles locked (DisableTVC) at afterburner / half / idle, the TVC on at idle
# (low thrust), roll reversals on the limit, pushes, a throttle cut and back. Outputs bench/out/matn/<tag>/*.csv (summary: asymmat.py
# with OUTSUB=matn)
tag=$1; shift; A=$(cd "$(dirname "$0")"; pwd); B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/matn/$tag; rm -rf $O; mkdir -p $O
export BENCH_DATA=$B/extract/ifrit_airframe.json BENCH_DMG=$B/data/ifrit_damage.json DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${ASYM_JOBS:-4}; i=0; go(){ local n=$1; shift; (cd $B && dotnet bin/bench.dll ifly alt=3000 storePart=wing1_L "$@" fcslog=$O/$n.csv >/dev/null 2>&1 || echo FAILED $n) & i=$((i+1)); [ $((i%N)) = 0 ] && wait; }
PULL="pitch=0:0,3:0,3.02:1,40:1"; PUSH="pitch=0:0,3:0,3.02:-1,40:-1"; L=DisableTVC=true
for v in 250 300 400 500 600; do go nt_ab_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=2450 $L "$@"; done
for v in 300 450; do go nt_half_$v thr=0.5 ias=$v T=22 $PULL roll=0:0 storeKg=2450 $L "$@"; go nt_idle_$v thr=0.1 ias=$v T=18 $PULL roll=0:0 storeKg=2450 $L "$@"; done
for v in 300 450; do go lowthr_tvc_$v thr=0.1 ias=$v T=18 $PULL roll=0:0 storeKg=2450 "$@"; done
for v in 300 450; do go nt_k1280_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=1280 $L "$@"; go nt_k630_$v thr=1 ias=$v T=22 $PULL roll=0:0 storeKg=630 $L "$@"; done
for v in 300 450; do for d in 1 -1; do go nt_roll_${v}_$([ $d = 1 ] && echo R || echo L) thr=1 ias=$v T=22 $PULL roll=0:0,9:0,9.02:$d,12:$d,12.02:$((-d)),15:$((-d)),15.02:0 storeKg=2450 $L "$@"; done; done
for v in 300 450; do go nt_push_$v thr=1 ias=$v T=16 $PUSH roll=0:0 storeKg=2450 $L "$@"; done
go nt_thrcut_350 ias=350 T=24 thrp=0:1,9:1,9.2:0.1,15:0.1,15.2:1 $PULL roll=0:0 storeKg=2450 $L "$@"
go nt_clean_300 thr=1 ias=300 T=16 $PULL roll=0:0 storeKg=0 $L "$@"
wait; echo "done $tag: $(ls $O/*.csv | wc -l) csv"
