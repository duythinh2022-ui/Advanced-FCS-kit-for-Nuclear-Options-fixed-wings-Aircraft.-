# Vortex FCS 0.5.4: Stability Assist off keeps the jet in the air

18 Nov 2026. On 0.5.3.

**Status: benched, not flown.** The throttle set on the game's lever (keyboard throttles) is game-side code: compiled only.

Tom, 18 Nov:

> when i turn off stability assist in stovl, i want it to automatically increase throttle to the last recorded thrust to keep the jet on the air. But i still have full manual throttle control

## What was wrong

In STOVL flight the throttle lever is a speed lever. In a hover it sits near the aft detent (near 0), and the laws run the engine themselves. Stability Assist off hands the engine straight to that lever, so the engine fell to idle.

Bench, your jet (Buffed, 13 824 kg), hovering at 16 m with the lever at 0, Stability Assist off at 20 s, 0.5.3: engine 0.666 → 0, on the ground 14 m below within 3 s.

## What it does now

When you switch Stability Assist off **in the air with the STOVL laws flying**:

- The FCS takes the throttle that holds the engine at the RPM it has at that moment. That is the thrust the laws were using, held rather than chased (dry, as the laws use).
- **Keyboard or button throttle:** the game's lever is set to that value once. Your keys carry on from there; it is an ordinary throttle you own from the first tick.
- **Analog throttle axis:** the game takes an axis' position back as soon as it moves, so the lever isn't touched. The engine holds the value until you bring the axis to it, from either side; from then on the engine follows the axis. No step in thrust either way.
- On the wheels, or with the laws not flying, nothing changes. Switching Stability Assist back on works as before.

The log says which happened: `Stability Assist off in STOVL: throttle set to N % (the engine's N % RPM, N kN) …`, then either `throttle lever set to N % …` (keyboard) or `lever not moved (analog throttle axis) …`.

## Bench (your jet)

| Case | 0.5.3 | 0.5.4 |
|---|---|---|
| Hover at 16 m, lever 0, Stability Assist off: engine | 0.666 → 0 | 0.666 held |
| Height after 5 s / 15 s | −14 m (on the ground at 3 s) | −0.33 m / −2.8 m |
| Sink rate after 5 s / 15 s | n/a | 0.14 / 0.34 m/s |
| At 30 kt, lever at centre detent (0.5), off: engine | 0.638 → 0.5 | 0.638 held |
| Height after 5 s / 15 s | −14.4 m, on the ground at about 5 s | −0.75 m / −11.3 m |
| Analog lever pushed from 0 to 1 over 2 s, 5 s after the switch | n/a | engine stays 0.666 until the lever reaches it, then follows; no step |
| Stability Assist on: hover → 30 kt → stop | n/a | bench output byte-identical to 0.5.3 |
| Stability Assist off from the wheels | n/a | byte-identical to 0.5.3 |

## What it doesn't do

- **It holds thrust, not height.** With Stability Assist off nothing holds the height. In the hover case the jet sinks slowly (thrust held, thrust line a little different). At 30 kt it sinks faster as the speed bleeds off and the wing lift goes with it. The throttle is yours to correct.
- **The jet drifts backwards in a hover** (5 m in the first 5 s, 3.9 m/s by then). This isn't new: in Stability Assist off the vanes go to their rest angle (10° forward), as since 0.5.0, while the laws held them at about −5°. That tilts the fan's thrust forward.
- On the HUD, an analog throttle's gauge shows the axis, not the held value, until the axis meets it.

## Not verified

- The lever set for keyboard throttles runs in the game's input code; the bench has no input layer. Not seen in game.
- With a throttle the game flies as a collective, or with no player input, the lever is not set; the analog rule applies.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.4 and `meta.json`; 0.5.3 kept as `Vortex_FS20_FCS.dll.0.5.3.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.4-release`. The source zip rebuilds the DLL byte for byte.
- Restart the game to load it.
