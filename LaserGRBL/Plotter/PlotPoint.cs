// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Collections.Generic;

namespace LaserGRBL.Plotter
{
	public struct PlotPoint
	{
		public double X;
		public double Y;

		public PlotPoint(double x, double y)
		{
			X = x;
			Y = y;
		}
	}

	public sealed class PlotPath
	{
		public List<PlotPoint> Points = new List<PlotPoint>();
		public bool Closed;
	}

	/// <summary>
	/// One motion of the knife holder. Cut=false is a pen-up travel.
	/// Arc I/J are offsets from the start of the arc to its center.
	/// </summary>
	public sealed class PlotMove
	{
		public double X;
		public double Y;
		public bool Cut;
		public bool Arc;
		public bool Clockwise;
		public double I;
		public double J;
	}
}
