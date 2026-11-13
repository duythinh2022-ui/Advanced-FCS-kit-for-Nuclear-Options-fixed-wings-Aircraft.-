# Su-30VXN Gandiva FCS 0.5.4: the differential brakes help you drift

Base: 0.5.3. Tom, 11 Nov 21:06: "seems like i cannot drift on runway" → 21:09: "on other fcs jet they allow me to drift up to 10degs" → asked how the 0.5.3 brakes should treat a pedal release mid-drift: "i dont want any gates or timed logic, why not make the diff brakes help in drifting"; for the fast roll: "Only pulling hands over".

**Changelog:** While the pedal is in, the main on the pedal's side is now braked by |pedal| × 0.5 (stock: 0.3, and only below 58 kt). The jet pivots harder and the mains break loose, so it drifts. On the bench, 3 s of full pedal gives 35–80 % more peak sideslip at 8–40 kt (15 kt, idle: 7.1 → 12.8°) and 10–20 % more yaw rate; the jet slows faster while the pedal is in. A slide now survives a short pedal release (8 kt: 12.7 → 20.3° after re-applying). New dev setting GroundPedalBrake (0.5; 1.0 for more, 0 = stock).

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,497,600 bytes, SHA-1 87c6243fe5d1a0b8581a40666228f290ced0b6ab). Native core unchanged.
- Benched on `gyaw` (the FCS's C# on the game's gear model), the take-off bench and the air suites.
- Run on the game's Mono under Wine: kernel self-test 200 / 200; gear accessors bound; `LandingGear.FixedUpdate` patched.
- **Not flown.**

## What the 21:02 and 21:04 logs showed
- **Pedal releases mid-drift (0.5.3):** the drift sequence is hold full pedal (8–13° of sideslip at 8–13 kt), let go ~0.6–0.8 s, push again. Each release started the release stop with the outer main's brake at 0.85–0.9 (21:02, 45.8–46.7 s: yaw 32.7 → 8.9 deg/s, 12.8 → 8.9 kt in 0.85 s). Bench: 2–3 kt more lost per 0.6 s release than 0.5.2.
- **Above ~87 kt with the stick centred (21:04, 87–155 kt, from before 0.5.3):** the light nose lifts and the FCS latches into the gear-down rate law (mode 2 in 34 % of the ticks at 60–100 kt, 86 % above 100 kt; the Revoker, Ifrit and Compass logs: 0 %). The nose wheel was told 60° and sat at 0–3°; sideslip ~1°. In 21:02 you held full forward stick and the drift worked (20–27° at 125–150 kt).
- **The game's castor at speed:** the nose wheel settles where its pull toward the contact velocity (min(0.1 v, 10) × 5 per s) equals the 60 deg/s slew: ~2.7° off the velocity at 45 m/s, on every jet. So at speed the yaw comes from the rudders and the mains, not the nose wheel.

## Changes
1. **Pedal brake** (`GsPedalBrake`, dev setting `GroundPedalBrake` 0.5). With the pedal in (|pedal| > 0.05, ground speed ≥ 1.5 m/s, human pilot, FBW steering on), each braked main in contact whose brake yaws the jet the pedal's way gets |pedal| × GroundPedalBrake. Never below what the game applies there (your brake + the stock differential term), so the stock 0.3 stays underneath. No speed window: it works on the runway at any speed. Written through the same per-leg brake swap as the 0.5.3 release brake. With the pedal released, 0.5.3's release stop is unchanged.
2. **Not changed: the fast-roll latch.** "Only pulling hands over" was built and benched: on the take-off bench, stick centred and no pedal, the jet rotated and lifted off by itself (throttle 0.6: nose off at 11.5 s, airborne at 13.4 s, pitch 6.9°; throttle 1.0: airborne at 6.9 s, 6.8°). With the current latch it stays on the runway (pitch max 0.1°). The latch is what holds this light-nosed jet on the runway with the stick centred, so it was taken back out. The pedal brake yaws the jet through the mains, so it drifts at speed without the nose.

## Bench (`gyaw`, 23.9 t, Iz 560,000 kg·m², 21 % on the nose; no aero, so nothing above ~60 kt is meant literally)
### Drift: 3 s of full pedal, speed free under thrust
| Entry, thrust/weight | Peak sideslip 0.5.3 → **0.5.4** | Peak yaw rate | Speed after 3 s |
|---|---|---|---|
| 8 kt, 0.15 | 12.9 → **20.0°** | 42.8 → 42.1 | 7.6 → 5.0 kt |
| 15 kt, 0.15 | 7.1 → **12.8°** | 48.2 → 53.2 | 13.2 → 9.2 kt |
| 15 kt, 0.5 | 5.7 → **7.6°** | 41.2 → 47.4 | 34.2 → 30.4 kt |
| 25 kt, 0.5 | 10.6 → **15.9°** | 36.9 → 42.7 | 44.5 → 40.3 kt |
| 40 kt, 0.15 | 8.4 → **13.8°** | 34.6 → 41.0 | 39.8 → 35.6 kt |
| 40 kt, 0.5 | 15.0 → **17.7°** | 30.3 → 33.3 | 59.9 → 54.4 kt |

GroundPedalBrake 1.0 instead: 15 kt idle 20.6°, 5.9 kt left; 40 kt idle 25.6°, 28.4 kt left.

### Your drift pattern: hold 2.5 s, release 0.6 s, re-apply (thrust/weight 0.15)
| Entry | 0.5.2 | 0.5.3 | **0.5.4** |
|---|---|---|---|
| 8 kt: peak sideslip after re-applying / speed 1 s after | 12.4° / 9.3 kt | 12.7° / 7.0 kt | **20.3° / 3.6 kt** |
| 12 kt | 9.8° / 11.7 kt | 12.3° / 8.4 kt | **19.8° / 5.1 kt** |
| 17 kt | 4.5° / 17.7 kt | 6.2° / 14.9 kt | **15.1° / 10.1 kt** |

### Release stops (constant speed through the 2.5 s turn)
The turn is tighter now, so the stop starts from a higher yaw rate: 7 kt from 51.7 deg/s (0.5.3: 39.2), stop 1.48 s; 10 kt from 49.8, 1.43 s; 13 kt from 49.7, 1.33 s; 17 kt from 48.0, 1.23 s; 23 kt from 43.5, 1.18 s. Swing-back 0.00–0.24 deg/s up to 17 kt, 0.94 at 23 kt.

### Take-off, stick centred, no pedal: unchanged
Throttle 0.6 / 1.0: pitch max 0.1°, stays on the runway, modes as 0.5.3.

## Regression
- Suites against 0.5.2 (pedal brake on, as shipped): **87 / 87 per-case logs identical** (timing columns and the 5 ground columns aside); tables differ only in the tag lines.

## Not done, open
- **Not flown.** The bench has no aero, so the take-off roll's drift above ~60 kt (rudders, the drift limit at 29–49 kt) is not benched; the brake works the same there.
- **Speed:** the pedal brake bleeds speed while the pedal is in (that is how it breaks the mains loose). Throttle covers it; GroundPedalBrake 0.3 is close to stock.
- **The 0.5.3 release brake is unchanged:** a release still costs 2–3 kt (no gate or timer, as asked). GroundIndiBrakeMax lowers it.
- **The drift limit** (GroundDriftAllow 8°, from 29 kt) still counter-steers past 8° with the nose wheel and rudders. Set GroundDriftGain 0 to turn it off.
- Not ported to the other jets.

## Install
- **Main:** `plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.5.4 + meta.json; 0.5.3 kept as `Su30_VXN_FCS.dll.0.5.3.bak`.
- **Dev:** unchanged file. GroundPedalBrake appears in "Ground" after the first start.
- **Release:** `C:\Games\NO mods\Su30-FCS-0.5.4-release`.
- **Mod manager:** update the version there. Restart the game.

## Bench tools
`gyaw` gained `tw=<thrust/weight>` (speed free from the start) and `rel=<s>` (re-apply the pedal that long after the release); e.g. `gyaw kt=15 nfrac=0.21 Tturn=3 Tafter=0.1 tw=0.15 GroundPedalBrake=1`.

Previous notes: `claude/Su30_FCS_0.5.3_notes.md`.
