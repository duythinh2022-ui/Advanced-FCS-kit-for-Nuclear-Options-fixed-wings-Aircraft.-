# Vortex FCS 0.6.8 (21 Nov 2026)

On 0.6.7: the same attested native core, embedded in the plugin DLL (sha256 `e20c7b99…`). A fix to 0.6.7's STOVL change; no new terms on top of the laws.

## What Tom reported (log 00:23, 0.6.7)
- Turning in STOVL: the v/s fluctuations are much tighter and smaller.
- Climbs and descents: maybe worse, and not consistent. At first no difference from 0.6.6, but later a few sink releases stuck at −20 to −30 ft/min, and some settled very slowly near 0 ft/min.
- Asked for STOVL manoeuvring to be checked for inconsistency too.

## What the log shows
**Releases of the v/s stick in the hover.** Time until |vs| stays under 5 ft/min:

| | 0.6.6 (log 22:12) | 0.6.7 (log 00:23) |
|---|---|---|
| Sink releases | 2.1–3.3 s every time | 3.2–9.1 s; at 114.7 s, 144.7 s and 159.0 s still −41, −83 and −41 ft/min 2–4 s after the release; the last two (186 s, 194 s) 3.2–3.8 s |
| Climb from +272 ft/min (171 s) | 2.0–3.2 s for similar climbs | 5.2 s |

**Quiet hover, no input.** How fast the thrust command moves:
- 0.6.6: 0.05–0.08 /s.
- 0.6.7: 0.3–1.1 /s, and the vertical demand jitters 10–40× more.

**Manoeuvres.** The v/s peaks are about the same size (pedal starts median 13 ft/min vs 10; lever starts 18 vs 21). The thrust moves 1.5–2× more, and the peaks spread wider (pedal 2–16 ft/min).

## Causes
1. **0.6.7 put the STOVL laws on the derivative of the CG velocity.** In the game that velocity is the mass-weighted velocity of every part's rigidbody, and the parts sit on joints. Their jitter makes the derivative far noisier than the game-side acceleration (the root body's), so the thrust chattered. On the bench every part moves as one body, so none of it showed.
   - Reproduced on the bench with noise on the CG velocity only (σ 0.015 m/s per tick, the game's level from the 0.6.6 notes; new key `BenchNoiseVelOnly`).
   - In the hover, the thrust command moves 1.5 /s in 0.6.6 and 10.6 /s in 0.6.7.
   - Release from a full-stick climb, time under 2 ft/min: 0.6.6 2.2 s, 0.6.7 5.8 s.
   - Release from a half-stick sink: 0.6.6 4.9 s, 0.6.7 12.9 s.
   - Release from a small climb: 0.6.6 1.2 s, 0.6.7 12.0 s.
2. **0.6.7's stop estimator fitted the push less the whole aero model.** In a vertical descent the model's part is vertical drag. Taken out of the estimator's own drag term (which the stop predictor runs along the predicted speed), it ended up in the push the predictor holds constant. So a descent's stop counted on drag that fades as the sink slows.

## Changes (`StovlAeroModel`, dev, on; false is still 0.6.6, bit-identical)
1. **The CG acceleration, measured at its source.** It is now the game-side acceleration (root body, 0.05 s low-pass) plus the derivative of the CG's velocity relative to the root body (angular velocity × the lever from the root body's centre of mass to the CG, a rigid-body term), through the same low-pass.
   - This removes the pitch acceleration × lever error that 0.6.7 found (up to 0.29 m/s²), without the other parts' joint noise.
   - The aero model learns from the same signal, with the propulsive force through the same filters.
2. **The stop estimator fits the push less the model's lift only.** Vertical drag stays in its own speed-squared term, as in 0.6.6.

## Bench
**Game-like part jitter** (noise on the CG velocity only). Time until |vs| stays under 2 ft/min / thrust-command motion in the hover:

| Release | 0.6.6 | 0.6.7 | 0.6.8 |
|---|---|---|---|
| Full-stick climb (+1115 ft/min) | 2.23 s | 5.77 s | 2.22 s |
| Full-stick sink, 2 s (−948) | 3.40 s | 12.35 s | 3.42 s |
| Half-stick sink, 3 s (−489) | 4.95 s | 12.90 s | 2.07 s |
| Short full sink (−471) | 4.17 s | 12.18 s | 4.17 s |
| Small climb (+223) | 1.18 s | 11.98 s | 1.25 s |
| Hover thrust motion | 1.47 /s | 10.6 /s | 1.47 /s |

**Without noise, 0.6.8 keeps 0.6.7's results** (within 0.2 ft/min and 0.1 cm):
- 12-case set: F0 −9.1/+12.4 ft/min and −1.5/+2.7 cm (0.6.6: −14.4/+15.0, ±5 cm); PB +12.8 (21.7); S128 −17.9/+9.6 (−29.5/+17.9); CF5 −4.5/+4.9 (−9.4/+9.8).
- Fore-aft stops: height ±1–4 cm (0.6.6: ±5–16).
- Regression set: within 1 mm and 0.01 m/s of 0.6.7.

## Still open
- **The translation-start transient** (about 20 ft/min peaks on lever starts in the log; −9/+13 on the bench): the thrust fit leans the nozzle before the RPM has the thrust. See the 0.6.7 notes, section 5.
- **The bench's part jitter is a stand-in** for the game's. Not flown.
