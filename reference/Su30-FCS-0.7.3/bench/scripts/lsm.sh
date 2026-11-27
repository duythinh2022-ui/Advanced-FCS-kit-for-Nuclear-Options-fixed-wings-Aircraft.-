#!/bin/bash
# lsm.sh tag [sets]: ls8 at six start offsets
tag=$1; shift
for o in -0.2 -0.1 0 0.1 0.15 0.2; do OFF=$o ./ls8.sh ${tag}_o$o "$@"; done
