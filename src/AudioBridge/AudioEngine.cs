using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioBridge;

internal enum AudioSourceMode
{
    SpecificApplication,
    DesktopAudio
}

internal sealed record AudioDeviceChoice(string Id, string Name, string? DisplayName = null)
{
    public override string ToString() => DisplayName ?? Name;
}

internal sealed class AudioEngine : IAsyncDisposable
{
    private readonly AudioRingBuffer _applicationBuffer = new();
    private readonly AudioRingBuffer _microphoneBuffer = new();
    private readonly MixedAudioProvider _mixer;
    private readonly MicrophoneNoiseGate _noiseGate = new();
    private readonly List<WasapiRecorder> _recorders = new();
    private readonly List<MMDevice> _ownedDevices = new();
    private MMDeviceEnumerator? _enumerator;
    private WasapiPlayer? _player;
    private WasapiPlayer? _monitorPlayer;
    private IDisposable? _primaryReader;
    private IDisposable? _monitorReader;
    private GainWaveProvider? _monitorGainProvider;
    private int _monitorGainPercent = 60;
    private bool _running;

    public AudioEngine() => _mixer = new MixedAudioProvider(_applicationBuffer, _microphoneBuffer);

    public float ApplicationGainDb { get => _mixer.ApplicationGainDb; set => _mixer.ApplicationGainDb = value; }
    public float MicrophoneGainDb { get => _mixer.MicrophoneGainDb; set => _mixer.MicrophoneGainDb = value; }
    public float OutputGainDb { get => _mixer.OutputGainDb; set => _mixer.OutputGainDb = value; }
    public bool ApplicationMuted { get => _mixer.ApplicationMuted; set => _mixer.ApplicationMuted = value; }
    public bool MicrophoneMuted { get => _mixer.MicrophoneMuted; set => _mixer.MicrophoneMuted = value; }
    public bool OutputMuted { get => _mixer.OutputMuted; set => _mixer.OutputMuted = value; }
    public bool NoiseGateEnabled { get => _noiseGate.Enabled; set => _noiseGate.Enabled = value; }
    public bool UseWindowsVoiceProcessing { get; set; }
    public bool WindowsVoiceProcessingActive { get; private set; }
    public string? WindowsVoiceProcessingFallbackReason { get; private set; }
    public int MonitorGainPercent
    {
        get => _monitorGainPercent;
        set
        {
            _monitorGainPercent = Math.Clamp(value, 0, 100);
            if (_monitorGainProvider is not null) _monitorGainProvider.Percent = _monitorGainPercent;
        }
    }
    public bool IsRunning => _running;
    public float ApplicationPeak => _applicationBuffer.PeakLevel;
    public float MicrophonePeak => _microphoneBuffer.PeakLevel;
    public float MixedPeak => _mixer.PeakLevel;

    public static List<AudioDeviceChoice> GetDevices(DataFlow flow)
    {
        var result = new List<AudioDeviceChoice>();
        using var enumerator = new MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        foreach (var endpoint in endpoints)
        {
            try { result.Add(new AudioDeviceChoice(endpoint.ID, endpoint.FriendlyName)); }
            finally { endpoint.Dispose(); }
        }
        return result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static string? GetDefaultDeviceId(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return device.ID;
        }
        catch { return null; }
    }

    public static bool IsCablePlaybackName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase);

    public static bool IsCableCaptureName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase);

    public async Task StartAsync(
        AudioSourceMode mode,
        int processId,
        string? microphoneDeviceId,
        string outputDeviceId,
        bool monitorEnabled,
        string? monitorDeviceId,
        int monitorGainPercent,
        string? defaultOutputDeviceId)
    {
        await StopAsync();
        _applicationBuffer.Clear();
        _microphoneBuffer.Clear();
        WindowsVoiceProcessingActive = false;
        WindowsVoiceProcessingFallbackReason = null;
        _enumerator = new MMDeviceEnumerator();

        try
        {
            var output = _enumerator.GetDevice(outputDeviceId);
            _ownedDevices.Add(output);
            _player = new WasapiPlayerBuilder()
                .WithDevice(output)
                .WithSharedMode()
                .WithLatency(80)
                .Build();
            var primaryProvider = _mixer.CreateReader(drivesTimeline: true);
            _primaryReader = primaryProvider as IDisposable;
            _player.Init(primaryProvider);

            var format = WaveFormat.CreateIeeeFloatWaveFormat(AudioRingBuffer.SampleRate, AudioRingBuffer.Channels);
            WasapiRecorder sourceRecorder;
            if (mode == AudioSourceMode.SpecificApplication)
            {
                if (processId <= 0) throw new InvalidOperationException("Choose a running application first.");
                sourceRecorder = await new WasapiRecorderBuilder()
                    .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
                    .WithFormat(format)
                    .WithBufferLength(60)
                    .BuildAsync();
            }
            else
            {
                sourceRecorder = new WasapiRecorderBuilder()
                    .WithLoopbackCapture()
                    .WithFormat(format)
                    .WithBufferLength(60)
                    .Build();
            }
            AttachRecorder(sourceRecorder, _applicationBuffer);
            sourceRecorder.StartRecording();

            if (!string.IsNullOrWhiteSpace(microphoneDeviceId))
            {
                var microphone = _enumerator.GetDevice(microphoneDeviceId);
                _ownedDevices.Add(microphone);
                WasapiRecorder? micRecorder = null;
                try
                {
                    micRecorder = CreateMicrophoneRecorder(microphone, format, UseWindowsVoiceProcessing);
                    AttachRecorder(micRecorder, _microphoneBuffer, UseWindowsVoiceProcessing ? null : _noiseGate);
                    micRecorder.StartRecording();
                    WindowsVoiceProcessingActive = UseWindowsVoiceProcessing;
                }
                catch (Exception ex) when (UseWindowsVoiceProcessing)
                {
                    if (micRecorder is not null)
                    {
                        _recorders.Remove(micRecorder);
                        try { micRecorder.StopRecording(); } catch { }
                        try { await micRecorder.DisposeAsync(); } catch { }
                    }
                    WindowsVoiceProcessingFallbackReason = ex.Message;
                    micRecorder = CreateMicrophoneRecorder(microphone, format, useWindowsVoiceProcessing: false);
                    AttachRecorder(micRecorder, _microphoneBuffer, _noiseGate);
                    micRecorder.StartRecording();
                    WindowsVoiceProcessingActive = false;
                }
            }

            string? targetMonitorDeviceId = string.IsNullOrWhiteSpace(monitorDeviceId)
                ? defaultOutputDeviceId
                : monitorDeviceId;
            MonitorGainPercent = monitorGainPercent;
            if (monitorEnabled && !string.IsNullOrWhiteSpace(targetMonitorDeviceId) &&
                !string.Equals(outputDeviceId, targetMonitorDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                var monitorOutput = _enumerator.GetDevice(targetMonitorDeviceId);
                _ownedDevices.Add(monitorOutput);
                _monitorPlayer = new WasapiPlayerBuilder()
                    .WithDevice(monitorOutput)
                    .WithSharedMode()
                    .WithLatency(80)
                    .Build();
                var monitorProvider = _mixer.CreateReader(drivesTimeline: false);
                _monitorGainProvider = new GainWaveProvider(monitorProvider, monitorGainPercent);
                _monitorReader = _monitorGainProvider;
                _monitorPlayer.Init(_monitorGainProvider);
            }

            _player.Play();
            _monitorPlayer?.Play();
            _running = true;
        }
        catch
        {
            await StopAsync();
            throw;
        }
    }

    private static WasapiRecorder CreateMicrophoneRecorder(MMDevice microphone, WaveFormat format, bool useWindowsVoiceProcessing)
    {
        var builder = new WasapiRecorderBuilder()
            .WithDevice(microphone)
            .WithFormat(format)
            .WithBufferLength(60);
        if (useWindowsVoiceProcessing) builder.WithCommunicationsMode();
        return builder.Build();
    }

    private void AttachRecorder(WasapiRecorder recorder, AudioRingBuffer destination, MicrophoneNoiseGate? gate = null)
    {
        recorder.DataAvailable += (buffer, flags, _, _) =>
        {
            try
            {
                if (buffer.Length <= 0) return;
                if ((((int)flags) & 0x2) != 0)
                    destination.WriteSilence(buffer.Length / (sizeof(float) * AudioRingBuffer.Channels));
                else
                    destination.WriteFloatBytes(buffer, gate);
            }
            catch
            {
                // Never let a processing error escape the native WASAPI callback.
            }
        };
        _recorders.Add(recorder);
    }

    public async Task StopAsync()
    {
        _running = false;
        if (_monitorPlayer is not null)
        {
            try { _monitorPlayer.Stop(); } catch { }
            try { await _monitorPlayer.DisposeAsync(); } catch { }
            _monitorPlayer = null;
        }
        if (_player is not null)
        {
            try { _player.Stop(); } catch { }
            try { await _player.DisposeAsync(); } catch { }
            _player = null;
        }
        _monitorReader?.Dispose();
        _monitorReader = null;
        _monitorGainProvider = null;
        _primaryReader?.Dispose();
        _primaryReader = null;

        foreach (var recorder in _recorders.ToArray())
        {
            try { recorder.StopRecording(); } catch { }
            try { await recorder.DisposeAsync(); } catch { }
        }
        _recorders.Clear();
        foreach (var device in _ownedDevices)
        {
            try { device.Dispose(); } catch { }
        }
        _ownedDevices.Clear();
        if (_enumerator is not null)
        {
            try { _enumerator.Dispose(); } catch { }
            _enumerator = null;
        }
        _applicationBuffer.Clear();
        _microphoneBuffer.Clear();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
