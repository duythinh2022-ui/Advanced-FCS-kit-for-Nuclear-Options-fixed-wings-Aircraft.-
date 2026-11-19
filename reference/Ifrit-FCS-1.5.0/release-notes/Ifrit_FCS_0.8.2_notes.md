# Ifrit FCS 0.8.2: reference-governor port, step 1 (capacity estimator as telemetry, air data at the CG)

Base: 0.8.1 (the VMC release ceiling and the sliding engine-out limit). Patch bump: the only behaviour change is where the air data is read.
Handoff: `claude/RefGov_port_Ifrit_Revoker_handoff.md`.

**Status:**
- Compiled against the game's assemblies.
- Benched on the port-base bench (`Ifrit-port-base-0.8.0`, with the 0.8.1 sources dropped in).
- **Not flown.** 0.8.1 is kept as `.dll.0.8.1.bak`.

## Tom's decisions (4 Nov, 22:2x)
- **Staging:** telemetry first. The law comes in 0.9.0, after a flight log, with the gains and the push plan fitted to this jet.
- **`PushoverSideslip`:** 8°, his `RollStopSideslip`, set explicitly when the push plan comes in.
- **rudW:** derived from the probes, not the copied rudder fade.
- **`SlipFrameAlloc`:** stays on.
- **Rate cap (`RevSim` vs 3.0.0's swing model):** asked back what it means. It isn't needed until the law step, and is still open.

## How the port was done
**Not the full three-way merge yet.** For a telemetry-only step, merging all of 3.0.0's consumers behind switches set to off would put about
860 lines of inert code into the controller with no effect on behaviour. That merge belongs to the law step (0.9.0), where each device
gets turned on and checked.

What came over now:

1. **Air data at the CG** (F-22E 2.30.0: `AirDataAtCg`, `FlightPathAtCg`).
   - AoA, sideslip, TAS/IAS and the flight-path rates (`omegaV`, `vDot`) are read from the mass-weighted velocity of every body, not the root
     rigidbody.
   - `nz` stays the root's.
   - Logged both ways: `alphaRoot`, `betaRoot`, `airRefCg`, `alphaCg`, `betaCg`.
2. **The capacity estimator**, logging only (`CapacityEstimator.cs`, ported from F-22E 3.0.0 with these changes):
   - **Every effector is grouped.** There are six groups: `r` rudders (none on the Ifrit), `s` stabilators (the canted tails), `f` other
     surfaces (ailerons, decelerons), `t` TVC of any kind (pitch nozzles and roll TVC), `p` splits, and `h` hinged surfaces. The F-22E file
     dropped every non-`SurfaceEffector`, which here meant roll TVC and the splits.
   - **Gains are 1 for every group.** The F-22E's 1.0 / 0.7 / 1.0 were fitted to the F-22E. This jet's gains come from its flight.
   - **New sideslip-axis read (`ceCapBP/BN`, `cePrBP*/BN*`):** acceleration along b = (sin α, −cos α) in (roll, yaw). This is the
     effector part of β̈ ≈ ṗ sin α − ṙ cos α. It is built from the measured term, plus the committed term, plus a probe along b, minus the
     allocator's unmet demand along b (per Tom's rule: count everything, on the sideslip axis).
   - **rudW from the probes (`ceRudW`, with `ceYawPureP/N`):** the yaw the allocator can add without spending roll.
     - The yaw-only probe can't answer this on this jet. Asked for 400 deg/s² of yaw, the allocator trades roll away for it: bench, 300 km/h,
       10° AoA, +39 of yaw for −333 deg/s² of roll.
     - So the roll probe on the opposite side is added in the share that cancels that roll. The yaw left over is the roll-neutral yaw.
     - The F-22E's rudder-fade value is logged beside it (`ceRudWFade`).
   - **Probes per tick:** 8 extra allocator solves: yaw ±, roll ±, velocity-roll ±, sideslip-axis ±. The F-22E runs 6.
   - **Columns:** about 110 `ce*` columns, appended before the per-surface columns. The full list is in `CapacityEstimator.Header()`.
   - **Drives nothing.** It is called after the allocation, and it restores every effector's command.
   - **Runs only while the FCS flight log is open** (`OnlyWhileLogging`). Cost on the bench: about 0.65 ms per tick on this jet (a 20 s flight,
     2.1 s with it against 1.3 s without). With the log off it costs nothing. The bench sync turns the gate off, so the bench always runs it.
   - The bench can now set `CapacityEstimator` statics as `Name=value` (`sim/Program.cs`).

## Bench
Full suite (reg / sw / cork / dmg; 20 + 64 + 20 + 32 cases).

**Identity check:** **passed.** With `AirDataAtCg=false FlightPathAtCg=false`, all four suite files (reg 20, sw 64, cork 20, dmg 32 cases)
match 0.8.1 line for line. The estimator changes nothing. The final build (log-gated, below) matches the CG-on run on all four files.

**Air data at the CG**, scored at the same point on both sides. The baseline's sideslip is moved to the CG with the measured nose-to-CG arm
(**+0.625 m**, fit residual 0.007°):

| | cases | worse by > 0.5° | mean Δ peak β | best |
|---|---|---|---|---|
| reg | 20 | **0** | −0.43° | −4.2° (full-aft roll + reversal, 500 km/h: 7.6 → 3.4°) |

Also:
- Pull-then-push-over at 350 km/h: β 4.8 → 3.0°, roll rate up (peak 63 → 81 deg/s).
- Damage suite: 1 of 32 worse by more than 0.5°. That one is the released push-over at 600 km/h with one ruddervator lost, 3.4 → 4.0°, and it
  is scored on the raw logged β (the measuring point changed with the switch).

**Sanity flight** (bench, 300 km/h, full pull then full roll):
- the tails' sideslip-axis share falls with AoA;
- the wing surfaces dominate;
- roll TVC adds 25–28 deg/s² on the sideslip axis at 64°, 44–53 m/s.

## For the flight (what step 2 needs)
One log, with:
- rolls and reversals at 15–35° AoA, 250–350 kt;
- full-aft rolling pulls;
- the falling leaf;
- **6–10 push-overs from 60–70° AoA**, released and held, at 150–250 kt.

From that log:
- the per-group gains;
- `PushPlanK` / `Exp` for this jet;
- the rudW check.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` and `meta.json` 0.8.2.
- The `meta.json` one level up is the mod manager's own manifest (its 0.5.6 GitHub artifact record). It was left alone.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.8.2-release`. The source zip includes the bench.
