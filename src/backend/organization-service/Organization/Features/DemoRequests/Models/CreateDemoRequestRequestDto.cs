using System.ComponentModel.DataAnnotations;

namespace Sellevate.Organization.Features.DemoRequests.Models;

/// <summary>
/// A visitor's submission of the public "Request a demo" form. <see cref="Website"/> is a honeypot: a
/// hidden field no human ever fills in, so a non-empty value marks the submission as automated (see
/// <c>DemoRequestService.SubmitAsync</c>). It lives on this DTO rather than a separate one so
/// <c>[ApiController]</c> model-binds it exactly like every other field, with nothing to give away
/// that it is treated differently.
///
/// <para>
/// <see cref="ConsentGiven"/> (required data processing) and <see cref="MarketingConsentGiven"/>
/// (optional marketing outreach) are deliberately two separate fields rather than one: 152-ФЗ/GDPR
/// guidance treats them as two distinct purposes, and a single checkbox bundling both would force a
/// visitor to accept marketing email just to request a demo. <see cref="MarketingConsentGiven"/>
/// carries no <c>[Required]</c> or <c>Range</c> constraint — <see langword="false"/> is exactly as
/// valid an answer as <see langword="true"/>.
/// </para>
///
/// <para>
/// <see cref="Phone"/> is required even though it is not needed to reply to the lead — <see cref="WorkEmail"/>
/// already covers that. It is required because the sales motion this form feeds is phone-first: both
/// Russian vendors whose live forms could actually be read (Talent Rocks, Эквио) require it, and CIS B2B
/// sales moves faster over a call than over email. That is a business decision about how sales works
/// here, not a technical requirement of the endpoint.
/// </para>
///
/// <para>
/// <see cref="SalesTeamSize"/> is <b>nullable on purpose</b>, even though the stored column is not.
/// <c>[Required]</c> on a non-nullable enum never fails validation: a body that omits the field binds
/// to the zero member — <c>UpToFive</c> — and the smallest team-size bucket gets recorded as though
/// somebody had chosen it. Making the property nullable is what gives the attribute something to
/// reject, so an omitted answer is a <c>400</c> rather than a silently invented one. This matters
/// here more than on an authenticated route, because the form is public and the qualifier is the
/// single most useful field on it.
/// </para>
/// </summary>
public sealed record CreateDemoRequestRequestDto(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(200)] string WorkEmail,
    [Required, MaxLength(40)] string Phone,
    [Required, MaxLength(200)] string CompanyName,
    [MaxLength(120)] string? JobTitle,
    [Required] SalesTeamSize? SalesTeamSize,
    [MaxLength(2000)] string? Comment,
    [Required, Range(typeof(bool), "true", "true")] bool ConsentGiven,
    bool MarketingConsentGiven,
    [MaxLength(200)] string? Website);
