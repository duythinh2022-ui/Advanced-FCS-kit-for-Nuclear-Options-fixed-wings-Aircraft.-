# F-22E Strike Raptor FCS 4.1.4: one DLL (the native core inside the plugin) + the first-tick load fix

Base: 4.1.3. **No control-law change, no arithmetic change.** Tom, 11 Nov 11:49: "put them together into 1 dll for each jet".

**Changelog:** The native core (F22E_FcsCore.dll) now ships inside the plugin DLL: one DLL to install. On first load it is written to `BepInEx\cache\F22E_FcsCore\<checksum>\` and loaded from there; also a first-tick fix: the surface tables no longer fall back to C# for one tick while the core's self-test runs.

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,667,072 bytes; the core embedded is the exact `F22E_FcsCore.dll` of 4.1.3, 4.1.0's build).
- Bench (.NET 8), full suite against 4.1.3: 119 / 119 per-case files and 5 / 5 tables identical (timing columns aside); 427,582 native solves, 1,069,117 kernel calls, 0 left to C#.
- Game's Mono under Wine (`-O=-float32`), the release DLL alone in a `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS` folder: the core written to `BepInEx\cache`, loaded, ABI / sizes OK, kernel self-test 200 / 200; second start reuses the cached file without rewriting it; a corrupted cached file is detected and replaced (tested on the Ifrit); outside a BepInEx folder it uses the temp folder (tested on the Su-30); the same loader code in all five. All five FCS plugins (F-22E 4.1.4, Revoker 1.1.1, Compass 0.3.1, Su-30 0.4.1, Ifrit 1.1.1) loaded in one process: each bound its own core.
- **Not flown.**

## How it works
- The build embeds `native/prebuilt/F22E_FcsCore.dll` as a resource of the plugin DLL (`build.sh`; `native/build.sh` rebuilds the crate, then copy the result to `native/prebuilt/`).
- At the first native call `FcsNative.Load` reads the resource, takes the first 16 hex digits of its SHA-256 as the folder name, and writes `BepInEx\cache\F22E_FcsCore\<hash>\F22E_FcsCore.dll` (through a temporary file) unless the same bytes are already there; it then loads that file by full path, as before. The file keeps the name `F22E_FcsCore.dll`, which is what the plugin's imports bind to.
- A new version has a new hash, so a new folder: an older core is never overwritten while another game instance uses it. Old folders can be deleted any time (the next start writes what it needs).
- Fallback: if the cache cannot be written, a loose `F22E_FcsCore.dll` beside the plugin is used if one is there; otherwise the C# code runs (one warning in the log), as before.

**Also in 4.1.4: the first-tick load-order fix** (found in the other jets' port, 11 Nov): `FcsNative.Load` published the solve as ready before the kernels' self-test had run, so a table thread arriving during the self-test saw the kernels "not tested yet" and ran that tick's table in C#. Same bits on the bench, one tick of Mono arithmetic in the game. "Ready" is now published after the kernel test. On the bench this is the only visible difference: the kernel-call total over the suite is 1,069,117 against 4.1.3's 1,068,849 (those first-tick tables now run native), with every output identical.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.1.4. 4.1.3 kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.1.3.bak`.
- **The loose `F22E_FcsCore.dll` beside the plugin is no longer needed.** I can't delete files from here: delete `plugins\Aryx_F22E_StrikeRaptor.FCS\F22E_FcsCore.dll` yourself (it does no harm if left: the embedded core is used first).
- **Dev:** unchanged.
- **Release:** `C:\Games\NO mods\F22E-FCS-4.1.4-release`. The mod zip holds the plugin DLL and meta.json only.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.

Previous notes: `claude/F22E_NO_FCS_4.1.3_notes.md`.
