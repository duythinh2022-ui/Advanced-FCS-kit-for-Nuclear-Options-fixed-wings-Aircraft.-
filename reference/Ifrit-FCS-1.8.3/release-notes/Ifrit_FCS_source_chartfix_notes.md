# Ifrit FCS source: aero-chart ownership fix (no release)

Base: 0.7.3 source. Source-only change, no new DLL: the Ifrit does not hook `AeroJobSettings.Schedule`, so `StallStaggerJobs` never runs in 0.7.3 and the built mod is unaffected.

**Why:** the same file in the F-22E (2.16.1) and Revoker (0.4.3) took any change in the shared aero chart table's length as a rebuild and re-added its charts; with two such mods loaded they re-added after each other every physics tick without end (gradual FPS loss). Each copy now keeps what it wrote and only re-adds when its own charts are actually gone. Patched here so that this code base, and any future FCS ported from it, starts from the fixed file: two unfixed copies in one game bring the loop back.

`StallStaggerJobs.cs` is now identical to the F-22E 2.16.1 / Revoker 0.4.3 copies apart from namespace and log names. Compiled against the 0.34.6 assemblies. The next Ifrit release (0.7.4) builds from this tree; it changes nothing in flight unless the chart hook is added.
