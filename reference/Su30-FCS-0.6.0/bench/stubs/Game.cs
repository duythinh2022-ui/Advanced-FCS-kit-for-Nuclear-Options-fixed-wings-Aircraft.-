// Nuclear Option types the FCS reads, reduced to the fields it actually touches.
using System;
using System.Collections.Generic;
using UnityEngine;

public class Airbrake : MonoBehaviour
{
    public float dragAmount = 0.6f, maxAngle, openSpeed = 5f, openAmount;
    public AeroPart part;
    public Transform tr; public Quaternion baseRot;
}
public class ControlInputs
{
    public float pitch, roll, yaw, throttle, brake, customAxis1;
}

[Serializable]
public class Airfoil
{
    public string name;
    public AnimationCurve liftCoef;
    public AnimationCurve dragCoef;
}

public class AircraftParameters : MonoBehaviour
{
    public Airfoil[] airfoils;
}

public class UnitPart : MonoBehaviour
{
    public Rigidbody rb;
    public float mass;
    public bool detached;
    public Aircraft parentUnit;
    public bool IsDetached() => detached;
    public void ModifyMass(float amount) { mass += amount; if (rb != null) rb.mass = mass; }
}

public class FuelTank : MonoBehaviour
{
    UnitPart part; Aircraft aircraft; float leakRate; bool onFire; float fuelCapacity;
    public float fuelMass;
    public float GetCapacity() => fuelCapacity;
    public float GetLevel() => fuelMass;
    public void UseFuel(float r) { float o = fuelMass; fuelMass = Math.Max(0f, fuelMass - r); part.ModifyMass(fuelMass - o); }
    public void Init(UnitPart p, Aircraft a, float cap) { part = p; aircraft = a; fuelCapacity = cap; fuelMass = cap; }
    public void SetLeak(float r) { leakRate = r; }
}

public class Hardpoint { public UnitPart part; }

public class AeroPart : UnitPart
{
    public int airfoil;
    public int airfoilID = -1;
    public int ChartId => airfoilID >= 0 ? airfoilID : airfoil;
    public Vector3 centerOfLift;
    public Transform liftNormal;
    public float wingArea;
    public float wingEffectiveness = 1f;
    public float dragArea;
    public float airflowChanneling;
}

public class Aircraft : MonoBehaviour
{
    public Rigidbody rb;
    public float airDensity = 1f;
    public float speed; public float radarAlt = 1000f;
    public bool gearDeployed;
    public List<UnitPart> partLookup = new List<UnitPart>();
    public Vector3 windVelocity;
    public List<ControlSurface> controlSurfaces = new List<ControlSurface>();
    public ControlInputs inputs = new ControlInputs();
    public AircraftParameters parameters;
    public readonly List<Component> all = new List<Component>();
    public ControlInputs GetInputs() => inputs;
    List<FuelTank> fuelTanks = new List<FuelTank>();
    float fuelCapacity;
    public bool LocalSim => true;
    public float needs = -1f;
    public void CheckNeedsFuel(float r) { needs = r; }
    public void AddTank(FuelTank t) { fuelTanks.Add(t); fuelCapacity += t.GetCapacity(); }
    public List<FuelTank> Tanks => fuelTanks;
    public AircraftParameters GetAircraftParameters() => parameters;
}

public class ControlSurface : MonoBehaviour
{
    public Aircraft aircraft;
    public UnitPart attachedSurface;
    public float pitchRange, rollRange, yawRange, brakeRange, maxSplit, splitAmount, servoSpeed, splitDrag, yawSplitFactor;
    public bool locked;
    public GameObject visibleMesh;
    public NuclearOption.Jobs.ControlSurfaceFields job;
}

public class JetNozzle : MonoBehaviour
{
    public Transform thrustTransform;
    public float totalThrust;
    public float abThrust; public float abStart = 0.95f; public float abAmt;   // game Afterburner.afterburnerAmount (Lerp smoothing)
    public UnitPart part;
}

public class Turbofan : MonoBehaviour
{
    public Aircraft aircraft;
    public Vector3 nozzleAngles;
    public JetNozzle[] nozzles;
    public bool operable = true; public float condition = 1f;
    public Vector3 thrustVectoring;
    public Vector3 thrustVectoringGain = new Vector3(1f, 1f, 4f);
    public float thrustVectoringMaxAirspeed = 450f;
    public Transform[] vectoringTransforms;
    public float staticThrust;
    public float[][] speedThrust;
    // Ifrit (NO Turbojet): rpm spool, throttle remap, altitude curve, overspeed lapse
    public bool turbojet; public float rpm = 3000f, minRPM = 1000f, maxRPM = 3000f, spoolRate = 700f, maxSpeed = 551f, remapX = 0f, remapY = 1f, abStart = 0.95f, parasitic = 0f;
    public float[][] altCurve;
    // Ternion 0.1.0: the game's Turbofan.GetThrustRatio (bench: delivered thrust over the military rating)
    public float GetThrustRatio() { float t = 0f; if (nozzles != null) foreach (var n in nozzles) t += n.totalThrust; return staticThrust > 0f ? Mathf.Clamp01(t / staticThrust) : 0f; }
}

public class LandingGear : MonoBehaviour
{
    public enum GearState { Uninitialized, LockedRetracted, LockedExtended, Retracting, Extending }
    public float compression, travel = 0.7f;
    public bool WeightOnWheel(float f) => compression > travel * f;
    public float steeringLock;
    public RaycastHit hit; public float steeringAngle, response = 1f, wheelSpeed, compressionForce, dampingForce, frictionCoef = 1f;
    // Su-30 0.5.3 ground-yaw bench (gyaw)
    public bool steering, braked; public float differentialBrakeFactor, groundSpeed, brakeStrength, steeringSpeed = 60f, aligningStrength = 5f;
    public ControlInputs controlInputs; public Aircraft aircraft;
}

public class MovingPart
{
    public Transform transform;
    public bool move, rotate;
    public Vector3 positionRetracted, positionDeployed, anglesRetracted, anglesDeployed;
}

public class HighLiftDevice : MonoBehaviour
{
    public AeroPart aeroPart;
    public float speedDeployed, speedRetracted, partAreaDeployed, partAreaRetracted, position, alphaMin, alphaFactor;
    public MovingPart[] movingParts;
}

namespace Aryx_F22E_StrikeRaptor
{
    public class AryxAlphaResponder : MonoBehaviour
    {
        public Transform rotator;
        public bool isFunctional = true;
        public float deflection;
    }
}

namespace NuclearOption.Jobs
{
    public struct ControlSurfaceFields
    {
        public float pitchRange, rollRange, yawRange, brakeRange;
        public bool flap;
        public float servoSpeed, maxSplit;
        public Quaternion restingRotation;
        public bool IsDetached;
        public ControlInputsBurst controlInputs;
        public float currentPitch, currentRoll, currentYaw, splitAmount;
        public LandingGear.GearState gearState;
    }
    public struct ControlInputsBurst { public float pitch, roll, yaw, throttle, brake; }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public static bool Echo = System.Environment.GetEnvironmentVariable("BENCH_ECHO") == "1";
        public readonly List<string> lines = new List<string>();
        public void LogInfo(object o) { lines.Add(o?.ToString()); if (Echo) Console.Error.WriteLine("[info] " + o); }
        public void LogWarning(object o) { lines.Add(o?.ToString()); if (Echo) Console.Error.WriteLine("[warn] " + o); }
        public void LogError(object o) { lines.Add(o?.ToString()); Console.Error.WriteLine("[err] " + o); }
        public void LogDebug(object o) { }
    }
}

namespace HarmonyLib
{
    using System.Reflection;
    public static class AccessTools
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        public static Type TypeByName(string n)
        {
            foreach (var t in typeof(AccessTools).Assembly.GetTypes()) if (t.FullName == n || t.Name == n) return t;
            return null;
        }
        public static FieldInfo Field(Type t, string n) => t?.GetField(n, all);
        public delegate ref F FieldRef<in T, F>(T instance);
        public static FieldRef<T, F> FieldRefAccess<T, F>(string name)
        {
            var fi = typeof(T).GetField(name, all) ?? throw new Exception("no field " + name);
            var dm = new System.Reflection.Emit.DynamicMethod("fr_" + name, typeof(F).MakeByRefType(), new[] { typeof(T) }, typeof(T), true);
            var il = dm.GetILGenerator(); il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Ldflda, fi); il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return (FieldRef<T, F>)dm.CreateDelegate(typeof(FieldRef<T, F>));
        }
    }
    public class Traverse
    {
        readonly object o; readonly FieldInfo f;
        Traverse(object o, FieldInfo f) { this.o = o; this.f = f; }
        public static Traverse Create(object o) => new Traverse(o, null);
        public Traverse Field(string n) => new Traverse(o, o.GetType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        public T GetValue<T>() => (T)f.GetValue(o);
    }
}

namespace Aryx_F22E_StrikeRaptor.FCS
{
	// Revoker 0.2.20: the game-side camber force for the staggered inboard elevons is a MonoBehaviour; the bench plant applies
	// the flap-camber coupling itself (Plant.cs), so the registration is a no-op here
	internal static class CamberForce
	{
		public static void AddHinge(Aircraft a, HingeFlapEffector hf, AirfoilTable table) { }
	}
}
