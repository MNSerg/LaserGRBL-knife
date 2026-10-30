// Fillet sharp polyline corners to a minimum radius for drag-knife turning
// Copyright (c) 2026 — fork extension under GPLv3

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Replaces sharp corners with circular fillets of radius >= MinCornerRadius.
	/// Already-smooth arcs (dense polylines whose local radius >= R) are left alone.
	/// </summary>
	public static class PathFillet
	{
		const float StraightDeg = 4f;       // below this — treat as collinear
		const float SharpTurnDeg = 12f;     // above this on long legs — sharp corner
		const int ArcSegmentsPer90 = 10;    // density of fillet approximation

		public static void ApplyToAll(IList<PlotterPath> paths, float radiusMm)
		{
			if (radiusMm < 0.05f || paths == null) return;
			for (int i = 0; i < paths.Count; i++)
			{
				if (paths[i] != null && paths[i].Points.Count >= 3)
					paths[i] = Apply(paths[i], radiusMm);
			}
		}

		public static PlotterPath Apply(PlotterPath src, float R)
		{
			if (src == null || src.Points.Count < 3 || R < 0.05f)
				return src;

			List<PlotterPoint> pts = new List<PlotterPoint>(src.Points);
			bool closed = src.Closed;
			// Work on a ring for closed paths
			List<PlotterPoint> result = new List<PlotterPoint>();

			int n = pts.Count;
			int count = closed ? n : n; // vertices to consider as corners
			// For open: corners at 1..n-2; for closed: all n vertices

			// We'll rebuild by walking edges and inserting fillets at corners
			List<CornerAction> actions = new List<CornerAction>();
			int cornerCount = closed ? n : n - 2;
			for (int k = 0; k < cornerCount; k++)
			{
				int i = closed ? k : k + 1;
				int i0 = closed ? (i - 1 + n) % n : i - 1;
				int i1 = i;
				int i2 = closed ? (i + 1) % n : i + 1;

				PlotterPoint A = pts[i0], B = pts[i1], C = pts[i2];
				float lenIn = PlotterPath.Dist(A, B);
				float lenOut = PlotterPath.Dist(B, C);
				if (lenIn < 1e-6f || lenOut < 1e-6f)
				{
					actions.Add(CornerAction.Keep(i1));
					continue;
				}

				float ang = TurnAngleDeg(A, B, C); // signed
				float absAng = Math.Abs(ang);
				if (absAng < StraightDeg)
				{
					actions.Add(CornerAction.Keep(i1));
					continue;
				}

				// Local circumradius of triple — for dense smooth arcs this is ~true R
				float circR = CircumRadius(A, B, C);
				bool shortLegs = lenIn < 2.5f * R && lenOut < 2.5f * R;
				if (shortLegs && !float.IsInfinity(circR) && circR >= R * 0.95f)
				{
					// Already rounded enough
					actions.Add(CornerAction.Keep(i1));
					continue;
				}

				// Discrete sharp corner (long legs + meaningful turn) → fillet to R
				if (absAng >= SharpTurnDeg)
				{
					FilletGeom g;
					if (TryBuildFillet(A, B, C, R, out g))
						actions.Add(CornerAction.Fillet(i1, g));
					else
						actions.Add(CornerAction.Keep(i1));
				}
				else
					actions.Add(CornerAction.Keep(i1));
			}

			if (closed)
			{
				// Emit: for each corner i, output fillet arc or vertex, between corners output nothing extra
				// Path = for i in 0..n-1: if fillet at i → T1+arc+T2 (T2 connects to next); if keep → point i
				// Careful: consecutive fillets — T2 of one should connect toward T1 of next along the shared edge.
				for (int k = 0; k < n; k++)
				{
					CornerAction act = actions[k];
					if (act.Kind == CornerKind.Fillet)
					{
						result.Add(act.Geom.T1);
						result.AddRange(act.Geom.ArcPoints); // exclusive of T1/T2 endpoints ideally
						result.Add(act.Geom.T2);
					}
					else
					{
						result.Add(pts[k]);
					}
				}
			}
			else
			{
				result.Add(pts[0]);
				for (int i = 1; i < n - 1; i++)
				{
					CornerAction act = actions[i - 1]; // actions indexed for corners 1..n-2
					if (act.Kind == CornerKind.Fillet)
					{
						result.Add(act.Geom.T1);
						result.AddRange(act.Geom.ArcPoints);
						result.Add(act.Geom.T2);
					}
					else
						result.Add(pts[i]);
				}
				result.Add(pts[n - 1]);
			}

			PlotterPath path = new PlotterPath { Closed = closed };
			foreach (PlotterPoint p in result)
				path.Add(p.X, p.Y);
			return path;
		}

		enum CornerKind { Keep, Fillet }

		struct CornerAction
		{
			public CornerKind Kind;
			public int Index;
			public FilletGeom Geom;
			public static CornerAction Keep(int i) { return new CornerAction { Kind = CornerKind.Keep, Index = i }; }
			public static CornerAction Fillet(int i, FilletGeom g) { return new CornerAction { Kind = CornerKind.Fillet, Index = i, Geom = g }; }
		}

		struct FilletGeom
		{
			public PlotterPoint T1, T2;
			public List<PlotterPoint> ArcPoints; // interior samples between T1 and T2
		}

		static bool TryBuildFillet(PlotterPoint A, PlotterPoint B, PlotterPoint C, float R, out FilletGeom g)
		{
			g = new FilletGeom { ArcPoints = new List<PlotterPoint>() };
			float lenIn = PlotterPath.Dist(A, B);
			float lenOut = PlotterPath.Dist(B, C);
			PlotterPoint v1 = Normalize(Sub(B, A));
			PlotterPoint v2 = Normalize(Sub(C, B));

			float angRad = TurnAngleRad(v1, v2);
			float absAng = Math.Abs(angRad);
			if (absAng < 1e-4f || absAng > Math.PI - 1e-3f)
				return false;

			// Trim distance from vertex to tangent points
			float d = R * (float)Math.Tan(absAng / 2.0);
			if (d < 1e-5f) return false;
			if (d > lenIn - 1e-4f || d > lenOut - 1e-4f)
			{
				// Shrink R to fit shorter leg
				float dMax = Math.Min(lenIn, lenOut) * 0.49f;
				if (dMax < 1e-4f) return false;
				R = dMax / (float)Math.Tan(absAng / 2.0);
				if (R < 0.05f) return false;
				d = dMax;
			}

			PlotterPoint T1 = new PlotterPoint(B.X - v1.X * d, B.Y - v1.Y * d);
			PlotterPoint T2 = new PlotterPoint(B.X + v2.X * d, B.Y + v2.Y * d);

			// Unit normals pointing toward the turn (left of v1 if ang>0)
			PlotterPoint n1 = angRad > 0 ? PerpLeft(v1) : PerpRight(v1);
			PlotterPoint center = new PlotterPoint(T1.X + n1.X * R, T1.Y + n1.Y * R);

			double a0 = Math.Atan2(T1.Y - center.Y, T1.X - center.X);
			double a1 = Math.Atan2(T2.Y - center.Y, T2.X - center.X);
			// Sweep in the turn direction
			double sweep;
			if (angRad > 0)
			{
				// CCW
				sweep = a1 - a0;
				while (sweep < 0) sweep += 2 * Math.PI;
				while (sweep >= 2 * Math.PI) sweep -= 2 * Math.PI;
			}
			else
			{
				// CW
				sweep = a1 - a0;
				while (sweep > 0) sweep -= 2 * Math.PI;
				while (sweep <= -2 * Math.PI) sweep += 2 * Math.PI;
			}

			int segs = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) * ArcSegmentsPer90));
			g.T1 = T1;
			g.T2 = T2;
			g.ArcPoints = new List<PlotterPoint>();
			for (int s = 1; s < segs; s++)
			{
				double a = a0 + sweep * s / segs;
				g.ArcPoints.Add(new PlotterPoint(
					center.X + R * (float)Math.Cos(a),
					center.Y + R * (float)Math.Sin(a)));
			}
			return true;
		}

		static float TurnAngleDeg(PlotterPoint A, PlotterPoint B, PlotterPoint C)
		{
			return TurnAngleRad(Normalize(Sub(B, A)), Normalize(Sub(C, B))) * 180f / (float)Math.PI;
		}

		static float TurnAngleRad(PlotterPoint v1, PlotterPoint v2)
		{
			float cross = v1.X * v2.Y - v1.Y * v2.X;
			float dot = v1.X * v2.X + v1.Y * v2.Y;
			return (float)Math.Atan2(cross, dot);
		}

		static float CircumRadius(PlotterPoint A, PlotterPoint B, PlotterPoint C)
		{
			float a = PlotterPath.Dist(B, C);
			float b = PlotterPath.Dist(A, C);
			float c = PlotterPath.Dist(A, B);
			float area2 = Math.Abs((B.X - A.X) * (C.Y - A.Y) - (B.Y - A.Y) * (C.X - A.X));
			if (area2 < 1e-10f) return float.PositiveInfinity;
			return a * b * c / area2; // = abc/(2*area) with area2=2*area → abc/area2
		}

		static PlotterPoint Sub(PlotterPoint a, PlotterPoint b)
		{
			return new PlotterPoint(a.X - b.X, a.Y - b.Y);
		}

		static PlotterPoint Normalize(PlotterPoint v)
		{
			float L = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y);
			if (L < 1e-12f) return new PlotterPoint(0, 0);
			return new PlotterPoint(v.X / L, v.Y / L);
		}

		static PlotterPoint PerpLeft(PlotterPoint v) { return new PlotterPoint(-v.Y, v.X); }
		static PlotterPoint PerpRight(PlotterPoint v) { return new PlotterPoint(v.Y, -v.X); }
	}
}
