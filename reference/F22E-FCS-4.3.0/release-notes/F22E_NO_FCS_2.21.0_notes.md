# F-22E FCS 2.21.0 — the rolling-pull pitch share: one device, one read (the pull-then-roll pitch hitch)

Base: 2.20.0 (the two fin-loss changes kept). **Compiled + bench-checked, not flown.** Law change on the pitch side
(the §4 (c) "one accounting" of the fresh-start handoff), hence the minor bump. No tuning values touched: `PullRollTrade`
0.7, `TradeCapShare` 0.7, `RollPaceRateBlend`, the margins, all as they were. No player or Dev config keys added or
removed (the deleted statics were never bound). Game CSV: 295 → **297 columns** (`ceCapTrP`, `ceCapTrN` appended after
`stabSym`, before the per-surface columns; the bench csv and the replay csv carry them too).

## The 09:53 flight (2.19.0), 39.8–41.0 s — "pitch hitch at the end of the log"
Full aft at 214 kt from −7° AoA, full left stick 0.3 s into the pull, stick pinned aft throughout. Pitch rate
**51 → 30 → 53 deg/s**; `qCmd` 60 → 24 (40.12 s) → 44 → 55 while `rateCmdG` stayed 60; `yawSatPitch` peak 36.4 at 13°;
sideslip −0.1 → −1.5° during the cut, −7° at 41.0 s; rudders commanded 20 → −35 at 39.85–40.08, arriving 40.42. Three
things stacked:

1. **The yaw-capacity read dipped by a third for the rudders' servo transit.** `ceCapZN` 65 → 39 → 60 over 39.9–40.3 s:
   the rudder probe collapsed 44 → 0 the tick the command reached the stop, the committed term was credited only as far
   as the servo arrives in `CapHorizon` 0.15 s (`ceDr` 0.6–0.7), and the measured yaw acceleration arrived with the
   surface (the rudder spends its first 20° coming back through neutral).
2. **The trade divided by that number** (`TradeCapShare` 0.7 × 39 = 28 deg/s² against the stick's 141 deg/s roll + 60
   deg/s pitch): roll target 62, pitch share 26–33 (`tradeAlpha`). With the steady 65 it reads roll 75 / pitch 38.
3. **The 2.5.3 AoA-rise cap ("roll first, then pitch") took the pitch again** from the same capacity, less the roll
   trajectory's own acceleration × sin α, to **21–24 deg/s** — the floor of the notch — and let go as the trajectory
   settled, which with the capacity read recovering made the two steps on the way up.

The other rolling pull in the log (30.6 s, pull *into* a 150 deg/s roll at 276 kt, rudders already pinned) went 60 → 40
monotonic and was correct: the trade alone was the binder there.

## Changes
1. **AoA-rise cap deleted** (`LateralLaw`: `aLeft` / `den` / `aCap` / `pinGate` / `bLead` / `wM` / `capPull`; statics
   `StopSlipPitchK`, `StopSlipPitchStart`, `StopSlipPitchLead`, `StopSlipPitchClose`, `StopSlipPinAB0/1`). Nose-up at
   positive AoA is the trade's share when the trade is holding, else free (`StopSlipPitchFree`). The nose-down /
   negative-AoA branches (`capSweep`, `capShed`, the 2.19.0 nose-hold) are untouched — the push-over pace accepted in
   2.10.0 stands. `StopSlipPitchFloor` 6 stays as the trade's pitch-share floor.
2. **The trade reads the capacity low-passed** (`CapacityEstimator.capTrZeP/N`, `TradeCapTau` 0.05 s = 3 ticks): the
   trade decides the pitch share for the next few tenths of a second from a reading that carries the allocator's per-tick
   unmet, which spikes for a tick when it moves a stab between roll and pitch (bench replay, 40.17 s: unmet −9 → −20 for
   two ticks, capacity 34 → 13 → 24, the raw trade's share 27 → 9). Every other consumer (rate cap, window, sweep / shed
   caps, coupling cap) still reads `capZeP/N` raw.
3. Tried and rejected, for the record:
   - crediting the rudders' committed travel over 0.4 s instead of 0.15 (a rudder sent to its stop at 100 deg/s arrives):
     +2–3 deg/s on the entry's pitch floor, but the bench pull + roll reversal at 450 km/h went 17.4 → 30.8° of sideslip
     — in a reversal the same credit promises the new side's yaw while the rudders are still crossing;
   - `TradeCapTau` 0.1: the lag on a real loss cost the same reversal 17.4 → 19.0 and the 556 km/h corkscrew-entry roll
     dip 63 → 47 (bench);
   - counting the roll trajectory's acceleration × sin α in the trade's demand (proposed in chat): a 0.3 s transient the
     sideslip margin pays for, not a steady demand; counted as one it is the deleted cap again (141 deg/s of roll asked,
     ~40 deg/s² of build at 12°, 45 of capacity → 10 deg/s of pitch).

## Bench (airframe `F22E_bench_airframe_2.4.18-cal.json`; tomcfg from the live player + Dev cfg of 3 Nov)
- **Replay of the 09:53 log, 39.2–41.6 s** (plant posed from the log, logged sticks; 2.19.0 and 2.20.0 are byte-identical
  on it): `q_cmd` 2.20.0 **60 → 44 → 23 → 24 → 29 → 32 → 34 → 37 → 32 → 35 → 47 → 55** (two steps); 2.21.0 **60 → 43 → 23
  (40.20 s) → 24 → 27 → 30 → 33 → 36 → 39 → 44 → 48 → 55**, monotonic after the minimum. Bench pitch rate 57 → 29 (40.65 s)
  → 43 becomes 57 → 38 (40.45 s) → 54. **On the bench the deletion alone changes nothing on this replay** — its trade
  read bottoms at 20.7 where the cap did; in the game the cap bound ~5 deg/s under the trade (`qCmd` 23.6 against a
  trade line of ~28), so that much is the game's gain. The bench's sideslip under 2.21.0 tracks the game's closer
  (−6.0 at 41.0 s vs 2.20.0's −0.9; game −6.5): its allocator puts the left stab on the roll at +35 as the game did.
- **Replay 30.3–31.9 s** (pull into the roll): one tick at the trade's onset 30.0 → 21.2 deg/s of command, then within
  1.5 deg/s to the end; sideslip −3.5 / +6.1 against −3.5 / +6.6.
- Suite vs 2.20.0 (diff of `out_*_v220.txt` / `out_*_v221f.txt`): utilisation invariant identical (4 FAIL / 9, as
  before); reversals `pull+roll rev@0.7` 450 / 556 / 650: 17.4 / 20.3 / 20.1 → 17.1 / 21.1 / 22.5°, `fullaft roll rev`
  300 / 500 6.3 / 11.4 → 6.7 / 11.0; corkscrew entries ±1° of sideslip, the 556 km/h thr1 entry roll dip 63 → 43 (the
  400 one 54 → 51); regression set: pull-then-roll 320 / 400 7.7 / 6.9 → 8.6 / 5.6, fullaft+roll+rev3s 320 / 500 5.0 /
  5.4 → 3.7 / 7.7, the push-over cases within 0.4°; damage suite: no-fin push-over released 600 12.9 → 13.9 (|β| after 9.9 → 11.4), one-fin cases within 0.6°, the rest
  within 1°. All at the bench's ~2× sideslip; nothing in one direction.

## What this is and is not
It removes the second device and the per-tick steps; the pitch still slows to roughly half the ask while the rudders
fill, because the trade's share is `TradeCapShare` 0.7 of a capacity that honestly reads 39–45 for ~0.25 s in the
transit. Lifting that floor means the capacity's committed term knowing a rudder arrives — in a form a reversal does
not pay for (the 0.4 s try above). That is the next piece if the flight still feels it.

## What to fly
The same entry: full aft, then full lateral 0.3 s later, at 200–300 kt from low or negative AoA — `qCmd` should fall
once to about the trade's line (`tradeAlpha` + the flight-path rate) and climb without a step; the pitch rate should
slow, not stop. One pull into an established roll (the 30.6 s case). One released-roll push-over from 65° (untouched by
construction). `LogOutput.log` must say 2.21.0; the CSV has 297 columns.
