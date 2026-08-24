using System.Security.Cryptography;
using System.Text;

namespace AgenticUI.Gateway;

internal static class GatewaySecurity
{
    public static bool FixedTimeEquals(string expected, string? supplied)
    {
        if (supplied is null)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(expectedBytes),
            SHA256.HashData(suppliedBytes));
    }
}

internal sealed class FixedWindowRateLimiter
{
    private readonly int _limit;
    private readonly object _sync = new();
    private DateTimeOffset _windowStarted = DateTimeOffset.UtcNow;
    private int _count;

    public FixedWindowRateLimiter(int limit)
    {
        _limit = limit;
    }

    public bool TryAcquire(DateTimeOffset now)
    {
        lock (_sync)
        {
            if (now - _windowStarted >= TimeSpan.FromMinutes(1))
            {
                _windowStarted = now;
                _count = 0;
            }

            if (_count >= _limit)
            {
                return false;
            }

            _count++;
            return true;
        }
    }
}
