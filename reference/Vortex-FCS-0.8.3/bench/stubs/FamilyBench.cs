// bench stand-ins for the 0.7.0 FS-20 Vortex Family support (FamilyCompat / GeomGate). The bench jet is the stock B unless
// BENCH_VARIANT names a family variant proxy; BENCH_NONSTOCK lists the parts the proxy reworked (comma separated names).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aryx_F22E_StrikeRaptor.FCS
{
    internal static class FamilyCompat
    {
        internal enum Variant { B, A, C, T, NA, Other }
        internal const string FamilyGuid = "fs20vortexfamily";
        internal static bool FamilyPresent;
        static readonly string V = Environment.GetEnvironmentVariable("BENCH_VARIANT") ?? "B";
        internal static Variant VariantOf(Aircraft a) => Enum.TryParse<Variant>(V, out var v) ? v : Variant.B;
        internal static bool FcsFlying(Aircraft a) => true;
        internal static bool Responsible(Aircraft a) => true;
        // 0.7.1 per-jet envelope, as FamilyCompat.JetLimits (BENCH_JETLIMITS=0: off, the jet flies the Plugin limits)
        internal static float FamilyG = 11f, FamilyMpoG = 14.5f, NaAoAPos = 70f, NaAoANeg = 55f;
        internal static void JetLimits(Aircraft a, out float aoaPos, out float aoaNeg, out float g, out float mpoG, out float refMass)
        {
            aoaPos = aoaNeg = g = mpoG = refMass = 0f;
            if (Environment.GetEnvironmentVariable("BENCH_JETLIMITS") == "0") return;
            switch (VariantOf(a))
            {
                case Variant.A: refMass = 13500f; break;
                case Variant.C: refMass = 12000f; break;
                case Variant.T: refMass = 16000f; break;
                case Variant.NA: refMass = 16000f; aoaPos = NaAoAPos; aoaNeg = NaAoANeg; break;
                default: return;
            }
            g = FamilyG; mpoG = FamilyMpoG;
        }
        internal static bool Finless(Aircraft a)
        {
            if (a == null) return false;
            foreach (var cs in Acc.Collect<ControlSurface>(a)) if (cs != null && cs.gameObject.name.ToLowerInvariant().StartsWith("rudder")) return false;
            foreach (var ap in Acc.Collect<AeroPart>(a)) if (ap != null && ap.gameObject.name.ToLowerInvariant().StartsWith("vstab")) return false;
            return true;
        }
    }

    internal static class GeomGate
    {
        static readonly HashSet<string> nonStock = new HashSet<string>((Environment.GetEnvironmentVariable("BENCH_NONSTOCK") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        internal static void Prepare(Aircraft a) { }
        internal static bool Stock(Component c) => c == null || !nonStock.Contains(c.gameObject.name);
    }
}
