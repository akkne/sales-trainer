// Exercise content type definitions for all 11 exercise types

export interface MultipleChoiceContent {
    situation: string;
    question: string;
    options: string[];
    correctOptionIndex: number;
    explanation: string;
}

export interface FillBlankContent {
    characterName: string;
    characterLine: string;
    options: string[];
    correctOptionIndex: number;
    explanation: string;
}

export interface OpenQuestionContent {
    question: string;
    aiPrompt: string;
}

export interface OrderingContent {
    instruction: string;
    items: string[];
    correctOrder: string[];
    explanation: string;
}

export interface MatchingContent {
    instruction: string;
    leftItems: string[];
    rightItems: string[];
    correctPairs: Array<{ left: string; right: string }>;
    explanation: string;
}

export interface CategorizingContent {
    instruction: string;
    categories: string[];
    items: Array<{ id: string; text: string }>;
    correctMapping: Record<string, string>;
    explanation: string;
}

export interface FindErrorContent {
    instruction: string;
    dialogLines: Array<{ id: string; speaker: string; text: string }>;
    errorLineId: string;
    requireExplanation: boolean;
    suggestedFixes?: Array<{ id: string; text: string }>;
    correctFixIds?: string[];
    aiPrompt: string;
}

export interface RewriteBetterContent {
    originalText: string;
    context: string;
    minLength: number;
    maxLength: number;
    aiPrompt: string;
}

export interface AiDialogContent {
    scenario: string;
    persona: { name: string; role: string; personality: string };
    systemPrompt: string;
    minTurnsForCompletion: number;
    aiPrompt: string;
}

export interface RateCallContent {
    transcript: Array<{ speaker: string; text: string }>;
    criteria: Array<{ id: string; name: string; description: string }>;
    aiPrompt: string;
}

export interface WrittenAnswerContent {
    prompt: string;
    context: string;
    minLength: number;
    maxLength: number;
    aiPrompt: string;
}

export type ExerciseContent =
    | MultipleChoiceContent
    | FillBlankContent
    | OpenQuestionContent
    | OrderingContent
    | MatchingContent
    | CategorizingContent
    | FindErrorContent
    | RewriteBetterContent
    | AiDialogContent
    | RateCallContent
    | WrittenAnswerContent;

// Empty content factories
export function emptyMultipleChoice(): MultipleChoiceContent {
    return { situation: "", question: "", options: ["", "", "", ""], correctOptionIndex: 0, explanation: "" };
}

export function emptyFillBlank(): FillBlankContent {
    return { characterName: "", characterLine: "___", options: ["", "", "", ""], correctOptionIndex: 0, explanation: "" };
}

export function emptyOpenQuestion(): OpenQuestionContent {
    return { question: "", aiPrompt: "" };
}

export function emptyOrdering(): OrderingContent {
    return { instruction: "", items: ["", "", ""], correctOrder: [], explanation: "" };
}

export function emptyMatching(): MatchingContent {
    return {
        instruction: "",
        leftItems: ["", ""],
        rightItems: ["", ""],
        correctPairs: [],
        explanation: ""
    };
}

export function emptyCategorizing(): CategorizingContent {
    return {
        instruction: "",
        categories: ["Category A", "Category B"],
        items: [{ id: "1", text: "" }],
        correctMapping: {},
        explanation: ""
    };
}

export function emptyFindError(): FindErrorContent {
    return {
        instruction: "",
        dialogLines: [{ id: "1", speaker: "", text: "" }],
        errorLineId: "",
        requireExplanation: false,
        suggestedFixes: [],
        correctFixIds: [],
        aiPrompt: ""
    };
}

export function emptyRewriteBetter(): RewriteBetterContent {
    return { originalText: "", context: "", minLength: 20, maxLength: 500, aiPrompt: "" };
}

export function emptyAiDialog(): AiDialogContent {
    return {
        scenario: "",
        persona: { name: "", role: "", personality: "" },
        systemPrompt: "",
        minTurnsForCompletion: 4,
        aiPrompt: ""
    };
}

export function emptyRateCall(): RateCallContent {
    return {
        transcript: [{ speaker: "", text: "" }],
        criteria: [{ id: "1", name: "", description: "" }],
        aiPrompt: ""
    };
}

export function emptyWrittenAnswer(): WrittenAnswerContent {
    return { prompt: "", context: "", minLength: 50, maxLength: 1000, aiPrompt: "" };
}

// Type constants
export const EXERCISE_TYPES = [
    "multiple_choice",
    "fill_blank",
    "open_question",
    "ordering",
    "matching",
    "categorizing",
    "find_error",
    "rewrite_better",
    "ai_dialog",
    "rate_call",
    "written_answer"
] as const;

export type ExerciseType = (typeof EXERCISE_TYPES)[number];

export const TYPE_LABELS: Record<ExerciseType, string> = {
    multiple_choice: "Multiple Choice",
    fill_blank: "Fill Blank",
    open_question: "Open Question",
    ordering: "Ordering",
    matching: "Matching",
    categorizing: "Categorizing",
    find_error: "Find Error",
    rewrite_better: "Rewrite Better",
    ai_dialog: "AI Dialog",
    rate_call: "Rate Call",
    written_answer: "Written Answer",
};

// Shared styling classes
export const inputCls = "mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary bg-surface";
export const labelCls = "text-xs text-on-surface-variant";
export const textareaCls = "mt-1 w-full border border-outline-variant rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary font-mono bg-surface";
