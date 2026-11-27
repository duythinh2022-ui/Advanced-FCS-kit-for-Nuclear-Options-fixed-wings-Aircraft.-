namespace Aryx_F22E_StrikeRaptor.FCS {
internal sealed class MpHostVariant { public float drAoAHigh, drTrans, drag=1f, ewCap=1f, ewRate=1f, flare=1f, fuel=1f, gun=1f, thrust=1f; }
internal static class MpPolicy {
  public static bool FcsAllowed = true, VariantLocked = false, StockForced = false;
  public static MpHostVariant Host = new MpHostVariant();
}
}
