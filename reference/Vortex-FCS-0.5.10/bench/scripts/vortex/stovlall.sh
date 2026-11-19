#!/bin/bash
# Vortex 0.1.1 STOVL regression set: mission, VTO cases, hover at four weights, touchdown at four weights
cd /home/claude/vortex; o=/tmp/chk; mkdir -p $o
( timeout 1500 ./b.sh stovl ias=250 alt=300 T=150 tog=1,128 "thr=0:0.15,6:0.15,7:0,30:0,31:0.20,85:0.20,86:0.5,95:0.5,96:0.8" "roll=0:0,60:0,60.3:0.5,65:0.5,65.3:0" "yaw=0:0,70:0,70.3:1,73:1,73.3:0" "pitch=0:0,78:0,78.3:0.5,82:0.5,82.3:0" print=5 csv=$o/s3.csv > $o/s3.txt 2>&1 ) &
( timeout 900 ./b.sh stovl ground=1 ias=0 T=60 tog=0.5 "thr=0:0,5:0,5.5:0.20" "pitch=0:0,10:0,10.3:0.6,16:0.6,16.3:0,30:0,30.3:-0.6,48:-0.6,48.3:0" print=2 csv=$o/v1.csv > $o/v1.txt 2>&1 ) &
( timeout 900 ./b.sh stovl ground=1 ias=0 T=40 tog=0.5 "thr=0:0,6:0,6.3:1" "pitch=0:0,10:0,10.3:0.5,13:0.5,13.3:0" print=2 csv=$o/v2.csv > $o/v2.txt 2>&1 ) &
( ./hovcase.sh $o/h_s1288.csv mass=12800 > $o/h_s1288.txt 2>&1 ) &
( ./hovcase.sh $o/h_b15.csv mass=15000 ThrustScale=1.12 DragScale=0.885 > $o/h_b15.txt 2>&1 ) &
( ./hovcase.sh $o/h_b.csv ThrustScale=1.12 DragScale=0.885 > $o/h_b.txt 2>&1 ) &
( ./hovcase.sh $o/h_s.csv > $o/h_s.txt 2>&1 ) &
wait
( ./vlcase.sh $o/vl_b1188.csv mass=11880 ThrustScale=1.12 DragScale=0.885 StovlSinkRate=12 > $o/vl_b1188.txt 2>&1 ) &
( ./vlcase.sh $o/vl_b135.csv mass=13500 ThrustScale=1.12 DragScale=0.885 StovlSinkRate=12 > $o/vl_b135.txt 2>&1 ) &
( ./vlcase.sh $o/vl_b15.csv mass=15000 ThrustScale=1.12 DragScale=0.885 StovlSinkRate=12 > $o/vl_b15.txt 2>&1 ) &
( ./vlcase.sh $o/vl_s1188.csv mass=11880 StovlSinkRate=12 > $o/vl_s1188.txt 2>&1 ) &
( ./vlcase.sh $o/vl_sdef.csv mass=11880 > $o/vl_sdef.txt 2>&1 ) &
wait
