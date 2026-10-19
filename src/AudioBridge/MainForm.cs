using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace AudioBridge;

internal sealed class MainForm : Form
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    private UiTheme Theme => _settings.DarkMode ? UiTheme.Dark : UiTheme.Light;
    private Color Canvas => Theme.Canvas;
    private Color Surface => Theme.Surface;
    private Color MainText => Theme.MainText;
    private Color SecondaryText => Theme.SecondaryText;
    private Color MutedText => Theme.MutedText;
    private Color Border => Theme.Border;
    private Color SoftFill => Theme.SoftFill;
    private Color DarkButton => Theme.DarkButton;

    private readonly AudioEngine _engine = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly Panel _viewport = new();
    private readonly FlowLayoutPanel _stack = new();
    private readonly List<Control> _cards = new();

    private readonly Button _oneApp = new();
    private readonly Button _desktopAudio = new();
    private readonly ThemedDropdown _application = new();
    private readonly ThemedDropdown _microphone = new();
    private readonly ThemedDropdown _outputDevice = new();
    private readonly ThemedDropdown _noiseSuppression = new();
    private readonly ThemedTrackBar _appVolume = new();
    private readonly ThemedTrackBar _micVolume = new();
    private readonly ThemedTrackBar _outputVolume = new();
    private readonly ThemedTrackBar _appGain = new();
    private readonly ThemedTrackBar _micGain = new();
    private readonly ThemedCheckBox _appGainEnabled = new();
    private readonly ThemedCheckBox _micGainEnabled = new();
    private readonly Label _appVolumeValue = new();
    private readonly Label _micVolumeValue = new();
    private readonly Label _outputVolumeValue = new();
    private readonly Label _appGainValue = new();
    private readonly Label _micGainValue = new();
    private readonly ThemedCheckBox _muteApp = new();
    private readonly ThemedCheckBox _muteMic = new();
    private readonly ThemedCheckBox _muteOutput = new();
    private readonly ThemedCheckBox _monitor = new();
    private readonly ThemedDropdown _monitorDevice = new();
    private readonly ThemedTrackBar _monitorGain = new();
    private readonly Label _monitorGainValue = new();
    private readonly Panel _monitorOptions = new();
    private readonly LevelMeter _appMeter = new();
    private readonly LevelMeter _micMeter = new();
    private readonly LevelMeter _mixMeter = new();
    private readonly Label _appSignalText = new();
    private readonly Label _micSignalText = new();
    private readonly Label _mixSignalText = new();
    private readonly Label _status = new();
    private readonly Button _startStop = new();
    private readonly Button _refreshDevices = new();
    private readonly Button _refreshApps = new();
    private readonly Button _driverSetup = new();
    private readonly ThemeToggleControl _themeToggle = new();
    private readonly System.Windows.Forms.Timer _meterTimer = new() { Interval = 50 };
    private readonly ToolTip _toolTip = new();
    private CleanCard? _inputsCard;
    private CleanCard? _outputCard;
    private Panel? _appColumn;
    private Panel? _micColumn;
    private Panel? _columnDivider;
    private bool _isDesktopAudio;
    private bool _closing;
    private bool _outputManuallySelected;

    public MainForm()
    {
        Text = "Audio Bridge";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1040, 900);
        MinimumSize = new Size(620, 620);
        BackColor = Canvas;
        ForeColor = MainText;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        _viewport.Dock = DockStyle.Fill;
        _viewport.AutoScroll = true;
        _viewport.BackColor = Canvas;
        _stack.FlowDirection = FlowDirection.TopDown;
        _stack.WrapContents = false;
        _stack.AutoSize = false;
        _stack.AutoScroll = false;
        _stack.Margin = Padding.Empty;
        _stack.Padding = Padding.Empty;
        _stack.BackColor = Canvas;
        _viewport.Controls.Add(_stack);
        Controls.Add(_viewport);

        BuildHeader();
        BuildInputsCard();
        BuildOutputCard();
        BuildFooter();

        _viewport.SizeChanged += (_, _) => ArrangeCards();
        SizeChanged += (_, _) => ArrangeCards();
        Shown += (_, _) => { ArrangeCards(); ApplyTitleBarTheme(_settings.DarkMode); };
        ArrangeCards();
        LoadDevices(initialLoad: true);
        RefreshApplications();
        RestoreSettingsToControls();
        ApplyTheme();
        SetSourceMode(_settings.DesktopAudioSource);
        SetMonitorOptionsVisible(_monitor.Checked);
        ApplyLiveSettings();

        _meterTimer.Tick += (_, _) => UpdateMeters();
        _meterTimer.Start();
        FormClosing += OnFormClosingAsync;
        Shown += OnShown;
    }

    private void BuildHeader()
    {
        var header = new Panel
        {
            Height = 82,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Canvas
        };
        _cards.Add(header);

        var icon = new Panel
        {
            Size = new Size(42, 42),
            Location = new Point(0, 18),
            BackColor = Surface
        };
        icon.Paint += (_, e) =>
        {
            using var borderPen = new Pen(Theme.Border, 1);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawRectangle(borderPen, 0, 0, icon.Width - 1, icon.Height - 1);
            using var wavePen = new Pen(Theme.MainText, 2)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawLine(wavePen, 12, 17, 12, 25);
            e.Graphics.DrawLine(wavePen, 17, 12, 17, 30);
            e.Graphics.DrawLine(wavePen, 22, 9, 22, 33);
            e.Graphics.DrawLine(wavePen, 27, 15, 27, 27);
            e.Graphics.DrawLine(wavePen, 32, 18, 32, 24);
        };

        var title = new Label
        {
            Text = "Audio Bridge",
            Location = new Point(54, 8),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
            ForeColor = MainText,
            Tag = "main"
        };
        var subtitle = new Label
        {
            Text = "Route app audio into your microphone",
            Location = new Point(55, 40),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = SecondaryText,
            Tag = "secondary"
        };
        _status.Text = "●  Not routing";
        _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleCenter;
        _status.Font = new Font("Segoe UI", 8.5F);
        _status.ForeColor = SecondaryText;
        _status.BackColor = Surface;
        _status.Size = new Size(136, 30);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _status.Location = new Point(header.Width - 186, 25);
        _status.BorderStyle = BorderStyle.FixedSingle;

        header.Controls.Add(icon);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_status);
        _themeToggle.Size = new Size(36, 34);
        _themeToggle.Location = new Point(header.Width - 40, 23);
        _themeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _themeToggle.AccessibleName = "Switch color theme";
        _toolTip.SetToolTip(_themeToggle, "Switch to dark mode");
        _themeToggle.Click += (_, _) =>
        {
            _settings.DarkMode = !_settings.DarkMode;
            _settings.Save();
            ApplyTheme();
        };
        _themeToggle.GlyphColor = Theme.MainText;
        _themeToggle.SurfaceColor = Theme.Surface;
        _themeToggle.BorderColor = Theme.Border;
        _themeToggle.HoverColor = Theme.SoftFill;
        header.Controls.Add(_themeToggle);
        _stack.Controls.Add(header);
    }

    private void BuildInputsCard()
    {
        var card = CreateCard("Inputs", "Choose the sound to share and the microphone that carries your voice.", 340, "01");
        _inputsCard = card;

        _appColumn = new Panel { BackColor = Surface, Location = new Point(18, 66), Height = 260 };
        _micColumn = new Panel { BackColor = Surface, Location = new Point(450, 66), Height = 260 };
        _columnDivider = new Panel { BackColor = Border, Location = new Point(434, 66), Width = 1, Height = 252 };

        _appColumn.Controls.Add(BodyLabel("Audio source", 0, 0, true));
        StyleSegmentButton(_oneApp, "Single application", selected: true);
        StyleSegmentButton(_desktopAudio, "Desktop audio", selected: false);
        _oneApp.Location = new Point(0, 24);
        _oneApp.Size = new Size(160, 29);
        _desktopAudio.Location = new Point(162, 24);
        _desktopAudio.Size = new Size(142, 29);
        _oneApp.Click += (_, _) => SetSourceMode(desktop: false);
        _desktopAudio.Click += (_, _) => SetSourceMode(desktop: true);
        _appColumn.Controls.Add(_oneApp);
        _appColumn.Controls.Add(_desktopAudio);

        StyleDropdown(_application);
        _application.Location = new Point(0, 61);
        _application.Height = 31;
        _application.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _appColumn.Controls.Add(_application);

        _refreshApps.Text = "Refresh";
        StyleButton(_refreshApps, secondary: true);
        _refreshApps.Size = new Size(70, 31);
        _refreshApps.Location = new Point(334, 61);
        _refreshApps.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _refreshApps.Click += (_, _) => RefreshApplications();
        _appColumn.Controls.Add(_refreshApps);

        _appColumn.Controls.Add(BodyLabel("Input level", 0, 103));
        _appSignalText.Text = "No signal";
        StyleQuietLabel(_appSignalText);
        _appSignalText.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _appSignalText.Location = new Point(260, 103);
        _appSignalText.AutoSize = false;
        _appSignalText.Width = 100;
        _appSignalText.TextAlign = ContentAlignment.MiddleRight;
        _appColumn.Controls.Add(_appSignalText);
        _appMeter.Location = new Point(0, 124);
        _appMeter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _appColumn.Controls.Add(_appMeter);

        _appColumn.Controls.Add(BodyLabel("Input volume", 0, 153));
        ConfigurePercentTrackbar(_appVolume, _settings.ApplicationVolumePercent);
        _appVolume.Location = new Point(0, 172);
        _appVolume.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_appVolumeValue);
        _appVolumeValue.Location = new Point(287, 152);
        _appVolumeValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _appVolumeValue.Width = 68;
        _appVolumeValue.TextAlign = ContentAlignment.MiddleRight;
        _appVolumeValue.Text = FormatPercent(_appVolume.Value);
        _appVolume.ValueChanged += (_, _) => { _appVolumeValue.Text = FormatPercent(_appVolume.Value); _engine.ApplicationVolumePercent = _appVolume.Value; };
        _appVolume.ValueChangeCommitted += (_, _) => SaveSettings();
        _appColumn.Controls.Add(_appVolume);
        _appColumn.Controls.Add(_appVolumeValue);

        StyleCheckBox(_appGainEnabled, "Enable input gain");
        _appGainEnabled.Location = new Point(0, 205);
        _appGainEnabled.Checked = _settings.ApplicationGainEnabled;
        _appGainEnabled.CheckedChanged += (_, _) => { _appGain.Enabled = _appGainEnabled.Checked; _engine.ApplicationGainDb = _appGainEnabled.Checked ? _appGain.Value : 0; SaveSettings(); };
        _appColumn.Controls.Add(_appGainEnabled);
        _appColumn.Controls.Add(BodyLabel("Gain", 0, 232));
        ConfigureGainTrackbar(_appGain, _settings.ApplicationGainDb);
        _appGain.Enabled = _appGainEnabled.Checked;
        _appGain.Location = new Point(0, 251);
        _appGain.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_appGainValue);
        _appGainValue.Location = new Point(287, 231);
        _appGainValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _appGainValue.Width = 68;
        _appGainValue.TextAlign = ContentAlignment.MiddleRight;
        _appGainValue.Text = FormatDb(_appGain.Value);
        _appGain.ValueChanged += (_, _) => { _appGainValue.Text = FormatDb(_appGain.Value); if (_appGainEnabled.Checked) _engine.ApplicationGainDb = _appGain.Value; };
        _appGain.ValueChangeCommitted += (_, _) => SaveSettings();
        _appColumn.Controls.Add(_appGain);
        _appColumn.Controls.Add(_appGainValue);
        _appColumn.Controls.Add(_muteApp);
        StyleCheckBox(_muteApp, "Mute app audio");
        _muteApp.Location = new Point(0, 294);
        _muteApp.CheckedChanged += (_, _) => _engine.ApplicationMuted = _muteApp.Checked;
        var resetApp = CreateLink("Reset");
        resetApp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        resetApp.Location = new Point(315, 295);
        resetApp.LinkClicked += (_, _) => { _appGain.Value = 0; SaveSettings(); };
        _appColumn.Controls.Add(resetApp);

        _micColumn.Controls.Add(BodyLabel("Microphone", 0, 0, true));
        StyleDropdown(_microphone);
        _microphone.Location = new Point(0, 24);
        _microphone.Height = 31;
        _microphone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _microphone.SelectedIndexChanged += (_, _) => SaveSettings();
        _micColumn.Controls.Add(_microphone);

        _micColumn.Controls.Add(BodyLabel("Input level", 0, 65));
        _micSignalText.Text = "No signal";
        StyleQuietLabel(_micSignalText);
        _micSignalText.AutoSize = false;
        _micSignalText.Width = 100;
        _micSignalText.TextAlign = ContentAlignment.MiddleRight;
        _micSignalText.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _micSignalText.Location = new Point(260, 65);
        _micColumn.Controls.Add(_micSignalText);
        _micMeter.Location = new Point(0, 86);
        _micMeter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _micColumn.Controls.Add(_micMeter);

        _micColumn.Controls.Add(BodyLabel("Input volume", 0, 115));
        ConfigurePercentTrackbar(_micVolume, _settings.MicrophoneVolumePercent);
        _micVolume.Location = new Point(0, 134);
        _micVolume.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_micVolumeValue);
        _micVolumeValue.Location = new Point(287, 114);
        _micVolumeValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _micVolumeValue.Width = 68;
        _micVolumeValue.TextAlign = ContentAlignment.MiddleRight;
        _micVolumeValue.Text = FormatPercent(_micVolume.Value);
        _micVolume.ValueChanged += (_, _) => { _micVolumeValue.Text = FormatPercent(_micVolume.Value); _engine.MicrophoneVolumePercent = _micVolume.Value; };
        _micVolume.ValueChangeCommitted += (_, _) => SaveSettings();
        _micColumn.Controls.Add(_micVolume);
        _micColumn.Controls.Add(_micVolumeValue);

        StyleCheckBox(_micGainEnabled, "Enable microphone gain");
        _micGainEnabled.Location = new Point(0, 167);
        _micGainEnabled.Checked = _settings.MicrophoneGainEnabled;
        _micGainEnabled.CheckedChanged += (_, _) => { _micGain.Enabled = _micGainEnabled.Checked; _engine.MicrophoneGainDb = _micGainEnabled.Checked ? _micGain.Value : 0; SaveSettings(); };
        _micColumn.Controls.Add(_micGainEnabled);
        _micColumn.Controls.Add(BodyLabel("Gain", 0, 194));
        ConfigureGainTrackbar(_micGain, _settings.MicrophoneGainDb);
        _micGain.Enabled = _micGainEnabled.Checked;
        _micGain.Location = new Point(0, 213);
        _micGain.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_micGainValue);
        _micGainValue.Location = new Point(287, 193);
        _micGainValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _micGainValue.Width = 68;
        _micGainValue.TextAlign = ContentAlignment.MiddleRight;
        _micGainValue.Text = FormatDb(_micGain.Value);
        _micGain.ValueChanged += (_, _) => { _micGainValue.Text = FormatDb(_micGain.Value); if (_micGainEnabled.Checked) _engine.MicrophoneGainDb = _micGain.Value; };
        _micGain.ValueChangeCommitted += (_, _) => SaveSettings();
        _micColumn.Controls.Add(_micGain);
        _micColumn.Controls.Add(_micGainValue);
        _micColumn.Controls.Add(_muteMic);
        StyleCheckBox(_muteMic, "Mute microphone");
        _muteMic.Location = new Point(0, 256);
        _muteMic.CheckedChanged += (_, _) => _engine.MicrophoneMuted = _muteMic.Checked;
        var resetMic = CreateLink("Reset");
        resetMic.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        resetMic.Location = new Point(315, 257);
        resetMic.LinkClicked += (_, _) => { _micGain.Value = 0; SaveSettings(); };
        _micColumn.Controls.Add(resetMic);

        _micColumn.Controls.Add(BodyLabel("Noise suppression", 0, 290));
        StyleDropdown(_noiseSuppression);
        _noiseSuppression.Items.Add("Off");
        _noiseSuppression.Items.Add("Lightweight noise gate");
        _noiseSuppression.Items.Add("Windows voice processing (if supported)");
        _noiseSuppression.SelectedIndex = Math.Clamp(_settings.NoiseSuppressionMode ?? (_settings.NoiseGateEnabled ? 1 : 0), 0, 2);
        _noiseSuppression.Location = new Point(0, 309);
        _noiseSuppression.Height = 28;
        _noiseSuppression.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _toolTip.SetToolTip(_noiseSuppression,
            "Lightweight noise gate: reduces sound during quiet moments and may trim very quiet speech. Windows voice processing requests the Windows/device communications noise-processing path where supported; results depend on the microphone and driver. Neither option is RNNoise.");
        _noiseSuppression.SelectedIndexChanged += (_, _) =>
        {
            _engine.NoiseGateEnabled = _noiseSuppression.SelectedIndex == 1;
            _engine.UseWindowsVoiceProcessing = _noiseSuppression.SelectedIndex == 2;
        };
        _micColumn.Controls.Add(_noiseSuppression);

        card.Controls.Add(_appColumn);
        card.Controls.Add(_columnDivider);
        card.Controls.Add(_micColumn);
        _stack.Controls.Add(card);
    }

    private void BuildOutputCard()
    {
        var card = CreateCard("Output", "Send the combined audio to the virtual microphone used by your game or chat app.", 278, "02");
        _outputCard = card;

        card.Controls.Add(BodyLabel("Send mixed audio to", 18, 60, true));
        StyleDropdown(_outputDevice);
        _outputDevice.Location = new Point(18, 82);
        _outputDevice.Height = 32;
        _outputDevice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _outputManuallySelected = _settings.OutputManuallySelected;
        _outputDevice.SelectionChangeCommitted += (_, _) => _outputManuallySelected = true;
        card.Controls.Add(_outputDevice);

        var hint = BodyLabel("Choose where the mixed audio appears as an input device.", 18, 119);
        hint.ForeColor = SecondaryText;
        card.Controls.Add(hint);
        card.Controls.Add(BodyLabel("Mixed output level", 18, 147));
        _mixSignalText.Text = "No signal";
        StyleQuietLabel(_mixSignalText);
        _mixSignalText.AutoSize = false;
        _mixSignalText.Width = 120;
        _mixSignalText.TextAlign = ContentAlignment.MiddleRight;
        _mixSignalText.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _mixSignalText.Location = new Point(card.Width - 144, 147);
        card.Controls.Add(_mixSignalText);
        _mixMeter.Location = new Point(18, 168);
        _mixMeter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(_mixMeter);

        card.Controls.Add(BodyLabel("Output volume", 18, 196));
        ConfigurePercentTrackbar(_outputVolume, _settings.OutputVolumePercent);
        _outputVolume.Location = new Point(18, 214);
        _outputVolume.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_outputVolumeValue);
        _outputVolumeValue.Width = 72;
        _outputVolumeValue.Location = new Point(card.Width - 102, 195);
        _outputVolumeValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _outputVolumeValue.TextAlign = ContentAlignment.MiddleRight;
        _outputVolumeValue.Text = FormatPercent(_outputVolume.Value);
        _outputVolume.ValueChanged += (_, _) => { _outputVolumeValue.Text = FormatPercent(_outputVolume.Value); _engine.OutputVolumePercent = _outputVolume.Value; };
        _outputVolume.ValueChangeCommitted += (_, _) => SaveSettings();
        card.Controls.Add(_outputVolume);
        card.Controls.Add(_outputVolumeValue);

        StyleCheckBox(_muteOutput, "Mute outgoing mix");
        _muteOutput.Location = new Point(18, 249);
        _muteOutput.CheckedChanged += (_, _) => _engine.OutputMuted = _muteOutput.Checked;
        StyleCheckBox(_monitor, "Monitor mix in headphones");
        _monitor.Location = new Point(210, 249);
        _monitor.CheckedChanged += (_, _) =>
        {
            _settings.MonitorInHeadphones = _monitor.Checked;
            SetMonitorOptionsVisible(_monitor.Checked);
        };
        _toolTip.SetToolTip(_monitor, "Optional local playback. Headphones are recommended to avoid your speakers leaking back into the microphone.");
        card.Controls.Add(_muteOutput);
        card.Controls.Add(_monitor);

        _monitorOptions.Location = new Point(18, 282);
        _monitorOptions.Size = new Size(card.Width - 36, 80);
        _monitorOptions.BackColor = Surface;
        _monitorOptions.Visible = false;
        _monitorOptions.Controls.Add(BodyLabel("Playback device", 0, 0));
        StyleDropdown(_monitorDevice);
        _monitorDevice.Location = new Point(0, 18);
        _monitorDevice.Height = 28;
        _monitorDevice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _monitorOptions.Controls.Add(_monitorDevice);
        _monitorOptions.Controls.Add(BodyLabel("Monitor volume", 0, 52));
        _monitorGain.Minimum = 0;
        _monitorGain.Maximum = 100;
        _monitorGain.Value = Math.Clamp(_settings.MonitorVolumePercent, 0, 100);
        _monitorGain.AutoSize = false;
        _monitorGain.Height = 30;
        _monitorGain.BackColor = Surface;
        _monitorGain.Cursor = Cursors.Hand;
        _monitorGain.Location = new Point(94, 47);
        _monitorGain.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _monitorGain.ValueChanged += (_, _) =>
        {
            _monitorGainValue.Text = $"{_monitorGain.Value}%";
            _engine.MonitorGainPercent = _monitorGain.Value;
        };
        _monitorGain.ValueChangeCommitted += (_, _) => SaveSettings();
        _monitorOptions.Controls.Add(_monitorGain);
        StyleValueLabel(_monitorGainValue);
        _monitorGainValue.Text = $"{_monitorGain.Value}%";
        _monitorGainValue.TextAlign = ContentAlignment.MiddleRight;
        _monitorGainValue.Location = new Point(_monitorOptions.Width - 58, 52);
        _monitorGainValue.Width = 54;
        _monitorGainValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _monitorOptions.Controls.Add(_monitorGainValue);
        card.Controls.Add(_monitorOptions);
        _stack.Controls.Add(card);
    }

    private void BuildFooter()
    {
        var footer = new Panel { Height = 58, Margin = new Padding(0, 0, 0, 0), BackColor = Canvas };
        _cards.Add(footer);

        _driverSetup.Text = "Install VB-CABLE";
        StyleButton(_driverSetup, secondary: true);
        _driverSetup.Size = new Size(142, 36);
        _driverSetup.Location = new Point(0, 7);
        _driverSetup.Click += async (_, _) => await SetupVirtualCableAsync(showDisclosure: true);

        _refreshDevices.Text = "Refresh devices";
        StyleButton(_refreshDevices, secondary: true);
        _refreshDevices.Size = new Size(130, 36);
        _refreshDevices.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _refreshDevices.Location = new Point(footer.Width - 276, 7);
        _refreshDevices.Click += (_, _) => LoadDevices(initialLoad: false);

        _startStop.Text = "Start routing";
        StyleButton(_startStop, secondary: false);
        _startStop.Size = new Size(136, 36);
        _startStop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _startStop.Location = new Point(footer.Width - 136, 7);
        _startStop.Click += async (_, _) => await ToggleRoutingAsync();

        footer.Controls.Add(_driverSetup);
        footer.Controls.Add(_refreshDevices);
        footer.Controls.Add(_startStop);
        _stack.Controls.Add(footer);
    }

    private void ApplyTheme()
    {
        var theme = Theme;
        ApplyTitleBarTheme(_settings.DarkMode);
        BackColor = _viewport.BackColor = _stack.BackColor = theme.Canvas;
        ForeColor = theme.MainText;
        foreach (Control control in ControlsRecursive(this))
        {
            if (control is CleanCard card)
            {
                card.BackColor = theme.Surface;
                card.BorderColor = theme.Border;
                card.Invalidate();
            }
            else if (control == _stack || control == _viewport || _cards.Contains(control)) control.BackColor = theme.Canvas;
            else if (control == _columnDivider) control.BackColor = theme.Border;
            else if (control is Panel) control.BackColor = theme.Surface;

            if (control is Label label)
            {
                label.ForeColor = label == _status ? theme.SecondaryText :
                    label.Tag?.ToString() switch
                    {
                        "secondary" => theme.SecondaryText,
                        "muted" => theme.MutedText,
                        _ => theme.MainText
                    };
            }
            if (control is LinkLabel link)
            {
                link.BackColor = theme.Surface;
                link.LinkColor = theme.SecondaryText;
                link.ActiveLinkColor = theme.MainText;
                link.VisitedLinkColor = theme.SecondaryText;
            }
            if (control is ThemedDropdown dropdown)
            {
                dropdown.SurfaceColor = theme.Surface;
                dropdown.TextColor = theme.MainText;
                dropdown.BorderColor = theme.Border;
                dropdown.HoverColor = theme.SoftFill;
                dropdown.Invalidate();
            }
            if (control is ThemedCheckBox check)
            {
                check.SurfaceColor = theme.Surface;
                check.TextColor = theme.SecondaryText;
                check.BorderColor = theme.MutedText;
                check.AccentColor = theme.Accent;
                check.HoverColor = theme.SoftFill;
                check.Invalidate();
            }
            if (control is ThemedTrackBar track)
            {
                track.BackColor = theme.Surface;
                track.TrackColor = theme.SliderTrack;
                track.FillColor = theme.SliderFill;
                track.ThumbColor = theme.SliderThumb;
                track.Invalidate();
            }
            if (control is LevelMeter meter)
            {
                meter.BackColor = theme.Surface;
                meter.InactiveColor = theme.MeterInactive;
                meter.ActiveColor = theme.MeterActive;
                meter.PeakColor = theme.MeterPeak;
                meter.Invalidate();
            }
        }

        StyleSegmentButton(_oneApp, "Single application", !_isDesktopAudio);
        StyleSegmentButton(_desktopAudio, "Desktop audio", _isDesktopAudio);
        StyleButton(_refreshApps, secondary: true);
        StyleButton(_refreshDevices, secondary: true);
        StyleButton(_driverSetup, secondary: true);
        bool routing = _engine.IsRunning;
        StyleButton(_startStop, secondary: routing);
        _themeToggle.DarkMode = _settings.DarkMode;
        _themeToggle.GlyphColor = theme.MainText;
        _themeToggle.SurfaceColor = theme.Surface;
        _themeToggle.BorderColor = theme.Border;
        _themeToggle.HoverColor = theme.SoftFill;
        _toolTip.SetToolTip(_themeToggle, _settings.DarkMode ? "Switch to light mode" : "Switch to dark mode");
        _themeToggle.Invalidate();
        _status.BackColor = theme.Surface;
        _status.ForeColor = theme.SecondaryText;
        _status.Invalidate();
        _cards.OfType<Panel>().FirstOrDefault()?.Invalidate();
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        if (!IsHandleCreated) return;
        int enabled = dark ? 1 : 0;
        int result = DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
        if (result != 0) _ = DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        _ = SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020);
    }

    private static IEnumerable<Control> ControlsRecursive(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in ControlsRecursive(child)) yield return descendant;
        }
    }

    private CleanCard CreateCard(string title, string subtitle, int height, string ordinal)
    {
        var card = new CleanCard
        {
            Height = height,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Surface,
            BorderColor = Border,
            Radius = 9
        };
        _cards.Add(card);
        var heading = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = MainText,
            Location = new Point(18, 13)
        };
        var explanation = new Label
        {
            Text = subtitle,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = SecondaryText,
            Location = new Point(18, 35),
            Tag = "secondary"
        };
        var number = new Label
        {
            Text = ordinal,
            AutoSize = true,
            Font = new Font("Segoe UI", 8F),
            ForeColor = MutedText,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(card.Width - 40, 15),
            Tag = "muted"
        };
        card.Controls.Add(heading);
        card.Controls.Add(explanation);
        card.Controls.Add(number);
        return card;
    }

    private Label BodyLabel(string text, int x, int y, bool bold = false) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", 8.8F, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = MainText,
        Tag = "main"
    };

    private void StyleQuietLabel(Label label)
    {
        label.Font = new Font("Segoe UI", 8F);
        label.ForeColor = SecondaryText;
        label.Tag = "secondary";
    }

    private void StyleValueLabel(Label label)
    {
        label.AutoSize = false;
        label.Font = new Font("Segoe UI", 8.5F);
        label.ForeColor = MainText;
        label.Tag = "main";
    }

    private void StyleDropdown(ThemedDropdown combo)
    {
        combo.Font = new Font("Segoe UI", 9F);
        combo.Height = 36;
        combo.Cursor = Cursors.Hand;
        combo.SurfaceColor = Surface;
        combo.TextColor = MainText;
        combo.BorderColor = Border;
        combo.HoverColor = SoftFill;
    }

    private void StyleSegmentButton(Button button, string text, bool selected)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = selected ? SoftFill : Surface;
        button.ForeColor = MainText;
        button.Font = new Font("Segoe UI", 8.5F, selected ? FontStyle.Bold : FontStyle.Regular);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        SetRoundedRegion(button);
    }

    private void StyleButton(Button button, bool secondary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = secondary ? 1 : 0;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = secondary ? Surface : DarkButton;
        button.ForeColor = secondary ? MainText : Color.White;
        button.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        SetRoundedRegion(button);
    }

    private void StyleCheckBox(ThemedCheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.Font = new Font("Segoe UI", 8.5F);
        checkBox.ForeColor = SecondaryText;
        checkBox.BackColor = Surface;
        checkBox.SurfaceColor = Surface;
        checkBox.TextColor = SecondaryText;
        checkBox.BorderColor = MutedText;
        checkBox.AccentColor = Theme.Accent;
        checkBox.HoverColor = SoftFill;
        checkBox.Cursor = Cursors.Hand;
    }

    private void ConfigureGainTrackbar(ThemedTrackBar trackBar, int value)
    {
        trackBar.Minimum = -12;
        trackBar.Maximum = 12;
        trackBar.Value = Math.Clamp(value, trackBar.Minimum, trackBar.Maximum);
        trackBar.BackColor = Surface;
        trackBar.AutoSize = false;
        trackBar.Height = 32;
        trackBar.Cursor = Cursors.Hand;
    }

    private void ConfigurePercentTrackbar(ThemedTrackBar trackBar, int value)
    {
        trackBar.Minimum = 0;
        trackBar.Maximum = 100;
        trackBar.Value = Math.Clamp(value, 0, 100);
        trackBar.BackColor = Surface;
        trackBar.AutoSize = false;
        trackBar.Height = 32;
        trackBar.Cursor = Cursors.Hand;
    }

    private static void SetRoundedRegion(Button button)
    {
        if (button.Tag?.ToString() != "rounded")
        {
            button.Tag = "rounded";
            button.SizeChanged += (_, _) => SetRoundedRegion(button);
        }
        if (button.Width < 2 || button.Height < 2) return;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float diameter = Math.Min(12, button.Height - 1);
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(button.Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(button.Width - diameter, button.Height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, button.Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        var oldRegion = button.Region;
        button.Region = new Region(path);
        oldRegion?.Dispose();
        path.Dispose();
    }

    private LinkLabel CreateLink(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 8F),
        LinkColor = SecondaryText,
        ActiveLinkColor = MainText,
        VisitedLinkColor = SecondaryText,
        BackColor = Surface
    };

    private void ArrangeCards()
    {
        Size viewportSize = GetViewportSize();
        int availableWidth = Math.Max(560, viewportSize.Width - 44);
        int width = Math.Min(1600, availableWidth);
        foreach (Control control in _stack.Controls)
            control.Width = width;
        int contentHeight = _stack.Controls.Cast<Control>().Sum(control => control.Height + control.Margin.Vertical);
        _stack.Size = new Size(width, contentHeight);
        int x = Math.Max(0, (viewportSize.Width - width) / 2);
        int y = Math.Max(0, (viewportSize.Height - contentHeight) / 2);
        _stack.Location = new Point(x, y);

        if (_cards.Count >= 3)
        {
            var header = _cards[0];
            if (_status.Parent == header)
            {
                _status.Location = new Point(header.Width - _themeToggle.Width - _status.Width - 10, 25);
                _themeToggle.Location = new Point(header.Width - _themeToggle.Width - 4, 23);
            }
            var footer = _cards[^1];
            _refreshDevices.Location = new Point(footer.Width - 280, 7);
            _startStop.Location = new Point(footer.Width - _startStop.Width, 7);
        }
        if (_inputsCard is not null && _appColumn is not null && _micColumn is not null && _columnDivider is not null)
        {
            bool stacked = _inputsCard.Width < 1100;
            int columnWidth = stacked ? _inputsCard.Width - 36 : Math.Max(250, (_inputsCard.Width - 64) / 2);
            _inputsCard.Height = stacked ? 800 : 440;
            _appColumn.Location = new Point(18, 66);
            _appColumn.Width = columnWidth;
            _micColumn.Location = stacked ? new Point(18, 415) : new Point(18 + columnWidth + 26, 66);
            _micColumn.Width = columnWidth;
            _columnDivider.Location = new Point(18 + columnWidth + 13, 66);
            _columnDivider.Height = 252;
            _columnDivider.Visible = !stacked;
            _appColumn.Height = 330;
            _micColumn.Height = 350;
            _application.Width = Math.Max(130, columnWidth - 78);
            _refreshApps.Location = new Point(columnWidth - 70, 61);
            _appSignalText.Location = new Point(columnWidth - 100, 103);
            _appMeter.Width = columnWidth;
            _appVolume.Width = Math.Max(150, columnWidth - 58);
            _appVolumeValue.Location = new Point(columnWidth - 68, 152);
            _appGain.Width = Math.Max(150, columnWidth - 58);
            _appGainValue.Location = new Point(columnWidth - 68, 231);
            foreach (Control c in _appColumn.Controls)
            {
                if (c is LinkLabel link && link.Text == "Reset") link.Location = new Point(columnWidth - 45, 295);
            }
            _microphone.Width = columnWidth;
            _micSignalText.Location = new Point(columnWidth - 100, 65);
            _micMeter.Width = columnWidth;
            _micVolume.Width = Math.Max(150, columnWidth - 58);
            _micVolumeValue.Location = new Point(columnWidth - 68, 114);
            _micGain.Width = Math.Max(150, columnWidth - 58);
            _micGainValue.Location = new Point(columnWidth - 68, 193);
            foreach (Control c in _micColumn.Controls)
            {
                if (c is LinkLabel link && link.Text == "Reset") link.Location = new Point(columnWidth - 45, 257);
            }
            _noiseSuppression.Width = columnWidth;
        }
        _stack.PerformLayout();
        if (_outputCard is not null)
        {
            _outputDevice.Width = _outputCard.Width - 36;
            _mixSignalText.Location = new Point(_outputCard.Width - 144, 147);
            _mixMeter.Width = _outputCard.Width - 36;
            _outputVolume.Width = Math.Max(200, _outputCard.Width - 120);
            _outputVolumeValue.Location = new Point(_outputCard.Width - 102, 195);
            _monitorOptions.Width = _outputCard.Width - 36;
            _monitorDevice.Width = _monitorOptions.Width;
            _monitorGain.Width = Math.Max(120, _monitorOptions.Width - 158);
            _monitorGainValue.Location = new Point(_monitorOptions.Width - 58, 52);
            foreach (Control c in _outputCard.Controls)
            {
                if (c is LinkLabel link && link.Text == "Reset") link.Location = new Point(_outputCard.Width - 56, 249);
            }
            _monitor.Location = new Point(Math.Max(210, _outputCard.Width / 2), 249);
        }
    }

    private Size GetViewportSize()
    {
        return _viewport.ClientSize;
    }

    private void SetSourceMode(bool desktop)
    {
        _isDesktopAudio = desktop;
        _settings.DesktopAudioSource = desktop;
        StyleSegmentButton(_oneApp, "Single application", !desktop);
        StyleSegmentButton(_desktopAudio, "Desktop audio", desktop);
        _application.Enabled = !desktop && !_engine.IsRunning && _application.Items.OfType<ProcessChoice>().Any();
        _refreshApps.Enabled = !desktop && !_engine.IsRunning;
    }

    private void LoadDevices(bool initialLoad)
    {
        try
        {
            string? oldMic = (_microphone.SelectedItem as AudioDeviceChoice)?.Id ?? _settings.MicrophoneDeviceId;
            string? oldOutput = (_outputDevice.SelectedItem as AudioDeviceChoice)?.Id ?? _settings.OutputDeviceId;
            var microphones = AudioEngine.GetDevices(DataFlow.Capture);
            var outputs = AudioEngine.GetDevices(DataFlow.Render)
                .Select(device => AudioEngine.IsCablePlaybackName(device.Name)
                    ? new AudioDeviceChoice(device.Id, device.Name, "AudioBridge Virtual Mic (VB-CABLE)")
                    : device)
                .ToList();

            _microphone.BeginUpdate();
            _microphone.Items.Clear();
            _microphone.Items.Add(new AudioDeviceChoice("", microphones.Count == 0
                ? "No microphone found (app audio only)"
                : "No microphone (app audio only)"));
            foreach (var device in microphones) _microphone.Items.Add(device);
            _microphone.EndUpdate();

            _outputDevice.BeginUpdate();
            _outputDevice.Items.Clear();
            if (outputs.Count == 0)
                _outputDevice.Items.Add(new AudioDeviceChoice("", "No playback devices found"));
            foreach (var device in outputs) _outputDevice.Items.Add(device);
            _outputDevice.EndUpdate();

            _monitorDevice.BeginUpdate();
            _monitorDevice.Items.Clear();
            _monitorDevice.Items.Add(new AudioDeviceChoice("", "Default Windows output"));
            foreach (var device in outputs) _monitorDevice.Items.Add(device);
            _monitorDevice.EndUpdate();
            SelectDevice(_monitorDevice, _settings.MonitorDeviceId ?? "");

            string? defaultMic = AudioEngine.GetDefaultDeviceId(DataFlow.Capture);
            string? defaultOutput = AudioEngine.GetDefaultDeviceId(DataFlow.Render);
            SelectDevice(_microphone, oldMic ?? defaultMic ?? "");

            var cable = outputs.FirstOrDefault(d => AudioEngine.IsCablePlaybackName(d.Name));
            string desiredOutput;
            if (_outputManuallySelected && !string.IsNullOrWhiteSpace(_settings.OutputDeviceId)) desiredOutput = _settings.OutputDeviceId!;
            else desiredOutput = cable?.Id ?? oldOutput ?? defaultOutput ?? outputs.FirstOrDefault()?.Id ?? "";
            SelectDevice(_outputDevice, desiredOutput);

            _driverSetup.Text = "Install VB-CABLE";
            _driverSetup.Visible = cable is null;
            if (cable is null)
                SetStatus("●  Virtual mic not set up", SecondaryText, Surface);
            else if (!_engine.IsRunning)
                SetStatus("●  Not routing", SecondaryText, Surface);

            if (!initialLoad) SaveSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "AudioBridge could not list audio devices.\r\n\r\n" + ex.Message,
                "Audio device error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void SelectDevice(ThemedDropdown combo, string desiredId)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is AudioDeviceChoice device && device.Id == desiredId)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void RefreshApplications()
    {
        int? previousId = (_application.SelectedItem as ProcessChoice)?.Id;
        var current = new List<ProcessChoice>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId) continue;
                string title = process.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(title)) continue;
                current.Add(new ProcessChoice(process.Id, process.ProcessName, title.Trim()));
            }
            catch { }
            finally { process.Dispose(); }
        }

        var choices = current.GroupBy(p => p.Id).Select(g => g.First())
            .OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        _application.BeginUpdate();
        _application.Items.Clear();
        foreach (var item in choices) _application.Items.Add(item);
        if (choices.Count == 0) _application.Items.Add("No eligible apps found — open an app and refresh");
        _application.EndUpdate();
        _application.Enabled = !_isDesktopAudio && !_engine.IsRunning && choices.Count > 0;
        _refreshApps.Enabled = !_isDesktopAudio && !_engine.IsRunning;

        if (previousId.HasValue)
        {
            for (int i = 0; i < _application.Items.Count; i++)
            {
                if (_application.Items[i] is ProcessChoice choice && choice.Id == previousId.Value)
                {
                    _application.SelectedIndex = i;
                    return;
                }
            }
        }
        if (_application.Items.Count > 0) _application.SelectedIndex = 0;
    }

    private void RestoreSettingsToControls()
    {
        _appGain.Value = Math.Clamp(_settings.ApplicationGainDb, -12, 12);
        _micGain.Value = Math.Clamp(_settings.MicrophoneGainDb, -12, 12);
        _appVolume.Value = Math.Clamp(_settings.ApplicationVolumePercent, 0, 100);
        _micVolume.Value = Math.Clamp(_settings.MicrophoneVolumePercent, 0, 100);
        _outputVolume.Value = Math.Clamp(_settings.OutputVolumePercent, 0, 100);
        _muteApp.Checked = _settings.ApplicationMuted;
        _muteMic.Checked = _settings.MicrophoneMuted;
        _muteOutput.Checked = _settings.OutputMuted;
        _monitor.Checked = _settings.MonitorInHeadphones;
        _monitorGain.Value = Math.Clamp(_settings.MonitorVolumePercent, 0, 100);
        _monitorGainValue.Text = $"{_monitorGain.Value}%";
        if (!string.IsNullOrWhiteSpace(_settings.MonitorDeviceId)) SelectDevice(_monitorDevice, _settings.MonitorDeviceId!);
        _noiseSuppression.SelectedIndex = Math.Clamp(_settings.NoiseSuppressionMode ?? (_settings.NoiseGateEnabled ? 1 : 0), 0, 2);
        _appGainValue.Text = FormatDb(_appGain.Value);
        _micGainValue.Text = FormatDb(_micGain.Value);
        _appVolumeValue.Text = FormatPercent(_appVolume.Value);
        _micVolumeValue.Text = FormatPercent(_micVolume.Value);
        _outputVolumeValue.Text = FormatPercent(_outputVolume.Value);
        if (!string.IsNullOrWhiteSpace(_settings.MicrophoneDeviceId)) SelectDevice(_microphone, _settings.MicrophoneDeviceId!);
        if (_settings.OutputManuallySelected && !string.IsNullOrWhiteSpace(_settings.OutputDeviceId))
            SelectDevice(_outputDevice, _settings.OutputDeviceId!);
    }

    private void ApplyLiveSettings()
    {
        _engine.ApplicationGainDb = _appGainEnabled.Checked ? _appGain.Value : 0;
        _engine.MicrophoneGainDb = _micGainEnabled.Checked ? _micGain.Value : 0;
        _engine.ApplicationVolumePercent = _appVolume.Value;
        _engine.MicrophoneVolumePercent = _micVolume.Value;
        _engine.OutputVolumePercent = _outputVolume.Value;
        _engine.ApplicationMuted = _muteApp.Checked;
        _engine.MicrophoneMuted = _muteMic.Checked;
        _engine.OutputMuted = _muteOutput.Checked;
        _engine.MonitorGainPercent = _monitorGain.Value;
        _engine.NoiseGateEnabled = _noiseSuppression.SelectedIndex == 1;
        _engine.UseWindowsVoiceProcessing = _noiseSuppression.SelectedIndex == 2;
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        if (_settings.HasSeenCablePrompt || AudioEngine.GetDevices(DataFlow.Render).Any(d => AudioEngine.IsCablePlaybackName(d.Name))) return;
        _settings.HasSeenCablePrompt = true;
        _settings.Save();
        await SetupVirtualCableAsync(showDisclosure: true);
    }

    private async Task SetupVirtualCableAsync(bool showDisclosure)
    {
        bool isInstalled = AudioEngine.GetDevices(DataFlow.Render).Any(d => AudioEngine.IsCablePlaybackName(d.Name));
        if (isInstalled)
        {
            MessageBox.Show(this,
                "VB-CABLE is already installed and available in the output selector.",
                "Virtual microphone", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (showDisclosure)
        {
            var result = MessageBox.Show(this,
                "AudioBridge needs a virtual audio cable to make the mixed signal available as a microphone in games and chat apps.\r\n\r\n" +
                "Continue to unpack the official VB-CABLE package included with this AudioBridge release and open its installer? Development builds may download the package if it is not embedded. Windows will ask you to approve driver installation. You may need to restart Windows.\r\n\r\n" +
                "VB-CABLE is a separate driver from VB-Audio. Windows will ask you to approve its installation.",
                "Set up AudioBridge Virtual Mic", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result != DialogResult.Yes) return;
        }

        _driverSetup.Enabled = false;
        _refreshDevices.Enabled = false;
        SetStatus("●  Setting up virtual mic", SecondaryText, Surface);
        try
        {
            await VirtualCableInstaller.InstallAsync(this, message =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() => _status.Text = "●  " + message));
            });
            await Task.Delay(1200);
            LoadDevices(initialLoad: false);
            bool installed = AudioEngine.GetDevices(DataFlow.Render).Any(d => AudioEngine.IsCablePlaybackName(d.Name));
            if (installed)
            {
                MessageBox.Show(this,
                    "The virtual cable is now available. Choose ‘AudioBridge Virtual Mic (VB-CABLE)’ as the mix destination, then choose the matching VB-CABLE recording endpoint in your game or chat app. If Windows does not show the new device yet, restart Windows and reopen AudioBridge.",
                    "Virtual microphone setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    "The VB-Audio installer was closed, but AudioBridge still cannot see the virtual cable. Check the installer result, then restart Windows if requested and click Refresh devices.",
                    "Virtual microphone not detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("●  Setup cancelled", SecondaryText, Surface);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "AudioBridge couldn't prepare the virtual cable. Check the download and Windows approval, then try again.\r\n\r\n" + ex.Message,
                "Virtual microphone setup failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _driverSetup.Enabled = true;
            _refreshDevices.Enabled = true;
            LoadDevices(initialLoad: false);
        }
    }

    private async Task ToggleRoutingAsync()
    {
        if (_engine.IsRunning)
        {
            await _engine.StopAsync();
            SetRoutingControlsEnabled(true);
            _startStop.Text = "Start routing";
            SetStatus("●  Not routing", SecondaryText, Surface);
            SaveSettings();
            return;
        }

        var output = _outputDevice.SelectedItem as AudioDeviceChoice;
        if (output is null || string.IsNullOrWhiteSpace(output.Id))
        {
            MessageBox.Show(this, "No active playback device is available. Connect or enable an output device, then click Refresh devices.", "AudioBridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AudioSourceMode mode = _isDesktopAudio ? AudioSourceMode.DesktopAudio : AudioSourceMode.SpecificApplication;
        var app = _application.SelectedItem as ProcessChoice;
        if (mode == AudioSourceMode.SpecificApplication && app is null)
        {
            MessageBox.Show(this, "Choose a running app, or select Desktop audio.", "AudioBridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var mic = _microphone.SelectedItem as AudioDeviceChoice;
        if (!AudioEngine.IsCablePlaybackName(output.Name))
        {
            var answer = MessageBox.Show(this,
                "The selected destination is not the virtual microphone. Audio will be played to that output device and will not become the microphone used by Valorant or Discord.\r\n\r\nFor game/chat routing, choose ‘AudioBridge Virtual Mic (VB-CABLE)’ here.\r\n\r\nStart routing to the selected device anyway?",
                "Check mix destination", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;
        }

        string? defaultOutput = AudioEngine.GetDefaultDeviceId(DataFlow.Render);
        string? monitorDeviceId = (_monitorDevice.SelectedItem as AudioDeviceChoice)?.Id;
        string? effectiveMonitorDeviceId = string.IsNullOrWhiteSpace(monitorDeviceId) ? defaultOutput : monitorDeviceId;
        if (mode == AudioSourceMode.DesktopAudio && _monitor.Checked &&
            !string.IsNullOrWhiteSpace(defaultOutput) &&
            string.Equals(effectiveMonitorDeviceId, defaultOutput, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this,
                "Desktop audio captures your default Windows playback device. Monitoring the mix on that same device would feed AudioBridge's output back into the capture. Choose a different monitor device or turn off headphone monitoring before starting.",
                "Avoid audio feedback", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplyLiveSettings();
        try
        {
            _startStop.Enabled = false;
            SetRoutingControlsEnabled(false);
            SetStatus("●  Starting…", SecondaryText, Surface);
            await _engine.StartAsync(mode, app?.Id ?? 0, mic?.Id, output.Id,
                _monitor.Checked, monitorDeviceId, _monitorGain.Value, defaultOutput);
            _startStop.Text = "Stop routing";
            SetStatus("●  Routing", MainText, Surface);
            SaveSettings();
            if (_engine.WindowsVoiceProcessingFallbackReason is not null)
            {
                MessageBox.Show(this,
                    "Windows voice processing could not be enabled for this microphone. AudioBridge has continued with normal microphone capture instead. You can try another microphone or select Off / Lightweight noise gate.\r\n\r\n" + _engine.WindowsVoiceProcessingFallbackReason,
                    "Windows voice processing unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            await _engine.StopAsync();
            SetRoutingControlsEnabled(true);
            SetStatus("●  Couldn't start", SecondaryText, Surface);
            MessageBox.Show(this,
                "AudioBridge couldn't start routing. Check that the selected application is still open and the selected devices are available.\r\n\r\n" + ex.Message,
                "Could not start audio routing", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _startStop.Enabled = true;
        }
    }

    private void SetRoutingControlsEnabled(bool enabled)
    {
        _oneApp.Enabled = _desktopAudio.Enabled = enabled;
        _application.Enabled = enabled && !_isDesktopAudio;
        _refreshApps.Enabled = enabled && !_isDesktopAudio;
        _microphone.Enabled = _outputDevice.Enabled = _refreshDevices.Enabled = enabled;
        _monitor.Enabled = enabled;
        _monitorDevice.Enabled = enabled;
        _noiseSuppression.Enabled = enabled;
        _driverSetup.Enabled = enabled;
        // Gain and mute controls intentionally remain live while routing. Noise-processing mode
        // is selected before Start because the Windows capture APO is initialized at stream creation.
    }

    private void UpdateMeters()
    {
        bool running = _engine.IsRunning;
        float app = running ? _engine.ApplicationPeak : 0f;
        float mic = running ? _engine.MicrophonePeak : 0f;
        float mix = running ? _engine.MixedPeak : 0f;
        _appMeter.Level = app;
        _micMeter.Level = mic;
        _mixMeter.Level = mix;
        _appSignalText.Text = app > 0.008f ? "Signal detected" : "No signal";
        _micSignalText.Text = mic > 0.008f ? "Signal detected" : "No signal";
        _mixSignalText.Text = mix > 0.008f ? "Signal detected" : "No signal";
    }

    private void SaveSettings()
    {
        _settings.DesktopAudioSource = _isDesktopAudio;
        _settings.MicrophoneDeviceId = (_microphone.SelectedItem as AudioDeviceChoice)?.Id;
        _settings.OutputDeviceId = (_outputDevice.SelectedItem as AudioDeviceChoice)?.Id;
        _settings.OutputManuallySelected = _outputManuallySelected;
        _settings.ApplicationGainDb = _appGain.Value;
        _settings.MicrophoneGainDb = _micGain.Value;
        _settings.ApplicationGainEnabled = _appGainEnabled.Checked;
        _settings.MicrophoneGainEnabled = _micGainEnabled.Checked;
        _settings.ApplicationVolumePercent = _appVolume.Value;
        _settings.MicrophoneVolumePercent = _micVolume.Value;
        _settings.OutputVolumePercent = _outputVolume.Value;
        _settings.ApplicationMuted = _muteApp.Checked;
        _settings.MicrophoneMuted = _muteMic.Checked;
        _settings.OutputMuted = _muteOutput.Checked;
        _settings.MonitorInHeadphones = _monitor.Checked;
        _settings.MonitorDeviceId = (_monitorDevice.SelectedItem as AudioDeviceChoice)?.Id;
        _settings.MonitorVolumePercent = _monitorGain.Value;
        _settings.NoiseGateEnabled = _noiseSuppression.SelectedIndex == 1;
        _settings.NoiseSuppressionMode = Math.Clamp(_noiseSuppression.SelectedIndex, 0, 2);
        _settings.Save();
    }

    private void SetMonitorOptionsVisible(bool visible)
    {
        _monitorOptions.Visible = visible;
        if (_outputCard is not null) _outputCard.Height = visible ? 370 : 278;
        ArrangeCards();
    }

    private void SetStatus(string text, Color foreground, Color background)
    {
        _status.Text = text;
        _status.ForeColor = foreground;
        _status.BackColor = background;
    }

    private static string FormatDb(int db) => db > 0 ? $"+{db} dB" : $"{db} dB";
    private static string FormatPercent(int percent) => $"{percent}%";

    private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;
        _closing = true;
        _meterTimer.Stop();
        SaveSettings();
        Enabled = false;
        await _engine.DisposeAsync();
        BeginInvoke(new Action(Close));
    }

    private sealed record ProcessChoice(int Id, string ProcessName, string Title)
    {
        public override string ToString() => $"{Title}  ·  {ProcessName}";
    }
}

internal sealed class CleanCard : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.LightGray;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 8;

    public CleanCard()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rectangle = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        int diameter = Radius * 2;
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var pen = new Pen(BorderColor, 1);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed record UiTheme(
    Color Canvas,
    Color Surface,
    Color MainText,
    Color SecondaryText,
    Color MutedText,
    Color Border,
    Color SoftFill,
    Color DarkButton,
    Color MeterInactive,
    Color MeterActive,
    Color MeterPeak,
    Color SliderTrack,
    Color SliderFill,
    Color SliderThumb,
    Color Accent)
{
    public static UiTheme Light { get; } = new(
        Color.FromArgb(245, 245, 244), Color.White, Color.FromArgb(28, 28, 28),
        Color.FromArgb(105, 105, 105), Color.FromArgb(145, 145, 145), Color.FromArgb(226, 226, 224),
        Color.FromArgb(246, 246, 245), Color.FromArgb(30, 30, 30), Color.FromArgb(238, 238, 237),
        Color.FromArgb(52, 52, 52), Color.FromArgb(105, 105, 105), Color.FromArgb(218, 220, 223),
        Color.FromArgb(58, 112, 207), Color.FromArgb(58, 112, 207), Color.FromArgb(58, 112, 207));

    public static UiTheme Dark { get; } = new(
        Color.FromArgb(25, 27, 31), Color.FromArgb(34, 37, 42), Color.FromArgb(239, 241, 245),
        Color.FromArgb(181, 187, 197), Color.FromArgb(132, 140, 152), Color.FromArgb(62, 67, 76),
        Color.FromArgb(48, 53, 61), Color.FromArgb(74, 126, 220), Color.FromArgb(57, 62, 71),
        Color.FromArgb(117, 166, 245), Color.FromArgb(255, 183, 77), Color.FromArgb(71, 76, 86),
        Color.FromArgb(117, 166, 245), Color.FromArgb(117, 166, 245), Color.FromArgb(117, 166, 245));
}

internal sealed class ThemedTrackBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _dragging;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; if (_maximum < value) _maximum = value; Value = _value; Invalidate(); }
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(_minimum, value); Value = _value; Invalidate(); }
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int next = Math.Clamp(value, _minimum, _maximum);
            if (_value == next) return;
            _value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? ValueChanged;
    public event EventHandler? ValueChangeCommitted;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = Color.LightGray;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = Color.SteelBlue;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color ThumbColor { get; set; } = Color.SteelBlue;

    public ThemedTrackBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.Slider;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float radius = 7f;
        float left = radius + 1;
        float right = Math.Max(left + 1, Width - radius - 1);
        float centerY = Height / 2f;
        float ratio = Maximum == Minimum ? 0 : (Value - Minimum) / (float)(Maximum - Minimum);
        float thumbX = left + ratio * (right - left);
        Color trackColor = Enabled ? TrackColor : ControlPaint.Light(TrackColor, .18f);
        Color fillColor = Enabled ? FillColor : ControlPaint.Light(FillColor, .32f);
        Color thumbColor = Enabled ? ThumbColor : ControlPaint.Light(ThumbColor, .32f);
        using var track = new SolidBrush(trackColor);
        using var fill = new SolidBrush(fillColor);
        using var thumb = new SolidBrush(thumbColor);
        var trackRect = new RectangleF(left, centerY - 2.5f, right - left, 5f);
        using var trackPath = RoundedTrack(trackRect);
        e.Graphics.FillPath(track, trackPath);
        if (ratio > 0)
        {
            using var fillPath = RoundedTrack(new RectangleF(left, centerY - 2.5f, Math.Max(5f, thumbX - left), 5f));
            e.Graphics.FillPath(fill, fillPath);
        }
        e.Graphics.FillEllipse(thumb, thumbX - radius, centerY - radius, radius * 2, radius * 2);
        if (Focused)
        {
            using var focus = new Pen(FillColor, 1f);
            e.Graphics.DrawEllipse(focus, thumbX - radius - 2, centerY - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        _dragging = true;
        Capture = true;
        SetValueFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) SetValueFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            bool changed = _dragging;
            _dragging = false;
            Capture = false;
            if (changed) ValueChangeCommitted?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int step = e.Shift ? 10 : 1;
        int oldValue = Value;
        if (e.KeyCode == Keys.Left) Value -= step;
        else if (e.KeyCode == Keys.Right) Value += step;
        else if (e.KeyCode == Keys.Home) Value = Minimum;
        else if (e.KeyCode == Keys.End) Value = Maximum;
        else return;
        if (Value != oldValue) ValueChangeCommitted?.Invoke(this, EventArgs.Empty);
        e.Handled = e.SuppressKeyPress = true;
    }

    private void SetValueFromX(int x)
    {
        float left = 8f;
        float right = Math.Max(left + 1, Width - 8f);
        float ratio = Math.Clamp((x - left) / (right - left), 0f, 1f);
        int next = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
        if (next != Value) Value = next;
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedTrack(RectangleF bounds)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = bounds.Height;
        path.AddArc(bounds.Left, bounds.Top, d, d, 90, 180);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ThemeToggleControl : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color GlyphColor { get; set; } = Color.Black;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color SurfaceColor { get; set; } = Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.LightGray;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Color.Gainsboro;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool DarkMode { get; set; }
    private bool _hovered;

    public ThemeToggleControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "Switch color theme";
        Size = new Size(40, 36);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new RectangleF(.5f, .5f, Width - 1f, Height - 1f);
        using var path = RoundedPath(rect, 7f);
        using var fill = new SolidBrush(_hovered || Focused ? HoverColor : SurfaceColor);
        using var border = new Pen(BorderColor);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        DrawThemeGlyph(e.Graphics, ClientRectangle, GlyphColor, SurfaceColor, DarkMode);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Focus(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = e.SuppressKeyPress = true; }
    }

    private static void DrawThemeGlyph(Graphics graphics, Rectangle bounds, Color color, Color background, bool darkMode)
    {
        float cx = bounds.Width / 2f;
        float cy = bounds.Height / 2f;
        using var pen = new Pen(color, 1.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        if (darkMode)
        {
            graphics.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                graphics.DrawLine(pen, cx + (float)Math.Cos(angle) * 8, cy + (float)Math.Sin(angle) * 8,
                    cx + (float)Math.Cos(angle) * 11, cy + (float)Math.Sin(angle) * 11);
            }
        }
        else
        {
            using var moon = new SolidBrush(color);
            using var cutout = new SolidBrush(background);
            graphics.FillEllipse(moon, cx - 7, cy - 8, 16, 16);
            graphics.FillEllipse(cutout, cx - 1, cy - 10, 15, 15);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(RectangleF rect, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ThemedDropdown : Control
{
    private readonly ItemList _items;
    private int _selectedIndex = -1;
    private ThemedDropdownPopup? _popup;
    private bool _hovered;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color SurfaceColor { get; set; } = Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TextColor { get; set; } = Color.FromArgb(28, 28, 28);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.FromArgb(226, 226, 224);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Color.FromArgb(246, 246, 245);
    public ItemList Items => _items;
    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? SelectionChangeCommitted;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int next = value >= 0 && value < _items.Count ? value : -1;
            if (_selectedIndex == next) return;
            _selectedIndex = next;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public ThemedDropdown()
    {
        _items = new ItemList(this);
        Height = 36;
        MinimumSize = new Size(120, 30);
        TabStop = true;
        AccessibleRole = AccessibleRole.ComboBox;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    public void BeginUpdate() => _items.BeginUpdate();
    public void EndUpdate() => _items.EndUpdate();

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var path = RoundedPath(bounds, 7);
        using var fill = new SolidBrush(Enabled ? SurfaceColor : ControlPaint.Light(SurfaceColor, .06f));
        using var border = new Pen(Focused || _hovered ? TextColor : BorderColor, Focused ? 1.5f : 1f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        var textBounds = new Rectangle(12, 1, Math.Max(10, Width - 46), Height - 2);
        TextRenderer.DrawText(e.Graphics, SelectedItem?.ToString() ?? "Select an option", Font, textBounds,
            Enabled ? TextColor : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        float midX = Width - 19;
        float midY = Height / 2f;
        using var arrow = new Pen(Enabled ? TextColor : SystemColors.GrayText, 1.6f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        e.Graphics.DrawLines(arrow, [new PointF(midX - 4, midY - 2), new PointF(midX, midY + 2), new PointF(midX + 4, midY - 2)]);
        if (Focused)
        {
            using var focus = new Pen(BorderColor) { DashPattern = [1, 2] };
            e.Graphics.DrawPath(focus, RoundedPath(RectangleF.Inflate(bounds, -4, -4), 4));
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && Enabled) OpenPopup();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.Down)
        {
            OpenPopup();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up && _items.Count > 0)
        {
            SelectedIndex = Math.Max(0, _selectedIndex - 1);
            e.Handled = true;
        }
    }

    private void OpenPopup()
    {
        if (_popup is not null || _items.Count == 0) return;
        var working = Screen.FromControl(this).WorkingArea;
        Point location = PointToScreen(new Point(0, Height + 4));
        int width = Math.Min(Math.Max(Width, 260), working.Width - 16);
        int height = Math.Min(360, _items.Count * ThemedDropdownPopup.RowHeight + 12);
        if (location.Y + height > working.Bottom - 8)
            location.Y = Math.Max(working.Top + 8, PointToScreen(Point.Empty).Y - height - 4);
        location.X = Math.Clamp(location.X, working.Left + 8, working.Right - width - 8);
        _popup = new ThemedDropdownPopup(_items.ToArray(), _selectedIndex, width, height,
            SurfaceColor, TextColor, BorderColor, HoverColor);
        _popup.ItemChosen += index =>
        {
            SelectedIndex = index;
            SelectionChangeCommitted?.Invoke(this, EventArgs.Empty);
            _popup?.Close();
        };
        _popup.FormClosed += (_, _) =>
        {
            _popup?.Dispose();
            _popup = null;
            Invalidate();
        };
        _popup.Location = location;
        _popup.Show(FindForm());
    }

    private void ItemsChanged()
    {
        if (_selectedIndex >= _items.Count) SelectedIndex = -1;
        Invalidate();
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(RectangleF bounds, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public sealed class ItemList : System.Collections.ObjectModel.Collection<object>
    {
        private readonly ThemedDropdown _owner;
        private int _updateDepth;
        internal ItemList(ThemedDropdown owner) => _owner = owner;
        internal void BeginUpdate() => _updateDepth++;
        internal void EndUpdate() { if (_updateDepth > 0) _updateDepth--; if (_updateDepth == 0) _owner.ItemsChanged(); }
        protected override void InsertItem(int index, object item) { base.InsertItem(index, item); Notify(); }
        protected override void SetItem(int index, object item) { base.SetItem(index, item); Notify(); }
        protected override void RemoveItem(int index) { base.RemoveItem(index); Notify(); }
        protected override void ClearItems() { base.ClearItems(); _owner._selectedIndex = -1; Notify(); }
        private void Notify() { if (_updateDepth == 0) _owner.ItemsChanged(); }
    }
}

internal sealed class ThemedDropdownPopup : Form
{
    public const int RowHeight = 36;
    public event Action<int>? ItemChosen;

    public ThemedDropdownPopup(object[] items, int selectedIndex, int width, int height,
        Color surface, Color text, Color border, Color hover)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = surface;
        ClientSize = new Size(width, height);
        DoubleBuffered = true;
        var list = new Panel { Location = new Point(1, 1), Size = new Size(width - 2, height - 2), AutoScroll = true, BackColor = surface, Padding = new Padding(4) };
        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            var row = new DropdownOption(items[index].ToString() ?? string.Empty, i == selectedIndex,
                text, surface, hover)
            {
                Location = new Point(4, 4 + i * RowHeight),
                Size = new Size(Math.Max(120, width - 24), RowHeight - 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            row.Click += (_, _) => ItemChosen?.Invoke(index);
            list.Controls.Add(row);
        }
        list.AutoScrollMinSize = new Size(0, items.Length * RowHeight + 8);
        Controls.Add(list);
        Deactivate += (_, _) => Close();
        using var path = RoundedPath(new Rectangle(0, 0, width, height), 9);
        Region = new Region(path);
        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var outline = new Pen(border);
            using var rounded = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 9);
            e.Graphics.DrawPath(outline, rounded);
        };
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DropdownOption : Control
{
    private readonly string _text;
    private readonly bool _selected;
    private readonly Color _textColor;
    private readonly Color _surfaceColor;
    private readonly Color _hoverColor;
    private bool _hovered;

    public DropdownOption(string text, bool selected, Color textColor, Color surfaceColor, Color hoverColor)
    {
        _text = text;
        _selected = selected;
        _textColor = textColor;
        _surfaceColor = surfaceColor;
        _hoverColor = hoverColor;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(_hovered || _selected ? _hoverColor : _surfaceColor);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(0, 0, 12, 12, 180, 90); path.AddArc(Width - 12, 0, 12, 12, 270, 90);
        path.AddArc(Width - 12, Height - 12, 12, 12, 0, 90); path.AddArc(0, Height - 12, 12, 12, 90, 90); path.CloseFigure();
        e.Graphics.FillPath(fill, path);
        TextRenderer.DrawText(e.Graphics, _text, Font, new Rectangle(12, 0, Math.Max(10, Width - 42), Height),
            _textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (_selected)
        {
            using var pen = new Pen(_textColor, 1.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            e.Graphics.DrawLines(pen, [new PointF(Width - 25, Height / 2f), new PointF(Width - 21, Height / 2f + 4), new PointF(Width - 14, Height / 2f - 4)]);
        }
    }
}

internal sealed class ThemedCheckBox : CheckBox
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color SurfaceColor { get; set; } = Color.White;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TextColor { get; set; } = Color.FromArgb(105, 105, 105);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.FromArgb(226, 226, 224);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.SteelBlue;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Color.FromArgb(246, 246, 245);

    private bool _hovered;

    public ThemedCheckBox()
    {
        AutoSize = true;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        CheckedChanged += (_, _) => Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textSize = TextRenderer.MeasureText(Text ?? string.Empty, Font);
        return new Size(textSize.Width + 26, Math.Max(20, textSize.Height + 2));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var surface = new SolidBrush(_hovered ? HoverColor : SurfaceColor);
        e.Graphics.FillRectangle(surface, ClientRectangle);
        var box = new Rectangle(1, (Height - 16) / 2, 16, 16);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const int radius = 4;
        path.AddArc(box.Left, box.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(box.Right - radius * 2, box.Top, radius * 2, radius * 2, 270, 90);
        path.AddArc(box.Right - radius * 2, box.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(box.Left, box.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        using var fill = new SolidBrush(Checked ? AccentColor : SurfaceColor);
        using var border = new Pen(Checked ? AccentColor : BorderColor, 1);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        if (Checked)
        {
            using var check = new Pen(Color.White, 1.8f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawLines(check, [new PointF(4, Height / 2f), new PointF(7, Height / 2f + 3), new PointF(13, Height / 2f - 4)]);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(24, 0, Math.Max(0, Width - 24), Height),
            Enabled ? TextColor : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (Focused)
        {
            using var focus = new Pen(BorderColor) { DashPattern = [1, 2] };
            e.Graphics.DrawRectangle(focus, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        }
    }
}
