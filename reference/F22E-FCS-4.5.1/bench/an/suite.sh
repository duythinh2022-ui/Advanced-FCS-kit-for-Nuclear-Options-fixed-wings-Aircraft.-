#!/bin/bash
# suite.sh <tag> [Name=value ...] : reversals, corkscrew entries, regression set, utilisation invariant, damage suite
# Outputs: $BENCH_OUT/out_{rev,cork,reg,util,dmg}_<tag>.txt (default BENCH_OUT = <bench>/out). Compare two tags with diff.
B=$(cd "$(dirname "$0")/.."; pwd); A=$B/an; S=${BENCH_OUT:-$B/out}; mkdir -p "$S"
tag=$1; shift
export BENCH_DIR=$B REG_S=$S BENCH_DATA=${BENCH_DATA:-$B/data/airframe_cal.json}
cd $A
rm -rf $S/revs/$tag $S/cork/$tag $S/reg/$tag $S/util/$tag $S/dmg/$tag
python3 revsuite.py $B $tag "$@" 2>&1 | grep -v Warn > $S/out_rev_$tag.txt
python3 corkentry.py $B $tag "$@" 2>&1 | grep -v Warn > $S/out_cork_$tag.txt
python3 reg.py $tag "$@" 2>&1 | grep -v Warn > $S/out_reg_$tag.txt
UTIL_TMP=$S/util/$tag python3 utilcheck.py f22 "$@" > $S/out_util_$tag.txt 2>&1
python3 dmgsuite.py $tag "$@" > $S/out_dmg_$tag.txt 2>&1
echo "== $tag $@"; cat $S/out_rev_$tag.txt; echo; cat $S/out_cork_$tag.txt; echo; cat $S/out_reg_$tag.txt; echo; tail -25 $S/out_util_$tag.txt; echo; cat $S/out_dmg_$tag.txt
