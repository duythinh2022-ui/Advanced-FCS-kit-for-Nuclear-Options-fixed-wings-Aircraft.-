# KR-67 Ifrit FCS 1.5.0 — one engine out: live engine matched to zero thrust, no engine-out AoA cap

Base: 1.4.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

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

## Bench (1.4.0 vs 1.5.0)
Left engine killed at the pull start, 1500 m, full throttle, full aft held. The default Ifrit bench data has no engine model of its own
kind (the turbojet RPM law, the parasitic loss), so this set runs on a copy with the engines marked turbojet; both builds the same.
Bench engine model now as the game's nozzle: no afterburner while a parasitic loss is set (both builds).

| case | 1.4.0 | 1.5.0 |
|---|---|---|
| full aft, 250 km/h (135 kt) | AoA 21 (limit 20.5), IAS 3 s / 6 s / 10 s: 273 / 248 / 204 km/h | peak 45.8, then down to 22 as the limit falls to 20 with the speed; 205 / 153 / 160 |
| full aft, 350 km/h | 22 | peak 34.4; 20 by 6 s (limit 20); IAS at 10 s 122 vs 196 |
| full aft, 500 km/h | 22 | 38-45 to 6 s (limit 45), sideslip 1.0; climbs to 50 km/h by 10 s, then tail-slides (AoA to 93) |
| half stick, 350 km/h | 22, sideslip 1.1 | 25-27, sideslip 1.2 |
| full aft + full roll reversal, 350 | sideslip 41, departs | AoA 34 -> 21, sideslip 6.1 (0-10 s) |
| MPO full aft, 350 | over the top with sideslip 73, spins | over the top, sideslip 1.9, no spin |
| engine lost 3 s into a full-aft pull, at 65 deg (TVC limit) | yaw departure: sideslip 72 | no yaw (sideslip 0.8), but the nose can't be brought back from 65 without thrust: the law holds q = 0 past the limit (stick held), the jet tail-slides to 166 deg AoA. With the stick released at 5 s it recovers wings-level (AoA 131 -> 98 deg by 13.7 s, sideslip <= 3.5); 1.4.0 spins (sideslip 71) |

Clean jet, the damaged-engine (running) match and every other bench set: loaded reversals 264 / 264, regression 21 / 21, stop /
reversal sweep 33 / 33, corkscrews 21 / 21 identical. Damage suite (default bench data, so the zero thrust itself is not modelled
there): 51 / 52 identical; held max AoA at 300 km/h with the left intake shot off at 3 s: sideslip 38.5 -> 13.9, no departure.

## Open
- An engine lost above the no-TVC limit (TVC holding 65 deg) leaves a jet that cannot get its nose down at zero thrust. Releasing
  the stick recovers it on the bench; holding it aft keeps the nose where it is (the soft-limit rule).
- Zero thrust with the stick held is a climb to zero speed: from 500 km/h the jet is at 50 km/h in 10 s.

## Install
- `BepInEx\plugins\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` 1.5.0; 1.4.0 kept as `Ifrit_KR67_FCS.dll.1.4.0.bak`. `meta.json` updated.
