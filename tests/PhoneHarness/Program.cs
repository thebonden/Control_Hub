using System.Net;
using System.Text.Json;
using ControlHub.Contracts;
using ControlHub.Core;
using ControlHub.Plugins.AndroidRemote;
using ControlHub.Plugins.AndroidRemote.Host;
using ControlHub.Plugins.PowerPoint;
using ControlHub.Plugins.Obs;

// The production adapters act on isolated fixtures. No Office, OBS or presentation file is opened.
var slides = new TestShow();
var obs = new TestObs();
await using var ppt = new PowerPointPlugin(slides);
await using var studio = new ObsPlugin(obs);
var devices = new RemoteDeviceRegistry();
await using var server = await PhoneServer.StartAsync(IPAddress.Loopback, 0, devices, new CommandDispatcher([ppt, studio]));
while (await Console.In.ReadLineAsync() is string line)
{
    object response;
    switch (line)
    {
        case "invite":
            response = devices.BeginPairing("Android test", ["status.read", "powerpoint.control", "obs.scenes"]);
            break;
        case "counts":
            response = new { slides = slides.Calls, scenes = obs.Switches, slide = slides.Position, scene = obs.Scene };
            break;
        case "revoke":
            devices.RevokeAll();
            response = new { revoked = true };
            break;
        default: return;
    }
    Console.WriteLine(JsonSerializer.Serialize(response, Wire.Json));
}

sealed class TestShow : ISlideShowSession
{
    public int Position = 1;
    public int Calls;
    public Task ApplyAsync(string action, int? slide, CancellationToken ct)
    {
        Position += action switch { "next" => 1, "previous" => -1, _ => throw new InvalidOperationException() };
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
    public Task<SlideShowSnapshot> ReadAsync(CancellationToken ct) => Task.FromResult(new SlideShowSnapshot(Position, 4, 1));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class TestObs : IObsClient
{
    public string Scene = "Slides";
    public int Switches;
    public Task<JsonElement?> RequestAsync(string type, object data, CancellationToken ct)
    {
        if (type == "SetCurrentProgramScene")
        {
            Scene = Wire.Data(data).GetProperty("sceneName").GetString()!;
            Interlocked.Increment(ref Switches);
            return Task.FromResult<JsonElement?>(null);
        }
        return Task.FromResult<JsonElement?>(type == "GetCurrentProgramScene" ? Wire.Data(new { currentProgramSceneName = Scene })
            : Wire.Data(new { outputActive = false }));
    }
}
