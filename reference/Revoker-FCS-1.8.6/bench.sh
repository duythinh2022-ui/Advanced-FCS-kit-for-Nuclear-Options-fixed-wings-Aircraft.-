#!/bin/bash
# bench.sh : revoker bench wrapper. Run from anywhere.
#   bench.sh build                      - sync the FCS sources (Revoker_FS12.FCS) into bench/fcs, regenerate the Plugin stub, build bench/bin/bench.dll
#   bench.sh suite <tag> [Name=value]   - regression set + stop/reversal sweep + corkscrews + damage suite -> bench/out/out_*_<tag>.txt
#   bench.sh diff <tagA> <tagB>         - diff the output files of two tags
#   bench.sh fly <args...>              - one ifly case, e.g. fly ias=400 alt=1500 thr=1 T=6 pitch=0:0,0.2:1 roll=0:0,0.2:1 fcslog=bench/out/x.csv
#   bench.sh replay <log.csv> <t0> <t1> <out.csv> [pinPitch=1] [Name=value] - closed-loop replay of a log window (pinPitch=1: pitch plane held to the log)
#   bench.sh replaycmp <log.csv> <replay.csv...>  - bench replay vs the game, by column
#   bench.sh eqerr log=<FCS log csv> out=<csv>   - equation-error dump of a flight log (bench vs logged accelerations)
R=$(cd "$(dirname "$0")"; pwd); B=$R/bench; export BENCH_DIR=$B BENCH_DATA=${BENCH_DATA:-$B/extract/rev_airframe.json} BENCH_DMG=${BENCH_DMG:-$B/data/revoker_damage.json} AN_OUT=${BENCH_OUT:-$B/out}
case "$1" in
  build) (cd "$B" && bash rsync.sh .. && dotnet build -c Release -o bin 2>&1 | grep -E " error |Build succeeded|warn.*CS8" | grep -v CS8 )
         # 1.1.0: the native core for the bench (Linux build of native/fcs_core); without cargo the bench runs the C# code
         (cd "$R/native/fcs_core" && bash ../build.sh linux 2>&1 | grep -E "^error|Finished"; cp -f target/release/lib*_FcsCore.so "$B/bin/" 2>/dev/null) || true ;;
  suite) shift; tag=$1; shift; mkdir -p "$AN_OUT"; cd "$B/an"
         rm -rf "$AN_OUT/reg/$tag" "$AN_OUT/sw/$tag" "$AN_OUT/cork/$tag" "$AN_OUT/sdmg/revoker/$tag"
         python3 sreg.py "$tag" "$@" 2>&1 | grep -v Warn > "$AN_OUT/out_reg_$tag.txt"
         python3 ssweep.py "$tag" "$@" 2>&1 | grep -v Warn > "$AN_OUT/out_sw_$tag.txt"
         STOCK=1 python3 cork.py "$tag" "$@" 2>&1 | grep -v Warn > "$AN_OUT/out_cork_$tag.txt"
         python3 sdmgsuite.py revoker "$tag" "$@" > "$AN_OUT/out_dmg_$tag.txt" 2>&1
         echo "== $tag $@"; for k in reg sw cork dmg; do echo "--- $k"; cat "$AN_OUT/out_${k}_$tag.txt"; done ;;
  diff)  for k in reg sw cork dmg; do echo "=== $k"; diff "$AN_OUT/out_${k}_$2.txt" "$AN_OUT/out_${k}_$3.txt"; done ;;
  fly)   shift; dotnet "$B/bin/bench.dll" ifly "$@" ;;
  replay) shift; log=$1; t0=$2; t1=$3; out=$4; shift 4; dotnet "$B/bin/bench.dll" replay "log=$log" "t0=$t0" "t1=$t1" "out=$out" "$@" ;;
  replaycmp) shift; python3 "$B/an/replaycmp.py" "$@" ;;
  eqerr) shift; dotnet "$B/bin/bench.dll" eqerr "$@" ;;
  *) sed -n 2,7p "$0" ;;
esac
