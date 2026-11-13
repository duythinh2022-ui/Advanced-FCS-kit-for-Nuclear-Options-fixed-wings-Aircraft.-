# Changelog

## FS-3 Ternion FCS 0.4.0 (12 Nov 2026)
Flying backwards (90-180 deg AoA) the stick and pedals turn the nose the way they point: the velocity-vector roll's body-roll share is |cos AoA| (`ReversedFlowNoseAxis`, the F-22E 4.3.0 law), replacing the reversed-flow sense blend; forward flight unchanged. Benched, not flown. Notes: release-notes/Ternion_FCS_0.4.0_notes.md. (Entry added in the NO-FCS-Kit 3.0 copy.)

## FS-3 Ternion FCS 0.3.2 (12 Nov 2026)
Ground steering: the outside main brake helps the nose wheel stop a turn's yaw (`GroundIndiBrake`), the light-nose hold, and the pedal-side main braked by |pedal| x 0.5 for drifting (`GroundPedalBrake`); port of Su-30 0.5.3 + 0.5.4. Benched, not flown. Notes: release-notes/. (Entry added in the NO-FCS-Kit 3.0 copy.)

## FS-3 Ternion FCS 0.3.1 (12 Nov 2026)
No law change (bench identical to 0.3.0). The design-sideslip cases of the coupled check are now solved one side each (3 phase-1 + 2 phase-2 solves per point instead of 3 + 6, same figures; native core call gains a side field, core rebuilt): the loaded sweep costs about 37 % less on the bench. See Ternion_FCS_0.3.1_notes.md.

## FS-3 Ternion FCS 0.3.0 (11 Nov 2026)
No law change (bench output bit-identical to 0.2.3). The F-22E 4.0.0 - 4.1.3 performance work, ported through the Compass 0.3.0
port: the allocator's solve, the surface-table and sweep grid kernels in a native Rust core (Ternion_FcsCore, exports tern_fcs_),
the sweep geometry cache, the 13-segment tick timer. New for this jet: the coupled asymmetric-load check's LP in the core
(tern_fcs_cpl, NativeCoupledCap) and its surface points on per-tick geometry / one kernel call (CplGridCache). One DLL: the core
is embedded in the plugin and written once to BepInEx/cache/Ternion_FcsCore/<hash>/. Benched, run on the game's Mono under Wine,
not flown; see Ternion_FCS_0.3.0_notes.md.

## FS-3 Ternion FCS 0.2.3 (11 Nov 2026)
With an asymmetric load the negative AoA limit no longer depends on where the jet is flying. 0.2.2 solved the coupled check on
the negative sweep points only below 5 deg of present AoA and gave them the one-axis demand otherwise: -18 to -20 deg in a pull,
-40 in a push at the same CG offset and speed, widening over ~2.5 s through every pushover (11 Nov 11:33 log). CplNegAlways: the
coupled check on the negative points at every AoA. Suite and loaded-reversal set unchanged; new negative-AoA loaded set
(bench/an/negload.sh). Benched, not flown; see Ternion_FCS_0.2.3_notes.md.

## FS-3 Ternion FCS 0.2.2 (11 Nov 2026)
With a heavy asymmetric load the AoA limit no longer collapses to 7 deg or wobbles in pitch (11 Nov 09:38 log). 0.1.10's
extrapolated nose-down limit (LiveAuthCap) is off; the limit is now where a small LP, solved on the asymmetric-load AoA sweep,
runs out of flight-path roll either way with pitch held and no sideslip build-up (CoupledCap), using only stab / canard positions
the allocator would use (CplAttachedOnly) and 3 deg of adverse design sideslip (CplBetaDesign). The pitch cap's 20 deg/s^2
nose-down margin scales with q below 130 m/s IAS, judged at the predicted q (floor 6). Slats follow AoA only in flight with the
gear up (SlatAoASchedule). Thrust x lateral CG offset is in the load always (AsymThrustCgLoad). Bench calibrated against the
logs first (game stall table for camber coupling, thrust yaw, fitted stores). Benched, not flown; see Ternion_FCS_0.2.2_notes.md.

## FS-3 Ternion FCS 0.2.1 (11 Nov 2026)
With an asymmetric load, a roll reversal toward the heavy side builds one roll acceleration from the stick flip, paced by the rudders' travel, and keeps it through the zero crossing (no more 'stop the roll, then spike'); against the load the new roll starts at its brake's acceleration (F-22E 4.1.1 + 4.1.2). Ported from F-22E 4.1.2; bench switches RevOneAccel / RevLoadRamp. Benched, not flown; see Ternion_FCS_0.2.1_notes.md.

## FS-3 Ternion FCS 0.1.12 (9 Nov 2026)
Spin recovery engages only with the yaw saturated (filtered yawSat > 0.5, SpinRecYawSat): releasing an engine-out rolling pull at
50-55 deg no longer triggers it (it shoved the nose to -8 deg AoA). Recovery pitch is a rate target scheduled on AoA (-80 deg/s at
75+ deg, 0 at 15) x 6/s, capped 300 deg/s^2, instead of a constant -300. EngineOutSlipTrim: engine out or asymmetric thrust,
centred controls, |AoA| < 25, AoA/G law - the steady sideslip is learned (tau 2 s, max 4 deg) and removed from the sideslip hold,
so the jet flies straight instead of creeping 1-2 deg/s. Bench hands-off heading drift -6.0/-3.3/-3.0 -> -2.4/-1.2/-0.8 deg in
20 s (220/300/400 km/h).

## FS-3 Ternion FCS 0.1.11 (9 Nov 2026)
Engine-out zero-thrust match point is its own dev key EngineOutMatchAoA (35), not EngineOutAoACap; latched on at the point or on
filtered yaw saturation > 0.5, off only 5 deg below the point with saturation < 0.2 (no more on/off at 40-50 deg AoA).
RollPaceRateBlend default 0.65 -> 0.45 (GovernorRevision 4 moves files holding 0.65 or 0.4; hand-set values kept).

## FS-3 Ternion FCS 0.1.10 (9 Nov 2026)
A destroyed engine no longer takes that side's elevator, fin and rudder: in the 1.0.1 blueprint wing_1c_L/R is a transform child
of engine_L/R_aero, so the game detached and froze the tail with the engine. StructureFix re-homes a part whose attach parent is
an engine but which is also jointed to the airframe (wing_1c -> wing_1b) through the game's CreateAttachInfo; a Harmony prefix on
UnitPart_OnParentDetached ignores the old parent (dev key EngineLossKeepsTail, default on). LiveAuthCap: with the asymmetric-load
cap in force (normal law, no pitch TVC) the AoA limit is where the measured nose-down authority extrapolates to 60 deg/s^2 (less
2 deg, floor 7) - full pulls with a wing's surfaces shot off no longer depart. SpinRecoveryIdle (dev) to keep the throttle.

## FS-3 Ternion FCS 0.1.9 (9 Nov 2026)
Engine out: no AoA cap for it; above the match AoA in every mode (MPO too) the live engine is matched to zero thrust
(parasiticThrustLoss = 1.3 x static thrust, throttle off the AB) - dev key EngineOutZeroThrust. FightHintUnpaired: with a roll
surface whose mirror partner is gone the roll-fight cost uses the groups' own net roll (fixed a roll/yaw kick every 2-3 s). Automatic
spin recovery (dev key SpinRecovery): AoA > 40, |r| > 15 deg/s for 0.8 s with lateral controls centred -> the allocator is asked for
nose-down, full yaw against the rotation and roll damping, throttle idle; ends at AoA < 30 and |r| < 10. Telemetry spinRec, matchF,
fightHintOff.

## FS-3 Ternion FCS 0.1.8 (7 Nov 2026)
MPO has no AoA ceiling any more. On the Ternion (no thrust vectoring) the MPO pitch-rate budget fell to 5 + 0.15 x IAS (m/s) at
high AoA, because the 40 x T/W term that keeps it near the cap on the vectoring jets is 0 here: 24 deg/s at 450 km/h, 14 deg/s at
215 km/h, and full aft stopped at 55-58 deg. MPO now gets the full pitch-rate cap (MaxPitchRate 48) at every AoA on a jet without
nozzle pitch (code switch MpoNoVecFullRate). Normal modes unchanged. Bench, MPO full aft from 350-650 km/h: 47-48 deg/s through
every AoA (held full aft goes over the top); released at 75-80 deg it recovers below 40 deg within 4 s; sideslip <= 1.1 deg.

## FS-3 Ternion FCS 0.1.7 (7 Nov 2026)
Fuel: the buffed fuel burn is applied once again when the Aryx Weaponry Expansion is loaded. Its external-tanks-first prefix on
Aircraft.UseFuel drew every engine fuel call a second time, unscaled (1.75x stock burn on Buffed, 2x on Stock with
FuelFromAttachedTanksOnly on). The fuel hook is now two prefixes: UseFuelScalePrefix (Priority.First) scales the amount,
UseFuelPrefix (Priority.Last) draws only if no other prefix already drew (__runOriginal).

## FS-3 Ternion FCS 0.1.6 (7 Nov 2026)
Canards and elevators no longer left in opposing roll splits after a manoeuvre: StabilatorRollCost 0.0005 -> 0.01 (two pitch
pairs made a cancelling split nearly free for the allocator). One-time move (TernionDevRevision 4). Corkscrew entries reach 30 deg
~0.15-0.2 s later.

## FS-3 Ternion FCS 0.1.5 (7 Nov 2026)
AoALimitPositive 50 -> 55 (TernionDevRevision 3). Holds 55 down to ~165-180 km/h on the bench.

## FS-3 Ternion FCS 0.1.4 (7 Nov 2026)
Elevator stall curve: the 30 deg stall keeps the airfoil's own drag (ElevatorStallDragShift false); 0.1.2-0.1.3 moved the drag rise
in with the peak, which raised the elevators' normal force past the stall. ElevatorPlateauCL (dev, 1 = off) for plateau tests.

## FS-3 Ternion FCS 0.1.3 (7 Nov 2026)
Stock split airbrake and split yaw off while the FCS flies (AirbrakeSplitInFlight / SplitFlapYaw false): they took the elevators',
rudders' and flaperons' travel; the in-flight brake is the F-22E trailing-edge set. HighAoAReleaseTau 0.3 s: no nose-down dip when
a corkscrew is let go. LowThrustCapMargin 8 -> 4: limits of 50-55 hold to ~150-185 km/h. AoALimitPositive 40 -> 50.
TernionDevRevision 2.

## FS-3 Ternion FCS 0.1.2 (7 Nov 2026)
Elevators stall at 30 deg local (ElevatorStallAoA) and x1.2 effective (ElevatorEffectiveness); canards x1.1 (CanardEffectiveness);
shoulder LERX x1.13 (LerxEffectiveness, with its HLD areas) so the static margin stays at 0.1.1's -0.29 m. Rollout aero-brake adds
the elevators fully trailing edge up (GroundBrakeElevator 35, the F-22E's stabs). LateralStabReserve 0.7 -> 0: corkscrew entries
built pitch on 30 % of the canard/elevator authority (no nozzle pitch on this jet); now within ~0.1-0.3 s of a plain pull.
One-time move of the three changed dev defaults (TernionDevRevision 1).

## FS-3 Ternion FCS 0.1.1 (7 Nov 2026)
AoA limits above 45 now fly: the inherited fixed no-TVC caps (NoTvcAoACapPositive / Negative 45) were always on (vectorable T/W
is 0 on this jet) and capped every limit above 45, MPO included. On a jet with no vectorable nozzle (noVecJet) they are off;
AoALimitPositive / Negative and the swept authority cap rule. Defaults unchanged (40 / -40). New dev key ElevatorStallAoA
(0 = stock curve) for the earlier-elevator-stall test; bench: no high-AoA roll gain (roll is yaw-capacity-limited).

## FS-3 Ternion FCS 0.1.0 (7 Nov 2026)
New mod (`Ternion_FS3.FCS`), forked from T/A-30 Compass FCS 0.1.11. FS-3 Ternion (Blueprinter, prefab P_Trisurface1). Canards and
aft elevators are both Stabilator-kind pairs (`isCanard` flag): canard travel 60 LE down / 45 LE up (Revoker), elevators +35/-30,
rudders +-35, flaperon_L/R and rollervon_L/R +-25 (F-22E), all surfaces 100 deg/s. Revoker staggered trailing-edge stall (inboard
flaperons 26, outboard rollervons 45, knee-preserving) with camber coupling (0.62 / 0.66) and rudder camber 0.54. No downwash.
Canard attached-flow window, settled window and rollout brake on the canards only; per-pair trim costs (ElevatorTrimCost);
FlightPathRateFloor; trailing-edge brake set and its lift balance handle control-surface flaperons. Turbojet -> Turbofan.
AoA limits +40 / -40. Buffed: thrust x1.08, fuel x0.75, parasitic drag x0.90, flares x10, gun x2, EW x1.5 / x1.2.


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
