# 2.5.3 analysis scripts (31 Oct 2026)
Paths at the top of each script point at the chat's scratchpad; edit `S` / `B` to your checkout. `BENCH_DATA` = the calibrated airframe json (F22E_bench_airframe_2.4.18-cal.json).
- `ev.py <log.csv> [thr]` - every sideslip excursion above thr with state at onset / peak.
- `stops.py` - every roll stop / reversal in the logs listed at the bottom, with the fitted-model tau, rudder usage, trajectory brake.
- `dump.py <log.csv> t0 t1 [step]` - telemetry dump of a window.
- `sweep.py <tag> [Name=value ...]` - 32-case stop / reversal sweep (450-850 km/h x 4 pulls x stop/reverse); summary.csv per tag.
- `reg.py <tag> [Name=value ...]` - 13 regression scenarios (rolling pulls, reversals, 65 deg release, push + roll, 1 g reversal).
- `cmp.py "<replay csvs>"` - bench vs game peaks for `bench replay` outputs.
