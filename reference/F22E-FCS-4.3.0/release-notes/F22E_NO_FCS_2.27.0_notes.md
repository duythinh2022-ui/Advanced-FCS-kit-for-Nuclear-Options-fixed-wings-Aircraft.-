# F-22E FCS 2.27.0 — rudder window off, held push paced at 0.7 of the yaw capacity, push intent without the dwell

Base: 2.26.0. **Compiled + bench-checked (full suite) + replayed from the 14:23 pushes, not flown.** No player/Dev keys; CSV 306 columns.

## What Tom flew (2.26.0, 14:23 log: six released pushes 177–240 kt / 47–77 deg/s, four held 135–243 kt / 46–70)
"Smoother for the released push now, maybe a little hitchy. For the held push-over the pitch-down is too slow; the sideslip is being kept
too tight." Both read from the log:
- Released (69.5 s, 223 kt, 73 deg/s): body rate 12, 10, 6, 1, −6, −12, −16, −17, −16, −15, −15, −17, −20 … −59 — one curve with a 0.3 s
  plateau at 55–50°; sideslip ≤ 4°; body roll 33 → 51 → 3. The plateau: the roll stick came off 0.1 s before the push, the follow's
  0.15 s dwell held it off for three ticks, the command ran −24 through the kinematic cap and came back to −15 (nozzles −16 → +3).
- Held (154.6 s, 201 kt, 66 deg/s): nose to −30, then **0.4 s at zero body rate at 43–22°**, then −43. At 155.8 s the `RudderWindowSweep`
  pulled the left rudder back from 35° to 14° mid-brake (its local AoA −24° read as past the peak) while the yaw was still −28 deg/s;
  the capacity read fell 42 → 7, the kinematic cap 42 → 16 against +23 of flight-path rotation — the stop. On the released pushes the
  window never bound (rudders at the stop with 28–36° of local AoA). Held (125.4 s, 135 kt): sideslip ≤ 2° of the 4° margin the whole
  way, AoA rate 15–20 deg/s against +12…+15 of flight-path rotation: body rate −5…−10 — the slow pitch-down he means.

## Changes
1. **`RudderWindowSweep` off** (kept in source). Flown once; it took a working yaw brake away on the held pushes. The plateau-flipping of
   the rudder commands is real and still open; this is not its fix.
2. **`SweepYawK` 0.5 → 0.7.** Since 2.26.0 the kinematic sweep cap paces the *held* push only (the released push is the follow pace's), so
   this no longer touches the released case that the 2.23.0 bisect was about. The held push's AoA rate goes up ~40 % at the same capacity
   read; the sideslip may use more of the 4° margin. Bench: clean held pushes 560/600 peak unchanged (7.5 / 8.5, the top-of-leaf value),
   after-push sideslip 2.5 → 5.6 / 3.4 → 4.2, and the push now goes through to −19 / −16° AoA where it stopped at −6 / −11; one-fin held
   560 3.6 → 9.3 (one-fin held 600 5.5 → 4.9); push+roll 300 2.7 → 3.2, pull-then-pushover 350 4.2 → 4.8, push-then-pullthrough 450 6.0 → 8.3,
   pullthrough-released 350 5.5 → 4.4.
3. **The push intent engages the follow without the dwell** (the dwell is the reversal-crossing guard; a reversal does not push).

Suite vs 2.26.0 otherwise: reversals back to 2.25.0's figures (±0.3), cork ±1, utilisation identical, leaf cases ±0.5.

## What to expect
Released push: as 2.26.0 (one curve, roll about its release value then to rest, sideslip small) with the start plateau gone when the roll
stick comes off together with the push. Held push: the nose comes down at roughly 1.4× the 2.26.0 rate; sideslip up to the 4° margin
(it was ≤ 2°); the 0.4 s stop at 43–22° should be gone with the window (if a stop remains there with the rudders at the stop, it is the
capacity read, not the window). `LogOutput.log` must say 2.27.0.
