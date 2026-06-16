"use client";

import { ImportPanel } from "@/features/admin/components/import-panel";
import { BUNDLE_TEMPLATE } from "@/features/admin/lib/import-templates";
import { useImportBundle } from "@/features/admin/hooks/use-admin";
import {
    EXERCISE_TYPES,
    validateExerciseContent,
    type ExerciseType,
} from "@/features/admin/components/exercise-editors";

/**
 * Client-side validation of a bundle file before upload. Mirrors the backend
 * shape checks so an admin sees problems (and a path to each) without a round
 * trip. Returns a flat list of human-readable problems; empty == valid.
 */
function validateBundle(parsed: unknown): string[] {
    const errors: string[] = [];

    const root = parsed as Record<string, unknown> | unknown[];
    const skills = Array.isArray(root) ? root : (root as Record<string, unknown>)?.skills;
    if (!Array.isArray(skills)) {
        return ['Root must be an object { "skills": [...] } or an array of skills.'];
    }

    skills.forEach((skill, si) => {
        const s = skill as Record<string, unknown>;
        const skillName = typeof s?.iconicName === "string" ? s.iconicName : `#${si + 1}`;
        if (typeof s?.iconicName !== "string" || !s.iconicName.trim()) errors.push(`Skill ${skillName}: iconicName is required.`);
        if (typeof s?.title !== "string" || !s.title.trim()) errors.push(`Skill ${skillName}: title is required.`);
        if (typeof s?.orderInTree !== "number") errors.push(`Skill ${skillName}: orderInTree must be a number.`);

        const topics = s?.topics;
        if (topics !== undefined && !Array.isArray(topics)) {
            errors.push(`Skill ${skillName}: topics must be an array.`);
            return;
        }
        (Array.isArray(topics) ? topics : []).forEach((topic, ti) => {
            const t = topic as Record<string, unknown>;
            const topicName = typeof t?.iconicName === "string" ? t.iconicName : `#${ti + 1}`;
            if (typeof t?.iconicName !== "string" || !t.iconicName.trim()) errors.push(`Skill ${skillName} › topic ${topicName}: iconicName is required.`);
            if (typeof t?.title !== "string" || !t.title.trim()) errors.push(`Skill ${skillName} › topic ${topicName}: title is required.`);
            if (typeof t?.orderInSkill !== "number") errors.push(`Skill ${skillName} › topic ${topicName}: orderInSkill must be a number.`);

            const lessons = t?.lessons;
            if (lessons !== undefined && !Array.isArray(lessons)) {
                errors.push(`Topic ${topicName}: lessons must be an array.`);
                return;
            }
            (Array.isArray(lessons) ? lessons : []).forEach((lesson, li) => {
                const l = lesson as Record<string, unknown>;
                const lessonName = typeof l?.title === "string" ? l.title : `#${li + 1}`;
                if (typeof l?.title !== "string" || !l.title.trim()) errors.push(`Topic ${topicName} › lesson #${li + 1}: title is required.`);
                if (typeof l?.orderInTopic !== "number") errors.push(`Topic ${topicName} › lesson ${lessonName}: orderInTopic must be a number.`);

                const exercises = l?.exercises;
                if (exercises !== undefined && !Array.isArray(exercises)) {
                    errors.push(`Lesson ${lessonName}: exercises must be an array.`);
                    return;
                }
                (Array.isArray(exercises) ? exercises : []).forEach((exercise, ei) => {
                    const e = exercise as Record<string, unknown>;
                    const type = e?.type as ExerciseType;
                    if (!EXERCISE_TYPES.includes(type)) {
                        errors.push(`Lesson ${lessonName} › exercise #${ei + 1}: unknown type "${String(e?.type)}".`);
                        return;
                    }
                    if (typeof e?.orderInLesson !== "number") errors.push(`Lesson ${lessonName} › exercise #${ei + 1}: orderInLesson must be a number.`);
                    const contentProblems = validateExerciseContent(type, e?.content);
                    contentProblems.forEach((p) => errors.push(`Lesson ${lessonName} › exercise #${ei + 1} (${type}): ${p}`));
                });
            });
        });
    });

    return errors;
}

export default function AdminBundleImportPage() {
    const importBundle = useImportBundle();

    return (
        <div className="max-w-3xl">
            <div className="mb-6">
                <h1 className="text-xl font-semibold text-ink">Bundle Import</h1>
                <p className="text-sm text-ink-3 mt-1">
                    Import an entire content tree from a single JSON file:{" "}
                    <strong>skill → topics → lessons → exercises</strong>. Everything is
                    upserted idempotently (skills/topics by <code className="font-mono text-xs">iconicName</code>,
                    lessons by title within their topic, exercises by{" "}
                    <code className="font-mono text-xs">orderInLesson</code> within their lesson),
                    so re-importing the same file is safe. Exercise content is validated per type
                    before anything is written.
                </p>
            </div>

            <ImportPanel
                title="Import full skill tree"
                description='JSON: { "skills": [{ ..., "topics": [{ ..., "lessons": [{ ..., "exercises": [...] }] }] }] }'
                templateData={BUNDLE_TEMPLATE}
                templateFilename="content_bundle_template.json"
                validate={validateBundle}
                onImport={async ({ text }) => {
                    const file = new File([text], "bundle.json", { type: "application/json" });
                    const result = await importBundle.mutateAsync(file);
                    return {
                        created:
                            result.skillsCreated + result.topicsCreated +
                            result.lessonsCreated + result.exercisesCreated,
                        updated:
                            result.skillsUpdated + result.topicsUpdated +
                            result.lessonsUpdated + result.exercisesUpdated,
                        errors: result.errors,
                        detail:
                            `Skills ${result.skillsCreated}/${result.skillsUpdated} · ` +
                            `Topics ${result.topicsCreated}/${result.topicsUpdated} · ` +
                            `Lessons ${result.lessonsCreated}/${result.lessonsUpdated} · ` +
                            `Exercises ${result.exercisesCreated}/${result.exercisesUpdated} (created/updated)`,
                    };
                }}
            />
        </div>
    );
}
