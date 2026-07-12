// Canonical JSON import templates for every admin content entity.
//
// Single source of truth so every "Download template" button across the admin
// panel produces a consistent, valid, copy-pasteable example. Per-entity import
// endpoints take the flat arrays; the bundle endpoint takes the nested tree.
//
// Field names mirror the backend import DTOs exactly (camelCase request fields;
// the exercise `content` payload itself stays snake_case — see
// exercise-editors/types.ts).

import { EXERCISE_CONTENT_TEMPLATES } from "@/features/admin/components/exercise-editors";

// A theory lesson is a lesson whose exercises are ALL `theory_card`. The learner
// swipes through the cards stories-style (no answer, no AI); reaching the last card
// completes the lesson. Cards may use any layout — text / dialogue / bullets / quote.
// This array is reused by both the lessons and bundle templates below.
const THEORY_LESSON_EXERCISES = [
    {
        type: "theory_card",
        orderInLesson: 1,
        content: { layout: "text", title: "Why research matters", body: "Before the call, gather at least basic information about the prospect.\nThis builds trust and saves time." },
    },
    {
        type: "theory_card",
        orderInLesson: 2,
        content: { layout: "bullets", title: "What to find out beforehand", items: ["Industry and company size", "Who the decision-maker is", "The problem we can solve"] },
    },
    {
        type: "theory_card",
        orderInLesson: 3,
        content: {
            layout: "dialogue",
            title: "Clarifying question instead of a discount",
            turns: [
                { side: "them", text: "This is too expensive." },
                { side: "me", text: "Expensive compared to what?", annotations: ["clarification"] },
                { side: "them", text: "Competitors are cheaper." },
            ],
        },
    },
    {
        type: "theory_card",
        orderInLesson: 4,
        content: { layout: "quote", text: "People buy from those they trust.", author: "Zig Ziglar" },
    },
];

/** `POST /admin/seeder/skills` — flat array of skills. */
export const SKILLS_TEMPLATE = [
    {
        iconicName: "cold-calling",
        title: "Cold Calling",
        description: "Mastering outbound cold calls",
        orderInTree: 1,
        stage: "preparation",
    },
    {
        iconicName: "objection-handling",
        title: "Objection Handling",
        description: "Techniques for common objections",
        orderInTree: 2,
        stage: "closing",
    },
];

/** `POST /admin/seeder/topics` — flat array; `skillIconicName` must already exist. */
export const TOPICS_TEMPLATE = [
    {
        skillIconicName: "cold-calling",
        iconicName: "cold-calling-basics",
        title: "Basics",
        orderInSkill: 1,
    },
    {
        skillIconicName: "cold-calling",
        iconicName: "cold-calling-openers",
        title: "Strong Openers",
        orderInSkill: 2,
    },
];

/** `POST /admin/seeder/lessons` — flat array of lessons with nested exercises; `topicIconicName` must already exist. */
export const LESSONS_TEMPLATE = [
    {
        topicIconicName: "cold-calling-basics",
        title: "Opening the call",
        orderInTopic: 1,
        exercises: [
            { type: "choose_option", orderInLesson: 1, content: EXERCISE_CONTENT_TEMPLATES.choose_option },
            { type: "free_text", orderInLesson: 2, content: EXERCISE_CONTENT_TEMPLATES.free_text, customAiPrompt: null },
        ],
    },
    {
        // Theory lesson: every exercise is a theory_card → played as swipeable story cards.
        topicIconicName: "cold-calling-basics",
        title: "Theory: preparing for the call",
        orderInTopic: 2,
        exercises: THEORY_LESSON_EXERCISES,
    },
];

/**
 * `POST /admin/techniques/import` — flat array of techniques (the same write body
 * used by create/update, so an export re-imports verbatim). `primarySkillId` /
 * `additionalSkillIds` are skill GUIDs; leave them `null` / `[]` here since a fresh
 * template has no real ids — set them in the UI after import. `dialog`, `case` and
 * `coach` are optional (use `null` to omit).
 */
export const TECHNIQUES_TEMPLATE = [
    {
        slug: "mirror-and-label",
        name: "Mirror & Label",
        summary: "Repeat the last few words, then name the emotion to build rapport.",
        body: "## When to use\nUse mirroring when the prospect goes quiet or guarded.\n\n1. Repeat their last 1-3 words as a question.\n2. Label the feeling: \"It sounds like…\".",
        tags: ["discovery", "rapport"],
        primarySkillId: null,
        additionalSkillIds: [],
        difficulty: 2,
        sortOrder: 1,
        dialog: [
            { orderIndex: 1, side: "them", text: "We already have a vendor.", annotations: [] },
            { orderIndex: 2, side: "me", text: "You already have a vendor?", annotations: ["mirror"] },
            { orderIndex: 3, side: "them", text: "Yeah, but honestly they're slow to respond." },
        ],
        case: {
            title: "Reviving a stalled deal",
            body: "A rep used a single label — \"It seems like timing is the real blocker\" — and the prospect opened up about a budget freeze.",
            metrics: [{ label: "Reply rate", value: "+38%" }],
        },
        coach: {
            avatarSeed: "mirror-coach",
            name: "Dana Cross",
            role: "Negotiation coach",
            quote: "Silence after a label is where the truth comes out.",
            challenges: [
                { label: "Practice 3 mirrors in a live call", kind: "dialog", targetSlug: null },
            ],
        },
    },
    {
        slug: "feel-felt-found",
        name: "Feel · Felt · Found",
        summary: "Acknowledge the objection, relate, then reframe with a proof point.",
        body: "Structure your reply as: I understand how you **feel** → others **felt** the same → here's what they **found**.",
        tags: ["objection-handling"],
        primarySkillId: null,
        additionalSkillIds: [],
        difficulty: 1,
        sortOrder: 2,
        dialog: null,
        case: null,
        coach: null,
    },
];

/**
 * `POST /admin/dialog/import` — dialog bundles with nested modes in one file.
 * `skillIconicName` must already exist. Upsert: bundles by (skill, title), modes
 * by (bundle, key). Re-importing the same file is safe.
 */
export const DIALOG_TEMPLATE = {
    bundles: [
        {
            skillIconicName: "cold-calling",
            title: "Cold call simulator",
            description: "Practice live cold calls with an AI customer",
            iconEmoji: "📞",
            sortOrder: 1,
            isActive: true,
            modes: [
                {
                    key: "secretary-bypass",
                    title: "Get past the gatekeeper",
                    description: "The AI plays a protective secretary",
                    chatSystemPrompt: "You are Marina, a secretary screening cold calls. Be polite but resistant; only put the seller through if they give a compelling, specific reason.",
                    feedbackSystemPrompt: "Evaluate how well the seller handled the gatekeeper: did they stay confident, give a concrete reason, and avoid sounding scripted? End with [XP:N] where N is 0-100.",
                    sortOrder: 1,
                    isActive: true,
                    voiceEnabled: false,
                    voiceId: null,
                },
            ],
        },
    ],
};

/**
 * `POST /admin/seeder/bundle` — an entire content tree in one file.
 * Skill → topics → lessons → exercises. Everything is upserted idempotently
 * (skills/topics by iconicName, lessons by title-within-topic, exercises by
 * orderInLesson-within-lesson), so re-importing the same file is safe.
 */
export const BUNDLE_TEMPLATE = {
    skills: [
        {
            iconicName: "cold-calling",
            title: "Cold Calling",
            description: "Mastering outbound cold calls",
            orderInTree: 1,
            stage: "preparation",
            topics: [
                {
                    iconicName: "cold-calling-basics",
                    title: "Basics",
                    orderInSkill: 1,
                    lessons: [
                        {
                            title: "Opening the call",
                            orderInTopic: 1,
                            exercises: [
                                { type: "choose_option", orderInLesson: 1, content: EXERCISE_CONTENT_TEMPLATES.choose_option },
                                { type: "reorder", orderInLesson: 2, content: EXERCISE_CONTENT_TEMPLATES.reorder },
                                { type: "free_text", orderInLesson: 3, content: EXERCISE_CONTENT_TEMPLATES.free_text, customAiPrompt: null },
                            ],
                        },
                        {
                            title: "Handling the gatekeeper",
                            orderInTopic: 2,
                            exercises: [
                                { type: "spot_mistake", orderInLesson: 1, content: EXERCISE_CONTENT_TEMPLATES.spot_mistake },
                            ],
                        },
                        {
                            // Theory lesson: all theory_card → swipeable story cards.
                            title: "Theory: preparing for the call",
                            orderInTopic: 3,
                            exercises: THEORY_LESSON_EXERCISES,
                        },
                    ],
                },
            ],
        },
    ],
};
