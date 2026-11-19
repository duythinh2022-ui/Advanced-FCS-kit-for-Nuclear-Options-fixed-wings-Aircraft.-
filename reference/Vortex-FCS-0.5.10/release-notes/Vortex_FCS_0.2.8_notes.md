# FS-20 Vortex FCS 0.2.8 (17 Nov 2026)

Base: 0.2.7. **Status: compiled and benched. Not flown.** The release guard and the throttle-to-zero are game-side code:
compiled against the game's assemblies, never run (the bench has no weapons and no throttle lever).

Built from `claude/Vortex_VS_hold_plan.md` (whole plan), the 01:22 flight log, and `claude/Vortex_Release_Guard_spec.md`.

## 1. STOVL height hold (the plan)

### What was wrong in 0.2.7 (found on the bench, beyond the plan's list)

- **The thrust never answered the roll posts.** In the game's model the posts push the wing *down* (5-7 kN, about
  0.45-0.6 m/s^2) whenever the roll accelerates. The force increment was taken on the whole propulsive force, posts
  included, so the fit asked for the same thrust while a post fired. Only the height loop reacted, after the sink.
- **The thrust was commanded as a 0.35 s lag.** The engine itself allows much faster for small changes.
- **The height loop's own demand fed the translation budget**, so tilt and budget chased each other (pitch target
  3 <-> 13 deg every 4.6 s in a combined stop).
- **Above 40 kt nothing held the height**: path angle only, and the path caught at the handover carried the height
  loop's correction of that moment as a standing climb or sink (0.1 m/s; 2-11 m in the bank case).
- **A released side stick swung the bank target at once** (29 -> -31 deg): the roll went through level at 59 deg/s with
  the thrust cut on the way and overshot to -46 deg.

### What changed (all in `Stovl.cs`)

| plan item | built as |
|---|---|
| D height recapture | One height reference in every regime. In the flight-path regime it moves along the commanded path (level when it came from the hold) and the path loop flies back onto it; it stays within 0.5 m of the jet so a path the jet can't hold isn't chased. A pilot's climb or sink still stops where it stops. |
| B feed-forward | (1) Force increment on the nozzle + fan-balance force only; the posts' and the fan pitch term's change is taken out the tick it is commanded. (2) Thrust sized for the lift direction half a thrust-response time ahead, from the measured body rates and the nozzle's slew. (3) Thrust lead set from the acceleration measurement's delay (2 x (0.05 s + one tick) = 0.13 s) instead of a fixed 0.35 s. |
| A governor | Closed form, each tick, no prediction loop: the engine can raise thrust no faster than (full dry - thrust) / 1 s. A tilt loses lift at tan(tilt) x rate, so the bank command and the pitch tilt move away from level only as fast as a share (20 %) of that headroom; they slow by themselves near full dry. A cap from the same headroom on the roll posts' and the fan pitch input's lift-losing travel was built too and is **off since 03:00** (section 3). The bank command no longer jumps. |
| C swivel target | Moves no faster than the nozzle can, from where it was. |
| budget pieces | The translation budget's vertical reserve is now the command's demand and the lift to fly a height deficit back (was the loop's whole demand). `aLag` and the half-spare-lift bank rule are still there. |

Gains for a commanded climb / sink and its stop stay on the slower 0.35 s response (large thrust swings); the stop's
linear end is set from how fast the engine can spool back. With the fast gains on the stop, a release from a full-stick
climb at 11.9 t reversed to -0.63 m/s.

One number in this is chosen, not derived: the governor's 20 % share.

### Bench: height error and vertical speed

Seven cases (`bench/scripts/vortex/vsset.sh`). "b" = your jet: buffed 1.12, 11.04 t. "s" = stock 11.9 t.
Height error is against the height held just before the inputs.

| case | 0.2.7 b: height / VS | 0.2.8 b: height / VS | longest VS past ±0.2 (b) | 0.2.7 s height | 0.2.8 s height |
|---|---|---|---|---|---|
| 30 kt and stop | -0.51/+0.66 m, -0.51/+0.59 m/s | -0.14/+0.09, -0.21/+0.21 | 2.5 -> 0.3 s | -0.23/+0.48 | -0.10/+0.09 |
| full side + 30 kt, released | -0.59/+0.49, -0.49/+1.19 | -0.17/+0.22, -0.26/+0.39 | 2.8 -> 0.7 s | -0.40/+0.41 | -0.09/+0.10 |
| yaw skid (00:14 repro) | -3.95, never back; -0.99/+0.38 | -0.11/+0.08, -0.30/+0.22 | 4.9 -> 0.5 s | -2.40 | -0.05/+0.08 |
| pedal turn at 30 kt | -0.76/+0.33, -0.38/+0.55 | -0.11/+0.12, -0.20/+0.21 | 2.5 -> 0.2 s | -0.49/+0.29 | -0.02/+0.03 |
| bank reversals (00:24 repro) | -0.83/+11.5, never back; -0.82/+0.78 | -0.12/+0.08, -0.17/+0.25 | 17.9 -> 0.4 s | -1.73/+0.36 | -0.07/+0.05 |
| hover pedal + crab | -0.39/+0.28, -0.50/+0.28 | -0.05/+0.05, -0.18/+0.14 | 1.2 -> 0 s | -0.39/+0.30 | -0.03/+0.04 |
| 15 m/s skid | -0.20/+0.23, -0.19/+0.29 | -0.01/+0.01, -0.03/+0.02 | 1.2 -> 0 s | -0.18/+0.16 | -0.01/+0.01 |

- **03:00 update:** the table is the build with the roll-post / fan caps. They are now off (section 3: they cost
  roll control in flight). Without them, your jet: 30 kt stop -0.14/+0.09 m, combined stop -0.18/+0.22, yaw skid
  -0.14/+0.08, pedal turn -0.19/+0.09, bank reversals -0.12/+0.07, hover -0.05/+0.05, skid ±0.01; longest VS excursion
  0.8 s. Stock: worst 0.15 m.
- **Against the plan's pass line (peak <= 0.2 m):** six of seven pass on your jet; the combined stop is +0.22 m. All
  seven pass at stock weight (worst 0.10 m).
- **Recovery:** on your jet every case is back inside 0.05 m before the translation itself has finished; at stock
  weight the 30 kt stop takes 1.1 s after it.
- **Vertical speed:** still crosses ±0.2 m/s in five cases on your jet, 0.2-0.7 s at a time, peaks 0.2-0.4 m/s.

### What it costs (your jet, 0.2.7 -> 0.2.8)

| | 0.2.7 | 0.2.8 |
|---|---|---|
| 0 -> 27 kt / 30 -> 1 kt | 4.3 / 5.6 s | 4.3 / 5.5 s |
| combined side + forward stop, under 1 m/s | 6.1 s | 7.0 s |
| 15 m/s skid stop | 4.7 s | 4.8 s |
| yaw skid, side speed under 1 m/s | 6.3 s | 6.9 s |
| pedal turn at 30 kt, side speed under 1 m/s | 3.0 s | 4.1 s |
| stick bank at ~40 kt: bank reached, peak roll rate | ±44 deg (overshoot), 48 deg/s | ±30 deg, 35 deg/s |
| full-throttle short take-off, speed / height after 34 s | 132 kt / 360 m | 125 kt / 355 m |

### Curve check

- **Bank command:** no jump on release; the roll through level no longer overshoots (0.2.7: 15 deg past its target).
- **Nozzle:** target reversals 4 -> 2 (hover), 5 -> 2 (bank case); nozzle reversals 9 -> 5 (hover).
- **Throttle command is busier.** Swings of 0.1 or more: 13 -> 33 in the hover case, 9 -> 24 in the yaw skid; travel
  0.08 -> 0.25 per second. That is the price of the faster lead. The RPM itself reverses as often as before (15 / 15 in
  the hover case). Whether you hear it in the engine sound is something only a flight shows.
- **Climb / sink stops:** as 0.2.7 (2.6-3.4 s to under 0.1 m/s from a full-stick climb, no reversal).

### Regression (stovlall)

- Touchdowns -5.0..-5.8 m/s, gear at most 0.409 m (0.2.7: 0.425; breaks at 0.5).
- VTO release: no dip. Hover pedal / crab at four weights: height within ±0.05 m (0.2.7: ±0.42), yaw response unchanged.
- Mission run: bank at most 12 deg, pitch 3-21 deg, reconverts.
- Not rerun: backwards 30 kt, MPO / freestyle, CTOL checks (none of that code changed).

## 2. From the 01:22 log

- **Altitude rise in the decel after switching STOVL on.** The pitch stick read 0.13 aft from 180.8 s to 189.0 s, while
  the airbrake key was down. At speed the stick is flight-path rate, so that became a 10 deg climb, which the released
  stick then held to the hover: about +230 m. Above 40 kt ground speed the real jet's Unified law does the same
  (flight-path rate, path held on release, blending to height rate by 30 kt). **Law left as it is**, as you chose.
  Note: a conversion made while climbing or sinking also keeps that path through the decel (bench: +1.4 deg at the
  conversion, +51 m by the hover).
- **Airbrake in the decel.** It opened only on the deceleration the propulsion couldn't give, and the aft detent asked
  for 2.3 of 4 m/s^2, which idle thrust and drag nearly gave: 4-25 % open in the log. Now:
  - the deceleration command reaches `StovlDecelMax` (4 m/s^2) at the aft detent (0 GS) and stays there behind it;
  - the airbrake opens by the share of the deceleration the lever asks, fully at the aft detent, fading out 30 -> 15 m/s
    as before.
  - Bench, conversion at 398 km/h, lever in the aft detent: 200 km/h after 16.5 s (0.2.7: 26.3), hover after 42.5 s
    (54.4), mean deceleration above 150 km/h 3.2 m/s^2 (2.1). The bench has less drag than the game showed, so its
    0.2.7 airbrake was already mostly open; the 4-25 % is from your log.
- **Throttle after switching STOVL off.** With the lever in or behind the aft detent when STOVL is switched off, the
  lever is set to 0 % once (keyboard / button throttles; an analog axis can't be moved). On the ground and in the air.
  Player.log: "throttle lever set to 0 % as STOVL was switched off". Not run in game. The Detents mod's own scaling
  is untouched.

- **Wheel brakes through a conversion on the ground (added 02:25).** The wheel brakes are held while the conversion
  runs, and after it for as long as the lever is in or behind the 0 GS detent and nothing asks to lift or roll. Only
  under 5 m/s, so a conversion during a ground roll isn't braked; not in MPO. Your own brake input is untouched (the
  airbrake doesn't open from it).
  - Bench, with a stand-in for the game hook: forward roll before the lift-off pull 26.6 m -> 1.05 m (0.9 m of that is
    before the conversion starts), lift-off time unchanged. The hook itself (`Hooks.GearPrefix`) is compiled only, and
    the game's brake strength is not the bench's.

## 3. STOVL release guard (`ReleaseGuard.cs`, new)

**Compiled only**, except where a log or the bench is named. Revised 02:45 after the first flight (02:21 log).

- **Hook:** a prefix on `WeaponManager.Fire` (the game calls it every frame the Fire button is down). Jettison doesn't go
  through it.
- **On:** STOVL laws flying your jet (not MPO), in the air, at any speed.
- **Checked:** the selected station's next store (one per target for a salvo); guns, weapons with no fire interval,
  slings and troops are never blocked.
- **Safe test (03:00):** the jet's mass and CG less the store at where it hangs -> sideways CG offset from the lift
  line -> roll moment to trim. Roll authority = the posts at the thrust a hover at the new weight takes, plus what the
  ailerons add at the airspeed. Two limits, both must pass:
  - **at once:** the trim right after the release <= 90 % of the authority (past that the posts can't stop the roll);
  - **settled:** the trim left once the fuel balancing has moved what it can <= 75 %. With full tanks nothing can move.
- **Blocked:** `UNSAFE WPN RELEASE - HOLD 1.5s` on the HUD; let go, then hold 1.5 s. **One store drops.** The same press
  drops nothing more for 0.7 s (so 2.2 s of holding for the next one); a new press drops at once. The store on the other
  side restores the balance, so it passes the check and gives no warning.
- Re-arms on weapon cycle, 10 s without a shot, STOVL or MPO toggles, or a shot needing more trim than was approved.
- **Settings** (main config, "1. General"): `ReleaseGuard` (on), `ReleaseGuardHold` 1.5 s, `ReleaseGuardCooldown` 10 s.

### What the 02:21 flight showed

- **The limit was far too low.** It blocked one Tusko-B (650 kg, inner pylon 2.57 m out): 16.1 kN m to trim against
  6.6 allowed, which was 25 % of the posts' 26.4 kN m. That drop is 61 % of the posts; you report the jet holds it.
  One 500 kg store off an outer pylon (4.07 m out) is 20.0 kN m, 76 % or more; you report it rolls the jet over.
  - The 26.4 kN m was the thrust of that moment, not the hover's: see the 02:38 / 02:39 flights below.
- **Both Tuskos came off because the guard switched itself off, not because of the override.** The log has the block
  but no override line. You were accelerating; at ~35 kt the jet left the hover regime, where the guard was built to
  stop, and the held trigger released both 0.25 s apart at 43 kt. The 500s went the same way at 64 kt. That is why the
  guard now works at any speed: the ailerons' authority is in the limit, so it relaxes by itself as speed builds.
- **One store per override** was not in the first build either: once overridden, the next store went as soon as the
  game's fire interval allowed.

### A fault of mine in the first 0.2.8, fixed

The governor's cap on the roll posts was taken from zero, so it also capped the posts' *standing* trim. Bench, lopsided
hover at 13.8 t (buffed), store on the outer wing, with pedal and crab inputs:

| store | to trim | 0.2.7 | first 0.2.8 | now |
|---|---|---|---|---|
| 650 kg | 63 % of the posts | holds | holds | holds |
| 800 kg | 76 % | not run | **flips** | holds |
| 900 kg | 84 % | holds | flips | holds |
| 1000 kg | 93 % | no flip, barely lifts | flips | holds |

That fix (cap taken around the slow part of the inputs) was not enough: see the next section. **The caps are now off.**

### What the 02:38 (full fuel) and 02:39 (half fuel) flights showed

- **02:38, full fuel, one Tusko-B released in the hover, not blocked: 49 deg of bank, 34 m/s sideways, -9.6 m/s.**
  The cause was my cap on the roll posts, not the load. The throttle was at full dry at the release, so the cap sat at
  its floor: posts at 0 for 0.2 s, about 20 % for 0.4 s, then trim + 20 %, creeping to full over 3 s while the jet rolled
  at 11-14 deg/s. Once the posts did reach full the roll rate fell at 7 deg/s^2: the posts can hold this load.
  - From that roll-rate change: the posts' authority there is about 20.6 kN m and the Tusko's trim 16.3 kN m, 79 %.
- **02:39, half fuel: blocked, overridden, 15 deg of bank and 7.6 m/s of skid, level again 9 s later.** Same cap in the
  first 1.5 s (posts at 35-62 %). The fuel balancing then took the trim down: posts settled at about 40 %.
- **Why one flight blocked and the other didn't:** the authority was read at the thrust of the moment (26.4 kN m at
  02:21, 19.7 kN m here). It is now the hover's thrust at the new weight.
- **With the new test:** one Tusko-B off an inner pylon is 79 % at once (passes 90 %). At full fuel it stays 79 %
  (blocked); at half fuel about 10 % is left (allowed). One GBM-500LR off an outer pylon at ~11.4 t is about 118 %
  at once (blocked).
- **Not shown yet:** a release with the caps off. Bench, Tusko-size imbalance dropped in the hover at 14.5 t, thrust
  at its limit: posts full after 0.15 s, bank peak 0.2 deg. The bench's jet has more thrust margin than yours showed,
  so expect more than that, but not what the cap did.

### Conversion guard

Holding Flight Assist to switch STOVL on with the jet already past the limit does not convert:
`UNSAFE STOVL - DROP LEFT WPN` / `DROP RIGHT WPN` (the heavy side). A second hold within 10 s converts anyway. In the air
and on the wheels.

- The posts are idle with STOVL off, so their hover authority is estimated: posts at the thrust a hover at this weight
  takes. Bench: within 1 % of the live value in the hover.
- Limit 75 % of that. With your 14.5 t loadout it is about 16 kN m: one Tusko missing (16.3 kN m) is just over it
  until the fuel has balanced, one outer 500 missing does not pass.
- The heavy side comes from the CG, so fuel or a missing part can trip it too, and then dropping a weapon is not the
  cure. Player.log has the numbers for each block.

### Differences from the spec

- Roll only: the fan balances the nozzle's pitch moment about the CG whatever the CG is.
- The warning uses the FCS's usual 4 s HUD line, not 2 s.
- Limits and speed range as above (the spec had the 25 % left-over share and the hover regime only).

## Files

- Changed: `Stovl.cs`, `ThrottleDetent.cs`, `Plugin.cs`, `FcsController.cs` (two accessors), `FcsStandalonePlugin.cs`
  (version). New: `ReleaseGuard.cs`.
- Bench: `bench/sim/Program.cs` (STOVL statics settable), `bench/scripts/vortex/`: `vsset.sh`, `vsan.py`, `vsum.py`,
  `vrun.sh`, `times.py`, `curve.py`, `regr.sh`, `decel.sh`, `show.py`.
- Native core: unchanged (the allocator isn't touched).
