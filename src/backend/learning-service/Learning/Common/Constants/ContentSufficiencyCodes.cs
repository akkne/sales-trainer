namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.28. Why the pipeline refused, and — the whole point of the block — <b>what to bring
/// instead</b>.
///
/// <para>
/// <b>The refusal is a useful answer, not an error.</b> A РОП who uploads three slides and gets
/// fifteen bland exercises blames the product, not their deck (roadmap 40.28). A РОП who gets
/// «добавьте примеры возражений или запись звонка» knows what to do next, and the second sentence is
/// worth more than the fifteen exercises. So every refusal is a list of these codes, each with a
/// fixed sentence — machine-readable enough for the 40.20 screen to render bullets, and identical on
/// every run so support can recognise it.
/// </para>
///
/// <para>
/// <b>The sentences live here and not in the model.</b> A model asked to write the refusal writes a
/// different one every time, occasionally demands something the product cannot accept («пришлите
/// договор»), and cannot be translated. ai-service therefore returns codes only
/// (<c>MaterialGapCodes</c>, the same list on the other side of the wire) and this class turns them
/// into the customer's sentence.
/// </para>
/// </summary>
public static class ContentSufficiencyCodes
{
    /// <summary>The material is not about selling at all.</summary>
    public const string OffTopic = "off_topic";

    /// <summary>There is material and there is not enough of it.</summary>
    public const string TooShort = "too_short";

    /// <summary>Nothing says what the company sells.</summary>
    public const string NoProduct = "no_product";

    /// <summary>Nothing says who they sell to.</summary>
    public const string NoIcp = "no_icp";

    /// <summary>No objection a client actually voices appears anywhere.</summary>
    public const string NoObjections = "no_objections";

    /// <summary>Nothing about how a conversation is supposed to go.</summary>
    public const string NoScript = "no_script";

    /// <summary>Abstractions only — no live wording to build an exercise on.</summary>
    public const string NoExamples = "no_examples";

    public static readonly string[] All =
    [
        OffTopic,
        TooShort,
        NoProduct,
        NoIcp,
        NoObjections,
        NoScript,
        NoExamples
    ];

    /// <summary>
    /// The sentence the РОП reads. Russian, imperative, and always naming a concrete artefact they
    /// already have somewhere — a deck, a script, a call recording — because «добавьте больше
    /// информации» is a refusal that teaches nothing.
    /// </summary>
    private static readonly Dictionary<string, string> Messages = new(StringComparer.Ordinal)
    {
        [OffTopic] =
            "Похоже, этот материал не про продажи. Загрузите презентацию продукта, скрипт звонка "
            + "или расшифровку разговора с клиентом.",
        [TooShort] =
            "Материала слишком мало, чтобы получились хорошие упражнения. Добавьте скрипт звонка, "
            + "расшифровку разговора или заметки с планёрки — хватит одной-двух страниц текста.",
        [NoProduct] =
            "Из материала не понятно, что именно вы продаёте. Добавьте описание продукта: что это, "
            + "какую задачу клиента решает, чем отличается от конкурентов.",
        [NoIcp] =
            "Из материала не понятно, кому вы продаёте. Добавьте описание клиента: сегмент, кто "
            + "принимает решение, средний размер сделки.",
        [NoObjections] =
            "В материале нет ни одного возражения клиента. Добавьте примеры возражений, которые "
            + "менеджеры слышат чаще всего, или запись звонка, где они звучат.",
        [NoScript] =
            "В материале нет этапов разговора. Добавьте скрипт звонка или встречи — хотя бы "
            + "перечень шагов от приветствия до договорённости.",
        [NoExamples] =
            "В материале только общие формулировки. Добавьте живые примеры: реплики клиентов, "
            + "ответы менеджеров, расшифровку реального разговора."
    };

    public static bool IsKnown(string? code) => code is not null && Messages.ContainsKey(code);

    /// <summary>
    /// The sentence for a code, or <see langword="null"/> for a code this service does not know. An
    /// unknown code is dropped rather than shown blank: the vocabulary is closed on purpose and a
    /// code invented by a model would otherwise arrive at a customer as an empty bullet.
    /// </summary>
    public static string? MessageFor(string? code)
        => code is not null && Messages.TryGetValue(code, out var message) ? message : null;
}
