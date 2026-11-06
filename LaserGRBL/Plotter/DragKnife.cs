// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Drag-knife compensation. The machine XY is the swivel axis.
	/// The blade tip trails that axis by <see cref="PlotterConfig.BladeOffsetMm"/>.
	/// Along a straight, an arc or a fillet the axis stays ahead of the tip on the
	/// tangent, so the curve is not broken into sideways steps. A fillet is a run
	/// of similar bends, even when each sample step is steeper than the corner
	/// threshold. Only an isolated corner sharper than
	/// <see cref="PlotterConfig.CornerThresholdDeg"/> swings the holder around the tip.
	/// A lead-in aligns the blade before the design, and an overcut closes the seam.
	/// </summary>
	public static class DragKnife
	{
		public static List<PlotMove> Build(IList<PlotPath> paths, PlotterConfig cfg)
		{
			return Build(paths, cfg, new PlotPoint(0, 0), false);
		}

		public static List<PlotMove> Build(IList<PlotPath> paths, PlotterConfig cfg, PlotPoint park, bool parkFirst)
		{
			List<PlotMove> moves = new List<PlotMove>();
			if (paths == null || cfg == null)
				return moves;

			int passes = cfg.Passes;
			PlotPoint cursor = new PlotPoint(0, 0);
			if (parkFirst)
				Approach(moves, cfg, paths, ref cursor, park);
			foreach (PlotPath path in paths)
			{
				if (path == null || path.Points == null || path.Points.Count < 2)
					continue;
				for (int pass = 0; pass < passes; pass++)
					Emit(path, cfg, moves, paths, ref cursor);
			}
			return moves;
		}

		private static void Approach(List<PlotMove> moves, PlotterConfig cfg, IList<PlotPath> obstacles, ref PlotPoint cursor, PlotPoint park)
		{
			if (PlotGeom.Dist(cursor, park) <= 0.0001)
			{
				cursor = park;
				return;
			}
			if (cfg.KnifeStaysDown)
			{
				double margin = cfg.LeadInMm;
				if (cfg.BladeOffsetMm > margin)
					margin = cfg.BladeOffsetMm;
				if (cfg.StartMarginMm > margin)
					margin = cfg.StartMarginMm;
				if (margin < 1)
					margin = 1;
				List<PlotPoint> route = SafeRoute.Route(cursor, park, obstacles, margin);
				for (int r = 0; r < route.Count; r++)
				{
					if (PlotGeom.Dist(cursor, route[r]) <= 0.0001)
						continue;
					Travel(moves, route[r]);
					cursor = route[r];
				}
			}
			else
			{
				Travel(moves, park);
			}
			cursor = park;
		}

		private static void Emit(PlotPath path, PlotterConfig cfg, List<PlotMove> moves, IList<PlotPath> obstacles, ref PlotPoint cursor)
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
			bool[] swivel = new bool[pts.Count];
			PlotPoint[] tangent = new PlotPoint[pts.Count];
			Classify(pts, dir, segCount, closed, cfg, swivel, tangent);
			PlotPoint align = tangent[0];
			if (closed && swivel[0])
				align = dir[0];
			PlotPoint holderStart = Shift(pts[0], align, offset - lead);

			if (cfg.KnifeStaysDown)
			{
				double margin = lead;
				if (offset > margin)
					margin = offset;
				if (margin < 1)
					margin = 1;
				List<PlotPoint> route = SafeRoute.Route(cursor, holderStart, obstacles, margin);
				PlotPoint at = cursor;
				for (int r = 0; r < route.Count; r++)
				{
					if (PlotGeom.Dist(at, route[r]) <= 0.0001)
						continue;
					Travel(moves, route[r]);
					at = route[r];
				}
			}
			else
			{
				Travel(moves, holderStart);
			}
			if (lead > 0.0001)
				Line(moves, Shift(pts[0], align, offset));

			for (int s = 0; s < segCount; s++)
			{
				int vertex = (s + 1) % pts.Count;
				bool pathEnd = !closed && s == segCount - 1;
				PlotPoint uIn = dir[s];
				PlotPoint uOut = pathEnd ? uIn : dir[(s + 1) % segCount];
				double deg = pathEnd ? 0 : TurnDegrees(uIn, uOut);
				PlotPoint end = Next(pts, s, closed);

				if (!pathEnd && ShouldLift(cfg, deg))
				{
					Line(moves, Shift(end, uIn, offset));
					LiftCorner(moves, cfg, end, uOut, offset, lead);
				}
				else if (!pathEnd && swivel[vertex])
				{
					Line(moves, Shift(end, uIn, offset));
					Swivel(moves, end, uIn, uOut, offset);
				}
				else
					Line(moves, Shift(end, tangent[vertex], offset));
			}

			if (closed && over > 0.0001)
			{
				PlotPoint overDir = swivel[0] ? dir[0] : tangent[0];
				Line(moves, Shift(pts[0], overDir, offset + over));
			}
			else if (!closed && over > 0.0001)
				Line(moves, Shift(pts[pts.Count - 1], tangent[pts.Count - 1], offset + over));

			if (moves.Count > 0)
			{
				PlotMove last = moves[moves.Count - 1];
				cursor = new PlotPoint(last.X, last.Y);
			}
		}

		private static PlotPoint Next(List<PlotPoint> pts, int index, bool closed)
		{
			if (closed)
				return pts[(index + 1) % pts.Count];
			return pts[index + 1];
		}

		private static void Classify(List<PlotPoint> pts, PlotPoint[] dir, int segCount, bool closed, PlotterConfig cfg, bool[] swivel, PlotPoint[] tangent)
		{
			int count = pts.Count;
			double[] deg = new double[count];
			int[] sign = new int[count];
			int corners = closed ? count : count - 2;
			for (int c = 0; c < corners; c++)
			{
				int vertex = closed ? c : c + 1;
				int inSeg = closed ? (vertex - 1 + segCount) % segCount : vertex - 1;
				int outSeg = vertex % segCount;
				double delta = PlotGeom.DeltaAngle(PlotGeom.Heading(dir[inSeg]), PlotGeom.Heading(dir[outSeg]));
				deg[vertex] = Math.Abs(delta * 180.0 / Math.PI);
				if (delta > 1e-9)
					sign[vertex] = 1;
				else if (delta < -1e-9)
					sign[vertex] = -1;
				tangent[vertex] = Bisector(dir[inSeg], dir[outSeg]);
			}

			if (!closed)
			{
				tangent[0] = dir[0];
				tangent[count - 1] = dir[segCount - 1];
			}

			// A fillet or arc is a run of similar bends. The sampler can leave
			// each step steeper than the corner threshold and longer than the
			// blade offset; that is still a curve. A square or hexagon is one
			// large kink between sides, so it still swivels.
			double shortSide = cfg.BladeOffsetMm * 1.25;
			if (shortSide < 1)
				shortSide = 1;
			bool[] curve = new bool[count];
			if (cfg.BladeOffsetMm > 0.0001 && corners > 0)
			{
				for (int c = 0; c < corners; c++)
				{
					int vertex = closed ? c : c + 1;
					int next = closed ? (vertex + 1) % count : vertex + 1;
					if (!closed && next > count - 2)
						continue;
					if (Continues(pts, vertex, next, deg, sign, shortSide))
					{
						curve[vertex] = true;
						curve[next] = true;
					}
				}

				double threshold = cfg.CornerThresholdDeg;
				for (int c = 0; c < corners; c++)
				{
					int vertex = closed ? c : c + 1;
					if (!curve[vertex] && deg[vertex] >= threshold)
						swivel[vertex] = true;
				}
			}

			if (!closed && count >= 3 && curve[1] && SameChord(pts, 0, 1, 2))
				tangent[0] = EndpointTangent(dir[0], deg[1], sign[1], false);
			if (!closed && count >= 3 && curve[count - 2] && SameChord(pts, count - 3, count - 2, count - 1))
				tangent[count - 1] = EndpointTangent(dir[segCount - 1], deg[count - 2], sign[count - 2], true);
		}

		private static bool Continues(List<PlotPoint> pts, int vertex, int next, double[] deg, int[] sign, double shortSide)
		{
			if (sign[vertex] == 0 || sign[vertex] != sign[next])
				return false;
			if (deg[vertex] < 2 || deg[next] < 2)
				return false;
			if (deg[vertex] > 40 || deg[next] > 40)
				return false;
			double smaller = deg[vertex] < deg[next] ? deg[vertex] : deg[next];
			double larger = deg[vertex] > deg[next] ? deg[vertex] : deg[next];
			if (smaller < larger * 0.5)
				return false;
			if (larger <= 20)
				return true;
			return PlotGeom.Dist(pts[vertex], pts[next]) <= shortSide;
		}

		private static bool SameChord(List<PlotPoint> pts, int a, int b, int c)
		{
			double left = PlotGeom.Dist(pts[a], pts[b]);
			double right = PlotGeom.Dist(pts[b], pts[c]);
			if (left <= 0.0001 || right <= 0.0001)
				return false;
			return left <= right * 1.5 && right <= left * 1.5;
		}

		private static PlotPoint EndpointTangent(PlotPoint chord, double turnDeg, int sign, bool incoming)
		{
			if (turnDeg < 2 || sign == 0)
				return chord;
			double half = turnDeg * 0.5 * Math.PI / 180.0;
			double rot = incoming ? sign * half : -sign * half;
			return Rotate(chord, rot);
		}

		private static PlotPoint Rotate(PlotPoint v, double radians)
		{
			double c = Math.Cos(radians);
			double s = Math.Sin(radians);
			return new PlotPoint(v.X * c - v.Y * s, v.X * s + v.Y * c);
		}

		private static bool ShouldLift(PlotterConfig cfg, double deg)
		{
			if (cfg.KnifeStaysDown)
				return false;
			return cfg.LiftAngleDeg > 0 && deg >= cfg.LiftAngleDeg;
		}

		private static void LiftCorner(List<PlotMove> moves, PlotterConfig cfg, PlotPoint corner, PlotPoint uOut, double offset, double lead)
		{
			double align = lead > 0 ? lead : offset;
			Travel(moves, Shift(corner, uOut, offset - align));
			if (align > 0.0001)
				Line(moves, Shift(corner, uOut, offset));
		}

		private static void Swivel(List<PlotMove> moves, PlotPoint corner, PlotPoint uIn, PlotPoint uOut, double offset)
		{
			PlotPoint from = Shift(corner, uIn, offset);
			PlotPoint to = Shift(corner, uOut, offset);
			if (PlotGeom.Dist(from, to) <= 0.0001)
				return;
			double delta = PlotGeom.DeltaAngle(PlotGeom.Heading(uIn), PlotGeom.Heading(uOut));
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

		private static double TurnDegrees(PlotPoint uIn, PlotPoint uOut)
		{
			double delta = PlotGeom.DeltaAngle(PlotGeom.Heading(uIn), PlotGeom.Heading(uOut));
			return Math.Abs(delta * 180.0 / Math.PI);
		}

		private static PlotPoint Bisector(PlotPoint u, PlotPoint v)
		{
			PlotPoint sum = new PlotPoint(u.X + v.X, u.Y + v.Y);
			PlotPoint tangent = PlotGeom.Normalize(sum);
			if (tangent.X == 0 && tangent.Y == 0)
				return v;
			return tangent;
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
