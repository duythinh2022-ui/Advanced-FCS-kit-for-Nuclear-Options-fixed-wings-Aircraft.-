// Managed stand-ins for the parts of UnityEngine the FCS touches. Semantics follow Unity's own
// (left-handed, Euler ZXY, Mathf.Sign(0) = +1, clamped Lerp, normalized() -> zero below 1e-5).
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }

public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float this[int i]
        {
            get => i == 0 ? x : (i == 1 ? y : z);
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float m = Dot(n, n); if (m < 1e-15f) return v; return v - n * (Dot(v, n) / m); }
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 ClampMagnitude(Vector3 v, float m) { float s = v.sqrMagnitude; return s > m * m ? v.normalized * m : v; }
        public static float Angle(Vector3 a, Vector3 b)
        {
            float d = (float)Math.Sqrt(a.sqrMagnitude * b.sqrMagnitude);
            if (d < 1e-15f) return 0f;
            return (float)Math.Acos(Mathf.Clamp(Dot(a, b) / d, -1f, 1f)) * 57.29578f;
        }
        public static Vector3 RotateTowards(Vector3 cur, Vector3 target, float maxRad, float maxMag)
        {
            Vector3 a = cur.normalized, b = target.normalized; float ang = Angle(a, b) * 0.017453292f;
            if (ang <= maxRad || ang < 1e-6f) return b * cur.magnitude;
            Vector3 ax = Cross(a, b); if (ax.sqrMagnitude < 1e-12f) return cur;
            ax = ax.normalized; float t = maxRad; Vector3 r = a * (float)Math.Cos(t) + Cross(ax, a) * (float)Math.Sin(t) + ax * Dot(ax, a) * (1f - (float)Math.Cos(t));
            return r * cur.magnitude;
        }
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) { float an = Angle(from, to); return Dot(axis, Cross(from, to)) < 0f ? -an : an; }
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public static Quaternion operator *(Quaternion l, Quaternion r) => new Quaternion(
            l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y,
            l.w * r.y + l.y * r.w + l.z * r.x - l.x * r.z,
            l.w * r.z + l.z * r.w + l.x * r.y - l.y * r.x,
            l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z);
        public static Vector3 operator *(Quaternion q, Vector3 p)
        {
            float num = q.x * 2f, num2 = q.y * 2f, num3 = q.z * 2f;
            float num4 = q.x * num, num5 = q.y * num2, num6 = q.z * num3;
            float num7 = q.x * num2, num8 = q.x * num3, num9 = q.y * num3;
            float num10 = q.w * num, num11 = q.w * num2, num12 = q.w * num3;
            return new Vector3(
                (1f - (num5 + num6)) * p.x + (num7 - num12) * p.y + (num8 + num11) * p.z,
                (num7 + num12) * p.x + (1f - (num4 + num6)) * p.y + (num9 - num10) * p.z,
                (num8 - num11) * p.x + (num9 + num10) * p.y + (1f - (num4 + num5)) * p.z);
        }
        public static Quaternion Inverse(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (n < 1e-12f) return identity;
            return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n);
        }
        public Quaternion normalized
        {
            get { float m = (float)Math.Sqrt(x * x + y * y + z * z + w * w); return m < 1e-12f ? identity : new Quaternion(x / m, y / m, z / m, w / m); }
        }
        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            Vector3 a = axis.normalized;
            float h = angle * 0.5f * (float)(Math.PI / 180.0);
            float s = (float)Math.Sin(h);
            return new Quaternion(a.x * s, a.y * s, a.z * s, (float)Math.Cos(h));
        }
        // Unity Euler: Z first, then X, then Y  =>  q = qy * qx * qz
        public static Quaternion Euler(float x, float y, float z)
            => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);
        public Vector3 eulerAngles
        {
            get
            {
                // inverse of Euler(ZXY): rotation matrix R = Ry*Rx*Rz
                Quaternion q = this;
                float m12 = 2f * (q.y * q.z - q.w * q.x);            // R[1,2]
                float sx = -m12;
                sx = Mathf.Clamp(sx, -1f, 1f);
                float ex = (float)Math.Asin(sx);
                float ey, ez;
                float m02 = 2f * (q.x * q.z + q.w * q.y);             // R[0,2]
                float m22 = 1f - 2f * (q.x * q.x + q.y * q.y);        // R[2,2]
                float m10 = 2f * (q.x * q.y + q.w * q.z);             // R[1,0]
                float m11 = 1f - 2f * (q.x * q.x + q.z * q.z);        // R[1,1]
                if (Math.Abs(sx) < 0.99999f)
                {
                    ey = (float)Math.Atan2(m02, m22);
                    ez = (float)Math.Atan2(m10, m11);
                }
                else
                {
                    float m00 = 1f - 2f * (q.y * q.y + q.z * q.z);
                    float m20 = 2f * (q.x * q.z - q.w * q.y);
                    ey = (float)Math.Atan2(-m20, m00);
                    ez = 0f;
                }
                return new Vector3(Wrap(ex * 57.29578f), Wrap(ey * 57.29578f), Wrap(ez * 57.29578f));
            }
        }
        private static float Wrap(float a) { a %= 360f; if (a < 0) a += 360f; return a; }
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public override string ToString() => $"({x:F4}, {y:F4}, {z:F4}, {w:F4})";
    }

    public struct Matrix4x4
    {
        private float[,] m;
        public float this[int r, int c] { get => m[r, c]; set => m[r, c] = value; }
        public static Matrix4x4 Rotate(Quaternion q)
        {
            var r = new Matrix4x4 { m = new float[4, 4] };
            float x = q.x * 2f, y = q.y * 2f, z = q.z * 2f;
            float xx = q.x * x, yy = q.y * y, zz = q.z * z, xy = q.x * y, xz = q.x * z, yz = q.y * z;
            float wx = q.w * x, wy = q.w * y, wz = q.w * z;
            r.m[0, 0] = 1f - (yy + zz); r.m[1, 0] = xy + wz; r.m[2, 0] = xz - wy;
            r.m[0, 1] = xy - wz; r.m[1, 1] = 1f - (xx + zz); r.m[2, 1] = yz + wx;
            r.m[0, 2] = xz + wy; r.m[1, 2] = yz - wx; r.m[2, 2] = 1f - (xx + yy);
            r.m[3, 3] = 1f;
            return r;
        }
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 57.29578f;
        public const float Infinity = float.PositiveInfinity;
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int f) => Math.Abs(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(params float[] v) { float r = v[0]; for (int i = 1; i < v.Length; i++) if (v[i] < r) r = v[i]; return r; }
        public static float Max(params float[] v) { float r = v[0]; for (int i = 1; i < v.Length; i++) if (v[i] > r) r = v[i]; return r; }
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Sign(t - c) * d;
        public static float Repeat(float t, float len) => Clamp(t - (float)Math.Floor(t / len) * len, 0f, len);
        public static float DeltaAngle(float c, float t) { float n = Repeat(t - c, 360f); if (n > 180f) n -= 360f; return n; }
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return b * t + a * (1f - t); }
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), 1.1754944E-38f * 8f);
    }

    public static class Time
    {
        public static float fixedDeltaTime = 0.02f;
        public static float deltaTime = 0.02f;
        public static float time;
    }

    public static class Physics
    {
        public static Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    }

    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { Console.Error.WriteLine(o); }
    }

    public class Object
    {
        private static int nextId = 1;
        private readonly int id = nextId++;
        public virtual string name { get; set; } = "";
        public int GetInstanceID() => id;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }

    public class GameObject : Object
    {
        public Transform transform;
        public readonly List<Component> components = new List<Component>();
        public GameObject(string n) { name = n; transform = new Transform(); transform.gameObject = this; components.Add(transform); }
        public T GetComponent<T>() where T : Component { foreach (var c in components) if (c is T t) return t; return null; }
        public T AddComponent<T>() where T : Component, new() { var c = new T(); c.gameObject = this; components.Add(c); return c; }
        public void Add(Component c) { c.gameObject = this; components.Add(c); }
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public override string name { get => gameObject != null ? gameObject.name : ""; set { if (gameObject != null) gameObject.name = value; } }
    }

    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        public static int Epoch;          // bump whenever any pose changes
        private Transform _parent;
        public readonly List<Transform> children = new List<Transform>();
        private Vector3 _lp; private Quaternion _lr = Quaternion.identity; private Vector3 _ls = Vector3.one;
        private int cacheEpoch = -1; private Vector3 cPos; private Quaternion cRot; private Vector3 cScale;
        public Transform parent
        {
            get => _parent;
            set { _parent?.children.Remove(this); _parent = value; _parent?.children.Add(this); Epoch++; }
        }
        public void SetParent(Transform p) => parent = p;
        public Vector3 localPosition { get => _lp; set { _lp = value; Epoch++; } }
        public Quaternion localRotation { get => _lr; set { _lr = value; Epoch++; } }
        public Vector3 localScale { get => _ls; set { _ls = value; Epoch++; } }
        public Vector3 localEulerAngles { get => _lr.eulerAngles; set { _lr = Quaternion.Euler(value); Epoch++; } }
        private void Update()
        {
            if (cacheEpoch == Epoch) return;
            if (_parent == null) { cPos = _lp; cRot = _lr; cScale = _ls; }
            else
            {
                _parent.Update();
                cPos = _parent.cPos + _parent.cRot * Vector3.Scale(_parent.cScale, _lp);
                cRot = _parent.cRot * _lr;
                cScale = Vector3.Scale(_parent.cScale, _ls);
            }
            cacheEpoch = Epoch;
        }
        public Vector3 position
        {
            get { Update(); return cPos; }
            set { if (_parent == null) localPosition = value; else { _parent.Update(); localPosition = Quaternion.Inverse(_parent.cRot) * (value - _parent.cPos); } }
        }
        public Quaternion rotation
        {
            get { Update(); return cRot; }
            set { if (_parent == null) localRotation = value; else { _parent.Update(); localRotation = Quaternion.Inverse(_parent.cRot) * value; } }
        }
        public Vector3 lossyScale { get { Update(); return cScale; } }
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 right => rotation * Vector3.right;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 TransformPoint(Vector3 p) { Update(); return cPos + cRot * Vector3.Scale(cScale, p); }
        public Vector3 InverseTransformPoint(Vector3 p) { Update(); var l = Quaternion.Inverse(cRot) * (p - cPos); return new Vector3(l.x / cScale.x, l.y / cScale.y, l.z / cScale.z); }
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        public Vector3 InverseTransformDirection(Vector3 d) => Quaternion.Inverse(rotation) * d;
        public bool IsChildOf(Transform p) { for (var t = this; t != null; t = t._parent) if (t == p) return true; return false; }
    }

    public class Rigidbody : Component
    {
        public float mass = 1f;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public Vector3 worldCenterOfMass;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 inertiaTensor = Vector3.one;
        public Quaternion inertiaTensorRotation = Quaternion.identity;
        public Vector3 GetPointVelocity(Vector3 p) => velocity + Vector3.Cross(angularVelocity, p - worldCenterOfMass);
    }

    public enum WrapMode { Default = 0, Once = 1, Clamp = 1, Loop = 2, PingPong = 4, ClampForever = 8 }

    public struct Keyframe
    {
        public float time, value, inTangent, outTangent;
        public Keyframe(float t, float v, float i, float o) { time = t; value = v; inTangent = i; outTangent = o; }
    }

    public class AnimationCurve
    {
        public Keyframe[] keys;
        public AnimationCurve(params Keyframe[] k) { keys = k; }
        public float Evaluate(float t)
        {
            var k = keys;
            if (k == null || k.Length == 0) return 0f;
            if (t <= k[0].time) return k[0].value;
            if (t >= k[k.Length - 1].time) return k[k.Length - 1].value;
            int i = 0;
            while (i < k.Length - 2 && t > k[i + 1].time) i++;
            var a = k[i]; var b = k[i + 1];
            float dt = b.time - a.time;
            if (dt <= 0f) return b.value;
            float s = (t - a.time) / dt;
            float m0 = a.outTangent * dt, m1 = b.inTangent * dt;
            if (float.IsInfinity(m0) || float.IsInfinity(m1)) return a.value; // stepped
            float s2 = s * s, s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * a.value + (s3 - 2f * s2 + s) * m0 + (-2f * s3 + 3f * s2) * b.value + (s3 - s2) * m1;
        }
    }
}
