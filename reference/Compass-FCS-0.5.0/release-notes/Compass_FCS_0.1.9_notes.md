# T/A-30 Compass FCS 0.1.9: the asymmetric-load cap comes down faster after a part is shot off

Base: 0.1.8. Patch bump: one internal change, the same as F-22E 3.0.3. Compiled; benched; **not flown**. 0.1.8 is kept as `.dll.0.1.8.bak`.

## Tom (5 Nov, 21:21)
"check new logs" (`Compass_FCS_20261105_211644.csv`, 0.1.8)

## What the log shows
**The damage:**
| Time | Lost |
|---|---|
| 119.5 s | `aileron_R` |
| 120.5 s | `wingtip_R` |
| 121.5 s | `wing2_R` |
| 124 s | `vstab2_L` + `rudder_L` |
| 127.5 s | `elevator_L` |

**0.1.8 worked here:**
- The damage gate stayed on (`dmgGate` 1) from 120 s to the end, while `asymDy` wandered −0.14 to −0.28 m.
- The cap held at 7° from 130 s.
- 130–175 s: pulls and rolls at 95–150 m/s, AoA ≤ 7–11°, sideslip ≤ 4°.

**The departure at 180 s was a speed problem, not the cap:**
- 174–182 s, at idle (T/W 0.0) with full aft, the speed bled 99 → 45 m/s.
- At that speed the AoA needed for 1.3–2 g is 19–24°.
- The lone right slab was at its lift peak (local AoA 28–29°, `aDn` at its floor of 5) while the FCS asked for −30 to −45 °/s of pitch rate. Nothing was left to push with.
- After the stick was released at 182 s the jet was past trim. AoA 26 → 32°, it rolled and yawed away, sideslip 49°.

**What it means:** with this damage the jet can't be trimmed above ~10–12° AoA, so it has a minimum speed. That is roughly 100 m/s (360 km/h) for 1 g. Below that no control law can hold it.

**205.8 s and 214.5 s** (−150 and −220 kg, gear down) were the landing and the impact.

## The change: `AsymCapGapTighten`
- **What it does:** a cap that has to come down a long way comes down faster.
  - The tighten rate (`AsymCapTightenRate`, 10 °/s) is multiplied by gap / 3°, between 1× and 4×.
  - A 23° drop now starts at 40 °/s.
  - Gaps under 3° are unchanged.
- **Why:** at 10 °/s, 35 → 12° took 2.3 s. A full pull in that time outran the cap.
  - On the F-22E it was 5 s. The 21:09 Raptor log departed exactly this way, so this is the 3.0.3 change in the same form.

## Bench
### Damage mid-flight, then a pull
- **Sequence:**
  - Right outer wing at 0.3 s, then full aft 0.9 s later and roll reversals.
  - Then the left fin, then the left stab.
  - Run at 330 / 400 / 470 / 540 km/h, both roll directions.

| | 0.1.8 | **0.1.9** |
|---|---|---|
| Outer-wing phase | **6 of 8 depart** (AoA 33–39°) | **0 of 8** (AoA ≤ 14.3°, sideslip ≤ 4.9°) |
| Fin and stab phases | ok | ok |

- **The 3° reference was tried against 5° and 10°:** 5° held 5 of 8; 10° held 2 of 8 (the same as off).

### Regressions
- **133-case damage matrix:** identical to 0.1.8 (damage at t = 0, so the cap is already down).
- **Clean regression and roll cases:** bit-identical.
- **Asymmetric stores:** 27 cases identical.

## Still open
- **The damaged jet's minimum speed** is the limit seen at 180 s.
  - No warning is given for it.
  - It could go in the HUD readout or the visualizer if you want it.
- **Landing it:** keep the speed above ~100 m/s until close in, and the power up.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.9; 0.1.8 kept as `.dll.0.1.8.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.9.
- **Config:** no changes.
