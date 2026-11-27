# F-22E Strike Raptor FCS 4.6.0: slats with their own physics

Base: 4.5.3. **Status:** compiled against the game's assemblies and benched on the calibrated plant with Tom's config. **Not flown.**

Tom, 23 Nov 21:44: "Since slats are separate plates from everything, make proper physics for them".

## What the slats were

In Aryx's mod the six slat plates (Slat_L1-3, Slat_R1-3) were meshes only. The game's high-lift device gave them no force. Instead it added **4 m² of area to the WingRear panel** as they deployed (5 → 9 m²). That's not where a slat acts, and that extra panel area also carried its own drag (at high AoA the panel's drag coefficient is around 1).

## What they are now (SlatPhysics, on)

1. **A slat delays the stall of the panel behind it.**
   - Slat_x1 sits ahead of WingFore (covers `SlatCoverFore` = 0.5 of it).
   - Slat_x2 sits ahead of Wingmid; Slat_x3 ahead of Wingtip.
   - That panel's lift peak moves `SlatStallGain` (0.6) deg per deg of droop.
   - Its small-angle slope is kept (a leading-edge device doesn't change the lift slope; CLmax moves along it). Drag follows the same angle map.
   - Positive AoA only.
2. **Each slat is a plate of its own,** at its hinge. Area = `SlatChordFrac` (0.15) × the panel area it covers. It sees the panel's airflow less its droop.
3. **Both terms are measured against the slats' schedule.** The wing panels' curves (Aryx's curve plus your 2.4.6 lift reshape) are the wing with its slats out, so on the schedule both terms are zero. A slat that lags its schedule, is retracted for roll relief, or is later commanded by the FCS makes the physical difference.
4. **The WingRear area trick is gone.** The fore/aft lift balance (X2e schedules) is refitted so lift and stabs-neutral trim match 4.5.3 at every AoA up to 52° (bench, static, 300 km/h). Past 52° the schedule fades out, leaving 6% less lift and 9 deg/s² more nose-down at 56-60°.
5. **Visuals and telemetry.** Left and right droop are separate. The meshes show the real droop (`SlatMaxDroop` 25°; Aryx's animation used 15°). New telemetry columns: `slatL`, `slatR`, `slatRefL`, `slatRefR`.
6. **With the FCS off,** the game's own slat model runs as before.

Settings are in the Dev config, "5. Airframe": `SlatPhysics`, `SlatMaxDroop`, `SlatStallGain`, `SlatChordFrac`, `SlatCoverFore`. A one-time migration (AirframeRevision 29) turns `SlatPhysics` on and installs the refitted schedules. `SlatPhysics = false` gives back 4.5.3's area trick (the old schedules would then need restoring by hand).

The airframe-study knobs `StabDownwashFadeStart`/`End` and `StabilatorPlateCurve` are kept in the Dev config at 4.5.3's values (45 / 60, off). The study is in `F22E_4.6.0_airframe_study_2026-11-23.md`.

## What the slats can do (bench, 400 km/h, droop away from schedule)

| | 28° | 32° | 36° | 40° | 44° | 48° | 52° |
|---|---|---|---|---|---|---|---|
| Pitch, both slats fully retracted (deg/s²) | −0.5 | −1.2 | −1.5 | −1.3 | −0.9 | −0.6 | −0.4 |
| Roll, one side fully retracted (deg/s²) | 4 | 19 | 27 | 26 | 19 | 13 | 10 |

- **Pitch: almost nothing.** The slatted panels sit around the CG (WingFore ahead of it, Wingmid and Wingtip just behind), so their pitch effects nearly cancel. On this airframe the slats can't take high-AoA pitch work off the stabs.
- **Roll: a real effector at 32-44°.** Differential slats give 19-27 deg/s², where the stabs give 141-152 and the flaperons are stalled. That only becomes usable if the allocator commands the slats. **Not built in 4.6.0.**

## Bench results, slats on schedule (vs 4.5.3)

**Held turns, 0-45°:** stabs and TVC within a few degrees of 4.5.3's. Less speed loss at high AoA (the area trick's drag is gone), for example 4.7 vs 3.5 deg/s held at 45° from 450 km/h.

**Regression suite:**
- **Better:**
  - Corkscrew-entry roll dips: 850 km/h throttle 0.3: 51 → 78 deg/s; 700 km/h throttle 1: 101 → 123.
  - Leaf reversal 300 km/h: time to stop 1.83 → 1.05 s.
  - Fin-damage rolling pull 900 km/h: sideslip 17.2 → 8.8°.
- **Worse:**
  - Pull+roll reversal 450 km/h: time to 90 % 1.58 → 1.88 s.
  - Pull+roll reversal 556 km/h: 2.13 → 2.30 s.
  - No-fins push-over 560 / 600 km/h: sideslip 5.6 → 8.2° and 6.1 → 9.0°.
  - Corkscrew 556 km/h throttle 0.3: dip 43 → 38.
- **Unchanged:** utilisation invariant (same 4 pre-existing FAILs), held 65° and the leaf stops.

**The 31 release-rolls from the 20:24 log (pinned replays):** mean roll at 0.5 s 17.8 → 17.8 deg/s, at 1 s 38.6 → 40.0. Event by event it ranges from −27 to +11 deg/s at 1 s. The worst is 178.4 s at 58° AoA (68 → 41), where the lift and nose-down differ past 52°.

## Not in this build

- **Slats as an allocator effector (roll at 32-44°):** a new effector type in the allocator and its native core, servo rate, and the FCS's own models of the slat terms.
- **The bench's pitch calibration at high pitch rate and high AoA.**
