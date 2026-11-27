# F-22E FCS 2.4.11

Base: **2.4.10**. One change, to ground steering only. Same change as Ifrit 0.5.1.

Compiled and bench only. Not taxied.

## Ground steering: stock while you steer, straight-line correction when you let go
This is the default, with `GroundStockMatch` on.
- **Pedal in:** the nose leg and the rudders get the **raw** pedal. `LandingGear` steers exactly as stock: its own slew to pedal × steering lock (45° at 60 deg/s on both jets) and its own castor. There is no stick smoothing, no rate limit, no feed-forward, and no line is held.
- **Pedal released:** the FCS takes the wheel at once.
  - It brakes the yaw to zero with the yaw-rate loop, proportional only. On the bench the integrator made the yaw swing back 1–2.5 deg/s; without it, the swing-back is under 0.1 deg/s.
  - Once |r| < 0.7 deg/s (or 2 s after release), it captures and holds the straight line along the track. This is the 1.8.36 hold, with the integrator back in.
- **Touchdown:** the FCS holds the approach track, as before (crab / drift correction).
- **Fallback:** `GroundStockMatch` = `false` gives the 2.4.9 capped yaw-rate law.

## Why
- **2.4.10:** it asked for the stock yaw rate through the FBW loop. It turned nearly as tight as stock but responded too slowly in the game.
- **The first 2.4.11 cuts** were not kept:
  - stock wheel plus FBW braking from a copy of the pedal that ignored castor;
  - pure stock until the yaw stopped by itself.

## Bench (yaw-plane model of the game's `LandingGear`, calibrated on the 27 Oct Ifrit taxi log)
- **Pedal in:** yaw rate and rise time are identical to stock at every speed and pedal size from 6 to 68 kt.
- **After a full-pedal turn, time for the yaw to stop (|r| < 1 deg/s):**

| Speed | Stock | 2.4.11 |
|---|---|---|
| 23 kt | 1.20 s | 0.90 s |
| 39 kt | 1.18 s | 0.66 s |
| 49 kt | 1.20 s | 0.56 s |
| 68 kt | 1.22 s | 0.44 s |

- **Full table:** `bench/groundsim/results_2411.txt`, made with `gsfbw4.py`.

## Log check (16 F-22E takeoff rolls, 25–26 Oct, line hold above 29 kt)
- **Yaw rate:** 0.26 deg/s rms, 0.80 at the 99th percentile.
- **Offset from the captured line:** 0.47 m rms, 1.42 m max.
- **Nose wheel:** 0.06° rms.
- **Missing data:** none of the logs has pedal input on the ground, so there is no F-22E tyre data. To get it, set `Telemetry = true` in the dev settings and taxi.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 70429fdd8884cc37ec3b285d1fbfd6ec) and `meta.json` 2.4.11.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (ba512f2e8f0b69b4c93f087d9262983f).
- **Backup:** 2.4.10 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.10-stable.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.11-release`.
