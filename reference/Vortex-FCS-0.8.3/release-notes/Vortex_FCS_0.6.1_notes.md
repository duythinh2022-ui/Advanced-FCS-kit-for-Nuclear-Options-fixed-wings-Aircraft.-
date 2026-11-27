# Vortex FCS 0.6.1 (20 Nov 2026)

## What Tom asked
- "towards the end i tried to slam the jet on to the ground to damage the engine, so thrust reduces after every impact. And when the jet doesnt have enough thrust to lift off anynorem, where is the low hvr t/w warning"
- "no need to tell that the engine is damaged, just use the same dialog"

## What the 13:02 log shows (Vortex_FCS_20261120_130258.csv, Buffed, 12.0 -> 11.25 t)
- After the impacts (from ~365 s) the jet sat on the runway in STOVL. At 425-435 s it was at 88 % throttle and the game's T/W read 0.43-0.67. It never lifted off.
- The FCS's hover T/W (`stTw`) read 1.30-1.40 the whole time, so no LOW HVR T/W caution came.

## Cause
Every figure of what the lift system can make started from the engine's static thrust (`Turbojet.maxThrust`). The game takes thrust off a damaged engine through its RPM ceiling, from the game IL (see claude/Engine_damage_stages_thrust.md):
- reachable RPM = minRPM + (maxRPM - minRPM) x (0.5 x condition + 0.5)
- dry thrust = static x clamp01((RPM - 0.85 x minRPM) / (maxRPM - minRPM))

On a 1000-3000 RPM engine that is full thrust down to condition 0.85, 82.5 % at 0.5, 67.5 % at 0.2 and 57.5 % at 0. `maxThrust` never changes with damage.

## Change (`StovlEngineCondition`, dev, on)
`ThrustReach()` is the share of static thrust the engine can still reach at its condition, by the law above. It multiplies every capability figure:
- the hover T/W (`twF`), behind both LOW HVR T/W cautions and the speed floor. The held geometry figure (`hoverKg`, held on the wheels because the nozzle's ground position reads 4 % low) keeps its rules, and the damage is applied to it live, on the wheels too;
- the translation and fore-aft budgets (`fvMax`, `aFullDry`, `FwdCap`);
- the thrust fit's ceiling;
- the tilt governor's thrust ratio;
- the vane-pair cost normalisation;
- the hover roll-trim thrust.

`ThrottleForThrust` keeps the static thrust, because the game's throttle-to-RPM map is built on it and already carries the condition.

No new message, as Tom asked: the existing cautions appear.
- LOW HVR T/W: REDUCED MNVR: hovers, but without the 20 deg manoeuvring margin.
- LOW HVR T/W: STO/RVL ONLY: cannot hover. The speed floor comes on in the air.

## Bench (Buffed 12.0 t, healthy hover T/W 1.274; new bench key `cond=t,c` sets the engine's condition from t)
| Case | 0.6.0 (switch off) | 0.6.1 |
|---|---|---|
| Hovering at 18 m, condition 0.5 at 25 s | hover T/W 1.274, no caution, jet holds height | 1.051, REDUCED MNVR, jet holds height |
| Hovering at 18 m, condition 0.2 at 25 s | 1.274, no caution | 0.860, STO/RVL ONLY, speed floor on |
| | lands at 5.19 m/s sink, 0.4 m/s forward | lands at 5.16 m/s sink, 3.5 m/s forward (the floor asks for speed) |
| | gear 0.335 m of 0.5 | gear 0.381 m of 0.5 |
| On the wheels in STOVL, condition 0.2, lever at the aft detent | 1.274, no caution, 88 % throttle asking a climb it cannot make | 0.858, STO/RVL ONLY, idle (no climb asked: the hover T/W is under 1) |

With condition 0.2 the jet cannot stop its descent in either version: thrust is 0.86 of the weight.

Healthy engine:
- 12-case identity set: identical to 0.6.0 (`ThrustReach` is exactly 1 above condition 0.85).
- Regression set: unchanged.
- Stability-Assist-off set: identical.

## Not verified
- Not flown.
- The caution box itself was not seen on a screen for this case. The bench shows the latch (hover T/W under 1.00, speed floor on), not the HUD.
- The 13:02 log has no engine-condition column, so I couldn't recompute what 0.6.1 would have shown at each impact. The thrust it logged (T/W 0.43-0.67 at 88 % throttle) is well under hover.
