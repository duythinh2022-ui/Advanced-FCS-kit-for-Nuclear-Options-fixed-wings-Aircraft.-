# F-22E FCS 2.17.0 — the latched window asks the yaw for its whole capacity, not a quarter of it

Base: 2.16.0. **Compiled + bench-checked, not flown.** 2.16.0 kept as `.dll.2.16.0.bak`. One constant re-derived
(`PaceLeadT` 0.1 s → 1 / `KYaw`), nothing else in the law; a second variant (`PaceHoldPaced`, off) left in for the bench A/B.
No tuning values touched.

## The 23:30 flight (2.16.0, both fins off at 53 s) — "still not fixed"
Confirmed in `LogOutput.log`: `Loading [F-22E Strike Raptor FCS 2.16.0]` (the 23:11 log was 2.4.16 — the mod manager had
replaced the plugin folder at 22:49).

- **Reversals at 65° without fins are hesitant, and the data says why.** 110 s (stick right → left at 109.7, 98 kt): the roll
  target went **back to +19…+23 deg/s and sat there for 2.5 s** against a −30 demand (`paceHold` −35…−54); the body roll
  drove +X to hold it and the rotation re-accelerated the old way (velocity roll 15 → 24 at 112 s); the zero crossing came
  4.0 s after the stick. 79.6 s: 3.3 s. Onsets from rest take 2 s to 40 deg/s (0.5–1 s with fins).
- **Push-overs:** released at 117 s (64°, 161 kt, 34 deg/s on): 19°. Held at 133.6 s (63°, 105 kt, 28 deg/s): 18°,
  recovered. Held at 156.3 s (63°, 129 kt, 48 deg/s): β 81°, tumble, recovered at 178 s.

Both are the **pace window**, latched on the yaw saturation as 2.7.2 designed it (on at 12.5 % of the table authority unmet,
off at 6 %). Once latched, its band is the yaw-implied roll ± a lead, and the lead is `max(margin lead, capacity × PaceLeadT)`
— the margin lead goes to zero as the sideslip uses the 4° margin, which leaves `capacity × 0.1 s`: with 10–20 deg/s² of
yaw capacity, **1–2 deg/s of lead**. The roll target is then pinned to the rotation the jet already has, the yaw loop (gain
`KYaw` 2.5 /s) sees a 1 deg/s error and asks for ~2.5–5 deg/s² of its 10–20, the rotation does not change, the sideslip
does not change, the latch does not release: a deadlock until the allocator's unmet report happens to fall under 6 % two
seconds later. In the held push-over the same band cut the roll target to −10…−23 while coordination needed −30…−54
(p sin α − r cos α = +9 deg/s, the measured sideslip growth). With fins the rudders break the deadlock in a few tenths; it
has been there since 2.7.2 and only the fin-loss case holds it long enough to see.

2.7.1's own comment at the lead floor says what it should be: *"the floor is what the yaw loop needs as error to ask for
that capacity (its gain KYaw)"* — capacity / KYaw = 0.4 s of capacity at the default gain, not 0.1. 2.7.1 covered the gap
with the target-pacing feed-forward; 2.7.2 switched that off (Tom: nothing paces the trajectory) and the 0.1 stayed.

## Change
`PaceLeadT` −1 → the lead floor is `capacity / KYaw` (`1 / Mathf.Max(0.5, Plugin.KYaw)`). The band, its margin lead, the
latch and the release are unchanged. On the 110 s case the yaw would be asked for its 10–20 deg/s² at once: a 17 deg/s yaw
reversal in ~1–1.5 s instead of a 2.5 s freeze. Not a bench-verifiable number — the bench's no-fin airframe sheds ~3 deg/s² of
yaw at the 65° stop where the flight sheds 11–23, so its no-fin leaf stops and reversals never complete on any build.

Also in the source, off: `PaceHoldPaced` — the latched window paces the target toward the demand at capacity / sin α
instead of clamping to the band. It removes the deadlock more completely (no-fin push-overs 74/56/75/66 → 31/34/10/18 on the
bench) but takes the band's protection away where it is legitimate (one-fin reversal 5.4 → 9.2°, fins + flaperon 23 → 46°,
clean regression set +1–4°). Kept for the A/B, not shipped.

## Bench (sideslip ~2× high), 2.16.0 → 2.17.0
Clean reversals: time to zero 0.2–0.7 s shorter (fullaft 500: 2.73 → 2.02 s, 300: 2.92 → 2.45, pull+roll 450: 1.92 → 1.72);
sideslip fullaft 500 7.7 → 12.0, 0.5-pull 400 4.2 → 6.3, the rest ±1. Corkscrew entries: entry sideslip +1.5–3° (8.3 → 10.6,
6.9 → 10.0, 6.5 → 8.8). Regression set: ±0.3 except fullaft+roll+rev3s 320 5.8 → 4.7 (better) and push-then-pullthrough 450
5.2 → 6.7. Utilisation invariant: 4 FAIL / 9 as before (§4 a/b). Damage suite: no-fin push-overs 74 / 56 / 75 / 66 → 66 / 47 /
**16 / 20** (the 600 km/h entries no longer depart; the 560 ones still do, on the bench's 3 deg/s² of shed); no-fin leaf
stops/reversals 9.1 / 6.8 / 6.3 / 5.1 → 7.2 / 5.4 / 6.4 / 5.3 (none complete, see above); one-fin cases ±0.4; fins at 8 HP
rolling pull 9.5 → 16.6; fins + flaperon 23 → 25.

**What you may feel on the clean jet:** corkscrew entries and the full-aft reversal ~1–2° more sideslip (half the bench
numbers) for reversals 0.2–0.7 s quicker. If the entries get dirty, `PaceLeadT` back to 0.1 is the A/B — and the no-fin
deadlock comes back with it; the honest middle would then be the window's margin, not the lead.

**Correction to the 2.16.0 notes:** there is no `latFloor` telemetry column; the field exists in the controller but was not
added to the writer. The floor's effect is read from `pSmax` against r / sin α.

## For the next log
No fins at 65°: reversals (the 110 s case — target should move at once, zero crossing ≤ 2 s), stops, both push-overs; one
clean corkscrew entry at 300–350 kt for the entry sideslip. Check `LogOutput.log` says 2.17.0.
