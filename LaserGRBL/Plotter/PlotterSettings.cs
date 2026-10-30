// Plotter / drag-knife mode for LaserGRBL-knife
// Copyright (c) 2026 — fork extension under GPLv3

using System;

namespace LaserGRBL.Plotter
{
	/// <summary>
	/// Typed accessors for knife/plotter settings stored in LaserGRBL Settings.bin.
	/// Machine profile (from owner): passive swivel blade, no Z, height set manually,
	/// continuous cut (travel between contours is also a cut), outer contours first.
	/// </summary>
	public static class PlotterSettings
	{
		public const string KeyEnabled = "Plotter.Enabled";
		public const string KeyBladeOffset = "Plotter.BladeOffsetMm";
		public const string KeyMinCornerRadius = "Plotter.MinCornerRadiusMm";
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
		public const string KeyContinuousCut = "Plotter.ContinuousCut";

		public enum KnifeActuation
		{
			/// <summary>No up/down commands — blade height is set manually; stays engaged the whole job.</summary>
			AlwaysDown = 0,
			/// <summary>Reuse laser M3/M5 as knife down/up (solenoid/servo on spindle pin).</summary>
			SpindleToggle = 1,
			/// <summary>Move Z axis for knife down/up (not used on machines without Z).</summary>
			ZAxis = 2,
			/// <summary>Emit custom G-code lines for down/up.</summary>
			CustomCommands = 3
		}

		public static bool Enabled
		{
			get { return Settings.GetObject(KeyEnabled, false); }
			set { Settings.SetObject(KeyEnabled, value); }
		}

		/// <summary>Distance from swivel pivot to blade tip (mm). Owner knife: 3.6 mm.</summary>
		public static float BladeOffsetMm
		{
			get { return Settings.GetObject(KeyBladeOffset, 3.6f); }
			set { Settings.SetObject(KeyBladeOffset, value); }
		}

		/// <summary>
		/// Minimum fillet radius (mm) for sharp corners. Softer existing arcs with R &gt;= this
		/// are left as-is; sharper corners are rounded to this radius before drag compensation.
		/// </summary>
		public static float MinCornerRadiusMm
		{
			get { return Settings.GetObject(KeyMinCornerRadius, 2f); }
			set { Settings.SetObject(KeyMinCornerRadius, value); }
		}

		/// <summary>Minimum direction-change angle (degrees) that inserts a drag-knife compensation arc.</summary>
		public static float SwivelAngleDeg
		{
			get { return Settings.GetObject(KeySwivelAngle, 30f); }
			set { Settings.SetObject(KeySwivelAngle, value); }
		}

		public static int CutSpeed
		{
			get { return Settings.GetObject(KeyCutSpeed, 800); }
			set { Settings.SetObject(KeyCutSpeed, value); }
		}

		/// <summary>Used only when ContinuousCut is false (lifted travel).</summary>
		public static int TravelSpeed
		{
			get { return Settings.GetObject(KeyTravelSpeed, 3000); }
			set { Settings.SetObject(KeyTravelSpeed, value); }
		}

		public static KnifeActuation DownMode
		{
			get { return (KnifeActuation)Settings.GetObject(KeyKnifeDownMode, (int)KnifeActuation.AlwaysDown); }
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

		/// <summary>Optional lead-in before path start (mm). 0 = off. Extends the approach so the blade aligns before the real contour.</summary>
		public static float LeadInMm
		{
			get { return Settings.GetObject(KeyLeadInMm, 0f); }
			set { Settings.SetObject(KeyLeadInMm, value); }
		}

		/// <summary>Optional overcut past closed-path end (mm). 0 = off. Helps complete corners with a swivel blade.</summary>
		public static float OvercutMm
		{
			get { return Settings.GetObject(KeyOvercutMm, 0f); }
			set { Settings.SetObject(KeyOvercutMm, value); }
		}

		/// <summary>Nearest-neighbour reordering. Off by default so Potrace outer-then-inner order is kept.</summary>
		public static bool OptimizePathOrder
		{
			get { return Settings.GetObject(KeyOptimizeOrder, false); }
			set { Settings.SetObject(KeyOptimizeOrder, value); }
		}

		/// <summary>False = outer contours first (recommended for continuous through-cut).</summary>
		public static bool CutInnerFirst
		{
			get { return Settings.GetObject(KeyInnerFirst, false); }
			set { Settings.SetObject(KeyInnerFirst, value); }
		}

		public static bool PauseOnKnifeDown
		{
			get { return Settings.GetObject(KeyPauseOnKnifeDown, false); }
			set { Settings.SetObject(KeyPauseOnKnifeDown, value); }
		}

		/// <summary>
		/// When true (default): no knife-up between paths; moves to the next contour are G1 at cut speed
		/// so the blade stays in the material (owner machine has no Z lift).
		/// </summary>
		public static bool ContinuousCut
		{
			get { return Settings.GetObject(KeyContinuousCut, true); }
			set { Settings.SetObject(KeyContinuousCut, value); }
		}

		public static bool UseDragCompensation
		{
			get { return Enabled && BladeOffsetMm > 0.001f; }
		}

		/// <summary>True when travel between paths must stay cutting (G1, no pen-up).</summary>
		public static bool UseContinuousCut
		{
			get { return Enabled && ContinuousCut; }
		}
	}
}
