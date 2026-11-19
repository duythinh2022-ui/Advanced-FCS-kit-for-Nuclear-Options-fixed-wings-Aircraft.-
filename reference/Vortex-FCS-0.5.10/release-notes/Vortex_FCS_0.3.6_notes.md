# FS-20 Vortex FCS 0.3.6 (17 Nov 2026)

On 0.3.5. Two changes from Tom's 14:21 flight, plus a diagnosis with no fix.

## 1. Throttle snap after leaving STOVL at full lever (fixed)

**Report:** in STOVL at full lever, STOVL off → CTOL at max afterburner (correct). The first throttle-up press then snapped the throttle back to MIL and it ramped back up to AB.

**Log (14:21, 405.06 s):** `thrIn` 1.00 → 0.900 in one frame, then back to 1.00 by 405.45 s.

**Cause:** the Detents mod (baanish, NuclearOptionDetents 0.5.1), not the FCS.
- While STOVL is active the FCS suspends Detents through its auto-hover predicate (`ThrottleDetent.StovlSuspendsDetents`), so the lever works as the STOVL thrust lever.
- When the suspend ends, Detents runs `Reset()` on its afterburner detent and it goes back to Locked.
- Detents' FS-20 preset has the AB boundary at 0.90. With the detent Locked and the lever above it, the next throttle-up press holds the throttle at 0.90 for its 200 ms hold, then unlocks. That is the 1.00 → 0.90 → 1.00 in the log.

**Fix:**
- Leaving STOVL with the lever in the AB range latches the suspend (`StovlSystem.detentsAbLatch`).
- The suspend stays on until the lever goes under `DetentsAbRelease` = 0.86. That is the 0.90 boundary minus Detents' 0.02 reset hysteresis, minus a margin.
- Detents then re-arms with the lever on the side the detent guards. Pushing back up from there holds at the AB detent as usual.

**Verification:** compiled only. The bench has no Detents mod, so this needs a flight: STOVL at full lever → off → press throttle-up. Expected result: no change from 1.00.

## 2. AoA limit +60 / −55 (new defaults, moved once)

- `AoALimitPositive` 70 → 60. `AoALimitNegative` was already 55 (since 0.2.7).
- One-time move: `AoALimitNegativeRevision` 1 → 2 sets both to 60 / 55 whatever the file holds. A value set after that is kept.
- STOVL keeps its own 24° cap (`StovlAoALimit`, 0.3.5).

**Bench**, same build, default 60 vs `AoAPos=70`:

| Case | 70 | 60 |
|---|---|---|
| CTOL case set (asym L/R/300/1 g, clean 1 g, VV 0.35 / 0.6) | — | identical |
| VV roll, full aft (vv_1): AoA | 68 | 59 |
| vv_1: right roll pV (deg/s) | 5.8 | 7.8 |
| vv_1: max β | 2.7 | 3.3 |
| Max-AoA roll, 420 km/h (roll at 1.9 s): time to pV 5 / 10 / 20 deg/s | 0.80 / 1.18 / 1.97 s | 0.78 / 1.28 / 1.83 s |
| Max-AoA roll: max β | 2.8 | 3.8 |
| Falling leaf, 300 km/h from −60° (full aft, full left): yaw rate | 36.9 | 38.5 deg/s |
| Falling leaf: max β | 4.6 | **7.7** |

- In the falling leaf at 60 the entry is slower: r is −15 deg/s at 4.0 s, against −22 at 70. β peaks −7.7° there, then settles to ±2.5 by 6 s.
- STOVL set (hover / VL / decel / VTO / transition): flight state bit-identical to 0.3.5. Only the AoA-limit-derived telemetry columns (asymCap…) and the timing columns differ.

## 3. Slow velocity-vector roll at max AoA (diagnosed, not fixed)

**Report:** at the end of the flight, full aft then roll; it took a while for the VV roll to get going.

**Log (14:21, 448–452 s; same pattern at 411–416 s):**
- pV ≈ 0–4 deg/s for 1.2–2.5 s while AoA climbed 54 → 70.
- Yaw was saturated the whole time (unmet yaw 50–80 deg/s²).
- The canted rudders first sat in the conventional yaw set (L +30 / R −30). The right fin was at 47–67° local AoA, so the set made no yaw: r ≈ 0 or slightly the wrong way.
- At AoA ≈ 67 the allocator flipped to the reversed set (L −30 / R +30). Yaw then built at ~10–11 deg/s², and the VV roll followed.

**Bench:** 420 km/h, full aft, roll at 1.9 s. Same ~2 s ramp, with the rudder flip at AoA ≈ 63. So this is authority-limited and not new in 0.3.x. It is how the allocator reads the rudders' effectiveness through the fin stall.

The 60° limit does not change the onset much on the bench (table above). The pull stops short of the flip region, but the rudders still pass through the low-effectiveness band on the way up.

**Not done; options for Tom:**
- (a) Make the allocator pick the reversed rudder set earlier when the fin's local AoA is past its stall. This is a change to the rudder effectiveness model on the Vortex only.
- (b) Lead the yaw with TVC yaw while the rudders are in the dead band, if yaw TVC is available at those conditions.
- (c) Accept it as is.

Benched, not flown.
