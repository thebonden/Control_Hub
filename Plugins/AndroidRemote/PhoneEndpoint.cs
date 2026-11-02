using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ControlHub.Plugins.AndroidRemote;

public sealed record PhoneEndpoint(string Endpoint, string CertificateSha256)
{
    public static bool IsPrivateAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168);
    }

    public static IPAddress[] LocalAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
        .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
        .Select(item => item.Address).Where(IsPrivateAddress).Distinct().ToArray();
}
