# F-22E FCS 2.4.13

Base: **2.4.12**. One change, to the lateral law with the gear down in the air. Same change as Ifrit 0.5.3.

Compiled only. Not flown; the bench was not run (see Verification).

## The sideslip spike (27 Oct 12:12 log, 133–139 s)
Conditions: gear down, idle (T/W 0.07), 98 → 82 m/s IAS.
- **133.5 s:** full right roll. Roll rate up to 60 deg/s, AoA 17 → 28°.
- **135.1 s:** full push. The AoA went +28° → −34° in 2 s while the roll ran at 50–85 deg/s.
- **The yaw couldn't keep up.** The coordinating yaw rate the FCS asked for reached −58 deg/s, yaw saturation hit 1.0, and the sideslip went to **−55.6°** at 138.1 s.

The cause is a mode gap, not a tuning issue. The two negative-AoA push + roll limiters from 2.1.0 are both off in gear-down mode:
- the **sideslip-rate lead**, which backs the roll off when the predicted sideslip grows;
- the **yaw-anchored roll**, which allows only the roll the yaw axis can follow from its current rate.

2.1.0 shipped them as "not active for AI, on the ground, or with the gear down". The gear-down law also runs on the runway, during rotation and the touchdown, which is presumably why.

## Change
- **In the air:** both limiters now act in gear-down mode while no wheel touches the ground.
- **On the runway:** any wheel in contact leaves the gear-down law as it was.
- **Positive AoA:** unchanged. The limiters only act at negative AoA (the `negGate` fades from 0 to −3°). On an approach, the flare and the rotation, AoA is positive, so they never engage there.
- **Switch:** dev key **`GearDownSlipProtect`** (Ground, `true`). `false` = 2.4.12.

## Verification
- **Offline, from the logged values:** using the logged yaw rate, AoA and modelled yaw authority, the yaw anchor would have limited the roll target to **10 deg/s from 136.3 s** (AoA −5.7°, sideslip −5°). 2.4.12 commanded 87–93 deg/s there, and its guard only brought the target under 15 deg/s at 136.9 s, by which point the sideslip was already −19°.
- **Not proven:** that this stops the departure in this case. The roll inertia at 136.3 s, about 51 deg/s, still has to be braked.
- **Earlier bench, gear-up law (2.1.0):** the same limiters took full push + full lateral from 37 / 49 / 27° of sideslip down to 3–6° at 150–650 km/h.

## Steering-reversal question (27 Oct 12:31 log), no change
At 26–30 kt, full-pedal reversals:
- **FCS:** commands the full opposite wheel within 0–0.02 s. The rudders go to full opposite at the same moment.
- **Nose leg:** slews ±45° at Aryx's 60 deg/s. It passes centre at 0.5–0.7 s and reaches the far stop at 1.25–1.45 s.
- **Yaw rate:** crosses zero at 0.8–1.0 s and reaches 63 % of the new turn at 1.7–1.9 s.

The ground bench reproduces this (0.92 / 1.82 s at 27 kt). What limits it is the nose tyre's grip against the jet's yaw inertia, not the slew:
- **Slew 120 / 180 deg/s:** zero crossing only 0.06 / 0.10 s sooner.
- **Fixing Aryx's differential-brake data** (both mains −1, so right pedal never brakes): +17 % steady turn rate, no faster reversal.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 a5212923c72c3e88b09585f6d61058bc) and `meta.json` 2.4.13.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (ecfb1aea6e5199a87586e5ec08142259).
- **Backup:** 2.4.12 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.12.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.13-release`.
