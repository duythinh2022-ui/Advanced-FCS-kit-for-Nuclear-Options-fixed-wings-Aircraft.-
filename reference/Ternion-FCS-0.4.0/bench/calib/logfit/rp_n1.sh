#!/bin/bash
# rp_n1.sh <tree> <tag> [Name=value...]: replays of the 11 Nov 09:38 log windows with the fitted store layout
tree=$1; tag=$2; shift 2; O=/home/claude/tern/cal/rp; mkdir -p $O
L=/home/claude/tern/logs/Ternion_FCS_20261111_093832.csv
ST="Stores=wing_2a_R:2500;fuselage_F:2440;P_Trisurface1:5560;fuselage_L:393;wing_1b_L:207;fuselage_R:-393;wing_1b_R:-207"
cd /home/claude/tern/$tree
for w in "118 134" "156 182" "236 260" "82 116" "182 236"; do set -- $w; 
  bash bench.sh replay $L $1 $2 $O/n1_${tag}_$1.csv thr=1 FuelFrac=0.5 MassTarget=0 "$ST" "${@:3}" > /dev/null 2>&1 &
done; wait
