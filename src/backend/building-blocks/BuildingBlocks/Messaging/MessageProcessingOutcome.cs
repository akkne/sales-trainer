namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// The result of processing a single consumed message, telling the consume loop whether the
/// offset may be committed (the message is fully accounted for) or must be left uncommitted
/// for redelivery.
/// </summary>
public enum MessageProcessingOutcome
{
    /// <summary>Handled successfully (or skipped as a duplicate / unparseable). Commit the offset.</summary>
    Commit,

    /// <summary>Exhausted retries and was forwarded to the dead-letter topic. Commit the offset.</summary>
    DeadLettered,

    /// <summary>Failed and must be redelivered (dead-lettering disabled or itself failed). Do not commit.</summary>
    Redeliver,
}
