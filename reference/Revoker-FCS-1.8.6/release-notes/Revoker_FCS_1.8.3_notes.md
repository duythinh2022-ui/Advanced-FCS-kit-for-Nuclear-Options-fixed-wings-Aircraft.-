# FS-12 Revoker FCS 1.8.3: Typhoon inner flaperons, second attempt

Base: 1.8.2. **Status:** compiled against the game's assemblies. Visual only; the flying is identical to 1.8.2. **Not seen in the game.**

Tom (24 Nov 15:19): "fix the dip and the inner flaps".

## Typhoon inner flaperons
The 15:19 LogOutput.log (1.8.2) shows the 1.8.2 fix found no inner-flap candidate: `Typhoon_flaperon_in_L (span centre -2.39) <- aileron_L_visible (candidates: aileron_L_visible at -4.50)`. 1.8.2 looked the inner flaps up by the name `flap_*_visible`, and nothing on the jet has that name in the game.

1.8.3 takes the inner flaps' visuals from the FCS's own inner-flap effectors: the transforms it moves for them, on this jet. The panel angle is also read about whatever axis the driver turns, projected on the panel's hinge; this reads the ailerons exactly as before. The log line now names each panel's driver, its candidates and their span centres, and the twice-a-second diagnostic names the driver too.

Expected in LogOutput.log: `Typhoon_flaperon_in_L ... <- <inner flap visual>` with the aileron no longer on the inner panel.

## The AoA dip when a roll is stopped at the limit: not fixed
See claude/Revoker_roll_stop_dip_findings_2026-11-24.md.
