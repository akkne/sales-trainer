using System.Text;
using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog.Helpers;

public static class CompanyContextPromptBuilder
{
    public static string BuildChatSystemPrompt(string basePrompt, CompanyCallContext? companyCallContext)
    {
        if (companyCallContext == null)
        {
            return basePrompt;
        }

        return basePrompt + BuildCompanyContextBlock(companyCallContext);
    }

    public static string BuildFeedbackSystemPrompt(string basePrompt, CompanyCallContext? companyCallContext)
    {
        if (companyCallContext == null)
        {
            return basePrompt;
        }

        return basePrompt + BuildCompanyContextBlock(companyCallContext);
    }

    private static string BuildCompanyContextBlock(CompanyCallContext companyCallContext)
    {
        var lines = new StringBuilder();
        lines.AppendLine();
        lines.AppendLine();
        lines.AppendLine("---");
        lines.AppendLine($"Компания: {companyCallContext.CompanyName}");
        lines.AppendLine($"Описание: {companyCallContext.CompanyDescription}");

        if (!string.IsNullOrWhiteSpace(companyCallContext.CallGoal))
        {
            lines.AppendLine($"Цель звонка пользователя: {companyCallContext.CallGoal}");
        }

        return lines.ToString();
    }
}
