// Convert Potrace curve lists into PlotterPath polylines (mm)
// Copyright (c) 2026 — fork extension under GPLv3

using System;
using System.Collections.Generic;
using CsPotrace;

namespace LaserGRBL.Plotter
{
	public static class PotraceToPlotter
	{
		public static List<PlotterPath> Convert(List<List<Curve>> list, float oXmm, float oYmm, double scale)
		{
			List<PlotterPath> paths = new List<PlotterPath>();
			if (list == null) return paths;

			foreach (List<Curve> curves in list)
			{
				if (curves == null || curves.Count == 0) continue;
				PlotterPath path = new PlotterPath { Closed = true };
				// Start at first curve A
				PlotterPoint start = ToMm(curves[0].A, oXmm, oYmm, scale);
				path.Add(start.X, start.Y);

				foreach (Curve c in curves)
				{
					if (double.IsNaN(c.LinearLenght)) continue;
					if (c.Kind == CurveKind.Line)
					{
						PlotterPoint b = ToMm(c.B, oXmm, oYmm, scale);
						path.Add(b.X, b.Y);
					}
					else // Bezier — sample
					{
						const int segs = 12;
						for (int s = 1; s <= segs; s++)
						{
							double t = (double)s / segs;
							dPoint p = EvalCubic(c, t);
							PlotterPoint pm = ToMm(p, oXmm, oYmm, scale);
							path.Add(pm.X, pm.Y);
						}
					}
				}

				if (path.Points.Count >= 2)
				{
					// Drop duplicate closing point if present; mark closed
					PlotterPoint a = path.Points[0];
					PlotterPoint b = path.Points[path.Points.Count - 1];
					if (PlotterPath.Dist(a, b) < 0.05f)
						path.Points.RemoveAt(path.Points.Count - 1);
					path.Closed = true;
					paths.Add(path);
				}
			}
			return paths;
		}

		private static PlotterPoint ToMm(dPoint p, float oX, float oY, double scale)
		{
			return new PlotterPoint((float)(p.X / scale + oX), (float)(p.Y / scale + oY));
		}

		private static dPoint EvalCubic(Curve c, double t)
		{
			// Cubic bezier A, CP-A, CP-B, B
			double u = 1 - t;
			double uu = u * u, tt = t * t;
			double uuu = uu * u, ttt = tt * t;
			double x = uuu * c.A.X + 3 * uu * t * c.ControlPointA.X + 3 * u * tt * c.ControlPointB.X + ttt * c.B.X;
			double y = uuu * c.A.Y + 3 * uu * t * c.ControlPointA.Y + 3 * u * tt * c.ControlPointB.Y + ttt * c.B.Y;
			return new dPoint(x, y);
		}
	}
}
