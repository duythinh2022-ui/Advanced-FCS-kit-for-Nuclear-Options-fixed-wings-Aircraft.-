# F-22E FCS 2.32.0 — released-follow engages on the law's push intent, not on AoA flicker

Base: 2.31.2 (2.31.0 law + passive-airframe telemetry). **One law change. Compiled against the game's assemblies, bench-checked,
not flown.** 2.31.2 kept as `.dll.2.31.2.bak`. No player or Dev key added; CSV unchanged from 2.31.2.

## The problem (Tom, 18:39: "at the end, there is roll oscillation" — 18:36 log, flown on 2.31.1, same law)
118.5–125 s: held pull (sp 0.54) at 31–34° AoA, 205–230 kt, bank ≈ −100°, lateral stick released after a short left roll. Roll rate
±7–10 deg/s at ~1.5 Hz, sideslip drifting to −6°. `ReleaseFollowYaw` (the 2.25–2.27 push-over device) engaged on its falling-AoA test
`alphaDot · α < −2` — at 34° a 0.06 deg/s drop, a one-tick AoA flicker — braked the yaw the law was using against the sideslip
(rCmd −8 → +12), let go at |r| < 3 deg/s, re-engaged on the next flicker: 9 engagements in 6.4 s, on 41 % of ticks. Present in every
log since 2.25.0 (2–18 in-pull engagements per flight), usually a one-tick blip.

## The change (`ReleaseFollowCmdIntent` = true, `ReleaseFollowCmdRate` = 5 deg/s; false = 2.31.2 byte for byte)
The falling-AoA engagement reads the law's own commanded AoA rate, sign(α) · (qCmd − ωV) < −5 deg/s (body pitch-rate command less the
flight path's rotation, toward zero AoA), instead of the measured alphaDot. The push-stick engagement (`ReleaseFollowPushStick`) and
everything the follow does once engaged are unchanged. At engagement across the 3 Nov logs the commanded rate was −22.8 deg/s (median)
on real pushes, 95 % below −10.8; −1.6 on the false ones.

## Bench (new: `bench.sh replay … pinPitch=1`, pitch plane held to the log — the free replay climbed 34 → 51° AoA, §5 pitch item)
| check | result |
|---|---|
| 18:36 chatter, pinned replay 118.3–125 s | 2.31.2: 10 engagements, 40.2 % of ticks (game 9 / 40.9 %); **2.32.0: 0**; rCmd jump 1.12 → 0.01 deg/s/tick; sideslip mean −3.9 → −0.6°; p std 3.1 → 1.9 |
| switch off | replay and full suite byte-identical to 2.31.2 (119 per-tick case files) |
| 22 logged push-overs (14:45, 15:25, 17:07), free and pinned | byte-identical to 2.31.2; follow engages in the 12 released ones within ±0.03 s (all via the push stick) |
| 11 snap-roll releases (17:07, 17:43) | byte-identical; in the game the follow engaged on 1 of 12 snap releases (17:43 149.0 s, the flicker) — that one +1.1° sideslip peak on the bench |
| 31 earlier in-pull false engagements, pinned | medians: β peak 11.5 / 11.5°, β RMS 4.84 → 4.63°; 17:07 161.2 s 7.8 → 2.6°; 17:43 149.2 s 7.7 → 8.8° |
| full suite | rev / cork / util unchanged; reg: 65° roll releases no longer engage the follow (β, rMax unchanged); dmg: leaf stops 296 / 370 clean 2.00 → 1.90 / 2.22 → 2.10 s (sideslip after 4.3 → 4.6° at 370), no-fin leaf rev 370 now reverses (3.53 s, was never), no-fin released push 600 β 20.1 → 21.3°, held 65 + LERX off 3.8 → 5.3° |

In the game, 55 of 61 in-pull engagements were at 0–40° AoA (3 above 50°), so the leaf-stop changes are mostly the bench engaging it at 65°.

## Not known until flown
- Whether the −5° sideslip at −100° bank fully clears once the follow stops interfering (the yaw axis saturated there, `unmetZ` to −51).
- The snap-roll release that the follow caught through the flicker (17:43 149 s) — it now stops under the normal law.

## Also in this tree (bench-only)
`bench.sh replay … pinPitch=1` (`sim/Replay.cs`); `bench sliptruth` / `partcmp`, `calib/passive_cmp.py` (2.31.2). Same replay ported to
the Ifrit and Revoker benches (`claude/Ifrit_Revoker_bench_replay.md`).

## What to fly
The 18:36 case: a held pull at 30–35° with lateral stick released after a small roll, banked steeply — the roll should hold steady.
Plus a few released push-overs and snap rolls to confirm they feel as on 2.31.0.
