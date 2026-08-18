/**
 * Turns the three raw documents `GET /admin/content/overrides/{kind}/{id}` returns into aligned
 * blocks the review screen can put side by side (docs/TENANCY/ADMIN_UI_DESIGN.md O15).
 *
 * **This is not a diff and must not become one.** The API returns no diff deliberately: the first
 * half of a merge creates the pressure to «apply the non-conflicting hunks», and a merged grading
 * criterion then scores a live salesperson. What is computed here is the one thing the design
 * allows — whether a whole block's text is character-for-character the same across the columns.
 * There is nothing per-word, nothing per-line, and no action attached to the answer.
 */

const SCHEMA_VERSION_KEY = "schemaVersion";

/** Keys these canonical documents use, named in Russian where a Russian name is honest. */
const DOCUMENT_FIELD_LABELS: Record<string, string> = {
    title: "Заголовок",
    name: "Название",
    summary: "Кратко",
    body: "Текст",
    markdownContent: "Текст",
    category: "Категория",
    tags: "Метки",
    difficulty: "Сложность",
    coach: "Наставник",
    sortOrder: "Порядок",
    slug: "Slug",
    caseJson: "Кейс",
    dialogJson: "Диалог",
    primarySkillId: "Основной навык",
    additionalSkillIds: "Дополнительные навыки",
    skillId: "Навык",
    chatSystemPrompt: "Системный промпт разговора",
    feedbackSystemPrompt: "Системный промпт обратной связи",
};

export interface ComparisonBlock {
    /** Stable across documents — this is what aligns the columns into one row. */
    key: string;
    label: string;
    text: string;
}

export interface ComparisonRow {
    key: string;
    label: string;
    /** One cell per column, in the order the columns were passed. Null = absent in that document. */
    cells: (string | null)[];
    /** True when the present cells are not all identical. Block-level only, by string equality. */
    differs: boolean;
}

function renderValue(value: unknown): string {
    if (value === null || value === undefined) return "";
    if (typeof value === "string") return value;

    return JSON.stringify(value, null, 2) ?? "";
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === "object" && value !== null && !Array.isArray(value);
}

interface SnapshotExercise {
    exerciseId?: unknown;
    type?: unknown;
    content?: unknown;
    customAiPrompt?: unknown;
}

/**
 * A lesson snapshot (`LessonSnapshotSerializer`, schemaVersion 1) is `{title, exercises[],
 * schemaVersion}`. It gets exercise-shaped blocks so the columns line up exercise by exercise;
 * every other document is read generically by its top-level keys, which is what makes this work for
 * a technique and a reference material without three more branches.
 */
function isLessonSnapshot(document: Record<string, unknown>): boolean {
    return Array.isArray(document.exercises);
}

function buildLessonSnapshotBlocks(document: Record<string, unknown>): ComparisonBlock[] {
    const blocks: ComparisonBlock[] = [
        { key: "title", label: DOCUMENT_FIELD_LABELS.title, text: renderValue(document.title) },
    ];

    const exercises = (document.exercises as SnapshotExercise[]) ?? [];
    exercises.forEach((exercise, index) => {
        const exerciseId = typeof exercise?.exerciseId === "string" ? exercise.exerciseId : String(index);
        const type = typeof exercise?.type === "string" ? exercise.type : "";

        blocks.push({
            key: `exercise:${exerciseId}`,
            label: type ? `Упражнение ${index + 1} · ${type}` : `Упражнение ${index + 1}`,
            text: renderValue({ type, content: exercise?.content, customAiPrompt: exercise?.customAiPrompt ?? null }),
        });
    });

    return blocks;
}

function buildGenericBlocks(document: Record<string, unknown>): ComparisonBlock[] {
    return Object.keys(document)
        .filter((key) => key !== SCHEMA_VERSION_KEY)
        .map((key) => ({
            key,
            label: DOCUMENT_FIELD_LABELS[key] ?? key,
            text: renderValue(document[key]),
        }));
}

/** One document → its blocks. A document that is not an object at all has none. */
export function buildComparisonBlocks(document: unknown): ComparisonBlock[] {
    if (!isRecord(document)) return [];

    return isLessonSnapshot(document)
        ? buildLessonSnapshotBlocks(document)
        : buildGenericBlocks(document);
}

/**
 * Aligns several documents by block key. Key order follows the first document that has the key, so
 * the organization's own text keeps its own ordering and blocks that exist only upstream are
 * appended rather than silently dropped.
 */
export function alignComparisonBlocks(documents: readonly unknown[]): ComparisonRow[] {
    const perDocument = documents.map(buildComparisonBlocks);

    const order: string[] = [];
    const labels = new Map<string, string>();

    for (const blocks of perDocument) {
        for (const block of blocks) {
            if (!labels.has(block.key)) {
                labels.set(block.key, block.label);
                order.push(block.key);
            }
        }
    }

    return order.map((key) => {
        const cells = perDocument.map((blocks) => blocks.find((block) => block.key === key)?.text ?? null);
        const present = cells.filter((cell): cell is string => cell !== null);

        return {
            key,
            label: labels.get(key) ?? key,
            cells,
            differs: present.some((cell) => cell !== present[0]) || present.length !== cells.length,
        };
    });
}
