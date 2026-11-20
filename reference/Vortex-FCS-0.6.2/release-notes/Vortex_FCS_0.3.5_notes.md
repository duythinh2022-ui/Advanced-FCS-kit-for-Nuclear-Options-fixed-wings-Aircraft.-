# FS-20 Vortex FCS 0.3.5 — STOVL mode AoA limit 24 deg; no conversion above it

Build of 17 Nov 2026. **Benched, not flown.** On 0.3.4.

## What changed (Tom, 17 Nov: "lower the aoa limit to 24 when stovl is activated, when aoa is above that, stovl is blocked")
- **AoA limit 24 deg each way** (`StovlAoALimit`, main config "1. General") from the conversion request until the jet is back in CTOL.
  - Covers doors opening, STOVL mode at any speed, and reconversion.
  - The CTOL limits (+70 / -70) come back when the reconversion completes.
- **No conversion above it:** a Flight Assist hold with |AoA| over 24 deg shows "STOVL conversion inhibited: AoA over 24 deg" and does nothing. The automatic conversion (`StovlAutoTransition`) waits for the AoA too. On the wheels: no check.
- `StovlAoALimit` 0 = 0.3.4 (CTOL limits in STOVL mode).

## Why (13:29 log, 169-184 s)
- STOVL mode at speed (doors open, CTOL laws), lever at zero, full aft stick in a 70 deg bank.
- The AoA reached 50 deg at 63 m/s; sideslip ran to +24, then +45, and the nose went 80 deg down.
- Not the conversion transient: the conversion had finished at 143.5 s and nothing changed mode afterwards.
- **Bench, same inputs from 395 km/h with zero thrust:**
  - STOVL mode bled 98 -> 59 m/s in 13 s, CTOL at idle 99 -> 70 m/s, so the jet reached 44 deg AoA at 60-67 m/s instead of 80-83.
  - The rudders ran out of yaw there: 13.6 deg of sideslip in STOVL mode against 1.6 in CTOL.

## Bench
- **The departure case with the limit:** AoA rises smoothly to 24.1 and holds 23.9-24.1. Max sideslip 3.2 deg (0.3.4: 13.6). Pitch never under -1 deg (0.3.4: -31), bank held at 56-70 deg. 69 m/s after 12 s.
- **Conversion requested at 54-90 deg AoA:** inhibited, the jet stays in CTOL.
- **STOVL set** (hover, vertical landing, vertical take-off, conversion and decel, transition): identical (none goes past 11.4 deg AoA).
- **CTOL** (asymmetric-load rolls, velocity-vector rolls, falling leaf and reversal): identical.

## Not checked
- A conversion with the AoA just under 24 and the stick still pulling: the limit holds it at 24 from the request on; not benched.
- Not flown.

## Install
- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.3.5; 0.3.4 kept as `Vortex_FS20_FCS.dll.0.3.4.bak`. `meta.json` updated.
- New main-config key `StovlAoALimit` (24).
