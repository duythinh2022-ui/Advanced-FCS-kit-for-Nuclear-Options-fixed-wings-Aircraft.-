# Su-30VXN FCS 0.6.2 (16 Nov 2026): low-AoA drag, Buffed thrust x1.10

Base: 0.6.1.

Tom, 16 Nov 10:36: "add drag and reduce the thrust buff from 15% to 10 %". The goal is for a full-aft pull at 330–350 kt to bleed airspeed like his F-22E, instead of accelerating.

## Why the Flanker accelerated (16 Nov logs, clean, max fuel)

| Full aft, full AB, ~350–360 kt | F-22E 4.3.0 (10:19 log) | Su-30 0.6.1 (10:20 log) |
|---|---|---|
| G reached / limit | 9.0–9.3 / 9.5 | 8.9–9.0 / 9.0 |
| AoA for it | 26–33° | 11–13° |
| Drag / weight | 1.52–1.66 | 1.01–1.02 |
| L/D at 9 g | 5.4–5.9 | ~8.7 |
| Excess power Ps | −66 to −96 m/s | +35 to +36 m/s |

- **Lift (nz / q̄):** the two jets have about the same peak lift for their weight (~0.51 g/kPa). The Su-30 reaches it at 20–24° AoA, the F-22E at 40–44°.
- **So:** the Su-30 makes 9 g at half the AoA, where its own foils have little drag.

## Changes

### 1. `LowAoADrag` (dev, `5. Airframe`, on) and `LowAoADragGain` (dev, 1.9)

**Where the extra drag goes:**
- **Full extra:** the Su-30's own wing panels, outer flaps and body foils (fuselage, LERX, intakes, engines, tail).
- **A quarter of the extra:** the canards.
- **None:** the flaperons (see "Lateral" below). The stabs and fins aren't touched either.

**How it is scaled:** by each part's own AoA magnitude.
- ×1 up to 4°.
- A smooth rise to the gain by 12°.
- Held at the gain to 22°.
- Back to ×1 by 32°. Past 32° the 0.1.5 `HighAoADragShape` curve is unchanged.
- Negative AoA gets the same, by magnitude.

**What is not changed:**
- Lift.
- Both profiles get it (as `HighAoADragShape`).
- Respawn after changing it.

**Pitch balance:** the canards' drag sits ahead of the CG, so their share is what balances pitch. A quarter keeps the static pitch moment within −0.13 / +0.30 °/s² of 0.6.1 from −10° to 40°. That is the bench at 350 km/h with surfaces neutral, where the nose-down to hold is 20–55 °/s² at those AoAs.

**Whole-jet drag against 0.6.1 (bench, static):**

| AoA | 0–2° | 4° | 6° | 8° | 10° | 12–22° | 24° | 26° | 28° | 30° | ≥34° |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Drag | ×1.00 | ×1.02 | ×1.09 | ×1.24 | ×1.40 | ×1.48–1.51 | ×1.42 | ×1.31 | ×1.17 | ×1.06 | ×1.00 |

### 2. `ThrustScale` (dev, Buffed) 1.15 → 1.10
- **Migration, forced (Tom, 11:09: "force this on ppl that update the mod as well"):** on updating, every dev-unlocked config is set to 1.10 once (`Su30DevRevision` 16 → 17), whatever it held, not only the old 1.15 default. LogOutput shows "Su-30 FCS 0.6.2 defaults moved once: ThrustScale 1.10 (was 1.15)." A value set after that is kept.
- **Players without the dev unlock** never read the dev file; they get the built-in default, now 1.10, with no migration needed.
- **Multiplayer:** clients locked to the host's Buffed variant use the host's thrust, so a host still on 0.6.1 hands out 1.15.
- **Stock profile** is not affected (thrust x1 there).
- **Tom's own file** was set to 1.1 by hand on 16 Nov 11:08, so his log will say "(none held the old values)".
- **Player text:** the `PerformanceProfile` description now says x1.10.

### Also
- **Ready line in LogOutput:** ends with "low-AoA drag x1.90".
- **Chart line:** gives each drag foil's factor at 16° as well as 45°.

## Bench

### Calibration against the 10:20 log
- **Same pull, 0.6.1 settings, 23,770 kg, 1,500 m:** drag/weight at 9 g is 1.03 / 0.97 / 0.90 at 360 / 370 / 380 kt. The game logged 1.01 / 0.98 / 0.93.
- **AoA:** 13.3 / 12.1 / 11.8° on the bench against 13.0 / 12.2 / 10.8° in the game.
- **At 350 kt** the bench is ~20 m/s more pessimistic on Ps (+16 against +35).

### Full aft from 660 km/h (356 kt), Ps by IAS (m/s)

| | 330 kt | 340 kt | 350 kt | 360 kt | 370 kt |
|---|---|---|---|---|---|
| 0.6.1 (thrust 1.15) | −22 | −1 | +16 | +29 | +43 |
| Thrust 1.10 only | −29 | −10 | +5 | +19 | +33 |
| **0.6.2** | **−115** | **−113** | **−94** | **−76** | **−56** |
| F-22E, game log | −254 | −187 | −96 | −66 | — |

- **From 610 km/h:** 0.6.2 gives −115 / −110 / −84 at 330 / 340 / 350 kt.
- **AoA at 9 g is unchanged** (lift isn't touched), so it still pulls 9 g at ~13°. It just loses energy like the Raptor at 350–360 kt.
- **Below 340 kt the F-22E still bleeds harder.** By then it sits at 32–36° AoA, at its lift peak. A higher gain (about 2.5–3) brings 340 kt closer, at the cost of more bleed everywhere else in the 8–30° band.

### Elsewhere (static drag ratio at the AoA for that g, no HLD in this sweep)

| Condition | AoA | Drag |
|---|---|---|
| 450 km/h, 1 g (cruise) | 5° | ×1.04 |
| 450 km/h, 2 / 3 / 5 g | 8 / 12 / 23° | ×1.24 / 1.48 / 1.47 |
| 300 km/h, 1 / 2 g | 9 / 19° | ×1.32 / 1.51 |
| 250 km/h, 1 g | 13° | ×1.49 |

- **Approach:** at 250 km/h the game's HLD is out, which lowers the AoA, so the real approach figure is lower than ×1.49. Expect more throttle on approach.
- **Any loaded turn at 8–30° AoA** bleeds more.
- **Top speed** drops a little with the thrust cut. Cruise drag is unchanged.

### Lateral (regression set and stop / reversal sweep; 0.6.1 at thrust 1.15 against 0.6.2)

**First try, extra drag on the flaperons too:**
- Stop sweep, worst 10.6 → 13.4°, mean 5.8 → 6.2°.
- Reversal mean 7.5 → 8.5°.
- Extra drag on a deflected flaperon is differential drag on every roll input (adverse yaw). So the flaperons were taken out.

**Shipped (flaperons excluded):**

| Sweep | 0.6.1 worst / mean | 0.6.2 worst / mean |
|---|---|---|
| Stop | 10.6° / 5.81° | 10.8° / 5.51° |
| Reversal | 14.0° / 7.53° | 12.7° / 7.78° |

Mean roll rate in the sweep: 104 → 101 °/s.

**Regression set, peak sideslip (0.6.1 → 0.6.2):**

| | Cases |
|---|---|
| Worse | pull-then-roll 400: 11.4 → 13.2<br>full aft + roll 250: 7.6 → 9.1<br>push, then pull-through + roll 350 / 450: 7.4 → 9.6 / 12.0 → 15.8<br>pull-through, roll released at zero 350: 5.0 → 6.7 |
| Better | 65° releases: 7.6 → 5.4, 5.5 → 4.0<br>pull-then-pushover + roll 450: 11.2 → 8.6<br>roll-then-push −55 200: 5.7 → 4.5 |
| Within ±0.6° | the rest |

**What the worse ones have in common:**
- They are rolls during a pull through 25–45° AoA.
- In the pull-then-roll 400 case, the roll rate sags more mid-pull: 42 → 26 °/s at 44° AoA. The yaw rate is ~10 °/s lower and sideslip is 1–2° higher.
- **Likely cause (not proven):** the extra drag on the fore and aft body sections adds yaw damping, which resists the yaw rate a velocity-vector roll needs.
- **Tried:** ending the band at 25° (gain 2.2 for the same Ps). It fixed pull-then-roll 400 and full aft + roll 250, but made full aft + roll + reversal 500 worse (9.6 → 12.2) and the stop sweep worst 14.4°. Not taken.

## Status
- **Compiled** against the game's assemblies: 1,532,416 bytes, SHA-1 cbbe3a0155667a9122560a34812fa45683230b00, md5 ab29b4804d95334e1bf76d8516049082.
  - Two builds of the same source here gave the same IL method by method, but not the same bytes.
  - The 0.6.1 source rebuilt here also matched the released 0.6.1 DLL method by method, but not byte for byte (the SDK here is 8.0.134).
- **Migration code** compiled, not run (no game here); the forced move is one condition changed from "holds 1.15" to "is not 1.10".
- **IL changes against 0.6.1:** only `StallStagger`, `Plugin` (binds, migration, ready line) and the `StallStaggerJobs` log line, plus renumbered compiler closures.
- **Not flown.**

## Install
- **Main:** `BepInEx\plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.6.2. 0.6.1 is kept as `Su30_VXN_FCS.dll.0.6.1.bak`.
- **Dev DLL:** unchanged.
- **Restart the game,** then respawn the jet.
- **Mod manager:** its record (`meta.json` in the plugin folder) still says 0.6.0. Update it, or its modpack may put an older DLL back.
- **Release folder:** `C:\Games\NO mods\Su30-FCS-0.6.2-release`.
