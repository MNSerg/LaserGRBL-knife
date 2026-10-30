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
			List<PlotPath> prepared = PathOptimizer.Prepare(paths, cfg);
			List<PlotMove> moves = DragKnife.Build(prepared, cfg);
			return PlotterGCodeWriter.Write(moves, cfg, prepared.Count);
		}
	}
}
