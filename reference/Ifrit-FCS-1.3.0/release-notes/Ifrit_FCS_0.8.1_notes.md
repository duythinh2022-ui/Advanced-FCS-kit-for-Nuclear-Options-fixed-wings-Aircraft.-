# Ifrit FCS 0.8.1: VMC release ceiling and a sliding engine-out AoA limit (22 → 18)

Base: 0.8.0 source. Compiled against the game's assemblies. **Not benched, not flown.** The only check is an open-loop replay of the
4 Nov 22:02 log (`Ifrit_FCS_20261104_220207.csv`), with the logged T/W standing in for the engine model. The replay can't show how the
jet would have flown with the new limits. 0.8.0 kept as `.dll.0.8.0.bak`.

## Tom (4 Nov, 22:08)
"for vmc, 1 engine damage, when the vmc allows the healthy one to go max ab again, i think it is a tad bit too early, because the jet
rolls a bit. Also make the aoa cap on ifrit dynamic from 18 to 22 so the vmc wont cut the engine as early when climbing at low speed,
make the limit change gradual, and only towards when the yaw is starting to get too much to compensate."

## What the log shows (engine out from 123.4 s; `engOut` = 1)
| VMC cut | Release | What happened |
|---|---|---|
| 169.4 s, 76 m/s, climbing at 50° pitch | 179.4–181.6 s, IAS rising +2.7 m/s² | Predicted yaw usage at full thrust was still ~0.9–1.0 when the release finished |
| 200.1 s, 49 m/s, 69° pitch | 211–215.7 s, accelerating in a dive | Usage fell under 0.8 at about 213 s. The release was about right |
| 223.4 s, 89 m/s, 57° pitch | **225.3–226.8 s, IAS still falling 7–8 m/s²** | Thrust back at full while the speed went 76 → 66 m/s. The thrust-yaw usage went 0.69 → 1.05, and the jet rolled 77° → 108° with the stick centred. This is the "rolls a bit" |

The old release only waited for calm sideslip (under 2.5°) and the yaw no longer saturated. Then it put the throttle back at 0.2 per
second, whatever the speed was doing.

## Change 1: VMC release ceiling (engine out or weak engine)
- **The rule:** the throttle is given back only up to the highest input whose **predicted yaw usage stays under `VmcReleaseUse` (0.8)**.
  - Predicted usage = the thrust yaw now (`asymAyNow`), scaled by the split's thrust at that input against the split now, divided by the
    tails' authority (`asymCyNow`) at the IAS `VmcLook` (1.5 s) ahead.
  - The "split's thrust at that input" comes from the engine model (`EngThrustAt`). The "split now" comes from the measured RPMs, so
    spool lag is included.
  - IAS ahead = IAS + min(0, IAS rate) × 1.5 s. Only deceleration counts.
- **It only stops the rise.** Cutting the throttle is still the run-away's job. If your throttle is at or under the ceiling, nothing changes.
- **Replay:** at 225–227 s it holds at about 70–80 % of full thrust instead of full. At 181 s full thrust comes back about 1 s later.
  The 213 s release is unchanged.

## Change 2: engine-out AoA limit slides from 22 toward 18
- **New Dev key:** `Asymmetric load / EngineOutAoACapMin` = 18. `EngineOutAoACap` (22) stays the top of the range. Setting them equal
  gives the 0.8.0 fixed limit.
- **What moves it:** the larger of two signals.
  - The predicted yaw usage at **your** throttle (same model as above), ramping 0.6 → 0.9.
  - The measured yaw saturation, filtered 0.3 s, ramping 0.3 → 0.8. It only counts while the stick is lateral-quiet, so your own rolls
    don't lower the limit.
- **How fast:** 2 °/s down, 1 °/s up. Both signs. It is applied after the asymmetric-cap slew. The AB gate stays on the fixed
  `EngineOutAoACap`.
- **Replay** (open loop, so the AoA did not actually follow):
  - 196 s climb: the limit starts down at 193.5 s and is at 18° by 195.6 s, 4.5 s before the 200 s cut.
  - 168 s climb: it starts at 167 s and is at 19.7° by 169 s (the cut came at 169.4 s).
  - 223 s: only 21.9° by 223 s. The model said 0.6 usage there, but the yaw was already saturating. The saturation signal is what moves
    it, and that is late.
- **Known gap:** the yaw-authority model reads optimistic in two of the three climbs. The yaw saturated at a model usage of 0.5–0.65
  (at 169 s and 223 s), against 1.2 at 199 s.

## Telemetry (appended before the surface columns)
- `eoCap`: the sliding limit.
- `vmcCeil`: the release ceiling (throttle input; 1 = none).
- `vmcUse`: predicted yaw usage at your throttle.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 3591e166e2a99100ee457b143eab3d93) and `meta.json` 0.8.1.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.8.1-release`.
