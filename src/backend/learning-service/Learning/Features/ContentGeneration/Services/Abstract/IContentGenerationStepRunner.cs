namespace Sellevate.Learning.Features.ContentGeneration.Services.Abstract;

/// <summary>
/// Phase 40.27. Advances one organization's pipeline runs by one step each. Always called inside a
/// scope whose <c>TenantContext</c> already names that organization
/// (docs/TENANCY/BACKGROUND_JOBS.md §1, per-organization iteration).
/// </summary>
public interface IContentGenerationStepRunner
{
    /// <summary>Returns how many runs were advanced. Zero is the normal answer.</summary>
    Task<int> RunPendingAsync(CancellationToken cancellationToken = default);
}
