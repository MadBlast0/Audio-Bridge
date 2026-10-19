using System.Runtime.InteropServices;
using NAudio.Wave;

namespace AudioBridge;

/// <summary>A bounded 48 kHz stereo float ring buffer. Old audio is dropped if consumers fall behind.</summary>
internal sealed class AudioRingBuffer
{
    public const int SampleRate = 48_000;
    public const int Channels = 2;
    private readonly int _capacityFrames = SampleRate * 2;
    private readonly float[] _data = new float[SampleRate * 2 * Channels];
    private readonly object _gate = new();
    private int _readFrame;
    private int _writeFrame;
    private int _countFrames;
    private float _peakLevel;

    public float PeakLevel => Volatile.Read(ref _peakLevel);

    public void Clear()
    {
        lock (_gate)
        {
            _readFrame = _writeFrame = _countFrames = 0;
            Volatile.Write(ref _peakLevel, 0f);
        }
    }

    public void WriteFloatBytes(ReadOnlySpan<byte> bytes, MicrophoneNoiseGate? gate = null)
    {
        int bytesPerFrame = sizeof(float) * Channels;
        int byteCount = bytes.Length - (bytes.Length % bytesPerFrame);
        if (byteCount <= 0) return;
        WriteSamples(MemoryMarshal.Cast<byte, float>(bytes[..byteCount]), gate);
    }

    public void WriteSilence(int frames)
    {
        if (frames <= 0) return;
        Volatile.Write(ref _peakLevel, 0f);
        lock (_gate)
        {
            if (frames >= _capacityFrames)
            {
                _readFrame = _writeFrame = _countFrames = 0;
                frames = _capacityFrames;
            }
            MakeRoom(frames);
            for (int i = 0; i < frames; i++)
            {
                int sampleIndex = _writeFrame * Channels;
                _data[sampleIndex] = 0f;
                _data[sampleIndex + 1] = 0f;
                _writeFrame = (_writeFrame + 1) % _capacityFrames;
            }
            _countFrames += frames;
        }
    }

    public void WriteSamples(ReadOnlySpan<float> samples, MicrophoneNoiseGate? gate = null)
    {
        int frames = samples.Length / Channels;
        if (frames <= 0) return;
        int sourceStartFrame = 0;
        if (frames > _capacityFrames)
        {
            sourceStartFrame = frames - _capacityFrames;
            frames = _capacityFrames;
        }

        float peak = 0f;
        lock (_gate)
        {
            MakeRoom(frames);
            for (int frame = 0; frame < frames; frame++)
            {
                int src = (sourceStartFrame + frame) * Channels;
                int dst = _writeFrame * Channels;
                float left = Sanitize(samples[src]);
                float right = Sanitize(samples[src + 1]);
                if (gate is not null) gate.ProcessFrame(ref left, ref right);
                _data[dst] = left;
                _data[dst + 1] = right;
                peak = Math.Max(peak, Math.Max(Math.Abs(left), Math.Abs(right)));
                _writeFrame = (_writeFrame + 1) % _capacityFrames;
            }
            _countFrames += frames;
        }
        Volatile.Write(ref _peakLevel, peak);
    }

    public void ReadFrames(float[] destination, int frames)
    {
        int samplesRequested = frames * Channels;
        if (destination.Length < samplesRequested)
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));

        lock (_gate)
        {
            int available = Math.Min(frames, _countFrames);
            for (int i = 0; i < available; i++)
            {
                int src = _readFrame * Channels;
                int dst = i * Channels;
                destination[dst] = _data[src];
                destination[dst + 1] = _data[src + 1];
                _readFrame = (_readFrame + 1) % _capacityFrames;
            }
            _countFrames -= available;
            Array.Clear(destination, available * Channels, (frames - available) * Channels);
        }
    }

    private void MakeRoom(int incomingFrames)
    {
        int overflow = _countFrames + incomingFrames - _capacityFrames;
        if (overflow <= 0) return;
        _readFrame = (_readFrame + overflow) % _capacityFrames;
        _countFrames -= overflow;
    }

    private static float Sanitize(float value) => float.IsFinite(value) ? value : 0f;
}

/// <summary>
/// Produces one shared mixed stream for one or more playback destinations. Each reader has its own
/// cursor, so optional headphone monitoring does not consume audio intended for the virtual cable.
/// </summary>
internal sealed class MixedAudioProvider
{
    private const int BufferSeconds = 2;
    private const int MaxMixChunkFrames = 4096;
    private readonly AudioRingBuffer _applicationAudio;
    private readonly AudioRingBuffer _microphoneAudio;
    private readonly object _gate = new();
    private readonly int _capacityFrames = AudioRingBuffer.SampleRate * BufferSeconds;
    private readonly float[] _sharedData = new float[AudioRingBuffer.SampleRate * BufferSeconds * AudioRingBuffer.Channels];
    private readonly Dictionary<int, long> _readerPositions = new();
    private float[] _applicationTemp = Array.Empty<float>();
    private float[] _microphoneTemp = Array.Empty<float>();
    private int _nextReaderId = 1;
    private long _writeFrame;
    private float _applicationGainDb;
    private float _microphoneGainDb;
    private float _outputGainDb;
    private bool _applicationMuted;
    private bool _microphoneMuted;
    private bool _outputMuted;
    private float _peakLevel;

    public MixedAudioProvider(AudioRingBuffer applicationAudio, AudioRingBuffer microphoneAudio)
    {
        _applicationAudio = applicationAudio;
        _microphoneAudio = microphoneAudio;
    }

    public float PeakLevel => Volatile.Read(ref _peakLevel);
    public float ApplicationGainDb { get => Volatile.Read(ref _applicationGainDb); set => Volatile.Write(ref _applicationGainDb, Math.Clamp(value, -24f, 12f)); }
    public float MicrophoneGainDb { get => Volatile.Read(ref _microphoneGainDb); set => Volatile.Write(ref _microphoneGainDb, Math.Clamp(value, -24f, 12f)); }
    public float OutputGainDb { get => Volatile.Read(ref _outputGainDb); set => Volatile.Write(ref _outputGainDb, Math.Clamp(value, -24f, 12f)); }
    public bool ApplicationMuted { get => Volatile.Read(ref _applicationMuted); set => Volatile.Write(ref _applicationMuted, value); }
    public bool MicrophoneMuted { get => Volatile.Read(ref _microphoneMuted); set => Volatile.Write(ref _microphoneMuted, value); }
    public bool OutputMuted { get => Volatile.Read(ref _outputMuted); set => Volatile.Write(ref _outputMuted, value); }

    public IWaveProvider CreateReader(bool drivesTimeline)
    {
        lock (_gate)
        {
            int id = _nextReaderId++;
            _readerPositions[id] = _writeFrame;
            return new SharedReader(this, id, drivesTimeline);
        }
    }

    private int ReadForReader(int readerId, bool drivesTimeline, Span<byte> output)
    {
        const int bytesPerFrame = sizeof(float) * AudioRingBuffer.Channels;
        int byteCount = output.Length - output.Length % bytesPerFrame;
        if (byteCount <= 0) return 0;
        int requestedFrames = byteCount / bytesPerFrame;

        lock (_gate)
        {
            if (!_readerPositions.TryGetValue(readerId, out long cursor)) return 0;
            cursor = Math.Max(cursor, _writeFrame - _capacityFrames);
            if (drivesTimeline)
            {
                long targetFrame = cursor + requestedFrames;
                while (_writeFrame < targetFrame)
                {
                    int toGenerate = (int)Math.Min(MaxMixChunkFrames, targetFrame - _writeFrame);
                    GenerateFrames(toGenerate);
                }
            }

            // Only the main destination advances the mix timeline. A monitor reader copies only
            // frames that exist and does not advance over an underrun; this lets it catch up when
            // the primary output produces the next block instead of permanently drifting ahead.
            cursor = Math.Max(cursor, _writeFrame - _capacityFrames);
            int availableFrames = (int)Math.Clamp(_writeFrame - cursor, 0L, requestedFrames);
            int framesToCopy = drivesTimeline ? requestedFrames : availableFrames;
            Span<float> outputSamples = MemoryMarshal.Cast<byte, float>(output[..byteCount]);
            float peak = 0f;
            for (int frame = 0; frame < requestedFrames; frame++)
            {
                int dest = frame * AudioRingBuffer.Channels;
                if (frame >= framesToCopy)
                {
                    outputSamples[dest] = 0f;
                    outputSamples[dest + 1] = 0f;
                    continue;
                }
                long sequence = cursor + frame;
                int src = (int)(sequence % _capacityFrames) * AudioRingBuffer.Channels;
                float left = _sharedData[src];
                float right = _sharedData[src + 1];
                outputSamples[dest] = left;
                outputSamples[dest + 1] = right;
                peak = Math.Max(peak, Math.Max(Math.Abs(left), Math.Abs(right)));
            }
            _readerPositions[readerId] = cursor + (drivesTimeline ? requestedFrames : framesToCopy);
            if (drivesTimeline) Volatile.Write(ref _peakLevel, peak);
            return byteCount;
        }
    }

    private void GenerateFrames(int frames)
    {
        int samples = frames * AudioRingBuffer.Channels;
        EnsureTempCapacity(samples);
        _applicationAudio.ReadFrames(_applicationTemp, frames);
        _microphoneAudio.ReadFrames(_microphoneTemp, frames);

        float appGain = DbToLinear(Volatile.Read(ref _applicationGainDb));
        float micGain = DbToLinear(Volatile.Read(ref _microphoneGainDb));
        float outputGain = DbToLinear(Volatile.Read(ref _outputGainDb));
        bool appMute = Volatile.Read(ref _applicationMuted);
        bool micMute = Volatile.Read(ref _microphoneMuted);
        bool outMute = Volatile.Read(ref _outputMuted);
        if (appMute) appGain = 0f;
        if (micMute) micGain = 0f;
        if (outMute) outputGain = 0f;

        float peak = 0f;
        for (int frame = 0; frame < frames; frame++)
        {
            int index = frame * AudioRingBuffer.Channels;
            float left = _applicationTemp[index] * appGain + _microphoneTemp[index] * micGain;
            float right = _applicationTemp[index + 1] * appGain + _microphoneTemp[index + 1] * micGain;
            left = Math.Clamp(left * outputGain, -1f, 1f);
            right = Math.Clamp(right * outputGain, -1f, 1f);
            int target = (int)((_writeFrame + frame) % _capacityFrames) * AudioRingBuffer.Channels;
            _sharedData[target] = left;
            _sharedData[target + 1] = right;
            peak = Math.Max(peak, Math.Max(Math.Abs(left), Math.Abs(right)));
        }
        _writeFrame += frames;
        Volatile.Write(ref _peakLevel, peak);
    }

    private void EnsureTempCapacity(int samples)
    {
        if (_applicationTemp.Length >= samples) return;
        _applicationTemp = new float[samples];
        _microphoneTemp = new float[samples];
    }

    private static float DbToLinear(float db) => (float)Math.Pow(10d, db / 20d);

    private void RemoveReader(int id)
    {
        lock (_gate) _readerPositions.Remove(id);
    }

    private sealed class SharedReader : IWaveProvider, IDisposable
    {
        private readonly MixedAudioProvider _owner;
        private readonly int _id;
        private readonly bool _drivesTimeline;
        private bool _disposed;
        public SharedReader(MixedAudioProvider owner, int id, bool drivesTimeline)
        {
            _owner = owner;
            _id = id;
            _drivesTimeline = drivesTimeline;
        }
        public WaveFormat WaveFormat { get; } = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(AudioRingBuffer.SampleRate, AudioRingBuffer.Channels);
        public int Read(Span<byte> buffer) => _disposed ? 0 : _owner.ReadForReader(_id, _drivesTimeline, buffer);
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.RemoveReader(_id);
        }
    }
}


/// <summary>Applies a separate output gain to a cached mix reader, used for headphone monitoring.</summary>
internal sealed class GainWaveProvider : IWaveProvider, IDisposable
{
    private readonly IWaveProvider _source;
    private readonly IDisposable? _sourceDisposable;
    private float _gain = 1f;
    private bool _disposed;

    public GainWaveProvider(IWaveProvider source, int percent)
    {
        _source = source;
        _sourceDisposable = source as IDisposable;
        Percent = percent;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;
    public int Percent
    {
        get => (int)Math.Round(Volatile.Read(ref _gain) * 100f);
        set => Volatile.Write(ref _gain, Math.Clamp(value, 0, 100) / 100f);
    }

    public int Read(Span<byte> buffer)
    {
        if (_disposed) return 0;
        int read = _source.Read(buffer);
        if (read <= 0) return read;
        int alignedBytes = read - read % sizeof(float);
        Span<float> samples = MemoryMarshal.Cast<byte, float>(buffer[..alignedBytes]);
        float gain = Volatile.Read(ref _gain);
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
        return read;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sourceDisposable?.Dispose();
    }
}
