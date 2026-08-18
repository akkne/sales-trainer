namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.27. The states of one run of the admin content pipeline; six since 40.28.
///
/// <para>
/// <b><see cref="Insufficient"/> is 40.28's refusal, and it is a state rather than an error</b>
/// because a refusal has to be arguable. «Материала мало, добавьте примеры возражений» is only
/// useful if the РОП can then add them and have the same run carry on — a 400 would make them start
/// over and would re-pay for structuring the deck they already uploaded.
/// </para>
///
/// <para>
/// <b><see cref="AwaitingReview"/> is the entire point of the block.</b> A pipeline that went from
/// material to fifteen exercises in one hop would make every mistake expensive: a wrong product name
/// or an objection the team never hears costs thirty seconds to fix here and a re-generation of the
/// whole lesson afterwards. The state exists so that the expensive half never starts on a structure
/// nobody looked at.
/// </para>
/// </summary>
public static class ContentGenerationJobStatuses
{
    /// <summary>Queued for, or in the middle of, the first LLM call. Nothing has been generated.</summary>
    public const string Structuring = "structuring";

    /// <summary>The checkpoint. A human is looking at the extracted structure and may edit it.</summary>
    public const string AwaitingReview = "awaiting_review";

    /// <summary>Approved. Queued for, or in the middle of, the second LLM call.</summary>
    public const string Generating = "generating";

    /// <summary>A lesson exists. Terminal.</summary>
    public const string Completed = "completed";

    /// <summary>
    /// Phase 40.28. The material is not enough to generate anything worth having, and the run says
    /// exactly what is missing (<c>Insufficiency</c>). Not a failure — nothing broke — and not
    /// terminal: adding material (<c>POST …/material</c>) sends the run back to structuring, and
    /// filling the gaps by hand (<c>PUT …/structure</c>) returns it to the checkpoint. No worker
    /// touches it, so a refused run costs nothing while it waits.
    /// </summary>
    public const string Insufficient = "insufficient";

    /// <summary>
    /// Out of attempts. Terminal until a person retries it, and a retry resumes the half that failed
    /// rather than starting over — a failed generation must not re-pay for structuring.
    /// </summary>
    public const string Failed = "failed";

    public static readonly string[] All =
    [
        Structuring,
        AwaitingReview,
        Generating,
        Completed,
        Failed,
        Insufficient
    ];

    /// <summary>The two states a background worker acts on. Every other state waits for a person.</summary>
    public static readonly string[] WorkerOwned =
    [
        Structuring,
        Generating
    ];

    public static bool IsKnown(string? status) => status is not null && All.Contains(status);
}
