using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Ai.Eventing;

/// <summary>
/// Mirrors gamification-service's dialog scoring weights into ai-service's in-memory
/// <see cref="IDialogScoringWeightsProvider"/>, which is a singleton.
///
/// <para>
/// <b>Tenancy: system mode, declared (40.14 audit).</b> The weights live in
/// gamification-service's <c>GamificationSettings</c>, which is a single platform-global row with
/// no <c>OrganizationId</c> at all — so this event is platform configuration, not tenant data, and
/// the consumer touches no database. It nonetheless inherited <c>RequiresOrganization = true</c>
/// until the 40.14 background-job audit, which made it wrong in both directions: an update saved by
/// Sellevate staff (no <c>org_id</c> claim, hence no organization on the envelope) would be
/// rejected, retried and dead-lettered, so the weights would silently never propagate; while an
/// update saved by one customer's administrator would be accepted and applied to every customer's
/// scoring anyway, because the destination is a process-wide singleton. Declaring the platform
/// scope explicitly at least makes the second behaviour honest.
/// </para>
///
/// <para>
/// That the weights are platform-global in the first place is a product gap, not a tenancy one —
/// per-organization scoring settings are recorded for the owner in docs/DONT_FORGET.md rather than
/// invented here.
/// </para>
/// </summary>
internal sealed class GamificationDialogWeightsConsumer : KafkaConsumerBackgroundService
{
    private readonly IDialogScoringWeightsProvider _weightsProvider;

    public GamificationDialogWeightsConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        IDialogScoringWeightsProvider weightsProvider,
        ILogger<GamificationDialogWeightsConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
        _weightsProvider = weightsProvider;
    }

    protected override IReadOnlyCollection<string> Topics => [BuildingBlocks.Eventing.Topics.GamificationDialogWeightsUpdated];

    /// <summary>
    /// The explicit, auditable opt-out the base class asks for: platform-wide configuration with
    /// no tenant to attribute it to, and no tenant-scoped read or write in the handler.
    /// </summary>
    protected override bool RequiresOrganization => false;

    protected override Task HandleAsync(EventEnvelope envelope, IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var payload = envelope.DataAs<GamificationDialogWeightsUpdatedEvent>();
        if (payload is null)
        {
            return Task.CompletedTask;
        }

        var multiplier = payload.Multiplier <= 0 ? 1.0 : payload.Multiplier;
        _weightsProvider.Update(new DialogScoringWeights(
            payload.Confidence,
            payload.Structure,
            payload.Objection,
            payload.Goal,
            multiplier));

        Logger.LogInformation(
            "Updated dialog scoring weights from Gamification: {Confidence}/{Structure}/{Objection}/{Goal} x{Multiplier}",
            payload.Confidence, payload.Structure, payload.Objection, payload.Goal, multiplier);

        return Task.CompletedTask;
    }
}
