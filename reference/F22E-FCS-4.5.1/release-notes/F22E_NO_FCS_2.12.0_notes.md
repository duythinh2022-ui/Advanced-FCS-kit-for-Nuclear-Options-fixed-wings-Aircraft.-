# F-22E FCS 2.12.0 — the fin-loss roll limit fades with the rudders

Base: 2.11.0. **Compiled + bench-checked (clean-jet suite identical — the bench has its fins), not flown.** 2.11.0 kept as
`.dll.2.11.0.bak`. One law change, damage case only.

## From the 18:32 flight (2.11.0, both fins shot off at 40–43 s, then to 66° AoA)
With both fins gone the velocity-vector roll was held to 20–33 deg/s at every AoA — not by the governor (the window held
65–80 % of the ask below 50°, 24 % of the time at 50–65°, 0 % above) but by the **raw stick limit** `pSmax` 25–46: `DamageLatLimit`
(2.x) scales the body yaw-rate limit `rMax` by fin health (×0.35 with both fins gone, floor), at every AoA. At 65° the
velocity roll is almost all body yaw (pS = r / sin α), so rMax 24 → 28 deg/s of velocity roll, with the sideslip at 2–6°
and the margin untouched. Tom: "isn't that when the rudders can't really do any yaw, so why is it capped?" — correct: past the
rudder fade the fins are not the yaw axis. The measured capacity says the same: `capZ` 38 deg/s² at 50–65° without fins
against 39 with (18:25 log), where at 15–40° it is 29–33 against 125–147.

## Change
The fin-health factor in `DamageLatLimit` is faded with `rudderAuth` (1 below 50° AoA, 0 above 60°): `finH = Lerp(1,
yawHealth, rudderAuth)`. Above 60° a fin loss takes nothing off the roll limit; below 50° it is as before; the asymmetric-load
share (`yawLeft`, already rudderAuth-aware) is untouched. Expect the no-fin corkscrew at the normal high-AoA rate, paced by
the governor on the measured capacity like the clean jet.

Below the fade the open-loop factor stays for now: the 18:08 log (541 s, both fins, 295 kt / 30° AoA) showed the estimator
reading 99 deg/s² toward the reversal side in the steady roll and 5–23 once the reversal loaded the flaperons for roll —
without fins the yaw comes from the roll surfaces, so a probe that holds the roll target unchanged promises yaw the reversal
cannot have (17.6°). That is the rate cap's reversal model with a two-axis probe — next — and `DamageLatLimit` goes with it.

## Bench
Reversal suite, corkscrew entries, regression set, utilisation invariant: identical to 2.11.0 (fins present).

## From the 18:25 flight
Not a high-AoA test: fins intact to 152 s (89° AoA, 50–65° rolling at sideslip p90 0.7°), then at 460–480 kt level: left fin
151.7 s, right flaperon + aileron frozen 156.6 s, right fin 160.1 s, yaw divergence r −8 → −30 deg/s and sideslip 3 → 50° in
1.6 s with +26 deg/s² asked and 120–155 unmet — the left wing's surfaces at their stops and the stabs split ±12°, which is all
the yaw there is at 0° AoA with no fins: a fuselage without a vertical tail at 480 kt. Not a law item.
