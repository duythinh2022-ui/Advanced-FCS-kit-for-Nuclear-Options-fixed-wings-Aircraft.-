# Su-30VXN Gandiva FCS 0.5.3: faster ground turn stops (main brakes as a yaw effector) and the light-nose fix

Base: 0.5.2. Tom, 11 Nov: "the turn stop is very slow" → "differential brakes on all 3 gears?" → handoff (`claude/Ground_yaw_brake_handoff_2026-11-11.md`) → "do the first one" (option 1, Su-30 first). The pedal phase is unchanged: steering stays stock while the pedal is held.

**Changelog:** After you release the pedal, the brake on the outside main now helps the nose wheel stop the yaw. It only takes the part the nose tyre can't grip, full up to 15 kt and faded out by 25 kt. On the bench a release stop from a full-pedal turn at 7–17 kt is about 20 % shorter (10 kt: 1.67 → 1.33 s, 8° less heading change). The price is speed: at 7–17 kt the jet loses 5–7 kt more per stop than 0.5.2 (bench). When the nose goes light mid-stop (the jet rocks on its suspension), the nose wheel now stays at its working angle instead of swinging to opposite lock and then to 0°. New dev settings: GroundIndiBrake, GroundIndiBrakeMax (1.0), GroundIndiBrakeFullSpeed / ZeroSpeed (7.7 / 12.9 m/s), GroundIndiLightNoseHold.

**Status:**
- Compiled against the game's assemblies; built twice, same bytes (1,496,064 bytes, SHA-1 db8e485f9d2c08644e2501eb9c127ae99bcf899f). Native core unchanged.
- Benched: the FCS's own C# ground code on a new yaw-plane gear bench (`gyaw`, the game's gear model from the IL), a Python mirror on gsim2's tyre, the air suites.
- Run on the game's Mono under Wine: plugin loads, kernel self-test 200 / 200, all six gear field accessors bind, Harmony patches `LandingGear.FixedUpdate` with the new prefix / postfix.
- **Not flown.**

## What the game does with the brakes (IL, `LandingGear.FixedUpdate`)
- Per leg: `b = braked ? brake : 0`, the parking brake (speed < 1 m/s, throttle < 0.1) sets 1, then `b += Clamp01(yaw × 0.3 × differentialBrakeFactor)` for 3 < groundSpeed < 30 m/s. A sum, not re-clamped.
- `brakeStrength = Lerp(brakeStrength, b, 5 dt)`: a 0.2 s lag.
- Force `brakeStrength × μN` along −contact velocity, summed with the side force and scrub, then clamped to μN. A braked main loses side grip.
- The steering slew toward `yaw × steeringLock` runs every tick, contact or not; the castor only with contact.

## The 18:55 light-nose event, read again
- The handoff said the wheel froze off the ground. It doesn't: the logged `gsWheel` stops updating there (GsNoseTyre returns before the debug line). The real wheel followed the command.
- 25.90–25.95 s: the leg still in contact but its damping force negative (extending), so μN read 0 and INDI dropped out. 0.5.2 then commanded the stock law's angle: **−60° (full opposite lock)** for 3 ticks, then 0 once off the ground.
- 25.97–26.40 s: off the ground; the wheel ran toward 0 (logged 0.36° at re-contact, 26.42 s), then INDI slewed it back to 20° by 26.60 s.
- Cause of the light nose: the jet rocks on its suspension after the release (roll −6° → +0.6°, pitch −2.7° → +1.9°, q up to +5.8 deg/s).

## Changes
1. **Light-nose hold** (`GroundIndiLightNoseHold`). In a release stop or the line hold that INDI was flying, while the nose tyre has no usable load: the wheel command is the contact velocity's angle + INDI's last slip (`GsLightNoseWheel`, `GsNoseAlpha`), at most 1 s (`GsLnMaxT`). The FCS's filtered nose force and yaw acceleration follow the lost load throughout, so INDI resumes without a step.
2. **Main brakes as the INDI's second effector** (`GroundIndiBrake`, `GsBrakeAlloc`). Each braked main in contact: its yaw effectiveness per unit brake from its load × friction, contact velocity, CG and yaw inertia, every tick; its brake as the game applies it (`brakeStrength`), filtered like the nose force.
   - Wanted total effector yaw acceleration next tick = nose now + brakes now + the INDI increment.
   - The nose tyre takes it up to its grip (μN). Only what is beyond goes to the brakes on the side that yaws the right way, up to `GroundIndiBrakeMax` × the speed fade.
   - The nose then takes the rest, crediting each brake's lagged value next tick. A first, purely incremental version held the brake on after the stop (bench: 0.3 s longer, 7 kt lost); this one releases it as the stop ends.
   - Never below what the game already applies on that leg: your brake + the stock differential term. `Hooks.GearPrefix` writes the leg's `brake` so the game's sum equals the target (restored in the postfix, like the yaw swap).
   - Active only with the pedal released, from 1.5 m/s up to `GroundIndiBrakeZeroSpeed`. Above it nothing changes.
3. Telemetry, at the end: `gsBrkL, gsBrkR` (target per main, −1 = not in the loop), `gsBsL, gsBsR` (the brake the game applies), `gsLn` (light-nose hold). With the light-nose switch on, `gsWheel` now logs the real wheel angle every tick.

## Bench: release stops (`gyaw`, the FCS's C#)
Stock full-pedal turn for 2.5 s at constant speed, pedal released over 0.15 s, then free (no thrust). 23.9 t (the 18:55 log), Iz 560,000 kg·m², nose share 21 % (the bench airframe's CG gives 12 %; the logs show ~51 kN on the nose tyre). Stop = yaw under 2 deg/s.

| Release speed | 0.5.2 stop / heading / swing-back | **0.5.3** | Speed lost by the stop (0.5.2 → 0.5.3) |
|---|---|---|---|
| 7 kt, from 39 deg/s | 1.53 s / 34.9° / 0.46 deg/s | **1.20 s / 28.0° / 0.21** | −1.7 → 4.3 kt |
| 10 kt, from 45 | 1.67 / 43.5° / 0.87 | **1.33 / 35.5° / 0.19** | −1.1 → 5.6 |
| 13 kt, from 46 | 1.55 / 39.7° / 1.02 | **1.23 / 33.5° / 0.00** | −0.6 → 5.3 |
| 17 kt, from 44 | 1.42 / 34.5° / 1.31 | **1.15 / 29.7° / 0.18** | 0.0 → 4.9 |
| 23 kt, from 40 | 1.23 / 27.5° / 1.17 | **1.13 / 26.2° / 0.71** | 0.6 → 2.2 |

(Negative speed lost = the jet speeds up a little as its yaw unwinds.) Light nose alone (A, brakes off): identical to 0.5.2 in every stop above. Peak sideslip unchanged in these stops; through the light-nose event at 10 kt it rose 10.9 → 14.3° with the brakes.

### Light nose as logged (μN 0 for 3 ticks at 0.30 s, off the ground 0.35–0.85 s, load back over 0.15 s)
| | Stop | Heading | Wheel at re-contact (0.5.2 → 0.5.3) |
|---|---|---|---|
| 10 kt: 0.5.2 / A / **A+B** | 2.10 / 2.10 / **1.70 s** | 61.1 / 61.3 / **47.0°** | 5.4° → 33.0° (A) |
| 13 kt | 1.97 / 1.97 / **1.57** | 56.0 / 56.2 / **43.5** | 0.0° → 24.1° |
| 17 kt | 1.82 / 1.82 / **1.43** | 48.8 / 48.9 / **39.0** | 0.0° → 15.6° |

- **The light-nose hold alone does not shorten the stop** (the handoff guessed 0.25 s). Past ~4° of slip the tyre is at full grip anyway, so a wheel 25° off at re-contact still saturates at once. What it removes: the 3 ticks of full opposite lock, and the re-contact at ~28° of slip (scrub). Swing-back 1.33 → 0.99 deg/s at 13 kt.
- With the brakes the stop is 19–21 % shorter through a light-nose event: the brake covers what the nose can't while it is up.

### Line hold (pedal released, straight, a 0.5 s yaw kick 1 s in)
| Kick | 0.5.2 heading peak | 0.5.3 heading peak | Brake | Extra speed lost |
|---|---|---|---|---|
| 15 deg/s² (inside the nose tyre's grip), 8–24 kt | 0.05° | 0.05° | target 0.08–0.18 for a moment | ~0.03 kt |
| 40 deg/s² (beyond it), 8–17 kt | 2.6° | 1.2–1.5° | 1.0 briefly | 3.2–3.5 kt |
| 40 deg/s², 24 kt (faded out) | 2.6° | 2.4° | 0.17 | 0.6 kt |

### Python mirror (`bench/groundsim/brakesim.py`, gsim2's tyre, with the stock 0.3 differential term on release)
Brake ceiling sweep, stop time / speed lost at 10 kt: 0.5.2 1.50 s / 1.7 kt; 0.4: 1.45 / 2.7; 0.6: 1.38 / 4.1; 0.8: 1.32 / 5.4; **1.0: 1.27 / 6.4**. About 15 % shorter here (vs 20 % in `gyaw`, whose 0.5.2 baseline lacks the stock 0.3 release brake: no stock FBW parameters in the bench).

## Regression
- Suites (reg, sweep, corkscrews, damage) against 0.5.2, both switches off and both on: **87 / 87 per-case logs identical** (timing columns and the 5 new columns aside); the tables differ only in the tag line. The damage suite errors on every case in both builds (it uses the Revoker's part names).
- 0.5.2 rebuilt here matched the shipped DLL in all 1,228 method bodies (a different compiler build, so not byte-identical).

## Not done, open
- **Not flown.** The bench has no suspension, so the light nose is imposed as logged, not produced. Iz is an estimate.
- **The speed cost is real.** At 10 kt a full-brake stop ends near 4 kt (bench). If that is too much, `GroundIndiBrakeMax` 0.6 keeps about half the gain for about half the extra loss.
- A strong yaw gust in the line hold (beyond the nose tyre's grip) brings the brake in briefly too: half the heading excursion, 3–4 kt.
- Not ported to the other five yet. Per the handoff, read each jet's gear data and run the stop sim first. The F-22E (both mains dbf −1, no stock differential braking) should gain the most.

## Install
- **The game was running while this was built** (its Mono DLL was locked). Close it first, or the first write fails.
- **Main:** `plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.5.3 + meta.json. 0.5.2 kept as `Su30_VXN_FCS.dll.0.5.2.bak`.
- **Dev:** unchanged file. The five new settings appear under "Ground" after the first start.
- **Release:** `C:\Games\NO mods\Su30-FCS-0.5.3-release`.
- **Mod manager:** update the version there. Restart the game.

## Bench tools
- `dotnet bench/bin/bench.dll gyaw kt=10 nfrac=0.21 [ped=1] [light=0.30:0.85] [kick=40] [csv=out.csv] [GroundIndiBrake=false ...]` (BENCH_DATA = `bench/extract/su30_airframe.json`).
- `python3 bench/groundsim/brakesim.py` (the Python mirror and its tables).
- `native/verify/gearcheck.cs`: on the game's Mono (via `host.c`), Acc's accessors and the gear patch.

Previous notes: `claude/Su30_FCS_0.5.2_notes.md`.
