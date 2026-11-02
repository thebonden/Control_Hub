using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Http.Headers;

namespace ControlHub.Desktop;

// Køres eksplicit mod en separat testmappe; sender ingen produktionskommandoer.
// Explicitly run against a separate test folder; sends no production commands.
internal static class PackageSelfTest
{
    public static async Task<int> RunAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var checks = new List<string>();
        try
        {
            var settingsDirectory = Path.Combine(outputDirectory, Guid.NewGuid().ToString("N"));
            var store = new SettingsStore(settingsDirectory);
            var original = Settings.Create() with { ObsUrl = "ws://127.0.0.1:1", ObsPassword = "self-test-only", Language = "en" };
            store.Save(original);
            if (store.Load() != original) throw new InvalidDataException("Settings round trip failed");
            var settingsFile = Path.Combine(settingsDirectory, "settings.dat");
            var encrypted = File.ReadAllBytes(settingsFile);
            if (Encoding.UTF8.GetString(encrypted).Contains(original.Token, StringComparison.Ordinal)) throw new InvalidDataException("Plaintext token");
            checks.Add("DPAPI settings round trip; no plaintext token");
            foreach (var invalid in new[] { "ws://example.com:4455", "wss://127.0.0.1:4455", "ws://127.0.0.1:4455/path", "ws://user@127.0.0.1:4455" })
            {
                try { (original with { ObsUrl = invalid }).Validate(); throw new Exception("Invalid URL accepted"); }
                catch (InvalidDataException) { }
            }
            checks.Add("Nonlocal and unsupported OBS addresses rejected");
            encrypted[encrypted.Length / 2] ^= 0xff;
            File.WriteAllBytes(settingsFile, encrypted);
            try { store.Load(); throw new InvalidDataException("Corrupt settings accepted"); }
            catch (CryptographicException) { checks.Add("Corrupt encrypted settings rejected"); }
            store.Save(original);
            using var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            var testPort = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            // Hvis porten overtages i mellemtiden, fejler testen sikkert ved HostSessions portkontrol.
            // If another process takes the port, HostSession's occupied-port check fails safely.
            using var host = new HostSession(testPort, phonePort: 0);
            await host.StartAsync(original, IPAddress.Loopback);
            try
            {
                using var status = await host.StatusAsync();
                if (status.RootElement.GetProperty("version").GetString() != "0.0.001") throw new InvalidDataException("Wrong version");
                var obs = status.RootElement.GetProperty("plugins").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "obs");
                if (obs.GetProperty("state").GetString() != "unavailable") throw new InvalidDataException("OBS isolation failed");
                var remote = status.RootElement.GetProperty("plugins").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "androidremote");
                if (remote.GetProperty("state").GetString() != "ready")
                    throw new InvalidDataException("AndroidRemote HTTPS listener failed");
                checks.Add("Published phone listener starts on an isolated TLS port");
                var owner = host.RemoteOwner;
                var invite = await owner.BeginAsync("Package test", ["status.read"]);
                var snapshot = await owner.GetAsync();
                if (!snapshot.PendingPairings.Any(p => p.Id == invite.Id && p.Name == "Package test") ||
                    !snapshot.TransportReady || JsonSerializer.Serialize(snapshot).Contains(invite.Secret, StringComparison.Ordinal))
                    throw new InvalidDataException("Owner invitation status failed");
                var connection = invite.Transport ?? throw new InvalidDataException("Missing phone address");
                using var phone = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false,
                    ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
                        (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == 0 && cert is not null &&
                        cert.GetCertHashString(HashAlgorithmName.SHA256) == connection.CertificateSha256 })
                    { BaseAddress = new Uri(connection.Endpoint) };
                using var claim = await phone.PostAsJsonAsync("/phone/v1/pair", new { invite.Id, invite.Secret });
                claim.EnsureSuccessStatusCode();
                using var credential = JsonDocument.Parse(await claim.Content.ReadAsStringAsync());
                var deviceId = credential.RootElement.GetProperty("device").GetProperty("id").GetString()!;
                phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.RootElement.GetProperty("token").GetString());
                phone.DefaultRequestHeaders.Add("X-ControlHub-Device", deviceId);
                using var phoneStatus = await phone.GetAsync("/phone/v1/status");
                phoneStatus.EnsureSuccessStatusCode();
                using var denied = await phone.PostAsJsonAsync("/phone/v1/commands", new { protocolVersion = "1",
                    id = Guid.NewGuid().ToString("D"), plugin = "powerpoint", action = "next", arguments = new { } });
                if (denied.StatusCode != HttpStatusCode.Forbidden) throw new InvalidDataException("Phone permission boundary failed");
                using var isolated = await phone.GetAsync("/api/v1/plugins");
                if (isolated.StatusCode != HttpStatusCode.NotFound) throw new InvalidDataException("Desktop route exposed on phone port");
                await owner.RemoveAsync(deviceId, pending: false);
                using var revoked = await phone.GetAsync("/phone/v1/status");
                if (revoked.StatusCode != HttpStatusCode.Unauthorized) throw new InvalidDataException("Revoked device accepted");
                checks.Add("Pinned TLS pairing, status, denied command, route isolation and revocation");
                var cancelled = await owner.BeginAsync("Cancel", ["status.read"]);
                await owner.RemoveAsync(cancelled.Id, pending: true);
                if ((await owner.GetAsync()).PendingPairings.Count != 0) throw new InvalidDataException("Owner cancellation failed");
                checks.Add("Desktop owner creates, lists and cancels an invitation; the secret is absent from lists");
                checks.Add("Published host starts; authenticated status works; test OBS is isolated");
                using var other = new HostSession(testPort);
                try { await other.StartAsync(original); throw new Exception("Duplicate host accepted"); }
                catch (InvalidOperationException) { checks.Add("Occupied port rejected without stopping existing host"); }
                using var stillRunning = await host.StatusAsync();
            }
            finally { await host.StopAsync(); }
            if (host.IsRunning) throw new InvalidDataException("Host did not stop");
            checks.Add("Owned host shuts down when stdin closes");
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(new { passed = true, checks }));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.GetType().Name }));
            return 1;
        }
    }
}
