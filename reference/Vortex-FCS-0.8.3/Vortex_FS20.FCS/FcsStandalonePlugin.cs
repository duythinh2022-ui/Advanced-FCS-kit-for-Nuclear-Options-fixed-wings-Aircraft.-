using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace Vortex_FS20.FCS
{
	[BepInPlugin(Guid, PluginName, Version)]
	[BepInDependency(DevGuid, BepInDependency.DependencyFlags.SoftDependency)]
#if FAMILY_FCS
	// 0.7.3: load after the FS-20 Vortex FCS when it is there (its native core, the hand-over check)
	[BepInDependency("Vortex_FS20.FCS", BepInDependency.DependencyFlags.SoftDependency)]
#endif
	public class FcsStandalonePlugin : BaseUnityPlugin
	{
		public const string Version = FcsVersion.Value;

		// Vortex 0.7.1: the same source builds two plugins. The FS-20 Vortex FCS (every FS-20 Vortex) and, with FAMILY_FCS defined (and the
		// namespace Vortex_FS20.FamilyFCS, see build-family.sh), the FS-20 Vortex Family FCS (the family mod's A / C / T / NA only, its native
		// core as a separate file). Which of the two flies a family jet when both are loaded: FamilyCompat.Decide.
#if FAMILY_FCS
		public const string Guid = "Vortex_FS20.FCS.Family";

		public const string PluginName = "FS-20 Vortex Family FCS";

		public const string HarmonyId = "tom.vortex_fs20.familyfcs";

		public const string TelemetryPrefix = "VortexFamily_FCS_";
#else
		public const string Guid = "Vortex_FS20.FCS";

		public const string PluginName = "FS-20 Vortex FCS";

		public const string HarmonyId = "tom.vortex_fs20.fcs";

		public const string TelemetryPrefix = "Vortex_FCS_";
#endif

		// the dev/debug unlock: a separate, empty plugin DLL. When it is loaded (soft dependency, so before this one),
		// every setting other than the player ones is bound to its config file and shows in the ConfigurationManager
		// under it; without it those settings are held at their defaults in memory and never written anywhere.
		public const string DevGuid = Guid + ".Dev";

		private void Awake()
		{
			ConfigFile dev = null;
			if (Chainloader.PluginInfos.TryGetValue(DevGuid, out PluginInfo devInfo) && devInfo != null && devInfo.Instance != null)
			{
				dev = devInfo.Instance.Config;
			}
			bool devUnlocked = dev != null;
			bool devFresh = devUnlocked && !File.Exists(dev.ConfigFilePath);
			if (!devUnlocked)
			{
				dev = new ConfigFile(Path.Combine(Paths.CachePath, Guid + ".builtin-defaults.cfg"), saveOnInit: false);
				dev.SaveOnConfigSet = false;
			}
			Plugin.Init(base.Config, dev, devUnlocked, devFresh, base.Logger);
			harmony = new Harmony(HarmonyId);
			Plugin.ApplyPatches(harmony);
			// 1.1.0: the native core (FcsNative): its state in the log from the start (loaded only if it is going to be used)
			base.Logger.LogInfo("Vortex FCS " + Version + ": native allocator core " + FcsNative.Describe() + "; " + FcsNative.DescribeKernels() + ".");
			cfgFile = base.Config;
			devFile = devUnlocked ? dev : null;
			// 0.4.0 multiplayer: the host's policy follows its settings (a change in F1, on disk or by migration)
			base.Config.SettingChanged += delegate
			{
				MpPolicy.SettingsChanged();
			};
			base.Config.ConfigReloaded += delegate
			{
				MpPolicy.SettingsChanged();
			};
			if (devUnlocked)
			{
				dev.SettingChanged += delegate
				{
					MpPolicy.SettingsChanged();
				};
				dev.ConfigReloaded += delegate
				{
					MpPolicy.SettingsChanged();
				};
			}
			cfgStamp = Stamp(cfgFile);
			devStamp = Stamp(devFile);
		}

		// 2.0.5: BepInEx reads a config file only at start-up. Edits made in a text editor while the game runs were not
		// applied, and the next save from inside the game (a setting changed in F1, a new key, a migration) wrote the old
		// values back over them. Both files are now re-read whenever they change on disk; flight settings apply at once,
		// spawn-time ones (airframe, countermeasures, ammo) on the next spawn.
		private Harmony harmony;

		// Vortex 0.7.0: the FS-20 Vortex Family coordination is installed here, once: every plugin has been loaded by the time Start runs
		private void Start()
		{
			if (harmony != null)
			{
				FamilyCompat.Install(harmony);
			}
		}

		private ConfigFile cfgFile;

		private ConfigFile devFile;

		private System.DateTime cfgStamp;

		private System.DateTime devStamp;

		private float nextCheck;

		private static System.DateTime Stamp(ConfigFile f)
		{
			try
			{
				return (f != null && File.Exists(f.ConfigFilePath)) ? File.GetLastWriteTimeUtc(f.ConfigFilePath) : default(System.DateTime);
			}
			catch
			{
				return default(System.DateTime);
			}
		}

		// (Vortex 0.4.11)
		private object lastMission;

		private bool missionWarned;

		// Vortex 0.5.8: the pirouette key's press (tap / hold)
		private bool pirKeyDown;

		private bool pirKeyBefore;

		private float pirKeyDownAt;

		// Vortex 0.5.8: the mod's keys are read here instead of with KeyboardShortcut.IsDown / IsPressed. BepInEx's own test
		// (ModifierKeyTest) also requires that no other key on the keyboard is held (its _modifierBlockKeyCodes), so with
		// the rudder on the keyboard (Tom, 18 Nov: "the hot key works until i hold yaw then it doesnt work") the pirouette key
		// did nothing while the pedal key was held, and a hold of it ended as soon as a pedal key went down. Here only the
		// shortcut's own keys count: its main key, and its modifiers held.
		private static bool KeyMods(KeyboardShortcut k)
		{
			foreach (UnityEngine.KeyCode m in k.Modifiers)
			{
				if (!UnityEngine.Input.GetKey(m))
				{
					return false;
				}
			}
			return true;
		}

		private static bool KeyDown(KeyboardShortcut k)
		{
			return k.MainKey != UnityEngine.KeyCode.None && UnityEngine.Input.GetKeyDown(k.MainKey) && KeyMods(k);
		}

		private static bool KeyHeld(KeyboardShortcut k)
		{
			return k.MainKey != UnityEngine.KeyCode.None && UnityEngine.Input.GetKey(k.MainKey);
		}

		// Vortex 0.5.16: the caution box (CautionBox): the cautions' owners are asked once a frame, after the game's own HUD
		// has been updated, and the box is placed under the game's message line. The player's own jet only.
		private bool cautionWarned;

		private void LateUpdate()
		{
			try
			{
				bool low = false;
				bool lowM = false;
				Aircraft pa = SceneSingleton<CombatHUD>.i?.aircraft;
				if ((UnityEngine.Object)(object)pa != null && Registry.IsF22(pa))
				{
					FcsController c = Registry.Find(pa);
					StovlSystem s = c?.stovl;
					low = s != null && c.Engaged && !c.Failed && s.CautionLowTw;
					lowM = s != null && c.Engaged && !c.Failed && s.CautionLowMargin;
				}
				CautionBox.Set("lowtw", StovlSystem.CautionLowTwText, low);
				// Vortex 0.5.20: hover allowed with less than the manoeuvring margin
				CautionBox.Set("lowmargin", StovlSystem.CautionLowMarginText, lowM);
				CautionBox.Tick();
			}
			catch (System.Exception exC)
			{
				if (!cautionWarned)
				{
					cautionWarned = true;
					Logger.LogWarning("Vortex FCS: caution box: " + exC.Message);
				}
			}
		}

		private void Update()
		{
			MpPolicy.Tick();
			// Vortex 0.4.11: a new match (the game's current mission object changed: MissionManager.SetMission runs on every
			// mission load, single player or joining a server) puts the ship-relative switch back to its config default
			try
			{
				object mis = MissionManager.CurrentMission;
				if (!ReferenceEquals(mis, lastMission))
				{
					lastMission = mis;
					// Vortex 0.5.16: no caution carried into a new mission
					CautionBox.ClearAll();
					// Vortex 0.5.8: and the pirouette key back to the setting
					Plugin.PirouetteKeyOn = false;
					pirKeyDown = false;
					if (mis != null && Plugin.ResetShipRelative())
					{
						Logger.LogInfo("Vortex FCS: new mission, STOVL ship speed back to the default: " + (Plugin.StovlShipRelative ? "SHIP RELATIVE" : "GROUND"));
					}
				}
			}
			catch (System.Exception exM)
			{
				if (!missionWarned)
				{
					missionWarned = true;
					Logger.LogWarning("Vortex FCS: mission watch: " + exM.Message);
				}
			}
			// Vortex 0.7.3: with the FS-20 Vortex FCS and the Family FCS both loaded, each reads its own keys (and puts up their HUD messages)
			// only while it flies the local jet, so a key works from the file of whichever plugin flies the jet and never toggles both. Out of a
			// jet: the FS-20 Vortex FCS's keys. A plugin loaded alone reads its keys in its own jets and out of a jet.
			bool keysMine = true;
			try
			{
				Aircraft la = SceneSingleton<CombatHUD>.i?.aircraft;
				keysMine = ((UnityEngine.Object)(object)la != null) ? Registry.IsF22(la) : !(FamilyCompat.IsFamilyBuild && Chainloader.PluginInfos.ContainsKey("Vortex_FS20.FCS"));
			}
			catch (System.Exception)
			{
			}
			// Vortex 0.4.1: the ship-relative STOVL switch's key
			try
			{
				if (keysMine && Plugin.cShipRelKey != null && Plugin.cShipRelKey.Value.MainKey != UnityEngine.KeyCode.None && KeyDown(Plugin.cShipRelKey.Value))
				{
					Plugin.ToggleShipRelative();
					string msg = Plugin.StovlShipRelative ? "STOVL SPD: SHIP REL" : "STOVL SPD: GND";
					Logger.LogInfo("Vortex FCS: " + msg);
					Aircraft pa = SceneSingleton<CombatHUD>.i?.aircraft;
					if ((UnityEngine.Object)(object)pa != null)
					{
						StovlHooks.ReportText(pa, msg);
					}
				}
			}
			catch (System.Exception exK)
			{
				Logger.LogWarning("Vortex FCS: ship-relative key: " + exK.Message);
			}
			// Vortex 0.5.8: the pedal pirouette key: tap = latch, hold = only while held
			try
			{
				KeyboardShortcut pk = (Plugin.cPirouetteKey != null) ? Plugin.cPirouetteKey.Value : KeyboardShortcut.Empty;
				if (pk.MainKey != UnityEngine.KeyCode.None && (keysMine || pirKeyDown))
				{
					bool was = (Plugin.StovlPedalPirouette != Plugin.PirouetteKeyOn);
					if (KeyDown(pk))
					{
						pirKeyBefore = Plugin.PirouetteKeyOn;
						pirKeyDownAt = UnityEngine.Time.unscaledTime;
						pirKeyDown = true;
						Plugin.PirouetteKeyOn = !Plugin.PirouetteKeyOn;
					}
					else if (pirKeyDown && !KeyHeld(pk))
					{
						pirKeyDown = false;
						if (UnityEngine.Time.unscaledTime - pirKeyDownAt >= Plugin.StovlPirouetteKeyHold)
						{
							Plugin.PirouetteKeyOn = pirKeyBefore;
						}
					}
					if ((Plugin.StovlPedalPirouette != Plugin.PirouetteKeyOn) != was)
					{
						string msg = (Plugin.StovlPedalPirouette != Plugin.PirouetteKeyOn) ? "STOVL PEDAL: PIROUETTE" : "STOVL PEDAL: COORD";
						Logger.LogInfo("Vortex FCS: " + msg);
						Aircraft pa = SceneSingleton<CombatHUD>.i?.aircraft;
						if ((UnityEngine.Object)(object)pa != null)
						{
							StovlHooks.ReportText(pa, msg);
						}
					}
				}
			}
			catch (System.Exception exP)
			{
				Logger.LogWarning("Vortex FCS: pirouette key: " + exP.Message);
			}
			if (UnityEngine.Time.unscaledTime < nextCheck)
			{
				return;
			}
			nextCheck = UnityEngine.Time.unscaledTime + 1f;
			CheckReload(cfgFile, ref cfgStamp);
			CheckReload(devFile, ref devStamp);
		}

		private void CheckReload(ConfigFile f, ref System.DateTime stamp)
		{
			if (f == null)
			{
				return;
			}
			System.DateTime now = Stamp(f);
			if (now == stamp || now == default(System.DateTime))
			{
				return;
			}
			try
			{
				f.Reload();
				Logger.LogInfo("Vortex FCS: " + Path.GetFileName(f.ConfigFilePath) + " changed on disk, settings re-read.");
			}
			catch (System.Exception ex)
			{
				Logger.LogWarning("Vortex FCS: re-reading " + Path.GetFileName(f.ConfigFilePath) + " failed: " + ex.Message);
			}
			stamp = Stamp(f);
		}
	}
}
