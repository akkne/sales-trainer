using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Dialog;

/// <summary>
/// Phase 40.25. «Цитаты из диалогов, а не только цифры» — the РОП reads their team's graded
/// conversations and takes three lines to Monday's meeting (docs/TENANCY/ASSIGNMENTS.md §4).
///
/// <para>
/// <b>Why this lives in ai-service and not on the assignment dashboard.</b> Conversations are Mongo
/// documents owned by <see cref="IDialogSessionRepository"/>, which is the single holder of that
/// collection precisely because Mongo has no row-level security and a filter spread over two
/// services is a filter that will be forgotten in one of them (docs/TENANCY/TENANCY.md §1.6). The
/// screen therefore asks learning-service for the funnel and ai-service for the words, and no
/// service reads the other's store. See docs/DECISIONS.md (2026-08-18).
/// </para>
///
/// <para>
/// <b>A separate controller from <see cref="AdminDialogController"/>, for the reason that file
/// records.</b> That one authors the shared prompt library and is platform-staff-only; stacking a
/// second <c>[Authorize]</c> on an action there would AND the policies rather than OR them, so an
/// organization administrator would be refused by code that reads as if they were allowed.
/// </para>
///
/// <para>
/// <c>[TenantTransaction]</c> because the mode titles come from Postgres, where <c>SET LOCAL</c>
/// only takes effect inside a transaction. The organization is never read from the request — it
/// comes from <see cref="ITenantContext"/> via the gateway header (docs/TENANCY/TENANCY.md §1.3).
/// </para>
/// </summary>
[ApiController]
[Route("admin/dialog-sessions")]
[TenantScoped]
[TenantTransaction]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminDialogSessionsController(
    IDialogSessionRepository sessionRepository,
    AiDbContext databaseContext) : ControllerBase
{
    private const int DefaultPageSize = 25;

    /// <summary>
    /// The team's graded conversations, newest first. <c>maxScore</c> is the parameter that makes
    /// this useful: «покажи разговоры на 4 и ниже» is a list a РОП can act on, and «покажи все
    /// разговоры» is not.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminDialogSessionSummaryDto>>> GetSessions(
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? modeId = null,
        [FromQuery] int? maxScore = null,
        [FromQuery] int limit = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var sessions = await sessionRepository.ListGradedForOrganizationAsync(
            userId, modeId, maxScore, limit, cancellationToken);

        var modes = await ReadModesAsync(sessions.Select(session => session.ModeId), cancellationToken);

        return Ok(sessions
            .Select(session =>
            {
                var mode = modes.GetValueOrDefault(session.ModeId);

                return new AdminDialogSessionSummaryDto(
                    session.Id,
                    session.UserId,
                    session.BundleId,
                    session.ModeId,
                    mode?.Key,
                    mode?.Title,
                    session.Status.ToString().ToLowerInvariant(),
                    session.Messages.Count,
                    session.Feedback?.Score,
                    session.Feedback?.Summary,
                    session.AssignmentPracticeContext?.AssignmentId,
                    session.CreatedAt,
                    session.CompletedAt);
            })
            .ToList());
    }

    /// <summary>One conversation in full, with per-message indexes a quote can point at.</summary>
    [HttpGet("{sessionId}")]
    public async Task<ActionResult<AdminDialogTranscriptDto>> GetTranscript(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.FindForOrganizationAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var modes = await ReadModesAsync([session.ModeId], cancellationToken);
        var mode = modes.GetValueOrDefault(session.ModeId);

        return Ok(new AdminDialogTranscriptDto(
            session.Id,
            session.UserId,
            session.BundleId,
            session.ModeId,
            mode?.Key,
            mode?.Title,
            session.Status.ToString().ToLowerInvariant(),
            session.Feedback?.Score,
            session.Feedback is null ? null : DialogFeedbackDto.FromEntity(session.Feedback),
            session.AssignmentPracticeContext?.AssignmentId,
            session.CreatedAt,
            session.CompletedAt,
            session.Messages
                .Select((message, index) => new AdminDialogTranscriptMessageDto(
                    index, message.Role, message.Content, message.Timestamp))
                .ToList()));
    }

    /// <summary>
    /// Mode key and title for the sessions on this page. A mode the caller cannot see — a global row
    /// retired, or another organization's — simply yields no name; the conversation is still shown,
    /// because it happened and the transcript is the point.
    /// </summary>
    private async Task<Dictionary<Guid, DialogModeIdentity>> ReadModesAsync(
        IEnumerable<Guid> modeIds,
        CancellationToken cancellationToken)
    {
        var distinctModeIds = modeIds.Distinct().ToList();
        if (distinctModeIds.Count == 0)
        {
            return [];
        }

        return await databaseContext.DialogModes
            .AsNoTracking()
            .Where(mode => distinctModeIds.Contains(mode.Id))
            .Select(mode => new DialogModeIdentity(mode.Id, mode.Key, mode.Title))
            .ToDictionaryAsync(mode => mode.Id, cancellationToken);
    }

    private sealed record DialogModeIdentity(Guid Id, string Key, string Title);
}
