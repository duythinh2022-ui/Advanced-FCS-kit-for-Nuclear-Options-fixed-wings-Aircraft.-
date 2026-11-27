#!/usr/bin/env python3
# cmpn.py <thr> <all.txt A> <all.txt B> ... : suite summaries side by side, row by row (same line order); rows where any
# number differs from A by more than thr (abs) are printed. Labels = the text before the first numeric column.
import sys, re
thr = float(sys.argv[1]); P = sys.argv[2:]
def rows(p): return [l.rstrip('\n') for l in open(p, errors='replace') if l.strip() and not l.startswith('==')]
R = [rows(p) for p in P]
num = lambda l: [float(x) for x in re.findall(r'(?<![\w.])-?\d+\.?\d*(?![\w.])|nan', l.replace('nan', ' nan '))] if l else []
n = min(len(r) for r in R)
if len(set(len(r) for r in R)) > 1: print('row counts', [len(r) for r in R])
for i in range(n):
    ls = [r[i] for r in R]; ns = [num(l) for l in ls]
    if len(set(len(x) for x in ns)) > 1: print('?? ' + ' || '.join(ls)); continue
    if not ns[0]: continue
    d = max((abs(a - b) for x in ns[1:] for a, b in zip(ns[0], x) if a == a and b == b), default=0)
    if d > thr:
        for j, l in enumerate(ls): print(('%d ' % j) + l[-118:])
        print()
