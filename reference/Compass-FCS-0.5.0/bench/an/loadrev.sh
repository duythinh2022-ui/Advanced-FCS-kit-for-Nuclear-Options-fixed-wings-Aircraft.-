#!/bin/bash
# bench/an/loadrev.sh <tag> [Name=value ...]  - the F-22E 4.1.2 loaded-reversal set (its loadrev.sh) on this jet's ifly bench
# (loaded-reversal port, Nov 2026). Outputs bench/out/loadrev/<tag>/*.csv and bench/out/loadrev/out_loadrev_<tag>.txt;
# compare two tags with: python3 bench/an/loadrev_cmp.py bench/out/loadrev/<A> bench/out/loadrev/<B>
# One case's curve: python3 bench/an/lrcurve.py bench/out/loadrev <case> <tagA> <tagB>
# This jet: store kg K08..K21 give ~0.08 / 0.12 / 0.16 / 0.21 m of lateral CG offset on PART
DATA=compass_airframe.json; PART=wing1_L; K08=210; K12=320; K16=430; K21=570; AIL=aileron_L,aileron_R; FLAP=aileron_L,aileron_R; STAB=elevator_L,elevator_R; RUDL=rudder_L; RUDR=rudder_R
# Mapping from the F-22E: 700 -> K08, 1100 -> K12, 1500 -> K16, 2000 -> K21 kg. Times as loadrev.sh, with a 1 s lead (pull at t = 1).
tag=$1; shift
A=$(cd "$(dirname "$0")"; pwd)
B=$(cd "$A/.."; pwd); O=${BENCH_OUT:-$B/out}/loadrev/$tag; rm -rf "$O"; mkdir -p "$O/raw"
export BENCH_DATA=$B/extract/$DATA DOTNET_CLI_TELEMETRY_OPTOUT=1
N=${LOADREV_JOBS:-4}; i=0
kg(){ case $1 in 700) echo $K08;; 1100) echo $K12;; 1500) echo $K16;; 2000) echo $K21;; esac; }
f(){ python3 -c "print(round($1,3))"; }
r(){ local n=$1 pitch=$2 roll=$3 T=$4 k=$5; shift 5
  (cd $B && dotnet bin/bench.dll ifly alt=2000 thr=1 T=$T pitch=$pitch roll=$roll storeKg=$k storePart=$PART fcslog=$O/raw/$n.log.csv dbg=pS,pSTgtDbg,latAccDbg,loadRevDbg,revA dbgcsv=$O/raw/$n.dbg.csv "$@" >/dev/null 2>&1) || echo "FAILED $n"
  python3 $A/lrmerge.py $O/raw/$n.log.csv $O/raw/$n.dbg.csv $O/$n.csv $AIL $FLAP $STAB $RUDL $RUDR 2>/dev/null || echo "MERGEFAIL $n"; }
go(){ r "$@" & i=$((i+1)); if [ $((i % N)) = 0 ]; then wait; fi; }
single(){ # name ias pull hold rollFromPull d rollT rollRev post kgF
  local n=$1 ias=$2 pu=$3 ho=$4 rf=$5 d=$6 rt=$7 rr=$8 po=$9 k=${10}; shift 10
  local tP=1 tR=$(f "1+$rf"); local tE=$(f "max(1+$ho, $tR+$rt)+$po")
  local pitch="0:0,$tP:0,$(f "$tP+0.017"):$pu,$(f "$tP+$ho"):$pu,$(f "$tP+$ho+0.017"):0"
  local roll="0:0,$tR:0,$(f "$tR+0.017"):$d,$(f "$tR+$rr"):$d,$(f "$tR+$rr+0.017"):$((-d)),$(f "$tR+$rt"):$((-d)),$(f "$tR+$rt+0.017"):0"
  go $n "$pitch" "$roll" $tE $k ias=$ias "$@"; }
for ias in 450 520 600; do for d in -1 1; do single s1_${ias}_$([ $d = 1 ] && echo R || echo L) $ias 1 7 2.5 $d 3.5 1.6 0.5 $K21 "$@"; done; done
for kk in 700 1100; do for ias in 480 580; do for d in -1 1; do single s2_${kk}_${ias}_$([ $d = 1 ] && echo R || echo L) $ias 1 6.5 2.5 $d 3.3 1.3 0.5 $(kg $kk) "$@"; done; done; done
for ias in 520 650; do for d in -1 1; do single s3_${ias}_$([ $d = 1 ] && echo R || echo L) $ias 0.35 7 2.5 $d 3.5 1.6 0.5 $K21 "$@"; done; done
for kk in 1100 1500; do for ias in 420 480 540 600; do for d in -1 1; do single s4_${kk}_${ias}_$([ $d = 1 ] && echo R || echo L) $ias 1 10 6 $d 3.3 1.3 0.5 $(kg $kk) "$@"; done; done; done
P1="2.5:0,2.55:S,4.0:S,4.05:-S,5.4:-S,5.45:S,6.5:S,6.55:-S,7.15:-S,7.2:S,7.75:S,7.8:-S,8.9:-S,8.95:0"
P2="2.5:0,2.55:S,3.6:S,3.65:-S,4.7:-S,4.75:S,5.8:S,5.85:-S,6.9:-S,6.95:S,8.0:S,8.05:-S,9.1:-S,9.15:0"
P3="2.5:0,2.55:S,4.3:S,4.35:-S,6.1:-S,6.15:S,7.9:S,7.95:-S,9.4:-S,9.45:0"
for kk in 1100 1500 2000; do for ias in 480 520 600; do for pi in 1 2 3; do for sg in 1 -1; do
  eval "PP=\$P$pi"; RP=$(echo $PP | sed "s/-S/$((-1 * sg))/g; s/S/$sg/g")
  RP="0:0,$(python3 -c "print(','.join('%g:%s'%(float(a)+1,b) for a,b in (x.split(':') for x in '$RP'.split(','))))")"
  go spam_k${kk}_v${ias}_p${pi}_$([ $sg = 1 ] && echo R || echo L) "0:0,1:0,1.017:1,11.8:1,11.817:0" "$RP" 12.3 $(kg $kk) ias=$ias "$@"
done; done; done; done; wait
python3 -W ignore $A/loadrev.py "$O" > "$O/../out_loadrev_$tag.txt"; echo "done $tag: $(ls $O/*.csv | wc -l) csv"
