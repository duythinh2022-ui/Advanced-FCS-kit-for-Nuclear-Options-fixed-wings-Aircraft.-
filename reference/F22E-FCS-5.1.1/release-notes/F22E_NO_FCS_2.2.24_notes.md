# F-22E FCS 2.2.24: rolling pulls use the sideslip margin

Base: 2.2.23. Log: 20 Oct 00:58 (nozzles on). Bench: your live Dev cfg with `DisableTVC = false`, as flown in the log.

## What the log shows
In the six rolling pulls, the governor held the AoA rise, but almost never because of the sideslip:

| t (s) | IAS (kt) | AoA | Worst sideslip | Time held by the feed-forward | Held by the sideslip margin |
|---|---|---|---|---|---|
| 28-34 | 92-111 | 3 -> 60 | 4.4 | 99 % | 0 % |
| 51-53 | 130-154 | 25 -> 60 | 4.8 | 66 % | 0 % |
| 58-59 | 107-133 | 35 -> 44 | 3.2 | 86 % | 0 % |
| 72-78 | 164-234 | 3 -> 60 | 5.0 | 79 % | 0 % |
| 111-118 | 102-125 | 0 -> 60 | 3.4 | 100 % | 0 % |
| 169-174 | 182-261 | 2 -> 60 | 5.6 | 59 % | 9 % |

- **The binding limit is the governor's feed-forward:** the AoA rise allowed is a fixed share (`SweepPosK` 0.25 x margin / 6) of what the measured yaw authority can coordinate at the roll in force.
- At 100-125 kt, rolling at 30-40 deg/s, that comes to about 10 deg/s of AoA rise (`sweepLim` 9-12, `slipGovPull` 1 throughout). The sideslip it left was 1-4 deg of your 9.
- **The margin taper (the part that is meant to hold 9 deg) never got to act.** Raising the margin only scaled the feed-forward by 9/6.

## Change: adaptive feed-forward (`PullFFBoostMax`, Dev, 3. Lateral-directional, default 4; 1 = 2.2.23)
**Growing the boost:** during a rolling pull, the feed-forward is scaled up, to at most 4x, at up to 6x per second, while all three of these hold:
- the sideslip, with its lead, is under half the margin;
- it isn't already running toward the margin (kinematic sideslip rate under 2 deg/s on the side the AoA rise drives it);
- the stick asks for more AoA rise than the feed-forward allows.

**Backing it off:** it is scaled back at 12x per second once the sideslip passes 70 % of the margin, and returns to 1 when the governor isn't acting.

**Unchanged:** the taper that holds the margin. The sideslip now has room to rise toward 9, and the taper stops it there.

**Two details:**
- **The roll trade is still judged on the un-boosted rate,** so the roll slows as in 2.2.23 and the extra goes to the AoA. Judged on the boosted rate, the roll stayed fast and the sideslip then held the AoA back: at 157 kt, time to 30 deg went 2.4 -> 3.4 s.
- **Off with a fin lost.** There the sideslip comes from the roll itself.

**Tried and not kept:**
- **No sideslip-rate gate, or a 3-6 deg/s gate:** full aft + full roll together at 140 kt (the 51 s pull) went to 10.8 deg. The boost let the pitch rate build early, and the taper couldn't stop it in time.
- **A 1 deg/s gate:** slower AoA build, and no better on sideslip.

## Bench
**Replay of your 28 s pull (92-111 kt):**

| | 2.2.23 | 2.2.24 |
|---|---|---|
| Time to 30 deg AoA | 2.1 s | 1.7 s |
| Mean pitch rate, first 2 s | 18.8 deg/s | 23.2 deg/s |
| Worst sideslip | 4.4 deg | 6.1 deg |

**Full roll established, then full aft** (108 / 157 / 205 / 248 / 324 kt):

| | 2.2.23 | 2.2.24 |
|---|---|---|
| Time to 30 deg AoA | 2.4 / 2.4 / 3.5 / 3.5 / 3.6 s | 1.9 / 1.9 / 3.0 / 2.8 / 3.6 s |
| Worst sideslip | 2.0 / 2.9 / 5.5 / 5.4 / 5.6 | 2.1 / 3.4 / 7.8 / 6.0 / 4.5 |

**Jittered replays** (your 28 / 51 / 72 / 111 s pulls; +-12 km/h entry speed; throttle 1.0 / 0.9; 40 runs):

| | Sideslip while the AoA rises, worst | Time to 30 deg, mean |
|---|---|---|
| 2.2.23 | 7.3 deg | 1.87 s |
| 2.2.24 | 6.8 deg | 1.74 s |

- **No case exceeds 9 deg while the AoA rises.**
- **At the 65 deg limit, once the roll is held there,** the sideslip reaches 10.1 (2.2.23) and 10.6 deg (2.2.24) in the 0.9-throttle cases. That is the yaw coordination at the AoA limit, where the governor doesn't act. The jet now gets there sooner. In your log it stayed at 3.4-5 deg there.
- **Half-stick pulls and pulls from wings level:** unchanged.
- **Suite** (nozzles on and off), **hitch set** and **fins-gone rolling pulls:** identical.

## What to expect in game
- **The bench yaw follows less well than your jet does.** For the same feed-forward the bench builds more sideslip, so the boost acts less on the bench than it will in game.
- Your log's pulls held 1-4 deg, so expect more of the 9 deg used, and a faster AoA build, than the bench numbers above.
- **What to check:** a new `pullFFBoost` column at the end of the CSV (before the surface columns): 1 = off, up to 4. If the sideslip still sits low while `sweepLim` binds and `pullFFBoost` is at 4, `PullFFBoostMax` can go to 8.

## Not verified
- Not flown. Bench only.
