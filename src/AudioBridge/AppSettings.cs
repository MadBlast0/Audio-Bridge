using System.Text.Json;

namespace AudioBridge;

internal sealed class AppSettings
{
    public string? MicrophoneDeviceId { get; set; }
    public string? OutputDeviceId { get; set; }
    public bool OutputManuallySelected { get; set; }
    public int ApplicationGainDb { get; set; }
    public int MicrophoneGainDb { get; set; }
    public int OutputGainDb { get; set; }
    public bool ApplicationMuted { get; set; }
    public bool MicrophoneMuted { get; set; }
    public bool OutputMuted { get; set; }
    public bool MonitorInHeadphones { get; set; }
    public string? MonitorDeviceId { get; set; }
    public int MonitorVolumePercent { get; set; } = 60;
    public bool NoiseGateEnabled { get; set; }
    public int? NoiseSuppressionMode { get; set; }
    public bool HasSeenCablePrompt { get; set; }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioBridge", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var data = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (data is not null) return data;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Configuration is a convenience; failure to save must not break audio routing.
        }
    }
}
