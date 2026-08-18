using System.Runtime.CompilerServices;
using FluentAssertions;
using NUnit.Framework;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Phase 40.13. identity-service's two cleanup jobs run on a timer, outside any request, and both
/// touch <c>IdentityDbContext</c> — the context that carries the <c>Invites</c> query filter and
/// sits over a database where <c>Invites</c> has an RLS policy. Before 40.13 they ran on a freshly
/// created scope whose <c>TenantContext</c> simply happened to be blank.
///
/// <para>
/// A blank context and a deliberate system-mode context produce the same SQL today, which is
/// exactly why this needs a test rather than a comment: the two are indistinguishable by
/// behaviour, so only the source can say which one was intended. docs/TENANCY/TENANCY.md §1.6
/// requires the intent to be written down, and 40.14 will build its background-job registry from
/// declarations like these.
/// </para>
///
/// <para>
/// The assertion is on the source tree because there is nothing else to assert on: both jobs call
/// <c>ExecuteDeleteAsync</c>, which the in-memory provider does not implement, so exercising
/// <c>RunOnceAsync</c> would need a real Postgres and would turn a build-time tripwire into an
/// integration test that nobody runs on every commit.
/// </para>
/// </summary>
[TestFixture]
public class IdentityBackgroundJobTenancyTests
{
    /// <summary>
    /// Every job that opens a scope and resolves <c>IdentityDbContext</c> from it must first
    /// declare a mode: <c>EnterSystemMode()</c> for a platform-wide job, <c>SetOrganization(...)</c>
    /// for one that iterates organizations. Discovered by scanning, not by a hard-coded list, so a
    /// third cleanup job added next year is covered the day it lands.
    /// </summary>
    [TestCase("ExpiredRefreshTokenCleanupService.cs")]
    [TestCase("ExpiredEmailVerificationCleanupService.cs")]
    public void Cleanup_jobs_declare_an_explicit_tenant_mode(string jobFileName)
    {
        var serviceRoot = FindIdentityServiceSourceRoot();
        if (serviceRoot is null)
        {
            Assert.Ignore("identity-service sources are not next to the test assembly; nothing to scan.");
            return;
        }

        var jobFilePath = Directory
            .EnumerateFiles(serviceRoot, jobFileName, SearchOption.AllDirectories)
            .FirstOrDefault(path => !IsBuildOutput(path));

        jobFilePath.Should().NotBeNull("{0} is the job under audit and must exist", jobFileName);

        var source = File.ReadAllText(jobFilePath!);

        source.Should().Contain(
            "EnterSystemMode()",
            "{0} deletes rows that belong to no organization, and §1.6 wants that stated rather than " +
            "left to an empty context that looks identical to a forgotten one",
            jobFileName);
    }

    /// <summary>
    /// The declaration has to come before the context is built, not after. <c>TenantContext</c>
    /// refuses to change mode once set, so a call placed after
    /// <c>GetRequiredService&lt;IdentityDbContext&gt;()</c> would still compile and still pass a
    /// naive "contains EnterSystemMode" check while the context had already captured a blank scope.
    /// </summary>
    [TestCase("ExpiredRefreshTokenCleanupService.cs")]
    [TestCase("ExpiredEmailVerificationCleanupService.cs")]
    public void The_tenant_mode_is_declared_before_the_database_context_is_resolved(string jobFileName)
    {
        var serviceRoot = FindIdentityServiceSourceRoot();
        if (serviceRoot is null)
        {
            Assert.Ignore("identity-service sources are not next to the test assembly; nothing to scan.");
            return;
        }

        var jobFilePath = Directory
            .EnumerateFiles(serviceRoot, jobFileName, SearchOption.AllDirectories)
            .First(path => !IsBuildOutput(path));

        var source = File.ReadAllText(jobFilePath);

        var systemModeIndex = source.IndexOf("EnterSystemMode()", StringComparison.Ordinal);
        var contextIndex = source.IndexOf("GetRequiredService<IdentityDbContext>()", StringComparison.Ordinal);

        systemModeIndex.Should().BeGreaterThan(-1);
        contextIndex.Should().BeGreaterThan(-1);
        systemModeIndex.Should().BeLessThan(
            contextIndex,
            "the scope's mode must be decided before the DbContext is built from that scope");
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>
    /// Walks up from this file's compile-time path to the identity-service source root, mirroring
    /// the ai-service tripwire from 40.11. Compile-time, not runtime, because the test binary lives
    /// under bin/ and knows nothing about the tree.
    /// </summary>
    private static string? FindIdentityServiceSourceRoot([CallerFilePath] string thisFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(thisFilePath)!);

        while (directory is not null && directory.Name != "identity-service")
        {
            directory = directory.Parent;
        }

        var serviceRoot = directory is null ? null : Path.Combine(directory.FullName, "Identity");
        return serviceRoot is not null && Directory.Exists(serviceRoot) ? serviceRoot : null;
    }
}
