using ControlHub.Contracts;

namespace ControlHub.Plugins.Planned;

// Explicit capability placeholders. They never report successful device execution.
// Eksplicitte pladsholdere. De rapporterer aldrig vellykket enhedsstyring.
public sealed class PlannedPlugin(string id, LocalizedText name) : IHubPlugin
{
    public PluginDescriptor Descriptor { get; } = new(id, name, "planned", []);
    public ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken)
        => ValueTask.FromResult(Wire.Result(command, false, "not_implemented", "Integrationen er planlagt.", "Integration is planned."));
    public ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(new PluginStatus(Descriptor.Id, "not_implemented", new("Integrationen er planlagt.", "Integration is planned.")));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
