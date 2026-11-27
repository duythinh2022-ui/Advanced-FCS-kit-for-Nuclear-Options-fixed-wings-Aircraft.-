# F-22E FCS 3.1.3: envelope on several cores, camber base cached, force hooks timed

Base: 3.1.2. Patch bump: no behaviour change.

**Status:**
- Compiled against the game's assemblies.
- Benched byte-identical to 3.1.2, with every switch on and with every switch off.
- **Not flown.** 3.1.2 is kept as `.dll.3.1.2.bak`.

## Tom (6 Nov)
"what to optimize next" → "do it as u want". The plan was: timers for the force hooks, the envelope gathered once and run in parallel, and the per-surface constants hoisted out of the repeated surface evaluations.

## 1. Envelope and hybrid slopes (`ParallelEnvelope`, Dev "0. Debug", default on)
These are refreshed every 0.1 s. In the 3.1.2 log: 51 µs per tick on average, but about 355 µs on each refresh tick.

**What it was:**
- `ComputeEnvelope` looped part by part. For every part it did the 25 AoA points, and each point read `aeroPart.rb.velocity` again.
- Each stab part also read `ac.transform.rotation` and `ac.rb.velocity` again.
- That is about 25 × parts Unity calls per refresh.
- `ComputeHybridSlopes` then walked the parts 4 more times (`NonSurfacePitch`), reading the same transforms and rigidbodies each time.

**What it is now:**
- **One pass over the parts on the main thread.** It keeps the existing sums (pitch damping, airframe drag) and stores each part's air velocity, rotation, foil and `qS`.
- **The per-point terms are worked out once per point instead of once per part:**
  - the rotation `Quaternion.AngleAxis(α_j, rootRight)`;
  - the stab downwash angle `Downwash.EpsFor(q_j·(v_ac − wind), root)`. `Downwash.Apply(v, root, vRef)` is `Turn(v, root, EpsFor(vRef, root))`, and `vRef` depends on the point only.
- **One more pass for the hybrid slopes' parts.**
- **The 25 envelope points and the 4 slope evaluations then run on the shared fork-join** (`IndexFork`: the shared pool, at most `FcsPool.Helpers` helpers, the main thread taking part).
  - Each envelope point sums over the parts in the original part order (`envLift[j] +=`), so the result is bit-identical.
  - The airbrake share, `nAlpha`, `Lp`, `nMaxAero` and the slopes are computed after the join, as before.
- **Off:** the same gather, with the points run in a loop on the main thread.

## 2. Camber base, cached once per table rebuild
- **What repeated:** `Camber.Delta` = `Foil(v, deflected host)` − `Foil(v, host)`. The second term depends only on the host's state, which is read once per rebuild. It was recomputed for every one of the ~32 table points of every flaperon, aileron and rudder.
- **Now:** it is computed once per rebuild, on whichever thread runs that surface's `EvalCompute`, and cleared by `EvalGather`.
  - Same zero test (`k ≤ 0` or `|δ| < 0.01`).
  - Same expressions (`Camber.DeltaFrom`).
- **The same split for `EvalFlow`** (`PrepFlow` + `EvalFlowAt`), where one flow is evaluated at several deflections: the sweep (a flaperon twice per point) and the min/max authority pair. The flow's magnitude, the stab downwash turn, the dynamic pressures, the host arm and the host base are computed once.

## 3. Force-hook timer
- **The hooks:** `DragRelief`, `CamberForce` and `DownwashForce` are `FixedUpdate`s on each F-22E, outside the FCS tick and its timer.
- **The log:** with `TickTimerLog`, a third 10 s line now reports their time per physics tick, for the player's jet and for all F-22Es:

  `F-22E force hooks (us per physics tick, outside the FCS tick, N ticks): player jet: drag relief …, camber …, downwash … | all F-22Es: …`
- With `TickTimerLog` off, the hooks run without the stopwatch calls.

## Bench identity
| check | result |
|---|---|
| Suite per-case files vs 3.1.2 (rev / cork / reg / util / dmg) | **153 / 153 identical**, all switches on and all off |
| Suite summaries vs 3.1.2 | identical (on and off) |
| Replays (10 released, 10 held, 25 other pushes, 7 free windows) vs 3.1.2 | **52 / 52 identical** (on and off) |
| 40 s corkscrew case: bench CSV md5, 3.1.2 / 3.1.3 on / 3.1.3 all off | identical |
| Game CSV, same three (timer columns aside) | 0 / 2316 rows differ |
| Four benches at once on 2 cores, no early join | game CSV identical ×4. Only the bench's own estimator columns differ, read mid-job by the bench logger (as documented in 3.0.4). |

**The one thing the bench can't check:** the gathered values are now stored in float arrays and fields rather than held in locals. That is identical under strict single-precision float maths, which is what the bench's .NET 8 does. The game's Mono runtime is assumed to do the same, as the 3.1.0 table gather and the 3.1.2 sweep already assume. If it didn't, the difference would be at the last-bit level, not in behaviour.

## Bench timing (main-thread µs per tick, telemetry off, 2-core VM, ±15 % run to run)
| | tables | envelope (per-tick average) |
|---|---|---|
| 3.1.2, all on | 104–110 | 22–26 |
| **3.1.3, all on** | **91–97** | **18–20** |
| 3.1.2, single thread | 110–123 | 22–24 |
| **3.1.3, single thread** | **86–104** | **17–18** |

- **The sweep's `EvalFlow` split:** no gain this VM can resolve.
- **The game should gain more than the bench on the envelope.** The bench's stub Unity makes the per-part `rb.velocity` / `transform` reads that the gather removes (about 25 × parts per refresh) nearly free; in the game they are native calls.
- **In game, watch:**
  - "envelope" in the rest-split line, and `tEnvUs` in the CSV;
  - "tables";
  - the new force-hook line.
- `ParallelEnvelope` can be switched off mid-flight for an A/B.

## Not checked
- Not flown.
- Multi-jet in game not tested.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 `aaa1c09234e39cf97dcacc02464ca160`) + `meta.json` 3.1.3; 3.1.2 kept as `.dll.3.1.2.bak`.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.1.3 (md5 `dd8b6ac2f6729361ec5f8f95bc8c77ee`); 3.1.2 kept as `.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.1.3-release`.
  - The mod zip holds the main DLL only; the Dev DLL ships separately as `(Optional)Aryx_F22E_StrikeRaptor_FCS_Dev.zip`.
  - `meta.json` gameVersion 0.34.6.
