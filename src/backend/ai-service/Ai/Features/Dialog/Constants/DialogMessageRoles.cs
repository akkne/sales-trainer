namespace Sellevate.Ai.Features.Dialog.Constants;

/// <summary>
/// The role vocabulary of a chat completion, shared by the provider wire format and by the
/// <c>role</c> field persisted on every Mongo dialog message.
///
/// <para>
/// Persisted and compared, so these values must never change: a stored conversation whose messages
/// say <c>"user"</c> would stop being replayable, and the feedback prompt decides who said what by
/// testing for <see cref="Assistant"/>. They are also the exact tokens the provider accepts —
/// anything else is rejected as a malformed request.
/// </para>
/// </summary>
public static class DialogMessageRoles
{
    /// <summary>The learner being trained.</summary>
    public const string User = "user";

    /// <summary>The roleplay character, and the side the grader reads as the client.</summary>
    public const string Assistant = "assistant";

    /// <summary>Instruction channel. Never persisted on a session — it is rebuilt per request.</summary>
    public const string System = "system";
}
