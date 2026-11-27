# FS-12 Revoker FCS 1.6.5: the TVC-off AoA limit follows NoTvcAoACapPositive / Negative

Base: 1.6.4.

**Status:** compiled. Bench-checked on one case (below). **Not flown.**

## Tom (24 Nov 02:10)
"i set it to 30 and it is still 50 fix that". In his dev file: `NoTvcAoACapPositive = 30`, `NoTvcAoACapNegative = 30`, `DisableTVC = true`.

## Why it stayed at 50
With `DisableTVC` the positive cap ran from `NoTvcAoACapPositive` at T/W 0.8 to the larger of `NoTvcAoACapPositive` and `NoTvcThrustAoACapPositive` (50) at T/W 1.0. On afterburner, lowering `NoTvcAoACapPositive` therefore changed nothing.

The negative cap with `DisableTVC` was `NoTvcThrustAoACapNegative` (45) at every thrust, also ignoring `NoTvcAoACapNegative`. The 02:06 log shows it: cap 50, AoA held at 49.9-51.1 at full aft.

## Change
- **`NoTvcAoACapPositive` / `Negative` are now the TVC-off limit.** With `DisableTVC` the thrust only adds a margin on top:
  - `NoTvcThrustAoAMarginPositive` (new, default 5): positive cap = `NoTvcAoACapPositive` + margin x (T/W 0.8 -> 1.0, linear).
  - `NoTvcThrustAoAMarginNegative` (new, default 0): negative cap = `NoTvcAoACapNegative` + margin, at every thrust.
- **Defaults are unchanged:** 45 -> 50 positive at afterburner, 45 negative.
- **Retired keys:** `NoTvcThrustAoACapPositive` / `Negative` are no longer read (marked RETIRED in their descriptions). A value on them is moved into the margins once (value - 45; config revision 17). Tom's file: 50 -> +5, 45 -> 0.
- **Tom's settings now give:** with TVC off, +30 at low thrust rising to +35 on afterburner, and -30.

## Bench (Revoker, TVC off, afterburner, 350 km/h, full aft 5 s then full push)

| Setting | AoA max / min | Cap |
|---|---|---|
| Defaults | 48.8 / -42.0 | 45 -> 50 |
| Caps 30 / 30 (margin +5) | 34.4 / -29.6 | 30 -> 35 |

## Not changed
The HUD's AoA-limit marker still shows `AoALimitPositive` (70), not the live TVC-off / low-thrust cap. That is a display fix, offered separately.
