# Vortex STOVL: authority- and physics-based replacements for fixed gains and schedules

Plan only, nothing built. Vortex only (the shared CTOL laws and their gains are out of scope).

**History**
- First version (on 0.3.10): `StovlTrcBankRate` / `StovlTrcSpeed` only. Tom, 17 Nov 2026 16:19–16:20: "can those be replaced with authority based logic … make an md". That content is now section 4.
- Extended on 0.3.11. Tom, 17 Nov 16:41: "add the stuffs that can be replaced to authority based and physics based. Also the least amount of specialized logic as possible".

Line numbers are `Stovl.cs` 0.3.11 unless marked.

## 0. Principle: a few shared primitives, no per-case logic

Every fixed number that gets replaced becomes an instance of one of five primitives (section 2). None of them is a new mechanism bolted onto a loop: each changes where a loop's existing number comes from.

After the change, only three kinds of constant remain:
1. **One universal damping rule.** k = 1/(2τ), the same for every loop. It is already the rule for the height loop's rate gain (0.2.1).
2. **One stop time per kind of motion,** calibrated once at the reference so the reference reproduces today's behaviour (the `StovlYawStopTime` pattern).
3. **Dimensionless fade bands** on physical shares (the `wPed` pattern, 0.25–0.5).

No speed thresholds outside the mode boundaries. No per-axis tuned gains.

**Reference condition** (used for every calibration): clean jet, full fuel, level hover, sea level. That is 11 880 kg on the bench, the reference 0.3.9 used for yaw.

**Default rule.** If a derived value at the reference differs from today's fixed value by more than 15 %, it is reported to Tom with the bench numbers before it ships. No calibration factor is added to hide the difference.

## 1. What stays fixed (and why)

**Control-law mode thresholds.** These decide what the stick, lever and pedals mean, so the pilot has to be able to predict them:

| Weight | Band | Inceptor meaning |
|---|---|---|
| `lamTrc` | 15–30 kt ground speed | Lateral stick: translational rate → bank-angle command |
| `lamT` | 25–35 kt ground speed | Lever: ground speed → acceleration command |
| `lamV` | 30–40 kt ground speed | Longitudinal stick: vertical speed → flight-path rate |
| `lamY` | 30–40 kt ground speed | Pedals: yaw rate → sideslip; TRC → sideslip loop for nulling side drift |
| CTOL blend (`VhCtolV0/V1`) | 40–60 kt airspeed | Stick and lever switch to the CTOL laws (lever = thrust) |
| `lamRoll` | 100–130 kt airspeed | Lateral stick: bank-angle → roll-rate command |

Logic that hangs off these weights stays with them (`nAv`, `aDes`, the `lamT` pitch-tilt scale, the `lamV`-weighted `yawLiftK`).

**Model constants.** `EngineLag` / `StopLag` (1 s) are the game's spool law, not tuning. The rate loops' `KRoll` / `KPitch` / `KYaw` are shared code.

**Upper limits** that are not covered in section 4 (ceilings stay ceilings):
- `StovlAccelMax` / `DecelMax`, `StovlBankMax`, `StovlClimbRate` / `SinkRate`.
- The `Stop()` vmax caps (60 / 30 / 15 deg/s).
- `thUp` / `thDn`.

**The height loop's own ratios.** `VhHoldRatio` 0.75 and `kVInt` = 0.25 kVHold are already derived from τ_V and were bench-tuned in 0.2.8. They are left alone. Aligning `VhHoldRatio` to the universal 0.5 would be a separate A/B, not part of this plan.

## 2. The five primitives

### P1 `LoopGain(τ)`: gain from the lag of what's inside the loop

$$k = \frac{1}{2\,\tau_{inner}}$$

- τ_inner is the response time of the loop this one commands through (its own effector lag plus the inner loop's closed-loop time).
- A loop's own closed-loop time, as seen by the loop outside it, is 1/k.
- Exists today: height loop (`kVRate = 1/(2 tauV)`, ~1714) and the shared `SlipGainFromLoops` (Revoker 0.2.14).

### P2 `Stop(e, k, a, τ)`: one start/stop profile

The command toward an error e is the smaller of the linear law and the fastest value it can still be stopped from:

$$u = \operatorname{sign}(e)\,\min\!\left(k\,|e|,\; a\left(\sqrt{\tau^2 + \frac{2|e|}{a}} - \tau\right)\right)$$

- a is how fast u itself can be brought back to zero (the authority).
- τ is the lag before it starts to (normally 1/k of the loop inside).
- τ = 0 gives today's attitude `Stop()` (√(2a|e|), line 300).
- With a = j and τ = `tauBank` it gives today's TRC profile (~1904).
- One function replaces both. The vmax argument stays as the ceiling.

### P3 `RateMax(α, T)`: authority-based ceiling

$$u_{max} = \alpha_{now} \times T_{stop}$$

- α_now is the live authority. T_stop is one calibrated stop time.
- Exists today: `YawRateMax` (`StovlYawStopTime`, 0.3.9–0.3.11).
- Section 4 applies it to the tilt rate and translation speed.

### P4 `Share(x, need, b0, b1)`: physics fade

$$w = \operatorname{Smooth}\!\left(b_0,\, b_1,\, \frac{x}{need}\right)$$

- A dimensionless share replaces a speed band.
- Exists today: `wPed` (slip stiffness vs yaw authority, ~2705) and the TE droop / rudder flare fades (surface authority share 25 → 60 %).
- Bands (b0, b1) are set once so the reference reproduces today's speed band, then left alone (question 3 in section 9).

### P5 Bryson weights: cost weight from the tolerable error

$$w_i = \frac{1}{e_{i,tol}^2}$$

e_tol on an axis is the residual the rest of the system can still absorb there.

## 3. Group A: loop gains (P1 + P2)

Today the outer loops run fixed gains, and the TRC profile carries a separate copy of their lag:

| # | Now (0.3.11) | Where | Becomes |
|---|---|---|---|
| A1 | Bank loop 2.5 /s × `kRollS` | 2619, 2630, 2647, 2652–2653 | k_φ = LoopGain(τ_p) × `kRollS`. The √rollWeak one-sided-load factor is kept as today. |
| A2 | Freestyle bank 2.5 /s | 1134 | same k_φ |
| A3 | Pitch-attitude loop 2 /s (law 15 deg/s cap, freestyle 30) | 2547, 1150 | k_θ = LoopGain(τ_q) |
| A4 | Heading hold 2.5 /s, 20 deg/s cap | 2691, 1167 | k_ψ = LoopGain(τ_r). The cap becomes `YawRateMax` (P3), so the release of a held pedal and the hold share one ceiling. |
| A5 | `StovlTrcLagMargin` 0.32 s (Dev key) | 1901 | τ_bank = 1/k_φ. Removes the key. Today it is a hand copy of the bank loop's lag, and already out of step with it: since 0.3.9 the profile assumes 0.32 s on a 2.5 /s loop, which is 0.4 s plus the rate loop. |
| A6 | `tauPitch` 0.5 s (const) | 1909 | τ_pitch = 1/k_θ |
| A7 | Position hold 0.3 /s, ±2 m/s (fore-aft and side) | 1881–1886 | k_pos = LoopGain(τ_vel), with τ_vel the TRC velocity loop's closed-loop time (2 τ_bank side, 2 τ_pitch fore-aft). The clamp becomes P2 with a = the translation budget `aHv`. |
| A8 | Centre-detent speed hold 0.3 /s, ±2 m/s² | 1927 | k = LoopGain(τ_long), with τ_long the slower of thrust (τ_V) and pitch tilt (τ_pitch) weighted by `lamT`. The clamp is the existing `StovlAccelMax` / `DecelMax`. |
| A9 | Forward-flight sideslip loop 1.5 /s, ±20 deg/s | 2744 | k_β = LoopGain(τ_r). The clamp is P2 with a = `aYaw`. Same idea as the shared `SlipGainFromLoops`, not a copy of it. |
| A10 | `tauVL = 0.35 + spoolT` | 1717 | `RpmLead + spoolT`, as `tauV` (~1714). The 0.35 is the 0.2.1 value left behind when 0.2.8 made `RpmLead` measured. A consistency fix, no new logic. |
| A11 | Auto-airbrake integral gain 0.5 | ~2358 | Integrator rate = 1/(a_brake,full × τ_brake). a_brake,full = q × `Airbrake.dragAmount` / m: the controller already reads `dragAmount` by reflection (AirbrakeAoAFade, FcsController ~2010). τ_brake comes from the brake's opening rate. |

**τ_inner per axis (phase 0 measures it first).** With the INDI feed-forward (`pDotOut` / `rDotOut`, 0.1.1), the rate loops follow their command faster than 1/K. So τ_inner is *not* simply 1/`KRoll` etc.

The model candidate is the allocated effectors' lag (roll posts and fan pitch: spool / door response; yaw: nozzle yaw servo) plus one tick, with 1/K only for the part the feed-forward misses.

Phase 0 measures the command-to-rate lag per axis in the hover and at 60 kt (bench step response) and picks the formula that matches. It also checks today's gains against it:

| Axis | Today's gain × measured τ | ≈ 0.5? |
|---|---|---|
| Bank | 2.5 × τ_p | ? |
| Pitch | 2 × τ_q | ? |
| Heading | 2.5 × τ_r | ? |

If one is far from 0.5, that is a finding to report (default rule), not something to calibrate away.

## 4. Group B: authority-based tilt rate and translation speed (P3)

The original plan, unchanged in substance. It replaces `StovlTrcBankRate` and `StovlTrcSpeed`.

### 4.1 What the two keys do today

| Key | Default | Where it acts (`Stovl.cs`, 0.3.10 line numbers) |
|---|---|---|
| `StovlTrcBankRate` | 10 deg/s, fixed | 1. Side translation bank target slew, `phiTrcF` (× √rollWeak), ~2547. 2. Fore-aft pitch-tilt slew, `thetaAssist`, ~2401 (through `GovLambda`). 3. Jerk of the side and fore-aft start/stop profiles, `jLat = g × rate`, ~1889. 4. The lift lost to thrust lag while the tilt comes in (`aLag` in the translation budget), ~1793. 5. The pitch tilt's share in `GovLambda` (`tTh`), ~1032. |
| `StovlTrcSpeed` | 15 kt, fixed | Full lateral stick = this ground speed, `vCmd`, ~1887. |

Both are the same numbers for every mass, fuel state, store load and thrust state. Only the roll-load factor √rollWeak (0.2.8) scales the side slew.

### 4.2 What each one stands in for

**`StovlTrcBankRate` is a thrust limit first, a roll limit second.**
- Its own description (0.1.6): "so the thrust keeps up with the lift the bank tilts away".
- Tilting the lift vector to φ needs thrust mg·sec φ. The engine can only follow at its thrust-rate headroom over its response time τ_V; the height loop already computes τ_V each tick from the engine model (≈ 0.5 s at 12 t).
- Logged reason for the cap: a stop from a 29 kt skid snapped to 11° in 0.3 s, the jet sank 0.5 m/s, and the bank was cut back (0.1.6 bench).
- The roll posts' start/stop authority (and the pitch authority for the fore-aft tilt) only matter near wings level, where the thrust demand barely changes.

**`StovlTrcSpeed` is a stopping limit.**
- A sideways speed is only safe if the jet can stop from it in reasonable time.
- The hover's stopping capability is the horizontal acceleration the full-dry lift leaves once the weight is carried. The code already computes it as `aHlevel` (0.2.6–0.2.7). The 0.2.6 comment for an 11.9 t hover: 26° of tilt, 4.8 m/s² possible.
- Heavier, hotter or higher means less spare lift, so less stopping acceleration, so a lower safe speed.

### 4.3 Tilt rate, replacing `StovlTrcBankRate`

Per tilt axis (bank for side, pitch tilt for fore-aft), the rate limit is the lower of two terms.

**(a) Thrust term.** The tilt rate the thrust can follow without losing height beyond the height loop's reserve:

$$\dot\phi_T = \frac{a_{res}}{g\,\sec\phi_e\,\tan\phi_e\,\tau_V}$$

- a_res is the vertical acceleration reserve the translation budget already keeps back (`aVres` plus the share the governor allows).
- φ_e is the larger of the present tilt and the tilt the move will reach (`atan(a_h / g)`), so the term stays finite near level.
- This replaces the fixed rate inside `aLag`, `GovLambda` and the tilt slews. The `GovLambda` sharing between bank and pitch tilt stays as it is.

**(b) Authority term (P3).** The tilt rate the axis can start and stop within one stop time at its present authority:

$$\dot\phi_A = \alpha_{axis}\,T_{tilt}$$

- α_roll is `aRoll` × √rollWeak, so the 0.2.8 one-sided-load slowdown is kept.
- α_pitch is `aPitch`.
- T_tilt is the one calibration constant, `StovlTiltStopTime`.

**Limit and profile.** The limit is $\dot\phi = \min(\dot\phi_T, \dot\phi_A)$. The start/stop profile jerk becomes $j = g\,\dot\phi$, per axis (side and fore-aft separately), fed into P2 with τ from A5 / A6.

### 4.4 Translation speed, replacing `StovlTrcSpeed`

$$v_{max} = \min\left(a_{stop}\,T_{trc},\ v_{bank}\right)$$

- a_stop = `aHlevel` (the hover's own stopping acceleration, no vertical demand), low-passed over 1 s so fuel burn and thrust noise don't move the command.
- v_bank is the speed whose stop needs no more than the hover bank limit (`StovlBankMax`) allows.
- T_trc is the one calibration constant, `StovlTrcStopTime`.
- Full lateral stick = v_max; released = position hold.
- While the stick is held, v_max may only fall at its low-pass rate. A change in the limit mid-move then never shows as a step in the speed command.

### 4.5 Calibration

At the reference:
- `StovlTiltStopTime` is set so the side bank rate is today's 10 deg/s.
- `StovlTrcStopTime` is set so the full-stick speed is today's 15 kt (7.72 m/s).
- Estimate from the 0.2.6 figure (4.8 m/s²): T_trc ≈ 7.72 / 4.8 ≈ 1.6 s. To be measured on the bench, not taken from the comment.

If Tom gives F-35B hover data (side translation speed or bank rate, like the 90° yaw time), calibrate to that instead and record the source.

## 5. Group C: physics fades instead of airspeed bands (P4)

Each of these is the FCS deciding *how* to do the job, not what the inceptors mean. Each runs on airspeed today, which assumes one weight, one air density and one loadout.

| # | Now (0.3.11) | Where | What it really is | Share it becomes |
|---|---|---|---|---|
| C1 | `wWing` = Smooth(15, 45 m/s, vSym) | 2470 | The wing's part of the load, and the pitch-attitude reference (hover attitude → γ + α_ref) | Wing-lift share λ_L = q·`sCLa`·α_ref / (m g), with α_ref = `StovlWingAoAMax`. Everything needed is in the code already (`sCLa` ~443, `qDyn`, `I.mass`). |
| C2 | Velocity-vector roll fade `wVv` = Smooth(10, 25 m/s, vSym) | 1199, 2731 | Whether rolling about the body axis turns AoA into sideslip that matters | Slip share λ_β = `slipStiff` × 45 / `aYaw`: the same number `wPed` uses (~2705), computed once and shared. |
| C3 | Sideslip-loop fade Smooth(10, 25 m/s, vA) | 2744 | Whether sideslip has an aerodynamic meaning yet | the same λ_β |
| C4 | `spdAb` = Smooth(15, 30 m/s, vSym) | 2355 | Whether the auto airbrake does anything | Airbrake share λ_B = q × `dragAmount` / (m × a_need), with a_need the deceleration the propulsion can't give (the residual the integral already sees). Same data as A11. |
| C5 | Take-off lift share Lerp(0.3, 0.9, Smooth(0, 35 m/s, vA)) of the weight the wing doesn't carry | 2188 | How light the wheels get on an STO roll. The wing's lift is already subtracted physically (`lw`); the ramp only decides how much load the wheels keep. | Wheels keep what their friction needs: N_min = m × \|a_ground,cmd\| / μ (plus nose-wheel steering load). The jet carries the rest of W − L_wing. **Conditional:** needs μ from the game's wheel model. If μ isn't readable, C5 stays as it is. |

**Bands.** For C1–C4, (b0, b1) are the shares the reference jet has at today's speed-band ends. The reference then reproduces 0.3.11, and only off-reference changes. C2 and C3 share one λ_β and one band: today they use the same 10–25 m/s, so the same share is the least logic.

**Interaction with the mode thresholds.** The CTOL blend (40–60 kt) stays fixed while C1 moves with weight. If a heavy jet's λ_L is still low at 60 kt, the CTOL laws take the stick and lever while the nozzles still carry most of the load. That is today's behaviour too (the incremental force request carries the rest), so no new logic, but the bench checks it (section 8).

## 6. Group D: thrust-fit weights (P5)

| # | Now (0.3.11) | Where | Becomes |
|---|---|---|---|
| D1 | `WlsCost` weights: vertical 1, fore-aft 0.25 (fixed 4:1) | 1056–1060 | w_y = 1/a_res² (the height loop's vertical reserve: a vertical miss nothing else absorbs). w_z = 1/a_tilt² (the fore-aft acceleration the pitch tilt `thetaAssist` can still make up: g·tan of its remaining range). |

Today's ratio means a_tilt ≈ 2 a_res at the reference. Phase 0 checks that. If it isn't close, that is reported (default rule).

Normalising by w_y keeps the cost's scale, so the thrust clamp and the residual used downstream don't change meaning.

## 7. Group E (later, not in this round): authority margins

`AuthorityShare` 0.75 and `GuardShare` 0.9 are fixed fractions of authority held back for couplings and stopping. The root replacement is to compute the coupling demand each tick (gyroscopic and inertial p·r / q·r terms, plus a disturbance estimate from the INDI residual) and reserve exactly that.

It touches every budget in the laws, so it waits until A–D are in and benched.

## 8. Config, telemetry, order, acceptance

### 8.1 Config (Dev, 9. STOVL)

One switch per group, so each group is A/B-able in game:

| Key | Default | false = |
|---|---|---|
| `StovlDerivedGains` | true | Group A off: today's fixed gains, `StovlTrcLagMargin`, `tauPitch`, 0.35 |
| `StovlTiltStopTime` | from calibration | 0 = fixed `StovlTrcBankRate` |
| `StovlTrcStopTime` | from calibration | 0 = fixed `StovlTrcSpeed` |
| `StovlPhysicsBlends` | true | Group C off: today's airspeed bands |
| `StovlFitWeights` | true | Group D off: 1 / 0.25 |

- `StovlTrcLagMargin`, `StovlTrcBankRate` and `StovlTrcSpeed` stay in the file, read only when their group is off.
- No migration: new keys default on, old values stay as the fallback.

### 8.2 Telemetry (STOVL columns, after `stYawLim`)

- **Group A:** `stKPhi`, `stKTheta`, `stKPsi`, `stTauP`, `stTauQ`, `stTauR`.
- **Group B:** `stTiltRateA`, `stTiltRateT`, `stTiltRate`, `stTiltRateU`, `stAStop`, `stTrcVmax`.
- **Group C:** `stLamL`, `stLamB`, `stLamBrake`.
- **Group D:** `stWz`.

### 8.3 Order

- **Phase 0:** measure only, no behaviour change. Per-axis command-to-rate lag (hover, 60 kt). a_res and a_tilt at the reference. λ_L, λ_β and λ_B along a decel and an STO. Report the gain × τ products and the D1 ratio before building.
- **Phase 1:** group A (A10 first: it is a one-line consistency fix).
- **Phase 2:** group B.
- **Phase 3:** group C (C5 only if μ is readable).
- **Phase 4:** group D.
- **Later:** group E.

Each phase is benched against the previous release before the next one starts.

### 8.4 Bench cases

Cases: reference, 20 % fuel, 650 kg and 1 760 kg stores (centre, then one side), at 0 and 2 000 m. Each run gets the usual hover set (hover, VL, VTO, decel, transition, STO) plus:

| Case | Check |
|---|---|
| Reference, every group on | Hover set within 0.1 m and 0.5° of 0.3.11, side bank rate 10 ± 0.5 deg/s, full-stick speed 15 ± 0.5 kt, except where phase 0 reported a difference Tom accepted |
| Full side stick 5 s, release; full lever translation, stop (each mass / altitude) | Time to 90 % speed; stop to 0.3 m/s; overshoot ≤ 0.1 m/s; height loss ≤ 0.3 m (no worse than 0.3.11); which term binds in B |
| Heading: full pedal 3 s, release; 90° turn | Yaw rate at `YawRateMax`, heading overshoot ≤ 1°, no limit cycle |
| Position hold with a 5 kt crosswind gust | Drift and recovery no worse than 0.3.11 |
| Decel and STO (each mass) | Pitch attitude, AoA and nozzle schedule; where C1 / C4 move the fade vs 0.3.11, the speed at which they do |
| One-sided store, side stick both ways | Bank and position within 0.3.11 (the √rollWeak path is kept in A1 and B) |
| Lever / stick steps mid-move | No step in any derived gain, `stTrcVmax` or tilt rate; no limit cycle (the 1.3 Hz roll-ceiling cycle on the F-22E 1.9.3 came from a limit fed by its own response) |

**Pass:** the reference reproduces 0.3.11. Off-reference (heavier, hotter, higher, stores) the gains, rates, speeds and fades move in the physical direction, with the same or less height loss and overshoot. Nothing else moves.

## 9. Risks

- **Gains derived from a lag that breathes** (spool, door position): filter τ_inner, and change k only at a bounded rate. Otherwise the loop's gain moves inside its own response.
- **Feedback through a share:** λ_L uses α_ref, not the measured α, so the wing share doesn't depend on the attitude it sets (C1).
- **Near-level singularity in 4.3 term T:** handled by φ_e (the target tilt).
- **Double counting in B:** `GovLambda` already scales tilt rate by the thrust-rate budget. Term T sets each axis's cap, and `GovLambda` only divides the shared budget. The bench checks the product isn't slower than either alone.
- **The speed limit moving with fuel burn while the stick is held:** the one-sided low-pass in 4.4.
- **D1 changing with tilt range in a turn:** a_tilt shrinks as the pitch tilt runs out, raising w_z exactly when the fore-aft miss can no longer be absorbed. That is the intended direction, but it is checked for chatter at the tilt limit.

## 10. Questions for Tom

1. Any F-35B reference for hover side translation speed or bank rate? Otherwise today's 10 deg/s and 15 kt at the reference are kept.
2. Should the fore-aft lever translation use the same tilt logic (pitch tilt), or stay as it is?
3. Group C bands: per fade, reproducing today's speeds at the reference (planned), or one common band (e.g. `wPed`'s 0.25–0.5) for every share, which is even less logic but moves the reference?
4. C5: OK if the STO wheel load follows the friction the wheels need (the roll gets lighter on the wheels when the commanded acceleration is low), instead of today's 30 → 90 % ramp?
