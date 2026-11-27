// generated from bench/extract/vortex_airframe.json (stock FS-20B, SmallFighter1) by the 0.7.0 port; do not edit by hand
using UnityEngine;

namespace Vortex_FS20.FCS
{
	internal static partial class GeomGate
	{
		// part name, wing area (m^2), part origin in the aircraft root frame (m), centerOfLift (lift frame, m), HLD deployedArea (0 = none)
		private static readonly (string name, float area, Vector3 pos, Vector3 col, float hldDep)[] Ref =
		{
			("wing1b_L", 2.6000f, new Vector3(-1.4173f, 0.0376f, -2.7441f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wing2b_L", 2.7000f, new Vector3(-3.0930f, -0.1826f, -2.8480f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("elevator_R", 2.0000f, new Vector3(2.6022f, -0.1852f, -3.7423f), new Vector3(0.0000f, 0.0000f, -0.5000f), 0.0000f),
			("aileron_R", 2.0000f, new Vector3(3.9423f, -0.2235f, -3.5830f), new Vector3(0.0000f, 0.0000f, -0.5000f), 0.0000f),
			("wingtip_R", 1.0000f, new Vector3(4.2627f, -0.2023f, -2.7480f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wing2a_R", 3.5000f, new Vector3(3.2901f, -0.1738f, -1.7734f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("gearBay_R", 0.3000f, new Vector3(1.4103f, -0.4806f, -1.1289f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wing1a_L", 3.5000f, new Vector3(-1.5889f, 0.0134f, -0.6173f), new Vector3(0.0000f, 0.0000f, 0.0000f), 1.0000f),
			("cockpit", 0.5000f, new Vector3(0.0000f, 0.0513f, 4.6335f), new Vector3(0.0000f, 0.0000f, 1.0000f), 0.0000f),
			("nozzle", 0.0000f, new Vector3(0.0000f, -0.2485f, -5.2389f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("tail_L", 1.5000f, new Vector3(-1.2195f, -0.2252f, -4.7346f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wing2b_R", 2.7000f, new Vector3(3.0930f, -0.1826f, -2.8480f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("vstab_R", 1.5000f, new Vector3(1.7340f, 0.9684f, -5.0585f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("fuselage_R", 0.0000f, new Vector3(0.0000f, -0.7983f, -3.2896f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wingRoot_L", 4.0000f, new Vector3(-0.9161f, -0.2867f, 2.0913f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wingRoot_R", 4.0000f, new Vector3(0.9161f, -0.2867f, 2.0913f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("gearBay_L", 0.3000f, new Vector3(-1.4103f, -0.4806f, -1.1289f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("intake", 1.2000f, new Vector3(0.0000f, -1.0786f, 4.2681f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("weaponBay_R", 0.5000f, new Vector3(0.5452f, -0.9441f, -0.7300f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("SmallFighter1", 0.5000f, new Vector3(0.0000f, 0.0000f, 0.0000f), new Vector3(0.0000f, 0.0000f, 0.5000f), 0.0000f),
			("rudder_R", 1.5000f, new Vector3(1.6734f, 0.9171f, -5.6533f), new Vector3(0.0000f, 0.0000f, -0.5000f), 0.0000f),
			("wing1a_R", 3.5000f, new Vector3(1.5889f, 0.0134f, -0.6173f), new Vector3(0.0000f, 0.0000f, 0.0000f), 1.0000f),
			("elevator_L", 2.0000f, new Vector3(-2.6022f, -0.1852f, -3.7423f), new Vector3(0.0000f, 0.0000f, -0.5000f), 0.0000f),
			("rudder_L", 1.5000f, new Vector3(-1.6734f, 0.9171f, -5.6533f), new Vector3(0.0000f, 0.0000f, -0.5000f), 0.0000f),
			("weaponBay_L", 0.5000f, new Vector3(-0.5452f, -0.9441f, -0.7300f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wingtip_L", 1.0000f, new Vector3(-4.2627f, -0.2023f, -2.7480f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("tail_R", 1.5000f, new Vector3(1.2195f, -0.2252f, -4.7346f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("vstab_L", 1.5000f, new Vector3(-1.7340f, 0.9684f, -5.0585f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("engine", 0.0000f, new Vector3(0.0000f, -0.3818f, -2.9128f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("aileron_L", 2.0000f, new Vector3(-3.9423f, -0.2235f, -3.5830f), new Vector3(0.0000f, 0.0000f, -0.5000f), 0.0000f),
			("wing1b_R", 2.6000f, new Vector3(1.4173f, 0.0376f, -2.7441f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("liftFan", 0.3000f, new Vector3(0.0000f, -0.2264f, 2.2087f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("wing2a_L", 3.5000f, new Vector3(-3.2901f, -0.1738f, -1.7734f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
			("nose", 0.0000f, new Vector3(0.0000f, -0.2931f, 6.7404f), new Vector3(0.0000f, 0.0000f, 0.0000f), 0.0000f),
		};

		// control surface name, visible (hinge) transform origin in the aircraft root frame (m)
		private static readonly (string name, Vector3 vis)[] RefVis =
		{
			("elevator_R", new Vector3(2.6022f, -0.1852f, -3.5143f)),
			("aileron_R", new Vector3(3.9423f, -0.2235f, -3.3550f)),
			("rudder_R", new Vector3(1.0825f, 0.1494f, -4.9952f)),
			("elevator_L", new Vector3(-2.6022f, -0.1852f, -3.5143f)),
			("rudder_L", new Vector3(-1.0825f, 0.1494f, -4.9952f)),
			("aileron_L", new Vector3(-3.9423f, -0.2235f, -3.3550f)),
		};
	}
}
