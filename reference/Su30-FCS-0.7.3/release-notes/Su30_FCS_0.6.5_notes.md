# Su-30VXN FCS 0.6.5 — engine lost above the no-TVC limit: recovery thrust

Base: 0.6.4. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Engine lost above the no-TVC limit: keep only as much of the live engine's thrust as the yaw allows, push the nose down, then zero

Tom, 17 Nov 13:11: "reduce thrust enough that the thing wont spin out, maybe some yaw, but still enough to shove the nose down".

Before this, a TVC jet that lost an engine while holding more AoA than the airframe can hold without thrust had its live engine taken
to zero thrust at once. Its nose-down authority went with it:
- Flanker: held at 87 deg (stick held), 30+ deg over its 50 deg no-TVC limit.
- Ifrit: tail-slid to 166-180 deg.

**What it does now**
- When an engine is lost with the AoA already past the zero-thrust match point (35 deg), the FCS arms an engine-out recovery.
- While the AoA is more than 2 deg above the no-TVC limit (the limit the zero match leaves), the live engine keeps as much thrust as
  keeps its yaw at `EngineOutRecoverUse` (0.7) of the fins' yaw authority:
  - the thrust yaw is measured from the nozzles' actual thrust;
  - the fins' authority is taken at the dynamic pressure the deceleration is heading to.
- The nozzles push the nose down to the limit. The normal AoA protection pushes; the soft-limit hold does not engage, since the AoA
  never passes `AoALimitPositive` + 5.
- Once the AoA is back under the limit (after at least 2 s), the match goes to zero as before.
- Never in MPO (no limit to recover to), never on a jet without pitch nozzles (Ternion: inert), and dropped at once if the yaw starts to
  saturate (filtered yaw saturation over 0.5).
- A pull begun after the engine loss is unchanged: there is no recovery for it, and the limit is the no-TVC one from the start.
- Settings (bench statics, `FcsController`): `EngineOutRecoverThrust` (on), `EngineOutRecoverUse` 0.7, `EngineOutRecoverBand` 2 deg,
  `EngineOutRecoverGain` 5 /s (the match's integral gain per unit of yaw usage).

## Bench (0.6.4 vs 0.6.5)
Left engine killed 3 s into a full-aft pull, at the 80 deg limit, 1500 m, full throttle.

| case | 0.6.4 | 0.6.5 |
|---|---|---|
| from 350 km/h, stick held | AoA held at 86-90 (soft-limit hold) | 80 -> 50 by 6 s (pitch rate -13 to -14 deg/s, smooth), then 47-52 to 11 s; sideslip 2.3 max, yaw rate 3-5 deg/s |
| from 350 km/h, stick released at 5 s | 86 at 6 s | 48 at 6 s, sideslip 1.0 |
| from 500 km/h, stick held | 80 -> 39 by 6 s (zero thrust), sideslip 6.5 | 80 -> 50.5 by 6 s, then 49.7-51.4; sideslip 0.5 |

- The live engine stays at full thrust through most of the recovery: its yaw use is under 0.7 of the fins' authority here.
- When the thrust goes at 50 deg, the AoA comes back up by 2 deg (49.6 -> 52.0) as the nozzles' nose-down goes, and is back at 47-50 after.
- Every other case (pulls begun with the engine already out, half stick, roll reversals, MPO) is identical to 0.6.4.

## Install
- `BepInEx\plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.6.5; 0.6.4 kept as `Su30_VXN_FCS.dll.0.6.4.bak`. `meta.json` updated.
