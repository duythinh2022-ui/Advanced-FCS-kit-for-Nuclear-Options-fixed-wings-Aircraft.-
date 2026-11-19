# FS-20 Vortex FCS 0.3.2 — asymmetric-load AoA floor 12 -> 10

Build of 17 Nov 2026. **Benched, not flown.** On 0.3.1. One change: the Vortex's own default for `AsymMinAoACap`.

## What changed
- `AsymMinAoACap` (Dev config, "Asymmetric load") default **12 -> 10** on the Vortex.
  - It is a per-jet setting. `AsymShared.cs` / `AsymCap.cs` / `CoupledCap.cs` are untouched (same hashes as the other six jets); the other jets stay at 12.
  - A Dev file still holding exactly 12 is moved to 10 once (`GovernorRevision` 3 -> 4). A value you set by hand afterwards is kept.
- Why (Tom, 17 Nov: "so lower the floor to 10"): in the heavy-load roll away from the load, the jet sat on the 12 deg floor and **yaw could not keep up with the roll**.
  - At 12 deg AoA a coordinated roll needs a yaw rate of p x tan(AoA). With the yaw already spent holding the load straight, the yaw saturated (`yawSat` 1.0), the nose kept yawing slightly the wrong way, and sideslip built at ~8 deg/s (p x sin AoA - r x cos AoA).
  - The sideslip then took the roll away (dihedral effect): p fell from -29 to -9 deg/s.
  - At 11 deg `yawSat` is 0.6-0.9 (7.7 deg sideslip), at 10 deg 0.26-0.30 (4 deg). The jet tips over between 11 and 12.

## Bench (same bench; 0.3.1 = this build with `AsymMinAoACap=12`)

**Heavy asymmetric load** (1760 kg on wing2a_R, full aft stick, full roll): bank in 5 s / max sideslip

| Case | 0.3.1 (12) | 0.3.2 (10) |
|---|---|---|
| Left, 470 km/h, throttle 0.6 | 125 deg / 19.0 | **316 / 4.0** |
| Left, 330 km/h, AB | 137 / 16.8 | 239 / 8.1 |
| Left, 300 km/h, AB | 163 / 17.1 | 224 / 9.0 |
| Left, 400 km/h, AB | 149 / 17.7 | 298 / 6.3 |
| Left, 400 km/h, half throttle | 119 / 15.2 | 264 / 5.9 |
| Left, 550 km/h, half throttle | 185 / 14.5 | 376 / 2.7 |
| Left, 330 km/h, half aft stick | 130 / 16.3 | 264 / 8.1 |
| Left, 250 km/h, 1000 kg | 203 / 7.0 (AoA 14.6) | 269 / 6.3 (14.5) |
| Right (toward the load), 330 km/h, AB | 314 / 2.2 | 416 / 2.4 |
| Right, 470 km/h, throttle 0.6 | 350 / 2.3 | 403 / 2.4 |

Held AoA in these: 12.4-12.8 -> 10.4-10.6.

**0.3.0's asymmetric-load set** (650 kg on wingtip_L)
- Rolls at 250 / 600 km/h: the same (the limit there is above both floors).
- 400 km/h: max AoA 14.5 -> 13.6, bank 103 -> 100 deg.
- Store drops in a pull (500 kg x 2 off the left, 1.5 / 4.0 / 5.7 s, throttle 1 / 0.75): held AoA after the drops 12.0 -> 10.6, max load factor 4.02 -> 3.74, 4.73 -> 4.42, 3.36 -> 3.13.

**Clean jet:** every clean case is identical (falling leaf and reversal, velocity-vector rolls, 1 g rolls, full aft): the floor only acts with a load.

## Cost
- **Less pull with a load:** the AoA limit with a heavy one-sided load is 10 instead of 12, so roughly 15 % less lift at the limit. The loaded store-drop pulls above lose ~0.3 g.
- **Slowest 1 g speed with the load on the limit:** with 1760 kg on one wing, full aft stick, ~257 km/h at 12 -> **~275 km/h** at 10 (bench, from load factor vs speed while the jet accelerated on the limit, approximate). Below that the jet sinks even with full aft stick, as it already did below ~257 at 12. The limit also applies with the gear down, so the same holds for a take-off or landing with such a load.

## Not checked
- STOVL with a one-sided load (the STOVL set is clean).
- Not flown. The bench is optimistic laterally at the 470 km/h condition (see 0.3.1 notes).

## Install
- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.3.2; 0.3.1 kept as `Vortex_FS20_FCS.dll.0.3.1.bak`. `meta.json` updated.
- Dev DLL unchanged.
