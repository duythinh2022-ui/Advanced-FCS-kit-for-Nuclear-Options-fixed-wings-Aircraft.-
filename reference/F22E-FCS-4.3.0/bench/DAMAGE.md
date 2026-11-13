# Bench part damage (2 Nov 2026) — bench-only, FCS 2.15.0 source unchanged

What the game does to a damaged F-22E, on the bench: `sim/Damage.cs`, data `data/f22e_damage.json` (read from Aryx's
bundle by `extract/extract_damage.py`), suite `an/dmgsuite.py` (fifth block of `an/suite.sh`). Game sources: project doc
`claude/Game_damage_model_Raptor.md`.

## Keys (scenario fields; times are seconds from the pull start, like `engOutT` and `prof`)
| key | what |
|---|---|
| `dmg=Vstab_L:20+Flap_R:30+EngL:50` | hit points at t = 0 (part suffix, or `EngL` / `EngR` = the turbofan's own HP) |
| `dmgAt=40:Vstab_L:0+Vstab_R:0,42.5:Flap_R:30` | timed hit points |
| `detachAt=40:Vstab_L+Vstab_R` | timed forced detach (the damage tool's last stage) |
| `detach=...` | as before, at t = 0; with the model running, parts left hanging go too (a fin takes its rudder) |
| `ripOff=1` / `0` | load-driven joint breaking on a clean jet / off; default on whenever any damage key is set |
| `dmgCsv=path` | per tick: mass, parts attached, engine condition/operable, worst joint load ratio and part, events |
Part names match case-insensitively. Events also print to stderr as `# dmg t=... ...`. The model runs only when a damage key is set: with none, the bench is the
2.15.0 bench bit for bit (suite rev / cork / reg / util identical before and after).

## What it models (game behaviour)
- **Damaged, attached**: lift and profile drag x e, damage drag (dragArea + 0.1 wingArea)(1 − e), e = Lerp(0.5, 1, hp/100).
  The FCS sees e (it reads `wingEffectiveness`), as in game. A control surface's damage is its part's e; no jamming.
- **Coming off**: jointed physics (the player's jet). Each part whose own joints hold it on carries, every tick, the force and
  moment of everything hanging off it (m·a at each COM incl. rotation, own inertia, minus aero, gravity, thrust) about the
  mean of its joint anchors, against listed x 10 x hp/100. Over → its joints break; whatever loses its path to the root goes;
  mass, CG, inertia update and the integrator's CG state moves with them (no impulse: ω unchanged). The FCS drops the parts
  from its own body list on its 0.5 s rebuild, as in game. 0 HP = zero strength = off at any load.
  Where other parts' joints connect into a part and their owner reaches the root without it (a rudder hinged to the
  fuselage as well as the fin), the load splits with those joints in proportion to present strength; zero own strength
  breaks a part's own joints at once and it hangs on whatever still holds it. Approximation: a part's joints break together
  (PhysX: one at a time).
- **Engines** (Turbofan, this jet; the stock Turbojet model is in the same file for the Ifrit / Revoker benches): own HP − weight x (80 − hp) per critical part below 80 (engine, LERX, intake, shared Rear: 1; nozzle: 1.5),
  − 100 x weight per critical part detached; condition = hp/80 below 80, never rising; 0 = killed. Losing a LERX, intake or
  nozzle kills that engine; losing Rear kills both.

## Checks done
- Clean jet, 9.3 G at 1100 km/h with a roll: worst joint 19 % of its limit (Flap_L 173 of 900 kN); nothing comes off.
- Wingmid_L at 20 HP stays on (11 % at 5 G), trim ±1.9° flaperon split; at 0 HP it goes at once with Aileron_L + Wingtip_L,
  WingRear_L (0 HP) a tick later with Flap_L.
- Fins at 8 HP mid-pull (900 km/h): survive 8.8 G, tear off when the roll's sideslip loads arrive (114 kN of 112), rudders
  with them; FCS yawHealth → 0.
- Engines: EngL 40 → 0.50; Intake_L 40 → 0.75; Nozzle_L 60 → 0.875; LERX_L off → EngL killed; Rear off → both killed.

## Damage suite, 2.15.0 (tag `fin`)
No-fin push-overs from 65° at the flight's entry (560/600 km/h, push at ~215 kt with the corkscrew's yaw on) **depart**,
held and released (β 43–64°); clean twin 6–8°, one fin 5–9°. Fins + right flaperon at 890 km/h departs (the 18:25 log).
No-fin falling-leaf stops and reversals never complete (velocity roll ~28 deg/s 4 s after the stick is centred), clean 1.9–2.6 s.
The released push-over held ~20° in 2.13/2.14 flights: the bench is harsher there (sideslip ~2x high with fins, uncalibrated
without) — read against the twin.

## FCS findings from the suite (not fixed — a law change is Tom's call)
- **Roll target frozen off zero with the fins gone**: the 2.7.1 pace rate limit (FcsController l.8857–8873) lets the target
  move away from the yaw-implied velocity roll at capZ / sin a; with no fins the brake-side yaw capacity is ~0, so after the
  stick is centred the target holds at 10.7 deg/s while the yaw carries the jet at ~28 → β −9. Not DamageLatLimit (identical
  with it off; dormant at 65°).
- **DamageLatLimit** cuts pSmax / rMax as a push-over crosses the rudder fade (claude/Bench_damage_smoke_findings.md).
Same root: yaw rate treated as commandable when, with the fins gone, it is a state roll and pitch must follow.

## Same model on the Ifrit and Revoker benches
`Damage.cs` is shared verbatim with the stock-jet benches (Ifrit 0.7.3, Revoker 0.4.2 sources): data from the game's
resources.assets (`extract/stock_damage.py`), stock Turbojet engine model, `ifly` mode keys, suite `an/sdmgsuite.py`.
See their `bench/DAMAGE.md`.
