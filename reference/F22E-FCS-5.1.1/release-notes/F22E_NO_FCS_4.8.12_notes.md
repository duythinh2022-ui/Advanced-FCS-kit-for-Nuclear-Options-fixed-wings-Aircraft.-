# F-22E Strike Raptor FCS 4.8.12 (Nuclear Option)

Base: 4.8.11. Bench only; not flown yet.

## What changed
- **Whole-stab stall at 20 deg body AoA** (two-zone, was 25). `TwoZoneStabStallAoA` 26.5, `TwoZoneStabCLScale` 1.36: the same peak force as 4.8.11, with the lift slope 25 % steeper. Both values are forced once over your dev file (AirframeRevision 36).
- **Body couple refit (fore/aft zero-net-lift offset tables).** These are the 4.8.9 tables plus a low-AoA part (0-14 deg) that puts back the instability the steeper stab took away.

## Why it isn't refit to the exact 4.8.11 trim curve
The first attempt matched 4.8.11's stabs-neutral pitch curve exactly from -24 to 36 deg. Trim was identical, but the no-TVC release-and-roll case lost a third of its roll rate (139 -> 91 deg/s at 36, sideslip 3.2 -> 8.6; doc `claude/F22E_stab20_refit_bench_2026-11-26.md`).

The reason:
- The most nose-down the stabs can make at a given AoA is set by the body's own moment plus the stab's peak lift. The stab's lift curve does not change it.
- With a 20 deg stab, being trim-free at 20 deg means the stab already carries close to its peak lift there. That costs about 12 deg/s^2 of nose-down margin at 16-22 deg.
- A fast roll at those AoAs needs that margin: the roll's inertial pitch-up has to be held.

So 4.8.12 keeps 4.8.11's body moment from 16 deg up, which keeps the nose-down margin the same. That costs some trailing-edge-up trim there instead.

## Trim (static, both stabs, no thrust; + = trailing edge up)
| AoA | -10 | 0 | 4-12 | 14 | 16 | 18 | 20 | 22 | 24 | 28 | 30 | 36 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 4.8.11 | -5.6 | 0 | -0.3..-1.4 | -1.7 | -1.5 | -0.4 | +1.2 | +2.8 | +4.5 | +8.2 | +9.2 | +12.4 |
| 4.8.12 | -6.1 | 0 | -0.3..-1.0 | -0.3 | +0.9 | +2.4 | +3.9 | +5.6 | +7.2 | +10.7 | +11.7 | +13.3 |

- The trim-free band (within +-2 deg) now ends at about 17 deg (4.8.11: 20).
- The low-AoA instability is kept up to 12 deg. 12-16 deg is a stable step; 16-28 deg is stable as before.

## Nose-down margin (the most nose-down the stabs can make from trim, deg/s^2, 110 m/s)
| AoA | 12 | 14 | 16 | 18 | 20 | 24 | 30 | 36 |
|---|---|---|---|---|---|---|---|---|
| 4.8.11 | -53 | -45 | -40 | -39 | -41 | -44 | -50 | -48 |
| 4.8.12 | -46 | -41 | -39 | -40 | -41 | -45 | -49 | -47 |

## Bench (4.8.11 -> 4.8.12)
**No TVC**
- Rev roll at 30/36/45/50: 67.9/60.8/57.8/53.0 -> 66.2/61.5/57.7/52.7.
- Push in 1 s from 36/45: -33.0/-30.4 -> -33.6/-31.1.
- Pull, release, then roll (proll) at 36/45: 139/111 -> 138/104 deg/s. Sideslip 3.2/6.7 -> 2.1/7.0.
- The same at idle (prolls) at 36: sideslip 10.2 -> 13.0.
- Stab trim at the 30/36/45/50 limit: +10.7/+12.8/+17.3/+22.7 -> +13.1/+15.3/+16.4/+21.7.

**TVC**
- Rev roll rates the same.
- Rev sideslip at 30/36: 2.0/1.8 -> 5.3/4.4.
- Pull at 45: sideslip 7.6 -> 4.2.
- proll the same.

**Low/mid AoA battery:** within 1-5 deg/s of roll rate and 1 deg of sideslip. Pushes are the same or slightly faster.

**Corkscrew (no TVC)**
- 220 km/h: about the same.
- 300 km/h: the pitch-over is faster (qT to -25 at 1.32 vs 1.62 s), with held-push sideslip 3.2 -> 7.5.

**Stuck-split hold test** (no TVC, 320 km/h, 14 s at the 28/32/35 deg limit): no split (0.0 deg). Stab trim is about 2 deg more trailing-edge up.

## Going back to 4.8.11
1. Reinstall the 4.8.11 DLL (`.4.8.11.bak` in the plugin folder).
2. In the dev cfg, set `TwoZoneStabStallAoA` 33 and `TwoZoneStabCLScale` 1.09. The migration changed them, and 4.8.11 reads the same keys.
