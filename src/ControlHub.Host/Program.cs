using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using ControlHub.Contracts;
using ControlHub.Core;
using ControlHub.Plugins.Obs;
using ControlHub.Plugins.Planned;
using ControlHub.Plugins.PowerPoint;
using Microsoft.AspNetCore.RateLimiting;

var token = Environment.GetEnvironmentVariable("CONTROLHUB_TOKEN");
if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
    throw new InvalidOperationException("CONTROLHUB_TOKEN skal være mindst 32 tegn / CONTROLHUB_TOKEN must be at least 32 characters.");
var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes("Bearer " + token));
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, 5187);
    options.Limits.MaxRequestBodySize = 16 * 1024;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddConcurrencyLimiter("local", limiter => { limiter.PermitLimit = 8; limiter.QueueLimit = 0; limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = Wire.Json.UnmappedMemberHandling;
    options.SerializerOptions.MaxDepth = 16;
});
var plugins = new IHubPlugin[]
{
    new PowerPointPlugin(),
    new ObsPlugin(new ObsClient(new Uri(Environment.GetEnvironmentVariable("CONTROLHUB_OBS_URL") ?? "ws://127.0.0.1:4455"),
        Environment.GetEnvironmentVariable("CONTROLHUB_OBS_PASSWORD"))),
    new PlannedPlugin("androidremote", new("AndroidRemote", "AndroidRemote")),
    new PlannedPlugin("audio", new("Lyd", "Audio")),
    new PlannedPlugin("logitech", new("Logitech", "Logitech")),
    new PlannedPlugin("android-live", new("Android Live", "Android Live")),
    new PlannedPlugin("physical-remote", new("Fysisk fjernkontrol", "Physical remote")),
    new PlannedPlugin("flux-keyboard", new("Flux Keyboard", "Flux Keyboard"))
};
var dispatcher = new CommandDispatcher(plugins);
var app = builder.Build();
app.Use(async (context, next) =>
{
    // Reject browser-originated requests; Stream Deck uses its Node runtime.
    // Afvis browserkald; Stream Deck benytter sin Node-runtime.
    if (context.Request.Headers.ContainsKey("Origin")) { context.Response.StatusCode = 403; return; }
    var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString()));
    if (!CryptographicOperations.FixedTimeEquals(tokenHash, supplied))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { code = "unauthorized", message = new LocalizedText("Adgang afvist.", "Access denied.") });
        return;
    }
    await next();
});
app.UseRateLimiter();
app.MapGet("/api/v1/plugins", () => dispatcher.Descriptors).RequireRateLimiting("local");
app.MapGet("/api/v1/status", (CancellationToken ct) => dispatcher.GetStatusAsync(ct)).RequireRateLimiting("local");
app.MapPost("/api/v1/commands", async (HttpRequest request, CancellationToken ct) =>
{
    Command? command;
    try { command = await JsonSerializer.DeserializeAsync<Command>(request.Body, Wire.Json, ct); }
    catch (JsonException) { return Results.BadRequest(new { code = "invalid_json", message = new LocalizedText("Ugyldig JSON.", "Invalid JSON.") }); }
    if (command is null) return Results.BadRequest(new { code = "invalid_command", message = new LocalizedText("Kommando mangler.", "Missing command.") });
    return Results.Json(await dispatcher.DispatchAsync(command, ct), Wire.Json);
}).RequireRateLimiting("local");
try { await app.RunAsync(); }
finally { foreach (var plugin in plugins) await plugin.DisposeAsync(); }
