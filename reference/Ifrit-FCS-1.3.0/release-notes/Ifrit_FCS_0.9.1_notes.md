# Ifrit FCS 0.9.1: the roll-fight cost no longer holds back the trim (engine-down creep, part 2)

Base: 0.9.0 (the governor law port). Patch bump: one allocator fix, no new device.
Developed and first benched on 0.8.3 (as "0.8.4", never installed), then merged onto 0.9.0 and benched again there; the tables below are the 0.9.0 → 0.9.1 runs unless marked.

**Status:**
- Compiled against the game's assemblies.
- Benched: full suite, 4 samples per variant, against 0.9.0.
- **Not flown.** 0.9.0 is kept as `.dll.0.9.0.bak`.

## Tom (4 Nov, 23:38, after flying 0.8.3)
"it is a bit better, but it still roll and yaw by a bit, but in a shorter time now" (23:29 log).

## What the 23:29 log shows
One engine damaged, still running; stick centred.

| Stretch | IAS m/s | Roll rate | Yaw − command | Loop's standing demand `nuR` / `nuY` | Allocator unmet `ceUnX` / `ceUnZ` |
|---|---|---|---|---|---|
| 44–51 s | 86 | −0.33 °/s | −0.40 | 2.50 / 1.01 °/s² | 2.58 / 1.00 |
| 70–80 s | 76 | −0.43 °/s | −0.39 | 2.47 / 0.93 | 2.38 / 0.92 |

- The table offset 0.8.3 removed is now small (`anchR` ≈ −0.03 … −0.07).
- The rest is the allocator: every tick it leaves the loop's whole standing demand unmade.

## Why the bench missed it, and the bench fix
- **Engine damage didn't reduce thrust.** The bench plant applied engine condition only in its turbojet thrust branch. The Ifrit's engines use the other branch, so `engdmg` changed nothing and no trim formed.
- **Fixed in `sim/Plant.cs` (`EngCondThrust`).** The other branch now applies the same RPM-ceiling factor, 0.5·condition + 0.5, to military and AB thrust.
- **The bench now reproduces the log.** Engine at condition 0.4, 275 km/h start, full throttle:

| | Bench | 23:29 log |
|---|---|---|
| AB ratio | 0.57 | 0.58 |
| Roll creep | −0.41 °/s | −0.33 … −0.43 |
| `nuR` | 2.49 °/s² | 2.47 – 2.50 |
| unmet roll | 2.44 | 2.38 – 2.58 |
| Roll TVC | 4.2° | 2.5 – 4.9° |

- **Second bench fix (`sim/IfritTest.cs`).** The FCS log of a bench flight was never closed, so its last row could be cut mid-number. That crashed two analysis runs.

## Cause: the solve stalls on the roll-fight cost
- **Traced at a steady tick (bench, condition 0.4).** The solve starts with −4.2 °/s² of roll and −1.0 of yaw unmade.
  - Its Gauss-Newton step is rejected at the first iteration.
  - The coordinate pass can't move any effector.
  - The allocation stays frozen, tick after tick.
- **`RollFightCost = 0`:** the step is accepted, unmet roll is 0.00 and the roll creep is 0.00 °/s.
- **Other costs ruled out.** The μ pull to zero, the pair costs and the neutral holds are not it. Proposal A in `Ifrit_083_residual_creep_2329.md` (μ toward the trim) would not have fixed this.

**Why the fight cost does this:**
- It prices each surface group's roll against the groups' net, by a sign rule.
- With an engine down, the trim needs exactly that pattern: the tails' differential holds the thrust yaw and makes roll, and the wing and roll TVC cancel it.
- In one cost with the moment error, the sign rule is a cliff. Ifrit 0.4.1 already described it: "the wing may cancel the tails' roll only once it over-cancels it". The solver can't cross it, so part of the target stays unmade.
- INDI asks for the moment relative to where the effectors are, so the withheld part is answered only by the proportional rate term: a standing rate error of withheld ÷ (I·K).

**The cost still has a job.** Since 0.4.3, with an engine down and the stick centred, it is what stops a crossed set (tails on their stops against a crossed wing) from holding.

## The change (`Allocator.Solve`, `FightNullSpace`)
In the **settled state** (the 2.3.0 settled window: lateral stick and pedals centred, roll and yaw rates under `SettledRateOn`), the fight costs are kept, but only in the null space of the moment:
1. **Stage 1** solves without the roll- and pitch-fight costs. Every other cost is kept: motion, reach, μ, the pairs, the holds, the nozzle budget. The result is the moment the effectors make toward the target.
2. **Stage 2** starts from that answer with the fight costs back on. The moment is held to stage 1's (residual weighted 1000×).

So the fight costs choose the least-crossed allocation that makes the moment, and can't hold any of it back. Stage 1 asks only for what the target asks, so nothing winds up on a saturated axis.

**Why only the settled state:**
- With the stick in, the fight cost is already off.
- Applied in the stop and reversal transients as well, the suite moved roll rates by −6 … +4 % and stop times by −0.3 … +0.3 s. That is a retune of the lateral feel, not this fix.
- The settled flag is the FCS's existing one; nothing new is scheduled.

## Bench (0.9.0 → 0.9.1; the same numbers on the 0.8.3 base)

**Engine down, stick centred (last 8 s of 16):**

| Case | Roll | Yaw − command | Unmet roll / yaw (`gUnmetX/Z`) | Bank drift |
|---|---|---|---|---|
| condition 0.4, 275 km/h | −0.41 → **0.00** °/s | −0.33 → 0.00 | 2.44 / 0.82 → 0.00 / 0.00 | −3.9° → −0.1° |
| condition 0.2, 275 km/h | −0.13 → **0.00** | −0.23 → 0.01 | 0.74 / 0.51 → −0.05 / 0.01 | −1.6° → −0.1° |
| dead engine, 550 km/h | 0.00 → 0.00 | 0.00 → 0.00 | 0 / 0 | identical |

Rate standard deviation stays at 0.00–0.02, so no jitter is added.

**The fight cost's job** (0.8.3 base): engine down, push and release at 200 km/h:
- No crossed set: the wing differential stays under 5°, no tail is past 20°.
- The creep is gone: −0.36 / +0.25 → ±0.02 °/s.
- Engine killed at 200 km/h: same crossed-wing time and sideslip.
- Engine-out hard pull at military power: end IAS 38.0 against 37.7 m/s, same crossed flaperons and splits. No extra drag.

**Regression suite, 4 samples per variant** (1500 / 1490 / 1510 / 1520 m, because cases flip between outcomes on a 10 m change):

| Block | Comparisons | Beyond noise (\|t\| > 2.5 and above the per-metric floor) |
|---|---|---|
| sweep (32 stops / reversals) | 154 | 5, all in two cases (below) |
| reg (20) | 120 | 2, one case (below) |
| cork (20) | 120 | 0 |
| dmg (32) | 210 | 1, better: leaf stop at 296 km/h, one tail lost, sideslip after 3.1–4.5 → 2.6–2.9° |

- **Sweep, 400 km/h, 0.40 pull, roll stop and reversal: worse.**
  - Sideslip at the roll's end: 7.0–7.9 → **8.0–9.1°**.
  - Sideslip at the stop: 7.1–8.0 → **8.2–9.7°**.
  - The roll is faster: mean roll rate 100–113 → 111–120 °/s.
  - Mechanism:
    - The settled pull before the roll ends with the tails 0.06° and the nozzles 0.07° different.
    - 0.2 s into the roll entry, 0.9.0's allocator picks the other tail branch (one tail 35° apart), from rest identical.
    - The measured pace window then lets the roll target rise further (in the 1500 m run, 170 against 122 °/s).
  - Nothing of this fix is active there: the stick is in and the fight cost is off.
  - It is the roll/sideslip balance of the 0.9.0 governor (its known open item), tipped by a sub-0.1° difference in the starting trim.
- **Sweep, 300 km/h, 0.40 pull, roll stop: better.** Sideslip 5.8–6.7 → 5.7–5.8°.
- **Reg, push-then-pull-through + roll at 450 km/h:** mean roll rate 94–109 → 109–118 °/s; peak AoA 26–34 → 26–27°.

**Measured on the 0.8.3 base too:** 0 of 605 comparisons beyond noise there. The 400 km/h branch flip appears only with 0.9.0's governor.

## Telemetry
No new columns. With the stick centred, 0.9.0's `gUnmetX` / `gUnmetZ` (the allocator's unmet) and `nuR` / `nuY` should now sit at about 0 with an engine down.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `3876f826a51a24dffbfdf779e746adb5`) and `meta.json` 0.9.1. 0.9.0 is kept as `.dll.0.9.0.bak`.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.9.1-release`. The source zip includes the bench with the two bench fixes.
- **About the 0.9.0 source zip:** it does not rebuild to the shipped 0.9.0 binary byte for byte (8d6afddf against e5f96d04). Comparing every method body, the only difference is a compile-time helper attribute (`IgnoresAccessChecksTo`) included in the zipped tree. 0.9.1 is built from that tree. Against the rebuilt 0.9.0, the 0.9.1 DLL differs only in the allocator (`SolveCore`, the new `Core`, `E`) and the one-line settled hook in `FcsController.Tick`.

## Not checked
- The 0.9.0 governor's sideslip (its open item) is not addressed here.
- The F-22E and the Revoker. If their released-stick fight cost is on, they would stall the same way under a differential trim (engine down, asymmetric load, damage). The F-22E turned `RollFightReleased` off in 2.3.2.
