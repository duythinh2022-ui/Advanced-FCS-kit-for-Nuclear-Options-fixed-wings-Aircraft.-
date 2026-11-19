#!/bin/bash
# base 3.1.9 and ported 3.2.0, full suite, sequential to keep timing honest on 2 cores
export BENCH_OUT=/home/claude/w/acc/suite_base BENCH_DATA=/home/claude/w/base/bench/data/airframe_cal.json
bash /home/claude/w/base/bench/an/suite.sh base > /home/claude/w/acc/suite_base.log 2>&1
export BENCH_OUT=/home/claude/w/acc/suite_P BENCH_DATA=/home/claude/w/f22/bench/data/airframe_cal.json
bash /home/claude/w/f22/bench/an/suite.sh P > /home/claude/w/acc/suite_P.log 2>&1
