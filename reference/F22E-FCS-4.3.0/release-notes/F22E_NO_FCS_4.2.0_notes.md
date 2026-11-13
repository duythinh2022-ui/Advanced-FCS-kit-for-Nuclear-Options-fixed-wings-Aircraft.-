# F-22E Strike Raptor FCS 4.2.0 (12 Nov 2026)

Base: 4.1.5 (the differential-braking release). Minor bump: the coupled asymmetric-load check is ported in, with its native solver, but it ships **off** (`AsymCoupledCheck` = false). With the defaults this release flies exactly as 4.1.5.

**Status:** compiled, benched (full suite, both asymmetric-load matrices, loaded-reversal set, with the check off and on), run on the game's Mono under Wine. **Not flown.**

## 1. What is in it
- **Coupled check** (from the Ternion 0.2.2, by way of the Revoker 1.2.0 / 1.3.0 and the Su-30 0.5.0): at each AoA the LP works out how much flight-path roll is left each way with the load held, pitch held and no sideslip acceleration, every surface and (while vectoring) the nozzles counted. The AoA cap sits where the weaker side is down to `AsymCoupledReserveMax` (0.7) of the roll range. With the nozzles locked or the thrust low it runs without the nozzles and the nose-down cap reads the same LP's held-pitch figure (`AsymCoupledReserveMaxNoTvc` 0.8).
- **Top-edge search** (Su-30 0.5.0 `FindTop`): the cap is the lower edge of the top over-threshold stretch, so a hump in the demand curve at low AoA no longer pulls the cap to its floor.
- **Free nozzles at low thrust** (Ifrit 1.2.0): with a load on, the check takes the thrust at the nozzles' neutral and adds their nose-down range, instead of their present angle (the controller's own output; on the Ifrit it made the cap flip 10 <-> 34 deg).
- **Performance:** native LP (`f22_fcs_cpl`, ABI 4002000), grid cache, one-side solve (as the other five jets). The core is now built like the others' (Rust 1.97, `-Zbuild-std`); core sha256 `713b56bb25dd93b9...`, embedded in the plugin.

## 2. Why it is off
On the bench, with `AsymCoupledCheck = true`:

| case (full aft, afterburner, 20 s) | 4.1.5 | 4.2.0 with the check on |
|---|---|---|
| 3,000 kg on `WingRear_L` (~30 cm), 250-600 km/h | limit 12-17 deg | 16-23 deg, steady, sideslip <= 0.5, no drift |
| 2,000 kg, 300 / 450 | 15-17 | 25-30, steady |
| 1,000 kg, 300 / 450 | 27-30, steady | opens to 65 like the clean jet, then **dips to 21 deg twice per pull** below ~55 m/s (raw limit 65 -> 21 -> 65 within 1 s; the nose follows 65 -> 41 -> 65) |
| nozzles locked, 300 / 450 | 12-13 | 13.5 |
| idle, 300 / 450 | 13-14 | 16-18 |

Loaded-reversal roll spam (54 runs, 1,100 / 1,500 / 2,000 kg): sideslip 6.3 mean / 11.1 worst -> **6.9 / 25.6**; the worst three are 1,500 kg at 600 km/h, where the limit opened to about 50 deg and the sideslip reached 22-26 deg. Roll travelled 296 -> 140 deg (much less roll at the higher AoA). Roll-spam steps at the zero crossing 19.5 / 54 -> 1.9 / 28 deg/s2 (smoother).

Cause, as far as the bench shows: this jet's coupled demand curve is flat near the threshold over a wide AoA band for light and medium loads, with a spike at 55 deg (the canted rudders' sideslip authority collapses near 60 deg), so the cap swings a long way for a small change in speed. 0.8 instead of 0.7 caps the heavy load lower but the 1,000 kg dips stay (6-7 jumps); a gain-scaled edge cut the dips to 42 deg but raised the sideslip to 8 deg. It needs its own campaign on this airframe before it can be the default.

## 3. With the defaults (check off)
- Per-case CSVs identical to 4.1.5: 231 / 231 (suite, matrix, loaded reversals, damage) and 22 / 22 in the no-TVC matrix; all suite tables identical.
- Only extra work: the load-time LP self-test (50 solves, native vs C#).

## Mono (game runtime, Wine)
`jetcheck`: allocator self-test 200 / 200, coupled-cap self-test 50 / 50, PASS; LP 47 µs native vs 149 µs C# per solve. All six FCS DLLs loaded together in one Mono process, PASS.

## New dev settings ("Asymmetric load")
- `AsymCoupledCheck` (false): take the AoA cap from the coupled check. Try it with light-to-heavy loads if you want to see it; the bench says heavy loads gain a lot, light/medium loads can dip or open too far (above).
- `AsymCoupledReserveMax` (0.7), `AsymCoupledReserveMaxNoTvc` (0.8): its reserve shares.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.2.0; 4.1.5 kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.1.5.bak`.
- Dev DLL unchanged.
- Release folder: `C:\Games\NO mods\F22E-FCS-4.2.0-release` (meta.json hash updated for the new zip).
- Mod manager: update its record, or its modpack may put 4.1.5 back. Restart the game to load it.
