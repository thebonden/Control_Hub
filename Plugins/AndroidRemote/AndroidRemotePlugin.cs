using ControlHub.Contracts;

namespace ControlHub.Plugins.AndroidRemote;

// Ærlig kapacitetsstatus: biblioteket er bygget, telefontransporten er ikke.
// Truthful capability status: the library exists, the phone transport does not.
public sealed class AndroidRemotePlugin : IHubPlugin
{
    public PluginDescriptor Descriptor { get; } = new("androidremote", new("AndroidRemote", "AndroidRemote"), "development", []);
    private static readonly LocalizedText Message = new(
        "Telefonadgang kan administreres i Control Hub. Telefonforbindelsen er endnu ikke klar.",
        "Phone access can be managed in Control Hub. Phone connectivity is not available yet.");
    public ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult(
        new PluginStatus(Descriptor.Id, "unavailable", Message, Wire.Data(new { reason = "transport_not_configured" })));
    public ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken) => ValueTask.FromResult(
        new CommandResult(HubVersion.Protocol, command.Id, Descriptor.Id, false, "not_implemented", Message));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
