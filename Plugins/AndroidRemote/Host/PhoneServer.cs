using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.RateLimiting;
using ControlHub.Contracts;
using ControlHub.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlHub.Plugins.AndroidRemote.Host;

// A separate HTTPS application has only the three phone routes. Owner routes stay on the desktop host.
// Denne HTTPS-app har kun tre telefonruter. Ejerruterne tilhører den lokale host.
public sealed class PhoneServer : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly X509Certificate2 certificate;
    private readonly RemoteDeviceRegistry devices;
    public PhoneEndpoint Endpoint { get; }
    private PhoneServer(WebApplication app, X509Certificate2 certificate, RemoteDeviceRegistry devices)
    {
        this.app = app;
        this.certificate = certificate;
        this.devices = devices;
        Endpoint = new(app.Urls.Single(), certificate.GetCertHashString(HashAlgorithmName.SHA256));
        devices.SetTransport(Endpoint);
    }

    public static async Task<PhoneServer> StartAsync(IPAddress address, int port, RemoteDeviceRegistry devices,
        CommandDispatcher dispatcher, CancellationToken ct = default)
    {
        if (!PhoneEndpoint.IsPrivateAddress(address) && !address.Equals(IPAddress.Loopback))
            throw new ArgumentException("Vælg en privat IPv4-adresse / Choose a private IPv4 address.");
        var certificate = CreateCertificate(address);
        WebApplication? app = null;
        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Configuration.Sources.Clear();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(server =>
            {
                server.AddServerHeader = false;
                server.Limits.MaxConcurrentConnections = 32;
                server.Limits.MaxRequestBodySize = 16 * 1024;
                server.Limits.MaxRequestHeadersTotalSize = 8192;
                server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
                server.Listen(address, port, listen => listen.UseHttps(tls =>
                {
                    tls.ServerCertificate = certificate;
                    tls.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                }));
            });
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetConcurrencyLimiter("phones", _ => new() { PermitLimit = 8, QueueLimit = 0 }));
                options.AddFixedWindowLimiter("redeem", limits =>
                { limits.PermitLimit = 24; limits.Window = TimeSpan.FromMinutes(1); limits.QueueLimit = 0; });
                options.AddFixedWindowLimiter("operations", limits =>
                { limits.PermitLimit = 240; limits.Window = TimeSpan.FromMinutes(1); limits.QueueLimit = 0; });
            });
            app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                if (context.Request.Headers.ContainsKey("Origin")) { context.Response.StatusCode = 403; return; }
                await next();
            });
            app.UseRateLimiter();
            var gateway = new RemoteCommandGateway(devices, dispatcher);
            app.MapPost("/phone/v1/pair", async (HttpRequest request, CancellationToken cancellation) =>
            {
                try
                {
                    using var body = await JsonDocument.ParseAsync(request.Body, new JsonDocumentOptions { MaxDepth = 4 }, cancellation);
                    var data = body.RootElement;
                    if (!Wire.OnlyArguments(data, "id", "secret") || !Wire.StringArgument(data, "id", out var id) ||
                        !Wire.StringArgument(data, "secret", out var secret)) return Results.BadRequest();
                    var credential = devices.CompletePairing(id, secret);
                    return credential is null ? Results.Unauthorized() : Results.Json(credential, Wire.Json);
                }
                catch (JsonException) { return Results.BadRequest(); }
            }).RequireRateLimiting("redeem");
            app.MapGet("/phone/v1/status", async (HttpRequest request, CancellationToken cancellation) =>
            {
                if (!GetProof(request, devices, out var id, out var token)) return Results.Unauthorized();
                var result = await gateway.GetStatusAsync(id, token, cancellation);
                return Results.Json(result, Wire.Json, statusCode: result.Success ? 200 : 403);
            }).RequireRateLimiting("operations");
            app.MapPost("/phone/v1/commands", async (HttpRequest request, CancellationToken cancellation) =>
            {
                if (!GetProof(request, devices, out var id, out var token)) return Results.Unauthorized();
                try
                {
                    var command = await JsonSerializer.DeserializeAsync<Command>(request.Body, Wire.Json, cancellation);
                    if (command is null) return Results.BadRequest();
                    var result = await gateway.DispatchAsync(id, token, command, cancellation);
                    return Results.Json(result, Wire.Json, statusCode: result.Code == "access_denied" ? 403 : 200);
                }
                catch (JsonException) { return Results.BadRequest(); }
            }).RequireRateLimiting("operations");
            await app.StartAsync(ct);
            return new PhoneServer(app, certificate, devices);
        }
        catch
        {
            if (app is not null) await app.DisposeAsync();
            certificate.Dispose();
            throw;
        }
    }

    private static bool GetProof(HttpRequest request, RemoteDeviceRegistry devices, out string id, out string token)
    {
        id = request.Headers["X-ControlHub-Device"].ToString();
        var authorization = request.Headers.Authorization.ToString();
        token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : "";
        return id.Length == 36 && devices.Authenticates(id, token);
    }

    private static X509Certificate2 CreateCertificate(IPAddress address)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Control Hub AndroidRemote", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(address);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
        // Windows Schannel needs an imported key to serve TLS. No certificate store installation.
        var pfx = ephemeral.Export(X509ContentType.Pkcs12);
        try { return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.DefaultKeySet); }
        finally { CryptographicOperations.ZeroMemory(pfx); }
    }

    public async ValueTask DisposeAsync()
    {
        devices.SetTransport(null);
        try { await app.StopAsync(); }
        finally { await app.DisposeAsync(); certificate.Dispose(); }
    }
}
