using ControlHub.Contracts;

namespace ControlHub.Plugins.AndroidRemote;

// Ærlig kapacitetsstatus: biblioteket er bygget, telefontransporten er ikke.
// Truthful capability status: the library exists, the phone transport does not.
public sealed class AndroidRemotePlugin(RemoteDeviceRegistry? devices = null) : IHubPlugin
{
    public PluginDescriptor Descriptor { get; } = new("androidremote", new("AndroidRemote", "AndroidRemote"), "development", []);
    private static readonly LocalizedText Message = new(
        "Telefonadgang er slået fra. Vælg et netværk før start.",
        "Phone access is off. Select a network before starting.");
    public ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult(
        devices?.IsListening == true
            ? new PluginStatus(Descriptor.Id, "ready", new("Klar til telefonforbindelser.", "Ready for phone connections."),
                Wire.Data(new { paired = devices.ListDevices().Count }))
            : new PluginStatus(Descriptor.Id, "unavailable", Message, Wire.Data(new { reason = "transport_not_configured" })));
    public ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken) => ValueTask.FromResult(
        new CommandResult(HubVersion.Protocol, command.Id, Descriptor.Id, false, "not_implemented", Message));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
