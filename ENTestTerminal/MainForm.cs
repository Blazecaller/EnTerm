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


    private static readonly Dictionary<string, CalibrationSetter> CalibrationSetters = new()
    {
        ["WindZeroSpeedPwm1"] = new("AT+CALP1", CalibInput.Integer, v => v is >= 0 and <= 1000, "0 – 1000"),
        ["WindZeroSpeedPwm2"] = new("AT+CALP2", CalibInput.Integer, v => v is >= 0 and <= 1000, "0 – 1000"),
        ["WindZeroSpeedPwm3"] = new("AT+CALP3", CalibInput.Integer, v => v is >= 0 and <= 1000, "0 – 1000"),
        ["CalibSpeed1"] = new("AT+CALS1", CalibInput.Float, v => v >= 0, "≥ 0"),
        ["CalibSpeed2"] = new("AT+CALS2", CalibInput.Float, v => v >= 0, "≥ 0"),
        ["CalibSpeed3"] = new("AT+CALS3", CalibInput.Float, v => v >= 0, "≥ 0"),
        ["WindRoomTempPwm1"] = new("AT+CALR1", CalibInput.Integer, v => v is >= 0 and <= 999, "0 – 999"),
        ["WindRoomTempPwm2"] = new("AT+CALR2", CalibInput.Integer, v => v is >= 0 and <= 999, "0 – 999"),
        ["NoWindPwm"] = new("AT+NOW", CalibInput.None, _ => true, string.Empty),
        ["HeaterSetpoint"] = new("AT+HSP", CalibInput.Float, v => v is > 0 and < 100, "0 < x < 100"),
        ["PidKp"] = new("AT+KP", CalibInput.Float, v => v >= 0, "≥ 0", Temporary: true),
        ["PidKi"] = new("AT+KI", CalibInput.Float, v => v >= 0, "≥ 0", Temporary: true),
        ["PidKd"] = new("AT+KD", CalibInput.Float, v => v >= 0, "≥ 0", Temporary: true),
    };

    private static readonly Parity[] ParityValues = { Parity.None, Parity.Odd, Parity.Even, Parity.Mark, Parity.Space };
    private static readonly string[] ParityKeys = { "parity.none", "parity.odd", "parity.even", "parity.mark", "parity.space" };
    private static readonly StopBits[] StopBitsValues = { StopBits.One, StopBits.OnePointFive, StopBits.Two };
    private static readonly string[] StopBitsLabels = { "1", "1.5", "2" };

    private static readonly Color WindowGray = Color.FromArgb(232, 233, 236);
    private static readonly Color PanelGray = Color.FromArgb(244, 244, 246);

    private const int BaseWidth = 1320;
    private const int BaseHeight = 820;
    private const int AdvancedWidth = 480;

    private SerialPort? _port;
    private readonly AtLinkParser _parser = new();
    private int _frameCount;
    private LinkMode _lastMode = LinkMode.Unknown;
    private bool _lastTestActive;
    private bool _lastCalibActive;
    private bool _calibUnsupported;
    private string? _lastPortName;
    private int _lastBaud;
    private bool _advancedVisible;

    private readonly System.Windows.Forms.Timer _linkWatchdog = new() { Interval = 1000 };
    private DateTime _lastFrameUtc = DateTime.UtcNow;
    private bool _linkStale;
    private bool _recovering;
    private const int LinkSilenceMs = 4000;

    private readonly Dictionary<string, InfoTile> _tiles = new();
    private readonly List<Action> _retext = new();

    private readonly ComboBox _cmbPort = new();
    private readonly ComboBox _cmbBaud = new();
    private readonly ComboBox _cmbParity = new();
    private readonly ComboBox _cmbDataBits = new();
    private readonly ComboBox _cmbStopBits = new();
    private readonly Button _btnConnect = new();
    private readonly Label _lblConnDot = new();
    private readonly Label _lblConnHelp = new();
    private readonly ToolTip _connToolTip = new();
    private readonly Label _lblSerialNo = new();
    private string? _lastSerialNo;


    private readonly ToolStripButton _btnTestToggle = new();

    private Panel _basicPage = null!;
    private Panel _calibrationPage = null!;
    private readonly Label _lblCalibBanner = new();
    private readonly Button _btnSetZeroWind = new();
    private readonly Button _btnCalibAdvanced = new();
    private bool _calibAdvancedVisible;
    private readonly Dictionary<string, Control> _cards = new();
    private static readonly string[] AdvancedCalibrationGroups = { "grp.heater", "grp.pid" };

    private readonly SplitContainer _mainSplit = new();
    private bool _splitterInitialized;
    private readonly ToolStripButton _btnAdvancedToggle = new();
    private readonly ToolStrip _atStrip = new();

    private readonly RichTextBox _terminal = new();
    private readonly TextBox _txtSend = new();
    private readonly CheckBox _chkAutoScroll = new();

    private readonly System.Windows.Forms.Timer _captureTimer = new() { Interval = 500 };
    private StringBuilder? _captureBuffer;
    private DateTime _captureDeadlineUtc;
    private const int CaptureMaxExtraMs = 2500;
    private TaskCompletionSource<string?>? _captureCompletion;
    private bool _queryInProgress;
    private readonly Random _jitter = new();

    private readonly ToolStripStatusLabel _statusConn = new();
    private readonly ToolStripStatusLabel _statusMode = new();
    private readonly ToolStripStatusLabel _statusTest = new();
    private readonly ToolStripStatusLabel _statusCalib = new();
    private readonly ToolStripStatusLabel _statusFrames = new();

    private readonly MenuStrip _menuStrip = new();
    private readonly ToolStripButton _btnLanguage = new();

    public MainForm()
    {
        Text = "ENterm";
        MinimumSize = new Size(1080, 640);
        Size = new Size(BaseWidth, BaseHeight);
        BackColor = WindowGray;
        Font = AppFonts.FormDefault;
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
        _parser.CalibrationFrameReceived += OnCalibrationFrame;
        _parser.CalibrationModeChanged += OnCalibrationModeChanged;
        _captureTimer.Tick += OnCaptureTimerTick;
        _linkWatchdog.Tick += OnLinkWatchdogTick;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = WindowGray,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var mainContent = BuildMainContent();
        var languageBar = BuildLanguageBar();
        var connectionStrip = BuildConnectionStrip();
        var atStrip = BuildAtStrip();

        root.Controls.Add(languageBar, 0, 0);
        root.Controls.Add(connectionStrip, 0, 1);
        root.Controls.Add(atStrip, 0, 2);
        root.Controls.Add(mainContent, 0, 3);

        var status = new StatusStrip();
        status.Items.Add(_statusConn);
        status.Items.Add(new ToolStripSeparator());
        status.Items.Add(_statusMode);
        status.Items.Add(new ToolStripSeparator());
        status.Items.Add(_statusTest);
        status.Items.Add(new ToolStripSeparator());
        status.Items.Add(_statusCalib);
        status.Items.Add(new ToolStripSeparator());
        status.Items.Add(_statusFrames);
        Bind(() => _statusConn.Text = _port is not { IsOpen: true }
            ? Loc.T("status.disconnected")
            : _linkStale
                ? $"{Loc.T("status.connected")}: {_lastPortName} @ {_lastBaud} ({Loc.T("status.noSignal")})"
                : $"{Loc.T("status.connected")}: {_lastPortName} @ {_lastBaud}");
        Bind(() => _statusMode.Text = $"{Loc.T("status.mode")}: {ModeText(_lastMode)}");
        Bind(() => _statusTest.Text = $"{Loc.T("status.test")}: {(_lastTestActive ? Loc.T("status.on") : Loc.T("status.off"))}");
        Bind(() => _statusCalib.Text = $"{Loc.T("status.calib")}: {(_lastCalibActive ? Loc.T("status.on") : Loc.T("status.off"))}");
        Bind(() => _statusFrames.Text = $"{Loc.T("status.frames")}: {_frameCount}");

        _menuStrip.Font = AppFonts.MenuStrip;
        _menuStrip.Renderer = new ButtonFrameRenderer();

        _btnLanguage.DisplayStyle = ToolStripItemDisplayStyle.Text;
        Bind(() => _btnLanguage.Text = Loc.Current == AppLanguage.Turkish ? "EN" : "TR");
        _btnLanguage.Click += (_, _) =>
        {
            Loc.Toggle();
            RefreshTexts();
        };
        _menuStrip.Items.Add(_btnLanguage);
        AddMenuBarButtons();

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
        // Equal side columns keep the mode button centred; the serial number sits to its right.
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = WindowGray, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var modeStrip = new ToolStrip
        {
            Dock = DockStyle.None,
            Anchor = AnchorStyles.None,
            AutoSize = true,
            GripStyle = ToolStripGripStyle.Hidden,
            Font = AppFonts.ToolbarRowTitle,
            Renderer = new ButtonFrameRenderer(),
            BackColor = WindowGray,
            CanOverflow = false,
        };
        var btnCalibToggle = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Text };
        Bind(() => btnCalibToggle.Text = _calibrationPage.Visible ? Loc.T("nav.back") : Loc.T("nav.calibration"));
        btnCalibToggle.Click += async (_, _) =>
        {
            if (_calibrationPage.Visible) await ExitCalibrationAsync(); else await EnterCalibrationAsync();
        };
        modeStrip.Items.Add(btnCalibToggle);
        panel.Controls.Add(modeStrip, 1, 0);

        _lblSerialNo.Dock = DockStyle.Fill;
        _lblSerialNo.TextAlign = ContentAlignment.MiddleLeft;
        _lblSerialNo.Padding = new Padding(12, 0, 0, 0);
        _lblSerialNo.Font = AppFonts.ToolbarRowTitle;
        Bind(() => _lblSerialNo.Text = _lastSerialNo is { } sn ? $"{Loc.T("popup.serialNo")}: {sn}" : string.Empty);
        panel.Controls.Add(_lblSerialNo, 2, 0);

        return panel;
    }

    private static Button StripButton()
    {
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = AppFonts.ConnectionStripText,
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonFrameRenderer.FillNormal,
            Margin = new Padding(3, 2, 3, 2),
            Padding = new Padding(4, 0, 4, 0),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = ButtonFrameRenderer.BorderColor;
        button.FlatAppearance.MouseOverBackColor = ButtonFrameRenderer.FillHover;
        button.FlatAppearance.MouseDownBackColor = ButtonFrameRenderer.FillPressed;
        return button;
    }

    private static void StyleCombo(ComboBox combo, int width, ComboBoxStyle style = ComboBoxStyle.DropDownList)
    {
        combo.DropDownStyle = style;
        combo.Font = AppFonts.ConnectionStripText;
        combo.Width = width;
        combo.Margin = new Padding(3, 3, 3, 3);
    }

    private Control BuildConnectionStrip()
    {
        var strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = HeaderColor(Color.FromArgb(214, 234, 248)),
            Padding = new Padding(6, 0, 6, 0),
            Margin = Padding.Empty,
        };

        Label StripLabel(string key, bool title = false)
        {
            var label = new Label
            {
                AutoSize = true,
                Font = title ? AppFonts.ConnectionStripTitle : AppFonts.ConnectionStripText,
                Margin = new Padding(title ? 2 : 10, 0, 2, 0),
            };
            Bind(() => label.Text = Loc.T(key));
            return label;
        }

        StyleCombo(_cmbPort, 100);
        var btnRefresh = StripButton();
        Bind(() => btnRefresh.Text = Loc.T("conn.refresh"));
        btnRefresh.Click += (_, _) => RefreshPorts();

        StyleCombo(_cmbBaud, 100, ComboBoxStyle.DropDown);
        _cmbBaud.Items.AddRange(new object[] { 9600, 19200, 38400, 57600, 115200 });
        _cmbBaud.SelectedItem = 19200;

        StyleCombo(_cmbParity, 100);
        Bind(() =>
        {
            int sel = Math.Max(_cmbParity.SelectedIndex, 0);
            _cmbParity.Items.Clear();
            foreach (var k in ParityKeys) _cmbParity.Items.Add(Loc.T(k));
            _cmbParity.SelectedIndex = sel;
        });

        StyleCombo(_cmbDataBits, 55);
        _cmbDataBits.Items.AddRange(new object[] { 7, 8 });
        _cmbDataBits.SelectedItem = 8;

        StyleCombo(_cmbStopBits, 65);
        _cmbStopBits.Items.AddRange(StopBitsLabels);
        _cmbStopBits.SelectedIndex = 0;

        _btnConnect.AutoSize = true;
        _btnConnect.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnConnect.Font = AppFonts.ConnectionStripTitle;
        _btnConnect.FlatStyle = FlatStyle.Flat;
        _btnConnect.BackColor = ButtonFrameRenderer.FillNormal;
        _btnConnect.FlatAppearance.BorderColor = ButtonFrameRenderer.BorderColor;
        _btnConnect.FlatAppearance.MouseOverBackColor = ButtonFrameRenderer.FillHover;
        _btnConnect.FlatAppearance.MouseDownBackColor = ButtonFrameRenderer.FillPressed;
        _btnConnect.Padding = new Padding(10, 2, 10, 2);
        _btnConnect.Margin = new Padding(14, 3, 3, 3);
        _btnConnect.Cursor = Cursors.Hand;
        Bind(() => _btnConnect.Text = (_port is { IsOpen: true }) ? Loc.T("conn.disconnect") : Loc.T("conn.connect"));
        _btnConnect.Click += (_, _) => ToggleConnection();

        _lblConnDot.Text = string.Empty;
        Bind(() => _lblConnDot.ForeColor = _port is not { IsOpen: true }
            ? Color.Firebrick
            : _linkStale ? Color.DarkOrange : Color.ForestGreen);
        _lblConnDot.AutoSize = false;
        _lblConnDot.Size = new Size(42, 42);
        _lblConnDot.Margin = new Padding(4, 0, 0, 0);
        _lblConnDot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int d = Math.Min(_lblConnDot.Width, _lblConnDot.Height) - 10;
            var rect = new Rectangle((_lblConnDot.Width - d) / 2, (_lblConnDot.Height - d) / 2, d, d);
            using var brush = new SolidBrush(_lblConnDot.ForeColor);
            e.Graphics.FillEllipse(brush, rect);
        };
        _lblConnDot.ForeColorChanged += (_, _) => _lblConnDot.Invalidate();

        _lblConnHelp.Text = "?";
        _lblConnHelp.Font = AppFonts.ConnectionHelpIcon;
        _lblConnHelp.ForeColor = Color.DimGray;
        _lblConnHelp.AutoSize = false;
        _lblConnHelp.Size = new Size(26, 26);
        _lblConnHelp.Margin = new Padding(2, 0, 0, 0);
        _lblConnHelp.TextAlign = ContentAlignment.MiddleCenter;
        _lblConnHelp.Cursor = Cursors.Help;
        _lblConnHelp.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, _lblConnHelp.Width - 3, _lblConnHelp.Height - 3);
            using var pen = new Pen(Color.DimGray, 1.2f);
            e.Graphics.DrawEllipse(pen, rect);
        };
        _connToolTip.AutoPopDelay = 20000;
        _connToolTip.InitialDelay = 300;
        _connToolTip.ReshowDelay = 100;
        _connToolTip.OwnerDraw = true;
        _connToolTip.Popup += (_, e) =>
        {
            string text = _connToolTip.GetToolTip(e.AssociatedControl) ?? string.Empty;
            Size textSize = TextRenderer.MeasureText(text, AppFonts.ConnectionHelpTooltip);
            e.ToolTipSize = new Size(textSize.Width + 20, textSize.Height + 16);
        };
        _connToolTip.Draw += (_, e) =>
        {
            e.DrawBackground();
            e.DrawBorder();
            var textBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 8, e.Bounds.Width - 20, e.Bounds.Height - 16);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, AppFonts.ConnectionHelpTooltip, textBounds, SystemColors.InfoText, TextFormatFlags.Left | TextFormatFlags.Top);
        };
        Bind(() => _connToolTip.SetToolTip(_lblConnHelp, Loc.T("conn.statusHelp")));

        strip.Controls.AddRange(new Control[]
        {
            StripLabel("conn.title", title: true),
            StripLabel("conn.port"), _cmbPort, btnRefresh,
            StripLabel("conn.baud"), _cmbBaud,
            StripLabel("conn.parity"), _cmbParity,
            StripLabel("conn.databits"), _cmbDataBits,
            StripLabel("conn.stopbits"), _cmbStopBits,
            _btnConnect, _lblConnDot, _lblConnHelp,
        });
        // Anchor without Top/Bottom centres each control vertically in the row.
        foreach (Control c in strip.Controls) c.Anchor = AnchorStyles.Left;
        return strip;
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
            CenteredMessage.Show(this, Loc.T("msg.noPort"), Loc.T("msg.noPortTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_cmbBaud.Text, out int baud) || baud <= 0)
        {
            CenteredMessage.Show(this, Loc.T("msg.invalidBaud"), Loc.T("msg.invalidBaudTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            CenteredMessage.Show(this, $"{portName}:\n{ex.Message}", Loc.T("msg.connFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            _port = null;
            return;
        }

        _lastPortName = portName;
        _lastBaud = baud;
        _lastFrameUtc = DateTime.UtcNow;
        _linkStale = false;
        _recovering = false;
        _linkWatchdog.Start();
        SetConnectionControlsEnabled(false);
        RefreshTexts();
        AppendTerminal($"--- connected to {portName} @ {baud} {parity} {dataBits}{StopBitsShort(stopBits)} ---\r\n", Color.Gray);

        await AutoEnterCommandModeAsync();

        if (_port is { IsOpen: true } && _lastMode != LinkMode.Command)
        {
            _linkStale = true;
            RefreshTexts();
            _ = RecoveryLoopAsync();
        }
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
            if (!await TryEnterCommandModeAsync())
            {
                AppendTerminal("--- " + Loc.T("msg.handshakeFailed") + " ---\r\n", Color.Orange);
                return;
            }

            await QuerySerialNumberSilentlyAsync();

            // After a power loss the device comes back with no stream, so restart the one the open page needs.
            if (_calibrationPage.Visible && !_calibUnsupported)
            {
                if (await CaptureParsedAsync("AT+CALIB=1", ParseCalibOnReply, attempts: 2) is { Accepted: true }) return;
                AppendTerminal("--- " + Loc.T("msg.calibEnterFailed") + " ---\r\n", Color.Orange);
            }

            var frameSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFrame(TestFrame _) => frameSeen.TrySetResult(true);
            void OnCalibFrame(CalibrationFrame _) => frameSeen.TrySetResult(true);
            _parser.TestFrameReceived += OnFrame;
            _parser.CalibrationFrameReceived += OnCalibFrame;
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
                _parser.CalibrationFrameReceived -= OnCalibFrame;
            }
        }
        finally
        {
            _queryInProgress = false;
        }
    }

    private async Task<bool> TryEnterCommandModeAsync()
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
            return modeReached.Task.IsCompleted;
        }
        finally
        {
            _parser.ModeChanged -= OnMode;
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
        _lastCalibActive = false;
        _calibUnsupported = false;
        _lastSerialNo = null;
        _linkWatchdog.Stop();
        _linkStale = false;
        _recovering = false;
        if (_calibrationPage.Visible) ShowBasicPage();
        SetConnectionControlsEnabled(true);
        RefreshTexts();
        AppendTerminal("--- disconnected ---\r\n", Color.Gray);
    }

    private void OnLinkWatchdogTick(object? sender, EventArgs e)
    {
        if (_port is { IsOpen: false })
        {
            HandlePortLost(new IOException("Serial port closed unexpectedly"));
            return;
        }
        if (_port is not { IsOpen: true }) return;
        if (_queryInProgress || _linkStale || !(_lastTestActive || _lastCalibActive)) return;
        if ((DateTime.UtcNow - _lastFrameUtc).TotalMilliseconds < LinkSilenceMs) return;

        _linkStale = true;
        _lastMode = LinkMode.Unknown;
        _lastTestActive = false;
        _lastCalibActive = false;
        AppendTerminal("--- no signal from device (possible power loss) ---\r\n", Color.Orange);
        RefreshTexts();
        _ = RecoveryLoopAsync();
    }

    private async Task RecoveryLoopAsync()
    {
        if (_recovering) return;
        _recovering = true;
        try
        {
            while (_linkStale && _port is { IsOpen: true })
            {
                await AutoEnterCommandModeAsync();
                if (!_linkStale) break;
                await Task.Delay(1500);
            }
        }
        finally
        {
            _recovering = false;
        }
    }

    private void HandlePortLost(Exception ex)
    {
        if (_port is null) return;
        AppendTerminal($"--- device disconnected: {ex.Message} ---\r\n", Color.IndianRed);
        ClosePort();
    }

    private void SetConnectionControlsEnabled(bool enabled)
    {
        _cmbPort.Enabled = enabled;
        _cmbBaud.Enabled = enabled;
        _cmbParity.Enabled = enabled;
        _cmbDataBits.Enabled = enabled;
        _cmbStopBits.Enabled = enabled;
    }

    private void RunCommandDialog(CommandDialog dialog, string titleKey)
    {
        using (dialog)
        {
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Command is null) return;
            RunQuery(dialog.Command, titleKey, PassthroughResponse);
        }
    }

    private void OpenDeviceSettings()
    {
        using var dialog = new DeviceSettingsDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Command is null) return;
        RunQuery(dialog.Command, dialog.IsSerialChange ? "basic.setSerial" : "basic.setBaud", PassthroughResponse);
    }

    private void AddMenuBarButtons()
    {
        ToolStripButton MenuButton(string key, Action onClick)
        {
            var button = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Text, Margin = new Padding(6, 1, 0, 2) };
            Bind(() => button.Text = Loc.T(key));
            button.Click += (_, _) => onClick();
            return button;
        }

        _menuStrip.Items.Add(MenuButton("cfg.sensors", () => RunCommandDialog(new SensorConfigDialog(SensorIds), "popup.sensorControl")));
        _menuStrip.Items.Add(MenuButton("cfg.device", OpenDeviceSettings));
        _menuStrip.Items.Add(MenuButton("basic.showSensorList", () => RunQuery("AT+C?", "popup.sensorList", ParseSensorListResponse)));
        _menuStrip.Items.Add(MenuButton("basic.systemStatus", () => RunQuery("AT+S?", "popup.status", ParseStatusResponse)));
        _menuStrip.Items.Add(MenuButton("basic.help", () => RunQuery("AT?", "popup.help", ParseHelpResponse)));

        _btnAdvancedToggle.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _btnAdvancedToggle.Margin = new Padding(6, 1, 0, 2);
        Bind(() => _btnAdvancedToggle.Text = _advancedVisible ? Loc.T("basic.advancedHide") : Loc.T("basic.advancedShow"));
        _btnAdvancedToggle.Click += (_, _) => SetAdvancedVisible(!_advancedVisible);
        _menuStrip.Items.Add(_btnAdvancedToggle);
    }

    private async Task RunCommand(string command, string? titleKey, Func<string, string?>? parse)
    {
        if (_queryInProgress) return;
        if (_port is not { IsOpen: true })
        {
            if (titleKey is not null)
            {
                CenteredMessage.Show(this, Loc.T("msg.commError"), Loc.T(titleKey), MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                CenteredMessage.Show(
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

    /// <summary>Sends the command and parses the reply without touching the running stream. Caller must hold <see cref="_queryInProgress"/>.</summary>
    private async Task<T?> CaptureParsedAsync<T>(string command, Func<string, T?> parse, int attempts = 3) where T : class
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            string? captured = await CaptureResponseAsync(command, idleMs: 500);
            if (captured is null) continue;
            T? result = parse(StripTestFrames(captured));
            if (result is not null) return result;
        }
        return null;
    }

    /// <summary>Runs <paramref name="body"/> while connected and in AT command mode. Returns false if it could not start.</summary>
    private async Task<bool> RunInCommandModeAsync(Func<Task> body)
    {
        if (_queryInProgress) return false;
        if (_port is not { IsOpen: true })
        {
            CenteredMessage.Show(this, Loc.T("msg.notConnected"), Loc.T("msg.calibTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        _queryInProgress = true;
        UseWaitCursor = true;
        try
        {
            if (_lastMode != LinkMode.Command && !await TryEnterCommandModeAsync())
            {
                CenteredMessage.Show(this, Loc.T("msg.commandModeFailed"), Loc.T("msg.calibTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            await body();
            return true;
        }
        finally
        {
            _queryInProgress = false;
            UseWaitCursor = false;
            RefreshTexts();
        }
    }

    private sealed record CalibOnReply(bool Accepted);

    private static CalibOnReply? ParseCalibOnReply(string raw) =>
        raw.Contains("CALIB PACK ON") ? new CalibOnReply(true)
        : raw.Contains("ERROR") ? new CalibOnReply(false)
        : null;

    private async Task EnterCalibrationAsync()
    {
        await RunInCommandModeAsync(async () =>
        {
            var reply = await CaptureParsedAsync("AT+CALIB=1", ParseCalibOnReply);
            if (reply is { Accepted: true })
            {
                // The firmware stops the test stream when the calibration pack starts.
                _calibUnsupported = false;
                _lastCalibActive = true;
                _lastTestActive = false;
                _lastFrameUtc = DateTime.UtcNow;
            }
            else
            {
                // Older firmware has no calibration pack: stay on the test pack and show its values read-only.
                _calibUnsupported = true;
                AppendTerminal("--- " + Loc.T("calib.unsupported") + " ---\r\n", Color.Orange);
                if (!_lastTestActive) await SetTestModeAsync(active: true);
            }

            ApplyCalibrationAvailability();
            ShowCalibrationPage();
        });
    }

    private async Task ExitCalibrationAsync()
    {
        if (_queryInProgress) return;
        if (_port is { IsOpen: true })
        {
            await RunInCommandModeAsync(async () =>
            {
                if (!_calibUnsupported)
                {
                    await CaptureParsedAsync("AT+CALIB=0", r => r.Contains("CALIB PACK OFF") ? r : null, attempts: 2);
                    _lastCalibActive = false;
                }

                // The test page needs the test stream. BEGIN TEST also switches calibration off if AT+CALIB=0 was missed.
                if (!_lastTestActive && !await SetTestModeAsync(active: true))
                {
                    CenteredMessage.Show(this, Loc.T("msg.calibExitFailed"), Loc.T("msg.calibTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }
        ShowBasicPage();
        RefreshTexts();
    }

    private static readonly string[] CalibrationOnlyTiles =
        { "CalibSpeed1", "CalibSpeed2", "CalibSpeed3", "NoWindPwm", "HeaterSetpoint", "PidKp", "PidKi", "PidKd", "HeaterTemp" };

    private void ApplyCalibrationAvailability()
    {
        _lblCalibBanner.Visible = _calibUnsupported;
        foreach (var key in CalibrationSetters.Keys) _tiles[key].SetClickEnabled(!_calibUnsupported);
        _btnSetZeroWind.Enabled = !_calibUnsupported;
        if (_calibUnsupported)
        {
            foreach (var key in CalibrationOnlyTiles) _tiles[key].SetValue("--");
        }
    }

    /// <summary>Applies "calibration is off" (CALIB PACK OFF, BEGIN TEST, +++ or a test pack) and leaves the calibration page if it depended on it.</summary>
    private void MarkCalibrationOff()
    {
        _lastCalibActive = false;
        if (!_calibrationPage.Visible || _calibUnsupported) return;
        if (!_queryInProgress)
        {
            AppendTerminal("--- " + Loc.T("calib.endedByDevice") + " ---\r\n", Color.Orange);
        }
        ShowBasicPage();
    }

    private sealed record SetterReply(bool Ok, string Message);

    private static SetterReply? ParseSetterReply(string raw)
    {
        var error = Regex.Match(raw, @"ERROR[^\r\n]*");
        if (error.Success) return new SetterReply(false, error.Value.Trim());

        var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
        if (!lines.Contains("OK")) return null;
        // A capture can start or end mid-pack, leaving a fragment with a '|' that is not part of the reply.
        string message = string.Join("\r\n", lines.Where(l => l.Length > 0 && l != "OK" && !l.StartsWith("AT", StringComparison.Ordinal) && !l.Contains('|')));
        return new SetterReply(true, message.Length > 0 ? message : "OK");
    }

    private void OnCalibrationTileClicked(string key, InfoTile tile, string title) => RunCalibrationSetter(key, title, tile.Value);

    private async void RunCalibrationSetter(string key, string title, string currentValue)
    {
        if (_queryInProgress || _calibUnsupported) return;
        var setter = CalibrationSetters[key];

        string? value;
        using (var dialog = new CalibrationEditDialog(title, setter, currentValue, _tiles["HeaterPwmLive"].Value))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            value = dialog.Value;
        }

        string command = setter.BuildCommand(value);
        SetterReply? reply = null;
        bool ran = await RunInCommandModeAsync(async () => reply = await CaptureParsedAsync(command, ParseSetterReply));
        if (!ran) return;

        if (reply is null)
        {
            CenteredMessage.Show(this, Loc.T("msg.commError"), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else if (reply.Ok)
        {
            string message = Loc.DeviceReply(reply.Message);
            string text = setter.Temporary ? $"{message}\r\n\r\n{Loc.T("dlg.notSaved")}" : message;
            CenteredMessage.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            CenteredMessage.Show(this, Loc.DeviceReply(reply.Message), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

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
        return trimmed.Length > 0 ? Loc.DeviceReply(trimmed) : null;
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
        // Without a cap, a stream sending packs faster than idleMs would keep the capture open forever.
        _captureDeadlineUtc = DateTime.UtcNow.AddMilliseconds(idleMs + CaptureMaxExtraMs);
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
        _mainSplit.Panel2.Controls.Add(terminalPanel);

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
        _atStrip.Visible = show;
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
        ("grp.thresholds", Color.FromArgb(255, 235, 217), new[]
        {
            ("WindSpeedSetpoint", "tile.windSpeedSetpoint", ""),
            ("AccelThreshold", "tile.accelThreshold", "g"),
            ("DeviceSleepInterval", "tile.sleepInterval", "@unit.min"),
        }),
    };

    private (string TitleKey, Color Tint, (string Key, string NameKey, string Unit)[] Fields)[] CalibrationGroups() => new[]
    {
        ("grp.heater", Color.FromArgb(250, 222, 222), new[]
        {
            ("NoWindPwm", "tile.noWindPwm", ""),
            ("HeaterSetpoint", "tile.heaterSetpoint", ""),
        }),
        ("grp.pid", Color.FromArgb(255, 243, 205), new[]
        {
            ("PidKp", "tile.kp", ""),
            ("PidKi", "tile.ki", ""),
            ("PidKd", "tile.kd", ""),
        }),
        ("grp.live", Color.FromArgb(228, 233, 240), new[]
        {
            ("CalAmbientTemp", "tile.ambientTemp", "°C"),
            ("CalWindSpeed", "tile.windSpeed", "m/s"),
            ("HeaterPwmLive", "tile.heaterPwmLive", ""),
            ("HeaterTemp", "tile.heaterTemp", "°C"),
        }),
        ("grp.zeroSpeedPwm", Color.FromArgb(214, 245, 240), new[]
        {
            ("WindZeroSpeedPwm1", "tile.zeroSpeedPwm1", ""),
            ("WindZeroSpeedPwm2", "tile.zeroSpeedPwm2", ""),
            ("WindZeroSpeedPwm3", "tile.zeroSpeedPwm3", ""),
            ("WindRoomTempPwm1", "tile.roomTempPwm1", ""),
            ("WindRoomTempPwm2", "tile.roomTempPwm2", ""),
        }),
        ("grp.calibSpeed", Color.FromArgb(214, 234, 248), new[]
        {
            ("CalibSpeed1", "tile.calibSpeed1", "m/s"),
            ("CalibSpeed2", "tile.calibSpeed2", "m/s"),
            ("CalibSpeed3", "tile.calibSpeed3", "m/s"),
        }),
    };

    private Control BuildDashboardHost()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = WindowGray };
        _calibrationPage = BuildPage(CalibrationGroups());
        _calibrationPage.Dock = DockStyle.Fill;
        _calibrationPage.Visible = false;
        _calibrationPage.Controls.Add(BuildCalibrationToolbar());
        _lblCalibBanner.Dock = DockStyle.Top;
        _lblCalibBanner.AutoSize = false;
        _lblCalibBanner.Height = 34;
        _lblCalibBanner.TextAlign = ContentAlignment.MiddleLeft;
        _lblCalibBanner.Padding = new Padding(12, 0, 8, 0);
        _lblCalibBanner.BackColor = Color.FromArgb(255, 236, 179);
        _lblCalibBanner.ForeColor = Color.FromArgb(102, 60, 0);
        _lblCalibBanner.Visible = false;
        Bind(() => _lblCalibBanner.Text = Loc.T("calib.unsupportedBanner"));
        _calibrationPage.Controls.Add(_lblCalibBanner);
        _basicPage = BuildPage(BasicGroups());
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

    private Control BuildCalibrationToolbar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = WindowGray,
            Padding = new Padding(12, 8, 8, 0),
        };

        _btnSetZeroWind.AutoSize = true;
        _btnSetZeroWind.Font = AppFonts.ToolbarRowText;
        _btnSetZeroWind.Padding = new Padding(8, 2, 8, 2);
        Bind(() => _btnSetZeroWind.Text = Loc.T("calib.setZeroWind"));
        _btnSetZeroWind.Click += (_, _) => RunCalibrationSetter("NoWindPwm", Loc.T("calib.setZeroWind"), "--");
        bar.Controls.Add(_btnSetZeroWind);

        _btnCalibAdvanced.AutoSize = true;
        _btnCalibAdvanced.Font = AppFonts.ToolbarRowText;
        _btnCalibAdvanced.Padding = new Padding(8, 2, 8, 2);
        _btnCalibAdvanced.Margin = new Padding(12, 3, 3, 3);
        Bind(() => _btnCalibAdvanced.Text = Loc.T(_calibAdvancedVisible ? "calib.advancedHide" : "calib.advancedShow"));
        _btnCalibAdvanced.Click += (_, _) => SetCalibrationAdvancedVisible(!_calibAdvancedVisible);
        bar.Controls.Add(_btnCalibAdvanced);

        return bar;
    }

    private void SetCalibrationAdvancedVisible(bool visible)
    {
        _calibAdvancedVisible = visible;
        foreach (var key in AdvancedCalibrationGroups) _cards[key].Visible = visible;
        RefreshTexts();
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

    private Panel BuildPage((string TitleKey, Color Tint, (string Key, string NameKey, string Unit)[] Fields)[] groups)
    {
        var page = new Panel { Dock = DockStyle.Fill };
        var grid = BuildGroupGrid(groups, columns: 1);
        grid.Dock = DockStyle.Fill;
        page.Controls.Add(grid);
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
            Font = AppFonts.DashboardCardHeader,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 4, 0),
        };
        Bind(() => header.Text = Loc.T(titleKey));

        bool hasIndicator = titleKey is "grp.globalStatus" or "grp.sensorLinks";
        int tileWidth = titleKey == "grp.sensorLinks" ? 230 : 250;

        var inner = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, MaximumSize = new Size(540, 3000), Padding = new Padding(4) };
        foreach (var (key, nameKey, unit) in fields)
        {
            bool isUnitKey = unit.StartsWith('@');
            string unitKey = isUnitKey ? unit[1..] : unit;
            var tile = new InfoTile(Loc.T(nameKey), isUnitKey ? Loc.T(unitKey) : unit, tint, tileWidth);
            if (hasIndicator) tile.EnableIndicator();
            Bind(() => tile.SetName(Loc.T(nameKey)));
            if (isUnitKey) Bind(() => tile.SetUnit(Loc.T(unitKey)));
            if (CalibrationSetters.ContainsKey(key))
            {
                tile.MakeClickable();
                tile.TileClicked += (_, _) => OnCalibrationTileClicked(key, tile, Loc.T(nameKey));
            }
            _tiles[key] = tile;
            inner.Controls.Add(tile);
        }

        card.Controls.Add(inner);
        card.Controls.Add(header);
        if (AdvancedCalibrationGroups.Contains(titleKey)) card.Visible = _calibAdvancedVisible;
        _cards[titleKey] = card;
        return card;
    }

    private ToolStrip BuildAtStrip()
    {
        _atStrip.Dock = DockStyle.Fill;
        _atStrip.GripStyle = ToolStripGripStyle.Hidden;
        _atStrip.Renderer = new ButtonFrameRenderer();
        _atStrip.Visible = false;

        var lblTitle = new ToolStripLabel { Text = "AT Commands:", Font = AppFonts.AtCommandsRowTitle };
        _atStrip.Items.Add(lblTitle);
        _atStrip.Items.Add(new ToolStripSeparator());

        _atStrip.Items.Add(CommandStripButton("Enter Command Mode (ATO)", EnterCommandModeManualAsync));
        _atStrip.Items.Add(CommandStripButton("Exit to Data Mode (+++)", ExitToDataModeManualAsync));

        Bind(() => _btnTestToggle.Text = _lastTestActive ? "Stop Test (AT+T)" : "Start Test (AT+T)");
        _btnTestToggle.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _btnTestToggle.Click += (_, _) => SendRaw("AT+T");
        _atStrip.Items.Add(_btnTestToggle);

        _atStrip.Items.Add(CommandStripButton("List Sensors (AT+C?)", () => RunCommandSilent("AT+C?")));
        _atStrip.Items.Add(CommandStripButton("Status (AT+S?)", () => RunCommandSilent("AT+S?")));
        _atStrip.Items.Add(CommandStripButton("Help (AT?)", () => RunCommandSilent("AT?")));

        return _atStrip;
    }

    private ToolStripButton CommandStripButton(string text, Action onClick)
    {
        var btn = new ToolStripButton { Text = text, DisplayStyle = ToolStripItemDisplayStyle.Text };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private ToolStripButton CommandStripButton(string text, Func<Task> onClick)
    {
        var btn = new ToolStripButton { Text = text, DisplayStyle = ToolStripItemDisplayStyle.Text };
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
        _terminal.Font = AppFonts.TerminalLog;
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
        if (_port is { IsOpen: false })
        {
            HandlePortLost(new IOException("Serial port closed unexpectedly"));
            return;
        }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HandlePortLost(ex);
        }
        catch (Exception ex)
        {
            AppendTerminal($"(send failed: {ex.Message})\r\n", Color.IndianRed);
        }
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is { IsOpen: false })
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(() => HandlePortLost(new IOException("Serial port closed unexpectedly")));
            }
            return;
        }
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
                        if (DateTime.UtcNow < _captureDeadlineUtc)
                        {
                            _captureTimer.Stop();
                            _captureTimer.Start();
                        }
                    }
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(() => HandlePortLost(ex));
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

    private void NoteFrameArrived()
    {
        _lastFrameUtc = DateTime.UtcNow;
        if (_linkStale)
        {
            _linkStale = false;
            AppendTerminal("--- signal restored ---\r\n", Color.Gray);
        }
        _frameCount++;
        _statusFrames.Text = $"{Loc.T("status.frames")}: {_frameCount}";
    }

    private void OnTestFrame(TestFrame f)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            NoteFrameArrived();
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
            _tiles["WindPwmOutput"].SetValue(f.WindPwmOutput.ToString(ci));
            _tiles["DeviceSleepInterval"].SetValue(f.DeviceSleepInterval.ToString(ci));
            _tiles["Version"].SetValue("v" + (f.Version / 100.0).ToString("0.00", ci));
            UpdateGlobalStatusTiles(f);
            UpdateSensorLinkTiles(f);

            // Calibration page fields the test pack also carries (the only source on older firmware).
            _tiles["CalAmbientTemp"].SetValue(FormatFloat(f.Sht20Temperature, 1000.0));
            _tiles["CalWindSpeed"].SetValue(FormatFloat(f.WindSpeed, 1000.0));
            _tiles["HeaterPwmLive"].SetValue(f.WindPwmOutput.ToString(ci));
            _tiles["WindZeroSpeedPwm1"].SetValue(f.WindZeroSpeedPwm1.ToString(ci));
            _tiles["WindZeroSpeedPwm2"].SetValue(f.WindZeroSpeedPwm2.ToString(ci));
            _tiles["WindZeroSpeedPwm3"].SetValue(f.WindZeroSpeedPwm3.ToString(ci));
            _tiles["WindRoomTempPwm1"].SetValue(f.WindRoomTempPwm1.ToString(ci));
            _tiles["WindRoomTempPwm2"].SetValue(f.WindRoomTempPwm3.ToString(ci));
        });
    }

    private void OnCalibrationFrame(CalibrationFrame f)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            NoteFrameArrived();
            if (!_lastCalibActive || _lastTestActive)
            {
                _lastCalibActive = true;
                _lastTestActive = false;
                RefreshTexts();
            }

            var ci = CultureInfo.InvariantCulture;
            _tiles["CalAmbientTemp"].SetValue(FormatFloat(f.Sht20Temperature, 1000.0));
            _tiles["CalWindSpeed"].SetValue(FormatFloat(f.WindSpeed, 1000.0));
            _tiles["HeaterPwmLive"].SetValue(f.HeaterPwmOutput.ToString(ci));
            _tiles["HeaterTemp"].SetValue(FormatFloat(f.HeaterTemperature));
            _tiles["WindZeroSpeedPwm1"].SetValue(f.WindZeroSpeedPwm1.ToString(ci));
            _tiles["WindZeroSpeedPwm2"].SetValue(f.WindZeroSpeedPwm2.ToString(ci));
            _tiles["WindZeroSpeedPwm3"].SetValue(f.WindZeroSpeedPwm3.ToString(ci));
            _tiles["WindRoomTempPwm1"].SetValue(f.WindRoomTempPwm1.ToString(ci));
            _tiles["WindRoomTempPwm2"].SetValue(f.WindRoomTempPwm3.ToString(ci));
            _tiles["CalibSpeed1"].SetValue(FormatFloat(f.CalibrationSpeed1));
            _tiles["CalibSpeed2"].SetValue(FormatFloat(f.CalibrationSpeed2));
            _tiles["CalibSpeed3"].SetValue(FormatFloat(f.CalibrationSpeed3));
            _tiles["NoWindPwm"].SetValue(f.NoWindPwm.ToString(ci));
            _tiles["HeaterSetpoint"].SetValue(FormatFloat(f.HeaterSetpoint));
            _tiles["PidKp"].SetValue(FormatFloat(f.Kp));
            _tiles["PidKi"].SetValue(FormatFloat(f.Ki));
            _tiles["PidKd"].SetValue(FormatFloat(f.Kd));
        });
    }

    private void OnModeChanged(LinkMode mode)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            _lastMode = mode;
            if (mode == LinkMode.Data) MarkCalibrationOff();
            RefreshTexts();
        });
    }

    private void OnTestModeChanged(bool active)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            _lastTestActive = active;
            if (active)
            {
                _lastFrameUtc = DateTime.UtcNow;
                MarkCalibrationOff();
            }
            RefreshTexts();
        });
    }

    private void OnCalibrationModeChanged(bool active)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            if (active)
            {
                _lastCalibActive = true;
                _lastTestActive = false;
                _lastFrameUtc = DateTime.UtcNow;
            }
            else
            {
                MarkCalibrationOff();
            }
            RefreshTexts();
        });
    }

}
