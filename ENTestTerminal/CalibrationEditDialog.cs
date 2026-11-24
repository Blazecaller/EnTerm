namespace ENTestTerminal;

public sealed class CalibrationEditDialog : Form
{
    private readonly CalibrationSetter _setter;
    private readonly TextBox? _txtValue;
    private readonly Label _lblError;

    public string? Value { get; private set; }

    public CalibrationEditDialog(string title, CalibrationSetter setter, string currentValue, string? liveHeaterPwm = null)
    {
        _setter = setter;

        Text = title;
        Font = AppFonts.FormDefault;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
        };

        Label Line(string text, Color? color = null) => new()
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            ForeColor = color ?? SystemColors.ControlText,
            Margin = new Padding(0, 0, 0, 6),
        };

        layout.Controls.Add(Line($"{Loc.T("dlg.command")}: {setter.Command}", Color.DimGray));

        if (setter.Input == CalibInput.None)
        {
            layout.Controls.Add(Line(string.Format(Loc.T("dlg.nowPrompt"), liveHeaterPwm ?? "--")));
            layout.Controls.Add(Line(Loc.T("dlg.nowNote"), Color.DimGray));
        }
        else
        {
            layout.Controls.Add(Line($"{Loc.T("dlg.current")}: {currentValue}"));
            string kind = Loc.T(setter.Input == CalibInput.Integer ? "dlg.integer" : "dlg.decimal");
            layout.Controls.Add(Line($"{Loc.T("dlg.range")}: {setter.RangeText} ({kind})"));
            if (setter.Temporary) layout.Controls.Add(Line(Loc.T("dlg.temporary"), Color.DarkOrange));

            layout.Controls.Add(Line(Loc.T("dlg.newValue")));
            _txtValue = new TextBox { Width = 200, Margin = new Padding(0, 0, 0, 4) };
            if (currentValue is not ("--" or "-")) _txtValue.Text = currentValue;
            layout.Controls.Add(_txtValue);
        }

        _lblError = Line(string.Empty, Color.Firebrick);
        layout.Controls.Add(_lblError);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 0, 0),
        };
        var btnCancel = new Button { Text = Loc.T("dlg.cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var btnSend = new Button { Text = Loc.T("dlg.send"), AutoSize = true };
        btnSend.Click += (_, _) => TryAccept();
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnSend);
        layout.Controls.Add(buttons);

        AcceptButton = btnSend;
        CancelButton = btnCancel;
        Controls.Add(layout);

        Shown += (_, _) =>
        {
            if (_txtValue is null) return;
            _txtValue.Focus();
            _txtValue.SelectAll();
        };
    }

    private void TryAccept()
    {
        if (_txtValue is not null)
        {
            if (!_setter.TryNormalize(_txtValue.Text, out string normalized))
            {
                _lblError.Text = string.Format(Loc.T("dlg.invalid"), _setter.RangeText);
                _txtValue.Focus();
                _txtValue.SelectAll();
                return;
            }
            Value = normalized;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
