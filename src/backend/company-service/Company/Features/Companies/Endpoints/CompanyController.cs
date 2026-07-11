using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Company.Features.Companies.Exceptions;
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

    [HttpPut("companies/{companyId:guid}/status")]
    public async Task<ActionResult<CompanyDetailDto>> UpdateCompanyStatus(
        Guid companyId,
        [FromBody] UpdateCompanyStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var company = await companyService.UpdateCompanyStatusAsync(userId, companyId, request, cancellationToken);
            if (company is null)
                return NotFound();

            return Ok(company);
        }
        catch (ArgumentException argumentException)
        {
            return BadRequest(new { message = argumentException.Message });
        }
    }

    [HttpPut("companies/{companyId:guid}/follow-up")]
    public async Task<ActionResult<CompanyDetailDto>> UpdateCompanyFollowUp(
        Guid companyId,
        [FromBody] UpdateCompanyFollowUpRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var company = await companyService.UpdateCompanyFollowUpAsync(userId, companyId, request, cancellationToken);
        if (company is null)
            return NotFound();

        return Ok(company);
    }

    [HttpPost("companies/{companyId:guid}/briefing")]
    public async Task<IActionResult> GenerateBriefing(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var briefing = await companyService.GenerateBriefingAsync(userId, companyId, cancellationToken);
            if (briefing is null)
                return NotFound();

            return Ok(briefing);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return StatusCode(503, new { message = invalidOperationException.Message });
        }
        catch (HttpRequestException)
        {
            // Raw transport failure (ai-service unreachable / DNS) — surface as 503, not 500.
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }

    [HttpGet("companies/{companyId:guid}/briefing")]
    public async Task<IActionResult> GetBriefing(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var briefing = await companyService.GetBriefingAsync(userId, companyId, cancellationToken);
        if (briefing is null)
            return NotFound();

        if (briefing.Content is null)
            return NoContent();

        return Ok(briefing);
    }

    [HttpGet("companies/{companyId:guid}/readiness")]
    public async Task<IActionResult> GetReadiness(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var readiness = await companyService.GetReadinessAsync(userId, companyId, cancellationToken);
            if (readiness is null)
                return NotFound();

            if (readiness.Score is null)
                return NoContent();

            return Ok(readiness);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return StatusCode(503, new { message = invalidOperationException.Message });
        }
        catch (HttpRequestException)
        {
            // Raw transport failure (ai-service unreachable / DNS) — surface as 503, not 500.
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
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

    [HttpPost("companies/{companyId:guid}/logs/parse")]
    public async Task<IActionResult> ParseCallLog(
        Guid companyId,
        [FromBody] ParseCallLogRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var parsed = await companyService.ParseCallLogAsync(userId, companyId, request, cancellationToken);
            if (parsed is null)
                return NotFound();

            return Ok(parsed);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return StatusCode(503, new { message = invalidOperationException.Message });
        }
        catch (HttpRequestException)
        {
            // Raw transport failure (ai-service unreachable / DNS) — surface as 503, not 500.
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
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

        try
        {
            var entry = await companyService.CreateCallLogEntryAsync(userId, companyId, request, cancellationToken);
            if (entry is null)
                return NotFound();

            return Created($"/companies/{companyId}/logs/{entry.Id}", entry);
        }
        catch (ContactNotFoundInCompanyException contactNotFoundException)
        {
            return BadRequest(new { code = ContactNotFoundInCompanyException.Code, message = contactNotFoundException.Message });
        }
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

        try
        {
            var entry = await companyService.UpdateCallLogEntryAsync(userId, companyId, logId, request, cancellationToken);
            if (entry is null)
                return NotFound();

            return Ok(entry);
        }
        catch (ContactNotFoundInCompanyException contactNotFoundException)
        {
            return BadRequest(new { code = ContactNotFoundInCompanyException.Code, message = contactNotFoundException.Message });
        }
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

    [HttpGet("companies/{companyId:guid}/contacts")]
    public async Task<ActionResult<IReadOnlyList<CompanyContactDto>>> ListContacts(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var contacts = await companyService.ListContactsAsync(userId, companyId, cancellationToken);
        if (contacts is null)
            return NotFound();

        return Ok(contacts);
    }

    [HttpPost("companies/{companyId:guid}/contacts")]
    public async Task<ActionResult<CompanyContactDto>> CreateContact(
        Guid companyId,
        [FromBody] CreateCompanyContactRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var contact = await companyService.CreateContactAsync(userId, companyId, request, cancellationToken);
        if (contact is null)
            return NotFound();

        return Created($"/companies/{companyId}/contacts/{contact.Id}", contact);
    }

    [HttpPut("companies/{companyId:guid}/contacts/{contactId:guid}")]
    public async Task<ActionResult<CompanyContactDto>> UpdateContact(
        Guid companyId,
        Guid contactId,
        [FromBody] UpdateCompanyContactRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var contact = await companyService.UpdateContactAsync(userId, companyId, contactId, request, cancellationToken);
        if (contact is null)
            return NotFound();

        return Ok(contact);
    }

    [HttpDelete("companies/{companyId:guid}/contacts/{contactId:guid}")]
    public async Task<IActionResult> DeleteContact(
        Guid companyId,
        Guid contactId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var deleted = await companyService.DeleteContactAsync(userId, companyId, contactId, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpGet("companies/{companyId:guid}/personas")]
    public async Task<ActionResult<IReadOnlyList<CompanyPersonaDto>>> ListPersonas(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var personas = await companyService.ListPersonasAsync(userId, companyId, cancellationToken);
        if (personas is null)
            return NotFound();

        return Ok(personas);
    }

    [HttpPost("companies/{companyId:guid}/personas")]
    public async Task<ActionResult<CompanyPersonaDto>> CreatePersona(
        Guid companyId,
        [FromBody] CreateCompanyPersonaRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var persona = await companyService.CreatePersonaAsync(userId, companyId, request, cancellationToken);
        if (persona is null)
            return NotFound();

        return Created($"/companies/{companyId}/personas/{persona.Id}", persona);
    }

    [HttpDelete("companies/{companyId:guid}/personas/{personaId:guid}")]
    public async Task<IActionResult> DeletePersona(
        Guid companyId,
        Guid personaId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var deleted = await companyService.DeletePersonaAsync(userId, companyId, personaId, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPost("companies/{companyId:guid}/personas/generate")]
    public async Task<IActionResult> GeneratePersona(
        Guid companyId,
        [FromBody] GenerateCompanyPersonaRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            var persona = await companyService.GeneratePersonaAsync(userId, companyId, request, cancellationToken);
            if (persona is null)
                return NotFound();

            return Ok(persona);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return StatusCode(503, new { message = invalidOperationException.Message });
        }
        catch (HttpRequestException)
        {
            // Raw transport failure (ai-service unreachable / DNS) — surface as 503, not 500.
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
