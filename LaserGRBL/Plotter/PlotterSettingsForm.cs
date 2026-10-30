// Plotter / drag-knife settings UI (Russian labels; code-built, no Designer)
// Copyright (c) 2026 — fork extension under GPLv3

using System;
using System.Drawing;
using System.Windows.Forms;

namespace LaserGRBL.Plotter
{
	public class PlotterSettingsForm : Form
	{
		CheckBox CbEnabled;
		CheckBox CbContinuous;
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
		Label LblTravel;
		Label LblDownCmd;
		Label LblUpCmd;
		Label LblZDown;
		Label LblZUp;
		Label LblZFeed;
		Label LblHelp;
		Button BtnOk;
		Button BtnCancel;

		public PlotterSettingsForm()
		{
			Text = "Режим плоттера / ножа";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			StartPosition = FormStartPosition.CenterParent;
			ClientSize = new Size(520, 580);
			Font = new Font("Segoe UI", 9f);

			BackColor = ColorScheme.FormBackColor;
			ForeColor = ColorScheme.FormForeColor;

			int y = 12;
			int left = 16;
			int ctrlLeft = 280;
			int ctrlW = 200;
			int row = 28;

			CbEnabled = AddCheck(left, ref y, "Включить режим плоттера (нож)");
			CbContinuous = AddCheck(left, ref y, "Непрерывный рез (без подъёма; переезды тоже режут)");
			y += 4;

			AddLabel(left, y, "Смещение лезвия от оси поворота (мм)");
			NudBladeOffset = AddNud(ctrlLeft, y, ctrlW, 0, 20, 0.05m, 2);
			y += row;

			AddLabel(left, y, "Порог угла для дуги поворота (°)");
			NudSwivelAngle = AddNud(ctrlLeft, y, ctrlW, 1, 180, 1, 0);
			y += row;

			AddLabel(left, y, "Скорость реза (мм/мин)");
			NudCutSpeed = AddNud(ctrlLeft, y, ctrlW, 1, 30000, 10, 0);
			y += row;

			LblTravel = AddLabel(left, y, "Скорость холостого хода (мм/мин)");
			NudTravelSpeed = AddNud(ctrlLeft, y, ctrlW, 1, 30000, 10, 0);
			y += row + 4;

			AddLabel(left, y, "Способ опускания / подъёма ножа");
			CbDownMode = new ComboBox
			{
				Left = ctrlLeft,
				Top = y - 2,
				Width = ctrlW,
				DropDownStyle = ComboBoxStyle.DropDownList,
				BackColor = ColorScheme.FormBackColor,
				ForeColor = ColorScheme.FormForeColor
			};
			CbDownMode.Items.Add("Всегда вниз (высота вручную, без Z)");
			CbDownMode.Items.Add("Пин шпинделя (M3/M5)");
			CbDownMode.Items.Add("Ось Z");
			CbDownMode.Items.Add("Свои G-code команды");
			CbDownMode.SelectedIndexChanged += (s, e) => RefreshModeVisibility();
			Controls.Add(CbDownMode);
			y += row;

			LblDownCmd = AddLabel(left, y, "Команда ОПУСТИТЬ нож");
			TbDownCmd = AddText(ctrlLeft, y, ctrlW);
			y += row;

			LblUpCmd = AddLabel(left, y, "Команда ПОДНЯТЬ нож");
			TbUpCmd = AddText(ctrlLeft, y, ctrlW);
			y += row;

			LblZDown = AddLabel(left, y, "Z вниз (рез)");
			NudZDown = AddNud(ctrlLeft, y, ctrlW, -100, 100, 0.1m, 2);
			y += row;

			LblZUp = AddLabel(left, y, "Z вверх (переезд)");
			NudZUp = AddNud(ctrlLeft, y, ctrlW, -100, 100, 0.1m, 2);
			y += row;

			LblZFeed = AddLabel(left, y, "Подача Z (мм/мин)");
			NudZFeed = AddNud(ctrlLeft, y, ctrlW, 1, 10000, 10, 0);
			y += row + 4;

			AddLabel(left, y, "Lead-in — разгон перед контуром (мм, 0=выкл)");
			NudLeadIn = AddNud(ctrlLeft, y, ctrlW, 0, 50, 0.1m, 2);
			y += row;

			AddLabel(left, y, "Overcut — дорез угла (мм, 0=выкл)");
			NudOvercut = AddNud(ctrlLeft, y, ctrlW, 0, 50, 0.1m, 2);
			y += row + 4;

			CbOptimize = AddCheck(left, ref y, "Оптимизация порядка (ближайший сосед; может нарушить «сначала внешние»)");
			CbInnerFirst = AddCheck(left, ref y, "Сначала внутренние контуры (для сквозного реза обычно выкл.)");
			CbPauseDown = AddCheck(left, ref y, "Пауза M0 перед опусканием ножа (выборочно)");

			LblHelp = new Label
			{
				Left = left,
				Top = y + 4,
				Width = ClientSize.Width - 32,
				Height = 48,
				ForeColor = ColorScheme.FormForeColor,
				Text = "Подсказка: kiss-cut = надрез верхнего слоя (винил); сквозной рез = насквозь. " +
				       "Lead-in выравнивает лезвие до контура; overcut добивает угол на замкнутом пути."
			};
			Controls.Add(LblHelp);

			BtnOk = new Button
			{
				Text = "Сохранить",
				DialogResult = DialogResult.OK,
				Left = ClientSize.Width - 200,
				Top = ClientSize.Height - 40,
				Width = 90,
				BackColor = ColorScheme.FormButtonsColor,
				ForeColor = ColorScheme.FormForeColor
			};
			BtnCancel = new Button
			{
				Text = "Отмена",
				DialogResult = DialogResult.Cancel,
				Left = ClientSize.Width - 100,
				Top = ClientSize.Height - 40,
				Width = 85,
				BackColor = ColorScheme.FormButtonsColor,
				ForeColor = ColorScheme.FormForeColor
			};
			Controls.Add(BtnOk);
			Controls.Add(BtnCancel);
			AcceptButton = BtnOk;
			CancelButton = BtnCancel;

			CbContinuous.CheckedChanged += (s, e) => RefreshModeVisibility();

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
				MaximumSize = new Size(ClientSize.Width - 32, 0),
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
			CbContinuous.Checked = PlotterSettings.ContinuousCut;
			NudBladeOffset.Value = Clamp(NudBladeOffset, (decimal)PlotterSettings.BladeOffsetMm);
			NudSwivelAngle.Value = Clamp(NudSwivelAngle, (decimal)PlotterSettings.SwivelAngleDeg);
			NudCutSpeed.Value = Clamp(NudCutSpeed, PlotterSettings.CutSpeed);
			NudTravelSpeed.Value = Clamp(NudTravelSpeed, PlotterSettings.TravelSpeed);
			CbDownMode.SelectedIndex = Math.Min(Math.Max((int)PlotterSettings.DownMode, 0), CbDownMode.Items.Count - 1);
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
			bool always = CbDownMode.SelectedIndex == 0;
			bool spindle = CbDownMode.SelectedIndex == 1;
			bool z = CbDownMode.SelectedIndex == 2;
			bool custom = CbDownMode.SelectedIndex == 3;
			bool continuous = CbContinuous.Checked;

			LblDownCmd.Visible = TbDownCmd.Visible = spindle || custom;
			LblUpCmd.Visible = TbUpCmd.Visible = spindle || custom;
			LblZDown.Visible = NudZDown.Visible = z;
			LblZUp.Visible = NudZUp.Visible = z;
			LblZFeed.Visible = NudZFeed.Visible = z;
			LblTravel.Visible = NudTravelSpeed.Visible = !continuous;
			CbPauseDown.Enabled = !always;
		}

		void SaveToSettings()
		{
			PlotterSettings.Enabled = CbEnabled.Checked;
			PlotterSettings.ContinuousCut = CbContinuous.Checked;
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
