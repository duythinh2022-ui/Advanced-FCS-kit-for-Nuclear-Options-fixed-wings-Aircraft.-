# J-20B Weilong FCS 0.2.4: the push governor rule replaces the 0.2.3 gate

Base: 0.2.3. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 15:31-16:03): J-20B "when i command roll and then release, then i command pitch down, roll kept going for a moment then stop then pitch kicks in"; "it does happen on the raptor, but much much shorter"; "fix all of them then".

## Change
- **The push governor never raises the roll above the stick (`PushGovStickCap`, 6. Lateral governor, on).** Pushing toward zero AoA under a roll, the governor (F-22E 2.34.0) bands how fast the roll may change. Its band divides by sin AoA; pushing from near zero AoA under a fast roll that divided by about zero, and where roll x AoA rate exceeded the yaw capacity it told the roll to keep going or grow, above what the stick asked. A released roll kept going until the AoA crossed zero, then was braked mid-push. Now the governor may slow the roll's change but never raise the roll above the stick (the rule for every governor: lower the command where an axis cannot keep up, never raise it). One line, the same on every FCS jet. false = the old law.

## Bench: full roll at about 2-3 deg AoA, then roll released and full push together (all jets)
| Jet, 450 km/h | Roll under 20 deg/s after release | Peak sideslip |
|---|---|---|
| F-22E | 0.83 -> 0.48 s | 6.2 -> 2.5 deg |
| Revoker TVC on / off | 0.73 / 0.88 -> 0.58 / 0.53 s | 2.4 / 4.7 -> 2.3 / 2.5 |
| Su-30 (uncalibrated airframe) | 0.93 -> 0.55 s | 4.8 -> 3.5 |
| J-20B | 0.65 -> 0.55 s | 3.8 -> 3.1 |
| Ifrit / Vortex / Compass | unchanged (the governor did not raise the roll on these) | unchanged |

At 550 km/h only the Revoker with TVC off showed it (0.24 s); now none.

Regression sets, on vs off: identical on the F-22E (regression set and corkscrew entries), Ifrit, Vortex, Compass, Su-30 and the Revoker with TVC on; Revoker TVC off mean sideslip 3.83 -> 3.81 deg. See claude/PushGov_released_roll_findings_2026-11-24.md.

## Removed
0.2.3's `PushGovNearZero` (the band faded near zero AoA, weighted by how far the roll stick was released: a gate) is gone; this rule replaces it.

## Replays of the 15:30 flight
| | Game | 0.2.2 | 0.2.3 (gate) | 0.2.4 |
|---|---|---|---|---|
| 43.3 s, peak sideslip | 14.5 deg | 11.4 | 5.2 | **3.3** |
| same, release to roll under 20 deg/s | | 0.80 s | 0.52 s | 0.33 s |
| 50.6 s, peak sideslip | 31.3 deg | 30.3 | 10.9 | 14.5 |
| same, release to roll under 20 deg/s | | 2.83 s | 0.55 s | 0.72 s |

J-20B regression set (your config): identical to 0.2.2. Held push-over from 55 deg at 300 km/h: 6.8 -> 8.9 deg (a 1 deg/s target difference at the push entry, then the runs diverge); 250 km/h unchanged (6.0).
