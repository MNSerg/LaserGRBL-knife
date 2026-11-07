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
		ZAxis = 2,
		/// <summary>Knife height is set by hand. The program only moves X and Y, and the blade stays down.</summary>
		Manual = 3
	}

	/// <summary>How a Z-axis knife leaves the material. Ignored unless <see cref="PlotterPenMode.ZAxis"/> is selected.</summary>
	public enum PlotterZLift
	{
		/// <summary>The blade is fixed on Z. The stepper raises and lowers that axis.</summary>
		Standard = 0,
		/// <summary>The Z motor lies on its side and turns a cam. The set turn pushes the head down; springs lift the blade when the cam releases.</summary>
		Cam = 1
	}

	public enum PlotterBladePreset
	{
		Custom = 0,
		Pen = 1,
		VinylFine = 2,
		Craft = 3,
		Deep = 4,
		Rubber = 5
	}

	/// <summary>Which closed contour the knife walks to next. Travel still goes around other contours.</summary>
	public enum PlotterRouteMode
	{
		/// <summary>Closest contour that can be reached without crossing another.</summary>
		Nearest = 0,
		/// <summary>Contours on the outer edge of the sheet first, then deeper toward the middle.</summary>
		OutsideIn = 1,
		/// <summary>A nested contour before the outline around it, when the knife can already reach it.</summary>
		InsideOut = 2,
		/// <summary>Leave the file order. Each contour is still entered from the outside.</summary>
		FileOrder = 3
	}

	/// <summary>How the blade offset is applied to a contour.</summary>
	public enum PlotterCutMode
	{
		/// <summary>Tangent offset on curves. An isolated sharp corner swings the holder around the tip.</summary>
		Swivel = 0,
		/// <summary>Every sample, including long curves and circles, is shifted along its tangent. A sharp corner is a straight miter, not a swivel arc.</summary>
		Tangent = 1
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
		public PlotterZLift ZLift;
		/// <summary>Z command that lets the cam go slack so the springs lift the blade.</summary>
		public double CamUp;
		/// <summary>Z command that turns the cam far enough to push the head down by the cam length.</summary>
		public double CamDown;
		public bool OptimizeOrder;
		public bool InsideFirst;
		public PlotterRouteMode RouteMode;
		public PlotterCutMode CutMode;
		/// <summary>Distance between tangent samples in <see cref="PlotterCutMode.Tangent"/>.</summary>
		public double TangentStepMm;
		public int Passes;
		public bool LinearizeArcs;
		public double ArcStepDeg;
		/// <summary>How far outside the drawing's bottom-left corner the blade waits before the first contour.</summary>
		public double StartMarginMm;
		/// <summary>Positioning laser minus blade axis. Positive X means the laser is to the right of the blade.</summary>
		public double LaserOffsetX;
		public double LaserOffsetY;

		public static PlotterConfig CreateDefault()
		{
			PlotterConfig c = new PlotterConfig();
			c.Enabled = false;
			c.Preset = PlotterBladePreset.Rubber;
			c.BladeName = "Rubber 3.60";
			c.BladeOffsetMm = 3.6;
			c.OvercutMm = 3.6;
			c.LeadInMm = 3.6;
			c.CornerThresholdDeg = 8;
			c.LiftAngleDeg = 0;
			c.SimplifyMm = 0.05;
			c.MinSegmentMm = 0.02;
			c.CutSpeed = 600;
			c.TravelSpeed = 0;
			c.PlungeSpeed = 300;
			c.DwellDownMs = 0;
			c.DwellUpMs = 0;
			c.PenMode = PlotterPenMode.Manual;
			c.ServoUp = 0;
			c.ServoDown = 255;
			c.PenUpZ = 5;
			c.PenDownZ = 0;
			c.ZLift = PlotterZLift.Standard;
			c.CamUp = 0;
			c.CamDown = 90;
			c.OptimizeOrder = true;
			c.InsideFirst = false;
			c.RouteMode = PlotterRouteMode.Nearest;
			c.CutMode = PlotterCutMode.Swivel;
			c.TangentStepMm = 0.25;
			c.Passes = 1;
			c.LinearizeArcs = false;
			c.ArcStepDeg = 10;
			c.StartMarginMm = 20;
			c.LaserOffsetX = 10;
			c.LaserOffsetY = 0;
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
			else if (preset == PlotterBladePreset.Rubber)
			{
				BladeName = "Rubber 3.60";
				BladeOffsetMm = 3.6;
				OvercutMm = 3.6;
				LeadInMm = 3.6;
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
			if (StartMarginMm < 0) StartMarginMm = 0;
			if (StartMarginMm > 500) StartMarginMm = 500;
			if (LaserOffsetX < -500) LaserOffsetX = -500;
			if (LaserOffsetX > 500) LaserOffsetX = 500;
			if (LaserOffsetY < -500) LaserOffsetY = -500;
			if (LaserOffsetY > 500) LaserOffsetY = 500;
			if (string.IsNullOrEmpty(BladeName)) BladeName = "Blade";
			int lift = (int)ZLift;
			if (lift != (int)PlotterZLift.Standard && lift != (int)PlotterZLift.Cam)
				ZLift = PlotterZLift.Standard;
			if (CamUp < -3600) CamUp = -3600;
			if (CamUp > 3600) CamUp = 3600;
			if (CamDown < -3600) CamDown = -3600;
			if (CamDown > 3600) CamDown = 3600;
			int route = (int)RouteMode;
			if (route < 0 || route > 3) RouteMode = PlotterRouteMode.Nearest;
			int cut = (int)CutMode;
			if (cut != (int)PlotterCutMode.Swivel && cut != (int)PlotterCutMode.Tangent)
				CutMode = PlotterCutMode.Swivel;
			if (TangentStepMm < 0.02) TangentStepMm = 0.02;
			if (TangentStepMm > 5) TangentStepMm = 5;
			OptimizeOrder = RouteMode != PlotterRouteMode.FileOrder;
			InsideFirst = RouteMode == PlotterRouteMode.InsideOut;
		}

		/// <summary>The blade never leaves the material, so every XY move cuts and travel must stay outside closed contours.</summary>
		public bool KnifeStaysDown
		{
			get { return PenMode == PlotterPenMode.Manual; }
		}
	}
}
