# Changelog

## T/A-30 Compass FCS 0.5.0 (12 Nov 2026)
Flying backwards (90-180 deg AoA) the stick and pedals turn the nose the way they point: the velocity-vector roll's body-roll share is |cos AoA| (`ReversedFlowNoseAxis`, the F-22E 4.3.0 law), replacing the reversed-flow sense blend; forward flight unchanged. Benched, not flown. Notes: release-notes/Compass_FCS_0.5.0_notes.md. (Entry added in the NO-FCS-Kit 3.0 copy.)

## T/A-30 Compass FCS 0.4.2 (12 Nov 2026)
Ground steering: the outside main brake helps the nose wheel stop a turn's yaw (`GroundIndiBrake`), the light-nose hold, and the pedal-side main braked by |pedal| x 0.5 for drifting (`GroundPedalBrake`); port of Su-30 0.5.3 + 0.5.4. Benched, not flown. Notes: release-notes/. (Entry added in the NO-FCS-Kit 3.0 copy.)

## T/A-30 Compass FCS 0.4.1 (12 Nov 2026)
No law change (bench identical to 0.4.0). The coupled asymmetric-load check's LP now runs in the native core (cmp_fcs_cpl), its surface points come from the per-tick grid in one kernel call, and the design-sideslip cases are solved one side each: the loaded sweep costs about 40 % less on the bench. See Compass_FCS_0.4.1_notes.md.

## T/A-30 Compass FCS 0.4.0 (11 Nov 2026)
With an asymmetric load the AoA limit now comes from a coupled check (every surface counted; roll, pitch and sideslip held together): about 22-25° with 32 cm of lateral CG offset where 0.3.1 gave 12°, and the 7° limit under about 120 kt is gone (the nose-down limit reads the same check and its margin scales with airspeed). The thrust's moment about the offset CG is now part of the load, which brings the limit down to its 12° floor under about 100 kt on afterburner. Negative limit −30° (was −14 to −27°). New dev setting AsymCoupledReserveMax (0.75). Coupled check ported from Ternion 0.2.3 (`CoupledCap.cs`); bench switches CoupledCap / AsymThrustCgLoad / AsymPitchMarginIasRef. Benched and run on the game's Mono, not flown; see Compass_FCS_0.4.0_notes.md.

## T/A-30 Compass FCS 0.3.1 (11 Nov 2026)
The native core (Compass_FcsCore.dll) now ships inside the plugin DLL: one DLL to install. On first load it is written to `BepInEx\cache\Compass_FcsCore\<checksum>\` and loaded from there. See Compass_FCS_0.3.1_notes.md.

## T/A-30 Compass FCS 0.3.0 (11 Nov 2026)
The control allocator's solve, the surface tables' maths and the asymmetric-load sweep's inner loop now run in a native library (Compass_FcsCore.dll, Rust, ported from the F-22E FCS), and the sweep works out each deflection's geometry once per tick instead of once per AoA point; same flying, less CPU time per tick. The tick timer now splits the whole tick into 13 segments, reports garbage collections, logs slow ticks and names the performance switches in force. See Compass_FCS_0.3.0_notes.md.

## T/A-30 Compass FCS 0.2.1 (11 Nov 2026)
With an asymmetric load, a roll reversal toward the heavy side builds one roll acceleration from the stick flip, paced by the rudders' travel, and keeps it through the zero crossing (no more 'stop the roll, then spike'); against the load the new roll starts at its brake's acceleration (F-22E 4.1.1 + 4.1.2). Ported from F-22E 4.1.2; bench switches RevOneAccel / RevLoadRamp. Benched, not flown; see Compass_FCS_0.2.1_notes.md.

## T/A-30 Compass FCS 0.1.11 (5 Nov 2026)
VMC protection fixed for this jet. The T/A-30's engines have no vectoring transform and the FCS skipped their thrust entirely, so
with an engine out the live engine's yaw read 0 and only the last-resort cut (after the sideslip ran away) could act. Their thrust
moment is now taken as it is (fixed nozzles; asymmetric-load model roll/yaw only, engine out only), and a predictive limit
(VmcPredict) brings the throttle back once the live engine's yaw passes 70 % of the yaw authority at the predicted q. Bench, engine
out, full-aft climbs from 220-300 km/h: departures at 25-31 m/s -> yaw held to the top of the climb; 0.25 pull at 180/200 km/h:
sideslip 53/35 -> 0.6/1.9 deg. Both engines running: identical. Note: the FCS's T/W (tw) reads 0 on this jet for the same reason;
not changed (it moves several laws).

## T/A-30 Compass FCS 0.1.10 (5 Nov 2026)
The inner flaps no longer move with the pitch stick, in the air or taxiing (FlapPitchLock): their common deflection is held to the
schedule (landing droop, airbrake / rollout sets), only the differential is free for roll, and the droop comes off when the
differential needs the travel. The ground law's pitch-stick throw on them is gone. With a tail slab shot off the lock lets go (the
flaps are then part of the last nose-down). Clean jet: rolls identical; 5-10 % less pitch rate at a push onset and pulling out of a
push. Damage matrix: same 1 departure; outer-wing cases 14.4 -> 12.4 deg peak AoA.

## T/A-30 Compass FCS 0.1.9 (5 Nov 2026)
The asymmetric-load cap comes down faster when it has far to go (AsymCapGapTighten: tighten rate x gap / 3 deg, 1-4x), so a pull
right after a part is shot off no longer outruns it. Bench, outer wing shot off 0.9 s before full aft: 6 of 8 depart -> 0 of 8;
damage matrix, clean jet and stores identical. 5 Nov 21:16 log: the 0.1.8 gate held the 7 deg cap; the 180 s departure was the
speed bleeding to 45 m/s at idle (minimum speed with this damage ~100 m/s).

## T/A-30 Compass FCS 0.1.8 (5 Nov 2026)
The asymmetric-load pitch protections (pitch cap and bias, pitch priority, stab roll price, held coupling gate) also open when a
control surface is lost with the cap in force (AsymDamageGate); the lateral CG offset they were gated on swung -0.28 <-> 0.00 m
with a hanging rudder and dropped the pitch cap 7 -> 12 deg (5 Nov 20:25 log, departures at 193 and 217-223 s). Negative-AoA
mirror of the bias and a negative pitch cap (AsymPitchBiasNeg, floor -4). PushPlanStopsRoll off: a held roll is no longer
stopped through a push-over (Tom 20:51). Bench, 133 damaged cases with the CG-offset gate shut as in game: 27 -> 2 departures;
clean jet identical.

## T/A-30 Compass FCS 0.1.7 (5 Nov 2026)
Under the asymmetric-load AoA cap without pitch TVC, the nose-up the cap's model leaves out is measured in flight (AsymPitchBias:
measured pitch less the stabs' share less the modelled airframe, peak-held; with a stab gone also the stabs' live nose-down end vs the
sweep) and taken off the pitch cap's nose-down margin, the capture braking at the cap and the roll-coupling limit; the pitch cap may
come down to 7 deg (AsymPitchCapFloorNoTvc; 10 before). 5 Nov 19:18 log: right outer wing, left fin and left stab gone, the cap's
model read 25-29 deg of room with every pitch surface out of nose-down at 12, AoA 12 -> 46. Bench, 133 damaged cases: 23 -> 1
departures; clean jet identical.

## T/A-30 Compass FCS 0.1.6 (5 Nov 2026)
Under the asymmetric-load AoA cap without pitch TVC: the AoA capture plans its stop with half the modelled nose-down at the cap and
no lean toward the authority available now (AsymCapBrake); the stabs' roll split is priced when the AoA reaches the cap and pitch is
short (AsymStabAntiCost 0 -> 0.1). 5 Nov 19:04 log: right outer wing gone, push to -8 then full aft (no roll) came through zero AoA at
30 deg/s, the stabs went to holding the lost wing's roll, AoA 12 -> 47, departure. Bench, outer wing detached: 0 of 20 depart
(0.1.5: 1, plus a 19 deg overshoot).

## T/A-30 Compass FCS 0.1.5 (5 Nov 2026)
Roll-biased flaps (FlapRollBias, dev, 1): in the air the flaps' high-lift droop comes off with lateral demand so the pair rolls with
its full +-20 differential (F-22E flaperon behaviour). Roll governor on the stabs' nose-down margin without pitch TVC
(StabRollGovernor): the 5 Nov 18:52 log departed after the right outer wing was lost (full aft + roll at 90 deg/s, the roll's
inertial pitch-up took the last nose-down, AoA 12 -> 24 -> 47). Bench, outer wing detached: no departure (0.1.4: 2 of 6).

## T/A-30 Compass FCS 0.1.4 (5 Nov 2026)
Low-AoA roll-rate ceiling: RollRateGainLowIAS 1.2 -> 2.0, RollRateGainHighIAS 1.1 -> 1.8, RollAuthorityUse 1 -> 2 (dev file moved once,
CompassDevRevision 4). The inherited F-22E roll reference and the FCS's steady-roll estimate held full-stick rolls to ~105 deg/s at
300 km/h with the ailerons at 6-7 deg (5 Nov 18:12 log). Bench 1 g: 142 -> 247 deg/s at 300 km/h.

## T/A-30 Compass FCS 0.1.3 (5 Nov 2026)
Tail slabs (elevator_L/R, hstab_L/R) fly a swept-slab curve (StabSlabFoil, new StallStagger family Slab: stall 30 deg, peak CL 1.22,
3.5 /rad) instead of the wing's 17 deg foil; LerxEffectiveness 3 -> 3.4 (dev file moved once, CompassDevRevision 3). Without pitch TVC
the roll-coupling limit is on from 3 deg AoA whatever the stick (CoupNoTvcAlways, floor 12 deg/s^2). Pitch-rate trajectory uses 1.0x the
authority estimate (PitchTrajAuthFrac; 0.7 before). From the 5 Nov 17:41 log: both elevators at the nose-down stop in a 100 deg/s roll at
70 m/s with flaps out, AoA 10 -> 46; slow reversals at +35 / -30.

## T/A-30 Compass FCS 0.1.2 (5 Nov 2026)
Strake vortex breakdown 38-60 -> 30-50 (settings StrakeBreakdownStart / End / StrakeResidualCN): strake lift peaks ~32 deg and falls past the
35 limit; LerxEffectiveness 2 -> 3, CgShiftAft 0.20 -> 0.15. AoA protection: the stick cancels the limit only on a jet with pitch TVC
(FcsController.StickBypass; never on the Compass). Config moves: CompassDevRevision in the dev file (0.1.1's check read the wrong file).

## T/A-30 Compass FCS 0.1.1 (5 Nov 2026)
Elevators +-30; AoA +35/-30; strakes (wingroot1) get a Polhamus vortex-lift curve (StrakeVortexLift, new Strake.cs / StallStagger family)
at 2x area; CgShiftAft 0.27 -> 0.20 (low-speed roll inertia coupling no longer pins both elevators nose-down); CanardAttachedWindow off;
MPO VelocityVectorRoll by default; one-time move of 0.1.0's defaults (CompassConfigRevision). Bench: Defaults() restores CgShiftAft /
LerxEffectiveness (0.1.0's closed-loop AoA sweeps had run without the CG shift); chart remap on the stock-jet path; cparts tool.

## T/A-30 Compass FCS 0.1.0 (5 Nov 2026)
New mod: the Revoker 0.8.2 laws on the stock T/A-30 Compass. AoA +-30, G +9/-3.5; surfaces 100 deg/s; lift-driven tail downwash 0.45;
camber coupling aileron 0.55 / rudder 0.55 / flap 0.40; flaps as allocator effectors with the stock 20 deg landing droop; relaxed
stability by CG 0.27 m aft; Buffed: thrust x1.08, fuel x0.75, flares x10, gun x2, EW x1.5 / x1.2. No Turbojet transpiler.

---
(Revoker history, inherited)
# Changelog

## 0.4.0
- **The allocator weighs the lateral residual in the velocity frame** (`SlipFrameAlloc`): the error about the velocity normal (dp sin a - dr cos a) is the sideslip acceleration itself, the error about the velocity vector is stability-axis roll. The roll weight becomes the velocity-axis weight and the yaw weight the velocity-normal one; the body-frame high-AoA roll fade is bypassed while it is on. Identical below ~10 deg AoA, swapped by 70. Bench: full-aft reversal at 320 km/h 4.1 -> 1.8 deg of sideslip, the 65 deg stick-release cases 5.4/5.7 -> 1.8/1.8, steady rolls below 45 deg unchanged.
- **The lateral governor's rate cap is a forward model of the reversal** (`RevSim`): trajectory, rate loops, a least-squares allocation over every effector, the probe's stiffness and damping, beta-dot kinematics, bisected for the largest rate that stays inside the margin. Replaces the kinematic formula over a hand-picked yaw-effector set (which left out the trailing edge - the dominant yaw axis above ~45 deg on this jet). 22 us/tick.
- **`RollStopSideslip` 4 -> 8**, once (`GovernorRevision`; a hand-set value is kept).
- Yaw-damping credit from the probe (`slipDamp`, `dampCredit`), `PaceStiffCredit`, `SweepMarginGate` (were to be 0.3.1, never released).
- Bench: falling leaf at 70 deg AoA 82 deg/s at 2.5 deg of sideslip (0.2.22: 71 at 11.3; 0.3.0: 41 at 3.0). Corkscrews 62-71 (0.2.22: 72-81, 0.3.0: 34-51).
- Bench-only. Not flown.

## 0.2.22
- **Knee-preserving elevon stall stagger.** The stagger now moves only the plateau above the wing foil's 17.2° knee; below it the curve is unchanged and the peak CL (1.10) is kept. 0.2.20–0.2.21 compressed the whole curve, which dragged the knee down to 12–14° and cost the inboard pair roll authority at 10–15° plus some lift.
- **Inboard reversal 37 → 26°** (`InnerElevonReversalAoA`, migrated once from exactly 37). Outboard stays 45°. Model (both pairs + camber, measured arms): worst-case velocity-vector roll authority over 10–55° AoA 0.26 / 0.18 (small / full deflection) vs 0.15 / 0.07 in 0.2.21 and 0.03 / 0.01 unstaggered; unchanged at 5–15°.
- `ElevonStaggerLiftSlope` retired (the peak CL is always kept). Takes effect on the next spawn.
- Compiled only. Not flown.

## 0.2.21
- **Inboard elevon reversal 32 → 37°** (`InnerElevonReversalAoA` 37; a dev file holding exactly 32 is moved once). Outboard stays 45. Reversal: inboard ±37.2 (±36.0 at full deflection), outboard ±44.8 (±40.7 at full). Takes effect on the next spawn.
- Compiled only. Not flown.

## 0.2.20
- **Staggered elevon stall:** the inboard elevons now reverse for velocity-vector roll at ±32° AoA and the outboard at ±45°, instead of both at ±45° together (`ElevonStallStagger`, `InnerElevonReversalAoA` 32, `OuterElevonReversalAoA` 45, `ElevonStaggerLiftSlope` 1). Each pair flies its own copy of the wing foil with the peak moved (game aero and FCS models alike); the inboard pair's camber share on wing2 is now a force on the inboard curve so it reverses with the elevon. Combined elevon roll authority at 45° AoA goes from 0.016 to 0.059 (×3.7, small-deflection model); the low point anywhere 25–50° goes from 0.016 to 0.059. Takes effect on the next spawn.
- Compiled only. Not flown.

## 0.2.19
- **Canard travel +60/−45°:** leading edge down 70 → 60° (`StabilatorRangeTEUp` 60; a dev file holding exactly 70 is moved once). On the runway full push and the rollout brake each go to the 60° stop.
- Compiled only. Not flown.

## 0.2.18
- **Runway canards reach their stops:** full push takes them to their full leading-edge-down travel (70°), full pull to full leading-edge-up (45°), instead of stick × the stock pitch range (~35°). The rollout aero-brake alone also takes them all the way down (`GroundBrakeCanard` 45 → 80, capped by the 70° stop; migrated once). Before, only push + brake together reached the stop.
- The canards' +70/−45 travel now applies from a standstill (geometry sign), not only after 10 m/s of airflow.
- Compiled only. Not flown.

## 0.2.17
- **Canard travel +70/−45°:** leading edge down 80 → 70° (`StabilatorRangeTEUp` 70; a dev file holding 0.2.16's 80 / 45 is moved once). Leading edge up stays 45°.
- Compiled only. Not flown.

## 0.2.16
- **Canard travel the right way round:** +80/−45 — leading edge **down** to 80°, leading edge up back to 45° (`StabilatorRangeTEUp` 80, `StabilatorRangeTEDown` 45; dev files holding 0.2.14/0.2.15's 45 / 80 are swapped once). 0.2.14 had extended the leading-edge-up side. Sign checked from flight data: at +45..70° AoA a +40° canard sits at ~+8° local AoA, at −45..−56° a −40° canard at ~−14°.
- Compiled only. Not flown.

## 0.2.15
- **Push + roll sideslip governor:** looks ahead as far as the lateral loops take to answer (0.33–0.47 s, the same lag model as the sideslip-hold gain) instead of a fixed 0.8 s (`SlipGovLeadFromLoops`, on). In the 30 Oct 23:45 log (0.2.14) the sideslip crossing zero at +11 deg/s on its way back from −3.3° read as +9° 0.8 s out and cut the roll target 79 → 46 deg/s with the yaw nozzle and rudder on their stops; the release 0.35 s later re-built it 46 → 85 and the yaw axis went round again. New telemetry columns `latLag`, `slipLead`.
- Compiled only. Not benched, not flown.

## 0.2.14
- **Push + roll (negative corkscrew) entry:** the sideslip-hold gain now comes from what the rate loops carrying the correction can follow (`SlipGainFromLoops`, on): 1.3 at 0° AoA, ~1.85 at −50°, ~2.3 at 70°, instead of the fixed ramp to 4. In the 30 Oct 22:48 log the fixed 4 through the 0.4 s yaw loop ran the sideslip at ~1 Hz (−3.4 ↔ +3.6°), the yaw rate ±12 deg/s about its command and the roll governor cut the roll twice — the hitch. The ramp is still there under `SlipGainFromLoops = false`. New dev key `ActuatorLagT` (0.07 s).
- **Canards:** travel −80/+45° (leading edge up to 80°; `StabilatorRangeTEDown` 45 → 80, migrated once) — the wrong side, corrected in 0.2.16.
- Compiled only. Not benched, not flown.

## 0.2.10
- In-flight brake surfaces close with AoA the way the F-22E's speedbrake does. They are fully out to 15° |AoA|, close linearly until they are stowed at 35°, and reopen as the AoA comes back. Dev key `TrailingEdgeBrakeAoAFade`, on by default.

## 0.2.9
- **In-flight brake surfaces:** move as fast as the actuators allow. The outboard elevon share is solved every frame for zero added lift, with the canards' re-trim counted, so deploying and retracting doesn't upset pitch, roll or yaw. Bench, 400–1200 km/h: nz 0.97–1.11 while deploying, pitch rate under 0.2 deg/s, sideslip 0°.
- **Take-off:** the nose wheel now lifts 0.14–0.2 s after a pull instead of 0.7–0.85 s (`GroundRotationRamp` 60 → 600, migrated once). A short pull no longer fails to rotate the jet.

## 0.2.8
- **`TrailingEdgeBrakeConfig`** (player config, on by default): F-22E-style brake surface sets.
  - **In the air:** inboard elevons down, outboard up.
  - **On the runway:** every trailing edge up, canards leading edge fully down.
  - **Braked touchdown:** the nose is lowered at 4 deg/s, then the full rollout set comes in.
- Needs `ThrottleIndependentAirbrake` on.

## 0.2.7
- **No-TVC rolls at the AoA cap:** smoother. Roll weight 1, and the canards are kept in their attached-flow range.
- **Canards and negative AoA:** canard travel ±45°, negative AoA limit 55°.
- **Fin lost:** 30° AoA limit without nozzle yaw. With TVC, the nozzle keeps vectoring above the game's 250 m/s cut-off. The pedal's sideslip is limited.
- **MPO:** the stick is velocity-vector roll and the pedals are pure body yaw, at every AoA.

## 0.2.6
- **ThrottleIndependentAirbrake off:** the airbrake is fully stock, so the brake key no longer opens it. This fixes Brake and Countermeasures sharing one controller button.

## 0.2.5
- **FcsApi v4:** HUD and visualizer auto-detection.
- **No TVC:** faster roll rate; AoA cap 50° on afterburner.

## 0.2.4
- **Corkscrew:** smoother yaw-rate curve.
- **Aileron visuals:** drawn about the measured hinge line.

## 0.2.3
- Aileron hinge-line visuals (render only).

## 0.2.2
- `MaxYawRate` 90.
- Yaw-nozzle hold gated on rudder headroom.

## 0.2.1
- Ground elevon pitch.
- Yaw-TVC sideslip gain ramp.

## 0.2.0
- AoA limit 70°.
- Inboard elevons as hinge effectors.
- `ElevonPitch` on.

## 0.1.0
- First release. A fork of KR-67 Ifrit FCS 0.5.4, retargeted to the FS-12 Revoker.
