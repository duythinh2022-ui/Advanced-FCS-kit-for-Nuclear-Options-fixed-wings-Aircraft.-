using System;
using System.Collections.Generic;
using UnityEngine;
using NuclearOption.Jobs;

namespace Aryx_F22E_StrikeRaptor.FCS
{
    internal static class Acc
    {
        public static ref int AP_airfoil(AeroPart p) => ref p.airfoil;
        public static ref Vector3 AP_centerOfLift(AeroPart p) => ref p.centerOfLift;
        public static ref Transform AP_liftNormal(AeroPart p) => ref p.liftNormal;
        public static ref float AP_wingArea(AeroPart p) => ref p.wingArea;
        public static ref float AP_wingEff(AeroPart p) => ref p.wingEffectiveness;
        public static ref float AP_dragArea(AeroPart p) => ref p.dragArea;
        public static ref List<ControlSurface> AC_surfaces(Aircraft a) => ref a.controlSurfaces;
        public static ref Vector3 AC_wind(Aircraft a) => ref a.windVelocity;
        public static ref Aircraft CS_aircraft(ControlSurface c) => ref c.aircraft;
        public static ref UnitPart CS_attached(ControlSurface c) => ref c.attachedSurface;
        public static ref float CS_pitchRange(ControlSurface c) => ref c.pitchRange;
        public static ref float CS_rollRange(ControlSurface c) => ref c.rollRange;
        public static ref float CS_yawRange(ControlSurface c) => ref c.yawRange;
        public static ref float CS_brakeRange(ControlSurface c) => ref c.brakeRange;
        public static ref float CS_maxSplit(ControlSurface c) => ref c.maxSplit;
        public static ref float CS_splitAmount(ControlSurface c) => ref c.splitAmount;
        public static ref float CS_servo(ControlSurface c) => ref c.servoSpeed;
        public static ref float LG_steerLock(LandingGear g) => ref g.steeringLock;
        // 2.4.17 bench: ground-steering fields (never reached in the air)
        static float _f; static UnityEngine.RaycastHit _h;
        public static ref float LG_steerAngle(LandingGear g) => ref g.steeringAngle;
        public static ref float LG_response(LandingGear g) => ref g.response;
        public static ref float LG_wheelSpeed(LandingGear g) => ref g.wheelSpeed;
        public static ref float LG_compForce(LandingGear g) => ref g.compressionForce;
        public static ref float LG_dampForce(LandingGear g) => ref g.dampingForce;
        public static ref float LG_friction(LandingGear g) => ref g.frictionCoef;
        public static ref UnityEngine.RaycastHit LG_hit(LandingGear g) => ref g.hit;
        public static ref bool LG_braked(LandingGear g) => ref g.braked;
        public static ref float LG_dbf(LandingGear g) => ref g.differentialBrakeFactor;
        public static ref float LG_groundSpeed(LandingGear g) => ref g.groundSpeed;
        public static ref float LG_brakeStrength(LandingGear g) => ref g.brakeStrength;
        public static ref bool LG_steering(LandingGear g) => ref g.steering;
        public static ref ControlInputs LG_inputs(LandingGear g) => ref g.controlInputs;

        public static ref bool CS_locked(ControlSurface c) => ref c.locked;
        public static ref GameObject CS_visible(ControlSurface c) => ref c.visibleMesh;
        public static ref Transform JN_thrustT(JetNozzle n) => ref n.thrustTransform;
        public static ref float JN_totalThrust(JetNozzle n) => ref n.totalThrust;
        public static ref Aircraft TF_aircraft(Turbofan t) => ref t.aircraft;
        public static ref Vector3 TF_nozzleAngles(Turbofan t) => ref t.nozzleAngles;
        public static ref JetNozzle[] TF_nozzles(Turbofan t) => ref t.nozzles;
        public static ref bool TF_operable(Turbofan t) => ref t.operable;
        public static ref Vector3 TF_vec(Turbofan t) => ref t.thrustVectoring;
        public static ref Vector3 TF_vecGain(Turbofan t) => ref t.thrustVectoringGain;
        public static ref float TF_vecMaxSpeed(Turbofan t) => ref t.thrustVectoringMaxAirspeed;
        public static ref Transform[] TF_vecT(Turbofan t) => ref t.vectoringTransforms;
        public static ref float TF_staticThrust(Turbofan t) => ref t.staticThrust;
        public static ref float TF_condition(Turbofan t) => ref t.condition;
        public static ref float TF_minRPM(Turbofan t) => ref t.minRPM;
        public static ref float TF_maxRPM(Turbofan t) => ref t.maxRPM;
        public static ref float TF_rpm(Turbofan t) => ref t.rpm;
        public static ref float TF_dynFactor(Turbofan t) => ref t.dynFactor;
        public static ref float TF_parasitic(Turbofan t) => ref t.parasitic;
        public static Vector2 TF_remap(Turbofan t) => new Vector2(t.remapX, t.remapY);
        public static float JN_abMax(JetNozzle n) => n.abThrust;
        public static float JN_abStart(JetNozzle n) => n.abStart;
        public static float JN_abEnd(JetNozzle n) => 1f;
        public static ref AeroPart HL_part(HighLiftDevice h) => ref h.aeroPart;
        public static ref float HL_speedDep(HighLiftDevice h) => ref h.speedDeployed;
        public static ref float HL_speedRet(HighLiftDevice h) => ref h.speedRetracted;
        public static ref float HL_areaDep(HighLiftDevice h) => ref h.partAreaDeployed;
        public static ref float HL_areaRet(HighLiftDevice h) => ref h.partAreaRetracted;
        public static ref float HL_position(HighLiftDevice h) => ref h.position;
        public static ref float HL_alphaMin(HighLiftDevice h) => ref h.alphaMin;
        public static ref float HL_alphaFactor(HighLiftDevice h) => ref h.alphaFactor;
        public static List<Component> Collect(Aircraft a, Type t)
        {
            var r = new List<Component>();
            foreach (var c in a.all) if (t.IsInstanceOfType(c)) r.Add(c);
            return r;
        }
        public static List<T> Collect<T>(Aircraft a) where T : Component
        {
            var r = new List<T>();
            foreach (var c in a.all) if (c is T t) r.Add(t);
            return r;
        }
        public static bool JobCreated(ControlSurface cs) => true;
        public static ref ControlSurfaceFields Job(ControlSurface cs) => ref cs.job;
    }

    internal sealed class Telemetry
    {
        // 3.0.4 bench: GAMECSV=<path> writes the game's CSV (header + rows, the game's own formatting) for the identity check
        static System.IO.StreamWriter w;
        public Telemetry(FcsController c) { var p = System.Environment.GetEnvironmentVariable("GAMECSV"); if (!string.IsNullOrEmpty(p) && w == null) { w = new System.IO.StreamWriter(p); c.WriteHeader(w); System.AppDomain.CurrentDomain.ProcessExit += (s, e) => { w?.Flush(); }; } }
        public void Enqueue(TelemetryRow r) { if (w != null) r.WriteTo(w); }
        public void Close() { w?.Flush(); }
    }
}
