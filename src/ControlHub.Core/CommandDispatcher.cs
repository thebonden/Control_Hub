using System.Text.Json;
using ControlHub.Contracts;

namespace ControlHub.Core;

// Explicit registration only: loading arbitrary DLLs would cross the trust boundary.
// Kun eksplicit registrering: vilkårlige DLL-filer må ikke indlæses.
public sealed class CommandDispatcher
{
    private readonly Dictionary<string, IHubPlugin> plugins;
    private readonly SemaphoreSlim gate = new(1);
    private readonly Dictionary<string, (string Fingerprint, CommandResult Result)> recent = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();
    private readonly TimeSpan timeout;
    public IReadOnlyList<PluginDescriptor> Descriptors => plugins.Values.Select(p => p.Descriptor).ToArray();

    public CommandDispatcher(IEnumerable<IHubPlugin> plugins, TimeSpan? timeout = null)
    {
        this.plugins = plugins.ToDictionary(p => p.Descriptor.Id, StringComparer.Ordinal);
        this.timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<CommandResult> DispatchAsync(Command command, CancellationToken cancellationToken = default)
    {
        if (command.ProtocolVersion != HubVersion.Protocol)
            return Wire.Result(command, false, "unsupported_protocol", "Ukendt protokolversion.", "Unsupported protocol version.");
        if (!Guid.TryParseExact(command.Id, "D", out _) || string.IsNullOrWhiteSpace(command.Plugin) ||
            string.IsNullOrWhiteSpace(command.Action) || command.Arguments.ValueKind != JsonValueKind.Object)
            return Wire.Result(command, false, "invalid_command", "Ugyldig kommando.", "Invalid command.");
        if (!plugins.TryGetValue(command.Plugin, out var plugin))
            return Wire.Result(command, false, "unknown_plugin", "Ukendt plugin.", "Unknown plugin.");
        if (!plugin.Descriptor.Actions.Contains(command.Action, StringComparer.Ordinal))
            return Wire.Result(command, false, "unsupported_action", "Handlingen understøttes ikke.", "Unsupported action.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try { await gate.WaitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            return Wire.Result(command, false, "cancelled_before_dispatch", "Ikke afsendt.", "Not dispatched.");
        }
        try
        {
            var key = Guid.Parse(command.Id).ToString("D");
            var fingerprint = JsonSerializer.Serialize(new { command.Plugin, command.Action, command.Arguments }, Wire.Json);
            if (recent.TryGetValue(key, out var previous))
                return previous.Fingerprint == fingerprint ? previous.Result :
                    Wire.Result(command, false, "id_conflict", "Id er allerede brugt til en anden kommando.", "Id already belongs to a different command.");
            CommandResult result;
            try { result = await plugin.ExecuteAsync(command, deadline.Token); }
            catch (OperationCanceledException)
            {
                result = Wire.Result(command, false, "outcome_unknown",
                    "Tidsgrænse eller afbrydelse. Kontrollér status før en ny handling.",
                    "Timed out or cancelled. Check status before another action.");
            }
            catch (Exception)
            {
                result = Wire.Result(command, false, "plugin_error",
                    "Pluginfejl. Kontrollér status før en ny handling.", "Plugin error. Check status before another action.");
            }
            recent[key] = (fingerprint, result);
            order.Enqueue(key);
            if (order.Count > 1024) recent.Remove(order.Dequeue());
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<HubStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new List<PluginStatus>();
        foreach (var plugin in plugins.Values)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            try { status.Add(await plugin.GetStatusAsync(deadline.Token)); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                status.Add(new(plugin.Descriptor.Id, "unavailable", new("Status fik timeout.", "Status timed out.")));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                status.Add(new(plugin.Descriptor.Id, "unavailable", new("Status kunne ikke læses.", "Status could not be read.")));
            }
        }
        return new(HubVersion.Protocol, HubVersion.Display, DateTimeOffset.UtcNow, status);
    }
}
