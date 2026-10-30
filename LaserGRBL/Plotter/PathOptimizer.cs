// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Cleans vector paths and orders them for a drag knife:
	/// inner contours before the outline that contains them, then a short pen-up travel.
	/// </summary>
	public static class PathOptimizer
	{
		private const double DuplicateTol = 0.0001;

		public static List<PlotPath> Prepare(IList<PlotPath> source, PlotterConfig cfg)
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

			if (clean.Count <= 1 || (!cfg.OptimizeOrder && !cfg.InsideFirst))
				return clean;

			int[] depth = new int[clean.Count];
			if (cfg.InsideFirst)
				depth = ComputeDepth(clean);

			int maxDepth = 0;
			for (int i = 0; i < depth.Length; i++)
			{
				if (depth[i] > maxDepth)
					maxDepth = depth[i];
			}

			List<PlotPath> ordered = new List<PlotPath>();
			PlotPoint cursor = new PlotPoint(0, 0);
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
				if (cfg.OptimizeOrder)
					TakeNearest(clean, bucket, ordered, ref cursor, ref heading, ref haveHeading);
				else
				{
					for (int i = 0; i < bucket.Count; i++)
					{
						PlotPath path = clean[bucket[i]];
						ordered.Add(path);
						cursor = path.Points[path.Points.Count - 1];
					}
				}
			}
			return ordered;
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
