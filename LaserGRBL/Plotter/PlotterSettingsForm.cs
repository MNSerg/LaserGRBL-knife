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
		private readonly ToolTip mTip;
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
		private readonly ComboBox mRoute;
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
			Text = Tr("Plotter / drag knife", "Плоттер / нож");
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;
			AutoScaleMode = AutoScaleMode.Font;
			Font = SystemFonts.MessageBoxFont;
			ClientSize = new Size(620, 640);
			try
			{
				BackColor = LaserGRBL.ColorScheme.FormBackColor;
				ForeColor = LaserGRBL.ColorScheme.FormForeColor;
			}
			catch { }

			mTip = new ToolTip();
			mTip.AutoPopDelay = 14000;
			mTip.InitialDelay = 350;
			mTip.ReshowDelay = 200;
			mTip.ShowAlways = true;

			TableLayoutPanel table = new TableLayoutPanel();
			table.Dock = DockStyle.Fill;
			table.Padding = new Padding(12, 8, 12, 4);
			table.ColumnCount = 4;
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			Controls.Add(table);

			mEnabled = new CheckBox();
			mEnabled.Text = Tr("Use plotter mode for images, SVG and DXF", "Режим плоттера для картинок, SVG и DXF");
			mEnabled.AutoSize = true;
			mEnabled.Anchor = AnchorStyles.Left;
			mEnabled.Margin = new Padding(0, 4, 0, 2);
			AddRow(table, 28);
			table.Controls.Add(mEnabled, 0, 0);
			table.SetColumnSpan(mEnabled, 4);
			mTip.SetToolTip(mEnabled, Tr(
				"When this is on, importing an image, SVG or DXF builds a drag-knife path. Apply plotter to a file that is already open from the File menu, once.",
				"Включено — импорт картинки, SVG или DXF сразу строит путь ножа. Уже открытый файл обрабатывается один раз из меню «Файл»."));

			Section(table, Tr("Blade", "Лезвие"));
			mPreset = PlaceCombo(table, Tr("Preset", "Пресет"), Tr(
				"Fills offset, overcut and lead-in. Editing those three switches the preset to Custom.",
				"Подставляет смещение, перерез и заход. Если поменять эти три числа, пресет станет «Своё»."));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Rubber, Tr("Rubber 3.60 mm", "Резина 3,60 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Pen, Tr("Pen, no offset", "Перо, без смещения")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.VinylFine, Tr("Vinyl 0.25 mm", "Винил 0,25 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Craft, Tr("Craft 0.50 mm", "Канцелярское 0,50 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Deep, Tr("Deep 0.75 mm", "Глубокое 0,75 мм")));
			mPreset.Items.Add(new PresetItem(PlotterBladePreset.Custom, Tr("Custom", "Своё")));
			mPreset.SelectedIndexChanged += PresetChanged;

			mName = PlaceText(table, 0, Tr("Name", "Имя"), Tr(
				"Comment in the G-code only. It does not change the path.",
				"Только комментарий в G-коде. На путь не влияет."));
			mPasses = PlaceNum(table, 2, Tr("Passes", "Проходы"), Tr(
				"How many times to trace each contour before moving to the next one.",
				"Сколько раз обойти контур, прежде чем перейти к следующему."), 1, 30, 0, 1);

			mOffset = PlaceNum(table, 0, Tr("Offset, mm", "Смещение, мм"), Tr(
				"How far the swivel axis leads the tip. Also the radius of the swing at a sharp corner.",
				"Насколько ось впереди острия. Это же радиус дуги на остром углу."), 0, 20, 2, 0.05m);
			mOvercut = PlaceNum(table, 2, Tr("Overcut, mm", "Перерез, мм"), Tr(
				"How far the tip runs past the seam of a closed contour. Keep it about the same as the offset, and shorter than the first side.",
				"Насколько остриё заходит за шов. Обычно как смещение, и короче первой стороны."), 0, 50, 2, 0.1m);

			mLeadIn = PlaceNum(table, 0, Tr("Lead-in, mm", "Заход, мм"), Tr(
				"Straight run that lines the blade up before the drawing. It is also a cut.",
				"Прямой участок, на котором лезвие выравнивается до рисунка. Он тоже режется."), 0, 50, 2, 0.1m);
			mCorner = PlaceNum(table, 2, Tr("Sharp from, °", "Острый от, °"), Tr(
				"An isolated corner at least this sharp swings the holder around the tip. Arcs and fillets stay smooth at any threshold.",
				"Отдельный угол не слабее этого числа получает дугу разворота. Дуги и скругления остаются плавными при любом пороге."), 0, 180, 0, 1);

			mSimplify = PlaceNum(table, 0, Tr("Simplify, mm", "Упрощение, мм"), Tr(
				"Removes wobble smaller than this before the blade offset is applied. Too much turns a fillet into one sharp corner.",
				"Стирает дрожание мельче этого до компенсации лезвия. Слишком большое значение схлопывает скругление в один острый угол."), 0, 5, 2, 0.01m);
			mMinSeg = PlaceNum(table, 2, Tr("Min segment, mm", "Мин. отрезок, мм"), Tr(
				"Drops pieces shorter than this after simplifying.",
				"Убирает куски короче этого уже после упрощения."), 0, 5, 2, 0.01m);

			Section(table, Tr("Sheet and path", "Лист и путь"));
			mRoute = PlaceCombo(table, Tr("Optimization", "Оптимизация"), Tr(
				"Nearest: the closest contour you can reach without crossing another. Outside in: shapes on the sheet edge first, then toward the middle. Inside out: a hole before its frame when the approach is already clear. File order: do not reorder. In every mode the knife walks around closed contours instead of cutting through them. A straight bridge remains only into a hole that cannot be reached from outside.",
				"До ближайшего — ближайший контур, до которого можно доехать, не пересекая другие. Снаружи внутрь — сначала фигуры у края листа, потом ближе к середине. Изнутри наружу — отверстие раньше рамки, если подъезд уже свободен. Как в файле — порядок не менять. В любом режиме нож обходит замкнутые контуры. Прямая сквозь контур остаётся только мостиком в дырку, до которой снаружи не добраться."));
			mRoute.Items.Add(new RouteItem(PlotterRouteMode.Nearest, Tr("Nearest contour", "До ближайшего контура")));
			mRoute.Items.Add(new RouteItem(PlotterRouteMode.OutsideIn, Tr("Outside in", "Снаружи внутрь")));
			mRoute.Items.Add(new RouteItem(PlotterRouteMode.InsideOut, Tr("Inside out", "Изнутри наружу")));
			mRoute.Items.Add(new RouteItem(PlotterRouteMode.FileOrder, Tr("File order", "Как в файле")));
			mRoute.DropDownWidth = 360;

			mMargin = PlaceNum(table, 0, Tr("Margin, mm", "Отступ, мм"), Tr(
				"Park distance outside the drawing corner, both left and toward you. 0 disables the park move.",
				"Насколько парковка левее и ближе к вам, чем угол рисунка. 0 — без парковки."), 0, 500, 1, 1);
			mLaserX = PlaceNum(table, 0, Tr("Laser X, mm", "Лазер X, мм"), Tr(
				"How far the positioning laser sits to the right of the blade. Aim the laser at the sheet corner, then zero XY.",
				"Насколько лазер правее лезвия. Наведите лазер на угол листа и обнулите XY."), -500, 500, 1, 1);
			mLaserY = PlaceNum(table, 2, Tr("Laser Y, mm", "Лазер Y, мм"), Tr(
				"How far the laser sits farther from you than the blade. Positive Y is away from the operator.",
				"Насколько лазер дальше от вас, чем лезвие. Плюс Y — от себя."), -500, 500, 1, 1);

			mCutSpeed = PlaceNum(table, 0, Tr("Cut, mm/min", "Рез, мм/мин"), Tr(
				"Feed on the contour, the lead-in, the corner swing and the overcut.",
				"Подача на контуре, заходе, дуге угла и перерезе."), 1, 50000, 0, 50);
			mTravel = PlaceNum(table, 2, Tr("Travel, mm/min", "Переезд, мм/мин"), Tr(
				"Feed between contours. 0 uses the cut speed. With manual height the move is still G1 and still cuts, so it is routed around other contours.",
				"Подача между контурами. 0 — скорость реза. При высоте вручную это всё равно G1 и всё равно режет, поэтому путь идёт в обход."), 0, 50000, 0, 50);

			mPen = PlaceCombo(table, Tr("Knife", "Нож"), Tr(
				"Manual height: the knife never lifts, and every XY move is G1. The other three modes lift between contours.",
				"Высота вручную — нож не поднимается, любое XY это G1. Остальные три режима поднимают нож между контурами."));
			mPen.Items.Add(new PenItem(PlotterPenMode.Manual, Tr("Manual height, XY only", "Высота вручную, только X и Y")));
			mPen.Items.Add(new PenItem(PlotterPenMode.ServoPwm, Tr("Servo on laser PWM (M3 S)", "Серво на ШИМ лазера (M3 S)")));
			mPen.Items.Add(new PenItem(PlotterPenMode.Digital, Tr("Solenoid M3 down / M5 up", "Соленоид M3 вниз / M5 вверх")));
			mPen.Items.Add(new PenItem(PlotterPenMode.ZAxis, Tr("Z axis", "Ось Z")));
			mPen.SelectedIndexChanged += PenModeChanged;

			Section(table, Tr("Lift (ignored for manual height)", "Подъём (для ручной высоты не используется)"));
			mLift = PlaceNum(table, 0, Tr("Lift above, °", "Подъём от, °"), Tr(
				"With a liftable knife, a turn at least this sharp lifts instead of swinging. 0 never lifts. Manual height ignores this.",
				"Если нож умеет подниматься, поворот не слабее этого поднимает его вместо дуги. 0 — не поднимать. Ручная высота это игнорирует."), 0, 180, 0, 1);
			mPlunge = PlaceNum(table, 2, Tr("Plunge, mm/min", "Врезка, мм/мин"), Tr(
				"Z feed when lowering the knife. Manual height ignores this.",
				"Подача оси Z при опускании. Ручная высота это игнорирует."), 1, 50000, 0, 50);

			mDwellDown = PlaceNum(table, 0, Tr("Pause down, ms", "Пауза вниз, мс"), Tr(
				"Wait after the knife goes down, before the cut. Manual height ignores this.",
				"Пауза после опускания, до реза. Ручная высота это игнорирует."), 0, 5000, 0, 10);
			mDwellUp = PlaceNum(table, 2, Tr("Pause up, ms", "Пауза вверх, мс"), Tr(
				"Wait after the knife lifts, before the travel. Manual height ignores this.",
				"Пауза после подъёма, до переезда. Ручная высота это игнорирует."), 0, 5000, 0, 10);

			mServoUp = PlaceNum(table, 0, Tr("S up", "S вверх"), Tr(
				"PWM or solenoid value with the knife up.",
				"Значение ШИМ или соленоида, когда нож поднят."), 0, 100000, 0, 1);
			mServoDown = PlaceNum(table, 2, Tr("S down", "S вниз"), Tr(
				"PWM or solenoid value with the knife down.",
				"Значение ШИМ или соленоида, когда нож опущен."), 0, 100000, 0, 1);

			mZUp = PlaceNum(table, 0, Tr("Z up, mm", "Z вверх, мм"), Tr("Z position with the knife up.", "Координата Z, когда нож поднят."), -50, 200, 2, 0.1m);
			mZDown = PlaceNum(table, 2, Tr("Z down, mm", "Z вниз, мм"), Tr("Z position with the knife down.", "Координата Z, когда нож опущен."), -50, 200, 2, 0.1m);

			mLinearize = new CheckBox();
			mLinearize.Text = Tr("Corner arcs as short lines", "Дуги угла — короткими отрезками");
			mLinearize.AutoSize = true;
			mLinearize.Anchor = AnchorStyles.Left;
			mLinearize.Margin = new Padding(0, 2, 0, 2);
			AddRow(table, 26);
			table.Controls.Add(mLinearize, 0, table.RowCount - 1);
			table.SetColumnSpan(mLinearize, 4);
			mTip.SetToolTip(mLinearize, Tr(
				"Same corner swing, written as short G1 moves every 10 degrees instead of G2 or G3.",
				"Та же дуга угла, но серией G1 через 10°, без G2 и G3."));

			Label help = new Label();
			help.AutoSize = false;
			help.Dock = DockStyle.Fill;
			help.TextAlign = ContentAlignment.TopLeft;
			help.Margin = new Padding(0, 4, 0, 0);
			help.Text = Tr(
				"Aim the laser at the bottom-left corner of the sheet and zero XY. The blade parks outside that corner and walks around other closed contours. Hover a field for what it does to the path.",
				"Наведите лазер на левый нижний угол листа и обнулите XY. Лезвие паркуется снаружи этого угла и обходит чужие замкнутые контуры. Наведите курсор на поле — подсказка, как оно меняет путь.");
			AddRow(table, 48);
			table.Controls.Add(help, 0, table.RowCount - 1);
			table.SetColumnSpan(help, 4);

			FlowLayoutPanel buttons = new FlowLayoutPanel();
			buttons.FlowDirection = FlowDirection.RightToLeft;
			buttons.Dock = DockStyle.Bottom;
			buttons.Height = 40;
			buttons.Padding = new Padding(8, 6, 8, 6);
			Button ok = new Button();
			ok.Text = "OK";
			ok.DialogResult = DialogResult.OK;
			ok.Width = 88;
			Button cancel = new Button();
			cancel.Text = Tr("Cancel", "Отмена");
			cancel.DialogResult = DialogResult.Cancel;
			cancel.Width = 88;
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
			SelectRoute(cfg.RouteMode);
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
			cfg.RouteMode = ((RouteItem)mRoute.SelectedItem).Mode;
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

		private void SelectRoute(PlotterRouteMode mode)
		{
			for (int i = 0; i < mRoute.Items.Count; i++)
			{
				if (((RouteItem)mRoute.Items[i]).Mode == mode)
				{
					mRoute.SelectedIndex = i;
					return;
				}
			}
		}

		private void Section(TableLayoutPanel table, string text)
		{
			AddRow(table, 22);
			Label label = new Label();
			label.Text = text;
			label.Dock = DockStyle.Fill;
			label.TextAlign = ContentAlignment.BottomLeft;
			label.Margin = new Padding(0, 2, 0, 0);
			label.Font = new Font(Font, FontStyle.Bold);
			table.Controls.Add(label, 0, table.RowCount - 1);
			table.SetColumnSpan(label, 4);
		}

		private void AddRow(TableLayoutPanel table, float height)
		{
			table.RowCount = table.RowCount + 1;
			table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
		}

		private Label Caption(string label, string tip)
		{
			Label caption = new Label();
			caption.Text = label;
			caption.AutoSize = false;
			caption.Dock = DockStyle.Fill;
			caption.TextAlign = ContentAlignment.MiddleLeft;
			caption.Margin = new Padding(0, 0, 6, 0);
			caption.AutoEllipsis = true;
			mTip.SetToolTip(caption, tip);
			return caption;
		}

		private NumericUpDown PlaceNum(TableLayoutPanel table, int col, string label, string tip, decimal min, decimal max, int decimals, decimal step)
		{
			int row;
			if (col == 0)
			{
				AddRow(table, 28);
				row = table.RowCount - 1;
			}
			else
				row = table.RowCount - 1;
			table.Controls.Add(Caption(label, tip), col, row);
			NumericUpDown num = new NumericUpDown();
			num.Minimum = min;
			num.Maximum = max;
			num.DecimalPlaces = decimals;
			num.Increment = step;
			num.Width = 108;
			num.Anchor = AnchorStyles.Left;
			num.Margin = new Padding(0, 2, 8, 2);
			num.TextAlign = HorizontalAlignment.Right;
			num.ThousandsSeparator = false;
			mTip.SetToolTip(num, tip);
			table.Controls.Add(num, col + 1, row);
			return num;
		}

		private ComboBox PlaceCombo(TableLayoutPanel table, string label, string tip)
		{
			AddRow(table, 28);
			int row = table.RowCount - 1;
			table.Controls.Add(Caption(label, tip), 0, row);
			ComboBox box = NewCombo(tip);
			table.Controls.Add(box, 1, row);
			table.SetColumnSpan(box, 3);
			return box;
		}

		private ComboBox NewCombo(string tip)
		{
			ComboBox box = new ComboBox();
			box.DropDownStyle = ComboBoxStyle.DropDownList;
			box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			box.Margin = new Padding(0, 2, 8, 2);
			box.DropDownWidth = 280;
			mTip.SetToolTip(box, tip);
			return box;
		}

		private TextBox PlaceText(TableLayoutPanel table, int col, string label, string tip)
		{
			AddRow(table, 28);
			int row = table.RowCount - 1;
			table.Controls.Add(Caption(label, tip), col, row);
			TextBox box = new TextBox();
			box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			box.Margin = new Padding(0, 3, 8, 3);
			mTip.SetToolTip(box, tip);
			table.Controls.Add(box, col + 1, row);
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

		private sealed class RouteItem
		{
			public PlotterRouteMode Mode;
			private readonly string mText;
			public RouteItem(PlotterRouteMode mode, string text)
			{
				Mode = mode;
				mText = text;
			}
			public override string ToString() { return mText; }
		}
	}
}
