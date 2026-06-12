"use client";

import { use, useState } from "react";
import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Textarea } from "@/shared/components/input";
import { GeoAvatar, Skeleton, ErrorState } from "@/shared/components";
import { useAuthStore } from "@/shared/stores/auth-store";
import { VoteButton } from "@/features/discuss/components/vote-button";
import { PhotoPicker } from "@/features/discuss/components/photo-picker";
import { PhotoGallery } from "@/features/discuss/components/photo-gallery";
import { formatTimeAgo } from "@/features/discuss/lib/format";
import {
    useAddReply,
    useDeleteDiscussPhoto,
    useDiscussThread,
    useReplyVote,
    useSetAcceptedReply,
    useThreadVote,
    useUploadReplyPhotos,
} from "@/features/discuss/hooks/use-discuss";

export default function ThreadDetailPage({ params }: { params: Promise<{ threadId: string }> }) {
    const { threadId } = use(params);
    const { authenticatedUser } = useAuthStore();

    const { data: thread, isLoading, error, refetch } = useDiscussThread(threadId);
    const threadVote = useThreadVote(threadId);
    const replyVote = useReplyVote(threadId);
    const acceptedReply = useSetAcceptedReply(threadId);
    const addReply = useAddReply(threadId);
    const uploadReplyPhotos = useUploadReplyPhotos(threadId);
    const deletePhoto = useDeleteDiscussPhoto(threadId);

    const [replyBody, setReplyBody] = useState("");
    const [replyFiles, setReplyFiles] = useState<File[]>([]);
    const [replyError, setReplyError] = useState<string | null>(null);

    const canModerate =
        !!thread &&
        !!authenticatedUser &&
        (authenticatedUser.id === thread.authorId ||
            authenticatedUser.role === "Admin" ||
            authenticatedUser.role === "SuperAdmin");

    const isViewingOwnThread = !!thread && !!authenticatedUser && authenticatedUser.id === thread.authorId;

    const isReplyBusy = addReply.isPending || uploadReplyPhotos.isPending;

    const submitReply = async () => {
        if (!replyBody.trim()) return;
        setReplyError(null);
        const createdReply = await addReply.mutateAsync(replyBody.trim());
        const filesToUpload = replyFiles;
        setReplyBody("");
        setReplyFiles([]);
        if (filesToUpload.length > 0) {
            try {
                await uploadReplyPhotos.mutateAsync({ replyId: createdReply.id, files: filesToUpload });
            } catch {
                setReplyError("Ответ опубликован, но фото не загрузились");
            }
        }
    };

    if (isLoading) {
        return (
            <div className="page container">
                <Skeleton width={120} height={16} style={{ marginTop: 24 }} />
                <Skeleton height={200} rounded={16} style={{ marginTop: 16 }} />
            </div>
        );
    }

    if (error || !thread) {
        return (
            <div className="page container" style={{ paddingTop: 60 }}>
                <ErrorState
                    title="Тема не найдена"
                    message={error?.message ?? "Возможно, она была удалена"}
                    onRetry={() => refetch()}
                />
            </div>
        );
    }

    return (
        <div className="page">
            <div className="container" style={{ maxWidth: 860 }}>
                <Link href="/discuss" className="row gap-1 small" style={{ color: "var(--ink-3)", marginTop: 16, textDecoration: "none" }}>
                    <Icon name="arrow-left" size={16} /> К обсуждениям
                </Link>

                {/* Thread */}
                <article className="card dsc-thread lift" style={{ marginTop: 16 }}>
                    <VoteButton
                        count={thread.upvoteCount}
                        active={thread.viewerHasUpvoted}
                        disabled={threadVote.isPending}
                        onToggle={() => threadVote.mutate(!thread.viewerHasUpvoted)}
                    />
                    <div className="dsc-body">
                        <div className="row gap-2 wrap" style={{ marginBottom: 8 }}>
                            {thread.isPinned && (
                                <span className="dsc-badge" style={{ background: "var(--primary-soft)", color: "var(--primary)" }}>
                                    <Icon name="bolt" size={14} /> Закреплено
                                </span>
                            )}
                            {thread.isSolved && (
                                <span className="dsc-badge" style={{ background: "var(--success-soft)", color: "var(--success)" }}>
                                    <Icon name="check" size={14} /> Решено
                                </span>
                            )}
                            {thread.isHot && (
                                <span className="dsc-badge" style={{ background: "var(--flame-soft)", color: "var(--flame)" }}>
                                    <Icon name="flame" size={14} /> Горячее
                                </span>
                            )}
                            {thread.tags.map((tag) => (
                                <span key={tag.slug} className="chip">{tag.name}</span>
                            ))}
                        </div>
                        <h1 className="h2 dsc-title">{thread.title}</h1>
                        <p className="body" style={{ margin: "12px 0 16px", whiteSpace: "pre-wrap" }}>{thread.body}</p>
                        <PhotoGallery
                            photos={thread.photos}
                            canDelete={isViewingOwnThread}
                            deleteDisabled={deletePhoto.isPending}
                            onDelete={(photoId) => deletePhoto.mutate(photoId)}
                        />
                        <div className="dsc-meta">
                            <GeoAvatar seed={thread.authorName || thread.authorId} size={24} />
                            <span style={{ fontWeight: 600 }}>{thread.authorName || "Аноним"}</span>
                            <span className="dsc-dot">·</span>
                            <span className="dsc-when">{formatTimeAgo(thread.createdAt)}</span>
                        </div>
                    </div>
                </article>

                {/* Replies */}
                <h2 className="h4" style={{ margin: "28px 0 14px" }}>
                    {thread.replyCount} {thread.replyCount === 1 ? "ответ" : "ответов"}
                </h2>

                <div className="col" style={{ gap: 12 }}>
                    {thread.replies.map((reply) => (
                        <article
                            key={reply.id}
                            className="card dsc-thread"
                            style={reply.isAccepted ? { borderColor: "var(--success)" } : undefined}
                        >
                            <VoteButton
                                count={reply.upvoteCount}
                                active={reply.viewerHasUpvoted}
                                disabled={replyVote.isPending}
                                onToggle={() => replyVote.mutate({ replyId: reply.id, upvote: !reply.viewerHasUpvoted })}
                            />
                            <div className="dsc-body">
                                {reply.isAccepted && (
                                    <span className="dsc-badge" style={{ background: "var(--success-soft)", color: "var(--success)", marginBottom: 8 }}>
                                        <Icon name="check" size={14} /> Лучший ответ
                                    </span>
                                )}
                                <p className="body" style={{ whiteSpace: "pre-wrap" }}>{reply.body}</p>
                                <PhotoGallery
                                    photos={reply.photos}
                                    canDelete={!!authenticatedUser && authenticatedUser.id === reply.authorId}
                                    deleteDisabled={deletePhoto.isPending}
                                    onDelete={(photoId) => deletePhoto.mutate(photoId)}
                                />
                                <div className="dsc-meta" style={{ marginTop: 12 }}>
                                    <GeoAvatar seed={reply.authorName || reply.authorId} size={24} />
                                    <span style={{ fontWeight: 600 }}>{reply.authorName || "Аноним"}</span>
                                    <span className="dsc-dot">·</span>
                                    <span className="dsc-when">{formatTimeAgo(reply.createdAt)}</span>
                                    {canModerate && (
                                        <button
                                            className="chip"
                                            style={{ marginLeft: 12 }}
                                            disabled={acceptedReply.isPending}
                                            onClick={() => acceptedReply.mutate(reply.isAccepted ? null : reply.id)}
                                        >
                                            {reply.isAccepted ? "Снять отметку" : "Отметить решением"}
                                        </button>
                                    )}
                                </div>
                            </div>
                        </article>
                    ))}
                    {thread.replies.length === 0 && (
                        <p className="small" style={{ color: "var(--ink-3)" }}>Ответов пока нет — будьте первым.</p>
                    )}
                </div>

                {/* Reply composer */}
                <div className="card card-pad" style={{ marginTop: 24 }}>
                    <label className="text-sm font-medium text-ink">Ваш ответ</label>
                    <Textarea
                        placeholder="Поделитесь опытом или скриптом…"
                        value={replyBody}
                        rows={4}
                        style={{ marginTop: 8 }}
                        onChange={(event) => setReplyBody(event.target.value)}
                    />
                    <div style={{ marginTop: 12 }}>
                        <PhotoPicker files={replyFiles} onChange={setReplyFiles} disabled={isReplyBusy} />
                    </div>
                    {replyError && <p className="text-xs text-bad" style={{ marginTop: 8 }}>{replyError}</p>}
                    <div className="row" style={{ justifyContent: "flex-end", marginTop: 12 }}>
                        <Button
                            variant="primary"
                            iconLeft="send"
                            loading={isReplyBusy}
                            disabled={!replyBody.trim()}
                            onClick={submitReply}
                        >
                            Ответить
                        </Button>
                    </div>
                </div>

                <div style={{ height: 60 }} />
            </div>
        </div>
    );
}
