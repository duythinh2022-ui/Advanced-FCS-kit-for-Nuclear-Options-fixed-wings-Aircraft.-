# F-22E Strike Raptor FCS 4.8.10 (26 Nov 2026)

Ask: Tom 26 Nov 02:01: "i set negative aoa limit to 55 with no tvc and it is still limited to 45. I want the jet aoa limit to be what it is when i set it." Bench only; not flown.

## Cause
- `NoTvcAoACapNegative` and `NoTvcThrustAoACapNegative` were clamped to 10-45 deg in their getters, so 55 read as 45.
- With DisableTVC the negative authority-cap target was `NoTvcThrustAoACapNegative`, then held to `AoALimitNegative` (50, the limit with the nozzles on). So a setting above 50 could never apply either.
- Positive side: `NoTvcAoACapPositive` was clamped to 65, and the limit was min(`AoALimitPositive`, no-TVC cap).

## Change
- With DisableTVC (and `NoTvcCapIsHardLimit` true, the default), `NoTvcAoACapPositive` / `NoTvcAoACapNegative` are the AoA limits, both signs. `AoALimitPositive` / `AoALimitNegative` are the limits with the nozzles on.
- The no-TVC caps' getter clamps are 10-90 (were 10-45 negative, 10-65 positive).
- The negative cap with DisableTVC uses `NoTvcAoACapNegative` (as the positive side has since 4.8.3), not the thrust cap.
- Unchanged: the swept authority cap still lowers a limit where pitch authority runs out; the AoALimit debug test switch (Full65 / Cap36) still overrides.

## Bench, two-zone, TVC off, full forward stick held 8 s (then a full roll left/right at 3-7 s in the roll runs)
| | -45 limit | -55 limit |
|---|---|---|
| 250 km/h hold, AoA min / mean | -45.2 / -43.2 | -53.7 / -49.5 |
| 350 km/h hold | -45.1 / -43.1 | -53.6 / -51.8 |
| 450 km/h hold | -26.4 (g-limited) | -26.4 (g-limited) |
| Rolling push, worst sideslip 250 / 350 km/h | 5.7 / 5.2 | 9.1 / 9.0 |
| AoA 3 s after release, 250 / 350 km/h | -12.1 / -11.7 | -18.2 / -17.0 |

No departure in any run. At -55 the rolling push makes about 4 deg more sideslip than at -45 (the 2.2.4 note on AoALimitNegative: past about -50 the canted fins are past their stall at negative AoA).
