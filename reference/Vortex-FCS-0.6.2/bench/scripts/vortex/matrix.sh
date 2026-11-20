#!/bin/bash
cd /home/claude/vortex
OFF=$(cat /tmp/OFF.txt); BUF="ThrustScale=1.12 DragScale=0.885"
r(){ n=$1; dt=$2; shift 2; DT=$dt ./b4.sh /tmp/b4/${n}_off "$@" $OFF; DT=$dt ./b4.sh /tmp/b4/${n}_on "$@"; echo "$n done $(date +%T)"; }
r bufref 115 mass=11880 $BUF
r stock 175 mass=11880
r buflight 60 FuelFrac=0.2 $BUF
r stk128 200 mass=12800
r buf136 150 mass=13640 $BUF
r bufasym 130 mass=11880 $BUF StoreKg=500 StorePart=wing2a_L
( BENCH_OFF=1 ./regr2.sh off $OFF ) > /tmp/regr_off.txt 2>&1; ./regr2.sh on > /tmp/regr_on.txt 2>&1
echo ALLDONE
