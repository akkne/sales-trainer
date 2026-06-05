// Exercise content type definitions for all 10 exercise types
// Must match backend ExerciseTypes.cs

// Type constants - must match backend ExerciseTypes.cs
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
    "free_text"
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
};

// Content interfaces for each exercise type
export interface ChooseOptionContent {
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

export interface ReorderContent {
    instruction: string;
    items: string[];
    correctOrder: string[];
    explanation: string;
}

export interface MatchPairsContent {
    instruction: string;
    leftItems: string[];
    rightItems: string[];
    correctPairs: Array<{ left: string; right: string }>;
    explanation: string;
}

export interface CategorizeContent {
    instruction: string;
    categories: string[];
    items: Array<{ id: string; text: string }>;
    correctMapping: Record<string, string>;
    explanation: string;
}

export interface SpotMistakeContent {
    instruction: string;
    dialogLines: Array<{ id: string; speaker: string; text: string }>;
    errorLineId: string;
    requireExplanation: boolean;
    suggestedFixes?: Array<{ id: string; text: string }>;
    correctFixIds?: string[];
    aiPrompt: string;
}

export interface RewriteContent {
    originalText: string;
    context: string;
    minLength: number;
    maxLength: number;
    aiPrompt: string;
}

export interface AiDialogueContent {
    scenario: string;
    persona: { name: string; role: string; personality: string };
    systemPrompt: string;
    minTurnsForCompletion: number;
    aiPrompt: string;
}

export interface EvaluateCallContent {
    transcript: Array<{ speaker: string; text: string }>;
    criteria: Array<{ id: string; name: string; description: string }>;
    aiPrompt: string;
}

export interface FreeTextContent {
    prompt: string;
    context: string;
    minLength: number;
    maxLength: number;
    aiPrompt: string;
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
    | FreeTextContent;

// Empty content factories
export function emptyChooseOption(): ChooseOptionContent {
    return { situation: "", question: "", options: ["", "", "", ""], correctOptionIndex: 0, explanation: "" };
}

export function emptyFillBlank(): FillBlankContent {
    return { characterName: "", characterLine: "___", options: ["", "", "", ""], correctOptionIndex: 0, explanation: "" };
}

export function emptyReorder(): ReorderContent {
    return { instruction: "", items: ["", "", ""], correctOrder: [], explanation: "" };
}

export function emptyMatchPairs(): MatchPairsContent {
    return {
        instruction: "",
        leftItems: ["", ""],
        rightItems: ["", ""],
        correctPairs: [],
        explanation: ""
    };
}

export function emptyCategorize(): CategorizeContent {
    return {
        instruction: "",
        categories: ["Category A", "Category B"],
        items: [{ id: "1", text: "" }],
        correctMapping: {},
        explanation: ""
    };
}

export function emptySpotMistake(): SpotMistakeContent {
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

export function emptyRewrite(): RewriteContent {
    return { originalText: "", context: "", minLength: 20, maxLength: 500, aiPrompt: "" };
}

export function emptyAiDialogue(): AiDialogueContent {
    return {
        scenario: "",
        persona: { name: "", role: "", personality: "" },
        systemPrompt: "",
        minTurnsForCompletion: 4,
        aiPrompt: ""
    };
}

export function emptyEvaluateCall(): EvaluateCallContent {
    return {
        transcript: [{ speaker: "", text: "" }],
        criteria: [{ id: "1", name: "", description: "" }],
        aiPrompt: ""
    };
}

export function emptyFreeText(): FreeTextContent {
    return { prompt: "", context: "", minLength: 50, maxLength: 1000, aiPrompt: "" };
}

// Shared styling classes
export const inputCls = "mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary bg-surface";
export const labelCls = "text-xs text-on-surface-variant";
export const textareaCls = "mt-1 w-full border border-outline-variant rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary font-mono bg-surface";

// Legacy aliases for backward compatibility during migration
export type MultipleChoiceContent = ChooseOptionContent;
export type OpenQuestionContent = FreeTextContent;
export type OrderingContent = ReorderContent;
export type MatchingContent = MatchPairsContent;
export type CategorizingContent = CategorizeContent;
export type FindErrorContent = SpotMistakeContent;
export type RewriteBetterContent = RewriteContent;
export type AiDialogContent = AiDialogueContent;
export type RateCallContent = EvaluateCallContent;
export type WrittenAnswerContent = FreeTextContent;

export const emptyMultipleChoice = emptyChooseOption;
export const emptyOpenQuestion = emptyFreeText;
export const emptyOrdering = emptyReorder;
export const emptyMatching = emptyMatchPairs;
export const emptyCategorizing = emptyCategorize;
export const emptyFindError = emptySpotMistake;
export const emptyRewriteBetter = emptyRewrite;
export const emptyAiDialog = emptyAiDialogue;
export const emptyRateCall = emptyEvaluateCall;
export const emptyWrittenAnswer = emptyFreeText;
