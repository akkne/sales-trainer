"use client";

import { useState } from "react";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { GeoAvatar, Skeleton, StatTile, ErrorState } from "@/shared/components";
import { ThreadCard } from "@/features/discuss/components/thread-card";
import { NewThreadModal } from "@/features/discuss/components/new-thread-modal";
import {
    useDiscussStats,
    useDiscussThreads,
    usePopularTags,
    type DiscussSort,
} from "@/features/discuss/hooks/use-discuss";

const SORTS: { id: DiscussSort; label: string; icon: import("@/shared/components/icon").IconName }[] = [
    { id: "hot", label: "Популярное", icon: "flame" },
    { id: "new", label: "Новое", icon: "clock" },
    { id: "unanswered", label: "Без ответа", icon: "info" },
];

export default function DiscussPage() {
    const [sort, setSort] = useState<DiscussSort>("hot");
    const [activeTag, setActiveTag] = useState<string | null>(null);
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [showNewThread, setShowNewThread] = useState(false);

    const { data: page, isLoading, error, refetch } = useDiscussThreads({
        sort,
        tag: activeTag ?? undefined,
        search: search || undefined,
        pageSize: 20,
    });
    const { data: stats } = useDiscussStats();
    const { data: popularTags } = usePopularTags(8);

    return (
        <div className="page">
            <div className="container">
                {/* Hero */}
                <div className="hero-head">
                    <div className="hh-left fade-up">
                        <span className="eyebrow">
                            Сообщество<span className="dot">·</span>
                            <span>{stats?.totalThreads ?? "—"} тем</span>
                        </span>
                        <h1 className="h1 hh-title">
                            Обсуждения<span className="grad-text">.</span>
                        </h1>
                        <p className="lead">
                            Задавай вопросы, делись скриптами и разбирай звонки вместе с другими
                            продавцами. Лучшие ответы поднимаются вверх.
                        </p>
                    </div>
                    <div className="hero-stats fade-up">
                        <StatTile label="Тем" value={String(stats?.totalThreads ?? "—")} icon={<Icon name="forum" size="xs" />} tone="primary" />
                        <StatTile label="Ответов" value={String(stats?.totalReplies ?? "—")} icon={<Icon name="message" size="xs" />} tone="violet" />
                    </div>
                </div>

                {/* Tools: search + new topic */}
                <div className="dsc-tools">
                    <form
                        className="field-wrap has-ic grow"
                        style={{ maxWidth: 420 }}
                        onSubmit={(event) => {
                            event.preventDefault();
                            setSearch(searchInput.trim());
                        }}
                    >
                        <Icon name="search" className="lead-ic" size={18} />
                        <input
                            className="field"
                            placeholder="Поиск по обсуждениям…"
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                        />
                    </form>
                    <Button variant="primary" iconLeft="edit" onClick={() => setShowNewThread(true)}>
                        Новая тема
                    </Button>
                </div>

                {/* Sort + tag filter */}
                <div className="dsc-bar">
                    <div className="seg">
                        {SORTS.map((option) => (
                            <button
                                key={option.id}
                                className={`seg-btn${sort === option.id ? " active" : ""}`}
                                onClick={() => setSort(option.id)}
                            >
                                <Icon name={option.icon} size={16} />{option.label}
                            </button>
                        ))}
                    </div>
                    <div className="row gap-2 wrap dsc-tags">
                        <button
                            className={`chip${activeTag === null ? " solid" : ""}`}
                            onClick={() => setActiveTag(null)}
                        >
                            Все
                        </button>
                        {(popularTags ?? []).map((tag) => (
                            <button
                                key={tag.slug}
                                className={`chip${activeTag === tag.slug ? " solid" : ""}`}
                                onClick={() => setActiveTag(tag.slug)}
                            >
                                {tag.name}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Main grid */}
                <div className="dsc-grid">
                    <div className="col" style={{ gap: 16 }}>
                        {isLoading && [1, 2, 3].map((i) => <Skeleton key={i} height={130} rounded={16} />)}
                        {error && (
                            <ErrorState title="Ошибка загрузки" message={error.message} onRetry={() => refetch()} />
                        )}
                        {page && page.items.length === 0 && (
                            <div className="empty" style={{ paddingTop: 60 }}>
                                <div className="ic"><Icon name="forum" size="lg" /></div>
                                <h2 className="h4" style={{ marginBottom: 8 }}>Пока нет обсуждений</h2>
                                <p className="small">Создайте первую тему — нажмите «Новая тема».</p>
                            </div>
                        )}
                        {page?.items.map((thread) => <ThreadCard key={thread.id} thread={thread} />)}
                    </div>

                    {/* Sidebar */}
                    <aside className="col" style={{ gap: 18 }}>
                        <div className="card card-pad">
                            <span className="eyebrow muted">Популярные теги</span>
                            <div className="row gap-2 wrap" style={{ marginTop: 12 }}>
                                {(popularTags ?? []).map((tag) => (
                                    <button
                                        key={tag.slug}
                                        className="chip"
                                        onClick={() => setActiveTag(tag.slug)}
                                    >
                                        {tag.name}
                                        <b style={{ color: "var(--ink-4)", fontWeight: 700, marginLeft: 6 }}>
                                            {tag.threadCount}
                                        </b>
                                    </button>
                                ))}
                                {(!popularTags || popularTags.length === 0) && (
                                    <span className="small" style={{ color: "var(--ink-3)" }}>Пока нет тегов</span>
                                )}
                            </div>
                        </div>

                        <div className="card card-pad">
                            <span className="eyebrow muted">Топ авторы недели</span>
                            <div className="col" style={{ marginTop: 8 }}>
                                {(stats?.topAuthorsOfWeek ?? []).map((author, index) => (
                                    <div key={author.authorId} className="dsc-author">
                                        <span className={`rank-badge r${index + 1}`}>{index + 1}</span>
                                        <GeoAvatar seed={author.authorName || author.authorId} size={32} />
                                        <span className="grow" style={{ fontWeight: 600, fontSize: 14 }}>
                                            {author.authorName || "Аноним"}
                                        </span>
                                        <span className="row gap-1 small" style={{ color: "var(--primary)", fontWeight: 700 }}>
                                            <Icon name="bolt" size={14} />{author.upvotesReceived.toLocaleString("ru")}
                                        </span>
                                    </div>
                                ))}
                                {(!stats || stats.topAuthorsOfWeek.length === 0) && (
                                    <span className="small" style={{ color: "var(--ink-3)" }}>Пока нет данных</span>
                                )}
                            </div>
                        </div>
                    </aside>
                </div>

                <div style={{ height: 60 }} />
            </div>

            {showNewThread && <NewThreadModal onClose={() => setShowNewThread(false)} />}
        </div>
    );
}
