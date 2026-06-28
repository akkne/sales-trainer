"use client";

import { useState } from "react";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Skeleton, ErrorState } from "@/shared/components";
import { UserAvatar } from "@/shared/components/user-avatar";
import { ThreadCard } from "@/features/discuss/components/thread-card";
import { NewThreadModal } from "@/features/discuss/components/new-thread-modal";
import {
    useDiscussStats,
    useDiscussThreads,
    usePopularTags,
    type DiscussSort,
} from "@/features/discuss/hooks/use-discuss";

const SORTS: { id: DiscussSort; label: string }[] = [
    { id: "hot",        label: "Popular" },
    { id: "new",        label: "New" },
    { id: "unanswered", label: "Unanswered" },
];

export default function DiscussPage() {
    const [sort, setSort]               = useState<DiscussSort>("hot");
    const [activeTag, setActiveTag]     = useState<string | null>(null);
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch]           = useState("");
    const [showNewThread, setShowNewThread] = useState(false);

    const { data: page, isLoading, error, refetch } = useDiscussThreads({
        sort,
        tag:      activeTag ?? undefined,
        search:   search || undefined,
        pageSize: 20,
    });
    const { data: stats }       = useDiscussStats();
    const { data: popularTags } = usePopularTags(8);

    // solvedThreads is not yet exposed by the API — show dash gracefully
    const solvedPct: number | null = null;

    return (
        <div className="page">
            <div className="container">

                {/* ── Header ── */}
                <div className="dsc-header">
                    <div className="dsc-header-left">
                        <h1 className="dsc-header-title">Discussions</h1>
                        <p className="dsc-header-sub">
                            Ask questions, share scripts — the best answers rise to the top
                        </p>
                    </div>
                    <Button variant="primary" iconLeft="plus" onClick={() => setShowNewThread(true)}>
                        New question
                    </Button>
                </div>

                {/* ── Search ── */}
                <div className="dsc-tools">
                    <form
                        className="field-wrap has-ic grow"
                        style={{ maxWidth: 400 }}
                        onSubmit={(event) => {
                            event.preventDefault();
                            setSearch(searchInput.trim());
                        }}
                    >
                        <Icon name="search" className="lead-ic" size={18} />
                        <input
                            className="field"
                            placeholder="Search discussions…"
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                        />
                    </form>
                </div>

                {/* ── Sort segmented control ── */}
                <div className="dsc-sort-row" style={{ paddingBottom: 16 }}>
                    <div className="seg">
                        {SORTS.map((option) => (
                            <button
                                key={option.id}
                                className={`seg-btn${sort === option.id ? " active" : ""}`}
                                onClick={() => setSort(option.id)}
                            >
                                {option.label}
                            </button>
                        ))}
                    </div>
                </div>

                {/* ── Main content + sidebar ── */}
                <div className="dsc-grid">

                    {/* Thread list */}
                    <div>
                        {isLoading && (
                            <div className="col" style={{ gap: 10 }}>
                                {[1, 2, 3].map((i) => <Skeleton key={i} height={110} rounded={14} />)}
                            </div>
                        )}
                        {error && (
                            <ErrorState title="Failed to load" message={error.message} onRetry={() => refetch()} />
                        )}
                        {page && page.items.length === 0 && (
                            <div className="empty" style={{ paddingTop: 60 }}>
                                <div className="ic"><Icon name="forum" size="lg" /></div>
                                <h2 className="h4" style={{ marginBottom: 8 }}>No discussions yet</h2>
                                <p className="small">Start the first thread — click "New question".</p>
                            </div>
                        )}
                        {page && page.items.length > 0 && (
                            <div className="dsc-list-card">
                                <div className="dsc-feed">
                                    {page.items.map((thread) => (
                                        <ThreadCard key={thread.id} thread={thread} />
                                    ))}
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Sidebar */}
                    <aside className="dsc-sidebar">

                        {/* Community stats */}
                        <div className="dsc-sidebar-card">
                            <p className="dsc-sidebar-title">Community</p>
                            <div className="dsc-stats-grid">
                                <div className="dsc-stat-cell">
                                    <span className="dsc-stat-value">{stats?.totalThreads ?? "—"}</span>
                                    <span className="dsc-stat-label">Threads</span>
                                </div>
                                <div className="dsc-stat-cell">
                                    <span className="dsc-stat-value">{stats?.totalReplies ?? "—"}</span>
                                    <span className="dsc-stat-label">Replies</span>
                                </div>
                                <div className="dsc-stat-cell">
                                    <span className="dsc-stat-value green">
                                        {solvedPct !== null ? `${solvedPct}%` : "—"}
                                    </span>
                                    <span className="dsc-stat-label">% solved</span>
                                </div>
                            </div>
                        </div>

                        {/* Popular tags */}
                        <div className="dsc-sidebar-card">
                            <p className="dsc-sidebar-title">Popular tags</p>
                            <div className="dsc-tag-cloud">
                                {(popularTags ?? []).map((tag) => (
                                    <button
                                        key={tag.slug}
                                        className={`dsc-tag-btn${activeTag === tag.slug ? " active" : ""}`}
                                        onClick={() => setActiveTag(activeTag === tag.slug ? null : tag.slug)}
                                    >
                                        {tag.name}
                                    </button>
                                ))}
                                {(!popularTags || popularTags.length === 0) && (
                                    <span style={{ fontSize: 13, color: "var(--ink-3)" }}>No tags yet</span>
                                )}
                            </div>
                        </div>

                        {/* Top authors */}
                        <div className="dsc-sidebar-card">
                            <p className="dsc-sidebar-title">Top authors this week</p>
                            <div>
                                {(stats?.topAuthorsOfWeek ?? []).map((author, index) => (
                                    <div key={author.authorId} className="dsc-author">
                                        <span className={`rank-badge r${index + 1}`}>{index + 1}</span>
                                        <UserAvatar
                                            avatarUrl={author.authorAvatarUrl}
                                            seed={author.authorName || author.authorId}
                                            size={28}
                                            circle
                                        />
                                        <span className="dsc-author-name">
                                            {author.authorName || "Anonymous"}
                                        </span>
                                        <span className="dsc-author-pts">
                                            <Icon name="arrow-up" size={12} />
                                            {author.upvotesReceived.toLocaleString("en")}
                                        </span>
                                    </div>
                                ))}
                                {(!stats || stats.topAuthorsOfWeek.length === 0) && (
                                    <span style={{ fontSize: 13, color: "var(--ink-3)" }}>No data yet</span>
                                )}
                            </div>
                        </div>

                    </aside>
                </div>

                <div style={{ height: 48 }} />
            </div>

            {showNewThread && <NewThreadModal onClose={() => setShowNewThread(false)} />}
        </div>
    );
}
