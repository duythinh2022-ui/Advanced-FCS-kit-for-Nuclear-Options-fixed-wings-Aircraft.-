# loadrev_cmp.py <dirA> <dirB> : run by run, B against A (loadrev.sh outputs): better / same / worse counts for the roll-spam metrics
# and the with-the-load / against-the-load split of the single reversals.
import sys, io, glob, os, runpy, contextlib
import numpy as np
A, B = sys.argv[1], sys.argv[2]
sys.argv = ["loadrev.py", A]
with contextlib.redirect_stdout(io.StringIO()):
    g = runpy.run_path(os.path.join(os.path.dirname(os.path.abspath(__file__)), "loadrev.py"))
spam, single = g["spam"], g["single"]
labs = ("step at a zero crossing", "sideslip max", "sideslip 95th pct", "seconds above 5 deg", "roll behind cmd, max", "roll behind cmd, rms", "governor retreats", "bank travelled", "AoA above the cap")
hib = (False, False, False, False, False, False, False, True, False); tol = (2.0, 0.2, 0.2, 0.05, 1.0, 0.3, 0.5, 3.0, 0.3)
ra = {os.path.basename(f): spam(f) for f in sorted(glob.glob(A+"/spam_*.csv"))}; rb = {os.path.basename(f): spam(f) for f in sorted(glob.glob(B+"/spam_*.csv"))}
print("ROLL SPAM, %d runs: B against A, better / same / worse (tolerance)" % len(ra))
for j, lab in enumerate(labs):
    b = s = w = 0
    for k in ra:
        d = (rb[k][j]-ra[k][j]) * (-1 if hib[j] else 1)
        if d < -tol[j]: b += 1
        elif d > tol[j]: w += 1
        else: s += 1
    print("  %-26s %2d / %2d / %2d   (%.2g)" % (lab, b, s, w, tol[j]))
