using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// shared asymmetric-load code (AsymShared.cs) stand-in: this jet has no hinged flaps (no HingeFlapEffector is ever made, so the
	// list 'hinges' stays empty). Only the members the shared sweep refers to; the real class is in the Revoker / Compass / Ternion /
	// Su-30 / Vortex mods.
	internal sealed class HingeFlapEffector : Effector
	{
		public AeroPart part;

		public AeroPart host;

		public Transform hostLN;

		public Quaternion HostRot0 => Quaternion.identity;

		public float SymNow => now;

		internal struct FlowSnap
		{
			public bool ok;
		}

		internal void Snap(out FlowSnap s)
		{
			s = default(FlowSnap);
		}

		internal Vector3 EvalFlowSnap(float delta, Vector3 vFlow, float rho, ref EvalContext ctx, ref FlowSnap s)
		{
			return Vector3.zero;
		}

		public Vector3 EvalFlow(float delta, Vector3 vFlow, float rho, ref EvalContext ctx)
		{
			return Vector3.zero;
		}

		public override void Evaluate(ref EvalContext ctx)
		{
		}
	}
}
