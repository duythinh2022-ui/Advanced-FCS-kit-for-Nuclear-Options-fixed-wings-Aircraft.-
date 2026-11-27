# T/A-30 Compass FCS 0.6.0 — one engine out: live engine matched to zero thrust, no engine-out AoA cap

Base: 0.5.1. Minor bump (law change). **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom, 18 Nov 21:47: "update compass then" (after: the Compass, a twin, was the one FCS mod still on the engine-out AoA cap and on
the first shared asymmetric-load text). Two things, one release:

## 1. One engine out: the live engine matched to zero thrust (your 9 Nov rule, now on every twin)

Until now an engine destroyed on the Compass capped the AoA at `EngineOutAoACap` (22 deg) and held the live engine at military
power above it. Now it does what the Ternion (0.1.9 / 0.1.11 / 0.4.2), Ifrit (1.5.0), Su-30 (0.6.4) and F-22E (4.5.0) do:
- **No AoA cap for the engine loss.** The limit is the jet's own 35 deg.
- **From `EngineOutMatchAoA` (35 deg, dev) up, the live engine is held at zero thrust** through its `parasiticThrustLoss` (game
  `Turbojet.FixedUpdate`: thrust = max(0, (max thrust - loss) x thrust ratio); with a loss set the nozzle lights no afterburner),
  and the throttle input comes off the afterburner with it. The match is timed to be complete as the AoA gets to the point.
- It also **latches on at any AoA when the yaw saturates**, MPO included.
- It **lets go** once the AoA is 5 deg under the point, the yaw has settled and the stick no longer pulls toward it.
- New dev keys ("Asymmetric load"): `EngineOutZeroThrust` (true) and `EngineOutMatchAoA` (35). false = 0.5.1.
- Note: on this jet the match point and the AoA limit are the same 35 deg (on the Ternion the limit is 55). So a full-aft pull
  with an engine out goes to zero thrust as it reaches the limit; a half-stick pull (27 deg) keeps the live engine.

Code: the Ternion's zero-match block in `VmcApply` (the `zeroLatch` latch, `EngineOutMatchHyst` 5), `ThrustZeroApply` in the
Ifrit's Turbojet form, and the two engine-out yaw rules (yaw-target clamp, roll-fight exemption) on `EngSplitOut()`.
No recovery thrust: that is for TVC jets above their no-TVC limit, and the Compass has no nozzles.

## 2. Shared asymmetric-load file brought up to date

`AsymShared.cs` is now the same text as on the F-22E, Ifrit, Ternion, Su-30 and Vortex (`checkshared.py`: AsymShared
`07b1e78b2722f37a`, AsymCap `f1b8d2915c6e087c`, CoupledCap `6f66ef4087ade420`). 0.5.1 carried the first shared text (`62f3…`),
without `ZeroThrustPlanned()` / `EngSplitOut()`. Only the Revoker (one engine, where these can never act) is left on the old text.

## Bench (0.5.1 vs 0.6.0)

Left engine killed 0.5 s in (late case: 3 s into the pull), 1500 m, full throttle, full aft held 12 s unless noted. Bench engine
model, both builds: no afterburner while a parasitic loss is set (the game's nozzle; as the Ifrit 1.5.0 bench).

| case | 0.5.1 | 0.6.0 |
|---|---|---|
| full aft, 250 km/h | AoA held at the 22 deg cap; peak sideslip 5.5 | 35.1 deg (the limit), sideslip 0.4; zero thrust from 1.6 s |
| full aft, 350 km/h | 22.1, sideslip 3.7 | 35.0 held to 6.5 s (127 km/h), sideslip 0.8 |
| full aft, 500 km/h | 22.2, sideslip 0.4 | 35.2, sideslip 0.3 to 10.4 s; see below |
| half stick, 350 km/h | 22.1, sideslip 0.9 | 27.4 (no cap, no match), sideslip 0.6 |
| full aft + full roll R then L, 350 | 23.5, sideslip 3.8 | 35.2, sideslip 7.6 (see below) |
| engine lost 3 s into a full-aft pull, 350 | 34.8 -> cap 22, sideslip 6.2 | 35.0 held, sideslip 2.6 |
| **MPO full aft, 350** | **sideslip 34.4, departs** | **sideslip 0.8** |
| held max AoA 300 + right intake shot off at 3 s (damage suite) | 35.1, peak sideslip 1.5 | 36.4, peak sideslip 0.4 |

**Curve check** (every engine-out pull, stick-in to 120 km/h): no pitch-rate reversals, no hitch; the match comes in at 1.2-2.5 s
with no dip in the pitch rate. Peak pitch rate is higher (350 km/h: 31 -> 47 deg/s) because the 22 deg cap no longer stops it.

**The cost: energy, at the higher entry speeds.** Lowest IAS in the 12 s full-aft holds, 0.5.1 -> 0.6.0: 250 km/h entry 51 -> 96
(0.6.0 goes over the top sooner at 35 deg); 350: 59 -> 55; 500: 184 -> 65; engine lost 3 s in: 63 -> 14. With the speed gone the
AoA leaves 35 deg in the tail-slide / over the top (350: 58 deg at 55 km/h; late loss: tail-slide to 166 deg at 15 km/h). The
Ternion notes record the same tail-slide for its 12 s holds; it is what the rule trades for (no thrust split, no AoA cap).

**Two cases looked at closely:**
- **Roll reversal, sideslip 7.6.** The 0.6.0 jet reverses at 35 deg AoA and ~150 km/h; 0.5.1 was at 22 deg with more speed. The
  same reversal with **both engines at idle** gives sideslip 7.8 deg and the same roll (-7.2 deg/s) on both builds, so this is the
  airframe's own authority at 35 deg / 150 km/h, not the engine-out logic.
- **500 km/h, sideslip up to 10.7 at 11-12 s.** The jet has gone over the top inverted at 66 km/h; the AoA turns negative, so with
  the stick still aft it is no longer "pulling toward the point" and the match lets go; the live engine's thrust comes back at
  70-87 km/h (yaw 16 deg/s) until the yaw-saturation latch re-takes it. This is the shared latch rule (the same on the Ternion,
  Ifrit, Su-30, F-22E); per your rule no Compass-only fix. Open: the let-go reads the stick against the AoA's sign, so after a
  fall over the top it treats a held aft stick as released.

**Unchanged** (both engines running, and everything without an engine out): the regression, stop / reversal sweep and corkscrew
outputs identical line for line (22 / 37 / 24 lines), the asymmetric-load matrix 24/24 cases identical (all logged columns but
wall-clock timers); damage suite identical apart
from the engine case above (2 cases fail to start on both builds: a bench key the Revoker-derived damage suite names, pre-existing).
Clean MPO full aft: identical.

## Build
- `Compass_TA30_FCS.dll` 0.6.0: 1,381,888 bytes, md5 `18bb6ccd3fd278efe75a0c5a9db47f8d` (built twice, same bytes; native core
  `Compass_FcsCore` unchanged, embedded).
- Dev DLL: unchanged (in the Optional zip).
- No migration: the two keys are new and default on.

## Install
- `BepInEx\plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` 0.6.0 and `meta.json`; 0.5.1 kept as `Compass_TA30_FCS.dll.0.5.1.bak`.
- Release folder: `C:\Games\NO mods\Compass-FCS-0.6.0-release`.
- Mod manager: update its record, or a modpack may put 0.5.1 back. Restart the game to load it.
