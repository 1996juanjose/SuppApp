using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace OldSchoolApi.Services;

public static class JwtKeyProvider
{
    private static string? fallbackKey;

    public static string GetKey(IConfiguration configuration)
    {
        var configured = configuration["Jwt:Key"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var env = Environment.GetEnvironmentVariable("Jwt__Key")?.Trim();
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        fallbackKey ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return fallbackKey;
    }
}
