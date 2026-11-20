#!/bin/bash
# fore-aft stop experiment: st.sh <tag> [bench args] -> /tmp/st/<tag>/{k128,ref,stock,tom30,tom45}.csv
tag=$1; shift; o=/tmp/st/$tag; mkdir -p $o
B=/home/claude/vortex/b.sh; BUF="ThrustScale=1.12 DragScale=0.885"
C='stovl ground=1 ias=0 T=50 tog=0.5 thr=0:0,20:0,20.1:0.35,33:0.35,33.1:0.20 pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0 print=100'
( timeout 600 $B $C mass=12800 csv=$o/k128.csv "$@" > $o/k128.txt 2>&1 ) &
( timeout 600 $B $C mass=11880 $BUF csv=$o/ref.csv "$@" > $o/ref.txt 2>&1 ) &
wait
( timeout 600 $B $C mass=11880 csv=$o/stock.csv "$@" > $o/stock.txt 2>&1 ) &
( OUT=$o ./tom.sh tom30 T=48 "thr=0:0,20:0,20.1:0.5,32:0.5,32.1:0.20" "$@" ) &
wait
( OUT=$o ./tom.sh tom45 T=52 "thr=0:0,20:0,20.1:0.64,35:0.64,35.1:0.20" "$@" ) &
( timeout 600 $B $C mass=13640 $BUF csv=$o/b136.csv "$@" > $o/b136.txt 2>&1 ) &
wait
