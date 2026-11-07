// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Cleans vector paths and chooses which contour is next.
	/// With the knife down, a contour that can be reached without crossing
	/// another closed contour is always preferred. The route mode only decides
	/// which of those reachable contours comes first.
	/// </summary>
	public static class PathOptimizer
	{
		private const double DuplicateTol = 0.0001;

		public static List<PlotPath> Prepare(IList<PlotPath> source, PlotterConfig cfg)
		{
			return Prepare(source, cfg, new PlotPoint(0, 0));
		}

		public static List<PlotPath> Prepare(IList<PlotPath> source, PlotterConfig cfg, PlotPoint start)
		{
			List<PlotPath> clean = new List<PlotPath>();
			if (source == null)
				return clean;

			for (int i = 0; i < source.Count; i++)
			{
				PlotPath path = Sanitize(source[i], cfg);
				if (path != null)
					clean.Add(path);
			}

			if (cfg.KnifeStaysDown)
				return OrderKnifeDown(clean, cfg, start);

			if (clean.Count <= 1 || cfg.RouteMode == PlotterRouteMode.FileOrder)
				return clean;

			if (cfg.RouteMode == PlotterRouteMode.InsideOut)
				return OrderByDepth(clean, start);

			return OrderPenUp(clean, cfg, start);
		}

		private static List<PlotPath> OrderByDepth(List<PlotPath> clean, PlotPoint start)
		{
			int[] depth = ComputeDepth(clean);
			int maxDepth = 0;
			for (int i = 0; i < depth.Length; i++)
			{
				if (depth[i] > maxDepth)
					maxDepth = depth[i];
			}

			List<PlotPath> ordered = new List<PlotPath>();
			PlotPoint cursor = start;
			double heading = 0;
			bool haveHeading = false;
			for (int d = maxDepth; d >= 0; d--)
			{
				List<int> bucket = new List<int>();
				for (int i = 0; i < clean.Count; i++)
				{
					if (depth[i] == d)
						bucket.Add(i);
				}
				TakeNearest(clean, bucket, ordered, ref cursor, ref heading, ref haveHeading);
			}
			return ordered;
		}

		private static List<PlotPath> OrderPenUp(List<PlotPath> clean, PlotterConfig cfg, PlotPoint start)
		{
			double[] inward = null;
			if (cfg.RouteMode == PlotterRouteMode.OutsideIn)
				inward = MeasureInward(clean);
			List<int> left = new List<int>();
			for (int i = 0; i < clean.Count; i++)
				left.Add(i);

			List<PlotPath> ordered = new List<PlotPath>();
			PlotPoint cursor = start;
			double heading = 0;
			bool haveHeading = false;
			while (left.Count > 0)
			{
				int pick = 0;
				PlotPath bestPath = null;
				double bestScore = double.MaxValue;
				double bestHeading = heading;
				PlotPoint bestEnd = cursor;
				for (int s = 0; s < left.Count; s++)
				{
					int index = left[s];
					PlotPoint end;
					double exitHeading;
					double cost;
					PlotPath oriented = BestOrientation(clean[index], cursor, heading, haveHeading, out cost, out end, out exitHeading);
					double score = cost;
					if (inward != null)
						score = inward[index] * 100000.0 + cost;
					if (score < bestScore)
					{
						bestScore = score;
						pick = s;
						bestPath = oriented;
						bestEnd = end;
						bestHeading = exitHeading;
					}
				}
				ordered.Add(bestPath);
				cursor = bestEnd;
				heading = bestHeading;
				haveHeading = true;
				left.RemoveAt(pick);
			}
			return ordered;
		}

		/// <summary>
		/// The blade stays down, so the first move is a cut. Start each closed
		/// contour at the boundary point closest to the knife, and do not walk
		/// across a contour to reach it. A hole that cannot be reached from
		/// outside waits until every reachable outline has been cut.
		/// </summary>
		private static List<PlotPath> OrderKnifeDown(List<PlotPath> clean, PlotterConfig cfg, PlotPoint start)
		{
			List<PlotPath> ordered = new List<PlotPath>();
			if (clean.Count == 0)
				return ordered;

			PlotterRouteMode mode = cfg.RouteMode;
			double[] inward = null;
			int[] depth = null;
			if (mode == PlotterRouteMode.OutsideIn)
				inward = MeasureInward(clean);
			if (mode == PlotterRouteMode.InsideOut)
				depth = ComputeDepth(clean);

			List<int> left = new List<int>();
			for (int i = 0; i < clean.Count; i++)
				left.Add(i);

			PlotPoint cursor = start;
			double heading = 0;
			bool haveHeading = false;

			while (left.Count > 0)
			{
				int pick = 0;
				if (mode != PlotterRouteMode.FileOrder)
				{
					double bestScore = double.MaxValue;
					bool anyClear = false;
					double[] costs = new double[left.Count];
					for (int s = 0; s < left.Count; s++)
					{
						double cost;
						PlotPoint end;
						double exitHeading;
						BestKnifeOrientation(clean[left[s]], cursor, heading, haveHeading, clean, cfg, out cost, out end, out exitHeading);
						costs[s] = cost;
						if (cost < 1e8)
							anyClear = true;
					}
					for (int s = 0; s < left.Count; s++)
					{
						if (anyClear && costs[s] >= 1e8)
							continue;
						double score = costs[s];
						if (mode == PlotterRouteMode.OutsideIn)
							score = inward[left[s]] * 100000.0 + costs[s];
						else if (mode == PlotterRouteMode.InsideOut)
							score = -depth[left[s]] * 100000.0 + costs[s];
						if (score < bestScore)
						{
							bestScore = score;
							pick = s;
						}
					}
				}

				PlotPath chosen;
				double chosenCost;
				PlotPoint chosenEnd;
				double chosenHeading;
				chosen = BestKnifeOrientation(clean[left[pick]], cursor, heading, haveHeading, clean, cfg, out chosenCost, out chosenEnd, out chosenHeading);
				ordered.Add(chosen);
				cursor = chosenEnd;
				heading = chosenHeading;
				haveHeading = true;
				left.RemoveAt(pick);
			}
			return ordered;
		}

		private static double[] MeasureInward(List<PlotPath> paths)
		{
			double minX = double.MaxValue;
			double minY = double.MaxValue;
			double maxX = double.MinValue;
			double maxY = double.MinValue;
			for (int i = 0; i < paths.Count; i++)
			{
				List<PlotPoint> pts = paths[i].Points;
				for (int p = 0; p < pts.Count; p++)
				{
					if (pts[p].X < minX) minX = pts[p].X;
					if (pts[p].Y < minY) minY = pts[p].Y;
					if (pts[p].X > maxX) maxX = pts[p].X;
					if (pts[p].Y > maxY) maxY = pts[p].Y;
				}
			}

			double[] inward = new double[paths.Count];
			for (int i = 0; i < paths.Count; i++)
			{
				List<PlotPoint> pts = paths[i].Points;
				double x0 = pts[0].X;
				double y0 = pts[0].Y;
				double x1 = x0;
				double y1 = y0;
				for (int p = 1; p < pts.Count; p++)
				{
					if (pts[p].X < x0) x0 = pts[p].X;
					if (pts[p].Y < y0) y0 = pts[p].Y;
					if (pts[p].X > x1) x1 = pts[p].X;
					if (pts[p].Y > y1) y1 = pts[p].Y;
				}
				double d = x0 - minX;
				double right = maxX - x1;
				double bottom = y0 - minY;
				double top = maxY - y1;
				if (right < d) d = right;
				if (bottom < d) d = bottom;
				if (top < d) d = top;
				if (d < 0) d = 0;
				inward[i] = d;
			}
			return inward;
		}

		private static PlotPath BestKnifeOrientation(PlotPath path, PlotPoint cursor, double heading, bool haveHeading, IList<PlotPath> obstacles, PlotterConfig cfg, out double cost, out PlotPoint end, out double exitHeading)
		{
			bool closed = path.Closed && path.Points.Count >= 3;
			PlotPath best = path;
			cost = double.MaxValue;
			end = path.Points[path.Points.Count - 1];
			exitHeading = heading;

			for (int rev = 0; rev < 2; rev++)
			{
				List<PlotPoint> ring = rev == 0 ? path.Points : Reversed(path.Points);
				if (!closed)
				{
					ConsiderEntry(ring, 0, 0, false, cursor, heading, haveHeading, obstacles, cfg, ref best, ref cost, ref end, ref exitHeading);
					continue;
				}
				int edge;
				double t;
				ClosestEdge(ring, cursor, out edge, out t);
				ConsiderEntry(ring, edge, t, true, cursor, heading, haveHeading, obstacles, cfg, ref best, ref cost, ref end, ref exitHeading);
			}

			if (closed && cost >= 1e8)
			{
				for (int rev = 0; rev < 2; rev++)
				{
					List<PlotPoint> ring = rev == 0 ? path.Points : Reversed(path.Points);
					int n = ring.Count;
					int step = n > 48 ? n / 48 : 1;
					if (step < 1)
						step = 1;
					for (int e = 0; e < n; e += step)
						ConsiderEntry(ring, e, 0, true, cursor, heading, haveHeading, obstacles, cfg, ref best, ref cost, ref end, ref exitHeading);
				}
			}
			return best;
		}

		private static void ClosestEdge(List<PlotPoint> ring, PlotPoint cursor, out int edge, out double t)
		{
			edge = 0;
			t = 0;
			double best = double.MaxValue;
			int n = ring.Count;
			for (int i = 0; i < n; i++)
			{
				double edgeT;
				PlotPoint point = PlotGeom.ClosestOnSegment(cursor, ring[i], ring[(i + 1) % n], out edgeT);
				double dist = PlotGeom.Dist(cursor, point);
				if (dist < best)
				{
					best = dist;
					edge = i;
					t = edgeT;
				}
			}
		}

		private static void ConsiderEntry(List<PlotPoint> ring, int edge, double t, bool closed, PlotPoint cursor, double heading, bool haveHeading, IList<PlotPath> obstacles, PlotterConfig cfg, ref PlotPath best, ref double cost, ref PlotPoint end, ref double exitHeading)
		{
			List<PlotPoint> pts = closed ? StartAt(ring, edge, t) : new List<PlotPoint>(ring);
			if (pts.Count < 2)
				return;
			PlotPoint dir = PlotGeom.Normalize(PlotGeom.Sub(pts[1], pts[0]));
			if (dir.X == 0 && dir.Y == 0)
				return;

			double along = cfg.BladeOffsetMm - cfg.LeadInMm;
			PlotPoint target = new PlotPoint(pts[0].X + dir.X * along, pts[0].Y + dir.Y * along);
			bool clear = !SafeRoute.Hits(cursor, target, obstacles);
			double travel = PlotGeom.Dist(cursor, target);
			double turn = 0;
			if (haveHeading)
				turn = Math.Abs(PlotGeom.DeltaAngle(heading, PlotGeom.Heading(dir))) * 180.0 / Math.PI;
			double c = travel + 0.03 * turn;
			if (!clear)
				c += 1e9;
			if (c >= cost)
				return;

			cost = c;
			PlotPath oriented = new PlotPath();
			oriented.Points = pts;
			oriented.Closed = closed && pts.Count >= 3;
			best = oriented;
			double shift = cfg.BladeOffsetMm + cfg.OvercutMm;
			if (oriented.Closed)
			{
				end = new PlotPoint(pts[0].X + dir.X * shift, pts[0].Y + dir.Y * shift);
				exitHeading = PlotGeom.Heading(dir);
			}
			else
			{
				PlotPoint exitDir = PlotGeom.Normalize(PlotGeom.Sub(pts[pts.Count - 1], pts[pts.Count - 2]));
				end = new PlotPoint(pts[pts.Count - 1].X + exitDir.X * shift, pts[pts.Count - 1].Y + exitDir.Y * shift);
				exitHeading = PlotGeom.Heading(exitDir);
			}
		}

		private static List<PlotPoint> StartAt(List<PlotPoint> ring, int edge, double t)
		{
			int n = ring.Count;
			List<PlotPoint> pts = new List<PlotPoint>();
			if (t <= 0.0001)
			{
				for (int i = 0; i < n; i++)
					pts.Add(ring[(edge + i) % n]);
				return pts;
			}
			if (t >= 0.9999)
			{
				int start = (edge + 1) % n;
				for (int i = 0; i < n; i++)
					pts.Add(ring[(start + i) % n]);
				return pts;
			}

			PlotPoint a = ring[edge];
			PlotPoint b = ring[(edge + 1) % n];
			pts.Add(new PlotPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
			int v = (edge + 1) % n;
			for (int i = 0; i < n; i++)
				pts.Add(ring[(v + i) % n]);
			return pts;
		}

		private static void TakeNearest(List<PlotPath> clean, List<int> bucket, List<PlotPath> ordered, ref PlotPoint cursor, ref double heading, ref bool haveHeading)
		{
			while (bucket.Count > 0)
			{
				int bestSlot = 0;
				PlotPath bestPath = null;
				double bestCost = double.MaxValue;
				double bestHeading = heading;
				PlotPoint bestEnd = cursor;

				for (int s = 0; s < bucket.Count; s++)
				{
					PlotPath candidate = clean[bucket[s]];
					PlotPoint end;
					double exitHeading;
					double cost;
					PlotPath oriented = BestOrientation(candidate, cursor, heading, haveHeading, out cost, out end, out exitHeading);
					if (cost < bestCost)
					{
						bestCost = cost;
						bestSlot = s;
						bestPath = oriented;
						bestEnd = end;
						bestHeading = exitHeading;
					}
				}

				ordered.Add(bestPath);
				cursor = bestEnd;
				heading = bestHeading;
				haveHeading = true;
				bucket.RemoveAt(bestSlot);
			}
		}

		private static PlotPath BestOrientation(PlotPath path, PlotPoint cursor, double heading, bool haveHeading, out double cost, out PlotPoint end, out double exitHeading)
		{
			bool closed = path.Closed && path.Points.Count >= 3;
			int steps = 1;
			int rotations = closed ? path.Points.Count : 1;
			if (rotations > 80)
				steps = rotations / 80;

			PlotPath best = path;
			cost = double.MaxValue;
			end = path.Points[path.Points.Count - 1];
			exitHeading = heading;

			for (int rev = 0; rev < 2; rev++)
			{
				List<PlotPoint> basePts = rev == 0 ? path.Points : Reversed(path.Points);
				int count = closed ? basePts.Count : 1;
				for (int start = 0; start < count; start += steps)
				{
					List<PlotPoint> pts = closed ? Rotated(basePts, start) : basePts;
					PlotPoint entry = pts[0];
					PlotPoint dir = PlotGeom.Normalize(PlotGeom.Sub(pts[1], pts[0]));
					double entryHeading = PlotGeom.Heading(dir);
					double travel = PlotGeom.Dist(cursor, entry);
					double turn = 0;
					if (haveHeading)
						turn = Math.Abs(PlotGeom.DeltaAngle(heading, entryHeading)) * 180.0 / Math.PI;
					double c = travel + 0.03 * turn;
					if (c < cost)
					{
						PlotPath oriented = new PlotPath();
						oriented.Points = pts;
						oriented.Closed = path.Closed;
						best = oriented;
						cost = c;
						// A closed cut finishes on the overcut, just past the start, with the blade
						// still pointing along the first edge. An open cut finishes at the far end.
						if (path.Closed)
						{
							end = pts[0];
							exitHeading = entryHeading;
						}
						else
						{
							end = pts[pts.Count - 1];
							PlotPoint exitDir = PlotGeom.Normalize(PlotGeom.Sub(pts[pts.Count - 1], pts[pts.Count - 2]));
							exitHeading = PlotGeom.Heading(exitDir);
						}
					}
				}
			}
			return best;
		}

		private static List<PlotPoint> Reversed(List<PlotPoint> pts)
		{
			List<PlotPoint> copy = new List<PlotPoint>(pts);
			copy.Reverse();
			return copy;
		}

		private static List<PlotPoint> Rotated(List<PlotPoint> pts, int start)
		{
			List<PlotPoint> copy = new List<PlotPoint>(pts.Count);
			for (int i = 0; i < pts.Count; i++)
				copy.Add(pts[(start + i) % pts.Count]);
			return copy;
		}

		private static int[] ComputeDepth(List<PlotPath> paths)
		{
			int[] depth = new int[paths.Count];
			for (int i = 0; i < paths.Count; i++)
			{
				if (paths[i].Points.Count < 2)
					continue;
				for (int j = 0; j < paths.Count; j++)
				{
					if (i == j || !paths[j].Closed || paths[j].Points.Count < 3)
						continue;
					if (IsInside(paths[i], paths[j]))
						depth[i]++;
				}
			}
			return depth;
		}

		private static bool IsInside(PlotPath inner, PlotPath outer)
		{
			if (inner.Closed)
				return PlotGeom.PointInPoly(inner.Points[0], outer.Points);

			int step = Math.Max(1, inner.Points.Count / 8);
			for (int i = 0; i < inner.Points.Count; i += step)
			{
				if (PlotGeom.PointInPoly(inner.Points[i], outer.Points))
					return true;
			}
			return false;
		}

		private static PlotPath Sanitize(PlotPath source, PlotterConfig cfg)
		{
			if (source == null || source.Points == null || source.Points.Count < 2)
				return null;

			List<PlotPoint> pts = new List<PlotPoint>();
			for (int i = 0; i < source.Points.Count; i++)
			{
				PlotPoint p = source.Points[i];
				if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.X) || double.IsInfinity(p.Y))
					continue;
				if (pts.Count == 0 || PlotGeom.Dist(pts[pts.Count - 1], p) > DuplicateTol)
					pts.Add(p);
			}

			bool closed = source.Closed;
			if (pts.Count >= 3 && PlotGeom.Dist(pts[0], pts[pts.Count - 1]) <= 0.2)
			{
				pts.RemoveAt(pts.Count - 1);
				closed = true;
			}
			if (!closed && source.Closed)
				closed = pts.Count >= 3;

			if (cfg.SimplifyMm > 0 && pts.Count >= 3)
				pts = PlotGeom.Simplify(pts, cfg.SimplifyMm);

			if (cfg.MinSegmentMm > 0)
				pts = DropShort(pts, cfg.MinSegmentMm, closed);

			if (pts.Count < 2)
				return null;
			if (closed && pts.Count < 3)
				closed = false;

			PlotPath path = new PlotPath();
			path.Points = pts;
			path.Closed = closed;
			return path;
		}

		private static List<PlotPoint> DropShort(List<PlotPoint> pts, double minLen, bool closed)
		{
			List<PlotPoint> kept = new List<PlotPoint>();
			kept.Add(pts[0]);
			for (int i = 1; i < pts.Count - 1; i++)
			{
				if (PlotGeom.Dist(kept[kept.Count - 1], pts[i]) >= minLen)
					kept.Add(pts[i]);
			}
			PlotPoint last = pts[pts.Count - 1];
			if (PlotGeom.Dist(kept[kept.Count - 1], last) >= minLen || kept.Count == 1)
				kept.Add(last);
			else
				kept[kept.Count - 1] = last;

			if (closed && kept.Count >= 3 && PlotGeom.Dist(kept[0], kept[kept.Count - 1]) < minLen)
				kept.RemoveAt(kept.Count - 1);
			return kept;
		}
	}
}
