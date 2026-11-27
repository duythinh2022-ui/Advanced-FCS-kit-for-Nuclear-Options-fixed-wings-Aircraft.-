# FS-20 Vortex FCS 0.4.3 (17 Nov 2026)

On 0.4.2. One text change. Compiled only: the bench has no HUD and no ship.

Tom, 22:38: "when i switch it off, instead of the hud message say map, change it to ground".

## Change

The HUD message when the ship-relative STOVL switch (`StovlShipRelativeKey`) is turned off:

| | 0.4.1 - 0.4.2 | 0.4.3 |
|---|---|---|
| Switched on | `STOVL SPEEDS: SHIP RELATIVE` | unchanged |
| Switched off | `STOVL SPEEDS: MAP` | `STOVL SPEEDS: GROUND` |

The same text goes to the BepInEx log line.

Nothing else changed: no law, allocator, config or native change. The source differs from 0.4.2 in that one string and the version number.
