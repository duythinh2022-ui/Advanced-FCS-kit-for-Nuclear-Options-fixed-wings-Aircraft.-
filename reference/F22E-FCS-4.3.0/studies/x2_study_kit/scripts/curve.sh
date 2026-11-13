#!/bin/bash
# $1 = name, rest = sets
n=$1; shift
echo -n "$n: "
for d in 10 0 -5 -10 -15 -20 -25 -30; do dotnet bin/bench.dll parts "$@" alpha=36 tas=85 rho=1.2 hl=1 Elevator_L=$d | grep "^Elevator_L" | awk -F, '{printf "%.0f:%.3f ", -$2, -$3}'; done
for d in 5 -5 -15 -25; do dotnet bin/bench.dll parts "$@" alpha=62 tas=85 rho=1.2 hl=1 Elevator_L=$d | grep "^Elevator_L" | awk -F, '{printf "%.0f:%.3f ", -$2, -$3}'; done; echo
