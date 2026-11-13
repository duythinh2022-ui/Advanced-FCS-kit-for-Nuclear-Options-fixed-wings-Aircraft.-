using BepInEx;

namespace Ternion_FS3.FCS.Dev
{
	// Dev/debug unlock for the FS-3 Ternion FCS. When it is loaded, the FCS binds every setting other than the player ones to
	// BepInEx/config/Ternion_FS3.FCS.Dev.cfg and shows them in the ConfigurationManager (F1). It does nothing else.
	[BepInPlugin("Ternion_FS3.FCS.Dev", "FS-3 Ternion FCS - Dev/Debug settings", "0.2.0")]
	public class FcsDevPlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			base.Logger.LogInfo("Ternion FCS dev/debug settings unlocked.");
		}
	}
}
