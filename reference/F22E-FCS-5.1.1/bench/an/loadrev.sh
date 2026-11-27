#!/bin/bash
# loadrev.sh <tag> [Name=value ...] : roll reversals with an asymmetric store (F-22E 4.1.1, 4.1.2). The suite has no such case:
# both versions' changes to the loaded reversal left all 153 suite files identical. 34 single reversals (s1-s3: full / part aft
# stick, 700 - 2000 kg on WingRear_L, the roll 2.5 s after the pull; s4, 4.1.2: 1100 / 1500 kg from a settled pull, the roll 6 s
# after it) in both directions, and 54 roll-spam runs, then loadrev.py's two tables. The _R cases reverse toward the side the load
# pulls, the _L cases against it. Compare two tags run by run with loadrev_cmp.py.
# Outputs: $BENCH_OUT/loadrev/<tag>/*.csv and $BENCH_OUT/out_loadrev_<tag>.txt. Compare two tags with diff.
B=$(cd "$(dirname "$0")/.."; pwd); S=${BENCH_OUT:-$B/out}; tag=$1; shift
export BENCH_DATA=${BENCH_DATA:-$B/data/airframe_cal.json} DOTNET_CLI_TELEMETRY_OPTOUT=1
O=$S/loadrev/$tag; rm -rf "$O"; mkdir -p "$O"; cd "$B"
N=${LOADREV_JOBS:-4}; i=0
r() { local n=$1; shift; dotnet bin/bench.dll one alt=2000 throttle=1 "$@" csv=$O/$n.csv >/dev/null 2>&1 || echo "FAILED $n"; }
go() { r "$@" & i=$((i+1)); if [ $((i % N)) = 0 ]; then wait; fi; }
for ias in 450 520 600; do for d in -1 1; do go s1_${ias}_$([ $d = 1 ] && echo R || echo L) iasKmh=$ias pull=1 hold=7 rollFromPull=2.5 roll=$d rollT=3.5 rollRev=1.6 post=0.5 storeKg=2000 storePart=WingRear_L "$@"; done; done
for kg in 700 1100; do for ias in 480 580; do for d in -1 1; do go s2_${kg}_${ias}_$([ $d = 1 ] && echo R || echo L) iasKmh=$ias pull=1 hold=6.5 rollFromPull=2.5 roll=$d rollT=3.3 rollRev=1.3 post=0.5 storeKg=$kg storePart=WingRear_L "$@"; done; done; done
for ias in 520 650; do for d in -1 1; do go s3_${ias}_$([ $d = 1 ] && echo R || echo L) iasKmh=$ias pull=0.35 hold=7 rollFromPull=2.5 roll=$d rollT=3.5 rollRev=1.6 post=0.5 storeKg=2000 storePart=WingRear_L "$@"; done; done
for kg in 1100 1500; do for ias in 420 480 540 600; do for d in -1 1; do go s4_${kg}_${ias}_$([ $d = 1 ] && echo R || echo L) iasKmh=$ias pull=1 hold=10 rollFromPull=6 roll=$d rollT=3.3 rollRev=1.3 post=0.5 storeKg=$kg storePart=WingRear_L "$@"; done; done; done
P1="2.5:0,2.55:S,4.0:S,4.05:-S,5.4:-S,5.45:S,6.5:S,6.55:-S,7.15:-S,7.2:S,7.75:S,7.8:-S,8.9:-S,8.95:0"
P2="2.5:0,2.55:S,3.6:S,3.65:-S,4.7:-S,4.75:S,5.8:S,5.85:-S,6.9:-S,6.95:S,8.0:S,8.05:-S,9.1:-S,9.15:0"
P3="2.5:0,2.55:S,4.3:S,4.35:-S,6.1:-S,6.15:S,7.9:S,7.95:-S,9.4:-S,9.45:0"
for kg in 1100 1500 2000; do for ias in 480 520 600; do for pi in 1 2 3; do for sg in 1 -1; do
  eval "PP=\$P$pi"; RP=$(echo $PP | sed "s/-S/$((-1 * sg))/g; s/S/$sg/g")
  go spam_k${kg}_v${ias}_p${pi}_$([ $sg = 1 ] && echo R || echo L) iasKmh=$ias pull=1 hold=10.8 post=0.5 rprof=$RP storeKg=$kg storePart=WingRear_L "$@"
done; done; done; done; wait
python3 -W ignore "$B/an/loadrev.py" "$O" | tee "$S/out_loadrev_$tag.txt"
