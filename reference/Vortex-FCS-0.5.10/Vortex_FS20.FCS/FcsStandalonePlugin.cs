using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace Vortex_FS20.FCS
{
	[BepInPlugin("Vortex_FS20.FCS", "FS-20 Vortex FCS", Version)]
	[BepInDependency(DevGuid, BepInDependency.DependencyFlags.SoftDependency)]
	public class FcsStandalonePlugin : BaseUnityPlugin
	{
		public const string Version = "0.5.10";

		// the dev/debug unlock: a separate, empty plugin DLL. When it is loaded (soft dependency, so before this one),
		// every setting other than the player ones is bound to its config file and shows in the ConfigurationManager
		// under it; without it those settings are held at their defaults in memory and never written anywhere.
		public const string DevGuid = "Vortex_FS20.FCS.Dev";

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
				dev = new ConfigFile(Path.Combine(Paths.CachePath, "Vortex_FS20.FCS.builtin-defaults.cfg"), saveOnInit: false);
				dev.SaveOnConfigSet = false;
			}
			Plugin.Init(base.Config, dev, devUnlocked, devFresh, base.Logger);
			Plugin.ApplyPatches(new Harmony("tom.vortex_fs20.fcs"));
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
			// Vortex 0.4.1: the ship-relative STOVL switch's key
			try
			{
				if (Plugin.cShipRelKey != null && Plugin.cShipRelKey.Value.MainKey != UnityEngine.KeyCode.None && KeyDown(Plugin.cShipRelKey.Value))
				{
					Plugin.ToggleShipRelative();
					string msg = Plugin.StovlShipRelative ? "STOVL SPEED: SHIP RELATIVE" : "STOVL SPEED: GROUND";
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
				if (pk.MainKey != UnityEngine.KeyCode.None)
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
						string msg = (Plugin.StovlPedalPirouette != Plugin.PirouetteKeyOn) ? "STOVL PEDAL: PIROUETTE" : "STOVL PEDAL: COORDINATED";
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
