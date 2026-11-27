# F-22E Strike Raptor FCS 4.8.14 (Nuclear Option)

Base: 4.8.13. Bench only, not flown. Asked for: fix the no-TVC AoA overshoot past the +55 limit (26 Nov 10:34 log). Sideslip left alone ("good enough").

## What the logs showed
- **The 4.8.13 flight (10:34):** 12 of 43 no-TVC approaches went past 55.5 deg and 7 past 57, with peaks of 56.4-61.7.
- **The 4.8.11 flight (02:56):** 15 of 35 approaches past 55.5, up to 64.4. So the overshoot is as old as the +55 limit, not the 20 deg stab.
- **Almost every event is the same move:** a full-aft pull from 400-500 km/h at full thrust, with full roll stick going in around 40-52 deg AoA.
  - Near 55 deg the jet has only 10-30 deg/s² of nose-down left.
  - The roll takes the stabs, and its inertial pitch-up adds 15-20 deg/s² nose-up.
- **One event (949 s) was a roll reversal right at the limit.**

## Causes (all in the law)
1. **The rolling AoA capture over-counted its braking.**
   - The pitch share of the nose-down while rolling had a floor of 20 deg/s², against a real 3-10.
   - The capture started counting the roll only once the roll rate passed 10 deg/s, 0.2-0.4 s after the stick went in.
2. **The roll's coupling allowance used a lagging reserve and had a floor.**
   - It was sized from a 0.3 s-filtered reserve at the present AoA. That reserve lags an authority that falls 150 → 20 deg/s² from 40 to 55 deg.
   - Its 5 deg/s² floor (about 26 deg/s of roll) spent nose-down that wasn't there at the limit.
3. **The lateral stab reserve didn't yield to braking.** It held back 70 % of the stabs whatever the AoA stop needed. 1.8.19's rule was that braking to the limits always gets full authority; this puts that rule back, in terms of need.

## Changes (`FcsController`, bench switches, all on)
- **`CaptureRollShare`**
  - No floor on the pitch share.
  - The rolling capture brakes with the planned braking × the pitch share the reserve leaves, minus the measured inertial pitch-up.
  - This acts from the roll plan, so there's no 10 deg/s wait.
- **`CoupAtLimit`**
  - The coupling split's reserve is scaled to what the jet will have where the AoA is headed, α + α̇²/(2 aDn) capped at the target.
  - The scale is the smaller of the full-net swept ratio and the measured falloff of the live estimate, exp(s · gap).
  - s = d ln(aDn)/dα, measured as the AoA moves.
  - At the limit, or in a held AoA, it is 1, so it changes nothing there.
- **`CoupExactSplit`:** the roll gets no coupling floor past what is spare once the AoA stop is paid for.
- **`LatReserveByNeed`:** the lateral stab reserve is capped so pitch keeps what stopping the AoA at its limit needs, α̇²/(2 · margin).
- No settings, airframe or Rust core changes.

## Bench (game mass 29.2 t and CG 0.05 m aft, fin blanking on)
The bench at your logged mass and CG reproduces the overshoot. A replay of the 29.6 s event matches your stab trim through the pull.

| Case | 4.8.13 | 4.8.14 |
|---|---|---|
| Full aft + full roll at about 48 or 40 deg, 330-500 km/h (10 cases): worst / mean peak | 65.7 / 59.0 | 55.4 / 55.2 |
| Pitch stick released at 50 deg + full roll (5 cases) | about 57 | 53.0-53.9 |
| Straight full-aft pull | 55.0-55.1 | same, same timing |
| Roll at the limit + reversal (12 cases): mean / worst | 61.0 / 64.8 | 58.9 / 60.5 |
| Replays of 9 log windows | 59.1-70.1 | lower in every window (for example 69.7 → 63.4, 70.1 → 62.2, 66.9 → 57.4) |

Bench replays overshoot more than your jet did (later windows by up to 10 deg), so the bench is the harsher of the two.

## What it costs (bench)
- **Pull + full roll from the start:** 50 deg AoA reached 0.3-0.5 s later, with 2-4 deg/s more roll rate.
- **Corkscrew 220 km/h:** roll-rate dip in the held push 22.7 vs 13.9 deg/s (5-speed mean), pitch-over 0.4 s faster.
  - Release is much better: roll stopped in 0.45 vs 0.80 s; AoA back above −10 in 1.0 s, where 4.8.13 never got there; unmet roll 0.2 vs 23.
- **Rolling pull to the 36/45 caps:** stops 0.3/0.8 deg shorter. Slip at 45: 4.4 → 4.5.
- **No-TVC, TVC and low/mid-AoA batteries:** roll rates within 1.6 deg/s. Pushes the same.

## Sideslip (5-speed means, deg)
| Case | 4.8.13 | 4.8.14 |
|---|---|---|
| TVC reversal 36 | 3.6 | 3.7 |
| TVC idle pull-release-roll 45 | 7.4 | 6.1 |
| TVC slow roll 36 | 5.8 | 6.1 |
| No-TVC slow roll 36 | 6.9 | 6.5 |
| Corkscrew 300 | 7.4 | 5.1 |
| Corkscrew 220 | 5.8 | 5.6 |

## Still open
- A sustained full roll or a reversal right at the 55 limit still runs 2-5 deg past it at low speed. At 56 deg and about 300 km/h the jet has 5-14 deg/s² of nose-down.
  - When a roll stops there, the body yaw it leaves behind has to be stopped too. With the fins blanked, the allocator uses the stabs for that yaw and pitch goes unmet. That's a yaw-versus-pitch priority question, not changed here.

## Bench method notes
- The bench's default mass and CG (27.5 t, 0.155 m forward) didn't reproduce the overshoot. Use `MassTarget=29200 FuelFrac=1 CgAftShift=0.05` for this jet as you fly it.
- The bench's own default has the fins unblanked. `tools/tzf.env` now adds the shipped fin blanking.

## Going back
Reinstall `Aryx_F22E_StrikeRaptor_FCS.dll.4.8.13.bak`.
