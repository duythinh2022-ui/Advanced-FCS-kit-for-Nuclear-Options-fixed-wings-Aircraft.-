using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 2.7.5: measured per-axis capacity estimator — TELEMETRY ONLY, drives nothing.
	// 2.7.6: fixed per-group gains, one lumped online gain per axis, horizon-discounted committed term (12:31 log).
	//
	// What each axis can still do toward a side, in deg/s^2, as three parts that are each either measured or the
	// allocator's own decision (no independent table sums):
	//   1. what the axis is doing now: the filtered measured angular acceleration (passive airframe + effectors at `now`);
	//   2. what is already committed and still arriving within CapHorizon: the table increment from `now` to this tick's
	//      command, per effector group, counted only as far as the servo gets there within the horizon (12:31 log, push
	//      from 65 deg: a 65 deg stab swing was reversed by the allocator before it arrived, and counting it whole read
	//      2-4x the yaw the jet made), scaled by the group's gain;
	//   3. what the allocator would still give: a probe — the allocator solved once more from this tick's commands for a
	//      large extra demand toward the side, with the rest of its target unchanged — read per group and scaled by the
	//      same gains, then the commands restored.
	// Gains (achieved acceleration per table-modelled moment): per-group constants identified from the 2 Nov logs
	// (62,701 samples; rudders ~1.0, stabilators ~0.7, flaperons ~1.0 in yaw; ~1.0 / ~0.65 / ~1.0 in roll, nearly
	// constant with AoA). A per-group online identification was tried in 2.7.5 and collapsed in flight (stabs and
	// flaperons move together; the three gains sat at the clamp 22-68 % of the 12:31 log); what remains online is one
	// lumped gain per axis (achieved per modelled total effector moment, RLS on 0.1 s differences), logged only.
	internal sealed class CapacityEstimator
	{
		internal static bool CapEstOn = true;

		internal static float ProbeZ = 400f;      // deg/s^2 of extra yaw asked of the allocator in the probe

		internal static float ProbeX = 1500f;     // deg/s^2 of extra roll

		internal static int ProbeIters = 1;       // allocator solves per probe (warm-started from this tick's command)

		// 2.14.0: the two-axis probe. Past the rudder fade (and without fins at any AoA) the yaw the governor budgets is not an axis
		// of its own: the velocity-vector roll is one demand split cos a : sin a over body roll and yaw, served by the same
		// surfaces. A yaw-only probe lets the allocator re-purpose those surfaces for yaw that the real allocation keeps on roll
		// (2-4x over-read in the no-fin push-overs, 18:47 / 19:30 logs), and the allocator's per-axis unmet counts one shortfall
		// twice (the no-fin stop at 65 deg, 19:30 log 164 s: 8-13 read where the stabs had 40). The probe along the velocity-roll
		// direction asks for the whole rotation, reads what every group adds along it, and takes the unmet along it once.
		internal static float ProbeS = 800f;      // deg/s^2 of extra velocity-roll acceleration asked in the two-axis probe

		internal static float CapHorizon = 0.15f; // s, how far ahead the committed increment is counted

		// 2.21.0: the trade (the rolling-pull pitch share, FcsController ~6700) reads capZeP/N low-passed over this time. The trade
		// decides the pitch share for the next few tenths of a second from a reading that includes the allocator's per-tick unmet,
		// which can spike for a tick when it reshuffles a stab between roll and pitch (bench replay of the 3 Nov 09:53 log, 40.17 s:
		// unmet -9 -> -17 -> -20 for two ticks, capacity 34 -> 13 -> 24, the trade's share 27 -> 9 and the pitch command 30 -> 11
		// for one tick), and in the game the same read stepped the command twice in a rolling-pull entry (39.9-40.5 s). A one-tick
		// reading is not a capacity; a loss that lasts (rudders pinning, the shed in a push-over) comes through in a few ticks.
		// Tried and rejected alongside it: crediting the rudders' committed travel over 0.4 s instead of CapHorizon (a rudder sent
		// to its stop arrives) - it lifted the entry's pitch floor by 2-3 deg/s on the replay and put the bench's pull + roll
		// reversal at 450 km/h from 17 to 31 deg of sideslip: in a reversal the same credit promises the new side's yaw while the
		// rudders are still crossing and the measured term still reads the old side.
		internal static float TradeCapTau = 0.05f;   // s (3 ticks; 0.1 cost the bench 450 km/h pull + roll reversal 17.4 -> 19.0 deg and the 556 corkscrew-entry roll dip 63 -> 47 - the lag on a real loss)

		// per-group gains, yaw (rudders, stabilators, flaperons) and roll — from the 2 Nov logs
		internal static float CapGainZR = 1.0f, CapGainZS = 0.7f, CapGainZF = 1.0f;
		internal static float CapGainXR = 1.0f, CapGainXS = 0.65f, CapGainXF = 1.0f;

		internal static float GainTau = 10f;      // s, RLS forgetting (lumped online gain, telemetry)

		internal static float GainRidge = 0.002f; // per tick pull toward the prior of 1

		internal static float GainLo = 0.2f;

		internal static float GainHi = 1.6f;

		internal static float GainExcite = 3f;    // deg/s^2 of 0.1 s moment change needed to update

		internal static int DiffTicks = 6;        // 0.1 s at 60 Hz

		// telemetry (deg/s^2 unless noted); Z = yaw, X = roll; P/N = toward the positive/negative side of the axis
		public float wdZ, wdX;                                   // measured angular acceleration (filtered)
		public float cmZr, cmZs, cmZf, cmXr, cmXs, cmXf;         // committed increment per group, undiscounted
		public float dr, ds, df;                                 // discount per group: the share of the committed travel arriving within CapHorizon
		public float prZPr, prZPs, prZPf, prZNr, prZNs, prZNf;   // probe delivery per group, yaw
		public float prXPr, prXPs, prXPf, prXNr, prXNs, prXNf;   // probe delivery per group, roll
		public float prZPx, prZNx, prXPz, prXNz;                 // the probe's collateral on the other axis (total)
		public float gZ = 1f, gX = 1f;                           // lumped online gains (telemetry)
		public float capZP, capZN, capXP, capXN;                 // the composite: fixed gains, discounted committed term
		public float capZPraw, capZNraw, capXPraw, capXNraw;     // every gain at 1, committed term undiscounted
		public float prSPs, prSPf, prSNs, prSNf, prSPr, prSNr;   // 2.14.0: two-axis probe delivery per group, along the velocity-roll direction
		public float capSP, capSN;                               // 2.14.0: capacity along the velocity-roll direction (deg/s^2 of velocity-roll acceleration), toward +S / -S
		public float capZeP, capZeN;                             // 2.14.0: effective yaw capacity the consumers read: the yaw component of capS where the rudders are not the yaw axis, capZ where they are
		public float rudWDbg;                                    // the blend (1 = rudders are the yaw axis)
		public float capTrZeP, capTrZeN;                         // 2.21.0: capZeP/N low-passed over TradeCapTau (the trade's read)
		// 2.22.0: the same composites without the stabilators - what the yaw keeps once the pitch takes the stabs back (the shed cap's
		// read for a sweep that will cross the stabs' return to pitch). Measured term less the stabs' modelled yaw at `now`, committed
		// and probe terms less the stab group's, same unmet.
		public float capZeNsP, capZeNsN;
		public float wdZNs;                                      // measured yaw acceleration less the stabs' modelled share
		private bool trInit;
		public int gUpdZ, gUpdX;                                 // whether the RLS updated this tick

		// lumped RLS state
		private float thZ = 1f, thX = 1f, PZ = 1f, PX = 1f;
		private readonly float[] ringMZ, ringMX, ringWdZ, ringWdX;
		private int ringN, ringHead;
		private readonly float[] savedCmd = new float[16];

		public CapacityEstimator()
		{
			int n = Mathf.Max(2, DiffTicks);
			ringMZ = new float[n]; ringMX = new float[n]; ringWdZ = new float[n]; ringWdX = new float[n];
		}

		private static int Group(Effector e)
		{
			if (e is SurfaceEffector s)
			{
				if (s.kind == SurfaceKind.Rudder) return 0;
				if (s.kind == SurfaceKind.Stabilator) return 1;
				return 2;   // flaperons / ailerons
			}
			return -1;      // TVC: pitch only on this jet; its yaw/roll share is not modelled
		}

		// Called once per tick right after the allocation, with the effectors' cmd = this tick's decision and Mnow at `now`.
		// wdot: filtered measured angular acceleration (rad/s^2, aero frame x roll / y pitch / z yaw).
		public void Step(List<Effector> effs, Allocator alloc, Vector3 wdot, float[,] IaInv, Vector3 target, float dt, Vector3 unmet, float alphaDeg, float rudW)
		{
			if (!CapEstOn) return;
			float ix = IaInv[0, 0] * 57.29578f, iz = IaInv[2, 2] * 57.29578f;
			wdZ = wdot.z * 57.29578f;
			wdX = wdot.x * 57.29578f;
			// ---- group moments at now, the committed increment to cmd, and the share of it arriving within the horizon
			float[] cmZ = { 0f, 0f, 0f }, cmX = { 0f, 0f, 0f }, cmZd = { 0f, 0f, 0f }, cmXd = { 0f, 0f, 0f }, trav = { 0f, 0f, 0f }, travIn = { 0f, 0f, 0f };
			float mNowZ = 0f, mNowX = 0f, mNowZs = 0f;
			for (int i = 0; i < effs.Count; i++)
			{
				Effector e = effs[i];
				if (!e.active) continue;
				int g = Group(e);
				if (g < 0) continue;
				Vector3 mn = e.Mnow, mc = e.MAt(e.cmd);
				mNowZ += mn.z * iz; mNowX += mn.x * ix;
				if (g == 1) mNowZs += mn.z * iz;
				float dz = (mc.z - mn.z) * iz, dx = (mc.x - mn.x) * ix;
				cmZ[g] += dz; cmX[g] += dx;
				float tr = Mathf.Abs(e.cmd - e.now);
				float share = (tr > 1E-03f) ? Mathf.Clamp01(Mathf.Max(1f, e.rate) * CapHorizon / tr) : 1f;
				cmZd[g] += dz * share; cmXd[g] += dx * share;
				trav[g] += tr; travIn[g] += tr * share;
			}
			cmZr = cmZ[0]; cmZs = cmZ[1]; cmZf = cmZ[2];
			cmXr = cmX[0]; cmXs = cmX[1]; cmXf = cmX[2];
			dr = (trav[0] > 1E-03f) ? travIn[0] / trav[0] : 1f;
			ds = (trav[1] > 1E-03f) ? travIn[1] / trav[1] : 1f;
			df = (trav[2] > 1E-03f) ? travIn[2] / trav[2] : 1f;
			// ---- lumped online gains (telemetry)
			gUpdZ = Rls(ref thZ, ref PZ, ringMZ, ringWdZ, mNowZ, wdZ, dt) ? 1 : 0;
			gUpdX = Rls(ref thX, ref PX, ringMX, ringWdX, mNowX, wdX, dt) ? 1 : 0;
			int n = ringMZ.Length;
			ringHead = (ringHead + 1) % n;
			ringMZ[ringHead] = mNowZ; ringMX[ringHead] = mNowX; ringWdZ[ringHead] = wdZ; ringWdX[ringHead] = wdX;
			if (ringN < n) ringN++;
			gZ = thZ; gX = thX;
			// ---- allocator probes from this tick's commands (restored afterwards)
			int cnt = Mathf.Min(effs.Count, savedCmd.Length);
			for (int i = 0; i < cnt; i++) savedCmd[i] = effs[i].cmd;
			float[] dZ = new float[3], dX = new float[3];
			float colX, colZ;
			Probe(effs, alloc, target, new Vector3(0f, 0f, ProbeZ / iz), ix, iz, dZ, out colX, out colZ, cnt, true);
			prZPr = dZ[0]; prZPs = dZ[1]; prZPf = dZ[2]; prZPx = colX;
			Probe(effs, alloc, target, new Vector3(0f, 0f, -ProbeZ / iz), ix, iz, dZ, out colX, out colZ, cnt, true);
			prZNr = -dZ[0]; prZNs = -dZ[1]; prZNf = -dZ[2]; prZNx = colX;
			Probe(effs, alloc, target, new Vector3(ProbeX / ix, 0f, 0f), ix, iz, dX, out colX, out colZ, cnt, false);
			prXPr = dX[0]; prXPs = dX[1]; prXPf = dX[2]; prXPz = colZ;
			Probe(effs, alloc, target, new Vector3(-ProbeX / ix, 0f, 0f), ix, iz, dX, out colX, out colZ, cnt, false);
			prXNr = -dX[0]; prXNs = -dX[1]; prXNf = -dX[2]; prXNz = colZ;
			// 2.14.0: along the velocity-roll direction u = (cos a, 0, sin a) in body (roll, pitch, yaw) acceleration space
			float ca = Mathf.Cos(alphaDeg * (3.14159274f / 180f)), sa = Mathf.Sin(alphaDeg * (3.14159274f / 180f));
			// 2.15.0: the probe's answer is read per axis. 2.16.0: the yaw consumers budget the YAW component of what the coordinated
			// rotation can do - the probe is sent along +/-u (so the roll surfaces stay on the roll; a pure-yaw probe re-purposes them,
			// 2.14.0's over-read) and read on the yaw axis alone, with the measured and committed yaw and the yaw unmet, exactly as capZP/N
			// are read from the yaw-only probe. 2.15.0's limiting-axis read let the roll axis's measured deceleration (the roll cut by a
			// rate limit) and the roll's unmet read as a yaw shortfall: 20:19 log (both fins gone), 120.7-121.3 s, capZeP -8...-27 while
			// the yaw was measurably shedding 15-23 deg/s^2.
			float sgZ = (sa >= 0f) ? 1f : -1f;
			float[] dSx = new float[3], dSzU = new float[3], dSzD = new float[3];
			ProbeS2(effs, alloc, target, new Vector3(ProbeS * ca / ix, 0f, ProbeS * sa / iz), ix, iz, dSx, dSzU, cnt);
			ProbeS2(effs, alloc, target, new Vector3(-ProbeS * ca / ix, 0f, -ProbeS * sa / iz), ix, iz, dSx, dSzD, cnt);
			for (int i = 0; i < cnt; i++) effs[i].cmd = savedCmd[i];
			// +Z side = the rotation about sign(sin a) u; -Z side the other way. Per group, the yaw increment toward that side.
			float[] zP = (sa >= 0f) ? dSzU : dSzD, zN = (sa >= 0f) ? dSzD : dSzU;
			prSPr = sgZ * zP[0]; prSPs = sgZ * zP[1]; prSPf = sgZ * zP[2];
			prSNr = -sgZ * zN[0]; prSNs = -sgZ * zN[1]; prSNf = -sgZ * zN[2];
			// ---- composite capacity toward each side (positive magnitudes; negative = the axis cannot even hold this side)
			// 2.8.1: a probe cannot promise what the allocator reports it is not delivering of the demand it already has toward that
			// side (the 15:29 pushes from 65 deg: unmet yaw 55-90 deg/s^2 with the probe still reading 30-40, capacity 2.3x the shed
			// the axis made; less the unmet, 1.3x)
			float uZP = Mathf.Max(0f, unmet.z), uZN = Mathf.Max(0f, 0f - unmet.z), uXP = Mathf.Max(0f, unmet.x), uXN = Mathf.Max(0f, 0f - unmet.x);
			float comZ = CapGainZR * cmZd[0] + CapGainZS * cmZd[1] + CapGainZF * cmZd[2];
			float comX = CapGainXR * cmXd[0] + CapGainXS * cmXd[1] + CapGainXF * cmXd[2];
			capZP = wdZ + comZ + Mathf.Max(0f, CapGainZR * prZPr + CapGainZS * prZPs + CapGainZF * prZPf - uZP);
			capZN = -wdZ - comZ + Mathf.Max(0f, CapGainZR * prZNr + CapGainZS * prZNs + CapGainZF * prZNf - uZN);
			capXP = wdX + comX + Mathf.Max(0f, CapGainXR * prXPr + CapGainXS * prXPs + CapGainXF * prXPf - uXP);
			capXN = -wdX - comX + Mathf.Max(0f, CapGainXR * prXNr + CapGainXS * prXNs + CapGainXF * prXNf - uXN);
			float comZ1 = cmZr + cmZs + cmZf, comX1 = cmXr + cmXs + cmXf;
			capZPraw = wdZ + comZ1 + prZPr + prZPs + prZPf;
			capZNraw = -wdZ - comZ1 + prZNr + prZNs + prZNf;
			capXPraw = wdX + comX1 + prXPr + prXPs + prXPf;
			capXNraw = -wdX - comX1 + prXNr + prXNs + prXNf;
			// 2.16.0: the yaw the coordinated rotation can add toward each side (same gains, same measured / committed / unmet terms
			// as capZP/N; only the probe differs). capSP/SN keep their names in the telemetry.
			capSP = wdZ + comZ + Mathf.Max(0f, CapGainZR * prSPr + CapGainZS * prSPs + CapGainZF * prSPf - uZP);
			capSN = -wdZ - comZ + Mathf.Max(0f, CapGainZR * prSNr + CapGainZS * prSNs + CapGainZF * prSNf - uZN);
			// the yaw the consumers may budget: with the rudders as the yaw axis (rudW 1) the yaw-only figure; without them the yaw
			// component of what the whole rotation can do
			float capZSP = capSP, capZSN = capSN;
			rudWDbg = Mathf.Clamp01(rudW);
			capZeP = Mathf.Lerp(capZSP, capZP, rudWDbg);
			capZeN = Mathf.Lerp(capZSN, capZN, rudWDbg);
			// 2.22.0: without the stabilators (see the fields)
			float sMeas = CapGainZS * mNowZs, sCom = CapGainZS * cmZd[1];
			wdZNs = wdZ - sMeas;
			float comZNs = comZ - sCom;
			float zPNs = wdZNs + comZNs + Mathf.Max(0f, CapGainZR * prZPr + CapGainZF * prZPf - uZP);
			float zNNs = -wdZNs - comZNs + Mathf.Max(0f, CapGainZR * prZNr + CapGainZF * prZNf - uZN);
			float sPNs = wdZNs + comZNs + Mathf.Max(0f, CapGainZR * prSPr + CapGainZF * prSPf - uZP);
			float sNNs = -wdZNs - comZNs + Mathf.Max(0f, CapGainZR * prSNr + CapGainZF * prSNf - uZN);
			capZeNsP = Mathf.Lerp(sPNs, zPNs, rudWDbg);
			capZeNsN = Mathf.Lerp(sNNs, zNNs, rudWDbg);
			// 2.21.0: the trade's read (see TradeCapTau)
			float trP = capZeP, trN = capZeN;
			if (!trInit) { capTrZeP = trP; capTrZeN = trN; trInit = true; }
			float kTr = dt / (Mathf.Max(0f, TradeCapTau) + dt);
			capTrZeP += (trP - capTrZeP) * kTr;
			capTrZeN += (trN - capTrZeN) * kTr;
		}

		private void Probe(List<Effector> effs, Allocator alloc, Vector3 target, Vector3 dT, float ix, float iz, float[] outG, out float colX, out float colZ, int cnt, bool yaw)
		{
			for (int i = 0; i < cnt; i++) effs[i].cmd = savedCmd[i];
			for (int it = 0; it < ProbeIters; it++) alloc.Solve(effs, target + dT);
			outG[0] = outG[1] = outG[2] = 0f;
			colX = colZ = 0f;
			for (int i = 0; i < cnt; i++)
			{
				Effector e = effs[i];
				if (!e.active) continue;
				Vector3 d = e.MAt(e.cmd) - e.MAt(savedCmd[i]);
				colX += d.x * ix; colZ += d.z * iz;
				int g = Group(e);
				if (g < 0) continue;
				outG[g] += yaw ? d.z * iz : d.x * ix;
			}
		}

		// 2.14.0: probe along u; each group's delivery read as its (roll, yaw) acceleration increment projected on u, with the per-axis gains
		private void ProbeS2(List<Effector> effs, Allocator alloc, Vector3 target, Vector3 dT, float ix, float iz, float[] outX, float[] outZ, int cnt)
		{
			for (int i = 0; i < cnt; i++) effs[i].cmd = savedCmd[i];
			for (int it = 0; it < ProbeIters; it++) alloc.Solve(effs, target + dT);
			outX[0] = outX[1] = outX[2] = 0f; outZ[0] = outZ[1] = outZ[2] = 0f;
			for (int i = 0; i < cnt; i++)
			{
				Effector e = effs[i];
				if (!e.active) continue;
				int g = Group(e);
				if (g < 0) continue;
				Vector3 d = e.MAt(e.cmd) - e.MAt(savedCmd[i]);
				float gx = (g == 0) ? CapGainXR : ((g == 1) ? CapGainXS : CapGainXF);
				float gz = (g == 0) ? CapGainZR : ((g == 1) ? CapGainZS : CapGainZF);
				outX[g] += gx * d.x * ix; outZ[g] += gz * d.z * iz;
			}
		}

		// one-parameter RLS on DiffTicks-sample differences: d(wdot) = th . d(total effector moment)
		private bool Rls(ref float th, ref float P, float[] ringM, float[] ringWd, float mNow, float wd, float dt)
		{
			int n = ringM.Length;
			if (ringN < n) return false;
			int old = (ringHead + 1) % n;   // DiffTicks ticks back
			float x = mNow - ringM[old];
			float y = wd - ringWd[old];
			if (Mathf.Abs(x) < GainExcite) return false;
			float lam = Mathf.Exp(-dt / Mathf.Max(0.1f, GainTau));
			float px = P * x;
			float den = lam + x * px;
			if (den < 1E-06f) return false;
			float k = px / den;
			th += k * (y - x * th);
			P = Mathf.Min(5f, (P - k * px) / lam);
			th += GainRidge * (1f - th);
			th = Mathf.Clamp(th, GainLo, GainHi);
			return true;
		}
	}
}
