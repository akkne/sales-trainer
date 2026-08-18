namespace Sellevate.Identity.Features.Invites.Services.Abstract;

/// <summary>The raw token (shown once) paired with the hash that is the only thing persisted.</summary>
public sealed record IssuedInviteToken(string RawToken, string TokenHash);
