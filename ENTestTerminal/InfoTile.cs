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
    private string _name;
    private string _lastValue = "--";
    private bool _clickable;
    private Color _baseColor;

    public event EventHandler? TileClicked;

    public string Value => _lastValue;

    public InfoTile(string name, string unit, Color tint, int width = 200)
    {
        Size = new Size(width, 62);
        Margin = new Padding(5);
        BackColor = tint;
        BorderStyle = BorderStyle.FixedSingle;
        _unit = unit;
        _name = name;
        _baseColor = tint;

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
        _name = name;
        _nameLabel.Text = _clickable && _clickEnabled ? $"{name}  ✎" : name;
    }

    public void MakeClickable()
    {
        if (_clickable) return;
        _clickable = true;

        foreach (Control c in new Control[] { this, _nameLabel, _valueLabel })
        {
            c.Click += (_, e) =>
            {
                if (_clickEnabled) TileClicked?.Invoke(this, e);
            };
            c.MouseEnter += (_, _) => SetHover(true);
            c.MouseLeave += (_, _) => SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }
        SetClickEnabled(true);
    }

    private bool _clickEnabled = true;

    public void SetClickEnabled(bool enabled)
    {
        if (!_clickable) return;
        _clickEnabled = enabled;
        foreach (Control c in new Control[] { this, _nameLabel, _valueLabel })
        {
            c.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        }
        SetName(_name);
        SetHover(false);
    }

    private void SetHover(bool hover)
    {
        BackColor = hover && _clickEnabled
            ? Color.FromArgb(Math.Max(_baseColor.R - 22, 0), Math.Max(_baseColor.G - 22, 0), Math.Max(_baseColor.B - 22, 0))
            : _baseColor;
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
