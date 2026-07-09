namespace Sellevate.Company.Common.Constants;

public static class CompanyRouteConstants
{
    public const string Companies = "companies";
    public const string CompanyById = "companies/{companyId:guid}";
    public const string Logs = "companies/{companyId:guid}/logs";
    public const string LogById = "companies/{companyId:guid}/logs/{logId:guid}";
    public const string PracticeCalls = "companies/{companyId:guid}/practice-calls";
    public const string RecentGoals = "companies/{companyId:guid}/recent-goals";
}
