// bench stub: offline, local settings apply
namespace Aryx_F22E_StrikeRaptor.FCS
{
    public struct FcsHostPolicyMsg { public bool stock; public float thrust, drag, fuel, flare, gun, ewCap, ewRate, drAoAHigh, drTrans; }
    internal static class MpPolicy
    {
        public static bool FcsAllowed => true;
        public static bool VariantLocked => false;
        public static bool StockForced => false;
        public static FcsHostPolicyMsg Host => default(FcsHostPolicyMsg);
    }
}
