# J-20B Weilong FCS 0.2.3: roll release then push

Base: 0.2.2. **Status:** compiled against the game's assemblies, benched on replays of the 15:30 flight. **Not flown.**

Tom (24 Nov 15:31): "when i command roll and then release, then i command pitch down, roll kept going for a moment then stop then pitch kicks in, there was one occasion that roll stays and sideslip spiked".

## What happened (15:30 log)
- 43.3 s: roll stick released at 165 deg/s, full push from 2 deg AoA. The roll target held at -167 and grew to -208 deg/s for 0.45 s, then snapped to zero as the AoA crossed zero; the roll was braked under a -40 deg/s push: 14.5 deg of sideslip.
- 50.6 s: same from 7 deg AoA at 190 deg/s: the target grew to -283 deg/s, the roll kept going for 2.8 s, 31 deg of sideslip.

The cause is the push governor (F-22E 2.34.0): pushing toward zero AoA under a velocity roll, it reads the yaw the sweep has to shed, roll x sin AoA, divided by sin AoA. Near zero AoA that divides by about zero; the band said the roll must keep growing (999), and the roll target was raised above the released stick.

## Change (`PushGovNearZero`, 6. Lateral governor, on)
The band's demand that the roll keep going or grow now fades out near zero AoA with the same weight as the governor's own pitch cap (SweepShedCos: near zero a hold buys nothing), by how far the roll stick is released. Its limit on how fast the roll may come down is unchanged, and with the roll stick held nothing changes. false = 0.2.2.

| Replay of the 15:30 flight | Game | 0.2.2 replay | 0.2.3 replay |
|---|---|---|---|
| 43.3 s: peak sideslip | 14.5 deg | 11.4 | **5.2** |
| same: release to roll under 20 deg/s | | 0.80 s | 0.52 s |
| 50.6 s: peak sideslip | 31.3 deg | 30.3 | **10.9** |
| same: release to roll under 20 deg/s | | 2.83 s | 0.55 s |

Regression set (20 cases, your config): identical to 0.2.2. Held push-overs (0.2.2 cases): unchanged.

Left: 10.9 deg at 50.6 s, the yaw not keeping up with a 190 deg/s roll stopped in half a second at 7 deg AoA.
