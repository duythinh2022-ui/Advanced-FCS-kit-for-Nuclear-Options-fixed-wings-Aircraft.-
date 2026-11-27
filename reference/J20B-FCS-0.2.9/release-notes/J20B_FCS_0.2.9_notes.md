# J-20B Weilong FCS 0.2.9: the 0.0.5 mod flies on 0.0.4's tuned mass layout

Base: 0.2.8. **Status:** compiled against the game's assemblies. Not benched (the bench has no J-20 airframe file). Not flown.

Tom (25 Nov 18:28): "how about making it overriding the 0.0.5 aero and cg changes, and just use the result we tunned for the prevois ver".

## What 0.0.5 changed (0.0.4 vs 0.0.5, bundle + AeroControl 0.66 -> 0.68 contract)
- **Aero: nothing.**
  - Every AeroPart lift area, drag area and airfoil is the same; so are all part world positions.
  - The surface specs (travel, servo, gains, schedules) are the same apart from the fins' air-brake gain (+-12 -> +-24), which the FCS doesn't use (it flies the fins itself).
  - The force points are pinned to the same fixed aircraft-frame markers ("frozen-009M-force-point-root-exact").
- **Mass layout: changed.**
  - The new `MassProperties069` table writes fixed rigidbody centres of mass on the root, cockpit, rear fuselage, intakes and engines, after the game's Aircraft setup. The mod picks them to keep its baseline aircraft CG with the rebuilt damage colliders.
  - The two side-bay parts (189 kg each, no lift) were removed and their mass folded into the intakes.
  - Centres of mass along the body axis, aircraft frame (m):

| part | 0.0.4 | 0.0.5 |
|---|---|---|
| root (J20NativeMinimum) | +3.00 | -0.01 |
| cockpit | +5.63 | +5.15 |
| fuselage_R | -3.60 | -3.03 |
| intake (+ side bay) | -2.59 | +2.64 |
| engines | ~-3.85 | -6.39 |

  - Same aircraft CG by design; about +3 % pitch inertia (point-mass estimate, full fuel).
- **Not flight-relevant:** hierarchy (canards on the intakes, wingtips and ailerons no longer under wing2, nozzles under the engines; this affects which parts go with which in damage), armour, joint strengths, fuel-tank ignition, gear springs (front 3e5 -> 6e5, mains 5e5 -> 1.2e6) and damping.

## Change
**`RestoreMassLayout004`** ("5. Airframe", dev, **on**): on the 0.0.5 mod, those seven parts' centres of mass go back to 0.0.4's.
- Applied as a Harmony postfix on the mod's `MassProperties069.AfterComplexPhysics`, so it runs right after the mod writes its own values.
- Also applied right before the FCS's CG move, so the order between the two doesn't matter. CgAheadOfMains 0.75 and the fuel balancing then work on 0.0.4's layout, as tuned.
- 0.0.4 is untouched (no mass table there). false = the mod's 0.0.5 centres.

**Values:**
- Taken from the 0.0.4 bundle's convex collider hulls. The same method reproduces the 0.0.5 table's own "uncorrected" centres to 1-5 mm.
- Each intake includes its old side bay.
- **Engines are an estimate:** 0.0.4 used one of the game's own collider meshes, which isn't in the bundle. Aircraft z = -3.85 is the value that fits the mod's 0.0.4 baseline dry CG and the 0.77 / 0.64 m (full / empty) the FCS logged on 0.0.4, to within +-0.1 m.

## Not done
- Rigidbody inertia tensors are left as Unity computes them from 0.0.5's colliders. Each part's own inertia is small next to the mass spread.
- The hierarchy and damage changes are left as the mod has them (structure, not tuning).

## Check in the log
- `J-20B FCS: hooked Reasonix.J20.AeroControl 0.68.0.0`
- `the J-20B mod's 0.0.5 mass table found; 0.0.4's centres of mass are put back after it`
- `J-20B mod 0.0.5 mass table overridden with 0.0.4's centres of mass ...`
- `J-20B FCS CG: clean full-tank CG ... now 0.75 m ahead`. The first figure there (before the move) should be near 0.0.4's 0.77.
