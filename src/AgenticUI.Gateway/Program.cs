using System.Net;
using System.Threading.RateLimiting;
using AgenticUI.Gateway;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var options = new GatewayOptions();
builder.Configuration.GetSection(GatewayOptions.SectionName).Bind(options);
ApplyLocalDiscoveryDefaults(options, builder.Environment);

if (args.Contains("--discover", StringComparer.OrdinalIgnoreCase))
{
    if (options.Discovery.Port is < 1 or > 65535)
    {
        throw new InvalidOperationException("AgenticUI:Discovery:Port must be between 1 and 65535.");
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    await UdpDiscoveryListener.RunAsync(options.Discovery.Port, cancellation.Token);
    return;
}

GatewayOptionsValidator.ValidateAndThrow(options);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new GatewayActionPolicy(options.AllowedActions));
builder.Services.AddSingleton<GatewayConnectionHandler>();
builder.Services.AddHostedService<UdpDiscoveryBroadcaster>();
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.AddPolicy("gateway-connections", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.ConnectionAttemptsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();
var connectionSlots = new SemaphoreSlim(options.MaxConnections, options.MaxConnections);
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(options.KeepAliveSeconds)
};
foreach (var origin in options.AllowedOrigins)
{
    webSocketOptions.AllowedOrigins.Add(origin);
}

app.UseWebSockets(webSocketOptions);
app.UseRateLimiter();
app.MapGet("/healthz", (HttpContext context) =>
{
    if (!context.Request.IsHttps)
    {
        return Results.BadRequest(new { error = "TLS is required." });
    }

    return Results.Ok(new
    {
        status = "ok",
        commandTransport = "wss",
        udpDiscovery = options.Discovery.Enabled
    });
});

app.Map(options.WebSocketPath, async context =>
{
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "WSS/TLS is required." });
        return;
    }

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "A WebSocket upgrade is required." });
        return;
    }

    if (!await connectionSlots.WaitAsync(0, context.RequestAborted))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "Gateway connection limit reached." });
        return;
    }

    try
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var address = context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
        var handler = context.RequestServices.GetRequiredService<GatewayConnectionHandler>();
        await handler.RunAsync(socket, address, context.RequestAborted);
    }
    finally
    {
        connectionSlots.Release();
    }
}).RequireRateLimiting("gateway-connections");

await app.RunAsync();

static void ApplyLocalDiscoveryDefaults(GatewayOptions options, IWebHostEnvironment environment)
{
    if (options.Discovery.Enabled)
    {
        return;
    }

    if (environment.IsDevelopment())
    {
        options.Discovery.Enabled = true;
        return;
    }

    if (Uri.TryCreate(options.Discovery.PublicWebSocketUrl, UriKind.Absolute, out var uri) &&
        uri.Host is "localhost" or "127.0.0.1" or "::1")
    {
        options.Discovery.Enabled = true;
    }
}
