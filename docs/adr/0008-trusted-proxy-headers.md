# ADR 0008: Explicit proxy trust

Status: Accepted; observed Azure ingress verified on 2026-09-09.

Azure terminates TLS and forwards traffic to the container. Forwarded client
addresses affect both IP rate limiters; accepting arbitrary client-supplied values
would let callers change their rate-limit partition.

Use ASP.NET Core's Forwarded Headers middleware before the request pipeline.
`ReverseProxy:Enabled` defaults to false. Enabling it requires explicit IP addresses
in `ReverseProxy:KnownProxies`. Clear implicit loopback/network defaults and add
only configured peers (including their IPv4-mapped equivalents). Accept
X-Forwarded-For and X-Forwarded-Proto, process one rightmost hop, and never accept
forwarded hosts. Reject the framework's automatic trust-all configuration switch.

The operator must verify the actual upstream peer and that it appends/replaces
client-address and scheme headers. Do not guess Azure addresses from app inbound,
outbound or VNet integration IPs, and do not trust all private networks. An address
change requires updating configuration; until then headers are ignored and clients
may share the proxy's rate budget. Multi-hop proxy chains need a separate review.

Tests use the real middleware and authentication rate-limit policy to check trusted
and untrusted peers, disabled behavior, IPv4-mapped peers, forged address prefixes,
separate client budgets, scheme handling, unchanged hosts and invalid configuration.

Live verification subsequently identified the peer as `::ffff:169.254.130.1`.
After configuring its IPv4 equivalent, normal and forged-header health requests
retained the same effective caller IP and HTTPS scheme. See the deployment runbook
for the active settings. This observation is deployment-specific, not a guarantee
that Azure's proxy address will remain stable or apply to other apps.

Reference: [ASP.NET Core proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
