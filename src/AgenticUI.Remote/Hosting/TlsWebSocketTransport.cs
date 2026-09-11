using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace AgenticUI.Remote;

/// <summary>只实现有界的 HTTP/1.1 Upgrade 入口；TLS 和 WebSocket 帧交由平台/兼容库处理。</summary>
internal static class TlsWebSocketTransport
{
    internal static async Task<string> ProbeAsync(Uri uri, CancellationToken token)
    {
        string? fingerprint = null;
        // 此连接只取证书，不发送 HTTP、配对码或令牌；返回值仍是不可信的候选身份。
        using var stream = await ConnectTlsAsync(uri, value => { fingerprint = value; return true; }, token).ConfigureAwait(false);
        return fingerprint ?? throw new AuthenticationException("服务端未提供有效证书。");
    }

    internal static async Task<WebSocket> ConnectAsync(Uri uri, string fingerprint, CancellationToken token)
    {
        var stream = await ConnectTlsAsync(uri, value => AgenticRemoteSecurity.FixedTimeEquals(value, fingerprint), token).ConfigureAwait(false);
        try
        {
            var random = new byte[16]; using var rng = RandomNumberGenerator.Create(); rng.GetBytes(random);
            var key = Convert.ToBase64String(random);
            await WriteAsync(stream, $"GET {uri.PathAndQuery} HTTP/1.1\r\nHost: {uri.Authority}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: {key}\r\n\r\n", token).ConfigureAwait(false);
            var response = await ReadHeadersAsync(stream, token).ConfigureAwait(false);
            if (!response.FirstLine.StartsWith("HTTP/1.1 101 ", StringComparison.Ordinal) ||
                !response.HasToken("Upgrade", "websocket") || !response.HasToken("Connection", "Upgrade") ||
                response.Get("Sec-WebSocket-Accept") != AcceptKey(key) ||
                response.Get("Sec-WebSocket-Extensions") is not null || response.Get("Sec-WebSocket-Protocol") is not null)
                throw new WebSocketException("WebSocket 升级响应无效。");
#if NET8_0_OR_GREATER
            return WebSocket.CreateFromStream(stream, false, null, TimeSpan.FromSeconds(30));
#else
            return WebSocket.CreateClientWebSocket(stream, null, 8192, 8192, TimeSpan.FromSeconds(30), false, WebSocket.CreateClientBuffer(8192, 8192));
#endif
        }
        catch { stream.Dispose(); throw; }
    }

    private static async Task<SslStream> ConnectTlsAsync(Uri uri, Func<string, bool> validate, CancellationToken token)
    {
        ValidateUri(uri);
        var tcp = new TcpClient { NoDelay = true };
        using var registration = token.Register(tcp.Close);
        SslStream? stream = null;
        try
        {
            await tcp.ConnectAsync(uri.DnsSafeHost, uri.Port).ConfigureAwait(false);
            stream = new SslStream(tcp.GetStream(), false, (_, certificate, _, _) =>
            {
                if (certificate is null) return false;
                using var cert = new X509Certificate2(certificate);
                return DateTime.UtcNow >= cert.NotBefore.ToUniversalTime() &&
                       DateTime.UtcNow <= cert.NotAfter.ToUniversalTime() &&
                       validate(ProtectedLocalFile.Hash(cert.RawData));
            });
            await stream.AuthenticateAsClientAsync(uri.DnsSafeHost, null, SslProtocols.Tls12, false).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return stream;
        }
        catch { stream?.Dispose(); tcp.Dispose(); throw; }
    }

    internal static void ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != "wss" || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("配对端点必须是无凭据、无查询参数的 wss:// 地址。");
    }

    internal static async Task<WebSocket> AcceptAsync(SslStream stream, string path, string[] origins, CancellationToken token)
    {
        var request = await ReadHeadersAsync(stream, token).ConfigureAwait(false);
        var key = request.Get("Sec-WebSocket-Key");
        if (request.FirstLine != $"GET {path} HTTP/1.1" ||
            !request.HasToken("Upgrade", "websocket") || !request.HasToken("Connection", "Upgrade") ||
            request.Get("Sec-WebSocket-Version") != "13" || string.IsNullOrWhiteSpace(request.Get("Host")) ||
            request.Get("Transfer-Encoding") is not null || request.Get("Content-Length") is not null ||
            key is null || Convert.FromBase64String(key).Length != 16 ||
            (request.Get("Origin") is string origin && !origins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
            throw new WebSocketException("不允许的 WebSocket 握手。");
#if NET8_0_OR_GREATER
        await WriteAsync(stream, $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {AcceptKey(key)}\r\n\r\n", token).ConfigureAwait(false);
        return WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30));
#else
        // net48 没有 CreateFromStream 服务端 API，使用返回标准 WebSocket 的兼容实现。
        var context = new Ninja.WebSockets.WebSocketHttpContext(true, new List<string>(), request.Raw, path, stream);
        return await new Ninja.WebSockets.WebSocketServerFactory().AcceptWebSocketAsync(context,
            new Ninja.WebSockets.WebSocketServerOptions { KeepAliveInterval = TimeSpan.Zero, IncludeExceptionInCloseResponse = false }, token).ConfigureAwait(false);
#endif
    }

    private static string AcceptKey(string key)
    {
        // RFC 6455 的 Upgrade 校验值，不用于身份认证或签名。
        using var sha = SHA1.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
    }

    internal static async Task<Headers> ReadHeadersAsync(Stream stream, CancellationToken token)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (bytes.Count < 8192)
        {
            if (await stream.ReadAsync(one, 0, 1, token).ConfigureAwait(false) != 1) throw new EndOfStreamException();
            if (one[0] > 126 || (one[0] < 32 && one[0] != 13 && one[0] != 10 && one[0] != 9))
                throw new InvalidDataException("非 ASCII HTTP 头。");
            bytes.Add(one[0]);
            var n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == 13 && bytes[n - 3] == 10 && bytes[n - 2] == 13 && bytes[n - 1] == 10)
                return new Headers(Encoding.ASCII.GetString(bytes.ToArray()));
        }
        throw new InvalidDataException("HTTP 头超过 8 KiB。");
    }
    private static Task WriteAsync(Stream stream, string value, CancellationToken token)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        return stream.WriteAsync(bytes, 0, bytes.Length, token);
    }
    internal sealed class Headers
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        internal string FirstLine { get; }
        internal string Raw { get; }
        internal Headers(string raw)
        {
            Raw = raw;
            var lines = raw.Split(new[] { "\r\n" }, StringSplitOptions.None); FirstLine = lines[0];
            foreach (var line in lines.Skip(1).Where(l => l.Length != 0))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0 || line.Take(colon).Any(c => !char.IsLetterOrDigit(c) && c != '-') ||
                    _values.ContainsKey(line.Substring(0, colon)))
                    throw new InvalidDataException("HTTP 头字段重复或非法。");
                _values.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
            }
        }
        internal string? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
        internal bool HasToken(string name, string value) => Get(name)?.Split(',').Any(v => v.Trim().Equals(value, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
