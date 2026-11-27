# Vortex FCS 0.4.11: ship speed match back on at every new match

18 Nov 2026. Built on 0.4.10 (the other chat's airbrake fix, carried unchanged). **Compiled only, not flown.** No flight-law change.

Tom, 01:05:

> make the ship speed relative on by default, like when i toggle it off with a hot key, and i join a different match, it must be on again

## Cause

The hotkey (`StovlShipRelativeKey`) wrote its new state into the config file (`StovlShipRelative`). Switching it off once kept it off in every later match and game.

## Change

- **Hotkey:** switches ship-relative for the current match only. It no longer touches the config file.
- **New match:** every mission load (single player or joining a server, detected when the game's current mission changes) puts it back to the config value. The default is on.
- **Config value:** `StovlShipRelative` is now the setting every match starts with. Changing it by hand (file or configuration manager) takes effect at once.
- **Migration:** a `false` left in the file by the old hotkey is moved back to `true` once (`GovernorRevision` 8). Set it to false by hand afterwards if you want every match to start off.
- The log notes `new mission, STOVL ship speed back to the default` when a hotkey change is cleared.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.4.11 and `meta.json`; 0.4.10 kept as `Vortex_FS20_FCS.dll.0.4.10.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.4.11-release`.
