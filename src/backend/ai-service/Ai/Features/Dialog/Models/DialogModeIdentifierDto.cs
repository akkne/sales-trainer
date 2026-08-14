namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>Bundle/mode id pair for a seeded hidden mode the client cannot discover by listing.</summary>
public sealed class DialogModeIdentifierDto
{
    public Guid BundleId { get; set; }

    public Guid ModeId { get; set; }
}
