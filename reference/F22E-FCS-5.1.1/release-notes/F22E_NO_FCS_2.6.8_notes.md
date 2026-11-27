# F-22E FCS 2.6.8

Base: **2.6.7**. One default changed: `RollPaceRateBlend` **0.4 → 0**, with a migration. That is the whole release. It fixes the slow falling-leaf rate you found on 2.6.7, and the fix is one line because the diagnosis took three tries.

**Bench-only. Not flown.** Your 2.4.16 is still `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.16.bak` and 2.6.7 is `…\.dll.2.6.7.bak`.

## What was wrong, and what wasn't
You flew 2.6.7, said the falling-leaf yaw was ~30 deg/s against ~43 before, reverted to 2.4.16 and said it was faster. Correct on both counts. Two wrong diagnoses came first and are worth recording so they are not repeated:

1. **"Not a regression, you were flying slower."** `rMax` and `pSmax` are byte-identical in 2.4.16 and 2.6.7, and the *authority* estimate is the same or better. That was all true and all beside the point — the binding device was upstream of the cap.
2. **"It's the pace window's yaw reach."** Plausible (the window reads its reach off the hand-picked rudder set, which fades at high AoA, and that same defect cost the stock jets their rates in Revoker 0.3.0 / Ifrit 0.6.0). Wrong here: the Raptor port of `PaceReachAll` moves the hold by **0.6 deg/s**, and its regression costs sideslip in two reversals (fullaft+roll+rev3s 320 4.8 → 6.1, roll-then-pull-rev 400 2.8 → 4.2) for that nothing. **Dropped, not shipped.**

The actual device, found by bisecting the pacing statics on the bench (65° AoA, 300 km/h, full lateral):

| `RollPaceRateBlend` | commanded | cap | util | `paceHold` | \|r\| | \|beta\|max |
|---|---|---|---|---|---|---|
| 0 | 36.1 | 38.7 | **93 %** | 0.6 | 30.3 | 4.6 |
| **0.4** (shipped) | 26.3 | 38.4 | **68 %** | 11.1 | 23.5 | 3.8 |
| 1 | 10.4 | 34.9 | **30 %** | 24.4 | 11.3 | 4.2 |

The blend splits the paced roll-rate target between the yaw-reach window and the **kinematic rate cap** — and on this jet that cap is the pre-`RevSim` formula over the hand-picked yaw set, far more pessimistic than the airframe. At 0.4, 40 % of the command came from it, holding the commanded roll rate to 68–71 % of its own cap from 15° AoA up with the sideslip at 1.3–2.0° of the margin. Because a velocity-vector roll at high AoA carries `r = pS sin(AoA)` (sin 65° = 0.91), the yaw rate fell with the roll command — the whole of your falling-leaf loss.

**This is why the Ifrit's blend default went to 0 in 0.7.0**: `RevSim` replaced the kinematic cap there, and a 0.4 blend against the old cap diluted the window to 60 %. The Raptor has no `RevSim`, so the honest setting is 0 — the window alone governs, as on both stock jets.

## Bench
Falling leaf, full aft held then full lateral, 1500 m:

| | util | commanded / cap | \|r\| | \|beta\|max |
|---|---|---|---|---|
| 160 km/h, 2.6.7 | 68 % | 21.3 / 31.3 | 20.1 | 5.7 |
| 160 km/h, **2.6.8** | **92 %** | 29.9 / 32.4 | **25.4** | 5.6 |
| 300 km/h, 2.6.7 | 68 % | 26.3 / 38.4 | 23.5 | 3.8 |
| 300 km/h, **2.6.8** | **93 %** | 36.1 / 38.7 | **30.2** | 4.6 |

Utilisation invariant (`an/utilcheck.py`) over the standard sweep: **16 fails of 17 → 2 of 18**.

### Regression set, 2.6.7 → 2.6.8 (peak sideslip, deg)
**This is a real trade, not free.** Mean `pMax` 46.1 → 49.9, worst sideslip 6.4 → 6.7, 6 cases better and 8 worse — all inside the 8° margin, none close to it.

| | 2.6.7 | 2.6.8 |
|---|---|---|
| held0.4+roll 350 | 3.3 | **0.6** |
| fullaft+roll+rev3s 320 / 500 | 4.8 / 6.4 | **2.8 / 3.8** |
| pull-then-roll 250 / 320 / 400 | 4.1 / 3.7 / 4.2 | **2.7 / 2.8 / 2.8** |
| 65° roll+yaw release / roll release 300 | 2.1 / 2.1 | 3.8 / 3.6 |
| push+roll 200 / 300 | 3.1 / 1.7 | 3.3 / 3.3 |
| pull-then-pushover+roll 350 / 450 | 1.9 / 2.6 | 2.6 / 3.8 |
| push-then-pullthrough+roll 350 / 450 | 3.7 / 4.3 | 5.1 / 6.7 |
| pullthrough, roll released at zero 350 | 3.7 | 5.1 |
| roll-then-pull-rev 400 | 2.8 | 3.1 (pMax 93 → 94) |
| 1g roll rev 500 | 1.9 | 2.1 (pMax 175 → 188) |

**The blend is not a monotonic trade.** 0.2 is worse than *either* end — 14 cases worse against 8 at zero, worst sideslip 7.5° against 6.7° — so there is no middle setting to split the difference with. Same shape as `RollStopRudderLoad` in 2.4.18.

## Your tuning
`RollPaceRateBlend` 0.4 was **your** value, and this moves it. The migration (`GovernorRevision` 0 → 1, new key in `[6. Lateral governor]`) moves a file holding *exactly* 0.4 to 0 once; anything you set by hand is kept. To put it back, set `RollPaceRateBlend = 0.4` in `Aryx_F22E_StrikeRaptor.FCS.cfg` — one line, takes effect on load. Everything else — `PullRollTrade` 0.7, `RollStopSideslip` 4, `RollPaceRateSlip` 1, `RollGovernorPitch`, `RollStopSlipProbe` — is untouched.

## Not done / known
- **Not flown.**
- 2 cells still fail the invariant at 25–55° AoA (63–77 % of cap). That is the window's own mid-AoA hold, and the Revoker shows the same residual (7 of 16 cells) in both 0.4.1 and 0.4.2 — i.e. it is not new and not from this change. Open, unchased.
- The Raptor still has no `RevSim` and no velocity-frame allocator. The allocator genuinely regresses this jet; `RevSim` would make the rate-cap path accurate enough to blend again, and is the proper fix if the blend is ever wanted back.
- `PaceReachAll` is **not** in this build (see above). `tools/patch_reachall_f22.py` exists if it is ever worth revisiting with the sideslip cost addressed.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 f6b6d4e6f30460c0255cacd3c890484d), version 2.6.8. Dev DLL unchanged.
- **Backups kept:** `.dll.2.4.16.bak` (what you were flying until tonight) and `.dll.2.6.7.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.6.8-release`; `meta.json` carries the v2.6.8 URL and the zip's sha256 `d5b0b540253cb94367eef2be1bbb8bb3e0a2539eb9a214043d662414df24d61e`. GitHub release is your step.

## First flight
1. **The falling leaf.** Bench says the command now runs at ~92 % of its cap instead of 68 %, so the yaw rate should come back toward what 2.4.16 gave you. That is the one thing this release is for.
2. **The eight cases that got worse** are push-overs, pull-throughs and the 65° stick-release — 2.1 → 3.8 on the release, 4.3 → 6.7 on push-then-pullthrough 450. All inside the margin on the bench, but the bench is not the game on exactly this quantity. If any of them feels loose in sideslip, that is the trade showing and I want the log.
3. Remember this is still 2.4.16 → 2.6.8 in everything else: the whole 2.5.x/2.6.x governor rework is in here, not just this one line.
