// 2.4.17 bench: stubs for the ground-steering types added after the bench stubs were last shipped (2.4.11-2.4.14).
// The bench flies in the air, so none of these paths run; they only need to compile.
using UnityEngine;
namespace UnityEngine {
    public class Collider : Component { public Rigidbody attachedRigidbody; }
    public struct RaycastHit { public Collider collider; public Vector3 point; public Vector3 normal; public float distance; public Rigidbody rigidbody => collider?.attachedRigidbody; }
}
public class AnimatedPhysicsSurface : MonoBehaviour { public Vector3 GetVelocity() => Vector3.zero; }
public struct GlobalPosition { public float x, y, z; }
public class ControlsFilter : MonoBehaviour { }
