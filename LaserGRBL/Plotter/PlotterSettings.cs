// Plotter / drag-knife mode for LaserGRBL-knife
// Copyright (c) 2026 — fork extension under GPLv3

using System;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Typed accessors for knife/plotter settings stored in LaserGRBL Settings.bin.
	/// When Enabled, vector/SVG conversion uses drag-knife compensation and knife-oriented defaults.
	/// </summary>
	public static class PlotterSettings
	{
		public const string KeyEnabled = "Plotter.Enabled";
		public const string KeyBladeOffset = "Plotter.BladeOffsetMm";
		public const string KeySwivelAngle = "Plotter.SwivelAngleDeg";
		public const string KeyCutSpeed = "Plotter.CutSpeed";
		public const string KeyTravelSpeed = "Plotter.TravelSpeed";
		public const string KeyKnifeDownMode = "Plotter.KnifeDownMode";
		public const string KeyKnifeDownCmd = "Plotter.KnifeDownCommand";
		public const string KeyKnifeUpCmd = "Plotter.KnifeUpCommand";
		public const string KeyZDown = "Plotter.ZDown";
		public const string KeyZUp = "Plotter.ZUp";
		public const string KeyZFeed = "Plotter.ZFeed";
		public const string KeyLeadInMm = "Plotter.LeadInMm";
		public const string KeyOvercutMm = "Plotter.OvercutMm";
		public const string KeyOptimizeOrder = "Plotter.OptimizePathOrder";
		public const string KeyInnerFirst = "Plotter.CutInnerFirst";
		public const string KeyPauseOnKnifeDown = "Plotter.PauseOnKnifeDown";

		public enum KnifeActuation
		{
			/// <summary>Reuse laser M3/M5 (or PWM S) as knife down/up — typical for solenoid/servo on spindle pin.</summary>
			SpindleToggle = 0,
			/// <summary>Move Z axis for knife down/up.</summary>
			ZAxis = 1,
			/// <summary>Emit custom G-code lines for down/up.</summary>
			CustomCommands = 2
		}

		public static bool Enabled
		{
			get { return Settings.GetObject(KeyEnabled, false); }
			set { Settings.SetObject(KeyEnabled, value); }
		}

		/// <summary>Distance from swivel pivot to blade tip (mm). Typical craft blades: 0.25–1.0 mm.</summary>
		public static float BladeOffsetMm
		{
			get { return Settings.GetObject(KeyBladeOffset, 0.5f); }
			set { Settings.SetObject(KeyBladeOffset, value); }
		}

		/// <summary>Minimum direction-change angle (degrees) that inserts a compensation arc.</summary>
		public static float SwivelAngleDeg
		{
			get { return Settings.GetObject(KeySwivelAngle, 30f); }
			set { Settings.SetObject(KeySwivelAngle, value); }
		}

		public static int CutSpeed
		{
			get { return Settings.GetObject(KeyCutSpeed, 1000); }
			set { Settings.SetObject(KeyCutSpeed, value); }
		}

		public static int TravelSpeed
		{
			get { return Settings.GetObject(KeyTravelSpeed, 3000); }
			set { Settings.SetObject(KeyTravelSpeed, value); }
		}

		public static KnifeActuation DownMode
		{
			get { return (KnifeActuation)Settings.GetObject(KeyKnifeDownMode, (int)KnifeActuation.SpindleToggle); }
			set { Settings.SetObject(KeyKnifeDownMode, (int)value); }
		}

		public static string KnifeDownCommand
		{
			get { return Settings.GetObject(KeyKnifeDownCmd, "M3"); }
			set { Settings.SetObject(KeyKnifeDownCmd, value ?? "M3"); }
		}

		public static string KnifeUpCommand
		{
			get { return Settings.GetObject(KeyKnifeUpCmd, "M5"); }
			set { Settings.SetObject(KeyKnifeUpCmd, value ?? "M5"); }
		}

		public static float ZDown
		{
			get { return Settings.GetObject(KeyZDown, -1f); }
			set { Settings.SetObject(KeyZDown, value); }
		}

		public static float ZUp
		{
			get { return Settings.GetObject(KeyZUp, 5f); }
			set { Settings.SetObject(KeyZUp, value); }
		}

		public static int ZFeed
		{
			get { return Settings.GetObject(KeyZFeed, 500); }
			set { Settings.SetObject(KeyZFeed, value); }
		}

		/// <summary>Optional lead-in distance before a path start (mm). 0 = disabled.</summary>
		public static float LeadInMm
		{
			get { return Settings.GetObject(KeyLeadInMm, 0f); }
			set { Settings.SetObject(KeyLeadInMm, value); }
		}

		/// <summary>Optional overcut past closed-path end (mm). 0 = disabled.</summary>
		public static float OvercutMm
		{
			get { return Settings.GetObject(KeyOvercutMm, 0f); }
			set { Settings.SetObject(KeyOvercutMm, value); }
		}

		public static bool OptimizePathOrder
		{
			get { return Settings.GetObject(KeyOptimizeOrder, true); }
			set { Settings.SetObject(KeyOptimizeOrder, value); }
		}

		public static bool CutInnerFirst
		{
			get { return Settings.GetObject(KeyInnerFirst, true); }
			set { Settings.SetObject(KeyInnerFirst, value); }
		}

		public static bool PauseOnKnifeDown
		{
			get { return Settings.GetObject(KeyPauseOnKnifeDown, false); }
			set { Settings.SetObject(KeyPauseOnKnifeDown, value); }
		}

		/// <summary>True when drag-knife geometry compensation should run during G-code moves.</summary>
		public static bool UseDragCompensation
		{
			get { return Enabled && BladeOffsetMm > 0.001f; }
		}
	}
}
