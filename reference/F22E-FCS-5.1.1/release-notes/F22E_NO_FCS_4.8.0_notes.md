# F-22E Strike Raptor FCS 4.8.0 (work in progress): the two-zone airframe as a switchable build

Base: 4.7.2. **Status: source only. Not compiled against the game, not installed, not flown.** The game's `Managed` and `BepInEx` folders were not connected on 25 Nov, so no DLL exists yet. The law-side code is compiled and run in the bench; the new game-side code is not.

Tom (25 Nov 16:56): "are those airframe on my pc, if not build it".

## Switches (dev file, "5. Airframe", all off by default)

| Setting | Default | What it does |
|---|---|---|
| `TwoZoneAirframe` | false | false = 4.7.2 exactly. true = the 25 Nov two-zone airframe. **Respawn after changing.** |
| `FinBlanking` | false | Only with `TwoZoneAirframe`. false = variant A (fins full strength), true = variant B (fins and rudders fade to `FinBlankMin` between `FinBlankAoA0` and `FinBlankAoA1`). Takes effect at once (not verified in the game). |
| `FinBlankMin` / `FinBlankAoA0` / `FinBlankAoA1` | 0.5 / 38 / 46 | Guesses, as in the handoff. |
| `TwoZoneRollReserve` | 0.5 | Share of the measured nose-down reserve a roll at the AoA limit may use. See the regression below. |

With `TwoZoneAirframe` on, these are forced whatever the dev file says: thin-plate stab curve (dip 0.9, width 12), stab downwash fade 45-60, the refitted fore / aft lift schedules, two-zone stab (outboard half, 0.2 upwash, 0.7 m), slat over-droop 15 deg with aft-lift gain 0.4, slat nose-down counted in the authority sweep.

## What was added to the 4.7.2 source

Law side (the v7 bench prototype, applied cleanly to 4.7.2):
- Two-zone stab in the stab tables and every sweep path; slat effectors with the coupled aft lift and the extra travel; fin factor in the rudder tables, `SlipMomentAt`, `NonSurfacePitch`, `HsPoint`, the authority sweep; roll limit from the measured reserve.
- The slat aft-lift moment moved from statics to the per-aircraft evaluation context (`EvalContext.aftMomL / aftMomR`), so two F-22Es cannot overwrite each other.
- `Downwash.IsFin` no longer caches "not a fin" while blanking is off (a live `FinBlanking` toggle would otherwise never reach the law's models).

Game side (new, **uncompiled**):
- `DownwashForce`: the stab's outboard-zone force change, 0.7 m outboard of its centre of lift.
- `AirfoilScaleForce` (new component): fin blanking on `Vstab_*` / `Rudder_*` and slat-coupled aft lift on Wingmid, Wingtip, tail booms, engine bays, tail and back, as (K - 1) x the part's airfoil lift and drag.
- `CamberForce`: a scaled host part's camber increment takes the same factor.
- `Registry`: attaches the component and reads the two switches at spawn.
- Slat meshes follow the droop past 25 deg through the existing per-slat animation (no change needed).

## Bench (single runs, no TVC unless stated, calibrated plant)

Switches off: two cases (reversal at the 36 deg limit with TVC off; slow idle roll with TVC on) are byte-identical to 4.7.2.

Reversal steady roll at the 30 / 36 / 45 / 50 deg limit, deg/s (450 km/h, full thrust, full aft, full roll):

| | 30 | 36 | 45 | 50 | worst overshoot | worst sideslip |
|---|---|---|---|---|---|---|
| 4.7.2 X2e | 50.0 | 37.4 | 29.9 | 26.2 | +0.8 | 4.2 |
| A, reserve 0.5 | 48.3 | 46.0 | 45.8 | 46.3 | **+2.7** | 6.7 |
| B, reserve 0.5 | 48.3 | 46.0 | 38.2 | 41.3 | **+2.9** | 6.2 |
| A, reserve 0.42 | 44.9 | 42.5 | 43.6 | 44.0 | +1.3 | 5.7 |
| B, reserve 0.42 | 44.9 | 42.5 | 36.2 | 39.2 | +1.1 | 5.7 |

## Regression against the handoff

On the 4.6.5 law the worst reversal overshoot of A was +1.3 deg. On 4.7.2 it is **+2.7 deg**, at the 36 deg limit, about 1 s after the roll is released, with 5.9 deg of sideslip. X2e is unaffected (+0.7).
- Turning off 4.7.2's `FlightPathRateMatched` gives +1.9; turning off 4.7.0's `CapacityFromHold` gives +2.2. Neither alone restores +1.3. Not traced further.
- `TwoZoneRollReserve` 0.42 brings it back to +1.3 / +1.1, at the cost of roll: 30 deg falls to 44.9 against X2e's 50.0 (-10 %).
- At 0.5 the 30 deg roll is already 3 % under X2e (48.3 vs 50.0). On 4.6.5 it was 1 % over.
- The default stays 0.5, as handed off. This is a choice for Tom.

Other battery rows (reserve 0.5):

| | X2e | A | B |
|---|---|---|---|
| Slow idle roll overshoot, 36 / 45 | +0.6 / +0.6 | +1.7 / +1.0 | +1.7 / +1.2 |
| Push in a roll, pitch-rate change in 1 s, 36 / 45 | -28.1 / -21.1 | -14.1 / -17.3 | -14.2 / -13.7 |
| Stop after reversal, s, 30 / 36 / 45 / 50 | 0.63 / 0.65 / 0.77 / 0.95 | 0.57 / 0.88 / 0.78 / 0.87 | 0.57 / 0.80 / 0.70 / 0.87 |

Low AoA (the check the handoff asked for; A against X2e; level flight then 0.15 and 0.4 aft stick at 280, 450, 700, 1000 km/h, TVC on and off): same AoA and g within 0.5 deg / 0.1 g, no pitch oscillation, stab trim smaller (level at 450 km/h: +0.1 vs +0.9 deg). Take-off roll and landing were not run.

## Known gaps

- Game-side code never compiled or run. The bench plant is a separate implementation of the same forces.
- From the other 25 Nov chat (stick-split bench): with any measured roll limit the pace governor's yaw cap makes the roll target dip (39 -> 15 deg/s for 0.1 s) about 0.4 s into slow idle rolls. Not addressed here.
- `NonSurfacePitch` / `HsPoint` do not know the slat coupling; the envelope and roll-damping sweep do not know the fin factor or the two-zone stab. Left as benched.
- `StabilatorPlateCurve` is on with the switch. 4.6.1 bench: with both fins gone that curve departed in push-overs from 65 deg. Damage cases were not rerun.
- Inert prototype statics still in `FcsController` (not bound to any setting): `CoupUsesAuthNet`, `StabFinShield*`, `ToeTrim*`, `ReflexReliefGain`, `FlaperonReflex*`, `WingTrimCost`, `RollInertiaWhatIf`, `SlatAftInCoupLim`, `DbgSweepAt`.
- TVC-on behaviour of the two-zone airframe beyond the low-AoA check was not run.

## To finish

Connect `...\Nuclear Option\NuclearOption_Data\Managed` and `...\Nuclear Option\BepInEx`, then: compile with `build.sh`, fix whatever the compiler finds in the game-side files, install with the 4.7.2 DLL kept as `.bak`.
