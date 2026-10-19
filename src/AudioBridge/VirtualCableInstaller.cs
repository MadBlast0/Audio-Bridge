using System.Diagnostics;
using System.ComponentModel;
using System.IO.Compression;
using System.Reflection;

namespace AudioBridge;

internal static class VirtualCableInstaller
{
    private const string OfficialDriverUrl = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip";
    private const string EmbeddedResourceName = "AudioBridge.ThirdParty.VBCABLE_Driver_Pack45.zip";
    private const long MaximumDownloadBytes = 25L * 1024L * 1024L;

    public static async Task InstallAsync(IWin32Window owner, Action<string>? statusChanged = null)
    {
        string workRoot = Path.Combine(Path.GetTempPath(), "AudioBridge-VBCABLE-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workRoot);
        try
        {
            string zipPath = Path.Combine(workRoot, "VBCABLE_Driver_Pack45.zip");
            await ObtainPackageAsync(zipPath, statusChanged);
            statusChanged?.Invoke("Preparing VB-Audio setup…");

            string extractPath = Path.Combine(workRoot, "driver");
            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            string? setup = Directory.GetFiles(extractPath, "VBCABLE_Setup_x64.exe", SearchOption.AllDirectories).FirstOrDefault();
            setup ??= Directory.GetFiles(extractPath, "*Setup*x64*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (setup is null)
                throw new InvalidDataException("The official VB-CABLE package did not contain the expected 64-bit setup program. No installer was run.");

            statusChanged?.Invoke("Waiting for Windows permission…");
            var startInfo = new ProcessStartInfo
            {
                FileName = setup,
                WorkingDirectory = Path.GetDirectoryName(setup)!,
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = Process.Start(startInfo);
            if (process is null) throw new InvalidOperationException("Windows did not start the VB-Audio installer.");
            statusChanged?.Invoke("Complete the VB-Audio setup window. Windows may ask you to approve driver installation.");
            await Task.Run(() => process.WaitForExit());
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("Driver installation was cancelled at the Windows permission prompt.", ex);
        }
        finally
        {
            try { Directory.Delete(workRoot, recursive: true); } catch { }
        }
    }

    private static async Task ObtainPackageAsync(string destination, Action<string>? statusChanged)
    {
        using (Stream? embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName))
        {
            if (embedded is not null)
            {
                statusChanged?.Invoke("Preparing the VB-Audio virtual cable package included with this release…");
                await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                await embedded.CopyToAsync(output);
                return;
            }
        }

        statusChanged?.Invoke("Downloading the official VB-Audio virtual cable package…");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AudioBridge/1.0 (VB-CABLE setup helper)");
        using var response = await client.GetAsync(OfficialDriverUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > MaximumDownloadBytes)
            throw new InvalidDataException("The driver package is larger than expected; it was not downloaded.");

        await using var input = await response.Content.ReadAsStreamAsync();
        await using var outputStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer);
            if (read == 0) break;
            total += read;
            if (total > MaximumDownloadBytes)
                throw new InvalidDataException("The driver package exceeded the size limit; it was not used.");
            await outputStream.WriteAsync(buffer.AsMemory(0, read));
        }
        if (total < 100_000) throw new InvalidDataException("The download was unexpectedly small and was not used as a driver package.");
    }
}
