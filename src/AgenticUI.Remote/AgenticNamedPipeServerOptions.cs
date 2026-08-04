using System.Security.Cryptography;

namespace AgenticUI.Remote;

public sealed class AgenticNamedPipeServerOptions
{
    public bool RequireAuthentication { get; set; } = true;
    public string? AuthenticationToken { get; set; }
    public int MaximumMessageLength { get; set; } = 1024 * 1024;

    internal string ResolveAuthenticationToken()
    {
        if (!RequireAuthentication)
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(AuthenticationToken)
            ? AgenticRemoteSecurity.CreateToken()
            : AuthenticationToken!;
    }
}

public static class AgenticRemoteSecurity
{
    public static string CreateToken(int byteLength = 32)
    {
        if (byteLength < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), "Tokens must contain at least 16 random bytes.");
        }

        var bytes = new byte[byteLength];
        using (var generator = RandomNumberGenerator.Create())
        {
            generator.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes);
    }

    internal static bool FixedTimeEquals(string expected, string? supplied)
    {
        if (supplied is null)
        {
            return false;
        }

        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = System.Text.Encoding.UTF8.GetBytes(supplied);
        var difference = expectedBytes.Length ^ suppliedBytes.Length;
        var length = Math.Max(expectedBytes.Length, suppliedBytes.Length);
        for (var index = 0; index < length; index++)
        {
            var left = index < expectedBytes.Length ? expectedBytes[index] : (byte)0;
            var right = index < suppliedBytes.Length ? suppliedBytes[index] : (byte)0;
            difference |= left ^ right;
        }

        return difference == 0;
    }
}
