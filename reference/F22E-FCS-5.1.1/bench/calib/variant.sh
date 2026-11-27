#!/bin/bash
# variant.sh <bench dir> <data json> <out tag> <log glob prefix> [Name=value ...]  -> runs eqerr on all logs, prints summary
B="$1"; DATA="$2"; TAG="$3"; PFX="$4"; shift 4
export BENCH_DATA="$DATA"
mkdir -p /home/claude/work/eq/$TAG
for f in "/mnt/user-data/uploads/Nuclear Option/BepInEx/${PFX}"*.csv; do
  b=$(basename "$f" .csv)
  dotnet "$B/bin/bench.dll" eqerr "log=$f" out=/home/claude/work/eq/$TAG/$b.csv "$@" 2>/dev/null
done
python3 -I /home/claude/work/tools/eqcmp.py /home/claude/work/eq/$TAG/*.csv "${EQOPTS[@]}" 2>&1
