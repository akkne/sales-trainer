namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class StartSessionRequestDto
{
    public Guid BundleId { get; set; }
    public Guid ModeId { get; set; }
    public CompanyCallContextDto? CompanyContext { get; set; }

    /// <summary>User-authored role-play brief. Only valid with the custom-scenario mode, and
    /// re-checked for sales relevance server-side before the session is created.</summary>
    public string? CustomScenario { get; set; }
}
