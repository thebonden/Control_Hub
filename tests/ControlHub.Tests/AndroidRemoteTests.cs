using System.Text.Json;
using ControlHub.Contracts;
using ControlHub.Core;
using ControlHub.Plugins.AndroidRemote;
using Xunit;

namespace ControlHub.Tests;

public sealed class AndroidRemoteTests
{
    [Fact]
    public void PairingIsSingleUseAndCredentialsDoNotAppearInDeviceListsOrToString()
    {
        var registry = new RemoteDeviceRegistry();
        var offer = registry.BeginPairing(" Telefon ", [RemotePermissions.Status, RemotePermissions.PowerPoint]);
        Assert.Equal(64, offer.Secret.Length);
        Assert.DoesNotContain(offer.Secret, offer.ToString()!);
        var paired = Assert.IsType<DeviceCredential>(registry.CompletePairing(offer.Id, offer.Secret));
        Assert.Equal("Telefon", paired.Device.Name);
        Assert.NotEqual(offer.Secret, paired.Token);
        Assert.DoesNotContain(paired.Token, paired.ToString()!);
        Assert.Null(registry.CompletePairing(offer.Id, offer.Secret));
        Assert.False(registry.Allows(paired.Device.Id, offer.Secret, RemotePermissions.Status));
        Assert.True(registry.Allows(paired.Device.Id, paired.Token, RemotePermissions.Status));
        Assert.False(registry.Allows(paired.Device.Id, paired.Token, RemotePermissions.Streaming));
        var list = JsonSerializer.Serialize(registry.ListDevices(), Wire.Json);
        Assert.DoesNotContain(offer.Secret, list);
        Assert.DoesNotContain(paired.Token, list);
        Assert.Equal(0, registry.PendingCount);
    }

    [Fact]
    public async Task SimultaneousPairingAttemptsIssueExactlyOneCredential()
    {
        var registry = new RemoteDeviceRegistry();
        var offer = registry.BeginPairing("Phone", [RemotePermissions.Status]);
        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => registry.CompletePairing(offer.Id, offer.Secret))));
        Assert.Single(results.OfType<DeviceCredential>());
        Assert.Single(registry.ListDevices());
    }

    [Fact]
    public void FailedAttemptsExhaustOnlyTheTargetPairing()
    {
        var registry = new RemoteDeviceRegistry();
        var offer = registry.BeginPairing("Phone", [RemotePermissions.Status]);
        var other = registry.BeginPairing("Other", [RemotePermissions.Status]);
        for (var i = 0; i < RemoteDeviceRegistry.MaximumPairingAttempts; i++)
            Assert.Null(registry.CompletePairing(offer.Id, i % 2 == 0 ? "bad" : new string('0', 64)));
        Assert.Null(registry.CompletePairing(offer.Id, offer.Secret));
        Assert.NotNull(registry.CompletePairing(other.Id, other.Secret));
        Assert.Null(registry.CompletePairing(null, null));
    }

    [Fact]
    public void PairingAndDeviceExpireAtTheirBoundariesDespiteWallClockChanges()
    {
        var clock = new TestClock();
        var registry = new RemoteDeviceRegistry(clock);
        var paired = Pair(registry, RemotePermissions.Status);
        var offer = registry.BeginPairing("Expired", [RemotePermissions.Status]);
        clock.Advance(RemoteDeviceRegistry.PairingLifetime - TimeSpan.FromTicks(1));
        Assert.Equal(1, registry.PendingCount);
        clock.Advance(TimeSpan.FromTicks(1));
        clock.Utc -= TimeSpan.FromDays(1);
        Assert.Null(registry.CompletePairing(offer.Id, offer.Secret));
        Assert.True(registry.Allows(paired.Device.Id, paired.Token, RemotePermissions.Status));
        clock.Advance(RemoteDeviceRegistry.DeviceLifetime - RemoteDeviceRegistry.PairingLifetime);
        Assert.False(registry.Allows(paired.Device.Id, paired.Token, RemotePermissions.Status));
        Assert.Empty(registry.ListDevices());
    }

    [Fact]
    public void OwnerCanCancelPairingRevokeOneDeviceAndRevokeEverything()
    {
        var registry = new RemoteDeviceRegistry();
        var first = Pair(registry, RemotePermissions.Status);
        var second = Pair(registry, RemotePermissions.Status);
        var offer = registry.BeginPairing("Cancel", [RemotePermissions.Status]);
        Assert.True(registry.CancelPairing(offer.Id));
        Assert.False(registry.CancelPairing(offer.Id));
        Assert.Null(registry.CompletePairing(offer.Id, offer.Secret));
        Assert.True(registry.RevokeDevice(first.Device.Id));
        Assert.False(registry.RevokeDevice(first.Device.Id));
        Assert.False(registry.Allows(first.Device.Id, first.Token, RemotePermissions.Status));
        Assert.True(registry.Allows(second.Device.Id, second.Token, RemotePermissions.Status));
        var pending = registry.BeginPairing("Pending", [RemotePermissions.Status]);
        registry.RevokeAll();
        Assert.False(registry.Allows(second.Device.Id, second.Token, RemotePermissions.Status));
        Assert.Null(registry.CompletePairing(pending.Id, pending.Secret));
        Assert.Empty(registry.ListDevices());
        Assert.Equal(0, registry.PendingCount);
        Assert.False(new RemoteDeviceRegistry().Allows(second.Device.Id, second.Token, RemotePermissions.Status));
    }

    [Fact]
    public void RegistriesHaveBoundedCapacityAndReservePendingDeviceSlots()
    {
        var clock = new TestClock();
        var registry = new RemoteDeviceRegistry(clock);
        var pending = Enumerable.Range(0, RemoteDeviceRegistry.MaximumPendingPairings)
            .Select(_ => registry.BeginPairing("Phone", [RemotePermissions.Status])).ToArray();
        Assert.Throws<InvalidOperationException>(() => registry.BeginPairing("Overflow", [RemotePermissions.Status]));
        foreach (var offer in pending) Assert.NotNull(registry.CompletePairing(offer.Id, offer.Secret));
        for (var i = pending.Length; i < RemoteDeviceRegistry.MaximumDevices; i++) Pair(registry, RemotePermissions.Status);
        Assert.Throws<InvalidOperationException>(() => registry.BeginPairing("Overflow", [RemotePermissions.Status]));
        clock.Advance(RemoteDeviceRegistry.DeviceLifetime);
        Assert.NotNull(registry.BeginPairing("Fresh", [RemotePermissions.Status]));
    }

    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("Phone\nName")]
    public void InvalidDeviceNamesCannotOpenPairing(string name)
        => Assert.Throws<ArgumentException>(() => new RemoteDeviceRegistry().BeginPairing(name, [RemotePermissions.Status]));

    [Fact]
    public void OwnerPermissionsAreValidatedAndCannotBeMutatedAfterPairing()
    {
        var registry = new RemoteDeviceRegistry();
        Assert.Throws<ArgumentException>(() => registry.BeginPairing(new string('x', 81), [RemotePermissions.Status]));
        Assert.Throws<ArgumentException>(() => registry.BeginPairing("Phone", []));
        Assert.Throws<ArgumentException>(() => registry.BeginPairing("Phone", ["*"]));
        Assert.Throws<ArgumentException>(() => registry.BeginPairing("Phone", [RemotePermissions.Status, RemotePermissions.Status]));
        string[] permissions = [RemotePermissions.Status];
        var offer = registry.BeginPairing("Phone", permissions);
        permissions[0] = RemotePermissions.Streaming;
        var paired = registry.CompletePairing(offer.Id, offer.Secret)!;
        Assert.Throws<NotSupportedException>(() => ((IList<string>)paired.Device.Permissions)[0] = RemotePermissions.Streaming);
        Assert.True(registry.Allows(paired.Device.Id, paired.Token, RemotePermissions.Status));
        Assert.False(registry.Allows(paired.Device.Id, paired.Token, RemotePermissions.Streaming));
    }

    [Theory]
    [InlineData(null)][InlineData("")][InlineData("invalid")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public void BadTokensCannotAuthenticate(string? token)
    {
        var registry = new RemoteDeviceRegistry();
        var paired = Pair(registry, RemotePermissions.Status);
        Assert.False(registry.Allows(paired.Device.Id, token, RemotePermissions.Status));
        Assert.False(registry.Allows("unknown", paired.Token, RemotePermissions.Status));
        Assert.False(registry.Allows(null, paired.Token, RemotePermissions.Status));
    }

    [Theory]
    [InlineData("powerpoint", "next", RemotePermissions.PowerPoint)]
    [InlineData("powerpoint", "previous", RemotePermissions.PowerPoint)]
    [InlineData("powerpoint", "goto", RemotePermissions.PowerPoint)]
    [InlineData("powerpoint", "black", RemotePermissions.PowerPoint)]
    [InlineData("powerpoint", "resume", RemotePermissions.PowerPoint)]
    [InlineData("obs", "scene.switch", RemotePermissions.Scenes)]
    [InlineData("obs", "record.start", RemotePermissions.Recording)]
    [InlineData("obs", "record.stop", RemotePermissions.Recording)]
    [InlineData("obs", "stream.start", RemotePermissions.Streaming)]
    [InlineData("obs", "stream.stop", RemotePermissions.Streaming)]
    [InlineData("obs", "input.mute", RemotePermissions.Audio)]
    public async Task CommandsRequireTheirSpecificOwnerGrantedPermission(string plugin, string action, string permission)
    {
        var registry = new RemoteDeviceRegistry();
        var target = new RecordingPlugin(plugin, action);
        var gateway = new RemoteCommandGateway(registry, new CommandDispatcher([target]));
        var readOnly = Pair(registry, RemotePermissions.Status);
        var authorized = Pair(registry, permission);
        var command = DispatcherTests.Command(plugin, action);
        var denied = await gateway.DispatchAsync(readOnly.Device.Id, readOnly.Token, command);
        Assert.Equal("access_denied", denied.Code);
        Assert.False(denied.Success);
        Assert.Equal(0, target.Calls);
        Assert.True((await gateway.DispatchAsync(authorized.Device.Id, authorized.Token, command)).Success);
        Assert.Equal(1, target.Calls);
    }

    [Theory]
    [InlineData("powerpoint", "save")][InlineData("powerpoint", "open")]
    [InlineData("powerpoint", "NEXT")][InlineData("obs", "request")]
    [InlineData("androidremote", "pair")][InlineData("other", "next")]
    public async Task UnknownActionsRemainDeniedEvenWithEveryRemotePermission(string plugin, string action)
    {
        var registry = new RemoteDeviceRegistry();
        var paired = Pair(registry, RemotePermissions.All.ToArray());
        var target = new RecordingPlugin(plugin, action);
        var gateway = new RemoteCommandGateway(registry, new CommandDispatcher([target]));
        Assert.Equal("access_denied", (await gateway.DispatchAsync(paired.Device.Id, paired.Token, DispatcherTests.Command(plugin, action))).Code);
        Assert.Equal(0, target.Calls);
    }

    [Fact]
    public async Task DuplicateResultsAreIsolatedByPairingAndPreserveClientCorrelation()
    {
        var registry = new RemoteDeviceRegistry();
        var first = Pair(registry, RemotePermissions.PowerPoint);
        var second = Pair(registry, RemotePermissions.PowerPoint);
        var target = new RecordingPlugin();
        var dispatcher = new CommandDispatcher([target]);
        var gateway = new RemoteCommandGateway(registry, dispatcher);
        var command = DispatcherTests.Command("powerpoint", "next");
        await dispatcher.DispatchAsync(command);
        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => gateway.DispatchAsync(first.Device.Id, first.Token, command)));
        Assert.All(results, r => { Assert.True(r.Success); Assert.Equal(command.Id, r.Id); });
        Assert.Equal(2, target.Calls); // Lokalt + første enhed / Local + first device.
        var uppercase = command with { Id = command.Id.ToUpperInvariant() };
        Assert.Equal(uppercase.Id, (await gateway.DispatchAsync(first.Device.Id, first.Token, uppercase)).Id);
        Assert.Equal("id_conflict", (await gateway.DispatchAsync(first.Device.Id, first.Token,
            command with { Arguments = Wire.Data(new { changed = true }) })).Code);
        Assert.True((await gateway.DispatchAsync(second.Device.Id, second.Token, command)).Success);
        Assert.Equal(3, target.Calls);
        registry.RevokeDevice(first.Device.Id);
        Assert.Equal("access_denied", (await gateway.DispatchAsync(first.Device.Id, first.Token, command)).Code);
        Assert.Equal(3, target.Calls);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RevokedOrExpiredDeviceCannotExecuteAfterWaitingBehindLocalCommand(bool expire)
    {
        var clock = new TestClock();
        var registry = new RemoteDeviceRegistry(clock);
        var paired = Pair(registry, RemotePermissions.PowerPoint);
        var target = new RecordingPlugin { BlockCommands = true };
        var dispatcher = new CommandDispatcher([target]);
        var local = dispatcher.DispatchAsync(DispatcherTests.Command("powerpoint", "next"));
        await target.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var gateway = new RemoteCommandGateway(registry, dispatcher);
        var remote = gateway.DispatchAsync(paired.Device.Id, paired.Token, DispatcherTests.Command("powerpoint", "next"));
        Assert.False(remote.IsCompleted);
        if (expire) clock.Advance(RemoteDeviceRegistry.DeviceLifetime);
        else registry.RevokeDevice(paired.Device.Id);
        target.Release.TrySetResult();
        Assert.True((await local).Success);
        Assert.Equal("access_denied", (await remote).Code);
        Assert.Equal(1, target.Calls);
    }

    [Fact]
    public async Task TimeoutReturnsUnknownAndDoesNotReplayTheCommand()
    {
        var registry = new RemoteDeviceRegistry();
        var paired = Pair(registry, RemotePermissions.PowerPoint);
        var target = new RecordingPlugin { BlockCommands = true };
        var gateway = new RemoteCommandGateway(registry, new CommandDispatcher([target], TimeSpan.FromMilliseconds(100)));
        var command = DispatcherTests.Command("powerpoint", "next");
        Assert.Equal("outcome_unknown", (await gateway.DispatchAsync(paired.Device.Id, paired.Token, command)).Code);
        Assert.Equal("outcome_unknown", (await gateway.DispatchAsync(paired.Device.Id, paired.Token, command)).Code);
        Assert.Equal(1, target.Calls);
    }

    [Fact]
    public async Task CoreValidationAndCancellationStillApplyToAuthorizedDevices()
    {
        var registry = new RemoteDeviceRegistry();
        var paired = Pair(registry, RemotePermissions.PowerPoint);
        var target = new RecordingPlugin();
        var gateway = new RemoteCommandGateway(registry, new CommandDispatcher([target]));
        var command = DispatcherTests.Command("powerpoint", "next");
        Assert.Equal("invalid_command", (await gateway.DispatchAsync(paired.Device.Id, paired.Token, command with { Id = "bad" })).Code);
        Assert.Equal("unsupported_protocol", (await gateway.DispatchAsync(paired.Device.Id, paired.Token, command with { ProtocolVersion = "2" })).Code);
        Assert.Equal("invalid_command", (await gateway.DispatchAsync(paired.Device.Id, paired.Token, command with { Arguments = default })).Code);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Equal("cancelled_before_dispatch", (await gateway.DispatchAsync(paired.Device.Id, paired.Token, command, cancelled.Token)).Code);
        Assert.Equal(0, target.Calls);
    }

    [Fact]
    public async Task StatusNeedsItsOwnPermissionAndRechecksRevocationAfterReading()
    {
        var registry = new RemoteDeviceRegistry();
        var slidesOnly = Pair(registry, RemotePermissions.PowerPoint);
        var reader = Pair(registry, RemotePermissions.Status);
        var target = new RecordingPlugin { BlockStatus = true };
        var gateway = new RemoteCommandGateway(registry, new CommandDispatcher([target]));
        Assert.False((await gateway.GetStatusAsync(slidesOnly.Device.Id, slidesOnly.Token)).Success);
        Assert.Equal(0, target.StatusCalls);
        var reading = gateway.GetStatusAsync(reader.Device.Id, reader.Token);
        await target.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        registry.RevokeDevice(reader.Device.Id);
        target.Release.TrySetResult();
        var denied = await reading;
        Assert.False(denied.Success);
        Assert.Null(denied.Status);
        var next = Pair(registry, RemotePermissions.Status);
        var status = await gateway.GetStatusAsync(next.Device.Id, next.Token);
        Assert.True(status.Success);
        Assert.Equal("ready", status.Status!.Plugins.Single().State);
        Assert.DoesNotContain(next.Token, JsonSerializer.Serialize(status, Wire.Json));
    }

    [Fact]
    public async Task CatalogNeverClaimsThatThePhoneTransportIsReady()
    {
        await using var plugin = new AndroidRemotePlugin();
        var dispatcher = new CommandDispatcher([plugin]);
        Assert.Empty(plugin.Descriptor.Actions);
        Assert.Equal("development", plugin.Descriptor.Maturity);
        var status = (await dispatcher.GetStatusAsync()).Plugins.Single();
        Assert.Equal("unavailable", status.State);
        Assert.Equal("transport_not_configured", status.Data!.Value.GetProperty("reason").GetString());
        Assert.NotEqual(status.Message.Da, status.Message.En);
        Assert.False((await dispatcher.DispatchAsync(DispatcherTests.Command("androidremote", "pair"))).Success);
    }

    private static DeviceCredential Pair(RemoteDeviceRegistry registry, params string[] permissions)
    {
        var offer = registry.BeginPairing("Test phone", permissions);
        return Assert.IsType<DeviceCredential>(registry.CompletePairing(offer.Id, offer.Secret));
    }

    private sealed class TestClock : TimeProvider
    {
        private long timestamp;
        public DateTimeOffset Utc = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Utc;
        public override long GetTimestamp() => timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan elapsed) { timestamp += elapsed.Ticks; Utc += elapsed; }
    }

    private sealed class RecordingPlugin(string id = "powerpoint", string action = "next") : IHubPlugin
    {
        public int Calls;
        public int StatusCalls;
        public bool BlockCommands;
        public bool BlockStatus;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public PluginDescriptor Descriptor { get; } = new(id, new("Test", "Test"), "test", [action]);
        public async ValueTask<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            if (BlockCommands) await Release.Task.WaitAsync(cancellationToken);
            return Wire.Result(command, true, "ok", "Bekræftet.", "Confirmed.");
        }
        public async ValueTask<PluginStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref StatusCalls);
            Entered.TrySetResult();
            if (BlockStatus) await Release.Task.WaitAsync(cancellationToken);
            return new(Descriptor.Id, "ready", new("Klar.", "Ready."));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
