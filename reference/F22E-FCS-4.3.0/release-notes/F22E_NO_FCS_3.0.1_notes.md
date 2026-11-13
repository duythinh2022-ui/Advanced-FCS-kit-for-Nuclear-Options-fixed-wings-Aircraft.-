# F-22E FCS 3.0.1: roll / yaw tables pass through the live moment (engine-out / damage creep)

Base: 3.0.0 (rebuilt here byte-identical to the installed DLL). Patch bump: a fix, no tuning value changed. The Dev DLL is 3.0.1 too (version string only).

**Status:**
- Compiled against the game's assemblies.
- Benched: creep cases, and the full suite with 4 samples per variant, plus speed-perturbation checks on the items it flagged.
- **Not flown.** 3.0.0 is kept as `.dll.3.0.0.bak`.

## Tom (5 Nov, 01:13)
"apply the indi fix to the other 2" (the Ifrit's 0.8.3 / 0.9.1 fixes for the roll creep with the stick released).

## The change (`Effector.MAt` / `SetLiveNode`, `SurfaceEffector`)
- **INDI** builds its target on the surfaces' *live* moment; **the allocator** answers from a table, linear between grid points.
- **The pitch column** already agreed with the live value; **the roll and yaw columns** did not.
- Each tick, the surface's live point now becomes **an extra node of its roll and yaw columns**. The table passes through the live moment at the surface's position and is unchanged at every grid point.
- Without it, the table error at the surfaces' positions is added to every tick's target like a pilot input. With an engine-out or damage trim (left and right surfaces at different deflections), the errors stop cancelling and the rate loop holds a small standing error.
- This is the Ifrit's 0.8.3 fix in its local form: a node, not a whole-column shift. The shift carries the error at the surface's position to deflections far from it.
- `AnchorLateral` (internal static, default on) is a bench switch only.

## Bench: stick released, last 8 s of 16 (3.0.0 → 3.0.1)
| Case | Yaw rate − its command | Roll rate |
|---|---|---|
| Engine out at 550 km/h, AB | −0.08 → **0.00** °/s | 0.01 → 0.01 |
| Engine at condition 0.4, 275 km/h | −0.05 → **0.00** | 0.01 → 0.02 |
| Engine at condition 0.3, 450 km/h | −0.03 → **0.00** | 0.01 → 0.01 |
| Engine out at 350 km/h, AB | −0.01 → 0.00 | 0.03 → 0.03 |
| R aileron gone, 300 km/h | 0.02 → 0.00 | 0.00 → 0.00 |

The F-22E barely crept: its rudders and nozzles take the engine-out yaw almost symmetrically.

## Regression suite, 4 samples per variant (1500 / 1510 / 1490 / 1520 m)
| Block | Comparisons | Beyond noise |
|---|---|---|
| reversals (12) | 96 | 2, both better: sideslip before the reversal at 0.5 pull 6.0 → 5.1°; new-direction peak roll 208 → 222 °/s |
| cork (20) | 220 | 1, a branch point (below) |
| reg (18) | 160 | 1: `pace` (how much the pace window held the roll back) in push+roll 300, 27.8 → 24.8; sideslip and roll unchanged |
| dmg (34) | 257 | 2, one case, a branch point (below) |
| utilisation invariant | | identical (the same 6 known FAIL rows as 3.0.0) |

**Two flagged cases flip between outcomes with a 1 km/h change of entry speed**, which the altitude samples don't move. Re-run at entry speed ±4 km/h (7 per variant):

| Case | 3.0.0 | 3.0.1 |
|---|---|---|
| Push-over, roll held, no fins, 560 km/h: peak sideslip | 17.7–22.1, mean 19.3° | 17.4–22.0, mean 19.7° |
| Corkscrew entry, full pull and AB, 630 km/h: peak sideslip | 7.5–11.4, mean 8.80° | 7.5–10.2, mean 8.81° |

## Not in this build: the Ifrit 0.9.1 fight-cost part
- **Not needed here.** That fix keeps the released-stick roll-fight cost from holding back the trim. On the F-22E that cost is off with the stick released (`RollFightReleased` false since 2.3.2), and the pitch-fight cost only acts during and just after a roll.
- **Ported anyway, it would be a no-op with a side effect.** The pitch-fight gate never reaches exactly 0, so it ran on every settled tick and shifted the allocation slightly (a 0.1 °/s roll blip with the right aileron gone).

## Telemetry
New columns at the end of the block, before the per-surface columns:
- `nuR`, `nuY`: what the roll / yaw rate loops ask for.
- `anchR`, `anchY`: the table error at the live point before the node, summed.
- `unR`, `unY`: what the main solve left unmade.

All in °/s². With the stick centred, `nuR` / `nuY` should sit near 0.

## Install
- `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 `70d16394318ccef53b75873ca178a5a6`) and `meta.json` 3.0.1; 3.0.0 kept as `.dll.3.0.0.bak`.
- `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.0.1 (md5 `2eb21280acafcbf4dc9ed828b7d90687`); 3.0.0 kept as `.dll.3.0.0.bak`.
- Release: `C:\Games\NO mods\F22E-FCS-3.0.1-release`. The source zip includes the bench.
