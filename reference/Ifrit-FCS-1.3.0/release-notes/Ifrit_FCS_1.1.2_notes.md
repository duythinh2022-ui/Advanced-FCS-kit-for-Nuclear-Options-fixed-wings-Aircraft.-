# KR-67 Ifrit FCS 1.1.2: free roll in MPO

Base: 1.1.1. Tom, 11 Nov 12:39: "if i turn on mpo, lemme roll freely regardless of sideslip".

**Changelog:** With Stability Assist off (MPO) and `MpoRollMode = SeparateRollYaw` (your setting), the roll stick's body roll is limited only by what the roll axis can do (control surfaces + roll nozzles). Sideslip no longer holds it back. New main-cfg key `1. General / MpoFreeRoll` (default **true**; false = exactly 1.1.1).

**Status:** bench replay of your 12:35 log only. **Not flown.**

## What held the roll (12:35 log, 1.1.1, MPO, full roll stick)
| Window | IAS | AoA | Roll rate | Roll limit (pMax) | Sideslip | Roll nozzle used |
|---|---|---|---|---|---|---|
| 186.8–192 s | 56–117 km/h | 75–107° | 12.9–13 °/s, flat | 13 °/s | to −34° | 2–2.6° of ±15 |
| 210.5–214.9 s | 30–55 km/h | −145 → −40° | 12.9 °/s, flat | 13 °/s | to −63° | 0.6–0.8° |

- **The roll followed its command exactly** (p = pCmd = pMax). The limit itself was the cap.
- **What set the limit:** the high-AoA braking floor (`HighAoABrakeFloor`, 25–35° AoA up) and the high-AoA lateral margin (0.86). They size the roll limit so roll and yaw can be stopped *together* (the sideslip guard of the coupled law), from the **control surfaces only**: aRoll 10 °/s² at that speed → 13 °/s.
- **What it ignored:** the roll nozzles, 38–39 °/s² there, sitting nearly unused.

## Change (MPO + SeparateRollYaw, pilot only; `MpoFreeRoll`)
- **Body-roll limit** = the higher of the surface caps (stock reference / steady surface rate, as at low AoA) and what the roll nozzles stop in `RollStopFactor` (1 s); never more than surfaces + nozzles stop in that time; the G-load factor and the 30 °/s floor as before.
- **Dropped for the body roll:** the braking floor, the high-AoA lateral margin, the fin-damage / engine-out yaw-share cuts (their stated reason is the sideslip a body roll makes with weak yaw).
- **Roll acceleration and brake** count the nozzles too (0.85 × (surfaces + nozzles) to build, RollBrakeUse × (surfaces + nozzles) to stop). The roll still stops when you let go.
- **Roll and yaw clamped on their own:** the yaw running into its limit (holding sideslip below the coupling AoA) no longer scales the roll down with it.
- **Unchanged:** roll + pedal together (velocity-vector roll, sideslip held), the pedals, the pitch law, VMC / asymmetric trim in the allocator, all modes other than MPO, `VelocityVectorRoll`, AI.

## Bench: your log's stick, MPO, replay (BENCH_MPO=1)
| Window | 1.1.1 roll | 1.1.2 roll | 1.1.2 sideslip (1.1.1) |
|---|---|---|---|
| 186–192 s (56–135 km/h, 90–120°) | 13 °/s plateau | 30–32 °/s plateau | +20 / −16° (−44°) |
| 197–199 s (150–190 km/h, 135–143°) | peak 48 °/s | peak 56 °/s | to +26° (+12°) |
| 210–214.9 s (30–45 km/h) | 12.9 °/s | 30.9 °/s | to −34° (−59°) |

**Curves (1.1.2):**
- **Onset:** smooth linear ramp, 0.6 → 8.5 → 16 → 24 → 30 °/s at 0.2 s steps (≈ 40 °/s²); 30.6 → 31.0 at the top, no overshoot past the limit, no dip.
- **Stop** (stick released at 214.9 s): 30.7 → 26 → 19 → 12 → 5 → −0.4 °/s in ~1 s, steady deceleration, no rebound.
- **Sideslip:** goes where the body roll takes it (that's the request). In these windows it came out lower than 1.1.1's, only because the faster roll swept the AoA through faster.
- **Bench vs game:** the bench's roll-nozzle authority at 30–45 km/h is 31 °/s² against 39 in your log, so the bench limit sat on the 30 °/s floor; in game expect ~38–39 °/s there.
- **Not checked:** MPO above ~250 km/h (the surface caps set the limit there, so little change is expected, not benched), damaged-fin MPO rolls, the regression suite (the change is gated to MPO + SeparateRollYaw; with the key off or outside MPO the law is identical by construction, and the AoA/G replays were identical).

## Install
- **Main:** `plugins\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` 1.1.2 (md5 4106588e4fa11468f949c05931365a98); 1.1.1 kept as `Ifrit_KR67_FCS.dll.1.1.1.bak`.
- **Dev:** unchanged.
- **Release:** `C:\Games\NO mods\Ifrit-FCS-1.1.2-release`.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.
- **To turn it off:** `Ifrit_KR67.FCS.cfg`, `[1. General] MpoFreeRoll = false` (takes effect at once).

Previous notes: `claude/Ifrit_FCS_1.1.1_notes.md`.
