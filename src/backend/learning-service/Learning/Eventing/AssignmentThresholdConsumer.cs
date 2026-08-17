using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Eventing;

/// <summary>
/// Phase 40.22. Re-judges a person's open assignments whenever they finish a piece of graded work
/// (docs/TENANCY/ASSIGNMENTS.md §1.1, docs/TENANCY/BACKGROUND_JOBS.md §2.2).
///
/// <para>
/// <b>Why a consumer and not a call inside the submit path.</b> Half the evidence arrives from
/// another service: a conversation is graded in ai-service and learning-service only ever hears
/// about it on <c>dialog.evaluated</c>. Having exercises take a synchronous in-process path and
/// conversations an asynchronous one would mean two writers of the same two columns, with two
/// different failure modes and two different idempotency stories. One consumer over both topics
/// keeps a single writer, and it keeps the learner's submit request free of work they are not
/// waiting for.
/// </para>
///
/// <para>
/// <b>Both events are treated as triggers, not as data.</b> Nothing here adds a number to a
/// counter. <c>exercise.completed</c> is used for its <c>userId</c> and nothing else — the attempt
/// rows it describes are already in this service's own database, and the evaluator recomputes from
/// them. <c>dialog.evaluated</c> carries one fact this service cannot otherwise obtain, the grade,
/// so it is written into <see cref="UserDialogScore"/> under a unique index on
/// <c>(organization, user, session)</c>; a redelivery of either event therefore leaves exactly the
/// same rows behind. That is the whole answer to "reprocessing must not inflate the attempt count".
/// </para>
///
/// <para>
/// <b>Tenant mode.</b> Default <c>RequiresOrganization = true</c>, like 40.19's
/// <see cref="OrganizationProfileConsumer"/> and unlike the replica projections: assignment progress
/// and dialog scores are strict tenant data under plain-equality policies, so the write has to land
/// with the envelope's organization in context. An envelope without one is retried and
/// dead-lettered rather than guessed at — which matters here more than usual, because 40.14 found
/// this very topic being published with no organization at all and the loss was silent.
/// </para>
/// </summary>
internal sealed class AssignmentThresholdConsumer : KafkaConsumerBackgroundService
{
    public AssignmentThresholdConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<AssignmentThresholdConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.DialogEvaluated,
        BuildingBlocks.Eventing.Topics.ExerciseCompleted,
    ];

    protected override async Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var userId = envelope.Type == BuildingBlocks.Eventing.Topics.DialogEvaluated
            ? await RecordDialogScoreAsync(envelope, scopedServices, cancellationToken)
            : envelope.DataAs<ExerciseCompletedIntegrationEvent>()?.UserId;

        if (userId is null || userId.Value == Guid.Empty)
        {
            return;
        }

        var evaluator = scopedServices.GetRequiredService<IAssignmentThresholdEvaluator>();

        await evaluator.EvaluateForUserAsync(userId.Value, cancellationToken);
    }

    /// <summary>
    /// Mirrors one graded conversation into learning-db, and returns whose it was.
    ///
    /// <para>
    /// The insert is guarded by a read rather than left to the unique index alone because the common
    /// case is a fresh event and a duplicate-key exception would abort the ambient transaction,
    /// taking the evaluation that follows down with it. The index is still the guarantee; this is
    /// only the cheap path.
    /// </para>
    /// </summary>
    private static async Task<Guid?> RecordDialogScoreAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var payload = envelope.DataAs<DialogEvaluatedEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.SessionId))
        {
            return null;
        }

        // A conversation published by a producer that predates 40.22 carries no mode key, and there
        // is no way to recover one. Recording a score under an empty key would make it match no
        // assignment while still counting as an attempt — a person credited with a try they cannot
        // be judged on. The score is dropped and the user is still re-evaluated, so any exercise
        // work of theirs is not held up by it.
        if (string.IsNullOrWhiteSpace(payload.ModeKey))
        {
            return payload.UserId;
        }

        var databaseContext = scopedServices.GetRequiredService<LearningDbContext>();
        var sessionId = payload.SessionId.Trim();

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var alreadyRecorded = await databaseContext.UserDialogScores
            .AnyAsync(
                score => score.UserId == payload.UserId && score.SessionId == sessionId,
                cancellationToken);

        if (alreadyRecorded)
        {
            return payload.UserId;
        }

        databaseContext.UserDialogScores.Add(new UserDialogScore
        {
            Id = Guid.NewGuid(),
            UserId = payload.UserId,
            SessionId = sessionId,
            DialogModeKey = payload.ModeKey.Trim(),
            DialogModeId = payload.ModeId,
            Score = Math.Clamp(payload.QualityScore, 0, 100),
            EvaluatedAt = envelope.OccurredAt.UtcDateTime,
        });

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return payload.UserId;
    }
}
