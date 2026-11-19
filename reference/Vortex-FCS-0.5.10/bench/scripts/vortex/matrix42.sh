#!/bin/bash
# 0.4.2 matrix on the 0.4.1 base: six jets, 0.4.1 (the four 0.4.2 switches off) against 0.4.2 -> /tmp/b42/<jet>_{off,on};
# the reference also on the 0.4.1 release build (BENCH_BIN) for the identity check; the STOVL regression set for both
cd /home/claude/vortex
OFF42=$(cat /tmp/OFF41.txt); BUF="ThrustScale=1.12 DragScale=0.885"
r(){ n=$1; dt=$2; shift 2; DT=$dt ./b4.sh /tmp/b42/${n}_off "$@" $OFF42; DT=$dt ./b4.sh /tmp/b42/${n}_on "$@"; echo "$n done $(date +%T)"; }
BENCH_BIN=/home/claude/vortex/bin_0401 DT=115 ./b4.sh /tmp/b42/bufref_0401 mass=11880 $BUF
r bufref 115 mass=11880 $BUF
r stock 175 mass=11880
r buflight 60 FuelFrac=0.2 $BUF
r stk128 200 mass=12800
r buf136 150 mass=13640 $BUF
r bufasym 130 mass=11880 $BUF StoreKg=500 StorePart=wing2a_L
./regr2.sh on42 > /tmp/regr_on42.txt 2>&1
BENCH_BIN=/home/claude/vortex/bin_0401 ./regr2.sh b0401 > /tmp/regr_b0401.txt 2>&1
echo ALLDONE
