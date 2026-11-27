using BepInEx;

namespace J20B_Weilong.FCS.Dev
{
	// Dev/debug unlock for the J-20B Weilong FCS. When it is loaded, the FCS binds every setting other than the player ones to
	// BepInEx/config/J20B_Weilong.FCS.Dev.cfg and shows them in the ConfigurationManager (F1). Nothing else.
	[BepInPlugin("J20B_Weilong.FCS.Dev", "J-20B Weilong FCS - Dev/Debug settings", "1.0.0")]
	public class FcsDevPlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			base.Logger.LogInfo("J-20B FCS dev/debug settings unlocked.");
		}
	}
}
