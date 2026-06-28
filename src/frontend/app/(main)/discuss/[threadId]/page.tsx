"use client";

import { use, useState } from "react";
import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Textarea } from "@/shared/components/input";
import { Skeleton, ErrorState } from "@/shared/components";
import { UserAvatar } from "@/shared/components/user-avatar";
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
    const threadVote      = useThreadVote(threadId);
    const replyVote       = useReplyVote(threadId);
    const acceptedReply   = useSetAcceptedReply(threadId);
    const addReply        = useAddReply(threadId);
    const uploadReplyPhotos = useUploadReplyPhotos(threadId);
    const deletePhoto     = useDeleteDiscussPhoto(threadId);

    const [replyBody, setReplyBody]   = useState("");
    const [replyFiles, setReplyFiles] = useState<File[]>([]);
    const [replyError, setReplyError] = useState<string | null>(null);

    const canModerate =
        !!thread &&
        !!authenticatedUser &&
        (authenticatedUser.id === thread.authorId ||
            authenticatedUser.role === "Admin" ||
            authenticatedUser.role === "SuperAdmin");

    const isViewingOwnThread =
        !!thread && !!authenticatedUser && authenticatedUser.id === thread.authorId;

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
                setReplyError("Reply posted, but photos failed to upload");
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
                    title="Thread not found"
                    message={error?.message ?? "It may have been deleted"}
                    onRetry={() => refetch()}
                />
            </div>
        );
    }

    return (
        <div className="page">
            <div className="container" style={{ maxWidth: 860 }}>

                {/* Back link */}
                <Link href="/discuss" className="dsc-detail-back">
                    <Icon name="chevron-left" size={16} />
                    Discussions
                </Link>

                {/* ── Thread ── */}
                <article className="dsc-detail-thread">
                    {/* Upvote column */}
                    <div className="dsc-detail-vote">
                        <VoteButton
                            count={thread.upvoteCount}
                            active={thread.viewerHasUpvoted}
                            disabled={threadVote.isPending}
                            onToggle={() => threadVote.mutate(!thread.viewerHasUpvoted)}
                        />
                    </div>

                    {/* Body */}
                    <div className="dsc-detail-body">
                        {/* Badges */}
                        <div className="dsc-badges">
                            {thread.isPinned && <span className="dsc-badge pinned">Pinned</span>}
                            {thread.isSolved && <span className="dsc-badge solved">Solved</span>}
                            {thread.isHot    && <span className="dsc-badge hot">Hot</span>}
                        </div>

                        <h1 className="dsc-detail-title">{thread.title}</h1>

                        {/* Tags */}
                        {thread.tags.length > 0 && (
                            <div style={{ display: "flex", gap: 6, flexWrap: "wrap", marginBottom: 14 }}>
                                {thread.tags.map((tag) => (
                                    <span key={tag.slug} className="dsc-tag">{tag.name}</span>
                                ))}
                            </div>
                        )}

                        <p className="dsc-detail-body-text">{thread.body}</p>

                        <PhotoGallery
                            photos={thread.photos}
                            canDelete={isViewingOwnThread}
                            deleteDisabled={deletePhoto.isPending}
                            onDelete={(photoId) => deletePhoto.mutate(photoId)}
                        />

                        {/* Meta */}
                        <div className="dsc-detail-meta">
                            <UserAvatar
                                seed={thread.authorName || thread.authorId}
                                size={22}
                                circle
                            />
                            <span className="dsc-detail-meta-name">{thread.authorName || "Anonymous"}</span>
                            <span className="dsc-detail-sep">·</span>
                            <span style={{ fontSize: 12, color: "var(--ink-4)" }}>
                                {formatTimeAgo(thread.createdAt)}
                            </span>
                        </div>
                    </div>
                </article>

                {/* ── Replies heading ── */}
                <h2 className="dsc-replies-heading">
                    {thread.replyCount === 1
                        ? "1 reply"
                        : `${thread.replyCount} replies`}
                </h2>

                {/* ── Replies ── */}
                <div className="col" style={{ gap: 10 }}>
                    {thread.replies.map((reply) => (
                        <article
                            key={reply.id}
                            className={`dsc-reply${reply.isAccepted ? " accepted" : ""}`}
                        >
                            {/* Upvote */}
                            <div className="dsc-reply-vote">
                                <VoteButton
                                    count={reply.upvoteCount}
                                    active={reply.viewerHasUpvoted}
                                    disabled={replyVote.isPending}
                                    onToggle={() =>
                                        replyVote.mutate({ replyId: reply.id, upvote: !reply.viewerHasUpvoted })
                                    }
                                />
                            </div>

                            {/* Body */}
                            <div className="dsc-reply-body">
                                {reply.isAccepted && (
                                    <div className="dsc-accepted-badge">
                                        <Icon name="check" size={11} /> Best answer
                                    </div>
                                )}
                                <p className="dsc-reply-text">{reply.body}</p>
                                <PhotoGallery
                                    photos={reply.photos}
                                    canDelete={
                                        !!authenticatedUser && authenticatedUser.id === reply.authorId
                                    }
                                    deleteDisabled={deletePhoto.isPending}
                                    onDelete={(photoId) => deletePhoto.mutate(photoId)}
                                />
                                <div className="dsc-reply-meta">
                                    <UserAvatar
                                        seed={reply.authorName || reply.authorId}
                                        size={20}
                                        circle
                                    />
                                    <span className="dsc-reply-meta-name">{reply.authorName || "Anonymous"}</span>
                                    <span style={{ color: "var(--ink-4)" }}>·</span>
                                    <span style={{ fontSize: 12, color: "var(--ink-4)" }}>
                                        {formatTimeAgo(reply.createdAt)}
                                    </span>
                                    {canModerate && (
                                        <button
                                            className={`dsc-accept-btn${reply.isAccepted ? " active" : ""}`}
                                            disabled={acceptedReply.isPending}
                                            onClick={() =>
                                                acceptedReply.mutate(reply.isAccepted ? null : reply.id)
                                            }
                                        >
                                            <Icon name={reply.isAccepted ? "close" : "check"} size={12} />
                                            {reply.isAccepted ? "Unmark" : "Mark as solution"}
                                        </button>
                                    )}
                                </div>
                            </div>
                        </article>
                    ))}

                    {thread.replies.length === 0 && (
                        <p style={{ fontSize: 13, color: "var(--ink-3)", padding: "12px 0" }}>
                            No replies yet — be the first.
                        </p>
                    )}
                </div>

                {/* ── Reply composer ── */}
                <div className="dsc-composer">
                    <span className="dsc-composer-label">Your reply</span>
                    <Textarea
                        placeholder="Share your experience or script…"
                        value={replyBody}
                        rows={4}
                        onChange={(event) => setReplyBody(event.target.value)}
                    />
                    <div style={{ marginTop: 12 }}>
                        <PhotoPicker files={replyFiles} onChange={setReplyFiles} disabled={isReplyBusy} />
                    </div>
                    {replyError && (
                        <p style={{ fontSize: 12, color: "var(--bad)", marginTop: 8 }}>{replyError}</p>
                    )}
                    <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 12 }}>
                        <Button
                            variant="primary"
                            iconLeft="send"
                            loading={isReplyBusy}
                            disabled={!replyBody.trim()}
                            onClick={submitReply}
                        >
                            Reply
                        </Button>
                    </div>
                </div>

                <div style={{ height: 48 }} />
            </div>
        </div>
    );
}
