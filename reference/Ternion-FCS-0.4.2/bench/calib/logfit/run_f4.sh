#!/bin/bash
# run_f4.sh <tag> [Name=value...]: eqerr on the 22:53 log 960.5-1024 s, TBM 2500 kg on wing_2a_R, fuel moved right->left S kg (4 bins)
tag=$1; shift; FF=${FF:-3050}; FR=${FR:-2810}; O=/home/claude/tern/cal/$tag; mkdir -p $O
cd /home/claude/tern/w; export BENCH_DATA=$PWD/bench/extract/ternion_airframe.json BENCH_DIR=$PWD/bench BENCH_DMG=$PWD/bench/data/ternion_damage.json
for S in 0 600 1200 1800; do
  a=$(python3 -c "print(round($S*2627.9/4016.5,1))"); b=$(python3 -c "print(round($S*1388.6/4016.5,1))")
  dotnet bench/bin/bench.dll eqerr log=/home/claude/tern/cal/f4.csv out=$O/f4_s$S.csv FuelFrac=0.47 MassTarget=0 "Stores=wing_2a_R:2500;fuselage_F:$FF;P_Trisurface1:$FR;fuselage_L:$a;wing_1b_L:$b;fuselage_R:-$a;wing_1b_R:-$b" "$@" 2>&1 | grep -o "cgB [^;]*" > $O/f4_s$S.cg &
done; wait; cat $O/f4_s*.cg
