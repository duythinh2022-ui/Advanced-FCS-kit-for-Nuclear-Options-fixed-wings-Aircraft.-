# Ifrit FCS 0.7.3

Base: **0.7.2**. One fix: the uneven roll rate at low IAS / low AoA you reported. Patch bump — no new control law, one gate on an existing one.

**Bench-only. Not flown.** Everything below is bench measurement on the calibrated plant; the Ifrit bench is still thin (`Ifrit_bench_calibration_0.5.6.md`), so treat sideslip numbers as directional. **No default changed**, and `PullRollTrade` is still your 0.7.

## The defect
Your report: *"rolling at low ias about under 200kt at low aoa is not even, roll rate just randomly speed up and down"*, and *"it doesn't happen on raptor."* Both correct.

It was the **feed-forward roll trade** (`PullRollTrade`) firing with the pitch stick centred. Its AoA-rate "ask" is the pitch command's distance from the measured flight-path rate:

```
aAskF = |qAskPre - num8|        // num8 = omega.y*57.3 - alphaDot, the velocity-frame pitch rate
```

With the stick in the detent the command is **exactly zero** — `qRaw = sp × qB` sits behind the law's own detent gate (`num9 = Clamp01((|sp| - 0.01)/0.06)`), so `qAskPre` is 0 and that distance collapses to the *measured* rate. Bench, 250 km/h full-stick roll through zero AoA with the pitch stick centred: `sp 0.00, qRaw 0.0, qAskPre 0.0, num8 2.1 → 6.7 → 0.6`. The jet's own settling sweeps the AoA 12.6° → −2° at 2–7 deg/s and the trade read that as a pilot sweep of the same size.

The trade's allowance is `tgt = share × yawCap × cos α / α̇`, so a *small* spurious ask bites hardest where the yaw capacity is small. At low AoA the Ifrit's hand-picked yaw set reads **7–13 deg/s²**, so `tgt` fell to the `PullTradeFloor` of 30 deg/s and recovered as the wander oscillated — the roll-rate limit swinging **30 ↔ 160 deg/s**. That is what you felt.

**Why not the Raptor:** the same spurious firing happens there (11 % of samples, cut min 0.47), but twin rudders keep the capacity high enough that the allowance stays above the authority limit, so the resulting limit only moves 106–127 deg/s. Measured `pSmax` spread in the same regime: Raptor 106–127 (20 %), Revoker 72–128 (78 %), **Ifrit 48–121 (152 %)**.

The 2.5.14 comment in the code blamed "the AoA-G law's own return toward 1 g". That was wrong: the command contributes nothing at all with the stick centred. It is the measured rate leaking into a term that is supposed to be a command.

## The fix
The ask counts only when the pitch command is actually asking for a sweep:

```
bool qSweepAsk = |sp| > 0.04f || prot || protHold || |num34 - qRaw| > 0.05f;
float aAskF = qSweepAsk ? |qAskPre - num8| : 0f;
```

- `|sp| > 0.04` is the law's own detent, written on `sp` directly so it reads the same under the rate law (where `num9` stays 1).
- `prot` / `protHold` — AoA or G protection commanding.
- `|num34 − qRaw| > 0.05` — any bound that *actually bound* the command this tick: the g / AoA-rate clamps, the unload, the protection onset. A push-over recovery with the stick released still trades.

A centred stick with nothing binding does not. A steady rolling pull still trades (the stick is out of the detent) — and once the g has settled the ask is 0 there anyway, as before. Measured state only, no schedule, no new constant.

## Bench
Full-stick roll, pitch stick centred, 1500 m, accelerating (`ifly alt=1500 ias=<v> thr=1 "pitch=0:0,12:0" "roll=0:0,1:0,1.05:-1,14:-1"`). "cut" is `pSmax ÷ (pMax / |cos α|)` — 1.00 means the limit is the authority, not the trade.

| IAS km/h | 0.7.2 cut | **0.7.3 cut** | 0.7.2 samples cut below 0.9 | **0.7.3** | peak β |
|---|---|---|---|---|---|
| 150 | 0.46–1.01 | **0.99–1.01** | 33 % | **0 %** | 3.1° (unchanged) |
| 200 | 0.46–1.01 | **0.99–1.01** | 33 % | **0 %** | 2.6° (unchanged) |
| 250 | 0.46–1.01 | **1.00–1.01** | 33 % | **0 %** | 2.0° (unchanged) |
| 300 | 0.46–1.01 | **1.00–1.01** | 33 % | **0 %** | 1.9° (unchanged) |

Identical to `PullRollTrade=0` in these cases — same `pSmax`, same sideslip to 0.1° — while a full-aft rolling pull still trades hard: the target goes 999 → 74 → 41 → 30 deg/s as the stick comes back (`sp` 0.11 → 0.32 → 0.44 → 0.56).

### Regression set, 0.7.2 → 0.7.3 (peak sideslip, deg)
Everything flat except one case:

| | 0.7.2 | 0.7.3 |
|---|---|---|
| pull-then-pushover+roll 350 | 4.4 | **4.8** |
| pull-then-pushover+roll 450 | 2.9 | 3.0 |
| push+roll 200 / 300 | 3.5 / 1.8 | 3.5 / 1.8 |
| push+roll −55 corkscrew 170 | 3.3 | 3.3 |
| roll-then-push −55 200 | 3.1 | 3.1 |
| push-then-pullthrough+roll 350 / 450 | 2.7 / 8.8 | 2.8 / 8.9 |
| pullthrough, roll released at zero 350 | 2.9 | 2.9 |
| fullaft+roll+rev3s 320 / 500 | 4.6 / 7.6 | 4.6 / 7.6 |
| 65° roll+yaw release 300 | 5.8 | 5.8 |

The 0.4° on pull-then-pushover 350 is inside the 8° margin (`pMean` there 55 → 51, so it is also rolling slightly less). Sweep stop / reversal sets: **byte-identical**. Corkscrews: identical except `roll-then-pull1.0` 350 / 450, which moved 11.0 → 11.5 and 10.7 → 10.2 — mixed, inside run-to-run spread.

That flatness is the point: in every protection case the stick *is* deflected, so the gate is open and the trade behaves exactly as in 0.7.2.

## Rejected on the way
Pointing the trade's yaw capacity at every allocator effector instead of the hand-picked yaw set — the same fix `RevSim` and `PaceReachAll` got in 0.7.0, and the capacity really is undercounted (7–13 where the airframe makes 12–26). It halved the swing on its own but cost sideslip in three pull-through-zero cases: **pull-then-pushover+roll 450 2.9 → 5.7**, push-then-pullthrough+roll 450 8.8 → 10.1, pullthrough-roll-released 2.9 → 3.8. Dropped — the gate alone does the whole job, and loosening genuine protection was not the ask. If you ever want the trade less tight in real rolling pulls, that capacity path is where to look, but it needs the share path (`stopSlipAbsF`) worked out first.

## Not done / known
- **Not flown.** The bench reproduction is a proxy for your complaint, not the complaint.
- **Rate law (`PitchRateCommandOnly` / mode 6) is reasoned, not benched.** The gate is written on `sp` so it applies there too; the bench stub can't set that key, so it is untested.
- The Ifrit is still slower than you want above 15–16° AoA. That is **authority**, not the governor (`pSmax` 86 at 16–22° against 65 at 22–30°) — a separate job.
- `yawCapDbg` is still the hand-picked yaw set everywhere it is used. Known undercount; see above.

## The knobs
Unchanged. `PullRollTrade` (your **0.7**), `RollStopSideslip` (8), `RollPaceRateBlend` (0), `RollPaceRateSlip` (1). A/B: `PullRollTrade=0` still removes the trade entirely — do **not** fly that as a fix, it costs 3.5 → 10.5° on push+roll 200.

## Install
- **Main:** `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 5f3d5f196a9688c6b60c1b7281dcaef0) and `meta.json`, version 0.7.3.
- **Backup:** 0.7.2 kept as `…\Ifrit_KR67_FCS.dll.0.7.2.bak`.
- **Release:** `C:\Games\NO mods\Ifrit-FCS-0.7.3-release`. The NOMM listing in the plugin folder is not updated — your release step.

## First flight — what to look at
1. **The thing you reported:** full-stick rolls at 150–250 kt, pitch stick centred, AoA low. The rate should hold steady instead of surging. If it still pulses, the next suspect is the pace window, not the trade.
2. **Push-over + roll entries at 90–110 kt** — the case the trade protects. Should feel exactly as in 0.7.2; if the sideslip is worse there, the gate is letting something through and I want the log.
3. **A pull-through-zero with the roll in** (push to negative AoA, then pull through with full lateral). Bench says unchanged; this is the one case where the trade used to get spurious help.
