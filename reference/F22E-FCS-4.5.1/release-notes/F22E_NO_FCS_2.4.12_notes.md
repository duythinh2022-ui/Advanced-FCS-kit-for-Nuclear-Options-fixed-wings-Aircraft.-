# F-22E FCS 2.4.12

Base: **2.4.11**. Ground steering only, three changes. Same changes as Ifrit 0.5.2.

Compiled only. The drift limit is not bench-tested: the ground bench has no rudder or aero, which is what drives the high-speed drift. Taxi it.

## 1. Pedal = the stock steering law
Stock steering on the ground is not the raw pedal. The game's `ControlsFilter` runs the stock fly-by-wire on the ground above its `minSpeed`, and the nose gear, rudders and differential brake all read its yaw output. The law, read from the game's IL:

`yaw = clamp(yawTightness × remap × (pedal − yaw rate [rad/s]) + yawWeathervaning × sideslip, ±1)`

`remap = 1 / max(V²ρ / (cornerSpeed² × 1.225), 1)`, which is 1 at taxi speeds.

| | FBW on the ground | yawTightness | yawWeathervaning | cornerSpeed |
|---|---|---|---|---|
| F-22E (Aryx) | above 10 m/s (19 kt) | 2 | 0 | 140 |
| Ifrit | always | 1 | 0 | 160 |

2.4.11 sent the raw pedal instead. That has no yaw damping, and at part pedal it was 2–3× sharper than stock.

2.4.12 computes the stock law with the jet's own `ControlsFilter` parameters, read live. It feeds the result to the nose leg, the rudders **and** the main legs' differential brake, exactly as stock does. It uses this law while the pedal is in, and after release until the yaw has stopped. If the parameters can't be read, it falls back to the raw pedal and logs one warning.

Bench (true-stock law against 2.4.11's raw pedal, F-22E):
- **Full pedal:** turn radius unchanged, 20.5 / 30.8 / 44.5 m at 23 / 31 / 39 kt.
- **Release:** the stock damping stops the yaw in 0.5–0.98 s, against 1.2–1.3 s for 2.4.11.

## 2. Drift limit (new)
Stock steering doesn't limit sideslip. Above 49 kt on 27 Oct, the Ifrit peaked at 17–25° in pedal turns and 42° in a slalom; the F-22E reached 38–40°.

The limit lets the sideslip reach **`GroundDriftAllow` 8°**, which is about half the Ifrit's drift. Past that, it counter-yaws by **`GroundDriftGain` 0.2** per degree, so full counter at 5° over.
- **Lead:** the limit acts on the sideslip predicted **`GroundDriftLead` 0.25 s** ahead from its rate, so a fast skid is caught before it overshoots.
- **Speed fade:** it fades in from **`GroundDriftFadeSpeed` 15 m/s** to 25 m/s (29–49 kt), so tight taxi turns keep their natural sideslip (up to 6–11° at 10 kt).
- **Tuning:** all four are dev keys (Ground). Raise `GroundDriftAllow` for more drift, or set `GroundDriftGain` to 0 to switch the limit off.

## 3. Line hold: world-origin shift fix
NO shifts its Unity world origin as the jet travels. The line hold, since 1.8.36, stored the captured line in Unity world coordinates, so each shift moved the line.
- **27 Oct F-22E log (12:12):** the line jumped 242 m (t = 59.9 s, takeoff roll) and 58 m (t = 206.6 s, landing roll).
- **The landing jump** made the hold ask for a +4 deg/s right yaw toward a phantom line. That was 0.2 s before the jet yawed right at up to 61 deg/s and spun to 88° of sideslip at 97 m/s.
  - The wheels were light at the time (nz 0.35–0.6, after a push at touchdown).
  - The rudders went to full left against it within 0.6 s.
  - So the phantom command was the trigger, not the whole cause.
- **The fix:** the line is now stored as the game's `GlobalPosition`.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 ad54624d4cc43c58d363bebfed0fe720) and `meta.json` 2.4.12.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (6f3affad962c595c7e9c5691beda8abb).
- **Backup:** 2.4.11 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.11.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.12-release`.
