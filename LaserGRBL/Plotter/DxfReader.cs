// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Reads an ASCII DXF drawing into millimetre polylines.
	/// LINE, LWPOLYLINE, POLYLINE, ARC, CIRCLE, ELLIPSE, SPLINE and INSERT are supported.
	/// </summary>
	public static class DxfReader
	{
		private struct Pair
		{
			public int Code;
			public string Value;
		}

		private struct Xform
		{
			public double A, B, C, D, E, F;

			public static Xform Identity()
			{
				Xform m = new Xform();
				m.A = 1;
				m.D = 1;
				return m;
			}

			public PlotPoint Apply(PlotPoint p)
			{
				return new PlotPoint(A * p.X + B * p.Y + E, C * p.X + D * p.Y + F);
			}

			public Xform Then(Xform next)
			{
				Xform m = new Xform();
				m.A = next.A * A + next.B * C;
				m.B = next.A * B + next.B * D;
				m.E = next.A * E + next.B * F + next.E;
				m.C = next.C * A + next.D * C;
				m.D = next.C * B + next.D * D;
				m.F = next.C * E + next.D * F + next.F;
				return m;
			}
		}

		private sealed class BlockDef
		{
			public string Name;
			public double BaseX;
			public double BaseY;
			public List<PlotPath> Paths = new List<PlotPath>();
			public List<InsertOp> Inserts = new List<InsertOp>();
		}

		private sealed class InsertOp
		{
			public string Name;
			public double X, Y, Rot, Sx, Sy;
		}

		public static List<PlotPath> Read(string content)
		{
			if (content == null)
				content = "";
			if (content.StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal) || content.IndexOf('\0') >= 0)
				throw new FormatException("Binary DXF is not supported. Save the drawing as ASCII DXF.");

			List<Pair> pairs = ParsePairs(content);
			double scale = UnitScale(pairs);
			Dictionary<string, BlockDef> blocks = new Dictionary<string, BlockDef>(StringComparer.OrdinalIgnoreCase);
			BlockDef model = new BlockDef();
			model.Name = "*MODEL";

			for (int i = 0; i < pairs.Count; i++)
			{
				if (pairs[i].Code != 0 || pairs[i].Value != "SECTION")
					continue;
				string name = "";
				if (i + 1 < pairs.Count && pairs[i + 1].Code == 2)
					name = pairs[i + 1].Value;
				int start = i + 1;
				int end = start;
				while (end < pairs.Count && !(pairs[end].Code == 0 && pairs[end].Value == "ENDSEC"))
					end++;
				if (name == "BLOCKS")
					ReadBlocks(pairs, start, end, scale, blocks);
				else if (name == "ENTITIES")
					ReadEntities(pairs, start, end, scale, model);
				i = end;
			}

			List<PlotPath> world = new List<PlotPath>();
			Paint(model, Xform.Identity(), blocks, 0, world);
			return JoinOpen(world);
		}

		public static string ToContourGCode(IList<PlotPath> paths, int feed)
		{
			if (feed < 1)
				feed = 1000;
			StringBuilder sb = new StringBuilder();
			sb.Append("G90\r\n");
			if (paths == null)
				return sb.ToString();
			string f = feed.ToString(CultureInfo.InvariantCulture);
			for (int p = 0; p < paths.Count; p++)
			{
				PlotPath path = paths[p];
				if (path == null || path.Points == null || path.Points.Count < 2)
					continue;
				sb.Append("G0 X").Append(PlotGeom.Num(path.Points[0].X)).Append(" Y").Append(PlotGeom.Num(path.Points[0].Y)).Append("\r\n");
				sb.Append("M3 S255\r\n");
				for (int i = 1; i < path.Points.Count; i++)
				{
					sb.Append("G1 X").Append(PlotGeom.Num(path.Points[i].X));
					sb.Append(" Y").Append(PlotGeom.Num(path.Points[i].Y));
					sb.Append(" F").Append(f).Append("\r\n");
				}
				if (path.Closed)
				{
					sb.Append("G1 X").Append(PlotGeom.Num(path.Points[0].X));
					sb.Append(" Y").Append(PlotGeom.Num(path.Points[0].Y));
					sb.Append(" F").Append(f).Append("\r\n");
				}
				sb.Append("M5 S0\r\n");
			}
			return sb.ToString();
		}

		private static void ReadBlocks(List<Pair> pairs, int start, int end, double scale, Dictionary<string, BlockDef> blocks)
		{
			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code != 0 || pairs[i].Value != "BLOCK")
					continue;
				BlockDef block = new BlockDef();
				int j = i + 1;
				while (j < end && pairs[j].Code != 0)
				{
					if (pairs[j].Code == 2 || pairs[j].Code == 3)
						block.Name = pairs[j].Value;
					else if (pairs[j].Code == 10)
						block.BaseX = Num(pairs[j].Value) * scale;
					else if (pairs[j].Code == 20)
						block.BaseY = Num(pairs[j].Value) * scale;
					j++;
				}
				int ent = j;
				while (ent < end && !(pairs[ent].Code == 0 && pairs[ent].Value == "ENDBLK"))
					ent++;
				if (!string.IsNullOrEmpty(block.Name))
				{
					ReadEntities(pairs, j, ent, scale, block);
					blocks[block.Name] = block;
				}
				i = ent;
			}
		}

		private static void ReadEntities(List<Pair> pairs, int start, int end, double scale, BlockDef into)
		{
			bool inPoly = false;
			bool polyClosed = false;
			List<PlotPoint> poly = null;
			List<double> polyBulge = null;

			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code != 0)
					continue;
				string type = pairs[i].Value;
				int j = i + 1;
				while (j < end && pairs[j].Code != 0)
					j++;

				if (inPoly && type != "VERTEX" && type != "SEQEND")
				{
					FinishPoly(into, poly, polyBulge, polyClosed);
					inPoly = false;
				}

				if (type == "POLYLINE")
				{
					inPoly = true;
					polyClosed = Flag(pairs, i + 1, j, 70, 1);
					poly = new List<PlotPoint>();
					polyBulge = new List<double>();
				}
				else if (type == "VERTEX" && inPoly)
				{
					double x, y, bulge;
					if (TryXY(pairs, i + 1, j, 10, 20, scale, out x, out y))
					{
						bulge = First(pairs, i + 1, j, 42);
						poly.Add(new PlotPoint(x, y));
						polyBulge.Add(bulge);
					}
				}
				else if (type == "SEQEND" && inPoly)
				{
					FinishPoly(into, poly, polyBulge, polyClosed);
					inPoly = false;
				}
				else if (type == "LINE")
					AddLine(into, pairs, i + 1, j, scale);
				else if (type == "LWPOLYLINE")
					AddLwPolyline(into, pairs, i + 1, j, scale);
				else if (type == "CIRCLE")
					AddCircle(into, pairs, i + 1, j, scale);
				else if (type == "ARC")
					AddArc(into, pairs, i + 1, j, scale);
				else if (type == "ELLIPSE")
					AddEllipse(into, pairs, i + 1, j, scale);
				else if (type == "SPLINE")
					AddSpline(into, pairs, i + 1, j, scale);
				else if (type == "INSERT")
					AddInsert(into, pairs, i + 1, j, scale);

				i = j - 1;
			}
			if (inPoly)
				FinishPoly(into, poly, polyBulge, polyClosed);
		}

		private static bool Hidden(List<Pair> pairs, int start, int end)
		{
			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code == 60 && (int)Math.Round(Num(pairs[i].Value)) == 1)
					return true;
				if (pairs[i].Code == 67 && (int)Math.Round(Num(pairs[i].Value)) == 1)
					return true;
			}
			return false;
		}

		private static void AddLine(BlockDef into, List<Pair> pairs, int start, int end, double scale)
		{
			if (Hidden(pairs, start, end))
				return;
			double x0, y0, x1, y1;
			if (!TryXY(pairs, start, end, 10, 20, scale, out x0, out y0))
				return;
			if (!TryXY(pairs, start, end, 11, 21, scale, out x1, out y1))
				return;
			if (PlotGeom.Dist(new PlotPoint(x0, y0), new PlotPoint(x1, y1)) < 1e-6)
				return;
			PlotPath path = new PlotPath();
			path.Points.Add(new PlotPoint(x0, y0));
			path.Points.Add(new PlotPoint(x1, y1));
			into.Paths.Add(path);
		}

		private static void AddLwPolyline(BlockDef into, List<Pair> pairs, int start, int end, double scale)
		{
			if (Hidden(pairs, start, end))
				return;
			bool closed = Flag(pairs, start, end, 70, 1);
			List<PlotPoint> verts = new List<PlotPoint>();
			List<double> bulges = new List<double>();
			bool have = false;
			double x = 0, y = 0, bulge = 0;
			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code == 10)
				{
					if (have)
					{
						verts.Add(new PlotPoint(x, y));
						bulges.Add(bulge);
						bulge = 0;
					}
					x = Num(pairs[i].Value) * scale;
					have = true;
				}
				else if (pairs[i].Code == 20)
					y = Num(pairs[i].Value) * scale;
				else if (pairs[i].Code == 42)
					bulge = Num(pairs[i].Value);
			}
			if (have)
			{
				verts.Add(new PlotPoint(x, y));
				bulges.Add(bulge);
			}
			FinishPoly(into, verts, bulges, closed);
		}

		private static void FinishPoly(BlockDef into, List<PlotPoint> verts, List<double> bulges, bool closed)
		{
			if (verts == null || verts.Count < 2)
				return;
			List<PlotPoint> pts = new List<PlotPoint>();
			int seg = closed ? verts.Count : verts.Count - 1;
			for (int i = 0; i < seg; i++)
			{
				double bulge = 0;
				if (bulges != null && i < bulges.Count)
					bulge = bulges[i];
				AppendBulge(pts, verts[i], verts[(i + 1) % verts.Count], bulge);
			}
			if (pts.Count < 2)
				return;
			if (closed && PlotGeom.Dist(pts[0], pts[pts.Count - 1]) < 0.0001)
				pts.RemoveAt(pts.Count - 1);
			if (pts.Count < 2)
				return;
			PlotPath path = new PlotPath();
			path.Points = pts;
			path.Closed = closed && pts.Count >= 3;
			into.Paths.Add(path);
		}

		private static void AddCircle(BlockDef into, List<Pair> pairs, int start, int end, double scale)
		{
			if (Hidden(pairs, start, end))
				return;
			double cx, cy;
			if (!TryXY(pairs, start, end, 10, 20, scale, out cx, out cy))
				return;
			double radius = Math.Abs(First(pairs, start, end, 40) * scale);
			if (radius < 1e-6)
				return;
			PlotPath path = new PlotPath();
			path.Closed = true;
			path.Points = SampleArc(cx, cy, radius, radius, 0, Math.PI * 2, true);
			into.Paths.Add(path);
		}

		private static void AddArc(BlockDef into, List<Pair> pairs, int start, int end, double scale)
		{
			if (Hidden(pairs, start, end))
				return;
			double cx, cy;
			if (!TryXY(pairs, start, end, 10, 20, scale, out cx, out cy))
				return;
			double radius = Math.Abs(First(pairs, start, end, 40) * scale);
			if (radius < 1e-6)
				return;
			double a0 = First(pairs, start, end, 50) * Math.PI / 180.0;
			double a1 = First(pairs, start, end, 51) * Math.PI / 180.0;
			double sweep = a1 - a0;
			if (sweep <= 1e-8)
				sweep += Math.PI * 2;
			PlotPath path = new PlotPath();
			path.Points = SampleArc(cx, cy, radius, radius, a0, sweep, false);
			path.Closed = false;
			into.Paths.Add(path);
		}

		private static void AddEllipse(BlockDef into, List<Pair> pairs, int start, int end, double scale)
		{
			if (Hidden(pairs, start, end))
				return;
			double cx, cy, mx, my;
			if (!TryXY(pairs, start, end, 10, 20, scale, out cx, out cy))
				return;
			if (!TryXY(pairs, start, end, 11, 21, scale, out mx, out my))
				return;
			double ratio = First(pairs, start, end, 40);
			if (ratio <= 0)
				ratio = 1;
			double a0 = First(pairs, start, end, 41);
			double a1 = First(pairs, start, end, 42);
			if (Math.Abs(a0) < 1e-12 && Math.Abs(a1) < 1e-12)
				a1 = Math.PI * 2;
			double sweep = a1 - a0;
			if (sweep <= 1e-8)
				sweep += Math.PI * 2;
			double major = Math.Sqrt(mx * mx + my * my);
			double minor = major * ratio;
			bool full = Math.Abs(sweep - Math.PI * 2) < 1e-3;
			PlotPath path = new PlotPath();
			path.Closed = full;
			path.Points = SampleEllipse(cx, cy, mx, my, minor, a0, sweep, full);
			if (path.Points.Count >= 2)
				into.Paths.Add(path);
		}

		private static void AddSpline(BlockDef into, List<Pair> pairs, int start, int end, double scale)
		{
			if (Hidden(pairs, start, end))
				return;
			int degree = (int)Math.Round(First(pairs, start, end, 71));
			if (degree < 1)
				degree = 3;
			List<PlotPoint> fit = new List<PlotPoint>();
			List<PlotPoint> controls = new List<PlotPoint>();
			List<double> knots = new List<double>();
			bool haveFit = false;
			bool haveCtl = false;
			double fx = 0, fy = 0, cx = 0, cy = 0;
			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code == 11)
				{
					if (haveFit)
						fit.Add(new PlotPoint(fx, fy));
					fx = Num(pairs[i].Value) * scale;
					haveFit = true;
				}
				else if (pairs[i].Code == 21 && haveFit)
					fy = Num(pairs[i].Value) * scale;
				else if (pairs[i].Code == 10)
				{
					if (haveCtl)
						controls.Add(new PlotPoint(cx, cy));
					cx = Num(pairs[i].Value) * scale;
					haveCtl = true;
				}
				else if (pairs[i].Code == 20 && haveCtl)
					cy = Num(pairs[i].Value) * scale;
				else if (pairs[i].Code == 40)
					knots.Add(Num(pairs[i].Value));
			}
			if (haveFit)
				fit.Add(new PlotPoint(fx, fy));
			if (haveCtl)
				controls.Add(new PlotPoint(cx, cy));

			List<PlotPoint> pts = null;
			if (fit.Count >= 2)
				pts = fit;
			else if (controls.Count >= 2)
				pts = SampleSpline(controls, knots, degree);
			if (pts == null || pts.Count < 2)
				return;
			int flag = (int)Math.Round(First(pairs, start, end, 70));
			PlotPath path = new PlotPath();
			path.Points = pts;
			path.Closed = (flag & 1) != 0 && pts.Count >= 3;
			into.Paths.Add(path);
		}

		private static void AddInsert(BlockDef into, List<Pair> pairs, int start, int end, double scale)
		{
			if (Hidden(pairs, start, end))
				return;
			string name = null;
			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code == 2)
					name = pairs[i].Value;
			}
			if (string.IsNullOrEmpty(name))
				return;
			double x, y;
			TryXY(pairs, start, end, 10, 20, scale, out x, out y);
			double sx = First(pairs, start, end, 41);
			double sy = First(pairs, start, end, 42);
			if (Math.Abs(sx) < 1e-12)
				sx = 1;
			if (Math.Abs(sy) < 1e-12)
				sy = 1;
			InsertOp op = new InsertOp();
			op.Name = name;
			op.X = x;
			op.Y = y;
			op.Sx = sx;
			op.Sy = sy;
			op.Rot = First(pairs, start, end, 50);
			into.Inserts.Add(op);
		}

		private static void Paint(BlockDef block, Xform xform, Dictionary<string, BlockDef> blocks, int depth, List<PlotPath> into)
		{
			if (block == null || depth > 8)
				return;
			for (int i = 0; i < block.Paths.Count; i++)
			{
				PlotPath src = block.Paths[i];
				PlotPath copy = new PlotPath();
				copy.Closed = src.Closed;
				for (int p = 0; p < src.Points.Count; p++)
					copy.Points.Add(xform.Apply(src.Points[p]));
				into.Add(copy);
			}
			for (int i = 0; i < block.Inserts.Count; i++)
			{
				InsertOp op = block.Inserts[i];
				BlockDef child;
				if (!blocks.TryGetValue(op.Name, out child))
					continue;
				Xform local = Xform.Identity();
				local = Translate(-child.BaseX, -child.BaseY).Then(Scale(op.Sx, op.Sy)).Then(Rotate(op.Rot)).Then(Translate(op.X, op.Y));
				Paint(child, local.Then(xform), blocks, depth + 1, into);
			}
		}

		private static Xform Translate(double x, double y)
		{
			Xform m = Xform.Identity();
			m.E = x;
			m.F = y;
			return m;
		}

		private static Xform Scale(double sx, double sy)
		{
			Xform m = new Xform();
			m.A = sx;
			m.D = sy;
			return m;
		}

		private static Xform Rotate(double degrees)
		{
			double r = degrees * Math.PI / 180.0;
			double co = Math.Cos(r);
			double si = Math.Sin(r);
			Xform m = new Xform();
			m.A = co;
			m.B = -si;
			m.C = si;
			m.D = co;
			return m;
		}

		private static List<PlotPath> JoinOpen(List<PlotPath> source)
		{
			List<PlotPath> closed = new List<PlotPath>();
			List<PlotPath> open = new List<PlotPath>();
			for (int i = 0; i < source.Count; i++)
			{
				PlotPath path = source[i];
				if (path == null || path.Points == null || path.Points.Count < 2)
					continue;
				if (path.Closed && path.Points.Count >= 3)
					closed.Add(path);
				else
				{
					path.Closed = false;
					open.Add(path);
				}
			}

			const double tol = 0.02;
			bool merged = true;
			while (merged)
			{
				merged = false;
				for (int i = 0; i < open.Count && !merged; i++)
				{
					for (int j = i + 1; j < open.Count && !merged; j++)
					{
						PlotPath joined;
						if (!TryJoin(open[i], open[j], tol, out joined))
							continue;
						open[i] = joined;
						open.RemoveAt(j);
						merged = true;
					}
				}
			}

			for (int i = 0; i < open.Count; i++)
			{
				PlotPath path = open[i];
				if (path.Points.Count >= 3 && PlotGeom.Dist(path.Points[0], path.Points[path.Points.Count - 1]) <= tol)
				{
					path.Points.RemoveAt(path.Points.Count - 1);
					path.Closed = path.Points.Count >= 3;
				}
				if (path.Closed)
					closed.Add(path);
				else
					closed.Add(path);
			}
			return closed;
		}

		private static bool TryJoin(PlotPath a, PlotPath b, double tol, out PlotPath joined)
		{
			joined = null;
			PlotPoint a0 = a.Points[0];
			PlotPoint a1 = a.Points[a.Points.Count - 1];
			PlotPoint b0 = b.Points[0];
			PlotPoint b1 = b.Points[b.Points.Count - 1];
			if (PlotGeom.Dist(a1, b0) <= tol)
				joined = Concat(a.Points, b.Points, false, false);
			else if (PlotGeom.Dist(a1, b1) <= tol)
				joined = Concat(a.Points, b.Points, false, true);
			else if (PlotGeom.Dist(a0, b0) <= tol)
				joined = Concat(a.Points, b.Points, true, false);
			else if (PlotGeom.Dist(a0, b1) <= tol)
				joined = Concat(a.Points, b.Points, true, true);
			return joined != null;
		}

		private static PlotPath Concat(List<PlotPoint> a, List<PlotPoint> b, bool reverseA, bool reverseB)
		{
			List<PlotPoint> left = new List<PlotPoint>(a);
			List<PlotPoint> right = new List<PlotPoint>(b);
			if (reverseA)
				left.Reverse();
			if (reverseB)
				right.Reverse();
			PlotPath path = new PlotPath();
			path.Points = left;
			int begin = 0;
			if (right.Count > 0 && PlotGeom.Dist(left[left.Count - 1], right[0]) <= 0.02)
				begin = 1;
			for (int i = begin; i < right.Count; i++)
				path.Points.Add(right[i]);
			return path;
		}

		private static void AppendBulge(List<PlotPoint> pts, PlotPoint a, PlotPoint b, double bulge)
		{
			if (pts.Count == 0)
				pts.Add(a);
			if (Math.Abs(bulge) < 1e-8 || PlotGeom.Dist(a, b) < 1e-9)
			{
				pts.Add(b);
				return;
			}
			double chord = PlotGeom.Dist(a, b);
			double radius = chord * (bulge * bulge + 1.0) / (4.0 * Math.Abs(bulge));
			double half = chord * 0.5;
			double dist2 = radius * radius - half * half;
			if (dist2 < 0)
				dist2 = 0;
			double dist = Math.Sqrt(dist2);
			double sign = bulge >= 0 ? 1 : -1;
			double mx = (a.X + b.X) * 0.5;
			double my = (a.Y + b.Y) * 0.5;
			double nx = -(b.Y - a.Y) / chord;
			double ny = (b.X - a.X) / chord;
			double cx = mx + sign * nx * dist;
			double cy = my + sign * ny * dist;
			double a0 = Math.Atan2(a.Y - cy, a.X - cx);
			double a1 = Math.Atan2(b.Y - cy, b.X - cx);
			double sweep = a1 - a0;
			if (bulge > 0 && sweep < 0)
				sweep += Math.PI * 2;
			if (bulge < 0 && sweep > 0)
				sweep -= Math.PI * 2;
			List<PlotPoint> arc = SampleArc(cx, cy, radius, radius, a0, sweep, false);
			for (int i = 1; i < arc.Count; i++)
				pts.Add(arc[i]);
		}

		private static List<PlotPoint> SampleArc(double cx, double cy, double rx, double ry, double start, double sweep, bool full)
		{
			double radius = Math.Max(Math.Abs(rx), Math.Abs(ry));
			double step = 0.2;
			if (radius > 0.05)
			{
				double cosArg = 1.0 - 0.05 / radius;
				if (cosArg < -1) cosArg = -1;
				if (cosArg > 1) cosArg = 1;
				step = 2 * Math.Acos(cosArg);
			}
			if (step < 0.05)
				step = 0.05;
			if (step > Math.PI / 6)
				step = Math.PI / 6;
			int n = (int)Math.Ceiling(Math.Abs(sweep) / step);
			if (n < 8)
				n = 8;
			if (n > 720)
				n = 720;
			int last = full ? n - 1 : n;
			List<PlotPoint> pts = new List<PlotPoint>();
			for (int i = 0; i <= last; i++)
			{
				double t = start + sweep * i / n;
				pts.Add(new PlotPoint(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
			}
			return pts;
		}

		private static List<PlotPoint> SampleEllipse(double cx, double cy, double mx, double my, double minor, double start, double sweep, bool full)
		{
			double major = Math.Sqrt(mx * mx + my * my);
			if (major < 1e-9)
				return new List<PlotPoint>();
			double nx = -my / major * minor;
			double ny = mx / major * minor;
			double step = Math.PI / 24;
			int n = (int)Math.Ceiling(Math.Abs(sweep) / step);
			if (n < 12)
				n = 12;
			if (n > 720)
				n = 720;
			int last = full ? n - 1 : n;
			List<PlotPoint> pts = new List<PlotPoint>();
			for (int i = 0; i <= last; i++)
			{
				double t = start + sweep * i / n;
				double c = Math.Cos(t);
				double s = Math.Sin(t);
				pts.Add(new PlotPoint(cx + mx * c + nx * s, cy + my * c + ny * s));
			}
			return pts;
		}

		private static List<PlotPoint> SampleSpline(List<PlotPoint> controls, List<double> knots, int degree)
		{
			int n = controls.Count;
			if (degree >= n)
				degree = n - 1;
			if (degree < 1)
				return new List<PlotPoint>(controls);
			if (knots == null || knots.Count < n + degree + 1)
			{
				List<double> made = new List<double>();
				for (int i = 0; i < n + degree + 1; i++)
				{
					if (i <= degree)
						made.Add(0);
					else if (i >= n)
						made.Add(n - degree);
					else
						made.Add(i - degree);
				}
				knots = made;
			}
			double t0 = knots[degree];
			double t1 = knots[knots.Count - degree - 1];
			if (t1 < t0)
			{
				double swap = t0;
				t0 = t1;
				t1 = swap;
			}
			int samples = n * 8;
			if (samples < 8)
				samples = 8;
			if (samples > 400)
				samples = 400;
			List<PlotPoint> pts = new List<PlotPoint>();
			for (int s = 0; s <= samples; s++)
			{
				double t = t0;
				if (samples > 0)
					t = t0 + (t1 - t0) * s / samples;
				if (s == samples)
					t = t1 - 1e-9;
				pts.Add(DeBoor(controls, knots, degree, t));
			}
			return pts;
		}

		private static PlotPoint DeBoor(List<PlotPoint> controls, List<double> knots, int degree, double t)
		{
			int n = controls.Count;
			int span = degree;
			if (t <= knots[degree])
				span = degree;
			else if (t >= knots[n])
				span = n - 1;
			else
			{
				for (int i = degree; i < n; i++)
				{
					if (t >= knots[i] && t < knots[i + 1])
					{
						span = i;
						break;
					}
				}
			}

			PlotPoint[] d = new PlotPoint[degree + 1];
			for (int j = 0; j <= degree; j++)
			{
				int idx = span - degree + j;
				if (idx < 0)
					idx = 0;
				if (idx >= n)
					idx = n - 1;
				d[j] = controls[idx];
			}
			for (int r = 1; r <= degree; r++)
			{
				for (int j = degree; j >= r; j--)
				{
					int left = j + span - degree;
					int right = j + 1 + span - r;
					double denom = 0;
					if (left >= 0 && right < knots.Count)
						denom = knots[right] - knots[left];
					double alpha = 0;
					if (Math.Abs(denom) > 1e-12 && left >= 0)
						alpha = (t - knots[left]) / denom;
					if (alpha < 0) alpha = 0;
					if (alpha > 1) alpha = 1;
					d[j] = new PlotPoint(
						(1 - alpha) * d[j - 1].X + alpha * d[j].X,
						(1 - alpha) * d[j - 1].Y + alpha * d[j].Y);
				}
			}
			return d[degree];
		}

		private static double UnitScale(List<Pair> pairs)
		{
			int units = int.MinValue;
			int measurement = 1;
			bool header = false;
			for (int i = 0; i < pairs.Count; i++)
			{
				if (pairs[i].Code == 0 && pairs[i].Value == "SECTION")
					header = i + 1 < pairs.Count && pairs[i + 1].Code == 2 && pairs[i + 1].Value == "HEADER";
				if (pairs[i].Code == 0 && pairs[i].Value == "ENDSEC")
					header = false;
				if (!header || pairs[i].Code != 9)
					continue;
				string key = pairs[i].Value;
				if (i + 1 >= pairs.Count)
					break;
				if (key == "$INSUNITS" && pairs[i + 1].Code == 70)
					units = (int)Math.Round(Num(pairs[i + 1].Value));
				if (key == "$MEASUREMENT" && pairs[i + 1].Code == 70)
					measurement = (int)Math.Round(Num(pairs[i + 1].Value));
			}
			if (units == int.MinValue)
				return measurement == 0 ? 25.4 : 1;
			switch (units)
			{
				case 1: return 25.4;
				case 2: return 304.8;
				case 4: return 1;
				case 5: return 10;
				case 6: return 1000;
				case 9: return 0.0254;
				case 10: return 914.4;
				case 13: return 0.001;
				case 14: return 100;
				case 15: return 10000;
				case 0: return measurement == 0 ? 25.4 : 1;
				default: return 1;
			}
		}

		private static List<Pair> ParsePairs(string content)
		{
			string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			List<Pair> pairs = new List<Pair>();
			int i = 0;
			while (i + 1 < lines.Length)
			{
				string codeText = lines[i].Trim();
				string value = lines[i + 1].Trim();
				i += 2;
				if (codeText.Length == 0)
					continue;
				int code;
				if (!int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
					continue;
				Pair pair = new Pair();
				pair.Code = code;
				pair.Value = value;
				pairs.Add(pair);
			}
			return pairs;
		}

		private static bool TryXY(List<Pair> pairs, int start, int end, int xc, int yc, double scale, out double x, out double y)
		{
			x = 0;
			y = 0;
			bool hx = false;
			bool hy = false;
			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code == xc)
				{
					x = Num(pairs[i].Value) * scale;
					hx = true;
				}
				else if (pairs[i].Code == yc)
				{
					y = Num(pairs[i].Value) * scale;
					hy = true;
				}
			}
			return hx && hy;
		}

		private static double First(List<Pair> pairs, int start, int end, int code)
		{
			for (int i = start; i < end; i++)
			{
				if (pairs[i].Code == code)
					return Num(pairs[i].Value);
			}
			return 0;
		}

		private static bool Flag(List<Pair> pairs, int start, int end, int code, int bit)
		{
			int value = (int)Math.Round(First(pairs, start, end, code));
			return (value & bit) != 0;
		}

		private static double Num(string text)
		{
			double value;
			if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
				return value;
			return 0;
		}
	}
}
