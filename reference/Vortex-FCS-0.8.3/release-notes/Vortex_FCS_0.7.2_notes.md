# FS-20 Vortex FCS 0.7.2 + FS-20 Vortex Family FCS 0.7.2 — notes (23 Nov 2026)

Asked (Tom, 23:08, "do it"): a FS-20 Vortex FCS whose B (vanilla Vortex) code differs but whose family code is the same as the Family
FCS's should still take over the family jets. In 0.7.1 the fingerprint worked per file, so a B-only tweak inside a shared file
(Plugin.cs, FcsController.cs, ...) broke the match.

## Change (tools/gen_buildinfo.py only; no control-law change)

B-only code inside a shared file can now be wrapped:

```csharp
// FAMILY-FP EXCLUDE BEGIN
if (FamilyCompat.VariantOf(aircraft) == FamilyCompat.Variant.B) { ... }   // code that only ever runs on the stock B
// FAMILY-FP EXCLUDE END
```

The marker lines and everything between them are left out of FcsBuildInfo.FamilyFingerprint. Whole files can still go in
family-fingerprint-exclude.txt.

- Not nested; a BEGIN without END (or the reverse) stops the build with the file and line.
- **Nothing checks that the marked code never runs on a family jet.** The guard is yours; a marked block that does act on a family
  jet makes the FS-20 Vortex FCS fly the family jets with code the Family FCS doesn't have, while the two report a match.
- A file without markers hashes byte-for-byte as in 0.7.1, so the 0.7.2 fingerprint is the 0.7.1 one (b0dcb4c4dde9de4c): 0.7.1 and
  0.7.2 of either plugin still match each other.

| who flies the family jets, both installed | 0.7.1 | 0.7.2 |
|---|---|---|
| same source | FS-20 Vortex FCS | FS-20 Vortex FCS |
| B-only change in an excluded file | FS-20 Vortex FCS | FS-20 Vortex FCS |
| B-only change inside marked blocks of a shared file | Family FCS | **FS-20 Vortex FCS** |
| B-only change in a shared file, unmarked | Family FCS | Family FCS |
| change that runs on a family jet | Family FCS | Family FCS |

## Checks

- Script, on a copy of the tree: no markers -> b0dcb4c4 (= 0.7.1); a marked block added -> b0dcb4c4; the marked value changed ->
  b0dcb4c4; END removed -> build stops ("BEGIN never closed", file:line); an unmarked one-line change -> a8eefbbf (differs).
- Both 0.7.2 DLLs vs 0.7.1: same size, 24 bytes differ (version string and the deterministic build's module id/hash); no other change,
  so no bench run. Not run in the game.
