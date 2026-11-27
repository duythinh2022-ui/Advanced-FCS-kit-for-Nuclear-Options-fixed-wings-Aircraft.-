# Ifrit FCS 0.9.0: the F-22E 3.0.0 reference governor law

Base: 0.8.3. Minor bump: this is a law change.

**Status:**
- Compiled against the game's assemblies.
- Benched.
- **Not flown.**
- 0.8.3 is kept as `.dll.0.8.3.bak`.

Handoff: `claude/RefGov_port_Ifrit_Revoker_handoff.md`. Bench findings: `claude/Ifrit_0.9.0_law_port_bench_findings.md`.

## Tom's call (5 Nov, 00:01)
"do 3 and fix sideslip later". Ship the full law as it stands: faster rolls and push-overs, with about 2° more sideslip than 0.8.3 on the bench.
The sideslip is a known, open item (below).

## What the jet now has (the Raptor's 3.0.0 governor)
"Governor" here means the part of the FCS that slows a command when an axis can't keep up. It now works from one number per axis and side, the
**capacity**: the angular acceleration the jet can still make.

- **Capacity estimator** (from 0.8.2, now driving the law). It is the measured acceleration, plus what the effectors already commanded will add
  within 0.15 s, plus a probe of the allocator itself: "if I asked for 400 deg/s² more yaw, what would I get".
  - It counts every effector: tails, ailerons, decelerons, splits and roll TVC.
  - It now runs every tick, not only while logging. Cost on the bench is about 0.65 ms per tick.
- **The trade** (rolling pull). When the pull and the roll together ask the yaw for more than it has, 70 % of the shortfall comes off the roll
  target and the rest off the AoA rate. This replaces the old trade and the AoA-rise cap.
- **Measured pace window.** The roll target is held back only while the allocator reports that it can't deliver the yaw it is asked for.
- **Planned push-overs (push plan).** A push-over is planned once, at its start:
  - Shed rate: capPlan = clamp(K0 + K·IAS^1.5, 6, 80), with **K0 9.6, K 0.0144, fitted to your 22:57 / 23:10 push-overs** (rms 3.7 deg/s²).
  - Push time: T = (r − rLeft) / capPlan, where rLeft = √(2·capPlan·8°).
  - The plan owns the pitch: the pitch command no longer waits for the yaw to shed. This was your "delayed push-over": 0.8.x held the pitch
    command near 0 for 0.38–0.82 s.
- **Push governor.** Paces the pushes the plan doesn't take: damaged fins, low yaw rate.
- **Allocator reach pass + two-horizon pick** on the tails. The tails keep their command, and the other effectors are placed against where the
  tails will be in 0.1 s. This replaces the stab rate bound.
- **Fin-loss devices**, scaled by the tails' health.
- **Removed:** the AoA-rise cap, the old sweep/shed caps, the sideslip guard and the stab rate bound.

**What stays the Ifrit's own:**
- the tail-pair yaw axis;
- the splits;
- roll TVC;
- VMC and the engine-out limit (0.8.1);
- the 0.8.3 moment anchoring;
- `SlipFrameAlloc`;
- `RevSim` as the rate cap (fed the estimator);
- rudW from the probes.

**Differences from the Raptor:**
- `RollPaceRateBlend` is your Ifrit value, **0**, so in practice there is no steady-rate cap. The Raptor runs 0.41.
- The margins are 8° (Raptor 4°).
- The push-plan constants and the estimator gains are per jet.

## New setting
`6. Lateral governor / PushoverSideslip` = **8**: the sideslip the push plan allows at the bottom of a push-over. −1 = follow `RollStopSideslip`.

## Bench (0.8.3 → 0.9.0)
| | 0.8.3 | 0.9.0 |
|---|---|---|
| Released push-over, pitch rate past −30 deg/s | 1.10 s | **0.75 s** |
| Released push-over, AoA under 10° | 1.57 s | **1.32–1.35 s** |
| Released push-over, peak β | 1.1–1.5° | 2.8–3.4° |
| Held push-over, timing / β | 1.52 s / 23° | 1.52 s / 25–29° |
| Stop/reversal sweep, mean roll | 101 deg/s | **118 deg/s** |
| Sweep, stop β mean / worst | 2.8 / 4.7° | 4.8 / 7.1° |
| Sweep, reversal β mean / worst | 3.2 / 5.4° | 5.4 / 10.3° |
| Regression cases with β worse by > 0.5° | | 16 of 20 |

The damage suite is mostly worse in β with one or both tails damaged (for example, one tail, released push at 560 km/h: 5.4 → 14.9°).

## Known: the extra sideslip (to fix later)
- **Where it comes from: the roll entry at 10–20° AoA.**
  - 0.8.3's old trade capped the roll there at about 75 deg/s.
  - 3.0.0's trade only counts the yaw that the AoA change needs, so with the pull held nothing caps the entry.
  - The roll accelerates to 100–130 deg/s. The yaw (30–60 deg/s² available) can't keep the turn coordinated (r = pS·sin α), so β builds.
- **The estimator isn't the cause.** It reads 0.83–1.11 of what's actually delivered on the bench, and 0.84–1.71 in your 0.8.2 logs.
- **Things tried that didn't fix it:**
  - lowering `RollStopSideslip` to 2°;
  - a shorter window lead;
  - `RollPaceRateBlend` 0.4 (`RevSim` only simulates a reversal, never an entry);
  - the window always latched.
- **Candidate fix:** model the roll entry in the rate cap (the `RevSim` forward model extended to the entry), so the steady rate is set by what
  the yaw can coordinate.

## For the flight
- Push-overs from 60–70°, released and held, 150–250 kt. Does the pitch response now feel immediate, and is the bump gone?
- Rolls and reversals at 10–35° AoA. Are the rate and the sideslip acceptable?
- Rolling pulls and the falling leaf.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `e5f96d04131b98f3e20007d7592330e8`) and the `meta.json` beside it (the one a level up is the mod manager's own record, left alone).
- Release: `C:\Games\NO mods\Ifrit-FCS-0.9.0-release`. The source zip includes the bench.
