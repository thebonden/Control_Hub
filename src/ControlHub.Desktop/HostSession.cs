using System.Diagnostics;
using System.Security.Cryptography;
using ControlHub.Plugins.AndroidRemote;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;

namespace ControlHub.Desktop;

// Ejer kun processen, som dette vindue starter / Owns only the process started by this window.
internal sealed class HostSession : IDisposable
{
    private Process? process;
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(35) };
    private string? ownerKey;
    public IRemoteOwnerClient RemoteOwner => IsRunning && ownerKey is not null
        ? new RemoteOwnerClient(client, new Uri(apiBase + "androidremote/owner/"), ownerKey)
        : throw new InvalidOperationException("Start Control Hub først / Start Control Hub first.");
    private readonly int portNumber;
    private readonly string apiBase;
    public bool IsRunning => process is { HasExited: false };

    // Separat port til pakketest; brugergrænsefladen benytter altid standardporten.
    // Separate port for package tests; the user interface always uses the default.
    public HostSession(int portNumber = 5187)
    {
        if (portNumber is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(portNumber));
        this.portNumber = portNumber;
        apiBase = $"http://127.0.0.1:{portNumber}/api/v1/";
    }

    public async Task StartAsync(Settings settings)
    {
        settings.Validate();
        if (IsRunning) return;
        using (var port = new TcpClient())
        {
            try
            {
                await port.ConnectAsync(IPAddress.Loopback, portNumber);
                throw new InvalidOperationException($"Port {portNumber} er optaget. Stop den anden host først. / Port {portNumber} is in use. Stop the other host first.");
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused) { }
        }
        var executable = Path.Combine(AppContext.BaseDirectory, "host", "ControlHub.Host.exe");
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        start.Environment["CONTROLHUB_TOKEN"] = settings.Token;
        ownerKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        start.Environment["CONTROLHUB_OWNER_KEY"] = ownerKey;
        start.Environment["CONTROLHUB_PORT"] = portNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["CONTROLHUB_OBS_URL"] = settings.ObsUrl;
        start.Environment["CONTROLHUB_OBS_PASSWORD"] = settings.ObsPassword;
        start.Environment["CONTROLHUB_DESKTOP_LIFETIME"] = "stdin";
        process?.Dispose();
        process = Process.Start(start) ?? throw new InvalidOperationException("Start mislykkedes / Start failed");
        // Dræn uden at gemme output; hemmeligheder må ikke ende i logfiler.
        // Drain without persisting output; secrets must not end up in logs.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (process.HasExited) throw new InvalidOperationException("Hosten stoppede under start / Host exited during startup");
                try
                {
                    using var response = await client.GetAsync(apiBase + "plugins");
                    if (response.IsSuccessStatusCode) return;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100);
            }
            throw new TimeoutException("Hosten svarede ikke / Host did not respond");
        }
        catch { await StopAsync(); throw; }
    }

    public async Task<JsonDocument> StatusAsync()
    {
        if (!IsRunning) throw new InvalidOperationException("Control Hub er stoppet / Control Hub is stopped");
        using var response = await client.GetAsync(apiBase + "status");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public async Task StopAsync()
    {
        ownerKey = null;
        if (!IsRunning) return;
        process!.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
        }
    }

    public void Dispose()
    {
        // Closing the pipe also stops the child if this window crashes.
        // Lukning af røret stopper også barnet, hvis vinduet går ned.
        if (IsRunning) process!.StandardInput.Close();
        process?.Dispose();
        client.Dispose();
    }
}
