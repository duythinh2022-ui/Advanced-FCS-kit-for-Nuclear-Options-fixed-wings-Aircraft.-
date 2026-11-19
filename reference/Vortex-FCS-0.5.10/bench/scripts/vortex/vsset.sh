#!/bin/bash
# Vortex STOVL height-hold test set (7 cases). usage: vsset.sh <tag> [Name=value ...]   (BENCH_BIN=dir picks the build)
# out: /tmp/lg/<case>_<tag>.csv
tag=$1; shift; o=/tmp/lg; mkdir -p $o; B=/home/claude/vortex/b.sh
P="pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0"
run(){ n=$1; shift; timeout 900 $B stovl ground=1 ias=0 tog=0.5 print=1 csv=$o/${n}_$tag.csv "$@" > $o/${n}_$tag.txt 2>&1; }
# dec: hover, lever to 30 kt at 25, back to the aft detent at 40
run dec T=70 "thr=0:0,5:0,5.5:0.20,25:0.20,25.3:0.5,40:0.5,40.3:0.20" "$P" "$@" &
# cb: full right stick + lever to 30 kt together for 8 s, both released
run cb T=70 "thr=0:0,5:0,5.5:0.20,25:0.20,25.3:0.5,33:0.5,33.3:0.20" "$P" "roll=0:0,25:0,25.3:1,33:1,33.3:0" "$@" &
wait
# ys: 00:14 log repro: lever 0.67 (~17 m/s), full right pedal 2.4 s, released, lever 0.44
run ys T=75 "thr=0:0,5:0,5.5:0.20,25:0.20,25.5:0.67,40:0.67,40.3:0.44" "$P" "yaw=0:0,35:0,35.2:1,37.6:1,37.8:0" "$@" &
# t30: 30 kt on the lever, 90 deg pedal turn, released
run t30 T=70 "thr=0:0,5:0,5.5:0.20,25:0.20,25.3:0.5" "$P" "yaw=0:0,40:0,40.2:1,42.2:1,42.4:0" "$@" &
wait
# bk: 00:24 log repro: lever 0.53, full left 3.2 s, released 2 s, full right 2 s, full left 2 s, released
run bk T=75 "thr=0:0,5:0,5.5:0.20,25:0.20,25.5:0.53" "$P" "roll=0:0,45:0,45.2:-1,48.2:-1,48.4:0,50.4:0,50.6:1,52.6:1,52.8:-1,54.8:-1,55:0" "$@" &
# hov: hover, pedal 3 s, crab 5 s, pedal + crab
run hov T=62 "thr=0:0,5:0,5.5:0.20" "$P" "yaw=0:0,20:0,20.2:1,23:1,23.2:0,45:0,45.2:-1,49:-1,49.2:0" "roll=0:0,32:0,32.2:1,37:1,37.2:0,45:0,45.2:1,50:1,50.2:0" "$@" &
wait
# sk4: hover, 15 m/s sideways kick at 25, stick released
run sk4 T=60 "thr=0:0,5:0,5.5:0.20" "$P" kick=25,15,0 "$@" &
wait
python3 /home/claude/vortex/tools/vsan.py $tag
