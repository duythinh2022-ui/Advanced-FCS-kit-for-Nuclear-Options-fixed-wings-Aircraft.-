# Rate-saturation study (24 Nov 2026): Revoker 1.7.0 / F-22E 4.7.0

The scripts used for the notes of these two versions, as run. Paths inside them point at the machine they ran on
(`/home/claude/work`, logs under `/mnt/user-data/uploads/Nuclear Option/BepInEx`); edit the paths at the top before use.

| File | What it does |
|---|---|
| `rs1.py` | One line per FCS log window: mean surface travel (deg/s), share of samples at the servo rate limit, 0.1-10 Hz bands of the roll demand, rate cap and roll target. |
| `rpb.sh` | Revoker: replays the three loaded-roll windows of the 24 Nov 02:21 log (TVC off, the AoA cap as flown) and prints `rs1.py` for each. |
| `rhold.sh` | Revoker: six held loaded rolls on the bench (0.45 aft / full aft, TVC on / off). |
| `frpb.sh`, `frs.py` | F-22E: replays six loaded-roll windows of the 23 Nov 21:09 and 24 Nov 00:42 logs, bench against game. |
| `fhold.py` | F-22E: seven held loaded rolls on the bench. |
| `onefin.sh` | F-22E: the damage suite's one-fin held push-over at one entry speed (for the speed sweep). |
| `quick.sh`, `fquick.sh` | Sweep subset + regression set (Revoker), regression set + corkscrew entries (F-22E). |
| `curves.py` | Curve-level A/B over a suite's per-case logs: surface travel and reversals, roll-target retreats with the stick held, roll-rate dips, pitch-command hitches, roll onset. |
| `cmpn.py` | Suite summaries of several builds side by side, rows that moved. |
| `csvsame.py` | Column-by-column identity of two logs (used for the switch-off and worker-thread checks). |
| `sel_experiment.py` | The bench-only patch behind "how the split was chosen": two estimators (command-based and hold-based) and a per-consumer mask `HoldSel` (1 rate cap, 2 pace, 4 trade, 8 push governor). Applies to the first, all-consumers version of the change, not to the released source. |

Replays need the flight as flown: `DisableTVC=true NoTvcAoACapPos=<cap> NoTvcThrCapPos=<cap>` for the Revoker's 02:21 log. Replayed with TVC on, the bench reads far calmer than the game.
