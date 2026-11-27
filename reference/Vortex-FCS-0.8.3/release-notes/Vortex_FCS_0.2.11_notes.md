# FS-20 Vortex FCS 0.2.11 — STOVL conversion guard is a hard guard

Build of 17 Nov 2026. **Benched, not flown.** On top of 0.2.10; no law changes.

## What changed
- **No override for "UNSAFE STOVL - DROP LEFT/RIGHT WPN".**
  - 0.2.8-0.2.10 converted anyway on a second Flight Assist hold within 10 s. That path is gone.
  - Now every hold shows the warning and does nothing.
  - The way to convert is to drop or jettison until the jet is balanced enough.
  - The check itself is unchanged: the lateral CG's roll trim against 90 % of the roll posts' hover authority (`HoverRollTrim`, GuardShare).
- **The automatic conversion is held back too.** This only applies with `StovlAutoTransition`, which is off by default; before, it bypassed the guard.
  - The guard's mass and CG are now refreshed every tick, not only on the button press.
- **The log line is rate-limited.** "conversion blocked ..." is written at most once per 10 s; the HUD warning shows on every press.
- **Unchanged:** the weapon release guard ("UNSAFE WPN RELEASE - HOLD 1.5s") keeps its hold-to-drop override, so you can always shed the store that is blocking the conversion. Jettison is never blocked.

## Bench (`stovl`, 300 km/h, store mass added on wingtip_L)
| Store on wingtip_L | Presses | Result |
|---|---|---|
| 300 kg | t = 1 s | converts |
| 600 kg | t = 1 s | blocked |
| 900 kg | t = 1 s | blocked |
| 1500 kg | t = 1, 4, 7 s | all three blocked (0.2.10 converted on the 2nd) |

STOVL suite (hover, VL, VTO, STO, mission): output identical to 0.2.10.
