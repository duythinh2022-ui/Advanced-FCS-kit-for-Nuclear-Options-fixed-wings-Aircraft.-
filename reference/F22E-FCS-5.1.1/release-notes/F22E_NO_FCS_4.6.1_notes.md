# F-22E Strike Raptor FCS 4.6.1 (24 Nov 2026)

Base: 4.6.0. Tom's asks (23 Nov, 23:37–23:58):
- At the end of the 23:37 log the TVC sat at −15 to −17° during a fast roll at low AoA.
- At low AoA, where the stabs have most of the pitch authority, the allocator should behave as in 4.5.1. At higher AoA with enough TVC authority, the stabs do the lateral and don't mind pitch.
- Build it with airframe W and try to improve it. Also slat roll.

Bench-verified only, not flown. The bench is calibrated on Tom's logs.

## 1. Lateral stab reserve gated on pitch work (on)

**Cause.** 4.5.3 removed 4.5.1's pitch-stick gate. After that, any roll stick made symmetric stab travel 71× more expensive in the allocator. Plain fast rolls at low AoA moved the trim onto the nozzles: TVC at −16° at 700 km/h, with the stabs 20° short of their stops.

**Fix.** The reserve now follows the pitch work the stabs are needed for, whichever of these is largest:
- the pitch stick (4.5.1's gate);
- the release capture still stopping the nose (rate error to the plan, the same term as the released pitch priority);
- the nozzles' pitch authority beyond the stabs' own, from the allocator's present moment spans. This is zero while the stabs are the stronger pitch effector.

`FcsController.LatShareNoGate = false`. New telemetry columns: `latGate`, `tvcShare`.

**Results:**
- 23:37 log, fast roll at 117–124 s (free replay):
  - TVC mean: 4.5.2 law −5.7°, 4.6.1 −6.1°, 4.6.0 −10 to −16°.
  - Stab mean: 4.5.2 law −2.0°, 4.6.1 −2.2°.
- 31 release-roll events (20:24 log), on the 4.6.0 airframe: roll rate at 0.5 s 24.5 vs 25.1 deg/s; at 1 s 43.7 vs 43.2. 4.5.3's gain is kept.
- Full suite on the 4.6.0 airframe (reserve fix only): within noise of 4.6.0 everywhere.

## 2. Slat physics corrections (always on with SlatPhysics)

- **SlatPlateFlush:** a retracted slat is flush with the wing, so its plate force fades out over its last 5° of droop. 4.6.0 kept it a separate plate at the panel's full AoA, which added nose-up lift at the leading edge exactly as the wing stalled.
- **Normal-force stall shift:** the slat's stall shift acts on the panel's normal force, with the chordwise force taken at the true angle. 4.6.0 read the drag at the remapped, higher angle, so a retracted slat added drag past the stall. Its roll then came with a spurious yaw toward the retracted side: −8 deg/s² against +8 of roll at 36–40°.
- **Effect:** on schedule both terms are zero, so they only change things while the slats lag their schedule in pulls.

## 3. Airframe W (default; forced once, AirframeRevision 30)

- Stab downwash fades out from 24 to 40° body AoA: the wake-exit tail.
- Aryx's stab curve (`StabilatorPlateCurve` false).
- Fore/aft lift schedules refitted with slat physics so stabs-neutral pitch and lift match 4.5.3's from 0 to 52°.

### Improvements tried

- **Thin-plate stab curve:** dropped. On the bench it departs with both fins gone, with 30–45° of sideslip in push-overs from 65°, at 560 and 600 km/h, released and held. Swapping only the curve both ways confirmed it is the curve and not the downwash fade.
- **Later wake exit (fade 22–48, 28–46, 30–50):** gives stab nose-down into the high 30s, matching the real-Raptor reference, and more roll up to 40°. With the plate curve, nose-down at 36° is −23 / −37 / −54 deg/s² against −4 for fade 24–40. The fade-30–50 airframe was refitted and suited (with and without the plate curve); its corkscrew-entry dips stay. Not used.
- **Slat roll (SlatEffectors):** see section 4.

### Static stab authority

400 km/h nose-down, 300 km/h roll, in deg/s²:

| Body AoA | 32 | 36 | 40 | 44 | 48 |
|---|---|---|---|---|---|
| Nose-down, 4.6.0 | −86 | −77 | −72 | −65 | −47 |
| Nose-down, W | −46 | −16 | −12 | −13 | −11 |
| Roll, 4.6.0 | 152 | 149 | 145 | 141 | 129 |
| Roll, W | 129 | 101 | 82 | 73 | 64 |

At negative AoA, nose-up authority at −40° goes from 73 to 13. Without downwash, a neutral stab sits at or past its 36° peak from about 34° AoA, on both signs.

### W vs 4.6.0, full jet bench (slat effectors off)

Better:
- Pull+roll reversal at 450 km/h: t90 1.88 → 1.75 s, sideslip 15.6 → 9.7°.
- Pull+roll reversal at 556 km/h: t90 2.30 → 2.05 s.

Worse:
- **Corkscrew-entry roll dips.** The roll-coupling cap has no aerodynamic nose-down to spend at 40–55°. Dip minimum (deg/s):
  - 556 km/h, throttle 1: 57 → 26
  - 630 km/h, throttle 1: 65 → 24
  - 400 km/h, throttle 0.3: 33 → 22
  - 700 km/h, throttle 0.3: 50 → 23

  The cap minimum falls from 46–103 to 24–58.
- **Release rolls (31 events):** roll rate at 0.5 s 25.1 → 18.2 deg/s; time to 15 deg/s 0.34 → 0.46 s; max AoA 49.5 → 52.6°. Roll rate at 1 s is the same (43.5).
- **Pull-then-roll AoA overshoot:** 47 → 52.5° at 250 km/h, 44 → 48° at 320 km/h.
- **No fins, released push-over at 560 km/h:** stops near 0° AoA with 22° of sideslip; 4.6.0 reaches −34° with 8°. At 600 km/h it is fine.
- Everything else is within noise, including no-fin leaf stops/reversals, one-fin push-overs and the damage cases.

**Back to 4.6.0's airframe, keeping the fixes above.** In the Dev cfg under `[5. Airframe]`:
```
StabDownwashFadeStart = 45
StabDownwashFadeEnd = 60
ForeLiftSchedule = 0:1.0000,4:1.0934,8:1.1443,12:1.1865,16:1.2282,20:1.2719,24:1.2632,28:1.2651,32:1.2766,36:1.2925,40:1.3151,44:1.2877,46:1.2706,48:1.2723,50:1.2139,52:1.9052,56:0.5670
AftLiftSchedule = 0:1.0000,4:0.8904,8:0.9368,12:0.9755,16:1.0225,20:1.0900,24:1.1033,28:1.1226,32:1.1354,36:1.1307,40:1.1058,44:1.1421,46:1.1914,48:1.2748,50:1.2818,52:2.0461,56:1.9918
```

## 4. Slats as allocator effectors (built, off by default)

**What it is:** `SlatEffectors`, `SlatRate` 50 deg/s.
- New `SlatEffector.cs`: one effector per slat and side (6), with moment tables from the same SlatAero physics the game applies.
- FlapSystem drives each slat to its schedule plus the effector's output while the FCS is engaged in the air.
- The capacity estimator counts the slats with the flaperons.
- The native core needs no change (generic effectors).
- Telemetry columns `sdL1..sdR3`: each slat's droop away from its schedule.

**Static, normal-force model, one side's slats fully retracted, 400 km/h:** body roll 51 / 83 / 82 / 76 deg/s² at 28 / 36 / 40 / 44° AoA. Pitch from the inboard slat alone is −3 to −4 deg/s²; the outboard pair gives +1.5.

**On W:**
- Release rolls: roll rate at 1 s 43.5 → 51.7 deg/s; sideslip lower.
- Sideslip at 60–65° gets worse:
  - clean leaf stop 296: 3.1 → 8.8°
  - leaf reversal 370: 3.2 → 15.8°
  - full-aft roll reversal 300 / 500 km/h: 2.2 → 12.1° / 4.2 → 17.0°
  - 65° roll release: 3.0 → 7.8°

**Cause:** there the slats' body roll is mostly sideslip-making, and the yaw axis can't follow it. Raising the slat rate or excluding the slats from the estimator didn't change it. Left off.

Other knobs: `SlatEffector.SlatMu` 0.05 (at 0.01 the slats drooped up to 14° at cruise to stand in for priced stab trim).

## Files

- `C:\Games\NO mods\F22E-FCS-4.6.1-release`: player zip, optional Dev zip, source zip, notes, `meta.json`, DLLs.
- Installed: `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 e822c6e767dda8bb3f8af43d28be0abe) and `meta.json`.
- Backups: `Aryx_F22E_StrikeRaptor_FCS.dll.4.6.0.bak` and `BepInEx\config\Aryx_F22E_StrikeRaptor.FCS.Dev.cfg.4.6.0.bak`. The update rewrites the airframe keys once, so both backups are needed to go back.
- Source tree: `/home/claude/r461`. Bench config `tomcfg.cfg` is set to the post-migration values.
