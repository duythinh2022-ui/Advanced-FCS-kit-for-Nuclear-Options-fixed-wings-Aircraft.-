"""Ifrit 0.17.0: port of the F-22E 3.1.5-3.1.8 push-over hand-offs into Ifrit_KR67.FCS/FcsController.cs (0.16.0 base)."""
import sys
p = sys.argv[1]
s = open(p).read()


def T(n, txt):
    return '\n'.join(('\t' * n + l if l else l) for l in txt.split('\n'))


def rep(a, b):
    global s
    assert s.count(a) == 1, (s.count(a), a[:100])
    s = s.replace(a, b)


rep(T(2, "internal static float SweepFloor = 6f;\n"), T(2, """internal static float SweepFloor = 6f;
// 0.17.0: the F-22E 3.1.5-3.1.8 push-over hand-offs, ported (Tom 7 Nov 09:31: "check the latest ifrit logs, see if the pushovers can be smoother").
// 09:16 log, 0.16.0: at the end of each planned push the command notched -48 -> -10..-25 for 0.1-0.3 s and came back (33.6 / 51.3 / 102.7 / 158.2 /
// 227.7 s), the nose humped 4-10 deg/s with it, and at 77.3 s the push was held to -25..-15 near zero AoA. The causes are the F-22E's:
//  - PushPlanDoneOpen: the plan ramped out at 10 deg and handed the rest to the governor's toward-zero cap, which came back at its 6 deg/s floor.
//  - SweepSideKept: the pitch law read the caps sized on the last lateral pass for the other AoA side for a tick at zero AoA.
//  - PushGovFade / PushGovFadeGrow: both sweep caps switched in whole at a velocity roll of 5 deg/s; now faded in with it (toward zero 5 -> 15, the
//    far side past zero 20 -> 40 deg/s).
//  - PushGovPaceYaw: the pace closed on a_min even with no yaw over the allowance (a_min 0), then let go at the crossing.
//  - PushPlanThrough: the plan's ramp-out was 60 deg/s^2 below 10 deg and cut at the crossing; it now goes on through zero at 30 deg/s^2.
// And, behind their own switches (see the notes for the sideslip each costs): PushNoRetreat (a held push's rate is never taken back by the caps),
// SweepFloorPushMin / PushPlanFloor (the caps and the plan never hold the nose under PushOverMinRate), SweepZeroBlend (with a big roll held, ease
// into the far side's rate before zero).
internal static bool PushGovFade = true;
internal static float PushGovFadeLo = 5f;
internal static float PushGovFadeHi = 15f;
internal static float PushGovOpenRate = 120f;
internal static bool PushGovPaceYaw = true;
internal static float PushGovPaceR = 3f;
internal static bool PushGovFadeGrow = true;
internal static float PushGovFarLo = 20f;
internal static float PushGovFarHi = 40f;
internal static bool PushPlanDoneOpen = true;
internal static bool SweepSideKept = true;
internal static bool PushPlanThrough = true;
internal static float PushPlanReleaseSoft = 30f;
internal static bool PushNoRetreat = true;
internal static bool SweepFloorPushMin = true;
internal static float PushOverMinRate = 23f;     // deg/s (F-22E 3.1.5's): the nose rate the sweep caps never hold a held push under; also the plan's floor
internal static bool PushPlanFloor = true;
internal static bool SweepZeroBlend = true;
internal static float SweepZeroDecel = 60f;
"""))
rep("\t\tinternal float yawSatAlphaRateDn = -1f;\n",
    "\t\tinternal float yawSatAlphaRateDn = -1f;\n\t\tinternal float yawSatAlphaSign = 1f, yawSatFarRate = 999f, pushNrQ, pullNrQ, pushGovFadeW;   // 0.17.0\n")
rep("public bool pushPlanOn; public float pushPlanQ,", "public bool pushPlanOn, pushPlanDone, pushPlanReleasing; public float pushPlanQ,")
# --- pitch-law cap block
rep(T(3, "bool planOwns = PushPlan && pushPlanOn && PushPlanOwnsPitch;\n"),
    T(3, "bool planOwns = PushPlan && pushPlanOn && PushPlanOwnsPitch && !pushPlanReleasing;   // 0.17.0: not in the ramp-out (the caps see the crossing)\n"))
rep(T(4, """float upRate = (alpha >= 0f) ? yawSatAlphaRate : yawSatAlphaRateDn;
float dnRate = (alpha >= 0f) ? yawSatAlphaRateDn : yawSatAlphaRate;
"""), T(4, """// 0.17.0: the toward-zero rate fades in with the velocity roll (PushGovFade); the rates are read for the AoA side they were sized on (SweepSideKept)
float rateTz = PushGovFade ? Mathf.Lerp(PushGovOpenRate, yawSatAlphaRateDn, pushGovFadeW) : yawSatAlphaRateDn;
bool aPosS = SweepSideKept ? (yawSatAlphaSign >= 0f) : (alpha >= 0f);
float zbRate = 999f;
if (SweepZeroBlend && yawSatFarRate < 900f && aPosS)
{
	float aLeftZ = ((alpha >= 0f) == aPosS) ? Mathf.Abs(alpha) : 0f;
	zbRate = Mathf.Max(yawSatFarRate, 0f) + Mathf.Sqrt(2f * Mathf.Max(1f, SweepZeroDecel) * aLeftZ);
}
float upRate = aPosS ? yawSatAlphaRate : rateTz;
float dnRate = aPosS ? rateTz : yawSatAlphaRate;
"""))
rep(T(4, """float capY = num8 + upRate;
float capLo = num8 - dnRate;
if (num34 > capY)
{
	yawSatPitchDbg = num34 - capY;"""), T(4, """float capY = num8 + upRate;
float capLo = num8 - dnRate;
// 0.17.0: the floor (SweepFloorPushMin), the rate a held push has reached (PushNoRetreat, toward zero only), then the zero-crossing blend
bool floorOk = SweepFloorPushMin && PushOverMinRate > 0f && yawHealth >= 0.99f && sweepHoldDbg <= 0f;
if (floorOk)
{
	if (aPosS || SweepZeroBlend) capLo = Mathf.Min(capLo, 0f - PushOverMinRate * Mathf.Clamp01(0f - sp));
	if (!aPosS) capY = Mathf.Max(capY, PushOverMinRate * Mathf.Clamp01(sp));
}
if (PushNoRetreat && yawHealth >= 0.99f && sweepHoldDbg <= 0f)
{
	bool towardZ = (alpha >= 0f) == aPosS;
	if (aPosS && towardZ) capLo = Mathf.Min(capLo, 0f - pushNrQ);
	if (!aPosS && towardZ) capY = Mathf.Max(capY, pullNrQ);
}
if (zbRate < 900f)
{
	if (aPosS) capLo = Mathf.Max(capLo, num8 - zbRate);
	else capY = Mathf.Min(capY, num8 + zbRate);
	if (floorOk)
	{
		if (aPosS) capLo = Mathf.Min(capLo, 0f - PushOverMinRate * Mathf.Clamp01(0f - sp));
		else capY = Mathf.Max(capY, PushOverMinRate * Mathf.Clamp01(sp));
	}
}
if (num34 > capY)
{
	yawSatPitchDbg = num34 - capY;"""))
rep(T(3, "qT = qTraj.x;\n"), T(3, """qT = qTraj.x;
// 0.17.0 PushNoRetreat: what the held push / the pull toward zero from negative AoA has reached
pushNrQ = (sp < -0.3f) ? Mathf.Max(pushNrQ, 0f - qT) : 0f;
pullNrQ = (sp > 0.3f && alpha < 0f) ? Mathf.Max(pullNrQ, qT) : 0f;
"""))
# --- plan
rep(T(3, """if (!PushPlan || isAI || tas < 40f || yawHealth < 0.99f)
{
	pushPlanOn = false;
	return;
}"""), T(3, """if (!PushPlan || isAI || tas < 40f || yawHealth < 0.99f)
{
	pushPlanOn = false;
	pushPlanDone = false;
	return;
}"""))
rep(T(3, """float wv = Mathf.Max(0f, sgA * omegaV);
if (!pushPlanOn)
{"""), T(3, """float wv = Mathf.Max(0f, sgA * omegaV);
if (pushPlanDone && (!pushing || sgA != pushPlanSide)) pushPlanDone = false;
if (!pushPlanOn)
{"""))
rep(T(5, "pushPlanOn = true;\npushPlanSide = sgA;"), T(5, "pushPlanOn = true;\npushPlanDone = false;\npushPlanReleasing = false;\npushPlanSide = sgA;"))
rep(T(3, """if (!pushing || sgA != pushPlanSide || (pushPlanQNow >= 60f && aAbs < 10f))
{
	pushPlanOn = false;
	return;
}"""), T(3, """// 0.17.0 (PushPlanThrough): the ramp-out goes on through zero AoA while the stick still pushes the plan's way, at PushPlanReleaseSoft
bool planPush = pushPlanSide * sp < -0.3f;
pushPlanReleasing = false;
if (PushPlanThrough && planPush && (sgA != pushPlanSide || aAbs < 10f))
{
	pushPlanReleasing = true;
	pushPlanQ = 60f;
	pushPlanQNow = Mathf.MoveTowards(pushPlanQNow, 60f, PushPlanReleaseSoft * dt);
	if (pushPlanQNow < 60f) return;
}
if (!pushing || sgA != pushPlanSide || (pushPlanQNow >= 60f && aAbs < 10f))
{
	pushPlanDone = PushPlanDoneOpen && pushing && sgA == pushPlanSide;   // 0.17.0: finished at the bottom - open to the crossing
	pushPlanOn = false;
	return;
}"""))
rep("\t\t\tpushPlanQNow = Mathf.MoveTowards(pushPlanQNow, pushPlanQ, PushPlanJerk * dt);\n",
    "\t\t\tpushPlanQNow = Mathf.MoveTowards(pushPlanQNow, (PushPlanFloor && PushOverMinRate > 0f) ? Mathf.Max(pushPlanQ, PushOverMinRate) : pushPlanQ, PushPlanJerk * dt);   // 0.17.0: floored at PushOverMinRate\n")
# --- governor
rep(T(6, "float capPaceG = Mathf.Min(capShedG, capTermG);\n"), T(6, """float capPaceG = Mathf.Min(capShedG, capTermG);
// 0.17.0 (PushGovPaceYaw): no yaw over the allowance, nothing to shed - the pace is open, faded in with r_ex
if (PushGovPaceYaw && yawHealth >= 0.99f) capPaceG = Mathf.Lerp(StopSlipPitchFree, Mathf.Min(StopSlipPitchFree, Mathf.Min(capTermG, (rExW < 1E-04f) ? StopSlipPitchFree : Mathf.Abs(alpha) * capYG / rExW)), Mathf.Clamp01(rExG / Mathf.Max(0.1f, PushGovPaceR)));
"""))
rep(T(6, """if (alpha < 0f)
{
	float capYGrow = (rSideG > 0f) ? capEst.capTrZeP : capEst.capTrZeN;
	capPullG = Mathf.Min(capPullG, Mathf.Max(SweepFloor, 57.29578f * SweepYawK * Mathf.Max(0f, capYGrow) * cG / Mathf.Max(5f, pSCap)));
}
yawSatAlphaRate = capPullG;
yawSatAlphaRateDn = Mathf.Min(StopSlipPitchFree, (PushPlan && pushPlanOn) ? StopSlipPitchFree : capDnG);"""), T(6, """// 0.17.0: the far side's grow rate at zero AoA (SweepZeroBlend), and the far side fading in over a larger roll (PushGovFadeGrow)
float pSFadeG = (PushGovFadeGrow && PushGovFade && yawHealth >= 0.99f) ? Mathf.Clamp01((pSCap - PushGovFarLo) / Mathf.Max(0.1f, PushGovFarHi - PushGovFarLo)) : 1f;
if (alpha >= 0f)
{
	float capYFar = (0f - rSideG > 0f) ? capEst.capTrZeP : capEst.capTrZeN;
	yawSatFarRate = Mathf.Lerp(PushGovOpenRate, Mathf.Max(SweepFloor, 57.29578f * SweepYawK * Mathf.Max(0f, capYFar) / Mathf.Max(5f, pSCap)), pSFadeG);
}
else
{
	yawSatFarRate = capPullG;
}
if (alpha < 0f)
{
	float capYGrow = (rSideG > 0f) ? capEst.capTrZeP : capEst.capTrZeN;
	float capGrowNeg = Mathf.Max(SweepFloor, 57.29578f * SweepYawK * Mathf.Max(0f, capYGrow) * cG / Mathf.Max(5f, pSCap));
	if (PushGovFadeGrow && PushGovFade && yawHealth >= 0.99f) capGrowNeg = Mathf.Lerp(PushGovOpenRate, capGrowNeg, pSFadeG);
	capPullG = Mathf.Min(capPullG, capGrowNeg);
}
pushGovFadeW = (PushGovFade && yawHealth >= 0.99f) ? Mathf.Clamp01((pSCap - PushGovFadeLo) / Mathf.Max(0.1f, PushGovFadeHi - PushGovFadeLo)) : 1f;
yawSatAlphaRate = capPullG;
yawSatAlphaSign = (alpha >= 0f) ? 1f : -1f;
yawSatAlphaRateDn = Mathf.Min(StopSlipPitchFree, (PushPlan && (pushPlanOn || pushPlanDone)) ? StopSlipPitchFree : capDnG);"""))
rep(T(6, "tradeAlphaDbg = 0f;\n"), T(6, "tradeAlphaDbg = 0f;\npushGovFadeW = 0f;\n"))
open(p, 'w').write(s)
print('ok')
