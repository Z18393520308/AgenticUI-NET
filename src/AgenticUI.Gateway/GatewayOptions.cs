using AgenticUI;

namespace AgenticUI.Gateway;

public sealed class GatewayOptions
{
    public const string SectionName = "AgenticUI";

    public string PipeName { get; set; } = "AgenticUI.NET";
    public string AuthenticationToken { get; set; } = "";
    public string LocalAuthenticationToken { get; set; } = "";
    public string WebSocketPath { get; set; } = "/agenticui";
    public int MaximumMessageLength { get; set; } = 1024 * 1024;
    public int NamedPipeConnectTimeoutMilliseconds { get; set; } = 5000;
    public int MaxConnections { get; set; } = 32;
    public int ConnectionAttemptsPerMinute { get; set; } = 30;
    public int RequestsPerMinute { get; set; } = 120;
    public int KeepAliveSeconds { get; set; } = 30;
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public string[] AllowedActions { get; set; } =
    [
        AgenticActions.Focus,
        AgenticActions.Highlight,
        AgenticActions.ClearHighlight,
        AgenticActions.GetText,
        AgenticActions.GetValue,
        AgenticActions.GetChecked,
        AgenticActions.GetRow,
        AgenticActions.GetRows,
        AgenticActions.GetColumns,
        AgenticActions.GetCell,
        AgenticActions.ScrollToRow,
        AgenticActions.HighlightCell,
        AgenticActions.SelectCell
    ];

    public DiscoveryOptions Discovery { get; set; } = new();
}

public sealed class DiscoveryOptions
{
    public bool Enabled { get; set; }
    public int Port { get; set; } = 47731;
    public int IntervalSeconds { get; set; } = 5;
    public string ServiceName { get; set; } = "AgenticUI Gateway";
    public string PublicWebSocketUrl { get; set; } = "wss://localhost:7443/agenticui";
    public string? InstanceId { get; set; }
}

public static class GatewayOptionsValidator
{
    public static IReadOnlyList<string> Validate(GatewayOptions options)
    {
        var errors = new List<string>();
        ValidateSecret(options.AuthenticationToken, nameof(options.AuthenticationToken), errors);
        ValidateSecret(options.LocalAuthenticationToken, nameof(options.LocalAuthenticationToken), errors);

        if (options.AuthenticationToken == options.LocalAuthenticationToken &&
            !string.IsNullOrEmpty(options.AuthenticationToken))
        {
            errors.Add("AuthenticationToken and LocalAuthenticationToken must be different.");
        }

        if (string.IsNullOrWhiteSpace(options.PipeName))
        {
            errors.Add("PipeName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.WebSocketPath) ||
            !options.WebSocketPath.StartsWith("/", StringComparison.Ordinal))
        {
            errors.Add("WebSocketPath must start with '/'.");
        }

        if (options.MaximumMessageLength is < 1024 or > 16 * 1024 * 1024)
        {
            errors.Add("MaximumMessageLength must be between 1 KiB and 16 MiB.");
        }

        if (options.NamedPipeConnectTimeoutMilliseconds is < 100 or > 120_000)
        {
            errors.Add("NamedPipeConnectTimeoutMilliseconds must be between 100 and 120000.");
        }

        if (options.MaxConnections is < 1 or > 10_000)
        {
            errors.Add("MaxConnections must be between 1 and 10000.");
        }

        if (options.RequestsPerMinute is < 1 or > 100_000)
        {
            errors.Add("RequestsPerMinute must be between 1 and 100000.");
        }

        if (options.ConnectionAttemptsPerMinute is < 1 or > 100_000)
        {
            errors.Add("ConnectionAttemptsPerMinute must be between 1 and 100000.");
        }

        if (options.KeepAliveSeconds is < 5 or > 600)
        {
            errors.Add("KeepAliveSeconds must be between 5 and 600.");
        }

        if (options.AllowedActions.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("AllowedActions cannot contain empty values.");
        }

        ValidateDiscovery(options.Discovery, errors);
        return errors;
    }

    public static void ValidateAndThrow(GatewayOptions options)
    {
        var errors = Validate(options);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid AgenticUI Gateway configuration:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
        }
    }

    private static void ValidateSecret(string value, string name, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 32)
        {
            errors.Add($"{name} must contain at least 32 characters.");
        }
    }

    private static void ValidateDiscovery(DiscoveryOptions options, ICollection<string> errors)
    {
        if (options.Port is < 1 or > 65535)
        {
            errors.Add("Discovery.Port must be between 1 and 65535.");
        }

        if (options.IntervalSeconds is < 2 or > 3600)
        {
            errors.Add("Discovery.IntervalSeconds must be between 2 and 3600.");
        }

        if (!options.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(options.PublicWebSocketUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != "wss")
        {
            errors.Add("Discovery.PublicWebSocketUrl must be an absolute wss:// URL.");
        }
    }
}

public sealed class GatewayActionPolicy
{
    private readonly HashSet<string> _allowed;
    private readonly bool _allowAll;

    public GatewayActionPolicy(IEnumerable<string> allowedActions)
    {
        _allowed = new HashSet<string>(allowedActions, StringComparer.OrdinalIgnoreCase);
        _allowAll = _allowed.Contains("*");
    }

    public bool IsAllowed(string? action) =>
        !string.IsNullOrWhiteSpace(action) && (_allowAll || _allowed.Contains(action));
}
