# Vortex FCS 0.5.8: pedal pirouette on a key; skid correction starts the moment the pedal is let go

18 Nov 2026. On 0.5.7. Replaces the first 0.5.8 (a key for `StovlTurnShare`), which is withdrawn: that change is gone and `StovlTurnShare` is 0.5.7's 0.85 with no key.

**Status: benched, not flown. The key handling is compiled only (it needs the game's input).**

Tom, 18 Nov:

> make it an assignable key-bind off by default in the dll, i can either tap or hold to toggle

> revert back to 0.5.7 and the hot key i meant is the pirouette not turn share and override 0.5.8

> with the pirouette logic on, no side skid will be corrected when there is yaw input

> the jet leveled off after i release yaw then a moment later the side skid is corrected instead instantly

## 1. The key

- **New key `StovlPedalPirouetteKey`** in the main config (`1. General`). It's unassigned by default, so nothing changes until you bind it.
- The pirouette stays **off by default** (`StovlPedalPirouette = false`). The key switches it the other way from that setting, so with the setting at false the key turns the pirouette on.
- **Tap** (released in under 0.35 s): latches the pirouette on. Tap again to turn it off.
- **Hold** (0.35 s or longer): the pirouette is on only while you hold the key.
- It goes back to the setting when the game loads and at every new mission.
- HUD message on every change: `STOVL PEDAL: PIROUETTE` / `STOVL PEDAL: COORDINATED`. The same line goes in the BepInEx log.

To set it, edit `BepInEx\config\Vortex_FS20.FCS.cfg` (e.g. `StovlPedalPirouetteKey = P`, or `P + LeftShift` with a modifier) or use the configuration manager.

## 2. The pirouette now follows the pedal exactly

**What your 18:30 log showed.** On 0.5.6–0.5.7 the pirouette stayed on after you let go of the pedal, until the yaw had stopped. In your log that was 1.15–2.5 s at 15–40 kt, and on the bench 1.1 s. During that time the jet kept flying its old track, so the skid wasn't corrected. On the bench the bank even went 5–7° the wrong way first. Only then did the correction start.

**Now:**
- **Pedal held:** the jet keeps flying the way it was going while the nose turns at the full yaw rate. No skid is corrected, including a skid the jet already had when the pedal came in. Only the side stick acts across the nose. 0.5.6–0.5.7 flew the lever's speed along the nose of that moment, so an existing skid was taken out during the pedal.
- **Pedal let go:** the side speed is taken out along the nose at once, while the yaw comes to rest. The yaw still stops as quickly as before: 1.1–1.2 s, 13–14° more heading.

Bench, your jet (13.8 t), key on, full pedal 4 s, released with the nose about 90° off the track:

| | 30 kt, 0.5.7 | 30 kt, 0.5.8 | 45 kt, 0.5.7 | 45 kt, 0.5.8 |
|---|---|---|---|---|
| Side speed at release | 15.1 m/s | 15.1 | 22.7 | 22.7 |
| First bank after release | 7° the wrong way | none the wrong way | 5° the wrong way | none the wrong way |
| Bank 10° toward the skid | +3.02 s | **+1.78 s** | +2.98 s | **+1.72 s** |
| Side speed under 1 m/s | +7.03 s | **+5.67 s** | +8.48 s | **+7.47 s** |
| Height band after release | −4/+6 cm | −5/+4 cm | −12/+14 cm | −8/+5 cm |

- **Nose 180° round at release (8 s pedal):** there is little side speed at release, so this is within 0.4 s of 0.5.7.
- **Skid already there at pedal-in** (full side stick 2 s at 30 kt, then 4 s of pedal): 3.3 m/s at pedal-in, 1.8 m/s at release (0.5.7: 0.2). It isn't held exactly. The track drifts a little while the nose swings, which is the pirouette's tracking lag.

**What still takes time after release.** The bank builds only as fast as the thrust margin allows. The tilt governor holds the tilt rate to 10–15 % for about 1 s, to keep the height. Meanwhile the jet also accelerates to the lever's speed along the new nose. So the correction starts at once, but the full bank arrives about 1.5–2 s later. That is the jet's limit under your height rule. Making it faster would cost height.

## Checks

- **Key never pressed:** the bench output is byte-identical to 0.5.7 on four cases: the freestyle flip case, the half-stick freestyle roll, and the 30 kt and 45 kt pedal turns.
- **Not tested:** the tap/hold timing and the HUD message run only in the game, so they are compiled only.
- **Build:** the offline smoke test under Mono passes (all type initializers OK). The source zip rebuilds the DLL byte for byte.
- **Bench script:** `prel.py` (release and pedal-in metrics).

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.8 (this build) and `meta.json`. 0.5.7 is kept as `Vortex_FS20_FCS.dll.0.5.7.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.8-release` (overwritten).
- The `StovlTurnShareKey` / `StovlTurnShareKeyValue` lines left in your config by the first 0.5.8 now do nothing. BepInEx keeps them in the file.
- Restart the game to load it, then bind the key.
