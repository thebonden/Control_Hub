using System.Net;
using System.Net.Http.Json;
using ControlHub.Contracts;

namespace ControlHub.Plugins.AndroidRemote;

public sealed record PendingPairing(string Id, string Name, IReadOnlyList<string> Permissions, DateTimeOffset ExpiresAt);
public sealed record RemoteOwnerSnapshot(bool TransportReady, IReadOnlyList<RemoteDevice> Devices,
    IReadOnlyList<PendingPairing> PendingPairings);
public sealed record BeginPairingRequest(string Name, IReadOnlyList<string> Permissions);

public interface IRemoteOwnerClient
{
    Task<RemoteOwnerSnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task<PairingOffer> BeginAsync(string name, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default);
    Task RemoveAsync(string id, bool pending, CancellationToken cancellationToken = default);
    Task RevokeAllAsync(CancellationToken cancellationToken = default);
}

// Kun ejerens lokale API. Klienten ejer ikke HttpClient eller dens forbindelsesnøgle.
// Local owner API only. This client does not own HttpClient or its connection key.
public sealed class RemoteOwnerClient : IRemoteOwnerClient
{
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly string ownerKey;
    public RemoteOwnerClient(HttpClient client, Uri endpoint, string ownerKey)
    {
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != "http" || endpoint.Host != "127.0.0.1" ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath != "/api/v1/androidremote/owner/")
            throw new ArgumentException("Ugyldig lokal ejeradresse / Invalid local owner address.", nameof(endpoint));
        this.client = client;
        this.endpoint = endpoint;
        this.ownerKey = ownerKey;
    }

    public async Task<RemoteOwnerSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        using var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RemoteOwnerSnapshot>(Wire.Json, cancellationToken)
            ?? throw new InvalidDataException("Status mangler / Missing status.");
    }

    public async Task<PairingOffer> BeginAsync(string name, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "pairings"))
        { Content = JsonContent.Create(new BeginPairingRequest(name, permissions), options: Wire.Json) };
        using var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PairingOffer>(Wire.Json, cancellationToken)
            ?? throw new InvalidDataException("Parringsbevis mangler / Missing pairing offer.");
    }

    public async Task RemoveAsync(string id, bool pending, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(id, "D", out var guid)) throw new ArgumentException("Ugyldigt id / Invalid ID.", nameof(id));
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(endpoint,
            (pending ? "pairings/" : "devices/") + guid.ToString("D")));
        using var response = await SendAsync(request, cancellationToken, allowMissing: true);
    }

    public async Task RevokeAllAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(endpoint, "access"));
        using var response = await SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken, bool allowMissing = false)
    {
        // Bevidst ingen retry. Efter en mistet oprettelsesrespons skal ejeren opdatere listen.
        // Deliberately no retry. After losing a creation response the owner must refresh the list.
        request.Headers.Add("X-ControlHub-Owner", ownerKey);
        var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode || (allowMissing && response.StatusCode == HttpStatusCode.NotFound)) return response;
        response.Dispose();
        throw new HttpRequestException("Ejerhandlingen mislykkedes. Opdater listen. / Owner operation failed. Refresh the list.", null, response.StatusCode);
    }
}
