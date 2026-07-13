"use client";

import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { UserAvatar } from "@/shared/components/user-avatar";
import { VoteButton } from "./vote-button";
import { formatTimeAgo } from "../lib/format";
import { resolveDiscussPhotoUrl } from "../utils/resolve-discuss-photo-url";
import { useThreadVote, type DiscussThreadSummary } from "../hooks/use-discuss";

export function ThreadCard({ thread }: { thread: DiscussThreadSummary }) {
    const vote = useThreadVote(thread.id);

    return (
        <article className="dsc-feed-row">
            {/* Upvote column */}
            <VoteButton
                count={thread.upvoteCount}
                active={thread.viewerHasUpvoted}
                disabled={vote.isPending}
                onToggle={() => vote.mutate(!thread.viewerHasUpvoted)}
            />

            {/* Body */}
            <div className="dsc-body">
                {/* Badges */}
                {(thread.isPinned || thread.isSolved || thread.isHot) && (
                    <div className="dsc-badges">
                        {thread.isPinned && (
                            <span className="dsc-badge pinned">Закреплено</span>
                        )}
                        {thread.isSolved && (
                            <span className="dsc-badge solved">Решено</span>
                        )}
                        {thread.isHot && (
                            <span className="dsc-badge hot">Популярное</span>
                        )}
                    </div>
                )}

                {/* Title + optional thumbnail */}
                <div style={{ display: "flex", alignItems: "flex-start", gap: 0 }}>
                    <div style={{ flex: 1, minWidth: 0 }}>
                        <Link href={`/discuss/${thread.id}`} className="dsc-title-link">
                            {thread.title}
                        </Link>
                        {thread.bodyPreview && (
                            <p className="dsc-excerpt">{thread.bodyPreview}</p>
                        )}
                    </div>
                    {thread.photoCount > 0 && thread.firstPhotoUrl && (
                        <Link href={`/discuss/${thread.id}`} className="dsc-thumb">
                            <img
                                src={resolveDiscussPhotoUrl(thread.firstPhotoUrl)}
                                alt=""
                                loading="lazy"
                            />
                            {thread.photoCount > 1 && (
                                <span className="dsc-thumb-more">+{thread.photoCount - 1}</span>
                            )}
                        </Link>
                    )}
                </div>

                {/* Meta row */}
                <div className="dsc-meta">
                    {/* Hashtag chips */}
                    {thread.tags.slice(0, 3).map((tag) => (
                        <span key={tag.slug} className="dsc-tag">{tag.name}</span>
                    ))}

                    {/* Author */}
                    <span className="dsc-meta-author">
                        <UserAvatar
                            avatarUrl={thread.authorAvatarUrl}
                            seed={thread.authorName || thread.authorId}
                            size={18}
                            circle
                        />
                        {thread.authorName || "Аноним"}
                    </span>

                    <span className="dsc-meta-sep">·</span>
                    <span className="dsc-meta-time">{formatTimeAgo(thread.lastActivityAt)}</span>

                    {/* Reply count */}
                    <span className="dsc-replies">
                        <Icon name="message" size={13} />
                        {thread.replyCount}
                    </span>
                </div>
            </div>
        </article>
    );
}
