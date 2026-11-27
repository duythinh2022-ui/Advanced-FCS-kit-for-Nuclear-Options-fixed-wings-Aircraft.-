// 0.5.x ground-steering stand-ins (never exercised by the air bench)
using UnityEngine;
namespace UnityEngine
{
    public class Collider : Component { public Rigidbody attachedRigidbody; }
    public struct RaycastHit { public Collider collider; public Vector3 point; public Vector3 normal; }
}
public struct GlobalPosition { public float x, y, z; }
public class ControlsFilter { }
public class AnimatedPhysicsSurface : MonoBehaviour { public Vector3 GetVelocity() => Vector3.zero; public Vector3 angularVelocity; }
public static class GroundStubExt { public static GlobalPosition GlobalPosition(this Aircraft a) => default; }
namespace Aryx_F22E_StrikeRaptor.FCS {
internal static partial class AccG { }
}
