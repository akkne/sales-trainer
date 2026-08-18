"use client";

import { Button } from "@/shared/components/button";
import { Chip } from "@/shared/components/chip";
import {
    DIFF_BUTTON_LABEL,
    PUBLISH_BUTTON_LABEL,
    REBUILD_DRAFT_BUTTON_LABEL,
    VIEW_VERSION_BUTTON_LABEL,
    describeProgramVersionStatus,
    resolveProgramVersionStatusTone,
} from "../constants/program-dictionary";
import {
    describeLessonCount,
    describePersonCount,
    formatProgramDate,
    formatVersionLabel,
} from "../lib/format-program-text";
import { selectPreviousPublishedVersion } from "../lib/program-versions";
import type { ProgramVersionSummary } from "../types/program";

interface VersionHistoryProps {
    versions: ProgramVersionSummary[];
    draftVersion: ProgramVersionSummary | null;
    onViewVersion: (version: ProgramVersionSummary) => void;
    onShowDiff: (version: ProgramVersionSummary, baseline: ProgramVersionSummary) => void;
    onRebuildDraft: () => void;
    onPublish: () => void;
    isRebuildingDraft: boolean;
    isPublishing: boolean;
}

function describePublishedLine(version: ProgramVersionSummary): string {
    const publishedOn = formatProgramDate(version.publishedAt);
    const publishedPart = publishedOn ? `опубликована ${publishedOn}` : "опубликована";
    return `${publishedPart} · ${describeLessonCount(version.itemCount)} · зачислено ${version.enrollmentCount}`;
}

/**
 * The version list of O18: every published snapshot newest first, then the single editable draft.
 *
 * «Что изменилось» appears only where a baseline exists — the first published version has no
 * predecessor to compare against, and a disabled button that explains itself in a tooltip would be
 * a worse answer than no button.
 */
export function VersionHistory({
    versions,
    draftVersion,
    onViewVersion,
    onShowDiff,
    onRebuildDraft,
    onPublish,
    isRebuildingDraft,
    isPublishing,
}: VersionHistoryProps) {
    const publishedVersions = versions.filter((version) => version.status === "published");

    return (
        <div className="flex flex-col gap-2">
            {publishedVersions.map((version) => {
                const baseline = selectPreviousPublishedVersion(versions, version.id);

                return (
                    <div
                        key={version.id}
                        className="flex flex-wrap items-center justify-between gap-3 rounded-2xl px-4 py-3"
                        style={{ background: "var(--bg-2)" }}
                    >
                        <div className="flex items-baseline gap-3 flex-wrap">
                            <span
                                className="font-medium text-ink tnum"
                                style={{ fontFamily: "var(--font-mono)" }}
                            >
                                {formatVersionLabel(version.versionNumber)}
                            </span>
                            <span className="text-sm text-ink-2">{describePublishedLine(version)}</span>
                        </div>
                        <div className="flex items-center gap-2">
                            <Button size="sm" variant="ghost" onClick={() => onViewVersion(version)}>
                                {VIEW_VERSION_BUTTON_LABEL}
                            </Button>
                            {baseline && (
                                <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() => onShowDiff(version, baseline)}
                                >
                                    {DIFF_BUTTON_LABEL}
                                </Button>
                            )}
                        </div>
                    </div>
                );
            })}

            {draftVersion ? (
                <div
                    className="flex flex-wrap items-center justify-between gap-3 rounded-2xl px-4 py-3"
                    style={{ background: "var(--bg-2)", border: "1px dashed var(--line-2)" }}
                >
                    <div className="flex items-baseline gap-3 flex-wrap">
                        <Chip
                            size="sm"
                            tone={resolveProgramVersionStatusTone(draftVersion.status)}
                        >
                            {describeProgramVersionStatus(draftVersion.status)}
                        </Chip>
                        <span className="text-sm text-ink-2">
                            {describeLessonCount(draftVersion.itemCount)} · собран{" "}
                            {formatProgramDate(draftVersion.createdAt)}
                        </span>
                    </div>
                    <div className="flex items-center gap-2 flex-wrap">
                        <Button size="sm" variant="ghost" onClick={() => onViewVersion(draftVersion)}>
                            {VIEW_VERSION_BUTTON_LABEL}
                        </Button>
                        <Button
                            size="sm"
                            variant="outline"
                            onClick={onRebuildDraft}
                            loading={isRebuildingDraft}
                        >
                            {REBUILD_DRAFT_BUTTON_LABEL}
                        </Button>
                        <Button size="sm" variant="primary" onClick={onPublish} loading={isPublishing}>
                            {PUBLISH_BUTTON_LABEL}
                        </Button>
                    </div>
                </div>
            ) : (
                <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                    <p className="text-sm text-ink-3">
                        Черновика нет. Соберите его из дерева навыков, чтобы подготовить следующую версию.
                    </p>
                    <Button
                        size="sm"
                        variant="outline"
                        onClick={onRebuildDraft}
                        loading={isRebuildingDraft}
                    >
                        {REBUILD_DRAFT_BUTTON_LABEL}
                    </Button>
                </div>
            )}

            <p className="text-xs text-ink-3 px-4">
                Опубликованная версия заморожена навсегда: ни порядок, ни версии уроков в ней больше не
                меняются. Следующие правки собираются в новый черновик.
            </p>
            {publishedVersions.some((version) => version.enrollmentCount > 0) && (
                <p className="text-xs text-ink-3 px-4">
                    «Зачислено» — сколько человек прямо сейчас учатся по этой версии, а не сколько
                    когда-либо на неё попали:{" "}
                    {describePersonCount(
                        publishedVersions.reduce(
                            (total, version) => total + version.enrollmentCount,
                            0
                        )
                    )}{" "}
                    всего.
                </p>
            )}
        </div>
    );
}
