# F-22E Strike Raptor FCS 2.4.15

Controller fix: `ThrottleIndependentAirbrake = false` now gives the stock airbrake controls. The same fix ships in Ifrit FCS 0.5.5 and Revoker FCS 0.2.6. Nothing else changed.

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

**F-22E-only parts of "fully stock" (key off):**
- **On the ground:** the speedbrake panels open at zero throttle on the ground too. 2.4.14 never opened them on the main wheels.
- **No AoA fade:** the panels open fully. `AirbrakeAoAFade` only applies with the key on.
- **No rollout aero-brake:** stock has none, so the flaperons, stabs, nozzles and rudders no longer configure for rollout drag.

## Verified
- Built, and the diff is limited to these lines.
- **The shared airbrake-command line** was run on the Revoker bench, where the code is the same:

| Key | Throttle | Brake held | Airbrake |
|---|---|---|---|
| off | 1 | yes | **stays shut** |
| off | 0 | no | open (stock) |
| on | 1 | yes | open |
| on | 0 | no | shut |

- **Not run:** the F-22E-only ground, fade and aero-brake gates (the 2.4.14 bench no longer compiles against the current sources). **Not flown.**

## Install
`BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 4b5224d9c1d90cc6866f62a4d42aa95d). The dev DLL is unchanged (2.4.14).
