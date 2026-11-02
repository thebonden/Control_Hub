using System.Text.Json;
using ControlHub.Contracts;
using ControlHub.Plugins.AndroidRemote;
using Xunit;

namespace ControlHub.Tests;

public sealed class RemoteOwnerTests
{
    [Fact]
    public void InvitationBecomesPairedDeviceWithoutRevealingEitherSecret()
    {
        var store = new RemoteDeviceRegistry();
        var invite = store.BeginPairing("Test phone", [RemotePermissions.Scenes]);
        var pending = Assert.Single(store.GetOwnerSnapshot().PendingPairings);
        Assert.Equal(invite.Id, pending.Id);
        Assert.Equal(invite.ExpiresAt, pending.ExpiresAt);
        Assert.Equal(RemotePermissions.Scenes, Assert.Single(pending.Permissions));
        var paired = store.CompletePairing(invite.Id, invite.Secret)!;
        var snapshot = store.GetOwnerSnapshot();
        Assert.Empty(snapshot.PendingPairings);
        Assert.False(snapshot.TransportReady);
        Assert.Equal(paired.Device.Id, Assert.Single(snapshot.Devices).Id);
        var serialized = JsonSerializer.Serialize(snapshot, Wire.Json);
        Assert.DoesNotContain(paired.Token, serialized);
        Assert.DoesNotContain(invite.Secret, serialized);
        store.RevokeDevice(paired.Device.Id);
        Assert.Empty(store.GetOwnerSnapshot().Devices);
    }

    [Theory]
    [InlineData("http://192.168.1.2/api/v1/androidremote/owner/")]
    [InlineData("http://127.0.0.1/api/v1/commands")]
    [InlineData("http://user@127.0.0.1/api/v1/androidremote/owner/")]
    [InlineData("http://127.0.0.1/api/v1/androidremote/owner/?key=test")]
    [InlineData("http://127.0.0.1/api/v1/androidremote/owner/#fragment")]
    public void OwnerClientRefusesUnexpectedCredentialDestinations(string endpoint)
    {
        using var http = new HttpClient();
        Assert.Throws<ArgumentException>(() => new RemoteOwnerClient(http, new Uri(endpoint), "test"));
    }

    [Fact]
    public async Task LostInvitationResponseIsNotAutomaticallyRepeated()
    {
        using var handler = new LostResponse();
        using var http = new HttpClient(handler);
        var client = new RemoteOwnerClient(http, new Uri("http://127.0.0.1:5187/api/v1/androidremote/owner/"), "test");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.BeginAsync("Phone", ["status.read"]));
        Assert.Equal(1, handler.Posts);
    }
    private sealed class LostResponse : HttpMessageHandler
    {
        public int Posts;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Interlocked.Increment(ref Posts);
            throw new HttpRequestException("Simulated lost response");
        }
    }
}
