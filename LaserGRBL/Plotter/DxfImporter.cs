// ASCII DXF → plotter paths → G-code
// Supports: LINE, LWPOLYLINE, POLYLINE, CIRCLE, ARC
// LINE segments are chained into polylines. Entry is from outside the bbox.
// Copyright (c) 2026 — fork extension under GPLv3

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LaserGRBL.Plotter
{
	public static class DxfImporter
	{
		private class Pair
		{
			public int Code;
			public string Value;
			public Pair(int code, string value) { Code = code; Value = value; }
		}

		public static string ConvertFileToGCode(string filename, GrblCore core)
		{
			if (IsBinaryDxf(filename))
			{
				MessageBox.Show(
					"Файл DXF в бинарном формате. Сохраните как ASCII DXF (R12/LT2 или «DXF Text») и откройте снова.",
					"DXF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return "(binary DXF not supported)\r\n";
			}

			List<Pair> pairs = ReadPairs(filename);
			float unitScale = ReadInsUnitsScaleToMm(pairs);

			List<PlotterPath> paths = new List<PlotterPath>();
			List<Tuple<PlotterPoint, PlotterPoint>> looseLines = new List<Tuple<PlotterPoint, PlotterPoint>>();

			bool inEntities = false;
			for (int i = 0; i < pairs.Count; i++)
			{
				Pair p = pairs[i];
				if (p.Code == 0 && Eq(p.Value, "SECTION"))
				{
					Pair next = Peek(pairs, i + 1);
					if (next != null && next.Code == 2 && Eq(next.Value, "ENTITIES"))
						inEntities = true;
					continue;
				}
				if (p.Code == 0 && Eq(p.Value, "ENDSEC"))
				{
					inEntities = false;
					continue;
				}
				if (!inEntities || p.Code != 0)
					continue;

				string ent = p.Value.Trim().ToUpperInvariant();
				if (ent == "LINE")
					i = ReadLine(pairs, i, unitScale, looseLines);
				else if (ent == "LWPOLYLINE")
					i = ReadLwPolyline(pairs, i, unitScale, paths);
				else if (ent == "POLYLINE")
					i = ReadPolyline(pairs, i, unitScale, paths);
				else if (ent == "CIRCLE")
					i = ReadCircle(pairs, i, unitScale, paths);
				else if (ent == "ARC")
					i = ReadArc(pairs, i, unitScale, paths);
			}

			if (looseLines.Count > 0)
				paths.AddRange(PlotterPathEmitter.ChainSegments(looseLines, 0.05f * Math.Max(1f, unitScale)));

			if (paths.Count == 0)
			{
				MessageBox.Show(
					"В DXF не найдено поддерживаемой геометрии (LINE / LWPOLYLINE / POLYLINE / CIRCLE / ARC).\n" +
					"Взрывьте блоки (Explode) и сохраните как ASCII DXF.",
					"DXF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return "(empty DXF)\r\n";
			}

			return PlotterPathEmitter.Emit(paths, core, "DXF import: " + Path.GetFileName(filename));
		}

		private static bool IsBinaryDxf(string filename)
		{
			using (FileStream fs = File.OpenRead(filename))
			{
				byte[] buf = new byte[22];
				int n = fs.Read(buf, 0, buf.Length);
				string head = Encoding.ASCII.GetString(buf, 0, n);
				return head.StartsWith("AutoCAD Binary DXF", StringComparison.OrdinalIgnoreCase);
			}
		}

		private static bool Eq(string a, string b)
		{
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}

		private static Pair Peek(List<Pair> pairs, int i)
		{
			return i >= 0 && i < pairs.Count ? pairs[i] : null;
		}

		/// <summary>
		/// Robust ASCII pair reader: skips blank lines so group codes never desync
		/// (blank lines are a common cause of "scattered" garbage geometry).
		/// </summary>
		private static List<Pair> ReadPairs(string filename)
		{
			List<string> raw = new List<string>();
			foreach (string line in File.ReadLines(filename, Encoding.Default))
			{
				string t = line.Trim();
				if (t.Length == 0) continue;
				raw.Add(t);
			}

			List<Pair> pairs = new List<Pair>();
			for (int i = 0; i + 1 < raw.Count; i += 2)
			{
				int code;
				if (!int.TryParse(raw[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
				{
					// Resync: try next line as code
					i--;
					continue;
				}
				pairs.Add(new Pair(code, raw[i + 1]));
			}
			return pairs;
		}

		/// <summary>$INSUNITS → multiply coordinates to millimetres. 0/4 = unitless/mm → 1.</summary>
		private static float ReadInsUnitsScaleToMm(List<Pair> pairs)
		{
			for (int i = 0; i < pairs.Count - 1; i++)
			{
				if (pairs[i].Code == 9 && Eq(pairs[i].Value, "$INSUNITS"))
				{
					int units;
					if (int.TryParse(pairs[i + 1].Value, out units))
					{
						switch (units)
						{
							case 1: return 25.4f;          // inches
							case 2: return 25.4f * 12f;    // feet
							case 4: return 1f;             // mm
							case 5: return 10f;            // cm
							case 6: return 1000f;          // m
							default: return 1f;
						}
					}
				}
			}
			return 1f;
		}

		private static int CollectEntity(List<Pair> pairs, int start, Dictionary<int, List<string>> map, out int end)
		{
			map.Clear();
			end = start;
			for (int i = start + 1; i < pairs.Count; i++)
			{
				if (pairs[i].Code == 0)
				{
					end = i - 1;
					return end;
				}
				List<string> list;
				if (!map.TryGetValue(pairs[i].Code, out list))
				{
					list = new List<string>();
					map[pairs[i].Code] = list;
				}
				list.Add(pairs[i].Value);
				end = i;
			}
			return end;
		}

		private static float ParseF(string s, float def = 0)
		{
			float v;
			if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
				return v;
			if (float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
				return v;
			return def;
		}

		private static float GetFloat(Dictionary<int, List<string>> map, int code, float def = 0)
		{
			List<string> list;
			if (!map.TryGetValue(code, out list) || list.Count == 0)
				return def;
			return ParseF(list[0], def);
		}

		private static int ReadLine(List<Pair> pairs, int start, float scale, List<Tuple<PlotterPoint, PlotterPoint>> loose)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);
			float x1 = GetFloat(map, 10) * scale, y1 = GetFloat(map, 20) * scale;
			float x2 = GetFloat(map, 11) * scale, y2 = GetFloat(map, 21) * scale;
			if (Math.Abs(x1 - x2) < 1e-7f && Math.Abs(y1 - y2) < 1e-7f)
				return end;
			loose.Add(Tuple.Create(new PlotterPoint(x1, y1), new PlotterPoint(x2, y2)));
			return end;
		}

		private static int ReadLwPolyline(List<Pair> pairs, int start, float scale, List<PlotterPath> paths)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);

			int flags = (int)GetFloat(map, 70, 0);
			bool closed = (flags & 1) != 0;

			// Prefer sequential walk of pairs to keep X/Y/bulge aligned (codes may interleave)
			PlotterPath path = new PlotterPath { Closed = closed };
			float pendingX = float.NaN;
			for (int i = start + 1; i <= end; i++)
			{
				if (pairs[i].Code == 10)
					pendingX = ParseF(pairs[i].Value) * scale;
				else if (pairs[i].Code == 20 && !float.IsNaN(pendingX))
				{
					path.Add(pendingX, ParseF(pairs[i].Value) * scale);
					pendingX = float.NaN;
				}
			}
			if (path.Points.Count >= 2)
				paths.Add(path);
			return end;
		}

		private static int ReadPolyline(List<Pair> pairs, int start, float scale, List<PlotterPath> paths)
		{
			int flags = 0;
			PlotterPath path = new PlotterPath();
			int i = start + 1;
			while (i < pairs.Count)
			{
				if (pairs[i].Code == 0 && Eq(pairs[i].Value, "SEQEND"))
					break;
				if (pairs[i].Code == 0 && Eq(pairs[i].Value, "VERTEX"))
				{
					float x = 0, y = 0;
					i++;
					while (i < pairs.Count && pairs[i].Code != 0)
					{
						if (pairs[i].Code == 10) x = ParseF(pairs[i].Value) * scale;
						if (pairs[i].Code == 20) y = ParseF(pairs[i].Value) * scale;
						i++;
					}
					path.Add(x, y);
					continue;
				}
				if (pairs[i].Code == 70)
					flags = (int)ParseF(pairs[i].Value);
				i++;
			}
			path.Closed = (flags & 1) != 0;
			if (path.Points.Count >= 2)
				paths.Add(path);
			return i < pairs.Count ? i : pairs.Count - 1;
		}

		private static int ReadCircle(List<Pair> pairs, int start, float scale, List<PlotterPath> paths)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);
			float cx = GetFloat(map, 10) * scale, cy = GetFloat(map, 20) * scale, r = GetFloat(map, 40) * scale;
			if (r <= 0) return end;

			PlotterPath path = new PlotterPath { Closed = true };
			const int segs = 64;
			for (int k = 0; k < segs; k++)
			{
				double a = 2 * Math.PI * k / segs;
				path.Add(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a));
			}
			paths.Add(path);
			return end;
		}

		private static int ReadArc(List<Pair> pairs, int start, float scale, List<PlotterPath> paths)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);
			float cx = GetFloat(map, 10) * scale, cy = GetFloat(map, 20) * scale, r = GetFloat(map, 40) * scale;
			float a0deg = GetFloat(map, 50);
			float a1deg = GetFloat(map, 51);
			if (r <= 0) return end;

			// DXF arcs are CCW from start to end
			double a0 = a0deg * Math.PI / 180.0;
			double a1 = a1deg * Math.PI / 180.0;
			if (a1 < a0) a1 += 2 * Math.PI;
			double sweep = a1 - a0;
			int segs = Math.Max(4, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 16)));

			PlotterPath path = new PlotterPath { Closed = false };
			for (int k = 0; k <= segs; k++)
			{
				double a = a0 + sweep * k / segs;
				path.Add(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a));
			}
			paths.Add(path);
			return end;
		}
	}
}
