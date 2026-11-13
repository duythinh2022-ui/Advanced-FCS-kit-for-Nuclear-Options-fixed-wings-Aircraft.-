# Push-over scoring (2.35.x / 2.36 dev)
Paths inside the scripts are the cloud session's (/home/claude/w/...): bench at /home/claude/w/src/F22E_FCS_2.31.1_src/bench,
logs at /home/claude/w/logs/F22E_FCS_20261103_HHMMSS.csv, outputs under /home/claude/w/score/id_<tag>/. Edit the three path lines
(R, the logs path, O) to run elsewhere.
- `idrun.py <tag> <bindir> [Name=value ...]` - pitch-pinned + free replays [t-0.5, t+3] of the 33 planned pushes of the 2.35.x
  flights (22:37, 22:52, 23:10, 23:29, 23:37 logs) + the 54-push set in hitch_all.csv: 174 runs, ~2.5 min on 2 cores. <bindir> is a
  copy of bench/bin (copy it, so a rebuild cannot change a run in progress).
- `idcmp.py <tagA> <tagB>` - byte / flown-column identity of two runs (behaviour-identical proofs).
- `s33.py <tagA> <tagB>` - metrics on the 33 2.35.x pushes, all / released / held: pinned command dip (cdip), free |beta| peak
  (fbeta), time to AoA < 10 (t10), free nose-down rate back-off (fqd - NOT trusted, no correlation with the game).
- `score.py id_<tagA> id_<tagB>` - the 54-push set (hitch_all.csv), same metric families.
