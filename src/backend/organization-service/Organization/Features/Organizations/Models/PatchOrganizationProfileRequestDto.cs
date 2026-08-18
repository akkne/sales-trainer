namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. One answer to one interview question.
///
/// <para>
/// <b>An omitted field is left alone; it is not cleared.</b> That is the difference between this and
/// <see cref="UpdateOrganizationProfileRequestDto"/>, and it is what makes an interview possible at
/// all. With only the whole-row <c>PUT</c>, answering «какие возражения вы слышите» means the client
/// reading the profile, splicing one field in and writing all seven back — a read-modify-write that
/// loses whatever a colleague saved in the meantime, and does it most often in exactly the
/// multi-person moment the block is designed for.
/// </para>
///
/// <para>
/// <b>Emptying a field is not something this route does</b>, because <see langword="null"/> already
/// means «не отвечал» and one JSON value cannot mean two things. Clearing a field is a deliberate act
/// on a form the person is looking at whole, which is what <c>PUT /organizations/profile</c> is. The
/// alternative — a <c>JsonElement</c>-based patch that can tell an absent key from an explicit null —
/// is real machinery for the rarest operation in the feature.
/// </para>
/// </summary>
public sealed record PatchOrganizationProfileRequestDto(
    string? Product,
    string? Icp,
    IReadOnlyList<OrganizationObjectionDto>? Objections,
    IReadOnlyList<string>? ScriptStages,
    string? Tone,
    IReadOnlyDictionary<string, string>? Glossary,
    IReadOnlyList<string>? BannedClaims);
