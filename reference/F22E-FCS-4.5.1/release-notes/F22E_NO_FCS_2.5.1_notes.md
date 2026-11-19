# F-22E FCS 2.5.1

Base: **2.5.0** (read its notes for the governor itself). One change: the roll-rate governor replaces the older sideslip limiters, in three selectable steps. Bench only, not flown.

## `RollGovernorReplaces` (new, dev, default 1)
Each level includes the lower ones. Takes effect at once.

- **0** — nothing replaced; 2.5.0 as shipped.
- **1 (default)** — the **rolling-pull sideslip governor is off**: no AoA-rise hold at `PullSideslipMargin`, no feedback roll trade from it. The pitch is never held for sideslip. **`PullRollTrade`'s feed-forward part stays and is the rolling-pull protection on its own:** the roll is held to what the yaw can coordinate while the AoA rises at the rate the stick asks (`PullTradeFFK × SweepPosK × aYaw cos AoA / AoA-rate asked`, scaled by `PullSideslipMargin / 6`, floor `PullTradeFloor` 30), at every IAS — its 180-230 kt fade-in is dropped because the hold's own reaction no longer covers low IAS — and it still lets the roll back only slowly while the stick keeps asking.
- **2** — also the **push + roll limiters** off at negative AoA: the sideslip-rate lead and yaw anchor (2.1.0-2.1.2) and the AoA-sweep yaw limit (2.1.8). Full push + lateral then relies on the governor and the plain sideslip guard. Their use for asymmetric loads at positive AoA stays.
- **3** — also the **trajectory's yaw-coordination caps** off: coordinated roll jerk (2.0.6), the entry / brake / reversal accelerations capped by the yaw authority (`RollEntryCoord`, `BrakeYawCap`, `RevYawCap`), `CoordinatedStop`. Roll entries and brakes run at the wing's full authority. **The bench says this is worse everywhere** (below); it is there because you asked for all three.

Not replaced at any level: the high-AoA braking floors and yaw-rate limits (they are the authority limit itself, not a sideslip fix, and your 280 km/h pedal-turn reference lives there), `CouplingRollLimit` (inertial pitch-up, not sideslip), the asymmetric-load limiters, no-chase, the yaw lead.

## Bench (your cfg, Aryx 1.0.4 airframe, 200 m, full throttle)
Peak sideslip / peak velocity-vector roll rate / AoA at 2 s. The bench overstates sideslip at low IAS (its yaw is weaker than the game's), so read the differences, not the levels.

| Case | km/h | L0 (2.5.0) | **L1** | L2 | L3 |
|---|---|---|---|---|---|
| Pull, then full roll | 250 | 6.0 / 47 / 37 | 7.8 / 49 / **59** | same as L1 | 7.4 / 64 / 60 |
| | 320 | 4.7 / 61 / 33 | 8.6 / 62 / 62 | same | 7.6 / 76 / 61 |
| | 400 | 3.7 / 77 / 48 | 6.7 / 78 / 60 | same | 5.3 / 87 / 60 |
| | 600 | 4.1 / 107 / 26 | 4.8 / 110 / 28 | same | 4.8 / 109 / 28 |
| Full aft + full roll from level | 250 | 2.7 / 96 / 14 | 5.4 / 93 / 28 | same | 3.1 / 113 / 26 |
| | 400 | 2.5 / 204 / 10 | 2.9 / 204 / 24 | same | 2.5 / 204 / 24 |
| | 600 | 3.9 / 250 / 12 | 4.3 / 250 / 16 | same | 4.1 / 250 / 17 |
| ... with a reversal at 3 s | 600 | 5.2 | 7.3 | same | **16.4** |
| Held 0.4 pull + full roll | 350 | 1.6 / 100 | same | same | 8.7 / 115 |
| Full push + full roll | 250 | 0.8 / 46 | same | same | 7.2 / 45 |
| Push reversal | 450 | 4.9 / 130 | same | 5.9 / 149 | 10.2 / 146 |
| Stop, 565 km/h, 21 deg AoA | 565 | 3.0 / 90 | same | same | 6.4 / 90 |
| Reversal, 565 / 650 km/h | | 9.4 / 4.7 | 9.4 / 5.1 | same | 14.8 / 9.3 |

What L1 does: in a rolling pull the AoA now rises at the pitch law's own rate (AoA at 2 s 33-48 → 54-62 deg at 250-500 km/h) and the roll rate is what the FF trade allows (unchanged); the sideslip that the hold was buying runs 2-4 deg higher on the bench. The 1 g rolls, plain pulls, stops and reversals are identical to 2.5.0.

## Not verified
- Not flown. L1 removes a protection you asked for in 2.2.4 (slowing the AoA rise mid-roll); the trade and the governor now carry that case alone. The low-IAS rolling pull (250 km/h rows) is where to look first.
- L3 is bench-worse in every case that brakes or reverses a roll.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.5.1. Backup: 2.5.0 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.5.0.bak`. Release: `C:\Games\NO mods\F22E-FCS-2.5.1-release`.
- Dev DLL unchanged. The new key appears in the dev cfg at its default (1) on first launch; no migration.
- LogOutput: `F-22E FCS 2.5.1`.
