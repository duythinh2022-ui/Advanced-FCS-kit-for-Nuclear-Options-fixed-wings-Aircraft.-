#!/bin/bash
# dmg.sh <tree> <tag> [Name=value...]: full pulls with flaperon_R + rollervon_R + elevator_R detached, 400/500/615/700 km/h
tree=$1; tag=$2; shift 2; O=/home/claude/tern/cal/dmg; mkdir -p $O
cd /home/claude/tern/$tree; export BENCH_DATA=$PWD/bench/extract/ternion_airframe.json BENCH_DIR=$PWD/bench BENCH_DMG=$PWD/bench/data/ternion_damage.json
for ias in 400 500 615 700; do
  dotnet bench/bin/bench.dll ifly ias=$ias alt=2000 thr=1 T=8 pitch=0:0,1:0,1.02:1 roll=0:0 detach=flaperon_R+rollervon_R+elevator_R fcslog=$O/${tag}_$ias.csv "$@" >/dev/null 2>&1 &
done; wait
python3 - "$O" "$tag" <<'PY'
import sys,pandas as pd
O,tag=sys.argv[1:3]
for ias in [400,500,615,700]:
    d=pd.read_csv(f"{O}/{tag}_{ias}.csv"); d=d[d.t>1]
    print(f"{tag} {ias}: AoA max {d.alpha.max():6.1f}  |beta| max {d.beta.abs().max():5.1f}  cap min {d.asymCapEff.min():5.1f} mean nz {d.nz.mean():4.1f}  phi end {d.phi.iloc[-1]:+6.1f}")
PY
