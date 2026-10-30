// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

namespace LaserGRBL.Plotter
{
	public enum PlotterPenMode
	{
		/// <summary>Servo or pen on the spindle PWM pin: M3 S{up} / M3 S{down}.</summary>
		ServoPwm = 0,
		/// <summary>Solenoid or relay: M3 down, M5 up.</summary>
		Digital = 1,
		/// <summary>Real Z axis: G0 Z up, G1 Z down.</summary>
		ZAxis = 2
	}

	public enum PlotterBladePreset
	{
		Custom = 0,
		Pen = 1,
		VinylFine = 2,
		Craft = 3,
		Deep = 4
	}

	/// <summary>
	/// Cutter parameters for a drag knife (passive swivel blade) or a pen.
	/// Distances are millimetres, speeds are millimetres per minute, angles are degrees.
	/// </summary>
	public sealed class PlotterConfig
	{
		public bool Enabled;
		public PlotterBladePreset Preset;
		public string BladeName;
		public double BladeOffsetMm;
		public double OvercutMm;
		public double LeadInMm;
		public double CornerThresholdDeg;
		public double LiftAngleDeg;
		public double SimplifyMm;
		public double MinSegmentMm;
		public int CutSpeed;
		public int TravelSpeed;
		public int PlungeSpeed;
		public int DwellDownMs;
		public int DwellUpMs;
		public PlotterPenMode PenMode;
		public int ServoUp;
		public int ServoDown;
		public double PenUpZ;
		public double PenDownZ;
		public bool OptimizeOrder;
		public bool InsideFirst;
		public int Passes;
		public bool LinearizeArcs;
		public double ArcStepDeg;

		public static PlotterConfig CreateDefault()
		{
			PlotterConfig c = new PlotterConfig();
			c.Enabled = false;
			c.Preset = PlotterBladePreset.Craft;
			c.BladeName = "Craft 0.50";
			c.BladeOffsetMm = 0.50;
			c.OvercutMm = 1.0;
			c.LeadInMm = 0.50;
			c.CornerThresholdDeg = 8;
			c.LiftAngleDeg = 150;
			c.SimplifyMm = 0.05;
			c.MinSegmentMm = 0.02;
			c.CutSpeed = 800;
			c.TravelSpeed = 0;
			c.PlungeSpeed = 300;
			c.DwellDownMs = 100;
			c.DwellUpMs = 50;
			c.PenMode = PlotterPenMode.ServoPwm;
			c.ServoUp = 0;
			c.ServoDown = 255;
			c.PenUpZ = 5;
			c.PenDownZ = 0;
			c.OptimizeOrder = true;
			c.InsideFirst = true;
			c.Passes = 1;
			c.LinearizeArcs = false;
			c.ArcStepDeg = 10;
			return c;
		}

		public void ApplyPreset(PlotterBladePreset preset)
		{
			Preset = preset;
			if (preset == PlotterBladePreset.Pen)
			{
				BladeName = "Pen";
				BladeOffsetMm = 0;
				OvercutMm = 0;
				LeadInMm = 0;
			}
			else if (preset == PlotterBladePreset.VinylFine)
			{
				BladeName = "Vinyl 0.25";
				BladeOffsetMm = 0.25;
				OvercutMm = 0.50;
				LeadInMm = 0.25;
			}
			else if (preset == PlotterBladePreset.Craft)
			{
				BladeName = "Craft 0.50";
				BladeOffsetMm = 0.50;
				OvercutMm = 1.0;
				LeadInMm = 0.50;
			}
			else if (preset == PlotterBladePreset.Deep)
			{
				BladeName = "Deep 0.75";
				BladeOffsetMm = 0.75;
				OvercutMm = 1.20;
				LeadInMm = 0.75;
			}
		}

		public void Normalize()
		{
			if (BladeOffsetMm < 0) BladeOffsetMm = 0;
			if (BladeOffsetMm > 20) BladeOffsetMm = 20;
			if (OvercutMm < 0) OvercutMm = 0;
			if (OvercutMm > 50) OvercutMm = 50;
			if (LeadInMm < 0) LeadInMm = 0;
			if (LeadInMm > 50) LeadInMm = 50;
			if (CornerThresholdDeg < 0) CornerThresholdDeg = 0;
			if (CornerThresholdDeg > 180) CornerThresholdDeg = 180;
			if (LiftAngleDeg < 0) LiftAngleDeg = 0;
			if (LiftAngleDeg > 180) LiftAngleDeg = 180;
			if (SimplifyMm < 0) SimplifyMm = 0;
			if (SimplifyMm > 5) SimplifyMm = 5;
			if (MinSegmentMm < 0) MinSegmentMm = 0;
			if (MinSegmentMm > 5) MinSegmentMm = 5;
			if (CutSpeed < 1) CutSpeed = 1;
			if (CutSpeed > 50000) CutSpeed = 50000;
			if (TravelSpeed < 0) TravelSpeed = 0;
			if (TravelSpeed > 50000) TravelSpeed = 50000;
			if (PlungeSpeed < 1) PlungeSpeed = 1;
			if (PlungeSpeed > 50000) PlungeSpeed = 50000;
			if (DwellDownMs < 0) DwellDownMs = 0;
			if (DwellDownMs > 5000) DwellDownMs = 5000;
			if (DwellUpMs < 0) DwellUpMs = 0;
			if (DwellUpMs > 5000) DwellUpMs = 5000;
			if (ServoUp < 0) ServoUp = 0;
			if (ServoDown < 0) ServoDown = 0;
			if (ServoUp > 100000) ServoUp = 100000;
			if (ServoDown > 100000) ServoDown = 100000;
			if (Passes < 1) Passes = 1;
			if (Passes > 30) Passes = 30;
			if (ArcStepDeg < 1) ArcStepDeg = 1;
			if (ArcStepDeg > 45) ArcStepDeg = 45;
			if (string.IsNullOrEmpty(BladeName)) BladeName = "Blade";
		}
	}
}
