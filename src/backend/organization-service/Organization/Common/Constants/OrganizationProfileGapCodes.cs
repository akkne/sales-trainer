namespace Sellevate.Organization.Common.Constants;

/// <summary>
/// Phase 40.29. The interview's vocabulary: what is still missing from the organization profile, in
/// what order to ask about it, and the question the РОП actually reads.
///
/// <para>
/// <b>Why this is not <c>ContentSufficiencyCodes</c>.</b> 40.28 already produces a machine-readable
/// list of gaps, and reusing it here was the tempting move. The two lists answer different questions
/// and disagree in both directions. «Хватит ли этого материала на четыре хороших упражнения» is not
/// «заполнен ли профиль компании»: <c>banned_claims</c> and the glossary block nothing in generation
/// and matter a great deal in the profile, while <c>too_short</c> and <c>off_topic</c> are facts about
/// an uploaded document and mean nothing about a profile row. A shared list would have to satisfy
/// both and would end up describing neither — and the sentences would have to be written for both
/// audiences at once, which is how «добавьте больше информации» gets written.
/// </para>
///
/// <para>
/// <b>A gap code and a profile field name are the same string on purpose.</b> Each code below is
/// defined as its <see cref="OrganizationProfileFields"/> counterpart rather than as a second literal,
/// because <c>OrganizationProfileDraftMerger.Plan</c> orders its per-field proposals by
/// <see cref="All"/> and looks each one up by name: two vocabularies that merely happened to agree
/// would turn a one-character typo into a runtime failure on the apply route instead of a compile
/// error here.
/// </para>
///
/// <para>
/// <b>The questions are fixed here, not authored by the model.</b> This is 40.28's call
/// («коды на проводе, предложения на сервере», docs/DECISIONS.md 2026-08-18) applied unchanged, and
/// for the same two reasons: a model writes a different question every run, so the screen cannot
/// count, sort or translate them; and a model occasionally asks for something the product cannot
/// accept. It matters more here than it did there, because a question is answered into a database
/// column — «пришлите ваш прайс в PDF» is a question with no field behind it.
/// </para>
/// </summary>
public static class OrganizationProfileGapCodes
{
    /// <summary>Nothing in <c>product</c>.</summary>
    public const string Product = OrganizationProfileFields.Product;

    /// <summary>Nothing in <c>icp</c>.</summary>
    public const string Icp = OrganizationProfileFields.Icp;

    /// <summary>Fewer than <see cref="MinimumObjectionCount"/> objections.</summary>
    public const string Objections = OrganizationProfileFields.Objections;

    /// <summary>Fewer than <see cref="MinimumScriptStageCount"/> script stages.</summary>
    public const string ScriptStages = OrganizationProfileFields.ScriptStages;

    /// <summary>Nothing in <c>tone</c>.</summary>
    public const string Tone = OrganizationProfileFields.Tone;

    /// <summary>Nothing in <c>banned_claims</c>.</summary>
    public const string BannedClaims = OrganizationProfileFields.BannedClaims;

    /// <summary>Nothing in <c>glossary</c>.</summary>
    public const string Glossary = OrganizationProfileFields.Glossary;

    /// <summary>
    /// The gap stops [CONTENT_PARAMETERIZATION.md](../../../../docs/CONTENT_PARAMETERIZATION.md) from
    /// working at all: the placeholder resolves to the neutral fallback in every lesson and every
    /// persona prompt, so the customer sees the library exactly as it read before 40.19 existed. This
    /// is the tier the roadmap's second bullet is about — «профиль останется пустым, и параметризация
    /// базового контента не заработает вообще».
    /// </summary>
    public const string BlockingPriority = "blocking";

    /// <summary>
    /// Substitution works without it, and what it substitutes is worse. A profile with no tone and no
    /// banned claims renders correct sentences in a voice that is not the customer's, and lets a
    /// persona voice a promise a regulated customer's lawyer has forbidden.
    /// </summary>
    public const string ImportantPriority = "important";

    /// <summary>Cosmetic. Nothing degrades measurably while it is empty.</summary>
    public const string OptionalPriority = "optional";

    /// <summary>
    /// Objections the profile needs before it stops being a gap. Three rather than 40.28's two: that
    /// threshold asks whether one lesson can be built, this one asks whether every persona in the
    /// product has a plausible repertoire, and a persona that only ever raises two objections is
    /// recognisable as a script by the second session.
    /// </summary>
    public const int MinimumObjectionCount = 3;

    /// <summary>
    /// Stages that count as a conversation rather than a note — an opening, something in the middle,
    /// and a close. Same number as 40.28's, and deliberately the same reasoning.
    /// </summary>
    public const int MinimumScriptStageCount = 3;

    /// <summary>
    /// Every code, in the order the interview asks about them. <b>The order is the feature.</b> The
    /// roadmap's promise is «5 минут вместо часа», and the way to break it is to show all seven
    /// questions at once — that is the thirty-field form again with fewer fields. The list is ordered
    /// by how much the answer changes what the customer sees, and
    /// <c>OrganizationProfileGapInspector</c> hands out only the first few.
    /// </summary>
    public static readonly string[] All =
    [
        Product,
        Icp,
        Objections,
        ScriptStages,
        Tone,
        BannedClaims,
        Glossary
    ];

    private static readonly Dictionary<string, string> Priorities = new(StringComparer.Ordinal)
    {
        [Product] = BlockingPriority,
        [Icp] = BlockingPriority,
        [Objections] = BlockingPriority,
        [ScriptStages] = ImportantPriority,
        [Tone] = ImportantPriority,
        [BannedClaims] = ImportantPriority,
        [Glossary] = OptionalPriority
    };

    /// <summary>
    /// The question the РОП reads. Russian, and each one is answerable from memory in under a minute
    /// — that is the whole constraint. A question that sends somebody looking for a document is a
    /// question that gets answered next week, which in practice means never, which is the state the
    /// block exists to get out of.
    ///
    /// <para>
    /// The two questions whose honest answer may be «таких нет» say so out loud. The profile has no
    /// «отвечено: ничего» marker and is not getting one for two fields, so those two gaps can persist
    /// forever — which is harmless, because readiness is computed from the blocking tier only and the
    /// cap on the list means they are never shown while a real gap is open.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> Questions = new(StringComparer.Ordinal)
    {
        [Product] =
            "Что именно вы продаёте? Одно-два предложения так, как вы объясняете это клиенту "
            + "на первом звонке.",
        [Icp] =
            "Кому вы продаёте? Сегмент, кто принимает решение, средний размер сделки — коротко.",
        [Objections] =
            "Какие возражения ваши менеджеры слышат чаще всего? Достаточно трёх-четырёх, "
            + "своими словами.",
        [ScriptStages] =
            "Из каких этапов состоит ваш звонок или встреча? Перечислите шаги от приветствия "
            + "до договорённости.",
        [Tone] =
            "Как ваши менеджеры разговаривают с клиентом: строго по-деловому, на равных или "
            + "как консультант?",
        [BannedClaims] =
            "Есть ли обещания, которые вашим менеджерам давать нельзя — гарантии дохода, сроков, "
            + "результата? Если таких нет, пропустите вопрос.",
        [Glossary] =
            "Есть ли слова, которые у вас называются по-своему — «сделка», «лид», «клиент»? "
            + "Если нет, пропустите вопрос."
    };

    public static bool IsKnown(string? code) => code is not null && Questions.ContainsKey(code);

    /// <summary>
    /// The question for a code, or <see langword="null"/> for a code this service does not know.
    /// Unknown codes are dropped rather than shown blank — the vocabulary is closed on purpose, the
    /// same rule 40.28 applies to the refusal codes arriving from ai-service.
    /// </summary>
    public static string? QuestionFor(string? code)
        => code is not null && Questions.TryGetValue(code, out var question) ? question : null;

    /// <summary>The tier for a code, or <see langword="null"/> for an unknown one.</summary>
    public static string? PriorityFor(string? code)
        => code is not null && Priorities.TryGetValue(code, out var priority) ? priority : null;

    /// <summary>
    /// True when the gap is one of the three that stop <c>{{organization.*}}</c> substitution from
    /// doing anything at all. This is what «профиль заполнен достаточно» means in practice, and it is
    /// deliberately a narrower claim than «профиль заполнен».
    /// </summary>
    public static bool IsBlocking(string code) => PriorityFor(code) == BlockingPriority;
}
