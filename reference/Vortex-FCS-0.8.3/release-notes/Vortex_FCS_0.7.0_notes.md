# Vortex FCS 0.7.0 (23 Nov 2026)

Two things in this release:
1. The Raptor's latest governor fix (F-22E 4.5.2 + 4.5.3), ported as the same code.
2. Support for the **FS-20 Vortex Family** mod (Jpgamer4045d, 0.7.2): the FCS now flies all five family jets (B, A, C, T, NA) and owns the jet while it is engaged.

**Status:** compiled against the game's assemblies and benched. **Not flown**, and the family mod is not installed on your PC right now (your 21:12 LogOutput has no `fs20vortexfamily`), so the family code has never run in game.

## Tom (23 Nov)
- 21:21: "port the latest governor fix from raptor 4.5.2/.3 to the vortex, make the vortex fcs work properly with the vortex family mod."
- 21:30: all five jets including the NA. While the FCS is engaged it owns the jet. The B-tuned airframe changes apply only where the geometry matches the B, plus "proper fcs changes/tuning to the jets that has different geometry than B".

## 1. Governor fix (Raptor 4.5.2 / 4.5.3, same code)

| Switch (`FcsController`, on) | What it does | Raptor version |
|---|---|---|
| `PaceRollSatDelivered` | While the allocator reports roll as saturated, the pace governor's roll capacity is what is actually being delivered, not the decaying "seen" floor. So the roll window binds and the yaw can't run ahead of the roll. | 4.5.2 |
| `LatShareNoGate` | The lateral surface share (`LateralStabReserve`) follows the lateral stick alone. Before, it needed pitch stick held, so it vanished on a pitch release, which is exactly when a release-and-roll starts. The released-pitch priority (×30) now yields to it. | 4.5.3 |
| `PlanFollowsJet` | A braking pitch-rate plan may not run more than 0.1 s of its own deceleration (max 8 °/s) ahead of the jet. | 4.5.3 |

- One adaptation: the jet's rate is read in the plan's frame. The airbrake's lift feed-forward (`teBrakeQFF`, Vortex only) rides on top of the plan, so it is subtracted from the measured rate. With the airbrake in, this is 0.
- New telemetry column `planHeld`, as on the Raptor. The dev-config text for `LateralStabReserve` is updated.
- All three off = 0.6.11.

### Bench (stock B, Tom's config)

**Regression set** (peak sideslip, 0.6.11 → 0.7.0)
- Better:
  - fullaft+roll+rev3s 320: 4.5 → 3.4
  - rev3s 500: 5.9 → 5.3
  - roll-then-pull-rev 400: 4.1 → 3.0
  - held0.4+roll 350: 4.1 → 3.6
  - push+roll 300: 4.3 → 3.2
  - pull-then-pushover 350: 5.3 → 4.3
  - push-then-pullthrough 350: 5.6 → 4.8
  - pullthrough, roll released: 4.8 → 3.0
  - pull-then-roll 250: 3.1 → 2.4, with peak roll 16 → 26 °/s
- Worse:
  - pull-then-pushover 450: 5.7 → 6.4
  - push+roll 200: 3.2 → 3.7
  - 65° roll+yaw release: 2.4 → 2.7

**Stop/reversal sweep:** mostly within ±0.3°. 300/0.55: 3.0 → 4.2. 400/0.40 with reversal: 5.6 → 4.7.

**Corkscrews:**
- Most cases are within a few tenths.
- pull1.0+roll@0.3 250: AoA std 3.6 → 5.8.
- pull0.6+roll@0.3 550 (already the roughest case): roll std 23 → 33, sideslip 6.5 → 7.3.

**A cost you should know about: slower AoA build in a full pull from low speed. The Raptor has it too.**
- Cause: the code's `PlanFollowsJet` rule doesn't only act while braking. It also holds the plan back during the onset of a pull, while the jet lags the plan.
- Vortex, full aft at 250 km/h:
  - AoA at 1.0 s: 30.6 → 25.6°.
  - AoA at 2.0 s: 53 → 47°.
  - The limit is reached ~0.5 s later.
  - In return, the pitch-rate curve has no overshoot above the plan.
- Raptor 4.5.3 bench, same pull:
  - Plan 54 → 38 °/s at 0.4 s.
  - AoA ~2–3° lower at 0.6–0.8 s, equal by 1.5 s.
- Raptor notes for comparison: 4.5.3's notes describe the rule as acting "while braking". Restricting it to braking would be a one-line change, the same in both jets. I left it as the Raptor has it, since you asked for the port. Your call.

## 2. FS-20 Vortex Family support

### What the family does to the flight controls, and what 0.7.0 does with it
| Family code | On | While the FCS flies the jet |
|---|---|---|
| `ControlsFilter.Filter` prefix (pitch governor / load envelope rewrites the stick, FBW G limit and pitch bias) | A, C, T, NA | Skipped. The FCS's own prefix now runs first (`Priority.First`, before the family), so it always reads your stick. |
| `ControlSurface.UpdateJobFields` postfix (elevon remap: pitchRange 40, own pitch/roll mix, flap) | C, T, NA | Skipped for the surfaces the FCS drives. The FCS's postfix now runs last (`Priority.Last`, after the family), so load order can no longer reverse the elevons (the 17 Nov finding). |
| `LocalSimFixedUpdate` prefix: C's per-tick TVC limit (20° → 0 by 550 m/s) and the remap's inputs | C | Skipped. |
| `FlyByWire.Filter` hooks: T gains, NA yaw allocator, pitch-derivative transpiler | T, NA | Idle by construction: the stock FBW doesn't run while the FCS flies. |

- **When the FCS lets go,** the family's code comes back: disabled, ejected, AI with AIFlightControl off, simulated elsewhere, or failed.
- **The rest of the family is untouched:** bays, hook, paint, vapor, NA sensors, multiplayer.
- **Rule compliance (19 Nov):** the family is looked up once, at the FCS's `Start`, via `Chainloader.PluginInfos` and the plugin's own assembly. A miss is cached and costs nothing.
- **Checked against the family DLL's metadata:** the three hooked methods exist with the expected parameters.

### The NA (no fins, no rudders)
Before, the FCS refused to attach ("expected 6 control surfaces") but still cut the NA's nozzle yaw and split range. Now:
- It attaches with the four elevons when the airframe has no rudders or fins. This is generic: it is keyed on the airframe, not on the NA's name.
- It keeps the engine's yaw vectoring (±15°) and builds it as a yaw effector (`TvcYawEffector`).
- It keeps the outer elevons' 45° split as drag-rudder effectors (`SplitEffector`, the Ifrit code). With no rudders, the yaw tiers already fall to these.

### B-tuned airframe changes only where the geometry is the B's (`GeomGate`)
**How a part is checked.** A part counts as the B's when all of these match the stock B table:
- wing area (±1.5 %)
- origin in the aircraft frame (±5 cm)
- centre of lift (±1 cm)
- slat areas, on the slat sections

The table is generated from the extracted SmallFighter1 asset. The stock B itself is never checked: everything counts as stock, exactly as 0.6.11.

**What is gated.** These apply only on parts that pass:
- staggered elevon stall
- wing and forebody vortex lift
- camber coupling
- elevon flap model
- hinge visuals

The FCS's own model uses the same check, so it always models what the game applies.

**What to expect per variant (from the family source).**
- **A:** keeps B wings and fins, so everything applies.
- **C:** the widened outer wing, its elevons and the stretched fins are left to the game's aero. The chines (wingRoot) stay stock.
- **T / NA:** the delta wing and the replaced elevons are left to the game's aero. The T's fins are stock.

**Log line.** On every family jet, the log lists which parts kept the changes, plus each reworked part's area ratio to the B and how far it moved. That is the data a bench model of each variant needs.

**Slats.**
- T / NA: the family's own slat system (`Fs20THighLift`, with damage sections) keeps its slats. The FCS no longer takes over a high-lift device its airframe has switched off.
- C: the FCS's slat animation uses the widened wing's hinge line.

### Per-variant tuning: the push-over plan's yaw model
**The problem.**
- 0.6.11's push-over plan (`PushPlanK` 0.0156 · IAS^1.5) is a fit to your B's push-overs, with rudders and the swivel nozzle's yaw bearing together.
- The A and C have no swivel nozzle. The NA has no rudders. The T is lighter.

**The fix.** Each jet's K is now the B's fit scaled by its modelled yaw authority over the B's:
- **Yaw moment:** rudders at full travel on their own airfoil, split drag of one side, and nozzle yaw × static thrust.
- **Divided by** the yaw inertia the FCS measures, at 70 m/s.
- **Reference:** B = 31.5 °/s² (bench). The B keeps its fit exactly.

| Bench proxy | Yaw authority | K × |
|---|---|---|
| B | 31.5 | 1.00 |
| A (no yaw bearing, 11.4 t) | 15.5 | 0.49 |
| C (no yaw bearing, 10.9 t) | 15.6 | 0.50 |
| NA proxy (engine yaw 15°, no rudders, 8.7 t) | 53.9 | 1.71 |

**What it changes.**
- In low-speed corkscrew push-overs the plan already runs at the 23 °/s floor, so this changes nothing there. On the A, CKR/CKN at 250/350 km/h are identical with the scaling on and off.
- It matters at higher-speed push-overs, where the plan is above the floor.
- The in-game inertia may differ from the bench's. The new telemetry columns `yawAuth` and `yawShedK` show this jet's value, so one B log calibrates the 31.5.

### Bench proxies of the variants
**How they were built.** From the family source applied to the B extract (`bench/proxy/mkproxy.py`):
- **A:** mass changes; no swivel.
- **C:** widened outer wing, with areas from the planform stretch; fins +60 % (an estimate); 110 + 55 kN; TVC to 550 m/s.
- **NA proxy:** the B wing (not the delta); fins and rudders removed; 45° split; engine yaw 15°; T composite masses.

**What they are good for.** They check that the FCS attaches and flies each layout. They are not the real T / NA delta aerodynamics.

**AoA limit test** (your 0.1.0 method: full aft from 200–500 km/h, full roll left then right, full push). Peak sideslip at the current +65 / −55 limits:

| | 200 km/h | 300 | 400 | 500 | velocity-vector roll at 65° |
|---|---|---|---|---|---|
| B | 11.3 (roll entry) | 4.2 | 2.3 | 2.3 | 16–24 °/s |
| A | 13.4 | 6.9 | 2.5 | 2.5 | 7–18 °/s |
| C | 2.4 | 2.1 | 2.0 | 4.4 | 10–22 °/s |
| NA proxy | 6.3 | 6.4 | 7.1 | 3.6 | 52–69 °/s (engine yaw nozzle) |

- No variant needs a lower AoA limit on this test. The limits stay +65 / −55 everywhere.
- The A is slightly worse than the B at a 200 km/h entry (13.4 vs 11.3).
- The NA proxy's push stops at −37° (the plan with no rudders).

**Regression set and corkscrews on the proxies:** all fly. Peak sideslip:
- A ≤ 6.9. Push+roll 200: 6.9 vs the B's 3.7.
- C ≤ 5.3. It is G-limited earlier at high speed (bigger wing).
- NA proxy ≤ 7.8 (rev3s 500).

## Not in this build
- **No flight with the family installed.** Install it next to the FCS. The first log should show these lines:
  - `FS-20 Vortex Family 0.7.2 found; 3/3 of its flight-control hooks step aside...`
  - the `GeomGate` part list per jet
  - `yaw authority (...)`
- **Family AI.** The family's AI guidance hooks (`Fs20AiGuidance`) still run. AI jets flown by the FCS (AIFlightControl on) use the FCS's AI law under them. Unchecked.
- **The T's STOVL.** The full STOVL laws run on a lighter delta. The laws are physics-based (mass, thrust, measured response), but not benched on a T model.
- **Fin area on the C.** The +60 % is my estimate of the family's mesh stretch. The log will give the real value.
- **Build:** the 0.6.11 source rebuilds here byte for byte to your shipped 0.6.11 DLL, so the baseline benched above is exactly what you have installed. The 0.7.0 source zip rebuilds this DLL byte for byte (md5 c32f0200ae916ae97af2ea794ec62096).

## Files
- `FcsController.cs`: governor fix, NA attach, yaw-authority model, `GeomGate` calls.
- `FamilyCompat.cs` (new).
- `GeomGate.cs` (new), `GeomGateData.cs` (new, generated).
- `Registry.cs`, `Plugin.cs` (hook priorities), `FcsStandalonePlugin.cs` (Start, version).
- `StallStagger.cs`, `StallStaggerJobs.cs`, `CamberForce.cs`, `VisualHinge.cs`, `FlapSystem.cs`.
- Bench: `stubs/FamilyBench.cs`, `sim/Airframe.cs` (surface-less proxies, yaw vectoring rule), `proxy/mkproxy.py`.
