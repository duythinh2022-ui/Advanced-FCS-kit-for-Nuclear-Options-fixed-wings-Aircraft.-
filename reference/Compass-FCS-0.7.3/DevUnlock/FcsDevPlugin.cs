using BepInEx;

namespace Compass_TA30.FCS.Dev
{
	// Dev/debug unlock for the T/A-30 Compass FCS. When it is loaded, the FCS binds every setting other than the player ones to
	// BepInEx/config/Compass_TA30.FCS.Dev.cfg and shows them in the ConfigurationManager (F1). It does nothing else.
	[BepInPlugin("Compass_TA30.FCS.Dev", "T/A-30 Compass FCS - Dev/Debug settings", "0.2.0")]
	public class FcsDevPlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			base.Logger.LogInfo("Compass FCS dev/debug settings unlocked.");
		}
	}
}
