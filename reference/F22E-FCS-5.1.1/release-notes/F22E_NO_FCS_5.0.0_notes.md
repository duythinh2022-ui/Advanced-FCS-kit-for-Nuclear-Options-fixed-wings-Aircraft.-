# F-22E Strike Raptor FCS 5.0.0 (Nuclear Option)

Base: 4.8.17. **Bench-checked, installed, not flown.**

Asked for (26 Nov 18:19): "build this allocator and bump to 5.0.0 for raptor" (design: `claude/F22E_pair_allocator_design_2026-11-26.md`). Also (18:29): keep it cheap on CPU in both C# and Rust, without making the flying worse.

Decided (19:20): ship with the search **off** by default, and A/B it in flight.

## In short
- **The pair-complete allocator is in, in C# and in the native core. Dev key `PairAllocator`, default off.** With it off, 5.0.0 flies exactly as 4.8.17: the full bench suite gives 119 of 119 per-case files identical to the real 4.8.17 build.
- **On the bench it did not fix the case it was built for.** Your 17:40 log was replayed on the game's motion with TVC off. With 4.8.17's slope rule switched off, the 234 s pose stays stuck with the search on, the same as without it. Only the 4.8.17 rule frees it.
- **The bench can't judge that moment.** In the game the allocator was missing its roll target by 110-243 deg/s^2, which looks like a search trap. On the bench the same pose comes from a target of the opposite sign that it mostly meets: the bench's model of the stabs there is not the game's. Flight is the test.

## What the allocator does (with `PairAllocator` on)
After its usual stages (Gauss-Newton steps, per-surface pass, the 1.8.18 stab/nozzle search):
- it tries every pair of table points of the two stabs that their servos reach within `PairAllocatorHorizon` (0.1 s), plus where each one is now, with everything else held;
- the judge is the allocator's own cost: moment error, regularisation and move cost, pair costs, roll- and pitch-fight costs;
- it keeps a pair only if that lowers the cost, then refines the best cell per stab with a parabola (as the per-surface pass does);
- it repeats until nothing moves (at most 3 passes).

At the end no move of one or both stabs lowers the cost. It runs last in each solve, including the capacity estimator's probe solves, so they measure the same allocator that flies.

## Dev keys (`4. Gains`, all take effect at once)

| Key | Default | |
|---|---|---|
| `PairAllocator` | **false** | true = the search on |
| `PairAllocatorBlocks` | 1 | 1 = stab L + R. 2 = also each flaperon pair, the rudder pair, each stab + nozzles, each stab + same-side flaperons |
| `PairAllocatorTrueCurves` | false | stabs judged on their true pitch curve (past the peak) instead of the envelope |
| `PairAllocatorHorizon` | 0.1 s | how far from where each stab is the search looks |
| `PairAllocatorReplacesClamp` | false | true = the two-zone out-zone clamp off while the search is on (the design's goal) |

The startup log line now begins `F-22E 5.0.0 pair allocator off (4.8.17)` or `on, blocks 1 …`.

## Bench, search ON (world: two-zone config, 29.2 t, stab strength 0.85)

**The design as written (all blocks, true curves, clamp off).** Pull-release-roll at 45 deg, TVC off, CG 0:

| | 4.8.17 | design |
|---|---|---|
| Roll rate | 95.7 deg/s | 102.9 deg/s |
| Peak sideslip | 7.9 deg | **16.3 deg** |

- **Clamp off fails.** The sideslip doubles, so the clamp stays: `PairAllocatorReplacesClamp` false.
- **The extra blocks break your TVC rule.** They move the TVC-on stab trim at 50 deg AoA from +1.7 to +8 deg (stabs taking pitch the nozzles carried). Blocks = 1.
- **True curves:** also move high-AoA trim (+1.7 -> +5.1 deg). Off.

**The shipped "on" setting (stab pair, envelope, clamp kept) against 4.8.17.** Most cases within a few tenths; the largest changes:

| Case | 4.8.17 | search on |
|---|---|---|
| Full-aft roll reversal 500 km/h (TVC on), sideslip | 7.0 | **11.7** |
| One fin, held push-over 600 km/h, sideslip | 16.6 | **20.4** |
| One fin, released push-over 600 km/h, sideslip | 16.7 | 12.7 |
| Idle pull-release-roll 45 deg (TVC on, CG 0), sideslip | 10.2 | 3.5 |
| Same, roll rate | 45.9 | 44.4 |

Units: deg and deg/s.

- In the 500 km/h reversal the commands are the same (all surfaces at their stops). The search had the surfaces in a different place when the reversal began, and the roll built faster than the yaw.
- Twitch, stab command reversals per second over the battery: unchanged (18.5 -> 18.5-18.8 TVC off, 17.7 -> 18.4-18.6 TVC on).

**234 s, 17:40 log, motion pinned to the game's** (AoA, sideslip, TAS, p, q, r), TVC off:

| Variant | Right / left stab at 234.0-234.4 s |
|---|---|
| 4.8.16 rule | -28..-30 / +15..+22 (stuck, as in the game) |
| 4.8.16 rule + search | the same, stuck |
| 4.8.16 rule + search, true curves | the same, stuck |
| No clamp + search, true curves | the same, stuck |
| 4.8.17 | freed |

## CPU (bench, one thread, every allocator solve incl. the estimator's probes, us per tick)

| | C# | native (game default) |
|---|---|---|
| 4.8.17 / search off | ~1330 | ~390 |
| search on, blocks 1 | ~1440 (+8 %) | ~415 (+6 %) |
| search on, blocks 2 | ~1580 (+19 %) | ~545 (+38 %) |

- **Off costs nothing:** the search isn't entered.
- **On:**
  - each surface's candidates are worked out once per pass;
  - a candidate pair costs about ten float operations, and its remaining terms are added only while it can still win;
  - every term is >= 0, so skipping can't change the pick (exact: the same answers as the full evaluation);
  - nothing is allocated per solve.

## Native core
- **ABI 4002000 -> 5000000** (effector record 640 -> 780 bytes, solve record 184 -> 228). A loose old `F22E_FcsCore.dll` is refused and the embedded one is used.
- **Native = C# bit for bit on the bench:** 35,457 solves compared one by one across every setting, TVC on and off.
- **Windows core:** Rust 1.97.1 with build-std, sha256 `40311206ba44…f94ea`, embedded byte for byte in the plugin. Its exports and imports are as before. **Not run on Windows or Mono here** (Wine wouldn't start in this container): the log line says `native allocator core …` when it loads. If it says it fell back, the C# allocator runs, which is the same flying.

## Telemetry (new columns after `capStabTau`)
- `pairMv`: search moves since the last row.
- `oppN`: surfaces whose moment opposes the allocation's demand on every axis where the demand is > 15 deg/s^2.
- `stabSideL` / `stabSideR`: -1 / +1 = commanded past the true-pitch extreme on the nose-down / nose-up side, 0 = between.
- `sideFlip`: side changes so far.

## A/B in flight
1. Fly as installed (search off = 4.8.17).
2. Set `PairAllocator = true` in the Dev cfg (takes effect at once) and repeat the 17:40-style roll: full right roll released from a ~35 deg pull, TVC off, letting the AoA bleed to 0.
3. Watch `stabSideR`, `oppN` and `unmetX` in the log, and the 500 km/h full-aft reversal for sideslip.

## Install
- **Plugin:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 5.0.0 + `meta.json`; 4.8.17 kept as `.4.8.17.bak`.
- **Dev DLL:** unchanged; the new keys appear by themselves.
- **Mod manager:** update the version there, or its modpack may put 4.8.17 back.
- **Going back:** `PairAllocator` false is already 4.8.17; or reinstall the `.4.8.17.bak`.

## Files
- `F22E-StrikeRaptor-FCS-5.0.0.zip`: the player zip (DLL with the native core inside, meta.json).
- `F22E-StrikeRaptor-FCS-5.0.0-Optional-Dev.zip`: the dev DLL (unchanged).
- `F22E_FCS_5.0.0_src.zip`: plugin source and the native core.
- `F22E_FCS_5.0.0_src_bench_tools.zip`: the whole tree with the bench (`replay … pinAll=1`, `timing=1` pair stats) and the battery tools.
- `F22E_500_vs_4817.diff`, `meta.json`, the DLL.
