#!/bin/bash
# gchk.sh <variant> [Name=value...]: full aft pull 4 s at 700/900 km/h, CTOL and MPO; prints nzMax and the bench's structure line
source /home/claude/scripts/penv.sh; penv $1; v=$1; shift; cd $BENCH_DIR
for ias in 700 900; do for m in 0 1; do
  r=$(BENCH_MPO=$m dotnet bin/bench.dll ifly ias=$ias alt=1500 thr=1 T=4 pitch=0:0,0.05:1 roll=0:0 "$@" 2>&1)
  echo "$v ias $ias mpo $m | $(echo "$r" | grep -o 'nzMax [0-9.]*') | $(echo "$r" | grep -o 'alphaMax [0-9.]*') | $(echo "$r" | grep 'struct load' | sed 's/.*max: //')"
done; done
