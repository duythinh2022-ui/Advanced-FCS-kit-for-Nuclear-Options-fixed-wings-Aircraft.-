#!/bin/bash
# vrun.sh <tag> [args]: the set at Tom's config (b: buffed 11.04 t) and stock 11.9 t (s), one summary line each
tag=$1; shift
/home/claude/vortex/tools/vsset.sh ${tag}b mass=11040 ThrustScale=1.12 DragScale=0.885 "$@" >/dev/null 2>&1
/home/claude/vortex/tools/vsset.sh ${tag}s "$@" >/dev/null 2>&1
python3 /home/claude/vortex/tools/vsum.py ${tag}b ${tag}s
