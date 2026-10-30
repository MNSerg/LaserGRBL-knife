// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Drag-knife compensation. The machine XY is the swivel axis.
	/// The blade tip trails that axis by <see cref="PlotterConfig.BladeOffsetMm"/>.
	/// On a corner the holder swings around the tip on an arc of that radius,
	/// so the tip stays on the vertex while the blade turns.
	/// A lead-in aligns the blade before the design, and an overcut closes the seam.
	/// </summary>
	public static class DragKnife
	{
		public static List<PlotMove> Build(IList<PlotPath> paths, PlotterConfig cfg)
		{
			List<PlotMove> moves = new List<PlotMove>();
			if (paths == null || cfg == null)
				return moves;

			int passes = cfg.Passes;
			foreach (PlotPath path in paths)
			{
				if (path == null || path.Points == null || path.Points.Count < 2)
					continue;
				for (int pass = 0; pass < passes; pass++)
					Emit(path, cfg, moves);
			}
			return moves;
		}

		private static void Emit(PlotPath path, PlotterConfig cfg, List<PlotMove> moves)
		{
			List<PlotPoint> pts = Compact(path.Points);
			if (pts.Count < 2)
				return;

			bool closed = path.Closed && pts.Count >= 3;
			int segCount = closed ? pts.Count : pts.Count - 1;
			PlotPoint[] dir = new PlotPoint[segCount];
			for (int s = 0; s < segCount; s++)
			{
				PlotPoint a = pts[s];
				PlotPoint b = pts[(s + 1) % pts.Count];
				dir[s] = PlotGeom.Normalize(PlotGeom.Sub(b, a));
				if (dir[s].X == 0 && dir[s].Y == 0)
					return;
			}

			double offset = cfg.BladeOffsetMm;
			double lead = cfg.LeadInMm;
			double over = cfg.OvercutMm;
			PlotPoint u0 = dir[0];

			Travel(moves, Shift(pts[0], u0, offset - lead));
			if (lead > 0.0001)
				Line(moves, Shift(pts[0], u0, offset));
			Line(moves, Shift(Next(pts, 0, closed), u0, offset));

			for (int s = 1; s < segCount; s++)
			{
				PlotPoint corner = pts[s];
				Corner(moves, cfg, corner, dir[s - 1], dir[s], offset, lead);
				Line(moves, Shift(Next(pts, s, closed), dir[s], offset));
			}

			if (closed)
			{
				Corner(moves, cfg, pts[0], dir[segCount - 1], dir[0], offset, lead);
				if (over > 0.0001)
					Line(moves, Shift(pts[0], dir[0], offset + over));
			}
			else if (over > 0.0001)
			{
				Line(moves, Shift(pts[pts.Count - 1], dir[segCount - 1], offset + over));
			}
		}

		private static PlotPoint Next(List<PlotPoint> pts, int index, bool closed)
		{
			if (closed)
				return pts[(index + 1) % pts.Count];
			return pts[index + 1];
		}

		private static void Corner(List<PlotMove> moves, PlotterConfig cfg, PlotPoint corner, PlotPoint uIn, PlotPoint uOut, double offset, double lead)
		{
			double delta = PlotGeom.DeltaAngle(PlotGeom.Heading(uIn), PlotGeom.Heading(uOut));
			double deg = Math.Abs(delta * 180.0 / Math.PI);
			PlotPoint from = Shift(corner, uIn, offset);
			PlotPoint to = Shift(corner, uOut, offset);

			double liftAngle = cfg.PenMode == PlotterPenMode.Manual ? 0 : cfg.LiftAngleDeg;
			if (liftAngle > 0 && deg >= liftAngle)
			{
				double align = lead > 0 ? lead : offset;
				Travel(moves, Shift(corner, uOut, offset - align));
				if (align > 0.0001)
					Line(moves, to);
				return;
			}

			if (offset <= 0.0001 || deg < cfg.CornerThresholdDeg)
			{
				if (PlotGeom.Dist(from, to) > 0.0001)
					Line(moves, to);
				return;
			}

			if (PlotGeom.Dist(from, to) <= 0.0001)
				return;

			PlotMove arc = new PlotMove();
			arc.Cut = true;
			arc.Arc = true;
			arc.Clockwise = delta < 0;
			arc.X = to.X;
			arc.Y = to.Y;
			arc.I = corner.X - from.X;
			arc.J = corner.Y - from.Y;
			moves.Add(arc);
		}

		private static PlotPoint Shift(PlotPoint point, PlotPoint unit, double distance)
		{
			return new PlotPoint(point.X + unit.X * distance, point.Y + unit.Y * distance);
		}

		private static void Travel(List<PlotMove> moves, PlotPoint p)
		{
			PlotMove move = new PlotMove();
			move.X = p.X;
			move.Y = p.Y;
			move.Cut = false;
			moves.Add(move);
		}

		private static void Line(List<PlotMove> moves, PlotPoint p)
		{
			if (moves.Count > 0)
			{
				PlotMove prev = moves[moves.Count - 1];
				if (Math.Abs(prev.X - p.X) < 0.0001 && Math.Abs(prev.Y - p.Y) < 0.0001)
					return;
			}
			PlotMove move = new PlotMove();
			move.X = p.X;
			move.Y = p.Y;
			move.Cut = true;
			moves.Add(move);
		}

		private static List<PlotPoint> Compact(List<PlotPoint> source)
		{
			List<PlotPoint> pts = new List<PlotPoint>();
			for (int i = 0; i < source.Count; i++)
			{
				if (pts.Count == 0 || PlotGeom.Dist(pts[pts.Count - 1], source[i]) > 0.0001)
					pts.Add(source[i]);
			}
			return pts;
		}
	}
}
