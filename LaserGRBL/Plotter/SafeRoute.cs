// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Routes a knife that cannot lift. A straight move is kept when it stays
	/// out of every closed contour. Otherwise the knife walks around the
	/// bounding box, still outside the part. If the target sits in a hole
	/// that cannot be reached from outside, the straight line is kept:
	/// it is the shortest unavoidable bridge.
	/// </summary>
	public static class SafeRoute
	{
		public static bool Hits(PlotPoint a, PlotPoint b, IList<PlotPath> obstacles)
		{
			if (obstacles == null)
				return false;
			for (int i = 0; i < obstacles.Count; i++)
			{
				PlotPath path = obstacles[i];
				if (path == null || !path.Closed || path.Points == null || path.Points.Count < 3)
					continue;
				if (!NearBox(a, b, path.Points))
					continue;
				if (PlotGeom.SegmentThroughInterior(a, b, path.Points))
					return true;
			}
			return false;
		}

		public static List<PlotPoint> Route(PlotPoint from, PlotPoint to, IList<PlotPath> obstacles, double margin)
		{
			List<PlotPoint> direct = new List<PlotPoint>();
			direct.Add(to);
			if (!Hits(from, to, obstacles))
				return direct;
			if (margin < 0.1)
				margin = 0.1;

			List<PlotPoint> nodes = new List<PlotPoint>();
			nodes.Add(from);
			nodes.Add(to);
			AddGates(nodes, obstacles, margin);

			int n = nodes.Count;
			double[] best = new double[n];
			int[] prev = new int[n];
			bool[] used = new bool[n];
			for (int i = 0; i < n; i++)
			{
				best[i] = double.MaxValue;
				prev[i] = -1;
			}
			best[0] = 0;

			for (int iter = 0; iter < n; iter++)
			{
				int u = -1;
				double bd = double.MaxValue;
				for (int i = 0; i < n; i++)
				{
					if (!used[i] && best[i] < bd)
					{
						bd = best[i];
						u = i;
					}
				}
				if (u < 0 || u == 1)
					break;
				used[u] = true;
				for (int v = 0; v < n; v++)
				{
					if (used[v] || v == u)
						continue;
					if (Hits(nodes[u], nodes[v], obstacles))
						continue;
					double nd = best[u] + PlotGeom.Dist(nodes[u], nodes[v]);
					if (nd < best[v])
					{
						best[v] = nd;
						prev[v] = u;
					}
				}
			}

			if (best[1] >= double.MaxValue / 2 || prev[1] < 0)
				return direct;

			List<PlotPoint> reversed = new List<PlotPoint>();
			int cur = 1;
			int guard = 0;
			while (cur > 0 && guard < n + 2)
			{
				reversed.Add(nodes[cur]);
				cur = prev[cur];
				guard++;
			}
			reversed.Reverse();
			return reversed;
		}

		private static void AddGates(List<PlotPoint> nodes, IList<PlotPath> obstacles, double margin)
		{
			if (obstacles == null)
				return;

			double minX = double.MaxValue;
			double minY = double.MaxValue;
			double maxX = double.MinValue;
			double maxY = double.MinValue;
			bool any = false;
			int closed = 0;
			for (int i = 0; i < obstacles.Count; i++)
			{
				PlotPath path = obstacles[i];
				if (path == null || !path.Closed || path.Points == null || path.Points.Count < 3)
					continue;
				closed++;
			}

			bool perShape = closed > 0 && closed <= 24;
			for (int i = 0; i < obstacles.Count; i++)
			{
				PlotPath path = obstacles[i];
				if (path == null || !path.Closed || path.Points == null || path.Points.Count < 3)
					continue;
				double x0, y0, x1, y1;
				Bounds(path.Points, out x0, out y0, out x1, out y1);
				if (x0 < minX) minX = x0;
				if (y0 < minY) minY = y0;
				if (x1 > maxX) maxX = x1;
				if (y1 > maxY) maxY = y1;
				any = true;
				if (perShape)
					AddBox(nodes, x0 - margin, y0 - margin, x1 + margin, y1 + margin);
			}
			if (any)
				AddBox(nodes, minX - margin, minY - margin, maxX + margin, maxY + margin);
		}

		private static void AddBox(List<PlotPoint> nodes, double x0, double y0, double x1, double y1)
		{
			AddNode(nodes, new PlotPoint(x0, y0));
			AddNode(nodes, new PlotPoint(x1, y0));
			AddNode(nodes, new PlotPoint(x1, y1));
			AddNode(nodes, new PlotPoint(x0, y1));
			AddNode(nodes, new PlotPoint((x0 + x1) * 0.5, y0));
			AddNode(nodes, new PlotPoint(x1, (y0 + y1) * 0.5));
			AddNode(nodes, new PlotPoint((x0 + x1) * 0.5, y1));
			AddNode(nodes, new PlotPoint(x0, (y0 + y1) * 0.5));
		}

		private static void AddNode(List<PlotPoint> nodes, PlotPoint p)
		{
			for (int i = 0; i < nodes.Count; i++)
			{
				if (PlotGeom.Dist(nodes[i], p) < 0.05)
					return;
			}
			if (nodes.Count >= 160)
				return;
			nodes.Add(p);
		}

		private static void Bounds(IList<PlotPoint> pts, out double x0, out double y0, out double x1, out double y1)
		{
			x0 = pts[0].X;
			y0 = pts[0].Y;
			x1 = pts[0].X;
			y1 = pts[0].Y;
			for (int i = 1; i < pts.Count; i++)
			{
				if (pts[i].X < x0) x0 = pts[i].X;
				if (pts[i].Y < y0) y0 = pts[i].Y;
				if (pts[i].X > x1) x1 = pts[i].X;
				if (pts[i].Y > y1) y1 = pts[i].Y;
			}
		}

		private static bool NearBox(PlotPoint a, PlotPoint b, IList<PlotPoint> poly)
		{
			double x0, y0, x1, y1;
			Bounds(poly, out x0, out y0, out x1, out y1);
			double sx0 = Math.Min(a.X, b.X);
			double sy0 = Math.Min(a.Y, b.Y);
			double sx1 = Math.Max(a.X, b.X);
			double sy1 = Math.Max(a.Y, b.Y);
			if (sx1 < x0 || sx0 > x1 || sy1 < y0 || sy0 > y1)
				return false;
			return true;
		}
	}
}
