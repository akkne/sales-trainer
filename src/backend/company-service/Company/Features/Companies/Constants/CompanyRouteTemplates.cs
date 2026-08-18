namespace Sellevate.Company.Features.Companies.Constants;

/// <summary>
/// Every route company-service serves, composed from the segments rather than written out. The
/// same path also has to be produced at runtime for the <c>Location</c> header of a 201, so each
/// sub-resource had its segment spelled twice — once in an attribute and once in an interpolated
/// string — with nothing to keep the two in step. Composing both from one segment constant makes a
/// renamed sub-resource a one-line change instead of a hunt.
///
/// <para>
/// The paths carry no prefix and no version because the gateway forwards <c>/companies/**</c>
/// through unchanged (<c>ReverseProxy:Routes:company-companies</c>). Adding a prefix here would
/// therefore break the proxy route, not sit behind it.
/// </para>
/// </summary>
public static class CompanyRouteTemplates
{
    private const string CallLogsSegment = "logs";
    private const string PracticeCallsSegment = "practice-calls";
    private const string ContactsSegment = "contacts";
    private const string PersonasSegment = "personas";

    /// <summary>Collection of the calling user's companies.</summary>
    public const string Companies = "companies";

    /// <summary>One company, addressed by its identifier.</summary>
    public const string CompanyById = $"{Companies}/{{companyId:guid}}";

    /// <summary>Pipeline status of one company.</summary>
    public const string CompanyStatus = $"{CompanyById}/status";

    /// <summary>Scheduled follow-up of one company.</summary>
    public const string CompanyFollowUp = $"{CompanyById}/follow-up";

    /// <summary>Cached pre-call briefing of one company.</summary>
    public const string CompanyBriefing = $"{CompanyById}/briefing";

    /// <summary>Cached readiness score of one company.</summary>
    public const string CompanyReadiness = $"{CompanyById}/readiness";

    /// <summary>Call-log timeline of one company.</summary>
    public const string CompanyCallLogs = $"{CompanyById}/{CallLogsSegment}";

    /// <summary>One call-log entry.</summary>
    public const string CompanyCallLogById = $"{CompanyCallLogs}/{{logId:guid}}";

    /// <summary>Extracts a draft call-log entry from pasted raw text. Persists nothing.</summary>
    public const string CompanyCallLogParse = $"{CompanyCallLogs}/parse";

    /// <summary>Practice calls recorded against one company.</summary>
    public const string CompanyPracticeCalls = $"{CompanyById}/{PracticeCallsSegment}";

    /// <summary>Distinct practice-call goals used most recently for one company.</summary>
    public const string CompanyRecentGoals = $"{CompanyById}/recent-goals";

    /// <summary>Contacts of one company.</summary>
    public const string CompanyContacts = $"{CompanyById}/{ContactsSegment}";

    /// <summary>One contact of one company.</summary>
    public const string CompanyContactById = $"{CompanyContacts}/{{contactId:guid}}";

    /// <summary>Saved buyer personas of one company.</summary>
    public const string CompanyPersonas = $"{CompanyById}/{PersonasSegment}";

    /// <summary>One saved persona of one company.</summary>
    public const string CompanyPersonaById = $"{CompanyPersonas}/{{personaId:guid}}";

    /// <summary>Drafts a persona via ai-service. Persists nothing.</summary>
    public const string CompanyPersonaGenerate = $"{CompanyPersonas}/generate";

    /// <summary><c>Location</c> of a newly created company.</summary>
    public static string CompanyLocation(Guid companyId)
        => $"/{Companies}/{companyId}";

    /// <summary><c>Location</c> of a newly created call-log entry.</summary>
    public static string CallLogLocation(Guid companyId, Guid logId)
        => $"/{Companies}/{companyId}/{CallLogsSegment}/{logId}";

    /// <summary><c>Location</c> of a newly created practice call.</summary>
    public static string PracticeCallLocation(Guid companyId, Guid practiceCallId)
        => $"/{Companies}/{companyId}/{PracticeCallsSegment}/{practiceCallId}";

    /// <summary><c>Location</c> of a newly created contact.</summary>
    public static string ContactLocation(Guid companyId, Guid contactId)
        => $"/{Companies}/{companyId}/{ContactsSegment}/{contactId}";

    /// <summary><c>Location</c> of a newly created persona.</summary>
    public static string PersonaLocation(Guid companyId, Guid personaId)
        => $"/{Companies}/{companyId}/{PersonasSegment}/{personaId}";
}
