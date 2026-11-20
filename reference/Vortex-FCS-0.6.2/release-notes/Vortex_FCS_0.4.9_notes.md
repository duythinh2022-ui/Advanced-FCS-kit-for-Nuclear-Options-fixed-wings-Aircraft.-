# Vortex FCS 0.4.9: trailing edges up instead of drooped after a carrier take-off

18 Nov 2026. Built on 0.4.8. One fix, in how the FCS knows which way each control surface moves its trailing edge. **Benched, not flown.** The bench has no ship; the wind over the deck is imitated with a steady wind.

Tom, 00:33:

> when i take off from carrier, the trailing surfaces are suppsed to droop down, and rudders fare out, but the jet makes the trailling flaps deflect up, even i switch to ground relative speed and at zero gs. The surfaces still not pose correctly.

## What the logs show

Hover pose in the three logs of 17 Nov (surface commands, the log's own sign: negative is trailing edge down):

| Log | Flow at spawn | Elevons | Ailerons | Rudders |
|---|---|---|---|---|
| 22:56, land, no wind | none | −25 / −25 | −25 / −25 | −20 / −20 (flared) |
| 22:54, ship, facing the wind | 16 m/s from ahead | −17 / −17 | −21 / −25 | −10 / −29 (flared, plus yaw) |
| 22:55, ship, tail to the wind | 16.8 m/s from behind and the left | **+24 / +25** | **+22 / +26** | −23 / −19 (flared) |

In the 22:55 flight the trailing edges are 25° **up** from lift-off to the end, including the 10 s after the jet had turned to face the wind. Its numbers are the 22:54 flight's with the sign turned over (the elevons sit at +14.67 and +6.67 where the other flight has −14.67). The rudders are right in all three.

## Cause

- **What the FCS needs.** For each surface, which sign of deflection raises its trailing edge (`teUp`). The droop is "25° the other way".
- **How it got it.** It learned the sign from the first airflow over 10 m/s, by comparing the surface's local angle of attack at +5° and −5° of deflection, then kept it for the rest of the flight. The rule assumes the air comes from the front.
- **Where that breaks.** With the air coming from behind, the comparison reads backwards. From the side it is decided by whichever small fore-aft component there is. On a carrier the first airflow is the wind over the deck, from wherever the jet happens to face. In the 22:55 flight the jet was spawned tail to a 16.8 m/s wind, all four trailing-edge surfaces learned the sign reversed, and the droop came out as 25° up.
- **Why the switch and zero ground speed didn't help.** The sign is learned once and never looked at again. It has nothing to do with the speed reference.
- **Why the rudders were right.** Their flare takes its direction from the hinge geometry, not from the learned sign.
- **What else used the wrong sign** on those surfaces in that flight: the flap droop and the ground brake.

The same learning line is in the other FCS mods (it came from the F-22E's code). Not changed here; it only bites when the first airflow over 10 m/s is from behind or the side.

## Fix (`TeUpFromGeometry`, Dev file, `5. Airframe`, default true)

The sign is taken from the hinge's geometry, which doesn't depend on the wind. The flow rule is kept only for a surface whose geometry gives no sign (a hinge near the vertical), and then only in air that runs from the leading edge to the trailing edge; until then nothing is learned. `false` gives 0.4.8.

On this jet the geometry and the forward-flow rule agree on all six surfaces, so nothing changes in flow from ahead.

## Bench (13.8 t, Tom's settings, vertical take-off into a steady 17 m/s wind, hover)

Signs learned (geometry says +1 on all six):

| Wind from | 0.4.8 | 0.4.9 |
|---|---|---|
| ahead | all +1 | all +1 |
| behind | all six −1 | all +1 |
| the left | left elevon and aileron −1, right +1 | all +1 |
| the right | left +1, right −1 | all +1 |
| behind and the left | elevons and ailerons −1, rudders +1 | all +1 |

Hover pose (trailing edge down is positive here):

| Wind from | | 0.4.8 | 0.4.9 |
|---|---|---|---|
| behind | elevons L / R | −25 / −25 (up) | +25 / +25 |
| | ailerons L / R | −26 / −24 (up) | +25 / +25 |
| | rudders, outboard | +20 / +20 | +20 / +20 |
| the left | elevons L / R | −15 / −7 | +20 / +30 |
| | ailerons L / R | −31 / −19 | +25 / +25 |
| | bank | swinging −0.3 to +0.8° | steady −5.5° (leaning into the wind) |
| | drift over 10 s | 2.5 m | 1.3 m |
| ahead | every column | | identical to 0.4.8 (difference 0.0) |

The "behind" row reproduces the 22:55 log.

**No wind.** Reference jet, all five case sets: every column identical to 0.4.7 / 0.4.8 (difference 0.0).

## Not verified

- **Flown: nothing.**
- The other five jets of the matrix and the CTOL regression set were not re-run. With no wind the sign is learned in forward flight, where the old rule and the geometry agree, so they should be unchanged; I ran only the reference jet.
- In a 17 m/s crosswind hover the jet holds 5.5° of bank and drifts 1.3 m in 10 s on the bench. I did not look into that further.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.4.9 and `meta.json`; 0.4.8 kept as `Vortex_FS20_FCS.dll.0.4.8.bak`.
- Dev DLL unchanged. No config change.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.4.9-release`.

## To check in flight

Spawn on the carrier facing aft (the 22:55 case), convert, lift off: elevons and ailerons should droop about 25° and stay drooped through a pedal turn.
