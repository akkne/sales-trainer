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
        stage: "active",
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
];

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
                    ],
                },
            ],
        },
    ],
};
