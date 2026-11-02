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
        catch (SlideShowUnavailableException error)
        {
            var message = FailureMessage(error.Reason);
            return Wire.Result(command, false, "slideshow_unavailable",
                message.Da, message.En, new { reason = FailureCode(error.Reason) });
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
        catch (SlideShowUnavailableException error)
        {
            return new("powerpoint", "unavailable", FailureMessage(error.Reason), Wire.Data(new { reason = FailureCode(error.Reason) }));
        }
    }

    private static string FailureCode(SlideShowFailure reason) => reason switch
    {
        SlideShowFailure.NotRegistered => "powerpoint_not_found",
        SlideShowFailure.NoSlideshow => "no_slideshow",
        SlideShowFailure.MultipleSlideshows => "multiple_slideshows",
        _ => "powerpoint_unavailable"
    };

    private static LocalizedText FailureMessage(SlideShowFailure reason) => reason switch
    {
        SlideShowFailure.NotRegistered => new("PowerPoint kunne ikke findes. Åbn PowerPoint og start ét slideshow; vent, til programmet er klar.",
            "PowerPoint could not be found. Open PowerPoint and start one slideshow; wait until it is ready."),
        SlideShowFailure.NoSlideshow => new("PowerPoint er åbent, men intet slideshow kører. Start visningen i PowerPoint.",
            "PowerPoint is open, but no slideshow is running. Start the slideshow in PowerPoint."),
        SlideShowFailure.MultipleSlideshows => new("Flere slideshows kører. Afslut de ekstra visninger, så kun ét slideshow er aktivt.",
            "Multiple slideshows are running. End the extra shows so exactly one remains active."),
        _ => new("PowerPoint-forbindelsen er utilgængelig. Luk eventuelle dialoger i PowerPoint og kontrollér status igen.",
            "The PowerPoint connection is unavailable. Close any PowerPoint dialogs and check status again.")
    };

    public ValueTask DisposeAsync() => session?.DisposeAsync() ?? ValueTask.CompletedTask;
}
public enum SlideShowFailure { ConnectionUnavailable, NotRegistered, NoSlideshow, MultipleSlideshows }
public sealed class SlideShowUnavailableException(SlideShowFailure reason = SlideShowFailure.ConnectionUnavailable) : Exception
{
    public SlideShowFailure Reason { get; } = reason;
}
