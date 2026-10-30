// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

namespace LaserGRBL.Plotter
{
	public static class PlotterSettings
	{
		public static bool IsEnabled
		{
			get { return LaserGRBL.Settings.GetObject("Plotter.Enabled", false); }
		}

		public static PlotterConfig Load()
		{
			PlotterConfig def = PlotterConfig.CreateDefault();
			PlotterConfig cfg = new PlotterConfig();
			cfg.Enabled = LaserGRBL.Settings.GetObject("Plotter.Enabled", def.Enabled);
			cfg.Preset = (PlotterBladePreset)LaserGRBL.Settings.GetObject("Plotter.Preset", (int)def.Preset);
			cfg.BladeName = LaserGRBL.Settings.GetObject("Plotter.BladeName", def.BladeName);
			cfg.BladeOffsetMm = LaserGRBL.Settings.GetObject("Plotter.BladeOffsetMm", def.BladeOffsetMm);
			cfg.OvercutMm = LaserGRBL.Settings.GetObject("Plotter.OvercutMm", def.OvercutMm);
			cfg.LeadInMm = LaserGRBL.Settings.GetObject("Plotter.LeadInMm", def.LeadInMm);
			cfg.CornerThresholdDeg = LaserGRBL.Settings.GetObject("Plotter.CornerThresholdDeg", def.CornerThresholdDeg);
			cfg.LiftAngleDeg = LaserGRBL.Settings.GetObject("Plotter.LiftAngleDeg", def.LiftAngleDeg);
			cfg.SimplifyMm = LaserGRBL.Settings.GetObject("Plotter.SimplifyMm", def.SimplifyMm);
			cfg.MinSegmentMm = LaserGRBL.Settings.GetObject("Plotter.MinSegmentMm", def.MinSegmentMm);
			cfg.CutSpeed = LaserGRBL.Settings.GetObject("Plotter.CutSpeed", def.CutSpeed);
			cfg.TravelSpeed = LaserGRBL.Settings.GetObject("Plotter.TravelSpeed", def.TravelSpeed);
			cfg.PlungeSpeed = LaserGRBL.Settings.GetObject("Plotter.PlungeSpeed", def.PlungeSpeed);
			cfg.DwellDownMs = LaserGRBL.Settings.GetObject("Plotter.DwellDownMs", def.DwellDownMs);
			cfg.DwellUpMs = LaserGRBL.Settings.GetObject("Plotter.DwellUpMs", def.DwellUpMs);
			cfg.PenMode = (PlotterPenMode)LaserGRBL.Settings.GetObject("Plotter.PenMode", (int)def.PenMode);
			cfg.ServoUp = LaserGRBL.Settings.GetObject("Plotter.ServoUp", def.ServoUp);
			cfg.ServoDown = LaserGRBL.Settings.GetObject("Plotter.ServoDown", def.ServoDown);
			cfg.PenUpZ = LaserGRBL.Settings.GetObject("Plotter.PenUpZ", def.PenUpZ);
			cfg.PenDownZ = LaserGRBL.Settings.GetObject("Plotter.PenDownZ", def.PenDownZ);
			cfg.OptimizeOrder = LaserGRBL.Settings.GetObject("Plotter.OptimizeOrder", def.OptimizeOrder);
			cfg.InsideFirst = LaserGRBL.Settings.GetObject("Plotter.InsideFirst", def.InsideFirst);
			cfg.Passes = LaserGRBL.Settings.GetObject("Plotter.Passes", def.Passes);
			cfg.LinearizeArcs = LaserGRBL.Settings.GetObject("Plotter.LinearizeArcs", def.LinearizeArcs);
			cfg.ArcStepDeg = LaserGRBL.Settings.GetObject("Plotter.ArcStepDeg", def.ArcStepDeg);
			cfg.Normalize();
			return cfg;
		}

		public static void Save(PlotterConfig cfg)
		{
			if (cfg == null)
				return;
			cfg.Normalize();
			LaserGRBL.Settings.SetObject("Plotter.Enabled", cfg.Enabled);
			LaserGRBL.Settings.SetObject("Plotter.Preset", (int)cfg.Preset);
			LaserGRBL.Settings.SetObject("Plotter.BladeName", cfg.BladeName ?? "Blade");
			LaserGRBL.Settings.SetObject("Plotter.BladeOffsetMm", cfg.BladeOffsetMm);
			LaserGRBL.Settings.SetObject("Plotter.OvercutMm", cfg.OvercutMm);
			LaserGRBL.Settings.SetObject("Plotter.LeadInMm", cfg.LeadInMm);
			LaserGRBL.Settings.SetObject("Plotter.CornerThresholdDeg", cfg.CornerThresholdDeg);
			LaserGRBL.Settings.SetObject("Plotter.LiftAngleDeg", cfg.LiftAngleDeg);
			LaserGRBL.Settings.SetObject("Plotter.SimplifyMm", cfg.SimplifyMm);
			LaserGRBL.Settings.SetObject("Plotter.MinSegmentMm", cfg.MinSegmentMm);
			LaserGRBL.Settings.SetObject("Plotter.CutSpeed", cfg.CutSpeed);
			LaserGRBL.Settings.SetObject("Plotter.TravelSpeed", cfg.TravelSpeed);
			LaserGRBL.Settings.SetObject("Plotter.PlungeSpeed", cfg.PlungeSpeed);
			LaserGRBL.Settings.SetObject("Plotter.DwellDownMs", cfg.DwellDownMs);
			LaserGRBL.Settings.SetObject("Plotter.DwellUpMs", cfg.DwellUpMs);
			LaserGRBL.Settings.SetObject("Plotter.PenMode", (int)cfg.PenMode);
			LaserGRBL.Settings.SetObject("Plotter.ServoUp", cfg.ServoUp);
			LaserGRBL.Settings.SetObject("Plotter.ServoDown", cfg.ServoDown);
			LaserGRBL.Settings.SetObject("Plotter.PenUpZ", cfg.PenUpZ);
			LaserGRBL.Settings.SetObject("Plotter.PenDownZ", cfg.PenDownZ);
			LaserGRBL.Settings.SetObject("Plotter.OptimizeOrder", cfg.OptimizeOrder);
			LaserGRBL.Settings.SetObject("Plotter.InsideFirst", cfg.InsideFirst);
			LaserGRBL.Settings.SetObject("Plotter.Passes", cfg.Passes);
			LaserGRBL.Settings.SetObject("Plotter.LinearizeArcs", cfg.LinearizeArcs);
			LaserGRBL.Settings.SetObject("Plotter.ArcStepDeg", cfg.ArcStepDeg);
		}

		public static void RememberCutSpeed(int speed)
		{
			if (speed < 1)
				return;
			PlotterConfig cfg = Load();
			cfg.CutSpeed = speed;
			Save(cfg);
		}
	}
}
