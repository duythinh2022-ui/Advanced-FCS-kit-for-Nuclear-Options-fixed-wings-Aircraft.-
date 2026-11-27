# Revoker FCS 0.8.2: roll / yaw tables pass through the live moment (stick-released creep under a lateral trim)

Base: 0.8.1 (rebuilt here; IL identical to the installed DLL apart from the access-checks attribute). Patch bump: a fix, no tuning value changed.

**Status:**
- Compiled against the game's assemblies.
- Benched: creep cases, and the full suite with 4 samples per variant, plus speed-perturbation checks on the 5 items it flagged.
- **Not flown.** 0.8.1 is kept as `.dll.0.8.1.bak`.

## Tom (5 Nov, 01:13)
"apply the indi fix to the other 2" (the Ifrit's 0.8.3 / 0.9.1 fixes for the roll creep with the stick released).

## The problem (same as the Ifrit's)
- **INDI** builds each tick's target on the effectors' *live* moment (`Mnow`, the full aero evaluation at the surface's actual deflection).
- **The allocator** answers from a *table* per surface, linear between grid points.
- **The pitch column** was already made to agree with the live value. **The roll and yaw columns** were not.
- Wherever table and live value disagree at the surface's position, the difference is added to every tick's target as if the pilot had asked for it. The rate loop settles with a standing error of (mismatch) / (I·K): a slow creep.
- **Symmetric flight hides it:** left and right errors cancel. Anything that puts the two sides at different deflections (a lateral load, a missing aileron, wing damage) exposes it.

## The change (`Effector.MAt` / `SetLiveNode`; `SurfaceEffector` and `HingeFlapEffector`)
Each tick, the surface's live point becomes **an extra node of its roll and yaw columns**.
- The table passes through the live moment at the surface's position.
- It is unchanged at every grid point; only the cell the surface sits in is bent to pass through the live value.
- The pitch column is left exactly as it was.

This is not the Ifrit's 0.8.3 form, which shifted the whole column by the error at the surface's position. That carries the error to deflections far from the surface. On the 0.6.0 base, the whole-column shift moved 3 regression cases beyond noise (the worst +1.1° sideslip in pull-then-roll at 400 km/h); the live node moved none of them.

The inboard hinged elevons (`HingeFlapEffector`) have their own table and get the same node. The splits, the yaw nozzle and the TVC differential already agree with their live value by construction.

`AnchorLateral` (internal static, default on) is a bench switch only, not a config key.

## Bench: stick released, last 8 s of 16 (0.8.1 → 0.8.2)
`nuR` is the roll loop's standing demand (°/s²); it should sit near 0 once the jet holds its command.

| Case | Roll rate | Bank drift | `nuR` | Table error removed (`anchR`) |
|---|---|---|---|---|
| R aileron gone, 350 km/h | 0.00 → 0.00 °/s | −0.2 → −0.1° | 0.16 → 0.02 | −0.14 |
| L wing at 20 HP, 300 km/h | 0.02 ± 0.04 → **0.00** | 0.3 → 0.0° | −0.21 → −0.05 | 0.17 |
| Roll 40 / yaw 10 °/s² load, 280 km/h | −0.07 ± 0.05 → **0.00** | −0.1 → 0.1° | 0.19 → 0.04 | −0.13 |
| R aileron gone + yaw 30 °/s² load, 300 km/h | jitter ± 0.09 → ± 0.03 | (commanded −0.1 °/s) | 0.72 → 0.05 | −0.68 |
| Roll 20 / yaw 6 °/s² load, full AB | 0.04 ± 0.05 → **0.00** | 0.5 → 0.0° | −0.30 → 0.03 | 0.31 |

On this jet the creep is small, under 0.1 °/s. The mismatches are 0.1–0.7 °/s² against the Ifrit's 3.8.

## Regression suite, 4 samples per variant (1500 / 1510 / 1490 / 1520 m)
| Block | Comparisons | Beyond noise |
|---|---|---|
| sweep (32 stops / reversals) | 158 | 3, all branch points (below) |
| reg (20) | 120 | 2, a branch point (below) |
| cork (20) | 120 | 0 |
| dmg (32) | 210 | 0 |

- Sweep mean roll 99.8 → 99.6 °/s; stop and reversal sideslip unchanged (2.23 / 2.65°).
- **The 5 flagged items are roll-rate values in cases that don't move with altitude but flip between two outcomes with a 1 km/h change of entry speed.** Re-run at entry speed ±3 km/h (7 per variant):

| Case | 0.8.1 | 0.8.2 |
|---|---|---|
| Sweep 300 km/h, 0.40 pull, roll rate at the event | 87–98, mean 94.7 °/s | 92–98, mean 95.0 |
| Sweep 500 km/h, 0.70 pull, reversal time | mean 0.74 s | 0.72 s |
| Pull-then-push-over + roll 350, peak / mean roll | 125.1 / 99.9 °/s | 124.4 / 98.7 |

## Not in this build: the Ifrit 0.9.1 fight-cost fix
- **The problem exists here too.** The released-stick roll-fight cost can stall the allocator on a lateral trim.
  - Bench case: 60 °/s² yaw and −15 °/s² roll load at 400 km/h.
  - 0.8.1: a standing 1.8 °/s roll error, with 7 °/s² of roll left unmade for the rest of the run.
- **The Ifrit's two-stage solve, ported as-is, makes this jet hold crossed sets.**
  - Its stage 1 solves without the fight cost from the last allocation and can drift into a crossed set. Stage 2 is then pinned to that.
  - Seen: a 50 °/s² yaw load at 250 km/h held the canards at 19.8 / −6.5° against the pitch nozzle and the ailerons, and the jet accelerated 44 m/s less in 30 s.
- **Two restructured versions still let a transient cross the surfaces and keep them there.** Worst: canard at 23° for 24 s with wing damage plus a yaw load, where 0.8.1 never crosses.
- **The fix that would hold:** the as-tuned solve keeps its own allocation track (warm-started from its own previous answer), and the output is that allocation plus only the moment the fight cost withheld. The reach pass and the capacity estimator's probes call the solver too, so it needs care. Details in `claude/FightNullSpace_port_findings.md`.

## Telemetry
New columns after `betaCg`: `nuR`, `nuY` (rate-loop demand), `anchR`, `anchY` (table error at the live point, before the node, summed), `unR`, `unY` (what the main solve left unmade; °/s²). With the stick centred, `nuR` / `nuY` should sit near 0. A standing `unR` there is the fight-cost stall above.

## Bench fix
`bench/sim/IfritTest.cs`: the FCS log of a bench flight is now closed, so its last row can no longer be cut mid-number.

## Install
- `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 `1611b84e5cbbad8f885bb96dfe8c85c9`) and `meta.json` 0.8.2; 0.8.1 kept as `.dll.0.8.1.bak`.
- Release: `C:\Games\NO mods\Revoker-FCS-0.8.2-release`. The source zip includes the bench.

## For the flight
Anything that trims the two sides differently, with the stick released: a missing or damaged surface, or asymmetric stores. Watch the bank and heading hold. Roll and stop feel should be unchanged from 0.8.1.
