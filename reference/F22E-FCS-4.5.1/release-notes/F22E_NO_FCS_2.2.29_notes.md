# F-22E FCS 2.2.29: corkscrew AoA pumping; roll bump on a falling-leaf stop

Base: 2.2.28. Log: 20 Oct 10:35. Bench: your live Dev cfg, `DisableTVC = false`.

## 1. AoA pumping in the corkscrew (275-285 s)
**Conditions:** full aft + full roll held, 215 -> 160 kt, roll 75-107 deg/s.

**What happened:** the AoA cycled 25 -> 38 -> 31 -> 39 deg about every 1.2 s, and the pitch-rate command 46 -> 12 -> 40 -> 5 -> 39 -> 4.
- Each time the sideslip reached 7.6-9.4 deg, the rolling-pull governor unloaded the AoA (`slipGovPull` -0.5).
- When the sideslip cleared, the 2.2.28 servo pushed the AoA back up.
- The 207-219 s and 291-295 s corkscrews did the same.

**This is 2.2.28's fault:**
- The servo aims at 85 % of the margin. In a fast roll the sideslip then overshoots the margin, and the governor's unload (from 2.2.7) acts.
- Before 2.2.28 the sideslip never got that close in these corkscrews.

**Change:** with the servo steering, past the margin the AoA is held, not unloaded. With a fin lost, the unload is unchanged.

**Bench, replays of the nine corkscrews in this log** (+-10 km/h, 27 cases):

| | Time with the pitch command under 5 deg/s, stick held (mean / worst) | Pitch-command activity | Worst sideslip | Mean AoA |
|---|---|---|---|---|
| 2.2.27 | 0 / 0 % | 26.0 deg/s | 11.5 | 54.9 |
| 2.2.28 | 6.2 / 17.9 % | 26.8 | 11.0 | 55.0 |
| 2.2.29 | 0 / 0 % | 23.9 | 10.0 | 56.3 |

## 2. Roll toward the yaw when you stop a falling leaf (198, 285 s and others)
**Stick released at 65 deg AoA, yawing right at 30-45 deg/s:**
- The roll command stayed about +19 deg/s while the velocity-vector roll was braked.
- The body roll rose 14 -> 34 deg/s against that command.
- **Ailerons and flaperons were at their stops rolling left.** The rudders braked the yaw at full deflection (-35 / +35 once the AoA was under 55-60), and their canted inverse roll rolls the jet the way it was yawing. The roll axis had nothing left to hold it.

**Change (`RelLatRollPrio` 3, internal):**
- With the lateral stick and pedals released, the allocator weights roll up to 3x as the body roll runs 2-10 deg/s past its command.
- The yaw stop gives way to the roll a little instead of the reverse.

**Bench, 30 replayed stops:** roll bump past the roll at release 2.4 / 10.6 -> 1.5 / 7.0 deg/s (mean / worst), sideslip 2.3 / 3.7 -> 2.0 / 3.7 deg.
- **Caveat:** the replays don't match your jet at the release well. Bench AoA at the stop is 37-64 against your 65, and the bench bumps are smaller than in the log. Treat it as direction, not size.

## 3. `ReversedFlowRollSense` on
- The default was already true, but your Dev cfg held false.
- A one-time migration (`AirframeRevision` 10) sets it to true. After that, whatever you set is kept.
- `LogOutput.log` shows `F-22E FCS 2.2.29 defaults: ReversedFlowRollSense true.` once.
- What it does: past 100 deg AoA (released below 80) the roll command is flipped, so lateral stick rolls the jet the way you push it from the seat while flying backwards.

## Regression
- **Suite:**
  - S1 worst pitch dip -3.8 / -4.0 -> -4.0 / -4.1 deg/s (nozzles on / off).
  - S4 roll limit within 1-5 deg/s.
  - S6 qMin -2.9 -> -3.1.
  - The rest identical.
- **Hitch set, fins-gone set, low-IAS rolling-pull replays (all four logs):** identical to 2.2.28.

## Not verified
- Not flown. Bench only.
