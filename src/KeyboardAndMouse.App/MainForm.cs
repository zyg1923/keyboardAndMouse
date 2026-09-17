using System.Net;

namespace KeyboardAndMouse.App;

internal sealed class MainForm : Form
{
    private readonly RadioButton _radioSend;
    private readonly RadioButton _radioRecv;
    private readonly TextBox _txtIp;
    private readonly TextBox _txtPort;
    private readonly Label _lblIpTitle;
    private readonly Label _lblLocalIps;
    private readonly Label _lblPortHint;
    private readonly RadioButton _radioChBoth;
    private readonly RadioButton _radioChKbd;
    private readonly RadioButton _radioChMouse;
    private readonly Button _btnStart;
    private readonly Button _btnStop;
    private readonly Button _btnIntercept;
    private readonly Button _btnRestartIntercept;
    private readonly CheckBox _chkKeepLocal;
    private readonly Label _lblKeyboard;
    private readonly ComboBox _cmbKeyboard;
    private readonly Button _btnIdentify;
    private readonly Label _lblIdentify;
    private readonly TableLayoutPanel _kbdRow;
    private readonly Label _lblSendRate;
    private readonly ComboBox _cmbSendRate;
    private readonly Label _lblStatus;
    private readonly Panel _statusDot;
    private readonly TextBox _txtLog;
    private readonly Label _lblHint;
    private readonly Label _lblSubtitle;
    private readonly Label _lblNet;
    private readonly TableLayoutPanel _netRow;
    private readonly NotifyIcon _tray;
    private readonly Panel _scroll;
    private readonly TableLayoutPanel _body;

    private MapperClient? _client;
    private MapperServer? _server;
    private InputHook? _hook;
    private InputInjector? _injector;
    private readonly AppSettings _settings;
    private readonly SessionLog _sessionLog;
    private bool _running;
    private bool _checkingPort;
    private bool _fullScreen;
    private FormBorderStyle _windowedBorder = FormBorderStyle.Sizable;
    private FormWindowState _windowedState = FormWindowState.Normal;
    private bool _relayouting;
    private bool _exitRequested;
    private bool _identifying;
    private int _identifySendIntervalMs;
    private bool _remoteCapturing;
    private bool _restartPending;
    private KeyboardIdentify? _identify;
    private readonly System.Windows.Forms.Timer _identifyTimer;
    private readonly WheelBlockFilter _wheelBlockFilter = new();

    public MainForm()
    {
        _settings = AppSettings.Load();
        _sessionLog = new SessionLog();
        Text = "键鼠映射";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        MinimumSize = new Size(520, 420);
        Size = new Size(700, 860);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        BackColor = Color.FromArgb(244, 246, 249);
        ForeColor = Color.FromArgb(30, 41, 59);
        KeyPreview = true;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        _lblSubtitle = new Label
        {
            Text = "同一程序里选择发送或接收",
            ForeColor = Color.FromArgb(226, 232, 240),
            AutoSize = true,
            Location = new Point(20, 16)
        };
        _statusDot = new Panel
        {
            Size = new Size(12, 12),
            Location = new Point(580, 20),
            BackColor = Color.FromArgb(100, 116, 139),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _lblStatus = new Label
        {
            Text = "未开始",
            ForeColor = Color.FromArgb(226, 232, 240),
            AutoSize = true,
            Location = new Point(480, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        header.Controls.AddRange(new Control[] { _lblSubtitle, _lblStatus, _statusDot });

        _scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0)
        };
        _body = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(16, 12, 16, 12),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            MinimumSize = new Size(640, 0)
        };
        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblMode = SectionLabel("运行模式");
        _radioSend = MakeModeRadio("发送端  ·  控制对方", "拦截本机键鼠，发送到另一台电脑", true);
        _radioRecv = MakeModeRadio("接收端  ·  被对方控制", "监听连接，把事件注入到本机", false);
        var modeRow = TwoColumn(_radioSend, _radioRecv);

        var lblNet = SectionLabel("网络");
        _lblNet = lblNet;
        var netRow = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 8)
        };
        _netRow = netRow;
        netRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        netRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        netRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        netRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        netRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var lblPort = FieldCaption($"端口（UDP，有效范围 {PortProbe.Min}–{PortProbe.Max}）");
        _txtPort = new TextBox
        {
            Text = Protocol.DefaultPort.ToString(),
            Font = new Font("Consolas", 10f),
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0)
        };
        _lblPortHint = new Label
        {
            AutoSize = true,
            AutoEllipsis = false,
            Visible = false,
            ForeColor = Color.FromArgb(185, 28, 28),
            Margin = new Padding(0, 4, 0, 0)
        };
        _lblIpTitle = FieldCaption("对方 IP");
        _txtIp = new TextBox
        {
            Text = "127.0.0.1",
            Font = new Font("Consolas", 10f),
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 0, 0, 0)
        };
        _lblLocalIps = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Visible = false,
            ForeColor = Color.FromArgb(71, 85, 105),
            Margin = new Padding(10, 4, 0, 0)
        };
        netRow.Controls.Add(lblPort, 0, 0);
        netRow.Controls.Add(_lblIpTitle, 1, 0);
        netRow.Controls.Add(_txtPort, 0, 1);
        netRow.Controls.Add(_txtIp, 1, 1);
        netRow.Controls.Add(_lblPortHint, 0, 2);
        netRow.Controls.Add(_lblLocalIps, 1, 2);

        var lblCh = SectionLabel("控制范围（双方取交集）");
        _radioChBoth = MakeChannelRadio("键盘和鼠标", true);
        _radioChKbd = MakeChannelRadio("仅键盘", false);
        _radioChMouse = MakeChannelRadio("仅鼠标", false);
        var chRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            WrapContents = true,
            Margin = new Padding(0, 4, 0, 16)
        };
        _radioChBoth.Margin = new Padding(0, 0, 10, 8);
        _radioChKbd.Margin = new Padding(0, 0, 10, 8);
        _radioChMouse.Margin = new Padding(0, 0, 0, 8);
        chRow.Controls.Add(_radioChBoth);
        chRow.Controls.Add(_radioChKbd);
        chRow.Controls.Add(_radioChMouse);

        _lblKeyboard = FieldCaption("拦截的键盘");
        _cmbKeyboard = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnIdentify = SecondaryButton("按键识别");
        _btnIdentify.Margin = new Padding(0);
        _btnIdentify.Size = new Size(116, 32);
        _kbdRow = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 4)
        };
        _kbdRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _kbdRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _kbdRow.Controls.Add(_cmbKeyboard, 0, 0);
        _kbdRow.Controls.Add(_btnIdentify, 1, 0);
        _lblIdentify = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Margin = new Padding(0, 0, 0, 8),
            Text = "必须先选定一块物理键盘。点「按键识别」后在目标键盘上按一下。拦截后该键盘除 Ctrl+Alt+Q / Ctrl+Alt+X 外全部吃掉；其它键盘仍本机可用。"
        };
        _identifyTimer = new System.Windows.Forms.Timer { Interval = 8000 };
        _identifyTimer.Tick += (_, _) => StopIdentify("没有收到按键，请再点一次「按键识别」。");

        _lblSendRate = FieldCaption("UDP 发送间隔");
        _cmbSendRate = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12)
        };
        _cmbSendRate.Items.AddRange(new object[]
        {
            new IntervalChoice(0, "最快（约 500 次/秒鼠标）"),
            new IntervalChoice(1, "1 ms（推荐）"),
            new IntervalChoice(2, "2 ms"),
            new IntervalChoice(4, "4 ms"),
            new IntervalChoice(8, "8 ms（更省电）")
        });

        _btnStart = PrimaryButton("开始");
        _btnStop = SecondaryButton("停止");
        _btnStop.Enabled = false;
        _btnIntercept = AccentButton("开启拦截");
        _btnIntercept.Enabled = false;
        _btnIntercept.AutoSize = false;
        _btnIntercept.Size = new Size(180, 40);
        _btnIntercept.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        _btnRestartIntercept = SecondaryButton("重启拦截");
        _btnRestartIntercept.Enabled = false;
        _btnRestartIntercept.AutoSize = false;
        _btnRestartIntercept.Size = new Size(140, 40);
        _btnRestartIntercept.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        var btnRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        btnRow.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnIntercept, _btnRestartIntercept });

        _chkKeepLocal = new CheckBox
        {
            Text = "鼠标此程序保留（键盘仍发给对方；鼠标留在本窗口，方便点关闭拦截）",
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            Margin = new Padding(0, 4, 0, 10),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        _lblHint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Margin = new Padding(0, 0, 0, 8),
            Text = "选定键盘后开启拦截：该键盘（含 Win）只发给对方、本机不回放；仅 Ctrl+Alt+Q / Ctrl+Alt+X 例外。其它键盘仍本机可用。Win+L、Ctrl+Alt+Del 系统组合可能仍抢走。关闭拦截/停止会恢复本机键盘。F11 全屏。"
        };

        var lblLog = SectionLabel("日志");
        _txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            TabStop = false,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Dock = DockStyle.Top,
            Height = 220,
            MinimumSize = new Size(0, 180)
        };

        void AddRow(Control control, bool fill = false)
        {
            var row = _body.RowCount;
            _body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _body.Controls.Add(control, 0, row);
            _body.RowCount++;
        }

        _body.RowCount = 0;
        _body.RowStyles.Clear();
        AddRow(lblMode);
        AddRow(modeRow);
        AddRow(lblNet);
        AddRow(netRow);
        AddRow(lblCh);
        AddRow(chRow);
        AddRow(_lblKeyboard);
        AddRow(_kbdRow);
        AddRow(_lblIdentify);
        AddRow(_lblSendRate);
        AddRow(_cmbSendRate);
        AddRow(btnRow);
        AddRow(_chkKeepLocal);
        AddRow(_lblHint);
        AddRow(lblLog);
        AddRow(_txtLog);

        _scroll.Controls.Add(_body);
        Controls.Add(_scroll);
        Controls.Add(header);

        ApplyLoadedSettings();

        _radioSend.CheckedChanged += (_, _) =>
        {
            ApplyModeUi();
            SaveSettings();
        };
        _radioRecv.CheckedChanged += (_, _) =>
        {
            ApplyModeUi();
            SaveSettings();
        };
        _radioChBoth.CheckedChanged += (_, _) => OnCapsChanged();
        _radioChKbd.CheckedChanged += (_, _) => OnCapsChanged();
        _radioChMouse.CheckedChanged += (_, _) => OnCapsChanged();
        _txtIp.Leave += (_, _) => SaveSettings();
        _txtPort.Leave += (_, _) => OnPortEdited();
        _txtPort.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            OnPortEdited();
        };
        _btnStart.Click += (_, _) => StartSession();
        _btnStop.Click += (_, _) => StopSession("已停止");
        _btnIntercept.Click += (_, _) => ToggleIntercept();
        _btnRestartIntercept.Click += (_, _) => RestartIntercept();
        _chkKeepLocal.CheckedChanged += (_, _) =>
        {
            ApplyKeepLocalUi();
            SaveSettings();
        };
        _cmbKeyboard.SelectionChangeCommitted += (_, _) =>
        {
            StopIdentify(null);
            _hook?.SetKeyboardDevice(SelectedKeyboardId());
            SaveSettings();
        };
        _btnIdentify.Click += (_, _) => BeginIdentify();
        _cmbSendRate.SelectionChangeCommitted += (_, _) =>
        {
            ApplySendInterval();
            SaveSettings();
        };
        Move += (_, _) => _hook?.RefreshLocalClip();
        Resize += (_, _) =>
        {
            AlignStatusLabel();
            RelayoutContent();
            _hook?.RefreshLocalClip();
        };
        KeyDown += (_, e) =>
        {
            // 识别过程中按键不能进 ComboBox，否则会误改「UDP 发送间隔」等下拉项。
            if (_identifying)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
            }
        };

        _tray = CreateTrayIcon();
        FormClosing += (_, e) =>
        {
            _checkingPort = true;
            if (!_exitRequested)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            SaveSettings();
            StopSession(null);
            StopIdentify(null);
            SetScrollLocked(false);
            _identifyTimer.Dispose();
            _sessionLog.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
        };

        Shown += (_, _) =>
        {
            RelayoutContent();
            if (_radioSend.Checked && _chkKeepLocal.Checked)
                SetFullScreen(true);
        };
        ApplyModeUi();
        AlignStatusLabel();
        RelayoutContent();
        Log($"日志文件：{Environment.NewLine}{_sessionLog.Path}");
    }

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
        ForeColor = Color.FromArgb(100, 116, 139),
        AutoSize = true,
        Margin = new Padding(0, 8, 0, 6)
    };

    private static Label FieldCaption(string text) => new()
    {
        Text = text,
        Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 4)
    };

    private static TableLayoutPanel TwoColumn(Control left, Control right)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        left.Dock = DockStyle.Fill;
        right.Dock = DockStyle.Fill;
        left.MinimumSize = new Size(240, 88);
        right.MinimumSize = new Size(240, 88);
        left.Margin = new Padding(0, 0, 6, 0);
        right.Margin = new Padding(6, 0, 0, 0);
        row.Controls.Add(left, 0, 0);
        row.Controls.Add(right, 1, 0);
        return row;
    }

    private RadioButton MakeModeRadio(string title, string desc, bool selected)
    {
        var radio = new RadioButton
        {
            Appearance = Appearance.Button,
            Text = title + Environment.NewLine + desc,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 8, 12, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Checked = selected
        };
        radio.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        radio.FlatAppearance.CheckedBackColor = Color.FromArgb(219, 234, 254);
        radio.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        return radio;
    }

    private RadioButton MakeChannelRadio(string text, bool selected)
    {
        var radio = new RadioButton
        {
            Appearance = Appearance.Button,
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            MinimumSize = new Size(168, 52),
            Padding = new Padding(18, 12, 18, 12),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Checked = selected,
            Margin = new Padding(0, 0, 10, 0)
        };
        radio.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        radio.FlatAppearance.CheckedBackColor = Color.FromArgb(219, 234, 254);
        radio.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        return radio;
    }

    private Button PrimaryButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(116, 36),
            Margin = new Padding(0, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private static Button SecondaryButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(116, 36),
            Margin = new Padding(0, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        return btn;
    }

    private static Button AccentButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(168, 36),
            Margin = new Padding(0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(245, 158, 11),
            ForeColor = Color.FromArgb(30, 41, 59),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void ApplyModeUi()
    {
        var send = _radioSend.Checked;
        _lblIpTitle.Text = send ? "对方 IP" : "本机 IP";
        _txtIp.Visible = send;
        _lblLocalIps.Visible = !send;
        if (!send)
            RefreshLocalIps();
        _btnIntercept.Visible = true;
        _btnRestartIntercept.Visible = true;
        _chkKeepLocal.Visible = send;
        _lblKeyboard.Visible = send;
        _kbdRow.Visible = send;
        _lblIdentify.Visible = send;
        _lblSendRate.Visible = send;
        _cmbSendRate.Visible = send;
        _lblHint.Visible = true;
        _lblHint.Text = send
            ? "选定键盘后开启拦截：该键盘（含 Win）只发给对方、本机不回放；仅 Ctrl+Alt+Q / Ctrl+Alt+X 例外。其它键盘仍本机可用。Win+L、Ctrl+Alt+Del 系统组合可能仍抢走。关闭拦截/停止会恢复本机键盘。「重启拦截」可快速关再开。F11 全屏。"
            : "接收端可点「关闭拦截 / 开启拦截 / 重启拦截」远程控制发送端的拦截状态（需双方都是新版本）。键盘会打到当前前台窗口，请不要把焦点留在本程序上。F11 全屏。";
        _btnStart.Text = send ? "连接对方" : "开始监听";
        if (!send)
            _remoteCapturing = false;
        SyncInterceptButton();
        ApplyKeepLocalUi();
    }

    private void ApplyKeepLocalUi()
    {
        _hook?.SetKeepLocalUi(_radioSend.Checked && _chkKeepLocal.Checked, Handle);
        if (_radioSend.Checked && _chkKeepLocal.Checked)
        {
            SetFullScreen(true);
            if (_hook?.Capturing == true)
                TopMost = true;
        }
        else
        {
            SetFullScreen(false);
            if (!(_hook?.Capturing == true))
                TopMost = false;
        }
    }

    private ChannelFlags GetSelectedCaps()
    {
        if (_radioChKbd.Checked)
            return ChannelFlags.Keyboard;
        if (_radioChMouse.Checked)
            return ChannelFlags.Mouse;
        return ChannelFlags.Both;
    }

    private void OnCapsChanged()
    {
        var caps = GetSelectedCaps();
        _client?.SetLocalCaps(caps);
        _server?.SetLocalCaps(caps);
        var effective = _client?.EffectiveCaps ?? _server?.EffectiveCaps ?? caps;
        _hook?.ApplyChannelFlags(effective);
        SaveSettings();
    }

    private void ApplyLoadedSettings()
    {
        _radioSend.Checked = _settings.IsSender;
        _radioRecv.Checked = !_settings.IsSender;
        _txtIp.Text = string.IsNullOrWhiteSpace(_settings.Ip) ? "127.0.0.1" : _settings.Ip;
        if (PortProbe.IsInRange(_settings.Port))
            _txtPort.Text = _settings.Port.ToString();
        switch (_settings.Channel)
        {
            case "keyboard":
                _radioChKbd.Checked = true;
                break;
            case "mouse":
                _radioChMouse.Checked = true;
                break;
            default:
                _radioChBoth.Checked = true;
                break;
        }
        _chkKeepLocal.Checked = _settings.KeepLocalUi;
        SelectSendInterval(_settings.SendIntervalMs);
        FillKeyboardDevices(_settings.KeyboardDevice);
    }

    private void SaveSettings()
    {
        _settings.IsSender = _radioSend.Checked;
        _settings.Ip = _txtIp.Text.Trim();
        if (PortProbe.TryParseValid(_txtPort.Text, out var port))
            _settings.Port = port;
        _settings.Channel = GetSelectedCaps() switch
        {
            ChannelFlags.Keyboard => "keyboard",
            ChannelFlags.Mouse => "mouse",
            _ => "both"
        };
        _settings.KeepLocalUi = _chkKeepLocal.Checked;
        _settings.KeyboardDevice = SelectedKeyboardId();
        _settings.SendIntervalMs = SelectedSendIntervalMs();
        _settings.Save();
    }

    private int SelectedSendIntervalMs() =>
        _cmbSendRate.SelectedItem is IntervalChoice choice ? choice.Ms : 0;

    private void SelectSendInterval(int ms)
    {
        ms = Math.Clamp(ms, 0, 8);
        for (var i = 0; i < _cmbSendRate.Items.Count; i++)
        {
            if (_cmbSendRate.Items[i] is IntervalChoice choice && choice.Ms == ms)
            {
                _cmbSendRate.SelectedIndex = i;
                return;
            }
        }

        _cmbSendRate.SelectedIndex = 0;
    }

    private void ApplySendInterval()
    {
        _client?.SetSendIntervalMs(SelectedSendIntervalMs());
    }

    private string SelectedKeyboardId() =>
        _cmbKeyboard.SelectedItem is KeyboardChoice choice ? choice.Id : "";

    private const string IdentifyIdleText =
        "必须先选定一块物理键盘。点「按键识别」后在目标键盘上按一下。拦截后该键盘除 Ctrl+Alt+Q / Ctrl+Alt+X 外全部吃掉；其它键盘仍本机可用。";

    private void FillKeyboardDevices(string? wanted)
    {
        if (wanted is null)
            wanted = _cmbKeyboard.Items.Count > 0 ? SelectedKeyboardId() : _settings.KeyboardDevice;

        _cmbKeyboard.BeginUpdate();
        try
        {
            IReadOnlyList<KeyboardDeviceInfo> devices;
            try
            {
                devices = KeyboardDevices.List();
            }
            catch
            {
                devices = Array.Empty<KeyboardDeviceInfo>();
            }

            _cmbKeyboard.Items.Clear();
            _cmbKeyboard.Items.Add(new KeyboardChoice("", "（请选择要拦截的键盘）"));
            foreach (var device in devices)
                _cmbKeyboard.Items.Add(new KeyboardChoice(device.Id, device.Label));

            var index = 0;
            var found = false;
            if (!string.IsNullOrEmpty(wanted))
            {
                for (var i = 0; i < _cmbKeyboard.Items.Count; i++)
                {
                    if (_cmbKeyboard.Items[i] is KeyboardChoice choice &&
                        string.Equals(choice.Id, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _cmbKeyboard.Items.Add(new KeyboardChoice(wanted, "上次的键盘（当前未连接）"));
                    index = _cmbKeyboard.Items.Count - 1;
                }
            }

            _cmbKeyboard.SelectedIndex = index;
        }
        finally
        {
            _cmbKeyboard.EndUpdate();
        }
    }

    private void BeginIdentify()
    {
        if (!_radioSend.Checked)
            return;

        StopIdentify(null);
        FillKeyboardDevices(null);
        _identifySendIntervalMs = SelectedSendIntervalMs();
        _identifying = true;
        _btnIdentify.Enabled = false;
        // 识别按键会被有焦点的下拉框吃掉（尤其是 UDP 间隔），先挪开焦点并禁用。
        _cmbSendRate.Enabled = false;
        _cmbKeyboard.Enabled = false;
        ActiveControl = _btnIdentify;
        _lblIdentify.ForeColor = Color.FromArgb(37, 99, 235);
        _lblIdentify.Text = "请在目标键盘上按任意键（8 秒内）…";

        try
        {
            if (_hook is not null)
            {
                _hook.KeyboardHeard += OnIdentifyHeard;
            }
            else
            {
                _identify = new KeyboardIdentify();
                _identify.Pressed += OnIdentifyHeard;
                _identify.Start();
            }
        }
        catch (Exception ex)
        {
            StopIdentify("无法开始识别：" + ex.Message);
            return;
        }

        _identifyTimer.Stop();
        _identifyTimer.Start();
    }

    private void OnIdentifyHeard(IntPtr device)
    {
        Ui(() =>
        {
            if (!_identifying)
                return;

            var info = KeyboardDevices.FindByHandle(device);
            if (info is null)
            {
                StopIdentify("收到按键，但没有匹配到列表里的设备，请再试一次。");
                return;
            }

            // 万一按键仍改过下拉框，保存前恢复识别开始时的发送间隔。
            SelectSendInterval(_identifySendIntervalMs);
            FillKeyboardDevices(info.Value.Id);
            _hook?.SetKeyboardDevice(info.Value.Id);
            SaveSettings();
            StopIdentify("已识别：" + info.Value.Label);
            Log("已选定键盘：" + info.Value.Label);
        });
    }

    private void StopIdentify(string? message)
    {
        _identifyTimer.Stop();
        if (_hook is not null)
            _hook.KeyboardHeard -= OnIdentifyHeard;
        if (_identify is not null)
        {
            _identify.Pressed -= OnIdentifyHeard;
            _identify.Dispose();
            _identify = null;
        }

        _identifying = false;
        var capturing = _hook?.Capturing == true;
        _btnIdentify.Enabled = _radioSend.Checked && !capturing;
        _cmbSendRate.Enabled = _radioSend.Checked;
        _cmbKeyboard.Enabled = _radioSend.Checked && !capturing;
        if (message is null)
        {
            _lblIdentify.ForeColor = Color.FromArgb(71, 85, 105);
            _lblIdentify.Text = IdentifyIdleText;
            return;
        }

        var ok = message.StartsWith("已识别", StringComparison.Ordinal);
        _lblIdentify.ForeColor = ok
            ? Color.FromArgb(22, 163, 74)
            : Color.FromArgb(185, 28, 28);
        _lblIdentify.Text = message;
    }

    private void RefreshLocalIps()
    {
        var ips = MapperServer.LocalIPv4Addresses().ToArray();
        var portText = PortProbe.TryParseValid(_txtPort.Text, out var port)
            ? port.ToString()
            : _txtPort.Text.Trim();
        _lblLocalIps.Text = ips.Length == 0
            ? "未找到 IPv4 地址"
            : "把这些地址和端口告诉发送端：" + Environment.NewLine +
              string.Join(Environment.NewLine, ips) + Environment.NewLine +
              "端口 " + portText;
    }

    private NotifyIcon CreateTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开面板", null, (_, _) => ShowPanel());
        menu.Items.Add("退出", null, (_, _) => ExitApp());

        Icon? icon = Icon;
        try
        {
            icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        }
        catch
        {
            // keep form icon
        }

        var tray = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Visible = true,
            Text = "键鼠映射",
            ContextMenuStrip = menu
        };
        tray.DoubleClick += (_, _) => ShowPanel();
        return tray;
    }

    private void HideToTray()
    {
        _hook?.NotifyUiVisible(false);
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowPanel()
    {
        ShowInTaskbar = true;
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Show();
        Activate();
        BringToFront();
        SingleInstance.SetForegroundWindow(Handle);
        SyncInterceptButton();
        _hook?.NotifyUiVisible(true);
    }

    private void ExitApp()
    {
        _exitRequested = true;
        Close();
    }

    private void OnPortEdited()
    {
        if (_checkingPort || _running)
            return;
        _checkingPort = true;
        try
        {
            EnsurePortReady(requireFree: true);
        }
        finally
        {
            _checkingPort = false;
        }

        if (_radioRecv.Checked)
            RefreshLocalIps();
    }

    private bool EnsurePortReady(bool requireFree)
    {
        var typed = _txtPort.Text.Trim();
        if (PortProbe.TryParseValid(typed, out var port) &&
            (!requireFree || PortProbe.IsUdpFree(port)))
        {
            ResetPortHint();
            SaveSettings();
            return true;
        }

        var invalid = !PortProbe.TryParseValid(typed, out port);
        var heading = invalid
            ? $"端口不合法，有效范围是 {PortProbe.Min}–{PortProbe.Max}"
            : $"端口 {port} 已被占用";
        var suggestions = PortProbe.SuggestFree(3, typed);
        var detail = suggestions.Length == 0
            ? "没有嗅探到可用端口。"
            : $"嗅探到 {suggestions.Length} 个当前可用的端口，选一个即可改用：";
        SetPortHint(
            suggestions.Length == 0
                ? heading
                : heading + "。可用：" + string.Join("  ", suggestions));

        var picked = OfferAlternativePorts(heading, detail, suggestions);
        if (picked is int chosen)
        {
            _txtPort.Text = chosen.ToString();
            ResetPortHint();
            SaveSettings();
            return true;
        }

        if (!invalid)
            SaveSettings();
        return false;
    }

    private int? OfferAlternativePorts(string heading, string text, int[] suggestions)
    {
        if (suggestions.Length == 0)
        {
            MessageBox.Show(this, heading + Environment.NewLine + text, "端口不可用",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var page = new TaskDialogPage
        {
            Caption = "端口不可用",
            Heading = heading,
            Text = text,
            Icon = TaskDialogIcon.Warning,
            SizeToContent = true
        };
        var map = new Dictionary<TaskDialogButton, int>();
        foreach (var p in suggestions)
        {
            var button = new TaskDialogCommandLinkButton($"使用端口 {p}", "本机当前空闲");
            map[button] = p;
            page.Buttons.Add(button);
        }

        page.Buttons.Add(TaskDialogButton.Cancel);
        var result = TaskDialog.ShowDialog(this, page);
        return map.TryGetValue(result, out var chosen) ? chosen : null;
    }

    private void ResetPortHint()
    {
        _lblPortHint.Visible = false;
        _lblPortHint.Text = "";
    }

    private void SetPortHint(string text)
    {
        _lblPortHint.Text = text;
        _lblPortHint.Visible = true;
        _lblPortHint.ForeColor = Color.FromArgb(185, 28, 28);
    }

    private void StartSession()
    {
        if (_running)
            return;

        _checkingPort = true;
        try
        {
            if (!EnsurePortReady(requireFree: !_radioSend.Checked))
                return;
        }
        finally
        {
            _checkingPort = false;
        }

        if (!PortProbe.TryParseValid(_txtPort.Text, out var port))
            return;

        SaveSettings();
        try
        {
            if (_radioSend.Checked)
            {
                var ip = _txtIp.Text.Trim();
                if (string.IsNullOrWhiteSpace(ip) ||
                    (!IPAddress.TryParse(ip, out _) && ip != "localhost"))
                {
                    MessageBox.Show(this, "请填写有效的对方 IP。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (GetSelectedCaps().HasFlag(ChannelFlags.Keyboard) &&
                    string.IsNullOrEmpty(SelectedKeyboardId()))
                {
                    MessageBox.Show(this, "请先用「按键识别」选定要拦截的键盘。", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                StartSender(ip, port);
            }
            else
            {
                StartReceiver(port);
            }
        }
        catch (Exception ex)
        {
            StopSession(null);
            MessageBox.Show(this, "启动失败：" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _running = true;
        SetRunningUi(true);
    }

    private void StartSender(string host, int port)
    {
        StopIdentify(null);
        var client = new MapperClient();
        var hook = new InputHook();
        _client = client;
        _hook = hook;
        client.SetLocalCaps(GetSelectedCaps());
        client.SetSendIntervalMs(SelectedSendIntervalMs());
        hook.ApplyChannelFlags(client.EffectiveCaps);
        hook.SetKeepLocalUi(_chkKeepLocal.Checked, Handle);
        hook.SetKeyboardDevice(SelectedKeyboardId());
        var keyboardId = SelectedKeyboardId();
        if (!string.IsNullOrEmpty(keyboardId) && KeyboardDevices.HandleForId(keyboardId) == IntPtr.Zero)
            Log("未找到所选键盘设备，请重新「按键识别」。");

        client.Connecting += (h, p) => Log($"正在连接 {h}:{p} ...");
        client.CapsChanged += () => Ui(() =>
        {
            hook.ApplyChannelFlags(client.EffectiveCaps);
        });
        client.Connected += () => Ui(() =>
        {
            Log("已连接。可点「开启拦截」，或按 Ctrl+Alt+Q。");
            SetStatus("已连接", Color.FromArgb(34, 197, 94));
            _btnIntercept.Enabled = true;
            _btnRestartIntercept.Enabled = true;
            client.SendCaptureState(hook.Capturing);
        });
        client.Disconnected += reason => Ui(() =>
        {
            SetIntercept(false, notifyRemote: false);
            Log($"连接断开（{reason}），本机输入已恢复。");
            SetStatus("重连中", Color.FromArgb(245, 158, 11));
            _btnIntercept.Enabled = false;
            _btnRestartIntercept.Enabled = false;
        });
        client.CaptureControlReceived += action => Ui(() => OnRemoteCaptureControl(action));

        hook.EventCaptured += client.Enqueue;
        hook.ToggleRequested += () => Ui(() => ToggleIntercept());
        hook.ExitRequested += () => Ui(() => StopSession("紧急停止，本机输入已恢复"));

        client.Start(host, port);
        hook.Install();
        SetStatus("连接中", Color.FromArgb(245, 158, 11));
        Log($"发送端已启动，目标 {host}:{port}");
    }

    private void StartReceiver(int port)
    {
        var previous = AppHost.GetForegroundWindow();
        _injector = new InputInjector();
        var server = new MapperServer();
        _server = server;
        _remoteCapturing = false;
        server.SetLocalCaps(GetSelectedCaps());
        server.ClientConnected += ip => Ui(() =>
        {
            Log($"发送端已连接：{ip}。可用「关闭拦截 / 重启拦截」远程控制对方。请先点击要输入的窗口。");
            SetStatus("已被控制", Color.FromArgb(34, 197, 94));
            _btnIntercept.Enabled = true;
            _btnRestartIntercept.Enabled = true;
            SyncInterceptButton(false);
            if (previous != IntPtr.Zero && previous != Handle)
                AppHost.SetForegroundWindow(previous);
        });
        server.ClientDisconnected += info => Ui(() =>
        {
            Log($"发送端断开：{info}，已松开按键。");
            SetStatus("等待连接", Color.FromArgb(59, 130, 246));
            _remoteCapturing = false;
            _btnIntercept.Enabled = false;
            _btnRestartIntercept.Enabled = false;
            SyncInterceptButton(false);
        });
        server.CaptureStateReceived += on => Ui(() =>
        {
            _remoteCapturing = on;
            SyncInterceptButton(on);
            if (on)
                SetStatus("对方拦截中", Color.FromArgb(239, 68, 68));
            else if (_server?.HasClient == true)
                SetStatus("已被控制", Color.FromArgb(34, 197, 94));
        });
        server.Received += ev =>
        {
            // 注入不依赖 UI 控件，直接在接收线程执行，避免忙等时 UI 队列堵死按键/点击。
            _injector?.Apply(ev);
        };
        server.Start(port);
        SetStatus("等待连接", Color.FromArgb(59, 130, 246));
        Log($"接收端正在监听 UDP {port}。可用按钮远程关闭/重启发送端拦截。");
        BeginInvoke(() =>
        {
            if (previous != IntPtr.Zero && previous != Handle)
                AppHost.SetForegroundWindow(previous);
        });
    }

    private void StopSession(string? message)
    {
        StopIdentify(null);
        if (_hook is not null)
            _hook.Capturing = false;

        try
        {
            _client?.SendReleaseAll();
            Thread.Sleep(40);
        }
        catch
        {
            // ignore flush errors while tearing down
        }

        _hook?.Dispose();
        _hook = null;
        _client?.Dispose();
        _client = null;
        _injector?.ReleaseAll();
        _injector = null;
        _server?.Dispose();
        _server = null;

        var wasRunning = _running;
        _running = false;
        _remoteCapturing = false;
        _restartPending = false;
        TopMost = false;
        SetRunningUi(false);
        SyncInterceptButton(false);
        SetStatus("未开始", Color.FromArgb(100, 116, 139));
        if (wasRunning && message is not null)
            Log(message);
    }

    private void ToggleIntercept()
    {
        if (_radioRecv.Checked)
        {
            RequestRemoteCapture(_remoteCapturing ? CaptureAction.Off : CaptureAction.On);
            return;
        }

        if (_hook is null || _client is null)
            return;

        if (!_client.IsConnected)
        {
            Log("尚未连接，无法开启拦截。");
            return;
        }

        if (!_hook.Capturing)
        {
            if (!EnsureKeyboardSelectedForCapture())
                return;
            _hook.SetKeyboardDevice(SelectedKeyboardId());
        }

        SetIntercept(!_hook.Capturing, notifyRemote: true);
    }

    private void RestartIntercept()
    {
        if (_radioRecv.Checked)
        {
            RequestRemoteCapture(CaptureAction.Restart);
            return;
        }

        if (_hook is null || _client is null)
            return;

        if (!_client.IsConnected)
        {
            Log("尚未连接，无法重启拦截。");
            return;
        }

        if (!EnsureKeyboardSelectedForCapture())
            return;

        _hook.SetKeyboardDevice(SelectedKeyboardId());
        if (_hook.Capturing)
        {
            _restartPending = true;
            SetIntercept(false, notifyRemote: true);
            _ = Task.Run(async () =>
            {
                await Task.Delay(80);
                Ui(() =>
                {
                    if (IsDisposed || _hook is null || _client?.IsConnected != true)
                    {
                        _restartPending = false;
                        return;
                    }

                    _hook.SetKeyboardDevice(SelectedKeyboardId());
                    SetIntercept(true, notifyRemote: true);
                    _restartPending = false;
                    Log("拦截已重启。");
                });
            });
        }
        else
        {
            SetIntercept(true, notifyRemote: true);
            Log("拦截已开启（重启）。");
        }
    }

    private void RequestRemoteCapture(CaptureAction action)
    {
        if (_server is null || !_server.HasClient)
        {
            Log("发送端未连接，无法远程控制拦截。");
            return;
        }

        _server.SendCaptureControl(action);
        Log(action switch
        {
            CaptureAction.Off => "已请求发送端关闭拦截。",
            CaptureAction.On => "已请求发送端开启拦截。",
            CaptureAction.Restart => "已请求发送端重启拦截。",
            _ => "已发送拦截控制请求。"
        });
    }

    private void OnRemoteCaptureControl(CaptureAction action)
    {
        if (_hook is null || _client is null || !_client.IsConnected)
            return;

        switch (action)
        {
            case CaptureAction.Off:
                if (_hook.Capturing)
                {
                    SetIntercept(false, notifyRemote: true);
                    Log("接收端请求：已关闭拦截。");
                }
                break;
            case CaptureAction.On:
                if (!_hook.Capturing)
                {
                    if (!EnsureKeyboardSelectedForCapture())
                    {
                        Log("接收端请求开启拦截，但本机尚未选定键盘。");
                        _client.SendCaptureState(false);
                        return;
                    }

                    _hook.SetKeyboardDevice(SelectedKeyboardId());
                    SetIntercept(true, notifyRemote: true);
                    Log("接收端请求：已开启拦截。");
                }
                break;
            case CaptureAction.Restart:
                Log("接收端请求：重启拦截。");
                RestartIntercept();
                break;
        }
    }

    private bool EnsureKeyboardSelectedForCapture()
    {
        if (!GetSelectedCaps().HasFlag(ChannelFlags.Keyboard))
            return true;
        if (!string.IsNullOrEmpty(SelectedKeyboardId()))
            return true;

        MessageBox.Show(this, "请先用「按键识别」选定要拦截的键盘。", Text,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void SetIntercept(bool on, bool notifyRemote)
    {
        if (_hook is null)
            return;

        var wasCapturing = _hook.Capturing;
        if (wasCapturing == on)
        {
            if (notifyRemote)
                _client?.SendCaptureState(on);
            return;
        }

        _hook.SetKeepLocalUi(_chkKeepLocal.Checked, Handle);
        _hook.Capturing = on;
        if (notifyRemote)
            _client?.SendReleaseAll();
        _client?.SendCaptureState(on);

        _btnIntercept.Enabled = _client?.IsConnected == true;
        _btnRestartIntercept.Enabled = _client?.IsConnected == true;
        SyncInterceptButton(on);
        _chkKeepLocal.Enabled = !on;
        _cmbKeyboard.Enabled = !on;
        if (!_identifying)
            _btnIdentify.Enabled = !on;

        if (on)
        {
            SetScrollLocked(true);
            if (_chkKeepLocal.Checked)
            {
                if (WindowState == FormWindowState.Minimized)
                    WindowState = FormWindowState.Normal;
                SetFullScreen(true);
                TopMost = true;
                Activate();
                _hook.SetKeepLocalUi(true, Handle);
                _hook.RefreshLocalClip();
                SetStatus("拦截中（本窗口可操作）", Color.FromArgb(239, 68, 68));
                if (!_restartPending)
                    Log("拦截已开启 —— 输入仍发给对方，鼠标留在本窗口。可直接点「关闭拦截」或「停止」。");
            }
            else
            {
                TopMost = false;
                SetStatus("拦截中", Color.FromArgb(239, 68, 68));
                if (!_restartPending)
                    Log("拦截已开启 —— 本机键鼠已阻断，输入发往接收端。Ctrl+Alt+Q 可关闭。");
            }
        }
        else
        {
            TopMost = false;
            SetScrollLocked(false);
            SetStatus(_client?.IsConnected == true ? "已连接" : "未开始",
                _client?.IsConnected == true ? Color.FromArgb(34, 197, 94) : Color.FromArgb(100, 116, 139));
            if (!_restartPending)
                Log("拦截已关闭 —— 本机键盘/鼠标监听已恢复。");
        }
    }

    private void SetScrollLocked(bool locked)
    {
        if (_wheelBlockFilter.Active == locked)
        {
            if (locked && _scroll.IsHandleCreated)
            {
                _scroll.AutoScrollPosition = Point.Empty;
                _scroll.AutoScroll = false;
            }
            return;
        }

        _wheelBlockFilter.Active = locked;
        if (locked)
            Application.AddMessageFilter(_wheelBlockFilter);
        else
            Application.RemoveMessageFilter(_wheelBlockFilter);

        if (!_scroll.IsHandleCreated)
            return;

        if (locked)
        {
            _scroll.AutoScrollPosition = Point.Empty;
            _scroll.AutoScroll = false;
        }
        else
        {
            _scroll.AutoScroll = true;
            _scroll.AutoScrollPosition = Point.Empty;
        }

        RelayoutContent();
    }

    private void ResetScrollLayout()
    {
        if (_scroll.IsHandleCreated)
        {
            _scroll.AutoScroll = true;
            _scroll.AutoScrollPosition = Point.Empty;
        }
        RelayoutContent();
    }

    private void SyncInterceptButton(bool? capturing = null)
    {
        var on = capturing ?? (_radioSend.Checked
            ? _hook?.Capturing == true
            : _remoteCapturing);
        _btnIntercept.Text = on ? "关闭拦截" : "开启拦截";
        _btnIntercept.BackColor = on
            ? Color.FromArgb(220, 38, 38)
            : Color.FromArgb(245, 158, 11);
        _btnIntercept.ForeColor = on
            ? Color.White
            : Color.FromArgb(30, 41, 59);
        _btnIntercept.Refresh();
        if (_lblSubtitle is not null)
        {
            _lblSubtitle.Text = on
                ? (_radioSend.Checked
                    ? "当前状态：拦截中（可点「关闭拦截」或 Ctrl+Alt+Q）"
                    : "当前状态：对方拦截中（可点「关闭拦截」远程关闭）")
                : "同一程序里选择发送或接收";
        }
        Text = on
            ? (_radioSend.Checked ? "键鼠映射 - 拦截中" : "键鼠映射 - 对方拦截中")
            : "键鼠映射";
        if (_tray is not null)
            _tray.Text = Text;
    }

    private void SetRunningUi(bool running)
    {
        _radioSend.Enabled = !running;
        _radioRecv.Enabled = !running;
        _txtIp.Enabled = !running;
        _txtPort.Enabled = !running;
        _btnStart.Enabled = !running;
        _btnStop.Enabled = running;
        _chkKeepLocal.Enabled = !(_hook?.Capturing == true);
        _cmbKeyboard.Enabled = !(_hook?.Capturing == true);
        if (!_identifying)
            _btnIdentify.Enabled = _radioSend.Checked;
        if (!running)
        {
            _btnIntercept.Enabled = false;
            _btnRestartIntercept.Enabled = false;
            SyncInterceptButton(false);
        }
        else if (_radioRecv.Checked)
        {
            var linked = _server?.HasClient == true;
            _btnIntercept.Enabled = linked;
            _btnRestartIntercept.Enabled = linked;
            SyncInterceptButton(_remoteCapturing);
        }
    }

    private void SetStatus(string text, Color color)
    {
        _lblStatus.Text = text;
        _statusDot.BackColor = color;
        AlignStatusLabel();
    }

    private void AlignStatusLabel()
    {
        _lblStatus.Location = new Point(_statusDot.Left - _lblStatus.PreferredWidth - 10, 27);
    }

    private void Log(string message)
    {
        _sessionLog.Append(message);
        Ui(() =>
        {
            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        });
    }

    private void Ui(Action action)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
            BeginInvoke(action);
        else
            action();
    }

    protected override void WndProc(ref Message m)
    {
        if ((uint)m.Msg == SingleInstance.ShowMessage)
        {
            ActivateExistingWindow();
            return;
        }

        const int wmMouseWheel = 0x020A;
        const int wmMouseHWheel = 0x020E;
        // 拦截中本窗口不响应滚轮，避免滚动面板把布局滚没。
        if (_hook?.Capturing == true && (m.Msg == wmMouseWheel || m.Msg == wmMouseHWheel))
        {
            m.Result = IntPtr.Zero;
            return;
        }

        const int wmMouseActivate = 0x0021;
        const int maNoActivate = 3;
        if (_running && _radioRecv.Checked && m.Msg == wmMouseActivate)
        {
            m.Result = (IntPtr)maNoActivate;
            return;
        }

        base.WndProc(ref m);
    }

    private void ActivateExistingWindow() => ShowPanel();

    private void RelayoutContent()
    {
        if (_relayouting || !_scroll.IsHandleCreated)
            return;
        _relayouting = true;
        try
        {
            _txtLog.Height = 220;
            var needed = _body.PreferredSize.Height;
            var extra = _scroll.ClientSize.Height - needed;
            _txtLog.Height = Math.Max(220, 220 + Math.Max(0, extra));
        }
        finally
        {
            _relayouting = false;
        }
    }

    private void ToggleFullScreen() => SetFullScreen(!_fullScreen);

    private void SetFullScreen(bool on)
    {
        if (on == _fullScreen)
            return;

        if (on)
        {
            _windowedBorder = FormBorderStyle;
            _windowedState = WindowState;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _fullScreen = true;
        }
        else
        {
            FormBorderStyle = _windowedBorder;
            WindowState = _windowedState == FormWindowState.Maximized
                ? FormWindowState.Maximized
                : FormWindowState.Normal;
            _fullScreen = false;
        }

        RelayoutContent();
        _hook?.RefreshLocalClip();
    }

    private sealed class WheelBlockFilter : IMessageFilter
    {
        public bool Active { get; set; }

        public bool PreFilterMessage(ref Message m)
        {
            if (!Active)
                return false;
            // 拦住发到任意子控件的滚轮，避免 AutoScroll / 文本框滚动把界面滚塌。
            return m.Msg is 0x020A or 0x020E;
        }
    }

    private sealed class KeyboardChoice
    {
        public KeyboardChoice(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }

    private sealed class IntervalChoice
    {
        public IntervalChoice(int ms, string label)
        {
            Ms = ms;
            Label = label;
        }

        public int Ms { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }
}
