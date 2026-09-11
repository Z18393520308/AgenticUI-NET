using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticUI.Remote;

/// <summary>应用级通信配置。只在初始化时读取；配置文件不存放令牌。</summary>
public sealed class AgenticHostOptions
{
    public bool AutoStart { get; set; } = true;
    public AgenticLocalHostOptions Local { get; set; } = new();
    public AgenticNetworkHostOptions Network { get; set; } = new();
    public AgenticHostDiscoveryOptions Discovery { get; set; } = new();

    public static AgenticHostOptions Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Configuration>(json, JsonOptions)?.AgenticUI
            ?? throw new InvalidDataException("配置中缺少 AgenticUI 节点。");
    }

    // 拒绝拼错的开关或旧 Gateway 配置，防止使用者以为已关闭网络或限制权限。
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal AgenticHostOptions Snapshot() =>
        JsonSerializer.Deserialize<AgenticHostOptions>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;

    internal void Validate()
    {
        if (Local is null || Network is null || Discovery is null) throw new InvalidDataException("通信配置节点不能为 null。");
        if (!AutoStart) return;
        if (Local.Enabled && (string.IsNullOrWhiteSpace(Local.PipeName) || Local.PipeName.Length > 100 ||
            Local.PipeName.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-' or '_'))))
            throw new InvalidDataException("Local.PipeName 只能包含字母、数字、点、横线或下划线，长度不超过 100。");
        if (!Network.Enabled) return; // 关闭网络时绝不广播，发现配置不隐式开启监听。
        if (!Local.Enabled) throw new InvalidDataException("进程内网关要求 Local.Enabled=true。");
        if (!System.Net.IPAddress.TryParse(Network.BindAddress, out _))
            throw new InvalidDataException("Network.BindAddress 必须是本机 IP，0.0.0.0 表示监听所有 IPv4 网卡。");
        if (!Uri.TryCreate(Network.ListenUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0 ||
            uri.Host == "0.0.0.0" || uri.Host == "::" || uri.Host.Contains('*') || uri.Host.Contains('+'))
            throw new InvalidDataException("Network.ListenUrl 必须为指定主机的 HTTPS 根地址，不能使用通配符或 0.0.0.0。");
        if (string.IsNullOrWhiteSpace(Network.WebSocketPath) || !Network.WebSocketPath.StartsWith("/", StringComparison.Ordinal) ||
            Network.WebSocketPath == "/" || Network.WebSocketPath.Any(c => c is '?' or '#' || char.IsWhiteSpace(c)))
            throw new InvalidDataException("Network.WebSocketPath 必须为非根路径，不能带查询参数。");
        if (Network.MaxConnections < 1 || Network.MaxConnections > 256 ||
            Network.RequestsPerMinute < 1 || Network.RequestsPerMinute > 10000 ||
            Network.ConnectionAttemptsPerMinute < 1 || Network.ConnectionAttemptsPerMinute > 1000 ||
            Network.MaximumMessageLength < 1024 || Network.MaximumMessageLength > 1024 * 1024 ||
            Network.CommandTimeoutSeconds < 1 || Network.CommandTimeoutSeconds > 300)
            throw new InvalidDataException("网络连接数、限流、消息大小或命令超时超出范围。");
        if (Network.AllowedActions is null || Network.AllowedActions.Any(string.IsNullOrWhiteSpace) ||
            Network.AllowedActions.Contains("*")) throw new InvalidDataException("请逐项配置 AllowedActions，不允许通配符。");
        if (Network.AllowedOrigins is null || Network.AllowedOrigins.Any(value =>
            !Uri.TryCreate(value, UriKind.Absolute, out var origin) || origin.Scheme != "https" ||
            origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0 || origin.UserInfo.Length > 0))
            throw new InvalidDataException("AllowedOrigins 必须是 HTTPS 源地址。");
        if (!Discovery.Enabled) return;
        if (Discovery.Port < 1 || Discovery.Port > 65535 || Discovery.IntervalSeconds < 2 || Discovery.IntervalSeconds > 3600 ||
            string.IsNullOrWhiteSpace(Discovery.ServiceName) || Discovery.ServiceName.Length > 100 ||
            (!string.IsNullOrWhiteSpace(Discovery.PublicWebSocketUrl) &&
            (!Uri.TryCreate(Discovery.PublicWebSocketUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != "wss" ||
            publicUri.Query.Length > 0 || publicUri.Fragment.Length > 0 || publicUri.UserInfo.Length > 0 ||
            publicUri.AbsolutePath != Network.WebSocketPath)))
            throw new InvalidDataException("发现服务需要有效的端口、间隔、名称及不含凭据的 WSS 地址，路径须与监听路径一致。");
    }

    private sealed class Configuration { public AgenticHostOptions? AgenticUI { get; set; } }
}

public sealed class AgenticLocalHostOptions
{
    public bool Enabled { get; set; } = true;
    public string PipeName { get; set; } = "AgenticUI.NET";
    /// <summary>未设置环境变量时，本机 Pipe 使用本次进程的随机令牌。</summary>
    public string TokenEnvironmentVariable { get; set; } = "AGENTICUI_PIPE_TOKEN";
}

public sealed class AgenticNetworkHostOptions
{
    /// <summary>独立于公告主机名的绑定地址。跨机器访问需显式改为局域网 IP 或 0.0.0.0。</summary>
    public string BindAddress { get; set; } = "127.0.0.1";
    /// <summary>为空则放在当前用户 LocalApplicationData 下，按管道名隔离。</summary>
    public string StateDirectory { get; set; } = "";
    public bool Enabled { get; set; }
    public string ListenUrl { get; set; } = "https://localhost:7443";
    public string WebSocketPath { get; set; } = "/agenticui";
    public string TokenEnvironmentVariable { get; set; } = "AGENTICUI_GATEWAY_TOKEN";
    public string[] AllowedActions { get; set; } = { "highlight", "clearHighlight", "focus", "getText", "getValue",
        "getChecked", "getRow", "getRows", "getColumns", "getCell", "scrollToRow", "highlightCell", "selectCell" };
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public int MaxConnections { get; set; } = 32;
    public int RequestsPerMinute { get; set; } = 120;
    public int ConnectionAttemptsPerMinute { get; set; } = 30;
    public int MaximumMessageLength { get; set; } = 1024 * 1024;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class AgenticHostDiscoveryOptions
{
    public bool Enabled { get; set; }
    public int Port { get; set; } = AgenticGatewayDiscovery.DefaultPort;
    public int IntervalSeconds { get; set; } = 5;
    public string ServiceName { get; set; } = "AgenticUI application";
    public string PublicWebSocketUrl { get; set; } = "";
}
