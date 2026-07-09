using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Abstract;

namespace Sellevate.Company.Features.Companies.Endpoints;

[ApiController]
[Authorize]
public sealed class CompanyController(ICompanyService companyService) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<CompanySummaryDto>>> ListCompanies(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var companies = await companyService.ListCompaniesAsync(userId, search, cancellationToken);
        return Ok(companies);
    }

    [HttpPost("companies")]
    public async Task<ActionResult<CompanyDetailDto>> CreateCompany(
        [FromBody] CreateCompanyRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var company = await companyService.CreateCompanyAsync(userId, request, cancellationToken);
        return Created($"/companies/{company.Id}", company);
    }

    [HttpGet("companies/{companyId:guid}")]
    public async Task<ActionResult<CompanyDetailDto>> GetCompany(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var company = await companyService.GetCompanyAsync(userId, companyId, cancellationToken);
        if (company is null)
            return NotFound();

        return Ok(company);
    }

    [HttpPut("companies/{companyId:guid}")]
    public async Task<ActionResult<CompanyDetailDto>> UpdateCompany(
        Guid companyId,
        [FromBody] UpdateCompanyRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var company = await companyService.UpdateCompanyAsync(userId, companyId, request, cancellationToken);
        if (company is null)
            return NotFound();

        return Ok(company);
    }

    [HttpDelete("companies/{companyId:guid}")]
    public async Task<IActionResult> DeleteCompany(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var deleted = await companyService.DeleteCompanyAsync(userId, companyId, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpGet("companies/{companyId:guid}/logs")]
    public async Task<ActionResult<IReadOnlyList<CallLogEntryDto>>> ListCallLogEntries(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var entries = await companyService.ListCallLogEntriesAsync(userId, companyId, cancellationToken);
        if (entries is null)
            return NotFound();

        return Ok(entries);
    }

    [HttpPost("companies/{companyId:guid}/logs")]
    public async Task<ActionResult<CallLogEntryDto>> CreateCallLogEntry(
        Guid companyId,
        [FromBody] CreateCallLogEntryRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var entry = await companyService.CreateCallLogEntryAsync(userId, companyId, request, cancellationToken);
        if (entry is null)
            return NotFound();

        return Created($"/companies/{companyId}/logs/{entry.Id}", entry);
    }

    [HttpPut("companies/{companyId:guid}/logs/{logId:guid}")]
    public async Task<ActionResult<CallLogEntryDto>> UpdateCallLogEntry(
        Guid companyId,
        Guid logId,
        [FromBody] UpdateCallLogEntryRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var entry = await companyService.UpdateCallLogEntryAsync(userId, companyId, logId, request, cancellationToken);
        if (entry is null)
            return NotFound();

        return Ok(entry);
    }

    [HttpDelete("companies/{companyId:guid}/logs/{logId:guid}")]
    public async Task<IActionResult> DeleteCallLogEntry(
        Guid companyId,
        Guid logId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var deleted = await companyService.DeleteCallLogEntryAsync(userId, companyId, logId, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPost("companies/{companyId:guid}/practice-calls")]
    public async Task<ActionResult<PracticeCallDto>> CreatePracticeCall(
        Guid companyId,
        [FromBody] CreatePracticeCallRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var practiceCall = await companyService.CreatePracticeCallAsync(userId, companyId, request, cancellationToken);
        if (practiceCall is null)
            return NotFound();

        return Created($"/companies/{companyId}/practice-calls/{practiceCall.Id}", practiceCall);
    }

    [HttpGet("companies/{companyId:guid}/practice-calls")]
    public async Task<ActionResult<IReadOnlyList<PracticeCallDto>>> ListPracticeCalls(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var practiceCalls = await companyService.ListPracticeCallsAsync(userId, companyId, cancellationToken);
        if (practiceCalls is null)
            return NotFound();

        return Ok(practiceCalls);
    }

    [HttpGet("companies/{companyId:guid}/recent-goals")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRecentGoals(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var goals = await companyService.GetRecentGoalsAsync(userId, companyId, cancellationToken);
        if (goals is null)
            return NotFound();

        return Ok(goals);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
