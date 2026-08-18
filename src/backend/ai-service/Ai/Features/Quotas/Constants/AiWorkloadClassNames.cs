namespace Sellevate.Ai.Features.Quotas.Constants;

/// <summary>
/// The wire spelling of <c>AiWorkloadClass</c>, shared by the two places that read it: the
/// <c>X-Ai-Workload</c> header the meter parses, and the <c>?workload=</c> query the preflight route
/// accepts. Only the batch value is named — anything else, including absence, is interactive, which is
/// the class with the larger allowance, so a caller that forgets to declare gets the permissive answer
/// instead of silently stopping at the reserve.
/// </summary>
public static class AiWorkloadClassNames
{
    public const string Batch = "batch";
}
