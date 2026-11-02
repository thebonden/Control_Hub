using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlHub.Contracts;

public static class HubVersion
{
    public const string Display = "0.0.001";
    public const string Protocol = "1";
}

public sealed record LocalizedText(string Da, string En);
public sealed record Command(string ProtocolVersion, string Id, string Plugin, string Action, JsonElement Arguments);
public sealed record CommandResult(string ProtocolVersion, string Id, string Plugin, bool Success,
    string Code, LocalizedText Message, JsonElement? Data = null);
public sealed record PluginStatus(string Id, string State, LocalizedText Message, JsonElement? Data = null);
public sealed record PluginDescriptor(string Id, LocalizedText Name, string Maturity, IReadOnlyList<string> Actions);
public sealed record HubStatus(string ProtocolVersion, string Version, DateTimeOffset ObservedAt,
    IReadOnlyList<PluginStatus> Plugins);

public interface IHubPlugin : IAsyncDisposable
{
    PluginDescriptor Descriptor { get; }
    ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken);
    ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken);
}

public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };
    public static JsonElement Data(object value) => JsonSerializer.SerializeToElement(value, Json);
    public static CommandResult Result(Command command, bool success, string code, string da, string en, object? data = null)
        => new(HubVersion.Protocol, command.Id, command.Plugin, success, code, new(da, en),
            data is null ? null : Data(data));
    public static bool OnlyArguments(JsonElement args, params string[] allowed)
        => args.ValueKind == JsonValueKind.Object &&
           args.EnumerateObject().All(p => allowed.Contains(p.Name, StringComparer.Ordinal)) &&
           args.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == args.EnumerateObject().Count();
    public static bool StringArgument(JsonElement args, string name, out string value)
    {
        value = "";
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var item) ||
            item.ValueKind != JsonValueKind.String) return false;
        value = item.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 256;
    }
}
