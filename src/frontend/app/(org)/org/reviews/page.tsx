"use client";

import { Suspense, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { SkeletonList } from "@/shared/components/skeleton";
import { Tabs } from "@/shared/components/tabs";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useTeamMemberNames } from "@/features/org-shell/hooks/use-team-directory";
import { ReviewNoteCard } from "@/features/org-dialogs/components/review-note-card";
import { useOrganizationReviewNotes } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import { buildMemberNamesByUserId } from "@/features/org-dialogs/lib/team-member-labels";
import {
    buildVisibleReviewNotes,
    countOpenDisputes,
    DEFAULT_REVIEW_QUEUE_KEY,
    REVIEW_QUEUE_LABELS,
    type ReviewQueueKey,
} from "@/features/org-dialogs/lib/review-queues";

const SKELETON_ROW_COUNT = 3;

const EMPTY_QUEUE_COPY: Record<ReviewQueueKey, { title: string; description: string }> = {
    open: {
        title: "Открытых споров нет",
        description:
            "Менеджер может оспорить оценку из разбора диалога — тогда спор появится здесь и будет ждать вашего решения.",
    },
    disputes: {
        title: "Оценки ещё никто не оспаривал",
        description:
            "Это нормально для новой команды. Возможность оспорить оценку есть у каждого менеджера в разборе разговора.",
    },
    notes: {
        title: "Вы ещё не отправляли заметок",
        description:
            "Откройте разговор в разделе «Разговоры», выделите реплики и отправьте их менеджеру с комментарием.",
    },
};

/**
 * O7 «Спорные оценки» (docs/TENANCY/ADMIN_UI_DESIGN.md O7).
 *
 * This is the second of the two live notification links block 40.20 has to rescue: 40.26 mints
 * `/admin/dialog-reviews?note={noteId}`, slice 0 redirects it here with the query intact, and the
 * parameter has to land on the note rather than on the top of a list. There is no by-id route and
 * no pagination, so the screen reads every note once and pins the named one above whichever queue
 * is open — see `buildVisibleReviewNotes` for why pinning beats switching tabs.
 */
export default function OrganizationReviewsPage() {
    return (
        <Suspense fallback={<PageHeader title="Спорные оценки" />}>
            <OrganizationReviewsScreen />
        </Suspense>
    );
}

function OrganizationReviewsScreen() {
    const searchParameters = useSearchParams();
    const requestedNoteId = searchParameters.get("note");

    const notesQuery = useOrganizationReviewNotes();
    const { memberNames } = useTeamMemberNames();
    const authenticatedUser = useAuthStore((state) => state.authenticatedUser);

    const [activeQueueKey, setActiveQueueKey] = useState<ReviewQueueKey>(DEFAULT_REVIEW_QUEUE_KEY);

    const memberNamesByUserId = useMemo(
        () => buildMemberNamesByUserId(memberNames),
        [memberNames]
    );

    const threadContext = useMemo(
        () => ({
            currentUserId: authenticatedUser?.id ?? null,
            memberNamesByUserId,
        }),
        [authenticatedUser?.id, memberNamesByUserId]
    );

    const notes = notesQuery.data ?? [];
    const visibleNotes = buildVisibleReviewNotes(notes, activeQueueKey, requestedNoteId);
    const openDisputeCount = countOpenDisputes(notes);

    return (
        <>
            <PageHeader
                title="Спорные оценки"
                subtitle="Менеджер не согласился с оценкой ИИ — здесь её судят. Здесь же видно, что вы уже отправляли команде."
            />

            <Tabs
                className="mb-6"
                activeKey={activeQueueKey}
                onChange={(queueKey) => setActiveQueueKey(queueKey as ReviewQueueKey)}
                items={[
                    {
                        key: "open",
                        label: REVIEW_QUEUE_LABELS.open,
                        badge: openDisputeCount,
                    },
                    { key: "disputes", label: REVIEW_QUEUE_LABELS.disputes },
                    { key: "notes", label: REVIEW_QUEUE_LABELS.notes },
                ]}
            />

            {notesQuery.isLoading && <SkeletonList count={SKELETON_ROW_COUNT} rowHeight={140} />}

            {!notesQuery.isLoading && notesQuery.isError && (
                <ErrorState
                    title="Не удалось загрузить споры"
                    message="Проверьте подключение и попробуйте снова."
                    onRetry={() => notesQuery.refetch()}
                />
            )}

            {!notesQuery.isLoading && !notesQuery.isError && visibleNotes.length === 0 && (
                <EmptyState
                    icon="warning"
                    title={EMPTY_QUEUE_COPY[activeQueueKey].title}
                    description={EMPTY_QUEUE_COPY[activeQueueKey].description}
                />
            )}

            {visibleNotes.map((note) => (
                <ReviewNoteCard
                    key={note.id}
                    note={note}
                    context={threadContext}
                    isHighlighted={note.id === requestedNoteId}
                />
            ))}
        </>
    );
}
