// Path model + emitter for continuous knife cutting (no Z lift)
// Copyright (c) 2026 — fork extension under GPLv3

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LaserGRBL.SvgConverter;

namespace LaserGRBL.Plotter
{
	public class PlotterPath
	{
		public readonly List<PlotterPoint> Points = new List<PlotterPoint>();
		public bool Closed;

		public void Add(float x, float y)
		{
			if (Points.Count > 0)
			{
				PlotterPoint last = Points[Points.Count - 1];
				if (Math.Abs(last.X - x) < 1e-5f && Math.Abs(last.Y - y) < 1e-5f)
					return;
			}
			Points.Add(new PlotterPoint(x, y));
		}

		public float Length
		{
			get
			{
				float len = 0;
				for (int i = 1; i < Points.Count; i++)
					len += Dist(Points[i - 1], Points[i]);
				if (Closed && Points.Count > 1)
					len += Dist(Points[Points.Count - 1], Points[0]);
				return len;
			}
		}

		public static float Dist(PlotterPoint a, PlotterPoint b)
		{
			float dx = a.X - b.X, dy = a.Y - b.Y;
			return (float)Math.Sqrt(dx * dx + dy * dy);
		}

		/// <summary>Rotate open/closed polyline so index becomes the new start.</summary>
		public void RotateStart(int index)
		{
			if (Points.Count < 2 || index <= 0 || index >= Points.Count) return;
			List<PlotterPoint> rotated = new List<PlotterPoint>(Points.Count);
			for (int i = index; i < Points.Count; i++) rotated.Add(Points[i]);
			for (int i = 0; i < index; i++) rotated.Add(Points[i]);
			Points.Clear();
			Points.AddRange(rotated);
		}

		public int IndexOfLeftmost()
		{
			int best = 0;
			for (int i = 1; i < Points.Count; i++)
			{
				if (Points[i].X < Points[best].X - 1e-6f ||
					(Math.Abs(Points[i].X - Points[best].X) < 1e-6f && Points[i].Y < Points[best].Y))
					best = i;
			}
			return best;
		}
	}

	public struct PlotterPoint
	{
		public float X;
		public float Y;
		public PlotterPoint(float x, float y) { X = x; Y = y; }
	}

	/// <summary>
	/// Emits knife G-code for AlwaysDown continuous cutting:
	/// entry from outside the design bounding box, then G1 everywhere
	/// (G0 would still cut because height is fixed).
	/// </summary>
	public static class PlotterPathEmitter
	{
		public static string Emit(IList<PlotterPath> paths, GrblCore core, string comment)
		{
			gcode.setup(core);
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("({0})\r\n", comment);
			sb.AppendFormat("F{0}\r\n", PlotterSettings.CutSpeed);

			List<PlotterPath> usable = new List<PlotterPath>();
			foreach (PlotterPath p in paths)
			{
				if (p == null || p.Points.Count < 2) continue;
				usable.Add(p);
			}
			if (usable.Count == 0)
			{
				sb.Append("(no geometry)\r\n");
				return sb.ToString();
			}

			// Sharp corners → fillet to MinCornerRadius; smooth arcs (R >= min) stay as-is.
			// Then drag-knife compensation (BladeOffset) runs inside gcode.MoveTo.
			float filletR = PlotterSettings.MinCornerRadiusMm;
			if (filletR > 0.05f)
			{
				PathFillet.ApplyToAll(usable, filletR);
				sb.AppendFormat(CultureInfo.InvariantCulture,
					"(corner fillet R>={0:0.###} mm; blade offset {1:0.###} mm)\r\n",
					filletR, PlotterSettings.BladeOffsetMm);
			}

			// Prefer larger closed contours first (outer before holes, approx by length)
			if (!PlotterSettings.CutInnerFirst)
			{
				usable.Sort((a, b) =>
				{
					int ca = a.Closed ? 0 : 1;
					int cb = b.Closed ? 0 : 1;
					if (ca != cb) return ca.CompareTo(cb);
					return -a.Length.CompareTo(b.Length);
				});
			}

			float minX, minY, maxX, maxY;
			ComputeBounds(usable, out minX, out minY, out maxX, out maxY);
			float margin = PlotterSettings.LeadInMm > 0.01f ? PlotterSettings.LeadInMm : 5f;

			// Start first path at its leftmost point so entry comes from the left (outside)
			PlotterPath first = usable[0];
			if (first.Closed)
				first.RotateStart(first.IndexOfLeftmost());

			PlotterPoint start = first.Points[0];
			// Outside entry point: left of bbox at start Y — never crosses the interior to reach the rim
			float entryX = minX - margin;
			float entryY = start.Y;

			sb.AppendFormat(CultureInfo.InvariantCulture,
				"(bbox X{0:0.###}..{1:0.###} Y{2:0.###}..{3:0.###}; outside entry)\r\n",
				minX, maxX, minY, maxY);

			// Park logically outside, then enter to contour start (both G1 — blade always cuts)
			gcode.SetPosition(entryX, entryY); // sync internal state without cutting from unknown pose
			gcode.MoveTo(sb, start.X, start.Y, "outside entry");
			gcode.PenDown(sb, "start");

			EmitPathBody(sb, first);

			PlotterPoint last = first.Points[first.Points.Count - 1];
			if (first.Closed)
				last = first.Points[0];

			for (int pi = 1; pi < usable.Count; pi++)
			{
				PlotterPath path = usable[pi];
				// Choose start vertex nearest to current knife position (minimize waste cut)
				int best = 0;
				float bestD = float.MaxValue;
				for (int i = 0; i < path.Points.Count; i++)
				{
					float d = PlotterPath.Dist(last, path.Points[i]);
					if (d < bestD) { bestD = d; best = i; }
				}
				if (path.Closed)
					path.RotateStart(best);

				PlotterPoint nextStart = path.Points[0];
				// Bridge through waste with G1 (no lift available)
				gcode.MoveTo(sb, nextStart.X, nextStart.Y, "bridge");
				EmitPathBody(sb, path);

				last = path.Points[path.Points.Count - 1];
				if (path.Closed)
					last = path.Points[0];
			}

			gcode.PenUp(sb, "end");
			// Retract to outside left of bbox so the knife leaves the artwork
			gcode.MoveTo(sb, minX - margin, last.Y, "exit outside");
			return sb.ToString();
		}

		private static void EmitPathBody(StringBuilder sb, PlotterPath path)
		{
			for (int i = 1; i < path.Points.Count; i++)
				gcode.MoveTo(sb, path.Points[i].X, path.Points[i].Y);
			if (path.Closed && path.Points.Count > 1)
			{
				gcode.MoveTo(sb, path.Points[0].X, path.Points[0].Y, "close");
				float over = PlotterSettings.OvercutMm;
				if (over > 0.01f && path.Points.Count > 1)
				{
					// short overcut along first segment direction
					PlotterPoint a = path.Points[0];
					PlotterPoint b = path.Points[1];
					float dx = b.X - a.X, dy = b.Y - a.Y;
					float len = (float)Math.Sqrt(dx * dx + dy * dy);
					if (len > 1e-6f)
					{
						float s = over / len;
						gcode.MoveTo(sb, a.X + dx * s, a.Y + dy * s, "overcut");
					}
				}
			}
		}

		public static void ComputeBounds(IList<PlotterPath> paths, out float minX, out float minY, out float maxX, out float maxY)
		{
			minX = minY = float.MaxValue;
			maxX = maxY = float.MinValue;
			foreach (PlotterPath p in paths)
			{
				foreach (PlotterPoint pt in p.Points)
				{
					if (pt.X < minX) minX = pt.X;
					if (pt.Y < minY) minY = pt.Y;
					if (pt.X > maxX) maxX = pt.X;
					if (pt.Y > maxY) maxY = pt.Y;
				}
			}
		}

		/// <summary>Chain loose LINE segments into polylines by matching endpoints.</summary>
		public static List<PlotterPath> ChainSegments(IList<Tuple<PlotterPoint, PlotterPoint>> segments, float eps = 0.05f)
		{
			List<PlotterPath> result = new List<PlotterPath>();
			List<Tuple<PlotterPoint, PlotterPoint>> open = new List<Tuple<PlotterPoint, PlotterPoint>>(segments);

			while (open.Count > 0)
			{
				PlotterPath path = new PlotterPath();
				Tuple<PlotterPoint, PlotterPoint> seg = open[0];
				open.RemoveAt(0);
				path.Add(seg.Item1.X, seg.Item1.Y);
				path.Add(seg.Item2.X, seg.Item2.Y);

				bool grew = true;
				while (grew)
				{
					grew = false;
					PlotterPoint head = path.Points[0];
					PlotterPoint tail = path.Points[path.Points.Count - 1];
					for (int i = 0; i < open.Count; i++)
					{
						Tuple<PlotterPoint, PlotterPoint> s = open[i];
						if (Near(tail, s.Item1, eps))
						{
							path.Add(s.Item2.X, s.Item2.Y);
							open.RemoveAt(i);
							grew = true;
							break;
						}
						if (Near(tail, s.Item2, eps))
						{
							path.Add(s.Item1.X, s.Item1.Y);
							open.RemoveAt(i);
							grew = true;
							break;
						}
						if (Near(head, s.Item2, eps))
						{
							path.Points.Insert(0, s.Item1);
							open.RemoveAt(i);
							grew = true;
							break;
						}
						if (Near(head, s.Item1, eps))
						{
							path.Points.Insert(0, s.Item2);
							open.RemoveAt(i);
							grew = true;
							break;
						}
					}
				}

				if (path.Points.Count >= 2 && Near(path.Points[0], path.Points[path.Points.Count - 1], eps))
				{
					path.Closed = true;
					path.Points.RemoveAt(path.Points.Count - 1); // drop duplicate close point
				}
				result.Add(path);
			}
			return result;
		}

		private static bool Near(PlotterPoint a, PlotterPoint b, float eps)
		{
			return Math.Abs(a.X - b.X) <= eps && Math.Abs(a.Y - b.Y) <= eps;
		}
	}
}
