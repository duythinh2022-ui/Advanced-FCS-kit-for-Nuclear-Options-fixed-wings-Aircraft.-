# Su-30VXN FCS 0.6.4 — one engine out: live engine matched to zero thrust, no engine-out AoA cap

Base: 0.6.3. **Status:** compiled against the game's assemblies, benched. **Not flown.**

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

## Bench (0.6.3 vs 0.6.4)
Left engine killed at the pull start, 1500 m, full throttle, full aft held. Bench engine model now as the game's nozzle: no
afterburner while a parasitic loss is set (both builds).

| case | 0.6.3 | 0.6.4 |
|---|---|---|
| full aft, 250 / 350 / 500 km/h | AoA 22.0-22.1 (cap 22) | 50.0-50.2 (limit 50), reached in 1.5-1.8 s, no overshoot; sideslip 0.1-0.4 |
| IAS at 6 s / 10 s, from 350 km/h | 272 / 203 km/h | 149 / 127 km/h |
| half stick, 350 km/h | 22, sideslip 0.5 | 42 at 6 s, then the climb runs out of speed: 67 km/h at 10 s, AoA to 98 at the end of the 12 s |
| full aft + full roll reversal, 350 | sideslip 32.6, departs | AoA 50-51, sideslip 2.2 (0-6 s) / 7.2 (0-10 s) |
| MPO full aft, 350 | over the top, sideslip 21 | 78-80 deg held, sideslip 1.1 |
| engine lost 3 s into a full-aft pull at 80 deg | AoA brought down to 22 by 6 s | AoA held at 87 (past the 50 limit, soft-limit hold with the stick held), sideslip 0.4 |

Curve at the limit (full aft, 350 km/h): q rises to 58 deg/s, brakes smoothly onto 50 deg with no overshoot. High-passed pitch-rate
noise at the limit is 0.05 deg/s rms, the same as an idle two-engine pull to 50. The match comes in at 10-21 deg AoA (timed), with no
hitch in q.

Clean jet and every other bench set: loaded reversals 264 / 264, regression 21 / 21, stop / reversal sweep 33 / 33, corkscrews 21 / 21,
damage suite 12 / 12 identical.

## Install
- `BepInEx\plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.6.4; 0.6.3 kept as `Su30_VXN_FCS.dll.0.6.3.bak`. `meta.json` updated.
