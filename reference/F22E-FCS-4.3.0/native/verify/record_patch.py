#!/usr/bin/env python3
# TEST ONLY. Patches a COPY of the bench's synced allocator (bench/fcs/Allocator.cs, after regen.sh) so that every checked
# solve is written to the file named by FCS_REC: the inputs exactly as the plugin marshals them, then the C# result.
# Never apply this to Aryx_F22E_StrikeRaptor.FCS/Allocator.cs.   usage: record_patch.py <copy of the tree>/bench/fcs/Allocator.cs
import sys
p = sys.argv[1]; s = open(p).read()
a = "\t\t\t\tint rc = FcsNative.Solve(&io, b);\n"
assert s.count(a) == 1
s = s.replace(a, "\t\t\t\tbyte[] recIn = null;\n\t\t\t\tif (Rec != null) { recIn = new byte[sizeof(FcsNative.AllocIo) + n * sizeof(FcsNative.EffN)]; System.Runtime.InteropServices.Marshal.Copy((System.IntPtr)(&io), recIn, 0, sizeof(FcsNative.AllocIo)); System.Runtime.InteropServices.Marshal.Copy((System.IntPtr)b, recIn, sizeof(FcsNative.AllocIo), n * sizeof(FcsNative.EffN)); }\n" + a)
a2 = "\t\t\t\t\tSystem.Threading.Interlocked.Increment(ref FcsNative.Checked);\n"
assert s.count(a2) == 1
s = s.replace(a2, a2 + "\t\t\t\t\tif (Rec != null) { lock (Rec) { Rec.Write(n); Rec.Write(recIn); for (int i = 0; i < n; i++) Rec.Write(effs[i].cmd); Rec.Write(lastCost); Rec.Write(resDbg.x); Rec.Write(resDbg.y); Rec.Write(resDbg.z); Rec.Write(reachMoves - r0); Rec.Write(twoHorizonBound - t0); Rec.Write(coordNewtonMoves - c0); Rec.Write(pairedMoves - p0); Rec.Write(lastNeededPick ? 1 : 0); Rec.Flush(); } }\n")
a3 = "\t\tprivate FcsNative.EffN[] nbuf;\n"
assert s.count(a3) == 1
s = s.replace(a3, a3 + "\n\t\t// TEST BUILD ONLY: FCS_REC=<file> records every checked solve's inputs and its C# result\n\t\tinternal static readonly System.IO.BinaryWriter Rec = (System.Environment.GetEnvironmentVariable(\"FCS_REC\") != null) ? new System.IO.BinaryWriter(new System.IO.FileStream(System.Environment.GetEnvironmentVariable(\"FCS_REC\"), System.IO.FileMode.Create)) : null;\n")
open(p, "w").write(s)
