// Content shapes for the theory_card exercise type (non-graded "story" cards).
// Single source of truth — mirrored by:
//   - backend validation: src/backend/api/Features/Exercises/Services/ExerciseContentValidator.cs (ValidateTheoryCard)
//   - docs:               docs/NEW_EXERCISE_TYPES.md (Type 11)
// The `layout` discriminator selects the visual template and the fields under it.

export type TheoryCardLayout = "text" | "dialogue" | "bullets" | "quote";

// A dialogue turn. "me" = salesperson (right bubble), "them" = client (left bubble).
// Same side semantics as the Guidebook dialogue renderer.
export interface TheoryDialogueTurn {
    side: "me" | "them";
    text: string;
    annotations?: string[];
}

export interface TheoryTextCard {
    layout: "text";
    title?: string;
    body: string;
}

export interface TheoryDialogueCard {
    layout: "dialogue";
    title?: string;
    turns: TheoryDialogueTurn[];
}

export interface TheoryBulletsCard {
    layout: "bullets";
    title?: string;
    items: string[];
}

export interface TheoryQuoteCard {
    layout: "quote";
    text: string;
    author?: string;
}

export type TheoryCardContent =
    | TheoryTextCard
    | TheoryDialogueCard
    | TheoryBulletsCard
    | TheoryQuoteCard;

export function emptyTheoryCard(): TheoryCardContent {
    return { layout: "text", title: "", body: "" };
}
