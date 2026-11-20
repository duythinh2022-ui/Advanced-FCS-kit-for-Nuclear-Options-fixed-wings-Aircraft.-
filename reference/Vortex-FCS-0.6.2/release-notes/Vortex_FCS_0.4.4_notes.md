# FS-20 Vortex FCS 0.4.4 (17 Nov 2026)

On 0.4.3. One text change. Compiled only: the bench has no HUD and no ship.

Tom, 22:40: "Why speed plural?", then "Do that" (singular on both messages).

## Change

The HUD message of the ship-relative STOVL switch (`StovlShipRelativeKey`):

| | 0.4.3 | 0.4.4 |
|---|---|---|
| Switched on | `STOVL SPEEDS: SHIP RELATIVE` | `STOVL SPEED: SHIP RELATIVE` |
| Switched off | `STOVL SPEEDS: GROUND` | `STOVL SPEED: GROUND` |

The same text goes to the BepInEx log line.

Nothing else changed: no law, allocator, config or native change. The source differs from 0.4.3 in that one line and the version number.
