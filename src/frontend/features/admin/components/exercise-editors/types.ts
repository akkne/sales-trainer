// Exercise content type definitions for all 10 exercise types.
//
// IMPORTANT: these shapes are the SINGLE SOURCE OF TRUTH for exercise content and
// MUST match the runtime exactly:
//   - backend grading:  src/backend/api/Features/Exercises/Services/Implementation/*EvaluationStrategy.cs
//   - learner UI:       src/frontend/features/exercise/components/*-exercise.tsx
//   - docs:             docs/NEW_EXERCISE_TYPES.md
//
// All content fields use snake_case because the JSON is stored verbatim and read
// back by the backend strategies (e.g. `is_correct`, `correct_position`,
// `is_mistake`, `ai_prompt`). Do NOT introduce camelCase content fields — the
// learner UI and grader will not see them.

import type { TheoryCardContent } from "@/features/exercise/types/theory-card";
export type { TheoryCardContent } from "@/features/exercise/types/theory-card";

// Type constants — must match backend ExerciseTypes.cs
export const EXERCISE_TYPES = [
    "choose_option",
    "fill_blank",
    "reorder",
    "match_pairs",
    "categorize",
    "spot_mistake",
    "rewrite",
    "ai_dialogue",
    "evaluate_call",
    "free_text",
    "theory_card",
] as const;

export type ExerciseType = (typeof EXERCISE_TYPES)[number];

export const TYPE_LABELS: Record<ExerciseType, string> = {
    choose_option: "Choose Option",
    fill_blank: "Fill Blank",
    reorder: "Reorder",
    match_pairs: "Match Pairs",
    categorize: "Categorize",
    spot_mistake: "Spot Mistake",
    rewrite: "Rewrite",
    ai_dialogue: "AI Dialogue",
    evaluate_call: "Evaluate Call",
    free_text: "Free Text",
    theory_card: "Theory Card",
};

// AI-evaluated types. These read a per-exercise `ai_prompt` from content and a
// global system prompt from the ExerciseTypePrompts table (the "AI Prompts" tab).
export const AI_EXERCISE_TYPES: ExerciseType[] = [
    "spot_mistake",
    "rewrite",
    "ai_dialogue",
    "evaluate_call",
    "free_text",
];

export function isAiExerciseType(type: ExerciseType): boolean {
    return AI_EXERCISE_TYPES.includes(type);
}

// --- Shared sub-shapes ---
export interface FlaggedOption {
    text: string;
    is_correct: boolean;
}

// --- Content interfaces (canonical, snake_case) ---

export interface ChooseOptionContent {
    situation: string;
    options: FlaggedOption[];
    explanation?: string;
}

export interface FillBlankContent {
    before: string;
    after: string;
    options: FlaggedOption[];
    explanation?: string;
}

export interface ReorderItem {
    text: string;
    correct_position: number;
}
export interface ReorderContent {
    instruction: string;
    items: ReorderItem[];
    explanation?: string;
}

export interface MatchPair {
    left: string;
    right: string;
}
export interface MatchPairsContent {
    instruction: string;
    pairs: MatchPair[];
    explanation?: string;
}

export interface CategorizeItem {
    text: string;
    category: string;
}
export interface CategorizeContent {
    instruction: string;
    categories: string[];
    items: CategorizeItem[];
    explanation?: string;
}

export interface DialogueLine {
    speaker: string;
    text: string;
    is_mistake: boolean;
}
export interface SpotMistakeContent {
    dialogue: DialogueLine[];
    explanation?: string;
    ai_prompt?: string;
}

export interface RewriteContent {
    instruction: string;
    original: string;
    evaluation_criteria?: string[];
    ai_prompt?: string;
}

export interface AiDialogueContent {
    persona: string;
    scenario: string;
    context?: string;
    max_turns?: number;
    success_criteria?: string[];
    ai_prompt?: string;
}

export interface TranscriptLine {
    speaker: string;
    text: string;
}
export interface EvaluationAxis {
    name: string;
    description: string;
}
export interface EvaluateCallContent {
    transcript: TranscriptLine[];
    evaluation_axes: EvaluationAxis[];
    ai_prompt?: string;
}

export interface FreeTextContent {
    situation?: string;
    instruction: string;
    evaluation_criteria?: string[];
    ai_prompt?: string;
}

export type ExerciseContent =
    | ChooseOptionContent
    | FillBlankContent
    | ReorderContent
    | MatchPairsContent
    | CategorizeContent
    | SpotMistakeContent
    | RewriteContent
    | AiDialogueContent
    | EvaluateCallContent
    | FreeTextContent
    | TheoryCardContent;

// --- Empty content factories (minimal editable starting points) ---

export function emptyChooseOption(): ChooseOptionContent {
    return {
        situation: "",
        options: [
            { text: "", is_correct: true },
            { text: "", is_correct: false },
            { text: "", is_correct: false },
        ],
        explanation: "",
    };
}

export function emptyFillBlank(): FillBlankContent {
    return {
        before: "",
        after: "",
        options: [
            { text: "", is_correct: true },
            { text: "", is_correct: false },
            { text: "", is_correct: false },
        ],
        explanation: "",
    };
}

export function emptyReorder(): ReorderContent {
    return {
        instruction: "",
        items: [
            { text: "", correct_position: 1 },
            { text: "", correct_position: 2 },
            { text: "", correct_position: 3 },
        ],
        explanation: "",
    };
}

export function emptyMatchPairs(): MatchPairsContent {
    return {
        instruction: "",
        pairs: [
            { left: "", right: "" },
            { left: "", right: "" },
        ],
        explanation: "",
    };
}

export function emptyCategorize(): CategorizeContent {
    return {
        instruction: "",
        categories: ["Category A", "Category B"],
        items: [
            { text: "", category: "Category A" },
            { text: "", category: "Category B" },
        ],
        explanation: "",
    };
}

export function emptySpotMistake(): SpotMistakeContent {
    return {
        dialogue: [
            { speaker: "seller", text: "", is_mistake: false },
            { speaker: "client", text: "", is_mistake: false },
        ],
        explanation: "",
        ai_prompt: "",
    };
}

export function emptyRewrite(): RewriteContent {
    return { instruction: "", original: "", evaluation_criteria: [], ai_prompt: "" };
}

export function emptyAiDialogue(): AiDialogueContent {
    return {
        persona: "",
        scenario: "",
        context: "",
        max_turns: 6,
        success_criteria: [],
        ai_prompt: "",
    };
}

export function emptyEvaluateCall(): EvaluateCallContent {
    return {
        transcript: [{ speaker: "seller", text: "" }],
        evaluation_axes: [{ name: "", description: "" }],
        ai_prompt: "",
    };
}

export function emptyFreeText(): FreeTextContent {
    return { situation: "", instruction: "", evaluation_criteria: [], ai_prompt: "" };
}

export function emptyTheoryCard(): TheoryCardContent {
    return { layout: "text", title: "", body: "" };
}

export function emptyContentFor(type: ExerciseType): ExerciseContent {
    switch (type) {
        case "choose_option": return emptyChooseOption();
        case "fill_blank": return emptyFillBlank();
        case "reorder": return emptyReorder();
        case "match_pairs": return emptyMatchPairs();
        case "categorize": return emptyCategorize();
        case "spot_mistake": return emptySpotMistake();
        case "rewrite": return emptyRewrite();
        case "ai_dialogue": return emptyAiDialogue();
        case "evaluate_call": return emptyEvaluateCall();
        case "free_text": return emptyFreeText();
        case "theory_card": return emptyTheoryCard();
    }
}

// --- Illustrative templates (realistic, valid example content per type) ---
// Used for "Download template" so an admin sees a fully-formed, correct example.

export const EXERCISE_CONTENT_TEMPLATES: Record<ExerciseType, ExerciseContent> = {
    choose_option: {
        situation: "The prospect says: 'This is too expensive.'",
        options: [
            { text: "I understand. I can offer a discount.", is_correct: false },
            { text: "Expensive compared to what?", is_correct: true },
            { text: "This is the best price on the market.", is_correct: false },
        ],
        explanation: "It's better to clarify the objection than to immediately lower the price.",
    },
    fill_blank: {
        before: "Prospect: We already have a supplier.",
        after: "Prospect: Well, overall yes, we could discuss it.",
        options: [
            { text: "I see, but we're better!", is_correct: false },
            { text: "What if I showed you how to save 20%?", is_correct: true },
            { text: "Too bad, goodbye.", is_correct: false },
        ],
        explanation: "An open value-focused question keeps the conversation going.",
    },
    reorder: {
        instruction: "Put the stages of a cold call in the correct order",
        items: [
            { text: "Greeting", correct_position: 1 },
            { text: "Needs discovery", correct_position: 2 },
            { text: "Presentation", correct_position: 3 },
            { text: "Objection handling", correct_position: 4 },
            { text: "Closing", correct_position: 5 },
        ],
        explanation: "Understand the need first, then propose a solution.",
    },
    match_pairs: {
        instruction: "Match each objection with the best response technique",
        pairs: [
            { left: "Too expensive", right: "Value comparison" },
            { left: "We don't need anything", right: "Boomerang technique" },
            { left: "Send it by email", right: "Bridge technique" },
        ],
        explanation: "Each objection calls for its own approach.",
    },
    categorize: {
        instruction: "Sort the questions into categories",
        categories: ["Good question", "Bad question"],
        items: [
            { text: "How many employees do you have?", category: "Good question" },
            { text: "Do you like our product?", category: "Bad question" },
            { text: "What are your goals for this quarter?", category: "Good question" },
            { text: "Would you like a discount?", category: "Bad question" },
        ],
        explanation: "Good discovery questions are open-ended and focused on understanding.",
    },
    spot_mistake: {
        dialogue: [
            { speaker: "seller", text: "Good afternoon! My name is Alex.", is_mistake: false },
            { speaker: "client", text: "Good afternoon.", is_mistake: false },
            { speaker: "seller", text: "We are the best CRM on the market!", is_mistake: true },
            { speaker: "client", text: "We don't need anything.", is_mistake: false },
        ],
        explanation: "The seller jumped straight into pitching instead of asking about the need.",
        ai_prompt: "",
    },
    rewrite: {
        instruction: "Rewrite the cold email subject line to make it more compelling",
        original: "Partnership proposal",
        evaluation_criteria: ["Personalisation", "Intrigue without clickbait", "Brevity (under 50 characters)"],
        ai_prompt: "",
    },
    ai_dialogue: {
        persona: "Skeptical Sam, IT director, gives short answers and is in a hurry",
        scenario: "Discovery call with an IT director",
        context: "The prospect is skeptical and values their time",
        max_turns: 6,
        success_criteria: ["Quality of questions", "Handling skepticism", "Achieving a next step"],
        ai_prompt: "",
    },
    evaluate_call: {
        transcript: [
            { speaker: "seller", text: "Hello, this is Alex from Growth Co." },
            { speaker: "client", text: "Good afternoon." },
            { speaker: "seller", text: "Are you considering new sales solutions?" },
        ],
        evaluation_axes: [
            { name: "Qualification", description: "Was qualification carried out?" },
            { name: "Open questions", description: "Were open-ended questions used?" },
            { name: "Next step", description: "Was a next step agreed?" },
        ],
        ai_prompt: "",
    },
    free_text: {
        situation: "The prospect says: 'This is too expensive for us.'",
        instruction: "Write a response to this objection",
        evaluation_criteria: ["Does not lower the price immediately", "Clarifies the reason for the objection", "Professional tone"],
        ai_prompt: "",
    },
    theory_card: {
        layout: "dialogue",
        title: "Clarifying question instead of a discount",
        turns: [
            { side: "them", text: "This is too expensive." },
            { side: "me", text: "Expensive compared to what?", annotations: ["clarification"] },
            { side: "them", text: "Well, competitors are cheaper." },
        ],
    },
};

// A single exercise object in import shape: { type, orderInLesson, content }.
export function buildExerciseImportTemplate(type: ExerciseType, orderInLesson = 1) {
    return {
        type,
        orderInLesson,
        content: EXERCISE_CONTENT_TEMPLATES[type],
    };
}

// --- Client-side content validation (mirrors backend ExerciseContentValidator) ---
// Returns a list of human-readable problems. Empty list == valid.

function isNonEmptyString(value: unknown): boolean {
    return typeof value === "string" && value.trim().length > 0;
}

function validateFlaggedOptions(options: unknown, errors: string[]): void {
    if (!Array.isArray(options) || options.length < 2) {
        errors.push("`options` must be an array of at least 2 items.");
        return;
    }
    let correctCount = 0;
    options.forEach((opt, i) => {
        if (typeof opt !== "object" || opt === null) {
            errors.push(`options[${i}] must be an object { text, is_correct }.`);
            return;
        }
        const o = opt as Record<string, unknown>;
        if (!isNonEmptyString(o.text)) errors.push(`options[${i}].text is required.`);
        if (typeof o.is_correct !== "boolean") errors.push(`options[${i}].is_correct must be a boolean.`);
        else if (o.is_correct) correctCount++;
    });
    if (correctCount !== 1) {
        errors.push(`Exactly one option must have is_correct: true (found ${correctCount}).`);
    }
}

export function validateExerciseContent(type: ExerciseType, content: unknown): string[] {
    const errors: string[] = [];
    if (typeof content !== "object" || content === null || Array.isArray(content)) {
        return ["content must be a JSON object."];
    }
    const c = content as Record<string, unknown>;

    switch (type) {
        case "choose_option":
            if (!isNonEmptyString(c.situation)) errors.push("`situation` is required.");
            validateFlaggedOptions(c.options, errors);
            break;
        case "fill_blank":
            if (typeof c.before !== "string") errors.push("`before` must be a string.");
            if (typeof c.after !== "string") errors.push("`after` must be a string.");
            validateFlaggedOptions(c.options, errors);
            break;
        case "reorder": {
            if (!isNonEmptyString(c.instruction)) errors.push("`instruction` is required.");
            const items = c.items;
            if (!Array.isArray(items) || items.length < 2) {
                errors.push("`items` must be an array of at least 2 items.");
            } else {
                items.forEach((it, i) => {
                    const o = it as Record<string, unknown>;
                    if (!isNonEmptyString(o?.text)) errors.push(`items[${i}].text is required.`);
                    if (typeof o?.correct_position !== "number") errors.push(`items[${i}].correct_position must be a number.`);
                });
                const positions = items.map((it) => (it as Record<string, unknown>).correct_position);
                const unique = new Set(positions);
                if (unique.size !== positions.length) errors.push("`correct_position` values must be unique.");
            }
            break;
        }
        case "match_pairs": {
            if (!isNonEmptyString(c.instruction)) errors.push("`instruction` is required.");
            const pairs = c.pairs;
            if (!Array.isArray(pairs) || pairs.length < 2) {
                errors.push("`pairs` must be an array of at least 2 items.");
            } else {
                pairs.forEach((p, i) => {
                    const o = p as Record<string, unknown>;
                    if (!isNonEmptyString(o?.left)) errors.push(`pairs[${i}].left is required.`);
                    if (!isNonEmptyString(o?.right)) errors.push(`pairs[${i}].right is required.`);
                });
            }
            break;
        }
        case "categorize": {
            if (!isNonEmptyString(c.instruction)) errors.push("`instruction` is required.");
            const categories = c.categories;
            const validCategories = Array.isArray(categories) && categories.length >= 2 && categories.every(isNonEmptyString);
            if (!validCategories) errors.push("`categories` must be an array of at least 2 non-empty strings.");
            const items = c.items;
            if (!Array.isArray(items) || items.length < 1) {
                errors.push("`items` must be a non-empty array.");
            } else if (validCategories) {
                items.forEach((it, i) => {
                    const o = it as Record<string, unknown>;
                    if (!isNonEmptyString(o?.text)) errors.push(`items[${i}].text is required.`);
                    if (!(categories as string[]).includes(o?.category as string)) {
                        errors.push(`items[${i}].category "${String(o?.category)}" is not in categories.`);
                    }
                });
            }
            break;
        }
        case "spot_mistake": {
            const dialogue = c.dialogue;
            if (!Array.isArray(dialogue) || dialogue.length < 2) {
                errors.push("`dialogue` must be an array of at least 2 lines.");
            } else {
                let mistakeCount = 0;
                dialogue.forEach((line, i) => {
                    const o = line as Record<string, unknown>;
                    if (!isNonEmptyString(o?.speaker)) errors.push(`dialogue[${i}].speaker is required.`);
                    if (!isNonEmptyString(o?.text)) errors.push(`dialogue[${i}].text is required.`);
                    if (typeof o?.is_mistake !== "boolean") errors.push(`dialogue[${i}].is_mistake must be a boolean.`);
                    else if (o.is_mistake) mistakeCount++;
                });
                if (mistakeCount !== 1) errors.push(`Exactly one line must have is_mistake: true (found ${mistakeCount}).`);
            }
            break;
        }
        case "rewrite":
            if (!isNonEmptyString(c.instruction)) errors.push("`instruction` is required.");
            if (!isNonEmptyString(c.original)) errors.push("`original` is required.");
            break;
        case "ai_dialogue":
            if (!isNonEmptyString(c.persona)) errors.push("`persona` is required.");
            if (!isNonEmptyString(c.scenario)) errors.push("`scenario` is required.");
            if (c.max_turns !== undefined && (typeof c.max_turns !== "number" || c.max_turns < 1)) {
                errors.push("`max_turns` must be a positive number.");
            }
            break;
        case "evaluate_call": {
            const transcript = c.transcript;
            if (!Array.isArray(transcript) || transcript.length < 1) {
                errors.push("`transcript` must be a non-empty array.");
            } else {
                transcript.forEach((line, i) => {
                    const o = line as Record<string, unknown>;
                    if (!isNonEmptyString(o?.speaker)) errors.push(`transcript[${i}].speaker is required.`);
                    if (!isNonEmptyString(o?.text)) errors.push(`transcript[${i}].text is required.`);
                });
            }
            const axes = c.evaluation_axes;
            if (!Array.isArray(axes) || axes.length < 1) {
                errors.push("`evaluation_axes` must be a non-empty array.");
            } else {
                axes.forEach((ax, i) => {
                    const o = ax as Record<string, unknown>;
                    if (!isNonEmptyString(o?.name)) errors.push(`evaluation_axes[${i}].name is required.`);
                    if (!isNonEmptyString(o?.description)) errors.push(`evaluation_axes[${i}].description is required.`);
                });
            }
            break;
        }
        case "free_text":
            if (!isNonEmptyString(c.instruction)) errors.push("`instruction` is required.");
            break;
        case "theory_card": {
            const layout = c.layout;
            const layouts = ["text", "dialogue", "bullets", "quote"];
            if (typeof layout !== "string" || !layouts.includes(layout)) {
                errors.push(`\`layout\` must be one of: ${layouts.join(", ")}.`);
                break;
            }
            if (layout === "text") {
                if (!isNonEmptyString(c.body)) errors.push("`body` is required.");
            } else if (layout === "dialogue") {
                const turns = c.turns;
                if (!Array.isArray(turns) || turns.length < 1) {
                    errors.push("`turns` must be a non-empty array.");
                } else {
                    turns.forEach((t, i) => {
                        const o = t as Record<string, unknown>;
                        if (o?.side !== "me" && o?.side !== "them") errors.push(`turns[${i}].side must be "me" or "them".`);
                        if (!isNonEmptyString(o?.text)) errors.push(`turns[${i}].text is required.`);
                    });
                }
            } else if (layout === "bullets") {
                const items = c.items;
                if (!Array.isArray(items) || items.length < 1 || !items.every(isNonEmptyString)) {
                    errors.push("`items` must be a non-empty array of non-empty strings.");
                }
            } else if (layout === "quote") {
                if (!isNonEmptyString(c.text)) errors.push("`text` is required.");
            }
            break;
        }
    }
    return errors;
}

// Shared styling classes (kept stable for editor components)
export const inputCls = "mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface";
export const labelCls = "text-xs text-ink-3";
export const textareaCls = "mt-1 w-full border border-line rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30 font-mono bg-surface";
