# F-22E FCS 2.44.0: full aft stick at the AoA limit holds zero rate once only a nose-down rate could hold the limit

Base: 2.43.0. Compiled against the game's assemblies, bench-checked, **not flown**. 2.43.0 kept as `.dll.2.43.0.bak`. CSV unchanged.

## Tom (4 Nov, 20:14 / 20:26)
- 20:14: "towards the later half of the log, i was at 65 aoa then, then i held the stick all the way back, but the jet did the stepping pitch down
  to for aoa protection".
- 20:26: "if i hold stick all the way back at max aoa, and the jet still can do positive pitch rate, then nothing happens, as soon as i hold stick
  back at max aoa but the only pitch rate that can hold that aoa is 0 then that is the point the stick can cancel the protection".

## What the 20:07 log shows (143–154 s, 173–182 s)
- Full aft at 65°, slowing at 22–50 IAS, the flight path falling at 1–8 deg/s (`omegaV` < 0).
- `prot` = 0 and `unload` = 0 throughout: the protection never ran.
- **Two rules took turns:**
  - **below limit + 0.5° (65.5°):** the aft-stick floor (aft stick never commands nose-down under the AoA/G law) held the command at exactly 0;
  - **above 65.5°:** 1.9.5's "the pilot's own command beats protection" took the stick command after the AoA-limit capture had clamped it. With
    full aft, that is the capture's flight-path-rate-plus-correction, −11 to −13 deg/s. Full aft did not cancel it.
- **Result:** a ~2 Hz cycle.
  - command 0 ↔ −11..−13 deg/s;
  - AoA 64.5–66.6°;
  - nozzles −13 ↔ +16°.
  - The bench replay of the same segments reproduces it: AoA up to 66.6°, 16 and 10 command jumps over 10 s.

## The change (`StickCancelsLimit`)
- **At the AoA limit,** the nose-down part of the limit's own command is scaled by 1 − stick / 0.9. This is the same factor protection's push has
  always used. The limit's own command is the capture bound below the limit and protection above it.
- **A positive holding rate is never scaled.** When the jet can hold 65° with a positive pitch rate, full aft changes nothing. When only a nose-down
  rate would hold it, full aft holds zero rate and the AoA rises with the falling flight path. Release the stick and protection pushes.
- **Partial aft stick** follows the same scale. This removes the jump at limit + 0.5° that the old floor vs protection split had.
- **1.9.5's rule keeps its purpose:** a stick asking for more nose-down than protection (forward stick above the limit) still gets it. It no longer
  takes the stick command after the capture bound has clamped it.
- **Same on the negative side,** with full forward stick at −50°.
- **Bench alternative, not shipped (`LimitFollowsPath`):** the limit holds 65° and the nose follows the falling flight path smoothly. On the two
  segments: AoA peak 65.06 / 65.09°, no command jumps.

## Bench
- **Your 20:07 segments (free replay):**

  | segment | 2.43.0 | 2.44.0 |
  |---|---|---|
  | 143–154 s | 16 command jumps | none; nozzle swing −17…+19 → −11…+8°; AoA rises to 112° by the end |
  | 173–182 s | 10 command jumps | none; AoA rises to 85° |
  | 160–172 s, 183–191 s (flight path rising, holding 65° needs a positive rate) | | identical to 2.43.0 |

- **Hybrid replays (61 released / 14 held pushes):**
  - released overshoot median 3.2 → 2.8, step 1.5 → 1.3, sideslip p90 12.4 → 11.9 (4 better, none worse);
  - held unchanged within one push.
- **Free replays:**
  - 48-push released sideslip 5.6 / 10.4 / 14.3 → 5.8 / 10.1 / 11.4;
  - 54-push released 6.9 / 13.5 / 18.6 → 7.3 / 13.6 / 18.7, held 7.3 / 12.4 / 12.8 → 7.0 / 10.7 / 12.6.
- **Suite:** identical within 0.3° except the case that already departs (roll rev 890 with fins and right flaperon gone, peak sideslip 85.8 → 89.5).
  The leaf cases hold 65° with a positive holding rate, so they are unchanged.
- **Identity:** the default build replays and suites identically to the switch run it was benched as.

## Not verified
- **Not flown.**
- With full aft held while slowing and the flight path falling, the AoA now goes past 65° (to 85–112° on your two segments) until you release.
  That is the behaviour asked for; it means the jet is in your hands there.
