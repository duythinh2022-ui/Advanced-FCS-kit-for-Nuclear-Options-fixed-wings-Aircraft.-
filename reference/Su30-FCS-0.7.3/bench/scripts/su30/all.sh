#!/bin/bash
S=$(cd "$(dirname "$0")"; pwd)
G="SlipTvcAssist=false SlipGainHighAoA=0 CorkscrewSlipTol=5 AsymFFMaxBeta=12"
tag=$1; shift
for f in leaf hiroll pw wr onset; do bash $S/$f.sh $tag $G "$@"; done
cd /home/claude/su30/v10/src/bench/an; export BENCH_DIR=/home/claude/su30/v10/src/bench BENCH_DATA=/home/claude/su30/v10/src/bench/extract/su30_airframe.json AN_OUT=/home/claude/su30/v10/src/bench/out
mkdir -p $AN_OUT; rm -rf $AN_OUT/reg/$tag $AN_OUT/sw/$tag $AN_OUT/cork/$tag
python3 sreg.py $tag $G "$@" 2>&1 | grep -v Warn > $AN_OUT/out_reg_$tag.txt
python3 ssweep.py $tag $G "$@" 2>&1 | grep -v Warn > $AN_OUT/out_sw_$tag.txt
STOCK=1 python3 cork.py $tag $G "$@" 2>&1 | grep -v Warn > $AN_OUT/out_cork_$tag.txt
echo done $tag
