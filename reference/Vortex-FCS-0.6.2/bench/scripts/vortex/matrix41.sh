#!/bin/bash
# 0.4.1 matrix: six jets on the shipped settings -> /tmp/b41/<jet>; the reference also with the 0.4.1 switches off (= 0.4.0)
# and with every switch off (= 0.3.15)
cd /home/claude/vortex
OFF40=$(cat /tmp/OFF.txt); OFF41=$(cat /tmp/OFF41.txt); BUF="ThrustScale=1.12 DragScale=0.885"
r(){ n=$1; dt=$2; shift 2; DT=$dt ./b4.sh /tmp/b41/$n "$@"; echo "$n done $(date +%T)"; }
r bufref 115 mass=11880 $BUF
r bufref_as040 115 mass=11880 $BUF $OFF41
r bufref_as0315 115 mass=11880 $BUF $OFF41 $OFF40
r stock 175 mass=11880
r buflight 60 FuelFrac=0.2 $BUF
r stk128 200 mass=12800
r buf136 150 mass=13640 $BUF
r bufasym 130 mass=11880 $BUF StoreKg=500 StorePart=wing2a_L
./regr2.sh on41 > /tmp/regr_on41.txt 2>&1
echo ALLDONE
