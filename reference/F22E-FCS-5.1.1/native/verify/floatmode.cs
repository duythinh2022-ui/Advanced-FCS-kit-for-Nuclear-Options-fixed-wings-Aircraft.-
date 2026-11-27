using System;
static class T0 {
  static float A = 16777216f, B = 1f, C = 0.1f, D = 3f;
  static int Main(string[] a) {
    float s = A + B;            // exact sum 16777217 does not fit a float
    float d = s - A;            // 0 when the sum was rounded to float, 1 when it was kept in double
    float p = C * D;            // 0.1f*3f: float product vs double product then rounded
    float q = p - 0.3f;
    Console.WriteLine("runtime: " + (Type.GetType("Mono.Runtime") != null ? "Mono" : ".NET") + " " + Environment.Version + ", OS " + Environment.OSVersion.Platform);
    Console.WriteLine("float (16777216+1)-16777216 = " + d.ToString("R") + "   (0 = float arithmetic, 1 = carried in double)");
    Console.WriteLine("0.1f*3f-0.3f = " + q.ToString("R"));
    return 0;
  }
}
