// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LaserGRBL.Plotter
{
	public sealed class PlotterSettingsForm : Form
	{
		private readonly CheckBox mEnabled;
		private readonly ComboBox mPreset;
		private readonly TextBox mName;
		private readonly NumericUpDown mOffset;
		private readonly NumericUpDown mOvercut;
		private readonly NumericUpDown mLeadIn;
		private readonly NumericUpDown mCorner;
		private readonly NumericUpDown mLift;
		private readonly NumericUpDown mSimplify;
		private readonly NumericUpDown mMinSeg;
		private readonly NumericUpDown mPasses;
		private readonly NumericUpDown mCutSpeed;
		private readonly NumericUpDown mTravel;
		private readonly NumericUpDown mPlunge;
		private readonly NumericUpDown mDwellDown;
		private readonly NumericUpDown mDwellUp;
		private readonly ComboBox mPen;
		private readonly NumericUpDown mServoUp;
		private readonly NumericUpDown mServoDown;
		private readonly NumericUpDown mZUp;
		private readonly NumericUpDown mZDown;
		private readonly CheckBox mOrder;
		private readonly CheckBox mInside;
		private readonly CheckBox mLinearize;
		private readonly NumericUpDown mMargin;
		private readonly NumericUpDown mLaserX;
		private readonly NumericUpDown mLaserY;
		private bool mLoading;

		public static bool Edit(IWin32Window owner)
		{
			using (PlotterSettingsForm form = new PlotterSettingsForm())
				return form.ShowDialog(owner) == DialogResult.OK;
		}

		public static string Tr(string english, string russian)
		{
			if (Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ru")
				return russian;
			return english;
		}

		public PlotterSettingsForm()
		{
			Text = Tr("Plotter / drag knife", "Плоттер / нож с пассивным поворотом");
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(720, 900);
			Font = SystemFonts.MessageBoxFont;
			try
			{
				BackColor = LaserGRBL.ColorScheme.FormBackColor;
				ForeColor = LaserGRBL.ColorScheme.FormForeColor;
			}
			catch { }

			TableLayoutPanel table = new TableLayoutPanel();
			table.Dock = DockStyle.Fill;
			table.Padding = new Padding(10, 8, 10, 8);
			table.AutoScroll = true;
			table.ColumnCount = 2;
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
			Controls.Add(table);

			mEnabled = new CheckBox();
			mEnabled.Text = Tr("Plotter mode when importing images, SVG and DXF", "Режим плоттера при импорте изображений, SVG и DXF");
			mEnabled.AutoSize = true;
			mEnabled.Margin = new Padding(3, 6, 3, 8);
			table.Controls.Add(mEnabled, 0, 0);
			table.SetColumnSpan(mEnabled, 2);

			mPreset = AddCombo(table, Tr("Blade preset", "Пресет лезвия"));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Rubber, Tr("Rubber blade 3.60 mm", "Резина, лезвие 3,60 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Pen, Tr("Pen (no offset)", "Перо (без смещения)")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.VinylFine, Tr("Vinyl blade 0.25 mm", "Винил 0.25 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Craft, Tr("Craft blade 0.50 mm", "Канцелярское 0.50 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Deep, Tr("Deep blade 0.75 mm", "Глубокое 0.75 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Custom, Tr("Custom", "Своё")));
			mPreset.SelectedIndexChanged += PresetChanged;

			mName = AddText(table, Tr("Blade name", "Имя лезвия"));
			mOffset = AddNum(table, Tr("Blade offset, mm", "Смещение лезвия, мм"), 0, 20, 2, 0.05m);
			mOvercut = AddNum(table, Tr("Overcut, mm", "Перерез (overcut), мм"), 0, 50, 2, 0.1m);
			mLeadIn = AddNum(table, Tr("Lead-in, mm", "Заход для разворота, мм"), 0, 50, 2, 0.1m);
			mCorner = AddNum(table, Tr("Sharp corner from, deg", "Острый угол от, град"), 0, 180, 0, 1);
			mLift = AddNum(table, Tr("Lift knife above, deg (0 = never)", "Поднимать нож свыше, град (0 = никогда)"), 0, 180, 0, 1);
			mSimplify = AddNum(table, Tr("Simplify tolerance, mm", "Упрощение пути, мм"), 0, 5, 2, 0.01m);
			mMinSeg = AddNum(table, Tr("Minimum segment, mm", "Мин. отрезок, мм"), 0, 5, 2, 0.01m);
			mPasses = AddNum(table, Tr("Passes", "Проходы"), 1, 30, 0, 1);

			mMargin = AddNum(table, Tr("Start margin outside the drawing, mm", "Отступ снаружи габарита, мм"), 0, 500, 1, 1);
			mLaserX = AddNum(table, Tr("Positioning laser offset X, mm", "Смещение позиционного лазера X, мм"), -500, 500, 1, 1);
			mLaserY = AddNum(table, Tr("Positioning laser offset Y, mm", "Смещение позиционного лазера Y, мм"), -500, 500, 1, 1);
			mCutSpeed = AddNum(table, Tr("Cut speed, mm/min", "Скорость реза, мм/мин"), 1, 50000, 0, 50);
			mTravel = AddNum(table, Tr("Travel speed, mm/min (0 = cut speed, no G0 while the knife stays down)", "Переезд, мм/мин (0 = скорость реза; нож не поднимается)"), 0, 50000, 0, 50);
			mPlunge = AddNum(table, Tr("Plunge speed, mm/min", "Скорость опускания, мм/мин"), 1, 50000, 0, 50);
			mDwellDown = AddNum(table, Tr("Dwell after down, ms", "Пауза после опускания, мс"), 0, 5000, 0, 10);
			mDwellUp = AddNum(table, Tr("Dwell after up, ms", "Пауза после подъёма, мс"), 0, 5000, 0, 10);

			mPen = AddCombo(table, Tr("Knife up / down", "Подъём / опускание ножа"));
			mPen.Items.Add(new PenItem(PlotterPenMode.Manual, Tr("Manual height, XY only", "Высота вручную, только X и Y")));
			mPen.Items.Add(new PenItem(PlotterPenMode.ServoPwm, Tr("Servo on laser PWM (M3 S)", "Серво на ШИМ лазера (M3 S)")));
			mPen.Items.Add(new PenItem(PlotterPenMode.Digital, Tr("Solenoid M3 down / M5 up", "Соленоид M3 вниз / M5 вверх")));
			mPen.Items.Add(new PenItem(PlotterPenMode.ZAxis, Tr("Z axis", "Ось Z")));
			mPen.SelectedIndexChanged += PenModeChanged;

			mServoUp = AddNum(table, Tr("S value, knife up", "Значение S, нож вверх"), 0, 100000, 0, 1);
			mServoDown = AddNum(table, Tr("S value, knife down", "Значение S, нож вниз"), 0, 100000, 0, 1);
			mZUp = AddNum(table, Tr("Z up, mm", "Z вверх, мм"), -50, 200, 2, 0.1m);
			mZDown = AddNum(table, Tr("Z down, mm", "Z вниз, мм"), -50, 200, 2, 0.1m);
			mOrder = AddCheck(table, Tr("Sort contours (short travels, small swivels)", "Сортировать контуры (короче переезды, меньше разворотов)"));
			mInside = AddCheck(table, Tr("Cut holes first when the knife can reach them", "Сначала отверстия, если к ним можно подойти"));
			mLinearize = AddCheck(table, Tr("Replace corner arcs with short lines", "Заменять дуги поворота короткими отрезками"));

			Label help = new Label();
			help.AutoSize = false;
			help.Height = 156;
			help.Dock = DockStyle.Top;
			help.Text = Tr(
				"Aim the positioning laser at the bottom-left corner of the sheet and zero XY. The laser sits to the right of the blade (default 10 mm, Y unchanged). The job moves the blade 20 mm outside that corner, to the left and toward you, then travels into the first contour. Every XY move is G1. A gentle turn follows the curve; a sharp corner swings the holder around the tip.",
				"Наведите позиционный лазер на левый нижний угол листа и обнулите XY. Лазер правее лезвия (по умолчанию 10 мм, по Y без смещения). Программа сначала уводит лезвие на 20 мм за этот угол — влево и на себя — и только потом едет к первому контуру и врезается в лист. Любое движение X Y — это G1. Плавный поворот идёт по кривой, острый — дугой держателя вокруг острия.");
			table.Controls.Add(help, 0, table.RowCount);
			table.SetColumnSpan(help, 2);
			table.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));

			FlowLayoutPanel buttons = new FlowLayoutPanel();
			buttons.FlowDirection = FlowDirection.RightToLeft;
			buttons.Dock = DockStyle.Bottom;
			buttons.Height = 42;
			buttons.Padding = new Padding(8, 6, 8, 6);
			Button ok = new Button();
			ok.Text = "OK";
			ok.DialogResult = DialogResult.OK;
			ok.Width = 100;
			Button cancel = new Button();
			cancel.Text = Tr("Cancel", "Отмена");
			cancel.DialogResult = DialogResult.Cancel;
			cancel.Width = 100;
			buttons.Controls.Add(ok);
			buttons.Controls.Add(cancel);
			Controls.Add(buttons);
			AcceptButton = ok;
			CancelButton = cancel;

			mOffset.ValueChanged += MarkCustom;
			mOvercut.ValueChanged += MarkCustom;
			mLeadIn.ValueChanged += MarkCustom;
			LoadValues(PlotterSettings.Load());
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (DialogResult == DialogResult.OK)
				PlotterSettings.Save(ReadValues());
			base.OnFormClosing(e);
		}

		private void LoadValues(PlotterConfig cfg)
		{
			mLoading = true;
			mEnabled.Checked = cfg.Enabled;
			mName.Text = cfg.BladeName;
			SelectPreset(cfg.Preset);
			Set(mOffset, (decimal)cfg.BladeOffsetMm);
			Set(mOvercut, (decimal)cfg.OvercutMm);
			Set(mLeadIn, (decimal)cfg.LeadInMm);
			Set(mCorner, (decimal)cfg.CornerThresholdDeg);
			Set(mLift, (decimal)cfg.LiftAngleDeg);
			Set(mSimplify, (decimal)cfg.SimplifyMm);
			Set(mMinSeg, (decimal)cfg.MinSegmentMm);
			Set(mPasses, cfg.Passes);
			Set(mMargin, (decimal)cfg.StartMarginMm);
			Set(mLaserX, (decimal)cfg.LaserOffsetX);
			Set(mLaserY, (decimal)cfg.LaserOffsetY);
			Set(mCutSpeed, cfg.CutSpeed);
			Set(mTravel, cfg.TravelSpeed);
			Set(mPlunge, cfg.PlungeSpeed);
			Set(mDwellDown, cfg.DwellDownMs);
			Set(mDwellUp, cfg.DwellUpMs);
			SelectPen(cfg.PenMode);
			Set(mServoUp, cfg.ServoUp);
			Set(mServoDown, cfg.ServoDown);
			Set(mZUp, (decimal)cfg.PenUpZ);
			Set(mZDown, (decimal)cfg.PenDownZ);
			mOrder.Checked = cfg.OptimizeOrder;
			mInside.Checked = cfg.InsideFirst;
			mLinearize.Checked = cfg.LinearizeArcs;
			mLoading = false;
			UpdatePenFields();
		}

		private void PenModeChanged(object sender, EventArgs e)
		{
			if (!mLoading)
				UpdatePenFields();
		}

		private void UpdatePenFields()
		{
			bool manual = mPen.SelectedItem != null && ((PenItem)mPen.SelectedItem).Mode == PlotterPenMode.Manual;
			mServoUp.Enabled = !manual;
			mServoDown.Enabled = !manual;
			mZUp.Enabled = !manual;
			mZDown.Enabled = !manual;
			mPlunge.Enabled = !manual;
			mDwellDown.Enabled = !manual;
			mDwellUp.Enabled = !manual;
			mLift.Enabled = !manual;
		}

		private PlotterConfig ReadValues()
		{
			PlotterConfig cfg = PlotterSettings.Load();
			cfg.Enabled = mEnabled.Checked;
			cfg.Preset = ((PresetItem)mPreset.SelectedItem).Preset;
			cfg.BladeName = mName.Text.Trim();
			cfg.BladeOffsetMm = (double)mOffset.Value;
			cfg.OvercutMm = (double)mOvercut.Value;
			cfg.LeadInMm = (double)mLeadIn.Value;
			cfg.CornerThresholdDeg = (double)mCorner.Value;
			cfg.LiftAngleDeg = (double)mLift.Value;
			cfg.SimplifyMm = (double)mSimplify.Value;
			cfg.MinSegmentMm = (double)mMinSeg.Value;
			cfg.Passes = (int)mPasses.Value;
			cfg.StartMarginMm = (double)mMargin.Value;
			cfg.LaserOffsetX = (double)mLaserX.Value;
			cfg.LaserOffsetY = (double)mLaserY.Value;
			cfg.CutSpeed = (int)mCutSpeed.Value;
			cfg.TravelSpeed = (int)mTravel.Value;
			cfg.PlungeSpeed = (int)mPlunge.Value;
			cfg.DwellDownMs = (int)mDwellDown.Value;
			cfg.DwellUpMs = (int)mDwellUp.Value;
			cfg.PenMode = ((PenItem)mPen.SelectedItem).Mode;
			cfg.ServoUp = (int)mServoUp.Value;
			cfg.ServoDown = (int)mServoDown.Value;
			cfg.PenUpZ = (double)mZUp.Value;
			cfg.PenDownZ = (double)mZDown.Value;
			cfg.OptimizeOrder = mOrder.Checked;
			cfg.InsideFirst = mInside.Checked;
			cfg.LinearizeArcs = mLinearize.Checked;
			return cfg;
		}

		private void PresetChanged(object sender, EventArgs e)
		{
			if (mLoading || mPreset.SelectedItem == null)
				return;
			PlotterBladePreset preset = ((PresetItem)mPreset.SelectedItem).Preset;
			if (preset == PlotterBladePreset.Custom)
				return;
			PlotterConfig cfg = new PlotterConfig();
			cfg.ApplyPreset(preset);
			mLoading = true;
			mName.Text = cfg.BladeName;
			Set(mOffset, (decimal)cfg.BladeOffsetMm);
			Set(mOvercut, (decimal)cfg.OvercutMm);
			Set(mLeadIn, (decimal)cfg.LeadInMm);
			mLoading = false;
		}

		private void MarkCustom(object sender, EventArgs e)
		{
			if (mLoading)
				return;
			mLoading = true;
			SelectPreset(PlotterBladePreset.Custom);
			mLoading = false;
		}

		private void SelectPreset(PlotterBladePreset preset)
		{
			for (int i = 0; i < mPreset.Items.Count; i++)
			{
				if (((PresetItem)mPreset.Items[i]).Preset == preset)
				{
					mPreset.SelectedIndex = i;
					return;
				}
			}
		}

		private void SelectPen(PlotterPenMode mode)
		{
			for (int i = 0; i < mPen.Items.Count; i++)
			{
				if (((PenItem)mPen.Items[i]).Mode == mode)
				{
					mPen.SelectedIndex = i;
					return;
				}
			}
		}

		private NumericUpDown AddNum(TableLayoutPanel table, string label, decimal min, decimal max, int decimals, decimal step)
		{
			int row = table.RowCount;
			table.RowCount = row + 1;
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			Label caption = new Label();
			caption.Text = label;
			caption.AutoSize = true;
			caption.Anchor = AnchorStyles.Left;
			caption.Margin = new Padding(3, 6, 3, 3);
			NumericUpDown num = new NumericUpDown();
			num.Minimum = min;
			num.Maximum = max;
			num.DecimalPlaces = decimals;
			num.Increment = step;
			num.Width = 120;
			num.Anchor = AnchorStyles.Left;
			num.ThousandsSeparator = false;
			table.Controls.Add(caption, 0, row);
			table.Controls.Add(num, 1, row);
			return num;
		}

		private ComboBox AddCombo(TableLayoutPanel table, string label)
		{
			int row = table.RowCount;
			table.RowCount = row + 1;
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			Label caption = new Label();
			caption.Text = label;
			caption.AutoSize = true;
			caption.Anchor = AnchorStyles.Left;
			caption.Margin = new Padding(3, 8, 3, 3);
			ComboBox box = new ComboBox();
			box.DropDownStyle = ComboBoxStyle.DropDownList;
			box.Width = 280;
			box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			table.Controls.Add(caption, 0, row);
			table.Controls.Add(box, 1, row);
			return box;
		}

		private TextBox AddText(TableLayoutPanel table, string label)
		{
			int row = table.RowCount;
			table.RowCount = row + 1;
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			Label caption = new Label();
			caption.Text = label;
			caption.AutoSize = true;
			caption.Anchor = AnchorStyles.Left;
			TextBox box = new TextBox();
			box.Width = 280;
			box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			table.Controls.Add(caption, 0, row);
			table.Controls.Add(box, 1, row);
			return box;
		}

		private CheckBox AddCheck(TableLayoutPanel table, string label)
		{
			int row = table.RowCount;
			table.RowCount = row + 1;
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			CheckBox box = new CheckBox();
			box.Text = label;
			box.AutoSize = true;
			box.Margin = new Padding(3, 4, 3, 4);
			table.Controls.Add(box, 0, row);
			table.SetColumnSpan(box, 2);
			return box;
		}

		private static void Set(NumericUpDown num, decimal value)
		{
			if (value < num.Minimum) value = num.Minimum;
			if (value > num.Maximum) value = num.Maximum;
			num.Value = value;
		}

		private sealed class PresetItem
		{
			public PlotterBladePreset Preset;
			private readonly string mText;
			public PresetItem(PlotterBladePreset preset, string text)
			{
				Preset = preset;
				mText = text;
			}
			public override string ToString() { return mText; }
		}

		private sealed class PenItem
		{
			public PlotterPenMode Mode;
			private readonly string mText;
			public PenItem(PlotterPenMode mode, string text)
			{
				Mode = mode;
				mText = text;
			}
			public override string ToString() { return mText; }
		}
	}
}
