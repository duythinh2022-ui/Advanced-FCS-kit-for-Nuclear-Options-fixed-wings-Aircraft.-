# FS-20 Vortex FCS 0.2.10 — in-flight control-surface airbrake; STOVL lever scale for HUD mods

Build of 17 Nov 2026. **Benched, not flown.** On top of 0.2.9 (STOVL CTOL blend), which is unchanged.

## Why
- The airbrake didn't move any control surfaces in the air (11:11 log, 131-141 s at 135-140 m/s, airbrake fully open). The common deflection of the aileron pair stayed at about 0. The stabs held their trim at 1-3 deg and the rudders stayed at 0.
- The in-flight set (TrailingEdgeBrakeConfig) was ported from the Revoker. There, the inboard elevons (HingeFlapEffector) go trailing edge down, and the outboard elevons get the trailing-edge-up share that cancels their lift.
- The Vortex has no inboard set (log: "inboard elevons 0", surfaces elevator = Stabilator, aileron, rudder). So the lift-neutral solve balanced against nothing and returned 0 every tick.

## What changed (`FcsController.cs`, shared TrailingEdgeBrakeConfig path)
- **Airframes without an inboard elevon set (the Vortex): the in-flight set is the rudders plus the wing pair.**
  - **Rudders split, trailing edges outboard,** by `AirbrakeRudderFlare` (new dev key, 25 deg). This matches how the F-22E's rudders show the brake. The split is held through the pair cost (×50, as the elevon set), so the yaw differential stays the allocator's.
  - **The wing trailing-edge pair goes trailing edge up** by the share whose pitch moment cancels the rudders' split. The share comes from the surfaces' own models, scanned 0..`AirbrakeOutboardElevonUp` in 0.5 deg steps.
  - **Flare, not toe-in:** on the canted fins a flare pitches nose down and the trailing-edge-up pair pitches nose up, so the two balance. In the bench, toe-in plus the pair ran the stabs to their stop (-15 deg) and held 1.5-1.8 g.
- **Set-in rate without an inboard set: 1 /s.** At the actuator rate, the pitch loop's catch-up showed as 0.62-1.43 g at 700-1200 km/h; at 1 /s it is 0.86-1.10 at 700-900 km/h.
- **Unchanged parts:** the AoA fade still applies (full to 15 deg |AoA|, stowed at 35), and the set is still off on the wheels, where the rollout aero-brake takes over.
- **Not in the STOVL hover schedule.** The set is scaled by 1 - stovlSurfW, so it doesn't fight the STOVL droop / flare. The STOVL deceleration's airbrake there is the dedicated panel.
- **Airframes with an inboard set (Revoker, etc.):** unchanged.

## FcsApi v5 (`FcsApi.cs`), for the Flight Data Readout HUD
- `GetStovlThrottle(Aircraft, float[7])`
  - v[0] aft detent (zero ground speed), v[1] detent half width, v[2] centre detent
  - v[3] commanded ground speed (kt)
  - v[4] regime: 0 hover GS command, 1 at speed, 2 CTOL blend thrust
  - v[5] CtolW
  - v[6] lever in the 0 GS detent
- `GetStovlThrottleText(Aircraft)`: the FCS's own gauge readout ("0 GS", "+12 KT", "HOLD SPD", "THR 75%").
- Both answer only in STOVL mode (state On, not freestyle).

## Bench
`ifly`, 2000 m, throttle 0.6, airbrake held 3.2-9.2 s. "Extra decel" is the drop in mean dV/dt over 5-9 s, 0.2.9 → 0.2.10.

| km/h | nz 0.2.9 | nz 0.2.10 | wing pair (TE up) | rudders | extra decel m/s^2 | max \|beta\| |
|---|---|---|---|---|---|---|
| 300 | 0.98-1.05 | 0.97-1.04 | 1.5-6 | -4 .. -17 | 0.16 | 0.01 |
| 500 | 0.98-1.04 | 0.94-1.01 | 1.8-6.5 | -4 .. -17 | 0.47 | 0.03 |
| 700 | 0.96-1.06 | 0.86-1.03 | 1.8-6.5 | -4 .. -19 | 0.93 | 0.04 |
| 900 | 0.93-1.09 | 0.78-1.10 | 1.8-6.5 | -4 .. -19 | 1.35 | 0.09 |
| 1200 | 0.88-1.18 | 0.63-1.20 | 1.8-6.5 | -4 .. -19 | 1.90 | 0.08 |

**Manoeuvres with the brake out**
- Full roll reversal: roll rate unchanged (±193-252 deg/s). |beta| 3.6 at 400 km/h, against 1.6 on 0.2.9.
- Full pull at 800 km/h: q 36 against 28 deg/s, nz 9.06 against 8.78.

**STOVL suite:** hover, vertical landing, VTO and STO give identical output to 0.2.9. Mission altitude differs by 5 m.

## Open / not checked
- Not flown. The bench has no dedicated airbrake panel, so the totals in game are the panel plus this set.
- The rudders reach 17-19 deg of the 25 asked. The allocator gives some of the split back to pitch.
- A 0.63 g dip at 1200 km/h on set-in.
