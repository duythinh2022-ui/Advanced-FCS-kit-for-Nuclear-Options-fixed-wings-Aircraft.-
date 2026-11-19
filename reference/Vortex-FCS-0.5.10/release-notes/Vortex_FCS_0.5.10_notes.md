# Vortex FCS 0.5.10: quicker sideways start and stop, less side speed

18 Nov 2026. On 0.5.9.

**Status: benched (the side-stick case only), not flown. No regression bench (Tom: not needed).**

Tom, 18 Nov: which setting makes the sideways start and stop faster at the cost of side speed → `StovlTrcStopTime`; "set it to 0.75 by default migrate settings".

## Change

- `StovlTrcStopTime` (dev cfg, `[9. STOVL]`): default **1.06 → 0.75**. Full side stick in the hover now asks for the speed the jet can stop from in 0.75 s, so the speed is 0.75 / 1.06 = 71 % of before. It still scales with weight, fuel, stores, thrust and altitude.
- **Migration:** `VortexConfigRevision` 12 → 13. If your dev file still holds 1.06, it's moved to 0.75 once (logged as `Vortex FCS 0.5.10 defaults: StovlTrcStopTime 1.06 -> 0.75`). A value you set by hand is left alone.

## Bench (13.8 t, hover, full side stick 8 s then release; left and right the same)

| | 0.5.9 (1.06) | 0.5.10 (0.75) |
|---|---|---|
| Full-stick side speed | 10.1 kt | 7.2 kt |
| 90 % of that speed reached | 3.08 s | 2.65 s |
| Stop to 0.3 m/s after release | 3.15 s | 2.57 s |
| Past zero | 0.08 m/s | 0.07 m/s |
| Largest bank | 14.4° | 12.3° |
| Height band | −2/+1 cm | −2/+1 cm |

Buffed clean full fuel at sea level: about 10.6 kt (was 15).

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.10 and `meta.json`; 0.5.9 kept as `Vortex_FS20_FCS.dll.0.5.9.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.10-release`.
- Restart the game to load it; the dev file is updated on the first start.
