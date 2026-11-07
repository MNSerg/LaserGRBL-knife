// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System.Collections.Generic;
using CsPotrace;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Turns Potrace outlines (pixel space, Y already flipped by the caller) into millimetre paths.
	/// </summary>
	public static class PotracePaths
	{
		public static List<PlotPath> ToPaths(List<List<Curve>> figures, double offsetX, double offsetY, double pixelsPerMm)
		{
			List<PlotPath> result = new List<PlotPath>();
			if (figures == null)
				return result;

			double scale = pixelsPerMm <= 0 ? 1 : pixelsPerMm;
			for (int f = 0; f < figures.Count; f++)
			{
				List<Curve> fig = figures[f];
				if (fig == null || fig.Count == 0)
					continue;

				PlotPath path = new PlotPath();
				for (int i = 0; i < fig.Count; i++)
				{
					Curve curve = fig[i];
					if (curve.A == null || curve.B == null)
						continue;
					PlotPoint a = ToMm(curve.A, offsetX, offsetY, scale);
					PlotPoint b = ToMm(curve.B, offsetX, offsetY, scale);
					if (path.Points.Count == 0)
						path.Points.Add(a);

					bool bezier = curve.Kind == CurveKind.Bezier && curve.ControlPointA != null && curve.ControlPointB != null;
					if (!bezier)
						path.Points.Add(b);
					else
					{
						PlotPoint c1 = ToMm(curve.ControlPointA, offsetX, offsetY, scale);
						PlotPoint c2 = ToMm(curve.ControlPointB, offsetX, offsetY, scale);
						PlotGeom.AppendCubic(path.Points, a, c1, c2, b, 0.05);
					}
				}
				if (path.Points.Count >= 2)
					result.Add(path);
			}
			return result;
		}

		private static PlotPoint ToMm(dPoint point, double offsetX, double offsetY, double scale)
		{
			return new PlotPoint(point.X / scale + offsetX, point.Y / scale + offsetY);
		}
	}
}
