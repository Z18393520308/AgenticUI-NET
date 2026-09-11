using System.Diagnostics;

namespace AgenticUI.Remote;

/// <summary>
/// 每个应用域只初始化一次的应用级宿主。应用入口调用一次即可，无需独立 Gateway 进程。
/// 初始化错误通过 LastError 返回，不抛入业务 UI；配置变更在下次启动时生效。
/// </summary>
public sealed class AgenticApplicationHost : IDisposable
{
    private static readonly object Gate = new();
    private static AgenticApplicationHost? _current;
    private AgenticNamedPipeServer? _pipe;
    private EmbeddedGateway? _gateway;
    public AgenticPairingService? Pairing { get; private set; }
    private int _disposed;

    private AgenticApplicationHost() { }
    public static AgenticApplicationHost? Current { get { lock (Gate) return _current; } }
    public bool LocalRunning => Volatile.Read(ref _disposed) == 0 && _pipe?.IsRunning == true;
    public bool NetworkRunning => Volatile.Read(ref _disposed) == 0 && _gateway?.IsRunning == true;
    public bool DiscoveryRunning => NetworkRunning && _gateway?.DiscoveryRunning == true;
    public string PipeName { get; private set; } = "";
    /// <summary>仅供宿主通过受保护的本地 UI 交付给客户端；不要写日志或广播。</summary>
    public string LocalAuthenticationToken => _pipe?.AuthenticationToken ?? "";
    public string? LastError { get; private set; }

    public static AgenticApplicationHost StartFromConfiguration(string? path = null) => StartOnce(() =>
        AgenticHostOptions.Load(path ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agenticui.json")));

    public static AgenticApplicationHost Start(AgenticHostOptions options) => StartOnce(() => options.Snapshot());

    private static AgenticApplicationHost StartOnce(Func<AgenticHostOptions> load)
    {
        lock (Gate)
        {
            if (_current is not null) return _current;
            var host = new AgenticApplicationHost();
            _current = host;
            AppDomain.CurrentDomain.ProcessExit += host.OnProcessExit;
            AppDomain.CurrentDomain.DomainUnload += host.OnProcessExit;
            try { host.Initialize(load()); }
            catch (Exception exception)
            {
                // 不拼接底层异常消息，避免证书路径、环境变量值或配置中的秘密进入日志。
                host.LastError = "AgenticUI 启动失败（" + exception.GetType().Name +
                    "）。请检查 agenticui.json、配置、端口、身份目录访问权限和可选令牌环境变量。";
                Trace.TraceError(host.LastError);
            }
            return host;
        }
    }

    private void Initialize(AgenticHostOptions options)
    {
        options.Validate();
        if (!options.AutoStart || !options.Local.Enabled) return;
        PipeName = options.Local.PipeName;
        var pipeToken = ReadToken(options.Local.TokenEnvironmentVariable, required: false);
        var pipe = new AgenticNamedPipeServer(PipeName, options: new AgenticNamedPipeServerOptions { AuthenticationToken = pipeToken });
        try { pipe.Start(); _pipe = pipe; }
        catch { pipe.Dispose(); throw; }
        if (!options.Network.Enabled) return;
        // 网络失败保留已经正常工作的本机管道，但绝不广播失败的网络服务。
        var networkToken = ReadToken(options.Network.TokenEnvironmentVariable, required: false) ?? "";
        if (AgenticRemoteSecurity.FixedTimeEquals(pipe.AuthenticationToken, networkToken))
            throw new InvalidDataException("网络与本机令牌必须独立。");
        var directory = options.Network.StateDirectory;
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AgenticUI.NET", "identities", ProtectedLocalFile.Hash(System.Text.Encoding.UTF8.GetBytes(PipeName)));
        Pairing = new AgenticPairingService(directory);
        var gateway = new EmbeddedGateway(options, pipe.AuthenticationToken, networkToken, Pairing);
        try { gateway.Start(); _gateway = gateway; }
        catch { gateway.Dispose(); Pairing.Dispose(); Pairing = null; throw; }
    }

    private static string? ReadToken(string name, bool required)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("令牌环境变量名称不能为空。");
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value) && !required) return null;
        if (string.IsNullOrWhiteSpace(value) || value!.Length < 32) throw new InvalidDataException("缺少有效令牌。");
        return value;
    }

    public void Dispose()
    {
        lock (Gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            AppDomain.CurrentDomain.DomainUnload -= OnProcessExit;
            // 不同步等待需要 UI Dispatcher 完成的会话清理，避免退出时与 UI 线程互锁。
            try { _gateway?.Dispose(); Pairing?.Dispose(); }
            finally { _pipe?.Dispose(); if (ReferenceEquals(_current, this)) _current = null; }
        }
    }

    private void OnProcessExit(object? sender, EventArgs args) => Dispose();
}
