# Handoff: porting the F-22E reference-capacity governor (3.0.0) to the Ifrit and the Revoker (4 Nov 2026, 22:10)

Start a new chat from this. It replaces `claude/Governor_port_handoff_Ifrit_Revoker.md` (31 Oct / 1 Nov). That handoff covered the **first** port, of the 2.6.6
governor. Read it only for history and for §8 here. Tom's requirements for each jet are in project memory, in the Ifrit FCS, Revoker FCS and F-22E FCS
area files. Read them before any law work.

Tom (4 Nov, 22:01): **"make handoff for porting the reference gov to ifrit and evoker"**. "Evoker" means the Revoker.

---

## 0. State right now

| jet | installed | md5 | flown? |
|---|---|---|---|
| F-22E | **3.0.0** (= 2.44.1, renumbered) | `096faf106b9d8edfc3a8bf9ef070823c` | 2.41.0's law flown and accepted (11:50); 2.43/2.44 law not flown. A **21:58 log on 3.0.0** exists, unread. |
| Ifrit | **0.8.0** (0.7.3 + the 2.44.0 protection override) | `c153773b0b4a49329711c31101cb3fe4` | not flown on 0.8.0 until tonight: an **Ifrit 22:02 log** exists, unread. |
| Revoker | **0.5.0** (0.4.3 + the same override) | `c8fed7073a0614bcc1e3879bf181d0bd` | not flown on 0.5.0. Last Revoker logs: 1 Nov. |

- **Port base trees on Tom's PC (made for this handoff):**
  - `C:\Games\NO mods\Ifrit-port-base-0.8.0.zip`
  - `C:\Games\NO mods\Revoker-port-base-0.5.0.zip`

  Each is the up-to-date portable bench (closed-loop replay + `pinPitch`, the damage suite, the leaf-calibration tools, the root-vs-CG log fix)
  with the **0.8.0 / 0.5.0** FCS sources inside. Both were built here before zipping (`bench.sh build`: "Build succeeded").
  - Do **not** use the release folders' `Source.*.zip`: they carry the old bench (no `Replay.cs`, no `Damage.cs`).
  - Do **not** use the older `*-bench-portable.zip` beside them either: it carries 0.7.3 / 0.4.3.
- **F-22E source of record:** `C:\Games\NO mods\F22E-FCS-3.0.0-release\F22E_FCS_3.0.0_src.zip`. The tree is still called
  `F22E_FCS_2.31.1_src`.
- **Merge base:** `C:\Games\NO mods\F22E-FCS-2.6.7-release\F22E_FCS_2.6.7_src.zip`. The reason is in §3.

## 1. What "the reference-capacity governor" is in F-22E 3.0.0 (what gets ported)

In plain terms, the governor is the part of the FCS that slows a command when one of the axes can't keep up. It is built on one number per axis and
side: **how much angular acceleration the jet can still make**, called the capacity. Lineage of 3.0.0 is 2.27.0 → 2.30.0 → 2.31.x → 2.34.0 → 2.35.x →
2.40.0 → 2.41.0 → 2.43.0 → 2.44.x. (2.28/2.29, 2.32/2.33, 2.36–2.39 and 2.42 were rejected or dropped.) Anchors are line numbers in 3.0.0's
`FcsController.cs` unless noted.

| # | piece | version | what it does | anchor |
|---|---|---|---|---|
| 1 | **Capacity estimator** | 2.7.5–2.22.0 | Capacity toward a side = measured ω̇ (filtered) + the committed increment (counted only as far as the servo arrives within `CapHorizon` 0.15 s) + an **allocator probe**. The probe is the real `Solve` re-run from this tick's commands with +400 deg/s² of extra yaw or +1500 of roll asked, then read per effector group × a fixed gain, less the allocator's unmet. Also: the two-axis probe along u = (cos α, 0, sin α) (2.14/2.16); `capZeP/N` = Lerp(capS, capZ, rudW); the trade's 3-tick low-pass `capTrZeP/N` (2.21); the no-stab read (2.22). | `CapacityEstimator.cs` (whole file, 290 lines); called at 5317; field at 804 |
| 2 | **The trade** (`PullRollTrade` 0.7, `TradeCapShare` 0.7) | 2.8.0 / 2.21.0 | Rolling pull. Demand = pS·cos α·α̇ against the stick's roll ask. A shortfall goes 70 % off the roll target, the rest off the AoA rate. It replaced the AoA-rise cap (deleted in 2.21.0). | 7047 |
| 3 | **Steady-rate cap + window** | 2.5.8/2.6.9, capacity from the estimator; window 2.7.x/2.17 | The roll-rate cap at `RollPaceRateBlend` with the reversal swing model (`RateCapNeedTravel`). The window is latched on the allocator's unmet (`PaceSatFrac`), lead 1/KYaw (`PaceLeadT`), no-retreat, paced where rudW < 1, no-fin stop. | rate cap ~9237; window in `LateralLaw` (8207–) |
| 4 | **Released-stop yaw brake** shaped to the rudders' unwind | 2.31.0 | Cut the snap-roll nose wobble. Flown and accepted: "within acceptable range". | `LateralLaw` |
| 5 | **Air data at the CG** (`AirDataAtCg`, `FlightPathAtCg`) | 2.30.0 | The estimator and the laws read the CG's velocity, not the root rigidbody's. The root is 2.48 m ahead of the CG on the Raptor, **0.62 m on the Ifrit, 0.95 m on the Revoker** (`Bench_leaf_calibration_results.md` §2). | 49, 3791–4127 |
| 6 | **Push governor** (`PushGov*`) | 2.34.0 | Paces what the plan doesn't take: pushes with fins damaged, pushes under 15 deg/s of yaw, and the no-fin nose hold. | 8601–8606 (the sweep block in `LateralLaw`) |
| 7 | **Push plan** (`PushPlan*`) | 2.35.0–2.35.3, 2.40.0 | A push-over is planned once at its start. Shed rate capPlan = clamp(`PushPlanK` · IAS^`PushPlanExp`, 6, 80), with K = 0.028 and Exp = 1.5 **fitted to 93 F-22E push-overs**. Push time T = (r − rLeft)/capPlan with rLeft = √(2·capPlan·`PushPlanSlip` 4°). Body pitch rate Q = (\|α\| − 10)/T − ωV, ramped in, rise-only at 15 deg/s², ramp-out below 10°. The released roll bleeds over T; the held roll is paced by the plan's yaw budget D (2.40.0). Under a plan it owns pitch: the 60 deg/s ceiling and the held band stand aside (2.40.0). | `PushPlanStep` 8149; bound 6947–7010; bleed and held 8822–8823 |
| 8 | **Allocator reach pass + two-horizon pick** (`ReachPass`, `TwoHorizon`, `TwoHorizonPath`) | 2.41.0 / 2.43.0 | The stab keeps its command, and the other effectors are placed against where it will be in 0.1 s. Otherwise the stab is moved only as far as it gets in that time, whichever plan costs less over the transit and at arrival. It **replaced the stab rate bound** (`ReleasedStickStabRateBound`). Applied to `SurfaceKind.Stabilator` only, under a plan or where relG = max(relStick, pitchNeedDn)·stabPitchFrac > 0.01. | `Allocator.cs` `Solve` / `ReachFrom`; 4398–4403 |
| 9 | Pitch priority under the plan (`PushPlanClipKey`, `pitchNeedDn`, `sweepHoldWin`) | 2.9.0 / 2.9.2 / 2.36 | Allocator pitch weight and the stab stall window during a capped nose-down sweep, keyed to the plan's bound. | 4408–4432; 7010 |
| 10 | Fin-loss devices (scaled by fin health, inert on the clean jet) | 2.16–2.20 | Coordination floor, `ReleaseCoordinate`, `PaceNoRetreat`, paced-by-rudder, nose hold, `YawClampStabReach`, `PaceStopNoFin`. | various |
| 11 | `StickCancelsLimit` | 2.44.0 | **Already on both jets** (Ifrit 0.8.0 / Revoker 0.5.0). Expect a trivial conflict, §3. | 6874 |

**What 3.0.0 no longer has** and both stock jets still do: the AoA-rise cap (`StopSlipPitchK` chain, deleted 2.21.0), the old kinematic/shed sweep caps
(`capSweep` / `capShed`, replaced 2.28→2.34/2.35), the 2.0.15 sideslip guard and yawSat guard (deleted 2.11.0), and the stab rate bound (2.43.0).

**Tom's verdicts on the F-22E pieces** (from the flights): released push-over accepted from 2.35.2 ("good enough now"). 2.41.0 accepted: "smoothness
acceptable, sideslip higher but acceptable, push faster … good trade". Snap-roll wobble acceptable on 2.31.0. Held push 2.36.0: "smooth".
The governor as a whole was never flown on a second airframe.

## 2. What the Ifrit and the Revoker have now

Both are the **2.6.6 port** (F-22E 2.4.16 → 2.6.6 merged into each controller, 31 Oct / 1 Nov: Revoker 0.3.0, Ifrit 0.6.0), plus their own inherent
fixes from 1 Nov:
- `SlipFrameAlloc`: the allocator weighs the lateral residual in the velocity frame. On; the F-22E regressed with it, so it never got it.
- `RevSim`: the rate cap as a forward model of the reversal, bisected for the largest rate that stays inside the margin.
- `PaceReachAll`: the window's reach is computed over every effector.
- The 2.6.7 trade ask gate (`qSweepAsk`).
- `RollStopSideslip` 4 → **8** once (`GovernorRevision`).
- On the Ifrit, `RollPaceRateBlend` **0**; on the Revoker, 0.4.

Then 0.7.3/0.4.3 (the chart re-registration FPS fix) and 0.8.0/0.5.0 (the protection override).

What they **lack** (grep counts, F-22E 3.0.0 vs Ifrit / Revoker): the estimator (`capEst` 11 / 0 / 0), the trade on it (`TradeCapShare` 7 / 0 / 0), the
push governor and plan (`PushPlan` 14 / 0 / 0), the reach pass (`ReachPass` 5 / 0 / 0), the 2.7.x window (`PaceSatFrac`, `PaceLeadT` 0), air data at the CG
(0), the fin-loss devices (0), `pitchNeedDn` (0).

What they **still have** that 3.0.0 replaced: `StopSlipPitchK` (5 / 5), `capSweep` / `capShed` (7 / 7), `ReleasedStabRateBound` (3 / 3), the sideslip guard.

The jets' own axes, and why §4 exists:
- **Ifrit:**
  - `elevator_L/R` are canted all-moving tails, classified `Stabilator`. Their **differential is the yaw axis**: the synthetic `yawEffs` effector, 0.6.0.
    The fins carry no control surface, so there are **no rudders**.
  - `aileron_L/R` are `Aileron`. `flap_L/R` are the decelerons, `Flaperon`, plus `SplitEffector` (split-drag yaw).
  - TVC pitch (`TvcEffector`) plus **roll** (`TvcDiffEffector`).
  - MaxPitchRate 48; AoA limit 65.
- **Revoker:**
  - `canard_L/R` are `Stabilator` (3-axis canards).
  - A single `rudder` is `Rudder`.
  - `aileron_L/R` are the outer elevons (`Aileron`). The inboard elevons are `HingeFlapEffector`, not a `SurfaceEffector`. Plus `SplitEffector`.
  - TVC pitch plus **yaw** (`TvcYawEffector`).
  - MaxPitchRate 48; AoA limit 70.
  - Elevon stall staggered: inner 26° knee-preserving, outer 45.

## 3. Port mechanics: a three-way merge, measured

The F-22E delta to bring over is 2.6.7 → 3.0.0:
- `FcsController.cs`: 1,917 changed lines (8,425 → 9,692);
- `Allocator.cs`: 146;
- `Effector.cs`: 7;
- `Plugin.cs`: 97;
- plus `CapacityEstimator.cs` (new, 290).

The stock jets diverged from F-22E 2.6.7 by:
- Ifrit: FcsController 1,896, Allocator 25, Plugin 984 lines;
- Revoker: FcsController 2,790, Allocator 27, Plugin 1,384.

**Why 2.6.7 is the base:** all three jets were at the 2.6.6 merge plus `patch_tradeask` (the 2.6.7 gate). So 2.6.7 is their last common ancestor.

**Trial merge done for this handoff.** It used `git merge-file` with ours = jet, base = F-22E 2.6.7, theirs = F-22E 3.0.0, with namespaces normalized. Nothing
was kept or built.

| file | Ifrit | Revoker |
|---|---|---|
| `Allocator.cs` | **clean** | **clean** |
| `Effector.cs` | **clean** | **clean** |
| `Plugin.cs` | 8 hunks, 136 lines | 8 hunks, 150 lines |
| `FcsController.cs` | **18 hunks, 861 lines** | **21 hunks, 1,071 lines** |
| `CapacityEstimator.cs` | new file, needs §4.1 | new file, needs §4.1 |

**Where the FcsController conflicts sit** (Ifrit hunk numbers; the Revoker's have the same content, shifted, plus 3 more):
1. Allocator axis-weight telemetry (2.4.6 → 2.9.0 `pitchNeedDn`).
2. Statics block (`CoordStopErr0`, among the new statics).
3. **`StickCancelsLimit`**: both sides wrote the same lines. Take theirs.
4. The trade (`tgtF`): 2.6.7 + gate vs 2.8/2.21 on the estimator.
5. The push plan step (new on theirs).
6. `num11raw` / the lateral stick shaping around the push bleed.
7. The sweep block: the jets' 2.6.6 sweep caps vs the 2.34 push governor.
8. The sideslip guard (deleted on theirs).
9. Probe / effector loop.
10. **The rate cap**: the jets' `RevSim` (ours, 1 line here, the call) vs 3.0.0's 185-line `PaceMarginReach` / reversal model.
11–15. Window, loR, `rcAOpp`, the paced target, the brake, and the 2.3.0 coordinated stop (ours 82 lines).
16–18. The CSV header and row.

**Rules for the merge:**
- Resolve by **function**, not by side. The governor's consumers come from theirs. The per-jet effector code (`yawEffs`, splits, `TvcDiffEffector`,
  `TvcYawEffector`, hinges, canard logic, VMC, brakes) and the jets' own airframe features come from ours.
- Decide **`RevSim` vs 3.0.0's rate cap** consciously (hunk 10; §7, decision 2).
- A clean hunk is not a correct hunk. Anything theirs keyed to `SurfaceKind.Stabilator` or `Rudder` must be re-read for each jet's surfaces (§4).
- Keep a switch for each imported device only while benching. Delete the switches before release, as in 2.44.1.

## 4. Per-jet adaptation: the actual design work

### 4.0 Count everything, on the sideslip axis, at every AoA (Tom, 4 Nov 22:14: read this first)

Tom: *"everything on the airframe can affect yaw and sideslip stiffness, but you often only read the obvious ones, especially only at low AoA."*

He is right, and this project has a track record of exactly that mistake:
- **The first port** counted rudders only, then left out the trailing edge, which is the yaw axis at high AoA on both stock jets (§8).
- **3.0.0's estimator** drops every effector that isn't a `SurfaceEffector` (§4.1).
- **The FCS's own sideslip probe** (`SlipMomentAt`) under-reads the Raptor's sideslip stiffness kβ 3–4× above 50°. It sums raw per-part charts at uniform flow (`Bench_leaf_calibration_results.md` §6).
- **The 2.24 capacity read** collapsed while the airframe was coordinating itself (58 deg/s² delivered, 12 read).
- **On 4 Nov at 22:10** I described the Ifrit's yaw axis as "thin" from yaw-only numbers, which left out roll TVC.

**Rules for the port:**
1. **Work on the sideslip axis, not the body yaw axis.** The quantity that matters is the sideslip acceleration, β̈ ∝ ṗ sin α − ṙ cos α. So score each effector and each passive term by Mz cos α − Mx sin α at the actual AoA. Above about 40°, a body-roll moment is mostly a sideslip moment. That makes the roll surfaces, roll TVC and the stabs'/tails' differential sideslip effectors, whatever they are called.
2. **Every effector counts:**
   - surfaces of every kind;
   - TVC, pitch differential and yaw;
   - splits;
   - hinged elevons;
   - canards, through their roll and yaw.

   No `Group()` that returns −1 for a lateral effector.
3. **Passive terms count:**
   - Lβ·sin α and Nβ·cos α (dihedral effect and weathercock stiffness, on the sideslip axis);
   - forebody, LERX and fuselage side force;
   - the belly or top facing the air in a high-AoA rotation (Tom, 3 Nov);
   - roll and yaw damping;
   - the airframe's own self-roll in a sweep.

   The estimator's measured term sees them only as they act *now*. Where a consumer needs them at a predicted state (the push plan, the rate cap), say so and model them, from the plant / logs, not from a guessed rudder fade.
4. **Show it across the whole AoA range.** For each jet, before any law: a per-part table of sideslip-axis authority and stiffness at 0–70° and 2–3 speeds, checked against the logs (the bench's `eqerr` per AoA band). A number quoted at one AoA is not a description of the jet.

**First cut of that table: the Ifrit's effectors on the sideslip axis** (bench `ieff`, full thrust, 1,500 m; deg/s² one way, each effector at its best end, so an upper bound):

| IAS (km/h) | AoA | roll TVC | ailerons | decelerons | tails | splits | aero total | roll TVC share |
|---|---|---|---|---|---|---|---|---|
| 150 | 20° | 16 | 13 | 3 | 11 | 3 | 31 | 34 % |
| 150 | 35° | 26 | 17 | 6 | 12 | 4 | 39 | 40 % |
| 150 | 50° | 35 | 24 | 10 | 8 | 5 | 47 | 43 % |
| 150 | 65° | 42 | 25 | 10 | 3 | 7 | 45 | **48 %** |
| 300 | 65° | 42 | 100 | 40 | 13 | 26 | 179 | 19 % |
| 450 | 65° | 42 | 226 | 90 | 28 | 59 | 403 | 9 % |

- Roll TVC is about half the Ifrit's sideslip authority in the slow, high-AoA corner, and the only part that doesn't fade with speed.
- Caveats:
  - the nozzles share travel with pitch trim at 50–65°;
  - roll TVC needs thrust;
  - the Ifrit bench is thinly calibrated.
- Passive terms are **not** in this table yet. They are the next column (the 1 Nov regressions put the Ifrit's high-AoA Lβ and kβ at −2.8 / +3.9 in game, unconfirmed by the bench check).
- The Revoker's table, with yaw TVC, canards, hinges and the rudder, is still to be made.

### 4.1 The estimator's effector groups (biggest item)

**The problem.** 3.0.0's `CapacityEstimator.Group()` returns 0 for `Rudder`, 1 for `Stabilator`, 2 for every other `SurfaceEffector`, and **−1 (ignored) for
anything that is not a `SurfaceEffector`**.

On the F-22E that only drops the pitch-only TVC. On the stock jets it drops real lateral effectors from the committed and probe terms:
- the Ifrit's roll TVC (`TvcDiffEffector`) and splits;
- the Revoker's yaw TVC (`TvcYawEffector`), inboard elevons (`HingeFlapEffector`) and splits.

The measured ω̇ term still sees them, but only after they have acted.

**Tom's rule (2 Nov):** the capacity must count every yaw source: indirect and passive ones, and "the roll TVC on the Ifrit as AoA increases since it can
suppress sideslip".

**The port must:**
1. Add groups for TVC lateral, splits and hinges, with their own gains in yaw and roll.
2. Map the Ifrit's tails (`Stabilator`, also its yaw axis) correctly.

### 4.2 Gains

The F-22E's fixed gains (rudders 1.0, stabs 0.7 / 0.65, flaperons 1.0) were identified from the F-22E's 2 Nov logs with `bench fcsmom` + `an/cap_gain.py`.
"Gain" means the acceleration achieved per unit of moment that the tables model. Neither stock bench has `fcsmom` (no `FcsMom.cs` in `bench/sim`), so it must
be ported.

The stock jets' logs are 1 Nov only, on the 2.6.6-port builds, with the lateral loops active, so the derivatives carry a closed-loop bias. Expect to need
a fresh flight per jet (§5).

### 4.3 rudW

`capZeP/N` = Lerp(two-axis yaw read, yaw-only read, rudW), with rudW = `rudderAuth` × clamp(yawHealth / 0.5). It means "the rudders are a yaw axis of
their own below the fade".
- **On the Ifrit there are no rudders.** `rudderAuth` is a copied 50–60° fade.
- **On the Revoker** the single fin's rudder is a minor yaw source at high AoA: the aileron pair makes ±68 deg/s² of yaw at 350 km/h / 70°, the rudder ±12
  (Governor_port handoff §3).

Redefine rudW per jet from what is actually independent of roll. The candidate stated in §7, decision 3, is the yaw-only probe's roll collateral. Don't
carry the rudder fade.

### 4.4 The reach pass and the stab windows

3.0.0 applies `reachTau` (0.1 s) and the sweep stall window (`sweepHoldWin`, `StallWindow` at peak + `RpMainStallMargin`) to `SurfaceKind.Stabilator`:
- On the **Ifrit** that is the tails, the same role as the stabs. It should transfer, but the tails are also the yaw axis, so the reach pass now shapes
  yaw too.
- On the **Revoker** that is the **canards**:
  - ahead of the CG, so a stall goes the opposite way in pitch;
  - travel +60 / −45;
  - the elevons carry much of the pitch (`ElevonPitch`).

The reach pass's premise is a slow pitch effector crossing its lift peak while the others are placed against its destination. Whether that is the canards,
the elevons or both is a bench question. The window on a canard is a new device, not a port. Decide with the bench, and default to the reach pass on the
effectors whose transit exceeds reachTau.

### 4.5 The push plan's constants are an F-22E fit

`PushPlanK` 0.028 and `PushPlanExp` 1.5 (the yaw shed vs IAS from 93 F-22E push-overs), `PushPlanA0` 20°, `PushPlanR0` 15 deg/s and `PushPlanSlip` 4° are
the F-22E's.
- **No push-over set exists for either stock jet** (`Bench_leaf_calibration_results.md` §7). Fitting K and Exp needs logged push-overs from 60–70° on
  each jet, released and held.
- Until then the plan would run on F-22E numbers, and **that is a schedule Tom didn't approve for a new airframe**. See §7, decision 1.

### 4.6 PushoverSideslip

Its default −1 means "= `RollStopSideslip`". That is 4° on the F-22E, **8° on both stock jets**. Ask Tom or set it explicitly. Don't let 8° in silently.

### 4.7 Don't bring over

- F-22E-only settings: nozzle rig and animation, the F-22E airframe lift reshaping, F-22E flaperon / LERX buffs.
- Tom's F-22E governor values: `RollPaceRateBlend` 0.4084507, `RollStopSideslip` 4. Each jet keeps its own current values (Ifrit 0.7 / 8 / 0 / 1;
  Revoker 0.7 / 8 / 0.4 / 1) unless he says otherwise.
- `FinLossAoACap` 65 as an F-22E value. Check what each jet has.

### 4.8 Fin loss

- The **Ifrit** has no rudders. "Fin loss" there is the tails (yaw and pitch at once), so the F-22E's fin-health devices (`yawHealth` / 0.5) need the
  Ifrit's own meaning of yawHealth.
- The **Revoker** has one fin.
- The damage suites (`bench.sh suite` → `out_dmg`) exist on both benches.

## 5. Order of work (recommended; §7 has the choices)

Each step is bench-checked before the next. Version bumps: minor for a law change, patch for telemetry and clean-ups.

1. **Merge + estimator as telemetry only** (Ifrit 0.8.1, Revoker 0.5.1):
   - the three-way merge with every imported consumer behind a switch, **off**;
   - the estimator running with the new groups (§4.1) and gain 1, logging the `ce*` columns;
   - air data at the CG.

   Proof: with the switches off, each jet's suite matches its 0.8.0 / 0.5.0 run, apart from the air-data change, reported separately. Tom then flies
   one log per jet with:
   - rolls and reversals at 15–35°, 250–350 kt;
   - full-aft rolling pulls;
   - the falling leaf;
   - **6–10 push-overs from 60–70°, released and held, at 150–250 kt** (for §4.5).

   This is the order Tom accepted for the F-22E on 2 Nov ("the estimator first, as telemetry only, validated on a flight log").
2. From those logs:
   - port `fcsmom` and fit the per-group gains (§4.2), checking the composite against the realised peaks (the F-22E target was ≤ 1.5× in reversals);
   - fit `PushPlanK` / `Exp` per jet;
   - redefine rudW (§4.3).
3. **The law port** (Ifrit 0.9.0, Revoker 0.6.0):
   - switch on the trade, the rate cap and window on the estimator, the push governor, the push plan, the reach pass + two-horizon pick, and the fin-loss
     devices;
   - delete the devices they replace: the AoA-rise cap, the old sweep and shed caps, the sideslip guard, the stab rate bound;
   - then delete the switches.

   **Ifrit first, then the Revoker** (the F-22E fresh-start handoff's order).
4. Fly; then a clean-up patch (dead switches, retired keys dropped from the Dev file the way 2.44.1 did).

## 6. Bench: what it can and cannot judge (state it, never wave at it)

**Available on both stock benches** (port-base trees):
- `bench.sh build | suite <tag> [Name=value] | diff a b | fly … | replay <log> <t0> <t1> <out> [pinPitch=1] | replaycmp | eqerr`;
- the suite blocks reg / sw / cork / dmg;
- `BENCH_DATA` defaults to `bench/extract/{ifrit,rev}_airframe.json`;
- any `internal static` can be set as `Name=value`.

**Missing vs the F-22E bench:**
- `fcsmom`;
- the hybrid replay (`pinUntil`: pitch pinned until the push, free after), which the F-22E push-over work was judged on;
- the push scoring scripts (score43 / regcheck / idrun2 style);
- the utilisation invariant (`utilcheck.py`) and the rate-regression method (`claude/Rate_regression_method.md`, `claude/Rate_hold_invariant.md`).

**Port the rate guard first.** Tom (1 Nov): rate regressions must be caught by the bench, "on a newly ported airframe too". The first port slowed both
jets' rates and he caught it in flight (§8).

**Known limits** (`Bench_leaf_calibration_results.md` §7):
- **Neither stock bench can judge a leaf push-over law.**
  - Ifrit: +12 deg/s² nose-up bias at every AoA (+20 at 40–55°), the decelerons' logged local AoA 10–18° off.
  - Revoker: roll error 37 at 0–20°, yaw 6–9 everywhere.
  - Both: closed-loop-biased derivative tables.
- The F-22E bench fails the same test in pitch during the sweep. So push-over pacing on the stock jets is **flight-validated**, and the candidate must
  also be checked on each jet's own logged push states (`newcap.py` style) before it goes near the plugins folder (Raptor rule, 3 Nov).
- Container: 2 cores, so a suite takes about 10 min. Run variants as at most two parallel chains, and never rebuild `bench/bin` while a chain reads it.

**"Without tradeoffs"** (Tom, 4 Nov) means no suite case or replay metric worse beyond null-perturbation noise (about 0.5°; measure the noise per jet by
perturbing a horizon, as done for 2.43.0).

## 7. Decisions pending for Tom (ask before step 1, in one message)

1. **Staging:** telemetry first and a push-over flight per jet before the law (recommended, §5), or straight to the law port on the F-22E's gains and
   `PushPlanK`, fitted later (faster, but the plan runs on another jet's fit)?
2. **Rate cap:** keep each jet's `RevSim` forward model, fed the estimator's capacity (the stock jets' 1 Nov inherent fix, which the F-22E never had), or
   replace it with 3.0.0's reversal swing model? Recommendation: keep `RevSim` and give it the estimator. It was the stock jets' fix for the first port's
   slow rates, and 3.0.0's model still fails 4 of 9 utilisation bands on the F-22E.
3. **rudW / the yaw axis per jet:** derive it from the probes (what yaw the allocator can add without spending roll), not from the rudder fade. This changes
   a governor input on both jets, so tell him.
4. **`PushoverSideslip`** on the stock jets: 4° like the Raptor, or 8° (their `RollStopSideslip`)?
5. **`SlipFrameAlloc`** stays on the stock jets (merge keeps it; the F-22E regressed with it). Confirm.

## 8. Lessons from the first port (31 Oct – 1 Nov): don't repeat

- **The Ifrit has no rudders.** Merged as-is, the governor found no yaw axis, its capacity read zero and the rate cap sat on its 10 deg/s floor
  (0.6.0 notes).
- **The first rate cap's effector set left out the trailing edge**, which is the yaw axis at high AoA on both stock jets:
  - Revoker 350 km/h / 70°: aileron pair ±68 deg/s² of yaw, rudder ±12;
  - Ifrit 150 km/h / 65°: ailerons ±12.5, flaps ±4.8, tails ±2.2, splits ±2.

  Tom flew 0.3.0: "yaw and roll rate much slower across the whole AoA range … falling-leaf yaw 75 → 40–45 deg/s … I thought it is just a governor port".
  Any capacity must be the allocator's own answer over **every** effector (§4.1).
- **Allocator saturation traded roll for yaw** (Revoker 68°: roll asked −50, delivered +28). `SlipFrameAlloc` fixed it there; the F-22E's weak axis is
  roll, so it regressed there.
- **The uneven low-IAS roll** was the trade reading a measured rate as a command (fixed by the ask gate). Any consumer that reads a measured quantity
  where it means a command will do this again.
- **Verify what is installed by md5 against the release, not the tree.** The Raptor ran 2.4.16 for days while the docs said 2.6.6. The mod manager (nomm)
  has also replaced a plugin folder once. Check `BepInEx\LogOutput.log` for `Loading [… FCS x.y.z]` before reading any log.

## 9. Files, tooling, release

- **Container setup:**
  - stage `NuclearOption_Data\Managed` and `BepInEx\core` from Tom's PC to `/home/claude/game/Nuclear Option/…` (`build.sh` hard-codes that path);
  - `apt-get install dotnet-sdk-8.0 python3-pandas python3-numpy python3-scipy`;
  - unzip the two port bases and the F-22E 3.0.0 / 2.6.7 sources.
- **Game builds:** each tree's `build.sh <srcdir> <out.dll>`. **The assembly name comes from the output file name.** Build to `Ifrit_KR67_FCS.dll` /
  `Revoker_FS12_FCS.dll` exactly (a wrong name once gave a different md5).
- **Plugins:**
  - `…\BepInEx\plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (a `meta.json` sits both beside it and one level up; update both);
  - `…\BepInEx\plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (+ `meta.json`);
  - the Dev DLLs sit at the plugins root (`Ifrit_KR67_FCS_Dev.dll`, `Revoker_FS12_FCS_Dev.dll`).

  Install with the previous DLL kept as `.dll.<prev>.bak`. The bridge writes but cannot rename, so commit the old DLL under the `.bak` name. Check the
  installed md5. **If a DLL write fails** ("fetch or write failed", the game holding the file), retry once and re-check every file: tonight a failed DLL
  write left meta.json updated and the DLL old.
- **Release folders:** `C:\Games\NO mods\Ifrit-FCS-<ver>-release` and `Revoker-FCS-<ver>-release`. Each holds:
  - the DLL;
  - the main zip, which must sort first (`Ifrit-KR67-FCS-<ver>.zip` / `FS12-Revoker-FCS-<ver>.zip`);
  - the source zip, sorting last (`Source.*.zip`), **including the up-to-date bench**;
  - the notes;
  - `meta.json` (version + description).
- **Notes** go to the project as `claude/Ifrit_FCS_<ver>_notes.md` / `claude/Revoker_FCS_<ver>_notes.md`.
- **Ifrit/Revoker logs:** `…\BepInEx\Ifrit_FCS_<date>_<time>.csv`, `Revoker_FCS_….csv`.
  - Ifrit: 1 Nov 00:12, 01:04, 01:18, 01:48, 22:48, 22:51, plus **4 Nov 22:02** (0.8.0).
  - Revoker: 1 Nov 00:03, 00:21, 01:00, 01:23, 01:40, 22:45.

## 10. Tom's rules (binding)

- **No on-top fixes.** Replace devices by modelling what they cover.
- Build or install only when the bench supports it. Quantitative. Say "compiled / bench-only / flown" with numbers; never overstate verification.
- Don't change his tuning values unasked.
- Minor bump per law change, patch per clean-up.
- A positive pitch command with the stick full forward is a hitch, whatever the pace behind it. "Smaller" does not ship.
- The governor slows the commanded **rate** only, never the roll acceleration or deceleration.
- At low–mid AoA, slow the velocity-vector roll; at high AoA, where the airframe's passive lateral stiffness keeps roll and yaw together, slow the pitch.
- Sideslip from gravity pulling the jet sideways is the only acceptable failure.
- Rate regressions must be caught by the bench, on a newly ported airframe too.
- **Everything on the airframe affects yaw and sideslip stiffness.** Count every effector and passive term, on the sideslip axis, across the whole AoA range, not the obvious ones at low AoA (§4.0).
