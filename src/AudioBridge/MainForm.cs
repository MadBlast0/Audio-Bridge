using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace AudioBridge;

internal sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(245, 245, 244);
    private static readonly Color Surface = Color.White;
    private static readonly Color MainText = Color.FromArgb(28, 28, 28);
    private static readonly Color SecondaryText = Color.FromArgb(105, 105, 105);
    private static readonly Color MutedText = Color.FromArgb(145, 145, 145);
    private static readonly Color Border = Color.FromArgb(226, 226, 224);
    private static readonly Color SoftFill = Color.FromArgb(246, 246, 245);
    private static readonly Color DarkButton = Color.FromArgb(30, 30, 30);

    private readonly AudioEngine _engine = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly FlowLayoutPanel _stack = new();
    private readonly List<Control> _cards = new();

    private readonly Button _oneApp = new();
    private readonly Button _desktopAudio = new();
    private readonly ComboBox _application = new();
    private readonly ComboBox _microphone = new();
    private readonly ComboBox _outputDevice = new();
    private readonly ComboBox _noiseSuppression = new();
    private readonly TrackBar _appGain = new();
    private readonly TrackBar _micGain = new();
    private readonly TrackBar _outputGain = new();
    private readonly Label _appGainValue = new();
    private readonly Label _micGainValue = new();
    private readonly Label _outputGainValue = new();
    private readonly CheckBox _muteApp = new();
    private readonly CheckBox _muteMic = new();
    private readonly CheckBox _muteOutput = new();
    private readonly CheckBox _monitor = new();
    private readonly ComboBox _monitorDevice = new();
    private readonly TrackBar _monitorGain = new();
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
    private readonly LinkLabel _vbAudioLink = new();
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
        Text = "AudioBridge";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 820);
        MinimumSize = new Size(820, 760);
        BackColor = Canvas;
        ForeColor = MainText;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        _stack.Dock = DockStyle.Fill;
        _stack.FlowDirection = FlowDirection.TopDown;
        _stack.WrapContents = false;
        _stack.AutoScroll = true;
        _stack.Padding = new Padding(22, 18, 22, 14);
        _stack.BackColor = Canvas;
        Controls.Add(_stack);

        BuildHeader();
        BuildInputsCard();
        BuildOutputCard();
        BuildFooter();

        _stack.SizeChanged += (_, _) => ArrangeCards();
        ArrangeCards();
        LoadDevices(initialLoad: true);
        RefreshApplications();
        RestoreSettingsToControls();
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
            Height = 58,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Canvas
        };
        _cards.Add(header);

        var icon = new Panel
        {
            Size = new Size(42, 42),
            Location = new Point(0, 5),
            BackColor = Surface
        };
        icon.Paint += (_, e) =>
        {
            using var borderPen = new Pen(Border, 1);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawRectangle(borderPen, 0, 0, icon.Width - 1, icon.Height - 1);
            using var wavePen = new Pen(MainText, 2, System.Drawing.Drawing2D.LineCap.Round);
            e.Graphics.DrawLine(wavePen, 12, 17, 12, 25);
            e.Graphics.DrawLine(wavePen, 17, 12, 17, 30);
            e.Graphics.DrawLine(wavePen, 22, 9, 22, 33);
            e.Graphics.DrawLine(wavePen, 27, 15, 27, 27);
            e.Graphics.DrawLine(wavePen, 32, 18, 32, 24);
        };

        var title = new Label
        {
            Text = "AudioBridge",
            Location = new Point(54, 3),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
            ForeColor = MainText
        };
        var subtitle = new Label
        {
            Text = "Route app audio into your microphone",
            Location = new Point(55, 31),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = SecondaryText
        };
        _status.Text = "●  Not routing";
        _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleCenter;
        _status.Font = new Font("Segoe UI", 8.5F);
        _status.ForeColor = SecondaryText;
        _status.BackColor = Surface;
        _status.Size = new Size(136, 30);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _status.Location = new Point(header.Width - 136, 12);
        _status.BorderStyle = BorderStyle.FixedSingle;

        header.Controls.Add(icon);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_status);
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
        StyleSegmentButton(_oneApp, "One app", selected: true);
        StyleSegmentButton(_desktopAudio, "Desktop audio", selected: false);
        _oneApp.Location = new Point(0, 24);
        _oneApp.Size = new Size(112, 29);
        _desktopAudio.Location = new Point(114, 24);
        _desktopAudio.Size = new Size(126, 29);
        _oneApp.Click += (_, _) => SetSourceMode(desktop: false);
        _desktopAudio.Click += (_, _) => SetSourceMode(desktop: true);

        StyleCombo(_application);
        _application.Location = new Point(0, 61);
        _application.Height = 31;
        _application.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _refreshApps.Text = "Refresh";
        StyleButton(_refreshApps, secondary: true);
        _refreshApps.Size = new Size(62, 31);
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

        _appColumn.Controls.Add(BodyLabel("Input gain", 0, 153));
        ConfigureGainTrackbar(_appGain, _settings.ApplicationGainDb);
        _appGain.Location = new Point(0, 172);
        _appGain.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_appGainValue);
        _appGainValue.Location = new Point(287, 152);
        _appGainValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _appGainValue.Width = 68;
        _appGainValue.TextAlign = ContentAlignment.MiddleRight;
        _appGainValue.Text = FormatDb(_appGain.Value);
        _appGain.ValueChanged += (_, _) => { _appGainValue.Text = FormatDb(_appGain.Value); _engine.ApplicationGainDb = _appGain.Value; };
        _appColumn.Controls.Add(_appGain);
        _appColumn.Controls.Add(_appGainValue);
        _appColumn.Controls.Add(_muteApp);
        StyleCheckBox(_muteApp, "Mute app audio");
        _muteApp.Location = new Point(0, 215);
        _muteApp.CheckedChanged += (_, _) => _engine.ApplicationMuted = _muteApp.Checked;
        var resetApp = CreateLink("Reset");
        resetApp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        resetApp.Location = new Point(315, 216);
        resetApp.LinkClicked += (_, _) => _appGain.Value = 0;
        _appColumn.Controls.Add(resetApp);

        _micColumn.Controls.Add(BodyLabel("Microphone", 0, 0, true));
        StyleCombo(_microphone);
        _microphone.Location = new Point(0, 24);
        _microphone.Height = 31;
        _microphone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

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

        _micColumn.Controls.Add(BodyLabel("Input gain", 0, 115));
        ConfigureGainTrackbar(_micGain, _settings.MicrophoneGainDb);
        _micGain.Location = new Point(0, 134);
        _micGain.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_micGainValue);
        _micGainValue.Location = new Point(287, 114);
        _micGainValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _micGainValue.Width = 68;
        _micGainValue.TextAlign = ContentAlignment.MiddleRight;
        _micGainValue.Text = FormatDb(_micGain.Value);
        _micGain.ValueChanged += (_, _) => { _micGainValue.Text = FormatDb(_micGain.Value); _engine.MicrophoneGainDb = _micGain.Value; };
        _micColumn.Controls.Add(_micGain);
        _micColumn.Controls.Add(_micGainValue);
        _micColumn.Controls.Add(_muteMic);
        StyleCheckBox(_muteMic, "Mute microphone");
        _muteMic.Location = new Point(0, 173);
        _muteMic.CheckedChanged += (_, _) => _engine.MicrophoneMuted = _muteMic.Checked;
        var resetMic = CreateLink("Reset");
        resetMic.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        resetMic.Location = new Point(315, 174);
        resetMic.LinkClicked += (_, _) => _micGain.Value = 0;
        _micColumn.Controls.Add(resetMic);

        _micColumn.Controls.Add(BodyLabel("Noise suppression", 0, 207));
        StyleCombo(_noiseSuppression);
        _noiseSuppression.Items.Add("Off");
        _noiseSuppression.Items.Add("Lightweight noise gate");
        _noiseSuppression.Items.Add("Windows voice processing (if supported)");
        _noiseSuppression.SelectedIndex = Math.Clamp(_settings.NoiseSuppressionMode ?? (_settings.NoiseGateEnabled ? 1 : 0), 0, 2);
        _noiseSuppression.Location = new Point(0, 226);
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
        StyleCombo(_outputDevice);
        _outputDevice.Location = new Point(18, 82);
        _outputDevice.Height = 32;
        _outputDevice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _outputManuallySelected = _settings.OutputManuallySelected;
        _outputDevice.SelectionChangeCommitted += (_, _) => _outputManuallySelected = true;
        card.Controls.Add(_outputDevice);

        var hint = BodyLabel("Recommended for Valorant / Discord: AudioBridge Virtual Mic.", 18, 119);
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

        card.Controls.Add(BodyLabel("Output gain", 18, 196));
        ConfigureGainTrackbar(_outputGain, _settings.OutputGainDb);
        _outputGain.Location = new Point(18, 214);
        _outputGain.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleValueLabel(_outputGainValue);
        _outputGainValue.Width = 72;
        _outputGainValue.Location = new Point(card.Width - 102, 195);
        _outputGainValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _outputGainValue.TextAlign = ContentAlignment.MiddleRight;
        _outputGainValue.Text = FormatDb(_outputGain.Value);
        _outputGain.ValueChanged += (_, _) => { _outputGainValue.Text = FormatDb(_outputGain.Value); _engine.OutputGainDb = _outputGain.Value; };
        card.Controls.Add(_outputGain);
        card.Controls.Add(_outputGainValue);

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
        var resetOutput = CreateLink("Reset");
        resetOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        resetOutput.Location = new Point(card.Width - 56, 249);
        resetOutput.LinkClicked += (_, _) => _outputGain.Value = 0;
        card.Controls.Add(_muteOutput);
        card.Controls.Add(_monitor);
        card.Controls.Add(resetOutput);

        _monitorOptions.Location = new Point(18, 282);
        _monitorOptions.Size = new Size(card.Width - 36, 80);
        _monitorOptions.BackColor = Surface;
        _monitorOptions.Visible = false;
        _monitorOptions.Controls.Add(BodyLabel("Playback device", 0, 0));
        StyleCombo(_monitorDevice);
        _monitorDevice.Location = new Point(0, 18);
        _monitorDevice.Height = 28;
        _monitorDevice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _monitorOptions.Controls.Add(_monitorDevice);
        _monitorOptions.Controls.Add(BodyLabel("Monitor volume", 0, 52));
        _monitorGain.Minimum = 0;
        _monitorGain.Maximum = 100;
        _monitorGain.Value = Math.Clamp(_settings.MonitorVolumePercent, 0, 100);
        _monitorGain.TickStyle = TickStyle.None;
        _monitorGain.SmallChange = 5;
        _monitorGain.LargeChange = 10;
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
        _monitorOptions.Controls.Add(_monitorGain);
        StyleValueLabel(_monitorGainValue);
        _monitorGainValue.Text = $"{_monitorGain.Value}%";
        _monitorGainValue.TextAlign = ContentAlignment.MiddleRight;
        _monitorGainValue.Location = new Point(_monitorOptions.Width - 58, 52);
        _monitorGainValue.Width = 54;
        _monitorGainValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _monitorOptions.Controls.Add(_monitorGainValue);
        card.Controls.Add(_monitorOptions);
    }

    private void BuildFooter()
    {
        var footer = new Panel { Height = 58, Margin = new Padding(0, 0, 0, 0), BackColor = Canvas };
        _cards.Add(footer);

        _driverSetup.Text = "Set up virtual mic";
        StyleButton(_driverSetup, secondary: true);
        _driverSetup.Size = new Size(142, 36);
        _driverSetup.Location = new Point(0, 7);
        _driverSetup.Click += async (_, _) => await SetupVirtualCableAsync(showDisclosure: true);

        _vbAudioLink.Text = "VB-Audio donationware";
        _vbAudioLink.AutoSize = true;
        _vbAudioLink.LinkColor = SecondaryText;
        _vbAudioLink.ActiveLinkColor = MainText;
        _vbAudioLink.VisitedLinkColor = SecondaryText;
        _vbAudioLink.Location = new Point(154, 19);
        _vbAudioLink.LinkClicked += (_, _) => OpenUrl("https://vb-audio.com/Cable/");

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
        footer.Controls.Add(_vbAudioLink);
        footer.Controls.Add(_refreshDevices);
        footer.Controls.Add(_startStop);
        _stack.Controls.Add(footer);
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
            Location = new Point(18, 35)
        };
        var number = new Label
        {
            Text = ordinal,
            AutoSize = true,
            Font = new Font("Segoe UI", 8F),
            ForeColor = MutedText,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(card.Width - 40, 15)
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
        ForeColor = MainText
    };

    private static void StyleQuietLabel(Label label)
    {
        label.Font = new Font("Segoe UI", 8F);
        label.ForeColor = SecondaryText;
    }

    private static void StyleValueLabel(Label label)
    {
        label.AutoSize = false;
        label.Font = new Font("Segoe UI", 8.5F);
        label.ForeColor = MainText;
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Font = new Font("Segoe UI", 9F);
        combo.IntegralHeight = false;
        combo.MaxDropDownItems = 16;
        combo.BackColor = Surface;
        combo.ForeColor = MainText;
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
    }

    private static void StyleButton(Button button, bool secondary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = secondary ? 1 : 0;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = secondary ? Surface : DarkButton;
        button.ForeColor = secondary ? MainText : Color.White;
        button.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    private static void StyleCheckBox(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.Font = new Font("Segoe UI", 8.5F);
        checkBox.ForeColor = SecondaryText;
        checkBox.BackColor = Surface;
        checkBox.Cursor = Cursors.Hand;
    }

    private static void ConfigureGainTrackbar(TrackBar trackBar, int value)
    {
        trackBar.Minimum = -12;
        trackBar.Maximum = 12;
        trackBar.Value = Math.Clamp(value, trackBar.Minimum, trackBar.Maximum);
        trackBar.TickStyle = TickStyle.None;
        trackBar.SmallChange = 1;
        trackBar.LargeChange = 3;
        trackBar.BackColor = Surface;
        trackBar.AutoSize = false;
        trackBar.Height = 32;
        trackBar.Cursor = Cursors.Hand;
    }

    private static LinkLabel CreateLink(string text) => new()
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
        int width = Math.Max(600, _stack.ClientSize.Width - _stack.Padding.Horizontal - 8);
        foreach (Control control in _stack.Controls)
            control.Width = width;

        if (_cards.Count >= 3)
        {
            var header = _cards[0];
            if (_status.Parent == header) _status.Location = new Point(header.Width - _status.Width, 13);
            var footer = _cards[^1];
            _refreshDevices.Location = new Point(footer.Width - 280, 7);
            _startStop.Location = new Point(footer.Width - _startStop.Width, 7);
        }
        if (_inputsCard is not null && _appColumn is not null && _micColumn is not null && _columnDivider is not null)
        {
            int columnWidth = Math.Max(250, (_inputsCard.Width - 64) / 2);
            _appColumn.Location = new Point(18, 66);
            _appColumn.Width = columnWidth;
            _micColumn.Location = new Point(18 + columnWidth + 26, 66);
            _micColumn.Width = columnWidth;
            _columnDivider.Location = new Point(18 + columnWidth + 13, 66);
            _columnDivider.Height = 252;
            _application.Width = Math.Max(130, columnWidth - 70);
            _refreshApps.Location = new Point(columnWidth - 62, 61);
            _appSignalText.Location = new Point(columnWidth - 100, 103);
            _appMeter.Width = columnWidth;
            _appGain.Width = Math.Max(150, columnWidth - 58);
            _appGainValue.Location = new Point(columnWidth - 68, 152);
            foreach (Control c in _appColumn.Controls)
            {
                if (c is LinkLabel link && link.Text == "Reset") link.Location = new Point(columnWidth - 45, 216);
            }
            _microphone.Width = columnWidth;
            _micSignalText.Location = new Point(columnWidth - 100, 65);
            _micMeter.Width = columnWidth;
            _micGain.Width = Math.Max(150, columnWidth - 58);
            _micGainValue.Location = new Point(columnWidth - 68, 114);
            foreach (Control c in _micColumn.Controls)
            {
                if (c is LinkLabel link && link.Text == "Reset") link.Location = new Point(columnWidth - 45, 174);
            }
            _noiseSuppression.Width = columnWidth;
        }
        if (_outputCard is not null)
        {
            _outputDevice.Width = _outputCard.Width - 36;
            _mixSignalText.Location = new Point(_outputCard.Width - 144, 147);
            _mixMeter.Width = _outputCard.Width - 36;
            _outputGain.Width = Math.Max(200, _outputCard.Width - 120);
            _outputGainValue.Location = new Point(_outputCard.Width - 102, 195);
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

    private void SetSourceMode(bool desktop)
    {
        _isDesktopAudio = desktop;
        StyleSegmentButton(_oneApp, "One app", !desktop);
        StyleSegmentButton(_desktopAudio, "Desktop audio", desktop);
        _application.Enabled = !desktop && !_engine.IsRunning;
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
            _microphone.Items.Add(new AudioDeviceChoice("", "No microphone (app audio only)"));
            foreach (var device in microphones) _microphone.Items.Add(device);
            _microphone.EndUpdate();

            _outputDevice.BeginUpdate();
            _outputDevice.Items.Clear();
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

            _driverSetup.Text = cable is null ? "Set up virtual mic" : "Virtual mic info";
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

    private static void SelectDevice(ComboBox combo, string desiredId)
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
        _application.EndUpdate();

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
        _outputGain.Value = Math.Clamp(_settings.OutputGainDb, -12, 12);
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
        _outputGainValue.Text = FormatDb(_outputGain.Value);
        if (!string.IsNullOrWhiteSpace(_settings.MicrophoneDeviceId)) SelectDevice(_microphone, _settings.MicrophoneDeviceId!);
        if (_settings.OutputManuallySelected && !string.IsNullOrWhiteSpace(_settings.OutputDeviceId))
            SelectDevice(_outputDevice, _settings.OutputDeviceId!);
    }

    private void ApplyLiveSettings()
    {
        _engine.ApplicationGainDb = _appGain.Value;
        _engine.MicrophoneGainDb = _micGain.Value;
        _engine.OutputGainDb = _outputGain.Value;
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
                "The VB-Audio virtual cable is already detected. AudioBridge labels its destination as ‘AudioBridge Virtual Mic’ inside this app. Windows and other apps may retain the vendor device name.",
                "Virtual microphone", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenUrl("https://vb-audio.com/Cable/");
            return;
        }

        if (showDisclosure)
        {
            var result = MessageBox.Show(this,
                "AudioBridge needs a virtual audio cable to make the mixed signal available as a microphone in games and chat apps.\r\n\r\n" +
                "Continue to unpack the official VB-CABLE package included with this AudioBridge release and open its installer? Development builds may download the package if it is not embedded. Windows will ask you to approve driver installation. You may need to restart Windows.\r\n\r\n" +
                "VB-CABLE is a separate VB-Audio donationware component. Origin: vb-audio.com/Cable. Optional donations and licensing are available on the vendor website.",
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
                "AudioBridge couldn't prepare the virtual cable. You can retry or open the official VB-Audio site.\r\n\r\n" + ex.Message,
                "Virtual microphone setup failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            OpenUrl("https://vb-audio.com/Cable/");
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
        if (output is null)
        {
            MessageBox.Show(this, "No active audio output device was found.", "AudioBridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        _settings.MicrophoneDeviceId = (_microphone.SelectedItem as AudioDeviceChoice)?.Id;
        _settings.OutputDeviceId = (_outputDevice.SelectedItem as AudioDeviceChoice)?.Id;
        _settings.OutputManuallySelected = _outputManuallySelected;
        _settings.ApplicationGainDb = _appGain.Value;
        _settings.MicrophoneGainDb = _micGain.Value;
        _settings.OutputGainDb = _outputGain.Value;
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

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

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
    public Color BorderColor { get; set; } = Color.LightGray;
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
