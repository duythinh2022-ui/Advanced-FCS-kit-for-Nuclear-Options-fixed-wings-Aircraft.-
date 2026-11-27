#!/bin/bash
# 0.5.0 matrix: six jets, the vane box off (must equal the 0.4.11 build) against on -> /tmp/b50/<jet>_{base,off,on}
cd /home/claude/vortex
BUF="ThrustScale=1.12 DragScale=0.885"; BASE=${BASE:-/home/claude/work/src0411/vortex/bench/bin}
r(){ n=$1; dt=$2; shift 2; DT=$dt ./b4.sh /tmp/b50/${n}_on "$@" & DT=$dt ./b4.sh /tmp/b50/${n}_off "$@" StovlVanes=false & wait; DT=$dt BENCH_BIN=$BASE ./b4.sh /tmp/b50/${n}_base "$@"; echo "$n done $(date +%T)"; }
for j in ${JETS:-bufref stock buflight stk128 buf136 bufasym}; do case $j in
 bufref) r bufref 115 mass=11880 $BUF;;
 stock) r stock 175 mass=11880;;
 buflight) r buflight 60 FuelFrac=0.2 $BUF;;
 stk128) r stk128 200 mass=12800;;
 buf136) r buf136 150 mass=13640 $BUF;;
 bufasym) r bufasym 130 mass=11880 $BUF StoreKg=500 StorePart=wing2a_L;;
esac; done
echo ALLDONE
