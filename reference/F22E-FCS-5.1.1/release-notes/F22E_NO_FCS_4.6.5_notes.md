# F-22E Strike Raptor FCS 4.6.5 (24 Nov 2026)

Tom (01:29): "reinstall the version with X2e, allocator with governor fix and the improved slat".

Bench-verified only, not flown.

## What is in it

- **Airframe X2e** (the 4.6.0 airframe):
  - stab downwash fading 45–60° body AoA;
  - Aryx's stab curve;
  - 4.6.0's fore/aft lift schedules (refitted for slat physics).
  - Forced once over the Dev cfg (AirframeRevision 34).
- **Allocator:**
  - 4.5.2's governor fix and 4.5.3's release capture (both kept since).
  - 4.6.1's lateral-reserve gate. The stabs leave the pitch to the nozzles only while there is pitch stick, while the release capture is still stopping the nose, or where the nozzles have more pitch authority than the stabs. Plain fast rolls at low AoA trim on the stabs again, as in 4.5.1, instead of pinning the TVC at −16°.
- **Slats** (4.6.0 slat physics, improved):
  - **Flush when retracted** (4.6.1): no separate plate lift as the wing stalls.
  - **Normal-force stall shift** (4.6.1): a retracted slat lowers lift and drag together, with no spurious yaw.
  - **Effect closed past full separation** (4.6.2, `SlatShiftSpan` 25°): no slat effect on a deep-stalled panel past ~60° AoA.
  - **Slat roll on** (4.6.2, `SlatEffectors`): the six slats are allocator effectors; one side's slats are worth ~40 deg/s² of roll at 36–44° AoA, 300 km/h.

## Bench vs 4.6.0

Better:
- Pull+roll reversal at 556 km/h: t90 2.30 → 1.95 s, sideslip 18.2 → 15.6°.
- Release rolls (31 events): roll rate at 1 s 43.2 → 48.0 deg/s; sideslip 3.1 → 2.6°. Roll rate at 0.5 s 25.1 vs 24.7, time to 15 deg/s 0.34 vs 0.36 s.
- One-fin leaf reversals: sideslip 5.0–5.2 → 2.6–4.4°.

Same within noise:
- corkscrew entries (dips, coupling cap);
- the regression set (pull-then-roll, held rolls, push+roll, push-overs, pull-throughs, 65° releases);
- damage cases (no fins, one fin, flaperon and wing damage);
- 1 g rolls.

Worse:
- Pull+roll reversal at 650 km/h: t90 0.78 → 0.95 s, sideslip 7.8 → 10.5°.
- Full-aft roll reversal at 500 km/h: sideslip 4.2 → 6.1°.
- Leaf reversal at 160 km/h: sideslip 5.5 → 6.4°.

These come from slat roll: the 4.6.1 build on this airframe with the slats off matched 4.6.0 in these cases. `SlatEffectors = false` in the Dev cfg turns it off.

Not included: the 4.6.4 experiment (the authority sweep counting rudder toe and the slats, plus the rudder-toe term that had lost its value in the shared-code port). It was benched only on airframe W.

## Files

- `C:\Games\NO mods\F22E-FCS-4.6.5-release`.
- Installed: `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 3991bc28072d65c4a4214dbf2599f37c) and `meta.json`.
- Backups: `.4.6.2.bak` (DLL and Dev cfg) next to them; earlier `.bak` files are still there.
- Source tree: `/home/claude/r465`.
