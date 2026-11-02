using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using ControlHub.Contracts;
using ControlHub.Plugins.Obs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ControlHub.Tests;

public sealed class ObsTransportTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task HandshakeAuthenticationFragmentsAndResponseWork(bool authenticated)
    {
        await using var server = await FakeServer.StartAsync(authenticated);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var data = await new ObsClient(server.Endpoint, authenticated ? "password" : null)
            .RequestAsync("GetCurrentProgramScene", new { }, timeout.Token);
        Assert.Equal("Main", data!.Value.GetProperty("currentProgramSceneName").GetString());
        Assert.Equal("GetCurrentProgramScene", await server.Request.Task.WaitAsync(timeout.Token));
    }

    [Theory]
    [InlineData(true, false)][InlineData(false, true)]
    public async Task RejectsFailedRequestsAndMismatchedCorrelation(bool reject, bool wrongId)
    {
        await using var server = await FakeServer.StartAsync(false, reject, wrongId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var error = await Assert.ThrowsAsync<ObsRequestException>(() =>
            new ObsClient(server.Endpoint, null).RequestAsync("StartRecord", new { }, timeout.Token));
        Assert.Equal(wrongId, error.OutcomeUnknown);
        Assert.Equal(wrongId ? ObsFailure.ProtocolError : ObsFailure.RequestRejected, error.Reason);
    }

    [Fact]
    public async Task MissingPasswordFailsBeforeSendingCommand()
    {
        await using var server = await FakeServer.StartAsync(true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var error = await Assert.ThrowsAsync<ObsRequestException>(() =>
            new ObsClient(server.Endpoint, null).RequestAsync("StartRecord", new { }, timeout.Token));
        Assert.Equal(ObsFailure.PasswordMissing, error.Reason);
        Assert.False(error.OutcomeUnknown);
        Assert.False(server.Request.Task.IsCompleted);
    }

    [Fact]
    public async Task WrongPasswordCloseIsIdentifiedWithoutSendingCommandOrLeakingServerText()
    {
        await using var server = await FakeServer.StartAsync(true, authenticationFailure: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var plugin = new ObsPlugin(new ObsClient(server.Endpoint, "wrong-secret"));
        var status = await plugin.GetStatusAsync(timeout.Token);
        Assert.Equal("authentication_failed", status.Data!.Value.GetProperty("reason").GetString());
        Assert.Contains("adgangskoden", status.Message.Da);
        Assert.Contains("password", status.Message.En);
        Assert.DoesNotContain("private-server-detail", JsonSerializer.Serialize(status));
        Assert.DoesNotContain("wrong-secret", JsonSerializer.Serialize(status));
        Assert.False(server.Request.Task.IsCompleted);
    }

    [Fact]
    public async Task ClosedPortHasConnectionGuidanceAndNoCommandWasSent()
    {
        // Reserve a port with no listener; avoid a fixed port or a release/rebind race.
        using var port = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        port.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new Uri($"ws://127.0.0.1:{((IPEndPoint)port.LocalEndPoint!).Port}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var error = await Assert.ThrowsAsync<ObsRequestException>(() =>
            new ObsClient(endpoint, null).RequestAsync("StartRecord", new { }, timeout.Token));
        Assert.Equal(ObsFailure.ConnectionUnavailable, error.Reason);
        Assert.False(error.OutcomeUnknown);
    }

    [Fact]
    public async Task DisconnectAfterRequestReportsUnknownOutcomeRatherThanRejection()
    {
        await using var server = await FakeServer.StartAsync(false, closeAfterRequest: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var plugin = new ObsPlugin(new ObsClient(server.Endpoint, null));
        var result = await plugin.ExecuteAsync(DispatcherTests.Command("obs", "record.start"), timeout.Token);
        Assert.Equal("outcome_unknown", result.Code);
        Assert.False(result.Success);
        Assert.Equal("StartRecord", await server.Request.Task.WaitAsync(timeout.Token));
        Assert.Equal("connection_lost", result.Data!.Value.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task MalformedHandshakeHasProtocolGuidanceAndSendsNoCommand()
    {
        await using var server = await FakeServer.StartAsync(true, malformedHello: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var error = await Assert.ThrowsAsync<ObsRequestException>(() =>
            new ObsClient(server.Endpoint, "password").RequestAsync("StartRecord", new { }, timeout.Token));
        Assert.Equal(ObsFailure.ProtocolError, error.Reason);
        Assert.False(error.OutcomeUnknown);
        Assert.False(server.Request.Task.IsCompleted);
    }

    private sealed class FakeServer(WebApplication app) : IAsyncDisposable
    {
        public Uri Endpoint => new(app.Urls.Single().Replace("http://", "ws://", StringComparison.Ordinal));
        public TaskCompletionSource<string> Request { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static async Task<FakeServer> StartAsync(bool authenticated, bool reject = false, bool wrongId = false,
            bool authenticationFailure = false, bool closeAfterRequest = false, bool malformedHello = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            var app = builder.Build();
            var server = new FakeServer(app);
            app.UseWebSockets();
            app.Map("/", async context =>
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var ct = context.RequestAborted;
                try
                {
                    if (malformedHello)
                    {
                        await Send(socket, new { op = 0, d = new { authentication = new { salt = "salt" } } }, ct);
                        await Read(socket, ct);
                        return;
                    }
                    await Send(socket, new { op = 0, d = authenticated ?
                        Wire.Data(new { rpcVersion = 1, authentication = new { salt = "salt", challenge = "challenge" } }) :
                        Wire.Data(new { rpcVersion = 1 }) }, ct);
                    var identify = await Read(socket, ct);
                    Assert.Equal(1, identify.GetProperty("op").GetInt32());
                    if (authenticationFailure)
                    {
                        await socket.CloseOutputAsync((WebSocketCloseStatus)4009, "private-server-detail", ct);
                        return;
                    }
                    if (authenticated) Assert.Equal(ObsClient.Authenticate("password", "salt", "challenge"), identify.GetProperty("d").GetProperty("authentication").GetString());
                    await Send(socket, new { op = 2, d = new { negotiatedRpcVersion = 1 } }, ct);
                    var request = (await Read(socket, ct)).GetProperty("d");
                    var requestType = request.GetProperty("requestType").GetString()!;
                    server.Request.TrySetResult(requestType);
                    if (closeAfterRequest)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, "private-server-detail", ct);
                        return;
                    }
                    await Send(socket, new { op = 5, d = new { eventType = "IgnoredEvent" } }, ct);
                    await Send(socket, new { op = 7, d = new {
                        requestType, requestId = wrongId ? "wrong" : request.GetProperty("requestId").GetString(),
                        requestStatus = new { result = !reject, code = reject ? 500 : 100 },
                        responseData = new { currentProgramSceneName = "Main" }
                    } }, ct);
                }
                catch (WebSocketException) { }
                catch (OperationCanceledException) { }
            });
            await app.StartAsync();
            return server;
        }
        private static async Task Send(WebSocket socket, object data, CancellationToken ct)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, Wire.Json);
            var split = bytes.Length / 2;
            await socket.SendAsync(new ArraySegment<byte>(bytes, 0, split), WebSocketMessageType.Text, false, ct);
            await socket.SendAsync(new ArraySegment<byte>(bytes, split, bytes.Length - split), WebSocketMessageType.Text, true, ct);
        }
        private static async Task<JsonElement> Read(WebSocket socket, CancellationToken ct)
        {
            var bytes = new byte[8192];
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), ct);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException();
            using var doc = JsonDocument.Parse(bytes.AsMemory(0, result.Count));
            return doc.RootElement.Clone();
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
