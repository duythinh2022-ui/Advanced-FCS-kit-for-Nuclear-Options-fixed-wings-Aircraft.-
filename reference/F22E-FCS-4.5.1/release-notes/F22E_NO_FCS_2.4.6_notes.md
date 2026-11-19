# F-22E FCS 2.4.6

Base: **2.4.4** (not 2.4.5). Tom's call: the old pitch-rate cap comes back. 2.4.5's authority pitch-rate budget (`AuthorityPitchRate`, `AuthorityPitchRateMax`, `AuthorityStickKnee`) is removed entirely, in normal flight and MPO alike. The pitch-rate budget is again the IAS / T-W schedule clamped to `MaxPitchRate` 48.

Kept from 2.4.5: **`ThrottleResponseScale` 1.3 → 1** (the game's own spool rate and afterburner light-up, every variant). A file still on 1.3 is moved once (`AirframeRevision` 15). The 2.4.4 military thrust +5 % (`MilThrustScale` 1.05) stays.

Bench: full pulls at 250–600 km/h are identical to 2.4.4 (peak 46.7–48.4 deg/s, AoA ≤ 65.0), in MPO too.

The 2.4.5 findings (in its notes) still stand if this is revisited:
- full stick 63–89 deg/s;
- MPO 211–259 deg/s;
- the S1 jab / roll-rate costs;
- the damaged-engine worst case fine at stock spool.

## Install
- **Installed:** `Aryx_F22E_StrikeRaptor_FCS.dll` (md5 840571409606d67f4fa119c86fbd2c16), `Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (3f702f2e224ee10e1e4fa13695e52cbd), `meta.json` 2.4.6.
- **Backup:** `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.5.bak` (d9bbda212d9fd10faba7c4615cfc87de).
- **Config:** the three `Authority…` keys are no longer read (left as orphans in a file 2.4.5 wrote; harmless).

## Not verified
- Not flown.
