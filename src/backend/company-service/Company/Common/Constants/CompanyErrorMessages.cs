namespace Sellevate.Company.Common.Constants;

/// <summary>
/// The messages company-service puts in a response body or an exception that a caller reads.
/// Collected here because several of them are produced from more than one place and must stay
/// word-for-word identical: the frontend and the tests match on the text, so a paraphrase in one
/// branch is a silent contract change.
/// </summary>
public static class CompanyErrorMessages
{
    /// <summary>
    /// Body of the 503 every AI-backed endpoint returns when ai-service is unreachable or refuses
    /// the call. Deliberately generic: the caller can retry, and the underlying transport detail is
    /// logged rather than shown.
    /// </summary>
    public const string AiServiceUnavailable = "AI service unavailable. Please try again later.";

    /// <summary>
    /// Rejection for a status update whose body omitted the status. The DTO marks it required, so
    /// this is the defence for a caller that bypassed model validation.
    /// </summary>
    public const string CompanyStatusRequired = "Status is required.";

    /// <summary>
    /// Rejection for a call-log entry naming a contact that does not belong to the target company.
    /// User-facing, hence Russian — the product's language.
    /// </summary>
    public const string ContactNotFoundInCompany = "Указанный контакт не найден в этой компании.";

    /// <summary>Startup failure when the JWT signing key is absent or too short for HMAC-SHA256.</summary>
    public const string JwtSigningKeyMisconfigured =
        "Jwt:Key must be configured and at least 32 bytes (256 bits) long for HMAC-SHA256.";

    /// <summary>Startup failure when the Postgres connection string names no database to create.</summary>
    public const string PostgresDatabaseNotSpecified = "ConnectionStrings:Postgres must specify a Database.";
}
