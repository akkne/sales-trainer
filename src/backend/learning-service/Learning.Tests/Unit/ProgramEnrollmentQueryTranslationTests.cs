using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Programs.Models;
using Sellevate.Learning.Features.Programs.Services.Implementation;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// <c>GET /admin/program/enrollments</c> answered 500 in production: its query ordered by a member of
/// a constructor-projected record, which Npgsql cannot translate. A query of that shape fails
/// identically at <c>ToQueryString()</c>, so this needs no database — which is exactly why the bug
/// survived the in-memory unit tests, where every LINQ shape "translates".
/// </summary>
[TestFixture]
public sealed class ProgramEnrollmentQueryTranslationTests
{
    private LearningDbContext _databaseContext = null!;

    [SetUp]
    public void SetUp()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(LearningDbContextFactory.DefaultOrganizationId);

        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseNpgsql("Host=localhost;Database=translation-only;Username=none;Password=none")
            .Options;

        _databaseContext = new LearningDbContext(options, tenantContext);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public void EnrollmentListQuery_TranslatesToSql()
    {
        var act = () => ProgramEnrollmentService.BuildEnrollmentListQuery(_databaseContext).ToQueryString();

        act.Should().NotThrow<InvalidOperationException>();
    }

    [Test]
    public void EnrollmentListQuery_OrdersByEnrolledAt()
    {
        var sql = ProgramEnrollmentService.BuildEnrollmentListQuery(_databaseContext).ToQueryString();

        sql.Should().Contain("ORDER BY").And.Contain("EnrolledAt");
    }

    /// <summary>
    /// Pins the failure this fix was about, so "the ordering reads better at the end" cannot quietly
    /// reintroduce it: put the <c>OrderBy</c> after the projection and the query stops translating.
    /// </summary>
    [Test]
    public void OrderingAfterTheProjection_DoesNotTranslate()
    {
        var badlyOrderedQuery = _databaseContext.ProgramEnrollments
            .AsNoTracking()
            .Join(
                _databaseContext.ProgramVersions,
                enrollment => enrollment.ProgramVersionId,
                version => version.Id,
                (enrollment, version) => new ProgramEnrollmentDto(
                    enrollment.UserId,
                    enrollment.ProgramVersionId,
                    version.VersionNumber,
                    enrollment.PreviousProgramVersionId,
                    enrollment.EnrolledAt,
                    enrollment.SwitchedAt))
            .OrderBy(enrollment => enrollment.EnrolledAt);

        var act = () => badlyOrderedQuery.ToQueryString();

        act.Should().Throw<InvalidOperationException>();
    }
}
