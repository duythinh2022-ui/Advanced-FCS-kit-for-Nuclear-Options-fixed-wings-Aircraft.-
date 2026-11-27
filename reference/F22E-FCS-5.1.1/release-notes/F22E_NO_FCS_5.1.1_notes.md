# F-22E Strike Raptor FCS 5.1.1: horizon objective without the chatter

5.1.0 fixed the stuck reverse-roll pose but roughly doubled stab command reversals in flight (22:25 log; push+roll at 160 s: left stab command 35 → 18 → 35 → 23 → 0 → 35 → −4 within 0.7 s). 5.1.1 keeps the escape and removes the chatter. The fix is a change in what the horizon objective is asked to judge. It adds no threshold or latch.

## What changed (Allocator.HorizonPick, C# and Rust core)

1. **A branch candidate has to cross a fold.** The stab-pair scan now offers a candidate only when one of the stabs, going from the destination plan's command to the candidate's, has a moment that turns back on itself: its projection on the chord stops being monotone, which for a stab means crossing a lift peak. The refined result must still be across a fold before it can be taken (`CrossesFold`). Inside one side of a fold, the local stages already find the best point. 5.1.0 accepted any pair more than one grid step away. Such a pair is a coarse grid point in the same basin, and with cost differences of 0.001–0.02 it won or lost every tick. That was most of the dithering.
2. **The destination plan versus the reach plan is decided by the 2.42 rule again** (2·jB ≤ jE + jD). Both plans are in the basin the stabs are already in. Over the horizon, their costs differ by little more than the linear model's noise. Judging them by horizon cost (5.1.0) caused about 45 of the extra reversals on its own.

The horizon cost, the 1.0 s horizon, the linear answer of the other surfaces during a trip and the refine (pinned, then freed) are all unchanged from 5.1.0. The native ABI went to 5101000 with no change to the struct layout.

Tried and rejected:
- Pricing the trip with the other surfaces held rather than answering linearly: on its own it took reversals from 228 to 200, and it gave nothing extra once 1 and 2 were in.
- Sliding each stab's table with the rate of change of its local AoA: the trap pose sits at the stab's travel limit, where the table can't extend, so this didn't help.

## Bench (C#; native core bit-identical)

**Chatter repro** (prel: 300/380/470 km/h × 0.8/1.5 s × push/pull × throttle 1/0.5, roll held 4 s; raw stab command reversals, summed over 24 cases):

| | 2.42 pick (≈5.0.x) | 5.1.0 | 5.1.1 |
|---|---|---|---|
| raw reversals | 121 | 228 | **108** |
| max slip | 6.1 | 5.7 | 5.9 |

Three cases are 3–5 reversals worse than 2.42 and several are better. The 380 km/h 1.5 s push cases improve the most: slip goes from 5.0 to 1.5 and from 6.1 to 2.3.

**Trap replays from the game state** (pinned to the log, then free; peak 0.5 s unmet roll/yaw demand):

| replay | 2.42 pick | 5.1.1 |
|---|---|---|
| 21:21 from 24.3 s | 238, stuck | 15 |
| 21:36 from 72.98 s | 327, stuck | 64, never enters |
| 20:10 from 396.4 s | 244, stuck | 41 |
| 21:36 from 166.6 s | doesn't reproduce in this setup | 3 |

**Stuck repro** (grel: 380/470/540 km/h × pull 0.7/1.2 s × throttle 1/0.6, full roll 5 s, TVC off):
- 2.42 pick: stuck in g540_1.2_0.6 (126 reversed-split rows, held until roll release).
- 5.1.1: the same case enters at body AoA ≈ 1° and **escapes after about 0.5 s** (82 rows; p dips 190 → 161, then recovers to 177). It is not prevented. The other 11 cases are clean.

**Battery** (rev/slow/push/proll/half/pull × 3 CG, TVC off) against 5.1.0 with the same command:
- Roll rate: mean +0.1 deg/s, range −4.9 to +7.6.
- Slip: mean +0.2°, worst +1.9 (proll45 at CG −0.05).

**Suite** against 5.1.0:
- rev, reg and util are within noise.
- One regression in the damage suite: the no-fin pushover at 560/600 km/h goes from 7.1/21.9° slip to 10.3/27.8°. These are the 5.0.x values; 5.1.0's horizon D/B judgement happened to help in those cases.

**Native vs C#:** identical CSVs on all 36 closed-loop runs (prel 24 + grel 12), with 0 fallbacks.

**CPU:** unchanged (suite wall time 2:22 vs 2:23). The fold check is O(N) on the one best candidate.

## Not verified

No flight yet. Things to look for in the next log:
- Stab command reversals back near 5.0.2's 18–26/min.
- `hzBranch` firing rarely, only in real traps.
- Whether the brief entry seen at 540 km/h shows up in the game.

## Files

- `F22E-StrikeRaptor-FCS-5.1.1.zip`: player install (DLL with the native core embedded, plus meta.json)
- `F22E-StrikeRaptor-FCS-5.1.1-Optional-Dev.zip`: dev unlock, unchanged from 5.1.0
- `F22E_FCS_5.1.1_src.zip`, `F22E_FCS_5.1.1_src_bench_tools.zip`, `F22E_511_vs_510.diff`
- DLL sha256 af07a5a03dd902200e50c3a4a4558d3bdacd06c1b5b9f9f9652cdb7cc7e917cf; native core 3629970b…4fba50
