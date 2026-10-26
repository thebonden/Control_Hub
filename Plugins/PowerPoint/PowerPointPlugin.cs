using ControlHub.Contracts;

namespace ControlHub.Plugins.PowerPoint;

public sealed record SlideShowSnapshot(int Position, int SlideCount, int State);
public interface ISlideShowSession : IAsyncDisposable
{
    Task<SlideShowSnapshot> ReadAsync(CancellationToken cancellationToken);
    Task ApplyAsync(string action, int? slide, CancellationToken cancellationToken);
}

public sealed class PowerPointPlugin : IHubPlugin
{
    private readonly ISlideShowSession? session;
    public PluginDescriptor Descriptor { get; } = new("powerpoint", new("PowerPoint", "PowerPoint"), "experimental",
        ["next", "previous", "goto", "black", "resume"]);
    public PowerPointPlugin(ISlideShowSession? session = null)
    {
        this.session = session ?? (OperatingSystem.IsWindows() ? new ComSlideShowSession() : null);
    }

    public async ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        if (!Descriptor.Actions.Contains(command.Action))
            return Wire.Result(command, false, "unsupported_action", "Ukendt handling.", "Unknown action.");
        int? slide = null;
        if (command.Action == "goto")
        {
            if (!Wire.OnlyArguments(command.Arguments, "slide") ||
                !command.Arguments.TryGetProperty("slide", out var element) ||
                element.ValueKind != System.Text.Json.JsonValueKind.Number || !element.TryGetInt32(out var number) || number < 1)
                return Wire.Result(command, false, "invalid_arguments", "Slide skal være et positivt heltal.", "Slide must be a positive integer.");
            slide = number;
        }
        else if (!Wire.OnlyArguments(command.Arguments))
            return Wire.Result(command, false, "invalid_arguments", "Handlingen tager ingen argumenter.", "This action takes no arguments.");
        if (session is null)
            return Wire.Result(command, false, "unsupported_platform", "PowerPoint COM kræver Windows.", "PowerPoint COM requires Windows.");
        try
        {
            await session.ApplyAsync(command.Action, slide, cancellationToken);
            return Wire.Result(command, true, "ok", "Slideshow-kommando udført.", "Slideshow command completed.");
        }
        catch (SlideShowUnavailableException)
        {
            return Wire.Result(command, false, "slideshow_unavailable",
                "Start præcis ét PowerPoint-slideshow manuelt.", "Start exactly one PowerPoint slideshow manually.");
        }
        catch (ArgumentOutOfRangeException)
        {
            return Wire.Result(command, false, "invalid_arguments", "Slide ligger uden for præsentationen.", "Slide is outside the presentation.");
        }
    }

    public async ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (session is null) return new("powerpoint", "unsupported", new("Kræver Windows.", "Requires Windows."));
        try
        {
            var snapshot = await session.ReadAsync(cancellationToken);
            return new("powerpoint", "ready", new("Slideshow tilsluttet.", "Slideshow connected."), Wire.Data(snapshot));
        }
        catch (SlideShowUnavailableException)
        {
            return new("powerpoint", "unavailable", new("Intet entydigt slideshow.", "No unambiguous slideshow."));
        }
    }

    public ValueTask DisposeAsync() => session?.DisposeAsync() ?? ValueTask.CompletedTask;
}
public sealed class SlideShowUnavailableException : Exception;
