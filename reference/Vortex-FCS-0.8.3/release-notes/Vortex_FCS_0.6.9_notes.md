# Vortex FCS 0.6.9 (21 Nov 2026)

On 0.6.8: the same attested native core, embedded in the plugin DLL. STOVL height hold through abrupt decelerations, pedal turns and side skids, plus braking priority when the thrust can't cover everything. Benched, not flown.

## What Tom asked for
- Smaller v/s and height swings during fore/aft acceleration and deceleration, especially abrupt deceleration with yaw or side skid at the same time (his 01:00 log: ±20–30 ft/min).
- When the thrust isn't enough, braking takes priority.
- Height targets: ±10 cm only in the worst, thrust-limited braking case; ±2 cm with tight grouping everywhere else.
- The learned prediction applied to every axis, "for it to be more robust".

## What the 01:00 log and the bench showed
The 01:00 log was flown at 12.1 t (hover T/W 1.30), lighter than the 13.8 t bench jet. Re-benched at 12.1 t with game-like part jitter, 0.6.8 reproduced the log: about +5.5 cm and ±23 ft/min on abrupt stops. The log itself shows −2.8/+7 cm on a 45→0 kt stop in a pedal turn.

Four causes, all found by comparing the law's inputs against the bench's true forces:
1. **The aero feed-forward pointed the wrong way at the start of a stop.** It predicted the airspeed from the *commanded* deceleration (the full value the instant the lever moves), but the attitude from the *measured* pitch rate. So it saw the speed fall before the nose came up, and predicted a lift loss when the pitch-up was actually adding lift. The result was about +0.1 m/s² of thrust for ~1 s.
2. **The thrust took part of the braking.** With the nozzle at swivel 1 and the vanes at −10° (their limits during a stop), the weighted thrust fit made up the missing brake with more thrust. Every newton of that is as much lift as brake, so the jet rose by 0.1–0.2 m/s² for the whole pitch-up.
3. **The attitude loops' forces lead the engine.** In the game, the lift fan's pitch input and the roll posts are forces of their own. Every pitch or roll acceleration adds or removes lift instantly, and only the RPM can take it back. 0.6.8 already removed them from the thrust request the moment they were commanded, but the thrust still arrived one response time late.
4. **During a stop in a turn, the brake and the turn's bank split the thrust-rate headroom evenly.** When the thrust couldn't follow both, the brake was held back by the bank.

## Changes
1. **Aero feed-forward: airspeed from the measured fore-aft acceleration.** Same kind of measurement as the attitude rate it is paired with. Its predicted lift change now matches the bench's true change through the stop (it was the wrong sign for the first second). Bench key `AmPlanMeasured`.
2. **Thrust sized for the vertical force alone.** The nozzle and vane angles still come from the weighted fit (fore-aft included). The brake that direction can't give is the pitch tilt's job; the tilt works from the asked deceleration, not from the fit's leftover. Where the direction can give both, the two sizings agree. Bench key `VhThrustForVertical`.
3. **Thrust lead on the attitude loops' known forces** (fan pitch input, roll posts):
   - The thrust is moved by their rate of change times the thrust's own response time (RPM lead plus the force delay the push estimator measured), the same lead the aero feed-forward uses.
   - Only the part of the lead above its own noise floor is used: the floor is the lead's lower envelope (up over 5 s, down in 0.5 s), and twice that is taken off.
   - Without that floor, game-like jitter doubled the hover's RPM motion with no gain in height.
   - Bench keys `VhKnownLead`, `VhKnownLeadFloor` (2).
4. **Braking priority** (`StovlBrakePriority`, dev cfg, on). While the lever brakes (asked fore-aft acceleration against the way the jet moves), the stop's pitch tilt and aft vanes get first claim on the thrust-rate headroom. The bank of a pedal turn or a side command gets what is left.
   - Tilts coming out still give their lift back to the other.
   - With enough headroom nothing changes.
   - false = the even split of 0.2.8–0.6.8.

All four off is bit-identical to 0.6.8 (checked on a clean case and a noisy one: every column identical).

## Bench
Tom's jet (Buffed, his 17 Nov settings), game-like part jitter, three noise seeds. Values are peak |height change| in cm, mean [min–max] over the seeds; vs is peak ft/min. "Thrust-limited" means the tilt governor was clamping. This is not a separate case set.

**12.1 t (the 01:00 log's mass), 0.6.8 → 0.6.9:**

| Case | Height (cm) | vs (ft/min) | Notes |
|---|---|---|---|
| 45→20 kt, full pedal | 5.3 [5.2–5.3] → 3.1 [2.8–3.4] | 14 → 13 | turn rate 16.2 → 13.8 °/s while braking (priority) |
| 45→0 kt, full pedal | 5.5 [5.4–5.6] → 2.8 [2.4–3.4] | 14 → 10 | stop 7.3 → 6.8 s |
| 30→0 kt, full side stick | 5.5 [5.1–5.7] → 2.5 [2.4–2.5] | 23 → 9 | stop 6.2 → 6.6 s (to under 1 kt *including* the side skid) |
| 45→0 kt, straight | 5.3 [5.1–5.5] → 3.4 [3.1–3.7] | 18 → 9 | stop 6.6 → 6.5 s |
| Climb released during a stop | 4.1 [2.1–7.8] → 3.4 [2.8–4.5] | 10 → 9 | |
| Climb released, lever 0.3 s later | 4.9 [4.2–5.6] → 3.5 [2.9–3.9] | 10 → 9 | |
| Full side stick at 30 kt | 2.7 → 2.0 | 14 → 8 | |
| Pedal while backing up (−15 kt) | 2.6 → 1.7 | 10 → 6 | |
| Half pedal at 30 kt | 2.0 → 1.2 | 11 → 5 | |
| Hover pedal | 1.2 → 1.2 | 5 → 5 | |

**13.8 t (hover T/W 1.14), 0.6.8 → 0.6.9:**
- Better:
  - 30→0 with side stick: 3.2 → 1.8.
  - Backing-up pedal: 2.7 → 1.8.
  - Half pedal at 30 kt: 2.0 → 1.3.
  - PB: 3.2 → 2.4.
  - S0: 1.6 → 1.2.
  - K45: 2.6 → 2.3.
  - Climb releases in a stop: 2.2/2.4 → 2.1/2.0.
- Same: F0 2.5 → 2.6, K30 2.8 → 2.6, hover yaw 1.2.
- Worse:
  - 45→0 straight: 2.0 → 2.7.
  - 45→0 with pedal: 2.5 → 2.7 (stop 8.4 → 8.2 s).
  - S128 (12.8 t Stock, thrust-limited stop with side stick): 3.0 → 3.8.
- vs peaks are lower or equal in every case but F0, K30 and K45 (+1).

**Thrust-limited stops** (governor clamping; the ±10 cm case), clean, 0.6.9 priority off → on:

| Case | Height (cm) | Stop |
|---|---|---|
| 13.8 t, 45→0 kt, full pedal | ±1.3 → −3.2/+2.0 | 8.92 → 8.18 s |
| 12.8 t Stock, same | −1.8/+1.6 → −1.5/+1.6 | 11.25 → 10.33 s |
| 13.8 t, 30→0 kt, side stick | −1.9/+1.1 → −2.3/+1.0 | 6.78 → 6.57 s |
| 12.8 t Stock, same | −2.7/+1.5 → −3.6/+2.0 | 9.15 → 8.72 s |

**Unchanged** (within 0.03 s / a few mm):
- v/s stick releases: full climb, half/full/short sink. Time under 2 ft/min within 0.03 s; overshoot 0.0–0.2 ft/min.
- Fore-aft stop set: stop times within 0.03 s; the reference stop's height went −2.7/+3.9 → −1.1/+1.7.
- Low-T/W airborne set.
- Stability Assist off set (freestyle has its own law).

**Hover with game-like jitter:**
- RPM motion: 43 → 58 rpm/s.
- Thrust-command motion: 1.39 → 1.82 /s.
- vs rms: 0.87 → 0.76 ft/min.
- Height band: 0.4 cm, unchanged.
- This is the cost of change 3. For scale, 0.6.7's chatter was 7× (10.6 against 1.47 /s).

## Not in 0.6.9
- **Learned model on the lateral and yaw axes** (side force and yaw/roll moments from sideslip). Not built. In every case above, the remaining height errors trace back to the thrust/attitude coupling, not to lateral aero. The side-skid and pedal cases are already at 1.2–2.0 cm at 12.1 t. I didn't want to ship a model I couldn't show helping.
- **The v/s stop predictor on the planned geometry.** Not built. The clean climb-release-in-a-stop case still creeps up ~9 cm over 4 s at +5–8 ft/min after the stop, the same in 0.6.8.

## Still above ±2 cm
- **12.1 t:** straight and pedal stops at 2.5–3.4 cm; climb releases in a stop at 3.4–3.5 cm.
- **13.8 t:** stops at 2.7 cm; F0, K30 and K45 at 2.3–2.6 cm. The 13.8 t stops are governor-clamped, so they count as the thrust-limited case.
- **Main remaining source: the roll posts.** The game fires one post at a time for roll (the other stays at zero). A hard roll reversal (end of a pedal turn, a skid stop) is then a one-sided lift kick of up to ~0.4 m/s² with no warning. Only the RPM can answer it.
- **The only further lever is shaping the roll command** (e.g. ~0.15 s for the thrust to pre-move). That trades roll and skid-stop snap, so it's your call.
- **The bench's part jitter is a stand-in** for the game's. Not flown.
