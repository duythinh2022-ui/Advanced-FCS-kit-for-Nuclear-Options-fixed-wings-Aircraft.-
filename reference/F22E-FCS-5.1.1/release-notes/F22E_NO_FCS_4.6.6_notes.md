# F-22E Strike Raptor FCS 4.6.6 (24 Nov 2026)

Tom asked me to fix 4.6.5's three slat-roll regressions. He also said: "could slat actuator speed increased to fix it. but it would be not elegant. Maybe do it and fix the laws so it would be robust".

This version is checked on the bench only. It has not been flown.

## What changed from 4.6.5

### 1. Slat need gate (new law: `SlatNeedGate`, `SlatShareLo` 0.05 / `SlatShareHi` 0.12)

Every tick the FCS measures the slats' share of the airframe's roll and pitch authority. The share is the slats' moment span over their range, compared with every other active effector on the same axis. Whichever axis gives the larger share is used.

The slat effectors' window scales with that share:
- below 5 % the window is shut, so the slats stay on their schedule;
- from 12 % the window is fully open.

The measured share:

| Body AoA | Slat share |
|---|---|
| below 20° | ~3 % |
| 20–30° | 6–8 % |
| 30–50° | 13–14 % |
| 50–60° | ~10 % |
| past the separation closure | 0 |

**Why.** The slat is the slowest effector in the allocator. At low and moderate AoA it adds about 3 % to the roll the surfaces already have, and its lag is all it brings there. It stays deflected for the old demand after a reversal, so the roll overshoots.

Bench result with slat roll ungated, pull+roll reversals at 635–665 km/h:
- the new-side peak went from 118 to 128 deg/s (median);
- the worst run reached 228 deg/s;
- sideslip went from 8.9 to 11.3° (median).

The same mechanism slowed the steady half-pull roll at 700 km/h from 155 to 150 deg/s. 4.6.5 had this too (160 → 149 in the single run), and its notes did not report it.

Where the surfaces lose authority (30–60° AoA), the share grows and slat roll works as before.

### 2. Slat reach horizon (`SlatReachTau` 0.1 s)

The allocator now balances the other surfaces against where a slat can actually be within 0.1 s, given its actuator rate. Before, it used the slat's command, so a slat still moving counted as already in place. This is the same reach logic the stabs use in push plans (`PushPlanReachTau` 0.1).

### 3. Slat actuator speed 50 → 100 deg/s (`SlatRate`)

100 deg/s is the primary surfaces' rate. It is forced once over the Dev cfg (AirframeRevision 35).

### 4. Slat separation closure fix (SlatAero)

A slat's effect now ends at the panel's own full separation (its stall peak + `SlatShiftSpan`), whatever the droop. In 4.6.2–4.6.5 extra droop moved that point later, so a drooped slat still acted at 60–67° AoA, where its roll mostly turns into sideslip. This fixed the full-aft and leaf reversal sideslip.

### 5. Telemetry

Two new columns, `slatShare` and `slatGate`, come after `sdR3`.

### Tried and not shipped

- **`ProbeReach`:** the capacity estimator's probes capped to each effector's actuator reach. It slowed steady rolls (650 km/h: 139 → 115 deg/s), so I removed it.
- **Roll-rate limits by total incidence:** evaluated at the total incidence (AoA and sideslip together) instead of AoA alone. This breaks a loop where sideslip trades AoA away and the roll limit opens: the 635 km/h overshoot peak fell from 162 to 126 deg/s. But sideslip in the leaf reversal at 160 km/h rose 1° in all three runs, so I took it out. It is a candidate for a separate look.

## Bench results

### The three regressions (single runs, same cases as before)

| Case | 4.6.0 | 4.6.5 | 4.6.6 |
|---|---|---|---|
| Pull+roll reversal 650 km/h: t90 / new peak / sideslip | 0.78 s / 116 / 7.8° | 0.95 s / 120 / 10.5° | 0.80 s / 116 / 8.9° |
| Full-aft reversal 500 km/h, sideslip | 4.2° | 6.1° | 4.0° |
| Leaf reversal 160 km/h, sideslip | 5.5° | 6.4° | 5.5° |
| Pull+roll reversal 556 km/h: t90 / sideslip | 2.30 s / 18.2° | 1.95 s / 15.6° | 1.13 s / 18.4° |
| Half-pull roll at 700 km/h, steady rate | 160 | 149 | 160 |
| Half-pull roll at 556 km/h, steady rate | 133 | 128 | 133 |

### Perturbation ensemble (new: `bench/an/ens.py`)

Single runs turned out to sit on either side of cliffs. For example, the 650 km/h reversal gave anything from 0.85 s / 120 / 10° to 1.10 s / 173 / 16° depending on tiny parameter changes. Each case is now flown over a small grid of entry speeds and reversal times, and I compare medians and worst runs.

"Slats held" means `SlatEffectors=false`: the 4.6.6 airframe with no slat roll.

| Group | 4.6.0 | Slats held | 4.6.6 |
|---|---|---|---|
| Pull+roll 635–665 km/h (15 runs): t90 median | 0.87 s | 0.85 s | 0.82 s |
| — new-peak median / worst | 118 / 132 | 119 / 158 | 118 / 162 |
| — sideslip median / worst | 8.9 / 16.3° | 8.6 / 16.7° | 9.4 / 18.1° |
| Pull+roll 540–570 km/h (9 runs): t90 median | 1.85 s | 1.82 s | 1.13 s |
| — sideslip median / worst | 17.8 / 19.6° | 17.5 / 19.1° | 18.4 / 20.0° |
| Full-aft 480–520 km/h (9 runs): sideslip median | 4.20° | 4.21° | 4.23° |
| Leaf 150–170 km/h (9 runs): sideslip median | 5.56° | 5.62° | 5.60° |
| Half-pull 680–720 km/h (9 runs): steady rate median | 155 | 159 | 160 |

### Release rolls (31 log events, ~36–50° AoA, replayed)

| | 4.6.0 | 4.6.5 | 4.6.6 |
|---|---|---|---|
| Velocity-vector roll rate at 0.5 s (deg/s) | 25.1 | 24.8 | 27.5 |
| Velocity-vector roll rate at 1 s (deg/s) | 43.2 | 48.0 | 46.9 |
| Time to 15 deg/s | 0.34 s | 0.36 s | 0.33 s |
| Peak sideslip | 3.1° | 2.6° | 3.0° |

### Utilisation suite

At 40–55° AoA the commanded roll is higher than 4.6.0 (34.8 → 38.3 and 51.0 → 55.8 deg/s), against a higher measured cap. This is the slat roll working where it is meant to.

### Same within noise as 4.6.0

The regression set, the damage set and 1 g rolls.

## Still open (not fixed, not hidden)

**Pull+roll overshoot at about 635 km/h.** In the 635–665 group, sideslip is up 0.5° (median) and 1.8° (worst) on 4.6.0. The worst runs are an overshoot at about 635 km/h:
1. the rudders saturate in the reversal;
2. sideslip trades AoA away, from 20.6° to 16°;
3. the yaw-share roll limit (rMax / sin AoA) rises as the AoA falls, from 184 to 236 deg/s;
4. the roll goes to 140–160 deg/s.

4.6.0 has the same overshoot (635 km/h, reversal at 0.8 s: 16.3°). It also happens with slat roll off (14.3° at 635 / 0.75), so it comes from the 4.6.1–4.6.2 slat aerodynamics moving the cliff, not from slat roll. The gate shuts slat roll there. The total-incidence limit above is the law-level candidate.

**Pull+roll at 556 km/h, sideslip.** 18.4° against 17.8° (median). The reversal is much faster: t90 1.13 against 1.85 s.

**Corkscrew entry, full pull at 400 km/h.** The roll-rate dip is 58 deg/s in 4.6.0 and 49 here. It is also 51 with slats held, and was 50 in 4.6.5. This is airframe-level (slat aerodynamics). The 4.6.5 notes said corkscrew entries were the same within noise, and that understated this case.

**One-fin damage cases.** These scatter by 1–3° between single runs; for example, the held push-over at 600 km/h gives 7.3–10.1° across variants of this build. 4.6.5's one-fin leaf gains (2.6–4.4°) are not reproduced here (4.8–4.9°), and 4.6.0 gave 5.0–5.2°. I can't tell regression from noise without an ensemble for these.

## Files

- `C:\Games\NO mods\F22E-FCS-4.6.6-release`.
- Installed DLL: `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll`.
- Backups: `.4.6.5.bak` (DLL and Dev cfg).
- Source tree: `/home/claude/r465`.
