using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.22. The <c>completion_rule</c> vocabulary, read strictly on the way in and tolerantly on
/// the way out — the same asymmetry <see cref="AssignmentDocumentSerializer"/> states for the other
/// three jsonb columns, and for the same reason.
///
/// <para>
/// <b>Strict on write.</b> An unknown kind, a missing number or a bar of zero is refused with a
/// message naming what is wrong, at the moment an administrator is looking at the screen. The
/// alternative — storing it and discovering at evaluation time that nothing can be measured — is
/// indistinguishable from having no threshold, which is exactly the failure this whole block exists
/// to prevent (docs/TENANCY/ASSIGNMENTS.md §1.1).
/// </para>
///
/// <para>
/// <b>Tolerant on read.</b> <see cref="TryRead"/> returns <see langword="null"/> rather than
/// throwing, because its callers are a Kafka consumer and a list endpoint. A rule written by a
/// future version of this service, or by a human with psql, must not take a message onto the
/// dead-letter topic or a screen down; it must leave that one assignment unevaluated and loudly
/// logged. Note that "unevaluated" fails closed: the person stays short of the threshold rather than
/// being handed a completion nobody measured.
/// </para>
/// </summary>
internal static class AssignmentCompletionRuleReader
{
    public const int MaximumRequiredCount = 20;

    private const string KindProperty = "kind";
    private const string MinimumScoreProperty = "minimumScore";
    private const string RequiredCountProperty = "requiredCount";
    private const string MinimumAccuracyPercentProperty = "minimumAccuracyPercent";

    /// <summary>
    /// Parses a rule an administrator just supplied, throwing
    /// <see cref="AssignmentValidationException"/> on anything the vocabulary does not cover.
    /// </summary>
    public static AssignmentCompletionRule Require(JsonElement rule)
    {
        if (rule.ValueKind != JsonValueKind.Object)
        {
            throw new AssignmentValidationException("completionRule must be a JSON object.");
        }

        if (!rule.TryGetProperty(KindProperty, out var kindElement)
            || kindElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(kindElement.GetString()))
        {
            throw new AssignmentValidationException(
                "completionRule must name its kind, for example "
                + "{\"kind\": \"dialog_score\", \"minimumScore\": 70, \"requiredCount\": 3}.");
        }

        var kind = kindElement.GetString()!.Trim();

        return kind switch
        {
            AssignmentCompletionRuleKinds.DialogScore => new AssignmentCompletionRule(
                kind,
                RequireBar(rule, MinimumScoreProperty),
                RequireCount(rule, RequiredCountProperty)),

            AssignmentCompletionRuleKinds.ExerciseAccuracy => new AssignmentCompletionRule(
                kind,
                RequireBar(rule, MinimumAccuracyPercentProperty),
                1),

            // Refused rather than stored. A rule nothing can evaluate completes nobody, and an
            // assignment nobody can complete looks identical on the dashboard to one nobody tried.
            _ => throw new AssignmentValidationException(
                $"'{kind}' is not a known completion rule kind. Known kinds: "
                + $"{AssignmentCompletionRuleKinds.DialogScore}, "
                + $"{AssignmentCompletionRuleKinds.ExerciseAccuracy}."),
        };
    }

    /// <summary>
    /// Parses a rule already stored in the database. Returns <see langword="null"/> when it cannot be
    /// read, which the caller must treat as "this assignment cannot be judged right now" — never as
    /// "this assignment has no threshold".
    /// </summary>
    public static AssignmentCompletionRule? TryRead(string? storedRule)
    {
        if (string.IsNullOrWhiteSpace(storedRule))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(storedRule);

            return Require(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (AssignmentValidationException)
        {
            return null;
        }
    }

    private static int RequireBar(JsonElement rule, string propertyName)
    {
        var value = ReadInteger(rule, propertyName);

        // Zero is refused explicitly, and it is the only refusal here that is about product rather
        // than about types: "score at least 0" is a threshold that every click clears.
        if (value is null or < 1 or > 100)
        {
            throw new AssignmentValidationException(
                $"completionRule.{propertyName} must be a whole number from 1 to 100. "
                + "A bar of zero would mean the assignment completes on a click.");
        }

        return value.Value;
    }

    private static int RequireCount(JsonElement rule, string propertyName)
    {
        var value = ReadInteger(rule, propertyName);

        if (value is null or < 1 or > MaximumRequiredCount)
        {
            throw new AssignmentValidationException(
                $"completionRule.{propertyName} must be a whole number from 1 to {MaximumRequiredCount}.");
        }

        return value.Value;
    }

    private static int? ReadInteger(JsonElement rule, string propertyName)
        => rule.TryGetProperty(propertyName, out var element)
           && element.ValueKind == JsonValueKind.Number
           && element.TryGetInt32(out var value)
            ? value
            : null;
}
