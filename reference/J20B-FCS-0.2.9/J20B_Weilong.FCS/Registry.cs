using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace J20B_Weilong.FCS
{
	internal static class Registry
	{
		private static readonly Dictionary<int, bool> isF22 = new Dictionary<int, bool>();

		private static readonly HashSet<int> setupDone = new HashSet<int>();

		private static readonly Dictionary<int, FcsController> controllers = new Dictionary<int, FcsController>();

		private static readonly Dictionary<int, SurfaceEffector> surfaceMap = new Dictionary<int, SurfaceEffector>();

		private static readonly Dictionary<int, FcsController> surfaceOwner = new Dictionary<int, FcsController>();

		private static readonly Dictionary<int, FcsController> anySurfaceOwner = new Dictionary<int, FcsController>();

		private static readonly Dictionary<ControlInputs, FcsController> byInputs = new Dictionary<ControlInputs, FcsController>();

		private static readonly HashSet<int> failed = new HashSet<int>();

		private static readonly Dictionary<int, int> attachRetry = new Dictionary<int, int>();

		private static readonly HashSet<int> tvcFixed = new HashSet<int>();

		private static readonly HashSet<int> servoScaled = new HashSet<int>();

		private static readonly HashSet<int> areaScaled = new HashSet<int>();

		private static readonly Dictionary<int, float> setupRetry = new Dictionary<int, float>();

		private static readonly HashSet<int> dragScaled = new HashSet<int>();

		private static readonly HashSet<int> cgShifted = new HashSet<int>();

		private static readonly Dictionary<int, FcsController> deviceOwner = new Dictionary<int, FcsController>();

		private static readonly FieldInfo fAfterburners = AccessTools.Field(typeof(JetNozzle), "afterburners");

		public static FcsController Find(Aircraft a)
		{
			if (!((UnityEngine.Object)(object)a != null) || !controllers.TryGetValue(((UnityEngine.Object)(object)a).GetInstanceID(), out var value))
			{
				return null;
			}
			return value;
		}

		public static FcsController FindAnySurface(ControlSurface cs)
		{
			if (!(cs != null) || !anySurfaceOwner.TryGetValue(cs.GetInstanceID(), out var value))
			{
				return null;
			}
			return value;
		}

		public static bool IsF22(Aircraft a)
		{
			if ((UnityEngine.Object)(object)a == null)
			{
				return false;
			}
			int instanceID = ((UnityEngine.Object)(object)a).GetInstanceID();
			if (isF22.TryGetValue(instanceID, out var value))
			{
				return value;
			}
			// J-20B 0.1.0: the J-20B Weilong mod's prefab is J20NativeMinimum (AircraftDefinition jsonKey J20_NativeMinimum, unitName
			// "J-20B Weilong"); its surfaces are the mod's own hinges (J20Bridge), so without the mod's aero system there is nothing to fly
			value = J20Bridge.Present && ((Component)(object)a).gameObject.name.StartsWith("J20NativeMinimum", StringComparison.Ordinal);
			if (!value)
			{
				try
				{
					AircraftParameters aircraftParameters = a.GetAircraftParameters();
					value = J20Bridge.Present && aircraftParameters != null && aircraftParameters.aircraftName != null && aircraftParameters.aircraftName.Contains("J-20");
				}
				catch
				{
					value = false;
				}
			}
			isF22[instanceID] = value;
			return value;
		}

		private static void ScaleAfterburners(JetNozzle n, float k)
		{
			if (n == null || fAfterburners == null || !(fAfterburners.GetValue(n) is Array array))
			{
				return;
			}
			foreach (object item in array)
			{
				if (item != null)
				{
					Traverse traverse = Traverse.Create(item).Field("thrust");
					traverse.SetValue(traverse.GetValue<float>() * k);
				}
			}
		}

		public static bool TryDevice(Component d, out FcsController c)
		{
			return deviceOwner.TryGetValue(d.GetInstanceID(), out c);
		}

		public static void EnsureSetup(Aircraft a)
		{
			if ((UnityEngine.Object)(object)a == null)
			{
				return;
			}
			Countermeasures.Ensure(a);
			int instanceID = ((UnityEngine.Object)(object)a).GetInstanceID();
			if (setupDone.Contains(instanceID))
			{
				return;
			}
			// 0.4.0 multiplayer: the airframe and engine changes are part of the FCS package; a client the host does not
			// allow keeps the stock jet (checked again every 0.5 s in case the policy arrives later)
			if (!MpPolicy.FcsAllowed)
			{
				return;
			}
			float time = Time.time;
			if (setupRetry.TryGetValue(instanceID, out var value) && time < value)
			{
				return;
			}
			setupRetry[instanceID] = time + 0.5f;
			try
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				// FS-12 Revoker: stock airframe. Only the TVC pitch range (optional) and the generic thrust/drag profile scaling
				// are applied; none of the F-22E airframe changes (nozzle rig, LERX, CG shift, effectiveness, stall stagger,
				// downwash). The stock pitch + yaw vectoring is kept: the FCS drives both axes.
				foreach (Turbojet item in Acc.Collect<Turbojet>(a))
				{
					num++;
					if (!tvcFixed.Add(item.GetInstanceID()))
					{
						continue;
					}
					Vector3 v = Acc.TF_vec(item);
					if (Plugin.TvcRange > 0f)
					{
						v.x = Plugin.TvcRange;
					}
					if (!Plugin.YawTvc)
					{
						v.y = 0f;
					}
					Acc.TF_vec(item) = v;
					// Ifrit 0.4.1: throttle response (every variant): the engine's spool rate and the afterburner's light-up speed
					if (Mathf.Abs(Plugin.ThrottleResponseScale - 1f) > 0.0001f)
					{
						Acc.TF_spoolRate(item) *= Plugin.ThrottleResponseScale;
						JetNozzle[] arrS = Acc.TF_nozzles(item);
						if (arrS != null)
						{
							for (int si = 0; si < arrS.Length; si++)
							{
								Acc.JN_scaleAbSmoothing(arrS[si], Plugin.ThrottleResponseScale);
							}
						}
					}
					if (Mathf.Abs(Plugin.ThrustScale - 1f) > 0.0001f)
					{
						Acc.TF_staticThrust(item) *= Plugin.ThrustScale;
						JetNozzle[] array = Acc.TF_nozzles(item);
						if (array != null)
						{
							for (int i = 0; i < array.Length; i++)
							{
								ScaleAfterburners(array[i], Plugin.ThrustScale);
							}
						}
					}
				}
				foreach (UnitPart item2 in a.partLookup)
				{
					if (item2 is AeroPart aeroPart && Mathf.Abs(Plugin.DragScale - 1f) > 0.0001f && dragScaled.Add(aeroPart.GetInstanceID()))
					{
						Acc.AP_dragArea(aeroPart) *= Plugin.DragScale;
						num4++;
					}
				}
				foreach (ControlSurface item5 in Acc.Collect<ControlSurface>(a))
				{
					num2++;
					// Ifrit 0.3.0: SurfaceServoSpeed sets every control surface's servo to one speed (stock: stabs 60, ailerons 45,
					// decelerons 70 deg/s); 0 = the old ActuatorSpeedScale multiplier on the stock speeds
					if (Plugin.SurfaceServoSpeed > 0f)
					{
						Acc.CS_servo(item5) = Plugin.SurfaceServoSpeed;
					}
					else if (Mathf.Abs(Plugin.ActuatorScale - 1f) > 0.0001f && servoScaled.Add(item5.GetInstanceID()))
					{
						Acc.CS_servo(item5) *= Plugin.ActuatorScale;
					}
					// Ifrit 0.3.0: deceleron split limit (game: the job copies maxSplit once when it is created; see Hooks)
					if (Acc.CS_maxSplit(item5) > 0f && Plugin.SplitRange > 0f)
					{
						Acc.CS_maxSplit(item5) = Plugin.SplitRange;
					}
				}
				// J-20B 0.1.0: no stock ControlSurfaces; the mod's 10 hinge surfaces (once its aero system has stepped this jet)
				num2 = J20Bridge.Surfaces(a).Count;
				bool flag = num2 >= 8 && num >= 1;
				if (flag)
				{
					J20Airframe(a);
				}
				if (flag)
				{
					setupDone.Add(instanceID);
					setupRetry.Remove(instanceID);
					try
					{
						DragRelief.Attach(a);
					}
					catch (Exception exD)
					{
						Plugin.Log.LogWarning("J-20B FCS drag relief: " + exD.Message);
					}
					try
					{
						int ns = StallStaggerJobs.Register(a);
						if (ns > 0)
						{
							Plugin.Log.LogInfo($"J-20B FCS staggered elevon stall on {((UnityEngine.Object)(object)a).name}: {ns} parts get their own curves (inboard {Plugin.InnerFlaperonStallAoA:F0}, outboard {Plugin.OuterFlaperonStallAoA:F0} deg, knee-preserving).");
						}
					}
					catch (Exception exS)
					{
						Plugin.Log.LogWarning("J-20B FCS staggered elevon stall: " + exS.Message);
					}
					try
					{
						int ncb = CamberForce.Attach(a);
						Plugin.Log.LogInfo($"J-20B FCS camber coupling: {ncb} surfaces paired with their wing section (flap K {Plugin.FlapCamberK:F2}, aileron K {Plugin.AileronCamberK:F2}).");
					}
					catch (Exception exC)
					{
						Plugin.Log.LogWarning("J-20B FCS camber coupling: " + exC.Message);
					}
					try
					{
						int nvh = VisualHinge.Attach(a);
						if (nvh >= 0)
						{
							Plugin.Log.LogInfo($"J-20B FCS: {nvh} trailing-edge surfaces drawn about their modelled hinge lines (render only).");
						}
					}
					catch (Exception exV)
					{
						Plugin.Log.LogWarning("J-20B FCS hinge visuals: " + exV.Message);
					}
				}
				Plugin.Log.LogInfo(string.Format("J-20B FCS airframe setup on {0}: {1} engine(s) (pitch + yaw TVC {8}, thrust x{2}), {3} surfaces (servo x{4}), {5} parts drag x{6}{7}.", ((UnityEngine.Object)(object)a).name, num, Plugin.ThrustScale, num2, Plugin.ActuatorScale, num4, Plugin.DragScale, flag ? "" : " - incomplete, will retry", Plugin.YawTvc ? "kept" : "pitch only"));
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("J-20B FCS airframe setup failed: " + ex);
			}
		}

		private static float nextPrune;

		private static readonly Dictionary<int, int> hookState = new Dictionary<int, int>();

		public static void NoteHook(Aircraft a, bool ai)
		{
			try
			{
				int num = (a.LocalSim ? (ai ? (Plugin.AIFlightControl ? 1 : 2) : 0) : 3);
				int instanceID = ((UnityEngine.Object)(object)a).GetInstanceID();
				if (hookState.TryGetValue(instanceID, out var value) && value == num)
				{
					return;
				}
				hookState[instanceID] = num;
				string text = num switch
				{
					0 => "human pilot on this machine -> full FCS (AoA/G command)", 
					1 => "AI pilot on this machine -> FCS with AI pitch-rate command", 
					2 => "AI pilot on this machine -> stock fly-by-wire (AIFlightControl = false)", 
					_ => "simulated on another machine -> left to that machine", 
				};
				Plugin.Log.LogInfo($"J-20B FCS: jet #{instanceID} reached the flight-control hook: {text}.");
			}
			catch
			{
			}
		}

		private static readonly FieldInfo fFbw = AccessTools.Field(typeof(ControlsFilter), "flyByWire");

		private static readonly FieldInfo fFbwG = ((fFbw != null) ? AccessTools.Field(fFbw.FieldType, "gLimitPositive") : null);

		private static readonly FieldInfo fFbwCorner = ((fFbw != null) ? AccessTools.Field(fFbw.FieldType, "cornerSpeed") : null);

		public static void ReadStockRateGain(ControlsFilter f, FcsController c)
		{
			try
			{
				object obj = ((f != null && fFbw != null) ? fFbw.GetValue(f) : null);
				if (obj != null)
				{
					if (fFbwG != null)
					{
						c.aiGLimit = Mathf.Clamp((float)fFbwG.GetValue(obj), 3f, 15f);
					}
					if (fFbwCorner != null)
					{
						c.aiCornerSpeed = Mathf.Clamp((float)fFbwCorner.GetValue(obj), 50f, 400f);
					}
				}
				Plugin.Log.LogInfo($"J-20B FCS flying an AI Ifrit: pitch-rate command, stock stick gain ({c.aiGLimit:0.#} g, corner {c.aiCornerSpeed:0} m/s), FCS AoA/G limits.");
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("J-20B FCS: could not read the stock fly-by-wire gain for an AI jet, using 9.5 g / 140 m/s: " + ex.Message);
			}
		}

		private static void Prune()
		{
			float time = Time.time;
			if (time < nextPrune && time >= nextPrune - 10f)
			{
				return;
			}
			nextPrune = time + 5f;
			List<int> list = null;
			foreach (KeyValuePair<int, FcsController> controller in controllers)
			{
				if ((UnityEngine.Object)(object)controller.Value.ac == null)
				{
					(list ?? (list = new List<int>())).Add(controller.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (int item in list)
			{
				FcsController c = controllers[item];
				controllers.Remove(item);
				try
				{
					c.Dispose();
				}
				catch
				{
				}
				foreach (KeyValuePair<int, FcsController> item3 in new List<KeyValuePair<int, FcsController>>(surfaceOwner))
				{
					if (item3.Value == c)
					{
						surfaceOwner.Remove(item3.Key);
						surfaceMap.Remove(item3.Key);
					}
				}
				RemoveFrom(anySurfaceOwner, (FcsController o) => o == c);
				RemoveFrom(deviceOwner, (FcsController o) => o == c);
				List<ControlInputs> list2 = new List<ControlInputs>();
				foreach (KeyValuePair<ControlInputs, FcsController> byInput in byInputs)
				{
					if (byInput.Value == c)
					{
						list2.Add(byInput.Key);
					}
				}
				foreach (ControlInputs item2 in list2)
				{
					byInputs.Remove(item2);
				}
			}
		}

		private static void RemoveFrom<T>(Dictionary<int, T> d, Func<T, bool> match)
		{
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, T> item in d)
			{
				if (match(item.Value))
				{
					list.Add(item.Key);
				}
			}
			foreach (int item2 in list)
			{
				d.Remove(item2);
			}
		}

		public static FcsController GetOrCreate(Aircraft a)
		{
			Prune();
			int instanceID = ((UnityEngine.Object)(object)a).GetInstanceID();
			if (controllers.TryGetValue(instanceID, out var value))
			{
				return value;
			}
			if (failed.Contains(instanceID))
			{
				return null;
			}
			try
			{
				EnsureSetup(a);
				value = new FcsController(a);
				controllers[instanceID] = value;
				foreach (SurfaceEffector surface in value.surfaces)
				{
					if (surface.cs == null)
					{
						continue;
					}
					surfaceMap[surface.cs.GetInstanceID()] = surface;
					surfaceOwner[surface.cs.GetInstanceID()] = value;
				}
				List<ControlSurface> list = Acc.Collect<ControlSurface>(a);
				List<string> list2 = new List<string>();
				foreach (ControlSurface item in list)
				{
					if (!(item == null))
					{
						anySurfaceOwner[item.GetInstanceID()] = value;
						float num = Acc.CS_maxSplit(item);
						float num2 = Acc.CS_brakeRange(item);
						bool flag = surfaceMap.ContainsKey(item.GetInstanceID());
						if (num > 0f || Mathf.Abs(num2) > 0.01f)
						{
							list2.Add(string.Format("{0}{1}: maxSplit {2:F1}, brakeRange {3:F1}", item.gameObject.name.Replace("Aryx_KingRaptor_", ""), flag ? "" : " [not an FCS effector]", num, num2));
						}
					}
				}
				Plugin.Log.LogInfo((list2.Count > 0) ? string.Format("J-20B FCS airbrake inventory ({0} control surfaces): {1} - the FCS drives these instead of the stock brake/idle-throttle rules.", list.Count, string.Join("; ", list2.ToArray())) : $"J-20B FCS airbrake inventory ({list.Count} control surfaces): none of them split or respond to the brake input, so whatever deploys is not a ControlSurface.");
				foreach (int ownedId in value.flaps.ownedIds)
				{
					deviceOwner[ownedId] = value;
				}
				if (value.inputs != null)
				{
					byInputs[value.inputs] = value;
				}
				return value;
			}
			catch (InvalidOperationException ex)
			{
				if (!attachRetry.TryGetValue(instanceID, out var value2))
				{
					value2 = 0;
				}
				value2 = (attachRetry[instanceID] = value2 + 1);
				if (value2 == 1 || value2 % 250 == 0)
				{
					Plugin.Log.LogWarning("J-20B FCS waiting to attach (attempt " + value2 + "): " + ex.Message);
				}
				if (value2 > 1500)
				{
					failed.Add(instanceID);
					Plugin.Log.LogError("J-20B FCS gave up attaching; stock flight controls stay active: " + ex.Message);
				}
				return null;
			}
			catch (Exception ex2)
			{
				failed.Add(instanceID);
				Plugin.Log.LogError("J-20B FCS could not attach; stock flight controls stay active for this aircraft: " + ex2);
				return null;
			}
		}

		public static void Fail(FcsController c, Exception e)
		{
			Plugin.Log.LogError("J-20B FCS fault, reverting this aircraft to stock flight controls: " + e);
			c.Failed = true;
			c.Engaged = false;
			if ((UnityEngine.Object)(object)c.ac != null)
			{
				failed.Add(((UnityEngine.Object)(object)c.ac).GetInstanceID());
			}
			Remove(c);
		}

		public static void Disengage(Aircraft a)
		{
			if (!((UnityEngine.Object)(object)a == null) && controllers.TryGetValue(((UnityEngine.Object)(object)a).GetInstanceID(), out var value))
			{
				value.Engaged = false;
				value.ThrustMatchClear();
			}
		}

		public static void Remove(FcsController c)
		{
			c.ThrustMatchClear();
			c.Dispose();
			if ((UnityEngine.Object)(object)c.ac != null)
			{
				controllers.Remove(((UnityEngine.Object)(object)c.ac).GetInstanceID());
			}
			if (c.inputs != null)
			{
				byInputs.Remove(c.inputs);
			}
			foreach (int ownedId in c.flaps.ownedIds)
			{
				deviceOwner.Remove(ownedId);
			}
		}

		public static bool TrySurface(ControlSurface cs, out SurfaceEffector s, out FcsController c)
		{
			int instanceID = cs.GetInstanceID();
			s = null;
			c = null;
			if (surfaceMap.TryGetValue(instanceID, out s))
			{
				return surfaceOwner.TryGetValue(instanceID, out c);
			}
			return false;
		}

		public static void ForgetSurface(ControlSurface cs)
		{
			int instanceID = cs.GetInstanceID();
			surfaceMap.Remove(instanceID);
			surfaceOwner.Remove(instanceID);
		}

		public static bool TryInputs(ControlInputs ci, out FcsController c)
		{
			return byInputs.TryGetValue(ci, out c);
		}

		public static bool SetupDone(Aircraft a)
		{
			return (UnityEngine.Object)(object)a != null && setupDone.Contains(((UnityEngine.Object)(object)a).GetInstanceID());
		}

		private static readonly Dictionary<int, List<AeroPart>> wing1Parts = new Dictionary<int, List<AeroPart>>();

		public static List<AeroPart> J20Wing1(Aircraft a)
		{
			int id = ((UnityEngine.Object)(object)a).GetInstanceID();
			if (!wing1Parts.TryGetValue(id, out var l))
			{
				l = new List<AeroPart>();
				foreach (AeroPart p in J20Bridge.HighLiftOwners(a))
				{
					if (p != null && p.gameObject.name.StartsWith("wing1_", StringComparison.Ordinal))
					{
						l.Add(p);
					}
				}
				wing1Parts[id] = l;
			}
			return l;
		}

		// J-20B 0.1.2: lift-area multiplier per J-20B part (forebody / chines, wing root, canards, trailing-edge elevons); wing1 is scaled
		// on the mod's high-lift area every step instead (J20Bridge)
		public static float J20AreaScale(string n)
		{
			if (string.IsNullOrEmpty(n))
			{
				return 1f;
			}
			if (IsForebody(n))
			{
				return Plugin.ForebodyLiftScale;
			}
			if (n.StartsWith("wingRoot_", StringComparison.Ordinal))
			{
				return Plugin.WingRootLiftScale;
			}
			if (n.StartsWith("canard_", StringComparison.Ordinal))
			{
				return Plugin.CanardLiftScale;
			}
			if (n.StartsWith("flap_", StringComparison.Ordinal) || n.StartsWith("aileron_", StringComparison.Ordinal))
			{
				return Plugin.ElevonLiftScale;
			}
			// J-20B 0.1.8 (AftBodyLiftScale): the aft fuselage / tail-cone lifting sections behind the CG
			if (n.StartsWith("fuselage_", StringComparison.Ordinal) || n == "tail")
			{
				return Plugin.AftBodyLiftScale;
			}
			return 1f;
		}

		// J-20B 0.1.0: forebody / chine sections (the root fuselage part, the cockpit) - the StrakeVortexLift parts
		public static bool IsForebody(string name)
		{
			return !string.IsNullOrEmpty(name) && (name.StartsWith("J20NativeMinimum", StringComparison.Ordinal) || name == "cockpit");
		}

		// J-20B 0.1.0 airframe (Tom 24 Nov), once per jet when its setup completes:
		//  - lift area: forebody / chines x ForebodyLiftScale, wing carry-through (wingRoot_L/R) x WingRootLiftScale; wing1_L/R x
		//    Wing1LiftScale is applied by J20Bridge after every mod Step (the mod rewrites wing1's high-lift area each step);
		//  - centre of mass: the clean jet with full tanks put CgAheadOfMains ahead of the main-gear axles by moving structural mass
		//    between the cockpit and the rear fuselage (UnitPart.mass and its rigidbody, total unchanged); FuelCgHold (ref 1) then
		//    holds that CG as the fuel burns.
		private static void J20Airframe(Aircraft a)
		{
			int id = ((UnityEngine.Object)(object)a).GetInstanceID();
			List<string> done = new List<string>();
			foreach (UnitPart up in a.partLookup)
			{
				if (!(up is AeroPart ap) || !areaScaled.Add(ap.GetInstanceID()))
				{
					continue;
				}
				string n = ap.gameObject.name;
				float k = J20AreaScale(n);
				if (Mathf.Abs(k - 1f) > 0.0001f && Acc.AP_wingArea(ap) > 0f)
				{
					Acc.AP_wingArea(ap) *= k;
					done.Add($"{n} x{k:F2}");
				}
			}
			if (done.Count > 0)
			{
				Plugin.Log.LogInfo("J-20B FCS lift areas: " + string.Join(", ", done.ToArray()) + $"; wing1_L/R x{Plugin.Wing1LiftScale:F2} on the mod's high-lift area every step.");
			}
			if (cgShifted.Contains(id))
			{
				return;
			}
			// 0.2.9: on the J-20B mod 0.0.5, 0.0.4's centres of mass first, so the CG move below works on 0.0.4's mass layout
			MassLayout004.Restore(a);
			try
			{
				Transform root = ((Component)(object)a).transform;
				// main-gear axle station: the two aft legs
				float zMain = 0f;
				int nMain = 0;
				List<float> zs = new List<float>();
				foreach (LandingGear g in Acc.Collect<LandingGear>(a))
				{
					if (g == null)
					{
						continue;
					}
					Transform ax = Traverse.Create(g).Field("axle").GetValue<Transform>();
					zs.Add(Vector3.Dot(((ax != null) ? ax.position : g.transform.position) - root.position, root.forward));
				}
				zs.Sort();
				for (int i = 0; i < zs.Count - 1; i++)
				{
					zMain += zs[i];
					nMain++;
				}
				if (nMain < 1)
				{
					return;
				}
				zMain /= nMain;
				// clean jet with full tanks: structure (part masses less any store mass on them) + every tank's capacity at its part
				float tot = 0f;
				double mom = 0.0;
				UnitPart fore = null;
				UnitPart back = null;
				foreach (UnitPart up in a.partLookup)
				{
					if (up == null)
					{
						continue;
					}
					float m = up.mass - FuelBalance.StoreMassOn(up);
					float z = Vector3.Dot(((up.rb != null) ? up.rb.worldCenterOfMass : up.transform.position) - root.position, root.forward);
					tot += m;
					mom += m * z;
					string n = up.gameObject.name;
					if (n == "cockpit")
					{
						fore = up;
					}
					else if (n == "fuselage_R")
					{
						back = up;
					}
				}
				foreach (FuelTank t in Acc.Collect<FuelTank>(a))
				{
					if (t == null)
					{
						continue;
					}
					UnitPart tp = Traverse.Create(t).Field("part").GetValue<UnitPart>();
					float cap = Traverse.Create(t).Field("fuelCapacity").GetValue<float>();
					float fm = t.fuelMass;
					float add = Mathf.Max(0f, cap - fm);
					if (tp == null || add <= 0f)
					{
						continue;
					}
					float z = Vector3.Dot(((tp.rb != null) ? tp.rb.worldCenterOfMass : tp.transform.position) - root.position, root.forward);
					tot += add;
					mom += add * z;
				}
				if (fore == null || back == null || tot < 1000f)
				{
					Plugin.Log.LogWarning("J-20B FCS CG: cockpit / fuselage_R not found, CG left as the mod has it.");
					cgShifted.Add(id);
					return;
				}
				float zCg = (float)(mom / tot);
				float shiftAft = zCg - (zMain + Plugin.CgAheadOfMains);
				float zF = Vector3.Dot(((fore.rb != null) ? fore.rb.worldCenterOfMass : fore.transform.position) - root.position, root.forward);
				float zB = Vector3.Dot(((back.rb != null) ? back.rb.worldCenterOfMass : back.transform.position) - root.position, root.forward);
				float arm = zF - zB;
				if (arm < 1f)
				{
					cgShifted.Add(id);
					return;
				}
				float mv = shiftAft * tot / arm;
				UnitPart from = (mv >= 0f) ? fore : back;
				UnitPart to = (mv >= 0f) ? back : fore;
				mv = Mathf.Min(Mathf.Abs(mv), 0.6f * from.mass);
				from.mass -= mv;
				to.mass += mv;
				if (from.rb != null && from.rb != a.rb)
				{
					from.rb.mass = from.mass;
				}
				if (to.rb != null && to.rb != a.rb)
				{
					to.rb.mass = to.mass;
				}
				cgShifted.Add(id);
				Plugin.Log.LogInfo($"J-20B FCS CG: clean full-tank CG {zCg - zMain:F3} m ahead of the main axles; {mv:F0} kg moved {from.gameObject.name} -> {to.gameObject.name} ({arm:F2} m) = {mv * arm / tot * Mathf.Sign(shiftAft):+0.000;-0.000} m aft, now {Plugin.CgAheadOfMains:F2} m ahead (FuelCgHold {(Plugin.FuelCgHold ? "on" : "off")}).");
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("J-20B FCS CG: " + ex.Message);
				cgShifted.Add(id);
			}
		}
	}
}
