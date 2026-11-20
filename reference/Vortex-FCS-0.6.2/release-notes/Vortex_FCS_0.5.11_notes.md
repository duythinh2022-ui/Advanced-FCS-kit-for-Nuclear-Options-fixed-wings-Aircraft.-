# Vortex FCS 0.5.11: no per-frame type scan without the Detents mod

19 Nov 2026. On 0.5.10.

**Status: compiled, IL-checked against 0.5.10. Not flown, not benched (no law change). Needs the tester's confirmation.**

## Report

Tester (tristian, Discord, 19 Nov): "major frame drops from 360fps to 10fps with the fs 20 fcs", single-player mission. His logs: `C:\Games\NO mods\Vortex perf log report`.

## Cause

- His LogOutput.log has 128 of these lines (Player.log has 135):
  `[Warning: HarmonyX] AccessTools.TypeByName: Could not find type named NuclearOptionDetents.Compatibility.RuntimeCompatibility`
  - One appears at load. The other ~127 appear once he is in the FS-20.
  - He doesn't have baanish's Detents mod installed.
- `ThrottleDetent.TryHookDetents` (since 0.2.1) found the Detents type with `AccessTools.TypeByName`.
  - When the type is missing, HarmonyX scans every type of every loaded assembly twice: the game, Unity and ~40 mods here. Then it writes a log line.
  - A miss also left `detentsHookDone` false.
- `Run()` is the postfix on `PilotPlayerState.PlayerThrottleAxis1Controls`, so it runs every player input frame.
  - It retried whenever `!detentsHookDone`, before any aircraft or STOVL test.
  - So without the Detents mod, every frame in **any** jet repeated the full scan.
- The cost of one scan was not measured. 128 misses at ~10 fps ≈ 13 s in the jet, which fits the report.
- Never seen on Tom's machine: with Detents installed the lookup succeeds the first time and stops.

## Change (ThrottleDetent.cs only, plus the version string)

- New `FindDetentsCompat()`: asks each loaded assembly for the type by its full name with `Assembly.GetType(name, false)`. That is a per-assembly name lookup, with no type scan and no log line.
- `TryHookDetents(bool final)`:
  - `Install()` passes `false`. If Detents isn't loaded yet (it can load after this plugin), the call returns and the hook can be tried again later.
  - `Run()` passes `true`. Run only fires in a cockpit, long after the chainloader has finished, so a miss there is final: `detentsHookDone = true`, plus one log line, "Vortex FCS: Detents mod not loaded; its STOVL suspend is not needed."
  - Without Detents the lookup now runs twice per session in total.
- With Detents installed, nothing changes: the same type is found and the same getter is patched.
- No law, input or HUD change.

## Checks

- My rebuild of the 0.5.10 source gives method IL identical to the shipped 0.5.10 DLL: 1526 methods, the same sizes and bytes, and the same embedded `Vortex_FcsCore.dll`. So the toolchain matches.
- 0.5.11 against that rebuild:
  - Only `ThrottleDetent.Install` / `Run` / `TryHookDetents` change size (+1 / +1 / +19 bytes).
  - One method is new: `FindDetentsCompat`.
  - The closure class was renumbered.
  - The other same-size byte differences are metadata tokens shifted by the new method and string.
- Not checked:
  - In game, with or without Detents.
  - The fps on the tester's machine. Expected: at most one "Could not find type" line at load, then none.
- Worth checking in your own log, with Detents installed: "the Detents mod's idle / afterburner detents stand down while the STOVL laws fly" should still appear.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.11 and `meta.json`. 0.5.10 kept as `Vortex_FS20_FCS.dll.0.5.10.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.11-release`.
- No config migration.
