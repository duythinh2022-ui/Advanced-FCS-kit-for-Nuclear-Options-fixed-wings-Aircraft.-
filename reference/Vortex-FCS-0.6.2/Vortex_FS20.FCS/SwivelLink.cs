using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Vortex_FS20.FCS
{
	// Vortex 0.1.0: the game's SwivelDuctSystem (FS-20 lift system) read through reflection for the STOVL laws (Stovl.cs). Its
	// arrays hold private nested types (Bearing and Thruster are structs, LiftFan a class); they are read once, the live state
	// (swivel and door positions) every call.
	internal sealed class SwivelLink : IStovlHardware
	{
		private static readonly Type tSds = typeof(SwivelDuctSystem);

		private static readonly FieldInfo fSwivel = AccessTools.Field(tSds, "swivelPosition");

		private static readonly FieldInfo fDoors = AccessTools.Field(tSds, "thrustDoorPosition");

		private static readonly FieldInfo fSwivelSpeed = AccessTools.Field(tSds, "swivelSpeed");

		private static readonly FieldInfo fMaxSwivelAs = AccessTools.Field(tSds, "maxSwivelAirspeed");

		private static readonly FieldInfo fBearings = AccessTools.Field(tSds, "bearings");

		private static readonly FieldInfo fLiftFans = AccessTools.Field(tSds, "liftFans");

		private static readonly FieldInfo fThrusters = AccessTools.Field(tSds, "thrusters");

		private static readonly FieldInfo fAircraft = AccessTools.Field(tSds, "aircraft");

		internal static readonly FieldInfo fMode = AccessTools.Field(tSds, "swivelDuctMode");

		private static readonly FieldInfo fAltThrust = AccessTools.Field(typeof(Turbojet), "altitudeThrust");

		public readonly SwivelDuctSystem sds;

		private readonly Aircraft ac;

		private readonly Turbojet jet;

		private readonly Transform mainT;

		private readonly Transform fanT;

		private readonly float fanPitch;

		private readonly float parasitic;

		// Vortex 0.5.0: the vane box. The lift fan object and its parasiticLoss field (the vanes' drive factor goes on it), the
		// fan thrust transform's own rotation (the game's fixed thrust line, the vanes' rest) and the thrust line now (deg from
		// the fan's axis, + aft)
		private readonly object fan0Obj;

		private readonly FieldInfo fFanParasitic;

		private readonly Quaternion fanRestLocal;

		private float vaneNow;

		private bool vaneInit;

		private bool vaneMoved;

		private float driveApplied = 1f;

		private readonly Transform[] bT;

		private readonly Vector3[] bMin;

		private readonly Vector3[] bMax;

		private readonly Vector3[] bYaw;

		private readonly AnimationCurve[] bCurve;

		private readonly bool[] bUseCurve;

		private readonly Transform[] pT;

		private readonly float[] pMax;

		private readonly float[] pRoll;

		private readonly float[] pMinSw;

		private readonly float[] pMaxSp;

		private readonly AnimationCurve altCurve;

		private Vector3 shipVel;

		private float shipCheckT = -10f;

		private Ship shipRef;

		private float shipW;

		private bool shipBlending;

		private float shipWT = -10f;

		private bool shipOnWas;

		public static object ManualMode;

		public static object ForwardMode;

		public bool Valid { get; private set; }

		private static readonly System.Collections.Generic.HashSet<int> liftScaled = new System.Collections.Generic.HashSet<int>();

		private struct VaneBase
		{
			public float parasitic;

			public Quaternion rest;
		}

		private static readonly System.Collections.Generic.Dictionary<int, VaneBase> vaneBase = new System.Collections.Generic.Dictionary<int, VaneBase>();

		// Vortex 0.1.1: the thrust profile scales the whole lift system, not only the engine. The lift fan's balancing force
		// follows the engine's thrust by itself (the game sizes it from jet.GetThrust() to trim the main nozzle's pitch
		// moment); its pitch-control term (pitchFactor) and the roll posts' maxThrust are fixed numbers, scaled here once
		// per lift system. The fan's parasiticLoss (the power it takes from the engine) is left as it is.
		public static void ScaleLiftSystem(Aircraft a, float k)
		{
			if (a == null || Mathf.Abs(k - 1f) < 0.0001f)
			{
				return;
			}
			SwivelDuctSystem s = ((Component)(object)a).GetComponentInChildren<SwivelDuctSystem>(true);
			if (s == null || !liftScaled.Add(s.GetInstanceID()))
			{
				return;
			}
			try
			{
				int nf = 0;
				int np = 0;
				if (fLiftFans?.GetValue(s) is Array fans)
				{
					for (int i = 0; i < fans.Length; i++)
					{
						object f = fans.GetValue(i);
						FieldInfo fp = (f != null) ? AccessTools.Field(f.GetType(), "pitchFactor") : null;
						if (fp != null)
						{
							fp.SetValue(f, (float)fp.GetValue(f) * k);
							fans.SetValue(f, i);
							nf++;
						}
					}
				}
				if (fThrusters?.GetValue(s) is Array th)
				{
					for (int i = 0; i < th.Length; i++)
					{
						object t = th.GetValue(i);   // a struct: boxed, changed, written back
						FieldInfo fm = (t != null) ? AccessTools.Field(t.GetType(), "maxThrust") : null;
						if (fm != null)
						{
							fm.SetValue(t, (float)fm.GetValue(t) * k);
							th.SetValue(t, i);
							np++;
						}
					}
				}
				Plugin.Log.LogInfo($"Vortex FCS: lift system thrust x{k:0.###} ({nf} lift fan pitch term(s), {np} roll post(s)); the fan's trim force follows the engine.");
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Vortex FCS: lift system scaling failed: " + ex.Message);
			}
		}

		public static SwivelLink TryCreate(Aircraft a)
		{
			ScaleLiftSystem(a, Plugin.ThrustScale);
			SwivelDuctSystem s = (a != null) ? ((Component)(object)a).GetComponentInChildren<SwivelDuctSystem>(true) : null;
			if (s == null)
			{
				return null;
			}
			SwivelLink l = new SwivelLink(s, a);
			return l.Valid ? l : null;
		}

		private static T F<T>(object o, string name)
		{
			FieldInfo f = AccessTools.Field(o.GetType(), name);
			object v = (f != null) ? f.GetValue(o) : null;
			return (v is T t) ? t : default(T);
		}

		private SwivelLink(SwivelDuctSystem s, Aircraft a)
		{
			sds = s;
			ac = a;
			try
			{
				if (ManualMode == null && fMode != null)
				{
					ManualMode = Enum.ToObject(fMode.FieldType, 4);
					ForwardMode = Enum.ToObject(fMode.FieldType, 0);
				}
				Array fans = fLiftFans?.GetValue(s) as Array;
				object fan0 = (fans != null && fans.Length > 0) ? fans.GetValue(0) : null;
				if (fan0 != null)
				{
					fanT = F<Transform>(fan0, "thrustTransform");
					mainT = F<Transform>(fan0, "nozzleThrustTransform");
					jet = F<Turbojet>(fan0, "jet");
					fanPitch = F<float>(fan0, "pitchFactor");
					parasitic = F<float>(fan0, "parasiticLoss");
					fan0Obj = fan0;
					fFanParasitic = AccessTools.Field(fan0.GetType(), "parasiticLoss");
					fanRestLocal = (fanT != null) ? fanT.localRotation : Quaternion.identity;
					// (a link made again for the same lift system, e.g. the FCS re-attached with the vanes turned, takes the
					// values the first one read, not the turned ones)
					int id = s.GetInstanceID();
					if (vaneBase.TryGetValue(id, out VaneBase vb))
					{
						parasitic = vb.parasitic;
						fanRestLocal = vb.rest;
						if (fanT != null)
						{
							fanT.localRotation = vb.rest;
						}
						fFanParasitic?.SetValue(fan0, vb.parasitic);
					}
					else
					{
						vaneBase[id] = new VaneBase
						{
							parasitic = parasitic,
							rest = fanRestLocal
						};
					}
				}
				Array br = fBearings?.GetValue(s) as Array;
				int nb = (br != null) ? br.Length : 0;
				bT = new Transform[nb];
				bMin = new Vector3[nb];
				bMax = new Vector3[nb];
				bYaw = new Vector3[nb];
				bCurve = new AnimationCurve[nb];
				bUseCurve = new bool[nb];
				for (int i = 0; i < nb; i++)
				{
					object b = br.GetValue(i);
					bT[i] = F<Transform>(b, "transform");
					bMin[i] = F<Vector3>(b, "minAngles");
					bMax[i] = F<Vector3>(b, "maxAngles");
					bYaw[i] = F<Vector3>(b, "yawFactor");
					bCurve[i] = F<AnimationCurve>(b, "adjustmentCurve");
					bUseCurve[i] = F<bool>(b, "useCurve");
				}
				Array th = fThrusters?.GetValue(s) as Array;
				int np = (th != null) ? th.Length : 0;
				pT = new Transform[np];
				pMax = new float[np];
				pRoll = new float[np];
				pMinSw = new float[np];
				pMaxSp = new float[np];
				for (int i = 0; i < np; i++)
				{
					object t = th.GetValue(i);
					pT[i] = F<Transform>(t, "thrustTransform");
					pMax[i] = F<float>(t, "maxThrust");
					pRoll[i] = F<float>(t, "rollFactor");
					pMinSw[i] = F<float>(t, "minSwivel");
					pMaxSp[i] = F<float>(t, "maxSpeed");
				}
				altCurve = (jet != null && fAltThrust != null) ? (fAltThrust.GetValue(jet) as AnimationCurve) : null;
				Valid = jet != null && mainT != null && fanT != null && nb > 0 && bT[0] != null && fSwivel != null && fDoors != null && fMode != null && ManualMode != null;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Vortex FCS: lift system read failed: " + ex.Message);
				Valid = false;
			}
		}

		public float Doors => (float)fDoors.GetValue(sds);

		public float Swivel => (float)fSwivel.GetValue(sds);

		public float SwivelRate => (fSwivelSpeed != null) ? ((float)fSwivelSpeed.GetValue(sds)) : 0.25f;

		public float MaxSwivelAirspeed => (fMaxSwivelAs != null) ? ((float)fMaxSwivelAs.GetValue(sds)) : 139f;

		// Vortex 0.4.6 (Tom, 17 Nov 23:19 / 23:26): the STOVL laws hold ground speed relative to the nearest friendly ship
		// within StovlShipRelativeRange (800 m), as the game's auto-hover does (AutoHover.CheckNearbyShip: within 500 m, looked up
		// every 3 s, all at once). On by default, toggled with StovlShipRelativeKey. Nothing steps: the ship's speed is read every
		// tick, faded in over the outer quarter of the range, and the reference moves toward it (entering the range, leaving it,
		// a change of nearest ship, the switch) by at most its own size in 3 s; a fresh STOVL conversion takes it at once.
		// 0.1.x-0.4.0: vanilla's rule (500 m, stepped, a 3 s old copy); 0.4.1-0.4.5: the switch off by default, a change of ship
		// stepped.
		public Vector3 SurfaceVelocity
		{
			get
			{
				float t = Time.timeSinceLevelLoad;
				// first call after the laws were idle (a new STOVL conversion): look now and take the ship's speed as it is
				bool fresh = t - shipWT > 0.5f;
				bool on = Plugin.StovlShipRelative;
				float range = Plugin.StovlShipRelRange;
				if (!on)
				{
					shipRef = null;
				}
				else if (fresh || !shipOnWas || t - shipCheckT >= 3f)
				{
					shipCheckT = t;
					shipRef = null;
					try
					{
						FactionHQ hq = ac.NetworkHQ;
						if ((UnityEngine.Object)(object)hq != null && hq.TryGetNearestShip(ac.GlobalPosition(), out var ship, out var d2) && d2 < range * range && (UnityEngine.Object)(object)ship != null)
						{
							shipRef = ship;
						}
					}
					catch
					{
						shipRef = null;
					}
				}
				Vector3 vTgt = Vector3.zero;
				float wTgt = 0f;
				if (shipRef != null)
				{
					try
					{
						if ((UnityEngine.Object)(object)shipRef == null || shipRef.rb == null)
						{
							shipRef = null;
						}
						else
						{
							float dist = (shipRef.GlobalPosition() - ac.GlobalPosition()).magnitude;
							wTgt = 1f - Mathf.InverseLerp(0.75f * range, range, dist);
							Vector3 vS = shipRef.rb.velocity;
							vTgt = new Vector3(vS.x, 0f, vS.z) * wTgt;
						}
					}
					catch
					{
						shipRef = null;
					}
				}
				if (fresh)
				{
					shipVel = vTgt;
					shipW = wTgt;
				}
				else if (t != shipWT)
				{
					float dt = Mathf.Max(0f, t - shipWT);
					shipVel = Vector3.MoveTowards(shipVel, vTgt, Mathf.Max(1f, Mathf.Max(shipVel.magnitude, vTgt.magnitude)) * dt / 3f);
					shipW = Mathf.MoveTowards(shipW, wTgt, dt / 3f);
				}
				// (0.4.8, for the HUD icon: still on the way to the ship's speed, or inside the range's fade)
				shipBlending = wTgt < 0.999f || shipW < 0.999f || (shipVel - vTgt).sqrMagnitude > 0.01f;
				shipWT = t;
				shipOnWas = on;
				return shipVel;
			}
		}

		public float ShipFrameWeight => shipW;

		// Vortex 0.4.8 (HudShipMatch): the reference was read within the last half second (the STOVL laws are running), and
		// whether it is still being blended (the range's outer quarter, the 3 s after a switch, a change of nearest ship)
		public bool ShipFrameLive => Time.timeSinceLevelLoad - shipWT < 0.5f;

		public bool ShipFrameBlending => shipBlending;

		public Transform MainThrustT => mainT;

		public float NozzleThrust
		{
			get
			{
				JetNozzle[] nz = Acc.TF_nozzles(jet);
				float t = 0f;
				if (nz != null)
				{
					foreach (JetNozzle n in nz)
					{
						if (n != null)
						{
							t += Acc.JN_totalThrust(n);
						}
					}
				}
				return t;
			}
		}

		public float DryThrust => jet.GetThrust();

		public float RpmRatio => jet.GetRPMRatio();

		public float ThrustRatio => jet.GetThrustRatio();

		public float MaxThrust => Acc.TF_staticThrust(jet);

		public float ParasiticLoss => parasitic;

		public float AltFactor => (altCurve != null) ? altCurve.Evaluate(((Component)(object)jet).transform.position.y - Datum.LocalSeaY) : 1f;

		public float MinRpm => jet.GetMinRPM();

		public float MaxRpm => jet.GetMaxRPM();

		public float SpoolRate => Acc.TF_spoolRate(jet);

		public Vector2 Remap => Acc.TF_remap(jet);

		public float Condition => Acc.TF_condition(jet);

		public int Bearings => bT.Length;

		public Transform BearingT(int i)
		{
			return bT[i];
		}

		public Vector3 BearingMin(int i)
		{
			return bMin[i];
		}

		public Vector3 BearingMax(int i)
		{
			return bMax[i];
		}

		public Vector3 BearingYaw(int i)
		{
			return bYaw[i];
		}

		public float BearingCurve(int i, float s)
		{
			return (bUseCurve[i] && bCurve[i] != null) ? bCurve[i].Evaluate(s) : s;
		}

		public Transform FanThrustT => fanT;

		public float FanPitchFactor => fanPitch;

		public bool HasVanes => fanT != null;

		public Vector3 FanRestForward => ((fanT.parent != null) ? (fanT.parent.rotation * fanRestLocal) : fanRestLocal) * Vector3.forward;

		// the game's fixed thrust line, deg from the fan's axis (the jet's up axis; -10 on the FS-20)
		public float VaneRest
		{
			get
			{
				Transform root = ((Component)(object)ac).transform;
				Vector3 f = FanRestForward;
				return Mathf.Atan2(Vector3.Dot(f, root.forward), Vector3.Dot(f, root.up)) * 57.29578f;
			}
		}

		public float VaneNow
		{
			get
			{
				if (!vaneInit)
				{
					vaneInit = true;
					vaneNow = VaneRest;
				}
				return vaneNow;
			}
		}

		// Vortex 0.5.0: one physics step of the vane box (SwivelDuctSystem.FixedUpdate's prefix, before the game sizes and applies
		// the fan's force). live: the STOVL laws command the vanes this tick (cmd, deg); otherwise, and with the fan doors shut,
		// they go back to the game's fixed line at the same rate. The thrust transform is turned about the jet's lateral axis by
		// the difference to that line (exactly its own rotation again at rest), and the fan's parasitic loss carries the vane
		// cascade's drive factor (StovlSystem.VaneDrive; 1 at rest and forward of the fan's axis).
		public void VaneStep(bool live, float cmd, float dt)
		{
			if (fanT == null)
			{
				return;
			}
			float rest = VaneRest;
			if (!vaneInit)
			{
				vaneInit = true;
				vaneNow = rest;
			}
			bool on = live && Plugin.StovlVanes && Doors > 0f;
			float tgt = on ? Mathf.Clamp(cmd, Plugin.StovlVaneMin, Plugin.StovlVaneMax) : rest;
			vaneNow = Mathf.MoveTowards(vaneNow, tgt, Plugin.StovlVaneRate * dt);
			if (Mathf.Abs(vaneNow - rest) < 0.0001f)
			{
				vaneNow = rest;
				if (vaneMoved)
				{
					fanT.localRotation = fanRestLocal;
					vaneMoved = false;
				}
			}
			else
			{
				Transform root = ((Component)(object)ac).transform;
				Quaternion pr = (fanT.parent != null) ? fanT.parent.rotation : Quaternion.identity;
				fanT.rotation = Quaternion.AngleAxis(vaneNow - rest, root.right) * (pr * fanRestLocal);
				vaneMoved = true;
			}
			float drive = StovlSystem.VaneDrive(vaneNow);
			if (drive != driveApplied && fFanParasitic != null && fan0Obj != null)
			{
				fFanParasitic.SetValue(fan0Obj, parasitic * drive);
				driveApplied = drive;
			}
		}

		public int Posts => pT.Length;

		public Transform PostT(int i)
		{
			return pT[i];
		}

		public float PostMax(int i)
		{
			return pMax[i];
		}

		public float PostRollFactor(int i)
		{
			return pRoll[i];
		}

		public float PostMinSwivel(int i)
		{
			return pMinSw[i];
		}

		public float PostMaxSpeed(int i)
		{
			return pMaxSp[i];
		}

		public float AircraftSpeed => ac.speed;

		public Vector3 CenterOfMass => ac.GetCenterOfMass();

		public static Aircraft AircraftOf(SwivelDuctSystem s)
		{
			return (fAircraft != null) ? (fAircraft.GetValue(s) as Aircraft) : null;
		}
	}
}
