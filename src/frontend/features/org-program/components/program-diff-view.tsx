"use client";

import type { ReactNode } from "react";
import { Icon } from "@/shared/components/icon";
import {
    DIFF_BREAKING_WARNING,
    DIFF_BUCKET_LABELS,
    DIFF_EMPTY_MESSAGE,
    UNKNOWN_LESSON_TITLE,
} from "../constants/program-dictionary";
import { formatVersionLabel } from "../lib/format-program-text";
import type { ProgramDiff } from "../types/program";

interface ProgramDiffViewProps {
    diff: ProgramDiff;
}

function describeLessonTitle(lessonTitle: string | null): string {
    return lessonTitle ?? UNKNOWN_LESSON_TITLE;
}

function describeLessonVersion(lessonVersionNumber: number | null): string {
    return lessonVersionNumber === null ? "—" : `v${lessonVersionNumber}`;
}

interface DiffSectionProps {
    label: string;
    count: number;
    children: ReactNode;
}

function DiffSection({ label, count, children }: DiffSectionProps) {
    if (count === 0) return null;

    return (
        <section className="mb-5">
            <h3 className="text-xs font-medium text-ink-3 uppercase tracking-wide mb-2">
                {label} · <span className="tnum" style={{ fontFamily: "var(--font-mono)" }}>{count}</span>
            </h3>
            <ul className="flex flex-col gap-1">{children}</ul>
        </section>
    );
}

function DiffRow({ children }: { children: ReactNode }) {
    return (
        <li
            className="flex items-baseline justify-between gap-3 rounded-xl px-3 py-2 text-sm"
            style={{ background: "var(--bg-2)" }}
        >
            {children}
        </li>
    );
}

/**
 * The four buckets of `ProgramDiffDto`, as four sections rather than one merged list
 * (docs/TENANCY/ADMIN_UI_DESIGN.md O18). They answer four different questions, and a lesson that
 * merely moved must not read like a lesson whose content changed — that distinction is the whole
 * reason the backend splits them.
 *
 * Nothing here is computed: the client does not diff programmes (§7).
 */
export function ProgramDiffView({ diff }: ProgramDiffViewProps) {
    const totalChangeCount =
        diff.addedLessons.length +
        diff.removedLessons.length +
        diff.changedLessons.length +
        diff.movedLessons.length;

    return (
        <div>
            <p className="text-sm text-ink-2 mb-4">
                Что изменится при переходе с {formatVersionLabel(diff.fromVersionNumber)} на{" "}
                {formatVersionLabel(diff.toVersionNumber)}.
            </p>

            {diff.hasBreakingChanges && (
                <div
                    role="status"
                    className="flex items-start gap-3 rounded-2xl p-3 mb-5"
                    style={{ background: "var(--bad-soft)" }}
                >
                    <Icon
                        name="warning"
                        size="md"
                        style={{ color: "var(--bad)", flexShrink: 0, marginTop: 2 }}
                    />
                    <p className="text-sm" style={{ color: "var(--bad)" }}>
                        {DIFF_BREAKING_WARNING}
                    </p>
                </div>
            )}

            {totalChangeCount === 0 && <p className="text-sm text-ink-3">{DIFF_EMPTY_MESSAGE}</p>}

            <DiffSection label={DIFF_BUCKET_LABELS.added} count={diff.addedLessons.length}>
                {diff.addedLessons.map((lesson) => (
                    <DiffRow key={`added-${lesson.lessonId}`}>
                        <span className="text-ink">{describeLessonTitle(lesson.lessonTitle)}</span>
                        <span className="text-ink-3 tnum" style={{ fontFamily: "var(--font-mono)" }}>
                            {describeLessonVersion(lesson.lessonVersionNumber)}
                        </span>
                    </DiffRow>
                ))}
            </DiffSection>

            <DiffSection label={DIFF_BUCKET_LABELS.removed} count={diff.removedLessons.length}>
                {diff.removedLessons.map((lesson) => (
                    <DiffRow key={`removed-${lesson.lessonId}`}>
                        <span className="text-ink">{describeLessonTitle(lesson.lessonTitle)}</span>
                        <span className="text-ink-3 tnum" style={{ fontFamily: "var(--font-mono)" }}>
                            {describeLessonVersion(lesson.lessonVersionNumber)}
                        </span>
                    </DiffRow>
                ))}
            </DiffSection>

            <DiffSection label={DIFF_BUCKET_LABELS.changed} count={diff.changedLessons.length}>
                {diff.changedLessons.map((change) => (
                    <DiffRow key={`changed-${change.lessonId}`}>
                        <span className="text-ink">
                            {describeLessonTitle(change.lessonTitle)}
                            {change.isBreaking && (
                                <span className="ml-2 text-xs" style={{ color: "var(--bad)" }}>
                                    изменился ответ или критерии
                                </span>
                            )}
                        </span>
                        <span className="text-ink-3 tnum" style={{ fontFamily: "var(--font-mono)" }}>
                            {describeLessonVersion(change.fromLessonVersionNumber)} →{" "}
                            {describeLessonVersion(change.toLessonVersionNumber)}
                        </span>
                    </DiffRow>
                ))}
            </DiffSection>

            <DiffSection label={DIFF_BUCKET_LABELS.moved} count={diff.movedLessons.length}>
                {diff.movedLessons.map((move) => (
                    <DiffRow key={`moved-${move.lessonId}`}>
                        <span className="text-ink">{describeLessonTitle(move.lessonTitle)}</span>
                        <span className="text-ink-3 tnum" style={{ fontFamily: "var(--font-mono)" }}>
                            {move.fromOrderIndex + 1} → {move.toOrderIndex + 1}
                        </span>
                    </DiffRow>
                ))}
            </DiffSection>

            {diff.movedLessons.length > 0 && (
                <p className="text-xs text-ink-3">
                    Переставленный урок остался на той же версии: содержимое не менялось, изменился порядок.
                </p>
            )}
        </div>
    );
}
