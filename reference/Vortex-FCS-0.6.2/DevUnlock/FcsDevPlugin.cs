using BepInEx;

namespace Vortex_FS20.FCS.Dev
{
	// Dev/debug unlock for the FS-20 Vortex FCS. When it is loaded, the FCS binds every setting other than the player ones to
	// BepInEx/config/Vortex_FS20.FCS.Dev.cfg and shows them in the ConfigurationManager (F1), the STOVL keys included.
	[BepInPlugin("Vortex_FS20.FCS.Dev", "FS-20 Vortex FCS - Dev/Debug settings", "1.0.0")]
	public class FcsDevPlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			base.Logger.LogInfo("Vortex FCS dev/debug settings unlocked.");
		}
	}
}
