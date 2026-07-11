using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Company.Features.Companies.Endpoints;
using Sellevate.Company.Features.Companies.Exceptions;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Abstract;

namespace Sellevate.Company.Tests.Unit;

/// <summary>
/// Controller-level coverage that doesn't fit CompanyServiceTests: the AI-failure -> 503
/// propagation path (when the underlying service throws, mirroring an ai-service failure
/// surfaced through IBriefingAiClient/IReadinessAiClient as InvalidOperationException, or a raw
/// HttpRequestException on transport failure, the endpoint must map it to 503, not 500 or an
/// unhandled exception — cache-unchanged behavior for the same failure path is covered at the
/// service layer in CompanyServiceTests), and the ContactId-hardening 400 mapping (39.17 PR #19/
/// #28 carry-over: ContactNotFoundInCompanyException -> 400 on the call-log create/update
/// endpoints).
/// </summary>
[TestFixture]
public sealed class CompanyControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

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

    private static CreateCallLogEntryRequestDto CreateLogRequest(Guid? contactId = null) =>
        new("Иван", "pitch", "ok", DateTime.UtcNow, contactId);

    private static UpdateCallLogEntryRequestDto UpdateLogRequest(Guid? contactId = null) =>
        new("Иван", "pitch", "ok", DateTime.UtcNow, contactId);

    [Test]
    public async Task GenerateBriefing_returns_503_when_service_throws_InvalidOperationException()
    {
        var companyId = Guid.NewGuid();
        _companyService
            .GenerateBriefingAsync(UserId, companyId, Arg.Any<CancellationToken>())
            .Returns<CompanyBriefingDto?>(
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
            .Returns<CompanyReadinessDto?>(
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
            .Returns<CompanyBriefingDto?>(
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
            .Returns<CompanyReadinessDto?>(
                _ => throw new HttpRequestException("Connection refused"));

        var result = await _controller.GetReadiness(companyId, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task CreateCallLogEntry_ReturnsBadRequest_OnContactNotFoundInCompanyException()
    {
        var contactId = Guid.NewGuid();
        _companyService
            .CreateCallLogEntryAsync(UserId, CompanyId, Arg.Any<CreateCallLogEntryRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<CallLogEntryDto?>(_ => throw new ContactNotFoundInCompanyException(contactId, CompanyId));

        var result = await _controller.CreateCallLogEntry(CompanyId, CreateLogRequest(contactId), CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { code = "CONTACT_NOT_FOUND", message = "Указанный контакт не найден в этой компании." });
    }

    [Test]
    public async Task UpdateCallLogEntry_ReturnsBadRequest_OnContactNotFoundInCompanyException()
    {
        var contactId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        _companyService
            .UpdateCallLogEntryAsync(UserId, CompanyId, logId, Arg.Any<UpdateCallLogEntryRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<CallLogEntryDto?>(_ => throw new ContactNotFoundInCompanyException(contactId, CompanyId));

        var result = await _controller.UpdateCallLogEntry(CompanyId, logId, UpdateLogRequest(contactId), CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { code = "CONTACT_NOT_FOUND", message = "Указанный контакт не найден в этой компании." });
    }

    [Test]
    public async Task CreateCallLogEntry_ReturnsCreated_WhenServiceSucceeds()
    {
        var entry = new CallLogEntryDto(Guid.NewGuid(), CompanyId, "Иван", "pitch", "ok", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, null);
        _companyService
            .CreateCallLogEntryAsync(UserId, CompanyId, Arg.Any<CreateCallLogEntryRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(entry);

        var result = await _controller.CreateCallLogEntry(CompanyId, CreateLogRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<CreatedResult>();
    }
}
