using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace AgenticUI.Remote;

/// <summary>仅由目标软件本地 UI 开启配对，不向网络公开配对码或允许远端开启。</summary>
public sealed class AgenticPairingService : IDisposable
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly FileStream _lease;
    private readonly Func<DateTimeOffset> _utcNow;
    private Identity _identity;
    private string? _code;
    private DateTimeOffset _expires;
    private int _attempts;
    private bool _disposed;
    internal X509Certificate2 Certificate { get; }
    public string CertificateFingerprint { get; }
    public int PairedClientCount { get { lock (_gate) return _identity.Clients.Count; } }
    internal event Action? Revoked;

    internal AgenticPairingService(string directory, Func<DateTimeOffset>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        Directory.CreateDirectory(directory);
        // 同一个身份目录不能被两个进程同时修改，也不能悄悄生成替代身份。
        _lease = new FileStream(Path.Combine(directory, "identity.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _path = Path.Combine(directory, "identity.bin");
        try
        {
            if (File.Exists(_path))
                _identity = JsonSerializer.Deserialize<Identity>(ProtectedLocalFile.Read(_path)) ?? throw new InvalidDataException("身份文件无效。");
            else
            {
                using var rsa = RSA.Create(2048);
                var request = new CertificateRequest("CN=AgenticUI Local Identity", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                var usages = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") };
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
                using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
                _identity = new Identity { Pfx = Convert.ToBase64String(generated.Export(X509ContentType.Pfx)) };
                Save();
            }
            if (_identity.Clients is null || _identity.Clients.Count > 64 ||
                _identity.Clients.Any(value => value is null || value.Length != 64))
                throw new InvalidDataException("配对记录无效，拒绝自动重建。");
            // PKCS#12 导入兼容 Windows Schannel；不加入 X509Store，也不设置 PersistKeySet。
            Certificate = new X509Certificate2(Convert.FromBase64String(_identity.Pfx));
            if (!Certificate.HasPrivateKey || DateTime.UtcNow > Certificate.NotAfter.ToUniversalTime())
                throw new InvalidDataException("身份已失效，必须显式重置并重新配对。");
            CertificateFingerprint = ProtectedLocalFile.Hash(Certificate.RawData);
        }
        catch { _lease.Dispose(); throw; }
    }

    /// <summary>返回 12 位十六进制一次性码，有效 3 分钟；最多尝试 5 次，重新开启会废弃旧码。</summary>
    public string BeginPairing()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AgenticPairingService));
            var bytes = new byte[6];
            using var rng = RandomNumberGenerator.Create(); rng.GetBytes(bytes);
            _code = BitConverter.ToString(bytes).Replace("-", "");
            _expires = _utcNow().AddMinutes(3); _attempts = 0;
            System.Diagnostics.Trace.TraceInformation("AgenticUI 本机已开启限时配对窗口。");
            return _code;
        }
    }
    public void CancelPairing() { lock (_gate) _code = null; }

    internal string? Exchange(string? code)
    {
        lock (_gate)
        {
            if (_disposed || _code is null || _utcNow() >= _expires || ++_attempts > 5)
            { _code = null; return null; }
            if (!AgenticRemoteSecurity.FixedTimeEquals(_code, code)) return null;
            _code = null; // 先消费，失败不得重放。
            if (_identity.Clients.Count >= 64) return null;
            var token = AgenticRemoteSecurity.CreateToken();
            var hash = TokenHash(token);
            _identity.Clients.Add(hash);
            try { Save(); }
            catch { _identity.Clients.Remove(hash); throw; }
            System.Diagnostics.Trace.TraceInformation("AgenticUI 新控制端配对成功；凭据未写入日志。");
            return token;
        }
    }

    internal bool IsAuthorized(string? token)
    {
        lock (_gate) return !_disposed && token is not null && _identity.Clients.Contains(TokenHash(token));
    }
    public void RevokeAllClients()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AgenticPairingService));
            var old = _identity.Clients;
            _identity.Clients = new List<string>();
            try { Save(); }
            catch { _identity.Clients = old; throw; }
            _code = null;
        }
        Revoked?.Invoke();
        System.Diagnostics.Trace.TraceInformation("AgenticUI 已撤销全部配对授权。");
    }
    private static string TokenHash(string token) => ProtectedLocalFile.Hash(Encoding.UTF8.GetBytes(token));
    private void Save() => ProtectedLocalFile.Write(_path, JsonSerializer.SerializeToUtf8Bytes(_identity));
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _code = null; Certificate.Dispose(); _lease.Dispose();
        }
    }
    private sealed class Identity
    {
        public string Pfx { get; set; } = "";
        public List<string> Clients { get; set; } = new();
    }
}
