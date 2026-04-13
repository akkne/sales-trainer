namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class StartSessionRequestDto
{
    public Guid BundleId { get; set; }
    public Guid ModeId { get; set; }
}
