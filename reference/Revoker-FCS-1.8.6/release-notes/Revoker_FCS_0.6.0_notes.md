# Revoker FCS 0.6.0: the F-22E 3.0.0 reference governor law

Base: 0.5.1. Minor bump: this is a law change.

**Status:**
- Compiled against the game's assemblies.
- Benched.
- **Not flown.**
- 0.5.1 is kept as `.dll.0.5.1.bak`.

Same port as Ifrit 0.9.0 (`claude/Ifrit_FCS_0.9.0_notes.md`). Tom (5 Nov, 00:01), on the Ifrit's version of the same trade: "do 3 and fix sideslip
later". That means the full law, with the faster rolls and the higher sideslip that come with it. The Revoker shows the same trade on the bench
(below), so it ships the same way.

## What came over
The Raptor's 3.0.0 governor:
- the capacity estimator, now driving the law and running every tick;
- the trade on it;
- the measured pace window;
- the push plan and push governor;
- the allocator reach pass + two-horizon pick (on `Stabilator` = **the canards** here);
- the fin-loss devices, with the no-fin stop **off**, see below.

Removed: the AoA-rise cap, the old sweep/shed caps, the sideslip guard and the stab rate bound.

**What stays the Revoker's own:**
- canard logic and yaw TVC;
- hinged inboard elevons and the splits;
- teBrake;
- `RevFlowSense`;
- the Revoker damage-lateral floor;
- `SlipFrameAlloc`;
- air data at the root (`AirDataAtCg` stays off, as in 0.5.1);
- `RevSim` as the rate cap.

`RevSim` now takes the estimator's yaw: its reversal is paced at `PaceYawUse` × the yaw the effectors can add, over sin AoA, as the Ifrit's has
since 0.7.2.

**Push plan, fitted to this jet:** shed = **K0 40 + K 0 × IAS^1.5** deg/s². This jet's push-over yaw shed is about constant with speed: 4
push-overs from the 23:03 / 23:13 logs, rms 3.9. The yaw nozzle doesn't fade with speed. This is provisional; refit as more push-overs are flown.

**New setting:** `6. Lateral governor / PushoverSideslip` = 8 (−1 = follow `RollStopSideslip`).

## Per-jet fix: the no-fin stop is off (`PaceStopNoFin = false`)
- **What the F-22E device assumes:** with the fins gone, nothing but the stabs is left to yaw.
- **Why that's wrong here:** on the Revoker yawHealth reads the rudder and the fins. The yaw nozzle and the elevons are untouched, and they are
  the yaw axis at high AoA: the elevon pair ±68 deg/s², the rudder ±12, at 350 km/h / 70°.
- **With it on (bench damage suite, rudder or fins + rudder detached):**
  - 4 push-overs **departed**: sideslip 41–52°, AoA to −80…−93 after the release at −50, a yaw rate running to 128 deg/s;
  - leaf stops made 13–17° against 0.5.1's 2–3°.
- **Off:** every damaged case is back to 0.5.1's (worst +0.8°). Clean cases are identical.
- It is inert on the Ifrit, which keeps it on.

## Bench (0.5.1 → 0.6.0)
| | 0.5.1 | 0.6.0 |
|---|---|---|
| Stop/reversal sweep, mean roll | 100 deg/s | **113 deg/s** |
| Sweep, reversal time | 0.52 s | **0.40 s** |
| Sweep, stop β mean / worst | 2.2 / 3.7° | 3.0 / 5.3° |
| Sweep, reversal β mean / worst | 2.7 / 5.2° | 3.9 / 9.5° |
| Corkscrews, velocity roll | 62–68 deg/s typical | 79–83 |
| Corkscrews, peak β | 1.1–3.4° | 1.3–8.6° |
| Regression cases with β worse by > 0.5° | | 16 of 20; roll rates up in most, 20–60 % |
| Released push-over, pitch rate past −30 / AoA under 10° | 0.60–0.62 / 1.37–1.38 s | 0.62–0.63 / 1.32–1.33 s |
| **Held push-over, pitch rate past −30 / AoA under 10°** | 0.70–0.72 / 1.42–1.43 s | **0.73–0.82 / 1.53–1.55 s** (slower) |
| Clean leaf, stop / reversal time (296, 370 km/h) | 1.77, 1.73 / 2.07, 1.37 s | 1.90, 1.35 / 2.27, 1.37 s |
| Damage suite | | as 0.5.1 within 0.8° (after the fix above) |

## Known
- **Sideslip** is higher in rolls and corkscrews. Same cause as on the Ifrit: nothing caps the roll entry at moderate AoA. To fix later.
- **The held push-over** is about 0.1 s slower to the bottom than 0.5.1, which you called fantastic. Push-overs with the roll released are
  unchanged.
- **The falling-leaf reversal** you reported as hesitant isn't addressed. The candidate is still the reach pass extended to the rudder and the
  yaw nozzle. The bench doesn't reproduce the dither, so it has to be judged in flight.

## For the flight
- Push-overs from 60–70°, released and held: did anything get worse?
- Rolls and corkscrews at 15–45°: rate and sideslip.
- The falling leaf.

## Install
- `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 `6f2065cd9070ede46205fac0d405997f`) and `meta.json`.
- Release: `C:\Games\NO mods\Revoker-FCS-0.6.0-release`. The source zip includes the bench.
