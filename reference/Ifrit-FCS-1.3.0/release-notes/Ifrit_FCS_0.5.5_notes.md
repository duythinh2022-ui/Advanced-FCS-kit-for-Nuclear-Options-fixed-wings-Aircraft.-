# KR-67 Ifrit FCS 0.5.5

Controller fix: `ThrottleIndependentAirbrake = false` now gives the stock airbrake controls (split decelerons). The same fix ships in F-22E FCS 2.4.15 and Revoker FCS 0.2.6. Nothing else changed.

## What the stock game does (from the game code)
- **`Airbrake.Update`** opens the airbrake while `controlInputs.throttle == 0` and closes it otherwise. It never reads the brake.
- **Split surfaces** (`ControlSurfaceJob_Math`) split to `maxSplit` at throttle == 0 and nowhere else.
- **The `Brake` action** (`PilotPlayerState.PlayerControls`) only sets `controlInputs.brake`, which only the wheel brakes read.
- **`Countermeasures`** fires when airborne (radar altitude > 0.2 m).
- **So in stock, on a controller with Brake and Countermeasures on one button:** in the air the button pops countermeasures and touches the wheel brakes, which do nothing in the air. The airbrake stays shut.

## What was wrong
With `ThrottleIndependentAirbrake = false` the FCS opened the airbrake on **brake key OR zero throttle**. The brake key was never taken out. So with Brake and Countermeasures on one button, every countermeasure press opened the airbrake.

## Fix
- **`ThrottleIndependentAirbrake = false`** is now the stock rule only. Zero throttle opens it (air and ground). The brake key is the wheel brake only.
- **`ThrottleIndependentAirbrake = true`** (default) is unchanged. The brake key opens it, and zero throttle doesn't.

## Verified
- Built.
- **The same code** was run on the Revoker bench. With the key off and the brake held at full throttle, the airbrake and splits stay shut. At zero throttle they open. With the key on, nothing changed.
- **Not flown.**

## Install
`BepInEx\plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 ab2aac126e7fe1aaed3016a670845c88). The dev DLL is unchanged (0.5.4).
