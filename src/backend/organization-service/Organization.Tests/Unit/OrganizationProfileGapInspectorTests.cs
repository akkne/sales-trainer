using FluentAssertions;
using NUnit.Framework;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Implementation;

namespace Sellevate.Organization.Tests.Unit;

/// <summary>
/// Phase 40.29 interview. The block's measure of success is "five minutes instead of an hour" and the
/// failure mode it names is "nobody fills in thirty empty fields", so the cap on how many questions a
/// person is shown at once is the feature, not a detail — and the ordering is the rest of it.
///
/// <para>
/// <see cref="OrganizationProfileGapInspector"/> is a pure function that calls no model: which fields
/// are empty is arithmetic and the questions are fixed sentences. These are therefore plain unit tests
/// with no harness.
/// </para>
/// </summary>
[TestFixture]
public sealed class OrganizationProfileGapInspectorTests
{
    private static OrganizationProfileDto Profile(
        string? product = null,
        string? icp = null,
        string? tone = null,
        IReadOnlyList<OrganizationObjectionDto>? objections = null,
        IReadOnlyList<string>? scriptStages = null,
        IReadOnlyDictionary<string, string>? glossary = null,
        IReadOnlyList<string>? bannedClaims = null) =>
        new(product, icp, objections ?? [], scriptStages ?? [], tone,
            glossary ?? new Dictionary<string, string>(), bannedClaims ?? [],
            CreatedAt: DateTime.UnixEpoch, UpdatedAt: DateTime.UnixEpoch);

    private static OrganizationProfileDto FullProfile() =>
        Profile(
            product: "Складской учёт",
            icp: "Розничные сети",
            tone: "на равных",
            objections:
            [
                new OrganizationObjectionDto("Дорого", null, null),
                new OrganizationObjectionDto("Нет времени", null, null),
                new OrganizationObjectionDto("Уже есть решение", null, null),
            ],
            scriptStages: ["Открытие", "Квалификация", "Закрытие"],
            glossary: new Dictionary<string, string> { ["лид"] = "входящая заявка" },
            bannedClaims: ["гарантия дохода"]);

    /// <summary>
    /// The one case that must not throw: the roadmap's second bullet is an organization that has never
    /// saved a profile at all. A missing row and a row of empty strings must ask the same questions.
    /// </summary>
    [Test]
    public void A_missing_profile_asks_the_same_questions_as_an_empty_one()
    {
        var fromNull = OrganizationProfileGapInspector.Inspect(profile: null);
        var fromEmpty = OrganizationProfileGapInspector.Inspect(Profile());

        fromNull.Should().BeEquivalentTo(fromEmpty);
        fromNull.TotalGapCount.Should().Be(OrganizationProfileGapCodes.All.Length);
    }

    [Test]
    public void A_complete_profile_asks_nothing_and_is_ready_for_parameterization()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(FullProfile());

        gaps.Questions.Should().BeEmpty();
        gaps.TotalGapCount.Should().Be(0);
        gaps.BlockingGapCount.Should().Be(0);
        gaps.IsReadyForParameterization.Should().BeTrue();
    }

    /// <summary>
    /// The cap is the feature. Three is what a sales lead answers in the tab they are already in;
    /// seven is a form, and a form is what this block exists to replace.
    /// </summary>
    [Test]
    public void An_empty_profile_asks_three_questions_by_default_while_counting_all_seven()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(profile: null);

        gaps.Questions.Should().HaveCount(OrganizationProfileGapInspector.DefaultQuestionLimit);
        gaps.TotalGapCount.Should().Be(7);
    }

    /// <summary>
    /// The order lives in <c>OrganizationProfileGapCodes.All</c> and nowhere else, so that reordering a
    /// check inside the inspector can never quietly reorder the interview.
    ///
    /// <para>
    /// <b>Known limit of this assertion, established by a mutation run.</b> Deleting the
    /// <c>All.Where(missing.Contains)</c> line in <c>FindMissingCodes</c> — the line that enforces the
    /// guarantee — does <i>not</i> fail this test, because the checks inside that method currently run
    /// in the same order as <c>All</c>, so no input can distinguish the two. The assertion is therefore
    /// a tripwire rather than a proof: it starts biting the moment either order changes without the
    /// other, which is exactly the day it is needed. Anyone reordering the checks should not read a
    /// green suite as evidence that the re-sorting line is redundant.
    /// </para>
    /// </summary>
    [Test]
    public void Questions_follow_the_codes_own_order_not_the_order_the_checks_run_in()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(profile: null, questionLimit: 7);

        gaps.Questions.Select(question => question.Code).Should().Equal(OrganizationProfileGapCodes.All);
    }

    [Test]
    public void The_three_blocking_gaps_are_product_icp_and_objections()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(profile: null);

        gaps.BlockingGapCount.Should().Be(3);
        gaps.IsReadyForParameterization.Should().BeFalse();

        OrganizationProfileGapCodes.IsBlocking(OrganizationProfileGapCodes.Product).Should().BeTrue();
        OrganizationProfileGapCodes.IsBlocking(OrganizationProfileGapCodes.Icp).Should().BeTrue();
        OrganizationProfileGapCodes.IsBlocking(OrganizationProfileGapCodes.Objections).Should().BeTrue();
    }

    /// <summary>
    /// Readiness is decided by the blocking gaps alone, so a profile missing only optional and
    /// important fields is still usable for parameterization while still having questions to ask.
    /// </summary>
    [Test]
    public void A_profile_missing_only_non_blocking_fields_is_ready_but_still_has_questions()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(Profile(
            product: "Складской учёт",
            icp: "Розничные сети",
            objections:
            [
                new OrganizationObjectionDto("Дорого", null, null),
                new OrganizationObjectionDto("Нет времени", null, null),
                new OrganizationObjectionDto("Уже есть решение", null, null),
            ]));

        gaps.BlockingGapCount.Should().Be(0);
        gaps.IsReadyForParameterization.Should().BeTrue();
        gaps.TotalGapCount.Should().Be(4);
        gaps.Questions.Select(question => question.Code)
            .Should().Equal(OrganizationProfileGapCodes.ScriptStages, OrganizationProfileGapCodes.Tone,
                OrganizationProfileGapCodes.BannedClaims);
    }

    /// <summary>
    /// Objections and script stages are counted against a threshold rather than tested for "any". One
    /// objection in the profile is what a persona then raises every single session, and a persona with
    /// one objection is recognisable as a script.
    /// </summary>
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    public void Objections_are_a_threshold_not_a_presence_check(int objectionCount, bool expectedToBeMissing)
    {
        var objections = Enumerable.Range(0, objectionCount)
            .Select(index => new OrganizationObjectionDto($"Возражение {index}", null, null))
            .ToList();

        var gaps = OrganizationProfileGapInspector.Inspect(Profile(objections: objections), questionLimit: 7);

        gaps.Questions.Any(question => question.Code == OrganizationProfileGapCodes.Objections)
            .Should().Be(expectedToBeMissing);
    }

    [TestCase(0, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    public void Script_stages_are_a_threshold_too(int stageCount, bool expectedToBeMissing)
    {
        var stages = Enumerable.Range(0, stageCount).Select(index => $"Этап {index}").ToList();

        var gaps = OrganizationProfileGapInspector.Inspect(Profile(scriptStages: stages), questionLimit: 7);

        gaps.Questions.Any(question => question.Code == OrganizationProfileGapCodes.ScriptStages)
            .Should().Be(expectedToBeMissing);
    }

    /// <summary>
    /// A list holding three empty strings satisfies a length check and answers nothing, and the
    /// profile's jsonb columns accept exactly that — so the threshold counts entries that are actually
    /// text.
    /// </summary>
    [Test]
    public void Blank_entries_do_not_count_towards_a_threshold()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(
            Profile(
                objections:
                [
                    new OrganizationObjectionDto("Дорого", null, null),
                    new OrganizationObjectionDto("   ", null, null),
                    new OrganizationObjectionDto("", null, null),
                ],
                scriptStages: ["Открытие", " ", "  "]),
            questionLimit: 7);

        gaps.Questions.Select(question => question.Code)
            .Should().Contain(OrganizationProfileGapCodes.Objections)
            .And.Contain(OrganizationProfileGapCodes.ScriptStages);
    }

    [Test]
    public void A_whitespace_only_single_value_field_counts_as_missing()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(
            Profile(product: "   ", icp: "\t", tone: "\n"), questionLimit: 7);

        gaps.Questions.Select(question => question.Code)
            .Should().Contain(OrganizationProfileGapCodes.Product)
            .And.Contain(OrganizationProfileGapCodes.Icp)
            .And.Contain(OrganizationProfileGapCodes.Tone);
    }

    /// <summary>
    /// An empty list is indistinguishable from "the profile is complete" on the screen, so a caller
    /// asking for zero or a negative number gets one question rather than none.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void A_limit_below_one_still_returns_one_question(int requestedLimit)
    {
        var gaps = OrganizationProfileGapInspector.Inspect(profile: null, requestedLimit);

        gaps.Questions.Should().HaveCount(OrganizationProfileGapInspector.MinimumQuestionLimit);
        gaps.TotalGapCount.Should().Be(7);
    }

    /// <summary>
    /// Seven is every question there is, so the ceiling is not a throttle — it stops a client asking
    /// for more questions than exist and then rendering an empty scroll.
    /// </summary>
    [TestCase(8)]
    [TestCase(100)]
    [TestCase(int.MaxValue)]
    public void A_limit_above_the_ceiling_returns_at_most_every_question_there_is(int requestedLimit)
    {
        var gaps = OrganizationProfileGapInspector.Inspect(profile: null, requestedLimit);

        gaps.Questions.Should().HaveCount(OrganizationProfileGapInspector.MaximumQuestionLimit);
        OrganizationProfileGapInspector.MaximumQuestionLimit
            .Should().Be(OrganizationProfileGapCodes.All.Length);
    }

    [Test]
    public void The_limit_never_changes_the_counts_only_how_many_are_asked()
    {
        var narrow = OrganizationProfileGapInspector.Inspect(profile: null, questionLimit: 1);
        var wide = OrganizationProfileGapInspector.Inspect(profile: null, questionLimit: 7);

        narrow.Questions.Should().HaveCount(1);
        wide.Questions.Should().HaveCount(7);
        narrow.TotalGapCount.Should().Be(wide.TotalGapCount);
        narrow.BlockingGapCount.Should().Be(wide.BlockingGapCount);
        narrow.IsReadyForParameterization.Should().Be(wide.IsReadyForParameterization);
    }

    /// <summary>
    /// Every question carries the fixed sentence and the priority for its code, so the screen never has
    /// to invent either.
    /// </summary>
    [Test]
    public void Every_question_carries_its_own_fixed_sentence_and_priority()
    {
        var gaps = OrganizationProfileGapInspector.Inspect(profile: null, questionLimit: 7);

        foreach (var question in gaps.Questions)
        {
            question.Question.Should().NotBeNullOrWhiteSpace();
            question.Question.Should().Be(OrganizationProfileGapCodes.QuestionFor(question.Code));
            question.Priority.Should().Be(OrganizationProfileGapCodes.PriorityFor(question.Code));
            question.Priority.Should().BeOneOf(
                OrganizationProfileGapCodes.BlockingPriority,
                OrganizationProfileGapCodes.ImportantPriority,
                OrganizationProfileGapCodes.OptionalPriority);
        }
    }

    /// <summary>
    /// The inspector reads only the seven fields it asks about. Timestamps carry no meaning for gaps,
    /// and a profile saved long ago is not more or less complete than one saved today.
    /// </summary>
    [Test]
    public void Timestamps_do_not_affect_which_gaps_are_reported()
    {
        var recent = FullProfile() with { UpdatedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        var ancient = FullProfile();

        OrganizationProfileGapInspector.Inspect(recent)
            .Should().BeEquivalentTo(OrganizationProfileGapInspector.Inspect(ancient));
    }
}
