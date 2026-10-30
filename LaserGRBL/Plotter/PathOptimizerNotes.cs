// Notes / helpers for plotter path strategy
// Copyright (c) 2026 — fork extension under GPLv3

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Documented strategy for knife path optimization.
	/// Image vectorization already uses GrblFile.OptimizePaths (nearest-neighbour TSP heuristic)
	/// and Potrace reverse for inner-first cutting when PlotterSettings is enabled.
	/// SVG import applies drag-knife compensation in LaserGRBL.SvgConverter.gcode during MoveTo.
	/// Further SVG path reordering (collect-then-sort) is planned after hardware parameters are confirmed.
	/// </summary>
	internal static class PathOptimizerNotes
	{
		// Placeholder type so the Plotter folder is self-describing in the project.
		// Real SVG collect-and-sort will land in a follow-up once knife actuation and offset are validated.
	}
}
