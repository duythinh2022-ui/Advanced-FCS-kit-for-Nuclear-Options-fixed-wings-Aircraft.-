# Revoker FCS 0.5.1: reference-governor port, step 1 (capacity estimator as telemetry)

Base: 0.5.0 (the protection override). Patch bump. **Flight behaviour is identical to 0.5.0**: air data at the CG is ported but **off** by
default on this jet (reason below). Handoff: `claude/RefGov_port_Ifrit_Revoker_handoff.md`.

**Status:**
- Compiled against the game's assemblies.
- Benched on the port-base bench (`Revoker-port-base-0.5.0`).
- **Not flown.** 0.5.0 is kept as `.dll.0.5.0.bak`.

## Tom's decisions (4 Nov, 22:2x)
Same as the Ifrit (see `Ifrit_FCS_0.8.2_notes.md`):
- telemetry first;
- `PushoverSideslip` 8°;
- rudW from the probes;
- `SlipFrameAlloc` stays on;
- `RevSim` vs 3.0.0's rate cap still open, needed at the law step.

## What came over
1. **The capacity estimator**, logging only. Same file as the Ifrit's, with:
   - six groups covering every effector (`r` the single rudder, `s` the canards, `f` the outer elevons, `t` pitch and yaw TVC, `p` splits,
     `h` the inboard elevons as `HingeFlapEffector`);
   - gains of 1;
   - the sideslip-axis read;
   - rudW from the probes;
   - about 110 `ce*` columns.

   The F-22E file dropped yaw TVC, the inboard elevons and the splits.
2. **Air data at the CG** (`AirDataAtCg`, `FlightPathAtCg`): ported, **default false** on this jet.
   - The FCS keeps reading the root body, as in 0.5.0.
   - Both are logged: `alphaRoot`/`betaRoot` (= the FCS's `alpha`/`beta` while off) and `alphaCg`/`betaCg`.

## Why the CG air data is off on the Revoker
- **Arm:** the root body sits **0.93 m behind** the CG here. It was fitted from the bench runs (`betaRoot − beta = arm · r / V`, residual
  0.04°). The handoff's "0.95 m ahead" had the sign wrong.
- **Bench, regression suite with the sideslip scored at the CG on both sides:** 2 of 20 cases are worse than the ~0.5° noise. Both are
  65° AoA releases:
  - roll + yaw release: peak β 1.7 → 2.8°;
  - roll release: 1.1 → 1.8°.
- **Mean over the 20 cases:** +0.23°.
- **Damage suite:** 5 of 32 worse (clean leaf stops 1.0 → 2.0°; held push-overs 6.3 → 6.9 and 5.9 → 7.2°). These are scored on the raw logged
  β, which changes measuring point with the switch.
- **"Without trade-offs"** is the rule, so it stays off. The flight log has both readings, to see which one the jet's real sideslip follows.

## Bench
- **Identity check:** **passed.** With the estimator in and the CG air data off (the shipped defaults), all four suite files (reg 20, sw 64,
  cork 20, dmg 32 cases) match 0.5.0 line for line, on the final build too.
- **Cost:** the estimator runs only while the FCS flight log is open. On the bench it costs about 0.33 ms per tick on this jet (a 20 s flight,
  2.1 s with it against 1.6–1.7 s without).

## For the flight
- Same list as the Ifrit: rolls and reversals at 15–35°, full-aft rolling pulls, the falling leaf, and **6–10 push-overs from 60–70° (the AoA
  limit here is 70)**, released and held, at 150–250 kt.

## Install
- `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` and `meta.json` 0.5.1.
- Release: `C:\Games\NO mods\Revoker-FCS-0.5.1-release`. The source zip includes the bench.
