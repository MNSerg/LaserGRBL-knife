// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Reads LaserGRBL vector G-code (absolute or relative) into polylines.
	/// A cutting move is G1/G2/G3 while the spindle is on and S is positive,
	/// or while M3/M4 is active and no S word has been seen yet.
	/// Arcs are linearized so the drag knife can offset them.
	/// </summary>
	public static class GCodePathReader
	{
		private const double CloseTol = 0.2;

		public static List<PlotPath> Read(IEnumerable<string> lines)
		{
			List<PlotPath> paths = new List<PlotPath>();
			if (lines == null)
				return paths;

			double x = 0, y = 0;
			bool absolute = true;
			int motion = 0;
			int spindle = 5;
			double sValue = 0;
			bool seenS = false;
			bool inCut = false;
			List<PlotPoint> pts = null;

			foreach (string raw in lines)
			{
				Dictionary<char, double> words = Parse(raw);
				if (words.Count == 0)
					continue;

				if (words.ContainsKey('G'))
				{
					int g = (int)Math.Round(words['G']);
					if (g == 90) absolute = true;
					else if (g == 91) absolute = false;
					else if (g == 0 || g == 1 || g == 2 || g == 3)
						motion = g;
				}
				if (words.ContainsKey('M'))
				{
					int m = (int)Math.Round(words['M']);
					if (m == 3 || m == 4 || m == 5)
						spindle = m;
				}
				if (words.ContainsKey('S'))
				{
					sValue = words['S'];
					seenS = true;
				}

				bool hasMove = words.ContainsKey('X') || words.ContainsKey('Y') || words.ContainsKey('I') || words.ContainsKey('J');
				if (!hasMove)
				{
					if (!IsCutting(spindle, sValue, seenS))
						Finish(paths, ref pts, ref inCut);
					continue;
				}

				double nx = x;
				double ny = y;
				if (words.ContainsKey('X'))
					nx = absolute ? words['X'] : x + words['X'];
				if (words.ContainsKey('Y'))
					ny = absolute ? words['Y'] : y + words['Y'];

				bool cutting = (motion == 1 || motion == 2 || motion == 3) && IsCutting(spindle, sValue, seenS);
				if (!cutting)
				{
					Finish(paths, ref pts, ref inCut);
					x = nx;
					y = ny;
					continue;
				}

				if (!inCut)
				{
					inCut = true;
					pts = new List<PlotPoint>();
					pts.Add(new PlotPoint(x, y));
				}

				if (motion == 2 || motion == 3)
				{
					double i = words.ContainsKey('I') ? words['I'] : 0;
					double j = words.ContainsKey('J') ? words['J'] : 0;
					PlotGeom.SampleArc(pts, new PlotPoint(x, y), new PlotPoint(nx, ny), i, j, motion == 2, 5);
				}
				else if (PlotGeom.Dist(new PlotPoint(x, y), new PlotPoint(nx, ny)) > 0.0001)
				{
					pts.Add(new PlotPoint(nx, ny));
				}

				x = nx;
				y = ny;
			}

			Finish(paths, ref pts, ref inCut);
			return paths;
		}

		private static bool IsCutting(int spindle, double sValue, bool seenS)
		{
			bool on = spindle == 3 || spindle == 4;
			if (!on)
				return false;
			if (!seenS)
				return true;
			return sValue > 0;
		}

		private static void Finish(List<PlotPath> paths, ref List<PlotPoint> pts, ref bool inCut)
		{
			inCut = false;
			if (pts == null || pts.Count < 2)
			{
				pts = null;
				return;
			}

			PlotPath path = new PlotPath();
			path.Points = pts;
			if (pts.Count >= 3 && PlotGeom.Dist(pts[0], pts[pts.Count - 1]) <= CloseTol)
			{
				pts.RemoveAt(pts.Count - 1);
				path.Closed = pts.Count >= 3;
			}
			if (path.Points.Count >= 2)
				paths.Add(path);
			pts = null;
		}

		private static Dictionary<char, double> Parse(string raw)
		{
			Dictionary<char, double> words = new Dictionary<char, double>();
			if (string.IsNullOrEmpty(raw))
				return words;

			string line = StripComments(raw);
			int i = 0;
			while (i < line.Length)
			{
				char c = line[i];
				if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
				{
					char key = char.ToUpperInvariant(c);
					i++;
					int start = i;
					if (i < line.Length && (line[i] == '+' || line[i] == '-'))
						i++;
					bool any = false;
					while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.'))
					{
						any = true;
						i++;
					}
					if (!any)
						continue;
					double value;
					if (double.TryParse(line.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
						words[key] = value;
				}
				else
				{
					i++;
				}
			}
			return words;
		}

		private static string StripComments(string raw)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder(raw.Length);
			bool comment = false;
			for (int i = 0; i < raw.Length; i++)
			{
				char c = raw[i];
				if (c == ';')
					break;
				if (c == '(')
				{
					comment = true;
					continue;
				}
				if (c == ')')
				{
					comment = false;
					continue;
				}
				if (!comment)
					sb.Append(c);
			}
			return sb.ToString();
		}
	}
}
