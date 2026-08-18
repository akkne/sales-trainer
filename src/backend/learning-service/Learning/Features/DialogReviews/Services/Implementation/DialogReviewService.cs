using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.DialogReviews.Models;
using Sellevate.Learning.Features.DialogReviews.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;
using Sellevate.Learning.Infrastructure.Identity;

namespace Sellevate.Learning.Features.DialogReviews.Services.Implementation;

/// <summary>
/// Phase 40.25. The feedback loop of docs/TENANCY/ASSIGNMENTS.md §4.1.
///
/// <para>
/// <b>Every write starts from a <c>UserDialogScores</c> row and never from the request body.</b> The
/// caller names a session id; who held that conversation, on which scenario and with what grade are
/// read out of learning-db. That is what makes "the РОП cannot address a note at somebody else's
/// employee" a property of the query rather than a validation somebody has to remember: the score
/// row is under the same row-level-security policy as everything else here, so a session belonging
/// to another organization simply does not exist to this code.
/// </para>
///
/// <para>
/// <b>The frozen fields are frozen at write time on purpose.</b> The disputed grade, the mode key
/// and the quoted lines are copied into the row instead of being resolved on read. A dataset built
/// from values that can still move is a dataset of unprovable claims — 40.16's argument, in a table
/// whose entire second purpose is to be that dataset.
/// </para>
/// </summary>
internal sealed class DialogReviewService(
    LearningDbContext databaseContext,
    IOrganizationMemberDirectory memberDirectory,
    ILearningEventPublisher eventPublisher,
    ILogger<DialogReviewService> logger) : IDialogReviewService
{
    public async Task<DialogReviewNoteDto> CreateCoachingNoteAsync(
        Guid authorUserId,
        CreateCoachingNoteRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestDto);

        var sessionId = RequireSessionId(requestDto.SessionId);
        var comment = RequireComment(requestDto.Comment);
        var quotedText = requestDto.QuotedText?.Trim();

        if (string.IsNullOrEmpty(quotedText))
        {
            throw new DialogReviewValidationException(
                "A coaching note has to carry the lines it is about. Select a fragment of the conversation first.");
        }

        ValidateQuoteRange(requestDto.QuotedFromMessageIndex, requestDto.QuotedToMessageIndex);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var score = await FindScoreAsync(sessionId, cancellationToken);

        var note = new DialogReviewNote
        {
            Id = Guid.NewGuid(),
            Kind = DialogReviewKinds.CoachingNote,
            SessionId = score.SessionId,
            DialogModeKey = score.DialogModeKey,
            SubjectUserId = score.UserId,
            AuthorUserId = authorUserId,
            QuotedFromMessageIndex = requestDto.QuotedFromMessageIndex,
            QuotedToMessageIndex = requestDto.QuotedToMessageIndex,
            QuotedText = quotedText,
            Comment = comment,
            DisputedScore = score.Score,
            Status = DialogReviewStatuses.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // OrganizationId is stamped by the tenant save interceptor, like every other ITenantScoped
        // insert in this service — never assigned here.
        databaseContext.DialogReviewNotes.Add(note);

        await eventPublisher.PublishDialogReviewCommentedAsync(
            new DialogReviewCommentedEvent(note.Id, note.SubjectUserId, note.SessionId, quotedText, comment),
            cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Coaching note created NoteId={NoteId} SubjectUserId={SubjectUserId} SessionId={SessionId}",
            note.Id, note.SubjectUserId, note.SessionId);

        return ToDto(note, subjectDisplayName: null, authorDisplayName: null);
    }

    public async Task<DialogReviewNoteDto> CreateScoreDisputeAsync(
        Guid authorUserId,
        CreateScoreDisputeRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestDto);

        var sessionId = RequireSessionId(requestDto.SessionId);
        var comment = RequireComment(requestDto.Comment);

        ValidateQuoteRange(requestDto.QuotedFromMessageIndex, requestDto.QuotedToMessageIndex);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var score = await FindScoreAsync(sessionId, cancellationToken);

        if (score.UserId != authorUserId)
        {
            // Deliberately the same sentence as "no such conversation". Whether somebody else's
            // conversation exists is not this caller's business, and a distinguishable refusal would
            // turn the endpoint into a probe for other people's session ids.
            throw new DialogReviewValidationException(
                "There is no graded conversation of yours with that identifier.");
        }

        var alreadyOpen = await databaseContext.DialogReviewNotes
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.SessionId == score.SessionId
                             && candidate.Kind == DialogReviewKinds.ScoreDispute
                             && candidate.Status == DialogReviewStatuses.Open,
                cancellationToken);

        if (alreadyOpen)
        {
            // One open dispute per conversation. Not a nicety: a queue that can be filled with
            // duplicates of one complaint is a queue the РОП stops opening, and the mechanism only
            // works while they do.
            throw new DialogReviewValidationException(
                "You have already disputed the score for this conversation and it has not been reviewed yet.");
        }

        var note = new DialogReviewNote
        {
            Id = Guid.NewGuid(),
            Kind = DialogReviewKinds.ScoreDispute,
            SessionId = score.SessionId,
            DialogModeKey = score.DialogModeKey,
            SubjectUserId = score.UserId,
            AuthorUserId = authorUserId,
            QuotedFromMessageIndex = requestDto.QuotedFromMessageIndex,
            QuotedToMessageIndex = requestDto.QuotedToMessageIndex,
            QuotedText = requestDto.QuotedText?.Trim(),
            Comment = comment,
            DisputedScore = score.Score,
            Status = DialogReviewStatuses.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        databaseContext.DialogReviewNotes.Add(note);

        // Phase 40.26 closes the gap 40.25 left open here: administrators are now enumerable, so a
        // filed dispute pushes rather than only queuing.
        await PublishDisputeNoticesAsync(note, authorUserId, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Score dispute filed NoteId={NoteId} UserId={UserId} SessionId={SessionId} DisputedScore={DisputedScore}",
            note.Id, note.AuthorUserId, note.SessionId, note.DisputedScore);

        return ToDto(note, subjectDisplayName: null, authorDisplayName: null);
    }

    /// <summary>
    /// Phase 40.26. Tells whoever administers the organization that a dispute is waiting.
    ///
    /// <para>
    /// <b>Fail-open, unlike issuing an assignment and unlike the reminder.</b> Those two decide who
    /// is asked to do work, and a wrong answer there is silent and permanent. This one decides who
    /// hears about a row that has already been written and is already visible in
    /// <c>GET /admin/dialog-reviews</c>. Refusing the dispute because identity-service is slow would
    /// take away the mechanism that exists to keep the team trusting the numbers, in order to protect
    /// a notification — the wrong trade, and the same one 40.25's dashboard made in the same
    /// direction.
    /// </para>
    ///
    /// <para>
    /// An administrator who filed the dispute themselves is skipped. Nothing in the platform stops a
    /// РОП from practising, and a notice telling somebody that somebody has disputed a score, where
    /// both are them, is the product not paying attention.
    /// </para>
    /// </summary>
    private async Task PublishDisputeNoticesAsync(
        DialogReviewNote note,
        Guid authorUserId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid>? administratorIds;
        try
        {
            administratorIds = (await memberDirectory.GetRosterAsync(cancellationToken)).AdministratorIds;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Dispute {NoteId} was filed but its notice could not be addressed: the organization "
                + "roster could not be read. It is still in the review queue.",
                note.Id);

            return;
        }

        if (administratorIds is null || administratorIds.Count == 0)
        {
            return;
        }

        var subjectDisplayName = await databaseContext.UserReplicas
            .AsNoTracking()
            .Where(replica => replica.UserId == note.SubjectUserId)
            .Select(replica => replica.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var administratorId in administratorIds.Where(candidate => candidate != authorUserId))
        {
            await eventPublisher.PublishDialogReviewDisputedAsync(
                new DialogReviewDisputedEvent(
                    note.Id,
                    administratorId,
                    note.SubjectUserId,
                    subjectDisplayName,
                    note.SessionId,
                    note.DisputedScore,
                    note.Comment),
                cancellationToken);
        }
    }

    public async Task<DialogReviewNoteDto?> ResolveScoreDisputeAsync(
        Guid actorUserId,
        Guid noteId,
        ResolveScoreDisputeRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestDto);

        var outcome = requestDto.Outcome?.Trim().ToLowerInvariant();
        if (outcome is not (DialogReviewStatuses.Upheld or DialogReviewStatuses.Rejected))
        {
            throw new DialogReviewValidationException(
                $"A dispute is closed as '{DialogReviewStatuses.Upheld}' or '{DialogReviewStatuses.Rejected}'.");
        }

        var resolution = requestDto.Resolution?.Trim();
        if (outcome == DialogReviewStatuses.Rejected && string.IsNullOrEmpty(resolution))
        {
            throw new DialogReviewValidationException(
                "Rejecting a dispute needs a reason. A complaint closed in silence is the black box "
                + "this mechanism exists to open.");
        }

        if (requestDto.AdjustedScore is { } adjustedScore)
        {
            if (outcome != DialogReviewStatuses.Upheld)
            {
                throw new DialogReviewValidationException(
                    "A corrected score belongs on an upheld dispute. Rejecting one leaves the grade as it stands.");
            }

            if (adjustedScore is < 0 or > 100)
            {
                throw new DialogReviewValidationException("A corrected score is between 0 and 100.");
            }
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var note = await databaseContext.DialogReviewNotes
            .FirstOrDefaultAsync(
                candidate => candidate.Id == noteId && candidate.Kind == DialogReviewKinds.ScoreDispute,
                cancellationToken);

        if (note is null)
        {
            return null;
        }

        if (note.Status != DialogReviewStatuses.Open)
        {
            throw new DialogReviewValidationException("This dispute has already been reviewed.");
        }

        note.Status = outcome;
        note.Resolution = resolution;
        note.AdjustedScore = requestDto.AdjustedScore;
        note.ResolvedBy = actorUserId;
        note.ResolvedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;

        await eventPublisher.PublishDialogReviewResolvedAsync(
            new DialogReviewResolvedEvent(
                note.Id, note.AuthorUserId, note.SessionId, outcome, note.DisputedScore, note.AdjustedScore, resolution),
            cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Score dispute resolved NoteId={NoteId} Outcome={Outcome} ActorId={ActorId}",
            note.Id, outcome, actorUserId);

        return ToDto(note, subjectDisplayName: null, authorDisplayName: null);
    }

    public async Task<DialogReviewNoteDto?> AcknowledgeCoachingNoteAsync(
        Guid actorUserId,
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var note = await databaseContext.DialogReviewNotes
            .FirstOrDefaultAsync(
                candidate => candidate.Id == noteId
                             && candidate.Kind == DialogReviewKinds.CoachingNote
                             && candidate.SubjectUserId == actorUserId,
                cancellationToken);

        if (note is null)
        {
            return null;
        }

        if (note.Status == DialogReviewStatuses.Acknowledged)
        {
            // Idempotent rather than a conflict: the button is on a screen that can be opened twice,
            // and "I have read this" is not a fact that can be true differently the second time.
            return ToDto(note, subjectDisplayName: null, authorDisplayName: null);
        }

        note.Status = DialogReviewStatuses.Acknowledged;
        note.ResolvedBy = actorUserId;
        note.ResolvedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return ToDto(note, subjectDisplayName: null, authorDisplayName: null);
    }

    public async Task<IReadOnlyList<DialogReviewNoteDto>> GetForOrganizationAsync(
        string? kind,
        string? status,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var query = databaseContext.DialogReviewNotes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var requestedKind = kind.Trim();
            if (!DialogReviewKinds.IsKnown(requestedKind))
            {
                throw new DialogReviewValidationException($"'{kind}' is not a known review kind.");
            }

            query = query.Where(note => note.Kind == requestedKind);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var requestedStatus = status.Trim();
            if (!DialogReviewStatuses.IsKnown(requestedStatus))
            {
                throw new DialogReviewValidationException($"'{status}' is not a known review status.");
            }

            query = query.Where(note => note.Status == requestedStatus);
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var requestedSessionId = sessionId.Trim();
            query = query.Where(note => note.SessionId == requestedSessionId);
        }

        return await ProjectAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<DialogReviewNoteDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        // Subject or author: a coaching note reaches them as its subject, a dispute is theirs as its
        // author, and both belong on the same screen because both are "the conversation about my
        // conversations".
        var query = databaseContext.DialogReviewNotes
            .AsNoTracking()
            .Where(note => note.SubjectUserId == userId || note.AuthorUserId == userId);

        return await ProjectAsync(query, cancellationToken);
    }

    /// <summary>
    /// Newest first, with names attached from <c>UserReplicas</c> in one round trip. Absent ids
    /// simply have no name — learning-service does not own identities and inventing a placeholder
    /// here would make the screen unable to tell a missing replica from a real display name.
    /// </summary>
    private async Task<IReadOnlyList<DialogReviewNoteDto>> ProjectAsync(
        IQueryable<DialogReviewNote> query,
        CancellationToken cancellationToken)
    {
        var notes = await query
            .OrderByDescending(note => note.CreatedAt)
            .ThenByDescending(note => note.Id)
            .ToListAsync(cancellationToken);

        if (notes.Count == 0)
        {
            return [];
        }

        var userIds = notes
            .SelectMany(note => new[] { note.SubjectUserId, note.AuthorUserId })
            .Distinct()
            .ToList();

        var displayNames = await databaseContext.UserReplicas
            .AsNoTracking()
            .Where(replica => userIds.Contains(replica.UserId))
            .ToDictionaryAsync(replica => replica.UserId, replica => replica.DisplayName, cancellationToken);

        return notes
            .Select(note => ToDto(
                note,
                displayNames.GetValueOrDefault(note.SubjectUserId),
                displayNames.GetValueOrDefault(note.AuthorUserId)))
            .ToList();
    }

    /// <summary>
    /// The score row this note is about. Its absence is the refusal that also covers "that session
    /// belongs to another organization" — see the class remarks.
    /// </summary>
    private async Task<UserDialogScoreRow> FindScoreAsync(string sessionId, CancellationToken cancellationToken)
    {
        var score = await databaseContext.UserDialogScores
            .AsNoTracking()
            .Where(candidate => candidate.SessionId == sessionId)
            .Select(candidate => new UserDialogScoreRow(
                candidate.SessionId, candidate.UserId, candidate.DialogModeKey, candidate.Score))
            .FirstOrDefaultAsync(cancellationToken);

        return score ?? throw new DialogReviewValidationException(
            "There is no graded conversation with that identifier. Only conversations the AI has "
            + "already scored can be commented on or disputed.");
    }

    private static string RequireSessionId(string? sessionId)
    {
        var trimmed = sessionId?.Trim();

        return string.IsNullOrEmpty(trimmed)
            ? throw new DialogReviewValidationException("A review has to name the conversation it is about.")
            : trimmed;
    }

    private static string RequireComment(string? comment)
    {
        var trimmed = comment?.Trim();

        return string.IsNullOrEmpty(trimmed)
            ? throw new DialogReviewValidationException("Say something. An empty comment is not feedback.")
            : trimmed;
    }

    private static void ValidateQuoteRange(int? fromIndex, int? toIndex)
    {
        if (fromIndex is < 0 || toIndex is < 0)
        {
            throw new DialogReviewValidationException("A quoted fragment starts at message 0 or later.");
        }

        if (fromIndex is { } from && toIndex is { } to && to < from)
        {
            throw new DialogReviewValidationException("A quoted fragment cannot end before it starts.");
        }
    }

    private static DialogReviewNoteDto ToDto(
        DialogReviewNote note,
        string? subjectDisplayName,
        string? authorDisplayName)
        => new(
            note.Id,
            note.Kind,
            note.Status,
            note.SessionId,
            note.DialogModeKey,
            note.SubjectUserId,
            subjectDisplayName,
            note.AuthorUserId,
            authorDisplayName,
            note.QuotedFromMessageIndex,
            note.QuotedToMessageIndex,
            note.QuotedText,
            note.Comment,
            note.DisputedScore,
            note.Resolution,
            note.AdjustedScore,
            note.ResolvedBy,
            note.ResolvedAt,
            note.CreatedAt,
            note.UpdatedAt);

    private sealed record UserDialogScoreRow(string SessionId, Guid UserId, string DialogModeKey, int Score);
}
