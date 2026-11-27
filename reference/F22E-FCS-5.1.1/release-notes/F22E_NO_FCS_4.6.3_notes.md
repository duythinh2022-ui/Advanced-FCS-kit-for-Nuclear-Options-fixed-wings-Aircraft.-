# F-22E Strike Raptor FCS 4.6.3 (24 Nov 2026)

Base: 4.6.2 (airframe W, slat roll, pitch-work-gated lateral reserve). Tom's asks (00:57):
- Could the slats give some more nose-down past 33°?
- Improve nose-down authority at idle and with no TVC up to 45° AoA.
- Improve lateral authority further.

Bench-verified only, not flown.

## Slat nose-down

Retracting the inboard slat (the one ahead of the CG) gives nose-down. At 300 km/h, in deg/s²:

| Body AoA | 28 | 32 | 36 | 40 | 44 |
|---|---|---|---|---|---|
| Inboard slat retracted | −2.1 | −2.9 | −3.3 | −3.1 | −2.6 |

The outboard pair gives a little nose-up. The allocator can use the slats for this as effectors, but it is small against the 8 deg/s² margin the low-thrust AoA cap keeps. On its own it moves the cap by about a degree.

## Natural nose-down from 34° (the change)

- **Airframe:** the fore/aft lift schedules are refitted so that, with the stabs neutral, the airframe pitches nose-down at 40 deg/s² (at 300 km/h) from 34° body AoA. It ramps in from 26°. From 52° up it is X2e's own nose-down, which is larger there. Lift is unchanged.
- **Below 24° nothing changes:** moment and lift match 4.5.3.
- **Migration:** forced once (AirframeRevision 32).

**Why it works without downwash.** In W a neutral stab sits at its 36° lift peak from ~34° body AoA. More lift from it is impossible, so there is no nose-down.
- With the airframe pitching nose-down there, the stabs hold the AoA trailing edge up. That keeps them below their peak.
- Nose-down authority is then the airframe's own moment plus the stabs' range down from that trim.
- The stabs' differential has room both ways again, so roll comes back too.
- This is how real jets meet the high-AoA nose-down guideline (HANG: 0.24–0.3 rad/s² from 40–50°): mainly from the airframe's own pitching moment.

Two bias levels were fitted and benched: 20 and 40 deg/s². 40 is the default.

## Results (bench)

**Idle and no-TVC full pulls** (target 45°):

| | 4.6.2 | 4.6.3 |
|---|---|---|
| 300 km/h, idle: peak / held AoA | 53.6° / 47.8 ± 3.0 | 45.1° / 44.9 ± 0.3 |
| 300 km/h, no TVC: peak / held AoA | 48.8° / 42.7 ± 3.6 | 45.0° / 44.8 ± 0.4 |
| 450 km/h, idle and no TVC | overshoot to 47–49° | 45.0° held ± 0.4 |

The stabs hold 45° at +21 to +25° (of +35).

**Corkscrew entries:** the dips are back to 4.6.0's level or better, and the cap minimum is restored. Minimum roll rate (deg/s), 4.6.0 / 4.6.2 / 4.6.3:

| Case | 4.6.0 | 4.6.2 | 4.6.3 |
|---|---|---|---|
| 556 km/h, throttle 1 | 57 | 28 | 59 |
| 630 km/h, throttle 1 | 65 | 24 | 66 |
| 400 km/h, throttle 0.3 | 33 | 22 | 39 |
| 556 km/h, throttle 0.3 | 38 | 23 | 45 |
| 700 km/h, throttle 0.15 | 39 | 24 | 48 |

Max g in corkscrews is 0.3–0.5 g lower.

**Release rolls (31 events)**, 4.6.0 / 4.6.2 / 4.6.3:

| | 4.6.0 | 4.6.2 | 4.6.3 |
|---|---|---|---|
| Roll rate at 0.5 s (deg/s) | 25.1 | 18.4 | 22.1 |
| Roll rate at 1 s (deg/s) | 43.2 | 48.1 | 55.2 |
| Time to 15 deg/s (s) | 0.34 | 0.47 | 0.42 |
| Max AoA | 49.5 | 52.4 | 49.8 |

**Other rolling manoeuvres:**
- Pull-then-roll AoA overshoot is fixed (250 km/h: 47.2 / 55.5 / 46.4°).
- Held 0.4-stick + roll: peak roll rate 27 / 23 / 35 deg/s.
- Pull+roll reversals at 450 km/h: t90 1.88 / 1.58 / 1.55 s.

**Same as 4.6.0:** leaf stops/reversals (clean, one fin, no fins), 65° roll releases, push+roll, pull-throughs, clean push-overs.

**Worse:**
- **Full-aft roll reversal at 500 km/h:** AoA 63.5 → 67°, time to zero roll 0.68 → 0.85 s, sideslip 4.2 → 7.3°. At 300 km/h it is unchanged.
- **No fins, push-overs from 65° at 560 km/h:** the pull overshoots to 72° and the push-over departs (33–39° sideslip; 4.6.2: 18–28°; 4.6.0: 8°). At 600 km/h it is fine. The W stabs have less nose-down at 48–55° than X2e's, and with no fins the stabs also carry the yaw.
- **Stab trim at high AoA:** a held 55° at 400 km/h trims the stabs at +30° (4.6.0: +22°), with the nozzles taking little of it.

## Files

- `C:\Games\NO mods\F22E-FCS-4.6.3-release`.
- Installed: `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 0a00ee9d1be84c861f6baa18bc0c5409) and `meta.json`.
- Backups: `Aryx_F22E_StrikeRaptor_FCS.dll.4.6.2.bak` and `BepInEx\config\Aryx_F22E_StrikeRaptor.FCS.Dev.cfg.4.6.2.bak` (the 4.6.0 and 4.6.1 backups are still there).
- Source tree: `/home/claude/r463`. The fit tool is `/home/claude/w/fitbias.py <bias> <tag>`.
