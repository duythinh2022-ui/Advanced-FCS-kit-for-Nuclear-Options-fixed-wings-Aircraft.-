# FS-12 Revoker FCS 1.7.1: Typhoon inner flaperons follow the inner flaps

Base: 1.7.0. Visual only. The flight code is unchanged (bench replay of a 24 Nov 12:32 log window: 396 log columns identical to 1.7.0).

**Status:** compiled. The binding fix is read from the game's log, not seen in the game: **not flown.**

Tom (24 Nov 12:38): "with the typhoon skin mod, the iinner trailing flaps are always moving at the same direction with the outer pair, even if the inner pair stall first or with on air speed brakes."

## Cause
The Typhoon mod lists two Revoker surfaces for each inner flaperon panel (`aileron_X_visible+flap_X_visible`) and the aileron alone for each outer one. The FCS's panel poser (1.6.4) picks one surface per panel, "the one under it". `LogOutput.log` of the 12:32 flight shows what it picked:

```
Typhoon_flaperon_in_L <- aileron_L_visible
Typhoon_flaperon_out_L <- aileron_L_visible
```

Both panels on a side were bound to the outer aileron, so the inner panel copied the outer pair whatever the inner flaps did. The inner flap's mesh sits on a child object, so its span could not be measured and it was scored by distance instead; the aileron, scored by span overlap, won on a different scale.

## Change (`ModelSwapSurfaces.cs`, `HingeFlapEffector.cs`)
- A surface whose mesh sits on a child is measured through its children, so every candidate is scored by span overlap.
- One surface per panel: where two panels took the same surface and one of them lists another surface that no panel took, that panel moves to it. With the Typhoon mod's list this puts the inner panel on the inner flap whatever the geometry test says.
- The inner flap's rest pose comes from the FCS's own flap effector (1.6.4 assumed its modelled pose).
- Nothing in the Typhoon mod's files is changed, and no transform the physics reads is touched.

## To check in the game
`LogOutput.log` after a spawn should read `Typhoon_flaperon_in_L <- flap_L_visible` and `Typhoon_flaperon_in_R <- flap_R_visible`. The inner panels should then split from the outer pair with the airbrake out, with flaps down, and past the inner pair's stall.

## Flight check of 1.7.0 (24 Nov 12:32 log, TVC off, caps 35 and 45)
`LogOutput.log`: `Loading [FS-12 Revoker FCS 1.7.0]`; the log has `ceRevP`. First flight of 1.7.0. Compared with the 12:06 flight on 1.6.9:

| | 1.6.9 (12:06) | 1.7.0 (12:32) |
|---|---|---|
| Rate cap, tick-to-tick jump in held rolls (deg/s) | 0.40-1.20 | 0.11-0.47 |
| Slow held roll at the cap (257-320 km/h): mean surface travel (deg/s) | 67-68 | 30-46 |
| Same: share of samples at the servo rate | 49 % | 10-23 % |
| Same: aileron travel (deg/s) | 54-68 | 16-34 |
| Same: canard travel (deg/s) | 73-83 | 39-60 |
| Held roll at 424-449 km/h (cap 35; 1.6.9: cap 30 at 425): aileron travel | 55 | 23-36 |
| Same: canard travel | 27-32 | 29-49 |

What is left, and why it still looks like tweaking:
- **Canards crossing their stall.** In the slow rolls at the 45 cap the canards' local AoA runs 10-57 deg; at each roll entry one canard goes to its -45 stop (local AoA 89 deg) for about a second. Symmetric canard swing 10-17 deg (std) at 0.4-1.1 Hz. Rudder swing 13-17 deg.
- **AoA dips at roll entry are unchanged** (1.7.0 was not built for them): 40.2 -> 28.1 deg at 43.2 s (cap 35, 504 km/h), 30.8 -> 22.6 at 63.7 s. At the 45 cap below 320 km/h the dips are 0.5-2.5 deg.
- Dip mechanism, from the logs and two bench replays that reproduce it (12:06 log 137-140 s: game 34.1 -> 23.3, bench 32.8 -> 20.1; 12:32 log 42.6-47 s: game 40.8 -> 28.2, bench 41.9 -> 31.3): with the roll stick in, the nose-up acceleration the pitch law may plan with (`aUpPitch`) falls from 90-180 to 35-45 deg/s^2, because the elevons and canards go to the roll. The pitch-rate trajectory then climbs back at about 35-40 deg/s^2 while holding the AoA in the rolling turn needs +17 to +20 deg/s, so AoA runs 6-12 deg under the cap before pitch catches up. The big dips happen when the roll goes in while the pull is being arrested at the cap (pitch rate just driven to zero).
- A third dip (12:32 log 63.0-63.8 s) starts on a roll **release** and is not reproduced by the bench (game 35.1 -> 27.1 with the canards split -20 / +53; bench holds 34.6).

Open: the canard stall crossings and the AoA dip, both TVC off.
