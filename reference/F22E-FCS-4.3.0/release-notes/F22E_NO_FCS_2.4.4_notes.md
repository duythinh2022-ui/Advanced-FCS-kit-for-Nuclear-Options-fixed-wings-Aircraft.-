# F-22E FCS 2.4.4

Base: 2.4.3 as last installed. One change: military (dry) thrust +5 %, fuel per throttle setting unchanged.

## Military thrust x1.05
New dev key `MilThrustScale` (5. Airframe), **1.05**, every variant (Stock included, like `ThrottleResponseScale`). It multiplies the engines' `staticThrust` at the airframe setup, on top of the variant's `ThrustScale` (Stock 1.0 → 1.05 dry; Buffed 1.08 → 1.134 dry). The afterburner's own thrust (`JetNozzle` afterburner `thrust`) is not touched, so the max-AB total rises only by the dry share.

**Fuel.** From the IL of `Turbofan.FixedUpdate`: fuel flow = `Lerp(fuelConsumptionMin, fuelConsumptionMax, spoolRatio)`, where `spoolRatio` = (RPM − 0.85 minRPM) / (maxRPM − minRPM) — the RPM, not the thrust. Thrust = (staticThrust × dynamic factor − parasiticThrustLoss) × spoolRatio. Raising `staticThrust` changes the second and not the first, so at any throttle setting the fuel used is the same as 2.4.3 (the buffed variant's `FuelConsumptionScale` 0.75 applies as before; the afterburner's fuel is its own term and unchanged). Thrust-specific fuel consumption at mil power is therefore 1/1.05 = 4.8 % lower.

The FCS's own thrust models (T/W, the damaged-engine match, the split prediction) read `staticThrust` after the setup, so they see the new value. Applied once per engine instance (the existing `tvcFixed` guard).

## Install
- **Installed:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 1ca796a821c7619096c14139cd8fbf0c), `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (45d1c6ee81ba291363314fc20fdabb25), `meta.json` 2.4.4.
- **Backup:** 2.4.3 is `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.3.bak` (8afa79ec633c5f25ef38cfbc645c935e).
- **Config:** new dev key `MilThrustScale` 1.05. Takes effect on the next spawn. `LogOutput.log` setup line now reads "thrust x…, mil x1.05".
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.4-release`.

## Not verified
- Not flown; compile-checked. The fuel law is read from the game's IL, not measured in flight.
