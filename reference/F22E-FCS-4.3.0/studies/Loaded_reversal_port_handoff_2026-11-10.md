# Handoff: port the loaded roll-reversal fix (F-22E 4.1.1 + 4.1.2) to the other FCS mods

Written 10 Nov 2026, 23:30. For a new chat.

## Tom's ask
- 10 Nov 23:28: "gimme handoff so the fix asymmetric load roll reversal fix can be applied to all of the other fcs mods"
- Background, same day: 18:14 "when i roll while holding pitch, then i reverse roll, the control surfaces stop the roll then they suddenly spiked in deflection to reverse the roll" (asymmetric load) → 18:53 "Build the fix" → 19:05 "make inherent fix not on top" → 19:34 after flying 4.1.1: "still not fully fixed, it is just smoother" → 4.1.2.
- 19:18 "I guess other fcs has this issues right?" → yes (source check below).

## Status of the fix on the F-22E (read before porting)
| version | what | verified |
|---|---|---|
| 4.1.1 | `revA` (the reversal's one acceleration, 1.7.4 rule) now includes the 2.1.6 load limit; the limit moved inside the brake branch; the carry blends from either side | bench + flown once (19:33 log): spike made continuous, the pause before it still there toward the heavy side |
| **4.1.2** | toward the side the load pulls: the reversal's acceleration builds over the rudders' measured crossing time to one figure (the entry's, within 60 % of the yaw left) and holds it through the zero crossing to the new rate | bench + **flown 22:50** (`F22E_FCS_20261110_225014.csv`, lateral CG offset 0.21 – 0.23 m, 71 s loaded); Tom's verdict on the feel not yet given |

**22:50 flight, 4.1.2 (log read 23:31):**
- Toward the load, 4 reversals (32.5 / 51.8 / 56.0 / 76.1 s, 265 – 311 kt, AoA 15 – 18°): the acceleration builds once from the stick flip and stays flat through the zero crossing (e.g. 56.0 s, every 3 ticks: 18, 56, 79, 103, 129, 153, 179, 200, 203, 200 … 203, then the run-out). Peak / brake 1.07 – 1.27 (4.1.1 flight: 1.66 – 1.97). The ailerons sweep from −22 to +15 – 17° at the crossing, the rudders cross steadily. Sideslip max 3.8 – 4.2° (4.1.1: 3.1 – 3.9°).
- Against the load, 2 reversals (53.8 / 57.9 s): unchanged 4.1.1 shape, flat at 128 – 140 with the surfaces at their stops, then a rise to 152 – 188 after the crossing (peak / brake 1.19 – 1.41). The remaining step is on this side.
- Two reversals from a low rate (71.8 / 73.3 s): one smooth build each.

**Recommendation: get Tom's verdict on the 22:50 flight before porting.** If he finds 4.1.2 good, port 4.1.1 + 4.1.2 together. Porting 4.1.1 alone is safe (strictly better than 4.1.0 on the bench and in his flight), but it leaves the "stop then spike" toward the heavy side.

Full write-ups: `claude/F22E_NO_FCS_4.1.1_notes.md`, `claude/F22E_NO_FCS_4.1.2_notes.md`, flight check `claude/F22E_FCS_4.1.0_flight_check_2026-11-10.md`. Source: `C:\Games\NO mods\F22E-FCS-4.1.2-release\F22E_FCS_4.1.2_src.zip` (`f22/Aryx_F22E_StrikeRaptor.FCS/FcsController.cs`).

## Which mods carry the defect (source check, 10 Nov 19:20)
All have the 4.1.0 code line for line, same constants (`LoadRevYawK 0.4`, `RevFloorK 1`, `RevConst true`, `ReversalFloor true`, `CoupHeldDy0 0.04`, `CoupHeldDySpan 0.08`, `AsymSharedLat true`, `RollBrakeUse 0.8`, `RollAccelUse 0.5`).

| mod (version checked) | source zip in `C:\Games\NO mods` | `num17 = (latAccDbg = a)` | `a2 = …RollJerkTau…` | `pTraj.Step(` |
|---|---|---|---|---|
| Ifrit 1.0.0 | `Ifrit-FCS-1.0.0-release\Source.Ifrit-KR67-FCS-1.0.0.zip` | 11298 | 11370 | 11481 |
| Revoker 1.0.0 | `Revoker-FCS-1.0.0-release\Source.FS12-Revoker-FCS-1.0.0.zip` | 11662 | 11734 | 11826 |
| Compass 0.2.0 | `Compass-FCS-0.2.0-release\Source.TA30-Compass-FCS-0.2.0.zip` | 12172 | 12244 | 12336 |
| Ternion 0.2.0 | `Ternion-FCS-0.2.0-release\Source.FS3-Ternion-FCS-0.2.0.zip` | 12555 | 12627 | 12719 |
| Su-30 0.3.3 | `Su30-FCS-0.3.3-release\Source.Su30-VXN-FCS-0.3.3.zip` | 13387 | 13459 | 13551 |
| NO-FCS-Kit 2.0 | `NO-FCS-Kit-2.0.zip` (all six reference trees) | | | |

- **Su-30 has moved on since** (0.3.4, 0.3.5 notes exist in the project). Re-check against the newest Su-30 source and any newer releases of the others before porting.
- The kit: decide with Tom whether its reference trees get the fix (new ports start from the kit).

The three tell-tale lines (4.1.0 shape), in `LateralLaw`'s roll trajectory:
```csharp
num17 = (revA = (RevConst ? num19 : Mathf.Max(num17, num19)));          // revA stored before the load limit
num17 = Mathf.Max(num17, Mathf.Lerp(num17, revA, num20));              // carry can only raise
if (AsymSharedLat && Plugin.AsymSharedLatOn && asymValid && LoadRevYawK > 0f && pTraj.x * (num11 - pTraj.x) < 0f) { … revCap … }   // limit after the branch, brake only
```

## The change (4.1.0 → 4.1.2), as one patch
Six edits in `FcsController.cs`; nothing in `Plugin.cs`, no setting, no telemetry column, no stored state beyond what is there.

**1. Statics** (after `internal static bool RevCarry = true;`):
```csharp
// <jet> <ver>: one acceleration through a reversal, the load's limit included (false = before)
internal static bool RevOneAccel = true;
// <jet> <ver>: a reversal the load itself brakes builds its acceleration over the rudders' travel (false = 4.1.1 shape)
internal static bool RevLoadRamp = true;
```
(bench comparison switches only)

**2. Three helpers** (before `private void LateralLaw(float dt)`):
```csharp
private float LoadSideWeight(float aRollGo)
{
	return Mathf.Clamp01(1f - (aRoll - aRollGo) / Mathf.Max(1f, 0.1f * aRoll));
}

private float LoadRevPace(float aRollGo, float cosA, float yawTop)
{
	if (!RevLoadRamp || !AsymSharedLat || !Plugin.AsymSharedLatOn || !asymValid || LoadRevYawK <= 0f) return 0f;
	float dyG = Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / Mathf.Max(0.005f, CoupHeldDySpan));
	if (dyG <= 0f || RudderCrossTime() <= 0f) return 0f;
	float revLaw = RevFloorK * aRoll / Mathf.Max(Mathf.Abs(cosA), 0.05f);
	return dyG * LoadSideWeight(aRollGo) * Mathf.Clamp01(2f * (1f - yawTop / Mathf.Max(1f, revLaw)));
}

private float RudderCrossTime()
{
	float sum = 0f; int n = 0;
	foreach (SurfaceEffector se in surfaces)
	{
		if (se.kind != SurfaceKind.Rudder || !se.active) continue;
		sum += (0.5f * (se.max - se.min) + Mathf.Abs(se.now - 0.5f * (se.max + se.min))) / Mathf.Max(1f, se.rate);
		n++;
	}
	return (n > 0) ? (sum / n) : 0f;
}
```

**3. Before the brake branch** (after `num17 *= Mathf.Lerp(RollGovAccelFloor, 1f, rollGov);`):
```csharp
loadRevDbg = 0f;
float entryA = num17;
float revRampW = 0f;
```

**4. The brake / reversal branch** (`if (pTraj.x * (num11 - pTraj.x) < 0f)`), replacing the `if (… revIntent) && ReversalFloor) { … revA = … }` block:
```csharp
bool revNow = ((num11 * pTraj.x < 0f && Mathf.Abs(num11) > 10f) || revIntent) && ReversalFloor;
float revFree = 0f;
if (revNow)
{
	float num19 = RevFloorK * aRoll / Mathf.Max(Mathf.Abs(num), 0.05f);
	num17 = (revFree = (RevConst ? num19 : Mathf.Max(num17, num19)));
}
if (AsymSharedLat && Plugin.AsymSharedLatOn && asymValid && LoadRevYawK > 0f)
{
	float dyG = Mathf.Clamp01((Mathf.Abs(asymDy) - CoupHeldDy0) / Mathf.Max(0.005f, CoupHeldDySpan));
	if (dyG > 0f)
	{
		float revCap = LoadRevYawK * aYaw * Mathf.Max(0.2f, sharedLatDbg) / Mathf.Max(Mathf.Abs(num2), 0.05f);
		if (RevLoadRamp && revNow && RudderCrossTime() > 0f)
		{
			float yawTop = num16 * Mathf.Max(0.2f, sharedLatDbg);
			revRampW = LoadRevPace(aRollGo, num, yawTop);
			revCap = Mathf.Lerp(revCap, Mathf.Max(revCap, Mathf.Min(entryA, yawTop)), LoadSideWeight(aRollGo));
		}
		loadRevDbg = dyG;
		num17 = Mathf.Lerp(num17, Mathf.Min(num17, revCap), dyG);
	}
}
if (revNow)
{
	revA = RevOneAccel ? num17 : revFree;
	revTarget = revIntent ? paceTgtRaw : num11;
}
```
**Delete** the old separate 2.1.6 block after the carry branch (the `… && pTraj.x * (num11 - pTraj.x) < 0f)` one) and its `loadRevDbg = 0f;`.

**5. The carry** (`else if (revA > 0f && RevCarry)`, inner `else`), replacing the two `num20` / `num17 = Mathf.Max(…)` lines:
```csharp
float yawTopC = num16 * Mathf.Max(0.2f, sharedLatDbg);
float pace = RevOneAccel ? LoadRevPace(aRollGo, num, yawTopC) : 0f;
float num20 = 1f - Mathf.Clamp01(Mathf.Abs(pTraj.x) / Mathf.Max(10f, Mathf.Lerp(0.5f, 1f, pace) * Mathf.Abs(num11)));
float carried = Mathf.Lerp(num17, revA, num20);
num17 = RevOneAccel ? carried : Mathf.Max(num17, carried);
if (pace > 0f) num17 = Mathf.Lerp(num17, Mathf.Min(num17, Mathf.Min(entryA, yawTopC)), pace);
revRampW = pace;
```

**6. The jerk** (after `float a2 = Mathf.Min(num17 / RollJerkTau, …);`), and the run-out jerk in `pTraj.Step`:
```csharp
float a2Free = a2;
if (revRampW > 0f)
{
	float tRud = RudderCrossTime();
	if (tRud > RollJerkTau) a2 = Mathf.Min(a2, num17 / Mathf.Lerp(RollJerkTau, tRud, revRampW));
}
...
pTraj.Step(num11, <unchanged 2nd arg>, Mathf.Max(a2, jFloor), Mathf.Max(Mathf.Max(a2, a2Free) * Mathf.Min(endJ, 1f), jFloor), dt);
```
Note: the F-22E passes `Mathf.Max(num17, 0.5f)` as the 2nd argument, the other five pass `num17`. Keep each jet's own; only the 4th argument changes.

A unified diff of the F-22E change (4.1.0 → 4.1.2, comments included) is `claude/F22E_loaded_reversal_4.1.0_to_4.1.2.diff`.

## Per-jet things to check (not checked yet)
- **Names.** All five have `SurfaceKind.Rudder`, `pTraj`, `revA`, `revTarget`, `revIntent`, `paceTgtRaw`, `num11`, `num16`, `num`, `num2`, `aRollGo`, `sharedLatDbg`, `loadRevDbg`, `aYaw` in the same places by the line-number check above, but decompiled local names (`numNN`) can differ per jet: match by role, not by name. `num` = cos AoA term, `num2` = sin AoA term, `num11` = roll-rate target, `num16` = the trajectory's yaw figure (0.6 x yaw authority), `num17` = the trajectory's acceleration.
- **`RudderCrossTime` per airframe:** it averages every active `SurfaceKind.Rudder` surface.
  - **Ifrit:** its ruddervators are classed as `Rudder` and also carry pitch (`RudderPitchAid`, Ifrit 0.16.0 TVC offload). `se.now` then includes the pitch share, so the crossing time is read from a mixed deflection. Check on the bench whether the time it returns during a loaded reversal is sensible (0.4 – 0.8 s); if not, use the yaw component only.
  - **Su-30 / Revoker / Compass / Ternion:** twin conventional rudders presumably; check servo `rate` and travel so the times are in the same 0.4 – 0.8 s range.
  - A jet with rudders lost (`n = 0`) returns 0 and the ramp switches off (4.1.1 behaviour).
- **`aRollGo`** must be the stick-side roll authority after the load's trim (as in the F-22E). If a jet computes it differently, `LoadSideWeight` will pick the wrong side.
- **AI path:** `isAI && Plugin.AIDirectRoll` overrides `a2` after this point, unchanged; AI jets with a load are affected only through `num17`.

## Verification, per jet
Tom's standard: suite identical apart from loaded cases, loaded cases benched both directions, response curve inspected (not just peaks).
1. **Switch-off identity:** `RevOneAccel=false RevLoadRamp=false` must reproduce the previous build byte for byte on the loaded runs.
2. **Suite:** all per-case files and tables identical (on the F-22E the suite has no loaded reversal; expect the same elsewhere: 153 / 153 there).
3. **Loaded reversals:** port `bench/an/loadrev.sh`, `loadrev.py`, `loadrev_cmp.py` from the F-22E 4.1.2 source into each bench. Change the store part (`storePart=WingRear_L` is the F-22E's) and the speeds / weights to that jet's envelope. 34 singles (both directions; `_R` toward the load) + 54 spam runs.
4. **What to read:** toward the load, new roll's peak / brake ≈ 1.0 (was ~2.0 – 2.6), rise after the crossing ≈ 0, ailerons sweeping through instead of resting near neutral; against the load byte-identical; sideslip not worse except where the rudders start near their stops (F-22E: 1,100 kg at 26° AoA +1°, one governor hold at 233 kt).
5. **Other identity checks:** clean reversal sequences, a loaded stop, a store under 4 cm of lateral CG offset, `LoadRevYawK=0`: identical.
6. Build, package (Tom's release-folder layout), install with `.bak`, read checksums back, notes per jet; remind him to update the mod manager.

## Open on the F-22E that will carry over
- 4.1.2 is still an acceleration limit for yaw (his 31 Oct / 2 Nov rule says the governor should slow the rate, not the acceleration). Deleting it: crisp reversals but 4.7 – 6.3° sideslip per reversal, 8.7° mean in spam. Not resolved.
- Against-the-load reversals keep the 4.1.1 shape (peak / brake 1.16 – 1.43 in the 19:33 flight, 1.19 – 1.41 at 22:50).
- Loaded roll spam at 6 – 11° sideslip against a 4° margin.
