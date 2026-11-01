// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Routes a knife that cannot lift. A straight move is kept when it stays
	/// out of every closed contour. Otherwise the knife walks around bounding
	/// boxes, and if that graph cannot reach the target it searches the free
	/// gaps on a grid. A straight line remains only when the target sits in a
	/// hole that cannot be reached from outside.
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
			{
				// The gate graph gives up once there are many shapes, or when a
				// rectangle sits behind another. A straight fallback would cut
				// through the part, so search the free space on a grid first.
				List<PlotPoint> around = RouteGrid(from, to, obstacles, margin);
				if (around != null && around.Count > 0)
					return around;
				return direct;
			}

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

		private static List<PlotPoint> RouteGrid(PlotPoint from, PlotPoint to, IList<PlotPath> obstacles, double margin)
		{
			double minX = Math.Min(from.X, to.X);
			double minY = Math.Min(from.Y, to.Y);
			double maxX = Math.Max(from.X, to.X);
			double maxY = Math.Max(from.Y, to.Y);
			if (obstacles != null)
			{
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
				}
			}
			minX -= margin;
			minY -= margin;
			maxX += margin;
			maxY += margin;

			double width = maxX - minX;
			double height = maxY - minY;
			if (width < 1) width = 1;
			if (height < 1) height = 1;
			double cell = margin;
			if (cell < 0.8)
				cell = 0.8;
			int nx = (int)Math.Ceiling(width / cell) + 1;
			int ny = (int)Math.Ceiling(height / cell) + 1;
			const int cap = 200;
			if (nx > cap || ny > cap)
			{
				double scale = nx / (double)cap;
				if (ny / (double)cap > scale)
					scale = ny / (double)cap;
				cell *= scale;
				nx = (int)Math.Ceiling(width / cell) + 1;
				ny = (int)Math.Ceiling(height / cell) + 1;
			}
			if (nx > cap) nx = cap;
			if (ny > cap) ny = cap;
			if (nx < 2) nx = 2;
			if (ny < 2) ny = 2;

			int count = nx * ny;
			bool[] blocked = new bool[count];
			for (int iy = 0; iy < ny; iy++)
			{
				for (int ix = 0; ix < nx; ix++)
				{
					PlotPoint cellPoint = CellAt(ix, iy, minX, minY, cell);
					blocked[ix + iy * nx] = InsideAny(cellPoint, obstacles);
				}
			}

			int start = FindLink(from, minX, minY, cell, nx, ny, blocked, obstacles);
			int goal = FindLink(to, minX, minY, cell, nx, ny, blocked, obstacles);
			if (start < 0 || goal < 0)
				return null;
			if (start == goal)
				goal = FindLinkExcept(to, start, minX, minY, cell, nx, ny, blocked, obstacles);
			if (goal < 0)
				return null;

			double[] score = new double[count];
			int[] prev = new int[count];
			bool[] seen = new bool[count];
			for (int i = 0; i < count; i++)
			{
				score[i] = double.MaxValue;
				prev[i] = -1;
			}
			score[start] = 0;
			RouteHeap heap = new RouteHeap();
			heap.Push(start, PlotGeom.Dist(CellAtIndex(start, nx, minX, minY, cell), to));

			int[] stepX = new int[] { 1, -1, 0, 0, 1, 1, -1, -1 };
			int[] stepY = new int[] { 0, 0, 1, -1, 1, -1, 1, -1 };
			while (heap.Count > 0)
			{
				int current = heap.Pop();
				if (seen[current])
					continue;
				seen[current] = true;
				if (current == goal)
					break;
				int cx = current % nx;
				int cy = current / nx;
				PlotPoint here = CellAt(cx, cy, minX, minY, cell);
				for (int k = 0; k < 8; k++)
				{
					int nxv = cx + stepX[k];
					int nyv = cy + stepY[k];
					if (nxv < 0 || nyv < 0 || nxv >= nx || nyv >= ny)
						continue;
					int next = nxv + nyv * nx;
					if (blocked[next] || seen[next])
						continue;
					PlotPoint there = CellAt(nxv, nyv, minX, minY, cell);
					if (Hits(here, there, obstacles))
						continue;
					double step = k < 4 ? cell : cell * 1.41421356237;
					double nextScore = score[current] + step;
					if (nextScore >= score[next])
						continue;
					score[next] = nextScore;
					prev[next] = current;
					heap.Push(next, nextScore + PlotGeom.Dist(there, to));
				}
			}

			if (score[goal] >= double.MaxValue / 2 || (goal != start && prev[goal] < 0))
				return null;

			List<PlotPoint> chain = new List<PlotPoint>();
			chain.Add(from);
			List<PlotPoint> reversed = new List<PlotPoint>();
			int walk = goal;
			int guard = 0;
			while (walk >= 0 && guard < count + 2)
			{
				reversed.Add(CellAtIndex(walk, nx, minX, minY, cell));
				if (walk == start)
					break;
				walk = prev[walk];
				guard++;
			}
			if (walk != start)
				return null;
			reversed.Reverse();
			for (int i = 0; i < reversed.Count; i++)
				chain.Add(reversed[i]);
			chain.Add(to);

			List<PlotPoint> pulled = Pull(chain, obstacles);
			if (pulled.Count > 0 && PlotGeom.Dist(pulled[0], from) <= 0.0001)
				pulled.RemoveAt(0);
			if (pulled.Count == 0)
				return null;
			return pulled;
		}

		private static int FindLink(PlotPoint exact, double minX, double minY, double cell, int nx, int ny, bool[] blocked, IList<PlotPath> obstacles)
		{
			return FindLinkExcept(exact, -1, minX, minY, cell, nx, ny, blocked, obstacles);
		}

		private static int FindLinkExcept(PlotPoint exact, int skip, double minX, double minY, double cell, int nx, int ny, bool[] blocked, IList<PlotPath> obstacles)
		{
			int ix = (int)Math.Round((exact.X - minX) / cell);
			int iy = (int)Math.Round((exact.Y - minY) / cell);
			if (ix < 0) ix = 0;
			if (iy < 0) iy = 0;
			if (ix >= nx) ix = nx - 1;
			if (iy >= ny) iy = ny - 1;

			int best = -1;
			double bestDist = double.MaxValue;
			for (int y = iy - 8; y <= iy + 8; y++)
			{
				if (y < 0 || y >= ny)
					continue;
				for (int x = ix - 8; x <= ix + 8; x++)
				{
					if (x < 0 || x >= nx)
						continue;
					int id = x + y * nx;
					if (id == skip || blocked[id])
						continue;
					PlotPoint sample = CellAt(x, y, minX, minY, cell);
					if (Hits(exact, sample, obstacles))
						continue;
					double dist = PlotGeom.Dist(exact, sample);
					if (dist < bestDist)
					{
						bestDist = dist;
						best = id;
					}
				}
			}
			return best;
		}

		private static PlotPoint CellAtIndex(int index, int nx, double minX, double minY, double cell)
		{
			return CellAt(index % nx, index / nx, minX, minY, cell);
		}

		private static PlotPoint CellAt(int ix, int iy, double minX, double minY, double cell)
		{
			return new PlotPoint(minX + ix * cell, minY + iy * cell);
		}

		private static bool InsideAny(PlotPoint point, IList<PlotPath> obstacles)
		{
			if (obstacles == null)
				return false;
			for (int i = 0; i < obstacles.Count; i++)
			{
				PlotPath path = obstacles[i];
				if (path == null || !path.Closed || path.Points == null || path.Points.Count < 3)
					continue;
				double x0, y0, x1, y1;
				Bounds(path.Points, out x0, out y0, out x1, out y1);
				if (point.X < x0 || point.X > x1 || point.Y < y0 || point.Y > y1)
					continue;
				if (PlotGeom.PointInPoly(point, path.Points))
					return true;
			}
			return false;
		}

		private static List<PlotPoint> Pull(List<PlotPoint> raw, IList<PlotPath> obstacles)
		{
			List<PlotPoint> kept = new List<PlotPoint>();
			if (raw.Count == 0)
				return kept;
			int i = 0;
			kept.Add(raw[0]);
			while (i < raw.Count - 1)
			{
				int farthest = raw.Count - 1;
				while (farthest > i + 1 && Hits(raw[i], raw[farthest], obstacles))
					farthest--;
				if (farthest == i)
					break;
				kept.Add(raw[farthest]);
				i = farthest;
			}
			return kept;
		}

		private sealed class RouteHeap
		{
			private readonly List<int> nodes = new List<int>();
			private readonly List<double> cost = new List<double>();

			public int Count
			{
				get { return nodes.Count; }
			}

			public void Push(int node, double priority)
			{
				nodes.Add(node);
				cost.Add(priority);
				int index = nodes.Count - 1;
				while (index > 0)
				{
					int parent = (index - 1) / 2;
					if (cost[parent] <= cost[index])
						break;
					Swap(index, parent);
					index = parent;
				}
			}

			public int Pop()
			{
				int top = nodes[0];
				int last = nodes.Count - 1;
				nodes[0] = nodes[last];
				cost[0] = cost[last];
				nodes.RemoveAt(last);
				cost.RemoveAt(last);
				int index = 0;
				while (true)
				{
					int left = index * 2 + 1;
					if (left >= nodes.Count)
						break;
					int right = left + 1;
					int smaller = left;
					if (right < nodes.Count && cost[right] < cost[left])
						smaller = right;
					if (cost[index] <= cost[smaller])
						break;
					Swap(index, smaller);
					index = smaller;
				}
				return top;
			}

			private void Swap(int a, int b)
			{
				int node = nodes[a];
				nodes[a] = nodes[b];
				nodes[b] = node;
				double priority = cost[a];
				cost[a] = cost[b];
				cost[b] = priority;
			}
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
