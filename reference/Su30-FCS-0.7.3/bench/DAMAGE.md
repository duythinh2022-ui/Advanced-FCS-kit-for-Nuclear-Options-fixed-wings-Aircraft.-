# Revoker bench part damage (2 Nov 2026) — bench-only, FCS source unchanged

The F-22E bench's damage model (`sim/Damage.cs`, shared verbatim) on the stock FS-12 Revoker (prefab Fighter1). Game sources and the full
description: the F-22E bench's `DAMAGE.md` and project doc `claude/Game_damage_model_Raptor.md`.

- Data: `data/revoker_damage.json`, from the game's resources.assets by `extract/stock_damage.py <prefab> <out.json> <NuclearOption_Data>`
  (type trees generated from the Managed DLLs; needs `pip install UnityPy TypeTreeGeneratorAPI`).
- Engines: one Turbojet, damageThreshold 50; it needs both intakes, the tail, the fuselage and the root. Turbojet model: every hit on a critical part sets condition = clamp((hp − threshold)/(100 − threshold), 0, condition);
  a critical part detached kills it. No hit points of its own (`dmg=Eng...` is refused: damage its critical parts).
- Yaw: rudder between vstab1 and vstab2, hinged to the fuselage as well.
- Some parts have structuralThreshold −100 / −200 (joints keep (hp − s)/(100 − s) of their strength at 0 HP) and
  integrityThreshold −100 (below it the part fragments: its joints go to zero).

## Keys (ifly mode; times from the profile start)
`dmg=part:hp+...`, `dmgAt=t:part:hp+...,t:...`, `detachAt=t:part+part`, `detach=part+part` (t = 0; now wired into ifly),
`ripOff=1/0`, `dmgCsv=path`. Part names as in the airframe json, case-insensitive. The model runs only when a damage key
is set: with none, `an/sreg.py` reproduces the shipped bench bit for bit (checked).

## Suite: `an/sdmgsuite.py revoker <tag>` (env BENCH_DIR, BENCH_DATA, AN_OUT)
Yaw-surface loss (leaf stops / reversals at 296 / 370 km/h, push-overs released / held from max AoA at 560 / 600 km/h),
yaw surfaces damaged mid-pull at 900 km/h, roll surface lost (reversal at 890), left wing at 20 HP (reversal at 600), an
engine killed by a critical part shot off at max AoA / 300 km/h — each with its clean twin.

## First results (FCS 0.4.2)
- Rudder or the whole tail gone: leaf stops fine; leaf reversals β 12° (clean 1.6°); push-overs β ≤ 2°.
- Fins + rudder at 2 HP in a 900 km/h rolling pull: tear off under load with the rudder.
- vstab1 at 0 HP holds at half strength (structural −100); vstab2 at 0 HP loses its own joints but hangs on the rudder,
  which hinges to the fuselage — the game's joint graph; the damage tool's last stage (detachAt) takes it.
- R intake shot off: the engine dies (glide); R aileron gone: reversal clean, roll rate 120 vs 250 deg/s.
- Clean jet, 9.8 G at 1100 km/h with a roll: worst joint 10 % (aileron_L); nothing comes off.
