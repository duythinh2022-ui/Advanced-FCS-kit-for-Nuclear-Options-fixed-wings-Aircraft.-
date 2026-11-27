# FS-20 Vortex FCS 0.7.3 + FS-20 Vortex Family FCS 0.7.3 — notes (23 Nov 2026)

Asked (Tom, 23:21 / 23:24): with both installed, the family jets should fly on the Family FCS's settings; the Family FCS should use the
FS-20 Vortex FCS's native core when its own isn't installed. No control-law change.

## 1. Who flies what (hand-over rule 2)

With matching code, "the FS-20 Vortex FCS's code with the Family FCS's settings" is the Family FCS flying the jet, so rule 2 is simply:
the Family FCS flies A / C / T / NA whenever it is loaded, on Vortex_FS20.FCS.Family.cfg; the FS-20 Vortex FCS flies the B on
Vortex_FS20.FCS.cfg. Each side reads the other's FcsBuildInfo.HandOverRule (missing = 1 with a fingerprint, 0 without) and follows the
older rule against an older plugin:

| | OG <= 0.7.0 (rule 0) | OG 0.7.1-0.7.2 (rule 1) | OG 0.7.3+ (rule 2) |
|---|---|---|---|
| Family 0.7.1-0.7.2 (rule 1) | — | same code: OG, else Family | same code: OG, else Family |
| Family 0.7.3+ (rule 2) | OG (Family stands by) | same code: OG, else Family | **Family, own settings** |

Never both, never neither. The fingerprint and the 0.7.2 B-only markers stay (logged; they matter only against a 0.7.1-0.7.2 plugin).
Both logs say which way it went and why.

## 2. Family FCS native core (FcsNative.Load, Windows)

1. own resource (none in the Family DLL)
2. VortexFamily_FcsCore.dll beside the plugin (the optional download)
3. **new:** the FS-20 Vortex FCS's built-in Vortex_FcsCore.dll, read from its loaded assembly and written to
   BepInEx/cache/VortexFamily_FcsCore/<sha first 16 hex>/VortexFamily_FcsCore.dll — a separate file under the Family FCS's name, so the
   two plugins still load separate modules (same bytes, the attested build)
4. the C# solver

The Family FCS now loads after the FS-20 Vortex FCS when it is there (soft BepInDependency). The startup line reads
"...loaded from <path> (the FS-20 Vortex FCS's core, own copy)" in case 3.

## 3. Keys

Each plugin reads its STOVL pirouette / ship-relative keys and puts up their HUD messages only while it flies the local jet; out of a
jet, only the FS-20 Vortex FCS's keys. So bind B keys in Vortex_FS20.FCS.cfg and family-jet keys in Vortex_FS20.FCS.Family.cfg (or the
same key in both). Side effect for a single install: the keys no longer toggle while flying a non-Vortex aircraft.

## 4. Checks

- Stock B bench suite (reg / sw / cork / dmg) identical to 0.7.1.
- Family DLL user strings: OG GUID "Vortex_FS20.FCS", type "Vortex_FS20.FCS.FcsStandalonePlugin", resource "Vortex_FcsCore.dll", own
  lib "VortexFamily_FcsCore" (a build-family.sh rename slip that left the type name pointing at the Family namespace was caught here and
  fixed through FamilyCompat.OtherNs).
- Hand-over truth table checked by reading the code for every version pair above; **not run in the game**, nor the core fallback
  (the installed Family folder still has VortexFamily_FcsCore.dll beside it, so case 2 runs; delete it to exercise case 3).
