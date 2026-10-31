using System.Text.Json;
using ControlHub.Contracts;
using ControlHub.Plugins.Obs;
using ControlHub.Plugins.Planned;
using ControlHub.Plugins.PowerPoint;
using Xunit;

namespace ControlHub.Tests;

public sealed class PluginTests
{
    [Theory]
    [InlineData("next")][InlineData("previous")][InlineData("black")][InlineData("resume")]
    public async Task PowerPointRoutesOnlySlideshowOperations(string action)
    {
        var session = new FakeSlideShow();
        await using var plugin = new PowerPointPlugin(session);
        Assert.True((await plugin.ExecuteAsync(DispatcherTests.Command("powerpoint", action), default)).Success);
        Assert.Equal(action, session.Action);
    }

    [Theory]
    [InlineData("{}")][InlineData("{\"slide\":0}")][InlineData("{\"slide\":-1}")]
    [InlineData("{\"slide\":1.5}")][InlineData("{\"slide\":\"2\"}")]
    [InlineData("{\"slide\":1,\"path\":\"x.pptx\"}")]
    [InlineData("{\"slide\":1,\"slide\":2}")]
    public async Task InvalidGoToNeverCallsPowerPoint(string args)
    {
        var session = new FakeSlideShow();
        await using var plugin = new PowerPointPlugin(session);
        using var document = JsonDocument.Parse(args);
        var result = await plugin.ExecuteAsync(DispatcherTests.Command("powerpoint", "goto") with { Arguments = document.RootElement.Clone() }, default);
        Assert.Equal("invalid_arguments", result.Code);
        Assert.Null(session.Action);
    }

    [Fact]
    public async Task GoToAndStatusUseSessionAndSurfaceUnavailability()
    {
        var session = new FakeSlideShow();
        await using var plugin = new PowerPointPlugin(session);
        Assert.True((await plugin.ExecuteAsync(DispatcherTests.Command("powerpoint", "goto", new { slide = 3 }), default)).Success);
        Assert.Equal(3, session.Slide);
        Assert.Equal("ready", (await plugin.GetStatusAsync(default)).State);
        session.Unavailable = true;
        Assert.Equal("slideshow_unavailable", (await plugin.ExecuteAsync(DispatcherTests.Command("powerpoint", "next"), default)).Code);
        Assert.Equal("unavailable", (await plugin.GetStatusAsync(default)).State);
    }

    [Theory]
    [InlineData("save")][InlineData("open")][InlineData("edit")][InlineData("close")]
    public async Task PresentationFileOperationsAreNotSupported(string action)
    {
        var session = new FakeSlideShow();
        await using var plugin = new PowerPointPlugin(session);
        Assert.Equal("unsupported_action", (await plugin.ExecuteAsync(DispatcherTests.Command("powerpoint", action), default)).Code);
        Assert.Null(session.Action);
    }

    [Theory]
    [InlineData("record.start", "StartRecord")][InlineData("record.stop", "StopRecord")]
    [InlineData("stream.start", "StartStream")][InlineData("stream.stop", "StopStream")]
    public async Task ObsActionsMapToExplicitRequests(string action, string request)
    {
        var client = new FakeObsClient();
        await using var plugin = new ObsPlugin(client);
        Assert.True((await plugin.ExecuteAsync(DispatcherTests.Command("obs", action), default)).Success);
        Assert.Equal(request, client.Request);
    }

    [Fact]
    public async Task ObsSceneAndMuteValidateAndMapArguments()
    {
        var client = new FakeObsClient();
        await using var plugin = new ObsPlugin(client);
        Assert.Equal("invalid_arguments", (await plugin.ExecuteAsync(DispatcherTests.Command("obs", "scene.switch", new { sceneName = "" }), default)).Code);
        Assert.Null(client.Request);
        Assert.True((await plugin.ExecuteAsync(DispatcherTests.Command("obs", "scene.switch", new { sceneName = "Main" }), default)).Success);
        Assert.Equal("SetCurrentProgramScene", client.Request);
        Assert.Equal("Main", client.Data.GetProperty("sceneName").GetString());
        Assert.True((await plugin.ExecuteAsync(DispatcherTests.Command("obs", "input.mute", new { inputName = "Mic", muted = false }), default)).Success);
        Assert.False(client.Data.GetProperty("inputMuted").GetBoolean());
    }

    [Fact]
    public async Task PlannedIntegrationCannotPretendToExecute()
    {
        await using var plugin = new PlannedPlugin("androidremote", new("AndroidRemote", "AndroidRemote"));
        Assert.Empty(plugin.Descriptor.Actions);
        Assert.Equal("not_implemented", (await plugin.GetStatusAsync(default)).State);
        Assert.False((await plugin.ExecuteAsync(DispatcherTests.Command("androidremote"), default)).Success);
    }

    [Theory]
    [InlineData("ws://192.168.1.2:4455")][InlineData("wss://127.0.0.1:4455")]
    [InlineData("ws://user:secret@localhost:4455")][InlineData("ws://localhost:4455/path")]
    public void ObsRejectsNonlocalOrAmbiguousEndpoints(string uri)
        => Assert.Throws<ArgumentException>(() => new ObsClient(new Uri(uri), null));

    [Fact]
    public void ObsAuthenticationMatchesIndependentSha256Fixture()
        => Assert.Equal("zTM5ki6L2vVvBQiTG9ckH1Lh64AbnCf6XZ226UmnkIA=",
            ObsClient.Authenticate("password", "salt", "challenge"));

    private sealed class FakeSlideShow : ISlideShowSession
    {
        public string? Action;
        public int? Slide;
        public bool Unavailable;
        public Task ApplyAsync(string action, int? slide, CancellationToken cancellationToken)
        {
            if (Unavailable) throw new SlideShowUnavailableException();
            Action = action; Slide = slide; return Task.CompletedTask;
        }
        public Task<SlideShowSnapshot> ReadAsync(CancellationToken cancellationToken)
            => Unavailable ? throw new SlideShowUnavailableException() : Task.FromResult(new SlideShowSnapshot(3, 10, 1));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FakeObsClient : IObsClient
    {
        public string? Request;
        public JsonElement Data;
        public Task<JsonElement?> RequestAsync(string requestType, object requestData, CancellationToken cancellationToken)
        {
            Request = requestType; Data = Wire.Data(requestData);
            return Task.FromResult<JsonElement?>(null);
        }
    }
}
