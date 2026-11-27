# J-20B Weilong FCS 0.1.8: CG 0.75 m ahead of the mains, canards 1.25x, roll limit from the surfaces that roll

Base: 0.1.7. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov)
- 11:34: "What other ways to get at least 20% more roll rate up to at 20aoa"
- 11:46: "what happen it the cg is 0.75m ahead of the main gears and maike wingroot x1.5, strakes 1.5. canards 1.25"
- 12:25: "find a way to keep 0.1.7 roll but cg is 0.75m ahead of main gears and making the stability margin thinner"
- 12:29: "do it"

## Changed
| Setting | 0.1.7 | 0.1.8 |
|---|---|---|
| `CgAheadOfMains` | 0.6 | **0.75** (stock J-20B: 0.77 at full fuel) |
| `CanardLiftScale` | 1 | **1.25** |
| `AftBodyLiftScale` (new, 5. Airframe) | (1) | **0** |
| `RollAuthorityAllElevons` (new, 3. Lateral-directional) | (off) | **on** |
| `WingRootLiftScale` | 1.25 | 1.25 (unchanged: its lift acts at the CG on the bench, so it does nothing for stability) |

**Roll limit (`RollAuthorityAllElevons`).**
- At 1 g to ~10 deg the roll rate is the FCS's ceiling, `pFull` = roll authority / roll damping. The surfaces were nowhere near their stops (outer elevons ±13.6 of 35).
- The authority sum read only the `surfaces` list: outer elevons plus a canard share. The inboard elevons are hinge effectors (`hinges` list), so the allocator rolls with them but the sum missed them. The canards are a pitch-only pair, yet they were counted.
- It now counts the hinge elevons and drops the canards. Shared static `FcsController.RollAuthHinges` (default off); other jets are unchanged.

**Aft body (`AftBodyLiftScale` 0).**
- The aft fuselage and tail-cone parts (`fuselage_*`, `tail`) flew a wing-like lift curve with their force points 2.7 / 5.8 m behind the CG.
- Slender-body theory gives the aft body, where the cross-section stops growing, next to no lift.

**Config.** `J20ConfigRevision` goes 4 -> 5. `CgAheadOfMains` and `CanardLiftScale` move once, each only if still at its 0.1.7 default. The log line is `J-20B FCS 0.1.8 defaults: ...`. The airframe changes take effect on the next spawn.

**Also in the source, off by default (from today's study, not used):**
- `Wing1VortexLift` / `Wing1VortexOnset` / `Wing1VortexFull` / `Wing1VortexBreakdownStart` / `Wing1VortexBreakdownEnd`: a leading-edge-vortex increment on the inboard wing panels (WingVortex.cs, new chart family).
- `StrakeVortexScale`: the chines' vortex term x pi.
- Why they're off: on your proposal airframe, wing1 3 / chines 2x took 38 deg to neutral and halved the 22-34 deg instability, but the extra drag cost AoA hold at the end of slow rolls at the limit (34-36 deg).

## Static margin (bench, 500 km/h, surfaces neutral, % of ~5.7 m mean chord, + = stable)
| AoA | 2 | 10 | 22 | 30 | 38 | Pitch moment at 40 / 44 deg (deg/s^2) |
|---|---|---|---|---|---|---|
| 0.1.7 | +24 | +22 | −22 | −30 | +12 | +8 / −29 |
| CG 0.75 alone | +26 | +24 | −19 | −27 | +14 | −16 / −52 |
| **0.1.8** | **+23** | **+21** | −22 | −33 | +13 | +42 / −1 |

- **Not thinner than 0.1.7, only held at 0.1.7's level with the CG forward.** Thinner needed more forward lift. On this airframe every option also added pitch-up near the limit: at +20 % the jet pitches up past 44 deg; at +11 % the natural pitch-up at 40 deg was +116 against about −130 of nose-down authority.
- **Past 44 deg the natural moment is now about neutral** (−1; 0.1.7: −29). Recovery comes from the surfaces: −119 nose-down authority there.

## Bench (0.1.7 vs 0.1.8), 3000 m, throttle 1

**Roll vs the mean AoA during the roll.** Interpolated across 9 pitch-stick settings: roll angle in 1.5 s / max sideslip.

| AoA | 400 km/h 0.1.7 | 400 km/h 0.1.8 | 550 km/h 0.1.7 | 550 km/h 0.1.8 |
|---|---|---|---|---|
| 8 | | | 222 / 3.3 | **279** / 5.4 |
| 12 | 151 / 5.2 | **163** / 4.6 | 193 / 5.8 | **209** / 4.5 |
| 15 | 134 / 7.0 | 138 / **5.9** | 159 / 8.0 | 159 / **4.6** |
| 20 | 102 / 7.3 | 105 / **6.5** | 118 / 7.1 | 116 / **5.1** |
| 25 | 72 / 5.8 | **79** / 5.1 | 88 / 6.1 | 89 / **4.9** |

**1 g roll at 500 km/h:** 215 deg in 1.5 s, peak 198 deg/s -> **304 deg, 250 deg/s** (at `MaxRollRate`). 600 km/h: peak 165 -> 227.

**Pull to 40 deg, 200-600 km/h:**
- 30 -> 39.3 deg in 0.38-0.75 s (0.1.7: 0.43-0.80), peak 40.0-40.3.
- Nose-up authority at 40 deg: 41-113 -> 50-151 deg/s^2.

**Full aft + full roll from 3.15 s:**
- Roll angle at 250 / 400 / 625 km/h: 96 / 112 / 141 -> 96 / **115 / 149** deg.
- AoA band 39.2-40.5 (0.1.7: 39.6-40.6). Worst miss from 40 deg: 0.60 -> **0.76** (the dip to 39.2 at 250 km/h).

**Roll and release at the limit, 200-625 km/h:** worst 0.49 -> 0.54, roll 56 deg mean (same).

**Full push:** 200 km/h -30.0. At 300 km/h the jet reaches -29.5, then holds about -10 for 1 s (G-limited, same as 0.1.7).

**Other checks:**
- Hands-off at 500 km/h: AoA 2.3, the same.
- 900 km/h pull: 9.33 -> 8.96 g peak (G-limit overshoot smaller).
- Engine-out pull: AoA 40.4, sideslip 0.6.
- Takeoff: nose-off 4.10 -> 4.18 s, lift-off 4.73 s (same).

## Corrections to what I said in chat
- `WingRootLiftScale`'s description ("force points at the CG") is right; I called it wrong at 11:43. Its lift change makes about 0 pitching moment on the bench.
- Your 11:46 proposal does not cost 6-15 % of roll at 20-25 deg. That came from comparing at equal stick, where 0.1.7's AoA sagged during the roll. At equal mean AoA the variants are within ±5 %.

## Shared code (port to other jets on the next shared sync)
- `FcsController.RollAuthHinges` (default off): use it on jets with hinge elevons (J-20B) or a pitch-only canard pair.
- `Strake.Kp` / `Strake.Kv` are now statics (same values).
- StallStagger family `WingVortex` (off unless `Wing1VortexLift` > 0).
- `Registry.J20AreaScale` has the aft-body case (J-20B only).
