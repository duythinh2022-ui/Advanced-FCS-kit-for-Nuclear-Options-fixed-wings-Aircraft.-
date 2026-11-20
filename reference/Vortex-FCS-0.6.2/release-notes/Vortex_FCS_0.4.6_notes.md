# Vortex FCS 0.4.6: STOVL ship speed match on by default, blended, with its own toggle

17 Nov 2026. Built on 0.4.5 (the airbrake root-cause fix), which is carried unchanged. **Compiled only:** the bench has no ship, and nothing here has been flown.

Tom:
- **23:19:** "replace the vanilla speed match for vortex".
- **23:26:** "make it blends like before and i want it as an optional separate toggle to turn it on and off" / "but on by default and auto match ship".

Background: `Vortex_ship_relative_STOVL_findings_2026-11-17.md`.

## Vanilla (game code)

- **The keybind:** the `Flight Assist` key: tap toggles the pitch limiter, hold toggles auto-hover. On the Vortex the mod takes the hold as the STOVL conversion and refuses the game's auto-hover, so vanilla's speed match never runs on this jet.
- **The match:** `AutoHover.CheckNearbyShip` takes the nearest friendly ship within 500 m every 3 s, copies its velocity, and steps in or out at 500 m.
- **No keybind of its own:** it is part of auto-hover.

## Change

| | Vanilla auto-hover | 0.4.1 – 0.4.5 | 0.4.6 |
|---|---|---|---|
| On | with auto-hover (Flight Assist hold) | `StovlShipRelative`, **default off** | `StovlShipRelative`, **default on** (moved once if still false) |
| Toggle | — | `StovlShipRelativeKey` (unbound) | same key, same HUD message (`STOVL SPEED: SHIP RELATIVE` / `GROUND`) |
| Range | 500 m, all at once | 800 m, faded 800 → 600 m | 800 m, faded 800 → 600 m |
| Ship velocity | copy, up to 3 s old | read every tick | read every tick |
| Change of nearest ship | step | **step** | **blended** |
| Range edge, switch | step | ship share ≤ 1/3 per s | reference moves ≤ its own size in 3 s |
| Fresh STOVL conversion near a ship | last check | faded in from zero over 3 s | **takes the ship's speed at once** (looks up immediately) |
| Switched on in flight | — | waits up to 3 s for the next ship check | **looks up at once**, then blends in |
| HUD | green `H` | ` SHP` on the throttle readout | same |

**Migration:** `GovernorRevision` 7 moves `StovlShipRelative` false → true once. Turn it off afterwards (key or config) and it stays off.

**What uses the ship reference** (unchanged):
- the throttle's ground-speed command;
- the hover hand-overs;
- the released-stick position hold.

## Things to know

- **The key is unbound by default.** Set `StovlShipRelativeKey` in `Vortex_FS20.FCS.cfg` (`1. General`), e.g. `RightBracket`. It's a BepInEx key, not in the game's controls menu.
- **Closing on a moving ship:** the lever's `+xx KT` becomes relative to the ship as it fades in between 800 and 600 m, so bring the lever back as you close.

## Not verified

- **No ship on the bench**, so the ship path is compiled only. The bench STOVL sets don't use this code (bench hardware returns zero surface velocity), so they are unchanged by construction; not re-run.
- **Build:** the toolchain rebuilds the shipped 0.4.5 DLL with the same IL length for every method. Same native core as 0.4.5.
