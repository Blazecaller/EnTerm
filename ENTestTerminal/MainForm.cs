using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace ENTestTerminal;

public sealed class MainForm : Form
{
    private static readonly (int Id, string NameKey)[] SensorIds =
    {
        (5, "sensor.digitalTemp"),
        (6, "sensor.solar"),
        (7, "sensor.soil"),
        (8, "sensor.rain"),
        (9, "sensor.bgtWind"),
    };

    private static readonly string[] SensorActionCommands = { "ON", "OFF" };

    private static readonly Parity[] ParityValues = { Parity.None, Parity.Odd, Parity.Even, Parity.Mark, Parity.Space };
    private static readonly string[] ParityKeys = { "parity.none", "parity.odd", "parity.even", "parity.mark", "parity.space" };
    private static readonly StopBits[] StopBitsValues = { StopBits.One, StopBits.OnePointFive, StopBits.Two };
    private static readonly string[] StopBitsLabels = { "1", "1.5", "2" };

    private static readonly Color WindowGray = Color.FromArgb(232, 233, 236);
    private static readonly Color PanelGray = Color.FromArgb(244, 244, 246);

    private const int BaseWidth = 1180;
    private const int BaseHeight = 820;
    private const int AdvancedWidth = 480;

    private SerialPort? _port;
    private readonly AtLinkParser _parser = new();
    private int _frameCount;
    private LinkMode _lastMode = LinkMode.Unknown;
    private bool _lastTestActive;
    private string? _lastPortName;
    private int _lastBaud;
    private bool _advancedVisible;

    private readonly Dictionary<string, InfoTile> _tiles = new();
    private readonly List<Action> _retext = new();

    private readonly ComboBox _cmbPort = new();
    private readonly ComboBox _cmbBaud = new();
    private readonly ComboBox _cmbParity = new();
    private readonly ComboBox _cmbDataBits = new();
    private readonly ComboBox _cmbStopBits = new();
    private readonly Button _btnConnect = new();
    private readonly Label _lblConnDot = new();
    private readonly Label _lblSerialNo = new();
    private string? _lastSerialNo;

    private readonly ComboBox _cmbSensorId = new();
    private readonly ComboBox _cmbSensorAction = new();
    private readonly ComboBox _cmbBaudSet = new();
    private readonly TextBox _txtSerialSet = new();

    private readonly Button _btnTestToggle = new();

    private Panel _basicPage = null!;
    private Panel _calibrationPage = null!;

    private readonly SplitContainer _mainSplit = new();
    private bool _splitterInitialized;
    private readonly Button _btnAdvancedToggle = new();

    private readonly RichTextBox _terminal = new();
    private readonly TextBox _txtSend = new();
    private readonly CheckBox _chkAutoScroll = new();

    private readonly System.Windows.Forms.Timer _captureTimer = new() { Interval = 500 };
    private StringBuilder? _captureBuffer;
    private TaskCompletionSource<string?>? _captureCompletion;
    private bool _queryInProgress;
    private readonly Random _jitter = new();

    private readonly ToolStripStatusLabel _statusConn = new();
    private readonly ToolStripStatusLabel _statusMode = new();
    private readonly ToolStripStatusLabel _statusTest = new();
    private readonly ToolStripStatusLabel _statusFrames = new();

    private readonly MenuStrip _menuStrip = new();
    private readonly ToolStripButton _btnLanguage = new();

    public MainForm()
    {
        Text = "ENterm";
        MinimumSize = new Size(1080, 640);
        Size = new Size(BaseWidth, BaseHeight);
        BackColor = WindowGray;
        Font = new Font("Segoe UI", 9f);
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch (Exception)
        {
        }

        _parser.TestFrameReceived += OnTestFrame;
        _parser.ModeChanged += OnModeChanged;
        _parser.TestModeChanged += OnTestModeChanged;
        _captureTimer.Tick += OnCaptureTimerTick;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = WindowGray,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildLanguageBar(), 0, 0);
        root.Controls.Add(BuildConnectionPanel(), 0, 1);
        root.Controls.Add(BuildBasicCommandsBar(), 0, 2);
        root.Controls.Add(BuildMainContent(), 0, 3);

        var status = new StatusStrip();
        status.Items.Add(_statusConn);
        status.Items.Add(new ToolStripSeparator());
        status.Items.Add(_statusMode);
        status.Items.Add(new ToolStripSeparator());
        status.Items.Add(_statusTest);
        status.Items.Add(new ToolStripSeparator());
        status.Items.Add(_statusFrames);
        Bind(() => _statusConn.Text = (_port is { IsOpen: true })
            ? $"{Loc.T("status.connected")}: {_lastPortName} @ {_lastBaud}"
            : Loc.T("status.disconnected"));
        Bind(() => _statusMode.Text = $"{Loc.T("status.mode")}: {ModeText(_lastMode)}");
        Bind(() => _statusTest.Text = $"{Loc.T("status.test")}: {(_lastTestActive ? Loc.T("status.on") : Loc.T("status.off"))}");
        Bind(() => _statusFrames.Text = $"{Loc.T("status.frames")}: {_frameCount}");

        _btnLanguage.DisplayStyle = ToolStripItemDisplayStyle.Text;
        Bind(() => _btnLanguage.Text = Loc.Current == AppLanguage.Turkish ? "EN" : "TR");
        _btnLanguage.Click += (_, _) =>
        {
            Loc.Toggle();
            RefreshTexts();
        };
        _menuStrip.Items.Add(_btnLanguage);
        MainMenuStrip = _menuStrip;

        Controls.Add(root);
        Controls.Add(status);
        Controls.Add(_menuStrip);

        RefreshPorts();
        RefreshTexts();
        FormClosing += (_, _) =>
        {
            if (_port is { IsOpen: true })
            {
                SendRaw("+++");
            }
            ClosePort();
        };
    }

    private void Bind(Action apply)
    {
        apply();
        _retext.Add(apply);
    }

    private void RefreshTexts()
    {
        foreach (var apply in _retext) apply();
    }

    private static string ModeText(LinkMode m) => m switch
    {
        LinkMode.Data => Loc.T("status.modeData"),
        LinkMode.Command => Loc.T("status.modeCommand"),
        _ => Loc.T("status.modeUnknown"),
    };

    private Control BuildLanguageBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = WindowGray };

        _lblSerialNo.Dock = DockStyle.Fill;
        _lblSerialNo.TextAlign = ContentAlignment.MiddleCenter;
        _lblSerialNo.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        Bind(() => _lblSerialNo.Text = _lastSerialNo is { } sn ? $"{Loc.T("popup.serialNo")}: {sn}" : string.Empty);
        panel.Controls.Add(_lblSerialNo);

        return panel;
    }

    private GroupBox BuildConnectionPanel()
    {
        var group = new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(6) };
        Bind(() => group.Text = Loc.T("conn.title"));
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };

        var lblPort = LabelFor(); Bind(() => lblPort.Text = Loc.T("conn.port"));
        flow.Controls.Add(lblPort);
        _cmbPort.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPort.Width = 90;
        flow.Controls.Add(_cmbPort);

        var btnRefresh = new Button { AutoSize = true };
        Bind(() => btnRefresh.Text = Loc.T("conn.refresh"));
        btnRefresh.Click += (_, _) => RefreshPorts();
        flow.Controls.Add(btnRefresh);

        flow.Controls.Add(Spacer());
        var lblBaud = LabelFor(); Bind(() => lblBaud.Text = Loc.T("conn.baud"));
        flow.Controls.Add(lblBaud);
        _cmbBaud.DropDownStyle = ComboBoxStyle.DropDown;
        _cmbBaud.Width = 80;
        _cmbBaud.Items.AddRange(new object[] { 9600, 19200, 38400, 57600, 115200 });
        _cmbBaud.SelectedItem = 9600;
        flow.Controls.Add(_cmbBaud);

        flow.Controls.Add(Spacer());
        var lblParity = LabelFor(); Bind(() => lblParity.Text = Loc.T("conn.parity"));
        flow.Controls.Add(lblParity);
        _cmbParity.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbParity.Width = 90;
        Bind(() =>
        {
            int sel = Math.Max(_cmbParity.SelectedIndex, 0);
            _cmbParity.Items.Clear();
            foreach (var k in ParityKeys) _cmbParity.Items.Add(Loc.T(k));
            _cmbParity.SelectedIndex = sel;
        });
        flow.Controls.Add(_cmbParity);

        flow.Controls.Add(Spacer());
        var lblDataBits = LabelFor(); Bind(() => lblDataBits.Text = Loc.T("conn.databits"));
        flow.Controls.Add(lblDataBits);
        _cmbDataBits.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDataBits.Width = 50;
        _cmbDataBits.Items.AddRange(new object[] { 7, 8 });
        _cmbDataBits.SelectedItem = 8;
        flow.Controls.Add(_cmbDataBits);

        flow.Controls.Add(Spacer());
        var lblStopBits = LabelFor(); Bind(() => lblStopBits.Text = Loc.T("conn.stopbits"));
        flow.Controls.Add(lblStopBits);
        _cmbStopBits.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbStopBits.Width = 55;
        _cmbStopBits.Items.AddRange(StopBitsLabels);
        _cmbStopBits.SelectedIndex = 0;
        flow.Controls.Add(_cmbStopBits);

        flow.Controls.Add(Spacer());
        _btnConnect.AutoSize = false;
        _btnConnect.Size = new Size(150, 44);
        _btnConnect.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _btnConnect.FlatStyle = FlatStyle.Flat;
        _btnConnect.FlatAppearance.BorderSize = 3;
        _btnConnect.FlatAppearance.BorderColor = Color.FromArgb(0, 153, 255);
        _btnConnect.FlatAppearance.MouseOverBackColor = Color.FromArgb(214, 236, 255);
        _btnConnect.FlatAppearance.MouseDownBackColor = Color.FromArgb(179, 219, 255);
        _btnConnect.BackColor = Color.FromArgb(235, 246, 255);
        Bind(() => _btnConnect.Text = (_port is { IsOpen: true }) ? Loc.T("conn.disconnect") : Loc.T("conn.connect"));
        _btnConnect.Click += (_, _) => ToggleConnection();
        flow.Controls.Add(_btnConnect);

        _lblConnDot.Text = string.Empty;
        _lblConnDot.ForeColor = Color.Firebrick;
        _lblConnDot.AutoSize = false;
        _lblConnDot.Size = new Size(_btnConnect.Height, _btnConnect.Height);
        _lblConnDot.Margin = new Padding(6, 0, 0, 0);
        _lblConnDot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int d = Math.Min(_lblConnDot.Width, _lblConnDot.Height) - 10;
            var rect = new Rectangle((_lblConnDot.Width - d) / 2, (_lblConnDot.Height - d) / 2, d, d);
            using var brush = new SolidBrush(_lblConnDot.ForeColor);
            e.Graphics.FillEllipse(brush, rect);
        };
        _lblConnDot.ForeColorChanged += (_, _) => _lblConnDot.Invalidate();
        flow.Controls.Add(_lblConnDot);

        group.Controls.Add(flow);
        return group;
    }

    private void RefreshPorts()
    {
        var current = _cmbPort.SelectedItem as string;
        _cmbPort.Items.Clear();
        _cmbPort.Items.AddRange(SerialPort.GetPortNames().OrderBy(p => p).Cast<object>().ToArray());
        if (current is not null && _cmbPort.Items.Contains(current))
        {
            _cmbPort.SelectedItem = current;
        }
        else if (_cmbPort.Items.Count > 0)
        {
            _cmbPort.SelectedIndex = 0;
        }
    }

    private async void ToggleConnection()
    {
        if (_port is { IsOpen: true })
        {
            await DisconnectAsync();
            return;
        }

        if (_cmbPort.SelectedItem is not string portName)
        {
            MessageBox.Show(this, Loc.T("msg.noPort"), Loc.T("msg.noPortTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_cmbBaud.Text, out int baud) || baud <= 0)
        {
            MessageBox.Show(this, Loc.T("msg.invalidBaud"), Loc.T("msg.invalidBaudTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var parity = ParityValues[_cmbParity.SelectedIndex];
        var dataBits = (int)_cmbDataBits.SelectedItem!;
        var stopBits = StopBitsValues[_cmbStopBits.SelectedIndex];

        try
        {
            _port = new SerialPort(portName, baud, parity, dataBits, stopBits)
            {
                ReadTimeout = 500,
                WriteTimeout = 500,
            };
            _port.DataReceived += OnPortDataReceived;
            _port.Open();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{portName}:\n{ex.Message}", Loc.T("msg.connFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            _port = null;
            return;
        }

        _lastPortName = portName;
        _lastBaud = baud;
        _lblConnDot.ForeColor = Color.ForestGreen;
        SetConnectionControlsEnabled(false);
        RefreshTexts();
        AppendTerminal($"--- connected to {portName} @ {baud} {parity} {dataBits}{StopBitsShort(stopBits)} ---\r\n", Color.Gray);

        await AutoEnterCommandModeAsync();
    }

    private static string StopBitsShort(StopBits s) => s switch
    {
        StopBits.One => "1",
        StopBits.OnePointFive => "1.5",
        StopBits.Two => "2",
        _ => "?",
    };

    private async Task AutoEnterCommandModeAsync()
    {
        _queryInProgress = true;
        try
        {
            var modeReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnMode(LinkMode m)
            {
                if (m == LinkMode.Command) modeReached.TrySetResult(true);
            }

            _parser.ModeChanged += OnMode;
            try
            {
                for (int attempt = 0; attempt < 3 && !modeReached.Task.IsCompleted; attempt++)
                {
                    SendRaw("ATO");
                    await Task.Delay(250);
                    SendRaw("ATO");
                    await Task.WhenAny(modeReached.Task, Task.Delay(600));
                }

                if (!modeReached.Task.IsCompleted)
                {
                    AppendTerminal("--- " + Loc.T("msg.handshakeFailed") + " ---\r\n", Color.Orange);
                    return;
                }
            }
            finally
            {
                _parser.ModeChanged -= OnMode;
            }

            await QuerySerialNumberSilentlyAsync();

            var frameSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFrame(TestFrame _) => frameSeen.TrySetResult(true);
            _parser.TestFrameReceived += OnFrame;
            try
            {
                await Task.WhenAny(frameSeen.Task, Task.Delay(1200));
                if (!frameSeen.Task.IsCompleted)
                {
                    SendRaw("AT+T");
                }
            }
            finally
            {
                _parser.TestFrameReceived -= OnFrame;
            }
        }
        finally
        {
            _queryInProgress = false;
        }
    }

    private async Task QuerySerialNumberSilentlyAsync()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            string? captured = await CaptureResponseAsync("AT+S?", idleMs: 500);
            if (captured is null) continue;
            var match = Regex.Match(StripTestFrames(captured), @"Serial No:\s*(\d+)");
            if (match.Success)
            {
                _lastSerialNo = match.Groups[1].Value;
                RefreshTexts();
                return;
            }
        }
    }

    private async Task DisconnectAsync()
    {
        SendRaw("+++");
        await Task.Delay(200);
        ClosePort();
    }

    private void ClosePort()
    {
        if (_port is null) return;
        try
        {
            _port.DataReceived -= OnPortDataReceived;
            if (_port.IsOpen) _port.Close();
        }
        catch { /* best-effort close */ }
        finally
        {
            _port.Dispose();
            _port = null;
        }

        _lastMode = LinkMode.Unknown;
        _lastTestActive = false;
        _lastSerialNo = null;
        _lblConnDot.ForeColor = Color.Firebrick;
        SetConnectionControlsEnabled(true);
        RefreshTexts();
        AppendTerminal("--- disconnected ---\r\n", Color.Gray);
    }

    private void SetConnectionControlsEnabled(bool enabled)
    {
        _cmbPort.Enabled = enabled;
        _cmbBaud.Enabled = enabled;
        _cmbParity.Enabled = enabled;
        _cmbDataBits.Enabled = enabled;
        _cmbStopBits.Enabled = enabled;
    }

    private Control BuildBasicCommandsBar()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var group = new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(6, 4, 6, 4) };
        Bind(() => group.Text = Loc.T("basic.title"));

        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var row2 = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var tightMargin = new Padding(2, 2, 2, 2);

        var lblSensor = LabelFor(); Bind(() => lblSensor.Text = Loc.T("basic.sensor"));
        lblSensor.Margin = tightMargin;
        row1.Controls.Add(lblSensor);
        _cmbSensorId.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbSensorId.Width = 175;
        _cmbSensorId.Margin = tightMargin;
        Bind(() =>
        {
            int sel = Math.Max(_cmbSensorId.SelectedIndex, 0);
            _cmbSensorId.Items.Clear();
            foreach (var (id, nameKey) in SensorIds) _cmbSensorId.Items.Add($"{id} - {Loc.T(nameKey)}");
            _cmbSensorId.SelectedIndex = sel;
        });
        row1.Controls.Add(_cmbSensorId);

        _cmbSensorAction.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbSensorAction.Width = 80;
        _cmbSensorAction.Margin = tightMargin;
        Bind(() =>
        {
            int sel = Math.Max(_cmbSensorAction.SelectedIndex, 0);
            _cmbSensorAction.Items.Clear();
            _cmbSensorAction.Items.Add(Loc.T("basic.on"));
            _cmbSensorAction.Items.Add(Loc.T("basic.off"));
            _cmbSensorAction.SelectedIndex = sel;
        });
        row1.Controls.Add(_cmbSensorAction);

        var btnApply = new Button { AutoSize = true, Margin = tightMargin };
        Bind(() => btnApply.Text = Loc.T("basic.apply"));
        btnApply.Click += (_, _) =>
        {
            int id = SensorIds[_cmbSensorId.SelectedIndex].Id;
            string action = SensorActionCommands[_cmbSensorAction.SelectedIndex];
            RunQuery($"AT+C={id:D2},{action}", "popup.sensorControl", PassthroughResponse);
        };
        row1.Controls.Add(btnApply);

        row1.Controls.Add(Spacer());
        var lblSerial = LabelFor(); Bind(() => lblSerial.Text = Loc.T("basic.serialLabel"));
        lblSerial.Margin = tightMargin;
        row1.Controls.Add(lblSerial);
        _txtSerialSet.Width = 80;
        _txtSerialSet.Margin = tightMargin;
        row1.Controls.Add(_txtSerialSet);
        var btnSetSerial = new Button { AutoSize = true, Margin = tightMargin };
        Bind(() => btnSetSerial.Text = Loc.T("basic.setSerial"));
        btnSetSerial.Click += (_, _) =>
        {
            if (_txtSerialSet.Text.Length > 0 && _txtSerialSet.Text.All(char.IsDigit))
            {
                RunQuery($"AT+SN={_txtSerialSet.Text}", "basic.setSerial", PassthroughResponse);
            }
            else
            {
                MessageBox.Show(this, Loc.T("msg.invalidSerial"), Loc.T("msg.invalidSerialTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        row1.Controls.Add(btnSetSerial);

        row1.Controls.Add(Spacer());
        _cmbBaudSet.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbBaudSet.Width = 100;
        _cmbBaudSet.Margin = tightMargin;
        _cmbBaudSet.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
        _cmbBaudSet.SelectedIndex = 0;
        row1.Controls.Add(_cmbBaudSet);
        var btnSetBaud = new Button { AutoSize = true, Margin = tightMargin };
        Bind(() => btnSetBaud.Text = Loc.T("basic.setBaud"));
        btnSetBaud.Click += (_, _) => RunQuery($"AT+SB={_cmbBaudSet.SelectedIndex}", "basic.setBaud", PassthroughResponse);
        row1.Controls.Add(btnSetBaud);

        var btnSensorList = new Button { AutoSize = true, Margin = tightMargin };
        Bind(() => btnSensorList.Text = Loc.T("basic.showSensorList"));
        btnSensorList.Click += (_, _) => RunQuery("AT+C?", "popup.sensorList", ParseSensorListResponse);
        row2.Controls.Add(btnSensorList);

        var btnStatus = new Button { AutoSize = true, Margin = tightMargin };
        Bind(() => btnStatus.Text = Loc.T("basic.systemStatus"));
        btnStatus.Click += (_, _) => RunQuery("AT+S?", "popup.status", ParseStatusResponse);
        row2.Controls.Add(btnStatus);

        var btnHelp = new Button { AutoSize = true, Margin = tightMargin };
        Bind(() => btnHelp.Text = Loc.T("basic.help"));
        btnHelp.Click += (_, _) => RunQuery("AT?", "popup.help", ParseHelpResponse);
        row2.Controls.Add(btnHelp);

        stack.Controls.Add(row1, 0, 0);
        stack.Controls.Add(row2, 0, 1);
        group.Controls.Add(stack);

        _btnAdvancedToggle.AutoSize = true;
        _btnAdvancedToggle.Dock = DockStyle.Right;
        Bind(() => _btnAdvancedToggle.Text = _advancedVisible ? Loc.T("basic.advancedHide") : Loc.T("basic.advancedShow"));
        _btnAdvancedToggle.Click += (_, _) => SetAdvancedVisible(!_advancedVisible);

        table.Controls.Add(group, 0, 0);
        table.Controls.Add(_btnAdvancedToggle, 1, 0);
        return table;
    }

    private async Task RunCommand(string command, string? titleKey, Func<string, string?>? parse)
    {
        if (_queryInProgress) return;
        if (_port is not { IsOpen: true })
        {
            if (titleKey is not null)
            {
                MessageBox.Show(this, Loc.T("msg.commError"), Loc.T(titleKey), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        _queryInProgress = true;
        try
        {
            bool needsResume = _lastTestActive;
            if (needsResume)
            {
                await SetTestModeAsync(active: false);
            }

            if (titleKey is not null && parse is not null)
            {
                string? result = null;
                for (int attempt = 0; attempt < 4 && result is null; attempt++)
                {
                    string? captured = await CaptureResponseAsync(command, idleMs: 500);
                    if (captured is not null) result = parse(StripTestFrames(captured));
                }

                MessageBox.Show(
                    this,
                    result ?? Loc.T("msg.commError"),
                    Loc.T(titleKey),
                    MessageBoxButtons.OK,
                    result is not null ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            else
            {
                await CaptureResponseAsync(command, idleMs: 500);
            }

            if (needsResume)
            {
                await SetTestModeAsync(active: true);
            }
        }
        finally
        {
            _queryInProgress = false;
        }
    }

    private async void RunQuery(string command, string titleKey, Func<string, string?> parse) => await RunCommand(command, titleKey, parse);

    private Task RunCommandSilent(string command) => RunCommand(command, null, null);

    private async Task EnterCommandModeManualAsync()
    {
        if (_queryInProgress) return;
        _queryInProgress = true;
        try
        {
            bool wasActive = _lastTestActive;
            if (wasActive)
            {
                await SetTestModeAsync(active: false);
            }

            SendRaw("ATO");
            await Task.Delay(250);
            SendRaw("ATO");

            if (wasActive)
            {
                await SetTestModeAsync(active: true);
            }
        }
        finally
        {
            _queryInProgress = false;
        }
    }

    private async Task ExitToDataModeManualAsync()
    {
        if (_queryInProgress) return;
        _queryInProgress = true;
        try
        {
            if (_lastTestActive)
            {
                await SetTestModeAsync(active: false);
            }

            SendRaw("+++");
        }
        finally
        {
            _queryInProgress = false;
        }
    }

    private static string? PassthroughResponse(string raw)
    {
        string trimmed = raw.Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }

    private static readonly Regex TestFrameLine = new(@"\|[^|\r\n]*\|\r\n", RegexOptions.Compiled);

    private static string StripTestFrames(string raw) => TestFrameLine.Replace(raw, string.Empty);

    private async Task<bool> SetTestModeAsync(bool active, int maxAttempts = 10, int perAttemptTimeoutMs = 1500)
    {
        if (_lastTestActive == active) return true;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(_jitter.Next(60, 420));
            }

            var confirmed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnTest(bool a)
            {
                if (a == active) confirmed.TrySetResult(true);
            }

            bool frameArrived = false;
            void OnFrame(TestFrame _) => frameArrived = true;

            _parser.TestModeChanged += OnTest;
            _parser.TestFrameReceived += OnFrame;
            try
            {
                SendRaw("AT+T");
                await Task.WhenAny(confirmed.Task, Task.Delay(perAttemptTimeoutMs + _jitter.Next(0, 300)));
            }
            finally
            {
                _parser.TestModeChanged -= OnTest;
                _parser.TestFrameReceived -= OnFrame;
            }

            if (_lastTestActive == active) return true;
            if (active && frameArrived) return true;
            if (!active && !frameArrived) return true;
        }

        return _lastTestActive == active;
    }

    private Task<string?> CaptureResponseAsync(string command, int idleMs)
    {
        _captureBuffer = new StringBuilder();
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureCompletion = tcs;
        _captureTimer.Interval = idleMs;
        SendRaw(command);
        _captureTimer.Stop();
        _captureTimer.Start();
        return tcs.Task;
    }

    private void OnCaptureTimerTick(object? sender, EventArgs e)
    {
        _captureTimer.Stop();
        string? text = _captureBuffer?.ToString();
        _captureBuffer = null;
        var tcs = _captureCompletion;
        _captureCompletion = null;
        tcs?.TrySetResult(string.IsNullOrWhiteSpace(text) ? null : text);
    }

    private static string? ParseStatusResponse(string raw)
    {
        var serial = Regex.Match(raw, @"Serial No:\s*(\d+)");
        var baud = Regex.Match(raw, @"Baud rate:\s*(\d+)");
        if (!serial.Success || !baud.Success) return null;
        return $"{Loc.T("popup.serialNo")}: {serial.Groups[1].Value}\r\n{Loc.T("popup.baudRate")}: {baud.Groups[1].Value}";
    }

    private static string? ParseSensorListResponse(string raw)
    {
        var matches = Regex.Matches(raw, @"ID:(\d+)\s*->\s*([^\r\n]+)");
        if (matches.Count == 0) return null;
        var sb = new StringBuilder();
        foreach (Match m in matches) sb.AppendLine($"ID {m.Groups[1].Value}: {m.Groups[2].Value.Trim()}");
        return sb.ToString().TrimEnd();
    }

    private static string? ParseHelpResponse(string raw)
    {
        int idx = raw.IndexOf("+HELP:", StringComparison.Ordinal);
        if (idx < 0) return null;
        return raw[(idx + "+HELP:".Length)..].Trim();
    }

    private Control BuildMainContent()
    {
        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.Orientation = Orientation.Vertical;
        _mainSplit.Panel1MinSize = 10;
        _mainSplit.Panel2MinSize = 10;
        _mainSplit.Panel2Collapsed = true;

        _mainSplit.Panel1.Controls.Add(BuildDashboardHost());

        var terminalPanel = BuildTerminalPanel();
        terminalPanel.Dock = DockStyle.Fill;
        var atPanel = BuildAdvancedAtPanel();
        atPanel.Dock = DockStyle.Top;
        _mainSplit.Panel2.Controls.Add(terminalPanel);
        _mainSplit.Panel2.Controls.Add(atPanel);

        return _mainSplit;
    }

    private void SetAdvancedVisible(bool show)
    {
        _advancedVisible = show;
        Width = show ? BaseWidth + AdvancedWidth : BaseWidth;

        if (show && !_splitterInitialized)
        {
            _mainSplit.Panel1MinSize = 480;
            _mainSplit.Panel2MinSize = 300;
            int desired = _mainSplit.Width - AdvancedWidth;
            _mainSplit.SplitterDistance = Math.Clamp(
                desired,
                _mainSplit.Panel1MinSize,
                Math.Max(_mainSplit.Panel1MinSize, _mainSplit.Width - _mainSplit.Panel2MinSize));
            _splitterInitialized = true;
        }

        _mainSplit.Panel2Collapsed = !show;
        RefreshTexts();
        Invalidate(true);
    }

    private (string TitleKey, Color Tint, (string Key, string NameKey, string Unit)[] Fields)[] BasicGroups() => new[]
    {
        ("grp.tempHumidity", Color.FromArgb(255, 235, 217), new[]
        {
            ("DigitalTemp", "tile.digitalTemp", "°C"),
            ("Sht20Temp", "tile.sht20Temp", "°C"),
            ("Sht20Humidity", "tile.sht20Humidity", "%RH"),
        }),
        ("grp.windVibration", Color.FromArgb(214, 234, 248), new[]
        {
            ("WindSpeed", "tile.windSpeed", "m/s"),
            ("AccelTotalG", "tile.accelTotalG", "g"),
            ("WindPwmOutput", "tile.windPwmOutput", ""),
        }),
        ("grp.powerStatus", Color.FromArgb(228, 233, 240), new[]
        {
            ("BatteryLevel", "tile.batteryLevel", "V"),
            ("Version", "tile.version", ""),
        }),
        ("grp.globalStatus", Color.FromArgb(250, 222, 222), new[]
        {
            ("GlobalTemp", "tile.statusTemp", ""),
            ("GlobalBattery", "tile.statusBattery", ""),
            ("GlobalVibration", "tile.statusVibration", ""),
            ("GlobalWind", "tile.statusWind", ""),
            ("GlobalDeltaTemp", "tile.statusDeltaTemp", ""),
        }),
        ("grp.sensorLinks", Color.FromArgb(222, 240, 245), new[]
        {
            ("LinkAmbientTemp", "tile.linkAmbientTemp", ""),
            ("LinkHeaterTemp", "tile.linkHeaterTemp", ""),
            ("LinkAccel", "tile.linkAccel", ""),
            ("LinkSht20", "tile.linkSht20", ""),
            ("LinkDigitalTemp", "tile.linkDigitalTemp", ""),
            ("LinkSolar", "tile.linkSolar", ""),
            ("LinkSoil", "tile.linkSoil", ""),
            ("LinkRain", "tile.linkRain", ""),
            ("LinkWind", "tile.linkWind", ""),
        }),
    };

    private (string TitleKey, Color Tint, (string Key, string NameKey, string Unit)[] Fields)[] CalibrationGroups() => new[]
    {
        ("grp.zeroSpeedPwm", Color.FromArgb(214, 245, 240), new[]
        {
            ("WindZeroSpeedPwm1", "tile.zeroSpeedPwm1", ""),
            ("WindZeroSpeedPwm2", "tile.zeroSpeedPwm2", ""),
            ("WindZeroSpeedPwm3", "tile.zeroSpeedPwm3", ""),
        }),
        ("grp.roomTempPwm", Color.FromArgb(233, 222, 245), new[]
        {
            ("WindRoomTempPwm1", "tile.roomTempPwm1", ""),
            ("WindRoomTempPwm3", "tile.roomTempPwm3", ""),
            ("WindSpeedSetpoint", "tile.windSpeedSetpoint", ""),
        }),
        ("grp.thresholds", Color.FromArgb(255, 235, 217), new[]
        {
            ("AccelThreshold", "tile.accelThreshold", "g"),
            ("DeviceSleepInterval", "tile.sleepInterval", "@unit.min"),
        }),
    };

    private Control BuildDashboardHost()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = WindowGray };
        _calibrationPage = BuildPage(CalibrationGroups(), "nav.back", ShowBasicPage);
        _calibrationPage.Dock = DockStyle.Fill;
        _calibrationPage.Visible = false;
        _basicPage = BuildPage(BasicGroups(), "nav.calibration", ShowCalibrationPage);
        _basicPage.Dock = DockStyle.Fill;
        host.Controls.Add(_calibrationPage);
        host.Controls.Add(_basicPage);

        Bind(() =>
        {
            if (_lastFrame is { } lf)
            {
                UpdateGlobalStatusTiles(lf);
                UpdateSensorLinkTiles(lf);
            }
        });

        return host;
    }

    private void ShowCalibrationPage()
    {
        _basicPage.Visible = false;
        _calibrationPage.Visible = true;
    }

    private void ShowBasicPage()
    {
        _calibrationPage.Visible = false;
        _basicPage.Visible = true;
    }

    private Panel BuildPage((string TitleKey, Color Tint, (string Key, string NameKey, string Unit)[] Fields)[] groups, string navKey, Action onNav)
    {
        var page = new Panel { Dock = DockStyle.Fill };
        var navRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var btnNav = new Button { AutoSize = true };
        Bind(() => btnNav.Text = Loc.T(navKey));
        btnNav.Click += (_, _) => onNav();
        navRow.Controls.Add(btnNav);

        var grid = BuildGroupGrid(groups, columns: 1);
        grid.Dock = DockStyle.Fill;

        page.Controls.Add(grid);
        page.Controls.Add(navRow);
        return page;
    }

    private Control BuildGroupGrid((string TitleKey, Color Tint, (string Key, string NameKey, string Unit)[] Fields)[] groups, int columns)
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowGray, Padding = new Padding(8) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = columns };
        for (int c = 0; c < columns; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 580));

        int row = 0, col = 0;
        foreach (var g in groups)
        {
            if (col == 0) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var card = BuildGroupCard(g.TitleKey, g.Tint, g.Fields);
            card.Dock = DockStyle.Fill;
            grid.Controls.Add(card, col, row);
            col++;
            if (col == columns)
            {
                col = 0;
                row++;
            }
        }

        scroll.Controls.Add(grid);
        return scroll;
    }

    private static Color HeaderColor(Color tint) => Color.FromArgb(
        Math.Max(tint.R - 35, 0), Math.Max(tint.G - 35, 0), Math.Max(tint.B - 35, 0));

    private Control BuildGroupCard(string titleKey, Color tint, (string Key, string NameKey, string Unit)[] fields)
    {
        var card = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(6),
            BackColor = PanelGray,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = HeaderColor(tint),
            ForeColor = Color.FromArgb(40, 40, 40),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 4, 0),
        };
        Bind(() => header.Text = Loc.T(titleKey));

        bool hasIndicator = titleKey is "grp.globalStatus" or "grp.sensorLinks";

        var inner = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, MaximumSize = new Size(540, 3000), Padding = new Padding(4) };
        foreach (var (key, nameKey, unit) in fields)
        {
            bool isUnitKey = unit.StartsWith('@');
            string unitKey = isUnitKey ? unit[1..] : unit;
            var tile = new InfoTile(Loc.T(nameKey), isUnitKey ? Loc.T(unitKey) : unit, tint);
            if (hasIndicator) tile.EnableIndicator();
            Bind(() => tile.SetName(Loc.T(nameKey)));
            if (isUnitKey) Bind(() => tile.SetUnit(Loc.T(unitKey)));
            _tiles[key] = tile;
            inner.Controls.Add(tile);
        }

        card.Controls.Add(inner);
        card.Controls.Add(header);
        return card;
    }

    private GroupBox BuildAdvancedAtPanel()
    {
        var group = new GroupBox { Text = "AT Commands", Height = 150, Padding = new Padding(6) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };

        flow.Controls.Add(CommandButton("Enter Command Mode (ATO)", EnterCommandModeManualAsync));
        flow.Controls.Add(CommandButton("Exit to Data Mode (+++)", ExitToDataModeManualAsync));

        _btnTestToggle.Text = "Start Test (AT+T)";
        _btnTestToggle.AutoSize = true;
        _btnTestToggle.Click += (_, _) => SendRaw("AT+T");
        flow.Controls.Add(_btnTestToggle);

        flow.Controls.Add(CommandButton("List Sensors (AT+C?)", () => RunCommandSilent("AT+C?")));
        flow.Controls.Add(CommandButton("Status (AT+S?)", () => RunCommandSilent("AT+S?")));
        flow.Controls.Add(CommandButton("Help (AT?)", () => RunCommandSilent("AT?")));

        group.Controls.Add(flow);
        return group;
    }

    private Button CommandButton(string text, Action onClick)
    {
        var btn = new Button { Text = text, AutoSize = true };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Button CommandButton(string text, Func<Task> onClick)
    {
        var btn = new Button { Text = text, AutoSize = true };
        btn.Click += async (_, _) => await onClick();
        return btn;
    }

    private Control BuildTerminalPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

        _terminal.Dock = DockStyle.Fill;
        _terminal.ReadOnly = true;
        _terminal.BackColor = Color.FromArgb(18, 18, 18);
        _terminal.ForeColor = Color.Gainsboro;
        _terminal.Font = new Font("Consolas", 9.5f);
        _terminal.WordWrap = false;
        _terminal.ScrollBars = RichTextBoxScrollBars.Both;
        _terminal.BorderStyle = BorderStyle.FixedSingle;

        var toolRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var btnClear = new Button { Text = "Clear", AutoSize = true };
        btnClear.Click += (_, _) => _terminal.Clear();
        _chkAutoScroll.Text = "Autoscroll";
        _chkAutoScroll.Checked = true;
        _chkAutoScroll.AutoSize = true;
        toolRow.Controls.Add(btnClear);
        toolRow.Controls.Add(_chkAutoScroll);

        var sendRow = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 3, AutoSize = true, Height = 32 };
        sendRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sendRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sendRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _txtSend.Dock = DockStyle.Fill;
        _txtSend.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendManual();
            }
        };
        var btnSend = new Button { Text = "Send", AutoSize = true };
        btnSend.Click += (_, _) => SendManual();
        var lblCrlf = new Label { Text = "(CRLF appended)", AutoSize = true, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 6, 0, 0) };
        sendRow.Controls.Add(_txtSend, 0, 0);
        sendRow.Controls.Add(btnSend, 1, 0);
        sendRow.Controls.Add(lblCrlf, 2, 0);

        panel.Controls.Add(_terminal);
        panel.Controls.Add(sendRow);
        panel.Controls.Add(toolRow);
        return panel;
    }

    private void SendManual()
    {
        if (_txtSend.Text.Length == 0) return;
        SendRaw(_txtSend.Text);
        _txtSend.Clear();
    }

    private void SendRaw(string command)
    {
        if (_port is not { IsOpen: true })
        {
            AppendTerminal("(not connected - command not sent)\r\n", Color.IndianRed);
            return;
        }
        try
        {
            byte[] bytes = Encoding.Latin1.GetBytes(command + "\r\n");
            _port.Write(bytes, 0, bytes.Length);
            AppendTerminal("» " + command + "\r\n", Color.DeepSkyBlue);
        }
        catch (Exception ex)
        {
            AppendTerminal($"(send failed: {ex.Message})\r\n", Color.IndianRed);
        }
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is not { IsOpen: true } port) return;
        try
        {
            int n = port.BytesToRead;
            if (n <= 0) return;
            var buffer = new byte[n];
            int read = port.Read(buffer, 0, n);
            if (read <= 0) return;

            _parser.Feed(buffer, read);

            string text = Encoding.Latin1.GetString(buffer, 0, read);
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(() =>
                {
                    AppendTerminal(text, Color.Gainsboro);
                    if (_captureBuffer is not null)
                    {
                        _captureBuffer.Append(text);
                        _captureTimer.Stop();
                        _captureTimer.Start();
                    }
                });
            }
        }
        catch (Exception)
        {
        }
    }

    private void AppendTerminal(string text, Color color)
    {
        if (_terminal.IsDisposed) return;
        _terminal.SelectionStart = _terminal.TextLength;
        _terminal.SelectionLength = 0;
        _terminal.SelectionColor = color;
        _terminal.AppendText(text);
        if (_chkAutoScroll.Checked)
        {
            _terminal.SelectionStart = _terminal.TextLength;
            _terminal.ScrollToCaret();
        }
    }

    private const int GlobalStatsTemp = 0x01;
    private const int GlobalStatsBattery = 0x02;
    private const int GlobalStatsVibration = 0x04;
    private const int GlobalStatsWind = 0x08;
    private const int GlobalStatsDeltaTmp = 0x80;

    private const int LinkAmbientTemp = 0;
    private const int LinkHeaterTemp = 1;
    private const int LinkAccel = 2;
    private const int LinkSht20 = 3;
    private const int LinkDigitalTemp = 4;
    private const int LinkSolar = 5;
    private const int LinkSoil = 6;
    private const int LinkRain = 7;
    private const int LinkWind = 8;

    private TestFrame? _lastFrame;

    private static readonly Color IndicatorRed = Color.FromArgb(214, 48, 48);
    private static readonly Color IndicatorGray = Color.FromArgb(165, 165, 165);

    private static string FormatFloat(double value, double? sentinel = null) =>
        sentinel is double s && value == s ? "-" : value.ToString("0.00", CultureInfo.InvariantCulture);

    private static (string Text, Color Indicator) DecodeGlobalBit(int status, int mask, bool setMeansLow)
    {
        bool bitSet = (status & mask) != 0;
        string text = (bitSet == setMeansLow) ? Loc.T("status.low") : Loc.T("status.high");
        Color color = bitSet ? IndicatorRed : IndicatorGray;
        return (text, color);
    }

    private static (string Text, Color Indicator) DecodeLink(int linkStatus, int bit)
    {
        bool ok = ((linkStatus >> bit) & 1) != 0;
        return ok ? (Loc.T("status.linkOk"), IndicatorRed) : ("-", IndicatorGray);
    }

    private void SetStatusTile(string key, (string Text, Color Indicator) decoded)
    {
        _tiles[key].SetValue(decoded.Text);
        _tiles[key].SetIndicatorColor(decoded.Indicator);
    }

    private void UpdateGlobalStatusTiles(TestFrame f)
    {
        SetStatusTile("GlobalTemp", DecodeGlobalBit(f.GlobalStatus, GlobalStatsTemp, setMeansLow: true));
        SetStatusTile("GlobalBattery", DecodeGlobalBit(f.GlobalStatus, GlobalStatsBattery, setMeansLow: true));
        SetStatusTile("GlobalVibration", DecodeGlobalBit(f.GlobalStatus, GlobalStatsVibration, setMeansLow: false));
        SetStatusTile("GlobalWind", DecodeGlobalBit(f.GlobalStatus, GlobalStatsWind, setMeansLow: false));
        SetStatusTile("GlobalDeltaTemp", DecodeGlobalBit(f.GlobalStatus, GlobalStatsDeltaTmp, setMeansLow: false));
    }

    private void UpdateSensorLinkTiles(TestFrame f)
    {
        SetStatusTile("LinkAmbientTemp", DecodeLink(f.SensorLinkStatus, LinkAmbientTemp));
        SetStatusTile("LinkHeaterTemp", DecodeLink(f.SensorLinkStatus, LinkHeaterTemp));
        SetStatusTile("LinkAccel", DecodeLink(f.SensorLinkStatus, LinkAccel));
        SetStatusTile("LinkSht20", DecodeLink(f.SensorLinkStatus, LinkSht20));
        SetStatusTile("LinkDigitalTemp", DecodeLink(f.SensorLinkStatus, LinkDigitalTemp));
        SetStatusTile("LinkSolar", DecodeLink(f.SensorLinkStatus, LinkSolar));
        SetStatusTile("LinkSoil", DecodeLink(f.SensorLinkStatus, LinkSoil));
        SetStatusTile("LinkRain", DecodeLink(f.SensorLinkStatus, LinkRain));
        SetStatusTile("LinkWind", DecodeLink(f.SensorLinkStatus, LinkWind));
    }

    private void OnTestFrame(TestFrame f)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            if (!_lastTestActive) OnTestModeChanged(true);

            _lastFrame = f;
            var ci = CultureInfo.InvariantCulture;
            _tiles["DigitalTemp"].SetValue(FormatFloat(f.DigitalTemperature, 10000.0));
            _tiles["Sht20Temp"].SetValue(FormatFloat(f.Sht20Temperature, 1000.0));
            _tiles["Sht20Humidity"].SetValue(FormatFloat(f.Sht20Humidity, 10000.0));
            _tiles["BatteryLevel"].SetValue(FormatFloat(f.BatteryLevel));
            _tiles["WindSpeed"].SetValue(FormatFloat(f.WindSpeed, 1000.0));
            _tiles["WindSpeedSetpoint"].SetValue(FormatFloat(f.WindSpeedSetpoint));
            _tiles["AccelTotalG"].SetValue(FormatFloat(f.AccelTotalG));
            _tiles["AccelThreshold"].SetValue(FormatFloat(f.AccelThreshold));
            _tiles["WindZeroSpeedPwm1"].SetValue(f.WindZeroSpeedPwm1.ToString(ci));
            _tiles["WindZeroSpeedPwm2"].SetValue(f.WindZeroSpeedPwm2.ToString(ci));
            _tiles["WindZeroSpeedPwm3"].SetValue(f.WindZeroSpeedPwm3.ToString(ci));
            _tiles["WindRoomTempPwm1"].SetValue(f.WindRoomTempPwm1.ToString(ci));
            _tiles["WindRoomTempPwm3"].SetValue(f.WindRoomTempPwm3.ToString(ci));
            _tiles["WindPwmOutput"].SetValue(f.WindPwmOutput.ToString(ci));
            _tiles["DeviceSleepInterval"].SetValue(f.DeviceSleepInterval.ToString(ci));
            _tiles["Version"].SetValue("v" + (f.Version / 100.0).ToString("0.00", ci));
            UpdateGlobalStatusTiles(f);
            UpdateSensorLinkTiles(f);

            _frameCount++;
            _statusFrames.Text = $"{Loc.T("status.frames")}: {_frameCount}";
        });
    }

    private void OnModeChanged(LinkMode mode)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            _lastMode = mode;
            RefreshTexts();
        });
    }

    private void OnTestModeChanged(bool active)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            _lastTestActive = active;
            _btnTestToggle.Text = active ? "Stop Test (AT+T)" : "Start Test (AT+T)";
            RefreshTexts();
        });
    }

    private static Label LabelFor() => new()
    {
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(0, 6, 2, 0),
    };

    private static Control Spacer() => new Panel { Width = 14, Height = 1 };
}
