#!/bin/bash
# rs.sh <tag> [Name=value...] : reg + sw only
R=$(cd "$(dirname "$0")"; pwd); B=$R/bench; export BENCH_DIR=$B BENCH_DATA=$B/extract/ifrit_airframe.json BENCH_DMG=$B/data/ifrit_damage.json AN_OUT=$B/out
tag=$1; shift; cd $B/an; rm -rf "$AN_OUT/reg/$tag" "$AN_OUT/sw/$tag"
python3 sreg.py "$tag" "$@" 2>&1 | grep -v Warn > "$AN_OUT/out_reg_$tag.txt"
python3 ssweep.py "$tag" "$@" 2>&1 | grep -v Warn > "$AN_OUT/out_sw_$tag.txt"
