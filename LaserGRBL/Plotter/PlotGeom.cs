// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LaserGRBL.Plotter
{
	public static class PlotGeom
	{
		public const double Eps = 1e-9;

		public static string Num(double value)
		{
			if (double.IsNaN(value) || double.IsInfinity(value))
				return "0";
			if (Math.Abs(value) < 0.0005)
				value = 0;
			return value.ToString("0.###", CultureInfo.InvariantCulture);
		}

		public static double Dist(PlotPoint a, PlotPoint b)
		{
			double dx = b.X - a.X;
			double dy = b.Y - a.Y;
			return Math.Sqrt(dx * dx + dy * dy);
		}

		public static PlotPoint Sub(PlotPoint a, PlotPoint b)
		{
			return new PlotPoint(a.X - b.X, a.Y - b.Y);
		}

		public static PlotPoint Add(PlotPoint a, PlotPoint b)
		{
			return new PlotPoint(a.X + b.X, a.Y + b.Y);
		}

		public static PlotPoint Mul(PlotPoint a, double s)
		{
			return new PlotPoint(a.X * s, a.Y * s);
		}

		public static PlotPoint Normalize(PlotPoint v)
		{
			double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
			if (len < Eps)
				return new PlotPoint(0, 0);
			return new PlotPoint(v.X / len, v.Y / len);
		}

		public static double Heading(PlotPoint unit)
		{
			return Math.Atan2(unit.Y, unit.X);
		}

		/// <summary>Signed shortest turn from angle a to angle b, radians in (-pi, pi].</summary>
		public static double DeltaAngle(double a, double b)
		{
			double d = b - a;
			while (d <= -Math.PI) d += 2 * Math.PI;
			while (d > Math.PI) d -= 2 * Math.PI;
			return d;
		}

		public static double DistPointSegment(PlotPoint p, PlotPoint a, PlotPoint b)
		{
			double dx = b.X - a.X;
			double dy = b.Y - a.Y;
			double len2 = dx * dx + dy * dy;
			if (len2 < Eps)
				return Dist(p, a);
			double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
			if (t < 0) t = 0;
			else if (t > 1) t = 1;
			return Dist(p, new PlotPoint(a.X + t * dx, a.Y + t * dy));
		}

		public static bool PointInPoly(PlotPoint p, IList<PlotPoint> poly)
		{
			if (poly == null || poly.Count < 3)
				return false;
			bool inside = false;
			int j = poly.Count - 1;
			for (int i = 0; i < poly.Count; i++)
			{
				double yi = poly[i].Y;
				double yj = poly[j].Y;
				double xi = poly[i].X;
				double xj = poly[j].X;
				bool crosses = (yi > p.Y) != (yj > p.Y);
				if (crosses)
				{
					double xint = (xj - xi) * (p.Y - yi) / (yj - yi) + xi;
					if (p.X < xint)
						inside = !inside;
				}
				j = i;
			}
			return inside;
		}

		public static List<PlotPoint> Simplify(IList<PlotPoint> pts, double tolerance)
		{
			List<PlotPoint> copy = new List<PlotPoint>(pts);
			if (copy.Count < 3 || tolerance <= 0)
				return copy;

			bool[] keep = new bool[copy.Count];
			keep[0] = true;
			keep[copy.Count - 1] = true;
			Stack<int> stack = new Stack<int>();
			stack.Push(0);
			stack.Push(copy.Count - 1);
			while (stack.Count > 0)
			{
				int last = stack.Pop();
				int first = stack.Pop();
				double max = 0;
				int idx = first;
				for (int i = first + 1; i < last; i++)
				{
					double d = DistPointSegment(copy[i], copy[first], copy[last]);
					if (d > max)
					{
						max = d;
						idx = i;
					}
				}
				if (max > tolerance && idx != first && idx != last)
				{
					keep[idx] = true;
					stack.Push(first);
					stack.Push(idx);
					stack.Push(idx);
					stack.Push(last);
				}
			}

			List<PlotPoint> result = new List<PlotPoint>();
			for (int i = 0; i < copy.Count; i++)
			{
				if (keep[i])
					result.Add(copy[i]);
			}
			return result;
		}

		public static void AppendCubic(List<PlotPoint> pts, PlotPoint p0, PlotPoint c1, PlotPoint c2, PlotPoint p1, double tolerance)
		{
			if (pts.Count == 0)
				pts.Add(p0);
			FlattenCubic(pts, p0, c1, c2, p1, tolerance, 0);
		}

		private static void FlattenCubic(List<PlotPoint> pts, PlotPoint p0, PlotPoint c1, PlotPoint c2, PlotPoint p1, double tolerance, int depth)
		{
			bool flat = depth >= 12
				|| (DistPointSegment(c1, p0, p1) <= tolerance && DistPointSegment(c2, p0, p1) <= tolerance);
			if (flat)
			{
				pts.Add(p1);
				return;
			}

			PlotPoint p01 = Mid(p0, c1);
			PlotPoint p12 = Mid(c1, c2);
			PlotPoint p23 = Mid(c2, p1);
			PlotPoint p012 = Mid(p01, p12);
			PlotPoint p123 = Mid(p12, p23);
			PlotPoint p0123 = Mid(p012, p123);
			FlattenCubic(pts, p0, p01, p012, p0123, tolerance, depth + 1);
			FlattenCubic(pts, p0123, p123, p23, p1, tolerance, depth + 1);
		}

		private static PlotPoint Mid(PlotPoint a, PlotPoint b)
		{
			return new PlotPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
		}

		public static void SampleArc(List<PlotPoint> pts, PlotPoint start, PlotPoint end, double i, double j, bool clockwise, double stepDeg)
		{
			double cx = start.X + i;
			double cy = start.Y + j;
			double radius = Math.Sqrt(i * i + j * j);
			if (radius < Eps)
			{
				pts.Add(end);
				return;
			}

			double a0 = Math.Atan2(start.Y - cy, start.X - cx);
			double a1 = Math.Atan2(end.Y - cy, end.X - cx);
			double sweep = a1 - a0;
			if (Dist(start, end) < 0.0001)
				sweep = clockwise ? -2 * Math.PI : 2 * Math.PI;
			else
			{
				if (clockwise && sweep > 0) sweep -= 2 * Math.PI;
				if (!clockwise && sweep < 0) sweep += 2 * Math.PI;
			}

			if (stepDeg < 1) stepDeg = 1;
			double step = stepDeg * Math.PI / 180.0;
			int n = (int)Math.Ceiling(Math.Abs(sweep) / step);
			if (n < 1) n = 1;
			if (n > 720) n = 720;
			for (int k = 1; k <= n; k++)
			{
				double ang = a0 + sweep * k / n;
				pts.Add(new PlotPoint(cx + radius * Math.Cos(ang), cy + radius * Math.Sin(ang)));
			}
		}
	}
}
