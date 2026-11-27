# F-22E Strike Raptor FCS 4.5.2: the governor's roll window now binds when roll is saturated

Base: 4.5.1. **Status:** compiled against the game's assemblies and benched on the calibrated plant with Tom's config. **Not flown.**

## The case (log F22E_FCS_20261122_224651, 4.5.1)

Tom, 22 Nov: "the rudders was commanding more yaw than roll could catch up so sideslip spiked to 10-15".

The sequence was: pull, release, then roll at the AoA where the stabs stall-reverse. Sideslip climbed to 10-15°.

## Root cause

The pace governor sizes the yaw command to what roll can follow. Roll capacity (`capXP`/`capXN`) has a floor, `PaceSeenUse × aRollSeen`, which is the roll acceleration the jet has recently demonstrated.

In 4.5.1 that floor stayed in force while the allocator was reporting roll as saturated (`allocUnmet`, giving `satXP`/`satXN`). So at the moment the stabs ran out of roll, the governor still believed roll could deliver its earlier rate. The roll window never bound, and the rudder was allowed to yaw the jet faster than the roll axis could follow. That gap is the sideslip.

## Fix (same law, not an extra layer)

Under saturation, the roll capacity now blends from the demonstrated floor down to the modelled capacity, scaled by the saturation fraction:

```
seenX = lerp(max(capX, PaceSeenUse·aRollSeen), capX, satX)
```

This means the "seen" floor is believed only to the extent that roll is actually being delivered. Nothing else in the governor changed: same windows, same gates, same yaw path.

- Switch: `FcsController.PaceRollSatDelivered` (true = 4.5.2, false = 4.5.1). With it off, the bench output is byte-identical to 4.5.1.
- IL diff vs 4.5.1: only `FcsController::.cctor` and `::LateralLaw` change. The native core and resources are unchanged, and the native allocator is bit-identical (2642/2642).

## Bench results (calibrated plant, tomcfg)

**Replays of the 22:46 log windows:**

| Window | Sideslip 4.5.1 → 4.5.2 | Roll at 1 s |
|---|---|---|
| ~30 s event | 8.1° → 2.4° | 55 → 41 deg/s |
| ~60 s event | 10.4° → 8.1° | about the same |

**Held rolls at 44-48° AoA:** sideslip 7-9° → 3.6-6.7°, with equal or better roll rate.

**Unchanged cases:**
- Pitch-only releases are identical.
- High-speed release-rolls and AoA-limit cases are unchanged.

**What it costs:**
- 30 s replay: roll at 1 s drops 55 → 41 deg/s. The jet waits for roll instead of yawing ahead of it.
- 450 km/h, idle, release-roll: roll at 1 s drops 28 → 22 deg/s.
- Corkscrew entry at 400 km/h, throttle 0.3: sideslip 4.0° → 5.5°, mean roll −2 deg/s.
- Pull-then-roll: peak roll −1 to −2 deg/s.
- One case gets worse: 400 km/h, hold 1.0, roll at release, sideslip 10.7° → 12.4°. This is a different mechanism. At 46-48° AoA the allocator's model and the plant disagree, and the allocator reports no unmet roll, so this fix never engages there. It is left open; see below.

## Not in this build

- **Release-capture redesign** (sharing stab authority between pitch capture and roll during the release transit). It was benched and rejected: it regressed 500-600 km/h release-rolls (roll at 1 s 98 → 53 deg/s). It needs a needs-based redesign first.
- **Airframe W** (plate stab curve, wake-exit downwash migration) and **velocity-frame allocation**: both failed the bench, so both are dropped. See `F22E_4.6.0_airframeW_governor_findings_2026-11-23.md`.
- **The Vortex 0.4.5 pair-link allocator fix**: it does nothing in these flights.

## Open items

1. The allocator model vs plant mismatch at 46-48° AoA, where no unmet roll is reported (the 400/1.0 case above).
2. The release-capture redesign.

## Bench tooling

`bench/sim/StabAuth.cs` adds the `stabauth` mode, which maps stab roll authority against AoA. It is bench-only, not part of the plugin.
