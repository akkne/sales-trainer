"use client";

import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { VoteButton } from "./vote-button";
import { formatTimeAgo } from "../lib/format";
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
                <p className="small dsc-excerpt">{thread.bodyPreview}</p>

                <div className="dsc-meta">
                    <GeoAvatar seed={thread.authorName || thread.authorId} size={24} />
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
