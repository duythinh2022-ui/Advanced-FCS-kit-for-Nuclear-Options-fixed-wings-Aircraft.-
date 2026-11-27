# KR-67 Ifrit FCS 1.7.1: HUD AoA-limit marker on jets with canards

Base: 1.7.0. Patch: display / log only, no flight-law change.

**Status:** compiled against the game's assemblies. Not flown.

## Tom (24 Nov 00:05)
"when i roll at max aoa, the aoa undershoots from 70 to 68. And the fdr hud shows the aoa limit marker at 65 instead of 70."

## Fixed
- **HUD limit marker.** The FCS API's AoA-limit value (`FcsApi` slot 8, read by the Flight Data Readout HUD) was still `AoALimitPositive` (65). It is now the limit the jet actually flies to (`AoAPosLim`): `AoALimitPositiveCanards` (70) on a jet with canards such as the Multirole MTD, `AoALimitPositive` on the stock Ifrit.
- **Startup log.** The "ready" line now reads `AoA +65 (jets with canards +70)/-60`. It is written before any jet exists, so it shows both limits.

## Not changed: the 70 -> 68 deg dip when rolling at the limit
From the 24 Nov 00:03 log (`Ifrit_FCS_20261124_000306.csv`, 1.7.0, MTD, AB): nine rolls at full aft.
- **The command holds.** `alphaCmd` stays at 69.0-69.6. The FCS is not lowering the limit in the roll.
- **The dip.** AoA goes 70.1 -> 68.6 deg, starting about 0.4 s after the roll input and recovering within about 1 s. The sideslip swings to +2.9 deg as the roll builds (p 21 deg/s, r 45 deg/s).
- **Why.** At 69 deg AoA a body roll turns sideslip into AoA loss. The rate is about tan(beta) x (p cos(alpha) + r sin(alpha)), roughly 2.4 deg/s at that point. The pitch loop only reacts after the AoA has dropped: `qCmd` 4.7 -> 13.3 deg/s, with `q` lagging it by about 3 deg/s.
- **Possible fix (needs a yes, shared logic).** Feed that kinematic term forward into the pitch-rate command, instead of waiting for the AoA error.
