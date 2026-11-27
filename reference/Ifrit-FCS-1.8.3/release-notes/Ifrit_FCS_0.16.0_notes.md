# Ifrit FCS 0.16.0: the nozzles take the pitch off the ruddervators when the yaw is short

Base: 0.15.0. Minor bump: this is a law change.

**Status:**
- Compiled.
- Benched (full suite, plus replays of 16 events from your logs).
- **Not flown.**
- 0.15.0 is kept as `.dll.0.15.0.bak`.

## Tom (5 Nov)
- 15:26: "make tvc offload the pitch away from the ruddervators for those demands".
- 15:48, on the bench table: "it is fine".
- 15:47: "is it an on top fix?" Yes: see below.

## What it does
On the Ifrit the ruddervators are both the pitch and the yaw. In the 125 s replay, a roll released and then pulled through zero AoA, the allocator
was 11–40 deg/s² short of the yaw it was asked for while the nozzles sat near their pitch trim.

When the allocator reports unmet yaw, two things happen:
- **The yaw outranks the pitch in the allocator.** Weight × (1 + 6 × need), where need runs 0 → 1 over 2 → 12 deg/s² of unmet yaw. The pitch then
  goes to the nozzles where they can carry it, and waits where they can't.
- **The roll-TVC budget,** which holds the nozzles near their pitch trim during lateral input, opens.

Neither part looks at any stick, and there is no AoA schedule. The tail reserve (`latReserve`) part is off: it gave the tails to the roll as
well.

**This is a reactive priority on the allocator's unmet-yaw report, not a model.** It is tuned by three constants (`YawNeedK` 6, `YawNeed0` 2,
`YawNeedSpan` 10).

Off switch (bench / source): `TvcYawOffload = false` is exactly 0.15.0.

## Bench (0.15.0 → 0.16.0, margin 6, blend 0.4)
**Stop/reversal sweep:**

| | 0.15.0 | 0.16.0 |
|---|---|---|
| Stop sideslip, worst / mean | 6.2 / 3.83° | **4.6 / 3.10°** |
| Reversal sideslip, worst / mean | 8.5 / 4.91° | 8.8 / **4.41°** |

**Regression suite:**
- better: roll-then-pull reversal 400, 9.5 → 6.6°; push-then-pullthrough 350 / 450, 10.7 / 8.9 → 8.0 / 7.4°; pullthrough with the roll released,
  5.4 → 4.3°; held 0.4 + roll, 4.7 → 3.7°; roll-then-push −55, 4.2 → 3.3°; pull-then-pushover 350, 6.1 → 5.7°.
- worse: full aft + roll + reversal 320, 3.3 → 3.8°; push + roll −55 corkscrew, 2.6 → 2.9°; push + roll 300, 2.2 → 2.3°.

**Corkscrews:**
- better: 8 cases, by 0.3–0.9°;
- worse: pull 0.6 / 0.8 + roll at 450, 5.0 / 5.9 → 6.8 / 6.8°; pull 1.0 + roll at 550, 7.7 → 8.8°; two cases +0.1°.

**Damage suite:**
- better:
  - one-ruddervator leaf stop / reversal at 370, 7.6 / 6.9 → 5.2 / 5.8°;
  - both-ruddervator push-overs at 560 / 600, sideslip after the push 8.7 / 9.6 → 3.1 / 5.8° held;
  - clean held push 600, 9.2 → 6.9°;
  - the 5 HP rolling pull at 900, 89.9 → 65.2° (still departs);
- worse:
  - one-ruddervator held push 560, 11.9 → 13.6°;
  - clean held push 560, 7.0 → 7.3°;
  - clean released push 600, sideslip after the push 2.8 → 3.3°;
  - both-ruddervator leaf reversal 370, 4.8 → 5.2°;
  - two more by up to 0.5°.

**Replays of your logs:**
- better: 69.9 s 6.2 → 4.9°; 104.8 s 10.2 → 7.5°; 31.9 s 12.1 → 8.1°; 125 s 8.0 → 7.1°; 82.3 s 9.1 → 8.0°; 88.5 s 5.9 → 5.2°; 131 / 183 / 262 s
  −0.3 to −0.5°.
- worse: 64.8 s 8.7 → 9.5°; 59.6 s 7.7 → 8.8°; 52.5 s 6.3 → 6.8°.

**Uncommanded roll:** none. In all 16 replays the roll never exceeds its value at release after the stick is let go.

**Note:** the bench is sensitive to start-up order. The same law run with the switches set from the command line (the table you saw) differs by up
to ±0.6° per case, and by more in a few push-overs. The numbers above are this build's own defaults.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `88903037e930d4a40e002f3ca5c6bb8c`) and the `meta.json` beside it.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.16.0-release`, with the main zip, Optional DevUnlock, Source (with the bench) and these notes.
