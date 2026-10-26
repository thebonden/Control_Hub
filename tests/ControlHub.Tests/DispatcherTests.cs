using System.Text.Json;
using ControlHub.Contracts;
using ControlHub.Core;
using Xunit;

namespace ControlHub.Tests;

public sealed class DispatcherTests
{
    internal static Command Command(string plugin = "fake", string action = "run", object? args = null)
        => new("1", Guid.NewGuid().ToString("D"), plugin, action, Wire.Data(args ?? new { }));

    [Fact]
    public async Task ConcurrentRetriesExecuteOnlyOnce()
    {
        var plugin = new FakePlugin();
        var dispatcher = new CommandDispatcher([plugin]);
        var command = Command();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => dispatcher.DispatchAsync(command)));
        Assert.Equal(1, plugin.Calls);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.All(results, r => Assert.Equal(command.Id, r.Id));
    }

    [Fact]
    public async Task SameIdWithDifferentPayloadIsRejected()
    {
        var plugin = new FakePlugin();
        var dispatcher = new CommandDispatcher([plugin]);
        var command = Command();
        await dispatcher.DispatchAsync(command);
        var result = await dispatcher.DispatchAsync(command with { Arguments = Wire.Data(new { other = true }) });
        Assert.Equal("id_conflict", result.Code);
        Assert.Equal(1, plugin.Calls);
    }

    [Theory]
    [InlineData("2", "fake", "run", "unsupported_protocol")]
    [InlineData("1", "missing", "run", "unknown_plugin")]
    [InlineData("1", "fake", "save", "unsupported_action")]
    public async Task InvalidRoutesNeverReachPlugin(string protocol, string pluginId, string action, string expected)
    {
        var plugin = new FakePlugin();
        var dispatcher = new CommandDispatcher([plugin]);
        var result = await dispatcher.DispatchAsync(Command(pluginId, action) with { ProtocolVersion = protocol });
        Assert.Equal(expected, result.Code);
        Assert.Equal(0, plugin.Calls);
    }

    [Fact]
    public async Task InvalidEnvelopeNeverReachesPlugin()
    {
        var plugin = new FakePlugin();
        var dispatcher = new CommandDispatcher([plugin]);
        Assert.Equal("invalid_command", (await dispatcher.DispatchAsync(Command() with { Id = "not-a-uuid" })).Code);
        Assert.Equal("invalid_command", (await dispatcher.DispatchAsync(Command() with { Arguments = default })).Code);
        Assert.Equal(0, plugin.Calls);
    }

    [Fact]
    public async Task TimeoutIsUnknownAndNotAutomaticallyRetried()
    {
        var plugin = new FakePlugin { WaitUntilCancelled = true };
        var dispatcher = new CommandDispatcher([plugin], TimeSpan.FromMilliseconds(50));
        var command = Command();
        Assert.Equal("outcome_unknown", (await dispatcher.DispatchAsync(command)).Code);
        Assert.Equal("outcome_unknown", (await dispatcher.DispatchAsync(command)).Code);
        Assert.Equal(1, plugin.Calls);
    }

    [Fact]
    public async Task CancelledBeforeDispatchNeverExecutes()
    {
        var plugin = new FakePlugin();
        var dispatcher = new CommandDispatcher([plugin]);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Equal("cancelled_before_dispatch", (await dispatcher.DispatchAsync(Command(), cancelled.Token)).Code);
        Assert.Equal(0, plugin.Calls);
    }

    [Fact]
    public async Task PluginFaultDoesNotExposeExceptionOrBreakStatus()
    {
        var plugin = new FakePlugin { Fail = true };
        var dispatcher = new CommandDispatcher([plugin]);
        var result = await dispatcher.DispatchAsync(Command());
        Assert.Equal("plugin_error", result.Code);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(result, Wire.Json));
        Assert.Equal("unavailable", (await dispatcher.GetStatusAsync()).Plugins.Single().State);
    }

    [Fact]
    public void WireRoundTripsAndRejectsUnknownFields()
    {
        var command = Command("obs", "scene.switch", new { sceneName = "Scene A" });
        var json = JsonSerializer.Serialize(command, Wire.Json);
        Assert.Contains("\"protocolVersion\"", json);
        Assert.Equal("scene.switch", JsonSerializer.Deserialize<Command>(json, Wire.Json)!.Action);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Command>(json[..^1] + ",\"extra\":true}", Wire.Json));
    }

    [Fact]
    public void DuplicatePluginIdsFailAtStartup()
        => Assert.Throws<ArgumentException>(() => new CommandDispatcher([new FakePlugin(), new FakePlugin()]));

    private sealed class FakePlugin : IHubPlugin
    {
        public int Calls;
        public bool Fail { get; init; }
        public bool WaitUntilCancelled { get; init; }
        public PluginDescriptor Descriptor { get; } = new("fake", new("Test", "Test"), "test", ["run"]);
        public async ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (Fail) throw new InvalidOperationException("secret");
            if (WaitUntilCancelled) await Task.Delay(Timeout.Infinite, cancellationToken);
            await Task.Yield();
            return Wire.Result(command, true, "ok", "OK", "OK");
        }
        public ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken)
            => Fail ? throw new InvalidOperationException("secret") : ValueTask.FromResult(new PluginStatus("fake", "ready", new("OK", "OK")));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
