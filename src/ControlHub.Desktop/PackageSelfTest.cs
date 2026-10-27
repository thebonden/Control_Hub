using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
            using var host = new HostSession();
            await host.StartAsync(original);
            try
            {
                using var status = await host.StatusAsync();
                if (status.RootElement.GetProperty("version").GetString() != "0.0.001") throw new InvalidDataException("Wrong version");
                var obs = status.RootElement.GetProperty("plugins").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "obs");
                if (obs.GetProperty("state").GetString() != "unavailable") throw new InvalidDataException("OBS isolation failed");
                checks.Add("Published host starts; authenticated status works; test OBS is isolated");
                using var other = new HostSession();
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
