using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Features.ContentGeneration.Services.Implementation;

/// <summary>
/// Phase 40.28. «Порог достаточности входа» — the two places the pipeline is allowed to say no.
///
/// <para>
/// <b>Why the threshold exists at all.</b> A РОП who pastes three slides and gets fifteen bland
/// exercises does not conclude that their deck was thin. They conclude that the product is weak, and
/// they are not wrong to — we are the ones who chose to answer. Four good exercises, or an honest
/// «добавьте примеры возражений или запись звонка», are both better outcomes than fifteen bland ones
/// (roadmap 40.28).
/// </para>
///
/// <para>
/// <b>Two stages, because neither one alone is honest.</b>
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b><see cref="InspectMaterial"/> — deterministic, free, before any call.</b> It knows how much
/// text there is and whether a single word in it belongs to selling. It cannot tell three slides
/// about a CRM from three pages of a recipe on length alone, which is exactly why the lexical check
/// is here: a document about selling that contains no word about selling does not exist, and this
/// costs nothing to establish.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b><see cref="InspectStructure"/> — after structuring, and the honest signal.</b> Length is a
/// proxy; what actually decides whether four good exercises can be built is what could be read out of
/// the material. A structure with no objections and no script stages means the material was thin
/// whatever its character count said — and a model that returned an invented ICP over an empty deck
/// is the same failure arriving later, which is why the structure is judged rather than trusted.
/// </description>
/// </item>
/// </list>
///
/// <para>
/// <b>The model's verdict can add a refusal and never lift one.</b> It rides the structuring call for
/// free (docs/DECISIONS.md, 2026-08-18) and it is the only judge that can recognise a recipe that
/// happens to mention a price. But «выглядит достаточно» over an empty structure must not open the
/// gate: otherwise the threshold is bypassed by whichever completion happens to be confident.
/// </para>
/// </summary>
internal static class ContentSufficiencyInspector
{
    /// <summary>
    /// The floor on raw material, in characters. Twice 40.27's 200, and it is still not the real
    /// check — it is the size below which structuring cannot possibly repay its own call. A deck's
    /// worth of text is thousands of characters; 400 is «one paragraph», which is a note, not a
    /// training.
    /// </summary>
    public const int MinimumMaterialLength = 400;

    /// <summary>
    /// The same floor counted in words, because a character count is fooled by one pasted URL or a
    /// wall of numbers.
    /// </summary>
    public const int MinimumMaterialWordCount = 60;

    /// <summary>
    /// Objections needed before the structure counts as something to drill. Two rather than one:
    /// one objection makes one exercise, and the promise of the block is four good ones.
    /// </summary>
    public const int MinimumObjectionCount = 2;

    /// <summary>
    /// Script stages that count as a conversation rather than a note. Three — an opening, something
    /// in the middle, and a close — is the smallest thing an exercise can walk a seller through.
    /// </summary>
    public const int MinimumScriptStageCount = 3;

    /// <summary>
    /// Stems of words that only appear in material about selling, in Russian and English. Matching is
    /// substring-on-lowercase, so stems are deliberately short and morphology-free.
    ///
    /// <para>
    /// <b>The test is zero hits, not a ratio.</b> A ratio would be a quality score and would start
    /// refusing unusual but perfectly good material; zero hits across an entire document is the
    /// signal that the wrong file was uploaded. A false positive is survivable and self-correcting —
    /// the refusal tells the customer what to add, and adding one sentence about what they sell
    /// clears it — which is the only reason a lexical rule is allowed to block anything at all.
    /// </para>
    /// </summary>
    private static readonly string[] SalesVocabularyStems =
    [
        "продаж", "продав", "продат", "продаё", "продае", "клиент", "покупател", "заказчик",
        "возражен", "скидк", "цена", "цены", "цену", "цене", "ценой", "прайс", "сделк", "звонк",
        "звонок", "менеджер", "воронк", "оффер", "коммерческ", "договор", "тариф", "конкурент",
        "презентац", "скрипт", "переговор", "лпр", "апселл", "допрода", "закрыти", "лид",
        "sale", "selling", "customer", "client", "objection", "discount", "pricing", "deal",
        "prospect", "pipeline", "upsell", "negotiat", "buyer", "crm"
    ];

    /// <summary>
    /// Stage one: the text itself, before a single token is paid for. Returns <see langword="null"/>
    /// when there is nothing to complain about yet — which is not a promise that the material is
    /// good, only that it is worth reading.
    /// </summary>
    public static ContentInsufficiencyDto? InspectMaterial(string material)
    {
        var text = material ?? string.Empty;

        if (text.Length < MinimumMaterialLength || CountWords(text) < MinimumMaterialWordCount)
        {
            return Refusal(ContentInsufficiencyDto.MaterialStage, [ContentSufficiencyCodes.TooShort], note: null);
        }

        // Only asked once the volume test has passed: refusing a two-line note for being off-topic
        // would be true and useless, and «добавьте материала» is the sentence that actually helps.
        if (!ContainsSalesVocabulary(text))
        {
            return Refusal(ContentInsufficiencyDto.MaterialStage, [ContentSufficiencyCodes.OffTopic], note: null);
        }

        return null;
    }

    /// <summary>
    /// Stage two: what was actually read out of the material, plus the model's opinion of the
    /// material it read. Returns <see langword="null"/> when the run may proceed to the checkpoint.
    /// </summary>
    /// <param name="verdict">
    /// The structuring call's verdict, or <see langword="null"/> when there is none — which is the
    /// case every time a human edits the structure by hand afterwards. From that point on the
    /// structure alone decides, and that is deliberate: it gives the person the last word without
    /// buying a second opinion from the provider, and a person who types four real objections has
    /// answered the question the refusal asked.
    /// </param>
    public static ContentInsufficiencyDto? InspectStructure(
        ContentStructureDto structure,
        AiMaterialSufficiency? verdict = null)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var codes = new List<string>();

        // Something to teach about. Either half answers it: a structure that knows the product but
        // not the buyer still supports exercises, and so does the reverse.
        if (string.IsNullOrWhiteSpace(structure.Product) && string.IsNullOrWhiteSpace(structure.Icp))
        {
            codes.Add(ContentSufficiencyCodes.NoProduct);
            codes.Add(ContentSufficiencyCodes.NoIcp);
        }

        // Something to drill. Objections and a script are the two shapes a sales exercise takes; with
        // neither, generation has a topic and no task, and produces the fifteen bland exercises this
        // block exists to prevent.
        if (structure.Objections.Count < MinimumObjectionCount
            && structure.ScriptStages.Count < MinimumScriptStageCount)
        {
            codes.Add(ContentSufficiencyCodes.NoObjections);
            codes.Add(ContentSufficiencyCodes.NoScript);
        }

        if (verdict is not null)
        {
            if (verdict.IsOffTopic)
            {
                codes.Add(ContentSufficiencyCodes.OffTopic);
            }

            // An unjustified refusal is dropped on purpose. A model that says «недостаточно» without
            // naming anything gives the customer nothing to do, and an unactionable refusal is the
            // one thing this block must never produce — so it is treated as no opinion at all.
            if (!verdict.IsSufficient)
            {
                codes.AddRange(verdict.MissingCodes);
            }
        }

        return codes.Count == 0
            ? null
            : Refusal(ContentInsufficiencyDto.StructureStage, codes, verdict?.Note);
    }

    /// <summary>
    /// Turns codes into the refusal that is stored and shown. Unknown codes are dropped, duplicates
    /// collapse, and the order is <c>ContentSufficiencyCodes.All</c>'s rather than the caller's, so
    /// the same refusal reads the same way whichever stage produced it.
    /// </summary>
    private static ContentInsufficiencyDto? Refusal(string stage, IReadOnlyList<string> codes, string? note)
    {
        var gaps = ContentSufficiencyCodes.All
            .Where(codes.Contains)
            .Select(code => new ContentSufficiencyGapDto(code, ContentSufficiencyCodes.MessageFor(code)!))
            .ToList();

        return gaps.Count == 0 ? null : new ContentInsufficiencyDto(stage, gaps, note);
    }

    private static bool ContainsSalesVocabulary(string text)
    {
        var lowered = text.ToLowerInvariant();

        return SalesVocabularyStems.Any(stem => lowered.Contains(stem, StringComparison.Ordinal));
    }

    private static int CountWords(string text)
    {
        var wordCount = 0;
        var insideWord = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                insideWord = false;
                continue;
            }

            if (!insideWord)
            {
                insideWord = true;
                wordCount++;
            }
        }

        return wordCount;
    }
}
