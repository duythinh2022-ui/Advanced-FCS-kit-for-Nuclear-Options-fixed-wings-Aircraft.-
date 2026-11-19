# Ground-steering bench (F-22E 2.4.10 / Ifrit 0.5.0)

- `gsim.py`: yaw-plane model of the game's `LandingGear.FixedUpdate`, read from the IL with `ildump.py`. It has the tyre law, castor, steering slew and differential brake. Speed is held constant and there is no aero, so the rudders are not modelled.
- `fit.py`: replays the 27 Oct Ifrit taxi log (`Ifrit_FCS_20261027_105534.csv`) through the model. The fit gives Iz = m × 5² and 24 % of the weight on the nose.
- `gsfbw.py`: compares stock against the 2.4.9 / 0.4.9 capped FBW and the stock-matched FBW. Pedal steps at 3–35 m/s.

Gear data from the game files:
- **Ifrit:** `Multirole1`, resources.assets.
- **F-22E:** Aryx bundle, cut from `Aryx_F22E_StrikeRaptor_1.0.4.dll` at the UnityFS signature.

## 2.4.14 / 0.5.4: INDI nose wheel
- `gsim2.py`: replaces gsim's tyre with the IL's exactly. The lateral force is perpendicular to the contact velocity, and scrub and brake drag run along −velocity, with the total clamped to μN. gsim.py put the lateral force perpendicular to the wheel and the drag along the wheel. Below the saturation slip the two agree; past it they don't.
- `gsindi.py`: 2.4.13 against the INDI nose wheel. Covers release braking after 3 s full and half pedal turns at 4–25 m/s. Use `sweepK.py` for the gain, filter and slip-margin sweep.
- `holdbench.py`: line hold with a 6 deg/s yaw kick and 3 deg/s² rms yaw gusts. The plant is heavier, or its CG further aft, than the controller assumes.
- `loadsweep.py` / `loadbound.py`: the stock law across mass, yaw inertia and CG, and the best a bang-bang loop can do on the tyre.
- Results are in `results_2414.txt`.
