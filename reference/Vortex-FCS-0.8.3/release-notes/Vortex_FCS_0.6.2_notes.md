# Vortex FCS 0.6.2: the native core is a reproducible, attested build

20 Nov 2026. On 0.6.1. **No control-law change.**

**Status: built; the embedded core checked against 0.6.1's core on Windows (every output byte identical). Not flown.**

## Why

NOMNOM review of PR #553 (kingwixly, 20 Nov): "Please return to C# and .NET dll. It took me 45+ mins to replicate your building setup and hash-match everything and it's still not perfectly verifiable."

- Tom wants to keep the Rust core.
- The 45 minutes came from the core's old build: Ubuntu's rustc 1.97.1 with `RUSTC_BOOTSTRAP=1`, `-Zbuild-std` and a patched copy of the std source (the Windows std couldn't be downloaded where it was built). Nobody else could reproduce that.

## What changed (repo: native/, .github/)

- **`native/fcs_core/rust-toolchain.toml`** pins Rust 1.97.0 and the `x86_64-pc-windows-gnu` target. rustup installs both automatically.
- **`native/build-repro.sh`** is a plain `cargo build --release --locked --target x86_64-pc-windows-gnu`. It remaps the build paths (`--remap-path-prefix`) and stops the linker writing a timestamp (`-Wl,--no-insert-timestamp`). The reference environment is `rust:1.97.0-bookworm` with Debian's mingw-w64 (GCC 12).
- **`.github/workflows/native-core.yml`** runs on every push:
  - builds the core twice, in different folders, and the two must match;
  - publishes a signed build-provenance attestation (Sigstore) for the DLL;
  - fails if the committed `native/prebuilt/Vortex_FcsCore.dll` is not that build.
- **The embedded core is now that build:**

  | | sha256 |
  |---|---|
  | new | `e20c7b999c26c63686ff9efc107d72725b5e886272fb4e10b6ee684b1feaa9b2` |
  | was | `858fef22c24fce69db10739240f900196c20aec9c75417d26df707116dbf0c12` |

- **The new core:**
  - is 918 KB (was 676 KB): rustup's prebuilt std instead of the one compiled from source;
  - has a PE timestamp of 0;
  - has the same seven exports;
  - has the same ABI number (2013001) and structure sizes.
- **`native/README.md`** has new "Build (reproducible)" and "Verify a shipped core" sections.

## Checks

- **CI:** two builds in different folders gave the same SHA-256. The attestation was created (`…/attestations/70348983` for the first run). The prebuilt check is green on `native-ci` (1042605).
- **Same results:**
  - The bench recorded the native calls of the full bench suite (regression, stop/reversal sweep, corkscrews, damage) and four STOVL cases (mission, two VTOs, the 15 t hover pedal/crab case).
  - `replay.exe`, run on Tom's PC on Windows, fed 35 recording files through both cores.
  - It replayed 32,281 allocator solves, 37,398 grid-kernel calls (16,749 table, 20,649 sweep) and 801 coupled-check calls.
  - **New vs old: 0 differences** in any output byte.
  - Both match the bench's Linux build of the core too. (The 20,649 "outM" lines in the log are sweep-mode calls, which don't write `outM`; the recorder had captured whatever was in that buffer.)
- **Plugin:**
  - Building from `main` (`9fb5402`) gives this DLL byte for byte.
  - Its IL is identical to 0.6.1's: 1,590 methods, the same sizes and bytes. Only the version string and the embedded core differ.
  - The game extracts the new core to a new cache folder: `BepInEx\cache\Vortex_FcsCore\e20c7b999c26c636\`.
- **Not checked:** in game. Expected: the log says the native allocator core is in use, loaded from that new cache folder.

## Still noisy (not changed)

The core's import table lists Windows functions from Rust's standard library, such as `CreateProcessW`, `CreateFileW` and `WS2_32.dll`. Nothing in the core calls them. Removing them means a `#![no_std]` core that never panics, which is a separate job.

## Verify a shipped core (for reviewers)

1. Extract `Vortex_FcsCore.dll` from `Vortex_FS20_FCS.dll` (ILSpy / dnSpy: Resources > Vortex_FcsCore.dll > Save), or take the copy the game writes to `BepInEx\cache\Vortex_FcsCore\<hash>\`.
2. Run:
   ```
   gh attestation verify Vortex_FcsCore.dll -R duythinh2022-ui/Advanced-Flight-Controls-System-for-FS-20-Vortex-Nuclear-Option
   ```

## For Tom

- **Branch cleanup:** delete the GitHub branches `tmp-core-dump` (a throwaway this session used to read the built DLL out of a CI log; it couldn't delete branches) and `native-ci` (merged).
- **Test folder:** `C:\Games\NO mods\core-equiv-check` (the equivalence test, about 600 MB) can be deleted.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.6.2 and `meta.json`. 0.6.1 is kept as `Vortex_FS20_FCS.dll.0.6.1.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.6.2-release`.
- No config migration.
