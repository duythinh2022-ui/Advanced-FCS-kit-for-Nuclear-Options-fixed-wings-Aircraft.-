namespace Aryx_F22E_StrikeRaptor.FCS { internal static class Registry {
	public static bool IsF22(Aircraft a) => true; public static FcsController Current; public static FcsController Find(Aircraft a) => Current;
	public static bool SetupDone(Aircraft a) => true;
	public static float J20AreaScale(string n) {
		if (string.IsNullOrEmpty(n)) return 1f; if (IsForebody(n)) return Plugin.ForebodyLiftScale; if (n.StartsWith("fuselage_", System.StringComparison.Ordinal) || n == "tail") return Plugin.AftBodyLiftScale;
		if (n.StartsWith("wingRoot_")) return Plugin.WingRootLiftScale; if (n.StartsWith("canard_")) return Plugin.CanardLiftScale;
		if (n.StartsWith("flap_") || n.StartsWith("aileron_")) return Plugin.ElevonLiftScale; return 1f; }
	public static bool IsForebody(string name) => !string.IsNullOrEmpty(name) && (name.StartsWith("J20NativeMinimum", System.StringComparison.Ordinal) || name == "cockpit");
} }
