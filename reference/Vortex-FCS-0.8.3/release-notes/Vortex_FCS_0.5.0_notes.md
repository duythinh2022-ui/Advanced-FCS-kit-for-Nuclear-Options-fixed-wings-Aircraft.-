# Vortex FCS 0.5.0: lift-fan vane box (thrust vectoring under the fan, with a 3D model)

18 Nov 2026. Built on 0.4.11 from `Vortex_lift_fan_vanes_handoff.md`.

**Status: benched, not flown.** The control law and the vane physics were flown on the bench (the bench plant got the same vane physics). The game-side code that turns the fan's thrust line and draws the model is **compiled only**. The model was checked offline against the game's own meshes; it has **not been seen in game**.

Tom, 18 Nov:

> build this, add model for the vanes, make sure to use the right material textures, no clipping, enough clearance, no artifacts.

> also put the extra vanes models into the lod system so it wont tank performance

> Maybe do 10 and 42 deg for the vanes

## What it does

- A vane box (the F-35B's VAVBN: a louvre box under the lift fan) turns the fan's thrust line fore and aft: **10° forward to 42° aft** of the fan's axis, at 30°/s.
- 10° forward is the game's own fixed fan thrust line. It is the vanes' rest position and forward stop, so braking is what it was.
- The jet now accelerates forward from the hover **at a level attitude** with the fan pushing too. Before, only the rear nozzle pushed.
- The FCS picks the nozzle angle and the vane angle together in its thrust fit. No bolt-on: the vanes are in the same force model, forward budget, governor and thrust lead as the nozzle.
- `StovlVanes = false` gives 0.4.11 exactly (checked, see below).

## Bench results, your jet (Buffed, 13 824 kg, your settings)

| Case | 0.4.11 | 0.5.0 |
|---|---|---|
| Hover → 30 kt, time to 90 % of the speed | 6.70 s | **4.35 s** |
| Mean acceleration (20–70 % of the speed) | 2.27 m/s² | **4.00 m/s²** (peak 5.00) |
| Height band during it | −0.026 / +0.031 m | −0.008 / +0.038 m |
| Pitch during it | 2.98–3.01° | 2.97–3.02° (level) |
| Hover → 45 kt, time to 90 % | 9.33 s | **5.55 s** |
| 0 → 39 kt | 8.95 s | **5.22 s** |
| Stop from 30 kt, to 0.3 m/s | 7.17 s | **6.53 s** |
| Speed past zero at the end of the stop | 0.07 m/s | 0.01 m/s |
| Height band in the stop | −0.048 / +0.063 m | −0.048 / +0.058 m |
| Stop from 45 kt | 8.70 s | **7.95 s** |
| Side stick 3 s into the 45 kt acceleration: bank past 5° | 0.87 s | **0.65 s** |
| Side speed 2.9 s after that stick | 2.77 m/s | **5.04 m/s** |
| Side stick during the stop: height band | −0.115 / +0.088 m | −0.097 / +0.088 m |
| Side + fore-aft together for 8 s: height band | −0.034 / +0.082 m | −0.039 / +0.073 m |
| Stop from 27 kt backwards | 8.32 s | **5.08 s** (level) |
| Pedal turn skid at 30 / 45 kt | 0.84 / 0.57 m/s | 0.76 / 0.57 m/s |
| Hover: spare vertical acceleration | 1.33 m/s² | 1.38 m/s² |

Curve shape, all cases above: no reversal of acceleration, thrust, nozzle, vane or pitch rate; the governor never shut; peak engine thrust 0.88 of full in both builds; the side-stick bank command never paused.

In the hover the fit now holds the vanes at about −5° and the nozzle a little further aft (swivel 0.841 → 0.886). That trim uses slightly less thrust, which is where the extra 0.05 m/s² of spare lift comes from.

## Six-jet matrix (0.4.11 → 0.5.0)

| Jet | Hover → 30 kt: mean accel, time to 90 % | Stop from 7 m/s to 0.3 m/s | Past zero |
|---|---|---|---|
| Buffed 11.9 t | 3.68 → 4.88 m/s², 4.73 → 3.75 s | 4.37 → 3.69 s | 0.08 → 0.01 m/s |
| Buffed, 20 % fuel | 4.43 → 4.94, 4.27 → 3.68 s | 4.37 → 3.69 s | 0.07 → 0.01 |
| Buffed 13.6 t | 2.42 → 4.09, 6.37 → 4.27 s | 4.62 → 4.09 s | 0.07 → 0.01 |
| Buffed + 500 kg on one wing | 2.87 → 4.69, 5.50 → 3.82 s | 4.40 → 3.75 s | 0.08 → 0.01 |
| Stock 11.9 t | 2.17 → 3.61, 7.00 → 4.72 s | 4.85 → 4.59 s | 0.07 → 0.01 |
| Stock 12.8 t | 1.25 → 2.60, 11.23 → 6.13 s | 4.52 → **5.67 s** | 0.24 → 0.01 |

- Height band in the 30 kt acceleration stays inside +0.02 / +0.09 m on all six (0.4.11: −0.04 / +0.09 m). Pitch stays 2.96–3.02°.
- Peak thrust in the 7 m/s step drops on five jets (Buffed 11.9 t: 0.98 → 0.85 of full) and is unchanged on Stock 12.8 t (0.96).
- Bank and pedal cases are unchanged within a few centimetres. The full-stick climb and sink case ends 0.0–0.4 m higher (after climbs of 12–39 m; spare lift in the hover is 0.02–0.06 m/s² higher).
- Decel-to-hover from 380 km/h is 0.3–0.6 s shorter on the five jets that reach the hover in that case (the 20 %-fuel jet doesn't, in either build).

## What got worse or changed

1. **Stock 12.8 t stops later.** It reaches 0.3 m/s in 5.67 s instead of 4.52 s. 0.4.11 got there by braking past the stop profile and running 0.24 m/s backwards; 0.5.0 follows the profile and doesn't run back. `StovlVaneStopJerk = true` brings it to 4.60 s (see below).
2. **Buffed 11.9 t sags 1.5 cm more after a stop** (−0.079 → −0.094 m; the +0.16 m balloon before it is unchanged).
3. **Short take-off flies differently.** The ground roll is faster (peak 8.8 against 7.6 m/s²; 39.5 against 29.6 kt at 10 s). With the same 3 s half-stick pull the jet then climbs at about 19° instead of about 6°, so 40 s in it is higher and slower (Buffed 11.9 t: 720 m at 547 km/h against 377 m at 741 km/h). Between 60 and 80 kt (the CTOL blend) the vanes go back to rest and the blend flies as 0.4.11.
4. **Roll posts saturate for 0.27 s** during a full side stick 3 s into the 45 kt acceleration (0.4.11: not at all). The bank still comes in sooner.

## Two decisions worth your call

**Stops.** The vanes can take out braking at once, so the stop profile no longer needs to wait for the pitch loop. `StovlVaneStopLag` (0.25 s, was the pitch loop's 0.5 s) is what makes stops shorter than 0.4.11 at the same height band. `StovlVaneStopJerk` (off) goes further:

| | 0.4.11 | 0.5.0 default | `StovlVaneStopJerk = true` |
|---|---|---|---|
| 13.8 t, stop from 30 kt | 7.17 s | 6.53 s | 5.90 s |
| 13.8 t, stop from 45 kt | 8.70 s | 7.95 s | 7.33 s |
| Stock 12.8 t, stop from 14 kt | 4.52 s | 5.68 s | 4.60 s |
| 13.8 t, height sag after the stop | 3 cm | 4 cm | 6.5 cm |
| Buffed 11.9 t, height sag after the stop | 8 cm | 9 cm | 12 cm |

(Stop-set runs; the matrix run of the Stock 12.8 t stop gives 5.67 s.)

With it on, the jet stops while the nose is still high, and the nose comes level afterwards, faster. The extra sag is the existing end-of-pitch-return dip, larger because the pitch rate is larger. I left it off because of your rule on height dips.

**Vane loss.** The handoff proposed taking the vane loss off the fan's force. That doesn't work in this game: the fan's force comes from a pitch-moment balance, so a force taken off would be put straight back by the balance. Instead the loss is paid as fan drive power: the fan's `parasiticLoss` (thrust the engine gives up to drive the fan) rises by up to 8 % (2 kN) at 42° aft. `StovlVaneLoss` (0.05 at full aft) is still an estimate; no figure for the real vane box was found.

## How the law uses the vanes

- **Thrust fit:** one fit over nozzle angle × vane angle (21 × 11 table, then two refinement steps). It carries small costs on moving either and on thrust, as the nozzle had.
- **Forward budget (`FwdCap`):** counts the fan vectored aft, so the budget shared between sideways and forward is what the jet has.
- **Governor:** the lift the vanes take while turning counts in the tilt governor's demand. The vanes themselves aren't held back: held back, the nozzle swung aft in their place and thrust hit its limit (bench: 97 % for 0.5 s, 5 cm lost).
- **Thrust lead:** thrust is sized for where nozzle and vanes will be one engine response time ahead.
- **CTOL blend (60–80 kt):** the vanes return to rest.
- **Telemetry:** `stVane` (commanded) and `stVaneNow` (actual), appended after `stOrigin` in the FCS CSV. Angles are the thrust line from the fan's axis, + aft.

## The model

Built in code from measurements of the game's own fan part (mesh `liftFan`), not an asset bundle.

- **Box:** inside 0.95 × 0.65 m, 15 mm walls, 46 mm proud of the duct exit.
- **Shroud:** the round-to-rectangular transition from the duct wall down to the box.
- **Keel:** a centre channel the fan's hub fairing hangs into, closing to a ridge. It is wider than the real one (0.34 m) because the game's hub fairing reaches down to the duct exit.
- **Six vane pairs:** 120 mm chord, hinged at mid chord on pivot pins. Drawn angle = thrust angle / 0.9 (flow turns less than the vanes), so −11° to +47°.
- **Materials:** the fan part's own. `aircraft_structure` for box, shroud and keel (white as the bay doors' inner structure on box and keel, dark as the duct wall on the shroud). `jet_mechanical` for the vanes, on the dark-metal patch the fan's own stators use.
- **Cost:** 912 triangles, 7 renderers. They join the airframe's own LOD group at the near level, next to the fan part, so they switch off with it at range. They are also off whenever the fan doors are closed. No shadow casting, no colliders.

Offline checks (exact mesh-to-mesh distances against the game's meshes):

| Check | Result |
|---|---|
| Box to duct wall / bay edge | 21 mm |
| Keel to hub fairing | 22 mm |
| Box and vanes to the fan blades | 200 mm below them |
| Box to the lower doors, closed and through their travel | 47 mm (closed) to 205 mm (open) |
| Vanes to the closed doors | 83 mm |
| Vane to neighbouring vane, whole travel | 61 mm or more |
| Vane ends to keel and wall | 10 mm |
| Vanes to hub fairing, whole travel | 50 mm or more |
| Coplanar overlapping faces (z-fighting), inside the model and against fan part and doors | 0 (the search finds the two cases the design avoids when they are put back) |
| Triangle winding against the game's convention | 912 of 912 |

The model follows the fan thrust transform itself, so it moves with whatever turns it.

## What only the game can show

- Whether the structure shader tints by vertex colour the way the duct and door meshes suggest (it should: I used the same two values those meshes carry).
- Lighting and the look at distance. The preview picture is a software render with base textures, not the game's shading.
- That the box lands in the airframe's LOD group. The log says which: `Vortex FCS vane box model: 6 vane pairs + frame on liftFan, 912 triangles, in the airframe's LOD group ...`. If the fan part's mesh ever changes (game update), the box is left out and the log says so.
- Other players' jets show the vanes at rest (nothing is synced). The replay add-on doesn't record the vane angle.

## Config

`1. General`: `StovlVanes` (true), `StovlVaneModel` (true).

Dev file, `9. STOVL`: `StovlVaneMin` (−10), `StovlVaneMax` (42), `StovlVaneRate` (30 °/s), `StovlVaneTurning` (0.9, looks only), `StovlVaneLoss` (0.05), `StovlVaneStopLag` (0.25 s), `StovlVaneStopJerk` (false).

## Checks run

- **Identity:** with `StovlVanes = false`, all 106 bench columns equal the 0.4.11 build on the six-jet matrix (30 runs), the STOVL regression set (12 runs) and the stop set (6 runs).
- **CTOL laws (not touched):** six pitch and roll cases, 461 FCS-log columns equal to 0.4.11; only the 11 wall-clock timer columns differ.
- **Regression set with vanes on, against 0.4.11:** vertical landings touch down at the same sink rates (gear compression within 3 cm); hover pedal starts and stops within 0.04 s; VTO unchanged; the mission case within 16 m of altitude. The full-lever short take-off differs as described above.
- **Build:** the source zip rebuilds the DLL byte for byte.

Bench scripts added: `vn.sh` / `vnm.py` (your jet), `st.sh` / `stm.py` (stops), `matrix50.sh` / `m50.py` / `idchk.py`, `final50.sh`. Model tools: `tools/vanebox/`.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.0 and `meta.json`; 0.4.11 kept as `Vortex_FS20_FCS.dll.0.4.11.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.0-release`.
