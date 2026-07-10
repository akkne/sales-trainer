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

        var prompt = basePrompt + BuildCompanyContextBlock(companyCallContext);

        if (HasPersona(companyCallContext))
        {
            prompt += BuildChatPersonaBlock(companyCallContext);
        }

        return prompt;
    }

    public static string BuildFeedbackSystemPrompt(string basePrompt, CompanyCallContext? companyCallContext)
    {
        if (companyCallContext == null)
        {
            return basePrompt;
        }

        var prompt = basePrompt + BuildCompanyContextBlock(companyCallContext);

        if (HasPersona(companyCallContext))
        {
            prompt += BuildFeedbackPersonaBlock(companyCallContext);
        }

        return prompt;
    }

    private static bool HasPersona(CompanyCallContext companyCallContext) =>
        !string.IsNullOrWhiteSpace(companyCallContext.PersonaName);

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

    private static string BuildChatPersonaBlock(CompanyCallContext companyCallContext)
    {
        var lines = new StringBuilder();
        lines.AppendLine();
        lines.AppendLine("---");
        lines.AppendLine("ВОЙДИ В РОЛЬ следующего персонажа и общайся с пользователем от его лица на протяжении всего разговора:");
        lines.AppendLine($"Имя: {companyCallContext.PersonaName}");
        lines.AppendLine($"Должность: {companyCallContext.PersonaPosition}");
        lines.AppendLine($"Характер: {companyCallContext.PersonaPersonality}");

        if (!string.IsNullOrWhiteSpace(companyCallContext.PersonaDifficulty))
        {
            lines.AppendLine($"Уровень сложности собеседника: {DescribeDifficultyToughness(companyCallContext.PersonaDifficulty)}");
        }

        return lines.ToString();
    }

    private static string BuildFeedbackPersonaBlock(CompanyCallContext companyCallContext)
    {
        var lines = new StringBuilder();
        lines.AppendLine();
        lines.AppendLine("---");
        lines.AppendLine("В этом звонке ИИ играл роль персонажа со следующими характеристиками — учти это при оценке звонка:");
        lines.AppendLine($"Имя: {companyCallContext.PersonaName}");
        lines.AppendLine($"Должность: {companyCallContext.PersonaPosition}");
        lines.AppendLine($"Характер: {companyCallContext.PersonaPersonality}");

        if (!string.IsNullOrWhiteSpace(companyCallContext.PersonaDifficulty))
        {
            lines.AppendLine($"Уровень сложности собеседника: {DescribeDifficultyToughness(companyCallContext.PersonaDifficulty)}");
        }

        return lines.ToString();
    }

    private static string DescribeDifficultyToughness(string difficulty) => difficulty switch
    {
        "Easy" => "лёгкий — персонаж дружелюбен и легко идёт на контакт",
        "Hard" => "сложный — персонаж скептичен, придирчив и активно возражает",
        _ => "средний — персонаж вежлив, но осторожен",
    };
}
