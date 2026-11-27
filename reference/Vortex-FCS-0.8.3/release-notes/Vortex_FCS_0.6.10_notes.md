# Vortex FCS 0.6.10 (21 Nov 2026)

0.6.9 plus one fix: after a STOVL landing, a stick pull takes off again. Same attested native core, embedded.

## What Tom reported (log 11:44, 0.6.9)
At the end, after landing, he couldn't take off: the HUD said IDLE, and he had to move the throttle to take off.

## What the log shows
- **228–232 s:** a released descent.
- **~232 s:** touchdown with the stick centred.
  - The lever was at 22.4 %, 0.4 % forward of the zero-GS detent's edge (detent 20 % ± 2 %). That is a +0.45 kt command, which the HUD shows as "+0 KT".
  - After 0.5 s on the wheels the FCS counted it as a landing and latched the touchdown idle.
- **The latch (0.3.7) let go only when the lever went into the detent or 5 % forward.** The stick pulls at 239, 241, 242 and 243 s did nothing.
- **245 s:** he moved the lever, the latch let go, and the jet lifted.

## Change
The touchdown idle also lets go on a stick pull for a climb (stick past 10 % aft). The latch exists to keep a landing's lever from rolling or lifting the jet by itself, and a pull is as clear a "go" as the lever.
- Dev switch `StovlIdlePullRelease` (on); false = 0.6.9.

## Bench (12.1 t, lever at 22.4 %, full-stick descent to the wheels, stick centred, full pull 6 s later)
- **0.6.9:** stays at idle on the ground through the pull.
- **0.6.10:** lifts off on the pull and holds 10.2 m after the release (drifting 0.4 m/s, the lever's +0.45 kt).
- **Stick left centred after the landing:** stays on the ground in both.
- **Landing regression set:** touchdown sinks identical, gear compression within 3 mm. Hover/VTO/STO/mission cases as 0.6.9.

Benched, not flown.
