# F-22E FCS 3.0.2: two no-fin rules no longer act in straight flight (roll twitch, sideslip departure, roll creep)

Base: 3.0.1. Patch bump: two gates, no tuning value changed. The Dev DLL is 3.0.2 too (version string only). Compiled; benched; **not flown**. 3.0.1 is kept as `.dll.3.0.1.bak`.

## Tom (5 Nov, 20:39)
"Raptor with no fins in the first log had roll twitching and sideslip departure issues when flying straight and accelerating. In the second raptor log, it departed at the end, and also it had some roll trim creep."

## 1. Roll twitch and sideslip departure with no fins (`F22E_FCS_20261105_202945.csv`)
**The damage:** both fins and the right stab (`Elevator_R`); `yawHealth` 0 from ~17 s.

**What the log shows:** stick centred, straight and accelerating at 2–10° AoA.
- **105–111 s, the twitch:**
  - `rCmd` flipped between +1.4 and −2.0 °/s every ~0.1 s.
  - The flaperons and ailerons doing the yaw swung ±15–25°.
  - The roll rate followed: ±7 °/s at ~1.5 Hz.
- **111.5–121 s, the departure:**
  - `rCmd` held at the measured yaw rate minus 2 °/s, while the sideslip loop's own command (`rS`) asked for +26.
  - The yaw rate drifted to −11 °/s.
  - Sideslip went 3 → 48° at 160 → 245 m/s.

**Why: the 2.20.0 no-fin stop (`PaceStopNoFin`).**
- **What it was for:** stopping a roll at high AoA with the fins gone. It holds the roll target at the jet's rotation and asks the yaw loop directly for its brake capacity: `rCmd = r − capacity / KYaw`, at full weight with no fins.
- **Why it ran with no roll:**
  - The side of "the roll in progress" falls back to `Mathf.Sign(num11)`, and Unity's `Sign(0)` is **+1**. With the stick centred and no roll at all, the rule saw a right roll every tick.
  - It also ignored the 2.5.4 rule that the window is off within ±`PaceSinMin` of zero AoA.
- **The result:** whenever the yaw was a little positive the test passed, and `rCmd` became "the yaw you have, minus 2". That gave the chatter when it flickered, and a steady drift when it held.

**Fix (`PaceStopNoFinGate`):** the stop now acts only:
- for a roll being stopped (the trajectory still rotating faster than 2 °/s, or the stick asking for more than 2 °/s); and
- where the window is on.

## 2. Roll creep (`F22E_FCS_20261105_203317.csv`, 139.6–143 s)
**The damage:** right stab, left mid-wing (left aileron frozen), right fin, left rudder; `yawHealth` 0 from 80 s.

**What the log shows:**
- Stick centred, 4° AoA, 160 m/s, banked −17° growing to −37°.
- Roll target `pSRaw` −9 °/s, roll rate −8 to −9 °/s.

**Why: the 2.18.0 `ReleaseCoordinate`.**
- **What it does:** with the fins gone and the lateral stick released while the AoA heads toward zero, the roll target is `r / sin AoA`, with sin AoA floored at sin 15°.
- **Where it was meant to act:** after a nose-down sweep from high AoA.
- **What happened at 4° AoA:** the floor bound, and the banked turn's own yaw rate (−1 to −2 °/s from g sin φ / V) was read as a rotation of −9 °/s. The roll steepened the bank, the bank raised the turn's yaw, and the loop fed itself.

**Fix (`ReleaseCoordGate`):**
- It acts only above the floor (|sin AoA| ≥ sin 15°, i.e. above ~15° AoA).
- It needs more than 5 °/s of rotation after the turn's yaw is taken out.

## Bench: stick centred, no fins, full throttle (accelerating), 20 s, at 300 / 450 / 600 km/h
**Damage sets:**
- **no fins** — both fins.
- **no fins + R stab** — both fins and the right stab (log 1's damage).
- **log 2's damage** — right stab, left mid-wing, right fin, left rudder.

**Manoeuvres:**
- **straight** — stick centred throughout.
- **bank & release** — half lateral stick for 0.6 s, then released.
- **banked turn** — the same with a 0.25 pull held.

| Case | 3.0.1 | **3.0.2** |
|---|---|---|
| Straight, no fins + R stab, 300 km/h: peak sideslip | **53°** | 0.0° |
| Straight, all 9: jumps of more than 2.5 °/s in the yaw command | 0–2.7 per s | **0** |
| Bank & release, all 9: bank drift over 18 s | −11 … +98° | **0 … +10°** |
| Bank & release: roll rate median | up to 6.7 °/s | ≤ 0.5 °/s |
| Banked turn, all 9: yaw-command jumps | 1.5–12 per s | **0 … 0.1** |
| Banked turn: roll rate median | 1.4–10 °/s | ≤ 0.55 °/s |
| Banked turn: peak sideslip | 5–29° | **0.9–6.0°** |

## Regression suite (rev, cork, reg, util, dmg)
- **Clean jet:** reversals, corkscrews, the regression set and the utilisation invariant are **identical**. Both rules need the fins gone.

**Damage suite, the only lines that move:**
| Case | 3.0.1 | **3.0.2** |
|---|---|---|
| No-fin push-over, 560 km/h: peak sideslip | 19.8° | 17.3° |
| No-fin push-over, 600 km/h: peak sideslip | 18.3° | **12.7°** |
| Fins torn off in a rolling pull, 900 km/h: peak sideslip | 22.9° | 19.6° |
| No-fin falling-leaf stops / reversals | 5.7–7.8°, stop 2.0–3.5 s | 5.9–7.3°, stop 2.0–3.2 s |

## Not changed: the departure at the end of log 2 (193–197 s)
**The situation:**
- Gear down at 190 m/s (GearDownRate, no AoA command), 7.5 g full aft.
- A left roll, then a full right reversal.

**What ran out:**
- **Right-roll authority:** with the left aileron and right stab gone, the FCS's right-roll capacity read 737 → 132 deg/s² as AoA rose to 22–28°; 100 deg/s² of the roll command went unmade.
- **Yaw brake:** about 13 deg/s² with no fins, against a −12 °/s yaw left over from the left roll.
- **Sideslip:** 3 → 21°, then departure.

**What it is:** a damage limit at that g. It is the case the 2.2.19 fin-loss AoA cap notes describe (25–30° with full roll held departs), not one of these two bugs.

## Same code in the Ifrit and Revoker DLLs
Both rules came from the F-22E. I haven't checked whether the Ifrit and Revoker ports carry the same `Sign(0)` fallback; they likely do.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 3.0.2; 3.0.1 kept as `.dll.3.0.1.bak`.
- **Dev unlock:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.0.2; 3.0.1 kept as `.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.0.2-release`.
