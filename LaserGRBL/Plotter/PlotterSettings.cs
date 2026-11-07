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
			// Older builds stored a servo lift and wrote the first move as G0.
			// This machine keeps the knife down, so that first move has to be G1.
			int schema = LaserGRBL.Settings.GetObject("Plotter.Schema", 0);
			if (schema < 3)
			{
				if (cfg.PenMode == PlotterPenMode.ServoPwm && cfg.TravelSpeed <= 0 && cfg.LiftAngleDeg <= 0)
				{
					cfg.PenMode = PlotterPenMode.Manual;
					LaserGRBL.Settings.SetObject("Plotter.PenMode", (int)PlotterPenMode.Manual);
				}
				LaserGRBL.Settings.SetObject("Plotter.Schema", 3);
			}
			cfg.ServoUp = LaserGRBL.Settings.GetObject("Plotter.ServoUp", def.ServoUp);
			cfg.ServoDown = LaserGRBL.Settings.GetObject("Plotter.ServoDown", def.ServoDown);
			cfg.PenUpZ = LaserGRBL.Settings.GetObject("Plotter.PenUpZ", def.PenUpZ);
			cfg.PenDownZ = LaserGRBL.Settings.GetObject("Plotter.PenDownZ", def.PenDownZ);
			cfg.ZLift = (PlotterZLift)LaserGRBL.Settings.GetObject("Plotter.ZLift", (int)def.ZLift);
			cfg.CamUp = LaserGRBL.Settings.GetObject("Plotter.CamUp", def.CamUp);
			cfg.CamDown = LaserGRBL.Settings.GetObject("Plotter.CamDown", def.CamDown);
			cfg.OptimizeOrder = LaserGRBL.Settings.GetObject("Plotter.OptimizeOrder", def.OptimizeOrder);
			cfg.InsideFirst = LaserGRBL.Settings.GetObject("Plotter.InsideFirst", def.InsideFirst);
			int routeStored = LaserGRBL.Settings.GetObject("Plotter.RouteMode", -1);
			if (routeStored < 0 || routeStored > 3)
			{
				if (!cfg.OptimizeOrder)
					cfg.RouteMode = PlotterRouteMode.FileOrder;
				else
					cfg.RouteMode = PlotterRouteMode.Nearest;
				LaserGRBL.Settings.SetObject("Plotter.RouteMode", (int)cfg.RouteMode);
			}
			else
				cfg.RouteMode = (PlotterRouteMode)routeStored;
			cfg.CutMode = (PlotterCutMode)LaserGRBL.Settings.GetObject("Plotter.CutMode", (int)def.CutMode);
			cfg.TangentStepMm = LaserGRBL.Settings.GetObject("Plotter.TangentStepMm", def.TangentStepMm);
			cfg.Passes = LaserGRBL.Settings.GetObject("Plotter.Passes", def.Passes);
			cfg.LinearizeArcs = LaserGRBL.Settings.GetObject("Plotter.LinearizeArcs", def.LinearizeArcs);
			cfg.ArcStepDeg = LaserGRBL.Settings.GetObject("Plotter.ArcStepDeg", def.ArcStepDeg);
			cfg.StartMarginMm = LaserGRBL.Settings.GetObject("Plotter.StartMarginMm", def.StartMarginMm);
			cfg.LaserOffsetX = LaserGRBL.Settings.GetObject("Plotter.LaserOffsetX", def.LaserOffsetX);
			cfg.LaserOffsetY = LaserGRBL.Settings.GetObject("Plotter.LaserOffsetY", def.LaserOffsetY);
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
			LaserGRBL.Settings.SetObject("Plotter.ZLift", (int)cfg.ZLift);
			LaserGRBL.Settings.SetObject("Plotter.CamUp", cfg.CamUp);
			LaserGRBL.Settings.SetObject("Plotter.CamDown", cfg.CamDown);
			LaserGRBL.Settings.SetObject("Plotter.RouteMode", (int)cfg.RouteMode);
			LaserGRBL.Settings.SetObject("Plotter.CutMode", (int)cfg.CutMode);
			LaserGRBL.Settings.SetObject("Plotter.TangentStepMm", cfg.TangentStepMm);
			LaserGRBL.Settings.SetObject("Plotter.OptimizeOrder", cfg.RouteMode != PlotterRouteMode.FileOrder);
			LaserGRBL.Settings.SetObject("Plotter.InsideFirst", cfg.RouteMode == PlotterRouteMode.InsideOut);
			LaserGRBL.Settings.SetObject("Plotter.Passes", cfg.Passes);
			LaserGRBL.Settings.SetObject("Plotter.LinearizeArcs", cfg.LinearizeArcs);
			LaserGRBL.Settings.SetObject("Plotter.ArcStepDeg", cfg.ArcStepDeg);
			LaserGRBL.Settings.SetObject("Plotter.StartMarginMm", cfg.StartMarginMm);
			LaserGRBL.Settings.SetObject("Plotter.LaserOffsetX", cfg.LaserOffsetX);
			LaserGRBL.Settings.SetObject("Plotter.LaserOffsetY", cfg.LaserOffsetY);
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
