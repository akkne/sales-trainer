"use client";

import { useMemo, useState } from "react";
import { useParams } from "next/navigation";
import { Card, CardContent } from "@/shared/components/card";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { FeedbackHtml } from "@/shared/components/feedback-html";
import { PageHeader } from "@/shared/components/page-header";
import { SkeletonList } from "@/shared/components/skeleton";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useTeamMemberNames } from "@/features/org-shell/hooks/use-team-directory";
import { ReviewNoteComposer } from "@/features/org-dialogs/components/review-note-composer";
import { ReviewNoteThreadView } from "@/features/org-dialogs/components/review-note-thread-view";
import { TranscriptViewer } from "@/features/org-dialogs/components/transcript-viewer";
import { useSessionReviewNotes } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import {
    isTranscriptNotFound,
    useDialogTranscript,
} from "@/features/org-dialogs/hooks/use-dialog-transcript";
import { formatPanelScore, toPanelScore } from "@/features/org-dialogs/lib/dialog-score";
import { formatDialogMomentInFull } from "@/features/org-dialogs/lib/format-dialog-moment";
import {
    buildMemberNamesByUserId,
    resolveMemberLabel,
} from "@/features/org-dialogs/lib/team-member-labels";
import {
    toggleTranscriptSelection,
    type TranscriptSelection,
} from "@/features/org-dialogs/lib/transcript-selection";

const TRANSCRIPT_SKELETON_ROW_COUNT = 8;

/**
 * O6 «Транскрипт и разбор» (docs/TENANCY/ADMIN_UI_DESIGN.md O6).
 *
 * Two services, two columns, two independent failures. The transcript is ai-service's and the
 * notes are learning-service's, so a dead ai-service leaves the left column in an error and the
 * right one still listing what was already sent — telling the reader both halves are broken
 * because one is would be a lie the screen is in a position to avoid.
 */
export default function OrganizationDialogTranscriptPage() {
    const routeParameters = useParams<{ sessionId: string }>();
    const sessionId = decodeURIComponent(String(routeParameters.sessionId ?? ""));

    const transcriptQuery = useDialogTranscript(sessionId);
    const notesQuery = useSessionReviewNotes(sessionId);
    const { memberNames } = useTeamMemberNames();
    const authenticatedUser = useAuthStore((state) => state.authenticatedUser);

    const [selection, setSelection] = useState<TranscriptSelection | null>(null);

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

    const transcript = transcriptQuery.data;
    const notes = notesQuery.data ?? [];
    const panelScore = toPanelScore(transcript?.score ?? null);
    const managerLabel = transcript
        ? resolveMemberLabel(transcript.userId, memberNamesByUserId)
        : "Менеджер";

    if (transcriptQuery.isError && isTranscriptNotFound(transcriptQuery.error)) {
        return (
            <>
                <PageHeader
                    title="Разговор"
                    backHref="/org/dialogs"
                    backLabel="Разговоры"
                />
                <EmptyState
                    icon="message"
                    title="Разговор не найден"
                    description="Возможно, ссылка устарела или разговор принадлежит другой компании."
                />
            </>
        );
    }

    const subtitleParts = transcript
        ? [
              managerLabel,
              transcript.modeTitle ? `«${transcript.modeTitle}»` : null,
              formatDialogMomentInFull(transcript.createdAt),
              panelScore === null ? "без оценки" : `оценка ${formatPanelScore(panelScore)}`,
          ].filter((part): part is string => part !== null)
        : [];

    return (
        <>
            <PageHeader
                title="Транскрипт и разбор"
                subtitle={subtitleParts.length > 0 ? subtitleParts.join(" · ") : undefined}
                backHref="/org/dialogs"
                backLabel="Разговоры"
            />

            <div className="grid gap-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)] items-start">
                <Card>
                    <CardContent style={{ marginTop: 0 }}>
                        {transcriptQuery.isLoading && (
                            <SkeletonList count={TRANSCRIPT_SKELETON_ROW_COUNT} rowHeight={48} />
                        )}

                        {transcriptQuery.isError && (
                            <ErrorState
                                title="Не удалось загрузить транскрипт"
                                message="Разговоры хранит отдельный сервис. Уже отправленные заметки справа при этом видны."
                                onRetry={() => transcriptQuery.refetch()}
                            />
                        )}

                        {transcript && transcript.messages.length === 0 && (
                            <EmptyState
                                compact
                                icon="message"
                                title="В разговоре нет реплик"
                                description="Цитировать нечего — разговор был прерван до первой реплики."
                            />
                        )}

                        {transcript && transcript.messages.length > 0 && (
                            <TranscriptViewer
                                messages={transcript.messages}
                                managerLabel={managerLabel}
                                selection={selection}
                                onMessageClick={(messageIndex, isRangeExtension) =>
                                    setSelection((current) =>
                                        toggleTranscriptSelection(
                                            current,
                                            messageIndex,
                                            isRangeExtension
                                        )
                                    )
                                }
                            />
                        )}
                    </CardContent>
                </Card>

                <div className="flex flex-col gap-6">
                    {transcript && panelScore === null && (
                        <Card>
                            <CardContent style={{ marginTop: 0 }}>
                                <p className="text-sm text-ink-3">
                                    Разговор не оценён — прокомментировать его нельзя. Заметка
                                    привязывается к выставленной оценке, а её здесь нет.
                                </p>
                            </CardContent>
                        </Card>
                    )}

                    {transcript && panelScore !== null && transcript.messages.length > 0 && (
                        <Card>
                            <CardContent style={{ marginTop: 0 }}>
                                <ReviewNoteComposer
                                    sessionId={sessionId}
                                    messages={transcript.messages}
                                    selection={selection}
                                    onSent={() => setSelection(null)}
                                />
                            </CardContent>
                        </Card>
                    )}

                    {transcript?.feedback && (
                        <Card>
                            <CardContent style={{ marginTop: 0 }}>
                                <h2 className="text-xs font-medium text-ink-3 uppercase tracking-wide mb-3">
                                    Обратная связь ИИ
                                </h2>
                                <FeedbackHtml
                                    html={transcript.feedback.content}
                                    className="text-sm text-ink-2 [&_h3]:text-[15px] [&_h3]:font-semibold [&_h3]:mt-4 [&_h3]:mb-2 [&_h3:first-child]:mt-0 [&_strong]:font-semibold [&_em]:italic [&_p]:my-2 [&_ul]:my-2 [&_ul]:pl-5"
                                />
                            </CardContent>
                        </Card>
                    )}

                    <Card>
                        <CardContent style={{ marginTop: 0 }}>
                            <h2 className="text-xs font-medium text-ink-3 uppercase tracking-wide mb-3">
                                Уже отправлено{notes.length > 0 ? ` (${notes.length})` : ""}
                            </h2>

                            {notesQuery.isLoading && <SkeletonList count={2} rowHeight={56} />}

                            {notesQuery.isError && (
                                <ErrorState
                                    compact
                                    title="Не удалось загрузить заметки"
                                    onRetry={() => notesQuery.refetch()}
                                />
                            )}

                            {!notesQuery.isLoading && !notesQuery.isError && notes.length === 0 && (
                                <p className="text-sm text-ink-3">
                                    По этому разговору ещё ничего не отправляли.
                                </p>
                            )}

                            {notes.length > 0 && (
                                <div className="flex flex-col gap-4">
                                    {notes.map((note) => (
                                        <ReviewNoteThreadView
                                            key={note.id}
                                            note={note}
                                            context={threadContext}
                                        />
                                    ))}
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </div>
            </div>
        </>
    );
}
