# F-35 footage: aerodynamic read, and why the F-22E airframe attempts keep missing it (25 Nov 2026)

Discussion only. Nothing built, nothing benched in this round. Hypotheses are marked as such. Item 4 was revised at 01:45 after Tom's correction (falling leaf shows barely any trim).

Inputs: seven clips from Tom (F-35A demo: square-loop exit, tactical pitch, pedal turn, high-AoA pass; three air-to-air clips of a Belgian F-35A), Tom's frame-by-frame read of the stabs, and the archive `C:\Games\NO mods\F22E-FCS-archive-2026-11-24` (4.5.2 → 4.6.5, airframe W, 4.6.3 bias, 4.6.4 experiment).

## What the frames support (30 fps crops)
- **Clip 4 (high-AoA pass), 1.8–2.2 s:** one stab swings through a large angle in about 0.45 s and a gap opens at its root; the other stab holds position within what can be resolved. "One moves, the other parks" is confirmed. Side and direction can't be called from that camera angle.
- **Clip 2 (tactical pitch), ~3.5 s:** the stab leading-edge roots pop out of the planform as the pull starts: a pitch-up pulse. How far they return can't be seen from a belly view.
- **Clip 6:** head-on from a photo ship, so left/right are mirrored: the jet rolls to its own left. It is a formation break, not a ~30° AoA manoeuvre (level formation attitude before the roll; vapour only as g comes on).
- Clips 1, 3, 5, 7: jet too small or the angle wrong to judge stab angle. Tom's read is taken as given.

## Aerodynamic read
1. **Near-neutral stabs in a turn = CG at the manoeuvre point, not the neutral point.** In a steady turn the jet is pitching, so pitch damping makes a nose-down moment. Stabs sit near neutral only if the airframe is statically *unstable* by the amount that cancels that damping. A statically neutral airframe still needs stab/TVC held in for the whole turn.
2. **"A nudge, then it drifts to max AoA" = an unstable band in the pitching-moment curve.** Through the mid AoA range the airframe's own nose-up moment grows with AoA and feeds the rotation against damping. Speed bleeds during the pitch (roughly 300 → 150 kt = dynamic pressure down ~4×), so damping falls too.
3. **"A small check stops it" = the pitch-down break near the top.** Vortex breakdown moves lift aft; the wide aft body and tail act as plates in crossflow. The airframe goes stable and decelerates its own rate.
4. **Max AoA is a trim point, and the trim seen is for pitch rate (revised).** Tom: in the falling leaf / pedal turn at max AoA the stabs barely trim; after the peak of the post-stall loop they trim a bit. The difference between the two is pitch rate: the leaf holds AoA with almost none, the loop sustains one. So the stabs-neutral static moment at the limiter AoA is about zero with a restoring slope (nose-up below it, nose-down above it), and the trailing-edge-up stab after the loop peak pays pitch damping, not a static nose-down.
   - Caveat: a velocity-vector rotation makes an inertial nose-up moment, q̇ ≈ (Izz − Ixx)/Iyy · Ω² · sin α · cos α. At ~30 deg/s and 50° that is roughly 6 deg/s² (assuming (Izz − Ixx)/Iyy ≈ 0.9). It could hide a modest static nose-down in the pedal turn. The footage can't separate the two.
5. **Post-stall stab = flat normal-force plateau.** Body-normal force of a stalled stab ≈ CN·cos δ, so trailing-edge-down deflection past the stall changes pitch very little (30° costs ~13 %), while it adds drag well aft and outboard: yaw into the roll, which is most of a velocity-vector roll at 30°+ AoA. Deflecting the other way (back toward attached flow) dumps that stab's force: large pitch change. That asymmetry explains "big differential barely affects pitch unless one stays near neutral" and "the inside stab goes down, the other parks".
6. **Coplanar tail (F-22/F-35) vs low tail (Super Hornet).** A coplanar, close-coupled tail is in the strongest downwash at low AoA; the wing's influence fades as the wake lifts above it. Reversal at ~30–36° body AoA says the fade is largely complete by then. That is airframe W's wake-exit model (fade 24–40), not X2e's (45–60).

## Why the mod keeps missing it
- **The game has no interference between parts.** Every part reads its own lift/drag chart at its own free-stream AoA. Items 2–6 are all interference effects, each emulated by a separate schedule (StabDownwash, Fore/AftLiftSchedule, StallStagger).
- **Three wants sit on one stab curve** (24 Nov study): no trim to 45°, nose-down to 45° at idle/no TVC, stab reversal at ~30–36° with the stall point unmoved. Aryx's foil has CN flat at 1.16–1.20 from 35 to 65°, so a neutral stab at its peak has no nose-down left.

| 400 km/h nose-down, deg/s² | 36° | 40° | 44° |
|---|---|---|---|
| X2e (downwash to 45°) | −77 | −72 | −65 |
| W (wake exit 24–40°) | −16 | −12 | −13 |

- **None of the tried airframes has the footage's moment shape.** X2/X2e: zero static moment across the band (neutral, no unstable band, break only from 44°). 4.6.3: a constant 40 deg/s² nose-down from 34° (ramping from 26°), which is a static trim cost at the cap (+21 to +25° of stab at 45°); the footage shows none in the leaf.
- **How much nose-down the real jet needs is small.** The guideline quoted in the 4.6.3 notes is 0.24–0.3 rad/s² (14–17 deg/s²) at 40–50°. W's stab-only figure is in that range at 400 km/h and below it at lower speed. X2e's −72 is several times it, and the FCS's protection and caps are tuned around having that much.

## Open hypotheses to test on the bench (not done on 25 Nov)
1. Measure the airframe's pitch damping (moment per deg/s of pitch rate at fixed AoA) and compare stab/TVC held in steady turns against it.
2. Refit the fore/aft schedule to zero moment along the steady-turn line (pitch rate = (g/V)·(n − 1/n) for a level turn at each AoA and speed; g·(n − 1)/V for a wings-level pull), not at zero rate.
3. On W: instead of 4.6.3's constant bias, fit the stabs-neutral moment to cross zero at the cap AoA with a restoring slope (nose-up band below, nose-down above). Then check what idle / no-TVC push-over rate that leaves, and whether the low-thrust cap and AoA protection can live with a real-jet-sized nose-down.

## What came of it (kit 5.0 note)
The two-zone stab (prototyped the same day, shipped in F-22E 4.8.x) is the model that followed from items 5 and 6. MANUAL §24.11
describes it and its stability margin.
