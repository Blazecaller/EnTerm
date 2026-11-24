namespace ENTestTerminal;

/// <summary>
/// Modal dialog that ends only through Send (with a valid command) or Cancel.
/// There is no title-bar close box; Esc maps to Cancel.
/// </summary>
public abstract class CommandDialog : Form
{
    private readonly TableLayoutPanel _layout;
    private readonly Label _lblCommand;
    private readonly Label _lblError;
    private readonly FlowLayoutPanel _buttons;

    public string? Command { get; private set; }

    protected CommandDialog(string title)
    {
        Text = title;
        Font = AppFonts.SettingsDialogText;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14);

        _layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
        };

        _lblCommand = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 10, 0, 0) };
        _lblError = new Label { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(420, 0), Margin = new Padding(0, 4, 0, 0) };

        var btnCancel = new Button { Text = Loc.T("dlg.cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var btnSend = new Button { Text = Loc.T("dlg.send"), AutoSize = true };
        btnSend.Click += (_, _) => TryAccept();
        _buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0),
        };
        _buttons.Controls.Add(btnCancel);
        _buttons.Controls.Add(btnSend);

        AcceptButton = btnSend;
        CancelButton = btnCancel;
        Controls.Add(_layout);
    }

    protected void AddContent(Control control) => _layout.Controls.Add(control);

    /// <summary>Call at the end of the subclass constructor, after all content is added.</summary>
    protected void FinishLayout()
    {
        _layout.Controls.Add(_lblCommand);
        _layout.Controls.Add(_lblError);
        _layout.Controls.Add(_buttons);
        UpdatePreview();
    }

    /// <summary>Returns the command to send, or null with an error message.</summary>
    protected abstract string? BuildCommand(out string error);

    protected void UpdatePreview()
    {
        string? cmd = BuildCommand(out _);
        _lblCommand.Text = $"{Loc.T("dlg.command")}: {cmd ?? "--"}";
        _lblError.Text = string.Empty;
    }

    private void TryAccept()
    {
        string? cmd = BuildCommand(out string error);
        if (cmd is null)
        {
            _lblError.Text = error;
            return;
        }
        Command = cmd;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected static GroupBox Group(string title, Control content)
    {
        var box = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 6),
            Margin = new Padding(0, 0, 0, 8),
        };
        content.Dock = DockStyle.Fill;
        box.Controls.Add(content);
        return box;
    }

    protected static FlowLayoutPanel Column() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
    };
}

/// <summary>Turn one sensor ON or OFF: AT+C=NN,ON|OFF.</summary>
public sealed class SensorConfigDialog : CommandDialog
{
    private readonly List<(int Id, RadioButton Radio)> _sensors = new();
    private readonly RadioButton _rbOn;
    private readonly RadioButton _rbOff;

    public SensorConfigDialog(IEnumerable<(int Id, string NameKey)> sensors) : base(Loc.T("cfg.sensors"))
    {
        var sensorList = Column();
        foreach (var (id, nameKey) in sensors)
        {
            var rb = new RadioButton { Text = $"{id} - {Loc.T(nameKey)}", AutoSize = true, Margin = new Padding(2, 3, 2, 3) };
            rb.CheckedChanged += (_, _) => UpdatePreview();
            _sensors.Add((id, rb));
            sensorList.Controls.Add(rb);
        }
        AddContent(Group(Loc.T("cfg.sensor"), sensorList));

        _rbOn = new RadioButton { Text = Loc.T("basic.on"), AutoSize = true, Margin = new Padding(2, 3, 24, 3) };
        _rbOff = new RadioButton { Text = Loc.T("basic.off"), AutoSize = true, Margin = new Padding(2, 3, 2, 3) };
        _rbOn.CheckedChanged += (_, _) => UpdatePreview();
        _rbOff.CheckedChanged += (_, _) => UpdatePreview();
        var stateRow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        stateRow.Controls.Add(_rbOn);
        stateRow.Controls.Add(_rbOff);
        AddContent(Group(Loc.T("cfg.state"), stateRow));

        FinishLayout();
    }

    protected override string? BuildCommand(out string error)
    {
        var sensor = _sensors.FirstOrDefault(s => s.Radio.Checked);
        if (sensor.Radio is null)
        {
            error = Loc.T("cfg.pickSensor");
            return null;
        }
        if (!_rbOn.Checked && !_rbOff.Checked)
        {
            error = Loc.T("cfg.pickState");
            return null;
        }
        error = string.Empty;
        return $"AT+C={sensor.Id:D2},{(_rbOn.Checked ? "ON" : "OFF")}";
    }
}

/// <summary>Set the serial number (AT+SN=) or save the device baud rate to EEPROM (AT+SB=).</summary>
public sealed class DeviceSettingsDialog : CommandDialog
{
    private static readonly string[] BaudRates = { "9600", "19200", "38400", "57600", "115200" };

    private readonly RadioButton _rbSerial;
    private readonly RadioButton _rbBaud;
    private readonly TextBox _txtSerial;
    private readonly ComboBox _cmbBaud;

    public bool IsSerialChange => _rbSerial.Checked;

    public DeviceSettingsDialog() : base(Loc.T("cfg.device"))
    {
        var grid = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _rbSerial = new RadioButton { Text = Loc.T("basic.serialLabel"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 4, 12, 4) };
        _txtSerial = new TextBox { Width = 160, Anchor = AnchorStyles.Left };
        _rbBaud = new RadioButton { Text = Loc.T("cfg.deviceBaud"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 4, 12, 4) };
        _cmbBaud = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Anchor = AnchorStyles.Left };
        _cmbBaud.Items.AddRange(BaudRates);
        _cmbBaud.SelectedIndex = 1;

        grid.Controls.Add(_rbSerial, 0, 0);
        grid.Controls.Add(_txtSerial, 1, 0);
        grid.Controls.Add(_rbBaud, 0, 1);
        grid.Controls.Add(_cmbBaud, 1, 1);
        AddContent(Group(Loc.T("cfg.pickOne"), grid));

        // Editing a field selects the option it belongs to.
        _txtSerial.TextChanged += (_, _) => { _rbSerial.Checked = true; UpdatePreview(); };
        _txtSerial.Enter += (_, _) => _rbSerial.Checked = true;
        _cmbBaud.SelectedIndexChanged += (_, _) => { _rbBaud.Checked = true; UpdatePreview(); };
        _cmbBaud.Enter += (_, _) => _rbBaud.Checked = true;
        _rbSerial.CheckedChanged += (_, _) => UpdatePreview();
        _rbBaud.CheckedChanged += (_, _) => UpdatePreview();

        FinishLayout();
    }

    protected override string? BuildCommand(out string error)
    {
        error = string.Empty;
        if (_rbSerial.Checked)
        {
            string sn = _txtSerial.Text.Trim();
            if (sn.Length > 0 && sn.All(char.IsDigit)) return $"AT+SN={sn}";
            error = Loc.T("msg.invalidSerial");
            return null;
        }
        if (_rbBaud.Checked) return $"AT+SB={_cmbBaud.SelectedIndex}";
        error = Loc.T("cfg.pickSetting");
        return null;
    }
}
