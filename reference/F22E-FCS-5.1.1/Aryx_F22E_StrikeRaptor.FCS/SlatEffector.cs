using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// 4.6.1 (SlatEffectors): one leading-edge slat as an effector of the control allocator. Tom, 23 Nov: "also slat roll".
	//
	// The effector's value is the slat's droop away from its schedule (deg): 0 = on the schedule (the airframe the fore / aft
	// balance is fitted for), negative = retracted toward flush, positive = drooped further, within [0, SlatMaxDroop] of droop.
	// Its moment table is SlatAero's physics at the present flow - the panel behind it stalling earlier / later (HostDelta at the
	// panel's centre of lift) and the slat's own plate (PlateDelta at its hinge) - the same terms the game-side SlatForce and the
	// bench apply, so the allocator's model is the force the jet gets. Mnow is that moment at the slat's present droop (zero on
	// the schedule), so the sum of the effectors' Mnow stays "what the effectors are doing beyond the scheduled airframe".
	// FlapSystem moves the slat to its schedule plus this effector's output at SlatRate while the FCS is engaged in the air.
	internal sealed class SlatEffector : Effector
	{
		public readonly int seg;

		// 5.0.0 (SlatGanged): one effector per side moving all three of that side's slats together (one slat per side to the
		// allocator). segs / hosts / slats / tables are the members (one entry when not ganged); the moment table is the sum of
		// the members' moments at the same droop away from the schedule. FlapSystem drives every member from this effector's
		// command, so the three slats on a side never split.
		internal static bool SlatGanged = false;

		public readonly int[] segs;

		// the slats' regularization (the allocator's price of moving one off its schedule, per (droop / SlatMaxDroop)^2) and its
		// rate term
		internal static float SlatMu = 0.05f;

		internal static float SlatLam = 2E-05f;

		internal static float SlatReachTau = 0.1f;

		private readonly AeroPart[] hosts;

		private readonly Transform[] slats;

		private readonly AirfoilTable[] tables;

		private readonly FlapSystem flaps;

		public SlatEffector(int seg, AeroPart host, Transform slat, AirfoilTable table, FlapSystem flaps)
			: this(new int[1] { seg }, new AeroPart[1] { host }, new Transform[1] { slat }, new AirfoilTable[1] { table }, flaps)
		{
		}

		public SlatEffector(int[] segs, AeroPart[] hosts, Transform[] slats, AirfoilTable[] tables, FlapSystem flaps)
		{
			seg = segs[0];
			this.segs = segs;
			this.hosts = hosts;
			this.slats = slats;
			this.tables = tables;
			this.flaps = flaps;
			name = "Slat_" + (SlatAero.Segs[seg].side < 0 ? "L" : "R") + ((segs.Length == 1) ? SlatAero.Segs[seg].idx.ToString() : "");
			min = 0f;
			max = 0f;
			lo = 0f;
			hi = 0f;
			mu = 0.0001f;
			lam = 2E-05f;
			AllocGrid(7);
		}

		private Vector3 Moment(int k, float dev, float droopRef, Vector3 v, Vector3 vp, Quaternion lr, Vector3 col, float qS, float cov, ref EvalContext ctx)
		{
			AirfoilTable table = tables[k];
			Transform slat = slats[k];
			Vector3 f = SlatAero.HostDelta(v, lr, table.ClTable, table.CdTable, qS, cov, dev);
			Vector3 m = Vector3.Cross(col - ctx.cg, f);
			if (slat != null && Plugin.SlatChordFrac > 0f)
			{
				float qSp = qS * cov * Plugin.SlatChordFrac * ((v.sqrMagnitude > 1f) ? (vp.sqrMagnitude / v.sqrMagnitude) : 1f);
				Vector3 fp = SlatAero.PlateDelta(vp, lr, table.ClTable, table.CdTable, qSp, droopRef + dev, droopRef);
				m += Vector3.Cross(slat.position - ctx.cg, fp);
			}
			Vector3 ma = Effector.ToAero(ctx.rootInv, m);
			if (FcsController.SlatAftGain != 0f)
			{
				// 4.7.0: this slat's third of its side's coupled aft lift
				ma += (SlatAero.Segs[segs[k]].side < 0 ? ctx.aftMomL : ctx.aftMomR) * (FcsController.SlatAftGain * dev / (3f * Mathf.Max(1f, Plugin.SlatMaxDroop)));
			}
			return ma;
		}

		public override void Evaluate(ref EvalContext ctx)
		{
			active = false;
			nodeOn = false;
			Mnow = Vector3.zero;
			for (int i = 0; i < N; i++)
			{
				M[i] = Vector3.zero;
			}
			if (!SlatAero.On || !Plugin.SlatEffectors || flaps == null)
			{
				cmd = 0f;
				return;
			}
			float droopRef = flaps.segRef[seg];
			float droopNow = flaps.segDroop[seg];
			float dMax = Plugin.SlatMaxDroop;
			min = -droopRef;
			max = dMax + FcsController.SlatOverDroop - droopRef;
			lo = min;
			hi = max;
			now = droopNow - droopRef;
			rate = Plugin.SlatRate;
			regNorm = dMax;
			mu = SlatMu * segs.Length;
			lam = SlatLam * segs.Length;
			// 4.6.6: the allocator balances the other surfaces against where the slat will be within SlatReachTau s (its
			// actuator's reach), not against its command, so a slat still in transit is not counted as already there
			reachTau = SlatReachTau;
			twoHorizon = SlatReachTau > 0f;
			for (int i = 0; i < N; i++)
			{
				grid[i] = min + (float)i * (max - min) / (float)(N - 1);
			}
			int used = 0;
			for (int k = 0; k < segs.Length; k++)
			{
				AeroPart host = hosts[k];
				AirfoilTable table = tables[k];
				if (table == null || table.Generic || host == null || host.rb == null || host.IsDetached())
				{
					continue;
				}
				Transform lt = Acc.AP_liftNormal(host);
				if (lt == null)
				{
					continue;
				}
				Vector3 v = host.rb.velocity - ctx.wind;
				float v2 = v.sqrMagnitude;
				if (v2 < 25f)
				{
					continue;
				}
				Transform slat = slats[k];
				float rk = flaps.segRef[segs[k]];
				float nk = flaps.segDroop[segs[k]] - rk;
				Vector3 vp = (slat != null) ? (host.rb.GetPointVelocity(slat.position) - ctx.wind) : v;
				Quaternion lr = lt.rotation;
				Vector3 col = host.rb.worldCenterOfMass + lr * Acc.AP_centerOfLift(host);
				float qS = 0.5f * ctx.rho * v2 * Acc.AP_wingArea(host) * Acc.AP_wingEff(host);
				float cov = SlatAero.Cover(SlatAero.Segs[segs[k]].idx);
				for (int i = 0; i < N; i++)
				{
					M[i] += Moment(k, grid[i], rk, v, vp, lr, col, qS, cov, ref ctx);
				}
				Mnow += Moment(k, nk, rk, v, vp, lr, col, qS, cov, ref ctx);
				used++;
			}
			if (used == 0)
			{
				for (int i = 0; i < N; i++)
				{
					M[i] = Vector3.zero;
				}
				Mnow = Vector3.zero;
				cmd = 0f;
				return;
			}
			active = true;
			cmd = Mathf.Clamp(cmd, min, max);
		}
	}
}
