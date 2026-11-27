# FS-12 Revoker FCS 1.6.6: the TVC-off AoA limit is exactly NoTvcAoACapPositive / Negative

Base: 1.6.5.

**Status:** compiled. Bench-checked on the cases below. **Not flown.**

## Tom (24 Nov 02:23-02:25)
"at 45 in dev settings it is 50 inflight. 30 in dev settings, it is 35 inflight", "at 25 it is 30".

## Cause
1.6.5 still added a thrust margin on afterburner: the positive TVC-off cap ran from `NoTvcAoACapPositive` at T/W 0.8 to `NoTvcAoACapPositive` + `NoTvcThrustAoAMarginPositive` (default 5) at T/W 1.0. So the setting was the limit only at low thrust; on afterburner it was 5 deg higher.

## Change
- With `DisableTVC`, `NoTvcAoACapPositive` is the positive limit at every thrust, and `NoTvcAoACapNegative` the negative one. No thrust step and no margin.
- `NoTvcThrustAoAMarginPositive` / `Negative` (new in 1.6.5) are removed; they are no longer read and the 1.6.5 migration that filled them is gone. The keys stay in an existing config file but do nothing.
- **Default behaviour changes:** TVC off on afterburner now holds 45 deg (1.6.5 and before: 50).

## Bench (Revoker, TVC off, 350 km/h, full aft 6 s with a 1.5 s full roll, then full push)

| Cap set | Afterburner: AoA max / min | Half throttle: AoA max / min |
|---|---|---|
| 25 / 25 | 25.1 / -24.7 | 25.2 / -24.9 |
| 30 / 30 | 30.2 / -29.2 | 30.2 / -29.8 |
| 45 / 45 | 46.3 / -43.4 | 46.3 / -40.1 |

The full regression suite was not rerun for this build (the change only touches the TVC-off caps).

## Not changed: surface chatter in TVC-off rolls (Tom, 02:25)
Investigated in the 02:21 log, not fixed in this build. In a held loaded roll (460 km/h, 30 deg, ~48 deg/s) the roll-rate cap swings 34-48 deg/s at ~2.3 Hz. The roll target follows it (+-5 deg/s), and the flaperons (+-20 deg) and rudders (+-14 deg) swing with it, while the roll rate holds within 2 deg/s.

The cap reads the estimator's yaw capacity toward the reversal side, `capZeN`, which swung 65-110 deg/s^2. That figure is the headroom left from where the effectors sit this tick, plus the measured yaw acceleration. On the Revoker with TVC off, most of it comes from the canards (57) and flaperons (41), with only 24 from the rudders. Those are the same surfaces the roll loop is moving, so the cap moves the surfaces and the surfaces move the cap.

Bench, rate cap off (`RollPaceRateBlend` 0): rudder and flaperon chatter about halves. A 0.3 s smoothing of the capacity cut the cap's swing 1.3 -> 0.3 deg/s in a replay, but it was dropped as an on-top fix (Tom, 02:34). The bench replay does not reproduce the game's magnitude (flaperons +-1 vs +-8 deg band).
