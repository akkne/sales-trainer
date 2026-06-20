namespace Sellevate.Social.Features.Discuss.Services;

internal sealed record ImageContentValidationResult(bool IsValid, string ContentType, string Extension)
{
    public static ImageContentValidationResult Invalid { get; } =
        new(IsValid: false, ContentType: string.Empty, Extension: string.Empty);
}
