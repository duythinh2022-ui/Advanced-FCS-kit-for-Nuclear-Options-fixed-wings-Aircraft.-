# T/A-30 Compass FCS 0.3.1: one DLL (the native core inside the plugin)

Base: 0.3.0. **No control-law change, no arithmetic change.** Tom, 11 Nov 11:49: "put them together into 1 dll for each jet".

**Changelog:** The native core (Compass_FcsCore.dll) now ships inside the plugin DLL: one DLL to install. On first load it is written to `BepInEx\cache\Compass_FcsCore\<checksum>\` and loaded from there.

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,321,472 bytes; the core embedded is the exact `Compass_FcsCore.dll` of 0.3.0, the one checked on the game's Mono).
- Bench (.NET 8), full suite against 0.2.1: 125 / 125 per-case files and 4 / 4 tables identical (timing columns aside); 503,890 native solves, 824,282 kernel calls, 0 left to C#.
- Game's Mono under Wine (`-O=-float32`), the release DLL alone in a `BepInEx\plugins\Compass_TA30.FCS` folder: the core written to `BepInEx\cache`, loaded, ABI / sizes OK, kernel self-test 200 / 200; second start reuses the cached file without rewriting it; a corrupted cached file is detected and replaced (tested on the Ifrit); outside a BepInEx folder it uses the temp folder (tested on the Su-30); the same loader code in all five. All five FCS plugins (F-22E 4.1.4, Revoker 1.1.1, Compass 0.3.1, Su-30 0.4.1, Ifrit 1.1.1) loaded in one process: each bound its own core.
- **Not flown.**

## How it works
- The build embeds `native/prebuilt/Compass_FcsCore.dll` as a resource of the plugin DLL (`build.sh`; `native/build.sh` rebuilds the crate, then copy the result to `native/prebuilt/`).
- At the first native call `FcsNative.Load` reads the resource, takes the first 16 hex digits of its SHA-256 as the folder name, and writes `BepInEx\cache\Compass_FcsCore\<hash>\Compass_FcsCore.dll` (through a temporary file) unless the same bytes are already there; it then loads that file by full path, as before. The file keeps the name `Compass_FcsCore.dll`, which is what the plugin's imports bind to.
- A new version has a new hash, so a new folder: an older core is never overwritten while another game instance uses it. Old folders can be deleted any time (the next start writes what it needs).
- Fallback: if the cache cannot be written, a loose `Compass_FcsCore.dll` beside the plugin is used if one is there; otherwise the C# code runs (one warning in the log), as before.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` 0.3.1 + meta.json. 0.3.0 kept as `Compass_TA30_FCS.dll.0.3.0.bak`.
- **The loose `Compass_FcsCore.dll` beside the plugin is no longer needed.** I can't delete files from here: delete `plugins\Compass_TA30.FCS\Compass_FcsCore.dll` yourself (it does no harm if left: the embedded core is used first).
- **Dev:** unchanged.
- **Release:** `C:\Games\NO mods\Compass-FCS-0.3.1-release`. The mod zip holds the plugin DLL and meta.json only.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.

Previous notes: `claude/Compass_FCS_0.3.0_notes.md`.
