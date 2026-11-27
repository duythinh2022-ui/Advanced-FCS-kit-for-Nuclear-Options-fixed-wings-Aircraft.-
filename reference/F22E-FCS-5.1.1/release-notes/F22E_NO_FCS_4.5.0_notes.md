# F-22E Strike Raptor FCS 4.5.0 — one engine out: live engine matched to zero thrust, no engine-out AoA cap

Base: 4.4.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## One engine out: the live engine matched to zero thrust (Ifrit 1.5.0, Su-30 0.6.4, F-22E 4.5.0)

Tom, 17 Nov 12:16: "do the 1 engine out thrust match on ifrit flanker and raptor so they could reach their max no tvc aoa". This is
his 9 Nov rule (Ternion 0.1.9 / 0.1.11): with one engine destroyed, no AoA cap for it; the live engine is matched to the dead one,
i.e. zero thrust, in every mode including MPO.

**What the three jets now do with an engine destroyed**
- No `EngineOutAoACap` (Ifrit 22 sliding to 18, Su-30 22, F-22E 30) any more. From `EngineOutMatchAoA` (35 deg, dev) up, the live
  engine is held at zero thrust through its `parasiticThrustLoss` (game `Turbojet` / `Turbofan.FixedUpdate`: thrust = max(0, (max
  thrust x dynamic factor - loss) x thrust ratio); with a loss set the nozzle lights no afterburner), and the throttle input comes off
  the afterburner with it.
  - The match is timed so it is complete as the AoA gets to the point.
  - It also latches on at any AoA when the yaw saturates.
  - It lets go once the AoA is 5 deg under the point, the yaw has settled and the stick no longer pulls.
- With no thrust the nozzles cover no pitch, so the limit that holds is the jet's own no-TVC one: Su-30 50 deg (your
  `NoTvcAoACapPositive`), F-22E 45, Ifrit 45 above about 250 km/h, falling to its 20 deg authority floor as the speed bleeds (the same
  as an idle pull on two engines).
- New dev keys ("Asymmetric load"): `EngineOutZeroThrust` (true) and `EngineOutMatchAoA` (35). false = the old behaviour.

**Three pieces beyond the Ternion's port (shared file, so the same in all seven)**
1. The low-thrust AoA cap is judged with the thrust the match will leave: none (`ZeroThrustPlanned()`; nozzle term out of the cap,
   nozzles counted as not covering pitch, from the engine loss on). Without it the Ifrit pulled toward its 65 deg TVC limit, the thrust
   went at 18-27 deg while rotating at 53 deg/s, and the airframe alone could not stop it: AoA past 170 deg.
2. The laws' engine-out yaw rules stand down while the live engine is matched to zero (`EngSplitOut()`: the yaw-target clamp to the
   fins' reach and the roll-fight exemption). They are for a thrust split; with the match full there is none. On the Ifrit the clamp
   held the split decelerons shut for yaw, so they were not there as its nose-down device at 40-50 deg: the jet pitched past 90 deg with
   the stick forward. With it standing down the same pulls hold.
3. The match does not let go while the stick still pulls toward the point. A match the timing rule put on early (fast AoA rise) let
   go at 27 deg as the rise slowed; the thrust came back for 0.5 s (q 22 -> 31 deg/s) and went again. This applies to the Ternion
   as well.

On the Revoker, Vortex and Compass none of this can act: `EngineOutZeroThrust` is false there, so both helpers reduce to the old
conditions. Their source trees carry the new shared text (checkshared: AsymShared `07b1e78b2722f37a` in all seven); not re-released,
since nothing they do changes.

## Bench (4.4.0 vs 4.5.0)
Left engine killed at the pull start, 1500 m, full throttle, full aft held 10-12 s. Bench engine model now as the game's nozzle: no
afterburner while a parasitic loss is set (both builds).

| case | 4.4.0 | 4.5.0 |
|---|---|---|
| full aft, 250 km/h (135 kt) | AoA 30-31.6 (cap 30), sideslip 2.7 | 45.1 held (limit 45), peak 50.1 on the way in, sideslip 0.5 |
| full aft, 350 / 500 km/h | 30.0 | 45.0 held, no overshoot, sideslip 0.2 / 0.1 |
| IAS at 6 s / 10 s, from 350 km/h | 309 / 217 km/h | 185 / 64 km/h (a vertical climb at zero thrust; AoA rises to 53-60 once under ~70 km/h) |
| half stick, 350 | 30 | 35-40 |
| full aft + full roll reversal, 350 | 30, sideslip 4.1 | 45, sideslip 3.3 to 6 s; **departs at 60-75 km/h at 9-12 s: sideslip 26 (0-10 s), 68 at 12 s** |
| MPO full aft, 350 | 117 deg by 6 s, sideslip 15 | 60-68 to 6 s, 92 by 10 s, sideslip 0.5 |
| engine lost 3 s into a full-aft pull at 65 deg | AoA to 30, sideslip 15 (0-10 s) | AoA to 45, sideslip 0.4 |

Clean jet and every other bench set: loaded reversals 88 / 88, regression 19 / 19, corkscrews 21 / 21 identical. Damage suite: one
line differs, held 65 deg at 300 km/h with the left LERX off and the left engine killed at 3 s: AoA after the event 29.8 -> 44.7 deg
(the no-TVC limit instead of the 30 engine-out cap), sideslip 7.9 -> 0.6.

## Open
- Full aft held with full roll reversals: at zero thrust the speed is down to 60-75 km/h after 9 s, and a roll reversal there
  departs (sideslip 68). The same reversals at idle on two engines stay at 122 km/h or more and peak at 12 deg of sideslip.
  4.4.0 kept the live engine's thrust there (210 km/h).
- Peak 50.1 deg on the way to the 45 limit at 250 km/h (5 deg over for under a second).

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.5.0; 4.4.0 kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.4.0.bak`.
  `meta.json` (manifest) updated with the new zip's hash.
