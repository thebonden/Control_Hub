using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlHub.Contracts;

namespace ControlHub.Plugins.Obs;

public interface IObsClient
{
    Task<JsonElement?> RequestAsync(string requestType, object requestData, CancellationToken cancellationToken);
}

public sealed class ObsRequestException : Exception;
public sealed class ObsClient : IObsClient
{
    private readonly Uri endpoint;
    private readonly string? password;
    public ObsClient(Uri endpoint, string? password)
    {
        if (!endpoint.IsLoopback || endpoint.Scheme != "ws" || !string.IsNullOrEmpty(endpoint.UserInfo) ||
            endpoint.AbsolutePath != "/" || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new ArgumentException("OBS must use a local ws:// endpoint / OBS skal bruge en lokal ws://-adresse.", nameof(endpoint));
        this.endpoint = endpoint;
        this.password = password;
    }

    public static string Authenticate(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    public async Task<JsonElement?> RequestAsync(string requestType, object requestData, CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(endpoint, cancellationToken);
        var hello = await ReceiveAsync(socket, cancellationToken);
        if (hello.GetProperty("op").GetInt32() != 0) throw new ObsRequestException();
        var identify = new Dictionary<string, object> { ["rpcVersion"] = 1, ["eventSubscriptions"] = 0 };
        if (hello.GetProperty("d").TryGetProperty("authentication", out var auth))
        {
            if (string.IsNullOrEmpty(password)) throw new ObsRequestException();
            identify["authentication"] = Authenticate(password, auth.GetProperty("salt").GetString()!, auth.GetProperty("challenge").GetString()!);
        }
        await SendAsync(socket, new { op = 1, d = identify }, cancellationToken);
        var identified = await ReceiveAsync(socket, cancellationToken);
        if (identified.GetProperty("op").GetInt32() != 2 ||
            identified.GetProperty("d").GetProperty("negotiatedRpcVersion").GetInt32() != 1) throw new ObsRequestException();
        var requestId = Guid.NewGuid().ToString("D");
        await SendAsync(socket, new { op = 6, d = new { requestType, requestId, requestData } }, cancellationToken);
        while (true)
        {
            var response = await ReceiveAsync(socket, cancellationToken);
            if (response.GetProperty("op").GetInt32() == 5) continue;
            if (response.GetProperty("op").GetInt32() != 7) throw new ObsRequestException();
            var data = response.GetProperty("d");
            if (data.GetProperty("requestId").GetString() != requestId ||
                data.GetProperty("requestType").GetString() != requestType) throw new ObsRequestException();
            if (!data.GetProperty("requestStatus").GetProperty("result").GetBoolean()) throw new ObsRequestException();
            return data.TryGetProperty("responseData", out var body) ? body.Clone() : null;
        }
    }

    private static Task SendAsync(WebSocket socket, object message, CancellationToken cancellationToken)
        => socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message, Wire.Json)),
            WebSocketMessageType.Text, true, cancellationToken);

    private static async Task<JsonElement> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType != WebSocketMessageType.Text || stream.Length + result.Count > 1_048_576)
                throw new ObsRequestException();
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        using var document = JsonDocument.Parse(stream.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        return document.RootElement.Clone();
    }
}
