# 3.2.0 (airframe X2) acceptance scripts, 9 Nov 2026
Paths at the top of each script are the cloud session's (/home/claude/w/{base,f22,study}/bench = pristine 3.1.9, 3.2.0, 3.1.9 + study patch).
- flt.py / sum.py: the handoff's flight set (loaded roll at 36, holds 20/30/45, push from 45) for cur / study X2 / 3.2.0 defaults.
- events.py -> events.json: planned push-over onsets (rising edge of ppOn) in the 5-7 Nov logs (139). push.py <tag> <bench>: free replays [t-0.5, t+3]; pushscore.py A B: curve metrics.
- ext.py: damage (one stab off, nozzles locked, both, fins off) and zero-AoA crossing cases + static moment through zero.
- ens.py / ens2.py: 5-member ensembles (entry IAS +-0.1 / 0.2 %) of the suite cases that moved.
- expt.py / attrib.py / x2b.py: bench experiments that were NOT shipped (StabCLScale, the X2b schedule, the protHold release, attribution of the roll-rate loss).
- fitsched2.py: the kit's fitsched.py with per-knot moment targets (QTAB="44:-5,48:-14.8" made X2b).
The study kit itself (bench patch, configs, results) is in ../x2_study_kit.
