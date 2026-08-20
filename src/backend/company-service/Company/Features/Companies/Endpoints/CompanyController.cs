using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies.Constants;
using Sellevate.Company.Features.Companies.Exceptions;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Abstract;

namespace Sellevate.Company.Features.Companies.Endpoints;

/// <summary>
/// HTTP surface for the CRM: companies and their call logs, practice calls, contacts and personas,
/// plus the four AI-backed endpoints. Holds no business logic — every action resolves the caller's
/// user id from the token, delegates to <see cref="ICompanyService"/>, and maps the result to a
/// status code.
///
/// <para>
/// The mapping is the contract worth respecting. A null service result means "no such row for this
/// caller" and becomes 404, never 403: whether a company exists is itself scoped, so distinguishing
/// "not yours" from "not there" would confirm a competitor's row to anyone who guessed its id. A
/// token with no usable subject is 401. An unresolvable AI dependency is 503, because nothing was
/// written and a retry may succeed.
/// </para>
/// </summary>
[ApiController]
[TenantScoped]
[Authorize]
public sealed class CompanyController(ICompanyService companyService) : ControllerBase
{
    [HttpGet(CompanyRouteTemplates.Companies)]
    public async Task<ActionResult<IReadOnlyList<CompanySummaryDto>>> ListCompanies(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var companies = await companyService.ListCompaniesAsync(userId, search, cancellationToken);
        return Ok(companies);
    }

    [HttpPost(CompanyRouteTemplates.Companies)]
    public async Task<ActionResult<CompanyDetailDto>> CreateCompany(
        [FromBody] CreateCompanyRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var company = await companyService.CreateCompanyAsync(userId, request, cancellationToken);
        return Created(CompanyRouteTemplates.CompanyLocation(company.Id), company);
    }

    [HttpGet(CompanyRouteTemplates.CompanyById)]
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

    [HttpPut(CompanyRouteTemplates.CompanyById)]
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

    [HttpPut(CompanyRouteTemplates.CompanyStatus)]
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

    [HttpPut(CompanyRouteTemplates.CompanyFollowUp)]
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

    [HttpPost(CompanyRouteTemplates.CompanyBriefing)]
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
            return AiServiceUnavailable(invalidOperationException.Message);
        }
        catch (HttpRequestException)
        {
            return AiServiceUnavailable(CompanyErrorMessages.AiServiceUnavailable);
        }
    }

    [HttpGet(CompanyRouteTemplates.CompanyBriefing)]
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

    [HttpGet(CompanyRouteTemplates.CompanyReadiness)]
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
            return AiServiceUnavailable(invalidOperationException.Message);
        }
        catch (HttpRequestException)
        {
            return AiServiceUnavailable(CompanyErrorMessages.AiServiceUnavailable);
        }
    }

    [HttpDelete(CompanyRouteTemplates.CompanyById)]
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

    [HttpPost(CompanyRouteTemplates.CompanyCallLogParse)]
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
            return AiServiceUnavailable(invalidOperationException.Message);
        }
        catch (HttpRequestException)
        {
            return AiServiceUnavailable(CompanyErrorMessages.AiServiceUnavailable);
        }
    }

    [HttpGet(CompanyRouteTemplates.CompanyCallLogs)]
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

    [HttpPost(CompanyRouteTemplates.CompanyCallLogs)]
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

            return Created(CompanyRouteTemplates.CallLogLocation(companyId, entry.Id), entry);
        }
        catch (ContactNotFoundInCompanyException contactNotFoundException)
        {
            return BadRequest(new { code = ContactNotFoundInCompanyException.Code, message = contactNotFoundException.Message });
        }
    }

    [HttpPut(CompanyRouteTemplates.CompanyCallLogById)]
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

    [HttpDelete(CompanyRouteTemplates.CompanyCallLogById)]
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

    [HttpPost(CompanyRouteTemplates.CompanyPracticeCalls)]
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

        return Created(CompanyRouteTemplates.PracticeCallLocation(companyId, practiceCall.Id), practiceCall);
    }

    [HttpGet(CompanyRouteTemplates.CompanyPracticeCalls)]
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

    [HttpGet(CompanyRouteTemplates.CompanyRecentGoals)]
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

    [HttpGet(CompanyRouteTemplates.CompanyContacts)]
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

    [HttpPost(CompanyRouteTemplates.CompanyContacts)]
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

        return Created(CompanyRouteTemplates.ContactLocation(companyId, contact.Id), contact);
    }

    [HttpPut(CompanyRouteTemplates.CompanyContactById)]
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

    [HttpDelete(CompanyRouteTemplates.CompanyContactById)]
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

    [HttpGet(CompanyRouteTemplates.CompanyPersonas)]
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

    [HttpPost(CompanyRouteTemplates.CompanyPersonas)]
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

        return Created(CompanyRouteTemplates.PersonaLocation(companyId, persona.Id), persona);
    }

    [HttpDelete(CompanyRouteTemplates.CompanyPersonaById)]
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

    [HttpPost(CompanyRouteTemplates.CompanyPersonaGenerate)]
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
            return AiServiceUnavailable(invalidOperationException.Message);
        }
        catch (HttpRequestException)
        {
            return AiServiceUnavailable(CompanyErrorMessages.AiServiceUnavailable);
        }
    }

    /// <summary>
    /// Reads the caller's user id from the validated access token. Returns false — never throws and
    /// never falls back to a default id — when the token carries no parseable subject, so a
    /// malformed-but-signed token cannot be mistaken for a real user.
    /// </summary>
    private bool TryGetCurrentUserId(out Guid userId)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }

    /// <summary>
    /// The single shape of an AI-backed failure response: 503 with a <c>message</c> body. Both
    /// causes land here — ai-service refusing the call (<see cref="InvalidOperationException"/>,
    /// whose message describes the refusal) and never reaching it at all
    /// (<see cref="HttpRequestException"/>, which gets the generic message because a DNS or socket
    /// error says nothing a caller can act on). 503 rather than 500 because the request is worth
    /// retrying and nothing was persisted.
    /// </summary>
    private ObjectResult AiServiceUnavailable(string message)
        => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message });
}
