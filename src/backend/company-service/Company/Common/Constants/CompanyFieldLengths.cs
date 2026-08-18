namespace Sellevate.Company.Common.Constants;

/// <summary>
/// The maximum stored length of every bounded text column in company-db, shared by the EF entity
/// configurations that create the columns and by the request DTOs whose <c>[MaxLength]</c>
/// attributes reject over-long input before it reaches them.
///
/// <para>
/// Both readers are mandatory and must agree. The DTO attribute is what turns an over-long field
/// into a 400 naming the field; the column length is what the database enforces. While the two
/// numbers were written out twice, drift between them was invisible until a user hit it — and the
/// user's half of that bug is a 500 from a truncation error instead of a validation message.
/// </para>
///
/// <para>
/// These are compile-time constants and not configuration on purpose: changing one changes the
/// schema, so it needs an EF migration rather than a restart.
/// </para>
/// </summary>
public static class CompanyFieldLengths
{
    /// <summary>Company, contact and persona names, and a call-log entry's contact name.</summary>
    public const int Name = 200;

    /// <summary>Job title on a contact or a persona.</summary>
    public const int Position = 200;

    /// <summary>Free-text company description; also the briefing prompt's main input.</summary>
    public const int CompanyDescription = 8000;

    /// <summary>Free-text notes on a contact.</summary>
    public const int ContactNotes = 2000;

    /// <summary>Free-text note attached to a scheduled follow-up.</summary>
    public const int NextActionNote = 2000;

    /// <summary>What a logged call was about.</summary>
    public const int CallLogSubject = 4000;

    /// <summary>What a logged call resulted in.</summary>
    public const int CallLogOutcome = 4000;

    /// <summary>Generated or hand-written persona behaviour description.</summary>
    public const int PersonaPersonality = 4000;

    /// <summary>Identifier of the ai-service dialog session a practice call refers to.</summary>
    public const int DialogSessionId = 100;

    /// <summary>The goal a practice call was run against.</summary>
    public const int PracticeCallGoal = 1000;

    /// <summary>
    /// Width of the <c>Status</c> column. The enum is stored as its name, so this bounds the
    /// longest <c>CompanyStatus</c> member rather than any user input.
    /// </summary>
    public const int CompanyStatusColumn = 32;

    /// <summary>
    /// Width of the persona <c>Difficulty</c> column. As with the status column, this bounds the
    /// longest <c>PersonaDifficulty</c> member name, not user input.
    /// </summary>
    public const int PersonaDifficultyColumn = 16;
}
