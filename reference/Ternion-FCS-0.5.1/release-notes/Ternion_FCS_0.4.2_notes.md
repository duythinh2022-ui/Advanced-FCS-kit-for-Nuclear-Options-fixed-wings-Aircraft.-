# FS-3 Ternion FCS 0.4.2 — engine-out match: no early let-go; engine-out yaw rules stand down at zero thrust

Base: 0.4.1. **Status:** compiled against the game's assemblies, benched. **Not flown.** The Ternion already had the zero-thrust
match (0.1.9 / 0.1.11). Two of the pieces added for the Ifrit / Su-30 / F-22E port act here too: the match no longer lets go while the
stick still pulls, and the engine-out yaw rules stand down while the live engine is matched to zero.

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

## Bench (0.4.1 vs 0.4.2)
Left engine killed at the pull start, 1500 m, full throttle, full aft held 12 s.

| case | 0.4.1 | 0.4.2 |
|---|---|---|
| full aft, 250 / 350 / 500 km/h | AoA, limit, timing of the match identical to 0.1 deg; sideslip 0.3 / 0.3 / 2.4 | 0.3 / 0.3 / 1.6 |
| full aft + roll reversal, 350 | sideslip 6.8, AoA max 98 | 4.0, AoA max 107 |
| half stick, 350 | sideslip 0.8, IAS end 52 m/s | 0.7, IAS end 42 m/s |
| MPO full aft; engine lost 3 s into a full-aft pull | AoA within 2.5 deg, sideslip within 0.5 deg | |

Every other bench set: loaded reversals 264 / 264, regression 21 / 21, stop / reversal sweep 33 / 33, corkscrews 21 / 21 identical;
damage suite: held max AoA at 300 km/h with the right intake off at 3 s, sideslip 0.3 -> 0.5 (departs on both builds at 88 deg), the
rest identical.

The 12 s full-aft holds end in a climb to under 10-25 m/s and a tail-slide on both builds (zero thrust); that is the 0.1.9 rule.

## Install
- `BepInEx\plugins\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` 0.4.2; 0.4.1 kept as `Ternion_FS3_FCS.dll.0.4.1.bak`. `meta.json` updated.
