// Minimal DXF → G-code importer for plotter/knife mode
// Supports: LINE, LWPOLYLINE, POLYLINE, CIRCLE, ARC (ASCII DXF)
// Copyright (c) 2026 — fork extension under GPLv3

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LaserGRBL.SvgConverter;

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
			List<Pair> pairs = ReadPairs(filename);
			gcode.setup(core);

			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("(DXF import: {0})\r\n", Path.GetFileName(filename));
			sb.AppendFormat("F{0}\r\n", PlotterSettings.CutSpeed);

			bool inEntities = false;
			for (int i = 0; i < pairs.Count; i++)
			{
				Pair p = pairs[i];
				if (p.Code == 0 && string.Equals(p.Value, "SECTION", StringComparison.OrdinalIgnoreCase))
				{
					Pair next = Peek(pairs, i + 1);
					if (next != null && next.Code == 2 && string.Equals(next.Value, "ENTITIES", StringComparison.OrdinalIgnoreCase))
						inEntities = true;
					continue;
				}
				if (p.Code == 0 && string.Equals(p.Value, "ENDSEC", StringComparison.OrdinalIgnoreCase))
				{
					inEntities = false;
					continue;
				}
				if (!inEntities || p.Code != 0)
					continue;

				string ent = p.Value.Trim().ToUpperInvariant();
				if (ent == "LINE")
					i = EmitLine(pairs, i, sb);
				else if (ent == "LWPOLYLINE")
					i = EmitLwPolyline(pairs, i, sb);
				else if (ent == "POLYLINE")
					i = EmitPolyline(pairs, i, sb);
				else if (ent == "CIRCLE")
					i = EmitCircle(pairs, i, sb);
				else if (ent == "ARC")
					i = EmitArc(pairs, i, sb);
			}

			gcode.PenUp(sb, "end DXF");
			return sb.ToString();
		}

		private static Pair Peek(List<Pair> pairs, int i)
		{
			return i >= 0 && i < pairs.Count ? pairs[i] : null;
		}

		private static List<Pair> ReadPairs(string filename)
		{
			List<Pair> pairs = new List<Pair>();
			string[] lines = File.ReadAllLines(filename, Encoding.Default);
			for (int i = 0; i + 1 < lines.Length; i += 2)
			{
				string c = lines[i].Trim();
				string v = lines[i + 1].Trim();
				int code;
				if (!int.TryParse(c, NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
					continue;
				pairs.Add(new Pair(code, v));
			}
			return pairs;
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
				if (!map.ContainsKey(pairs[i].Code))
					map[pairs[i].Code] = new List<string>();
				map[pairs[i].Code].Add(pairs[i].Value);
				end = i;
			}
			return end;
		}

		private static float GetFloat(Dictionary<int, List<string>> map, int code, float def = 0)
		{
			List<string> list;
			if (!map.TryGetValue(code, out list) || list.Count == 0)
				return def;
			float v;
			if (float.TryParse(list[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
				return v;
			if (float.TryParse(list[0], NumberStyles.Float, CultureInfo.CurrentCulture, out v))
				return v;
			return def;
		}

		private static int EmitLine(List<Pair> pairs, int start, StringBuilder sb)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);
			float x1 = GetFloat(map, 10), y1 = GetFloat(map, 20);
			float x2 = GetFloat(map, 11), y2 = GetFloat(map, 21);
			gcode.MoveToRapid(sb, x1, y1, "LINE");
			gcode.PenDown(sb, "LINE");
			gcode.MoveTo(sb, x2, y2, "LINE");
			gcode.PenUp(sb, "LINE");
			return end;
		}

		private static int EmitLwPolyline(List<Pair> pairs, int start, StringBuilder sb)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);

			int flags = (int)GetFloat(map, 70, 0);
			bool closed = (flags & 1) != 0;

			List<string> xs, ys;
			if (!map.TryGetValue(10, out xs) || !map.TryGetValue(20, out ys))
				return end;
			int n = Math.Min(xs.Count, ys.Count);
			if (n == 0) return end;

			List<float> px = new List<float>(n);
			List<float> py = new List<float>(n);
			for (int i = 0; i < n; i++)
			{
				float x, y;
				if (!float.TryParse(xs[i], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
					float.TryParse(xs[i], NumberStyles.Float, CultureInfo.CurrentCulture, out x);
				if (!float.TryParse(ys[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
					float.TryParse(ys[i], NumberStyles.Float, CultureInfo.CurrentCulture, out y);
				px.Add(x); py.Add(y);
			}

			gcode.MoveToRapid(sb, px[0], py[0], "LWPOLYLINE");
			gcode.PenDown(sb, "LWPOLYLINE");
			for (int i = 1; i < px.Count; i++)
				gcode.MoveTo(sb, px[i], py[i]);
			if (closed)
				gcode.MoveTo(sb, px[0], py[0], "close");
			gcode.PenUp(sb, "LWPOLYLINE");
			return end;
		}

		private static int EmitPolyline(List<Pair> pairs, int start, StringBuilder sb)
		{
			// POLYLINE ... VERTEX ... SEQEND
			int flags = 0;
			List<float> px = new List<float>();
			List<float> py = new List<float>();
			int i = start + 1;
			while (i < pairs.Count)
			{
				if (pairs[i].Code == 0 && string.Equals(pairs[i].Value, "SEQEND", StringComparison.OrdinalIgnoreCase))
					break;
				if (pairs[i].Code == 0 && string.Equals(pairs[i].Value, "VERTEX", StringComparison.OrdinalIgnoreCase))
				{
					float x = 0, y = 0;
					i++;
					while (i < pairs.Count && pairs[i].Code != 0)
					{
						if (pairs[i].Code == 10) float.TryParse(pairs[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
						if (pairs[i].Code == 20) float.TryParse(pairs[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y);
						i++;
					}
					px.Add(x); py.Add(y);
					continue;
				}
				if (pairs[i].Code == 70)
					int.TryParse(pairs[i].Value, out flags);
				i++;
			}
			bool closed = (flags & 1) != 0;
			if (px.Count > 0)
			{
				gcode.MoveToRapid(sb, px[0], py[0], "POLYLINE");
				gcode.PenDown(sb, "POLYLINE");
				for (int k = 1; k < px.Count; k++)
					gcode.MoveTo(sb, px[k], py[k]);
				if (closed)
					gcode.MoveTo(sb, px[0], py[0], "close");
				gcode.PenUp(sb, "POLYLINE");
			}
			return i < pairs.Count ? i : pairs.Count - 1;
		}

		private static int EmitCircle(List<Pair> pairs, int start, StringBuilder sb)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);
			float cx = GetFloat(map, 10), cy = GetFloat(map, 20), r = GetFloat(map, 40);
			if (r <= 0) return end;

			// Approximate circle with G2: start at rightmost point
			float x0 = cx + r, y0 = cy;
			gcode.MoveToRapid(sb, x0, y0, "CIRCLE");
			gcode.PenDown(sb, "CIRCLE");
			// Full circle as two semicircles (some GRBL prefer this)
			gcode.Arc(sb, 2, cx - r, cy, -r, 0, "CIRCLE");
			gcode.Arc(sb, 2, x0, y0, r, 0, "CIRCLE");
			gcode.PenUp(sb, "CIRCLE");
			return end;
		}

		private static int EmitArc(List<Pair> pairs, int start, StringBuilder sb)
		{
			Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
			int end;
			CollectEntity(pairs, start, map, out end);
			float cx = GetFloat(map, 10), cy = GetFloat(map, 20), r = GetFloat(map, 40);
			float a0 = GetFloat(map, 50) * (float)Math.PI / 180f;
			float a1 = GetFloat(map, 51) * (float)Math.PI / 180f;
			if (r <= 0) return end;

			float x0 = cx + r * (float)Math.Cos(a0);
			float y0 = cy + r * (float)Math.Sin(a0);
			float x1 = cx + r * (float)Math.Cos(a1);
			float y1 = cy + r * (float)Math.Sin(a1);
			float i = cx - x0;
			float j = cy - y0;

			gcode.MoveToRapid(sb, x0, y0, "ARC");
			gcode.PenDown(sb, "ARC");
			// DXF arcs are CCW from start to end
			gcode.Arc(sb, 3, x1, y1, i, j, "ARC");
			gcode.PenUp(sb, "ARC");
			return end;
		}
	}
}
