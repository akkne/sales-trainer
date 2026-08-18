using FluentAssertions;
using NUnit.Framework;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Implementation;

namespace Sellevate.Organization.Tests.Unit;

/// <summary>
/// Phase 40.29 merge policy. The rule under test is "fill blanks, grow lists, never silently replace
/// a human's words", and the scenario it exists for is the one 40.27 named: a compliance officer types
/// <c>banned_claims</c> in March, a sales lead pastes a new product deck in June, and the model reads
/// the deck's marketing copy as the company's position. An overwrite there is not a lost edit — it is
/// a persona that starts voicing a promise a lawyer forbade, discovered by the customer.
///
/// <para>
/// <see cref="OrganizationProfileDraftMerger"/> is a pure function with no dependencies, so these are
/// plain unit tests with no harness.
/// </para>
/// </summary>
[TestFixture]
public sealed class OrganizationProfileDraftMergerTests
{
    private static ExtractedProfileDraftDto EmptyDraft() =>
        new(Product: null, Icp: null, Tone: null, Objections: null, ScriptStages: null,
            Glossary: null, BannedClaims: null);

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

    private static OrganizationProfileFieldProposalDto ProposalFor(
        OrganizationProfileMergePlan plan, string field) =>
        plan.Proposals.Single(proposal => proposal.Field == field);

    [Test]
    public void A_blank_field_is_filled_from_the_draft_without_needing_consent()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(),
            EmptyDraft() with { Product = "Складской учёт для розницы" },
            acceptedFields: null);

        plan.Merged.Product.Should().Be("Складской учёт для розницы");
        ProposalFor(plan, OrganizationProfileFields.Product).Decision
            .Should().Be(OrganizationProfileFieldProposalDto.DecisionFill);
    }

    [Test]
    public void A_missing_profile_is_treated_as_every_field_blank()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            profile: null,
            EmptyDraft() with { Product = "CRM", Icp = "SMB" },
            acceptedFields: null);

        plan.Merged.Product.Should().Be("CRM");
        plan.Merged.Icp.Should().Be("SMB");
    }

    [Test]
    public void A_populated_field_is_kept_and_the_conflict_is_reported_when_consent_is_absent()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(product: "Что написал человек"),
            EmptyDraft() with { Product = "Что предложила модель" },
            acceptedFields: null);

        plan.Merged.Product.Should().Be("Что написал человек");

        var proposal = ProposalFor(plan, OrganizationProfileFields.Product);
        proposal.Decision.Should().Be(OrganizationProfileFieldProposalDto.DecisionConflict);
        proposal.CurrentValue.Should().Be("Что написал человек");
        proposal.SuggestedValue.Should().Be("Что предложила модель");
    }

    /// <summary>
    /// The audit of this merge must not be able to lie. A proposal that reported itself as
    /// <c>unchanged</c> once it had been applied would be the one place it could, so an accepted
    /// conflict is still reported as a conflict — that is what lets the screen say afterwards that a
    /// human's value was replaced.
    /// </summary>
    [Test]
    public void An_accepted_conflict_is_applied_and_still_reported_as_a_conflict()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(product: "Что написал человек"),
            EmptyDraft() with { Product = "Что предложила модель" },
            acceptedFields: [OrganizationProfileFields.Product]);

        plan.Merged.Product.Should().Be("Что предложила модель");
        ProposalFor(plan, OrganizationProfileFields.Product).Decision
            .Should().Be(OrganizationProfileFieldProposalDto.DecisionConflict);
    }

    [Test]
    public void A_draft_that_says_nothing_about_a_field_changes_nothing()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(product: "Существующее значение"),
            EmptyDraft() with { Product = "   " },
            acceptedFields: [OrganizationProfileFields.Product]);

        plan.Merged.Product.Should().Be("Существующее значение");
        ProposalFor(plan, OrganizationProfileFields.Product).Decision
            .Should().Be(OrganizationProfileFieldProposalDto.DecisionUnchanged);
    }

    [Test]
    public void An_identical_suggestion_is_unchanged_rather_than_a_conflict()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(tone: "на равных"),
            EmptyDraft() with { Tone = "  на равных  " },
            acceptedFields: null);

        plan.Merged.Tone.Should().Be("на равных");
        ProposalFor(plan, OrganizationProfileFields.Tone).Decision
            .Should().Be(OrganizationProfileFieldProposalDto.DecisionUnchanged);
    }

    /// <summary>
    /// The closed-vocabulary rule: a field name the server does not recognise must not be able to
    /// decide what happens to a field, so it is dropped rather than honoured or rejected.
    /// </summary>
    [Test]
    public void A_field_name_outside_the_overwritable_set_grants_nothing()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(icp: "Наш сегмент"),
            EmptyDraft() with { Icp = "Сегмент из презентации" },
            acceptedFields: ["ICP", "icp_", "banned_claims", "not_a_field"]);

        plan.Merged.Icp.Should().Be("Наш сегмент");
    }

    /// <summary>
    /// The field the whole policy was designed around. <c>banned_claims</c> is absent from
    /// <see cref="OrganizationProfileFields.Overwritable"/> on purpose: there is no
    /// <c>acceptedFields</c> value that deletes a banned claim, so no client bug and no stale second
    /// tab can produce one. Removing an entry stays a decision with a person's name on it, made on the
    /// whole-profile form.
    /// </summary>
    [Test]
    public void Banned_claims_only_ever_grow_and_consent_cannot_remove_one()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(bannedClaims: ["гарантия дохода", "гарантия сроков"]),
            EmptyDraft() with { BannedClaims = ["гарантия результата"] },
            acceptedFields: [OrganizationProfileFields.BannedClaims]);

        plan.Merged.BannedClaims.Should().BeEquivalentTo(
            ["гарантия дохода", "гарантия сроков", "гарантия результата"],
            options => options.WithStrictOrdering());

        var proposal = ProposalFor(plan, OrganizationProfileFields.BannedClaims);
        proposal.Decision.Should().Be(OrganizationProfileFieldProposalDto.DecisionExtend);
        proposal.AddedItemCount.Should().Be(1);
    }

    /// <summary>
    /// Pins the second, weaker layer of the same protection. <c>MergeBannedClaims</c> never reads
    /// <c>acceptedFields</c> at all, which is what actually makes removal impossible; the absence of
    /// the three additive fields from <see cref="OrganizationProfileFields.Overwritable"/> is
    /// defence-in-depth on top of it.
    ///
    /// <para>
    /// This test exists because a mutation run proved the distinction matters: adding
    /// <c>banned_claims</c> to <c>Overwritable</c> broke no other test in this file, since the merge
    /// ignores consent for that field anyway. Without this assertion the documented reason — "there is
    /// no acceptedFields value that deletes a banned claim" — would be unguarded, and a later change
    /// that did start honouring consent there would find the door already open.
    /// </para>
    /// </summary>
    [Test]
    public void Only_the_four_single_valued_fields_may_be_named_in_accepted_fields()
    {
        OrganizationProfileFields.Overwritable.Should().BeEquivalentTo(
        [
            OrganizationProfileFields.Product,
            OrganizationProfileFields.Icp,
            OrganizationProfileFields.Tone,
            OrganizationProfileFields.ScriptStages,
        ]);

        OrganizationProfileFields.IsOverwritable(OrganizationProfileFields.BannedClaims).Should().BeFalse();
        OrganizationProfileFields.IsOverwritable(OrganizationProfileFields.Objections).Should().BeFalse();
        OrganizationProfileFields.IsOverwritable(OrganizationProfileFields.Glossary).Should().BeFalse();
    }

    [Test]
    public void A_banned_claim_the_draft_repeats_in_another_case_is_not_added_twice()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(bannedClaims: ["Гарантия дохода"]),
            EmptyDraft() with { BannedClaims = ["гарантия дохода", "  ", "гарантия сроков"] },
            acceptedFields: null);

        plan.Merged.BannedClaims.Should().BeEquivalentTo(["Гарантия дохода", "гарантия сроков"]);
        ProposalFor(plan, OrganizationProfileFields.BannedClaims).AddedItemCount.Should().Be(1);
    }

    /// <summary>
    /// An objection already on file wins, which is what preserves the <c>Frequency</c> the extraction
    /// cannot know and the answer a manager wrote from experience rather than from a deck.
    /// </summary>
    [Test]
    public void An_existing_objection_keeps_its_frequency_and_its_managers_answer()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(objections:
            [
                new OrganizationObjectionDto("Дорого", "часто", "Считаем экономию за год"),
            ]),
            EmptyDraft() with
            {
                Objections =
                [
                    new ExtractedProfileObjectionDto("дорого", "Ответ из презентации"),
                    new ExtractedProfileObjectionDto("Нет времени", "Пятнадцать минут"),
                ],
            },
            acceptedFields: null);

        var merged = plan.Merged.Objections!;
        merged.Should().HaveCount(2);

        var existing = merged.Single(objection => objection.Text == "Дорого");
        existing.Frequency.Should().Be("часто");
        existing.BestResponse.Should().Be("Считаем экономию за год");

        var added = merged.Single(objection => objection.Text == "Нет времени");
        added.Frequency.Should().BeNull();
        added.BestResponse.Should().Be("Пятнадцать минут");

        ProposalFor(plan, OrganizationProfileFields.Objections).AddedItemCount.Should().Be(1);
    }

    [Test]
    public void An_objection_with_no_text_is_dropped_rather_than_added_blank()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(),
            EmptyDraft() with
            {
                Objections =
                [
                    new ExtractedProfileObjectionDto("   ", "ответ"),
                    new ExtractedProfileObjectionDto("Дорого", null),
                ],
            },
            acceptedFields: null);

        plan.Merged.Objections!.Select(objection => objection.Text).Should().Equal("Дорого");
        ProposalFor(plan, OrganizationProfileFields.Objections).AddedItemCount.Should().Be(1);
    }

    /// <summary>
    /// The glossary is precisely the field where the customer's word beats the model's — that is what
    /// it is for.
    /// </summary>
    [Test]
    public void A_term_the_customer_has_defined_keeps_their_definition()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(glossary: new Dictionary<string, string> { ["сделка"] = "подписанный договор" }),
            EmptyDraft() with
            {
                Glossary = new Dictionary<string, string>
                {
                    ["Сделка"] = "любая возможность в воронке",
                    ["лид"] = "входящая заявка",
                },
            },
            acceptedFields: null);

        plan.Merged.Glossary!["сделка"].Should().Be("подписанный договор");
        plan.Merged.Glossary!["лид"].Should().Be("входящая заявка");
        ProposalFor(plan, OrganizationProfileFields.Glossary).AddedItemCount.Should().Be(1);
    }

    [Test]
    public void A_glossary_entry_missing_either_half_is_dropped()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(),
            EmptyDraft() with
            {
                Glossary = new Dictionary<string, string>
                {
                    ["термин без определения"] = "  ",
                    ["лид"] = "входящая заявка",
                },
            },
            acceptedFields: null);

        plan.Merged.Glossary!.Should().ContainSingle().Which.Key.Should().Be("лид");
    }

    /// <summary>
    /// <c>script_stages</c> sits with the single-valued fields although it is a list: it is an ordered
    /// sequence describing one conversation, not a set. Unioning a five-stage script with a seven-stage
    /// one produces twelve stages in an order that describes no call anybody makes.
    /// </summary>
    [Test]
    public void The_script_is_replaced_whole_or_kept_whole_and_never_unioned()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(scriptStages: ["Приветствие", "Выявление", "Договорённость"]),
            EmptyDraft() with { ScriptStages = ["Открытие", "Квалификация", "Демо", "Закрытие"] },
            acceptedFields: null);

        plan.Merged.ScriptStages.Should().Equal("Приветствие", "Выявление", "Договорённость");
        ProposalFor(plan, OrganizationProfileFields.ScriptStages).Decision
            .Should().Be(OrganizationProfileFieldProposalDto.DecisionConflict);
    }

    [Test]
    public void An_accepted_script_replaces_the_whole_sequence()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(scriptStages: ["Приветствие", "Договорённость"]),
            EmptyDraft() with { ScriptStages = ["Открытие", "Квалификация", "Закрытие"] },
            acceptedFields: [OrganizationProfileFields.ScriptStages]);

        plan.Merged.ScriptStages.Should().Equal("Открытие", "Квалификация", "Закрытие");
    }

    [Test]
    public void A_script_that_matches_stage_for_stage_is_unchanged()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(scriptStages: ["Открытие", "Закрытие"]),
            EmptyDraft() with { ScriptStages = ["  Открытие  ", "Закрытие"] },
            acceptedFields: null);

        ProposalFor(plan, OrganizationProfileFields.ScriptStages).Decision
            .Should().Be(OrganizationProfileFieldProposalDto.DecisionUnchanged);
    }

    [Test]
    public void An_empty_script_is_filled_and_reports_how_many_stages_arrived()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(),
            EmptyDraft() with { ScriptStages = ["Открытие", "Квалификация", "Закрытие"] },
            acceptedFields: null);

        plan.Merged.ScriptStages.Should().HaveCount(3);

        var proposal = ProposalFor(plan, OrganizationProfileFields.ScriptStages);
        proposal.Decision.Should().Be(OrganizationProfileFieldProposalDto.DecisionFill);
        proposal.AddedItemCount.Should().Be(3);
        proposal.SuggestedValue.Should().Be("Открытие → Квалификация → Закрытие");
    }

    /// <summary>
    /// A screen rendering the proposals top to bottom should read product-first however the per-field
    /// merges are later reordered, so the plan is ordered by the interview's own ordering rather than
    /// by the order the merges happen to run in.
    /// </summary>
    [Test]
    public void Proposals_carry_one_entry_per_field_in_the_interviews_order()
    {
        var plan = OrganizationProfileDraftMerger.Plan(Profile(), EmptyDraft(), acceptedFields: null);

        plan.Proposals.Select(proposal => proposal.Field)
            .Should().Equal(OrganizationProfileGapCodes.All);
    }

    [Test]
    public void Values_are_trimmed_on_the_way_in()
    {
        var plan = OrganizationProfileDraftMerger.Plan(
            Profile(),
            EmptyDraft() with
            {
                Product = "  CRM  ",
                BannedClaims = ["  гарантия дохода  "],
            },
            acceptedFields: null);

        plan.Merged.Product.Should().Be("CRM");
        plan.Merged.BannedClaims.Should().Equal("гарантия дохода");
    }

    [Test]
    public void A_null_accepted_list_and_an_empty_one_behave_the_same()
    {
        var withNull = OrganizationProfileDraftMerger.Plan(
            Profile(product: "наше"), EmptyDraft() with { Product = "их" }, acceptedFields: null);
        var withEmpty = OrganizationProfileDraftMerger.Plan(
            Profile(product: "наше"), EmptyDraft() with { Product = "их" }, acceptedFields: []);

        withNull.Merged.Should().BeEquivalentTo(withEmpty.Merged);
        withNull.Proposals.Should().BeEquivalentTo(withEmpty.Proposals);
    }

    [Test]
    public void A_null_draft_is_refused_rather_than_treated_as_an_empty_one()
    {
        var act = () => OrganizationProfileDraftMerger.Plan(Profile(), draft: null!, acceptedFields: null);

        act.Should().Throw<ArgumentNullException>();
    }
}
