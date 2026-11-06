// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;
using System.Text;

namespace LaserGRBL.Plotter
{
	public static class PlotterGCodeWriter
	{
		public static string Write(IList<PlotMove> moves, PlotterConfig cfg, int pathCount)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("(LaserGRBL plotter drag-knife)\r\n");
			sb.Append("(preview is the swivel axis; the blade tip follows the design)\r\n");
			sb.Append("(blade ").Append(Safe(cfg.BladeName));
			sb.Append(" offset ").Append(PlotGeom.Num(cfg.BladeOffsetMm));
			sb.Append(" mm overcut ").Append(PlotGeom.Num(cfg.OvercutMm));
			sb.Append(" lead-in ").Append(PlotGeom.Num(cfg.LeadInMm)).Append(")\r\n");
			sb.Append("(cut F").Append(cfg.CutSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
			sb.Append(" travel ");
			if (cfg.PenMode == PlotterPenMode.Manual && cfg.TravelSpeed <= 0) sb.Append("cut feed");
			else if (cfg.TravelSpeed <= 0) sb.Append("rapid");
			else sb.Append("F").Append(cfg.TravelSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
			sb.Append(" paths ").Append(pathCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
			sb.Append(" pen ").Append(cfg.PenMode.ToString()).Append(")\r\n");
			if (cfg.PenMode == PlotterPenMode.ZAxis && cfg.ZLift == PlotterZLift.Cam)
			{
				sb.Append("(z cam, turn Z").Append(PlotGeom.Num(cfg.CamDown));
				sb.Append(" pushes the head down, Z").Append(PlotGeom.Num(cfg.CamUp));
				sb.Append(" releases and the springs lift)\r\n");
			}
			else if (cfg.PenMode == PlotterPenMode.ZAxis)
			{
				sb.Append("(z standard, up Z").Append(PlotGeom.Num(cfg.PenUpZ));
				sb.Append(" down Z").Append(PlotGeom.Num(cfg.PenDownZ)).Append(")\r\n");
			}
			if (cfg.PenMode == PlotterPenMode.Manual)
			{
				sb.Append("(manual knife height, blade stays down)\r\n");
				sb.Append("(every XY move is a cut; the approach does not cross a contour)\r\n");
			}
			if (cfg.StartMarginMm > 0 || System.Math.Abs(cfg.LaserOffsetX) > 0.0001 || System.Math.Abs(cfg.LaserOffsetY) > 0.0001)
			{
				sb.Append("(zero XY with the positioning laser on the sheet bottom-left)\r\n");
				sb.Append("(blade parks X").Append(PlotGeom.Num(cfg.LaserOffsetX - cfg.StartMarginMm));
				sb.Append(" Y").Append(PlotGeom.Num(cfg.LaserOffsetY - cfg.StartMarginMm));
				sb.Append(" then cuts into the drawing)\r\n");
			}
			sb.Append("G90\r\n");

			bool manual = cfg.KnifeStaysDown;
			bool down = false;
			if (!manual)
				PenUp(sb, cfg);
			double x = double.NaN;
			double y = double.NaN;
			double cutLength = 0;

			if (moves != null)
			{
				for (int i = 0; i < moves.Count; i++)
				{
					PlotMove move = moves[i];
					if (!move.Cut)
					{
						if (down && !manual)
						{
							PenUp(sb, cfg);
							down = false;
						}
						if (double.IsNaN(x) || Math.Abs(x - move.X) > 0.0001 || Math.Abs(y - move.Y) > 0.0001)
						{
							Rapid(sb, cfg, move.X, move.Y);
							x = move.X;
							y = move.Y;
						}
						continue;
					}

					if (!down && !manual)
					{
						PenDown(sb, cfg);
						down = true;
					}

					if (move.Arc && !cfg.LinearizeArcs)
					{
						sb.Append(move.Clockwise ? "G2" : "G3");
						sb.Append(" X").Append(PlotGeom.Num(move.X));
						sb.Append(" Y").Append(PlotGeom.Num(move.Y));
						sb.Append(" I").Append(PlotGeom.Num(move.I));
						sb.Append(" J").Append(PlotGeom.Num(move.J));
						sb.Append(" F").Append(cfg.CutSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
						sb.Append("\r\n");
						if (!double.IsNaN(x))
							cutLength += PlotGeom.Dist(new PlotPoint(x, y), new PlotPoint(move.X, move.Y));
						x = move.X;
						y = move.Y;
					}
					else if (move.Arc)
					{
						List<PlotPoint> samples = new List<PlotPoint>();
						PlotPoint start = new PlotPoint(x, y);
						PlotGeom.SampleArc(samples, start, new PlotPoint(move.X, move.Y), move.I, move.J, move.Clockwise, cfg.ArcStepDeg);
						for (int s = 0; s < samples.Count; s++)
						{
							cutLength += PlotGeom.Dist(new PlotPoint(x, y), samples[s]);
							CutLine(sb, cfg, samples[s].X, samples[s].Y);
							x = samples[s].X;
							y = samples[s].Y;
						}
					}
					else
					{
						if (!double.IsNaN(x))
							cutLength += PlotGeom.Dist(new PlotPoint(x, y), new PlotPoint(move.X, move.Y));
						CutLine(sb, cfg, move.X, move.Y);
						x = move.X;
						y = move.Y;
					}
				}
			}

			if (down && !manual)
				PenUp(sb, cfg);

			sb.Append("(cut length mm ").Append(PlotGeom.Num(cutLength)).Append(")\r\n");
			return sb.ToString();
		}

		private static void Rapid(StringBuilder sb, PlotterConfig cfg, double x, double y)
		{
			// The knife is already in the material, so a rapid would still cut.
			// Write the approach at feed rate and let the path planner keep it outside the part.
			if (cfg.KnifeStaysDown)
			{
				int stayDown = cfg.TravelSpeed > 0 ? cfg.TravelSpeed : cfg.CutSpeed;
				if (stayDown <= 0)
					stayDown = 1;
				sb.Append("G1 X").Append(PlotGeom.Num(x)).Append(" Y").Append(PlotGeom.Num(y));
				sb.Append(" F").Append(stayDown.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
				return;
			}

			int feed = cfg.TravelSpeed;
			if (feed <= 0)
			{
				sb.Append("G0 X").Append(PlotGeom.Num(x)).Append(" Y").Append(PlotGeom.Num(y)).Append("\r\n");
			}
			else
			{
				sb.Append("G1 X").Append(PlotGeom.Num(x)).Append(" Y").Append(PlotGeom.Num(y));
				sb.Append(" F").Append(feed.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
			}
		}

		private static void CutLine(StringBuilder sb, PlotterConfig cfg, double x, double y)
		{
			sb.Append("G1 X").Append(PlotGeom.Num(x)).Append(" Y").Append(PlotGeom.Num(y));
			sb.Append(" F").Append(cfg.CutSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
		}

		private static void PenUp(StringBuilder sb, PlotterConfig cfg)
		{
			if (cfg.KnifeStaysDown)
				return;
			if (cfg.PenMode == PlotterPenMode.ZAxis && cfg.ZLift == PlotterZLift.Cam)
			{
				sb.Append("G1 Z").Append(PlotGeom.Num(cfg.CamUp));
				sb.Append(" F").Append(cfg.PlungeSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
			}
			else if (cfg.PenMode == PlotterPenMode.ZAxis)
				sb.Append("G0 Z").Append(PlotGeom.Num(cfg.PenUpZ)).Append("\r\n");
			else if (cfg.PenMode == PlotterPenMode.Digital)
				sb.Append("M5 S0\r\n");
			else
				sb.Append("M3 S").Append(cfg.ServoUp.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
			Dwell(sb, cfg.DwellUpMs);
		}

		private static void PenDown(StringBuilder sb, PlotterConfig cfg)
		{
			if (cfg.KnifeStaysDown)
				return;
			if (cfg.PenMode == PlotterPenMode.ZAxis)
			{
				double z = cfg.ZLift == PlotterZLift.Cam ? cfg.CamDown : cfg.PenDownZ;
				sb.Append("G1 Z").Append(PlotGeom.Num(z));
				sb.Append(" F").Append(cfg.PlungeSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
			}
			else if (cfg.PenMode == PlotterPenMode.Digital)
			{
				int s = cfg.ServoDown > 0 ? cfg.ServoDown : 255;
				sb.Append("M3 S").Append(s.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
			}
			else
			{
				sb.Append("M3 S").Append(cfg.ServoDown.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
			}
			Dwell(sb, cfg.DwellDownMs);
		}

		private static void Dwell(StringBuilder sb, int milliseconds)
		{
			if (milliseconds <= 0)
				return;
			double seconds = milliseconds / 1000.0;
			sb.Append("G4 P").Append(PlotGeom.Num(seconds)).Append("\r\n");
		}

		private static string Safe(string name)
		{
			if (string.IsNullOrEmpty(name))
				return "blade";
			return name.Replace('(', ' ').Replace(')', ' ');
		}
	}
}
