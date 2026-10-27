using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace LANType.Service.Network;

public record NetworkEndpointInfo(string Name, string Description, IPAddress IpAddress, bool IsLikelyHotspot);

public class NetworkInterfaceManager
{
    private readonly ILogger<NetworkInterfaceManager> _logger;

    public NetworkInterfaceManager(ILogger<NetworkInterfaceManager> logger)
    {
        _logger = logger;
    }

    public List<NetworkEndpointInfo> GetAvailableEndpoints()
    {
        var endpoints = new List<NetworkEndpointInfo>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up || 
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProps = ni.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var ip = unicast.Address;
                    // Filter out link-local APIPA 169.254.x.x
                    if (ip.ToString().StartsWith("169.254."))
                    {
                        continue;
                    }

                    // Windows Mobile Hotspot commonly uses 192.168.137.1 or has Microsoft Wi-Fi Direct Virtual Adapter
                    bool isHotspot = ip.ToString() == "192.168.137.1" || 
                                     ni.Description.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase) ||
                                     ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase);

                    endpoints.Add(new NetworkEndpointInfo(ni.Name, ni.Description, ip, isHotspot));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate network interfaces");
        }

        return endpoints;
    }

    private IPAddress? _lastLoggedIp;

    public IPAddress GetPreferredEndpointAddress()
    {
        var endpoints = GetAvailableEndpoints();
        if (endpoints.Count == 0)
        {
            if (_lastLoggedIp != IPAddress.Loopback)
            {
                _logger.LogWarning("No active network endpoints found, defaulting to loopback 127.0.0.1");
                _lastLoggedIp = IPAddress.Loopback;
            }
            return IPAddress.Loopback;
        }

        // Prioritize Hotspot IP
        var hotspot = endpoints.FirstOrDefault(e => e.IsLikelyHotspot);
        if (hotspot != null)
        {
            if (!hotspot.IpAddress.Equals(_lastLoggedIp))
            {
                _logger.LogInformation("Selected hotspot endpoint: {IP} ({Desc})", hotspot.IpAddress, hotspot.Description);
                _lastLoggedIp = hotspot.IpAddress;
            }
            return hotspot.IpAddress;
        }

        // Otherwise pick the first non-loopback IP (e.g. Wi-Fi)
        var primary = endpoints[0];
        if (!primary.IpAddress.Equals(_lastLoggedIp))
        {
            _logger.LogInformation("Selected primary network endpoint: {IP} ({Desc})", primary.IpAddress, primary.Description);
            _lastLoggedIp = primary.IpAddress;
        }
        return primary.IpAddress;
    }
}
