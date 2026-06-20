using System.Text.RegularExpressions;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Tests.Helpers;

public static class TestCodeExtractor
{
    public static string ExtractSixDigitCode(string emailBody)
    {
        var match = Regex.Match(emailBody, "\\b(\\d{6})\\b");
        if (!match.Success)
        {
            throw new InvalidOperationException("No 6-digit code found in the email body.");
        }

        return match.Groups[1].Value;
    }
}

public sealed class InMemoryHolder(IdentityDbContext database)
{
    public IdentityDbContext Db { get; } = database;
}
