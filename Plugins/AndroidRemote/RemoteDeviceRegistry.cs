using System.Security.Cryptography;
using System.Text;

namespace ControlHub.Plugins.AndroidRemote;

public sealed record RemoteDevice(string Id, string Name, IReadOnlyList<string> Permissions,
    DateTimeOffset PairedAt, DateTimeOffset ExpiresAt);

// Almindelige klasser: ToString må ikke udskrive hemmeligheder som en record gør.
// Ordinary classes: ToString must not print secrets as a record would.
public sealed class PairingOffer(string id, string secret, DateTimeOffset expiresAt)
{
    public PhoneEndpoint? Transport { get; init; }
    public string Id { get; } = id;
    public string Secret { get; } = secret;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
}

public sealed class DeviceCredential(RemoteDevice device, string token)
{
    public RemoteDevice Device { get; } = device;
    public string Token { get; } = token;
}

// Kun i hukommelsen. Genstart kræver ny parring. Ingen netværkslytter eller filadgang.
// Memory only. Restart requires pairing again. No network listener or file access.
public sealed class RemoteDeviceRegistry(TimeProvider? timeProvider = null)
{
    private PhoneEndpoint? transport;
    public bool IsListening { get { lock (sync) return transport is not null; } }
    public void SetTransport(PhoneEndpoint? endpoint)
    {
        lock (sync)
        {
            RevokeAll();
            transport = endpoint;
        }
    }
    public static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan DeviceLifetime = TimeSpan.FromHours(8);
    public const int MaximumDevices = 16;
    public const int MaximumPendingPairings = 8;
    public const int MaximumPairingAttempts = 5;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly object sync = new();
    private readonly Dictionary<string, Pending> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Paired> devices = new(StringComparer.Ordinal);

    private sealed class Pending(string name, IReadOnlyList<string> permissions, byte[] hash, long created, DateTimeOffset expiresAt)
    {
        public string Name { get; } = name;
        public IReadOnlyList<string> Permissions { get; } = permissions;
        public byte[] Hash { get; } = hash;
        public long Created { get; } = created;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public int Attempts { get; set; }
    }
    private sealed record Paired(RemoteDevice Device, byte[] Hash, long Created);

    // Lokal ejerfunktion; må aldrig eksponeres som en anonym parringstjeneste.
    // Local owner operation; never expose this as an anonymous pairing endpoint.
    public PairingOffer BeginPairing(string deviceName, IEnumerable<string> permissions)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || deviceName.Length > 80 || deviceName.Any(char.IsControl))
            throw new ArgumentException("Ugyldigt enhedsnavn / Invalid device name.", nameof(deviceName));
        ArgumentNullException.ThrowIfNull(permissions);
        var selected = permissions.Take(RemotePermissions.All.Count + 1).ToArray();
        if (selected.Length == 0 || selected.Length > RemotePermissions.All.Count ||
            selected.Any(p => !RemotePermissions.All.Contains(p, StringComparer.Ordinal)) ||
            selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new ArgumentException("Vælg kendte, entydige rettigheder / Select known, unique permissions.", nameof(permissions));
        var immutable = Array.AsReadOnly(selected.Order(StringComparer.Ordinal).ToArray());
        lock (sync)
        {
            Prune();
            if (pending.Count >= MaximumPendingPairings || devices.Count + pending.Count >= MaximumDevices)
                throw new InvalidOperationException("Enhedsgrænsen er nået / Device limit reached.");
            var id = Guid.NewGuid().ToString("D");
            var secret = NewSecret();
            var expiresAt = clock.GetUtcNow() + PairingLifetime;
            pending.Add(id, new Pending(deviceName.Trim(), immutable, Hash(secret), clock.GetTimestamp(), expiresAt));
            return new(id, secret, expiresAt) { Transport = transport };
        }
    }

    public DeviceCredential? CompletePairing(string? pairingId, string? secret)
    {
        lock (sync)
        {
            Prune();
            if (pairingId is null || !pending.TryGetValue(pairingId, out var offer)) return null;
            offer.Attempts++;
            if (!Matches(offer.Hash, secret))
            {
                if (offer.Attempts >= MaximumPairingAttempts) RemovePending(pairingId);
                return null;
            }
            RemovePending(pairingId); // Enkeltbrug, også ved samtidige forsøg / Single use, including concurrent attempts.
            var now = clock.GetUtcNow();
            var device = new RemoteDevice(Guid.NewGuid().ToString("D"), offer.Name, offer.Permissions, now, now + DeviceLifetime);
            var token = NewSecret();
            devices.Add(device.Id, new(device, Hash(token), clock.GetTimestamp()));
            return new(device, token);
        }
    }

    public bool Allows(string? deviceId, string? token, string? permission)
    {
        lock (sync)
        {
            Prune();
            return deviceId is not null && devices.TryGetValue(deviceId, out var entry) &&
                Matches(entry.Hash, token) && permission is not null &&
                entry.Device.Permissions.Contains(permission, StringComparer.Ordinal);
        }
    }

    public bool Authenticates(string deviceId, string token)
    {
        lock (sync)
        {
            Prune();
            return devices.TryGetValue(deviceId, out var entry) && Matches(entry.Hash, token);
        }
    }

    public IReadOnlyList<RemoteDevice> ListDevices()
    {
        lock (sync) { Prune(); return devices.Values.Select(d => d.Device).ToArray(); }
    }

    public int PendingCount { get { lock (sync) { Prune(); return pending.Count; } } }
    public RemoteOwnerSnapshot GetOwnerSnapshot()
    {
        lock (sync)
        {
            Prune();
            return new(transport is not null, devices.Values.Select(d => d.Device).ToArray(),
                pending.Select(p => new PendingPairing(p.Key, p.Value.Name, p.Value.Permissions, p.Value.ExpiresAt)).ToArray());
        }
    }
    public bool CancelPairing(string pairingId) { lock (sync) return RemovePending(pairingId); }
    public bool RevokeDevice(string deviceId) { lock (sync) return RemoveDevice(deviceId); }
    public void RevokeAll()
    {
        lock (sync)
        {
            foreach (var id in pending.Keys.ToArray()) RemovePending(id);
            foreach (var id in devices.Keys.ToArray()) RemoveDevice(id);
        }
    }

    private void Prune()
    {
        var now = clock.GetTimestamp();
        foreach (var item in pending.Where(p => clock.GetElapsedTime(p.Value.Created, now) >= PairingLifetime).ToArray())
            RemovePending(item.Key);
        foreach (var item in devices.Where(p => clock.GetElapsedTime(p.Value.Created, now) >= DeviceLifetime).ToArray())
            RemoveDevice(item.Key);
    }
    private bool RemovePending(string id)
    {
        if (!pending.Remove(id, out var entry)) return false;
        CryptographicOperations.ZeroMemory(entry.Hash);
        return true;
    }
    private bool RemoveDevice(string id)
    {
        if (!devices.Remove(id, out var entry)) return false;
        CryptographicOperations.ZeroMemory(entry.Hash);
        return true;
    }
    private static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
    private static bool Matches(byte[] expected, string? supplied)
    {
        if (supplied is null || supplied.Length != 64 || !supplied.All(char.IsAsciiHexDigit)) return false;
        var actual = Hash(supplied);
        try { return CryptographicOperations.FixedTimeEquals(expected, actual); }
        finally { CryptographicOperations.ZeroMemory(actual); }
    }
}
