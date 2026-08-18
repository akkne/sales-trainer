"use client";

import { useMemo, useState } from "react";
import { Button } from "@/shared/components/button";
import { Card, CardContent } from "@/shared/components/card";
import { ConfirmDialog } from "@/shared/components/confirm-dialog";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { Skeleton } from "@/shared/components/skeleton";
import {
    DRAFT_FAILED_MESSAGE,
    ENROLLMENT_TABLE_TITLE,
    ENROLL_BUTTON_LABEL,
    ENROLL_HINT,
    LOAD_FAILED_MESSAGE,
    LOAD_FAILED_TITLE,
    NO_BULK_SWITCH_EXPLANATION,
    NO_VERSIONS_DESCRIPTION,
    NO_VERSIONS_TITLE,
    PIN_NOT_YET_ON_LEARNER_SCREENS_NOTE,
    PROGRAM_PAGE_SUBTITLE,
    PROGRAM_PAGE_TITLE,
    PUBLISH_CONFIRM_BODY,
    PUBLISH_CONFIRM_TITLE,
    PUBLISH_BUTTON_LABEL,
    PUBLISH_NO_CHANGES_MESSAGE,
    REBUILD_DRAFT_BUTTON_LABEL,
} from "@/features/org-program/constants/program-dictionary";
import { EnrollPeopleDialog } from "@/features/org-program/components/enroll-people-dialog";
import { EnrollmentSpreadSummary } from "@/features/org-program/components/enrollment-spread-summary";
import { EnrollmentTable } from "@/features/org-program/components/enrollment-table";
import { ProgramDiffDialog } from "@/features/org-program/components/program-diff-dialog";
import { VersionHistory } from "@/features/org-program/components/version-history";
import { VersionItemsDialog } from "@/features/org-program/components/version-items-dialog";
import {
    describePublishFailure,
    describeProgramWriteFailure,
    useEnsureProgramDraft,
    useProgramEnrollments,
    useProgramRoster,
    useProgramVersions,
    usePublishProgramVersion,
} from "@/features/org-program/hooks/use-program";
import { formatVersionLabel } from "@/features/org-program/lib/format-program-text";
import {
    buildMemberNameLookup,
    selectCurrentPublishedVersion,
    selectDraftVersion,
    selectEnrollableMembers,
    summarizeEnrollmentSpread,
} from "@/features/org-program/lib/program-versions";
import type { ProgramVersionSummary } from "@/features/org-program/types/program";

interface DiffRequest {
    title: string;
    targetProgramVersionId: string;
    baselineProgramVersionId: string;
}

/**
 * O18 «Программа обучения» (docs/TENANCY/ADMIN_UI_DESIGN.md → O18, docs/TENANCY/CONTENT_MODEL.md
 * §2.5). The screen that turns 40.17's pin into something a РОП can see: which version is current,
 * who is on which one, and what a move would change.
 *
 * **There is no control on this page that moves another person's pin, and there must never be one.**
 * The guarantee «программу под учащимся никто не переставит» is a property of the API surface — no
 * such route exists — and a button here would immediately turn it into a question of what the panel
 * chose to draw (docs/TENANCY/ADMIN_UI_DESIGN.md §7, docs/DONT_FORGET.md → блок 40.17).
 */
export default function OrganizationProgramPage() {
    const versionsQuery = useProgramVersions();
    const enrollmentsQuery = useProgramEnrollments();
    const rosterQuery = useProgramRoster();

    const ensureDraftMutation = useEnsureProgramDraft();
    const publishMutation = usePublishProgramVersion();

    const [viewedVersion, setViewedVersion] = useState<ProgramVersionSummary | null>(null);
    const [diffRequest, setDiffRequest] = useState<DiffRequest | null>(null);
    const [isPublishConfirmOpen, setIsPublishConfirmOpen] = useState(false);
    const [isEnrollDialogOpen, setIsEnrollDialogOpen] = useState(false);
    const [publishNotice, setPublishNotice] = useState<string | null>(null);
    const [draftFailureMessage, setDraftFailureMessage] = useState<string | null>(null);

    const versions = useMemo(() => versionsQuery.data ?? [], [versionsQuery.data]);
    const enrollments = useMemo(() => enrollmentsQuery.data ?? [], [enrollmentsQuery.data]);
    const rosterMembers = useMemo(() => rosterQuery.data ?? [], [rosterQuery.data]);

    const draftVersion = useMemo(() => selectDraftVersion(versions), [versions]);
    const currentPublishedVersion = useMemo(
        () => selectCurrentPublishedVersion(versions),
        [versions]
    );

    const spread = useMemo(
        () =>
            summarizeEnrollmentSpread({
                enrollments,
                currentPublishedVersion,
                rosterMembers,
            }),
        [enrollments, currentPublishedVersion, rosterMembers]
    );

    const memberNamesByUserId = useMemo(() => buildMemberNameLookup(rosterMembers), [rosterMembers]);
    const enrollableMembers = useMemo(
        () => selectEnrollableMembers(rosterMembers, enrollments),
        [rosterMembers, enrollments]
    );

    const rebuildDraft = () => {
        setDraftFailureMessage(null);
        setPublishNotice(null);
        ensureDraftMutation.mutate(undefined, {
            onError: (error) =>
                setDraftFailureMessage(describeProgramWriteFailure(error, DRAFT_FAILED_MESSAGE)),
        });
    };

    const confirmPublish = () => {
        setPublishNotice(null);
        publishMutation.mutate(undefined, {
            onSuccess: (result) => {
                setIsPublishConfirmOpen(false);
                setPublishNotice(
                    result.createdNewVersion
                        ? `Опубликована ${formatVersionLabel(result.version.versionNumber)}. Никто из тех, кто уже учится, не сдвинулся.`
                        : PUBLISH_NO_CHANGES_MESSAGE
                );
            },
            onError: (error) => {
                setIsPublishConfirmOpen(false);
                setPublishNotice(describePublishFailure(error));
            },
        });
    };

    if (versionsQuery.isLoading || enrollmentsQuery.isLoading) {
        return (
            <>
                <PageHeader title={PROGRAM_PAGE_TITLE} subtitle={PROGRAM_PAGE_SUBTITLE} />
                <div className="flex flex-col gap-4">
                    <Skeleton height={140} rounded={16} />
                    <Skeleton height={220} rounded={16} />
                </div>
            </>
        );
    }

    if (versionsQuery.isError || enrollmentsQuery.isError) {
        return (
            <>
                <PageHeader title={PROGRAM_PAGE_TITLE} subtitle={PROGRAM_PAGE_SUBTITLE} />
                <ErrorState
                    title={LOAD_FAILED_TITLE}
                    message={LOAD_FAILED_MESSAGE}
                    onRetry={() => {
                        versionsQuery.refetch();
                        enrollmentsQuery.refetch();
                    }}
                />
            </>
        );
    }

    return (
        <>
            <PageHeader title={PROGRAM_PAGE_TITLE} subtitle={PROGRAM_PAGE_SUBTITLE} />

            {draftFailureMessage && (
                <p className="text-sm mb-4" style={{ color: "var(--bad)" }} role="alert">
                    {draftFailureMessage}
                </p>
            )}

            {publishNotice && (
                <p className="text-sm mb-4 text-ink-2" role="status">
                    {publishNotice}
                </p>
            )}

            {versions.length === 0 ? (
                <Card className="mb-6">
                    <CardContent style={{ marginTop: 0 }}>
                        <EmptyState
                            icon="book"
                            title={NO_VERSIONS_TITLE}
                            description={NO_VERSIONS_DESCRIPTION}
                            action={
                                <Button
                                    variant="primary"
                                    onClick={rebuildDraft}
                                    loading={ensureDraftMutation.isPending}
                                >
                                    {REBUILD_DRAFT_BUTTON_LABEL}
                                </Button>
                            }
                        />
                    </CardContent>
                </Card>
            ) : (
                <Card className="mb-6">
                    <CardContent style={{ marginTop: 0 }}>
                        <VersionHistory
                            versions={versions}
                            draftVersion={draftVersion}
                            onViewVersion={setViewedVersion}
                            onShowDiff={(version, baseline) =>
                                setDiffRequest({
                                    title: `Что изменилось: ${formatVersionLabel(baseline.versionNumber)} → ${formatVersionLabel(version.versionNumber)}`,
                                    targetProgramVersionId: version.id,
                                    baselineProgramVersionId: baseline.id,
                                })
                            }
                            onRebuildDraft={rebuildDraft}
                            onPublish={() => setIsPublishConfirmOpen(true)}
                            isRebuildingDraft={ensureDraftMutation.isPending}
                            isPublishing={publishMutation.isPending}
                        />
                    </CardContent>
                </Card>
            )}

            <div className="flex items-center justify-between gap-3 mb-3 flex-wrap">
                <h2 className="text-xs font-medium text-ink-3 uppercase tracking-wide">
                    {ENROLLMENT_TABLE_TITLE}
                </h2>
                {currentPublishedVersion && (
                    <div className="flex items-center gap-3 flex-wrap">
                        <span className="text-xs text-ink-3">{ENROLL_HINT}</span>
                        <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setIsEnrollDialogOpen(true)}
                        >
                            {ENROLL_BUTTON_LABEL}
                        </Button>
                    </div>
                )}
            </div>

            {currentPublishedVersion && (
                <Card className="mb-4">
                    <CardContent style={{ marginTop: 0 }}>
                        <EnrollmentSpreadSummary
                            spread={spread}
                            currentPublishedVersion={currentPublishedVersion}
                            rosterState={
                                rosterQuery.isError
                                    ? "unavailable"
                                    : rosterQuery.isSuccess
                                      ? "ready"
                                      : "loading"
                            }
                            
                        />
                    </CardContent>
                </Card>
            )}

            <EnrollmentTable
                enrollments={enrollments}
                currentPublishedVersion={currentPublishedVersion}
                memberNamesByUserId={memberNamesByUserId}
                isLoading={enrollmentsQuery.isFetching && enrollments.length === 0}
                onShowPendingDiff={(enrollment) => {
                    if (!currentPublishedVersion) return;
                    setDiffRequest({
                        title: `Что изменится: ${formatVersionLabel(enrollment.programVersionNumber)} → ${formatVersionLabel(currentPublishedVersion.versionNumber)}`,
                        targetProgramVersionId: currentPublishedVersion.id,
                        baselineProgramVersionId: enrollment.programVersionId,
                    });
                }}
            />

            <p className="text-sm text-ink-2 mt-6">{NO_BULK_SWITCH_EXPLANATION}</p>
            <p className="text-xs text-ink-3 mt-3">{PIN_NOT_YET_ON_LEARNER_SCREENS_NOTE}</p>

            <VersionItemsDialog
                open={viewedVersion !== null}
                title={
                    viewedVersion
                        ? `${formatVersionLabel(viewedVersion.versionNumber)} · ${viewedVersion.status === "draft" ? "черновик" : "опубликована"}`
                        : ""
                }
                programVersionId={viewedVersion?.id ?? null}
                onClose={() => setViewedVersion(null)}
            />

            <ProgramDiffDialog
                open={diffRequest !== null}
                title={diffRequest?.title ?? ""}
                targetProgramVersionId={diffRequest?.targetProgramVersionId ?? null}
                baselineProgramVersionId={diffRequest?.baselineProgramVersionId ?? null}
                onClose={() => setDiffRequest(null)}
            />

            <ConfirmDialog
                open={isPublishConfirmOpen}
                title={PUBLISH_CONFIRM_TITLE}
                body={PUBLISH_CONFIRM_BODY}
                confirmLabel={PUBLISH_BUTTON_LABEL}
                onConfirm={confirmPublish}
                onCancel={() => setIsPublishConfirmOpen(false)}
                isPending={publishMutation.isPending}
            />

            {currentPublishedVersion && (
                <EnrollPeopleDialog
                    open={isEnrollDialogOpen}
                    enrollableMembers={enrollableMembers}
                    currentPublishedVersion={currentPublishedVersion}
                    isRosterLoading={rosterQuery.isLoading}
                    isRosterKnown={!rosterQuery.isError}
                    onClose={() => setIsEnrollDialogOpen(false)}
                />
            )}
        </>
    );
}
