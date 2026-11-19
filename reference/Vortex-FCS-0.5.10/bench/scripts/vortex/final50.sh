#!/bin/bash
# 0.5.0 final bench run on one build (no rebuild while it runs): Tom's-jet vane set, stop set, six-jet matrix, STOVL regression set, CTOL identity
cd /home/claude/vortex; BASE=/home/claude/work/src0411/vortex/bench/bin; L=/tmp/final50.log; : > $L
rm -rf /tmp/vn/base /tmp/vn/fin /tmp/st/base /tmp/st/fin /tmp/st/finj /tmp/st/off /tmp/b50 /tmp/ct; mkdir -p /tmp/b50 /tmp/ct
BENCH_BIN=$BASE ./vn.sh base >/dev/null 2>&1; ./vn.sh fin >/dev/null 2>&1; echo "vn done $(date +%T)" >> $L
BENCH_BIN=$BASE ./st.sh base >/dev/null 2>&1; ./st.sh fin >/dev/null 2>&1; ./st.sh finj StovlVaneStopJerk=true >/dev/null 2>&1; ./st.sh off StovlVanes=false >/dev/null 2>&1; echo "st done $(date +%T)" >> $L
./matrix50.sh > /tmp/b50/matrix.log 2>&1; echo "matrix done $(date +%T)" >> $L
BENCH_BIN=$BASE ./regr2.sh r50base > /tmp/regr_base.txt 2>&1; ./regr2.sh r50off StovlVanes=false > /tmp/regr_off.txt 2>&1; ./regr2.sh r50on > /tmp/regr_on.txt 2>&1; echo "regr done $(date +%T)" >> $L
# CTOL laws (not touched): pitch and roll steps, a full-aft pull and an airbrake case on both builds, every shared column
for b in base fin; do
  if [ $b = base ]; then export BENCH_BIN=$BASE; else unset BENCH_BIN; fi
  for I in 400 700; do timeout 600 ./b.sh ifly ias=$I alt=3000 thr=1 T=6 "pitch=0:0,0.5:0,0.6:1,3:1,3.1:0" fcslog=/tmp/ct/${b}_ps$I.csv >/dev/null 2>&1; done
  for I in 300 500 800; do timeout 600 ./b.sh ifly ias=$I alt=3000 thr=1 T=5 "roll=0:0,0.5:0,0.6:1,2.5:1,2.6:0" fcslog=/tmp/ct/${b}_rs$I.csv >/dev/null 2>&1; done
  timeout 600 ./b.sh ifly ias=600 alt=3000 thr=0.6 T=8 "pitch=0:0,1:0,1.1:1,5:1,5.1:0" "roll=0:0,2:0,2.1:1,4:1,4.1:0" fcslog=/tmp/ct/${b}_pr600.csv >/dev/null 2>&1
done; unset BENCH_BIN
echo "ctol done $(date +%T)" >> $L; echo ALLDONE >> $L
