namespace AudioBridge;

/// <summary>
/// Very lightweight microphone noise gate. It attenuates audio only when the signal remains below
/// a conservative threshold; it is not the neural RNNoise algorithm and may trim very quiet speech.
/// </summary>
internal sealed class MicrophoneNoiseGate
{
    private bool _enabled;
    private float _envelope;
    private float _gain = 1f;
    private const float SampleRate = AudioRingBuffer.SampleRate;
    private static readonly float EnvelopeAttack = 1f - (float)Math.Exp(-1f / (0.008f * SampleRate));
    private static readonly float EnvelopeRelease = 1f - (float)Math.Exp(-1f / (0.075f * SampleRate));
    private static readonly float GateAttack = 1f - (float)Math.Exp(-1f / (0.006f * SampleRate));
    private static readonly float GateRelease = 1f - (float)Math.Exp(-1f / (0.16f * SampleRate));
    private const float OpenThreshold = 0.0032f; // approximately -50 dBFS
    private const float CloseThreshold = 0.0022f; // hysteresis avoids rapid chattering

    public bool Enabled { get => Volatile.Read(ref _enabled); set => Volatile.Write(ref _enabled, value); }

    public void ProcessFrame(ref float left, ref float right)
    {
        if (!Enabled)
        {
            _envelope = Math.Max(Math.Abs(left), Math.Abs(right));
            _gain = 1f;
            return;
        }

        float level = Math.Max(Math.Abs(left), Math.Abs(right));
        float envelopeCoefficient = level > _envelope ? EnvelopeAttack : EnvelopeRelease;
        _envelope += (level - _envelope) * envelopeCoefficient;
        bool shouldOpen = _gain > 0.5f ? _envelope >= CloseThreshold : _envelope >= OpenThreshold;
        float targetGain = shouldOpen ? 1f : 0f;
        float gateCoefficient = targetGain > _gain ? GateAttack : GateRelease;
        _gain += (targetGain - _gain) * gateCoefficient;
        left *= _gain;
        right *= _gain;
    }
}
