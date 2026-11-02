namespace ControlHub.Plugins.AndroidRemote;

// Rettigheder vælges af ejeren, aldrig af telefonens kommando.
// Permissions are selected by the owner, never by a phone's command.
public static class RemotePermissions
{
    public const string Status = "status.read";
    public const string PowerPoint = "powerpoint.control";
    public const string Scenes = "obs.scenes";
    public const string Recording = "obs.recording";
    public const string Streaming = "obs.streaming";
    public const string Audio = "obs.audio";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
        { Status, PowerPoint, Scenes, Recording, Streaming, Audio });

    public static string? ForCommand(string plugin, string action) => (plugin, action) switch
    {
        ("powerpoint", "next" or "previous" or "goto" or "black" or "resume") => PowerPoint,
        ("obs", "scene.switch") => Scenes,
        ("obs", "record.start" or "record.stop") => Recording,
        ("obs", "stream.start" or "stream.stop") => Streaming,
        ("obs", "input.mute") => Audio,
        _ => null
    };
}
