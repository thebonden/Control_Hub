using ControlHub.Contracts;

namespace ControlHub.Plugins.Obs;

public sealed class ObsPlugin(IObsClient client) : IHubPlugin
{
    public PluginDescriptor Descriptor { get; } = new("obs", new("OBS Studio", "OBS Studio"), "experimental",
        ["scene.switch", "record.start", "record.stop", "stream.start", "stream.stop", "input.mute"]);
    public async ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        string request;
        object data = new { };
        switch (command.Action)
        {
            case "scene.switch":
                if (!Wire.OnlyArguments(command.Arguments, "sceneName") || !Wire.StringArgument(command.Arguments, "sceneName", out var scene))
                    return Invalid(command);
                request = "SetCurrentProgramScene"; data = new { sceneName = scene }; break;
            case "input.mute":
                if (!Wire.OnlyArguments(command.Arguments, "inputName", "muted") ||
                    !Wire.StringArgument(command.Arguments, "inputName", out var input) ||
                    !command.Arguments.TryGetProperty("muted", out var mute) ||
                    mute.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
                    return Invalid(command);
                request = "SetInputMute"; data = new { inputName = input, inputMuted = mute.GetBoolean() }; break;
            case "record.start": request = "StartRecord"; break;
            case "record.stop": request = "StopRecord"; break;
            case "stream.start": request = "StartStream"; break;
            case "stream.stop": request = "StopStream"; break;
            default: return Wire.Result(command, false, "unsupported_action", "Ukendt OBS-handling.", "Unknown OBS action.");
        }
        if (command.Action is not ("scene.switch" or "input.mute") && !Wire.OnlyArguments(command.Arguments)) return Invalid(command);
        try
        {
            await client.RequestAsync(request, data, cancellationToken);
            return Wire.Result(command, true, "ok", "OBS bekræftede handlingen.", "OBS acknowledged the action.");
        }
        catch (ObsRequestException)
        {
            return Wire.Result(command, false, "obs_rejected", "OBS-forbindelse eller handling afvist. Kontrollér status.",
                "OBS connection or request rejected. Check status.");
        }
    }
    private static CommandResult Invalid(Command command) => Wire.Result(command, false, "invalid_arguments", "Ugyldige OBS-argumenter.", "Invalid OBS arguments.");

    public async ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var scene = await client.RequestAsync("GetCurrentProgramScene", new { }, cancellationToken);
        var recording = await client.RequestAsync("GetRecordStatus", new { }, cancellationToken);
        var streaming = await client.RequestAsync("GetStreamStatus", new { }, cancellationToken);
        return new("obs", "ready", new("OBS tilsluttet.", "OBS connected."), Wire.Data(new { scene, recording, streaming }));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
