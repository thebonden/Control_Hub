using ControlHub.Contracts;

namespace ControlHub.Plugins.Obs;

public enum ObsFailure { RequestRejected, ConnectionUnavailable, PasswordMissing, AuthenticationFailed, ProtocolError, ConnectionLost }

public sealed class ObsRequestException(ObsFailure reason = ObsFailure.RequestRejected, bool outcomeUnknown = false) : Exception
{
    public ObsFailure Reason { get; } = reason;
    public bool OutcomeUnknown { get; } = outcomeUnknown;
}

internal static class ObsFailureInfo
{
    public static string Code(ObsFailure reason) => reason switch
    {
        ObsFailure.ConnectionUnavailable => "connection_unavailable",
        ObsFailure.PasswordMissing => "password_missing",
        ObsFailure.AuthenticationFailed => "authentication_failed",
        ObsFailure.ProtocolError => "protocol_error",
        ObsFailure.ConnectionLost => "connection_lost",
        _ => "request_rejected"
    };

    // Kun faste beskeder; aldrig serverens fejltekst eller adgangskode.
    // Fixed messages only; never server-provided error text or credentials.
    public static LocalizedText Message(ObsFailure reason) => reason switch
    {
        ObsFailure.ConnectionUnavailable => new("Kan ikke forbinde til OBS. Start OBS, aktivér WebSocket-serveren, og kontrollér porten.",
            "Cannot connect to OBS. Start OBS, enable its WebSocket server and check the port."),
        ObsFailure.PasswordMissing => new("OBS kræver en adgangskode. Indtast den i Control Hub, og start forbindelsen igen.",
            "OBS requires a password. Enter it in Control Hub and restart the connection."),
        ObsFailure.AuthenticationFailed => new("OBS afviste adgangskoden. Kontrollér den i Control Hub, og start forbindelsen igen.",
            "OBS rejected the password. Check it in Control Hub and restart the connection."),
        ObsFailure.ProtocolError => new("Svaret var ikke kompatibelt med OBS WebSocket v5. Kontrollér server og port.",
            "The reply was not compatible with OBS WebSocket v5. Check the server and port."),
        ObsFailure.ConnectionLost => new("OBS-forbindelsen blev afbrudt. Kontrollér status før en ny handling.",
            "The OBS connection was interrupted. Check status before another action."),
        _ => new("OBS afviste handlingen. Kontrollér scenenavn, lydkilde og aktuel status.",
            "OBS rejected the request. Check the scene name, audio input and current status.")
    };
}
