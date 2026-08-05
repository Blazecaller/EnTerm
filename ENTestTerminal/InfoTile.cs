namespace ENTestTerminal;

public sealed class InfoTile : Panel
{
    private const int IndicatorDiameter = 12;
    private const int IndicatorLeftMargin = 8;
    private const int NameLabelVerticalPadding = 6;

    private readonly Label _nameLabel;
    private readonly Label _valueLabel;
    private StatusDot? _dot;
    private string _unit;
    private string _lastValue = "--";

    public InfoTile(string name, string unit, Color tint, int width = 200)
    {
        Size = new Size(width, 62);
        Margin = new Padding(5);
        BackColor = tint;
        BorderStyle = BorderStyle.FixedSingle;
        _unit = unit;

        _nameLabel = new Label
        {
            Text = name,
            Dock = DockStyle.Top,
            Height = AppFonts.DashboardTileCaption.Height + NameLabelVerticalPadding,
            Font = AppFonts.DashboardTileCaption,
            ForeColor = Color.FromArgb(70, 70, 70),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 3, 4, 3),
            AutoEllipsis = true,
        };

        _valueLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = AppFonts.DashboardTileValue,
            ForeColor = Color.FromArgb(30, 30, 30),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 4, 6),
        };

        Controls.Add(_valueLabel);
        Controls.Add(_nameLabel);

        Render();
    }

    private void Render()
    {
        _valueLabel.Text = (string.IsNullOrEmpty(_unit) || _lastValue == "-") ? _lastValue : $"{_lastValue} {_unit}";
    }

    public void SetValue(string formatted)
    {
        _lastValue = formatted;
        Render();
    }

    public void SetName(string name)
    {
        _nameLabel.Text = name;
    }

    public void SetUnit(string unit)
    {
        _unit = unit;
        Render();
    }

    public void EnableIndicator()
    {
        int textLeft = IndicatorLeftMargin + IndicatorDiameter + 6;
        _nameLabel.Padding = new Padding(textLeft, 3, 4, 3);
        _valueLabel.Padding = new Padding(textLeft, 0, 4, 6);

        _dot = new StatusDot(BackColor)
        {
            Location = new Point(IndicatorLeftMargin, (Size.Height - IndicatorDiameter) / 2),
        };
        Controls.Add(_dot);
        _dot.BringToFront();
    }

    public void SetIndicatorColor(Color color)
    {
        if (_dot is null) return;
        _dot.DotColor = color;
        _dot.Invalidate();
    }

    private sealed class StatusDot : Panel
    {
        public Color DotColor { get; set; } = Color.Gray;

        public StatusDot(Color background)
        {
            Size = new Size(IndicatorDiameter, IndicatorDiameter);
            BackColor = background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var brush = new SolidBrush(DotColor);
            e.Graphics.FillEllipse(brush, rect);
            using var pen = new Pen(Color.FromArgb(90, 0, 0, 0));
            e.Graphics.DrawEllipse(pen, rect);
        }
    }
}
