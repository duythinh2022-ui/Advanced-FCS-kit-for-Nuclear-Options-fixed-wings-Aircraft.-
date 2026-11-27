#!/bin/bash
# vsuite.sh <variant> <tag> [Name=value...] : reg + cork for a variant proxy
source /home/claude/scripts/penv.sh; penv $1; v=$1; tag=$2; shift 2
export AN_OUT=/home/claude/x/proxy/out_$v; mkdir -p $AN_OUT; cd $BENCH_DIR/an
rm -rf "$AN_OUT/reg/$tag" "$AN_OUT/cork/$tag"
python3 sreg.py "$tag" "$@" 2>&1 | grep -v Warn > "$AN_OUT/out_reg_$tag.txt"
STOCK=1 python3 cork.py "$tag" "$@" 2>&1 | grep -v Warn > "$AN_OUT/out_cork_$tag.txt"
echo done > "$AN_OUT/done_$tag"
