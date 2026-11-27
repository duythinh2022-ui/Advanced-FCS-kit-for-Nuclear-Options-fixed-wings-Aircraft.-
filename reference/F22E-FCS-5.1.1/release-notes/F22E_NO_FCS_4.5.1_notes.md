# F-22E Strike Raptor FCS 4.5.1 — engine lost above the no-TVC limit: recovery thrust

Base: 4.5.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

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

## Bench (4.5.0 vs 4.5.1)
Left engine killed 3 s into a full-aft pull at the 65 deg TVC limit, 1500 m, full throttle, stick held.

| case | 4.5.0 | 4.5.1 |
|---|---|---|
| from 350 km/h | 65 -> 45 by 6 s on the airframe alone, sideslip 0.4 | 65 -> 45 by 6 s with the nozzles, sideslip 1.0; IAS at 6 s 201 vs 186 km/h, at 10 s 53 vs 58 |
| from 500 km/h | 64 -> 45 by 6 s, sideslip 0.3 | the same AoA, sideslip 0.4; IAS at 10 s 101 vs 76 km/h |

- The Raptor's airframe already brought the nose down to 45 deg on its own, so the gain here is energy: the thrust stays on through the
  recovery, 15-18 km/h more at 6 s (at 10 s: +25 km/h from 500 km/h, -5 from 350 km/h).
- The thrust goes once the AoA is at 47 deg; the jet captures 45.0 with no overshoot.
- Every other case is identical to 4.5.0.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.5.1; 4.5.0 kept as
  `Aryx_F22E_StrikeRaptor_FCS.dll.4.5.0.bak`. `meta.json` (manifest) updated.
