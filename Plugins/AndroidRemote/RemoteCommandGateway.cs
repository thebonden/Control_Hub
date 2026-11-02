using System.Security.Cryptography;
using System.Text;
using ControlHub.Contracts;
using ControlHub.Core;

namespace ControlHub.Plugins.AndroidRemote;

public sealed record RemoteStatusResult(bool Success, string Code, LocalizedText Message, HubStatus? Status = null);

// Transportuafhængig adgangskontrol. Kaldes kun bag en fremtidig godkendt TLS-transport.
// Transport-independent access control. Invoke only behind a future approved TLS transport.
public sealed class RemoteCommandGateway(RemoteDeviceRegistry devices, CommandDispatcher dispatcher)
{
    public async Task<CommandResult> DispatchAsync(string deviceId, string token, Command command,
        CancellationToken cancellationToken = default)
    {
        var permission = RemotePermissions.ForCommand(command.Plugin, command.Action);
        bool Authorized() => devices.Allows(deviceId, token, permission);
        if (!Authorized()) return Denied(command);
        // Hver parret enhed har sit eget UUID-navnerum; lokale/andre enheders svar kan ikke hentes.
        // Each pairing has its own UUID namespace; local/other device results cannot be retrieved.
        var forwarded = command;
        if (Guid.TryParseExact(command.Id, "D", out var id))
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes("androidremote\0" + deviceId + "\0" + id.ToString("D")));
            forwarded = command with { Id = new Guid(digest.AsSpan(0, 16)).ToString("D") };
        }
        var result = await dispatcher.DispatchAsync(forwarded, cancellationToken, Authorized);
        return result with { Id = command.Id };
    }

    public async Task<RemoteStatusResult> GetStatusAsync(string deviceId, string token,
        CancellationToken cancellationToken = default)
    {
        bool Authorized() => devices.Allows(deviceId, token, RemotePermissions.Status);
        if (!Authorized()) return DeniedStatus();
        var status = await dispatcher.GetStatusAsync(cancellationToken);
        // Tilbagekaldelse/udløb under læsning må heller ikke frigive status.
        // Revocation/expiry during the read must not release status either.
        return Authorized() ? new(true, "ok", new("Status hentet.", "Status retrieved."), status) : DeniedStatus();
    }

    private static CommandResult Denied(Command command) => Wire.Result(command, false, "access_denied",
        "Enheden har ikke adgang til handlingen. Kontrollér parring og rettigheder.",
        "This device cannot use this action. Check pairing and permissions.");
    private static RemoteStatusResult DeniedStatus() => new(false, "access_denied",
        new("Enheden har ikke adgang til status. Kontrollér parring og rettigheder.",
            "This device cannot read status. Check pairing and permissions."));
}
