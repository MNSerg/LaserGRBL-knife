//Copyright (c) 2016-2021 Diego Settimi - https://github.com/arkypita/

// This program is free software; you can redistribute it and/or modify  it under the terms of the GPLv3 General Public License as published by  the Free Software Foundation; either version 3 of the License, or (at  your option) any later version.
// This program is distributed in the hope that it will be useful, but  WITHOUT ANY WARRANTY; without even the implied warranty of  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GPLv3  General Public License for more details.
// You should have received a copy of the GPLv3 General Public License  along with this program; if not, write to the Free Software  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307,  USA. using System;

using System;
using System.Collections.Generic;
using CsPotrace.BezierToBiarc;
using System.Collections;
using CsPotrace;
using System.Drawing;

namespace CsPotrace
{
	/// <summary>
	/// Description of CsPotraceExportGCODE.
	/// </summary>
	public partial class Potrace
	{

		/// <summary>
		/// Exports a figure, created by Potrace from a Bitmap to a svg-formatted string
		/// </summary>
		/// <param name="list">Arraylist, which contains vectorinformations about the Curves</param>
		/// <param name="Width">Width of the exportd cvg-File</param>
		/// <param name="Height">Height of the exportd cvg-File</param>
		/// <returns></returns>
		public static List<string> Export2GCode(List<List<CsPotrace.Curve>> list, float oX, float oY, double scale, string lOn, string lOff, Size originalImageSize, string skipcmd)
		{
			bool debug = false;

			Bitmap bmp = null;
			System.Drawing.Graphics g = null;

			if (debug)
			{
				bmp = new Bitmap(originalImageSize.Width, originalImageSize.Height);
				g = System.Drawing.Graphics.FromImage(bmp);
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
				g.Clear(System.Drawing.Color.White);
			}

			List<string> rv = new List<string>();
			double oxs = oX * scale;
			double oys = oY * scale;

			bool continuous = LaserGRBL.Plotter.PlotterSettings.UseContinuousCut;
			plotterFirstApproach = true;
			if (continuous && list != null && list.Count > 0)
			{
				// AlwaysDown knife cuts on G0 too — enter from outside the artwork bbox
				double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
				foreach (List<CsPotrace.Curve> Curves in list)
				{
					if (Curves == null) continue;
					foreach (CsPotrace.Curve c in Curves)
					{
						ExpandBBox(c.A.X + oxs, c.A.Y + oys, scale, ref minX, ref minY, ref maxX, ref maxY);
						ExpandBBox(c.B.X + oxs, c.B.Y + oys, scale, ref minX, ref minY, ref maxX, ref maxY);
					}
				}
				plotterBBoxMinX = minX;
				plotterBBoxValid = minX < maxX;

				// Rotate first contour so we start at its leftmost vertex —
				// horizontal entry from the left then cannot cross the interior.
				RotateClosedPathToLeftmost(list[0]);
			}
			else
				plotterBBoxValid = false;

			foreach (List<CsPotrace.Curve> Curves in list)
				rv.AddRange(GetPathGC(Curves, lOn, lOff, oxs, oys, scale, g, skipcmd));

			if (debug)
			{
				bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
				bmp.Save("preview.png");
				g.Dispose();
				bmp.Dispose();
			}
			return rv;
		}

		private static void ExpandBBox(double x, double y, double scale, ref double minX, ref double minY, ref double maxX, ref double maxY)
		{
			double X = x / scale;
			double Y = y / scale;
			if (X < minX) minX = X;
			if (Y < minY) minY = Y;
			if (X > maxX) maxX = X;
			if (Y > maxY) maxY = Y;
		}

		private static void RotateClosedPathToLeftmost(List<CsPotrace.Curve> curves)
		{
			if (curves == null || curves.Count < 2) return;
			int best = 0;
			double bestX = curves[0].A.X, bestY = curves[0].A.Y;
			for (int i = 1; i < curves.Count; i++)
			{
				dPoint a = curves[i].A;
				if (a.X < bestX - 1e-9 || (Math.Abs(a.X - bestX) < 1e-9 && a.Y < bestY))
				{
					best = i;
					bestX = a.X;
					bestY = a.Y;
				}
			}
			if (best == 0) return;
			List<CsPotrace.Curve> rotated = new List<CsPotrace.Curve>(curves.Count);
			for (int i = best; i < curves.Count; i++) rotated.Add(curves[i]);
			for (int i = 0; i < best; i++) rotated.Add(curves[i]);
			curves.Clear();
			curves.AddRange(rotated);
		}

		private static List<string> GetPathGC(List<CsPotrace.Curve> Curves, string lOn, string lOff, double oX, double oY, double scale, Graphics g, string skipcmd)
		{
			List<string> rv = new List<string>();

			OnPathBegin(Curves, lOn, oX, oY, scale, rv, skipcmd);

			foreach (CsPotrace.Curve Curve in Curves)
				OnPathSegment(Curve, oX, oY, scale, rv, g);

			OnPathEnd(Curves, lOff, oX, oY, scale, rv);

			return rv;
		}

		private static void OnPathSegment(CsPotrace.Curve Curve, double oX, double oY, double scale, List<string> rv, Graphics g)
		{
			if (double.IsNaN(Curve.LinearLenght)) // problem?
				return;

			if (Curve.Kind == CsPotrace.CurveKind.Line)
			{
				//trace line
				if (g != null) 
					g.DrawLine(Pens.DarkGray, (float)Curve.A.X, (float)Curve.A.Y, (float)Curve.B.X, (float)Curve.B.Y);
				
				rv.Add(String.Format("G1 X{0} Y{1}", formatnumber(Curve.B.X + oX, scale), formatnumber(Curve.B.Y + oY, scale)));
			}

			if (Curve.Kind == CsPotrace.CurveKind.Bezier)
			{
				CubicBezier cb = new CubicBezier(new Vector2((float)Curve.A.X, (float)Curve.A.Y),
												 new Vector2((float)Curve.ControlPointA.X, (float)Curve.ControlPointA.Y),
												 new Vector2((float)Curve.ControlPointB.X, (float)Curve.ControlPointB.Y),
												 new Vector2((float)Curve.B.X, (float)Curve.B.Y));

				if (g != null) g.DrawBezier(Pens.Green,
								AsPointF(cb.P1),
								AsPointF(cb.C1),
								AsPointF(cb.C2),
								AsPointF(cb.P2));

				try
				{
					List<BiArc> bal = Algorithm.ApproxCubicBezier(cb, 5, 2);
					if (bal != null)	//può ritornare null se ha troppi punti da processare
					{
						foreach (BiArc ba in bal)
						{
							if (!double.IsNaN(ba.A1.Length) && !double.IsNaN(ba.A1.LinearLength))
								rv.Add(GetArcGC(ba.A1, oX, oY, scale, g));

							if (!double.IsNaN(ba.A2.Length) && !double.IsNaN(ba.A2.LinearLength))
								rv.Add(GetArcGC(ba.A2, oX, oY, scale, g));
						}
					}
					else //same as exception
					{
						if (g != null) g.DrawLine(Pens.DarkGray, (float)Curve.A.X, (float)Curve.A.Y, (float)Curve.B.X, (float)Curve.B.Y);
						rv.Add(String.Format("G1 X{0} Y{1}", formatnumber(Curve.B.X + oX, scale), formatnumber(Curve.B.Y + oY, scale)));
					}
				}
				catch
				{
					if (g != null) g.DrawLine(Pens.DarkGray, (float)Curve.A.X, (float)Curve.A.Y, (float)Curve.B.X, (float)Curve.B.Y);
					rv.Add(String.Format("G1 X{0} Y{1}", formatnumber(Curve.B.X + oX, scale), formatnumber(Curve.B.Y + oY, scale)));
				}

			}
		}

		private static bool plotterFirstApproach = true;
		private static bool plotterBBoxValid = false;
		private static double plotterBBoxMinX = 0;

		/// <summary>Call before exporting a new job so the first contour uses outside entry.</summary>
		public static void ResetPlotterApproach()
		{
			plotterFirstApproach = true;
			plotterBBoxValid = false;
		}

		private static void OnPathBegin(List<CsPotrace.Curve> Curves, string lOn, double oX, double oY, double scale, List<string> rv, string skipcmd)
		{
			if (Curves.Count > 0)
			{
				bool continuous = LaserGRBL.Plotter.PlotterSettings.UseContinuousCut;
				string sx = formatnumber(Curves[0].A.X + oX, scale);
				string sy = formatnumber(Curves[0].A.Y + oY, scale);

				if (continuous)
				{
					// Blade is always down: G0 still cuts. Enter from left of bbox on first path.
					if (plotterFirstApproach && plotterBBoxValid)
					{
						float margin = LaserGRBL.Plotter.PlotterSettings.LeadInMm > 0.01f
							? LaserGRBL.Plotter.PlotterSettings.LeadInMm : 5f;
						double startXm = (Curves[0].A.X + oX) / scale;
						double startYm = (Curves[0].A.Y + oY) / scale;
						double entryXm = plotterBBoxMinX - margin;
						rv.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
							"G1 X{0:0.###} Y{1:0.###} (outside entry)", entryXm, startYm));
						rv.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
							"G1 X{0:0.###} Y{1:0.###}", startXm, startYm));
						plotterFirstApproach = false;
					}
					else
					{
						rv.Add(String.Format("G1 X{0} Y{1}", sx, sy));
						plotterFirstApproach = false;
					}
				}
				else
				{
					rv.Add(String.Format("{0} X{1} Y{2}", skipcmd, sx, sy));
				}

				if (!string.IsNullOrEmpty(lOn) && !(continuous && LaserGRBL.Plotter.PlotterSettings.DownMode == LaserGRBL.Plotter.PlotterSettings.KnifeActuation.AlwaysDown))
					rv.Add(lOn);
			}
		}

		private static void OnPathEnd(List<CsPotrace.Curve> Curves, string lOff, double oX, double oY, double scale, List<string> rv)
		{
			if (Curves.Count == 0)
				return;
			// continuous through-cut: keep blade engaged between contours
			if (LaserGRBL.Plotter.PlotterSettings.UseContinuousCut)
				return;
			if (!string.IsNullOrEmpty(lOff))
				rv.Add(lOff);
		}

		private static string GetArcGC(Arc arc, double oX, double oY, double scale, Graphics g)
		{
			//http://www.cnccookbook.com/CCCNCGCodeArcsG02G03.htm
			//https://www.tormach.com/g02_g03.html

			if (!CasoLimite(arc, scale))
			{
				if (g != null) g.DrawArc(Pens.Red, arc.C.X - arc.r, arc.C.Y - arc.r, 2 * arc.r, 2 * arc.r, arc.startAngle * 180.0f / (float)Math.PI, arc.sweepAngle * 180.0f / (float)Math.PI);
				return String.Format("G{0} X{1} Y{2} I{3} J{4}", !arc.IsClockwise ? 2 : 3, formatnumber(arc.P2.X + oX, scale), formatnumber(arc.P2.Y + oY, scale), formatnumber(arc.C.X - arc.P1.X, scale), formatnumber(arc.C.Y - arc.P1.Y, scale));
			}
			else //approximate with a line
			{
				if (g != null) g.DrawLine(Pens.DarkGray, (float)arc.P1.X, (float)arc.P1.Y, (float)arc.P2.X, (float)arc.P2.Y);
				return String.Format("G1 X{0} Y{1}", formatnumber(arc.P2.X + oX, scale), formatnumber(arc.P2.Y + oY, scale));
			}
		}

		private static bool CasoLimite(Arc arc, double scale)
		{
			if (arc.r == 0 || arc.LinearLength == 0)
				return true;
			if ((arc.LinearLength / scale) <= 0.5)
				return true;
			if (arc.r / arc.LinearLength > 1000)
				return true;

			return false;
		}

		private static string formatnumber(double number, double scale)
		{
			double num = number / scale;
			if (!double.IsNaN(num))
				return num.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
			else
				return "0";
		}

		public static PointF AsPointF(Vector2 v)
		{
			return new PointF(v.X, v.Y);
		}

	}
}
