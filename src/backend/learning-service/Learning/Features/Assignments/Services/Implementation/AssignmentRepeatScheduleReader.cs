using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.24. The <c>repeat_schedule</c> vocabulary, read strictly on the way in and tolerantly on
/// the way out — the same asymmetry <see cref="AssignmentCompletionRuleReader"/> states for the
/// completion rule, and for the same reason.
///
/// <para>
/// <b>Strict on write.</b> 40.21 accepted any object carrying a <c>kind</c>, which was the most it
/// could assert before the vocabulary existed. It exists now, so an unknown kind, a non-ascending
/// list or an offset of zero is refused at create/update time, where an administrator is present to
/// read the message. The alternative — storing it and finding out weeks later that no wave ever
/// fired — is the exact failure this block is written to remove: an assignment that silently never
/// repeats looks, on every screen, like an assignment nobody configured repeats for.
/// </para>
///
/// <para>
/// <b>Tolerant on read.</b> <see cref="TryRead"/> returns <see langword="null"/> rather than
/// throwing, because its callers are a background sweep and a list endpoint. A schedule written by a
/// future version of this service, or by a human with psql, must leave that one assignment
/// un-repeated and loudly logged rather than take down the tick for every other organization. That
/// failure direction is the safe one: nothing is issued to anybody, and nothing is issued twice.
/// </para>
/// </summary>
internal static class AssignmentRepeatScheduleReader
{
    private const string KindProperty = "kind";
    private const string OffsetDaysProperty = "offsetDays";

    /// <summary>
    /// Parses a schedule an administrator just supplied, throwing
    /// <see cref="AssignmentValidationException"/> on anything the vocabulary does not cover.
    /// </summary>
    public static AssignmentRepeatSchedule Require(JsonElement schedule)
    {
        if (schedule.ValueKind != JsonValueKind.Object)
        {
            throw new AssignmentValidationException("repeatSchedule must be a JSON object.");
        }

        if (!schedule.TryGetProperty(KindProperty, out var kindElement)
            || kindElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(kindElement.GetString()))
        {
            throw new AssignmentValidationException(
                "repeatSchedule must name its kind, for example "
                + "{\"kind\": \"fixed_offsets\", \"offsetDays\": [7, 21]}.");
        }

        var kind = kindElement.GetString()!.Trim();
        if (!AssignmentRepeatScheduleKinds.IsKnown(kind))
        {
            throw new AssignmentValidationException(
                $"'{kind}' is not a known repeat schedule kind. Known kinds: "
                + $"{AssignmentRepeatScheduleKinds.FixedOffsets}.");
        }

        return new AssignmentRepeatSchedule(ReadOffsetDays(schedule));
    }

    /// <summary>
    /// Parses a schedule already stored in the database. Returns <see langword="null"/> when there is
    /// none or it cannot be read, which the caller must treat as "this assignment does not repeat
    /// right now" — never as "this assignment repeats on the default schedule".
    /// </summary>
    public static AssignmentRepeatSchedule? TryRead(string? storedSchedule)
    {
        if (string.IsNullOrWhiteSpace(storedSchedule))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(storedSchedule);

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

    /// <summary>
    /// <c>offsetDays</c> is optional and omitting it means the roadmap's <c>[7, 21]</c>. The default
    /// lives here rather than in the schema so that a stored schedule always means today what it
    /// meant the day it was written — a database default would silently re-point every existing row
    /// at a changed constant.
    /// </summary>
    private static IReadOnlyList<int> ReadOffsetDays(JsonElement schedule)
    {
        if (!schedule.TryGetProperty(OffsetDaysProperty, out var offsetsElement)
            || offsetsElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return AssignmentRepeatScheduleLimits.DefaultOffsetDays;
        }

        if (offsetsElement.ValueKind != JsonValueKind.Array)
        {
            throw new AssignmentValidationException(
                "repeatSchedule.offsetDays must be an array of whole numbers of days, for example [7, 21].");
        }

        var offsetDays = new List<int>();

        foreach (var offsetElement in offsetsElement.EnumerateArray())
        {
            if (offsetElement.ValueKind != JsonValueKind.Number
                || !offsetElement.TryGetInt32(out var offsetDay)
                || offsetDay < 1
                || offsetDay > AssignmentRepeatScheduleLimits.MaximumOffsetDays)
            {
                throw new AssignmentValidationException(
                    "Every repeatSchedule.offsetDays entry must be a whole number of days from 1 to "
                    + $"{AssignmentRepeatScheduleLimits.MaximumOffsetDays}. An offset of zero would re-issue "
                    + "the assignment the moment it was issued.");
            }

            // Ascending and distinct, checked as one thing. Two waves on the same day are two
            // fan-outs of the same shortened work to the same people on the same morning, and an
            // out-of-order list would make the wave ordinal — the thing that identifies a wave for
            // the rest of its life — depend on how the administrator happened to type it.
            if (offsetDays.Count > 0 && offsetDay <= offsetDays[^1])
            {
                throw new AssignmentValidationException(
                    "repeatSchedule.offsetDays must be in ascending order with no repeats, for example [7, 21].");
            }

            offsetDays.Add(offsetDay);
        }

        if (offsetDays.Count == 0 || offsetDays.Count > AssignmentRepeatScheduleLimits.MaximumWaveCount)
        {
            throw new AssignmentValidationException(
                "repeatSchedule.offsetDays must name 1 to "
                + $"{AssignmentRepeatScheduleLimits.MaximumWaveCount} days. Omit it entirely for the default "
                + "[7, 21]; an empty list is a schedule that repeats nothing, which is what leaving "
                + "repeatSchedule out already means.");
        }

        return offsetDays;
    }
}
