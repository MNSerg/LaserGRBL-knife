// Plotter / drag-knife settings UI (code-built, no Designer)
// Copyright (c) 2026 — fork extension under GPLv3

using System;
using System.Drawing;
using System.Windows.Forms;

namespace LaserGRBL.Plotter
{
	public class PlotterSettingsForm : Form
	{
		CheckBox CbEnabled;
		NumericUpDown NudBladeOffset;
		NumericUpDown NudSwivelAngle;
		NumericUpDown NudCutSpeed;
		NumericUpDown NudTravelSpeed;
		ComboBox CbDownMode;
		TextBox TbDownCmd;
		TextBox TbUpCmd;
		NumericUpDown NudZDown;
		NumericUpDown NudZUp;
		NumericUpDown NudZFeed;
		NumericUpDown NudLeadIn;
		NumericUpDown NudOvercut;
		CheckBox CbOptimize;
		CheckBox CbInnerFirst;
		CheckBox CbPauseDown;
		Label LblDownCmd;
		Label LblUpCmd;
		Label LblZDown;
		Label LblZUp;
		Label LblZFeed;
		Button BtnOk;
		Button BtnCancel;

		public PlotterSettingsForm()
		{
			Text = "Plotter / Knife mode";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			StartPosition = FormStartPosition.CenterParent;
			ClientSize = new Size(440, 520);
			Font = new Font("Segoe UI", 9f);

			BackColor = ColorScheme.FormBackColor;
			ForeColor = ColorScheme.FormForeColor;

			int y = 12;
			int left = 16;
			int labelW = 200;
			int ctrlLeft = 220;
			int ctrlW = 180;
			int row = 28;

			CbEnabled = AddCheck(left, ref y, "Enable plotter (knife) mode");
			y += 6;

			AddLabel(left, y, "Blade offset / tip distance (mm)");
			NudBladeOffset = AddNud(ctrlLeft, y, ctrlW, 0, 20, 0.05m, 2);
			y += row;

			AddLabel(left, y, "Swivel arc threshold (deg)");
			NudSwivelAngle = AddNud(ctrlLeft, y, ctrlW, 1, 180, 1, 0);
			y += row;

			AddLabel(left, y, "Cut speed (mm/min)");
			NudCutSpeed = AddNud(ctrlLeft, y, ctrlW, 1, 30000, 10, 0);
			y += row;

			AddLabel(left, y, "Travel speed (mm/min)");
			NudTravelSpeed = AddNud(ctrlLeft, y, ctrlW, 1, 30000, 10, 0);
			y += row + 4;

			AddLabel(left, y, "Knife down / up method");
			CbDownMode = new ComboBox
			{
				Left = ctrlLeft,
				Top = y - 2,
				Width = ctrlW,
				DropDownStyle = ComboBoxStyle.DropDownList,
				BackColor = ColorScheme.FormBackColor,
				ForeColor = ColorScheme.FormForeColor
			};
			CbDownMode.Items.Add("Spindle pin (M3/M5 or PWM)");
			CbDownMode.Items.Add("Z axis");
			CbDownMode.Items.Add("Custom G-code commands");
			CbDownMode.SelectedIndexChanged += (s, e) => RefreshModeVisibility();
			Controls.Add(CbDownMode);
			y += row;

			LblDownCmd = AddLabel(left, y, "Knife DOWN command");
			TbDownCmd = AddText(ctrlLeft, y, ctrlW);
			y += row;

			LblUpCmd = AddLabel(left, y, "Knife UP command");
			TbUpCmd = AddText(ctrlLeft, y, ctrlW);
			y += row;

			LblZDown = AddLabel(left, y, "Z down (cut) position");
			NudZDown = AddNud(ctrlLeft, y, ctrlW, -100, 100, 0.1m, 2);
			y += row;

			LblZUp = AddLabel(left, y, "Z up (travel) position");
			NudZUp = AddNud(ctrlLeft, y, ctrlW, -100, 100, 0.1m, 2);
			y += row;

			LblZFeed = AddLabel(left, y, "Z feed (mm/min)");
			NudZFeed = AddNud(ctrlLeft, y, ctrlW, 1, 10000, 10, 0);
			y += row + 4;

			AddLabel(left, y, "Lead-in (mm, 0=off)");
			NudLeadIn = AddNud(ctrlLeft, y, ctrlW, 0, 50, 0.1m, 2);
			y += row;

			AddLabel(left, y, "Overcut (mm, 0=off)");
			NudOvercut = AddNud(ctrlLeft, y, ctrlW, 0, 50, 0.1m, 2);
			y += row + 4;

			CbOptimize = AddCheck(left, ref y, "Optimize path order (nearest-neighbour)");
			CbInnerFirst = AddCheck(left, ref y, "Cut inner contours before outer");
			CbPauseDown = AddCheck(left, ref y, "Pause (M0) before each knife-down");

			BtnOk = new Button
			{
				Text = "Save",
				DialogResult = DialogResult.OK,
				Left = ClientSize.Width - 180,
				Top = ClientSize.Height - 40,
				Width = 75,
				BackColor = ColorScheme.FormButtonsColor,
				ForeColor = ColorScheme.FormForeColor
			};
			BtnCancel = new Button
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				Left = ClientSize.Width - 95,
				Top = ClientSize.Height - 40,
				Width = 75,
				BackColor = ColorScheme.FormButtonsColor,
				ForeColor = ColorScheme.FormForeColor
			};
			Controls.Add(BtnOk);
			Controls.Add(BtnCancel);
			AcceptButton = BtnOk;
			CancelButton = BtnCancel;

			LoadFromSettings();
			RefreshModeVisibility();
		}

		Label AddLabel(int x, int y, string text)
		{
			Label l = new Label
			{
				Text = text,
				Left = x,
				Top = y + 2,
				AutoSize = true,
				ForeColor = ColorScheme.FormForeColor
			};
			Controls.Add(l);
			return l;
		}

		CheckBox AddCheck(int x, ref int y, string text)
		{
			CheckBox c = new CheckBox
			{
				Text = text,
				Left = x,
				Top = y,
				AutoSize = true,
				ForeColor = ColorScheme.FormForeColor
			};
			Controls.Add(c);
			y += 26;
			return c;
		}

		NumericUpDown AddNud(int x, int y, int w, decimal min, decimal max, decimal inc, int decimals)
		{
			NumericUpDown n = new NumericUpDown
			{
				Left = x,
				Top = y - 2,
				Width = w,
				Minimum = min,
				Maximum = max,
				Increment = inc,
				DecimalPlaces = decimals,
				BackColor = ColorScheme.FormBackColor,
				ForeColor = ColorScheme.FormForeColor
			};
			Controls.Add(n);
			return n;
		}

		TextBox AddText(int x, int y, int w)
		{
			TextBox t = new TextBox
			{
				Left = x,
				Top = y - 2,
				Width = w,
				BackColor = ColorScheme.FormBackColor,
				ForeColor = ColorScheme.FormForeColor
			};
			Controls.Add(t);
			return t;
		}

		void LoadFromSettings()
		{
			CbEnabled.Checked = PlotterSettings.Enabled;
			NudBladeOffset.Value = Clamp(NudBladeOffset, (decimal)PlotterSettings.BladeOffsetMm);
			NudSwivelAngle.Value = Clamp(NudSwivelAngle, (decimal)PlotterSettings.SwivelAngleDeg);
			NudCutSpeed.Value = Clamp(NudCutSpeed, PlotterSettings.CutSpeed);
			NudTravelSpeed.Value = Clamp(NudTravelSpeed, PlotterSettings.TravelSpeed);
			CbDownMode.SelectedIndex = Math.Min((int)PlotterSettings.DownMode, CbDownMode.Items.Count - 1);
			TbDownCmd.Text = PlotterSettings.KnifeDownCommand;
			TbUpCmd.Text = PlotterSettings.KnifeUpCommand;
			NudZDown.Value = Clamp(NudZDown, (decimal)PlotterSettings.ZDown);
			NudZUp.Value = Clamp(NudZUp, (decimal)PlotterSettings.ZUp);
			NudZFeed.Value = Clamp(NudZFeed, PlotterSettings.ZFeed);
			NudLeadIn.Value = Clamp(NudLeadIn, (decimal)PlotterSettings.LeadInMm);
			NudOvercut.Value = Clamp(NudOvercut, (decimal)PlotterSettings.OvercutMm);
			CbOptimize.Checked = PlotterSettings.OptimizePathOrder;
			CbInnerFirst.Checked = PlotterSettings.CutInnerFirst;
			CbPauseDown.Checked = PlotterSettings.PauseOnKnifeDown;
		}

		static decimal Clamp(NumericUpDown nud, decimal v)
		{
			if (v < nud.Minimum) return nud.Minimum;
			if (v > nud.Maximum) return nud.Maximum;
			return v;
		}

		void RefreshModeVisibility()
		{
			bool spindle = CbDownMode.SelectedIndex == 0;
			bool z = CbDownMode.SelectedIndex == 1;
			bool custom = CbDownMode.SelectedIndex == 2;

			LblDownCmd.Visible = TbDownCmd.Visible = spindle || custom;
			LblUpCmd.Visible = TbUpCmd.Visible = spindle || custom;
			LblZDown.Visible = NudZDown.Visible = z;
			LblZUp.Visible = NudZUp.Visible = z;
			LblZFeed.Visible = NudZFeed.Visible = z;
		}

		void SaveToSettings()
		{
			PlotterSettings.Enabled = CbEnabled.Checked;
			PlotterSettings.BladeOffsetMm = (float)NudBladeOffset.Value;
			PlotterSettings.SwivelAngleDeg = (float)NudSwivelAngle.Value;
			PlotterSettings.CutSpeed = (int)NudCutSpeed.Value;
			PlotterSettings.TravelSpeed = (int)NudTravelSpeed.Value;
			PlotterSettings.DownMode = (PlotterSettings.KnifeActuation)CbDownMode.SelectedIndex;
			PlotterSettings.KnifeDownCommand = TbDownCmd.Text.Trim();
			PlotterSettings.KnifeUpCommand = TbUpCmd.Text.Trim();
			PlotterSettings.ZDown = (float)NudZDown.Value;
			PlotterSettings.ZUp = (float)NudZUp.Value;
			PlotterSettings.ZFeed = (int)NudZFeed.Value;
			PlotterSettings.LeadInMm = (float)NudLeadIn.Value;
			PlotterSettings.OvercutMm = (float)NudOvercut.Value;
			PlotterSettings.OptimizePathOrder = CbOptimize.Checked;
			PlotterSettings.CutInnerFirst = CbInnerFirst.Checked;
			PlotterSettings.PauseOnKnifeDown = CbPauseDown.Checked;

			// Keep existing laser speed keys in sync so converters pick cut speed without more plumbing
			if (CbEnabled.Checked)
			{
				Settings.SetObject("GrayScaleConversion.VectorizeOptions.BorderSpeed", PlotterSettings.CutSpeed);
				if (PlotterSettings.DownMode == PlotterSettings.KnifeActuation.SpindleToggle
					|| PlotterSettings.DownMode == PlotterSettings.KnifeActuation.CustomCommands)
				{
					Settings.SetObject("GrayScaleConversion.Gcode.LaserOptions.LaserOn", PlotterSettings.KnifeDownCommand);
					Settings.SetObject("GrayScaleConversion.Gcode.LaserOptions.LaserOff", PlotterSettings.KnifeUpCommand);
				}
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (DialogResult == DialogResult.OK)
				SaveToSettings();
			base.OnFormClosing(e);
		}

		public static void CreateAndShowDialog(IWin32Window parent)
		{
			using (PlotterSettingsForm f = new PlotterSettingsForm())
				f.ShowDialog(parent);
		}
	}
}
