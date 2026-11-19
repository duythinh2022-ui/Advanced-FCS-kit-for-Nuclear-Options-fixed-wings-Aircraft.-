# FS-20 Vortex FCS 0.3.8 (17 Nov 2026)

On 0.3.7. One fix, plus a diagnosis from Tom's 15:36 log.

## 1. STOVL touchdown: engine stuck at idle with the lever forward (fixed)

**Report:** touched down in STOVL with the lever up. The HUD kept showing ~34 kt commanded, the engine stayed at idle, and the jet would not accelerate until the lever was brought back and pushed up again.

**Log 15:36:**
- 49.0–49.7 s: the stick was full forward (sp −1), the jet was translating at ~5 m/s, and the lever was at 0.54.
- The wheels touched, so the FCS read it as a landing: touchdown idle latched, laws off, engine at idle (thrOut 0).
- 51.3 s: lever to 1.0. Nothing happened, and the HUD held its last command, "+34 KT" (gsCmd 17.6 m/s).
- The latch only released with the lever back at the aft detent (≤ 0.22). That happened at 60 s; the laws came back at 61 s and the jet accelerated.

**Fix** (`StovlSystem.GroundIntent`):
- The touchdown idle also releases when the lever is pushed 0.05 forward of the lowest position it has been at since the touchdown.
- While the idle holds, the HUD reads IDLE.
- On the wheels with the laws off it reads 0 GS, not a stale command.

**Bench**, full sequence:
- Hover, lever 0.54, stick full forward to touch down.
- The touchdown latches idle as before.
- Lever to 1.0 at 23.3 s: the laws are back within 0.2 s and the jet accelerates (23.8 → 49 kt by 27 s).
- CTOL case set: identical to 0.3.6.
- STOVL set: identical to 0.3.7.

## 2. "Lateral authority like a brick" at 55–60° AoA (diagnosed; fix in development, not in this build)

Every full-roll-stick episode in the 15:36 log above 45° AoA (CTOL, 55° and 60° limits):

| Start (s) | AoA | IAS (m/s) | Time to 10 / 20 deg/s velocity-vector roll | Mean pV (deg/s) | Max β | Unmet yaw (deg/s²) |
|---|---|---|---|---|---|---|
| 82.8 | 58–60 | 67 | 3.4 / 5.6 s | 13.9 | 6.8 | 54 |
| 99.8 | 55 | 75 | 2.4 s / never in 3.9 s | 9.1 | 11.5 | 89 |
| **111.1** | 46→55 | 77 (108→59) | **10.9 s / never in 13 s** | **2.4** | 7.9 | 76 |
| 132.7 | 55 | 70 | 2.2 / 4.8 s | 15.3 | 13.9 | 73 |

Below 40° AoA the same stick gives 20 deg/s in 0.3–0.6 s.

**The 111–120 s case, 8 s stuck:**
- The rudders sat in the conventional set (L +30 / R −30). The right fin was at 72° local AoA (stalled), and the jet yawed the **wrong** way: r −2 → −8 deg/s with right roll commanded.
- β held at 7–8°. The wing trailing edges were nearly symmetric.
- The allocator's sideslip-axis weight fell from 8 to 2.2 as β grew (the YawPrioHi sideslip gate).
- At 119.6 s the right outer elevon went trailing edge down to its stop (drag pair), and at 121.6 s the rudders crossed to the reversed set. pV then went 0 → 19 deg/s in 4 s.

**Cause:** the allocator sat in a local minimum of its own cost and left it only when the speed had bled from 108 to 66 m/s. This is the same mechanism found in the 14:21 log, but here it held for 8 s.

The bench reproduces it: full aft then roll at the limit at 300 km/h never reaches 20 deg/s in 9 s. With the pair pass in the managed solver (0.3.7 notes) the same case takes 2.4 s. The pair pass is not in the native solver, so it is still off.

Benched, not flown.
