# FS-20 Vortex FCS 0.3.9 (17 Nov 2026)

On 0.3.8. High-AoA lateral allocation (Tom, 15:36 log and 15:47–15:55 messages), the STOVL sideways start and stop margin, and the STOVL yaw-rate limit derived from authority (16:06). Vortex-only. The native core (`Vortex_FcsCore`, ABI 2013001) is rebuilt and embedded.

## 1. Allocator pair search (`AllocatorPairPass`, on)

**Problem** (0.3.8 notes §2): at 55–60° AoA the roll stuck for up to 8 s (15:36 log, 111–120 s, pV mean 2.4 deg/s).
- The rudders sat in the conventional set, and at that AoA it rolls the jet the wrong way: roll −26 / yaw +9 deg/s² on the bench plant.
- The wing trailing edges stayed near symmetric.

**What the trailing edges can do instead.** Bench plant, 55° AoA, 82 m/s:

| Pose | Roll (deg/s²) | Yaw (deg/s²) |
|---|---|---|
| Full drag differential | −112 | +42 |
| Best coordinated pose with the rudders centred (left inner +30, right inner −30, right outer +30, left outer −10) | +7 | +10 |

The coordinated pose gives pS +12 deg/s² with no sideslip build-up.

**Cause:** the solver's passes are local (Gauss-Newton) or global in one surface at a time. The useful moves need two surfaces at once: the roll-side inner trailing edge down for drag with the outer up to cancel its roll, or the rudders crossing to the reversed set.

**Fix, inside the solve** (`Allocator.PairPassRun`, Rust `pair_pass`):
- After the per-surface pass, every pair of surfaces is searched over both grids together: every other point, then the 8 points round the best.
- The score is the solve's own cost, taken half at the end and half averaged over the servo transit.
- The best pair move is kept.
- No AoA gate, no fixed poses.
- Native vs managed on the bench: 4042 of 4042 checked solves identical (strict float).

## 2. Managed sideslip band (`SlipBandAllocation`, on; `SlipBand` 2, `SlipBandSpan` 3, `SlipBandGain` 6)

**Problem:** with the velocity-frame allocator, the high-AoA yaw weight is the sideslip-acceleration weight. The 2.1.10 gate it inherited lowers that weight as sideslip grows; in the 15:36 log it fell 8 → 2.2 while sideslip held at 7–8°. That is backwards here.

**Fix:**
- The weight is now flat inside ±2° (sideslip + 0.3 s lead) and rises to 7× by 5°.
- Some sideslip is spent for velocity-vector roll and bought back first past the band.
- Tom asked for sideslip close to the Raptor's: falling leaf peaks 4.6–5.7°, corkscrews ~3.7°. The bench peaks below are in that range.

## 3. STOVL sideways start and stop margin 20 % less (`StovlTrcLagMargin` 0.32, was 0.4)

The hover's side-velocity profile leaves room for the bank loop's lag when it starts and stops a sideways translation. That room is now 0.32 s.

Bench, full side stick 5 s in the hover:

| | 0.4 s | 0.32 s |
|---|---|---|
| Time to 90 % of 15 kt | 3.90 s | 3.85 s |
| Stop to 0.3 m/s | 4.23 s | 4.08 s |
| Overshoot | 0 | 0.06 m/s |
| Bank peak | 15.8° | 16.0° |

The bank rate (`StovlTrcBankRate` 10 deg/s) is what mostly sets those times. That is the knob if it should feel quicker.

## 4. STOVL pedal yaw rate: no fixed limit (`StovlYawStopTime` 1.84 s)

**Was:** full pedal asked for a fixed 30 deg/s (`StovlHoverYawRate`, not scheduled). The ramp to it, and the stop, ran at the scheduled yaw authority (75 % of the table authority).

**Now:** full pedal asks for the yaw rate the axis can start, and stop, within 1.84 s at the authority it has this tick. 1.84 s comes from the bench: a clean jet with full fuel (11 880 kg) has 16.3 deg/s² of yaw in the hover, and 16.3 × 1.84 = 30 deg/s, the old value.

Bench, full pedal 6 s in the hover:

| Mass | Yaw authority (deg/s²) | Limit (deg/s) | Peak r (deg/s) | Time to 90 % | Stop |
|---|---|---|---|---|---|
| 10 200 kg (40 % fuel) | 15.9 | 29.2 | 29.6 | 1.63 s | 1.70 s |
| 11 880 kg (clean, full fuel) | 16.7 | 30.7 | 31.0 | 1.63 s | 1.70 s |
| 12 600 kg | 10.5 | 19.4 | 19.6 | 1.58 s | 1.63 s |

- The start and stop now take the same time at every mass; the rate follows the authority.
- The lighter jet does not get more rate: the nozzle's yaw bearing gives less moment at its lower hover thrust, which roughly cancels its lower inertia.
- `StovlYawStopTime` 0 = the fixed 30 deg/s of 0.3.8.

## Bench

**Max-AoA roll set** (full aft; roll during the pull-up both ways at 300 / 420 / 550 km/h, and roll at the limit at 300 / 420 / 550; native solve):

| | 0.3.8 | 0.3.9 |
|---|---|---|
| 60° limit: time to 20 deg/s pV, mean | 2.61 s | 1.46 s |
| 60° limit: roll at the limit, 300 / 420 / 550 km/h | never in 9 s / 4.45 / 2.23 s | 2.37 / 1.38 / 1.25 s |
| 60° limit: max β mean (worst) | 4.0 (5.5) | 4.1 (5.5) |
| 55° limit: time to 20 deg/s, mean | 2.15 s | 1.23 s |
| 55° limit: roll at the limit, 300 / 420 / 550 km/h | never / 2.22 / 1.60 s | 1.83 / 1.03 / 1.20 s |
| 55° limit: max β mean (worst) | 4.7 (7.1) | 4.3 (6.0) |

The bench reproduces the 15:36 stuck roll (300 km/h at the limit, never reaching 20 deg/s in 9 s). With 0.3.9 that case takes 1.8–2.4 s.

**CTOL case set vs 0.3.6/0.3.8.** The pair pass acts at every AoA:
- Heavy one-sided load: left roll bank in 5 s 320 → 343°; others within 5°; sideslip within 0.7°.
- VV rolls: 0.35 pull 171 / 90 → 174 / 96 deg/s; 0.6 pull 38 / 11 → 39 / 14, β 5.0 → 4.2; full aft 7.8 / −5.9 → 6.8 / −8.9, β 3.3 → 3.5.
- Falling leaf: yaw rate 35.7 → 34.7 deg/s, stop 1.95 → 1.73 s.

**STOVL set vs 0.3.8** (before the yaw-rate change; with it, hover pedal peaks 29.9 / 31.0 → 30.7 / 32.1 deg/s, transition heading within 2°):
- Decel, VL, VTO: identical or within 0.2 m.
- Hover (with the side-stick inputs): position within 0.65 m, bank within 1.3°.
- Transition: path within 10 m over 150 s, bank within 1.2°.

**Cost:** not measurable above the bench's timing noise. The in-game timing log line "allocator NN us" will show it.

Benched, not flown.
