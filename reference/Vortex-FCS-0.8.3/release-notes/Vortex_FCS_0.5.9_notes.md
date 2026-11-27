# Vortex FCS 0.5.9: the pirouette key works while you hold the pedal

18 Nov 2026. On 0.5.8.

**Status: compiled and smoke-tested; not tried in game (key input only runs there).**

Tom, 18 Nov (on 0.5.8):

> the hot key works until i hold yaw then it doesnt work

## Cause

The keys were read with BepInEx's own shortcut check (`KeyboardShortcut.IsDown` / `IsPressed`). That check has a rule of its own: the shortcut only counts if **no other key on the keyboard is held** (its `_modifierBlockKeyCodes` list, in the installed `BepInEx.dll`). With the rudder on keyboard keys, holding a yaw key blocked the pirouette key:

- a tap while yawing did nothing;
- a hold of the pirouette key ended the moment a yaw key went down.

Your log shows the key itself working (the `STOVL PEDAL: PIROUETTE` / `COORDINATED` lines) when nothing else was held.

## Fix

The mod reads its keys itself now: the key counts as pressed when its main key goes down with its modifiers (if any) held, whatever else is held. A hold lasts as long as the main key is held. This applies to both of the mod's keys, `StovlPedalPirouetteKey` and `StovlShipRelativeKey` (same problem).

No change to the flight laws; nothing the bench runs changed.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.9 and `meta.json`; 0.5.8 kept as `Vortex_FS20_FCS.dll.0.5.8.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.9-release`.
- Restart the game to load it.
