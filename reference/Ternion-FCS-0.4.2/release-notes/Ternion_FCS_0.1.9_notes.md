# FS-3 Ternion FCS 0.1.9: engine-out zero-thrust match, damaged-roll-surface fix, spin recovery

Base: 0.1.8. Compiled and benched. **Not flown.** 0.1.8 is kept as `.dll.0.1.8.bak` (main and dev).
Main md5 `33271de134fc29c06af0732bcc93d5cc`, dev `e5d02deb146387021452dff16a667ad6`. Release folder `C:\Games\NO mods\Ternion-FCS-0.1.9-release`.

## Tom (9 Nov, 07:56–08:18)
- One engine destroyed: wants max AoA (the airframe can do it without TVC), no AoA cap for it. In MPO past the max AoA the live engine "refused to do the VMC thing until it is about to spin out". Wants the live engine matched to the dead one's thrust: zero. Standing rule for all future FCS mods (saved to project preferences).
- "Even when I set 55 as the limit for VMC, the jet is limited to 30."
- Log 2: right flaperons and right stab destroyed; jet flies straight, yaws left, straight, yaws left. MPO spin he could not recover from: make spins recoverable.
- Also asked (not changed in 0.1.9, see below): more roll/yaw rate at mid-high AoA without much more sideslip or stopping margin; AoA undershoots during falling-leaf reversals.

## Findings
### Log 1 (07:46): the 30° limit
- At 152.8 s the game flagged `wing_1c_R`, `elevator_R`, `VStab_R`, `rudder_R` as detached with `engine_R_aero`. In the 1.0.1 blueprint those parts are transform children of the right engine nacelle (`wing_1c_R`'s parent is `engine_R_aero`), and `UnitPart.UnitPart_OnParentDetached` flags transform children. FCS mass fell 24 530 → 21 029 kg = engine 3007 + wing_1c_R 232 + stab 103 + fin 102 + rudder 51.
- The game stops driving any control surface it flags detached (`ControlSurfaceJob_Math.Execute` returns on `IsDetached`): right stab and rudder froze at 0.3°.
- So yawHealth went to 0.5 and `FinLossNoTvcAoACap` (30) applied, not `EngineOutAoACap` (Tom's 55). Tom says only the engine was destroyed and the rest kept working: open question whether the tail chunk stays on visually (game flag only) or leaves with the engine.
- MPO, 175–182 s: past 57° the old gate held the live engine at military power; yaw saturated at 57–62°, r 13→36 °/s, spin; the run-away throttle cut came at 182 s.

### Log 2 (07:50): periodic left yaw
- Damage: elevator_R at 19 s, flaperon_R 21.9 s, rollervon_R 45.8 s. Hands off at ~1060 km/h: allocator left 13–65 °/s² of roll unmet every 2.0–3.3 s, rudders snapped to a common -5/-8°, p to -9 °/s.
- Cause: the roll-fight cost's reference sign (`fightSignHint`) uses target − Σ roll at zero deflection. A lone left flaperon/rollervon makes 23–30 °/s² of roll at zero deflection, so that reference sat at the 15 °/s² switch (`FightTargetMin`) and flipped; each flip priced the only wing left as "fighting".
- Reproduced on the bench (synthetic `bench/data/ternion_damage.json`, all parts jointed to root): 8 onsets in 17 s; `FightTargetMin` 0 or 1000 removes them.

### Log 2: spin
- MPO, AoA 40–120 oscillating, yaw 15–35 °/s, 40–60 m/s, for 70 s. Reproduced on the bench with the same damage: no recovery in 20 s in MPO or normal law, idle or AB. Max nose-down available at 70° AoA ~10 °/s² (elevator_L at -30, canards at +60 = least nose-up).

## Changes
1. **EngineOutZeroThrust** (dev, "Asymmetric load", default true). Engine OUT: no AoA cap for it (`EngineOutAoACap` no longer a limit, no fixed cap on entry). From `EngineOutAoACap` up, every mode incl. MPO, the live engine is matched to zero: `parasiticThrustLoss` = 1.3 × static thrust (game Turbofan: thrust = max(0,(static × dyn − loss) × spool); nozzles light no AB while a loss is set) plus throttle off the AB with the match. Timed like the weak-engine match (complete before the AoA arrives), also on the VMC run-away. Weak-engine path unchanged.
2. **FightHintUnpaired** (code): with a roll surface whose mirror partner is gone, the fight cost uses the groups' own net roll instead of the target reference.
3. **SpinRecovery** (dev, "2. Envelope", default true): AoA > 40°, |r| > 15 °/s for 0.8 s (leaky timer), lateral stick and pedal centred, pitch stick < 0.5 → allocator asked for full nose-down (300 °/s²), full yaw against the rotation (300), roll rate damped (6/s); throttle idle. Rejected variants listed under Bench. Ends at AoA < 30° and |r| < 10 for 0.3 s, or on lateral input. Not a scripted surface schedule: the allocator picks surfaces from its live model.
4. Telemetry: `spinRec`, `matchF`, `fightHintOff` columns.
5. Bench: Turbofan path now honours `parasitic` (it ignored it), `BENCH_MPO_OFF=<t>`, Tom's live player+dev cfg as `bench/tomtern.cfg`.

## Bench (Tom's live config: AoA 55, EngineOutAoACap 55, RollPaceRateBlend 0.65)
Engine out (left engine killed, pull 3.5 s then release, 1500 m):
| Case | 0.1.8 | 0.1.9 |
|---|---|---|
| Normal, 350 km/h | 52.6° / β 1.1 | 55.5° / β 0.8, match on |
| Normal, 500 km/h | 49.3° / β 0.7 | 50.5° / β 0.7 |
| MPO, 350 km/h (past 90°) | β 7.3, r 12.9 | β 0.5, r 0.7 |
| MPO, 500 km/h | β 19.2, r 8.0 | β 0.3, r 0.6 |
With the right tail chunk also removed (game-accurate log 1 state): normal law still capped 14–30° (fin-loss cap + aero asymmetry cap at low speed); MPO departs in both versions.

Damaged cruise (right flaperons + stab off, 1000 km/h, 20 s hands off): unmet-roll onsets 8 → 0; p std 0.86 → 0.00 °/s; φ drift −22° → +0.8°. Damaged 600 km/h roll reversal: identical (192/−208 °/s).

Spin (same damage, MPO pulls released past 100°, 8 cases a/b/c/h × idle/AB, 20 s): 0.1.8 none recovered; 0.1.9 7 of 8 out for good (AoA < 25, |r| < 10) 9–13 s after engagement (13.8–16.9 s). The 8th (300 km/h, push held 5.3–9.3 s, idle) gets out at ~17 s, the nose comes back up from 20–30° in MPO and it re-enters; still spinning at 40 s. Tried and rejected: exit AoA 15/20 (no change), roll weight 0.2/0.05 in recovery (lost h_0 and a_0), rate-damped yaw/pitch law (slower), fixed accelerations without roll damping (re-departures from inertial coupling). False-trigger check: normal-law loaded rolls/stops/pedal at 300–600 km/h: never. Clean MPO: engages after a roll at 100+° AoA is released at ≤ 450 km/h (r 15–25 °/s not decaying with yaw saturated) and flies it out; never at 600 km/h or with pedal.

## Not changed: roll/yaw rate at mid-high AoA, leaf AoA undershoot (trade-offs for Tom to choose)
- `RollPaceRateBlend` 0.65 → 0.4: velocity-vector roll +12–20 % at 33–53° AoA (e.g. 26→29–31 °/s at 46–51°), stops within ±0.05 s except 53° / 350 km/h (1.28 s → not done in 1.3 s), reversal sideslip +0.4–0.8°, reversals 0.03–0.15 s slower. 0.2: +20–50 % rate, sideslip up to +1°, slower still.
- Leaf (full aft stick, pedal reversals, normal law): AoA 54–55 → 42–45° after each reversal. Elevators already on their +35° stop; canards go to yaw, nose-up authority falls 19 → 6 °/s². Allocator pitch and yaw weights ~7.7 vs 8. Fixing it means pitch priority over yaw at the limit = slower leaf reversals. Same canards as the extra roll/yaw rate.
