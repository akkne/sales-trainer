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
        <article className="card dsc-thread lift">
            <VoteButton
                count={thread.upvoteCount}
                active={thread.viewerHasUpvoted}
                disabled={vote.isPending}
                onToggle={() => vote.mutate(!thread.viewerHasUpvoted)}
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

                <Link href={`/discuss/${thread.id}`} style={{ textDecoration: "none", color: "inherit" }}>
                    <h3 className="h4 dsc-title">{thread.title}</h3>
                </Link>
                <div className="row gap-2" style={{ alignItems: "flex-start" }}>
                    <p className="small dsc-excerpt" style={{ flex: 1 }}>{thread.bodyPreview}</p>
                    {thread.photoCount > 0 && thread.firstPhotoUrl && (
                        <Link
                            href={`/discuss/${thread.id}`}
                            style={{
                                position: "relative",
                                flexShrink: 0,
                                width: 64,
                                height: 64,
                                borderRadius: 10,
                                overflow: "hidden",
                                border: "1px solid var(--line-2)",
                            }}
                        >
                            <img
                                src={resolveDiscussPhotoUrl(thread.firstPhotoUrl)}
                                alt=""
                                loading="lazy"
                                style={{ width: "100%", height: "100%", objectFit: "cover", display: "block" }}
                            />
                            {thread.photoCount > 1 && (
                                <span
                                    style={{
                                        position: "absolute",
                                        right: 2,
                                        bottom: 2,
                                        padding: "1px 6px",
                                        borderRadius: 8,
                                        fontSize: 11,
                                        fontWeight: 700,
                                        background: "rgba(0,0,0,0.65)",
                                        color: "#fff",
                                    }}
                                >
                                    +{thread.photoCount - 1}
                                </span>
                            )}
                        </Link>
                    )}
                </div>

                <div className="dsc-meta">
                    <UserAvatar avatarUrl={thread.authorAvatarUrl} seed={thread.authorName || thread.authorId} size={24} circle />
                    <span style={{ fontWeight: 600 }}>{thread.authorName || "Аноним"}</span>
                    <span className="dsc-dot">·</span>
                    <span className="row gap-1"><Icon name="message" size={15} />{thread.replyCount}</span>
                    <span className="row gap-1"><Icon name="search" size={15} />{thread.viewCount.toLocaleString("ru")}</span>
                    <span className="dsc-when">{formatTimeAgo(thread.lastActivityAt)}</span>
                </div>
            </div>
        </article>
    );
}
