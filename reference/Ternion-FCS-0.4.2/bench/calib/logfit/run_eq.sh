#!/bin/bash
# run_eq.sh <tag> [Name=value...] : eqerr on the clean Ternion logs at 4 fuel levels -> cal/<tag>/<log>_f<frac>.csv
tag=$1; shift; L=/home/claude/tern/logs; O=/home/claude/tern/cal/$tag; mkdir -p $O
logs="20261108_225710 20261109_074611 20261109_075030 20261109_091100 20261109_092106 20261109_093823 20261109_094034 20261109_094126 20261109_095247 20261109_101114 20261110_020612"
for lg in $logs; do for f in 0.05 0.20 0.35 0.50; do
  m=$(python3 -c "print(19690+12000*$f)")
  echo "dotnet /home/claude/tern/w/bench/bin/bench.dll eqerr log=$L/Ternion_FCS_$lg.csv out=$O/${lg}_f$f.csv FuelFrac=$f MassTarget=$m $* 2>/dev/null"
done; done | xargs -P 4 -I{} bash -c "{}"
ls $O | wc -l
