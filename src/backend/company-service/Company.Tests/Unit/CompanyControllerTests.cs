using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Company.Features.Companies.Endpoints;
using Sellevate.Company.Features.Companies.Services.Abstract;

namespace Sellevate.Company.Tests.Unit;

/// <summary>
/// Controller-level coverage for the AI-failure -> 503 propagation path: when the underlying
/// service throws (mirroring an ai-service failure surfaced through IBriefingAiClient /
/// IReadinessAiClient as InvalidOperationException), the endpoint must map it to 503, not 500
/// or an unhandled exception. Cache-unchanged behavior for the same failure path is covered at
/// the service layer in CompanyServiceTests (GenerateBriefingAsync_propagates_ai_failure_and_leaves_cache_unchanged,
/// GetReadinessAsync_propagates_ai_failure_and_leaves_cache_unchanged).
/// </summary>
[TestFixture]
public sealed class CompanyControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ICompanyService _companyService = null!;
    private CompanyController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _companyService = Substitute.For<ICompanyService>();
        _controller = new CompanyController(_companyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())],
                        "TestAuth"))
                }
            }
        };
    }

    [Test]
    public async Task GenerateBriefing_returns_503_when_service_throws_InvalidOperationException()
    {
        var companyId = Guid.NewGuid();
        _companyService
            .GenerateBriefingAsync(UserId, companyId, Arg.Any<CancellationToken>())
            .Returns<Sellevate.Company.Features.Companies.Models.CompanyBriefingDto?>(
                _ => throw new InvalidOperationException("AI briefing service returned 503."));

        var result = await _controller.GenerateBriefing(companyId, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task GetReadiness_returns_503_when_service_throws_InvalidOperationException()
    {
        var companyId = Guid.NewGuid();
        _companyService
            .GetReadinessAsync(UserId, companyId, Arg.Any<CancellationToken>())
            .Returns<Sellevate.Company.Features.Companies.Models.CompanyReadinessDto?>(
                _ => throw new InvalidOperationException("AI readiness service returned 503."));

        var result = await _controller.GetReadiness(companyId, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task GenerateBriefing_returns_503_on_transport_failure()
    {
        var companyId = Guid.NewGuid();
        _companyService
            .GenerateBriefingAsync(UserId, companyId, Arg.Any<CancellationToken>())
            .Returns<Sellevate.Company.Features.Companies.Models.CompanyBriefingDto?>(
                _ => throw new HttpRequestException("Connection refused"));

        var result = await _controller.GenerateBriefing(companyId, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task GetReadiness_returns_503_on_transport_failure()
    {
        var companyId = Guid.NewGuid();
        _companyService
            .GetReadinessAsync(UserId, companyId, Arg.Any<CancellationToken>())
            .Returns<Sellevate.Company.Features.Companies.Models.CompanyReadinessDto?>(
                _ => throw new HttpRequestException("Connection refused"));

        var result = await _controller.GetReadiness(companyId, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }
}
