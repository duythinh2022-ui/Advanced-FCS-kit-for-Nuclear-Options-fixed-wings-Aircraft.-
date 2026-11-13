# F-22E FCS 2.31.1 — cleanup, behaviour identical to 2.31.0

Base: 2.31.0 (flown and accepted, 17:43 log). **Compiled + bench-proven identical, not flown (nothing to fly: same law).**
No tuning value, player or Dev key touched; CSV unchanged (310 columns). FcsController 9610 → 9499 lines. 2.31.0 kept as
`.dll.2.31.0.bak`.

## What went (Tom, 17:58: "do it")
1. **`RudderWindowSweep`** (2.25.0 rudder stall window in the nose-down sweep) and its three statics and debug field — off since 2.27.0.
2. **Dead A/B branches** — every static below was constant false, set by nothing (no player / Dev key, no bench script):
   - `PaceRateLimitOn` — the 2.7.1 pace rate limit (off since 2.7.2) and its now-unused `PaceRateCapEst`;
   - `PaceWindowOn` — the 2.7.1 margin window (the latch on the allocator's unmet stays, unchanged);
   - `CoupRollMeasured` — the 2.6.9 measured coupling roll cap (the table version, the one that flies, stays);
   - `PaceHoldPaced` (the `PaceHoldPacedByRudder` path stays), `PaceAirframeAxis`, `AuthCapRudderToe`;
   - `RpMainStall` / `StabWindowAlways` / `RpMainAoA` — the 1.8.40 rolling-pull stab window (the 2.9.2 sweep window it shared a branch with stays);
   - `DebugBranch` console prints.
3. **`SweepNoReverse` written into the pace rates** instead of as a clamp on the caps after them: the nose-down / nose-up AoA-rate
   allowances are floored at the flight-path rotation (`dnRate ≥ wVe`, `upRate ≥ −wVe`, same weights: held and released, off under the
   no-fin hold) — algebraically the same as clamping the caps at zero.

Correction to the earlier tally: there is no ~800-line retired lateral governor left inside `LateralLaw`; it was archived in 2.6.0
(`C:\Games\NO mods\F22E-FCS-retired-governors`). What was left were these A/B switches.

## Proof
- Full suite (rev / cork / reg / util / dmg): all five summary files identical to 2.31.0, and **all 119 per-tick case CSVs byte-identical**.
- Ten replays from logged states (17:43: 59.7, 116.3, 185.3 s; 17:07: 43.1, 56.2, 86.74, 122.0 s; 14:45: 42.7, 109.16 s; 15:25: 141.0 s):
  **byte-identical** to 2.31.0 (snap releases, released / held push-overs, the neg → pos pull, a full reversal).
- In game: identical flying expected; `LogOutput.log` must say 2.31.1.

## Still to replace (see `claude/F22E_FCS_handoff_2.31.0.md` §7)
Still 17 devices (3 core, 6 push-over, 8 older add-ons); the "+1" floor clamp is gone (now part of the pace), and
`RudderWindowSweep` was never in the 17 (it was off). This build changed what the code looks like, not what the governor does.
