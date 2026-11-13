# Su-30VXN Gandiva FCS 0.4.1: one DLL (the native core inside the plugin)

Base: 0.4.0. **No control-law change, no arithmetic change.** Tom, 11 Nov 11:49: "put them together into 1 dll for each jet".

**Changelog:** The native core (Su30_FcsCore.dll) now ships inside the plugin DLL: one DLL to install. On first load it is written to `BepInEx\cache\Su30_FcsCore\<checksum>\` and loaded from there.

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,465,856 bytes; the core embedded is the exact `Su30_FcsCore.dll` of 0.4.0, the one checked on the game's Mono).
- Bench (.NET 8), full suite against 0.3.10: 87 / 87 per-case files and 4 / 4 tables identical (timing columns aside); 404,590 native solves, 867,024 kernel calls, 0 left to C#.
- Game's Mono under Wine (`-O=-float32`), the release DLL alone in a `BepInEx\plugins\Su30_VXN.FCS` folder: the core written to `BepInEx\cache`, loaded, ABI / sizes OK, kernel self-test 200 / 200; second start reuses the cached file without rewriting it; a corrupted cached file is detected and replaced (tested on the Ifrit); outside a BepInEx folder it uses the temp folder (tested on the Su-30); the same loader code in all five. All five FCS plugins (F-22E 4.1.4, Revoker 1.1.1, Compass 0.3.1, Su-30 0.4.1, Ifrit 1.1.1) loaded in one process: each bound its own core.
- **Not flown.**

## How it works
- The build embeds `native/prebuilt/Su30_FcsCore.dll` as a resource of the plugin DLL (`build.sh`; `native/build.sh` rebuilds the crate, then copy the result to `native/prebuilt/`).
- At the first native call `FcsNative.Load` reads the resource, takes the first 16 hex digits of its SHA-256 as the folder name, and writes `BepInEx\cache\Su30_FcsCore\<hash>\Su30_FcsCore.dll` (through a temporary file) unless the same bytes are already there; it then loads that file by full path, as before. The file keeps the name `Su30_FcsCore.dll`, which is what the plugin's imports bind to.
- A new version has a new hash, so a new folder: an older core is never overwritten while another game instance uses it. Old folders can be deleted any time (the next start writes what it needs).
- Fallback: if the cache cannot be written, a loose `Su30_FcsCore.dll` beside the plugin is used if one is there; otherwise the C# code runs (one warning in the log), as before.

## Install
- **Main:** `plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.4.1 + meta.json. 0.4.0 kept as `Su30_VXN_FCS.dll.0.4.0.bak`.
- **The loose `Su30_FcsCore.dll` beside the plugin is no longer needed.** I can't delete files from here: delete `plugins\Su30_VXN.FCS\Su30_FcsCore.dll` yourself (it does no harm if left: the embedded core is used first).
- **Dev:** unchanged.
- **Release:** `C:\Games\NO mods\Su30-FCS-0.4.1-release`. The mod zip holds the plugin DLL and meta.json only.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.

Previous notes: `claude/Su30_FCS_0.4.0_notes.md`.
