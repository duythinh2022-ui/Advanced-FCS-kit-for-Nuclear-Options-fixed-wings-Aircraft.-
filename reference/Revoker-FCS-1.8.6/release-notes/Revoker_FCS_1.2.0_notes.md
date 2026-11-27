# FS-12 Revoker FCS 1.2.0: the asymmetric-load AoA limit with the nozzles counted

Base: 1.1.1. **Law change**, only with a lateral CG offset over 4 cm, vectoring on and T/W over 0.3. Tom, 11 Nov 13:20, after flying 1.1.1 with stores on one side (27 - 34 cm of offset): "with tvc on the aoa limit swings a lot, and it allows for 40-60aoa at that point the jet tipped to one side even with tvc. with tvc off seems like the limit works correctly at least for the most part"; 13:34: "fix revoker".

**Changelog:** With an asymmetric load, vectoring on and the thrust up, the AoA limit now comes from a coupled check that counts both nozzles (Ternion 0.2.2's, extended): steady with speed and throttle, about 28 deg with 30 cm of offset where 1.1.1 gave 12 deg at speed and 40 - 70 deg when slow on afterburner. New dev setting AsymCoupledReserveMax (0.7).

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,311,232 bytes, sha1 b0a23688...). The same toolchain rebuilds the released 1.1.1 byte for byte.
- Benched (.NET 8). Not flown.
- Loaded on the game's Mono under Wine (`-O=-float32`): the core unpacks and loads (same core as 1.1.1, hash 4c6b1055a4c69bc9), kernel self-test 200 / 200. The new solver on that runtime: a hand-checked case right (60 / 40 deg/s2), 2,000 random problems without a bad result.
- The solver runs in C# in this version (the Ternion 0.3.0 native one is not ported yet). Cost on the bench VM, loaded and vectoring: sweep 189 -> about 435 us per tick (mean). Not measured in the game; the 10 s timer line will show it (`asym sweep`). Nothing changes for a symmetric jet.

## What the 13:16 log showed (1.1.1)
- The limit followed throttle and speed, not the TVC switch: at 107 - 194 kt with vectoring on, 17 deg at idle and 56 deg (38 - 70) on afterburner; 12 jumps of more than 20 deg inside 0.5 s in 168 s. Over 214 kt it sat on the 12 deg floor in both states (3.7 g at full aft stick).
- Cause 1: the thrust x CG-offset yaw (17 deg/s2) was in the load, the yaw nozzle that trims it (1.6 deg of 15) was not in the authority. Level and hands off at 159 kt the demand read 4.7 where the limit engages at 1.08.
- Cause 2: with the demand over the threshold at every AoA, `AsymCap.Find` returns the AoA of least demand (Ifrit 0.4.6). That was the case 91 % of the time under 194 kt: 41 - 70 deg.
- Cause 3: the roll reserve takes 80 % of the roll authority at every speed, which is what puts the limit on the floor at speed with the load at 16 % of the roll authority.
- The tip itself: stick centred at 61 deg and 126 kt, a steady 15 deg/s of yaw with the left elevon and canard on their stops, sideslip under 2 deg.

## What changed
- `CoupledCap.cs` (new, the Ternion 0.2.3 file): per sweep AoA a small LP over every effector's moment points: the most flight-path roll each way with the load held, no nose-up acceleration and no sideslip acceleration.
- `FcsController.AsymSweepPoint`: the airframe's moment without surfaces is the load; the canards (attached points only, inside +-40 deg), both elevon pairs, the rudder, **the pitch nozzle (its grid carries the thrust moment about the CoM at each angle, so the offset yaw and the roll its pitch angle makes are in it) and the yaw nozzle** are the LP's effectors. The thrust at the nozzle's present angle leaves the load. Each side is solved with 3 deg of the sideslip that works against it.
- The demand is the weaker side's shortfall against the mean of the two, over the share the reserve leaves (`AsymCoupledReserveMax`, 0.7; the one-axis check's ceiling is 0.8). `AsymCap.Find`, the 3-sample median, the filter and the slew are unchanged.
- Active only where the nozzles cover the jet: vectoring on, not cut out by speed, T/W over `LowThrustFixedAoATW` (0.3), latched per sweep. **With the nozzles locked or the thrust low the limit is 1.1.1's, unchanged** (see open points).
- While it runs the sweep takes 2 points per tick instead of 3.
- Telemetry: three columns at the end (`cplU`, `cplRp`, `cplRn`: demand and reserve each way at the present AoA). The log line says "coupled" or "one-axis".
- Bench switches (all in `FcsController`): `CoupledCap` (false = 1.1.1), `CplTvc`, `CplBetaDesign` 3, `CplAttachedOnly`, `CplPts` 9, `CplPointsPerStep` 2, `CplDeadband` 2; diagnostics `CplDbgT`, `CplDebug`.

## Bench results (store on the left wing; 2,000 kg = 26 - 30 cm at 14.2 t)
The bench reproduces the tip with the limiter off: 14.8 deg/s of flight-path roll at 65 - 71 deg and 50 - 70 m/s (the flight: 15.3). It does not reproduce 1.1.1's 40 - 70 deg fallback (its one-axis curve bottoms at low AoA, so 1.1.1 reads 12 deg there).

| case (full aft stick unless said) | 1.1.1 | 1.2.0 |
|---|---|---|
| afterburner, entered at 250 - 600 km/h, 19 s each | limit 12 - 16 deg | 27.4 - 28.9 deg (to 31.6 in the 500 km/h run); the raw limit moves 0.2 - 1.3 deg inside a run (3.4 at 500 km/h), no jump; drift 0.1 - 0.8 deg/s (mean; 2.3 at most), sideslip 0.7 deg or less |
| half throttle, 300 / 450 km/h | 12 deg | 27.7 - 33.8 deg; drift 0.2 - 0.5 deg/s |
| roll reversal on the limit, both ways, 300 / 450 km/h | at 12 deg | at 27 - 30 deg: 38 - 65 deg/s, sideslip 1.1 deg or less; the rate follows the command through zero, no stop-and-spike |
| full forward, 300 km/h | negative limit -8 to -23 deg | -26 to -30 deg; reached -30.3 deg, sideslip 0.3 deg |
| throttle cut on the limit, then afterburner again | - | the bench's nose-down cap takes over at idle (10 - 17 deg): AoA 29.5 -> 17.7 deg in 2 s at -4.5 deg/s, no roll or yaw excursion; back up at 12 deg/s |
| idle (T/W 0.07), nozzles locked | 12 - 14 deg | identical to 1.1.1 |
| 1,160 kg (16 cm), afterburner | 12 - 18 deg | 35 - 44 deg down to 70 m/s; under 55 m/s it rises to 58 deg (2 raw jumps), drift 1.6 mean / 6.4 max deg/s |
| 560 kg (8 cm), afterburner | 51 - 70 deg, drift to 12 - 13 deg/s | 51 - 70 deg, drift to 13 - 15 deg/s |

Curves read, 300 km/h pull: pitch rate to 43 deg/s, AoA 1 deg past the limit at 4.0 s and on it from 4.4 s; a 0.4 s roll dip of 6 - 9 deg/s during the pitch-in (sideslip to 2.3 deg, gone by 6 s). Reversal on the limit: 0 -> 44 deg/s in 1.0 s behind the command by under 2 deg/s, through zero in 0.7 s, AoA inside 27.5 - 28.4 deg.

Reserve share, 2,000 kg, afterburner, full aft for 37 s (IAS 64 - 156 m/s):

| AsymCoupledReserveMax | limit | drift, stick centred | unmet roll |
|---|---|---|---|
| 0.8 | 21 - 22 deg | 0.0 deg/s | 0.0 deg/s2 |
| **0.7** | 28 deg | 0.0 - 0.4 | 0.3 - 1.1 |
| 0.65 | 31 - 36 deg | 0.1 - 4.2 | 0.4 - 11 |
| 0.6 | 32 - 38 deg | 1.2 - 3.1 | 3.3 - 7.7 |
| 0.5 | 36 - 45 deg | 1.8 - 4.4 | 4.4 - 9.6 |

Regression: `CoupledCap=false` against 1.1.1: 127 / 127 per-case files and 4 / 4 tables identical (timing columns aside). Default against 1.1.1: 126 / 127 and 4 / 4; the one file is a damage case with a part gone and a CG shift, where the flying is identical and only the check's own columns differ. Loaded-reversal set (88 cases, 8 - 21 cm): median AoA at the reversal 17.6 -> 38.9 deg, sideslip 3.2 -> 1.4 deg, hitch 0 in both, roll acceleration at the brake 147 -> 84 deg/s2 (the higher AoA); roll spam sideslip max 1.95 mean / 3.03 worst.

## Open points
- **The allocator leaves roll unmet that the airframe has.** On a limit of 36 - 49 deg (share 0.5) the LP reads 22 - 50 deg/s2 spare on the weak side, yet the allocator leaves the whole 10 - 15 deg/s2 of feedback demand unmet with the yaw nozzle at 4 deg of 15 and the outer elevons at 6 - 14 deg of 30, and the jet yaws 3 - 8 deg/s toward the heavy side. Raising the flight-path-roll axis weight 1 -> 8 on the bench cut the unmet roll 9.4 -> 1.3 deg/s2 and the drift 4.7 -> 1.2 deg/s at 70 - 90 m/s; cutting the yaw nozzle's cost 25 times changed nothing. This is why the share is 0.7 and not 0.5, and it is the likely reason the jet tipped at 60 deg with nozzle travel left. Changing that weight touches every roll, so it needs its own bench campaign.
- **Nozzles locked / low thrust is 1.1.1's check**, fallback included (the log shows it at 46 - 52 deg under 175 kt with the nozzles locked, held to 38.7 deg only by the nose-down cap). With the coupled check on there, the bench departed: lateral limit 20 - 26 deg, the 1.1.1 nose-down cap fell 23 -> 12 deg under the jet during the pull, AoA 30 deg, sideslip 18 deg. That cap needs the Ternion's held-load pitch figure before the lateral one can move.
- The bench's nose-down cap at idle (10 - 15 deg) is far under the game's (39 - 52 deg in the log), so the throttle-cut row above is the bench's, not necessarily the game's.
- Medium loads under about 105 kt: the curve is flat near the threshold and the limit rises and jumps (the 1,160 kg row). Light loads behave as in 1.1.1.
- Native solver (Ternion 0.3.0's `cpl.rs`) not ported: the C# one costs sweep time while a load is on.
- The Ifrit has the same one-axis code and thrust accounting; not touched.

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` 1.2.0; 1.1.1 kept as `Revoker_FS12_FCS.dll.1.1.1.bak`. Loads at the next game start.
- **Dev:** unchanged (the 1.1.1 Dev DLL). The new setting appears in `Revoker_FS12.FCS.Dev.cfg` under "Asymmetric load".
- **Release:** `C:\Games\NO mods\Revoker-FCS-1.2.0-release`.
- **Mod manager:** its record in the plugin folder still says 1.0.0; update the version there, or its modpack may put an old DLL back.

Previous notes: `claude/Revoker_FCS_1.1.1_notes.md`. The log read: `claude/Asym_coupled_cap_port_handoff_2026-11-11.md`.
