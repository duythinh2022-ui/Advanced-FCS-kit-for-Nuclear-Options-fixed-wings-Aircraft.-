# F-22E Strike Raptor FCS 3.2.1: airframe X2e (45–55° roll dip fixed)

Base: 3.2.0. Patch bump: two airframe defaults changed (the fore/aft lift schedules), plus a one-time migration. No control-law code changed.

**Changelog:** Restored full roll rate between 45° and 55° AoA by giving the airframe back its nose-down in that range.

**Status:**
- Compiled against the game's assemblies.
- Benched.
- Flown by Tom in this form via the Dev cfg (X2e from 21:0x, 9 Nov). The X2d step before it was flown and logged (20:52 log); no X2e log has been analysed yet.

## Tom (9 Nov)
- 20:18: "the velocity roll seems to be a bit slower at the higher end of mid aoa. Idk if it is placebo, check logs"
- 20:27: "i'm liking this airframe but the roll dip at 45-55 is slower, are there anyway to boost it up to as good as 3.1.9 if not better without generating more sideslip, stopping margin."
- 20:57 (after flying X2d): "it is better but idk if it is as good as 3.1.9"
- 20:59: "do it" (the extra 52/56° points); 21:44: "migrate the airframe vortex lift settings"

## Cause (20:10 log on 3.2.0)
- **The roll was being capped by the coupling roll limit.** At high AoA, rolling pitches the nose up by itself (inertial coupling). The FCS therefore caps the velocity-vector roll rate at what the remaining nose-down can absorb:
  - the cap is √(2 · 0.5 · netC / (k_I · |sin 2α|));
  - netC is the airframe's full-stab nose-down from the FCS's tables plus the nozzles' nose-down moment.
- **X2 removed most of the airframe's nose-down at 45–55°.** Its 48° schedule point moved lift forward just as the stab downwash starts fading out.
  - Static, 85 m/s, full nose-down stab at 48°: −18 °/s², against −41 on 3.1.9.
  - The FCS's own table read about zero at 48–52°.
- **Effect in flight:**
  - The limit dropped to 24–54 °/s, against 73–80 on 3.1.9.
  - The velocity-vector roll at 45–55° fell to 0.48–0.82 of the max-AoA rate (3.1.9: 0.88–1.03).
- **Loosening the limit itself would spend the stop margin,** so the fix is in the airframe instead.

## Change
**`ForeLiftSchedule` / `AftLiftSchedule` defaults are now airframe X2e:**
- Identical to X2 up to 40°, with a schedule point added at 52° and one at 56°.
- From 44° the schedule gives the nose-down back. The stabs-neutral moment targets are −6.5 / −18.4 / −31 / −30 °/s² at 44 / 48 / 52 / 56° (85 m/s); X2 was zero up to 48°.
- Lift at every AoA is unchanged, and the schedule fades back to stock by 59–60° as before.

```
ForeLiftSchedule = 0:1.0000,4:1.0843,8:1.0814,12:1.0795,16:1.0738,20:1.0577,24:1.0466,28:1.0359,32:1.0319,36:1.0391,40:1.0607,44:1.0334,48:0.9673,52:0.9270,56:0.5670
AftLiftSchedule  = 0:1.0000,4:0.8823,8:0.8862,12:0.8888,16:0.8984,20:0.9232,24:0.9402,28:0.9554,32:0.9616,36:0.9537,40:0.9287,44:0.9596,48:1.0419,52:1.0889,56:1.9918
```

The 56° point looks extreme, but by 56° the fade has already scaled it to about ±20 % effective.

**AirframeRevision 27** forces the two schedules once over older Dev files.
- Log line: `3.2.1 defaults: airframe X2e fore / aft lift schedules (overriding the dev file).`
- A 3.1.9 Dev file runs rev 26 then rev 27, so it ends with stab peak 36 and X2e.
- Tested on copies of your Dev cfg with BepInEx's `ConfigFile`, using the same steps as the migration (the plugin itself was not run):
  - rev 26 with X2 strings → X2e, rev 27;
  - rev 25 (stab peak 25, no schedules) → stab 36, X2e, rev 27.

## Bench (calibrated plant; X2e against X2 and 3.1.9)
**Static, 85 m/s, full nose-down stab (°/s²):**

| AoA | X2 | 3.1.9 | X2e |
|---|---|---|---|
| 45 | −30 | −44 | −42 |
| 48 | −18 | −41 | −44 |
| 52 | −21 | −32 | −41 |
| 55 | −22 | −27 | −36 |
| 58 | −23 | −24 | −27 |

**Flight set:**

| test | 3.1.9 | X2 (3.2.0) | X2e (3.2.1) |
|---|---|---|---|
| hold 20 / 30 / 45° AB, stab | +4.5 / +5.9 / +9.4 | +0.2 / +0.7 / +0.1 | +0.2 / +0.7 / **+4.2** |
| hold 45° idle: stab, peak AoA | +7.4, 45.5 | −0.7, 50.4 | +2.6, 47.8 |
| push 45→20°: idle / TVC off / AB (s) | 1.95 / 1.42 / 0.88 | 2.05 / 1.53 / 0.90 | 1.98 / 1.45 / 0.88 |
| push from 65° at AB (s) | 1.22 | 1.28 | 1.28 |
| low-throttle pull + roll, 400 km/h: peak AoA (5-run median) | 45.1 / 45.4 | 53.9 / 55.9 | 50.3 / 51.1 |
| sideslip, same cases (5-run median) | 4.7 / 5.0 | 4.6 / 4.5 | 5.3 / 5.7 |
| loaded roll at 36°: 400 AB / 250 AB / 300 idle (°/s) | 68.5 / 50.9 / 28.8 | 65.6 / 46.9 / 26.4 | 65.7 / 46.9 / 26.4 |

- **Unchanged from X2:**
  - corkscrew-entry sideslip and the 65° peak (5-run ensembles at 110 / 140 / 180 kt);
  - roll stops released at 48°: 1.5–1.6 s, sideslip 3.0–3.4°, no reversal;
  - full-aft roll reversals at 300 / 500 km/h: sideslip 3.2 / 4.8;
  - push-then-pull-through + roll: 10.4;
  - the no-TVC 45° cap.
- **The bench can't show the roll gain.** The bench's coupling limit doesn't clamp in these entries: its nose-down table reads 20–60 °/s² more nose-down than the game's (cause not found; it isn't load, fuel trim or nozzle authority). So X2, X2d and X2e roll the same there.
- **The flight evidence is the X2d log (20:52).** X2d is the same as X2e up to 48° but has no 52/56° points.
  - The limit was back to 70–82 °/s at AB.
  - The 45–55° roll matched 3.1.9 at matched entry speed (130 kt: 57.7 vs 57.2 °/s).
  - Sideslip 1.4–6.5° (3.1.9: 1.7–8.4).
  - The remaining dip was at 169 kt around 54° (56 against 72 °/s at max AoA). X2e's 52/56° points target that.

## Still open (from 3.2.0)
- **Low-throttle overshoot:** still 50–51° against the 45° cap. It was 54–56° on X2; 3.1.9 stopped at 45.
- **"Hold 30 at idle" case:** the full-aft latch still holds 26.8°.
- **Game vs bench nose-down table gap:** unexplained. A telemetry-only build that logs netC's terms would find it.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 3.2.1 + `meta.json`. 3.2.0 is kept as `.dll.3.2.0.bak`.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.2.1 (3.2.0 kept as `.dll.3.2.0.bak`).
- **Your Dev cfg** already holds X2e; the migration rewrites the same values and sets rev 27.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.2.1-release`.
- **Mod manager:** update the version there, or its modpack may put an old DLL back.
