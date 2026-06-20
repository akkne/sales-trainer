using System.Text.RegularExpressions;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Tests.Helpers;

/// <summary>Pulls the 6-digit verification code out of a recorded email body for end-to-end tests.</summary>
public static class TestCodeExtractor
{
    public static string ExtractSixDigitCode(string emailBody)
    {
        var match = Regex.Match(emailBody, "\\b(\\d{6})\\b");
        if (!match.Success)
            throw new InvalidOperationException("No 6-digit code found in the email body.");
        return match.Groups[1].Value;
    }
}

/// <summary>Small holder so a unit test can reuse the same in-memory context the service writes to.</summary>
public sealed class InMemoryHolder(IdentityDbContext db)
{
    public IdentityDbContext Db { get; } = db;
}
