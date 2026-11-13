# F-22E FCS 3.0.0: 2.44.1 renumbered

Base: 2.44.1. Compiled against the game's assemblies, **not flown**. 2.44.1 kept as `.dll.2.44.1.bak`. CSV unchanged.

## Tom (4 Nov, 21:58)
"bump to 3.0.0"

## Change
- Version strings only: the FCS plugin is 3.0.0 (it was 2.44.1), and the Dev DLL is 3.0.0 (it was 2.44.1).
- The source diff against 2.44.1 is those two lines. The control law, settings and CSV are exactly 2.44.1's.

## What 3.0.0 is
The 2.x line ended with the device replacements and the cleanup:
- **2.41.0:** the allocator's reach pass (flown 11:50).
- **2.43.0:** the stab rate bound replaced by the two-horizon reach model.
- **2.44.0:** full aft stick at the AoA limit holds zero rate once only a nose-down rate could hold the limit.
- **2.44.1:** 19 always-off bench switches and the dead `ReleasedStickStabRateBound` setting removed, bench-identical to 2.44.0. It also
  drops that setting's line from the Dev file once, on first load.

Device tally: 13 replaced by the reference capacity governor, 3 by the push plan, 1 by the reach model.

## Bench
- Not re-run. The bench does not compile the plugin entry file that holds the version, so its binary is the 2.44.1 one.
- 2.44.1's result therefore stands: hybrid (61 released + 14 held), 204 free replays and the suite all match 2.44.0 exactly.

## Not verified
- Not flown (nor has 2.44.0's law been).
