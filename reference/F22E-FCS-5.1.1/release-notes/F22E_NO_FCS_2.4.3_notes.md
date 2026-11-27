# F-22E FCS 2.4.3

Base: 2.4.2 as last installed. One change, from the Ifrit's 23 Oct 00:51 log (Ifrit 0.4.3 §2): the predictive thrust match now counts the healthy engine's afterburner. The Ifrit's other 0.4.3 changes (the settled-window knee, the roll-fight exemption) are for that jet's stab foil and its lack of rudders; this jet has had the settled window since 2.3.0 and never had the exemption.

## The match sees the afterburner
`splitUsage` (2.4.1) predicted the thrust split from each engine's *dry* thrust at its RPM, on the nozzles' arms — "the afterburner is equal on both when both have it, and the throttle is held under it when only one has". The throttle is only held while the match is in: released, the healthy engine's afterburner is the split. On the Ifrit, vertical at 35–42 m/s with one engine at condition ~0.45, the real split was 7.1 °/s² against 6–7 of yaw authority, the prediction without the afterburner 3.8, `splitUsage` 0.80 at most against a latch above 0.8, and the nose yawed 0.3 → 9.5 °/s over eight seconds with the stick centred.

**Now:** each engine's predicted thrust includes the afterburner it would have at the pilot's throttle (`rawThrottle`, before the match holds it under the start), on an engine whose condition lets it light. Same arms as before (the nozzles' own positions, `Vector3.Cross(nozzle − cg, thrust)` — the engine spacing has been in the prediction since 2.4.1; this jet's shorter arms are why the yaw was minor here). With the afterburner in, the match holds below the speed where the split would use more than 64 % of the fins' yaw authority (`MatchYawOn` 0.8 × `AsymResMaxFrac` 0.8) whatever the AoA; above it the afterburner stays yours. On the Ifrit bench that is about 250 km/h level at full throttle; this jet's number will come from its log.

## Install
- **Installed:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 8afa79ec633c5f25ef38cfbc645c935e), `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (37dfb3215de048b26a32d6d1f4396738), `meta.json` 2.4.3 (sha256 of the release zip in the file).
- **Backup:** 2.4.2 is `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.2.bak` (c748e7880eb61bdd0a5848f3f16b9b22); 2.4.1 and 2.4.0 are still there.
- **Config:** nothing new.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.3-release`.

## Not verified
- Not flown; compile-checked only (the F-22E bench has no engine-damage model). The logic is the Ifrit's, bench-verified there.
