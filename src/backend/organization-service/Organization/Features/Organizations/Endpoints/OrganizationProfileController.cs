using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;
using Sellevate.Organization.Features.Organizations.Services.Implementation;

namespace Sellevate.Organization.Features.Organizations.Endpoints;

/// <summary>
/// Reads and writes the caller's own organization profile (docs/TENANCY/CONTENT_MODEL.md §3).
/// <see cref="TenantScopedAttribute"/> makes <c>TenantContextMiddleware</c> reject any request
/// with no <c>X-Organization-Id</c> header with 403 before it reaches this controller — there is
/// no route parameter for the organization id, by design (docs/TENANCY/TENANCY.md §1.3).
///
/// <para>
/// <b>Phase 40.29 turned the form into an interview.</b> The two original verbs — read the profile,
/// write all seven fields — are the thirty-field form the roadmap says nobody fills in, and an empty
/// profile is the state in which 40.19's <c>{{organization.*}}</c> substitution does nothing at all.
/// The four routes added here are the other path to the same row: what is still missing
/// (<c>GET …/gaps</c>), one answer at a time (<c>PATCH</c>), and the promotion of a structure the
/// 40.27 pipeline already extracted from the customer's own material (<c>POST …/draft</c> to look,
/// <c>POST …/draft/apply</c> to commit).
/// </para>
///
/// <para>
/// <b>The class-level gate stays open and every writing route is gated.</b> Reading the profile
/// is something the organization's own members legitimately do — <c>OrganizationControllerAuthorizationTests</c>
/// asserts that policy is null for exactly that reason — but promoting a draft, answering the
/// interview and replacing the profile wholesale are the РОП's job, and they are the routes on this
/// controller that can change what every lesson in the organization says.
/// </para>
///
/// <para>
/// <b>40.34 closed the <c>PUT</c> hole.</b> Until the final acceptance block <c>PUT</c> carried only
/// the class-level <c>[Authorize]</c>, so any member of the organization could replace all seven
/// columns at once — including <c>banned_claims</c>, which binds both the AI persona and the grader.
/// Emptying it made the organization's AI coach its reps into the exact promises compliance forbade,
/// and a crafted entry landed attacker text in a <b>system</b> prompt. It is now gated like its two
/// siblings below.
/// </para>
/// </summary>
[ApiController]
[Route(RouteConstants.OrganizationProfileBase)]
[Authorize]
[TenantScoped]
public sealed class OrganizationProfileController(IOrganizationProfileService organizationProfileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrganizationProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await organizationProfileService.GetProfileAsync(cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
    public async Task<ActionResult<OrganizationProfileDto>> UpdateProfile(
        [FromBody] UpdateOrganizationProfileRequestDto request,
        CancellationToken cancellationToken)
    {
        var profile = await organizationProfileService.UpsertProfileAsync(request, cancellationToken);
        return Ok(profile);
    }

    /// <summary>
    /// Phase 40.29. The interview: the next few questions, hardest-hitting first.
    ///
    /// <para>
    /// <b>200 with seven questions, never 404.</b> An organization that has never saved a profile is
    /// the exact case the block exists for, and «не найдено» is the least useful thing to tell them
    /// about it.
    /// </para>
    /// </summary>
    /// <param name="limit">
    /// How many questions to return. Defaults to three and is clamped to one … seven; the small
    /// default is the roadmap's «5 минут вместо часа» expressed as an integer.
    /// </param>
    [HttpGet("gaps")]
    public async Task<ActionResult<OrganizationProfileGapsDto>> GetGaps(
        [FromQuery] int limit = OrganizationProfileGapInspector.DefaultQuestionLimit,
        CancellationToken cancellationToken = default)
        => Ok(await organizationProfileService.GetGapsAsync(limit, cancellationToken));

    /// <summary>
    /// Phase 40.29. One answer to one question. Omitted fields keep their stored value, which is what
    /// makes answering a single question safe while somebody else answers another.
    /// </summary>
    [HttpPatch]
    [Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
    public async Task<ActionResult<OrganizationProfileDto>> PatchProfile(
        [FromBody] PatchOrganizationProfileRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await organizationProfileService.PatchProfileAsync(request, cancellationToken));

    /// <summary>
    /// Phase 40.29. «Вот что ИИ вытащил из ваших материалов» — what promoting this draft would do to
    /// each field, and which questions would still be left afterwards. Writes nothing.
    /// </summary>
    [HttpPost("draft")]
    public async Task<ActionResult<OrganizationProfileDraftPreviewDto>> PreviewDraft(
        [FromBody] ExtractedProfileDraftDto request,
        CancellationToken cancellationToken)
        => Ok(await organizationProfileService.PreviewDraftAsync(request, cancellationToken));

    /// <summary>
    /// Phase 40.29. «Перенести в профиль». Blanks are filled and lists grow without asking; a field
    /// that already had a value is replaced only when its name appears in <c>acceptedFields</c>, and
    /// <c>banned_claims</c> is never replaced at all.
    /// </summary>
    [HttpPost("draft/apply")]
    [Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
    public async Task<ActionResult<OrganizationProfileDraftAppliedDto>> ApplyDraft(
        [FromBody] ApplyOrganizationProfileDraftRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await organizationProfileService.ApplyDraftAsync(request, cancellationToken));
        }
        catch (OrganizationProfileValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }
}
