# Revoker FCS 0.4.3: FPS loss with the F-22E FCS loaded

Base: 0.4.2. One fix; flight behaviour unchanged.

## Cause
The F-22E and the Revoker each add their own lift/drag curves to the game's one shared aero chart table, in a prefix on `AeroJobSettings.Schedule` (every physics tick). Each prefix took **any** change in the table's length to mean "the game rebuilt the table" and re-added its charts. With both mods in a mission, each one's append looked like a rebuild to the other, so they re-added after each other on every tick, without end:
- the table grew by 10 charts (8 F-22E + 2 Revoker) per tick, about 500 per second at 50 Hz, plus copying the whole, growing table on each append. That is the FPS that slides from spawn onwards.
- the log showed only 8 rounds because that message is capped at 8; the loop kept going silently after it.
- one mod alone, or the Ifrit (which does not hook `Schedule`), never triggers it. Tom's bisect (8 runs, 2 Nov) matched this exactly: FPS fell only with the F-22E FCS and the Revoker FCS loaded together and an F-22E spawned; switching off the Revoker's `ElevonStallStagger` (no Revoker charts) removed it.

## Fix (`StallStaggerJobs.cs` only)
- The plugin keeps a copy of every chart it wrote. When the table length changes, it checks its own charts: still at their ids with exactly the values it wrote → another plugin appended, keep them (logged up to 4 times as `... table grew N -> M (another plugin's charts); ours are intact, not re-added.`). Missing or changed → a real rebuild (new mission), re-add as before.
- The check runs only when the length changes (spawns), never per tick in steady flight.
- Nothing about the curves, the airframe or the control laws changed.

## Check
- Compiled against the game's 0.34.6 assemblies, no new warnings. The unmodified source builds to the same size as the shipped DLL; not byte-identical (compiler version / source order), so the build is not a byte-for-byte reproduction.
- Model of the prefix logic with two plugins sharing one table, 500 ticks: old rule 5024 charts and 500 re-adds each; new rule 34 charts, 1 add each; a table rebuild mid-run re-adds once. With only one of the two mods updated the loop also stops (old F-22E + new Revoker settles at 42 charts after one extra round).
- **Not run in game.** What to look for in `LogOutput.log` with both mods: one F-22E `aero charts` line and one Revoker line per mission, then `table grew ... ours are intact, not re-added`, and no further `aero charts` lines.
