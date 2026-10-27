using System.Security.Cryptography;
using System.Text.Json;

namespace ControlHub.Desktop;

internal sealed record Settings(string Token, string ObsUrl, string ObsPassword, string Language)
{
    public static Settings Create() => new(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        "ws://127.0.0.1:4455", "", "da");

    public void Validate()
    {
        if (Token.Length < 32 || Language is not ("da" or "en") ||
            !Uri.TryCreate(ObsUrl, UriKind.Absolute, out var endpoint) ||
            !endpoint.IsLoopback || endpoint.Scheme != "ws" || endpoint.UserInfo.Length != 0 ||
            endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new InvalidDataException("Ugyldige indstillinger / Invalid settings");
    }
}

internal sealed class SettingsStore(string directory)
{
    private readonly string file = Path.Combine(directory, "settings.dat");
    public Settings Load()
    {
        if (!File.Exists(file)) return Settings.Create();
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser);
        try
        {
            var value = JsonSerializer.Deserialize<Settings>(plain) ?? throw new InvalidDataException();
            value.Validate();
            return value;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public void Save(Settings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(directory);
        var plain = JsonSerializer.SerializeToUtf8Bytes(settings);
        try
        {
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            var temporary = file + ".tmp";
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, file, true);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
