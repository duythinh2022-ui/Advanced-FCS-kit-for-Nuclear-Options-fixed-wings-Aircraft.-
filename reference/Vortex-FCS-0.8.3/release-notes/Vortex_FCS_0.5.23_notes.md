# Vortex FCS 0.5.23: hotfix for the 0.5.22 departure in the CTOL blend (full pull + full roll)

20 Nov 2026. On 0.5.22.

**Status: benched, not flown.**

Tom, 20 Nov:

> check the end of the log, i departed, is it because i acidentally turn off stability assist

## Answer: no, it was 0.5.22's nose hold

Log `Vortex_FCS_20261120_025251.csv`, 15.0 t, hover T/W 1.05.

**Stability Assist stayed on.**
- The FCS mode is Rate from 26 s to the end, never MPO.
- The STOVL law owns the jet throughout.
- There is no "Stability Assist off in STOVL" line in the game log.

**What happened at 231–234 s:** STOVL at 85–89 m/s (165–173 kt), the CTOL blend fully in. You put in full right roll and full aft stick.
1. 0.5.22's new turn term (pitch rate = yaw rate × tan(bank), which holds the nose in a banked turn) was added **after** the CTOL pitch law. That put it past the law's AoA envelope and its pitch-rate budget.
2. At 75–83° of bank, tan(bank) is 4–8. With the yaw rate at 30 deg/s, the term asked for **120–245 deg/s** of pitch. The 24° STOVL AoA limit never saw it.
3. AoA went 14 → 46 → 160°, sideslip to −37°. The jet departed into a spin and dive.

**This was my bug in 0.5.22, not your input.** Full pull plus full roll should be held at the AoA limit.

## Fix (still `StovlCtolNoseHold`)
- **Pitch:** the turn term now goes **into** the CTOL pitch law's rate command (both the Rate and the AoA/G branches). The AoA envelope, the AoA protection and the pitch-rate budget bound it like any other rate command.
- **Path:** the propulsion's path rate takes the law's output (which now contains the term), so the term is no longer counted twice.
- **Unchanged:**
  - the walk up to the reference AoA;
  - the bank keeping (−ψ'·sin(pitch), still bounded by the bank-limit roll rates).

## Bench: 15 t, ~230 km/h, CTOL blend, full roll 1.5 s + full aft 3 s, then release

| | 0.5.21 | 0.5.22 | 0.5.23 |
|---|---|---|---|
| Max AoA in the blend | 22.7 | 47.6 | **23.9** (limit 24) |
| Max sideslip | 10.0 | **40.7** (departs) | 10.1 |
| Max pitch-rate command | 16.6 | 40.6 | 14.4 |
| After release | Nose falls | — | Nose held at ~20°, bank 48 → 42° |

*0.5.21's 53° AoA in this case comes later (22–25 s at 40–77 km/h, the STOVL laws slowing nose-high), not in the blend.*

**The 0.5.22 cases still hold:**
- **Nose after release, 35–40° bank:** +0.04 / +0.03 deg/s.
- **Departure case** (16.1 t, pedal and pull at 45 m/s): sideslip 11.7°.
- **12-case set:** identical to 0.5.21, except the full-lever short take-off (one authority column, 3.7 at one moment; height and attitude identical).
- **Regression set:** unchanged.

## Also seen in the log, not the cause, not changed
At 216–223 s (full left pedal from 27 to 45 m/s), the sideslip reached **31–35°**:
- the pirouette was on until 218 s;
- after that the stiffness read low (0.24–0.6 of the hover-pedal threshold), so the pedal was still half the hover yaw command.

It settled at 14° once the stiffness read normally. This is the noisy stiffness reading flagged in the 0.5.22 notes. It only applies when the jet can hover (T/W 1.05 here).

## Release

Main DLL 0.5.23. Release folder `C:\Games\NO mods\Vortex-FCS-0.5.23-release`. No new switches; `StovlCtolNoseHold` false = 0.5.21 behaviour.
