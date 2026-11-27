# F-22E FCS 2.10.0 — the table governor's dead paths deleted; the shed cap's coordination credit fixed

Base: 2.9.4. **Compiled + bench-checked, not flown.** 2.9.4 kept as `.dll.2.9.4.bak`. Two parts, proven separately.

## Part 1 — removal (bench byte-identical)
Deleted from `FcsController.cs` (9322 → 9230 lines) and `Plugin.cs`:
- the `MeasuredGovernor = false` path — the 2.6.12 rudder-table / rudder-load governor: `PaceMeasured` and its false branches
  (table capacity in the pitch caps, the rudder-usage window gate, the 2.5.x open-loop acceleration cap `paceAccelCap` with
  its `capRud / capFaded` chain, the rate cap's table capacity `hdRev − unmetRev` and open-loop `capR`, the release pace's
  table term, `RateCapReversalOnly`), and the two player keys **`MeasuredGovernor`** and **`RateCapReversalOnly`** (section 6;
  the lines left in your cfg are ignored);
- the `TradeMeasured = false` path — the 2.7.3 trade (`shareF / yawFree` and the roll limit from them, the rNeed / rRoll
  pitch share) and the static;
- the 2.9.0 / 2.9.2 A/B switches `SweepHoldPitchPrio`, `SweepHoldStallWindow` — now the law.
Proof: with your config, every bench output — reversal suite, corkscrew entries, the 18 regression cases' per-tick CSVs, the
utilisation invariant — is **byte-identical** to 2.9.4's. The deleted code is in the 2.9.4 source zip
(`C:\Games\NO mods\F22E-FCS-2.9.4-release\F22E_FCS_2.9.4_src.zip`); nothing was archived into the tree.

## Part 2 — the 17:33 flight (2.9.4) and one fix
Released-roll push-overs: 177 kt / 49 → 3.0°, **270 kt / 63 → 4.7°**, 205 kt / 63 → 4.2°, then 223 kt / 67 → **24.6°**,
216 / 65 → 12.8°, 219 / 79 → 21.4°. Sweeps 1.2–1.6 s (2.9.3: 1.7–2.1). Tom: "I said a few more degrees, not 3–4× more."

The three bad ones were not the allowance. Same yaw rate as the good 205 kt push (63 vs 67), but **pushed as the roll was
released, not after** — the roll trajectory was still braking (−78 → −46 at the push, where the 125.5 s push had −14, stick
let go 0.3 s earlier). The shed cap credits the trajectory's coordination (|pS_traj|·sin α) as yaw that is not excess, so it
read 69 − 41 − 5 = 23 deg/s of excess instead of 64 and sat open (46 deg/s of AoA rate) for the 0.5 s the trajectory took
to wind down. By then the AoA was 45° with 62 deg/s of yaw on, the stabs had been on the hold's pitch trim instead of the
yaw (L +7 / R +35 at the push, then a −30 → +35 swing), and the shed at 65 → 50° was 4–17 deg/s² against 35–55 in the
good push. Everything after that was the familiar runaway: 24.6°. In the 125.5 s push the trajectory was already near zero,
the excess read in full, the stabs were differential from the first tick, and it made 4.2°.

**Fix:** the coordination credit is min(|trajectory|, |stick target|)·sin α — a released stick credits nothing, a held roll
credits what it does now. The 2.9.4 allowance (`PushoverSideslip`) is unchanged and still yours to place. Bench: one case
moves, the pull-through with the roll released at zero — the cap's cut 13.4 → 44.9 deg/s (the released stick now counts the
full yaw), sideslip 7.5 → 7.5°; everything else identical.

Expected: the "push as you release" case paced from the first tick like the "release then push" case. If the nose-down with
a high yaw rate on is then slower than you want, that is `PushoverSideslip`; if 65 deg/s pushes still run past ~10°, send the log.
