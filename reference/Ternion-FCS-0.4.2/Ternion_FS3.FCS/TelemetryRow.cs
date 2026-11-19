using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Ternion_FS3.FCS
{
	// F-22E 3.0.4: one CSV row as its parts - each a format string and its boxed values, captured on the main thread in 3.0.3's
	// WriteRow order. Slots are parts filled later (FcsController.FinishRow). WriteTo formats and writes them in order on whichever
	// thread calls it: string.Format(InvariantCulture, fmt, args) per part, then a line end - the text 3.0.3 wrote.
	internal sealed class TelemetryRow
	{
		private readonly List<string> fmts = new List<string>(48);

		private readonly List<object[]> args = new List<object[]>(48);

		// slots (FinishRow)
		public int ceA, reachSlot, timeSlot, timeSlot2, timeSlot3;

		public int reachMain;

		public bool threaded;

		public void Add(string fmt, params object[] a)
		{
			fmts.Add(fmt);
			args.Add(a);
		}

		public int Slot()
		{
			fmts.Add(null);
			args.Add(null);
			return fmts.Count - 1;
		}

		public void Fill(int slot, string fmt, params object[] a)
		{
			fmts[slot] = fmt;
			args[slot] = a;
		}

		// stock-jet port: a slot filled with another row's parts (the capacity estimator's own columns, CapacityEstimator.RowTo)
		private const string Sub = "\u0001sub";

		public void FillSub(int slot, TelemetryRow sub)
		{
			fmts[slot] = Sub;
			args[slot] = new object[] { sub };
		}

		public void WriteTo(TextWriter w)
		{
			WriteParts(w);
			w.WriteLine();
		}

		private void WriteParts(TextWriter w)
		{
			CultureInfo ci = CultureInfo.InvariantCulture;
			for (int i = 0; i < fmts.Count; i++)
			{
				if (fmts[i] == null)
				{
					continue;
				}
				if (ReferenceEquals(fmts[i], Sub))
				{
					((TelemetryRow)args[i][0]).WriteParts(w);
				}
				else
				{
					w.Write(string.Format(ci, fmts[i], args[i]));
				}
			}
		}
	}
}
