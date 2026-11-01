using ControlHub.Contracts;

namespace ControlHub.Plugins.AndroidRemote;

// Ærlig kapacitetsstatus: biblioteket er bygget, telefontransporten er ikke.
// Truthful capability status: the library exists, the phone transport does not.
public sealed class AndroidRemotePlugin : IHubPlugin
{
    public PluginDescriptor Descriptor { get; } = new("androidremote", new("AndroidRemote", "AndroidRemote"), "development", []);
    private static readonly LocalizedText Message = new(
        "Parring og adgangskontrol er forberedt. Telefonforbindelse er endnu ikke implementeret.",
        "Pairing and access control are prepared. Phone connectivity is not implemented yet.");
    public ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult(
        new PluginStatus(Descriptor.Id, "unavailable", Message, Wire.Data(new { reason = "transport_not_configured" })));
    public ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken) => ValueTask.FromResult(
        new CommandResult(HubVersion.Protocol, command.Id, Descriptor.Id, false, "not_implemented", Message));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
