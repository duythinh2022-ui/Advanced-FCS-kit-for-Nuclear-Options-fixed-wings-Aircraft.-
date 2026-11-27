# Why the Multirole MTD gets less low-thrust AoA than the stock Ifrit (bench, 23 Nov)

Tom (23:49): "massive canards but worse aoa" — look into it again.

**Result:** max usable AoA at idle / 0.3 T/W is 55 deg on the stock Ifrit and 45 on the MTD. The limiting case on the MTD is a loaded roll entered at 450 km/h at 0.3 T/W, held at the AoA limit. Nothing here is flown; all numbers are from the bench with the airframe built from the blueprint (`mtd_build.py`).

## 1. Static derivatives, neutral surfaces, 250 km/h, idle (`ibeta`)
Pitch = pitch acceleration at zero sideslip (+ = nose up). Yaw stiffness = yaw acceleration per deg of sideslip. Roll from slip = roll acceleration per deg of sideslip. All in deg/s^2.

| airframe | AoA | pitch | yaw stiffness | roll from slip |
|---|---|---|---|---|
| stock | 45 | -12.8 | +0.52 | -0.20 |
| MTD | 45 | **+30.0** | **+0.28** | -0.50 |
| MTD, canards removed | 45 | -31.2 | +0.53 | -0.30 |
| MTD, canards halved (7.5 m^2) | 45 | -0.7 | +0.40 | -0.40 |

What the canards do (15 m^2 each, 3.74 m ahead of the CG, mounted on the canted tail stations):
- **Pitch:** about 60 deg/s^2 nose-up, at every AoA from 20 to 65. With the canards the airframe is nose-up out of trim (+30); without them it is nose-down (-31). At high AoA a canard's nose-down authority is only unloading its own lift, so most of its travel goes on cancelling its own trim load. It is not a large nose-down control.
- **Yaw:** directional stiffness down 45 % (0.53 -> 0.28 at 45 deg). Their side-projected area sits ahead of the CG.
- **Roll from sideslip:** about 65 % more (-0.30 -> -0.50 at 45 deg).

## 2. Walking the airframe from stock to MTD (stock law, cap 55, 0.3 T/W, roll entered at 450 km/h)

| airframe | IAS when the roll starts | lowest IAS | peak sideslip |
|---|---|---|---|
| stock | 74 m/s | 25 m/s (89 km/h) | 7.9 |
| + MTD tails (10 m^2) | 63 | 27 (98 km/h) | 11.9 |
| + MTD wings (inner 10 -> 22 m^2, outer 7 -> 10) | 54 | 18 (66 km/h) | 37.3 |
| + canards (full MTD) | 51 | **13 (48 km/h)** | **72.7** |
| full MTD, 1.7.0 law | — | — | 29.7 |

- The sideslip runs away at the lowest airspeed, after the roll. On the MTD that is 13 m/s (48 km/h): dynamic pressure about 1/5 of the stock jet's at its lowest point.
- At 0.3 T/W the nozzles cannot stop it, and neither can the surfaces at that dynamic pressure.
- At 55 deg AoA the MTD has about 94 m^2 of lifting area (wings 64 + canards 30) against the stock jet's 34. It sheds speed much faster at the same AoA: 51 vs 74 m/s just 4 s into the pull. The big wing also keeps it flying, so the AoA hold carries it down to 48 km/h.
- The canards then add their nose-up trim load (less spare travel), less yaw stiffness, and more roll from sideslip, on top of the drag.

## Conclusion
The canards are not a nose-down control at high AoA, and the bigger wings cost airspeed. So the MTD's 45 deg at low thrust comes from the airframe, not from the FCS. With TVC off at AB (speed kept up) the MTD is clean at 65 deg, like the stock jet.

## Reproduce
- Static derivatives: `bench.dll ibeta ias=250 thr=0.1` with `BENCH_DATA` pointing at each variant JSON.
- Variants were made by editing part `wingArea` in `mtd_airframe.json` (canards 0.01 / 7.5; wings 10/7; tails 6).
- Dynamic case: `ifly thr=0.3 ias=450 T=11 pitch=0:0,1:0,1.02:1,11:1 roll=0:0,5:0,5.02:1,9:1,9.02:0 NoTvcAoACapPos=55 StabLatShareCanards=0 ADnStabShareCanards=false AoALimitPositiveCanards=65`.
