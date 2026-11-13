# F-22E FCS 2.4.8

Base: 2.4.7. One fix: a regression 2.4.7 introduced in corkscrew releases at idle. Everything else is 2.4.7.

Not flown. Bench only.

## The regression
Found while checking whether the 2.4.7 fix would help the Ifrit, by running idle releases that 2.4.7 had not been tested on.

Bench, full aft + full roll at idle (AoA held at the 45° idle cap), then both released:

| Start IAS | 2.4.6 | 2.4.7 | 2.4.8 |
|---|---|---|---|
| 300 km/h | q min −2.09 | −2.14 | −2.09 |
| 380 km/h | q min −0.86 | **−2.02** (0.40 s below −1) | −0.86 |
| 450 km/h | q min −0.25, median −0.11 | −0.27, median **+1.07** (nose held climbing ~1 deg/s for 2 s) | −0.25, median −0.11 |

**Why:**
- At idle the nozzles make almost no moment per degree; the stabs carry the pitch.
- `AllocNozzleNewton` had the nozzles chase the hold with that tiny authority. The 4° grid had been the only thing holding them still. This caused the +1 deg/s at 450 km/h.
- `HybridIndiHighAoALead` is sized for the nozzles' response. At idle it only shuffled the stabs, and on top of the Newton step it gave the −2.0 at 380 km/h.

## Fix
- **Newton step:** only with T/W ≥ 0.25 (`NewtonMinTw`).
- **Disturbance-rate lead:** fades out with T/W from 0.4 to 0.2 (`DistLeadTw0` / `DistLeadTw1`).
- **At idle:** both are now exactly 2.4.6.
- **Unchanged:**
  - **Full-thrust results:** identical to 2.4.7. All nine corkscrew cases match to the hundredth: q min −0.25…−0.75, and −1.76 at 450 km/h.
  - **Regression suite S1–S7:** identical to 2.4.6 within rounding.

Part throttle, start IAS 300 / 380 / 450 km/h, 2.4.6 → 2.4.8 (q min):

| Throttle | 300 km/h | 380 km/h | 450 km/h |
|---|---|---|---|
| 0.5 | −0.83 → −0.69 | −0.93 → −0.36 | −1.08 → −0.33 |
| 0.3 | −0.82 → −0.93 | −0.47 → −0.51 | −0.20 → −0.13 |

## Still open
- **Idle releases at 300 km/h** dip to about −2.1 in 2.4.6 as well. With the nozzles idle, the stabs on their stall plateau can't hold the zero-rate as the AoA unwinds. This was not changed.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 41589ee213d8f7fe4066a17c0a2c6c29) and `meta.json` 2.4.8.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (6060deb6e17f0e1efcca69efde4da9ad).
- **Backup:** 2.4.7 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.7.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.8-release`.
