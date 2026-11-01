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
		public void Fillet_follows_the_tangent_without_steps()
		{
			PlotPath fillet = new PlotPath();
			fillet.Closed = false;
			int n = 8;
			double radius = 40;
			for (int i = 0; i <= n; i++)
			{
				double t = (Math.PI / 2) * i / n;
				fillet.Points.Add(new PlotPoint(radius * Math.Cos(t), radius * Math.Sin(t)));
			}

			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 3.6;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.CornerThresholdDeg = 0;
			List<PlotMove> moves = DragKnife.Build(new[] { fillet }, cfg);

			Assert.DoesNotContain(moves, m => m.Arc);
			double worst = MaxCutTurn(moves);
			Assert.True(worst < 25, worst.ToString(System.Globalization.CultureInfo.InvariantCulture));
			for (int i = 0; i < moves.Count; i++)
			{
				double r = Hypot(moves[i].X, moves[i].Y);
				Assert.True(r > radius - 0.05, r.ToString(System.Globalization.CultureInfo.InvariantCulture));
			}
		}

		[Fact]
		public void Closed_circle_stays_on_the_offset_curve()
		{
			double radius = 15;
			int n = 36;
			PlotPath circle = new PlotPath();
			circle.Closed = true;
			for (int i = 0; i < n; i++)
			{
				double t = 2 * Math.PI * i / n;
				circle.Points.Add(new PlotPoint(radius * Math.Cos(t), radius * Math.Sin(t)));
			}

			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 3.6;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.CornerThresholdDeg = 0;
			List<PlotMove> moves = DragKnife.Build(new[] { circle }, cfg);

			Assert.DoesNotContain(moves, m => m.Arc);
			double expect = Math.Sqrt(radius * radius + 3.6 * 3.6);
			for (int i = 0; i < moves.Count; i++)
			{
				double r = Hypot(moves[i].X, moves[i].Y);
				Assert.True(Math.Abs(r - expect) < 0.2, r.ToString(System.Globalization.CultureInfo.InvariantCulture));
			}
			double worst = MaxCutTurn(moves);
			Assert.True(worst < 20, worst.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		[Fact]
		public void Rounded_rectangle_keeps_fillets_and_swivels_nothing()
		{
			PlotPath shape = RoundedRect(0, 0, 50, 40, 10, 8);
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 3.6;
			cfg.LeadInMm = 3.6;
			cfg.OvercutMm = 3.6;
			cfg.CornerThresholdDeg = 8;
			List<PlotMove> moves = DragKnife.Build(new[] { shape }, cfg);

			Assert.DoesNotContain(moves, m => m.Arc);
			double worst = MaxCutTurn(moves);
			Assert.True(worst < 25, worst.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		[Fact]
		public void Idle_moves_with_blade_offset_do_not_cut_rectangles()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.BladeOffsetMm = 3.6;
			cfg.LeadInMm = 3.6;
			cfg.OvercutMm = 3.6;
			cfg.CornerThresholdDeg = 8;
			cfg.OptimizeOrder = true;
			cfg.InsideFirst = true;
			cfg.StartMarginMm = 0;
			cfg.LaserOffsetX = 0;
			cfg.LaserOffsetY = 0;

			List<PlotPath> walls = new List<PlotPath>();
			for (int row = 0; row < 5; row++)
			{
				for (int col = 0; col < 6; col++)
				{
					double x = 20 + col * 36;
					double y = 20 + row * 32;
					walls.Add(Rect(x, y, x + 18, y + 14));
				}
			}

			string gcode = PlotterPipeline.ConvertPaths(walls, cfg);
			Assert.Contains("G3", gcode);
			List<PlotPoint> motion = MotionPoints(gcode);
			PlotPoint prev = new PlotPoint(0, 0);
			for (int i = 0; i < motion.Count; i++)
			{
				for (int w = 0; w < walls.Count; w++)
				{
					Assert.False(
						CutsOpenInterior(prev, motion[i], walls[w].Points),
						prev.X + "," + prev.Y + " -> " + motion[i].X + "," + motion[i].Y);
				}
				prev = motion[i];
			}
		}

		[Fact]
		public void Short_square_still_swivels_sharp_corners()
		{
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 3.6;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.CornerThresholdDeg = 8;
			List<PlotMove> moves = DragKnife.Build(new[] { Rect(0, 0, 4, 4) }, cfg);

			Assert.Contains(moves, m => m.Arc);
			PlotMove arc = moves.First(m => m.Arc);
			Assert.Equal(3.6, Hypot(arc.I, arc.J), 2);
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
		public void Narrow_gaps_are_not_cut_when_the_knife_changes_contour()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.BladeOffsetMm = 3.6;
			cfg.LeadInMm = 3.6;
			cfg.OvercutMm = 3.6;
			cfg.CornerThresholdDeg = 8;
			cfg.RouteMode = PlotterRouteMode.Nearest;
			cfg.StartMarginMm = 0;
			cfg.LaserOffsetX = 0;
			cfg.LaserOffsetY = 0;

			List<PlotPath> walls = new List<PlotPath>();
			for (int row = 0; row < 4; row++)
			{
				for (int col = 0; col < 5; col++)
				{
					double x = 20 + col * 16;
					double y = 20 + row * 14;
					walls.Add(Rect(x, y, x + 12, y + 10));
				}
			}

			string gcode = PlotterPipeline.ConvertPaths(walls, cfg);
			List<PlotPoint> motion = MotionPoints(gcode);
			PlotPoint prev = new PlotPoint(0, 0);
			for (int i = 0; i < motion.Count; i++)
			{
				for (int w = 0; w < walls.Count; w++)
				{
					Assert.False(
						CutsOpenInterior(prev, motion[i], walls[w].Points),
						prev.X + "," + prev.Y + " -> " + motion[i].X + "," + motion[i].Y);
				}
				prev = motion[i];
			}
		}

		[Fact]
		public void Outside_in_starts_at_the_sheet_edge()
		{
			PlotPath left = Rect(0, 0, 10, 40);
			PlotPath right = Rect(90, 0, 100, 40);
			PlotPath center = Rect(45, 18, 55, 28);
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 0;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.PenMode = PlotterPenMode.Manual;
			cfg.RouteMode = PlotterRouteMode.OutsideIn;

			List<PlotPath> ordered = PathOptimizer.Prepare(new[] { center, left, right }, cfg, new PlotPoint(50, 2));
			double first = ordered[0].Points.Min(p => p.X);
			Assert.True(first < 15 || first > 85, first.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		[Fact]
		public void Nearest_starts_at_the_closest_contour()
		{
			PlotPath left = Rect(0, 0, 10, 40);
			PlotPath right = Rect(90, 0, 100, 40);
			PlotPath center = Rect(45, 18, 55, 28);
			PlotterConfig cfg = Knife();
			cfg.BladeOffsetMm = 0;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.PenMode = PlotterPenMode.Manual;
			cfg.RouteMode = PlotterRouteMode.Nearest;

			List<PlotPath> ordered = PathOptimizer.Prepare(new[] { center, left, right }, cfg, new PlotPoint(50, 2));
			double first = ordered[0].Points.Min(p => p.X);
			Assert.InRange(first, 40, 60);
		}

		[Fact]
		public void Route_threads_narrow_gaps_between_many_rectangles()
		{
			List<PlotPath> walls = new List<PlotPath>();
			for (int row = 0; row < 5; row++)
			{
				for (int col = 0; col < 6; col++)
				{
					double x = 12 + col * 14;
					double y = 12 + row * 12;
					walls.Add(Rect(x, y, x + 12, y + 10));
				}
			}

			PlotPoint from = new PlotPoint(0, 0);
			PlotPoint to = new PlotPoint(12 + 3 * 14, 12 + 2 * 12);
			List<PlotPoint> route = SafeRoute.Route(from, to, walls, 3.6);
			Assert.True(PlotGeom.Dist(route[route.Count - 1], to) < 0.05);

			PlotPoint prev = from;
			for (int i = 0; i < route.Count; i++)
			{
				for (int w = 0; w < walls.Count; w++)
					Assert.False(PlotGeom.SegmentThroughInterior(prev, route[i], walls[w].Points),
						prev.X + "," + prev.Y + " -> " + route[i].X + "," + route[i].Y);
				prev = route[i];
			}
		}

		[Fact]
		public void Inner_contour_is_cut_before_the_outline()
		{
			PlotPath outer = Rect(0, 0, 100, 100);
			PlotPath inner = Rect(40, 40, 60, 60);
			PlotterConfig cfg = Knife();
			cfg.RouteMode = PlotterRouteMode.InsideOut;
			cfg.PenMode = PlotterPenMode.ServoPwm;
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

		[Fact]
		public void Manual_approach_enters_at_the_near_corner()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.BladeOffsetMm = 3.6;
			cfg.LeadInMm = 3.6;
			cfg.OvercutMm = 3.6;
			cfg.CutSpeed = 600;
			cfg.TravelSpeed = 0;
			cfg.OptimizeOrder = true;
			cfg.InsideFirst = true;
			cfg.StartMarginMm = 0;
			cfg.LaserOffsetX = 0;
			cfg.LaserOffsetY = 0;

			PlotPath square = new PlotPath();
			square.Closed = true;
			square.Points.Add(new PlotPoint(30, 30));
			square.Points.Add(new PlotPoint(10, 30));
			square.Points.Add(new PlotPoint(10, 10));
			square.Points.Add(new PlotPoint(30, 10));

			string gcode = PlotterPipeline.ConvertPaths(new[] { square }, cfg);
			Assert.DoesNotContain("G0", gcode);

			PlotPoint first = FirstMove(gcode);
			Assert.True(PlotGeom.Dist(first, new PlotPoint(10, 10)) < 0.05, first.X + "," + first.Y);
			Assert.False(PlotGeom.SegmentThroughInterior(new PlotPoint(0, 0), first, square.Points));
		}

		[Fact]
		public void Manual_approach_does_not_cross_the_outline_to_reach_a_hole()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.BladeOffsetMm = 0;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.CornerThresholdDeg = 180;
			cfg.OptimizeOrder = true;
			cfg.InsideFirst = true;
			cfg.StartMarginMm = 0;
			cfg.LaserOffsetX = 0;
			cfg.LaserOffsetY = 0;

			PlotPath outer = Rect(20, 20, 80, 80);
			PlotPath inner = Rect(40, 40, 60, 60);
			string gcode = PlotterPipeline.ConvertPaths(new[] { outer, inner }, cfg);
			PlotPoint first = FirstMove(gcode);
			Assert.False(PlotGeom.SegmentThroughInterior(new PlotPoint(0, 0), first, outer.Points));
			Assert.True(PlotGeom.Dist(first, new PlotPoint(20, 20)) < 0.05, first.X + "," + first.Y);
		}

		[Fact]
		public void Blade_parks_outside_the_corner_marked_by_the_laser()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.BladeOffsetMm = 0;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.CornerThresholdDeg = 180;
			cfg.StartMarginMm = 20;
			cfg.LaserOffsetX = 10;
			cfg.LaserOffsetY = 0;

			PlotPath square = Rect(5, 8, 40, 50);
			string gcode = PlotterPipeline.ConvertPaths(new[] { square }, cfg);
			List<PlotPoint> motion = MotionPoints(gcode);

			Assert.True(motion.Count >= 2);
			Assert.True(Math.Abs(motion[0].X - (-10)) < 0.05, motion[0].X + "," + motion[0].Y);
			Assert.True(Math.Abs(motion[0].Y - (-20)) < 0.05, motion[0].X + "," + motion[0].Y);
			Assert.True(Math.Abs(motion[1].X - 10) < 0.05, motion[1].X + "," + motion[1].Y);
			Assert.True(Math.Abs(motion[1].Y) < 0.05, motion[1].X + "," + motion[1].Y);
			Assert.DoesNotContain("G0", gcode);

			List<PlotPoint> onSheet = new List<PlotPoint>();
			onSheet.Add(new PlotPoint(10, 0));
			onSheet.Add(new PlotPoint(45, 0));
			onSheet.Add(new PlotPoint(45, 42));
			onSheet.Add(new PlotPoint(10, 42));
			Assert.False(PlotGeom.SegmentThroughInterior(motion[0], motion[1], onSheet));
		}

		[Fact]
		public void Route_walks_around_a_blocking_square()
		{
			PlotPath wall = Rect(10, 10, 20, 20);
			List<PlotPoint> route = SafeRoute.Route(new PlotPoint(0, 0), new PlotPoint(30, 30), new[] { wall }, 1);
			Assert.True(PlotGeom.Dist(route[route.Count - 1], new PlotPoint(30, 30)) < 0.001);
			PlotPoint prev = new PlotPoint(0, 0);
			for (int i = 0; i < route.Count; i++)
			{
				Assert.False(PlotGeom.SegmentThroughInterior(prev, route[i], wall.Points));
				prev = route[i];
			}
		}

		[Fact]
		public void Route_across_many_rectangles_stays_outside_them()
		{
			List<PlotPath> walls = new List<PlotPath>();
			for (int row = 0; row < 5; row++)
			{
				for (int col = 0; col < 6; col++)
				{
					double x = 15 + col * 30;
					double y = 15 + row * 26;
					walls.Add(Rect(x, y, x + 16, y + 12));
				}
			}

			PlotPoint from = new PlotPoint(0, 0);
			PlotPoint to = new PlotPoint(15 + 5 * 30, 15 + 4 * 26);
			List<PlotPoint> route = SafeRoute.Route(from, to, walls, 2);
			Assert.True(PlotGeom.Dist(route[route.Count - 1], to) < 0.05);

			PlotPoint prev = from;
			for (int i = 0; i < route.Count; i++)
			{
				for (int w = 0; w < walls.Count; w++)
					Assert.False(PlotGeom.SegmentThroughInterior(prev, route[i], walls[w].Points));
				prev = route[i];
			}
		}

		[Fact]
		public void Idle_moves_between_many_rectangles_do_not_cut_them()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.BladeOffsetMm = 0;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.CornerThresholdDeg = 180;
			cfg.OptimizeOrder = true;
			cfg.InsideFirst = true;
			cfg.StartMarginMm = 0;
			cfg.LaserOffsetX = 0;
			cfg.LaserOffsetY = 0;

			List<PlotPath> walls = new List<PlotPath>();
			for (int row = 0; row < 5; row++)
			{
				for (int col = 0; col < 6; col++)
				{
					double x = 20 + col * 36;
					double y = 20 + row * 32;
					walls.Add(Rect(x, y, x + 18, y + 14));
				}
			}

			string gcode = PlotterPipeline.ConvertPaths(walls, cfg);
			Assert.DoesNotContain("G0", gcode);
			List<PlotPoint> motion = MotionPoints(gcode);
			PlotPoint prev = new PlotPoint(0, 0);
			for (int i = 0; i < motion.Count; i++)
			{
				for (int w = 0; w < walls.Count; w++)
				{
					Assert.False(
						CutsOpenInterior(prev, motion[i], walls[w].Points),
						prev.X + "," + prev.Y + " -> " + motion[i].X + "," + motion[i].Y);
				}
				prev = motion[i];
			}
		}

		[Fact]
		public void Dxf_reads_line_polyline_circle_bulge_and_inches()
		{
			string dxf = "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n"
				+ "0\nLINE\n10\n0\n20\n0\n11\n10\n21\n0\n"
				+ "0\nLWPOLYLINE\n90\n4\n70\n1\n10\n0\n20\n0\n10\n5\n20\n0\n10\n5\n20\n5\n10\n0\n20\n5\n"
				+ "0\nLWPOLYLINE\n90\n2\n70\n0\n10\n0\n20\n0\n42\n1\n10\n2\n20\n0\n"
				+ "0\nCIRCLE\n10\n10\n20\n10\n40\n2\n"
				+ "0\nENDSEC\n0\nEOF\n";
			List<PlotPath> paths = DxfReader.Read(dxf);
			Assert.Contains(paths, p => p.Closed && p.Points.Count == 4 && p.Points.Any(pt => Math.Abs(pt.X - 5) < 0.01 && Math.Abs(pt.Y - 5) < 0.01));
			Assert.Contains(paths, p => p.Closed && p.Points.Count > 8);
			Assert.Contains(paths, p => !p.Closed && p.Points.Any(pt => Math.Abs(pt.Y) > 0.5));

			string arc = "0\nSECTION\n2\nENTITIES\n0\nARC\n10\n0\n20\n0\n40\n10\n50\n0\n51\n90\n0\nENDSEC\n0\nEOF\n";
			List<PlotPath> arcPaths = DxfReader.Read(arc);
			Assert.Single(arcPaths);
			Assert.True(Math.Abs(arcPaths[0].Points[0].X - 10) < 0.05);
			Assert.True(Math.Abs(arcPaths[0].Points[0].Y) < 0.05);
			Assert.True(Math.Abs(arcPaths[0].Points[arcPaths[0].Points.Count - 1].X) < 0.05);
			Assert.True(Math.Abs(arcPaths[0].Points[arcPaths[0].Points.Count - 1].Y - 10) < 0.05);

			string inches = "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n1\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n"
				+ "0\nLINE\n10\n0\n20\n0\n11\n1\n21\n0\n0\nENDSEC\n0\nEOF\n";
			List<PlotPath> inchPaths = DxfReader.Read(inches);
			Assert.Single(inchPaths);
			Assert.True(Math.Abs(inchPaths[0].Points[1].X - 25.4) < 0.01);

			string block = "0\nSECTION\n2\nBLOCKS\n0\nBLOCK\n2\nSLOT\n10\n0\n20\n0\n"
				+ "0\nLINE\n10\n0\n20\n0\n11\n4\n21\n0\n0\nENDBLK\n0\nENDSEC\n"
				+ "0\nSECTION\n2\nENTITIES\n0\nINSERT\n2\nSLOT\n10\n5\n20\n6\n50\n0\n0\nENDSEC\n0\nEOF\n";
			List<PlotPath> inserted = DxfReader.Read(block);
			Assert.Single(inserted);
			Assert.True(Math.Abs(inserted[0].Points[0].X - 5) < 0.01);
			Assert.True(Math.Abs(inserted[0].Points[0].Y - 6) < 0.01);
			Assert.True(Math.Abs(inserted[0].Points[1].X - 9) < 0.01);
		}

		[Fact]
		public void Manual_height_emits_xy_only_at_cut_speed()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			Assert.Equal(PlotterPenMode.Manual, cfg.PenMode);
			Assert.Equal(3.6, cfg.BladeOffsetMm, 5);
			Assert.Equal(1, cfg.Passes);
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.OptimizeOrder = false;
			cfg.InsideFirst = false;
			cfg.CutSpeed = 600;
			cfg.TravelSpeed = 0;
			cfg.LiftAngleDeg = 150;

			string source = "G0 X0 Y0\nM3 S0\nS255\nG1 X10 Y0\nG1 X10 Y10\nG1 X0 Y10\nG1 X0 Y0\nS0\n";
			string result = PlotterPipeline.Convert(source, cfg);

			Assert.Contains("F600", result);
			Assert.DoesNotContain("M3", result);
			Assert.DoesNotContain("M5", result);
			Assert.DoesNotContain("G0 ", result);
			Assert.DoesNotContain(" Z", result);
			Assert.Contains("G3", result);
		}

		private static PlotPoint FirstMove(string gcode)
		{
			return MotionPoints(gcode)[0];
		}

		private static List<PlotPoint> MotionPoints(string gcode)
		{
			List<PlotPoint> points = new List<PlotPoint>();
			string[] lines = gcode.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.StartsWith("G0") || line.StartsWith("G1") || line.StartsWith("G2") || line.StartsWith("G3"))
					points.Add(new PlotPoint(Word(line, 'X'), Word(line, 'Y')));
			}
			if (points.Count == 0)
				throw new InvalidOperationException("no motion");
			return points;
		}

		private static double Word(string line, char axis)
		{
			int at = line.IndexOf(axis);
			if (at < 0)
				return 0;
			int end = at + 1;
			while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' || line[end] == '-' || line[end] == '+'))
				end++;
			return double.Parse(line.Substring(at + 1, end - at - 1), System.Globalization.CultureInfo.InvariantCulture);
		}

		private static PlotterConfig Knife()
		{
			PlotterConfig cfg = PlotterConfig.CreateDefault();
			cfg.PenMode = PlotterPenMode.ServoPwm;
			cfg.ServoUp = 0;
			cfg.ServoDown = 255;
			cfg.SimplifyMm = 0;
			cfg.MinSegmentMm = 0;
			cfg.OptimizeOrder = false;
			cfg.InsideFirst = false;
			cfg.Passes = 1;
			cfg.StartMarginMm = 0;
			cfg.LaserOffsetX = 0;
			cfg.LaserOffsetY = 0;
			cfg.LeadInMm = 0;
			cfg.OvercutMm = 0;
			cfg.LiftAngleDeg = 0;
			return cfg;
		}

		private static PlotPath Square()
		{
			return Rect(0, 0, 10, 10);
		}

		private static PlotPath RoundedRect(double x, double y, double width, double height, double radius, int steps)
		{
			PlotPath path = new PlotPath();
			path.Closed = true;
			AddCorner(path, x + width - radius, y + radius, radius, -Math.PI / 2, 0, steps);
			AddCorner(path, x + width - radius, y + height - radius, radius, 0, Math.PI / 2, steps);
			AddCorner(path, x + radius, y + height - radius, radius, Math.PI / 2, Math.PI, steps);
			AddCorner(path, x + radius, y + radius, radius, Math.PI, Math.PI * 1.5, steps);
			return path;
		}

		private static void AddCorner(PlotPath path, double cx, double cy, double radius, double from, double to, int steps)
		{
			for (int i = 0; i <= steps; i++)
			{
				double t = from + (to - from) * i / steps;
				PlotPoint point = new PlotPoint(cx + radius * Math.Cos(t), cy + radius * Math.Sin(t));
				if (path.Points.Count > 0 && PlotGeom.Dist(path.Points[path.Points.Count - 1], point) <= 0.0001)
					continue;
				path.Points.Add(point);
			}
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

		private static bool CutsOpenInterior(PlotPoint a, PlotPoint b, IList<PlotPoint> poly)
		{
			for (int sample = 1; sample <= 9; sample++)
			{
				double t = sample / 10.0;
				PlotPoint point = new PlotPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
				if (!PlotGeom.PointInPoly(point, poly))
					continue;
				double nearest = double.MaxValue;
				for (int i = 0; i < poly.Count; i++)
				{
					double dist = PlotGeom.DistPointSegment(point, poly[i], poly[(i + 1) % poly.Count]);
					if (dist < nearest)
						nearest = dist;
				}
				if (nearest > 0.3)
					return true;
			}
			return false;
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

		private static double MaxCutTurn(List<PlotMove> moves)
		{
			double worst = 0;
			PlotMove a = null;
			PlotMove b = null;
			for (int i = 0; i < moves.Count; i++)
			{
				if (!moves[i].Cut || moves[i].Arc)
				{
					a = null;
					b = null;
					continue;
				}
				if (a != null && b != null)
				{
					double h1 = Math.Atan2(b.Y - a.Y, b.X - a.X);
					double h2 = Math.Atan2(moves[i].Y - b.Y, moves[i].X - b.X);
					double d = h2 - h1;
					while (d > Math.PI)
						d -= Math.PI * 2;
					while (d < -Math.PI)
						d += Math.PI * 2;
					d = Math.Abs(d * 180.0 / Math.PI);
					if (d > worst)
						worst = d;
				}
				a = b;
				b = moves[i];
			}
			return worst;
		}
	}
}
