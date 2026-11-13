# FS-12 Revoker FCS 1.4.1 (12 Nov 2026): max pitch rate 60 deg/s by default

Base: 1.4.0. Patch bump: a config default only.

Tom, 12 Nov 17:28: "make the revoker do 60deg/s max pitch rate as default, just change the config no need for bench".

**Changelog:**
- **`MaxPitchRate` default 48 -> 60 deg/s** (dev, `2. Envelope`), the F-22E's and the Su-30's value.
- **Migration:** a dev config still holding 48 is moved to 60 once (`RevokerConfigRevision` 15 -> 16). A value set by hand is kept. LogOutput shows "Revoker FCS 1.4.1 defaults: MaxPitchRate 60 (from 48), once."

**What it changes:**
- `MaxPitchRate` is the ceiling of the pitch-rate budget everywhere: the MPO and gear-down rate command, the AoA / G law's rate part, and the push side.
- `FullStickPitchRate` is already 60, so that ramp (from MaxPitchRate to 60 over the last 20 % of aft stick) is now flat and full aft is unchanged.
- Partial aft stick, every push, MPO and the gear-down law: up to 25 % more rate per unit of stick where the budget reaches the ceiling.

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,349,120 bytes, SHA-1 8fda1d3ac5cdabda414442537681f0ebf1af9429.
- **Not benched** (Tom's call). On the Su-30 the same change made 0.7 stick at 300-450 km/h ask for more pitch rate than the flight path turns at, so the nose came further than it used to (Su-30 0.5.7 notes); expect something similar here.
- **Not flown.**
