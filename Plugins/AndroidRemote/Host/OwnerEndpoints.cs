using System.Text.Json;
using System.Security.Cryptography;
using System.Net;
using System.Text;
using ControlHub.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ControlHub.Plugins.AndroidRemote.Host;

public static class OwnerEndpoints
{
    // Skal placeres bag hostens ejer-godkendelse, Origin-afvisning og størrelsesgrænse.
    // Must sit behind host owner authentication, Origin rejection and body-size limits.
    public static void MapAndroidRemoteOwner(this IEndpointRouteBuilder routes, RemoteDeviceRegistry devices, string ownerKey)
    {
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(ownerKey));
        var group = routes.MapGroup("/api/v1/androidremote/owner").RequireRateLimiting("local");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            if (context.HttpContext.Connection.RemoteIpAddress is not { } peer || !IPAddress.IsLoopback(peer))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            var proof = context.HttpContext.Request.Headers["X-ControlHub-Owner"];
            if (proof.Count != 1 || !CryptographicOperations.FixedTimeEquals(expected,
                SHA256.HashData(Encoding.UTF8.GetBytes(proof.ToString()))))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await next(context);
        });
        group.MapGet("/", () => Results.Json(devices.GetOwnerSnapshot(), Wire.Json));
        group.MapPost("/pairings", async (HttpRequest request, CancellationToken ct) =>
        {
            try
            {
                using var document = await JsonDocument.ParseAsync(request.Body, new JsonDocumentOptions { MaxDepth = 16 }, ct);
                var root = document.RootElement;
                if (!Wire.OnlyArguments(root, "name", "permissions") ||
                    !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
                    !root.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Array ||
                    permissions.GetArrayLength() > RemotePermissions.All.Count ||
                    permissions.EnumerateArray().Any(p => p.ValueKind != JsonValueKind.String)) return Invalid();
                var offer = devices.BeginPairing(name.GetString()!, permissions.EnumerateArray().Select(p => p.GetString()!));
                return Results.Json(offer, Wire.Json, statusCode: StatusCodes.Status201Created);
            }
            catch (JsonException) { return Invalid(); }
            catch (ArgumentException) { return Invalid(); }
            catch (InvalidOperationException)
            {
                return Results.Json(new { code = "capacity_reached", message = new LocalizedText(
                    "Fjern en gammel parring eller enhed først.", "Remove an old pairing or device first.") }, Wire.Json, statusCode: 409);
            }
        });
        group.MapDelete("/pairings/{id}", (string id) => Remove(id, devices.CancelPairing));
        group.MapDelete("/devices/{id}", (string id) => Remove(id, devices.RevokeDevice));
        group.MapDelete("/access", () => { devices.RevokeAll(); return Results.NoContent(); });
    }

    private static IResult Remove(string id, Func<string, bool> remove)
    {
        if (!Guid.TryParseExact(id, "D", out var guid)) return Invalid();
        return remove(guid.ToString("D")) ? Results.NoContent() : Results.NotFound();
    }
    private static IResult Invalid() => Results.BadRequest(new { code = "invalid_pairing", message = new LocalizedText(
        "Vælg et gyldigt navn og mindst én kendt rettighed.", "Choose a valid name and at least one known permission.") });
}
