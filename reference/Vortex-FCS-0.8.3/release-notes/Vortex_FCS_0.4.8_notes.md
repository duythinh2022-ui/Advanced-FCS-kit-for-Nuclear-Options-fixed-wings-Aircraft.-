# Vortex FCS 0.4.8: the hover icon shows the ship speed match

18 Nov 2026. Built on 0.4.7; the control laws are unchanged (the law sources are the 0.4.7 files, byte for byte). One HUD change. **Compiled only:** the bench has no HUD and no ship, and nothing here has been flown.

Tom, 00:26:

> when the ship speed matching is engaged, show the vanilla H inside the shield symbol on the hud, make it flash when it is in the blends in region

## What it does

| State | Icon |
|---|---|
| STOVL laws flying, ship speed match fully in (ship within 600 m, blend finished) | H in the shield, steady, green |
| Blending in or out: between 800 and 600 m from the ship, the 3 s after the switch (`StovlShipRelativeKey`), a change of nearest ship | flashing, twice a second |
| No ship in range, switch off, STOVL laws not flying (CTOL, freestyle, sitting on the wheels) | off |
| Any other jet | the game's own behaviour |

- **Which icon.** The game's own auto-hover icon (`AutopilotIndicator.icon`). In vanilla it is lit while auto-hover is on. On the Vortex the mod refuses the game's auto-hover (the Flight Assist hold is the STOVL conversion), so until now it never lit.
- **Colour.** Always green. The game turns it orange above 27 m/s, which is its auto-hover's speed limit and doesn't apply here.
- **" SHP" on the throttle readout** stays as it was (shown once the ship's share is over half).

## How

- `HudShipMatch.cs`: a postfix on `AutopilotIndicator.Refresh`, which the HUD runs every frame. It lights the icon from the STOVL system's ship reference and puts the game's own state back when it lets go.
- `SwivelLink`: two read-only values for it, `ShipFrameLive` (the reference was read in the last half second, i.e. the laws are running) and `ShipFrameBlending`.

## Not verified

- **Not seen in the game.** I read the icon's class from the game code but could not look at the HUD: that this icon is the H in the shield, and that the Vortex's HUD carries it, are both taken from the code (the game shows it for auto-hover, which the Vortex has in vanilla). If nothing lights, the log line `Installed hook: AutopilotIndicator.Refresh` tells whether the hook went in.
- On the deck before lift-off the laws are not flying yet, so the icon comes on at lift-off, not before.

## Not in this build

A nose-down tilt for the forward acceleration was built and part-benched on 18 Nov, then dropped: the F-35B accelerates at a level attitude, on the swivel nozzle and the lift fan's vanes (Tom, 00:24). What the bench showed before it was stopped (two of six jets, plus Tom's): 13.8 t, hover to 39 kt in 6.2 s (0.4.7: 8.9 s; 0.4.6: 6.8 s) at 21° nose-down, height −0.10 / +0.07 m; Buffed 11.9 t, side and forward together, a 0.18 m dip (0.08 before). The forward acceleration stays as in 0.4.7.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.4.8 and `meta.json`; 0.4.7 kept as `Vortex_FS20_FCS.dll.0.4.7.bak`.
- Dev DLL unchanged. No config change.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.4.8-release`.
