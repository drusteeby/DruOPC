namespace OpcPlc;

using System.Net;

/// <summary>
/// Service to provide IP address information.
/// </summary>
public class IpAddressProvider
{
    /// <summary>
    /// Get IP address of first interface, otherwise host name.
    /// </summary>
    public string GetIpAddress()
    {
        string ip = Dns.GetHostName();

        try
        {
            // Ignore System.Net.Internals.SocketExceptionFactory+ExtendedSocketException
            var hostEntry = Dns.GetHostEntry(ip);
            if (hostEntry.AddressList.Length > 0)
            {
                ip = hostEntry.AddressList[0].ToString();
            }
        }
        catch
        {
            // Default to Dns.GetHostName.
        }

        return ip;
    }
}
