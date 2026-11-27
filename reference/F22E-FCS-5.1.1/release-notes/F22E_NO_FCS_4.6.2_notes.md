# F-22E Strike Raptor FCS 4.6.2 (24 Nov 2026)

Base: 4.6.1 (airframe W, pitch-work-gated lateral reserve). Tom's asks (00:46):
- Why is the AoA limit at 0 throttle reduced, not 45°?
- Improve the airframe's lateral authority to at least what it was with X2e (the 4.6.0 airframe).

Bench-verified only, not flown.

## Why the 0-throttle AoA limit is ~33°, not 45° (00:42 log)

- **The rule doing it:** at low thrust the AoA is capped where the stabs can still stop the nose aerodynamically (authority cap, `authCap`). This is Tom's rule from 2.0.x: "at low throttle cap max AoA to where the stabs still have nose-down authority".
- **The log:** with T/W under 0.2, `authCap` was 32.8–33.5°, holding the jet at 33°. At T/W 0.2–0.4 the cap opened to 45° again.
- **Why W triggers it:** in airframe W the stab has no downwash past ~34° body AoA, so a neutral stab sits at its 36° lift peak and nose-down is nearly gone. At 400 km/h, nose-down goes from −46 deg/s² at 32° to −16 at 36° and −12 at 40°. With X2e it is still −72 at 40° and −47 at 48°, so the cap sat at the 45° limit.
- **Tried:** raising the stab's lift peak from 36° to 40° / 44° gives only −16 / −26 at 40° and −15 / −21 at 44°. Not enough, and it lowers low-AoA stab roll (164 → 157 / 148).
- **What it would take:** a 45° idle limit needs the stabs to keep nose-down to 45°, which on this model means the downwash, i.e. X2e.

## Slat roll (on by default, rev 31)

**Physics fix.** A leading-edge device only moves the stall; once the wing is fully separated it changes nothing. SlatAero's stall remap now closes `SlatShiftSpan` (25°) past the later of the two lift peaks, instead of at 90°. The slat plate's own force also fades out over the last 10° before that. On Aryx's wing (peak 35°) this means no slat effect past ~60° AoA.

In 4.6.1 the slats still moved deep-stalled panels at 60–65°. There their roll is mostly sideslip, which is why `SlatEffectors` was off.

**Static lateral authority** (stab roll at 300 km/h + one side's slats retracted, in deg/s²):

| Body AoA | 36 | 40 | 44 | 48 | 52 |
|---|---|---|---|---|---|
| X2e (4.6.0) stabs | 149 | 145 | 141 | 129 | 95 |
| W stabs | 101 | 82 | 73 | 64 | 57 |
| One side's slats | 39 | 37 | 32 | 27 | 23 |
| W + slats | 140 (94 %) | 119 (82 %) | 105 (74 %) | 91 (71 %) | 80 (84 %) |

The other side can't droop further: the schedule is already at the 25° maximum from 20° AoA. Above ~60° both airframes are the same (no downwash in either).

## Bench, 4.6.2 vs 4.6.0

Better:
- Pull+roll reversals: at 450 km/h t90 1.88 → 1.58 s, sideslip 15.6 → 9.2°; at 556 km/h 2.30 → 1.87 s.
- Release rolls (31 events): roll rate at 1 s 43.2 → 48.1 deg/s; sideslip 3.1 → 2.2°.

Same as 4.6.0 (the 4.6.1 slat-effector sideslip is gone):
- Leaf reversals, full-aft roll reversals: sideslip 4.1 / 5.7° against 2.2 / 4.2° (4.6.1 with slats: 12 / 17°).
- Clean and no-fin leaf stops / reversals.
- 65° roll releases.

Still worse (from W, not lateral authority):
- **Corkscrew-entry roll dips.** The roll-coupling cap limits the roll to what nose-down can hold, and W has no aerodynamic nose-down at 40–55°. Minimum roll rate:
  - 556 km/h, throttle 1: 57 → 28
  - 630 km/h, throttle 1: 65 → 24
  - 400 km/h, throttle 0.3: 33 → 22
- **Release-roll start:** 25 → 18 deg/s at 0.5 s.
- **Pull-then-roll AoA overshoot:** 47 → 55° at 250 km/h, 44 → 49° at 320 km/h.
- **No fins, push-overs at 560 km/h:** sideslip peaks at 28° held and 18° released, against 8° on 4.6.0. At 600 km/h both are fine.

## Files

- `C:\Games\NO mods\F22E-FCS-4.6.2-release`.
- Installed: `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 115c7a5daf4211ab6851acbf37c7b5c2) and `meta.json`.
- Backups: `Aryx_F22E_StrikeRaptor_FCS.dll.4.6.1.bak` and `BepInEx\config\Aryx_F22E_StrikeRaptor.FCS.Dev.cfg.4.6.1.bak`. The 4.6.0 backups from the previous update are still there.
- Back to 4.6.0's airframe, keeping the reserve fix and slat roll: the four Dev cfg lines in the 4.6.1 notes.
- Source tree: `/home/claude/r462`.
