using System;
using System.Collections.Generic;
using System.Linq;
using LaserGRBL.Plotter;
using Xunit;

namespace LaserGRBL.Plotter.Tests
{
	public class PlotterTests
	{
		[Fact]
		public void Pen_follows_square_corners()
		{
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 0;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			List<PlotMove> moves = DragKnife.Build(new[] { Square() }, cfg);

			Assert.Contains(moves, m => m.Cut && Near(m, 10, 0));
			Assert.Contains(moves, m => m.Cut && Near(m, 10, 10));
			Assert.Contains(moves, m => m.Cut && Near(m, 0, 10));
			Assert.Contains(moves, m => m.Cut && Near(m, 0, 0));
			Assert.DoesNotContain(moves, m => m.Arc);
		}

		[Fact]
		public void Drag_knife_offsets_square_and_swings_around_corners()
		{
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 1;
			cfg.LeadInMm = 1;
			cfg.OvercutMm = 1;
			cfg.CornerThresholdDeg = 0;
			List<PlotMove> moves = DragKnife.Build(new[] { Square() }, cfg);

			Assert.False(moves[0].Cut);
			Assert.True(Near(moves[0], 0, 0));
			Assert.Contains(moves, m => m.Cut && !m.Arc && Near(m, 11, 0));

			PlotMove arc = moves.First(m => m.Arc);
			Assert.False(arc.Clockwise);
			Assert.Equal(1, Hypot(arc.I, arc.J), 3);

			PlotMove last = moves[moves.Count - 1];
			Assert.True(last.Cut);
			Assert.True(Near(last, 2, 0));
		}

		[Fact]
		public void Sharp_turn_lifts_the_knife()
		{
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 1;
			cfg.LeadInMm = 1;
			cfg.OvercutMm = 0;
			cfg.LiftAngleDeg = 45;
			List<PlotMove> moves = DragKnife.Build(new[] { Square() }, cfg);

			Assert.Contains(moves.Skip(1), m => !m.Cut);
			Assert.DoesNotContain(moves, m => m.Arc);
		}

		[Fact]
		public void Reader_keeps_one_closed_square_and_modal_lines()
		{
			string gcode = @"
G90
G0 X0 Y0
M3 S0
S255
G1 X10 Y0 F1000
X10 Y10
X0 Y10
X0 Y0
S0
M5";
			List<PlotPath> paths = GCodePathReader.Read(gcode.Split('\n'));
			Assert.Single(paths);
			Assert.True(paths[0].Closed);
			Assert.Equal(4, paths[0].Points.Count);
			Assert.True(Near(paths[0].Points[1], 10, 0));
			Assert.True(Near(paths[0].Points[2], 10, 10));
		}

		[Fact]
		public void Reader_linearizes_quarter_arc()
		{
			string gcode = "G0 X10 Y0\nM3 S100\nG3 X0 Y10 I-10 J0\nS0\n";
			List<PlotPath> paths = GCodePathReader.Read(gcode.Split('\n'));
			Assert.Single(paths);
			Assert.True(paths[0].Points.Count > 4);
			Assert.True(Near(paths[0].Points[paths[0].Points.Count - 1], 0, 10));
		}

		[Fact]
		public void Inner_contour_is_cut_before_the_outline()
		{
			PlotPath outer = Rect(0, 0, 100, 100);
			PlotPath inner = Rect(40, 40, 60, 60);
			PlotterConfig cfg = Knife();
			cfg.OptimizeOrder = true;
			cfg.InsideFirst = true;
			List<PlotPath> ordered = PathOptimizer.Prepare(new[] { outer, inner }, cfg);

			Assert.Equal(2, ordered.Count);
			Assert.InRange(ordered[0].Points[0].X, 40, 60);
			Assert.InRange(ordered[0].Points[0].Y, 40, 60);
			Assert.Equal(0, ordered[1].Points.Min(p => p.X), 3);
		}

		[Fact]
		public void Pipeline_emits_servo_and_corner_arc()
		{
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 1;
			cfg.LeadInMm = 1;
			cfg.OvercutMm = 1;
			cfg.CutSpeed = 800;
			cfg.PenMode = PlotterPenMode.ServoPwm;
			cfg.ServoUp = 0;
			cfg.ServoDown = 255;
			string source = "G0 X0 Y0\nM3 S0\nS255\nG1 X10 Y0\nG1 X10 Y10\nG1 X0 Y10\nG1 X0 Y0\nS0\n";
			string result = PlotterPipeline.Convert(source, cfg);

			Assert.Contains("G90", result);
			Assert.Contains("G3", result);
			Assert.Contains("M3 S255", result);
			Assert.Contains("F800", result);
			Assert.Contains("X11", result);

			List<PlotPath> again = GCodePathReader.Read(result.Split('\n'));
			Assert.Single(again);
		}

		[Fact]
		public void Straight_bezier_is_not_subdivided()
		{
			List<PlotPoint> pts = new List<PlotPoint>();
			PlotGeom.AppendCubic(pts, new PlotPoint(0, 0), new PlotPoint(3, 0), new PlotPoint(7, 0), new PlotPoint(10, 0), 0.05);
			Assert.Equal(2, pts.Count);
			Assert.True(Near(pts[0], 0, 0));
			Assert.True(Near(pts[1], 10, 0));
		}

		[Fact]
		public void Empty_program_still_lifts_the_knife()
		{
			string result = PlotterPipeline.Convert("", Knife());
			Assert.Contains("M3 S0", result);
			Assert.Contains("paths 0", result);
		}

		private static PlotterConfig Knife()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.OptimizeOrder = false;
			cfg.InsideFirst = false;
			cfg.Passes = 1;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.LiftAngleDeg = 0;
			return cfg;
		}

		private static PlotPath Square()
		{
			return Rect(0, 0, 10, 10);
		}

		private static PlotPath Rect(double x0, double y0, double x1, double y1)
		{
			PlotPath path = new PlotPath();
			path.Closed = true;
			path.Points.Add(new PlotPoint(x0, y0));
			path.Points.Add(new PlotPoint(x1, y0));
			path.Points.Add(new PlotPoint(x1, y1));
			path.Points.Add(new PlotPoint(x0, y1));
			return path;
		}

		private static bool Near(PlotMove move, double x, double y)
		{
			return Near(new PlotPoint(move.X, move.Y), x, y);
		}

		private static bool Near(PlotPoint point, double x, double y)
		{
			return Math.Abs(point.X - x) < 0.02 && Math.Abs(point.Y - y) < 0.02;
		}

		private static double Hypot(double x, double y)
		{
			return Math.Sqrt(x * x + y * y);
		}
	}
}
