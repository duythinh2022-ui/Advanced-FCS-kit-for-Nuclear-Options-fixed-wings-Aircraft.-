# Revoker FCS 0.4.2

Base: **0.4.1**. The same single fix as Ifrit 0.7.3 — the uneven roll rate at low IAS / low AoA. Patch bump; no new control law, one gate on an existing one. **No default changed**, `PullRollTrade` is still your 0.7.

**Bench-only. Not flown.**

## The defect
You reported it on both jets: *"rolling at low ias about under 200kt at low aoa is not even, roll rate just randomly speed up and down."* On the Revoker the roll still felt fast, but the log has the same signature — `pSmax` 91→110→73→81→120→68→58 deg/s in that regime with `paceHold` ~0, i.e. the **rate limit itself** swinging while `pMax` rises smoothly through all of it.

Cause: the feed-forward roll trade (`PullRollTrade`) fires with the pitch stick centred. Its AoA-rate ask is `|qAskPre − num8|` — the pitch command's distance from the *measured* velocity-frame pitch rate. With the stick in the detent `qAskPre` is **exactly zero** (`qRaw = sp × qB` behind the law's `num9` detent gate), so the ask becomes the measured rate itself: the jet's own settling sweeps the AoA a few degrees at 2–7 deg/s and the trade pays for a sweep nobody commanded. Since the allowance is `share × yawCap × cos α / α̇`, a small spurious ask bites hardest where the yaw capacity is small, and it drove the limit down toward the 30 deg/s `PullTradeFloor` and back.

Full derivation, the Raptor comparison, and the measured `trd` trace are in `Ifrit_FCS_0.7.3_notes.md` — the code is the same on both jets.

## The fix
```
bool qSweepAsk = |sp| > 0.04f || prot || protHold || |num34 - qRaw| > 0.05f;
float aAskF = qSweepAsk ? |qAskPre - num8| : 0f;
```
`|sp| > 0.04` is the law's own detent (written on `sp` so it reads the same under the rate law, where `num9` stays 1); `prot`/`protHold` is protection commanding; `|num34 − qRaw| > 0.05` catches any bound that actually bound the command — the g / AoA-rate clamps, the unload, the protection onset. A centred stick with nothing binding does not count. Measured state only, no schedule, no new constant.

## Bench
Full-stick roll, pitch stick centred, 1500 m, accelerating. "cut" is `pSmax ÷ (pMax / |cos α|)`; 1.00 means the limit is the airframe's authority, not the trade.

| IAS km/h | **0.4.2 cut** | samples cut below 0.9 | `pSmax` | peak β |
|---|---|---|---|---|
| 150 | 0.99–1.01 | **0 %** | 72–150 | 3.0° |
| 200 | 0.99–1.01 | **0 %** | 110–163 | 2.6° |
| 250 | 0.99–1.01 | **0 %** | 128–182 | 2.1° |
| 300 | 0.99–1.01 | **0 %** | 148–198 | 1.8° |

Identical to `PullRollTrade=0` in all four — same `pSmax` range, same sideslip to 0.1°.

### Regression set, 0.4.1 → 0.4.2 (peak sideslip, deg)
**Every case flat or better.** No regression anywhere:

| | 0.4.1 | 0.4.2 |
|---|---|---|
| push+roll 200 / 300 | 0.8 / 0.9 | 0.8 / 0.9 |
| pull-then-pushover+roll 350 / 450 | 3.0 / 3.0 | 3.0 / 3.0 |
| push-then-pullthrough+roll 350 / 450 | 3.4 / 3.2 | 3.3 / 3.3 |
| pullthrough, roll released at zero 350 | 2.8 | **2.5** |
| push+roll −55 corkscrew 170 | 1.3 | 1.3 |
| roll-then-push −55 200 | 1.8 | 1.8 |
| fullaft+roll+rev3s 320 / 500 | 2.2 / 2.7 | 2.2 / 2.7 |
| 65° roll+yaw release 300 | 2.7 | 2.7 |
| held0.4+roll 350 | 3.2 | 3.2 |

Sweep stop / reversal sets and the corkscrew set: **byte-identical**. In every protection case the stick is deflected, so the gate is open and the trade behaves exactly as in 0.4.1.

## Rejected on the way
Pointing the trade's yaw capacity at every allocator effector (the fix `RevSim` and `PaceReachAll` got in 0.4.0). It halved the swing but cost sideslip in three pull-through-zero cases on the Ifrit, so it was dropped on both jets. See the Ifrit notes.

## Not done / known
- **Not flown.**
- **Rate law (`PitchRateCommandOnly`) is reasoned, not benched** — the gate is written on `sp` so it applies, but the bench stub can't set that key.
- `yawCapDbg` is still the hand-picked yaw set everywhere it is used; it is a known undercount (the trailing edge is excluded).

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 44cd8c3ea87c2ebe35f730df50c8e3b5) and `meta.json`, version 0.4.2.
- **Backup:** 0.4.1 kept as `…\Revoker_FS12_FCS.dll.0.4.1.bak`.
- **Release:** `C:\Games\NO mods\Revoker-FCS-0.4.2-release`. The NOMM listing in the plugin folder is not updated — your release step.

## First flight
1. Full-stick rolls at 150–250 kt with the pitch stick centred and AoA low — the rate should hold steady.
2. Push-over + roll and the −55° corkscrew entries: should feel exactly as in 0.4.1.
