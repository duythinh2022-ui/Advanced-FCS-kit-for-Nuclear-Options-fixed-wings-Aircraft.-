# FS-12 Revoker FCS 1.6.1: hinge visuals step aside for a replacement model (EF-2000 Typhoon mod)

Base: 1.6.0. Patch: visual only, no flight-law change.

**Status:** compiled against the game's assemblies; the bench builds. **Not checked in game** (the first 1.6.1 build was, and failed; see below).

## Tom (24 Nov 01:15)
"i have the eurofighter skin downloaded for the revoker, but the trailing flaps fix kinda be broken visually with the skin. Can u fix that, keep it visually the original of the skin." And at 01:16: "also the inner flaps are slightly detached when deflected".

## What was wrong
The "skin" is the TyphoonMod plugin (EF-2000 Typhoon 1.0.0 by Assassin). It is a full model swap, not a texture:
- It hides the Revoker's meshes with `forceRenderingOff` (log: "76 original meshes hidden").
- It draws its own Typhoon parts. Its 26 moving surfaces turn about their own hinge lines, by the angle it reads off the game's surface transforms (`typhoon_surfaces.txt`).

The Revoker FCS's hinge fix (`VisualHinge`, 0.2.3) draws its own render-only copy of each outer aileron (`aileron_L/R_visible_hingeVis`). The Typhoon mod builds its hide list before that copy exists, so the copy was still drawn: the grey stock Revoker ailerons standing off the Typhoon's wingtips in the screenshot.

## Fixed (1.6.1 rebuilt in place, 24 Nov 01:30)
The first 1.6.1 build did not hide the copies in game. Tom's 01:30 screenshot still showed the copied outer aileron standing off the Typhoon's wing. The test it used was "more than half of the Revoker's level-of-detail-0 renderers switched off", and its log line never appeared. That list evidently holds renderers the Typhoon mod leaves on, so the test missed. The cause was not confirmed in game.

The rebuilt 1.6.1 tests what is directly under each copy: the Revoker wing part the aileron hangs on (`wingtip_L` / `wingtip_R`).
- **How the wing is found:** by name among the aircraft's parts. Its own mesh, or else its largest full-detail mesh, leaving out the aileron, `_lod` copies, and hardpoints / pylons / stores.
- **Trigger:** when that mesh is switched off by anything (`forceRenderingOff`, disabled or inactive), the copies are hidden. The old half-of-the-airframe test stays as a second trigger.
- **What the Typhoon mod does:** its early hide sets `forceRenderingOff = true` on the base meshes it covers. The Revoker's wing is one of them, since it is not drawn under the Typhoon.
- **Logging:** a one-time line about 3 s after spawn, `Revoker FCS hinge visuals check: base renderers k/n off; wingtip_L: forceRenderingOff ..., enabled ..., active ...; copies hidden/drawn`. If the copies are still drawn, this line shows why.
- **When the copies hide:** the line "the hinge copies are hidden too" is logged.
- **What is unchanged:** without a replacement model nothing changes, and the Typhoon's own surfaces are untouched.

## Not changed (it is in the Typhoon mod): the inner flaperons look detached when deflected
In `typhoon_surfaces.txt` the Typhoon's inner flaperon is driven by `aileron_R_visible+flap_R_visible`. Its update adds the two surfaces' deflections, each projected on the Typhoon hinge, and clamps the sum to ±45. The outer flaperon is driven by `aileron_R_visible` alone.

Revoker geometry (bench airframe):

| Revoker surface | Span | Typhoon panel at the same span |
|---|---|---|
| Inboard elevon `flap` | about 2.4-3.6 m | inner flaperon, 1.22-3.56 m |
| Outer `aileron` | about 3.7-5.3 m | outer flaperon, 3.56-5.12 m |

The Revoker's inner and outer elevons both move in flight, under the FCS and under the stock FBW alike. So the Typhoon's inner panel turns by the sum of the two, up to about twice the outer panel's angle. That opens a step at the joint between the two panels and at the inner panel's hinge.

A one-word change to the Typhoon mod's data file would map each panel to the surface under it: `flap_R_visible` (and `flap_L_visible`) for the inner flaperons. But the TyphoonMod licence does not allow changing its files without the author's written permission. So the FCS does not touch it. The author takes requests as issues on the mod's GitHub page.
