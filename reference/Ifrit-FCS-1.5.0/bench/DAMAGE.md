# Ifrit bench part damage (2 Nov 2026) — bench-only, FCS source unchanged

The F-22E bench's damage model (`sim/Damage.cs`, shared verbatim) on the stock KR-67 Ifrit (prefab Multirole1). Game sources and the full
description: the F-22E bench's `DAMAGE.md` and project doc `claude/Game_damage_model_Raptor.md`.

- Data: `data/ifrit_damage.json`, from the game's resources.assets by `extract/stock_damage.py <prefab> <out.json> <NuclearOption_Data>`
  (type trees generated from the Managed DLLs; needs `pip install UnityPy TypeTreeGeneratorAPI`).
- Engines: two Turbojets, damageThreshold 55; each needs its engine, nozzle, intake and gear bay. Turbojet model: every hit on a critical part sets condition = clamp((hp − threshold)/(100 − threshold), 0, condition);
  a critical part detached kills it. No hit points of its own (`dmg=Eng...` is refused: damage its critical parts).
- Yaw: elevator_L / elevator_R are the ruddervators (yaw ±10° on top of pitch); no rudder.
- Some parts have structuralThreshold −100 / −200 (joints keep (hp − s)/(100 − s) of their strength at 0 HP) and
  integrityThreshold −100 (below it the part fragments: its joints go to zero).

## Keys (ifly mode; times from the profile start)
`dmg=part:hp+...`, `dmgAt=t:part:hp+...,t:...`, `detachAt=t:part+part`, `detach=part+part` (t = 0; now wired into ifly),
`ripOff=1/0`, `dmgCsv=path`. Part names as in the airframe json, case-insensitive. The model runs only when a damage key
is set: with none, `an/sreg.py` reproduces the shipped bench bit for bit (checked).

## Suite: `an/sdmgsuite.py ifrit <tag>` (env BENCH_DIR, BENCH_DATA, AN_OUT)
Yaw-surface loss (leaf stops / reversals at 296 / 370 km/h, push-overs released / held from max AoA at 560 / 600 km/h),
yaw surfaces damaged mid-pull at 900 km/h, roll surface lost (reversal at 890), left wing at 20 HP (reversal at 600), an
engine killed by a critical part shot off at max AoA / 300 km/h — each with its clean twin.

## First results (FCS 0.7.3)
- L intake shot off at 65° / 300 km/h: left engine killed, **departs** (β 43°): asymmetric thrust at max AoA, no rudder.
- Both ruddervators at 5 HP in a 900 km/h rolling pull: both tear off under load (elevator_R 102 kN of 100), it tumbles.
- One ruddervator gone: the FCS caps AoA at ~47°; held push-overs β 16–17°. Both gone (t = 0): it still reaches 65° on TVC.
- Clean jet, push-over with the roll held from 65° at 560 / 600 km/h: β 23–24° — an FCS observation, not damage.
- Clean jet, 9.5 G at 1100 km/h with a roll: worst joint 14.5 % (flap_L); nothing comes off.
