using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace TaskFlow.Api.Security;

public static class ProxyConfiguration
{
    public static IServiceCollection AddTrustedProxyHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        // The framework shortcut installs middleware before ours and trusts all peers.
        if (configuration.GetValue<bool>("ForwardedHeaders_Enabled"))
            throw new InvalidOperationException(
                "Use ReverseProxy configuration instead of the automatic forwarded-headers switch.");

        var enabled = configuration.GetValue<bool>("ReverseProxy:Enabled");
        var addresses = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        var proxies = new List<IPAddress>();
        foreach (var address in addresses)
        {
            if (!IPAddress.TryParse(address, out var proxy)
                || proxy.Equals(IPAddress.Any) || proxy.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("ReverseProxy:KnownProxies must contain specific IP addresses.");
            proxies.Add(proxy);
        }
        if (enabled && proxies.Count == 0)
            throw new InvalidOperationException("ReverseProxy requires at least one explicitly trusted proxy.");

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = enabled
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
                // Kestrel dual-mode sockets can report IPv4 peers as mapped IPv6.
                if (proxy.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    options.KnownProxies.Add(proxy.MapToIPv6());
            }
        });
        return services;
    }
}
