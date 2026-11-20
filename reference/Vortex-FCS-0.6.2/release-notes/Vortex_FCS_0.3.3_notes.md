# FS-20 Vortex FCS 0.3.3 — STOVL lever: the aft detent is zero thrust at speed

Build of 17 Nov 2026. **Benched, not flown.** On 0.3.2. One change, plus findings from the 13:29 log (no change for them yet).

## What changed
- **STOVL at speed (the CTOL blend, above ~60 kt, lever = thrust):** the lever's zero is now the aft detent.
  - From the detent's forward edge (`StovlAftDetent` + `StovlDetentWidth`, 0.22) to the stop the lever is idle .. full dry. At the detent and behind it: idle.
  - 0.2.9-0.3.2 spread idle .. full dry over the whole travel, so the lever parked at the detent by the conversion (20 %) gave ~18 % thrust.
  - The HUD's "THR %" reads the same scale (0 % at the detent).
  - Still applies: the lift-share thrust floor (the wing cannot carry the jet below ~75 m/s, so thrust comes back there), no afterburner in STOVL mode.
- Hover regime and the speed-lever regime (below ~60 kt): unchanged.

## Log 13:29 (0.3.2)
- **144-168 s:** STOVL mode at 360 km/h, lever 0.20 (set by the conversion), engine 0.18 of the dry range, the CTOL blend fully on. The jet held ~92-110 m/s instead of slowing.

## Bench
- **Conversion at 380 km/h, lever left at the detent** (`stovl` decel): 0.3.2 accelerated 106 -> 125 m/s over 75 s (thrust 0.18). 0.3.3: thrust 0 down to ~75 m/s, then the lift-share floor; 110 -> 42 m/s in 70 s.
- **Transition with lever 0.5 / 0.8** (`stovl` trans): 75 -> 64 m/s at 112 s (lever 0.8 is now 0.77 of dry, was 0.8). Reconversion the same.
- **Hover, vertical landing, vertical take-off:** identical.
- CTOL: no change (the code only runs in STOVL mode).

## Findings, not changed

### Sideslip in the loaded left roll (92.5-98 s, 565 km/h, AoA 10 on the load limit)
- Yaw rate commanded -24 to -31 deg/s, flown -3 to -12. The allocator's yaw target ~-45 deg/s^2; its own unmet report -36 to -53 (almost all of it).
- Roll was further short: commanded -97 deg/s, flown -50 to -70, unmet -175 to -270 deg/s^2 with the elevons at +-30.
- Rudders: L -2 to -11, R 0 to -4. Their yaw (differential) part was 0-5 deg of 30; the rest was the common (pitch) part.
- With left pedal (98.3 s): L -15 to -30, R +6 to +11 (differential ~-13 deg). Yaw rate -16 to -19 deg/s, sideslip -8 -> +9.5 in 4 s, AoA unchanged (10.3-10.6). Roll first dropped (-50 -> -39 deg/s), then -60 to -100 as the sideslip went over.
- **Why the allocator did not do it:** it minimises the sum of axis weight x (shortfall)^2.
  - Roll 1 x 230^2 ~ 53 000 against yaw 8 x 45^2 ~ 16 000: the roll term is the bigger one even at weight 1.
  - The rudders sit on canted fins, so a nose-left differential also rolls the jet right. With the roll that far short, the solver keeps it.
  - Pedal doubled the yaw demand (rate command -41, target ~-90 deg/s^2), which quadruples the yaw term; then the differential pays.
  - It is self-defeating: the sideslip it lets build costs more roll (dihedral effect) than the rudders would have.
- **Bench reproduction** (same load and speed, full aft + full left roll, 1.5 s of right pedal to -12 deg sideslip, then released):
  - `YawAxisPriority` 8 (shipped): no rudder differential, sideslip -12 -> -3 over 5 s, roll -50 to -65 deg/s.
  - 30: differential L -6..-8 / R +4..+5, sideslip -12 -> 0 in ~3 s, roll -78 deg/s.
- **Not shipped:** `YawAxisPriority` also sets the yaw weight at high AoA. Regression suite, 8 / 16 / 30:
  - 65 deg AoA roll release: max sideslip 2.2 / 3.4 / 6.4
  - Pull-through, roll released at zero: 2.1 / 4.1 / 3.4
  - Full aft + roll at 250: 1.3 / 1.7 / 2.4
  - Stop / reversal sweep, worst: 4.6, 5.8 / 4.5, 6.0 / 4.2, 5.8 (mean 3.21, 4.31 / 3.02, 4.17 / 2.95, 4.04)
- **Fix options (shared logic, all seven jets):**
  - (a) Before the solve, hold each axis's demand to what the effectors can reach, so an unreachable roll demand stops outweighing yaw.
  - (b) Raise the yaw weight with sideslip at low AoA.
  - (c) A separate low-AoA yaw priority.

### Departure (169-184 s)
- STOVL mode at speed (CTOL laws, lift-fan doors open), lever to 0 at 169 s: thrust 0, so no pitch TVC either.
- **172-173 s:** a roll to 70 deg of left bank with -10 deg of sideslip: the same trade as above.
- **174-179 s:** full aft stick held in the 70 deg bank.
  - AoA 13 -> 50 deg while IAS fell 100 -> 63 m/s (the AoA limit in STOVL mode is the CTOL 70).
  - Sideslip +2 -> +24 deg. The yaw rate stayed -10 to -19 deg/s (nose left) while the command went from -18 to +15 deg/s; from 177 s the yaw was saturated (unmet right yaw 60-100 deg/s^2).
  - Above 20 deg AoA with sideslip the high-AoA gate (`YawPrioHi`) took the yaw weight from 8 to 2.
- **180-182 s:** nose 60-83 deg down, sideslip to +45.

## Install
- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.3.3; 0.3.2 kept as `Vortex_FS20_FCS.dll.0.3.2.bak`. `meta.json` updated.
- No config change.
