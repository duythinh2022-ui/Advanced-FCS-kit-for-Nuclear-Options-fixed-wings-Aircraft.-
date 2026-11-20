# Vortex FCS 0.5.15: shorter, all-caps HUD messages

19 Nov 2026. On 0.5.14. **Text only: no control-law change.**

Tom, 19 Nov:

> for those ones, what can be shortened, like WEAPONS to WPN, SPEED to SPD. And should the lower case ones be capitalized

> do that then for 0.5.15

## What changed

The messages on the game's action report line (the line the mod's STOVL and weapon messages go to, about 4 s each) are all caps and use standard cockpit abbreviations: CONV conversion, INHIB inhibited, OVSPD overspeed, REL release, GND ground, MNVR manoeuvre, COORD coordinated. `>` / `<` replace "above" / "below"; units stay `KM/H` as on the game's HUD. The bold tags are gone.

| 0.5.14 | 0.5.15 |
|---|---|
| STOVL conversion: doors opening | `STOVL CONV - DOORS OPENING` |
| STOVL mode | `STOVL MODE` |
| STOVL conversion inhibited above 460 km/h | `STOVL INHIB: IAS > 460 KM/H` |
| STOVL conversion inhibited: true airspeed over … km/h | `STOVL INHIB: TAS > … KM/H` |
| STOVL conversion inhibited: AoA over 24 deg | `STOVL INHIB: AOA > 24°` |
| UNSAFE STOVL - DROP RIGHT/LEFT WPN | unchanged |
| Reconversion to CTOL | `CTOL CONV` |
| Reconversion inhibited below 240 km/h | `CTOL INHIB: IAS < 240 KM/H` |
| CTOL mode | `CTOL MODE` |
| Overspeed: reconversion to CTOL | `OVSPD - CTOL CONV` |
| STOVL off | `AUTO STOVL DISARMED` |
| STOVL armed: converts below 350 km/h | `AUTO STOVL ARMED: < 350 KM/H` |
| STOVL conversion below 350 km/h: doors opening | `AUTO STOVL CONV - DOORS OPENING` |
| CTOL above 370 km/h (STOVL stays armed) | `AUTO CTOL CONV > 370 KM/H - STOVL ARMED` |
| STOVL conversion called off: manoeuvring (STOVL stays armed) | `STOVL CONV ABORT: MNVR - STOVL ARMED` |
| UNSAFE WPN RELEASE - HOLD 1s | `UNSAFE WPN REL - HOLD 1 SEC` |
| STOVL SPEED: SHIP RELATIVE / GROUND | `STOVL SPD: SHIP REL / STOVL SPD: GND` |
| STOVL PEDAL: PIROUETTE / COORDINATED | `STOVL PEDAL: PIROUETTE / STOVL PEDAL: COORD` |

- The speeds shown are the config values at run time (defaults above); the TAS limit is worked out from the game's nozzle cut-off, so its number varies.
- `AUTO STOVL DISARMED` (was "STOVL off") only shows with `StovlAutoTransition` on (off by default): pressing the button again disarms the automatic conversion.
- The degree sign: the game's own assembly uses it in 58 strings, so its fonts have it.
- Unchanged: the throttle gauge readout (`0 GS`, `+12 KT`, `SHP`, `HOLD SPD`, `ACCEL`, `DECEL`, `THR 85%`, `IDLE`), the detent indicator (`STOP HOLD n%`, `STOP`, `STOP LOCK`), the ship-match icon.
- The BepInEx log lines carry the new text too (log line: "Vortex FCS STOVL: <message> (IAS …)"). No script in the source tree matched on the old wording.

## Checks

- Bench traces identical to 0.5.14 (byte for byte) on the hover, 30 kt turn, side step and climb-then-command cases: the change touches no control path.
- Offline smoke test passes; the source zip rebuilds the DLL byte for byte.
- **Not seen in the game yet**: line length on the HUD and the degree sign are untested on screen.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.15 and `meta.json`. 0.5.14 kept as `Vortex_FS20_FCS.dll.0.5.14.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.15-release`.
- No config migration. Restart the game to load it.
