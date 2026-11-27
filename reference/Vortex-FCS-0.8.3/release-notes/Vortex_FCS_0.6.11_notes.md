# Vortex FCS 0.6.11 (23 Nov 2026)

The F-22E's push governor (from Raptor 4.5.1, same code), fitted to this jet's yaw. Also: the STOVL aero model's fore-aft channel in the translation feed-forward, and a faster C# allocator for players without the native core. Same attested native core, embedded.

**Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (22 Nov)
- 22:24: "the corkscrews held and release pushovers are not very smooth and sideslip is not very well managed. Is the reference capacity governor working properly? Check the curve quality smoothness, for every axis ... the entire thing."
- 22:31: "i dont want extra logic, i want the same gov as the raptor, it works well on the raptor, so it should work well on the vortex, given the right model."

## 1. What the 22:19 log showed (0.6.10)

**Push-overs.** There were 8, from 54–65° AoA at 42–94 m/s. They bottomed out at 9–29° of sideslip.
- **Cause: the wrong yaw model.** The push plan came from the Revoker. It assumed a 40 °/s² yaw shed at every speed (the Revoker's fit). This jet sheds far less. In the log the nozzle's yaw bearing sat on its 15° braking stop through every push, and the rudders swung from holding the corkscrew to braking in 0.3–0.6 s. Together they shed 3.3–19.4 °/s².
- **Correction (24 Nov).** An earlier version of these notes said this jet flies with the yaw nozzle off. That is wrong. `YawTvc` false only removes the engine's stock yaw vector; the swivel nozzle's yaw bearing (±15°) is a yaw effector in normal flight as well as in the hover. The fit below is unaffected: it is the measured shed, nozzle and rudders together.
- **Effect.** The plan pegged the nose-down rate at 60 °/s. At the bottom it left 17–36 °/s of yaw still on board.

**Governor code.** The Vortex had the F-22E 3.0.0 governor. It was missing everything from 3.1.4 to 3.1.8 (the push-over floor and the hand-off fixes).

**Reference capacity governor (pace window).**
- It runs the F-22E's logic. The Revoker's capacity-model inputs are kept: every yaw effector, the stiffness and damping credit, reach over all effectors.
- At the positive corkscrew entry (135.5–137 s), the sideslip margin is used up: `paceMLeft` is 0 with 4.4° of sideslip against a margin of 4. So the gate engages, which is the governor doing its job.
- The roll target then cycles as the margin comes and goes, about every 0.3 s. The body roll went −90 → −23 → −46 → −29 °/s.
- The F-22E rule switches (`PaceWeakAxis`, `PaceRollSlew`, `PaceRollGate`, `SweepMarginGate` set false) changed nothing on bench replays of that window. **Not fixed in this release.**

**`slipTol` stuck at 25.** That is the telemetry's default value; the code that sets it doesn't run in this mode. It is not a fault.

## 2. The change: the F-22E 4.5.1 push governor (code merged, not retyped)
| piece | F-22E version |
|---|---|
| `PushOverMinRate` 23 °/s floor on the plan (dev cfg "4. Gains") | 3.1.4/3.1.5 |
| `PushGovFade` (toward-zero cap fades in with the velocity roll, 5→15 °/s) | 3.1.6 |
| `SweepFloorPushMin` (the sweep cap never holds the nose under 23 °/s × push) | 3.1.6 |
| `PushPlanDoneOpen`, `SweepSideKept`, `PushGovPaceYaw`, `PushGovFadeGrow`, `PushNoRetreat`, `SweepZeroBlend`, `PushPlanThrough` | 3.1.7 |
| `PushPlanRise` 15 → 40 °/s² | 3.1.8 |
| `PushPlanStep` (whole method; `PushPlanRamp` present and off, as on the F-22E) | 4.5.1 |

All of these stay out of the way with fins damaged, as on the F-22E. The F-22E's `PitchTrajLead` is off there and is not merged.

## 3. The model (this jet's numbers)
- **Shed.** The F-22E's form, K · IAS^1.5 (IAS in m/s), fitted to the log's 8 push-overs (nozzle bearing and rudders together): **K = 0.0156** (F-22E 0.028).
  - Residual rms is 2.7 °/s².
  - A free exponent fits 2.26 (rms 1.9), but that's not used on 8 samples.
  - Provisional: refit once more push-overs have been flown.
- **`PushPlanSlip`** 4°, the F-22E's value (was −1, following PushoverSideslip 8).
- **Kept from the Revoker: `PushPlanStopsRoll`.** Your Revoker rule: a planned push also bleeds a held roll.
  - The F-22E rule instead paces the held roll on the plan's yaw budget.
  - Bench CKN (full push with the roll held through to the negative corkscrew): sideslip 6.6° with the Revoker rule, 12.4° with the F-22E rule.

## 4. Bench (0.6.10 → 0.6.11)
**The 8 logged push-overs, free replays.** The bench reads sideslip 2–3× low at high AoA (the fin's sidewash, a known bench limit): the game showed 13–29° where the 0.6.10 replay shows 3–10°.

| push | sideslip, game / 0.6.10 / 0.6.11 | time to 10° AoA, 0.6.10 → 0.6.11 |
|---|---|---|
| 72.8 s, 44 m/s | 28.8 / 3.4 / 1.6 | 1.20 → 1.22 |
| 77.3 s, 73 m/s | 9.2 / 6.9 / 6.1 | 2.12 → 2.07 |
| 87.9 s, 75 m/s | 13.0 / 5.7 / 5.2 | 1.28 → 1.83 |
| 107.0 s, 67 m/s | 22.1 / 7.9 / 5.2 | 1.37 → 1.77 |
| 111.5 s, 80 m/s | 14.4 / 8.1 / 4.2 | 1.00 → 1.33 |
| 116.3 s, 73 m/s | 14.7 / 9.2 / 7.5 | 1.80 → 2.43 |
| 139.9 s, 69 m/s | 27.7 / 9.7 / 4.0 | 1.38 → 1.87 |
| 150.3 s, 94 m/s | 13.7 / 4.7 / 2.7 | 1.05 → 1.38 |

- **Shape of the push.** At these speeds the plan's own rate (14–20 °/s) is under the 23 °/s floor, so the push runs at the floor. The rate ramps in, holds at 23, then rises to 35–40 °/s as the yaw sheds (87.9 s replay). There is no dip in the held push.
- **Cost.** It takes 0.3–0.6 s longer to reach 10° AoA than 0.6.10's 60 °/s push.

**Scripted CTOL cases (ct.sh), peak sideslip:**
- CKN (pull + roll, then push with the roll held): 10.0 → 6.5.
- CKR (push-over of a corkscrew, roll released): 6.6 → 6.4.
- RP and NP: identical.

**Regression set.** Only the push cases change:
- Better or about the same:
  - push+roll 300: 4.2 → 4.3;
  - pull-then-pushover 350: 5.3 → 5.3;
  - push-then-pullthrough 350 / 450: 5.8 / 5.5 → 5.6 / 5.4;
  - corkscrew push 170: 2.3 → 2.0;
  - roll-then-push −55: 3.5 → 2.6.
- **Worse: push+roll 200, 1.7 → 3.2.** The full push through zero now carries −54 °/s past zero, and the far side's cap pulls it back. Over the whole case the rudder reversals went 2 → 11 and the peak sideslip after the roll release 2.3 → 5.9.
- Sweep set and corkscrew set: identical.

**Damage suite.**
- Clean push-overs: released 560 6.2 → 5.4, held 560 4.3 → 3.8, released 600 15.0 → 9.7, held 600 7.5 → 6.3.
- **Damaged cases: not tested.** The suite uses the Revoker's damage data, which has no Vortex part names, so every damaged case errored in both builds. (An earlier version of these notes said "identical"; that was wrong.) A static both-fins-gone check (bench `detachOnly=1`, added 24 Nov) is in the next section.

**Both fins and rudders gone (static detach, 0.6.11 only, 400 km/h, held left roll at 20–26° AoA).**
- The inboard elevons cross the outboard pair, the Raptor's crossed split: inboard L/R +18/−14°, outboard −32/+3°. Their rolls cancel and the drag yaws the nose into the roll.
- The nozzle's yaw bearing sits on its −15° stop.
- Peak sideslip 4.6° (clean 3.0°).

**Worse, by the F-22E's 3.1.7 design (`PushGovPaceYaw`): a pull from negative AoA during a roll stop.** Replay of the log's 78.5 s window: sideslip 9.6 → 12.7 (the game showed 13).
- Before, the pace slowed the pull toward zero AoA even with no excess yaw. On the F-22E that was the "q 40 → 25 → 40" notch 3.1.7 removed.
- With `PushGovPaceYaw` = false it is 9.7 again.

**Whole-manoeuvre curve check** (every held-stick segment, wiggles over 3 °/s on q/p/r and over 1° on sideslip, actuator reversals):
- ct cases: CKR's pitch wiggles 2 → 0, CKN 2 → 2.
- Regression, pitch wiggles:
  - corkscrew push 170: 8 → 4;
  - roll-then-push −55: 4 → 4;
  - **pull-then-pushover 350: 0 → 2** (worse);
  - push+roll 200: as above.

## 5. STOVL: the aero model's fore-aft channel in the translation feed-forward (`AmUseFFH`, on)
- **What it is.** The 0.6.7 model has been learning the fore-aft aero force since 0.6.7, but nothing used it. It now feeds the fore-aft request, the same way the vertical channel feeds the vertical one. The lead is the measurement's delay: the fore-aft force comes from the nozzle bearing and the tilt, not the RPM.
- **Bench, 30 and 45 kt translations and stops at 12.1 and 13.8 t, game-like jitter, 3 seeds.**
  - The feed-forward peaks at 0.002–0.007 m/s². With the thrust's lead (`AmFFHThrustLead`) it is 0.006–0.022.
  - Height, vertical speed, speed tracking, attitude roughness and stop time are identical to two decimals. No gain, as you expected.
- **Off = 0.6.10, bit-identical** (checked).
- **The side axis is not done.** The model has no side channel: it drops the sideways airflow.

## 6. C# allocator (players without the native core, e.g. a C#-only build)
- **Speed on Mono.** The game's Mono runtime, same 2297 recorded STOVL solves: **6.9 → 1.05 ms per solve** on this machine (3.9 → 0.50 ms on the first one).
  - Cached roll/pitch terms, group member lists, in-place elimination.
  - Exact early exits in the pair search.
- **Same results?**
  - On .NET: bit-identical on the bench.
  - On Mono: not bit-identical. The replay checksum moves 0.014 %, because the reordered sums round differently in Mono's double arithmetic.
- The bench-only dump and profiling code is stripped from the shipped source.
- **The native path is unchanged.**

## Files
- `FcsController.cs`: the push governor and the model.
- `Plugin.cs`: the `PushOverMinRate` dev key.
- `Stovl.cs`: `AmUseFFH`.
- `Allocator.cs`.
- `FcsStandalonePlugin.cs`: version.
