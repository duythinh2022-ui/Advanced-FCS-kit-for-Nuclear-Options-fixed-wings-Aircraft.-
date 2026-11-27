using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
	internal struct EvalContext
	{
		public Vector3 cg;

		public Quaternion rootInv;

		public float rho;

		public Vector3 wind;

		// 2.0.8: the aircraft's air velocity (world), the reference flow for the stab downwash
		public Vector3 vAir;

		// 4.8.0 slat-coupled aft lift: the aft fixed parts' airfoil moment per side at the present flow (aero frame; centre
		// parts half to each side), set by the controller each tick before the effectors are evaluated
		public Vector3 aftMomL;

		public Vector3 aftMomR;
	}
}
