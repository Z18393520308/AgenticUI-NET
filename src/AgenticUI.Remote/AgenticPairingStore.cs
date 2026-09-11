using System.Text.Json;

namespace AgenticUI.Remote;

/// <summary>控制端的用户级信任库。按完整 WSS 端点绑定证书和凭据，不从 UDP 自动导入。</summary>
public sealed class AgenticPairingStore
{
    private readonly string _directory;
    public AgenticPairingStore(string? directory = null) => _directory = directory ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgenticUI.NET", "paired-endpoints");
    private string PathFor(Uri uri)
    {
        TlsWebSocketTransport.ValidateUri(uri);
        return Path.Combine(_directory, ProtectedLocalFile.Hash(System.Text.Encoding.UTF8.GetBytes(uri.AbsoluteUri)) + ".bin");
    }
    internal Entry? Load(Uri uri)
    {
        var path = PathFor(uri);
        if (!File.Exists(path)) return null;
        var entry = JsonSerializer.Deserialize<Entry>(ProtectedLocalFile.Read(path));
        if (entry is null || entry.Endpoint != uri.AbsoluteUri || entry.Fingerprint.Length != 64 ||
            entry.Token.Length < 32) throw new InvalidDataException("配对记录无效，请在本机显式删除后重新配对。");
        return entry;
    }
    internal void Save(Uri uri, string fingerprint, string token)
    {
        var path = PathFor(uri);
        Directory.CreateDirectory(_directory);
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var existing = Load(uri);
        if (existing is not null && existing.Fingerprint != fingerprint)
            throw new InvalidDataException("已有配对身份不同，不允许自动覆盖。");
        ProtectedLocalFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(new Entry
            { Endpoint = uri.AbsoluteUri, Fingerprint = fingerprint, Token = token }));
    }
    /// <summary>仅删除控制端记忆，不撤销目标端凭据；目标端可调用 RevokeAllClients 撤销授权。</summary>
    public void Forget(Uri uri)
    {
        var path = PathFor(uri);
        Directory.CreateDirectory(_directory);
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path)) File.Delete(path);
    }
    internal sealed class Entry
    {
        public string Endpoint { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string Token { get; set; } = "";
    }
}

public sealed class AgenticPairingPrompt
{
    public Uri Endpoint { get; }
    /// <summary>来自尚未受信任的 TLS 连接，必须与目标端本地显示独立核对。</summary>
    public string CertificateFingerprint { get; }
    internal AgenticPairingPrompt(Uri endpoint, string fingerprint)
    { Endpoint = endpoint; CertificateFingerprint = fingerprint; }
}
