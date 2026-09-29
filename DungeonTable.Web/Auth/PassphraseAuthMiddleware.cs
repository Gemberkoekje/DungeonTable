using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DungeonTable.Web.Auth;

/// <summary>
/// Gates every request behind a single shared passphrase (HTTP Basic auth; any username, the
/// password is the passphrase). The whole app is protected, not just <c>/dm</c> — the maps and
/// stat blocks served under it are copyrighted material, and the app is reachable at a public URL.
/// </summary>
public sealed class PassphraseAuthMiddleware(RequestDelegate next, string passphrase)
{
    private const string RealmChallenge = "Basic realm=\"DungeonTable\", charset=\"UTF-8\"";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/healthz", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        if (TryGetSuppliedPassphrase(context.Request, out string supplied) && PassphraseMatches(supplied))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = RealmChallenge;
    }

    private bool PassphraseMatches(string supplied)
    {
        // Hash both sides to a fixed 32-byte digest first so FixedTimeEquals never has to branch
        // on input length (it requires equal-length spans) — the raw passphrase length leaks nothing.
        byte[] suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }

    private static bool TryGetSuppliedPassphrase(HttpRequest request, out string passphrase)
    {
        passphrase = "";

        string header = request.Headers.Authorization.ToString();
        if (header.Length == 0 || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string encoded = header["Basic ".Length..].Trim();
        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return false;
        }

        string decoded = Encoding.UTF8.GetString(decodedBytes);
        int separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
        {
            return false;
        }

        passphrase = decoded[(separatorIndex + 1)..];
        return true;
    }
}
