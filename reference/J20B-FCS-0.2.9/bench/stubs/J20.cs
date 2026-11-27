using System.Collections.Generic;
using UnityEngine;

// J-20B 0.1.0 bench: the J-20B mod's surface system as the FCS sees it (J20Bridge), filled from the airframe JSON's "j20" block by
// Airframe; the plant runs the mod's own Step for what the FCS does not own (LE flaps, wing1 high-lift area, force points).
namespace Aryx_F22E_StrikeRaptor.FCS
{
	internal static class J20Bridge
	{
		public const string ModGuid = "reasonix.j20.aerocontrol";

		public static bool Present = true;

		internal sealed class Raw { public float angle; }

		internal sealed class Surf
		{
			public object raw; public string id; public string role; public string side; public AeroPart owner; public Transform hinge; public Transform normal;
			public Quaternion rest; public float min; public float max; public float servo; public float pitchGain; public float rollGain; public float yawGain;
			// the mod's schedule (bench J20Step)
			public float baseDeg, lowSpeedDegrees, lowSpeedFull, lowSpeedZero, aoaGain, aoaDeadzone, aoaMaxDegrees, aoaSpeedFull, aoaSpeedZero; public bool lowSpeedRequiresGear;
			public string PartName => (owner != null) ? owner.gameObject.name : id;
		}

		internal sealed class CamberNormal { public object raw; public AeroPart owner; public Transform normal; public Quaternion rest; }

		internal sealed class HighLift { public AeroPart owner; public float baseArea, additionalArea, leadingAdditionalArea, leadingFullDegrees, leadingSign; public List<Surf> leading = new List<Surf>(); }

		public static readonly List<Surf> All = new List<Surf>();
		public static readonly List<CamberNormal> Cambers = new List<CamberNormal>();
		public static readonly List<HighLift> HL = new List<HighLift>();
		public static readonly Dictionary<AeroPart, Transform> ForcePoints = new Dictionary<AeroPart, Transform>();

		public static List<Surf> Surfaces(Aircraft a) => new List<Surf>(All);
		public static List<CamberNormal> CamberNormals(Aircraft a) => new List<CamberNormal>(Cambers);
		public static List<AeroPart> HighLiftOwners(Aircraft a) { var l = new List<AeroPart>(); foreach (var h in HL) l.Add(h.owner); return l; }
		public static void SyncAngle(object raw, float angle) { if (raw is Raw r) r.angle = angle; }
		public static float AngleOf(object raw) => (raw is Raw r) ? r.angle : 0f;
	}

	internal static class CamberForce
	{
		public static readonly List<(HingeFlapEffector hf, AirfoilTable t)> Hinges = new List<(HingeFlapEffector, AirfoilTable)>();
		public static void AddHinge(Aircraft a, HingeFlapEffector hf, AirfoilTable table) { if (hf != null && table != null) Hinges.Add((hf, table)); }
		public static readonly List<(SurfaceEffector se, AirfoilTable t)> HingeSurfaces = new List<(SurfaceEffector, AirfoilTable)>();
		public static void AddHingeSurface(Aircraft a, FcsController owner, SurfaceEffector se, AirfoilTable table) { if (se != null && table != null && se.host != null) HingeSurfaces.Add((se, table)); }
	}
}
