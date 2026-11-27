# J-20B Weilong FCS 0.1.0: first release

Base: Revoker FCS 1.6.0 (F-22E 4.5.2 / 4.5.3 governor, F-22E 4.6.1 lateral-share gate), shared asymmetric-load files from the Su-30 / Ternion line.

**Status:** compiled against the game's assemblies, benched on a new J-20B bench airframe. **Not flown.** Rebuilt in place 24 Nov 01:58 after the first in-game load (see below). Requires the J-20B Weilong mod (Reasonix.J20.* DLLs + `J-20B Weilong_0.0.16.nobp`).

## Rebuilt in place (24 Nov 01:58): first in-game load fell back to the stock FBW
Tom: "it is not working, still flying with the stock fbw". LogOutput.log:
- `J-20B FCS could not attach; stock flight controls stay active: System.ArgumentException: Field normal defined on type ...NativeAeroSystem+Camber is not a field on the target object which is of type ...NativeAeroSystem+Camber[]`.
  The mod's `camberPairs` is `Camber[][]` (pairs); the bridge read it as a flat list. Now it reads the flat `wingCamber` (`Camber[]`, field signatures checked in the DLL) and falls back to walking `camberPairs` one level down.
- `AccessTools.Field: Could not find field ... SurfaceSpec ... side`: `SurfaceSpec` has no side field in AeroControl 0.66.0. The side now comes from the surface id (`canardL`, `trailingInnerR`, `tailL`); looked up without the HarmonyX warning.
- The bench missed both: its stand-in for the mod's surface system is filled from the extracted data, not from the mod's own types.
- The airframe setup is part of the same attach (it waits for the mod's surfaces), so neither the FCS nor the airframe changes were active on the first build.
- Your PerformanceProfile is Stock in this config (the log: thrust x1, drag x1); the buffed variant needs `PerformanceProfile = Buffed`.

## Builds
- `J20B-Weilong-FCS-0.1.0.zip`: one DLL, the Rust core inside (written to `BepInEx\cache\J20B_FcsCore\<hash>\` at load), as the Revoker.
- `Optional.J20B-Weilong-FCS-0.1.0-CSharpOnly.zip`: the full C# build, no core embedded. On its own it runs every solve in C# (same laws; bench: identical summary).
- `Optional.J20B-Weilong-FCS-0.1.0-NativeCore.zip`: `J20B_FcsCore.dll` for the C#-only build. Put it beside `J20B_Weilong_FCS.dll` in `BepInEx\plugins\J20B_Weilong.FCS\` and the plugin loads it. Not needed with the main zip.
- `build.sh`: `NOEMBED=1` builds the C#-only DLL.

## How it hooks the J-20B
- The J-20B has no stock `ControlSurface`s. Its `Reasonix.J20.AeroControl` drives ten hinged surfaces every physics step (`NativeAeroSystem.StepFromNative`).
- The FCS drives the canards, both elevon pairs and the fins through those hinges in its own tick, then puts its poses back after the mod's Step (a Harmony postfix).
- The mod keeps its leading-edge flap schedule and the LE flaps' extra wing area.
- The mod's own wing camber is replaced by the FCS's:
  - inner elevons turn the wing-root lift normal (FlapCamberCoupling 0.61);
  - outer elevons add a wingtip camber force (AileronCamberCoupling 0.47);
  - the other camber normals are held at rest.
- The assembly is found once (BepInEx PluginInfos, then an assembly-name scan) and a miss is cached. No per-frame lookups. Without the J-20B mod the FCS stays idle.

## What you asked for (24 Nov)

| Item | Setting |
|---|---|
| Buffed profile | thrust x1.08, parasitic drag x0.885, fuel burn x0.75, flares x10, ammo x2, EW x1.5 / x1.2. No AoA / transonic / supersonic drag relief |
| Centre of mass | 0.6 m ahead of the main-gear axles with full tanks (mass moved between the cockpit and the rear fuselage, total unchanged) and held there as fuel burns (FuelCgHold, reference full) |
| Lift areas | forebody / chines (root part + cockpit) x1.5 (the best within your 1.5 cap, see below); wing root x1.25; inner wing (wing1) x1.25 on top of the mod's LE-flap area |
| Trailing-edge elevons | +35 TE up / -30 TE down, with camber coupling |
| Canards | pitch only (symmetric pair, split prohibited), +-25 |
| Vortex lift | forebody flies a slender-strake (Polhamus) curve; breakdown 45 -> 70 deg |
| Air brake (air) | F-22E style: inboard TE down 15, outboard TE up (zero added lift), fins toed out 15, faded out at high AoA |
| Air brake (ground) | inboard fully up, outboard fully down, fins out, canards 80 deg LE down |
| Governor | F-22E 4.5.2 / 4.5.3 / 4.6.1 lateral gate (from Revoker 1.6.0) |

## Bench findings (no game log yet, so no calibration)
**Highest usable AoA: 42.** Canards at their best and all four elevons fully TE up leave this much nose-up:

| AoA | 40 | 44 | 47-48 |
|---|---|---|---|
| Nose-up left, 500 km/h (deg/s^2) | ~45 | ~30 | 0, at every speed |

Past 44 the airframe pitches down on its own: there is no deep-stall trim point.
- **Full aft:** holds 40-42 at 250-600 km/h, sideslip under 2 deg. G-limited at 9 g above about 600 km/h.
- **Negative limit 30.** The airframe stops restoring at about -33 and has a stable inverted trim near -42 (recoverable).

**Trim-free 20-45 (vortex lift): partly met.**
- With surfaces neutral the pitch moment is within about +-10 deg/s^2 from 32 to 42 deg (canard trim within +-5 deg).
- From 20 to 28 deg it still needs 16 -> 6 deg of canard leading edge up.
- The canard is the trim surface (lift-positive, so little trim drag); the elevons stay near zero there.
- To make 20-28 trim-free the forebody would need well over x1.5: ~1.2 more CL on 6 m^2 at 4.7 m. So x1.5 is the cap, and it is the best value inside it.

**Roll:**

| Condition | Velocity-vector roll |
|---|---|
| 1 g, 300 / 500 / 800 km/h | 155 / 205 / 237 deg/s, smooth onset, no hitch |
| Held at 15 deg AoA | ~100 deg/s |
| Held at 30 deg AoA | ~40 deg/s |
| At the 40-42 limit | ~10 deg/s |

There is no thrust vectoring, the fins' yaw rolls the jet the wrong way (canted fins), and at the limit the elevons are spent on pitch.

**Engine out:** no AoA cap. The live engine is matched to zero thrust from 35 deg, as on the Ternion. Same VMC block and the same numbers (match point 35, hysteresis 5, rise 4/s, fall 1.5/s); the TVC recovery thrust is inert here. Bench, left engine killed, full aft at 250 / 350 / 500 km/h: the limit holds with sideslip under 1.6 deg until the zero-thrust climb bleeds the speed.

## Fixes found by the J-20B bench
1. **Constructor crash with hinge-backed surfaces.** The split-flap lookup read `maxSplit` on a null `ControlSurface`, so the FCS would have thrown at spawn. Now guarded.
2. **Settled stall window vs. this canard foil.**
   - The canard foil ("J20 canard effective high-alpha lift v1") holds CL 2.7-2.9 from 20 to 37 deg, and its normal force keeps rising.
   - The window's lift-slope knee (22 deg) walked the canards 16 deg leading edge down at 38-40 deg AoA. That cost ~50 deg/s^2 of nose-up: full aft peaked at 39.7 and sagged to 27.
   - Per-jet defaults now SettledSlopeFrac 0, SettledStallMargin 1. Full aft holds 40-42.
3. **Inboard elevons counted twice in the asymmetric-load model** (`FillSurfaceParts`, FcsController.cs).
   - The cause: HsGather filled `surfaceParts` with the control surfaces only, then AsymPrepSym skipped its own fill. So the hinge elevons' parts stayed in the airframe term at their actual deflection, while their effector added them again at the symmetric one.
   - The effect: any elevon split read as an asymmetric load. At 400 km/h full aft the asym cap went 40 -> 25 -> 40 and the AoA oscillated 38 <-> 25.
   - Both now fill the same set; the shared file is unchanged.
   - **The Revoker and the Vortex (hinge elevons) can have the same bug,** depending on which of the two runs first. Worth porting there.

## Settings worth knowing (dev)
- `AoALimitPositive` 42, `AoALimitNegative` 30.
- `CgAheadOfMains` 0.6, `FuelCgHold` true.
- `ForebodyLiftScale` 1.5 (clamped 0.5-1.5), `WingRootLiftScale` 1.25, `Wing1LiftScale` 1.25.
- `StrakeVortexLift` true, `StrakeBreakdownStart` 45 / `End` 70, `StrakeResidualCN` 1.2.
- `CanardRangeLEDown` / `LEUp` 25, `ElevonRangeTEUp` 35 / `TEDown` 30, `FinRange` 30.
- `AirBrakeFinToe` 15, `GroundBrakeOuterElevonDown` true, `GroundBrakeCanard` 80.
- `EngineOutZeroThrust` true, `EngineOutMatchAoA` 35.
- MPO: velocity-vector roll only (no TVC).

## Not checked
- The ground brake pose: the bench has no ground model for this jet.
- The mod's own idle-throttle wheel brake.
- Multiplayer.
- Stores, beyond the FuelCgHold code path.

## Install
- `BepInEx\plugins\J20B_Weilong.FCS\J20B_Weilong_FCS.dll` (Rust core inside).
- Optional: `BepInEx\plugins\J20B_Weilong_FCS_Dev.dll` for the dev settings.
