#!/bin/bash
# kr.sh tag [sets...]: TVC-on push replays from the 14:55 and 16:16 logs
tag=$1; shift; mkdir -p rp/k_$tag
A="/mnt/user-data/uploads/Nuclear Option/BepInEx/Su30_FCS_20261110_145524.csv"
B="/mnt/user-data/uploads/Nuclear Option/BepInEx/Su30_FCS_20261110_161649.csv"
run(){ L=$1; t=$2; n=$3; shift 3; t0=$(python3 -c "print(round($t-0.6,2))"); t1=$(python3 -c "print(round($t+4.0,2))"); (cd src && bash bench.sh replay "$L" $t0 $t1 ../rp/k_$tag/${n}_$t.csv AoAPos=75 "$@" >/dev/null 2>&1); }
for t in 17.43 26.95 397.93 403.9 409.24 416.49 425.11 433.69 438.59 441.89 462.75 472.2; do run "$A" $t a "$@" & done; wait
for t in 28.02 141.98 153.64 183.42 230.41 240.97 291.24 297.24 334.66 1001.73 1033.98; do run "$B" $t b "$@" & done; wait
