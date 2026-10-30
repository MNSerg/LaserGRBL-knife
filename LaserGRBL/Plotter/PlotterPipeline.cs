// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	public static class PlotterPipeline
	{
		public static string Convert(string gcode, PlotterConfig cfg)
		{
			if (gcode == null)
				gcode = "";
			string[] lines = gcode.Split(new char[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
			return Convert(lines, cfg);
		}

		public static string Convert(IEnumerable<string> gcodeLines, PlotterConfig cfg)
		{
			return ConvertPaths(GCodePathReader.Read(gcodeLines), cfg);
		}

		public static string ConvertPaths(IList<PlotPath> paths, PlotterConfig cfg)
		{
			if (cfg == null)
				cfg = PlotterConfig.CreateDefault();
			cfg.Normalize();
			List<PlotPath> copy = Clone(paths);
			PlotPoint park;
			bool parkFirst = PlaceOnSheet(copy, cfg, out park);
			PlotPoint orderFrom = parkFirst ? park : new PlotPoint(0, 0);
			List<PlotPath> prepared = PathOptimizer.Prepare(copy, cfg, orderFrom);
			if (prepared.Count == 0)
				parkFirst = false;
			List<PlotMove> moves = DragKnife.Build(prepared, cfg, park, parkFirst);
			return PlotterGCodeWriter.Write(moves, cfg, prepared.Count);
		}

		/// <summary>
		/// Puts the drawing's bottom-left corner on the point the positioning laser
		/// marked, then returns the blade park  <see cref="PlotterConfig.StartMarginMm"/>
		/// outside that corner, toward the operator and to the left.
		/// Work zero is the blade axis after XY is zeroed with the laser on the sheet corner.
		/// </summary>
		private static bool PlaceOnSheet(List<PlotPath> paths, PlotterConfig cfg, out PlotPoint park)
		{
			park = new PlotPoint(0, 0);
			if (cfg.StartMarginMm <= 0 && System.Math.Abs(cfg.LaserOffsetX) < 1e-9 && System.Math.Abs(cfg.LaserOffsetY) < 1e-9)
				return false;

			bool any = false;
			double minX = 0;
			double minY = 0;
			for (int i = 0; i < paths.Count; i++)
			{
				PlotPath path = paths[i];
				if (path == null || path.Points == null)
					continue;
				for (int p = 0; p < path.Points.Count; p++)
				{
					PlotPoint point = path.Points[p];
					if (!any || point.X < minX) minX = point.X;
					if (!any || point.Y < minY) minY = point.Y;
					any = true;
				}
			}
			if (!any)
				return false;

			double shiftX = cfg.LaserOffsetX - minX;
			double shiftY = cfg.LaserOffsetY - minY;
			if (System.Math.Abs(shiftX) > 1e-9 || System.Math.Abs(shiftY) > 1e-9)
			{
				for (int i = 0; i < paths.Count; i++)
				{
					PlotPath path = paths[i];
					if (path == null || path.Points == null)
						continue;
					for (int p = 0; p < path.Points.Count; p++)
					{
						PlotPoint point = path.Points[p];
						path.Points[p] = new PlotPoint(point.X + shiftX, point.Y + shiftY);
					}
				}
			}

			park = new PlotPoint(cfg.LaserOffsetX - cfg.StartMarginMm, cfg.LaserOffsetY - cfg.StartMarginMm);
			return true;
		}

		private static List<PlotPath> Clone(IList<PlotPath> paths)
		{
			List<PlotPath> copy = new List<PlotPath>();
			if (paths == null)
				return copy;
			for (int i = 0; i < paths.Count; i++)
			{
				PlotPath path = paths[i];
				if (path == null || path.Points == null)
					continue;
				PlotPath dup = new PlotPath();
				dup.Closed = path.Closed;
				dup.Points = new List<PlotPoint>(path.Points);
				copy.Add(dup);
			}
			return copy;
		}
	}
}
